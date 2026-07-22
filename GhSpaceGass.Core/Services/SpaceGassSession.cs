using GhSpaceGass.Core.Models;
using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Services;

/// <summary>
///     Singleton session managing the SpaceGass API service lifecycle and client.
///     All Grasshopper components access SpaceGass through this instance.
/// </summary>
public class SpaceGassSession : IDisposable
{
    public const string DefaultInstallPath = @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe";
    public const int DefaultPort = 34560;
    public static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan InitialStartupDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan AnalysisPollInterval = TimeSpan.FromMilliseconds(500);
    private readonly ISpaceGassApiFactory _apiFactory;
    private readonly string _installPath;

    private const double DoubleEqTolerance = 1e-6;

    private readonly int _port;
    private readonly IProcessManager _processManager;
    private readonly TimeSpan _startupTimeout;

    private ISpaceGassApi? _api;
    private int _disposed;
    private bool _weOwnProcess;

    /// <summary>
    ///     Production constructor.
    /// </summary>
    public SpaceGassSession(
        int port = DefaultPort,
        string installPath = DefaultInstallPath,
        TimeSpan? startupTimeout = null)
        : this(port, installPath, startupTimeout ?? DefaultStartupTimeout,
            new SystemProcessManager(),
            new SpaceGassApiClientFactory())
    {
    }

    /// <summary>
    ///     Test constructor — accepts injected dependencies.
    /// </summary>
    internal SpaceGassSession(
        int port,
        string installPath,
        TimeSpan startupTimeout,
        IProcessManager processManager,
        ISpaceGassApiFactory apiFactory)
    {
        _port = port;
        _installPath = installPath;
        _startupTimeout = startupTimeout;
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _apiFactory = apiFactory ?? throw new ArgumentNullException(nameof(apiFactory));
    }

    /// <summary>
    ///     True after a successful <see cref="ConnectAsync" /> call and before <see cref="Dispose" />.
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    ///     The port this session is configured to use.
    /// </summary>
    public int Port => _port;

    /// <summary>
    ///     The base URL of the connected SpaceGass API service (e.g. "http://localhost:34560").
    /// </summary>
    public string ServiceUrl => BaseUrl;

    /// <summary>
    ///     The SpaceGass version reported by the connected service. Populated after successful connect.
    /// </summary>
    public string SpaceGassVersion { get; private set; } = string.Empty;

    private string BaseUrl => $"http://localhost:{_port}";

    /// <summary>
    ///     Cleans up: disposes the API client and kills the service process if we launched it (ADR-0007).
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _api?.Dispose();

        if (_weOwnProcess)
            _processManager.Kill();

        IsConnected = false;
    }

    /// <summary>
    ///     Connects to the SpaceGass API service. If the service is not already running,
    ///     launches SpaceGassApi.exe and waits for it to become responsive.
    ///     Uses client.Service.Info.GetAsync() as the health probe (matching official docs).
    /// </summary>
    public async Task ConnectAsync(bool showConsole = false, CancellationToken ct = default)
    {
        if (_disposed != 0) throw new ObjectDisposedException(nameof(SpaceGassSession));

        // Create the API client first — its probe method knows the correct URL/path.
        // Dispose any previous client to prevent leaking HttpClient/sockets on retry.
        _api?.Dispose();
        _api = _apiFactory.Create(BaseUrl);

        // Step 1: Probe — is the service already running? (ADR-0004)
        // Use a short timeout so we don't wait long if nothing is listening.
        var alreadyRunning = await IsServiceReadyAsync(ProbeTimeout, ct).ConfigureAwait(false);

        if (alreadyRunning)
        {
            _weOwnProcess = false;
        }
        else
        {
            // Step 2: Validate install path exists
            if (!File.Exists(_installPath))
                throw new FileNotFoundException(
                    $"SpaceGassApi.exe not found at {_installPath}", _installPath);

            // Step 3: Launch the process
            _processManager.Launch(_installPath, $"--urls http://localhost:{_port}", showConsole);
            _weOwnProcess = true;

            // Step 4: Wait for the service to start — give it a moment before first probe
            await Task.Delay(InitialStartupDelay, ct).ConfigureAwait(false);

            // Step 5: Poll until healthy or timeout
            var deadline = DateTime.UtcNow + _startupTimeout;
            var healthy = false;
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                healthy = await IsServiceReadyAsync(ProbeTimeout, ct).ConfigureAwait(false);
                if (healthy) break;
                await Task.Delay(PollInterval, ct).ConfigureAwait(false);
            }

            if (!healthy)
                throw new TimeoutException(
                    $"SpaceGass API service did not start within {_startupTimeout.TotalSeconds}s. " +
                    "Check the install path, or if the service is already running on a different port.");
        }

        IsConnected = true;

        // Capture version info from the service
        try
        {
            var info = await _api!.GetServiceInfoAsync(ct).ConfigureAwait(false);
            SpaceGassVersion = info.SpaceGassVersion ?? string.Empty;
        }
        catch
        {
            // Non-fatal — version is informational only
            SpaceGassVersion = string.Empty;
        }
    }

    /// <summary>
    ///     Probes the service using client.Service.Info.GetAsync() — matches the official documentation pattern.
    ///     Uses a timeout to avoid long waits when nothing is listening on the port.
    /// </summary>
    private async Task<bool> IsServiceReadyAsync(TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            await _api!.GetServiceInfoAsync(linkedCts.Token).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Checks if the connected service is still responsive (quick health probe).
    ///     Returns false if the service has crashed or become unresponsive.
    /// </summary>
    public async Task<bool> IsServiceResponsiveAsync(CancellationToken ct = default)
    {
        if (!IsConnected || _api == null) return false;
        return await IsServiceReadyAsync(ProbeTimeout, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Closes the currently open job in SpaceGass.
    ///     Safe to call when no job is open — the API treats it as a no-op.
    /// </summary>
    public async Task CloseJobAsync(CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        await _api!.CloseJobAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Checks whether the service currently has a job open.
    ///     Returns true if a job is open, false otherwise.
    /// </summary>
    public async Task<bool> IsJobOpenAsync(CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        try
        {
            var status = await _api!.GetJobStatusAsync(ct).ConfigureAwait(false);
            return status.State?.IsOpen ?? false;
        }
        catch (Exception)
        {
            // API errors (network, 404, deserialization) treated as "no job open"
            return false;
        }
    }

    /// <summary>
    ///     Creates a new empty job in SpaceGass.
    /// </summary>
    public async Task<JobStatus> NewJobAsync(CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        return await _api!.NewJobAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Opens an existing job file in SpaceGass.
    /// </summary>
    public async Task<JobStatus> OpenJobAsync(string filePath, JobForceAccessOption? forceOption = null, CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        return await _api!.OpenJobAsync(filePath, forceOption, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Saves the current job to the specified file path.
    /// </summary>
    public async Task SaveJobAsync(string filePath, CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        await _api!.SaveJobAsync(filePath, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Saves the current job and returns the full job info summary.
    ///     If filePath is null/empty, resolves the current file path from the job state.
    ///     Throws if no file path is associated with the job and none is provided.
    /// </summary>
    public async Task<SgJobInfo> SaveAndGetInfoAsync(string? filePath, CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        ct.ThrowIfCancellationRequested();

        // Resolve file path if not provided
        var savePath = filePath;
        if (string.IsNullOrWhiteSpace(savePath))
        {
            JobStatus currentStatus;
            try
            {
                currentStatus = await _api!.GetJobStatusAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    ModelAssembler.FormatApiError(ex, "querying current job status"), ex);
            }

            savePath = currentStatus.State?.File?.Path;
            if (string.IsNullOrWhiteSpace(savePath))
                throw new InvalidOperationException(
                    "No file path associated with the current job. Provide a file path to save to.");
        }

        // Save
        try
        {
            await _api!.SaveJobAsync(savePath, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "saving job"), ex);
        }

        // Query full status after save for the summary
        return await GetJobInfoAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Assembles a structural model from in-memory member and restraint data and pushes it to SpaceGass.
    ///     Clears existing model data first (ADR-0001 — Clear &amp; Rebuild).
    /// </summary>
    public async Task<AssemblyResult> AssembleModelAsync(
        IReadOnlyList<SgMemberData> members,
        double tolerance,
        IReadOnlyList<SgRestraintData>? restraints = null,
        IReadOnlyList<SgNodeLoadData>? nodeLoads = null,
        IReadOnlyList<SgMemberDistributedLoadData>? memberDistributedLoads = null,
        IReadOnlyList<SgSelfWeightLoadData>? selfWeightLoads = null,
        IReadOnlyList<SgCombinationLoadCaseData>? combinationLoadCases = null,
        IReadOnlyList<SgLumpedMassLoadData>? lumpedMassLoads = null,
        IReadOnlyList<SgPrescribedDisplacementData>? prescribedDisplacements = null,
        IReadOnlyList<SgMemberConcentratedLoadData>? memberConcentratedLoads = null,
        IReadOnlyList<SgMemberPrestressLoadData>? memberPrestressLoads = null,
        IReadOnlyList<SgNodeConstraintData>? nodeConstraints = null,
        IReadOnlyList<SgPlateData>? plates = null,
        IReadOnlyList<SgPlatePressureLoadData>? platePressureLoads = null,
        IReadOnlyList<SgThermalLoadData>? thermalLoads = null,
        IReadOnlyList<SgMovingLoadScenarioData>? movingLoadScenarios = null,
        bool appendMode = false,
        CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var assembler = new ModelAssembler();
        return await assembler.AssembleAsync(_api!, members, tolerance, restraints, nodeLoads,
            memberDistributedLoads, selfWeightLoads, combinationLoadCases,
            lumpedMassLoads, prescribedDisplacements, memberConcentratedLoads,
            memberPrestressLoads, nodeConstraints, plates, platePressureLoads,
            thermalLoads, movingLoadScenarios,
            appendMode, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Generates the moving-load load cases for each subgroup in the input branch trees.
    ///     For each branch, PATCHes the ElementsToLoad selection, toggles the Include flag on
    ///     the scenarios that belong to this branch (and disables the others), then POSTs
    ///     Generate. Settings — when supplied — are PATCHed once at the start. All scenario
    ///     Include flags are snapshotted before the run and restored afterwards, even on
    ///     exception.
    /// </summary>
    /// <param name="model">The assembled model — MemberMap / PlateMap / MovingLoadScenarioMap must be populated.</param>
    /// <param name="membersToLoad">Branch path → resolved member IDs that receive moving-load distribution in that subgroup.</param>
    /// <param name="platesToLoad">Branch path → resolved plate IDs that receive moving-load distribution in that subgroup.</param>
    /// <param name="scenariosToApply">Branch path → scenario names to enable (Include=true) for that subgroup.</param>
    /// <param name="settings">Optional moving-load engine settings, PATCHed once at the start.</param>
    /// <param name="loadCategoryName">Optional load-category name — resolved via <see cref="SgModelData.LoadCategoryMap"/>.</param>
    public async Task<SgMovingLoadGenerationResult> GenerateMovingLoadsAsync(
        SgModelData model,
        IReadOnlyDictionary<string, IReadOnlyList<int>> membersToLoad,
        IReadOnlyDictionary<string, IReadOnlyList<int>> platesToLoad,
        IReadOnlyDictionary<string, IReadOnlyList<string>> scenariosToApply,
        SgMovingLoadSettingsData? settings = null,
        string? loadCategoryName = null,
        CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgMovingLoadGenerationResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Union of branch paths across the three input trees
        var branchPaths = membersToLoad.Keys
            .Concat(platesToLoad.Keys)
            .Concat(scenariosToApply.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        if (branchPaths.Count == 0)
        {
            result.Warnings.Add("No subgroups supplied — nothing to generate.");
            result.ElapsedTime = stopwatch.Elapsed;
            return result;
        }

        // Resolve load category (optional)
        int? loadCategoryId = null;
        if (!string.IsNullOrEmpty(loadCategoryName))
        {
            if (model.LoadCategoryMap.TryGetValue(loadCategoryName!, out var catId))
                loadCategoryId = catId;
            else
                result.Warnings.Add(
                    $"Load category '{loadCategoryName}' not found in the model — the generated " +
                    "cases will not be tagged with a category.");
        }

        // Settings PATCH — once at the start
        if (settings is { HasAnyValue: true })
        {
            var update = new MovingLoadSettingsUpdate
            {
                ApplyToClosestMember = settings.ApplyToClosestMember,
                CheckVerticalProximity = settings.CheckVerticalProximity,
                VerticalProximity = settings.VerticalProximity,
                IgnoreLoadsOnOneMember = settings.IgnoreLoadsOnOneMember,
                IgnoreOutsideLoadedArea = settings.IgnoreOutsideLoadedArea,
                KeepLoadsWithinTravelPath = settings.KeepLoadsWithinTravelPath,
                RetainLoads = settings.RetainLoads
            };
            try
            {
                await _api!.PatchMovingLoadSettingsAsync(update, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    ModelAssembler.FormatApiError(ex, "updating moving load settings"), ex);
            }
        }

        // Collect scenario IDs that appear anywhere in the input tree — these are the ones
        // whose Include flag we'll toggle per branch and restore at the end. Also validate
        // that every named scenario exists in the model's scenario map.
        var scenarioNamesInTree = scenariosToApply.Values
            .SelectMany(names => names)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var scenarioNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in scenarioNamesInTree)
        {
            if (model.MovingLoadScenarioMap.TryGetValue(name, out var id))
                scenarioNameToId[name] = id;
            else
                result.Warnings.Add(
                    $"Moving load scenario '{name}' referenced by Scenarios To Apply was not " +
                    "found in the model — skipping.");
        }

        // Snapshot the Include flag of every scenario that will be touched.
        var includeSnapshot = new Dictionary<int, bool>();
        if (scenarioNameToId.Count > 0)
        {
            List<MovingLoadScenario> jobScenarios;
            try
            {
                jobScenarios = await _api!.ListMovingLoadScenariosAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    ModelAssembler.FormatApiError(ex, "listing moving load scenarios"), ex);
            }

            var toTouch = scenarioNameToId.Values.ToHashSet();
            foreach (var s in jobScenarios)
                if (s.Id.HasValue && toTouch.Contains(s.Id.Value))
                    includeSnapshot[s.Id.Value] = s.Include ?? true;
        }

        try
        {
            foreach (var path in branchPaths)
            {
                membersToLoad.TryGetValue(path, out var branchMemberIds);
                platesToLoad.TryGetValue(path, out var branchPlateIds);
                scenariosToApply.TryGetValue(path, out var branchScenarioNames);

                var memberSelection = ModelAssembler.FormatIdSelectionString(
                    branchMemberIds ?? Array.Empty<int>());
                var plateSelection = ModelAssembler.FormatIdSelectionString(
                    branchPlateIds ?? Array.Empty<int>());

                if (memberSelection.Length == 0 && plateSelection.Length == 0)
                {
                    result.Warnings.Add(
                        $"Subgroup '{path}' has no members or plates to load — skipping.");
                    continue;
                }

                // Collect branch's scenario IDs early so we can skip empty-scenarios branches
                // before touching any live job state — Generate would just produce nothing.
                var branchScenarioIds = new HashSet<int>();
                if (branchScenarioNames != null)
                    foreach (var name in branchScenarioNames)
                        if (scenarioNameToId.TryGetValue(name, out var sid))
                            branchScenarioIds.Add(sid);

                if (branchScenarioIds.Count == 0)
                {
                    result.Warnings.Add(
                        $"Subgroup '{path}' has no moving load scenarios to apply — skipping " +
                        "(Generate would produce no load cases for this subgroup).");
                    continue;
                }

                // PATCH ElementsToLoad
                var etlUpdate = new MovingLoadElementsToLoadUpdate
                {
                    Members = memberSelection.Length > 0 ? memberSelection : null,
                    Plates = plateSelection.Length > 0 ? plateSelection : null
                };
                try
                {
                    await _api!.PatchMovingLoadElementsToLoadAsync(etlUpdate, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        ModelAssembler.FormatApiError(ex,
                            $"updating moving load elements to load for subgroup '{path}'"), ex);
                }

                // Toggle scenario Include flags — true if in this branch's scenarios, false
                // otherwise. Only touch scenarios that appear somewhere in the input tree.
                // branchScenarioIds was populated earlier before the ElementsToLoad PATCH so
                // we can skip empty-scenarios branches without side effects.
                foreach (var (_, sid) in scenarioNameToId)
                {
                    var include = branchScenarioIds.Contains(sid);
                    try
                    {
                        await _api!.PatchMovingLoadScenarioAsync(sid,
                            new MovingLoadScenarioUpdate { Include = include }, ct)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            ModelAssembler.FormatApiError(ex,
                                $"toggling scenario Include for subgroup '{path}'"), ex);
                    }
                }

                // POST Generate
                MovingLoadGenerationResult apiResult;
                try
                {
                    apiResult = await _api!.GenerateMovingLoadsAsync(
                        new MovingLoadGenerateRequest { LoadCategory = loadCategoryId }, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        ModelAssembler.FormatApiError(ex,
                            $"generating moving loads for subgroup '{path}'"), ex);
                }

                var branch = new SgMovingLoadGenerationBranch(path);
                if (apiResult.GeneratedLoadCaseIds != null)
                    foreach (var id in apiResult.GeneratedLoadCaseIds)
                        if (id.HasValue) branch.LoadCaseIds.Add(id.Value);
                if (apiResult.GeneratedGroups != null)
                    branch.Groups.AddRange(apiResult.GeneratedGroups);
                result.Branches[path] = branch;
            }
        }
        finally
        {
            // Restore Include state on every scenario we snapshotted, in a best-effort loop.
            // Restoration errors do not overwrite the primary exception (if any) — they are
            // appended as warnings on the result.
            foreach (var (sid, originalInclude) in includeSnapshot)
            {
                try
                {
                    await _api!.PatchMovingLoadScenarioAsync(sid,
                        new MovingLoadScenarioUpdate { Include = originalInclude },
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    result.Warnings.Add(
                        $"Failed to restore Include flag on scenario {sid}: {ex.Message}");
                }
            }
        }

        result.ElapsedTime = stopwatch.Elapsed;
        return result;
    }

    /// <summary>
    ///     Reads an existing SpaceGass model from the open job and returns the structural data
    ///     with populated ID ↔ geometry mappings for downstream chaining to Analysis and Results.
    /// </summary>
    public async Task<SgDisassembledModel> DisassembleModelAsync(CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var model = new SgModelData();
        var result = new SgDisassembledModel(model);

        // ── Query nodes ──────────────────────────────────────────────
        List<Node> apiNodes;
        try
        {
            apiNodes = await _api!.ListNodesAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying nodes"), ex);
        }

        var nodeMap = new Dictionary<int, SgPoint3D>();
        foreach (var n in apiNodes)
        {
            if (n.Id == null) continue;
            var pt = new SgPoint3D(n.X ?? 0, n.Y ?? 0, n.Z ?? 0);
            nodeMap[n.Id.Value] = pt;
            model.NodeMap[pt] = n.Id.Value;
            result.Nodes.Add(new SgDisassembledNode(n.Id.Value, pt));
        }

        // ── Query sections ───────────────────────────────────────────
        List<Section> apiSections;
        try
        {
            apiSections = await _api!.ListSectionsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying sections"), ex);
        }

        foreach (var s in apiSections)
        {
            if (s.Id == null || s.Name == null) continue;
            var key = string.IsNullOrEmpty(s.Library) ? s.Name : $"{s.Library}::{s.Name}";
            model.SectionMap[key] = s.Id.Value;
        }

        // ── Query materials ──────────────────────────────────────────
        List<Material> apiMaterials;
        try
        {
            apiMaterials = await _api!.ListMaterialsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying materials"), ex);
        }

        foreach (var m in apiMaterials)
        {
            if (m.Id == null || m.Name == null) continue;
            var key = string.IsNullOrEmpty(m.Library) ? m.Name : $"{m.Library}::{m.Name}";
            model.MaterialMap[key] = m.Id.Value;
        }

        // ── Query members ────────────────────────────────────────────
        List<Member> apiMembers;
        try
        {
            apiMembers = await _api!.ListMembersAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying members"), ex);
        }

        foreach (var m in apiMembers)
        {
            if (m.Id == null || m.NodeA == null || m.NodeB == null) continue;
            if (!nodeMap.TryGetValue(m.NodeA.Value, out var startPt) ||
                !nodeMap.TryGetValue(m.NodeB.Value, out var endPt))
                continue;

            model.MemberMap[m.Id.Value] = (startPt, endPt);
            result.Members.Add(new SgDisassembledMember(
                m.Id.Value,
                startPt,
                endPt,
                m.Section ?? 0,
                m.Material ?? 0,
                MapMemberType(m.Type)));
        }

        // ── Query plates ─────────────────────────────────────────────
        List<Plate> apiPlates;
        try
        {
            apiPlates = await _api!.ListPlatesAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying plates"), ex);
        }

        foreach (var p in apiPlates)
        {
            if (p.Id == null || p.NodeA == null || p.NodeB == null || p.NodeC == null) continue;

            var corners = new List<SgPoint3D>();
            if (nodeMap.TryGetValue(p.NodeA.Value, out var ptA)) corners.Add(ptA);
            if (nodeMap.TryGetValue(p.NodeB.Value, out var ptB)) corners.Add(ptB);
            if (nodeMap.TryGetValue(p.NodeC.Value, out var ptC)) corners.Add(ptC);
            if (p.NodeD != null && nodeMap.TryGetValue(p.NodeD.Value, out var ptD)) corners.Add(ptD);

            if (corners.Count < 3) continue;

            var cornerArray = corners.ToArray();
            model.PlateMap[p.Id.Value] = cornerArray;
            result.Plates.Add(new SgDisassembledPlate(
                p.Id.Value,
                cornerArray,
                p.Material ?? 0));
        }

        // ── Query load cases ─────────────────────────────────────────
        List<LoadCase> apiLoadCases;
        try
        {
            apiLoadCases = await _api!.ListLoadCasesAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying load cases"), ex);
        }

        foreach (var lc in apiLoadCases)
        {
            if (lc.Id == null || lc.Title == null) continue;
            if (lc.Type == LoadCaseType.Combination)
                model.CombinationLoadCaseMap[lc.Title] = lc.Id.Value;
            else if (lc.Type == LoadCaseType.Primary)
                model.LoadCaseMap[lc.Title] = lc.Id.Value;
        }

        // ── Empty check ──────────────────────────────────────────────
        if (result.Nodes.Count == 0)
            result.Warnings.Add("No structure found in the open job.");

        return result;
    }

    private static string MapMemberType(MemberType? type)
    {
        return type switch
        {
            MemberType.Normal => "Beam",
            MemberType.Truss => "Truss",
            MemberType.Cable => "Cable",
            MemberType.CompressionOnly => "Compression Only",
            MemberType.TensionOnly => "Tension Only",
            MemberType.Gap => "Gap",
            MemberType.BrittleFuse => "Brittle Fuse",
            MemberType.PlasticFuse => "Plastic Fuse",
            MemberType.Pulley => "Pulley",
            _ => "Unknown"
        };
    }

    /// <summary>
    ///     Queries all section properties from the open SpaceGass job.
    /// </summary>
    public async Task<SgSectionPropertiesResult> GetSectionPropertiesAsync(CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgSectionPropertiesResult();

        List<Section> apiSections;
        try
        {
            apiSections = await _api!.ListSectionsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying section properties"), ex);
        }

        foreach (var s in apiSections)
        {
            if (s.Id == null) continue;
            result.Sections.Add(new SgSectionPropertyData(
                s.Id.Value,
                s.Name ?? "",
                s.Library ?? "",
                MapPropertySource(s.Source),
                s.A ?? 0,
                s.Iy ?? 0,
                s.Iz ?? 0,
                s.J ?? 0,
                s.Ay ?? 0,
                s.Az ?? 0,
                s.PrincipalAngle ?? 0,
                s.Mark ?? "",
                s.AreaFactor ?? 0,
                s.IyFactor ?? 0,
                s.IzFactor ?? 0,
                s.TorsionFactor ?? 0,
                s.Transposed ?? false,
                MapAngleType(s.AngleType)));
        }

        if (result.Sections.Count == 0)
            result.Warnings.Add("No sections found in the open job.");

        return result;
    }

    /// <summary>
    ///     Queries all material properties from the open SpaceGass job.
    /// </summary>
    public async Task<SgMaterialPropertiesResult> GetMaterialPropertiesAsync(CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgMaterialPropertiesResult();

        List<Material> apiMaterials;
        try
        {
            apiMaterials = await _api!.ListMaterialsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying material properties"), ex);
        }

        foreach (var m in apiMaterials)
        {
            if (m.Id == null) continue;
            result.Materials.Add(new SgMaterialPropertyData(
                m.Id.Value,
                m.Name ?? "",
                m.Library ?? "",
                MapPropertySource(m.Source),
                m.YoungsModulus ?? 0,
                m.PoissonsRatio ?? 0,
                m.MassDensity ?? 0,
                m.ThermalCoeff ?? 0,
                m.ConcreteStrength ?? 0));
        }

        if (result.Materials.Count == 0)
            result.Warnings.Add("No materials found in the open job.");

        return result;
    }

    private static string MapPropertySource(PropertySource? source)
    {
        return source switch
        {
            PropertySource.Library => "Library",
            PropertySource.User => "User",
            _ => "Unknown"
        };
    }

    private static string MapAngleType(AngleType? angleType)
    {
        return angleType switch
        {
            AngleType.NotApplicable => "Not Applicable",
            AngleType.SingleType => "Single",
            AngleType.ShortShort => "Short-Short",
            AngleType.LongLong => "Long-Long",
            AngleType.Starred => "Starred",
            _ => "Not Applicable"
        };
    }

    /// <summary>
    ///     Queries all load cases, load categories, and load case groups from the open job.
    /// </summary>
    public async Task<SgLoadCaseDataResult> GetLoadCaseDataAsync(CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgLoadCaseDataResult();

        // ── Query load cases ─────────────────────────────────────────
        List<LoadCase> apiLoadCases;
        try
        {
            apiLoadCases = await _api!.ListLoadCasesAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying load cases"), ex);
        }

        // Build ID → name lookup for combination item resolution
        var idToName = new Dictionary<int, string>();
        foreach (var lc in apiLoadCases)
            if (lc.Id != null && lc.Title != null)
                idToName[lc.Id.Value] = lc.Title;

        foreach (var lc in apiLoadCases)
        {
            if (lc.Id == null) continue;

            var combinationItems = new List<string>();
            if (lc.HasCombinationItems == true && lc.CombinationItems != null)
                foreach (var item in lc.CombinationItems)
                {
                    if (item.LoadCase == null) continue;
                    var factor = item.MultiplyingFactor ?? 1;
                    
                    var factorStr = Math.Abs(factor - (int)factor) < DoubleEqTolerance
                        ? ((int)factor).ToString()
                        : factor.ToString("0.####");
                    var name = idToName.TryGetValue(item.LoadCase.Value, out var n)
                        ? n
                        : $"LC{item.LoadCase.Value}";
                    combinationItems.Add($"{factorStr}×{name}");
                }

            result.LoadCases.Add(new SgLoadCaseInfo(
                lc.Id.Value,
                lc.Title ?? "",
                MapLoadCaseType(lc.Type),
                lc.Notes ?? "",
                combinationItems));
        }

        // ── Query load categories ────────────────────────────────────
        List<LoadCategory> apiCategories;
        try
        {
            apiCategories = await _api!.ListLoadCategoriesAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying load categories"), ex);
        }

        foreach (var cat in apiCategories)
        {
            if (cat.Id == null) continue;
            result.Categories.Add(new SgLoadCategoryInfo(
                cat.Id.Value,
                cat.Title ?? "",
                cat.Notes ?? ""));
        }

        // ── Query load case groups ───────────────────────────────────
        List<LoadCaseGroup> apiGroups;
        try
        {
            apiGroups = await _api!.ListLoadCaseGroupsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying load case groups"), ex);
        }

        foreach (var grp in apiGroups)
        {
            if (grp.Id == null) continue;
            result.Groups.Add(new SgLoadCaseGroupInfo(
                grp.Id.Value,
                grp.Title ?? "",
                grp.LoadCaseList ?? ""));
        }

        if (result.LoadCases.Count == 0)
            result.Warnings.Add("No load cases found in the open job.");

        return result;
    }

    private static string MapLoadCaseType(LoadCaseType? type)
    {
        return type switch
        {
            LoadCaseType.Primary => "Primary",
            LoadCaseType.Combination => "Combination",
            LoadCaseType.Step => "Step",
            LoadCaseType.Unused => "Unused",
            _ => "Unknown"
        };
    }

    /// <summary>
    ///     Queries all self-weight loads from the open job.
    /// </summary>
    public async Task<SgSelfWeightLoadsDataResult> GetSelfWeightLoadsDataAsync(
        SgModelData model, CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgSelfWeightLoadsDataResult();

        List<SelfWeightLoad> apiLoads;
        try
        {
            apiLoads = await _api!.ListSelfWeightLoadsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying self-weight loads"), ex);
        }

        var idToName = BuildLoadCaseIdToNameMap(model);
        foreach (var sw in apiLoads)
        {
            if (sw.LoadCase == null) continue;
            result.Loads.Add(new SgSelfWeightLoadInfo(
                sw.LoadCase.Value,
                ResolveLoadCaseName(idToName, sw.LoadCase.Value),
                sw.LoadCategory ?? 0,
                sw.AccelerationX ?? 0,
                sw.AccelerationY ?? 0,
                sw.AccelerationZ ?? 0));
        }

        if (result.Loads.Count == 0)
            result.Warnings.Add("No self-weight loads found in the open job.");

        return result;
    }

    /// <summary>
    ///     Resolves a load case ID to its name using a pre-built reverse lookup.
    /// </summary>
    private static string ResolveLoadCaseName(Dictionary<int, string> idToName, int loadCaseId)
    {
        return idToName.TryGetValue(loadCaseId, out var name) ? name : $"LC{loadCaseId}";
    }

    /// <summary>
    ///     Builds a reverse lookup dictionary (ID → name) from the model's load case maps.
    /// </summary>
    private static Dictionary<int, string> BuildLoadCaseIdToNameMap(SgModelData model)
    {
        var map = new Dictionary<int, string>();
        foreach (var kvp in model.LoadCaseMap)
            map[kvp.Value] = kvp.Key;
        foreach (var kvp in model.CombinationLoadCaseMap)
            map[kvp.Value] = kvp.Key;
        return map;
    }

    /// <summary>
    ///     Queries all node-based loads (node loads, lumped mass, prescribed displacements)
    ///     from the open job, grouped by node.
    /// </summary>
    public async Task<SgNodeLoadsDataResult> GetNodeLoadsDataAsync(
        SgModelData model, CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgNodeLoadsDataResult();
        var lcMap = BuildLoadCaseIdToNameMap(model);
        var nodeIdToPoint = BuildNodeIdToPointMap(model);
        var entries = new SortedDictionary<int, SgNodeLoadEntry>();

        SgNodeLoadEntry GetOrCreateEntry(int nodeId)
        {
            if (entries.TryGetValue(nodeId, out var existing))
                return existing;
            if (!nodeIdToPoint.TryGetValue(nodeId, out var point))
                throw new InvalidOperationException(
                    $"Node {nodeId} not found in model — ensure caller checks before calling GetOrCreateEntry.");
            var entry = new SgNodeLoadEntry(nodeId, point);
            entries[nodeId] = entry;
            return entry;
        }

        var unresolvedNodes = new HashSet<int>();

        // ── Node loads ───────────────────────────────────────────────
        List<NodeLoad> apiNodeLoads;
        try
        {
            apiNodeLoads = await _api!.ListNodeLoadsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying node loads"), ex);
        }

        foreach (var nl in apiNodeLoads)
        {
            if (nl.Node == null || nl.LoadCase == null) continue;
            if (!nodeIdToPoint.ContainsKey(nl.Node.Value))
            {
                unresolvedNodes.Add(nl.Node.Value);
                continue;
            }

            GetOrCreateEntry(nl.Node.Value).NodeLoads.Add(new SgNodeLoadInfo(
                nl.LoadCase.Value,
                ResolveLoadCaseName(lcMap, nl.LoadCase.Value),
                nl.LoadCategory ?? 0,
                nl.Fx ?? 0, nl.Fy ?? 0, nl.Fz ?? 0,
                nl.Mx ?? 0, nl.My ?? 0, nl.Mz ?? 0));
        }

        // ── Lumped mass loads ────────────────────────────────────────
        List<LumpedMassLoad> apiLumpedMass;
        try
        {
            apiLumpedMass = await _api!.ListLumpedMassLoadsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying lumped mass loads"), ex);
        }

        foreach (var lm in apiLumpedMass)
        {
            if (lm.Node == null || lm.LoadCase == null) continue;
            if (!nodeIdToPoint.ContainsKey(lm.Node.Value))
            {
                unresolvedNodes.Add(lm.Node.Value);
                continue;
            }

            GetOrCreateEntry(lm.Node.Value).LumpedMassLoads.Add(new SgLumpedMassLoadInfo(
                lm.LoadCase.Value,
                ResolveLoadCaseName(lcMap, lm.LoadCase.Value),
                lm.LoadCategory ?? 0,
                lm.Tmx ?? 0, lm.Tmy ?? 0, lm.Tmz ?? 0,
                lm.Rmx ?? 0, lm.Rmy ?? 0, lm.Rmz ?? 0));
        }

        // ── Prescribed displacements ─────────────────────────────────
        List<PrescribedDisplacement> apiPrescribed;
        try
        {
            apiPrescribed = await _api!.ListPrescribedDisplacementsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying prescribed displacements"), ex);
        }

        foreach (var pd in apiPrescribed)
        {
            if (pd.Node == null || pd.LoadCase == null) continue;
            if (!nodeIdToPoint.ContainsKey(pd.Node.Value))
            {
                unresolvedNodes.Add(pd.Node.Value);
                continue;
            }

            GetOrCreateEntry(pd.Node.Value).PrescribedDisplacements.Add(new SgPrescribedDisplacementInfo(
                pd.LoadCase.Value,
                ResolveLoadCaseName(lcMap, pd.LoadCase.Value),
                pd.LoadCategory ?? 0,
                pd.Tx ?? 0, pd.Ty ?? 0, pd.Tz ?? 0,
                pd.Rx ?? 0, pd.Ry ?? 0, pd.Rz ?? 0));
        }

        // ── Collect results ──────────────────────────────────────────
        result.NodeEntries.AddRange(entries.Values);

        foreach (var nodeId in unresolvedNodes)
            result.Warnings.Add($"Node {nodeId} not found in model — loads skipped.");

        if (result.NodeEntries.Count == 0)
            result.Warnings.Add("No node loads found in the open job.");

        return result;
    }

    /// <summary>
    ///     Builds a reverse lookup dictionary (ID → Point) from the model's NodeMap.
    /// </summary>
    private static Dictionary<int, SgPoint3D> BuildNodeIdToPointMap(SgModelData model)
    {
        var map = new Dictionary<int, SgPoint3D>();
        foreach (var kvp in model.NodeMap)
            map[kvp.Value] = kvp.Key;
        return map;
    }

    /// <summary>
    ///     Queries all member-based loads (concentrated, distributed, distributed moments,
    ///     prestress, and member thermal) from the open job, grouped by member.
    /// </summary>
    public async Task<SgMemberLoadsDataResult> GetMemberLoadsDataAsync(
        SgModelData model, CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgMemberLoadsDataResult();
        var lcMap = BuildLoadCaseIdToNameMap(model);
        var entries = new SortedDictionary<int, SgMemberLoadEntry>();
        var unresolvedMembers = new HashSet<int>();

        SgMemberLoadEntry GetOrCreateEntry(int memberId)
        {
            if (entries.TryGetValue(memberId, out var existing))
                return existing;
            if (!model.MemberMap.TryGetValue(memberId, out var geom))
                throw new InvalidOperationException(
                    $"Member {memberId} not found in model — ensure caller checks before calling GetOrCreateEntry.");
            var entry = new SgMemberLoadEntry(memberId, geom.Start, geom.End);
            entries[memberId] = entry;
            return entry;
        }

        bool MemberExists(int memberId)
        {
            if (model.MemberMap.ContainsKey(memberId)) return true;
            unresolvedMembers.Add(memberId);
            return false;
        }

        // ── Concentrated loads ───────────────────────────────────────
        List<MemberConcentratedLoad> apiConc;
        try
        {
            apiConc = await _api!.ListMemberConcentratedLoadsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying member concentrated loads"), ex);
        }

        foreach (var cl in apiConc)
        {
            if (cl.Member == null || cl.LoadCase == null) continue;
            if (!MemberExists(cl.Member.Value)) continue;
            GetOrCreateEntry(cl.Member.Value).ConcentratedLoads.Add(new SgConcentratedLoadInfo(
                cl.LoadCase.Value, ResolveLoadCaseName(lcMap, cl.LoadCase.Value), cl.LoadCategory ?? 0,
                cl.Fx ?? 0, cl.Fy ?? 0, cl.Fz ?? 0,
                cl.Mx ?? 0, cl.My ?? 0, cl.Mz ?? 0,
                cl.Position ?? 0, MapPositionUnits(cl.PositionUnits), MapLoadAxes(cl.Axes)));
        }

        // ── Distributed loads ────────────────────────────────────────
        List<MemberDistributedLoad> apiDist;
        try
        {
            apiDist = await _api!.ListMemberDistributedLoadsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying member distributed loads"), ex);
        }

        foreach (var dl in apiDist)
        {
            if (dl.Member == null || dl.LoadCase == null) continue;
            if (!MemberExists(dl.Member.Value)) continue;
            GetOrCreateEntry(dl.Member.Value).DistributedLoads.Add(new SgDistributedLoadInfo(
                dl.LoadCase.Value, ResolveLoadCaseName(lcMap, dl.LoadCase.Value), dl.LoadCategory ?? 0,
                dl.FxStart ?? 0, dl.FyStart ?? 0, dl.FzStart ?? 0,
                dl.FxFinish ?? 0, dl.FyFinish ?? 0, dl.FzFinish ?? 0,
                dl.StartPosition ?? 0, dl.FinishPosition ?? 0,
                MapPositionUnits(dl.PositionUnits), MapLoadAxes(dl.Axes)));
        }

        // ── Distributed moments ──────────────────────────────────────
        List<MemberDistributedMoment> apiMom;
        try
        {
            apiMom = await _api!.ListMemberDistributedMomentsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying member distributed moments"), ex);
        }

        foreach (var dm in apiMom)
        {
            if (dm.Member == null || dm.LoadCase == null) continue;
            if (!MemberExists(dm.Member.Value)) continue;
            GetOrCreateEntry(dm.Member.Value).DistributedMoments.Add(new SgDistributedMomentInfo(
                dm.LoadCase.Value, ResolveLoadCaseName(lcMap, dm.LoadCase.Value), dm.LoadCategory ?? 0,
                dm.MxStart ?? 0, dm.MyStart ?? 0, dm.MzStart ?? 0,
                dm.MxFinish ?? 0, dm.MyFinish ?? 0, dm.MzFinish ?? 0,
                dm.StartPosition ?? 0, dm.FinishPosition ?? 0,
                MapPositionUnits(dm.PositionUnits), MapLoadAxes(dm.Axes)));
        }

        // ── Prestress loads ──────────────────────────────────────────
        List<MemberPrestressLoad> apiPre;
        try
        {
            apiPre = await _api!.ListMemberPrestressLoadsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying member prestress loads"), ex);
        }

        foreach (var pl in apiPre)
        {
            if (pl.Member == null || pl.LoadCase == null) continue;
            if (!MemberExists(pl.Member.Value)) continue;
            GetOrCreateEntry(pl.Member.Value).PrestressLoads.Add(new SgPrestressLoadInfo(
                pl.LoadCase.Value, ResolveLoadCaseName(lcMap, pl.LoadCase.Value), pl.LoadCategory ?? 0,
                pl.Prestress ?? 0));
        }

        // ── Thermal loads (member only) ──────────────────────────────
        List<ThermalLoad> apiTherm;
        try
        {
            apiTherm = await _api!.ListThermalLoadsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying thermal loads"), ex);
        }

        foreach (var tl in apiTherm)
        {
            if (tl.ElementType != ThermalElementType.Member) continue;
            if (tl.ElementId == null || tl.LoadCase == null) continue;
            if (!MemberExists(tl.ElementId.Value)) continue;
            GetOrCreateEntry(tl.ElementId.Value).ThermalLoads.Add(new SgMemberThermalLoadInfo(
                tl.LoadCase.Value, ResolveLoadCaseName(lcMap, tl.LoadCase.Value), tl.LoadCategory ?? 0,
                tl.ThermalLoadProp ?? 0, tl.YThermalGradient ?? 0, tl.ZThermalGradient ?? 0));
        }

        result.MemberEntries.AddRange(entries.Values);

        foreach (var memberId in unresolvedMembers)
            result.Warnings.Add($"Member {memberId} not found in model — loads skipped.");

        if (result.MemberEntries.Count == 0)
            result.Warnings.Add("No member loads found in the open job.");

        return result;
    }

    private static string MapPositionUnits(LoadPositionUnits? units)
    {
        return units switch
        {
            LoadPositionUnits.Actual => "Actual",
            _ => "Percent"
        };
    }

    private static string MapLoadAxes(LoadAxes? axes)
    {
        return axes switch
        {
            LoadAxes.Local => "Local",
            LoadAxes.GlobalInclined => "Global Inclined",
            LoadAxes.GlobalProjected => "Global Projected",
            _ => "Local"
        };
    }

    /// <summary>
    ///     Queries all plate-based loads (pressure and plate thermal) from the open job, grouped by plate.
    /// </summary>
    public async Task<SgPlateLoadsDataResult> GetPlateLoadsDataAsync(
        SgModelData model, CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgPlateLoadsDataResult();
        var lcMap = BuildLoadCaseIdToNameMap(model);
        var entries = new SortedDictionary<int, SgPlateLoadEntry>();
        var unresolvedPlates = new HashSet<int>();

        SgPlateLoadEntry GetOrCreateEntry(int plateId)
        {
            if (entries.TryGetValue(plateId, out var existing))
                return existing;
            if (!model.PlateMap.TryGetValue(plateId, out var corners))
                throw new InvalidOperationException(
                    $"Plate {plateId} not found in model — ensure caller checks before calling GetOrCreateEntry.");
            var entry = new SgPlateLoadEntry(plateId, corners);
            entries[plateId] = entry;
            return entry;
        }

        bool PlateExists(int plateId)
        {
            if (model.PlateMap.ContainsKey(plateId)) return true;
            unresolvedPlates.Add(plateId);
            return false;
        }

        // ── Pressure loads ───────────────────────────────────────────
        List<PlatePressureLoad> apiPressure;
        try
        {
            apiPressure = await _api!.ListPlatePressureLoadsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying plate pressure loads"), ex);
        }

        foreach (var pp in apiPressure)
        {
            if (pp.Plate == null || pp.LoadCase == null) continue;
            if (!PlateExists(pp.Plate.Value)) continue;
            GetOrCreateEntry(pp.Plate.Value).PressureLoads.Add(new SgPlatePressureLoadInfo(
                pp.LoadCase.Value, ResolveLoadCaseName(lcMap, pp.LoadCase.Value), pp.LoadCategory ?? 0,
                pp.Px ?? 0, pp.Py ?? 0, pp.Pz ?? 0, MapLoadAxes(pp.Axes)));
        }

        // ── Thermal loads (plate only) ───────────────────────────────
        List<ThermalLoad> apiTherm;
        try
        {
            apiTherm = await _api!.ListThermalLoadsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying thermal loads"), ex);
        }

        foreach (var tl in apiTherm)
        {
            if (tl.ElementType != ThermalElementType.Plate) continue;
            if (tl.ElementId == null || tl.LoadCase == null) continue;
            if (!PlateExists(tl.ElementId.Value)) continue;
            GetOrCreateEntry(tl.ElementId.Value).ThermalLoads.Add(new SgPlateThermalLoadInfo(
                tl.LoadCase.Value, ResolveLoadCaseName(lcMap, tl.LoadCase.Value), tl.LoadCategory ?? 0,
                tl.ThermalLoadProp ?? 0, tl.YThermalGradient ?? 0, tl.ZThermalGradient ?? 0));
        }

        result.PlateEntries.AddRange(entries.Values);

        foreach (var plateId in unresolvedPlates)
            result.Warnings.Add($"Plate {plateId} not found in model — loads skipped.");

        if (result.PlateEntries.Count == 0)
            result.Warnings.Add("No plate loads found in the open job.");

        return result;
    }

    /// <summary>
    ///     Runs an analysis on the current job. Dispatches to the appropriate API endpoint
    ///     based on analysis type. Returns a domain result with success/failure,
    ///     elapsed time, run ID, and any warnings.
    ///     The SpaceGass API is asynchronous — this method polls until completion.
    /// </summary>
    public async Task<SgAnalysisResult> RunAnalysisAsync(
        SgAnalysisType type = SgAnalysisType.LinearStatic,
        SgAnalysisSettingsData? settings = null,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var typeName = type switch
        {
            SgAnalysisType.LinearStatic => "Linear Static",
            SgAnalysisType.NonlinearStatic => "Non-linear Static",
            SgAnalysisType.Buckling => "Buckling",
            SgAnalysisType.DynamicFrequency => "Dynamic Frequency",
            _ => "Analysis"
        };

        onProgress?.Invoke($"Starting {typeName} analysis...");

        AnalysisRun run;
        try
        {
            run = type switch
            {
                SgAnalysisType.LinearStatic =>
                    await _api!.RunStaticAnalysisAsync(settings?.StaticSettings, ct).ConfigureAwait(false),
                SgAnalysisType.NonlinearStatic =>
                    await _api!.RunNonlinearAnalysisAsync(settings?.StaticSettings, ct).ConfigureAwait(false),
                SgAnalysisType.Buckling =>
                    await _api!.RunBucklingAnalysisAsync(settings?.BucklingSettings, ct).ConfigureAwait(false),
                SgAnalysisType.DynamicFrequency =>
                    await _api!.RunDynamicFrequencyAnalysisAsync(settings?.DynamicSettings, ct).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, $"running {typeName} analysis"), ex);
        }

        onProgress?.Invoke($"Analysing ({typeName})...");

        // The API status GET blocks server-side until the analysis completes or
        // progress changes. For fast analyses, it returns with Completed status
        // immediately — no intermediate progress is reported.
        if (run.RunId != null && !IsTerminalStatus(run.Status))
            run = await PollForAnalysisCompletionAsync(run.RunId.Value, onProgress, ct)
                .ConfigureAwait(false);

        return new SgAnalysisResult
        {
            Succeeded = run.Status == AnalysisRunStatus.Completed,
            RunId = run.RunId,
            ElapsedTime = run.ElapsedTime ?? string.Empty,
            ErrorMessage = run.ErrorMessage,
            Warnings = run.Warnings ?? new List<string>()
        };
    }

    /// <summary>
    ///     Runs a linear static analysis (backward-compatible convenience method).
    /// </summary>
    public Task<SgAnalysisResult> RunStaticAnalysisAsync(
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        return RunAnalysisAsync(SgAnalysisType.LinearStatic, null, onProgress, ct);
    }

    /// <summary>
    ///     Polls the analysis run until it reaches a terminal state (Completed, Failed, or Cancelled).
    ///     Uses the exact polling pattern from the SpaceGass API documentation.
    /// </summary>
    private async Task<AnalysisRun> PollForAnalysisCompletionAsync(
        Guid runId, Action<string>? onProgress, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromHours(1);
        var lastStep = -1;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(AnalysisPollInterval, ct).ConfigureAwait(false);

            AnalysisRun status;
            try
            {
                status = await _api!.GetAnalysisRunAsync(runId, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    ModelAssembler.FormatApiError(ex, "polling analysis status"), ex);
            }

            if (status.Progress != null)
            {
                var p = status.Progress;
                var currentStep = p.CurrentStep ?? 0;
                var stepInfo = $"Step {currentStep}/{p.TotalSteps}";

                // Include step label when available
                string? stepLabel = null;
                if (currentStep != lastStep && p.StepLabels != null &&
                    currentStep < p.StepLabels.Count &&
                    !string.IsNullOrEmpty(p.StepLabels[currentStep]))
                {
                    stepLabel = p.StepLabels[currentStep];
                    lastStep = currentStep;
                }

                var progressText = stepLabel != null
                    ? $"{stepInfo}:\n{stepLabel}"
                    : $"{stepInfo}:";

                if (p.IterationPercentage != null)
                    progressText += $" ({p.IterationPercentage}%)";
                if (p.LoadCaseStatus != null)
                    progressText += $"\nLoad Case: {p.LoadCaseStatus}";

                onProgress?.Invoke(progressText);
            }

            if (status.Status is AnalysisRunStatus.Completed
                or AnalysisRunStatus.Failed
                or AnalysisRunStatus.Cancelled)
                return status;
        }

        throw new TimeoutException("Analysis did not complete within the 1-hour safety timeout.");
    }

    /// <summary>
    ///     Returns true if the analysis run status is a terminal (final) state.
    /// </summary>
    private static bool IsTerminalStatus(AnalysisRunStatus? status)
    {
        return status == AnalysisRunStatus.Completed
               || status == AnalysisRunStatus.Failed
               || status == AnalysisRunStatus.Cancelled;
    }

    /// <summary>
    ///     Queries node reactions from the current job. Optionally filters by node
    ///     IDs and/or load case names. Returns mapped domain results with warnings.
    /// </summary>
    public async Task<SgNodeReactionsResult> GetNodeReactionsAsync(
        SgModelData model,
        IReadOnlyList<int>? nodeFilter = null,
        IReadOnlyList<string>? loadCaseFilter = null,
        CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgNodeReactionsResult();

        // ── Resolve node filter → comma-separated IDs ─────────────
        string? nodesParam = null;
        if (nodeFilter != null && nodeFilter.Count > 0)
        {
            var validNodeIds = new HashSet<int>(model.NodeMap.Values);
            var nodeIds = new List<int>();
            foreach (var nodeId in nodeFilter)
                if (validNodeIds.Contains(nodeId))
                    nodeIds.Add(nodeId);
                else
                    result.Warnings.Add(
                        $"Filter node ID {nodeId} does not match any model node — skipped.");

            if (nodeIds.Count > 0)
                nodesParam = string.Join(",", nodeIds);
        }

        // ── Resolve load case filter → comma-separated IDs ────────
        string? loadCasesParam = null;
        if (loadCaseFilter != null && loadCaseFilter.Count > 0)
        {
            var loadCaseIds = new List<int>();
            foreach (var name in loadCaseFilter)
                if (model.LoadCaseMap.TryGetValue(name, out var lcId))
                    loadCaseIds.Add(lcId);
                else if (model.CombinationLoadCaseMap.TryGetValue(name, out var clcId))
                    loadCaseIds.Add(clcId);
                else
                    result.Warnings.Add(
                        $"Load case '{name}' does not match any model load case — skipped.");

            if (loadCaseIds.Count > 0)
                loadCasesParam = string.Join(",", loadCaseIds);
        }

        // ── Query the API ─────────────────────────────────────────
        List<NodeReaction> reactions;
        try
        {
            reactions = await _api!.GetNodeReactionsAsync(nodesParam, loadCasesParam, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying node reactions"), ex);
        }

        // ── Map API results to domain model ───────────────────────
        foreach (var r in reactions)
        {
            if (r.Node == null || r.LoadCase == null)
                continue;

            result.Reactions.Add(new SgNodeReactionData(
                r.Node.Value,
                r.LoadCase.Value,
                r.Fx ?? 0,
                r.Fy ?? 0,
                r.Fz ?? 0,
                r.Mx ?? 0,
                r.My ?? 0,
                r.Mz ?? 0));
        }

        if (result.Reactions.Count == 0 && result.Warnings.Count == 0)
            result.Warnings.Add(
                "No node reactions found — has the analysis been run?");

        return result;
    }

    /// <summary>
    ///     Queries node displacements from the current job. Optionally filters by node
    ///     IDs and/or load case names. Returns mapped domain results with warnings.
    /// </summary>
    public async Task<SgNodeDisplacementsResult> GetNodeDisplacementsAsync(
        SgModelData model,
        IReadOnlyList<int>? nodeFilter = null,
        IReadOnlyList<string>? loadCaseFilter = null,
        CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgNodeDisplacementsResult();

        // ── Resolve node filter → comma-separated IDs ─────────────
        string? nodesParam = null;
        if (nodeFilter != null && nodeFilter.Count > 0)
        {
            var validNodeIds = new HashSet<int>(model.NodeMap.Values);
            var nodeIds = new List<int>();
            foreach (var nodeId in nodeFilter)
                if (validNodeIds.Contains(nodeId))
                    nodeIds.Add(nodeId);
                else
                    result.Warnings.Add(
                        $"Filter node ID {nodeId} does not match any model node — skipped.");
            if (nodeIds.Count > 0)
                nodesParam = string.Join(",", nodeIds);
        }

        // ── Resolve load case filter → comma-separated IDs ────────
        string? loadCasesParam = null;
        if (loadCaseFilter != null && loadCaseFilter.Count > 0)
        {
            var loadCaseIds = new List<int>();
            foreach (var name in loadCaseFilter)
                if (model.LoadCaseMap.TryGetValue(name, out var lcId))
                    loadCaseIds.Add(lcId);
                else if (model.CombinationLoadCaseMap.TryGetValue(name, out var clcId))
                    loadCaseIds.Add(clcId);
                else
                    result.Warnings.Add(
                        $"Load case '{name}' does not match any model load case — skipped.");
            if (loadCaseIds.Count > 0)
                loadCasesParam = string.Join(",", loadCaseIds);
        }

        // ── Query the API ─────────────────────────────────────────
        List<NodeDisplacement> displacements;
        try
        {
            displacements = await _api!.GetNodeDisplacementsAsync(nodesParam, loadCasesParam, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying node displacements"), ex);
        }

        // ── Map API results to domain model ───────────────────────
        foreach (var d in displacements)
        {
            if (d.Node == null || d.LoadCase == null)
                continue;

            result.Displacements.Add(new SgNodeDisplacementData(
                d.Node.Value,
                d.LoadCase.Value,
                d.Tx ?? 0,
                d.Ty ?? 0,
                d.Tz ?? 0,
                d.Rx ?? 0,
                d.Ry ?? 0,
                d.Rz ?? 0));
        }

        if (result.Displacements.Count == 0 && result.Warnings.Count == 0)
            result.Warnings.Add(
                "No node displacements found — has the analysis been run?");

        return result;
    }

    /// <summary>
    ///     Queries member end forces from the current job. Optionally filters by member
    ///     IDs and/or load case names. Returns flattened domain results (one record
    ///     per member end) with warnings.
    /// </summary>
    public async Task<SgMemberEndForcesResult> GetMemberEndForcesAsync(
        SgModelData model,
        IReadOnlyList<int>? memberFilter = null,
        IReadOnlyList<string>? loadCaseFilter = null,
        CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgMemberEndForcesResult();

        // ── Resolve member filter → comma-separated IDs ───────────
        var membersParam = ResolveMemberFilter(memberFilter, result.Warnings, model);

        // ── Resolve load case filter → comma-separated IDs ────────
        string? loadCasesParam = null;
        if (loadCaseFilter != null && loadCaseFilter.Count > 0)
        {
            var loadCaseIds = new List<int>();
            foreach (var name in loadCaseFilter)
                if (model.LoadCaseMap.TryGetValue(name, out var lcId))
                    loadCaseIds.Add(lcId);
                else if (model.CombinationLoadCaseMap.TryGetValue(name, out var clcId))
                    loadCaseIds.Add(clcId);
                else
                    result.Warnings.Add(
                        $"Load case '{name}' does not match any model load case — skipped.");
            if (loadCaseIds.Count > 0)
                loadCasesParam = string.Join(",", loadCaseIds);
        }

        // ── Query the API ─────────────────────────────────────────
        List<MemberEndForce> endForces;
        try
        {
            endForces = await _api!.GetMemberEndForcesAsync(membersParam, loadCasesParam, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying member end forces"), ex);
        }

        // ── Map API results to domain model (flatten per-end) ─────
        foreach (var ef in endForces)
        {
            if (ef.Member == null || ef.LoadCase == null)
                continue;

            // Each MemberEndForce has lists with one entry per end (typically 2)
            var nodeList = ef.Node ?? new List<int?>();
            var fxList = ef.Fx ?? new List<float?>();
            var fyList = ef.Fy ?? new List<float?>();
            var fzList = ef.Fz ?? new List<float?>();
            var mxList = ef.Mx ?? new List<float?>();
            var myList = ef.My ?? new List<float?>();
            var mzList = ef.Mz ?? new List<float?>();

            var count = nodeList.Count;
            for (var i = 0; i < count; i++)
            {
                var nodeId = i < nodeList.Count ? nodeList[i] : null;
                if (nodeId == null) continue;

                result.EndForces.Add(new SgMemberEndForceData(
                    ef.Member.Value,
                    ef.LoadCase.Value,
                    nodeId.Value,
                    i < fxList.Count ? fxList[i] ?? 0 : 0,
                    i < fyList.Count ? fyList[i] ?? 0 : 0,
                    i < fzList.Count ? fzList[i] ?? 0 : 0,
                    i < mxList.Count ? mxList[i] ?? 0 : 0,
                    i < myList.Count ? myList[i] ?? 0 : 0,
                    i < mzList.Count ? mzList[i] ?? 0 : 0));
            }
        }

        if (result.EndForces.Count == 0 && result.Warnings.Count == 0)
            result.Warnings.Add(
                "No member end forces found — has the analysis been run?");

        return result;
    }

    /// <summary>
    ///     Queries intermediate member forces from the current job. Optionally filters by member
    ///     IDs and/or load case names. Returns flattened domain results (one record per
    ///     station per member) with warnings.
    /// </summary>
    public async Task<SgMemberIntermediateForcesResult> GetMemberIntermediateForcesAsync(
        SgModelData model,
        IReadOnlyList<int>? memberFilter = null,
        IReadOnlyList<string>? loadCaseFilter = null,
        CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgMemberIntermediateForcesResult();

        // ── Resolve member filter → comma-separated IDs ───────────
        var membersParam = ResolveMemberFilter(memberFilter, result.Warnings, model);

        // ── Resolve load case filter → comma-separated IDs ────────
        var loadCasesParam = ResolveLoadCaseFilter(loadCaseFilter, result.Warnings, model);

        // ── Query the API ─────────────────────────────────────────
        List<MemberIntermediateForce> forces;
        try
        {
            forces = await _api!.GetMemberIntermediateForcesAsync(membersParam, loadCasesParam, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying intermediate member forces"), ex);
        }

        // ── Map API results to domain model (flatten per-station) ──
        foreach (var f in forces)
        {
            if (f.Member == null || f.LoadCase == null)
                continue;

            var stationList = f.Station ?? new List<int?>();
            var locationList = f.Location ?? new List<float?>();
            var fxList = f.Fx ?? new List<float?>();
            var fyList = f.Fy ?? new List<float?>();
            var fzList = f.Fz ?? new List<float?>();
            var mxList = f.Mx ?? new List<float?>();
            var myList = f.My ?? new List<float?>();
            var mzList = f.Mz ?? new List<float?>();

            var count = stationList.Count;
            for (var i = 0; i < count; i++)
            {
                var station = i < stationList.Count ? stationList[i] : null;
                if (station == null) continue;

                result.Forces.Add(new SgMemberIntermediateForceData(
                    f.Member.Value,
                    f.LoadCase.Value,
                    station.Value,
                    i < locationList.Count ? locationList[i] ?? 0 : 0,
                    i < fxList.Count ? fxList[i] ?? 0 : 0,
                    i < fyList.Count ? fyList[i] ?? 0 : 0,
                    i < fzList.Count ? fzList[i] ?? 0 : 0,
                    i < mxList.Count ? mxList[i] ?? 0 : 0,
                    i < myList.Count ? myList[i] ?? 0 : 0,
                    i < mzList.Count ? mzList[i] ?? 0 : 0));
            }
        }

        if (result.Forces.Count == 0 && result.Warnings.Count == 0)
            result.Warnings.Add(
                "No intermediate member forces found — has the analysis been run?");

        return result;
    }

    /// <summary>
    ///     Queries intermediate member displacements from the current job. Optionally filters by
    ///     member IDs and/or load case names. Returns global and local translations at
    ///     each station with warnings.
    /// </summary>
    public async Task<SgMemberDisplacementsResult> GetMemberDisplacementsAsync(
        SgModelData model,
        IReadOnlyList<int>? memberFilter = null,
        IReadOnlyList<string>? loadCaseFilter = null,
        CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgMemberDisplacementsResult();

        // ── Resolve member filter → comma-separated IDs ───────────
        var membersParam = ResolveMemberFilter(memberFilter, result.Warnings, model);

        // ── Resolve load case filter → comma-separated IDs ────────
        var loadCasesParam = ResolveLoadCaseFilter(loadCaseFilter, result.Warnings, model);

        // ── Query the API ─────────────────────────────────────────
        List<MemberIntermediateDisplacement> displacements;
        try
        {
            displacements = await _api!.GetMemberIntermediateDisplacementsAsync(membersParam, loadCasesParam, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying member displacements"), ex);
        }

        // ── Map API results to domain model (flatten per-station) ──
        foreach (var d in displacements)
        {
            if (d.Member == null || d.LoadCase == null)
                continue;

            var stationList = d.Station ?? new List<int?>();
            var locationList = d.Location ?? new List<float?>();
            var txgList = d.TxGlobal ?? new List<float?>();
            var tygList = d.TyGlobal ?? new List<float?>();
            var tzgList = d.TzGlobal ?? new List<float?>();
            var txlList = d.TxLocal ?? new List<float?>();
            var tylList = d.TyLocal ?? new List<float?>();
            var tzlList = d.TzLocal ?? new List<float?>();

            var count = stationList.Count;
            for (var i = 0; i < count; i++)
            {
                var station = i < stationList.Count ? stationList[i] : null;
                if (station == null) continue;

                result.Displacements.Add(new SgMemberDisplacementData(
                    d.Member.Value,
                    d.LoadCase.Value,
                    station.Value,
                    i < locationList.Count ? locationList[i] ?? 0 : 0,
                    i < txgList.Count ? txgList[i] ?? 0 : 0,
                    i < tygList.Count ? tygList[i] ?? 0 : 0,
                    i < tzgList.Count ? tzgList[i] ?? 0 : 0,
                    i < txlList.Count ? txlList[i] ?? 0 : 0,
                    i < tylList.Count ? tylList[i] ?? 0 : 0,
                    i < tzlList.Count ? tzlList[i] ?? 0 : 0));
            }
        }

        if (result.Displacements.Count == 0 && result.Warnings.Count == 0)
            result.Warnings.Add(
                "No member displacements found — has the analysis been run?");

        return result;
    }

    /// <summary>
    ///     Resolves member ID filter to comma-separated SpaceGass member IDs.
    ///     Adds warnings for unmatched members.
    /// </summary>
    private static string? ResolveMemberFilter(
        IReadOnlyList<int>? memberFilter,
        List<string> warnings,
        SgModelData model)
    {
        if (memberFilter == null || memberFilter.Count == 0)
            return null;

        var memberIds = new List<int>();
        foreach (var memberId in memberFilter)
        {
            if (model.MemberMap.ContainsKey(memberId))
                memberIds.Add(memberId);
            else
                warnings.Add($"Filter member ID {memberId} does not match any model member — skipped.");
        }

        return memberIds.Count > 0 ? string.Join(",", memberIds) : null;
    }

    /// <summary>
    ///     Resolves load case name filter to comma-separated SpaceGass load case IDs.
    ///     Checks both primary LoadCaseMap and CombinationLoadCaseMap.
    ///     Adds warnings for unmatched load case names.
    /// </summary>
    private static string? ResolveLoadCaseFilter(
        IReadOnlyList<string>? loadCaseFilter,
        List<string> warnings,
        SgModelData model)
    {
        if (loadCaseFilter == null || loadCaseFilter.Count == 0)
            return null;

        var loadCaseIds = new List<int>();
        foreach (var name in loadCaseFilter)
            if (model.LoadCaseMap.TryGetValue(name, out var lcId))
                loadCaseIds.Add(lcId);
            else if (model.CombinationLoadCaseMap.TryGetValue(name, out var clcId))
                loadCaseIds.Add(clcId);
            else
                warnings.Add(
                    $"Load case '{name}' does not match any model load case — skipped.");
        return loadCaseIds.Count > 0 ? string.Join(",", loadCaseIds) : null;
    }

    /// <summary>
    ///     Queries buckling results (load factors + effective lengths) from the current job.
    ///     Optionally filters by member IDs, mode numbers, and/or load case names.
    /// </summary>
    public async Task<SgBucklingResultsResult> GetBucklingResultsAsync(
        SgModelData model,
        IReadOnlyList<int>? memberFilter = null,
        IReadOnlyList<int>? modesFilter = null,
        IReadOnlyList<string>? loadCaseFilter = null,
        CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgBucklingResultsResult();

        // ── Resolve member filter → comma-separated IDs ───────────
        var membersParam = ResolveMemberFilter(memberFilter, result.Warnings, model);

        // ── Resolve modes filter → comma-separated mode numbers ───
        string? modesParam = null;
        HashSet<int>? modesSet = null;
        if (modesFilter != null && modesFilter.Count > 0)
        {
            modesParam = string.Join(",", modesFilter);
            modesSet = new HashSet<int>(modesFilter);
        }

        // ── Resolve load case filter → set of IDs for client-side filtering + string for server-side ──
        HashSet<int>? loadCaseIdSet = null;
        string? loadCasesParam = null;
        if (loadCaseFilter != null && loadCaseFilter.Count > 0)
        {
            loadCaseIdSet = new HashSet<int>();
            foreach (var name in loadCaseFilter)
                if (model.LoadCaseMap.TryGetValue(name, out var lcId))
                    loadCaseIdSet.Add(lcId);
                else if (model.CombinationLoadCaseMap.TryGetValue(name, out var clcId))
                    loadCaseIdSet.Add(clcId);
                else
                    result.Warnings.Add(
                        $"Load case '{name}' does not match any model load case — skipped.");

            if (loadCaseIdSet.Count > 0)
                loadCasesParam = string.Join(",", loadCaseIdSet);
        }

        // ── Query 1: Load Factors (no server-side load case filter available) ─────────
        List<BucklingLoadFactor> loadFactors;
        try
        {
            loadFactors = await _api!.GetBucklingLoadFactorsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying buckling load factors"), ex);
        }

        // Build reverse node map (ID → point) for resolving node references
        var idToPoint = new Dictionary<int, SgPoint3D>();
        foreach (var kvp in model.NodeMap)
            idToPoint[kvp.Value] = kvp.Key;

        foreach (var lf in loadFactors)
        {
            if (lf.Mode == null) continue;

            // Client-side mode filter
            if (modesSet != null && !modesSet.Contains(lf.Mode.Value)) continue;

            // Client-side load case filter
            if (loadCaseIdSet != null && (lf.LoadCase == null || !loadCaseIdSet.Contains(lf.LoadCase.Value))) continue;

            var transNodeId = (int)(lf.NodeAtMaxTrans ?? 0);
            var rotnNodeId = (int)(lf.NodeAtMaxRotn ?? 0);

            result.LoadFactors.Add(new SgBucklingLoadFactorData(
                lf.LoadCase ?? 0,
                lf.Mode.Value,
                lf.LoadFactor ?? 0,
                transNodeId > 0 && idToPoint.TryGetValue(transNodeId, out var transPt) ? transPt : null,
                lf.TransAxis ?? "",
                rotnNodeId > 0 && idToPoint.TryGetValue(rotnNodeId, out var rotnPt) ? rotnPt : null,
                lf.RotnAxis ?? ""));
        }

        // ── Query 2: Effective Lengths (server-side filter) ───────
        List<BucklingEffectiveLength> effectiveLengths;
        try
        {
            effectiveLengths = await _api!.GetBucklingEffectiveLengthsAsync(
                membersParam, modesParam, loadCasesParam, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying buckling effective lengths"), ex);
        }

        foreach (var el in effectiveLengths)
        {
            if (el.Member == null || el.Mode == null) continue;

            // Client-side load case filter
            if (loadCaseIdSet != null && (el.LoadCase == null || !loadCaseIdSet.Contains(el.LoadCase.Value))) continue;

            result.EffectiveLengths.Add(new SgBucklingEffectiveLengthData(
                el.LoadCase ?? 0,
                el.Member.Value,
                el.Mode.Value,
                el.Ly ?? 0,
                el.Lz ?? 0,
                el.Pcr ?? 0,
                el.Length ?? 0));
        }

        if (result.LoadFactors.Count == 0 && result.EffectiveLengths.Count == 0 && result.Warnings.Count == 0)
            result.Warnings.Add(
                "No buckling results found — has a buckling analysis been run?");

        return result;
    }

    /// <summary>
    ///     Queries dynamic frequency results (natural frequencies + mode shapes) from the current job.
    ///     Optionally filters by mode numbers, node IDs, and/or load case names.
    /// </summary>
    public async Task<SgDynamicFrequencyResultsResult> GetDynamicFrequencyResultsAsync(
        SgModelData model,
        IReadOnlyList<int>? modesFilter = null,
        IReadOnlyList<int>? nodesFilter = null,
        IReadOnlyList<string>? loadCaseFilter = null,
        CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgDynamicFrequencyResultsResult();

        // ── Resolve modes filter → comma-separated mode numbers ────
        string? modesParam = null;
        if (modesFilter != null && modesFilter.Count > 0)
            modesParam = string.Join(",", modesFilter);

        // ── Resolve node filter → comma-separated IDs ──────────────
        string? nodesParam = null;
        if (nodesFilter != null && nodesFilter.Count > 0)
        {
            var validNodeIds = new HashSet<int>(model.NodeMap.Values);
            var nodeIds = new List<int>();
            foreach (var nodeId in nodesFilter)
                if (validNodeIds.Contains(nodeId))
                    nodeIds.Add(nodeId);
                else
                    result.Warnings.Add(
                        $"Filter node ID {nodeId} does not match any model node — skipped.");

            if (nodeIds.Count > 0)
                nodesParam = string.Join(",", nodeIds);
        }

        // ── Resolve load case filter → set of IDs for client-side filtering + string for server-side ──
        HashSet<int>? loadCaseIdSet = null;
        string? loadCasesParam = null;
        if (loadCaseFilter != null && loadCaseFilter.Count > 0)
        {
            loadCaseIdSet = new HashSet<int>();
            foreach (var name in loadCaseFilter)
                if (model.LoadCaseMap.TryGetValue(name, out var lcId))
                    loadCaseIdSet.Add(lcId);
                else if (model.CombinationLoadCaseMap.TryGetValue(name, out var clcId))
                    loadCaseIdSet.Add(clcId);
                else
                    result.Warnings.Add(
                        $"Load case '{name}' does not match any model load case — skipped.");

            if (loadCaseIdSet.Count > 0)
                loadCasesParam = string.Join(",", loadCaseIdSet);
        }

        // ── Query 1: Natural Frequencies (server-side filter) ────
        List<NaturalFrequency> frequencies;
        try
        {
            frequencies = await _api!.GetNaturalFrequenciesAsync(modesParam, loadCasesParam, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying natural frequencies"), ex);
        }

        foreach (var f in frequencies)
        {
            if (f.Mode == null) continue;

            // Client-side load case filter
            if (loadCaseIdSet != null && (f.LoadCase == null || !loadCaseIdSet.Contains(f.LoadCase.Value))) continue;

            result.NaturalFrequencies.Add(new SgNaturalFrequencyData(
                f.LoadCase ?? 0,
                f.Mode.Value,
                f.NaturalFrequencyProp ?? 0,
                f.NaturalPeriod ?? 0,
                f.MassPartX ?? 0,
                f.MassPartY ?? 0,
                f.MassPartZ ?? 0));
        }

        // ── Query 2: Mode Shapes (server-side filter) ──────────────
        List<ModeShape> modeShapes;
        try
        {
            modeShapes = await _api!.GetModeShapesAsync(modesParam, nodesParam, loadCasesParam, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying mode shapes"), ex);
        }

        foreach (var ms in modeShapes)
        {
            if (ms.Mode == null) continue;

            // Client-side load case filter
            if (loadCaseIdSet != null && (ms.LoadCase == null || !loadCaseIdSet.Contains(ms.LoadCase.Value))) continue;

            var nodeList = ms.Node ?? new List<int?>();
            var txList = ms.Tx ?? new List<float?>();
            var tyList = ms.Ty ?? new List<float?>();
            var tzList = ms.Tz ?? new List<float?>();
            var rxList = ms.Rx ?? new List<float?>();
            var ryList = ms.Ry ?? new List<float?>();
            var rzList = ms.Rz ?? new List<float?>();

            var count = nodeList.Count;
            for (var i = 0; i < count; i++)
            {
                var nodeId = i < nodeList.Count ? nodeList[i] : null;
                if (nodeId == null) continue;

                result.ModeShapes.Add(new SgModeShapeNodeData(
                    ms.LoadCase ?? 0,
                    ms.Mode.Value,
                    nodeId.Value,
                    i < txList.Count ? txList[i] ?? 0 : 0,
                    i < tyList.Count ? tyList[i] ?? 0 : 0,
                    i < tzList.Count ? tzList[i] ?? 0 : 0,
                    i < rxList.Count ? rxList[i] ?? 0 : 0,
                    i < ryList.Count ? ryList[i] ?? 0 : 0,
                    i < rzList.Count ? rzList[i] ?? 0 : 0));
            }
        }

        if (result.NaturalFrequencies.Count == 0 && result.ModeShapes.Count == 0 && result.Warnings.Count == 0)
            result.Warnings.Add(
                "No dynamic frequency results found — has a dynamic frequency analysis been run?");

        return result;
    }

    /// <summary>
    ///     Queries plate element forces from the current job. Optionally filters by
    ///     plate corner nodes and/or load case names.
    /// </summary>
    public async Task<SgPlateElementForcesResult> GetPlateElementForcesAsync(
        SgModelData model,
        IReadOnlyList<int>? plateFilter = null,
        IReadOnlyList<string>? loadCaseFilter = null,
        CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgPlateElementForcesResult();

        var platesParam = ResolvePlateFilter(plateFilter, result.Warnings, model);
        var loadCasesParam = ResolveLoadCaseFilter(loadCaseFilter, result.Warnings, model);

        List<PlateElementForce> forces;
        try
        {
            forces = await _api!.GetPlateElementForcesAsync(platesParam, loadCasesParam, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying plate element forces"), ex);
        }

        foreach (var f in forces)
        {
            if (f.Plate == null || f.LoadCase == null) continue;
            result.Forces.Add(new SgPlateElementForceData(
                f.Plate.Value, f.LoadCase.Value,
                f.Fx ?? 0, f.Fy ?? 0, f.Fxy ?? 0,
                f.Mx ?? 0, f.My ?? 0, f.Mxy ?? 0,
                f.MxTop ?? 0, f.MxBtm ?? 0, f.MyTop ?? 0, f.MyBtm ?? 0,
                f.Vxz ?? 0, f.Vyz ?? 0));
        }

        if (result.Forces.Count == 0 && result.Warnings.Count == 0)
            result.Warnings.Add("No plate element forces found — has the analysis been run?");

        return result;
    }

    /// <summary>
    ///     Queries plate nodal forces from the current job. Optionally filters by
    ///     plate corner nodes and/or load case names.
    /// </summary>
    public async Task<SgPlateNodalForcesResult> GetPlateNodalForcesAsync(
        SgModelData model,
        IReadOnlyList<int>? plateFilter = null,
        IReadOnlyList<string>? loadCaseFilter = null,
        CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgPlateNodalForcesResult();

        var platesParam = ResolvePlateFilter(plateFilter, result.Warnings, model);
        var loadCasesParam = ResolveLoadCaseFilter(loadCaseFilter, result.Warnings, model);

        List<PlateNodalForce> forces;
        try
        {
            forces = await _api!.GetPlateNodalForcesAsync(platesParam, loadCasesParam, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying plate nodal forces"), ex);
        }

        foreach (var f in forces)
        {
            if (f.Plate == null || f.LoadCase == null) continue;

            var nodeList = f.Node ?? new List<int?>();
            var fxList = f.Fx ?? new List<float?>();
            var fyList = f.Fy ?? new List<float?>();
            var fzList = f.Fz ?? new List<float?>();
            var mxList = f.Mx ?? new List<float?>();
            var myList = f.My ?? new List<float?>();
            var mzList = f.Mz ?? new List<float?>();

            for (var i = 0; i < nodeList.Count; i++)
            {
                var nodeId = nodeList[i];
                if (nodeId == null) continue;

                result.Forces.Add(new SgPlateNodalForceData(
                    f.Plate.Value, f.LoadCase.Value, nodeId.Value,
                    i < fxList.Count ? fxList[i] ?? 0 : 0,
                    i < fyList.Count ? fyList[i] ?? 0 : 0,
                    i < fzList.Count ? fzList[i] ?? 0 : 0,
                    i < mxList.Count ? mxList[i] ?? 0 : 0,
                    i < myList.Count ? myList[i] ?? 0 : 0,
                    i < mzList.Count ? mzList[i] ?? 0 : 0));
            }
        }

        if (result.Forces.Count == 0 && result.Warnings.Count == 0)
            result.Warnings.Add("No plate nodal forces found — has the analysis been run?");

        return result;
    }

    /// <summary>
    ///     Resolves plate corner node arrays to comma-separated SpaceGass plate IDs.
    /// </summary>
    private static string? ResolvePlateFilter(
        IReadOnlyList<int>? plateFilter,
        List<string> warnings,
        SgModelData model)
    {
        if (plateFilter == null || plateFilter.Count == 0) return null;

        var plateIds = new List<int>();
        foreach (var plateId in plateFilter)
        {
            if (model.PlateMap.ContainsKey(plateId))
                plateIds.Add(plateId);
            else
                warnings.Add($"Filter plate ID {plateId} does not match any model plate — skipped.");
        }

        return plateIds.Count > 0 ? string.Join(",", plateIds) : null;
    }

    /// <summary>
    ///     Queries the steel-design member check summary from the current job.
    ///     Returns one aggregated record per steel-design group — the critical load case is a value
    ///     on each record, not a data-tree dimension (ADR-0016). Optionally filters by design-group IDs
    ///     (passed to the API's <c>Members</c> query parameter — the API uses the term "Member" for
    ///     what is actually a design-group identifier).
    /// </summary>
    public async Task<SgSteelMemberCheckSummaryResult> GetSteelMemberCheckSummaryAsync(
        SgModelData model,
        IReadOnlyList<int>? designGroupFilter = null,
        CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var result = new SgSteelMemberCheckSummaryResult();

        // The API's Members query parameter is really a design-group filter — but we still validate
        // against MemberMap since design-group IDs share the model member ID space in the current API.
        var groupsParam = ResolveDesignGroupFilter(designGroupFilter, result.Warnings, model);

        List<SteelCheckSummary> summaries;
        try
        {
            summaries = await _api!.GetSteelMemberCheckSummaryAsync(groupsParam, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "querying steel member check summary"), ex);
        }

        foreach (var s in summaries)
        {
            if (s.Member == null) continue;

            result.Checks.Add(new SgSteelMemberCheckData(
                designGroupId: s.Member.Value,
                section: s.Section ?? string.Empty,
                flag: s.Flag ?? string.Empty,
                loadFactor: s.LoadFactor ?? 0.0,
                criticalCaseId: s.CriticalCase,
                failureMode: s.Failure ?? string.Empty,
                segmentLength: s.SegmentLength ?? 0.0,
                totalLength: s.TotalLength ?? 0.0,
                yield: s.Yield ?? 0.0));
        }

        result.Checks.Sort((a, b) => a.DesignGroupId.CompareTo(b.DesignGroupId));

        return result;
    }

    /// <summary>
    ///     Resolves a design-group ID filter to a comma-separated string for the API's
    ///     <c>Members</c> query parameter. Validates against the model's MemberMap
    ///     (design-group IDs share the member ID space in the current API surface).
    /// </summary>
    private static string? ResolveDesignGroupFilter(
        IReadOnlyList<int>? designGroupFilter,
        List<string> warnings,
        SgModelData model)
    {
        if (designGroupFilter == null || designGroupFilter.Count == 0)
            return null;

        var ids = new List<int>();
        foreach (var id in designGroupFilter)
        {
            if (model.MemberMap.ContainsKey(id))
                ids.Add(id);
            else
                warnings.Add($"Filter design group ID {id} does not match any model member — skipped.");
        }

        return ids.Count > 0 ? string.Join(",", ids) : null;
    }

    /// <summary>
    ///     Retrieves the full job status including headings, settings, units, and summary counts.
    ///     Maps from the API's JobStatus to a clean SgJobInfo domain model.
    /// </summary>
    public async Task<SgJobInfo> GetJobInfoAsync(CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        JobStatus status;
        try
        {
            status = await _api!.GetFullJobStatusAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "getting job status"), ex);
        }

        return MapJobStatus(status);
    }

    /// <summary>
    ///     Updates the job headings via PATCH. Returns the updated SgJobInfo
    ///     (re-queries full status after update to ensure consistent output).
    /// </summary>
    public async Task<SgJobInfo> UpdateHeadingsAsync(
        string? heading = null,
        string? projectHeading = null,
        string? designerInitials = null,
        string? notes = null,
        CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SpaceGass");

        var update = new JobHeadingsUpdate();
        if (heading != null) update.Heading = heading;
        if (projectHeading != null) update.ProjectHeading = projectHeading;
        if (designerInitials != null) update.DesignerInitials = designerInitials;
        if (notes != null) update.Notes = notes;

        try
        {
            await _api!.UpdateHeadingsAsync(update, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                ModelAssembler.FormatApiError(ex, "updating job headings"), ex);
        }

        // Re-query full status to return consistent output
        return await GetJobInfoAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Maps the API's JobStatus response to the domain SgJobInfo model.
    /// </summary>
    internal static SgJobInfo MapJobStatus(JobStatus status)
    {
        var info = new SgJobInfo();

        // State
        if (status.State != null)
        {
            info.IsOpen = status.State.IsOpen ?? false;
            info.IsNew = status.State.IsNew ?? false;
            info.IsModified = status.State.IsModified ?? false;
            if (status.State.File != null)
            {
                info.FilePath = status.State.File.Path ?? string.Empty;
                info.FileName = status.State.File.Name ?? string.Empty;
            }
        }

        // Job sub-object: Headings, Settings, Units
        if (status.Job != null)
        {
            if (status.Job.Headings != null)
            {
                info.Heading = status.Job.Headings.Heading ?? string.Empty;
                info.ProjectHeading = status.Job.Headings.ProjectHeading ?? string.Empty;
                info.DesignerInitials = status.Job.Headings.DesignerInitials ?? string.Empty;
                info.Notes = status.Job.Headings.Notes ?? string.Empty;
            }

            if (status.Job.Settings != null)
                info.VerticalAxis = status.Job.Settings.VerticalAxis?.ToString() ?? string.Empty;

            if (status.Job.Units != null)
            {
                info.LengthUnit = status.Job.Units.Length?.ToString() ?? string.Empty;
                info.ForceUnit = status.Job.Units.Force?.ToString() ?? string.Empty;
                info.MomentUnit = status.Job.Units.Moment?.ToString() ?? string.Empty;
                info.StressUnit = status.Job.Units.Stress?.ToString() ?? string.Empty;
                info.TemperatureUnit = status.Job.Units.Temperature?.ToString() ?? string.Empty;
                info.MassUnit = status.Job.Units.Mass?.ToString() ?? string.Empty;
                info.MassDensityUnit = status.Job.Units.MassDensity?.ToString() ?? string.Empty;
                info.TranslationUnit = status.Job.Units.Translation?.ToString() ?? string.Empty;
                info.AccelerationUnit = status.Job.Units.Acceleration?.ToString() ?? string.Empty;
                info.SectionPropertiesUnit = status.Job.Units.SectionProperties?.ToString() ?? string.Empty;
                info.MaterialStrengthUnit = status.Job.Units.MaterialStrength?.ToString() ?? string.Empty;
            }
        }

        // Structure summary
        if (status.Structure != null)
        {
            info.NodeCount = status.Structure.Nodes ?? 0;
            info.MemberCount = status.Structure.Members ?? 0;
            info.MaterialCount = status.Structure.Materials ?? 0;
            info.SectionCount = status.Structure.Sections ?? 0;
            info.RestraintCount = status.Structure.NodeRestraints ?? 0;
            info.PlateCount = status.Structure.Plates ?? 0;
        }

        // Loads summary
        if (status.Loads != null)
        {
            info.LoadCaseCount = status.Loads.LoadCases ?? 0;
            info.LoadCategoryCount = status.Loads.LoadCategories ?? 0;
            info.NodeLoadCount = status.Loads.NodeLoads ?? 0;
            info.MemberDistributedLoadCount = status.Loads.MemberDistributedLoads ?? 0;
            info.SelfWeightLoadCount = status.Loads.SelfWeightLoads ?? 0;
        }

        // Analysis summary
        if (status.Analysis != null)
        {
            info.HasStaticResults = status.Analysis.HasStaticResults ?? false;
            info.HasBucklingResults = status.Analysis.HasBucklingResults ?? false;
            info.HasDynamicResults = status.Analysis.HasDynamicResults ?? false;
        }

        return info;
    }
}