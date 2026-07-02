using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class GetPlateLoadsDataTests
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
        model.PlateMap[1] = new[] { new SgPoint3D(0, 0, 0), new SgPoint3D(1, 0, 0), new SgPoint3D(1, 1, 0), new SgPoint3D(0, 1, 0) };
        model.PlateMap[2] = new[] { new SgPoint3D(2, 0, 0), new SgPoint3D(3, 0, 0), new SgPoint3D(2.5, 1, 0) };
        model.LoadCaseMap["Dead Load"] = 10;
        model.LoadCaseMap["Live Load"] = 20;
        return model;
    }

    private void SetupEmpty()
    {
        _api.ListPlatePressureLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<PlatePressureLoad>());
        _api.ListThermalLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<ThermalLoad>());
    }

    // ── Connection guard ──────────────────────────────────────────────

    [Fact]
    public async Task GetPlateLoadsData_WhenNotConnected_Throws()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            34560, @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe",
            TimeSpan.FromSeconds(5), _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetPlateLoadsDataAsync(MakeModel()));
    }

    // ── Empty results ────────────────────────────────────────────────

    [Fact]
    public async Task GetPlateLoadsData_AllEmpty_ReturnsWarning()
    {
        var session = CreateConnectedSession();
        SetupEmpty();

        var result = await session.GetPlateLoadsDataAsync(MakeModel());

        Assert.Contains(result.Warnings, w => w.Contains("No plate loads"));
        Assert.Empty(result.PlateEntries);
    }

    // ── Pressure loads ───────────────────────────────────────────────

    [Fact]
    public async Task GetPlateLoadsData_PressureLoads_GroupedByPlate()
    {
        var session = CreateConnectedSession();
        _api.ListPlatePressureLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<PlatePressureLoad>
        {
            new() { Plate = 1, LoadCase = 10, LoadCategory = null, Px = 0, Py = 0, Pz = -5, Axes = LoadAxes.Local },
            new() { Plate = 1, LoadCase = 20, LoadCategory = 3, Px = 0, Py = 0, Pz = -10, Axes = LoadAxes.GlobalInclined },
            new() { Plate = 2, LoadCase = 10, LoadCategory = null, Px = 0, Py = 0, Pz = -8, Axes = LoadAxes.Local }
        });
        _api.ListThermalLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<ThermalLoad>());

        var result = await session.GetPlateLoadsDataAsync(MakeModel());

        Assert.Equal(2, result.PlateEntries.Count);

        // Plate 1: quad, 2 pressure loads
        Assert.Equal(1, result.PlateEntries[0].PlateId);
        Assert.Equal(4, result.PlateEntries[0].CornerPoints.Length);
        Assert.Equal(2, result.PlateEntries[0].PressureLoads.Count);
        Assert.Equal(10, result.PlateEntries[0].PressureLoads[0].LoadCaseId);
        Assert.Equal("Dead Load", result.PlateEntries[0].PressureLoads[0].LoadCaseName);
        Assert.Equal(-5, result.PlateEntries[0].PressureLoads[0].Pz);
        Assert.Equal("Local", result.PlateEntries[0].PressureLoads[0].Axes);
        Assert.Equal("Global Inclined", result.PlateEntries[0].PressureLoads[1].Axes);
        Assert.Equal(3, result.PlateEntries[0].PressureLoads[1].LoadCategoryId);

        // Plate 2: tri, 1 pressure load
        Assert.Equal(2, result.PlateEntries[1].PlateId);
        Assert.Equal(3, result.PlateEntries[1].CornerPoints.Length);
        Assert.Single(result.PlateEntries[1].PressureLoads);
        Assert.Empty(result.PlateEntries[1].ThermalLoads);
    }

    // ── Thermal loads (plate only) ───────────────────────────────────

    [Fact]
    public async Task GetPlateLoadsData_ThermalLoads_PlateOnly()
    {
        var session = CreateConnectedSession();
        _api.ListPlatePressureLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<PlatePressureLoad>());
        _api.ListThermalLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<ThermalLoad>
        {
            new() { ElementId = 1, ElementType = ThermalElementType.Plate, LoadCase = 10, ThermalLoadProp = 25, YThermalGradient = 3, ZThermalGradient = 0 },
            new() { ElementId = 5, ElementType = ThermalElementType.Member, LoadCase = 10, ThermalLoadProp = 30, YThermalGradient = 0, ZThermalGradient = 0 }
        });

        var result = await session.GetPlateLoadsDataAsync(MakeModel());

        Assert.Single(result.PlateEntries);
        Assert.Equal(1, result.PlateEntries[0].PlateId);
        Assert.Single(result.PlateEntries[0].ThermalLoads);
        Assert.Equal(25, result.PlateEntries[0].ThermalLoads[0].Temperature);
        Assert.Equal(3, result.PlateEntries[0].ThermalLoads[0].YGradient);
        Assert.Empty(result.PlateEntries[0].PressureLoads);
    }

    // ── Mixed types on same plate ────────────────────────────────────

    [Fact]
    public async Task GetPlateLoadsData_MixedTypes_SamePlate()
    {
        var session = CreateConnectedSession();
        _api.ListPlatePressureLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<PlatePressureLoad>
        {
            new() { Plate = 1, LoadCase = 10, Pz = -5, Axes = LoadAxes.Local }
        });
        _api.ListThermalLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<ThermalLoad>
        {
            new() { ElementId = 1, ElementType = ThermalElementType.Plate, LoadCase = 20, ThermalLoadProp = 15, YThermalGradient = 0, ZThermalGradient = 0 }
        });

        var result = await session.GetPlateLoadsDataAsync(MakeModel());

        Assert.Single(result.PlateEntries);
        Assert.Single(result.PlateEntries[0].PressureLoads);
        Assert.Single(result.PlateEntries[0].ThermalLoads);
    }

    // ── Plates ordered by ID ─────────────────────────────────────────

    [Fact]
    public async Task GetPlateLoadsData_PlatesOrderedById()
    {
        var session = CreateConnectedSession();
        _api.ListPlatePressureLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<PlatePressureLoad>
        {
            new() { Plate = 2, LoadCase = 10, Pz = -1, Axes = LoadAxes.Local },
            new() { Plate = 1, LoadCase = 10, Pz = -2, Axes = LoadAxes.Local }
        });
        _api.ListThermalLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<ThermalLoad>());

        var result = await session.GetPlateLoadsDataAsync(MakeModel());

        Assert.Equal(2, result.PlateEntries.Count);
        Assert.Equal(1, result.PlateEntries[0].PlateId);
        Assert.Equal(2, result.PlateEntries[1].PlateId);
    }

    // ── Unresolved plate ID warns and skips ───────────────────────────

    [Fact]
    public async Task GetPlateLoadsData_UnresolvedPlateId_WarnsAndSkips()
    {
        var session = CreateConnectedSession();
        _api.ListPlatePressureLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<PlatePressureLoad>
        {
            new() { Plate = 99, LoadCase = 10, Pz = -1, Axes = LoadAxes.Local },
            new() { Plate = 1, LoadCase = 10, Pz = -2, Axes = LoadAxes.Local }
        });
        _api.ListThermalLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<ThermalLoad>());

        var result = await session.GetPlateLoadsDataAsync(MakeModel());

        Assert.Single(result.PlateEntries);
        Assert.Contains(result.Warnings, w => w.Contains("99"));
    }

    // ── API error wrapping ───────────────────────────────────────────

    [Fact]
    public async Task GetPlateLoadsData_ApiError_Wrapped()
    {
        var session = CreateConnectedSession();
        _api.ListPlatePressureLoadsAsync(Arg.Any<CancellationToken>())
            .Returns<List<PlatePressureLoad>>(x => throw new Exception("timeout"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.GetPlateLoadsDataAsync(MakeModel()));

        Assert.Contains("querying plate pressure loads", ex.Message);
    }

    // ── Queries both endpoints ───────────────────────────────────────

    [Fact]
    public async Task GetPlateLoadsData_QueriesBothEndpoints()
    {
        var session = CreateConnectedSession();
        SetupEmpty();

        await session.GetPlateLoadsDataAsync(MakeModel());

        await _api.Received(1).ListPlatePressureLoadsAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).ListThermalLoadsAsync(Arg.Any<CancellationToken>());
    }

    // ── Null plate/load case skipped ─────────────────────────────────

    [Fact]
    public async Task GetPlateLoadsData_NullPlateOrLoadCase_Skipped()
    {
        var session = CreateConnectedSession();
        _api.ListPlatePressureLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<PlatePressureLoad>
        {
            new() { Plate = null, LoadCase = 10, Pz = -1, Axes = LoadAxes.Local },
            new() { Plate = 1, LoadCase = null, Pz = -2, Axes = LoadAxes.Local },
            new() { Plate = 1, LoadCase = 10, Pz = -3, Axes = LoadAxes.Local }
        });
        _api.ListThermalLoadsAsync(Arg.Any<CancellationToken>()).Returns(new List<ThermalLoad>());

        var result = await session.GetPlateLoadsDataAsync(MakeModel());

        Assert.Single(result.PlateEntries);
        Assert.Single(result.PlateEntries[0].PressureLoads);
        Assert.Equal(-3, result.PlateEntries[0].PressureLoads[0].Pz);
    }
}
