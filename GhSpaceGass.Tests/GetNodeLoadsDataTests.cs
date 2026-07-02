using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class GetNodeLoadsDataTests
{
    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ISpaceGassApiFactory _apiFactory = Substitute.For<ISpaceGassApiFactory>();
    private readonly IProcessManager _processManager = Substitute.For<IProcessManager>();

    private SpaceGassSession CreateConnectedSession()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        _api.GetServiceInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new ServiceInfo { SpaceGassVersion = "14.5" });
        var session = new SpaceGassSession(
            34560, @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe",
            TimeSpan.FromSeconds(5), _processManager, _apiFactory);
        session.ConnectAsync().GetAwaiter().GetResult();
        _api.NewJobAsync(Arg.Any<CancellationToken>())
            .Returns(new JobStatus { State = new JobState { IsOpen = true } });
        session.NewJobAsync().GetAwaiter().GetResult();
        return session;
    }

    private static SgModelData MakeModel()
    {
        var model = new SgModelData();
        model.NodeMap[new SgPoint3D(0, 0, 0)] = 1;
        model.NodeMap[new SgPoint3D(1, 0, 0)] = 2;
        model.NodeMap[new SgPoint3D(2, 0, 0)] = 3;
        model.LoadCaseMap["Dead Load"] = 10;
        model.LoadCaseMap["Live Load"] = 20;
        model.CombinationLoadCaseMap["ULS"] = 30;
        return model;
    }

    private void SetupEmpty()
    {
        _api.ListNodeLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<NodeLoad>());
        _api.ListLumpedMassLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<LumpedMassLoad>());
        _api.ListPrescribedDisplacementsAsync(Arg.Any<CancellationToken>()).Returns(new List<PrescribedDisplacement>());
    }

    // ── Connection guard ──────────────────────────────────────────────

    [Fact]
    public async Task GetNodeLoadsData_WhenNotConnected_Throws()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            34560, @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe",
            TimeSpan.FromSeconds(5), _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetNodeLoadsDataAsync(MakeModel()));
    }

    // ── Empty results ────────────────────────────────────────────────

    [Fact]
    public async Task GetNodeLoadsData_AllEmpty_ReturnsWarning()
    {
        var session = CreateConnectedSession();
        SetupEmpty();

        var result = await session.GetNodeLoadsDataAsync(MakeModel());

        Assert.Contains(result.Warnings, w => w.Contains("No node loads"));
        Assert.Empty(result.NodeEntries);
    }

    // ── Node loads only ──────────────────────────────────────────────

    [Fact]
    public async Task GetNodeLoadsData_NodeLoads_GroupedByNode()
    {
        var session = CreateConnectedSession();
        _api.ListNodeLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<NodeLoad>
        {
            new() { Node = 1, LoadCase = 10, LoadCategory = 5, Fx = 10, Fy = -20, Fz = 0, Mx = 0, My = 0, Mz = 0 },
            new() { Node = 1, LoadCase = 20, LoadCategory = null, Fx = 5, Fy = 0, Fz = 0, Mx = 0, My = 0, Mz = 0 },
            new() { Node = 3, LoadCase = 10, LoadCategory = null, Fx = 0, Fy = -50, Fz = 0, Mx = 0, My = 0, Mz = 0 }
        });
        _api.ListLumpedMassLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<LumpedMassLoad>());
        _api.ListPrescribedDisplacementsAsync(Arg.Any<CancellationToken>()).Returns(new List<PrescribedDisplacement>());

        var result = await session.GetNodeLoadsDataAsync(MakeModel());

        // Two unique nodes: 1 and 3
        Assert.Equal(2, result.NodeEntries.Count);

        // Node 1 (index 0): 2 node loads
        Assert.Equal(1, result.NodeEntries[0].NodeId);
        Assert.Equal(new SgPoint3D(0, 0, 0), result.NodeEntries[0].Point);
        Assert.Equal(2, result.NodeEntries[0].NodeLoads.Count);
        Assert.Equal("Dead Load", result.NodeEntries[0].NodeLoads[0].LoadCaseName);
        Assert.Equal(10, result.NodeEntries[0].NodeLoads[0].LoadCaseId);
        Assert.Equal(10, result.NodeEntries[0].NodeLoads[0].Fx);
        Assert.Equal("Live Load", result.NodeEntries[0].NodeLoads[1].LoadCaseName);
        Assert.Equal(5, result.NodeEntries[0].NodeLoads[1].Fx);
        Assert.Equal(5, result.NodeEntries[0].NodeLoads[0].LoadCategoryId);
        Assert.Equal(0, result.NodeEntries[0].NodeLoads[1].LoadCategoryId);

        // Node 3 (index 1): 1 node load
        Assert.Equal(3, result.NodeEntries[1].NodeId);
        Assert.Single(result.NodeEntries[1].NodeLoads);

        // Both nodes have empty lumped mass and prescribed displacement lists
        Assert.Empty(result.NodeEntries[0].LumpedMassLoads);
        Assert.Empty(result.NodeEntries[0].PrescribedDisplacements);
        Assert.Empty(result.NodeEntries[1].LumpedMassLoads);
    }

    // ── Lumped mass loads only ────────────────────────────────────────

    [Fact]
    public async Task GetNodeLoadsData_LumpedMass_GroupedByNode()
    {
        var session = CreateConnectedSession();
        _api.ListNodeLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<NodeLoad>());
        _api.ListLumpedMassLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<LumpedMassLoad>
        {
            new() { Node = 2, LoadCase = 10, LoadCategory = null, Tmx = 100, Tmy = 100, Tmz = 100, Rmx = 0, Rmy = 0, Rmz = 0 }
        });
        _api.ListPrescribedDisplacementsAsync(Arg.Any<CancellationToken>()).Returns(new List<PrescribedDisplacement>());

        var result = await session.GetNodeLoadsDataAsync(MakeModel());

        Assert.Single(result.NodeEntries);
        Assert.Equal(2, result.NodeEntries[0].NodeId);
        Assert.Equal(new SgPoint3D(1, 0, 0), result.NodeEntries[0].Point);
        Assert.Empty(result.NodeEntries[0].NodeLoads);
        Assert.Single(result.NodeEntries[0].LumpedMassLoads);
        Assert.Equal("Dead Load", result.NodeEntries[0].LumpedMassLoads[0].LoadCaseName);
        Assert.Equal(100, result.NodeEntries[0].LumpedMassLoads[0].Tmx);
    }

    // ── Mixed load types on same node ────────────────────────────────

    [Fact]
    public async Task GetNodeLoadsData_MixedTypes_SameNode()
    {
        var session = CreateConnectedSession();
        _api.ListNodeLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<NodeLoad>
        {
            new() { Node = 1, LoadCase = 10, Fx = 10, Fy = 0, Fz = 0, Mx = 0, My = 0, Mz = 0 }
        });
        _api.ListLumpedMassLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<LumpedMassLoad>
        {
            new() { Node = 1, LoadCase = 10, Tmx = 50, Tmy = 50, Tmz = 50, Rmx = 0, Rmy = 0, Rmz = 0 }
        });
        _api.ListPrescribedDisplacementsAsync(Arg.Any<CancellationToken>()).Returns(new List<PrescribedDisplacement>
        {
            new() { Node = 1, LoadCase = 20, Tx = 0.01, Ty = 0, Tz = 0, Rx = 0, Ry = 0, Rz = 0 }
        });

        var result = await session.GetNodeLoadsDataAsync(MakeModel());

        // All on node 1 — single entry with all three populated
        Assert.Single(result.NodeEntries);
        Assert.Equal(1, result.NodeEntries[0].NodeId);
        Assert.Single(result.NodeEntries[0].NodeLoads);
        Assert.Single(result.NodeEntries[0].LumpedMassLoads);
        Assert.Single(result.NodeEntries[0].PrescribedDisplacements);
        Assert.Equal(0.01, result.NodeEntries[0].PrescribedDisplacements[0].Tx);
        Assert.Equal("Live Load", result.NodeEntries[0].PrescribedDisplacements[0].LoadCaseName);
    }

    // ── Nodes ordered by ID ──────────────────────────────────────────

    [Fact]
    public async Task GetNodeLoadsData_NodesOrderedById()
    {
        var session = CreateConnectedSession();
        _api.ListNodeLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<NodeLoad>
        {
            new() { Node = 3, LoadCase = 10, Fx = 1, Fy = 0, Fz = 0, Mx = 0, My = 0, Mz = 0 },
            new() { Node = 1, LoadCase = 10, Fx = 2, Fy = 0, Fz = 0, Mx = 0, My = 0, Mz = 0 }
        });
        _api.ListLumpedMassLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<LumpedMassLoad>());
        _api.ListPrescribedDisplacementsAsync(Arg.Any<CancellationToken>()).Returns(new List<PrescribedDisplacement>());

        var result = await session.GetNodeLoadsDataAsync(MakeModel());

        Assert.Equal(2, result.NodeEntries.Count);
        Assert.Equal(1, result.NodeEntries[0].NodeId);
        Assert.Equal(3, result.NodeEntries[1].NodeId);
    }

    // ── Combination load case name resolution ────────────────────────

    [Fact]
    public async Task GetNodeLoadsData_ResolvesCombinationLoadCaseName()
    {
        var session = CreateConnectedSession();
        _api.ListNodeLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<NodeLoad>
        {
            new() { Node = 1, LoadCase = 30, Fx = 1, Fy = 0, Fz = 0, Mx = 0, My = 0, Mz = 0 }
        });
        _api.ListLumpedMassLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<LumpedMassLoad>());
        _api.ListPrescribedDisplacementsAsync(Arg.Any<CancellationToken>()).Returns(new List<PrescribedDisplacement>());

        var result = await session.GetNodeLoadsDataAsync(MakeModel());

        Assert.Equal("ULS", result.NodeEntries[0].NodeLoads[0].LoadCaseName);
    }

    // ── Unresolved node ID warns and skips ────────────────────────────

    [Fact]
    public async Task GetNodeLoadsData_UnresolvedNodeId_WarnsAndSkips()
    {
        var session = CreateConnectedSession();
        _api.ListNodeLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<NodeLoad>
        {
            new() { Node = 99, LoadCase = 10, Fx = 1, Fy = 0, Fz = 0, Mx = 0, My = 0, Mz = 0 },
            new() { Node = 1, LoadCase = 10, Fx = 2, Fy = 0, Fz = 0, Mx = 0, My = 0, Mz = 0 }
        });
        _api.ListLumpedMassLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<LumpedMassLoad>());
        _api.ListPrescribedDisplacementsAsync(Arg.Any<CancellationToken>()).Returns(new List<PrescribedDisplacement>());

        var result = await session.GetNodeLoadsDataAsync(MakeModel());

        Assert.Single(result.NodeEntries);
        Assert.Equal(1, result.NodeEntries[0].NodeId);
        Assert.Contains(result.Warnings, w => w.Contains("99"));
    }

    // ── Null node/load case skipped ──────────────────────────────────

    [Fact]
    public async Task GetNodeLoadsData_NullNodeOrLoadCase_Skipped()
    {
        var session = CreateConnectedSession();
        _api.ListNodeLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<NodeLoad>
        {
            new() { Node = null, LoadCase = 10, Fx = 1, Fy = 0, Fz = 0, Mx = 0, My = 0, Mz = 0 },
            new() { Node = 1, LoadCase = null, Fx = 2, Fy = 0, Fz = 0, Mx = 0, My = 0, Mz = 0 },
            new() { Node = 1, LoadCase = 10, Fx = 3, Fy = 0, Fz = 0, Mx = 0, My = 0, Mz = 0 }
        });
        _api.ListLumpedMassLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<LumpedMassLoad>());
        _api.ListPrescribedDisplacementsAsync(Arg.Any<CancellationToken>()).Returns(new List<PrescribedDisplacement>());

        var result = await session.GetNodeLoadsDataAsync(MakeModel());

        Assert.Single(result.NodeEntries);
        Assert.Single(result.NodeEntries[0].NodeLoads);
        Assert.Equal(3, result.NodeEntries[0].NodeLoads[0].Fx);
    }

    // ── API error wrapping ───────────────────────────────────────────

    [Fact]
    public async Task GetNodeLoadsData_ApiError_Wrapped()
    {
        var session = CreateConnectedSession();
        _api.ListNodeLoadsAsync(Arg.Any<CancellationToken>())
            .Returns<List<NodeLoad>>(x => throw new Exception("timeout"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetNodeLoadsDataAsync(MakeModel()));

        Assert.Contains("querying node loads", ex.Message);
    }

    // ── Queries all three endpoints ──────────────────────────────────

    [Fact]
    public async Task GetNodeLoadsData_QueriesAllEndpoints()
    {
        var session = CreateConnectedSession();
        SetupEmpty();

        await session.GetNodeLoadsDataAsync(MakeModel());

        await _api.Received(1).ListNodeLoadsAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).ListLumpedMassLoadsAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).ListPrescribedDisplacementsAsync(Arg.Any<CancellationToken>());
    }
}
