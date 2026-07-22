using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

/// <summary>
///     Tests for Moving Load (scenario Loads[] entry) construction and the assembler's
///     scenario-loads wiring step (Slice 43.1). Also exercises the "scenarios contain
///     everything" design — vehicles, pressures and travel paths are discovered by walking
///     each scenario's Loads[] rather than fed as separate lists to Assemble Model.
/// </summary>
public class MovingLoadWiringTests
{
    private const double Tolerance = 0.001;

    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ModelAssembler _assembler = new();

    // ── Helpers ───────────────────────────────────────────────────────

    private static SgMemberData MakeMember()
    {
        return new SgMemberData(
            new SgPoint3D(0, 0, 0),
            new SgPoint3D(10, 0, 0),
            new SgSectionData("Aust300", "360 UB 44.7"),
            new SgMaterialData("Aust", "STEEL"));
    }

    private static SgMovingLoadTravelPathData MakePath(string name = "P1")
    {
        return new SgMovingLoadTravelPathData(name, new[]
        {
            new SgMovingLoadStationData(new SgPoint3D(0, 0, 0)),
            new SgMovingLoadStationData(new SgPoint3D(10, 0, 0))
        });
    }

    private static SgMovingLoadVehicleData MakeUserVehicle(string name = "V1")
    {
        return new SgMovingLoadVehicleData(
            name,
            new[] { new SgVehicleWheelLoadData(fz: -50) },
            ForceUnit.KN, LengthUnit.M, MomentUnit.KNm);
    }

    private static SgMovingLoadPressureData MakePressure(string name = "PR1")
    {
        return new SgMovingLoadPressureData(name, width: 2, length: 4, pz: -10);
    }

    private void SetupApiReturns()
    {
        _api.ClearJobDataAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        _api.CreateMaterialsFromLibraryAsync(Arg.Any<List<MaterialLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MaterialLibraryCreate>)args[0];
                return input.Select((_, i) => new Material { Id = i + 1 }).ToList();
            });

        _api.CreateSectionsFromLibraryAsync(Arg.Any<List<SectionLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<SectionLibraryCreate>)args[0];
                return input.Select((_, i) => new Section { Id = i + 1 }).ToList();
            });

        _api.CreateNodesAsync(Arg.Any<List<NodeCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<NodeCreate>)args[0];
                return input.Select((n, i) => new Node { Id = i + 1, X = n.X, Y = n.Y, Z = n.Z }).ToList();
            });

        _api.CreateMembersAsync(Arg.Any<List<MemberCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MemberCreate>)args[0];
                return input.Select((m, i) => new Member { Id = i + 1, NodeA = m.NodeA, NodeB = m.NodeB }).ToList();
            });

        _api.CreateMovingLoadVehiclesFromUserAsync(
                Arg.Any<List<MovingLoadVehicleCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MovingLoadVehicleCreate>)args[0];
                return input.Select((v, i) => new MovingLoadVehicle { Id = 700 + i + 1, Name = v.Name }).ToList();
            });

        _api.CreateMovingLoadVehicleFromLibraryAsync(
                Arg.Any<MovingLoadVehicleLibraryCreate>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (MovingLoadVehicleLibraryCreate)args[0];
                return new MovingLoadVehicle
                {
                    Id = 800 + Math.Abs((input.Library + "::" + input.Name).GetHashCode() % 100),
                    Name = input.Name, Library = input.Library
                };
            });

        _api.CreateMovingLoadPressuresAsync(
                Arg.Any<List<MovingLoadPressureCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MovingLoadPressureCreate>)args[0];
                return input.Select((p, i) => new MovingLoadPressure { Id = 900 + i + 1, Name = p.Name }).ToList();
            });

        _api.CreateMovingLoadTravelPathsAsync(
                Arg.Any<List<MovingLoadTravelPathCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MovingLoadTravelPathCreate>)args[0];
                return input.Select((p, i) => new MovingLoadTravelPath { Id = 1000 + i + 1, Name = p.Name }).ToList();
            });

        _api.SetMovingLoadTravelPathStationsAsync(
                Arg.Any<int>(), Arg.Any<List<MovingLoadStation>>(), Arg.Any<CancellationToken>())
            .Returns(args => (List<MovingLoadStation>)args[1]);

        _api.CreateMovingLoadScenariosAsync(
                Arg.Any<List<MovingLoadScenarioCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MovingLoadScenarioCreate>)args[0];
                return input.Select((s, i) => new MovingLoadScenario { Id = 500 + i + 1, Name = s.Name }).ToList();
            });

        _api.SetMovingLoadScenarioLoadsAsync(
                Arg.Any<int>(), Arg.Any<List<MovingLoadScenarioLoad>>(), Arg.Any<CancellationToken>())
            .Returns(args => (List<MovingLoadScenarioLoad>)args[1]);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgMovingLoadData construction ─────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void MovingLoad_VehicleMode_StoresVehicleAndPath()
    {
        var path = MakePath();
        var vehicle = MakeUserVehicle();
        var ml = new SgMovingLoadData(travelPath: path, vehicle: vehicle);

        Assert.Same(vehicle, ml.Vehicle);
        Assert.Null(ml.Pressure);
        Assert.Same(path, ml.TravelPath);
        Assert.Equal(MovingLoadType.Vehicle, ml.LoadType);
    }

    [Fact]
    public void MovingLoad_PressureMode_StoresPressureAndPath()
    {
        var path = MakePath();
        var pressure = MakePressure();
        var ml = new SgMovingLoadData(travelPath: path, pressure: pressure);

        Assert.Null(ml.Vehicle);
        Assert.Same(pressure, ml.Pressure);
        Assert.Same(path, ml.TravelPath);
        Assert.Equal(MovingLoadType.Pressure, ml.LoadType);
    }

    [Fact]
    public void MovingLoad_MissingTravelPath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SgMovingLoadData(travelPath: null!, vehicle: MakeUserVehicle()));
    }

    [Fact]
    public void MovingLoad_BothVehicleAndPressure_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SgMovingLoadData(
                travelPath: MakePath(),
                vehicle: MakeUserVehicle(),
                pressure: MakePressure()));
    }

    [Fact]
    public void MovingLoad_NeitherVehicleNorPressure_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SgMovingLoadData(travelPath: MakePath()));
    }

    [Fact]
    public void MovingLoad_NegativeStartPosition_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SgMovingLoadData(travelPath: MakePath(), vehicle: MakeUserVehicle(),
                startPosition: -0.5));
    }

    [Fact]
    public void MovingLoad_ZeroStartPosition_Allowed()
    {
        var ml = new SgMovingLoadData(travelPath: MakePath(), vehicle: MakeUserVehicle(),
            startPosition: 0);
        Assert.Equal(0, ml.StartPosition);
    }

    [Fact]
    public void MovingLoad_AllOptionalFactors_Stored()
    {
        var ml = new SgMovingLoadData(
            travelPath: MakePath(),
            vehicle: MakeUserVehicle(),
            speed: 15,
            startPosition: 3,
            delay: 0.5,
            loadFactor: 1.2,
            laneFactor: 0.9,
            dynamicFactor: 1.3,
            generateStationaryLc: MovingLoadStationaryOption.AllLoadCases);

        Assert.Equal(15, ml.Speed);
        Assert.Equal(3, ml.StartPosition);
        Assert.Equal(0.5, ml.Delay);
        Assert.Equal(1.2, ml.LoadFactor);
        Assert.Equal(0.9, ml.LaneFactor);
        Assert.Equal(1.3, ml.DynamicFactor);
        Assert.Equal(MovingLoadStationaryOption.AllLoadCases, ml.GenerateStationaryLc);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgMovingLoadScenarioData.Loads ────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Scenario_DefaultLoads_Empty()
    {
        var scen = new SgMovingLoadScenarioData("S");
        Assert.Empty(scen.Loads);
    }

    [Fact]
    public void Scenario_LoadsProvided_Stored()
    {
        var ml1 = new SgMovingLoadData(MakePath("P1"), vehicle: MakeUserVehicle("V1"));
        var ml2 = new SgMovingLoadData(MakePath("P2"), pressure: MakePressure("PR1"));
        var scen = new SgMovingLoadScenarioData("S", loads: new[] { ml1, ml2 });

        Assert.Equal(2, scen.Loads.Count);
        Assert.Same(ml1, scen.Loads[0]);
        Assert.Same(ml2, scen.Loads[1]);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — resource discovery through scenarios ────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_ScenarioWithVehicleLoad_CreatesVehicleThroughScenario()
    {
        SetupApiReturns();

        var vehicle = MakeUserVehicle("T44");
        var scenario = new SgMovingLoadScenarioData("Truck Scen",
            loads: new[] { new SgMovingLoadData(MakePath("Lane"), vehicle: vehicle) });

        var result = await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: new[] { scenario });

        // Vehicle was pushed even though not fed to Assemble Model directly
        await _api.Received(1).CreateMovingLoadVehiclesFromUserAsync(
            Arg.Is<List<MovingLoadVehicleCreate>>(list => list.Count == 1 && list[0].Name == "T44"),
            Arg.Any<CancellationToken>());
        Assert.True(result.Model.MovingLoadVehicleMap.ContainsKey("T44"));
    }

    [Fact]
    public async Task Assemble_ScenarioWithPressureLoad_CreatesPressureThroughScenario()
    {
        SetupApiReturns();

        var pressure = MakePressure("UDL");
        var scenario = new SgMovingLoadScenarioData("Pressure Scen",
            loads: new[] { new SgMovingLoadData(MakePath("Lane"), pressure: pressure) });

        var result = await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: new[] { scenario });

        await _api.Received(1).CreateMovingLoadPressuresAsync(
            Arg.Is<List<MovingLoadPressureCreate>>(list => list.Count == 1 && list[0].Name == "UDL"),
            Arg.Any<CancellationToken>());
        Assert.True(result.Model.MovingLoadPressureMap.ContainsKey("UDL"));
    }

    [Fact]
    public async Task Assemble_ScenarioWithTravelPath_CreatesPathThroughScenario()
    {
        SetupApiReturns();

        var path = MakePath("Left Lane");
        var scenario = new SgMovingLoadScenarioData("Scen",
            loads: new[] { new SgMovingLoadData(path, vehicle: MakeUserVehicle()) });

        var result = await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: new[] { scenario });

        await _api.Received(1).CreateMovingLoadTravelPathsAsync(
            Arg.Is<List<MovingLoadTravelPathCreate>>(list => list.Count == 1 && list[0].Name == "Left Lane"),
            Arg.Any<CancellationToken>());
        Assert.True(result.Model.MovingLoadTravelPathMap.ContainsKey("Left Lane"));
    }

    [Fact]
    public async Task Assemble_MultipleLoadsSharingResources_DeduplicatesResources()
    {
        SetupApiReturns();

        var sharedVehicle = MakeUserVehicle("V1");
        var sharedPath = MakePath("Lane");
        var scenario = new SgMovingLoadScenarioData("Scen",
            loads: new[]
            {
                new SgMovingLoadData(sharedPath, vehicle: sharedVehicle, delay: 0),
                new SgMovingLoadData(sharedPath, vehicle: sharedVehicle, delay: 5)
            });

        var result = await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: new[] { scenario });

        // Vehicle created once, path created once, even though referenced twice
        await _api.Received(1).CreateMovingLoadVehiclesFromUserAsync(
            Arg.Is<List<MovingLoadVehicleCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
        await _api.Received(1).CreateMovingLoadTravelPathsAsync(
            Arg.Is<List<MovingLoadTravelPathCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — scenario Loads[] pushed via PUT ─────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_ScenarioLoads_PushedViaLoadsPut_PerScenario()
    {
        SetupApiReturns();

        var scenario = new SgMovingLoadScenarioData("Scen",
            loads: new[] { new SgMovingLoadData(MakePath(), vehicle: MakeUserVehicle()) });

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: new[] { scenario });

        await _api.Received(1).SetMovingLoadScenarioLoadsAsync(
            501, // first scenario ID from the setup
            Arg.Is<List<MovingLoadScenarioLoad>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_ScenarioLoads_VehicleId_ResolvedFromMap()
    {
        SetupApiReturns();

        var vehicle = MakeUserVehicle("V1");
        var scenario = new SgMovingLoadScenarioData("Scen",
            loads: new[] { new SgMovingLoadData(MakePath(), vehicle: vehicle) });

        var result = await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: new[] { scenario });

        var vehicleId = result.Model.MovingLoadVehicleMap["V1"];
        await _api.Received(1).SetMovingLoadScenarioLoadsAsync(
            Arg.Any<int>(),
            Arg.Is<List<MovingLoadScenarioLoad>>(list =>
                list[0].LoadType == MovingLoadType.Vehicle &&
                list[0].VehicleId == vehicleId &&
                list[0].PressureId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_ScenarioLoads_PressureId_ResolvedFromMap()
    {
        SetupApiReturns();

        var pressure = MakePressure("PR1");
        var scenario = new SgMovingLoadScenarioData("Scen",
            loads: new[] { new SgMovingLoadData(MakePath(), pressure: pressure) });

        var result = await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: new[] { scenario });

        var pressureId = result.Model.MovingLoadPressureMap["PR1"];
        await _api.Received(1).SetMovingLoadScenarioLoadsAsync(
            Arg.Any<int>(),
            Arg.Is<List<MovingLoadScenarioLoad>>(list =>
                list[0].LoadType == MovingLoadType.Pressure &&
                list[0].PressureId == pressureId &&
                list[0].VehicleId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_ScenarioLoads_TravelPathId_ResolvedFromMap()
    {
        SetupApiReturns();

        var path = MakePath("Left Lane");
        var scenario = new SgMovingLoadScenarioData("Scen",
            loads: new[] { new SgMovingLoadData(path, vehicle: MakeUserVehicle()) });

        var result = await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: new[] { scenario });

        var pathId = result.Model.MovingLoadTravelPathMap["Left Lane"];
        await _api.Received(1).SetMovingLoadScenarioLoadsAsync(
            Arg.Any<int>(),
            Arg.Is<List<MovingLoadScenarioLoad>>(list => list[0].TravelPathId == pathId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_ScenarioLoads_AllOptionalFactorsPassThrough()
    {
        SetupApiReturns();

        var scenario = new SgMovingLoadScenarioData("Scen",
            loads: new[]
            {
                new SgMovingLoadData(
                    MakePath(), vehicle: MakeUserVehicle(),
                    speed: 20, startPosition: 5, delay: 1.5,
                    loadFactor: 1.5, laneFactor: 0.9, dynamicFactor: 1.3,
                    generateStationaryLc: MovingLoadStationaryOption.AllLoadCases)
            });

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: new[] { scenario });

        await _api.Received(1).SetMovingLoadScenarioLoadsAsync(
            Arg.Any<int>(),
            Arg.Is<List<MovingLoadScenarioLoad>>(list =>
                list[0].Speed == 20 &&
                list[0].StartPosition == 5 &&
                list[0].Delay == 1.5 &&
                list[0].LoadFactor == 1.5 &&
                list[0].LaneFactor == 0.9 &&
                list[0].DynamicFactor == 1.3 &&
                list[0].GenerateStationaryLc == MovingLoadStationaryOption.AllLoadCases),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_ScenarioLoads_OmittedFactors_SendNull()
    {
        SetupApiReturns();

        var scenario = new SgMovingLoadScenarioData("Scen",
            loads: new[] { new SgMovingLoadData(MakePath(), vehicle: MakeUserVehicle()) });

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: new[] { scenario });

        await _api.Received(1).SetMovingLoadScenarioLoadsAsync(
            Arg.Any<int>(),
            Arg.Is<List<MovingLoadScenarioLoad>>(list =>
                list[0].Speed == null &&
                list[0].StartPosition == null &&
                list[0].Delay == null &&
                list[0].LoadFactor == null &&
                list[0].LaneFactor == null &&
                list[0].DynamicFactor == null &&
                list[0].GenerateStationaryLc == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MultipleScenarios_EachGetsSeparateLoadsPut()
    {
        SetupApiReturns();

        var scen1 = new SgMovingLoadScenarioData("Scen1",
            loads: new[] { new SgMovingLoadData(MakePath("P1"), vehicle: MakeUserVehicle("V1")) });
        var scen2 = new SgMovingLoadScenarioData("Scen2",
            loads: new[]
            {
                new SgMovingLoadData(MakePath("P2"), vehicle: MakeUserVehicle("V2")),
                new SgMovingLoadData(MakePath("P3"), pressure: MakePressure("PR1"))
            });

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: new[] { scen1, scen2 });

        await _api.Received(1).SetMovingLoadScenarioLoadsAsync(
            501, Arg.Is<List<MovingLoadScenarioLoad>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
        await _api.Received(1).SetMovingLoadScenarioLoadsAsync(
            502, Arg.Is<List<MovingLoadScenarioLoad>>(list => list.Count == 2),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — empty-scenario warning ──────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_EmptyScenario_WarnsButAssembles()
    {
        SetupApiReturns();

        var scenario = new SgMovingLoadScenarioData("Empty"); // no loads

        var result = await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: new[] { scenario });

        // Scenario still created
        Assert.True(result.Model.MovingLoadScenarioMap.ContainsKey("Empty"));

        // Warning fired
        Assert.Contains(result.Warnings,
            w => w.Contains("Empty", StringComparison.OrdinalIgnoreCase) &&
                 w.Contains("no moving loads", StringComparison.OrdinalIgnoreCase));

        // No PUT call for the empty scenario's Loads[]
        await _api.DidNotReceive().SetMovingLoadScenarioLoadsAsync(
            Arg.Any<int>(), Arg.Any<List<MovingLoadScenarioLoad>>(), Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — dependency ordering ─────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_ScenarioLoadsPut_HappensAfterAllResourcesCreated()
    {
        SetupApiReturns();

        var callOrder = new List<string>();
        _api.CreateMovingLoadVehiclesFromUserAsync(
                Arg.Any<List<MovingLoadVehicleCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("vehicles");
                var input = (List<MovingLoadVehicleCreate>)args[0];
                return input.Select((v, i) => new MovingLoadVehicle { Id = 700 + i + 1, Name = v.Name }).ToList();
            });
        _api.CreateMovingLoadPressuresAsync(
                Arg.Any<List<MovingLoadPressureCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("pressures");
                var input = (List<MovingLoadPressureCreate>)args[0];
                return input.Select((p, i) => new MovingLoadPressure { Id = 900 + i + 1, Name = p.Name }).ToList();
            });
        _api.CreateMovingLoadTravelPathsAsync(
                Arg.Any<List<MovingLoadTravelPathCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("paths");
                var input = (List<MovingLoadTravelPathCreate>)args[0];
                return input.Select((p, i) => new MovingLoadTravelPath { Id = 1000 + i + 1, Name = p.Name }).ToList();
            });
        _api.CreateMovingLoadScenariosAsync(
                Arg.Any<List<MovingLoadScenarioCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("scenarios");
                var input = (List<MovingLoadScenarioCreate>)args[0];
                return input.Select((s, i) => new MovingLoadScenario { Id = 500 + i + 1, Name = s.Name }).ToList();
            });
        _api.SetMovingLoadScenarioLoadsAsync(
                Arg.Any<int>(), Arg.Any<List<MovingLoadScenarioLoad>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("scenario-loads");
                return (List<MovingLoadScenarioLoad>)args[1];
            });

        var scenario = new SgMovingLoadScenarioData("S",
            loads: new[]
            {
                new SgMovingLoadData(MakePath(), vehicle: MakeUserVehicle()),
                new SgMovingLoadData(MakePath("P2"), pressure: MakePressure())
            });

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: new[] { scenario });

        var vIdx = callOrder.IndexOf("vehicles");
        var pIdx = callOrder.IndexOf("pressures");
        var tIdx = callOrder.IndexOf("paths");
        var sIdx = callOrder.IndexOf("scenarios");
        var slIdx = callOrder.IndexOf("scenario-loads");

        Assert.True(vIdx >= 0 && pIdx >= 0 && tIdx >= 0 && sIdx >= 0 && slIdx >= 0);
        Assert.True(sIdx > vIdx && sIdx > pIdx && sIdx > tIdx,
            "Scenarios must be created after their referenced resources.");
        Assert.True(slIdx > sIdx,
            "Scenario Loads[] PUT must happen after scenarios are created.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — error handling ──────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_ScenarioLoadsPutFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.SetMovingLoadScenarioLoadsAsync(
                Arg.Any<int>(), Arg.Any<List<MovingLoadScenarioLoad>>(), Arg.Any<CancellationToken>())
            .Returns<List<MovingLoadScenarioLoad>>(_ =>
                throw new InvalidOperationException("boom"));

        var scenario = new SgMovingLoadScenarioData("Scen",
            loads: new[] { new SgMovingLoadData(MakePath(), vehicle: MakeUserVehicle()) });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
                movingLoadScenarios: new[] { scenario }));

        Assert.Contains("moving load", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Scen", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
