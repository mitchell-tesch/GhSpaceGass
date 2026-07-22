using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Services;

/// <summary>
///     Thin wrapper over SpaceGassApiClient exposing only the calls needed by the session.
/// </summary>
internal interface ISpaceGassApi : IDisposable
{
    // ── Service ───────────────────────────────────────────────────
    Task<ServiceInfo> GetServiceInfoAsync(CancellationToken ct = default);

    // ── Job lifecycle ─────────────────────────────────────────────
    Task<JobStatus> NewJobAsync(CancellationToken ct = default);
    Task<JobStatus> OpenJobAsync(string filePath, JobForceAccessOption? forceOption = null, CancellationToken ct = default);
    Task SaveJobAsync(string filePath, CancellationToken ct = default);
    Task<JobStatus> GetJobStatusAsync(CancellationToken ct = default);
    Task CloseJobAsync(CancellationToken ct = default);

    // ── Job data ──────────────────────────────────────────────────
    Task ClearJobDataAsync(CancellationToken ct = default);

    // ── Structure (bulk create) ───────────────────────────────────
    Task<List<Material>> CreateMaterialsFromLibraryAsync(
        List<MaterialLibraryCreate> materials, CancellationToken ct = default);

    Task<List<Material>> CreateMaterialsFromUserAsync(
        List<MaterialUserCreate> materials, CancellationToken ct = default);

    Task<List<Section>> CreateSectionsFromLibraryAsync(
        List<SectionLibraryCreate> sections, CancellationToken ct = default);

    Task<List<Section>> CreateSectionsFromUserAsync(
        List<SectionUserCreate> sections, CancellationToken ct = default);

    Task<List<Node>> CreateNodesAsync(
        List<NodeCreate> nodes, CancellationToken ct = default);

    Task<List<Member>> CreateMembersAsync(
        List<MemberCreate> members, CancellationToken ct = default);

    Task<List<MemberOffset>> CreateMemberOffsetsAsync(
        List<MemberOffsetCreate> offsets, CancellationToken ct = default);

    Task<List<Plate>> CreatePlatesAsync(
        List<PlateCreate> plates, CancellationToken ct = default);

    Task<List<PlatePressureLoad>> CreatePlatePressureLoadsAsync(
        List<PlatePressureLoadCreate> loads, CancellationToken ct = default);

    Task<List<ThermalLoad>> CreateThermalLoadsAsync(
        List<ThermalLoadCreate> loads, CancellationToken ct = default);

    // ── Restraints (bulk create) ───────────────────────────────────
    Task<List<NodeRestraint>> CreateNodeRestraintsAsync(
        List<NodeRestraintCreate> restraints, CancellationToken ct = default);

    // ── Constraints (bulk create) ──────────────────────────────────
    Task<List<NodeConstraint>> CreateNodeConstraintsAsync(
        List<NodeConstraintCreate> constraints, CancellationToken ct = default);

    // ── Loads (bulk create) ─────────────────────────────────────────
    Task<List<LoadCase>> CreateLoadCasesAsync(
        List<LoadCaseCreate> loadCases, CancellationToken ct = default);

    Task<List<LoadCategory>> CreateLoadCategoriesAsync(
        List<LoadCategoryCreate> loadCategories, CancellationToken ct = default);

    Task<List<NodeLoad>> CreateNodeLoadsAsync(
        List<NodeLoadCreate> nodeLoads, CancellationToken ct = default);

    Task<List<MemberDistributedLoad>> CreateMemberDistributedLoadsAsync(
        List<MemberDistributedLoadCreate> loads, CancellationToken ct = default);

    Task<List<MemberDistributedMoment>> CreateMemberDistributedMomentsAsync(
        List<MemberDistributedMomentCreate> moments, CancellationToken ct = default);

    Task<List<MemberConcentratedLoad>> CreateMemberConcentratedLoadsAsync(
        List<MemberConcentratedLoadCreate> loads, CancellationToken ct = default);

    Task<List<MemberPrestressLoad>> CreateMemberPrestressLoadsAsync(
        List<MemberPrestressLoadCreate> loads, CancellationToken ct = default);

    Task<List<SelfWeightLoad>> CreateSelfWeightLoadsAsync(
        List<SelfWeightLoadCreate> loads, CancellationToken ct = default);

    Task<List<LoadCase>> CreateCombinationLoadCasesAsync(
        List<CombinationLoadCaseCreate> combinations, CancellationToken ct = default);

    Task<List<LumpedMassLoad>> CreateLumpedMassLoadsAsync(
        List<LumpedMassLoadCreate> loads, CancellationToken ct = default);

    Task<List<PrescribedDisplacement>> CreatePrescribedDisplacementsAsync(
        List<PrescribedDisplacementCreate> displacements, CancellationToken ct = default);

    Task<List<MovingLoadScenario>> CreateMovingLoadScenariosAsync(
        List<MovingLoadScenarioCreate> scenarios, CancellationToken ct = default);

    Task<List<MovingLoadVehicle>> CreateMovingLoadVehiclesFromUserAsync(
        List<MovingLoadVehicleCreate> vehicles, CancellationToken ct = default);

    Task<MovingLoadVehicle> CreateMovingLoadVehicleFromLibraryAsync(
        MovingLoadVehicleLibraryCreate vehicle, CancellationToken ct = default);

    Task<List<MovingLoadPressure>> CreateMovingLoadPressuresAsync(
        List<MovingLoadPressureCreate> pressures, CancellationToken ct = default);

    Task<List<MovingLoadTravelPath>> CreateMovingLoadTravelPathsAsync(
        List<MovingLoadTravelPathCreate> travelPaths, CancellationToken ct = default);

    Task<List<MovingLoadStation>> SetMovingLoadTravelPathStationsAsync(
        int travelPathId, List<MovingLoadStation> stations, CancellationToken ct = default);

    Task<List<MovingLoadScenarioLoad>> SetMovingLoadScenarioLoadsAsync(
        int scenarioId, List<MovingLoadScenarioLoad> loads, CancellationToken ct = default);

    Task<MovingLoadSettings> PatchMovingLoadSettingsAsync(
        MovingLoadSettingsUpdate settings, CancellationToken ct = default);

    Task<MovingLoadElementsToLoad> PatchMovingLoadElementsToLoadAsync(
        MovingLoadElementsToLoadUpdate elements, CancellationToken ct = default);

    Task<MovingLoadScenario> PatchMovingLoadScenarioAsync(
        int scenarioId, MovingLoadScenarioUpdate update, CancellationToken ct = default);

    Task<List<MovingLoadScenario>> ListMovingLoadScenariosAsync(CancellationToken ct = default);

    Task<MovingLoadGenerationResult> GenerateMovingLoadsAsync(
        MovingLoadGenerateRequest request, CancellationToken ct = default);

    // ── Analysis ────────────────────────────────────────────────────
    Task<AnalysisRun> RunStaticAnalysisAsync(
        StaticSettingsUpdate? settings = null, CancellationToken ct = default);

    Task<AnalysisRun> RunNonlinearAnalysisAsync(
        StaticSettingsUpdate? settings = null, CancellationToken ct = default);

    Task<AnalysisRun> RunBucklingAnalysisAsync(
        BucklingSettingsUpdate? settings = null, CancellationToken ct = default);

    Task<AnalysisRun> RunDynamicFrequencyAnalysisAsync(
        DynamicFrequencySettingsUpdate? settings = null, CancellationToken ct = default);

    /// <summary>
    ///     Gets the current status of an analysis run by its ID.
    ///     Used to poll for completion after RunStaticAnalysisAsync returns.
    /// </summary>
    Task<AnalysisRun> GetAnalysisRunAsync(Guid runId, CancellationToken ct = default);


    // ── Results (queries) ────────────────────────────────────────────
    Task<List<NodeReaction>> GetNodeReactionsAsync(
        string? nodes = null, string? loadCases = null, CancellationToken ct = default);

    Task<List<NodeDisplacement>> GetNodeDisplacementsAsync(
        string? nodes = null, string? loadCases = null, CancellationToken ct = default);

    Task<List<MemberEndForce>> GetMemberEndForcesAsync(
        string? members = null, string? loadCases = null, CancellationToken ct = default);

    Task<List<MemberIntermediateForce>> GetMemberIntermediateForcesAsync(
        string? members = null, string? loadCases = null, CancellationToken ct = default);

    Task<List<MemberIntermediateDisplacement>> GetMemberIntermediateDisplacementsAsync(
        string? members = null, string? loadCases = null, CancellationToken ct = default);

    // ── Buckling results (queries) ────────────────────────────────────
    Task<List<BucklingLoadFactor>> GetBucklingLoadFactorsAsync(
        CancellationToken ct = default);

    Task<List<BucklingEffectiveLength>> GetBucklingEffectiveLengthsAsync(
        string? members = null, string? modes = null, string? loadCases = null, CancellationToken ct = default);

    // ── Dynamic frequency results (queries) ────────────────────────────
    Task<List<NaturalFrequency>> GetNaturalFrequenciesAsync(
        string? modes = null, string? loadCases = null, CancellationToken ct = default);

    Task<List<ModeShape>> GetModeShapesAsync(
        string? modes = null, string? nodes = null, string? loadCases = null, CancellationToken ct = default);

    // ── Plate results (queries) ────────────────────────────────────────
    Task<List<PlateElementForce>> GetPlateElementForcesAsync(
        string? plates = null, string? loadCases = null, CancellationToken ct = default);

    Task<List<PlateNodalForce>> GetPlateNodalForcesAsync(
        string? plates = null, string? loadCases = null, CancellationToken ct = default);

    // ── Steel design (queries) ─────────────────────────────────────────
    Task<List<SteelCheckSummary>> GetSteelMemberCheckSummaryAsync(
        string? members = null, CancellationToken ct = default);

    // ── Job Info ─────────────────────────────────────────────────────
    Task<JobStatus> GetFullJobStatusAsync(CancellationToken ct = default);
    Task<JobHeadings> UpdateHeadingsAsync(JobHeadingsUpdate headings, CancellationToken ct = default);

    // ── Structure (list/query) ────────────────────────────────────────
    Task<List<Node>> ListNodesAsync(CancellationToken ct = default);
    Task<List<Member>> ListMembersAsync(CancellationToken ct = default);
    Task<List<Section>> ListSectionsAsync(CancellationToken ct = default);
    Task<List<Material>> ListMaterialsAsync(CancellationToken ct = default);
    Task<List<Plate>> ListPlatesAsync(CancellationToken ct = default);

    // ── Loads (list/query) ────────────────────────────────────────────
    Task<List<LoadCase>> ListLoadCasesAsync(CancellationToken ct = default);
    Task<List<LoadCategory>> ListLoadCategoriesAsync(CancellationToken ct = default);
    Task<List<LoadCaseGroup>> ListLoadCaseGroupsAsync(CancellationToken ct = default);
    Task<List<SelfWeightLoad>> ListSelfWeightLoadsAsync(CancellationToken ct = default);
    Task<List<NodeLoad>> ListNodeLoadsAsync(CancellationToken ct = default);
    Task<List<LumpedMassLoad>> ListLumpedMassLoadsAsync(CancellationToken ct = default);
    Task<List<PrescribedDisplacement>> ListPrescribedDisplacementsAsync(CancellationToken ct = default);
    Task<List<MemberConcentratedLoad>> ListMemberConcentratedLoadsAsync(CancellationToken ct = default);
    Task<List<MemberDistributedLoad>> ListMemberDistributedLoadsAsync(CancellationToken ct = default);
    Task<List<MemberDistributedMoment>> ListMemberDistributedMomentsAsync(CancellationToken ct = default);
    Task<List<MemberPrestressLoad>> ListMemberPrestressLoadsAsync(CancellationToken ct = default);
    Task<List<ThermalLoad>> ListThermalLoadsAsync(CancellationToken ct = default);
    Task<List<PlatePressureLoad>> ListPlatePressureLoadsAsync(CancellationToken ct = default);
}

/// <summary>
///     Factory that creates an <see cref="ISpaceGassApi" /> instance for a given base URL.
/// </summary>
internal interface ISpaceGassApiFactory
{
    ISpaceGassApi Create(string baseUrl);
}