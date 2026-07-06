using System.Reflection;
using SpaceGassApi;
using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Services;

internal class SpaceGassApiClientFactory : ISpaceGassApiFactory
{
    public ISpaceGassApi Create(string baseUrl)
    {
        var client = SpaceGassApiClient.CreateClient(baseUrl);
        return new SpaceGassApiWrapper(client);
    }
}

internal class SpaceGassApiWrapper : ISpaceGassApi
{
    private readonly SpaceGassApiClient _client;

    public SpaceGassApiWrapper(SpaceGassApiClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    ///     Extracts succeeded items from a bulk result and throws if any errors were reported.
    /// </summary>
    private static List<T> ExtractBulkResult<T>(
        List<T>? succeeded, List<BulkError>? errors, string entityType)
    {
        if (errors is { Count: > 0 })
        {
            var messages = errors
                .Take(5)
                .Select(e => e.Error ?? "Unknown error")
                .ToList();
            var summary = string.Join("; ", messages);
            if (errors.Count > 5)
                summary += $"; ... and {errors.Count - 5} more";
            throw new InvalidOperationException(
                $"Bulk create {entityType} had {errors.Count} error(s): {summary}");
        }

        return succeeded ?? new List<T>();
    }

    public async Task<ServiceInfo> GetServiceInfoAsync(CancellationToken ct = default)
    {
        return (await _client.Service.Info.GetAsync(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<JobStatus> NewJobAsync(CancellationToken ct = default)
    {
        return (await _client.Job.New.PostAsync(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<JobStatus> OpenJobAsync(string filePath, JobForceAccessOption? forceOption = null, CancellationToken ct = default)
    {
        var request = new OpenJobRequest { FilePath = filePath };
        if (forceOption != null && forceOption != JobForceAccessOption.None)
            request.ForceOption = forceOption;
        return (await _client.Job.Open.PostAsync(request, cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task SaveJobAsync(string filePath, CancellationToken ct = default)
    {
        await _client.Job.Save.PostAsync(
            new SaveJobRequest { FilePath = filePath },
            cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<JobStatus> GetJobStatusAsync(CancellationToken ct = default)
    {
        return (await _client.Job.Status.GetAsync(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task CloseJobAsync(CancellationToken ct = default)
    {
        await _client.Job.Close.PostAsync(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task ClearJobDataAsync(CancellationToken ct = default)
    {
        await _client.Job.Data.DeleteAsync(config => { config.QueryParameters.Force = true; }, ct)
            .ConfigureAwait(false);
    }

    public async Task<List<Material>> CreateMaterialsFromLibraryAsync(
        List<MaterialLibraryCreate> materials, CancellationToken ct = default)
    {
        var result = await _client.Job.Structure.Materials.Library.Bulk.PostAsync(
            materials, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "library materials");
    }

    public async Task<List<Material>> CreateMaterialsFromUserAsync(
        List<MaterialUserCreate> materials, CancellationToken ct = default)
    {
        var result = await _client.Job.Structure.Materials.Bulk.PostAsync(
            materials, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "custom materials");
    }

    public async Task<List<Section>> CreateSectionsFromLibraryAsync(
        List<SectionLibraryCreate> sections, CancellationToken ct = default)
    {
        var result = await _client.Job.Structure.Sections.Library.Bulk.PostAsync(
            sections, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "library sections");
    }

    public async Task<List<Section>> CreateSectionsFromUserAsync(
        List<SectionUserCreate> sections, CancellationToken ct = default)
    {
        var result = await _client.Job.Structure.Sections.Bulk.PostAsync(
            sections, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "custom sections");
    }

    public async Task<List<Node>> CreateNodesAsync(
        List<NodeCreate> nodes, CancellationToken ct = default)
    {
        var result = await _client.Job.Structure.Nodes.Bulk.PostAsync(
            nodes, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "nodes");
    }

    public async Task<List<Member>> CreateMembersAsync(
        List<MemberCreate> members, CancellationToken ct = default)
    {
        var result = await _client.Job.Structure.Members.Bulk.PostAsync(
            members, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "members");
    }

    public async Task<List<MemberOffset>> CreateMemberOffsetsAsync(
        List<MemberOffsetCreate> offsets, CancellationToken ct = default)
    {
        var result = await _client.Job.Structure.MemberOffsets.Bulk.PostAsync(
            offsets, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "member offsets");
    }

    public async Task<List<Plate>> CreatePlatesAsync(
        List<PlateCreate> plates, CancellationToken ct = default)
    {
        var result = await _client.Job.Structure.Plates.Bulk.PostAsync(
            plates, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "plates");
    }

    public async Task<List<PlatePressureLoad>> CreatePlatePressureLoadsAsync(
        List<PlatePressureLoadCreate> loads, CancellationToken ct = default)
    {
        var result = await _client.Job.Loads.PlatePressureLoads.Bulk.PostAsync(
            loads, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "plate pressure loads");
    }

    public async Task<List<ThermalLoad>> CreateThermalLoadsAsync(
        List<ThermalLoadCreate> loads, CancellationToken ct = default)
    {
        var result = await _client.Job.Loads.ThermalLoads.Bulk.PostAsync(
            loads, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "thermal loads");
    }

    public async Task<List<NodeRestraint>> CreateNodeRestraintsAsync(
        List<NodeRestraintCreate> restraints, CancellationToken ct = default)
    {
        var result = await _client.Job.Structure.NodeRestraints.Bulk.PostAsync(
            restraints, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "restraints");
    }

    public async Task<List<NodeConstraint>> CreateNodeConstraintsAsync(
        List<NodeConstraintCreate> constraints, CancellationToken ct = default)
    {
        var result = await _client.Job.Structure.NodeConstraints.Bulk.PostAsync(
            constraints, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "constraints");
    }

    public async Task<List<LoadCase>> CreateLoadCasesAsync(
        List<LoadCaseCreate> loadCases, CancellationToken ct = default)
    {
        var result = await _client.Job.Loads.LoadCases.Bulk.PostAsync(
            loadCases, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "load cases");
    }

    public async Task<List<LoadCategory>> CreateLoadCategoriesAsync(
        List<LoadCategoryCreate> loadCategories, CancellationToken ct = default)
    {
        var result = await _client.Job.Loads.LoadCategories.Bulk.PostAsync(
            loadCategories, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "load categories");
    }

    public async Task<List<NodeLoad>> CreateNodeLoadsAsync(
        List<NodeLoadCreate> nodeLoads, CancellationToken ct = default)
    {
        var result = await _client.Job.Loads.NodeLoads.Bulk.PostAsync(
            nodeLoads, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "node loads");
    }

    public async Task<List<MemberDistributedLoad>> CreateMemberDistributedLoadsAsync(
        List<MemberDistributedLoadCreate> loads, CancellationToken ct = default)
    {
        var result = await _client.Job.Loads.MemberDistributedLoads.Bulk.PostAsync(
            loads, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "distributed loads");
    }

    public async Task<List<MemberDistributedMoment>> CreateMemberDistributedMomentsAsync(
        List<MemberDistributedMomentCreate> moments, CancellationToken ct = default)
    {
        var result = await _client.Job.Loads.MemberDistributedMoments.Bulk.PostAsync(
            moments, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "distributed moments");
    }

    public async Task<List<MemberConcentratedLoad>> CreateMemberConcentratedLoadsAsync(
        List<MemberConcentratedLoadCreate> loads, CancellationToken ct = default)
    {
        var result = await _client.Job.Loads.MemberConcentratedLoads.Bulk.PostAsync(
            loads, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "concentrated loads");
    }

    public async Task<List<MemberPrestressLoad>> CreateMemberPrestressLoadsAsync(
        List<MemberPrestressLoadCreate> loads, CancellationToken ct = default)
    {
        var result = await _client.Job.Loads.MemberPrestressLoads.Bulk.PostAsync(
            loads, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "prestress loads");
    }

    public async Task<List<SelfWeightLoad>> CreateSelfWeightLoadsAsync(
        List<SelfWeightLoadCreate> loads, CancellationToken ct = default)
    {
        var result = await _client.Job.Loads.SelfWeightLoads.Bulk.PostAsync(
            loads, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "self-weight loads");
    }

    public async Task<List<LoadCase>> CreateCombinationLoadCasesAsync(
        List<CombinationLoadCaseCreate> combinations, CancellationToken ct = default)
    {
        var result = await _client.Job.Loads.CombinationLoadCases.Bulk.PostAsync(
            combinations, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "combination load cases");
    }

    public async Task<List<LumpedMassLoad>> CreateLumpedMassLoadsAsync(
        List<LumpedMassLoadCreate> loads, CancellationToken ct = default)
    {
        var result = await _client.Job.Loads.LumpedMassLoads.Bulk.PostAsync(
            loads, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "lumped mass loads");
    }

    public async Task<List<PrescribedDisplacement>> CreatePrescribedDisplacementsAsync(
        List<PrescribedDisplacementCreate> displacements, CancellationToken ct = default)
    {
        var result = await _client.Job.Loads.NodeDisplacements.Bulk.PostAsync(
            displacements, cancellationToken: ct).ConfigureAwait(false);
        return ExtractBulkResult(result?.Succeeded, result?.Errors, "prescribed displacements");
    }

    public async Task<AnalysisRun> RunStaticAnalysisAsync(
        StaticSettingsUpdate? settings = null, CancellationToken ct = default)
    {
        var body = settings ?? new StaticSettingsUpdate();
        return (await _client.Job.Analysis.Static.RunLinear.PostAsync(
            body, cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<AnalysisRun> RunNonlinearAnalysisAsync(
        StaticSettingsUpdate? settings = null, CancellationToken ct = default)
    {
        var body = settings ?? new StaticSettingsUpdate();
        return (await _client.Job.Analysis.Static.RunNonLinear.PostAsync(
            body, cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<AnalysisRun> RunBucklingAnalysisAsync(
        BucklingSettingsUpdate? settings = null, CancellationToken ct = default)
    {
        var body = settings ?? new BucklingSettingsUpdate();
        return (await _client.Job.Analysis.Buckling.Run.PostAsync(
            body, cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<AnalysisRun> RunDynamicFrequencyAnalysisAsync(
        DynamicFrequencySettingsUpdate? settings = null, CancellationToken ct = default)
    {
        var body = settings ?? new DynamicFrequencySettingsUpdate();
        return (await _client.Job.Analysis.DynamicFrequency.Run.PostAsync(
            body, cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<AnalysisRun> GetAnalysisRunAsync(Guid runId, CancellationToken ct = default)
    {
        return (await _client.Job.Analysis.Runs[runId].GetAsync(
            cancellationToken: ct).ConfigureAwait(false))!;
    }


    public async Task<List<NodeReaction>> GetNodeReactionsAsync(
        string? nodes = null, string? loadCases = null, CancellationToken ct = default)
    {
        var result = await _client.Job.Query.Analysis.Static.NodeReactions.GetAsync(
            config =>
            {
                if (nodes != null)
                    config.QueryParameters.Nodes = nodes;
                if (loadCases != null)
                    config.QueryParameters.LoadCases = loadCases;
            },
            ct).ConfigureAwait(false);
        return result?.Results ?? new List<NodeReaction>();
    }

    public async Task<List<NodeDisplacement>> GetNodeDisplacementsAsync(
        string? nodes = null, string? loadCases = null, CancellationToken ct = default)
    {
        var result = await _client.Job.Query.Analysis.Static.NodeDisplacements.GetAsync(
            config =>
            {
                if (nodes != null)
                    config.QueryParameters.Nodes = nodes;
                if (loadCases != null)
                    config.QueryParameters.LoadCases = loadCases;
            },
            ct).ConfigureAwait(false);
        return result?.Results ?? new List<NodeDisplacement>();
    }

    public async Task<List<MemberEndForce>> GetMemberEndForcesAsync(
        string? members = null, string? loadCases = null, CancellationToken ct = default)
    {
        var result = await _client.Job.Query.Analysis.Static.MemberEndForces.GetAsync(
            config =>
            {
                if (members != null)
                    config.QueryParameters.Members = members;
                if (loadCases != null)
                    config.QueryParameters.LoadCases = loadCases;
            },
            ct).ConfigureAwait(false);
        return result?.Results ?? new List<MemberEndForce>();
    }

    public async Task<List<MemberIntermediateForce>> GetMemberIntermediateForcesAsync(
        string? members = null, string? loadCases = null, CancellationToken ct = default)
    {
        var result = await _client.Job.Query.Analysis.Static.MemberIntermediateForces.GetAsync(
            config =>
            {
                if (members != null)
                    config.QueryParameters.Members = members;
                if (loadCases != null)
                    config.QueryParameters.LoadCases = loadCases;
            },
            ct).ConfigureAwait(false);
        return result?.Results ?? new List<MemberIntermediateForce>();
    }

    public async Task<List<MemberIntermediateDisplacement>> GetMemberIntermediateDisplacementsAsync(
        string? members = null, string? loadCases = null, CancellationToken ct = default)
    {
        var result = await _client.Job.Query.Analysis.Static.MemberIntermediateDisplacements.GetAsync(
            config =>
            {
                if (members != null)
                    config.QueryParameters.Members = members;
                if (loadCases != null)
                    config.QueryParameters.LoadCases = loadCases;
            },
            ct).ConfigureAwait(false);
        return result?.Results ?? new List<MemberIntermediateDisplacement>();
    }

    public async Task<List<BucklingLoadFactor>> GetBucklingLoadFactorsAsync(
        CancellationToken ct = default)
    {
        var result = await _client.Job.Query.Analysis.Buckling.LoadFactors.GetAsync(
            cancellationToken: ct).ConfigureAwait(false);
        return result?.Results ?? new List<BucklingLoadFactor>();
    }

    public async Task<List<BucklingEffectiveLength>> GetBucklingEffectiveLengthsAsync(
        string? members = null, string? modes = null, string? loadCases = null, CancellationToken ct = default)
    {
        var result = await _client.Job.Query.Analysis.Buckling.MemberEffectiveLengths.GetAsync(
            config =>
            {
                if (members != null)
                    config.QueryParameters.Members = members;
                if (modes != null)
                    config.QueryParameters.Modes = modes;
                if (loadCases != null)
                    config.QueryParameters.LoadCases = loadCases;
            },
            ct).ConfigureAwait(false);
        return result?.Results ?? new List<BucklingEffectiveLength>();
    }

    public async Task<List<NaturalFrequency>> GetNaturalFrequenciesAsync(
        string? modes = null, string? loadCases = null, CancellationToken ct = default)
    {
        var result = await _client.Job.Query.Analysis.Dynamic.NaturalFrequencies.GetAsync(
            config =>
            {
                if (modes != null)
                    config.QueryParameters.Modes = modes;
                if (loadCases != null)
                    config.QueryParameters.LoadCases = loadCases;
            },
            ct).ConfigureAwait(false);
        return result?.Results ?? new List<NaturalFrequency>();
    }

    public async Task<List<ModeShape>> GetModeShapesAsync(
        string? modes = null, string? nodes = null, string? loadCases = null, CancellationToken ct = default)
    {
        var result = await _client.Job.Query.Analysis.Dynamic.ModeShapes.GetAsync(
            config =>
            {
                if (modes != null)
                    config.QueryParameters.Modes = modes;
                if (nodes != null)
                    config.QueryParameters.Nodes = nodes;
                if (loadCases != null)
                    config.QueryParameters.LoadCases = loadCases;
            },
            ct).ConfigureAwait(false);
        return result?.Results ?? new List<ModeShape>();
    }

    public async Task<List<PlateElementForce>> GetPlateElementForcesAsync(
        string? plates = null, string? loadCases = null, CancellationToken ct = default)
    {
        var result = await _client.Job.Query.Analysis.Static.PlateElementForces.GetAsync(
            config =>
            {
                if (plates != null)
                    config.QueryParameters.Plates = plates;
                if (loadCases != null)
                    config.QueryParameters.LoadCases = loadCases;
            },
            ct).ConfigureAwait(false);
        return result?.Results ?? new List<PlateElementForce>();
    }

    public async Task<List<PlateNodalForce>> GetPlateNodalForcesAsync(
        string? plates = null, string? loadCases = null, CancellationToken ct = default)
    {
        var result = await _client.Job.Query.Analysis.Static.PlateNodalForces.GetAsync(
            config =>
            {
                if (plates != null)
                    config.QueryParameters.Plates = plates;
                if (loadCases != null)
                    config.QueryParameters.LoadCases = loadCases;
            },
            ct).ConfigureAwait(false);
        return result?.Results ?? new List<PlateNodalForce>();
    }

    public async Task<JobStatus> GetFullJobStatusAsync(CancellationToken ct = default)
    {
        return (await _client.Job.Status.GetAsync(cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<JobHeadings> UpdateHeadingsAsync(JobHeadingsUpdate headings, CancellationToken ct = default)
    {
        return (await _client.Job.Headings.PatchAsync(headings, cancellationToken: ct).ConfigureAwait(false))!;
    }

    public async Task<List<Node>> ListNodesAsync(CancellationToken ct = default)
    {
        return await _client.Job.Structure.Nodes.GetAsync(cancellationToken: ct).ConfigureAwait(false)
               ?? new List<Node>();
    }

    public async Task<List<Member>> ListMembersAsync(CancellationToken ct = default)
    {
        return await _client.Job.Structure.Members.GetAsync(cancellationToken: ct).ConfigureAwait(false)
               ?? new List<Member>();
    }

    public async Task<List<Section>> ListSectionsAsync(CancellationToken ct = default)
    {
        return await _client.Job.Structure.Sections.GetAsync(cancellationToken: ct).ConfigureAwait(false)
               ?? new List<Section>();
    }

    public async Task<List<Material>> ListMaterialsAsync(CancellationToken ct = default)
    {
        return await _client.Job.Structure.Materials.GetAsync(cancellationToken: ct).ConfigureAwait(false)
               ?? new List<Material>();
    }

    public async Task<List<Plate>> ListPlatesAsync(CancellationToken ct = default)
    {
        return await _client.Job.Structure.Plates.GetAsync(cancellationToken: ct).ConfigureAwait(false)
               ?? new List<Plate>();
    }

    public async Task<List<LoadCase>> ListLoadCasesAsync(CancellationToken ct = default)
    {
        return await _client.Job.Loads.LoadCases.GetAsync(
           config =>
           {
               config.QueryParameters.ExpandAsExpandOption = ExpandOption.All;
           },
           ct).ConfigureAwait(false) 
               ?? new List<LoadCase>();
    }

    public async Task<List<LoadCategory>> ListLoadCategoriesAsync(CancellationToken ct = default)
    {
        return await _client.Job.Loads.LoadCategories.GetAsync(cancellationToken: ct).ConfigureAwait(false)
               ?? new List<LoadCategory>();
    }

    public async Task<List<LoadCaseGroup>> ListLoadCaseGroupsAsync(CancellationToken ct = default)
    {
        return await _client.Job.Loads.LoadCaseGroups.GetAsync(cancellationToken: ct).ConfigureAwait(false)
               ?? new List<LoadCaseGroup>();
    }

    public async Task<List<SelfWeightLoad>> ListSelfWeightLoadsAsync(CancellationToken ct = default)
    {
        return await _client.Job.Loads.SelfWeightLoads.GetAsync(cancellationToken: ct).ConfigureAwait(false)
               ?? new List<SelfWeightLoad>();
    }

    public async Task<List<NodeLoad>> ListNodeLoadsAsync(CancellationToken ct = default)
    {
        return await _client.Job.Loads.NodeLoads.GetAsync(cancellationToken: ct).ConfigureAwait(false)
               ?? new List<NodeLoad>();
    }

    public async Task<List<LumpedMassLoad>> ListLumpedMassLoadsAsync(CancellationToken ct = default)
    {
        return await _client.Job.Loads.LumpedMassLoads.GetAsync(cancellationToken: ct).ConfigureAwait(false)
               ?? new List<LumpedMassLoad>();
    }

    public async Task<List<PrescribedDisplacement>> ListPrescribedDisplacementsAsync(CancellationToken ct = default)
    {
        return await _client.Job.Loads.NodeDisplacements.GetAsync(cancellationToken: ct).ConfigureAwait(false)
               ?? new List<PrescribedDisplacement>();
    }

    public async Task<List<MemberConcentratedLoad>> ListMemberConcentratedLoadsAsync(CancellationToken ct = default)
    {
        return await _client.Job.Loads.MemberConcentratedLoads.GetAsync(cancellationToken: ct).ConfigureAwait(false)
               ?? new List<MemberConcentratedLoad>();
    }

    public async Task<List<MemberDistributedLoad>> ListMemberDistributedLoadsAsync(CancellationToken ct = default)
    {
        return await _client.Job.Loads.MemberDistributedLoads.GetAsync(cancellationToken: ct).ConfigureAwait(false)
               ?? new List<MemberDistributedLoad>();
    }

    public async Task<List<MemberDistributedMoment>> ListMemberDistributedMomentsAsync(CancellationToken ct = default)
    {
        return await _client.Job.Loads.MemberDistributedMoments.GetAsync(cancellationToken: ct).ConfigureAwait(false)
               ?? new List<MemberDistributedMoment>();
    }

    public async Task<List<MemberPrestressLoad>> ListMemberPrestressLoadsAsync(CancellationToken ct = default)
    {
        return await _client.Job.Loads.MemberPrestressLoads.GetAsync(cancellationToken: ct).ConfigureAwait(false)
               ?? new List<MemberPrestressLoad>();
    }

    public async Task<List<ThermalLoad>> ListThermalLoadsAsync(CancellationToken ct = default)
    {
        return await _client.Job.Loads.ThermalLoads.GetAsync(cancellationToken: ct).ConfigureAwait(false)
               ?? new List<ThermalLoad>();
    }

    public async Task<List<PlatePressureLoad>> ListPlatePressureLoadsAsync(CancellationToken ct = default)
    {
        return await _client.Job.Loads.PlatePressureLoads.GetAsync(cancellationToken: ct).ConfigureAwait(false)
               ?? new List<PlatePressureLoad>();
    }

    public void Dispose()
    {
        // SpaceGassApiClient extends BaseRequestBuilder which holds a non-public
        // RequestAdapter property. The HttpClientRequestAdapter is IDisposable
        // and owns the underlying HttpClient — disposing it prevents socket leaks.
        try
        {
            var prop = typeof(Microsoft.Kiota.Abstractions.BaseRequestBuilder)
                .GetProperty("RequestAdapter",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            (prop?.GetValue(_client) as IDisposable)?.Dispose();
        }
        catch
        {
            // Non-fatal — if Kiota internals change, we degrade to the original
            // no-op behaviour (GC will eventually collect the HttpClient).
        }
    }
}