using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class GetNodeDisplacementsTests
{
    private const int TestPort = 34560;
    private const string TestInstallPath = @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ISpaceGassApiFactory _apiFactory = Substitute.For<ISpaceGassApiFactory>();

    private readonly IProcessManager _processManager = Substitute.For<IProcessManager>();

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

    private static SgModelData CreateTestModel()
    {
        var model = new SgModelData();
        model.NodeMap[new SgPoint3D(0, 0, 0)] = 1;
        model.NodeMap[new SgPoint3D(10, 0, 0)] = 2;
        model.NodeMap[new SgPoint3D(20, 0, 0)] = 3;
        model.LoadCaseMap["Dead Load"] = 1;
        model.LoadCaseMap["Live Load"] = 2;
        return model;
    }

    [Fact]
    public async Task GetNodeDisplacementsAsync_ReturnsDisplacements()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNodeDisplacementsAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<NodeDisplacement>
            {
                new() { Node = 1, LoadCase = 1, Tx = 0.1f, Ty = -0.5f, Tz = 0, Rx = 0, Ry = 0, Rz = 0.01f },
                new() { Node = 2, LoadCase = 1, Tx = 0.2f, Ty = -1.0f, Tz = 0, Rx = 0, Ry = 0, Rz = 0.02f }
            });

        var result = await session.GetNodeDisplacementsAsync(model);

        Assert.Equal(2, result.Displacements.Count);
        Assert.Equal(1, result.Displacements[0].NodeId);
        Assert.Equal(0.1, result.Displacements[0].Tx, 3);
        Assert.Equal(-0.5, result.Displacements[0].Ty, 3);
        Assert.Equal(0.01, result.Displacements[0].Rz, 3);
    }

    [Fact]
    public async Task GetNodeDisplacementsAsync_WithNodeFilter_ResolvesIds()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNodeDisplacementsAsync("1", null, Arg.Any<CancellationToken>())
            .Returns(new List<NodeDisplacement>
            {
                new() { Node = 1, LoadCase = 1, Tx = 0.1f, Ty = -0.5f }
            });

        var result = await session.GetNodeDisplacementsAsync(model,
            new[] { 1 });

        Assert.Single(result.Displacements);
        await _api.Received(1).GetNodeDisplacementsAsync("1", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetNodeDisplacementsAsync_WithLoadCaseFilter_ResolvesIds()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNodeDisplacementsAsync(null, "2", Arg.Any<CancellationToken>())
            .Returns(new List<NodeDisplacement>
            {
                new() { Node = 1, LoadCase = 2, Tx = 0.3f, Ty = -1.5f }
            });

        var result = await session.GetNodeDisplacementsAsync(model,
            loadCaseFilter: new[] { "Live Load" });

        Assert.Single(result.Displacements);
        Assert.Equal(2, result.Displacements[0].LoadCaseId);
    }

    [Fact]
    public async Task GetNodeDisplacementsAsync_UnmatchedFilter_WarnsAndSkips()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNodeDisplacementsAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<NodeDisplacement>());

        var result = await session.GetNodeDisplacementsAsync(model,
            new[] { 99 },
            new[] { "Nonexistent" });

        Assert.Contains(result.Warnings, w => w.Contains("node ID 99"));
        Assert.Contains(result.Warnings, w => w.Contains("Nonexistent"));
    }

    [Fact]
    public async Task GetNodeDisplacementsAsync_EmptyResults_WarnsNoDisplacements()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNodeDisplacementsAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<NodeDisplacement>());

        var result = await session.GetNodeDisplacementsAsync(model);

        Assert.Contains(result.Warnings, w => w.Contains("No node displacements found"));
    }

    [Fact]
    public async Task GetNodeDisplacementsAsync_NullNodeOrLoadCase_SkipsRecord()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNodeDisplacementsAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<NodeDisplacement>
            {
                new() { Node = null, LoadCase = 1, Tx = 0.1f },
                new() { Node = 1, LoadCase = null, Tx = 0.2f },
                new() { Node = 1, LoadCase = 1, Tx = 0.3f }
            });

        var result = await session.GetNodeDisplacementsAsync(model);

        Assert.Single(result.Displacements);
        Assert.Equal(0.3, result.Displacements[0].Tx, 3);
    }

    [Fact]
    public async Task GetNodeDisplacementsAsync_WhenNotConnected_Throws()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.GetNodeDisplacementsAsync(new SgModelData()));
    }

    [Fact]
    public async Task GetNodeDisplacementsAsync_ApiThrows_WrapsException()
    {
        var session = CreateConnectedSession();
        _api.GetNodeDisplacementsAsync(null, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("API fail"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GetNodeDisplacementsAsync(new SgModelData()));
        Assert.Contains("querying node displacements", ex.Message);
    }

    [Fact]
    public async Task GetNodeDisplacementsAsync_NullableValues_DefaultToZero()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetNodeDisplacementsAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<NodeDisplacement>
            {
                new() { Node = 1, LoadCase = 1 } // all nullable values null
            });

        var result = await session.GetNodeDisplacementsAsync(model);

        Assert.Single(result.Displacements);
        Assert.Equal(0, result.Displacements[0].Tx);
        Assert.Equal(0, result.Displacements[0].Ty);
        Assert.Equal(0, result.Displacements[0].Tz);
        Assert.Equal(0, result.Displacements[0].Rx);
        Assert.Equal(0, result.Displacements[0].Ry);
        Assert.Equal(0, result.Displacements[0].Rz);
    }
}

public class GetMemberEndForcesTests
{
    private const int TestPort = 34560;
    private const string TestInstallPath = @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ISpaceGassApiFactory _apiFactory = Substitute.For<ISpaceGassApiFactory>();

    private readonly IProcessManager _processManager = Substitute.For<IProcessManager>();

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

    private static SgModelData CreateTestModel()
    {
        var model = new SgModelData();
        model.NodeMap[new SgPoint3D(0, 0, 0)] = 1;
        model.NodeMap[new SgPoint3D(10, 0, 0)] = 2;
        model.NodeMap[new SgPoint3D(20, 0, 0)] = 3;
        model.MemberMap[1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0));
        model.MemberMap[2] = (new SgPoint3D(10, 0, 0), new SgPoint3D(20, 0, 0));
        model.LoadCaseMap["Dead Load"] = 1;
        return model;
    }

    [Fact]
    public async Task GetMemberEndForcesAsync_FlattensPerEndRecords()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberEndForcesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberEndForce>
            {
                new()
                {
                    Member = 1, LoadCase = 1,
                    Node = new List<int?> { 1, 2 },
                    Fx = new List<float?> { 10f, -10f },
                    Fy = new List<float?> { 5f, -5f },
                    Fz = new List<float?> { 0f, 0f },
                    Mx = new List<float?> { 0f, 0f },
                    My = new List<float?> { 0f, 0f },
                    Mz = new List<float?> { 20f, -20f }
                }
            });

        var result = await session.GetMemberEndForcesAsync(model);

        // 1 API record with 2 ends → 2 domain records
        Assert.Equal(2, result.EndForces.Count);

        // Node A
        Assert.Equal(1, result.EndForces[0].MemberId);
        Assert.Equal(1, result.EndForces[0].NodeId);
        Assert.Equal(10, result.EndForces[0].Fx, 3);
        Assert.Equal(5, result.EndForces[0].Fy, 3);
        Assert.Equal(20, result.EndForces[0].Mz, 3);

        // Node B
        Assert.Equal(1, result.EndForces[1].MemberId);
        Assert.Equal(2, result.EndForces[1].NodeId);
        Assert.Equal(-10, result.EndForces[1].Fx, 3);
        Assert.Equal(-5, result.EndForces[1].Fy, 3);
        Assert.Equal(-20, result.EndForces[1].Mz, 3);
    }

    [Fact]
    public async Task GetMemberEndForcesAsync_WithMemberFilter_ResolvesGeometryToId()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberEndForcesAsync("1", null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberEndForce>
            {
                new()
                {
                    Member = 1, LoadCase = 1,
                    Node = new List<int?> { 1, 2 },
                    Fx = new List<float?> { 10f, -10f },
                    Fy = new List<float?> { 0f, 0f },
                    Fz = new List<float?> { 0f, 0f },
                    Mx = new List<float?> { 0f, 0f },
                    My = new List<float?> { 0f, 0f },
                    Mz = new List<float?> { 0f, 0f }
                }
            });

        var result = await session.GetMemberEndForcesAsync(model,
            new[] { 1 });

        Assert.Equal(2, result.EndForces.Count);
        await _api.Received(1).GetMemberEndForcesAsync("1", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMemberEndForcesAsync_UnmatchedMemberFilter_WarnsAndSkips()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberEndForcesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberEndForce>());

        var result = await session.GetMemberEndForcesAsync(model,
            new[] { 99 });

        Assert.Contains(result.Warnings, w => w.Contains("does not match any model member"));
    }

    [Fact]
    public async Task GetMemberEndForcesAsync_WithLoadCaseFilter_ResolvesIds()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberEndForcesAsync(null, "1", Arg.Any<CancellationToken>())
            .Returns(new List<MemberEndForce>
            {
                new()
                {
                    Member = 1, LoadCase = 1,
                    Node = new List<int?> { 1, 2 },
                    Fx = new List<float?> { 5f, -5f },
                    Fy = new List<float?> { 0f, 0f },
                    Fz = new List<float?> { 0f, 0f },
                    Mx = new List<float?> { 0f, 0f },
                    My = new List<float?> { 0f, 0f },
                    Mz = new List<float?> { 0f, 0f }
                }
            });

        var result = await session.GetMemberEndForcesAsync(model,
            loadCaseFilter: new[] { "Dead Load" });

        Assert.Equal(2, result.EndForces.Count);
    }

    [Fact]
    public async Task GetMemberEndForcesAsync_EmptyResults_WarnsNoEndForces()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberEndForcesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberEndForce>());

        var result = await session.GetMemberEndForcesAsync(model);

        Assert.Contains(result.Warnings, w => w.Contains("No member end forces found"));
    }

    [Fact]
    public async Task GetMemberEndForcesAsync_NullMemberOrLoadCase_SkipsRecord()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberEndForcesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberEndForce>
            {
                new() { Member = null, LoadCase = 1, Node = new List<int?> { 1 } },
                new() { Member = 1, LoadCase = null, Node = new List<int?> { 1 } },
                new()
                {
                    Member = 1, LoadCase = 1,
                    Node = new List<int?> { 1, 2 },
                    Fx = new List<float?> { 1f, 2f },
                    Fy = new List<float?> { 0f, 0f },
                    Fz = new List<float?> { 0f, 0f },
                    Mx = new List<float?> { 0f, 0f },
                    My = new List<float?> { 0f, 0f },
                    Mz = new List<float?> { 0f, 0f }
                }
            });

        var result = await session.GetMemberEndForcesAsync(model);

        Assert.Equal(2, result.EndForces.Count); // only the valid record, flattened to 2 ends
    }

    [Fact]
    public async Task GetMemberEndForcesAsync_WhenNotConnected_Throws()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.GetMemberEndForcesAsync(new SgModelData()));
    }

    [Fact]
    public async Task GetMemberEndForcesAsync_ApiThrows_WrapsException()
    {
        var session = CreateConnectedSession();
        _api.GetMemberEndForcesAsync(null, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("API fail"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GetMemberEndForcesAsync(new SgModelData()));
        Assert.Contains("querying member end forces", ex.Message);
    }

    [Fact]
    public async Task GetMemberEndForcesAsync_NullableForceValues_DefaultToZero()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetMemberEndForcesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<MemberEndForce>
            {
                new()
                {
                    Member = 1, LoadCase = 1,
                    Node = new List<int?> { 1 }
                    // All force lists null
                }
            });

        var result = await session.GetMemberEndForcesAsync(model);

        Assert.Single(result.EndForces);
        Assert.Equal(0, result.EndForces[0].Fx);
        Assert.Equal(0, result.EndForces[0].Fy);
        Assert.Equal(0, result.EndForces[0].Fz);
        Assert.Equal(0, result.EndForces[0].Mx);
        Assert.Equal(0, result.EndForces[0].My);
        Assert.Equal(0, result.EndForces[0].Mz);
    }
}