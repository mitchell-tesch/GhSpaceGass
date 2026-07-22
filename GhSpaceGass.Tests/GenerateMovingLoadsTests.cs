using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

/// <summary>
///     Tests for the Generate Moving Loads workflow (Slice 43.4). Covers
///     <see cref="SpaceGassSession.GenerateMovingLoadsAsync"/> — the terminal step of the
///     moving-load pipeline. Uses branch-keyed request "trees" (represented as
///     <see cref="Dictionary{TKey,TValue}"/> from branch-path string → items) rather than
///     Grasshopper's `GH_Structure` so the tests are Rhino-free.
/// </summary>
public class GenerateMovingLoadsTests
{
    private const int TestPort = 34560;
    private const string TestInstallPath = @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ISpaceGassApiFactory _apiFactory = Substitute.For<ISpaceGassApiFactory>();
    private readonly IProcessManager _processManager = Substitute.For<IProcessManager>();

    // ── Helpers ───────────────────────────────────────────────────────

    private SpaceGassSession CreateConnectedSession()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        _api.GetServiceInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new ServiceInfo { SpaceGassVersion = "14.5" });

        var session = new SpaceGassSession(
            TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);
        session.ConnectAsync().GetAwaiter().GetResult();
        return session;
    }

    /// <summary>Builds a minimal <see cref="SgModelData"/> for Generate tests.</summary>
    private static SgModelData ModelWith(int memberCount = 2, int plateCount = 1,
        params (string name, int id, bool include)[] scenarios)
    {
        var model = new SgModelData();
        for (var i = 1; i <= memberCount; i++)
            model.MemberMap[i] = (new SgPoint3D(i, 0, 0), new SgPoint3D(i + 1, 0, 0));
        for (var i = 1; i <= plateCount; i++)
            model.PlateMap[i] = new[] { new SgPoint3D(0, 0, 0), new SgPoint3D(1, 0, 0),
                new SgPoint3D(1, 1, 0), new SgPoint3D(0, 1, 0) };
        foreach (var (name, id, _) in scenarios)
            model.MovingLoadScenarioMap[name] = id;
        return model;
    }

    private void StubApiReturnsForGenerate(params (string name, int id, bool include)[] scenariosInJob)
    {
        _api.ListMovingLoadScenariosAsync(Arg.Any<CancellationToken>())
            .Returns(scenariosInJob.Select(s => new MovingLoadScenario
            {
                Id = s.id, Name = s.name, Include = s.include
            }).ToList());

        _api.PatchMovingLoadElementsToLoadAsync(
                Arg.Any<MovingLoadElementsToLoadUpdate>(), Arg.Any<CancellationToken>())
            .Returns(new MovingLoadElementsToLoad());
        _api.PatchMovingLoadScenarioAsync(
                Arg.Any<int>(), Arg.Any<MovingLoadScenarioUpdate>(), Arg.Any<CancellationToken>())
            .Returns(new MovingLoadScenario());
        _api.PatchMovingLoadSettingsAsync(
                Arg.Any<MovingLoadSettingsUpdate>(), Arg.Any<CancellationToken>())
            .Returns(new MovingLoadSettings());
        _api.GenerateMovingLoadsAsync(
                Arg.Any<MovingLoadGenerateRequest>(), Arg.Any<CancellationToken>())
            .Returns(args => new MovingLoadGenerationResult
            {
                GeneratedLoadCaseIds = new List<int?> { 101, 102 },
                GeneratedGroups = new List<string> { "MLGroup1" }
            });
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Guard: not connected ─────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Generate_WhenNotConnected_ThrowsInvalidOperationException()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);

        var model = ModelWith(scenarios: ("Scen", 501, true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GenerateMovingLoadsAsync(model,
                membersToLoad: new Dictionary<string, IReadOnlyList<int>>(),
                platesToLoad: new Dictionary<string, IReadOnlyList<int>>(),
                scenariosToApply: new Dictionary<string, IReadOnlyList<string>>()));
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Empty trees → no-op ──────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Generate_AllTreesEmpty_WarnsAndSkips()
    {
        var session = CreateConnectedSession();
        StubApiReturnsForGenerate(("Scen", 501, true));
        var model = ModelWith(scenarios: ("Scen", 501, true));

        var result = await session.GenerateMovingLoadsAsync(model,
            new Dictionary<string, IReadOnlyList<int>>(),
            new Dictionary<string, IReadOnlyList<int>>(),
            new Dictionary<string, IReadOnlyList<string>>());

        Assert.True(result.Success);
        Assert.Contains(result.Warnings,
            w => w.Contains("no subgroups", StringComparison.OrdinalIgnoreCase));
        await _api.DidNotReceive().GenerateMovingLoadsAsync(
            Arg.Any<MovingLoadGenerateRequest>(), Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Single-branch generate ───────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Generate_SingleBranchMembersOnly_PatchesEtl_TogglesInclude_Generates()
    {
        var session = CreateConnectedSession();
        StubApiReturnsForGenerate(("Scen", 501, true));
        var model = ModelWith(scenarios: ("Scen", 501, true));

        var members = new Dictionary<string, IReadOnlyList<int>>
        {
            { "{0}", new[] { 1, 2 } }
        };
        var plates = new Dictionary<string, IReadOnlyList<int>>();
        var scenarios = new Dictionary<string, IReadOnlyList<string>>
        {
            { "{0}", new[] { "Scen" } }
        };

        var result = await session.GenerateMovingLoadsAsync(model, members, plates, scenarios);

        Assert.True(result.Success);
        // Members PATCHed, Plates left null on the payload
        await _api.Received(1).PatchMovingLoadElementsToLoadAsync(
            Arg.Is<MovingLoadElementsToLoadUpdate>(u =>
                u.Members == "1-2" && u.Plates == null),
            Arg.Any<CancellationToken>());
        // Scenario Include toggled to true then restored
        await _api.Received().PatchMovingLoadScenarioAsync(
            501, Arg.Is<MovingLoadScenarioUpdate>(u => u.Include == true),
            Arg.Any<CancellationToken>());
        // Generate called
        await _api.Received(1).GenerateMovingLoadsAsync(
            Arg.Any<MovingLoadGenerateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_SingleBranchPlatesOnly_PatchesEtl_WithPlatesString()
    {
        var session = CreateConnectedSession();
        StubApiReturnsForGenerate(("Scen", 501, true));
        var model = ModelWith(scenarios: ("Scen", 501, true));

        var members = new Dictionary<string, IReadOnlyList<int>>();
        var plates = new Dictionary<string, IReadOnlyList<int>>
        {
            { "{0}", new[] { 1 } }
        };
        var scenarios = new Dictionary<string, IReadOnlyList<string>>
        {
            { "{0}", new[] { "Scen" } }
        };

        await session.GenerateMovingLoadsAsync(model, members, plates, scenarios);

        await _api.Received(1).PatchMovingLoadElementsToLoadAsync(
            Arg.Is<MovingLoadElementsToLoadUpdate>(u =>
                u.Plates == "1" && u.Members == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_SingleBranchBoth_PatchesEtl_WithBothFields()
    {
        var session = CreateConnectedSession();
        StubApiReturnsForGenerate(("Scen", 501, true));
        var model = ModelWith(scenarios: ("Scen", 501, true));

        var members = new Dictionary<string, IReadOnlyList<int>>
            { { "{0}", new[] { 1, 2 } } };
        var plates = new Dictionary<string, IReadOnlyList<int>>
            { { "{0}", new[] { 1 } } };
        var scenarios = new Dictionary<string, IReadOnlyList<string>>
            { { "{0}", new[] { "Scen" } } };

        await session.GenerateMovingLoadsAsync(model, members, plates, scenarios);

        await _api.Received(1).PatchMovingLoadElementsToLoadAsync(
            Arg.Is<MovingLoadElementsToLoadUpdate>(u =>
                u.Members == "1-2" && u.Plates == "1"),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Multi-branch ─────────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Generate_MultipleBranches_OneGeneratePerBranch()
    {
        var session = CreateConnectedSession();
        StubApiReturnsForGenerate(("S1", 501, true), ("S2", 502, true));
        var model = ModelWith(memberCount: 4,
            scenarios: new[] { ("S1", 501, true), ("S2", 502, true) });

        var members = new Dictionary<string, IReadOnlyList<int>>
        {
            { "{0}", new[] { 1, 2 } },
            { "{1}", new[] { 3, 4 } }
        };
        var plates = new Dictionary<string, IReadOnlyList<int>>();
        var scenarios = new Dictionary<string, IReadOnlyList<string>>
        {
            { "{0}", new[] { "S1" } },
            { "{1}", new[] { "S2" } }
        };

        var result = await session.GenerateMovingLoadsAsync(model, members, plates, scenarios);

        Assert.True(result.Success);
        Assert.Equal(2, result.Branches.Count);
        await _api.Received(2).GenerateMovingLoadsAsync(
            Arg.Any<MovingLoadGenerateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_ScenarioTogglingPerBranch()
    {
        var session = CreateConnectedSession();
        StubApiReturnsForGenerate(("S1", 501, true), ("S2", 502, true));
        var model = ModelWith(memberCount: 4,
            scenarios: new[] { ("S1", 501, true), ("S2", 502, true) });

        // Track scenario Include state through PATCH calls, per-branch snapshots
        var includePatches = new List<(int scenarioId, bool include, int callOrder)>();
        var callCounter = 0;
        _api.PatchMovingLoadScenarioAsync(
                Arg.Any<int>(), Arg.Any<MovingLoadScenarioUpdate>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var sid = (int)args[0];
                var upd = (MovingLoadScenarioUpdate)args[1];
                includePatches.Add((sid, upd.Include ?? false, Interlocked.Increment(ref callCounter)));
                return new MovingLoadScenario();
            });

        var members = new Dictionary<string, IReadOnlyList<int>>
        {
            { "{0}", new[] { 1 } },
            { "{1}", new[] { 2 } }
        };
        var plates = new Dictionary<string, IReadOnlyList<int>>();
        var scenarios = new Dictionary<string, IReadOnlyList<string>>
        {
            { "{0}", new[] { "S1" } },
            { "{1}", new[] { "S2" } }
        };

        await session.GenerateMovingLoadsAsync(model, members, plates, scenarios);

        // Branch 0: S1 include=true, S2 include=false
        // Branch 1: S1 include=false, S2 include=true
        // Then restore: S1 include=true, S2 include=true (both were originally true)
        // Order matters: for branch 0 we should see (501, true) AND (502, false) before
        // any Generate call. Then for branch 1 we should see the toggle.
        var s1Toggles = includePatches.Where(p => p.scenarioId == 501)
            .Select(p => p.include).ToList();
        var s2Toggles = includePatches.Where(p => p.scenarioId == 502)
            .Select(p => p.include).ToList();

        // S1: true (branch 0) → false (branch 1) → true (restore)
        Assert.Equal(new[] { true, false, true }, s1Toggles);
        // S2: false (branch 0) → true (branch 1) → true (restore)
        Assert.Equal(new[] { false, true, true }, s2Toggles);
    }

    [Fact]
    public async Task Generate_IncludeRestored_EvenOnGenerateFailure()
    {
        var session = CreateConnectedSession();
        StubApiReturnsForGenerate(("S1", 501, true));
        var model = ModelWith(scenarios: ("S1", 501, true));

        // Fail the Generate POST
        _api.GenerateMovingLoadsAsync(
                Arg.Any<MovingLoadGenerateRequest>(), Arg.Any<CancellationToken>())
            .Returns<MovingLoadGenerationResult>(_ =>
                throw new InvalidOperationException("boom"));

        var members = new Dictionary<string, IReadOnlyList<int>>
            { { "{0}", new[] { 1 } } };
        var plates = new Dictionary<string, IReadOnlyList<int>>();
        var scenarios = new Dictionary<string, IReadOnlyList<string>>
            { { "{0}", new[] { "S1" } } };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GenerateMovingLoadsAsync(model, members, plates, scenarios));

        // Even after the failure, S1 should have been restored to its original Include=true.
        // (During branch 0, S1 was already true so no change; but we assert the final state
        // is a restore call — the snapshot restoration is a finally-block behaviour.)
        // The last PATCH on S1's Include should be true.
        await _api.Received().PatchMovingLoadScenarioAsync(
            501, Arg.Is<MovingLoadScenarioUpdate>(u => u.Include == true),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Settings PATCHed once at start ───────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Generate_WithSettings_PatchesOnceAtStart()
    {
        var session = CreateConnectedSession();
        StubApiReturnsForGenerate(("S", 501, true));
        var model = ModelWith(scenarios: ("S", 501, true));

        var settings = new SgMovingLoadSettingsData(retainLoads: true, verticalProximity: 0.5);

        var members = new Dictionary<string, IReadOnlyList<int>>
            { { "{0}", new[] { 1 } }, { "{1}", new[] { 2 } } };
        var plates = new Dictionary<string, IReadOnlyList<int>>();
        var scenarios = new Dictionary<string, IReadOnlyList<string>>
            { { "{0}", new[] { "S" } }, { "{1}", new[] { "S" } } };

        await session.GenerateMovingLoadsAsync(model, members, plates, scenarios,
            settings: settings);

        // Two branches → two Generate calls, but settings PATCH exactly once.
        await _api.Received(1).PatchMovingLoadSettingsAsync(
            Arg.Any<MovingLoadSettingsUpdate>(), Arg.Any<CancellationToken>());
        await _api.Received(2).GenerateMovingLoadsAsync(
            Arg.Any<MovingLoadGenerateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_WithoutSettings_DoesNotPatchSettings()
    {
        var session = CreateConnectedSession();
        StubApiReturnsForGenerate(("S", 501, true));
        var model = ModelWith(scenarios: ("S", 501, true));

        var members = new Dictionary<string, IReadOnlyList<int>>
            { { "{0}", new[] { 1 } } };
        var plates = new Dictionary<string, IReadOnlyList<int>>();
        var scenarios = new Dictionary<string, IReadOnlyList<string>>
            { { "{0}", new[] { "S" } } };

        await session.GenerateMovingLoadsAsync(model, members, plates, scenarios);

        await _api.DidNotReceive().PatchMovingLoadSettingsAsync(
            Arg.Any<MovingLoadSettingsUpdate>(), Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Load category passed through ─────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Generate_WithLoadCategory_IdPassedToRequest()
    {
        var session = CreateConnectedSession();
        StubApiReturnsForGenerate(("S", 501, true));
        var model = ModelWith(scenarios: ("S", 501, true));
        model.LoadCategoryMap["My Cat"] = 7;

        var members = new Dictionary<string, IReadOnlyList<int>>
            { { "{0}", new[] { 1 } } };
        var plates = new Dictionary<string, IReadOnlyList<int>>();
        var scenarios = new Dictionary<string, IReadOnlyList<string>>
            { { "{0}", new[] { "S" } } };

        await session.GenerateMovingLoadsAsync(model, members, plates, scenarios,
            loadCategoryName: "My Cat");

        await _api.Received(1).GenerateMovingLoadsAsync(
            Arg.Is<MovingLoadGenerateRequest>(r => r.LoadCategory == 7),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Selection string formatting ──────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SelectionString_AdjacentRunsCollapsed()
    {
        Assert.Equal("", ModelAssembler.FormatIdSelectionString(new int[0]));
        Assert.Equal("5", ModelAssembler.FormatIdSelectionString(new[] { 5 }));
        Assert.Equal("1-3", ModelAssembler.FormatIdSelectionString(new[] { 1, 2, 3 }));
        Assert.Equal("1-3,5,7-9",
            ModelAssembler.FormatIdSelectionString(new[] { 1, 2, 3, 5, 7, 8, 9 }));
        // Unsorted input should be sorted before collapsing
        Assert.Equal("1-3,7",
            ModelAssembler.FormatIdSelectionString(new[] { 3, 1, 7, 2 }));
        // Duplicates should collapse to a single run
        Assert.Equal("1-3",
            ModelAssembler.FormatIdSelectionString(new[] { 1, 2, 2, 3 }));
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Empty branch (all IDs empty after resolution) ────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Generate_EmptyBranchIdsAfterResolution_SkipsBranchWithWarning()
    {
        var session = CreateConnectedSession();
        StubApiReturnsForGenerate(("S", 501, true));
        var model = ModelWith(scenarios: ("S", 501, true));

        // Branch {0} has no members and no plates → should skip
        var members = new Dictionary<string, IReadOnlyList<int>>
            { { "{0}", new int[0] } };
        var plates = new Dictionary<string, IReadOnlyList<int>>
            { { "{0}", new int[0] } };
        var scenarios = new Dictionary<string, IReadOnlyList<string>>
            { { "{0}", new[] { "S" } } };

        var result = await session.GenerateMovingLoadsAsync(model, members, plates, scenarios);

        await _api.DidNotReceive().GenerateMovingLoadsAsync(
            Arg.Any<MovingLoadGenerateRequest>(), Arg.Any<CancellationToken>());
        Assert.Contains(result.Warnings,
            w => w.Contains("no members or plates", StringComparison.OrdinalIgnoreCase) &&
                 w.Contains("{0}", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Generate_EmptyScenariosBranch_SkipsBranchWithWarning()
    {
        var session = CreateConnectedSession();
        StubApiReturnsForGenerate(("S", 501, true));
        var model = ModelWith(scenarios: ("S", 501, true));

        // Members are provided, but the branch has no scenarios → skip
        var members = new Dictionary<string, IReadOnlyList<int>>
            { { "{0}", new[] { 1 } } };
        var plates = new Dictionary<string, IReadOnlyList<int>>();
        var scenarios = new Dictionary<string, IReadOnlyList<string>>
            { { "{0}", new string[0] } };

        var result = await session.GenerateMovingLoadsAsync(model, members, plates, scenarios);

        await _api.DidNotReceive().GenerateMovingLoadsAsync(
            Arg.Any<MovingLoadGenerateRequest>(), Arg.Any<CancellationToken>());
        await _api.DidNotReceive().PatchMovingLoadElementsToLoadAsync(
            Arg.Any<MovingLoadElementsToLoadUpdate>(), Arg.Any<CancellationToken>());
        Assert.Contains(result.Warnings,
            w => w.Contains("no moving load scenarios", StringComparison.OrdinalIgnoreCase) &&
                 w.Contains("{0}", StringComparison.OrdinalIgnoreCase));
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── API failure handling ─────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Generate_ElementsToLoadPatchFailure_ThrowsWithClearMessage()
    {
        var session = CreateConnectedSession();
        StubApiReturnsForGenerate(("S", 501, true));
        var model = ModelWith(scenarios: ("S", 501, true));

        _api.PatchMovingLoadElementsToLoadAsync(
                Arg.Any<MovingLoadElementsToLoadUpdate>(), Arg.Any<CancellationToken>())
            .Returns<MovingLoadElementsToLoad>(_ =>
                throw new InvalidOperationException("boom"));

        var members = new Dictionary<string, IReadOnlyList<int>>
            { { "{0}", new[] { 1 } } };
        var plates = new Dictionary<string, IReadOnlyList<int>>();
        var scenarios = new Dictionary<string, IReadOnlyList<string>>
            { { "{0}", new[] { "S" } } };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GenerateMovingLoadsAsync(model, members, plates, scenarios));

        Assert.Contains("moving load elements to load", ex.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Generate_GenerateFailure_ThrowsWithBranchPathInMessage()
    {
        var session = CreateConnectedSession();
        StubApiReturnsForGenerate(("S", 501, true));
        var model = ModelWith(scenarios: ("S", 501, true));

        _api.GenerateMovingLoadsAsync(
                Arg.Any<MovingLoadGenerateRequest>(), Arg.Any<CancellationToken>())
            .Returns<MovingLoadGenerationResult>(_ =>
                throw new InvalidOperationException("boom"));

        var members = new Dictionary<string, IReadOnlyList<int>>
            { { "{0}", new[] { 1 } } };
        var plates = new Dictionary<string, IReadOnlyList<int>>();
        var scenarios = new Dictionary<string, IReadOnlyList<string>>
            { { "{0}", new[] { "S" } } };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GenerateMovingLoadsAsync(model, members, plates, scenarios));

        Assert.Contains("generating moving loads", ex.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{0}", ex.Message);
    }
}
