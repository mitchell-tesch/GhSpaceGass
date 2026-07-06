using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class GetPlateForceTests
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
        model.NodeMap[new SgPoint3D(5, 0, 0)] = 2;
        model.NodeMap[new SgPoint3D(5, 5, 0)] = 3;
        model.NodeMap[new SgPoint3D(0, 5, 0)] = 4;
        model.LoadCaseMap["Dead Load"] = 1;
        model.LoadCaseMap["Live Load"] = 2;
        model.PlateMap[1] = new[]
        {
            new SgPoint3D(0, 0, 0), new SgPoint3D(5, 0, 0),
            new SgPoint3D(5, 5, 0), new SgPoint3D(0, 5, 0)
        };
        return model;
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgPlateElementForceData construction ──────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SgPlateElementForceData_StoresAllProperties()
    {
        var data = new SgPlateElementForceData(1, 1,
            fx: 10, fy: 20, fxy: 5,
            mx: 100, my: 200, mxy: 50,
            mxTop: 110, mxBtm: 90, myTop: 210, myBtm: 190,
            vxz: 30, vyz: 40);

        Assert.Equal(1, data.PlateId);
        Assert.Equal(1, data.LoadCaseId);
        Assert.Equal(10, data.Fx);
        Assert.Equal(20, data.Fy);
        Assert.Equal(5, data.Fxy);
        Assert.Equal(100, data.Mx);
        Assert.Equal(200, data.My);
        Assert.Equal(50, data.Mxy);
        Assert.Equal(110, data.MxTop);
        Assert.Equal(90, data.MxBtm);
        Assert.Equal(210, data.MyTop);
        Assert.Equal(190, data.MyBtm);
        Assert.Equal(30, data.Vxz);
        Assert.Equal(40, data.Vyz);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgPlateNodalForceData construction ────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SgPlateNodalForceData_StoresAllProperties()
    {
        var data = new SgPlateNodalForceData(1, 1, 2,
            fx: 5, fy: 10, fz: -15, mx: 1, my: 2, mz: 3);

        Assert.Equal(1, data.PlateId);
        Assert.Equal(1, data.LoadCaseId);
        Assert.Equal(2, data.NodeId);
        Assert.Equal(5, data.Fx);
        Assert.Equal(10, data.Fy);
        Assert.Equal(-15, data.Fz);
        Assert.Equal(1, data.Mx);
        Assert.Equal(2, data.My);
        Assert.Equal(3, data.Mz);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Session: GetPlateElementForcesAsync ───────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetPlateElementForcesAsync_ReturnsResults()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetPlateElementForcesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<PlateElementForce>
            {
                new() { Plate = 1, LoadCase = 1, Fx = 10, Fy = 20, Mx = 100 }
            });

        var result = await session.GetPlateElementForcesAsync(model);

        Assert.Single(result.Forces);
        Assert.Equal(1, result.Forces[0].PlateId);
        Assert.Equal(10, result.Forces[0].Fx);
    }

    [Fact]
    public async Task GetPlateElementForcesAsync_WithLoadCaseFilter_ResolvesIds()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetPlateElementForcesAsync(null, "1", Arg.Any<CancellationToken>())
            .Returns(new List<PlateElementForce>
            {
                new() { Plate = 1, LoadCase = 1, Fx = 10 }
            });

        var result = await session.GetPlateElementForcesAsync(model,
            loadCaseFilter: new[] { "Dead Load" });

        Assert.Single(result.Forces);
    }

    [Fact]
    public async Task GetPlateElementForcesAsync_EmptyResults_WarnsNoResults()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetPlateElementForcesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<PlateElementForce>());

        var result = await session.GetPlateElementForcesAsync(model);

        Assert.Contains(result.Warnings,
            w => w.Contains("No plate element forces", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetPlateElementForcesAsync_NotConnected_Throws()
    {
        var session = new SpaceGassSession(TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GetPlateElementForcesAsync(CreateTestModel()));
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Session: GetPlateNodalForcesAsync ─────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetPlateNodalForcesAsync_ReturnsResults()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetPlateNodalForcesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<PlateNodalForce>
            {
                new()
                {
                    Plate = 1, LoadCase = 1,
                    Node = new List<int?> { 1, 2, 3, 4 },
                    Fx = new List<float?> { 1, 2, 3, 4 },
                    Fy = new List<float?> { 5, 6, 7, 8 },
                    Fz = new List<float?> { -1, -2, -3, -4 },
                    Mx = new List<float?> { 0, 0, 0, 0 },
                    My = new List<float?> { 0, 0, 0, 0 },
                    Mz = new List<float?> { 0, 0, 0, 0 }
                }
            });

        var result = await session.GetPlateNodalForcesAsync(model);

        Assert.Equal(4, result.Forces.Count);
        Assert.Equal(1, result.Forces[0].PlateId);
        Assert.Equal(1, result.Forces[0].NodeId);
        Assert.Equal(1, result.Forces[0].Fx);
    }

    [Fact]
    public async Task GetPlateNodalForcesAsync_EmptyResults_WarnsNoResults()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetPlateNodalForcesAsync(null, null, Arg.Any<CancellationToken>())
            .Returns(new List<PlateNodalForce>());

        var result = await session.GetPlateNodalForcesAsync(model);

        Assert.Contains(result.Warnings,
            w => w.Contains("No plate nodal forces", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetPlateNodalForcesAsync_NotConnected_Throws()
    {
        var session = new SpaceGassSession(TestPort, TestInstallPath, TestTimeout,
            _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.GetPlateNodalForcesAsync(CreateTestModel()));
    }

    [Fact]
    public async Task GetPlateElementForcesAsync_WithPlateFilter_ResolvesIds()
    {
        var session = CreateConnectedSession();
        var model = CreateTestModel();

        _api.GetPlateElementForcesAsync("1", null, Arg.Any<CancellationToken>())
            .Returns(new List<PlateElementForce>
            {
                new() { Plate = 1, LoadCase = 1, Fx = 10 }
            });

        var result = await session.GetPlateElementForcesAsync(model,
            plateFilter: new[] { 1 });

        Assert.Single(result.Forces);
    }
}

