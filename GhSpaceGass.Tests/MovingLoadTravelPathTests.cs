using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

/// <summary>
///     Tests for moving load travel paths (Slice 42).
///     Covers <see cref="SgMovingLoadStationData"/> and <see cref="SgMovingLoadTravelPathData"/>
///     construction, and <see cref="ModelAssembler"/> mapping to the SpaceGass bulk-path create +
///     per-path stations PUT endpoints, deduplication, ID map population, dependency ordering,
///     and API error handling.
/// </summary>
public class MovingLoadTravelPathTests
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

    private static SgMovingLoadStationData Station(double x, double y = 0, double z = 0, double? radius = null)
    {
        return new SgMovingLoadStationData(new SgPoint3D(x, y, z), radius);
    }

    private static SgMovingLoadVehicleData WrapperVehicleForPathTests =>
        new("__wrap_vehicle", new[] { new SgVehicleWheelLoadData(fz: -1) },
            ForceUnit.KN, LengthUnit.M, MomentUnit.KNm);

    /// <summary>
    ///     Wraps a collection of travel paths into a single scenario (each with a placeholder
    ///     vehicle) so Slice 42's path-focused tests still exercise the assembler through the
    ///     "scenarios contain everything" entry point introduced in Slice 43.1. Every wrapped
    ///     scenario Load reuses the same placeholder vehicle so vehicle-side APIs receive at
    ///     most one call (reference-equal dedup in the assembler).
    /// </summary>
    private static SgMovingLoadScenarioData[] ScenariosFor(IEnumerable<SgMovingLoadTravelPathData> paths)
    {
        var placeholder = WrapperVehicleForPathTests;
        var loads = paths
            .Select(p => new SgMovingLoadData(p, vehicle: placeholder))
            .ToArray();
        return new[] { new SgMovingLoadScenarioData("__wrap_scen", loads: loads) };
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

        _api.CreateMovingLoadVehiclesFromUserAsync(
                Arg.Any<List<MovingLoadVehicleCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MovingLoadVehicleCreate>)args[0];
                return input.Select((v, i) => new MovingLoadVehicle { Id = 700 + i + 1, Name = v.Name }).ToList();
            });

        _api.CreateMovingLoadScenariosAsync(
                Arg.Any<List<MovingLoadScenarioCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MovingLoadScenarioCreate>)args[0];
                return input.Select((s, i) => new MovingLoadScenario { Id = 500 + i + 1, Name = s.Name })
                    .ToList();
            });

        _api.SetMovingLoadScenarioLoadsAsync(
                Arg.Any<int>(), Arg.Any<List<MovingLoadScenarioLoad>>(), Arg.Any<CancellationToken>())
            .Returns(args => (List<MovingLoadScenarioLoad>)args[1]);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgMovingLoadStationData construction ──────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Station_StoresPositionAndRadius()
    {
        var s = new SgMovingLoadStationData(new SgPoint3D(1, 2, 3), radius: 25);

        Assert.Equal(new SgPoint3D(1, 2, 3), s.Position);
        Assert.Equal(25, s.Radius);
    }

    [Fact]
    public void Station_RadiusOmitted_IsNull()
    {
        var s = new SgMovingLoadStationData(new SgPoint3D(0, 0, 0));

        Assert.Null(s.Radius);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgMovingLoadTravelPathData construction ───────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void TravelPath_StoresNameAndStations()
    {
        var stations = new[] { Station(0), Station(5), Station(10) };
        var p = new SgMovingLoadTravelPathData("Left Lane", stations);

        Assert.Equal("Left Lane", p.Name);
        Assert.Equal(3, p.Stations.Count);
        Assert.Equal(new SgPoint3D(5, 0, 0), p.Stations[1].Position);
        Assert.Equal("Left Lane", p.Key);
    }

    [Fact]
    public void TravelPath_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SgMovingLoadTravelPathData("", new[] { Station(0), Station(5) }));
    }

    [Fact]
    public void TravelPath_LessThanTwoStations_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SgMovingLoadTravelPathData("P", new[] { Station(0) }));
        Assert.Throws<ArgumentException>(() =>
            new SgMovingLoadTravelPathData("P", Array.Empty<SgMovingLoadStationData>()));
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — no travel paths ─────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_NoTravelPaths_DoesNotCallApi()
    {
        SetupApiReturns();

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance);

        await _api.DidNotReceive().CreateMovingLoadTravelPathsAsync(
            Arg.Any<List<MovingLoadTravelPathCreate>>(), Arg.Any<CancellationToken>());
        await _api.DidNotReceive().SetMovingLoadTravelPathStationsAsync(
            Arg.Any<int>(), Arg.Any<List<MovingLoadStation>>(), Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — bulk create paths + PUT stations per path ────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_SinglePath_BulkCreatesPathThenPutsStations()
    {
        SetupApiReturns();

        var path = new SgMovingLoadTravelPathData("Left Lane",
            new[] { Station(0), Station(5), Station(10) });

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(new[] { path }));

        await _api.Received(1).CreateMovingLoadTravelPathsAsync(
            Arg.Is<List<MovingLoadTravelPathCreate>>(list =>
                list.Count == 1 && list[0].Name == "Left Lane"),
            Arg.Any<CancellationToken>());

        await _api.Received(1).SetMovingLoadTravelPathStationsAsync(
            1001,
            Arg.Is<List<MovingLoadStation>>(list => list.Count == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MultiplePaths_PutStationsPerPath()
    {
        SetupApiReturns();

        var paths = new[]
        {
            new SgMovingLoadTravelPathData("Left", new[] { Station(0), Station(5) }),
            new SgMovingLoadTravelPathData("Right", new[] { Station(0, 3), Station(5, 3), Station(10, 3) }),
            new SgMovingLoadTravelPathData("Centre", new[] { Station(0, 1.5), Station(10, 1.5) })
        };

        var result = await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(paths));

        await _api.Received(1).CreateMovingLoadTravelPathsAsync(
            Arg.Is<List<MovingLoadTravelPathCreate>>(list => list.Count == 3),
            Arg.Any<CancellationToken>());

        await _api.Received(3).SetMovingLoadTravelPathStationsAsync(
            Arg.Any<int>(),
            Arg.Any<List<MovingLoadStation>>(),
            Arg.Any<CancellationToken>());

        Assert.Equal(3, result.Model.MovingLoadTravelPathMap.Count);
        Assert.Equal(1001, result.Model.MovingLoadTravelPathMap["Left"]);
        Assert.Equal(1002, result.Model.MovingLoadTravelPathMap["Right"]);
        Assert.Equal(1003, result.Model.MovingLoadTravelPathMap["Centre"]);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — station data preserved verbatim ─────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_PathStations_SendXyzVerbatim_NoNodeKey()
    {
        SetupApiReturns();

        // Even when station points coincide with existing model nodes, the assembler must
        // send absolute X, Y, Z coordinates and leave NodeKey null — the SpaceGass NodeKey
        // is the station's own identifier within the travel path, not a link to model nodes.
        var path = new SgMovingLoadTravelPathData("Left",
            new[]
            {
                Station(0, 0, 0),     // coincides with member start node
                Station(10, 0, 0)     // coincides with member end node
            });

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(new[] { path }));

        await _api.Received(1).SetMovingLoadTravelPathStationsAsync(
            Arg.Any<int>(),
            Arg.Is<List<MovingLoadStation>>(list =>
                list[0].NodeKey == null &&
                list[0].X == 0 && list[0].Y == 0 && list[0].Z == 0 &&
                list[1].NodeKey == null &&
                list[1].X == 10 && list[1].Y == 0 && list[1].Z == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_PathStations_RadiusPassedThrough()
    {
        SetupApiReturns();

        var path = new SgMovingLoadTravelPathData("Curved",
            new[]
            {
                Station(0),                            // first station — radius meaningless
                Station(5, radius: 20),                // arc radius 20 between station 1 and 2
                Station(10, 5, radius: 30)             // arc radius 30 between station 2 and 3
            });

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(new[] { path }));

        await _api.Received(1).SetMovingLoadTravelPathStationsAsync(
            Arg.Any<int>(),
            Arg.Is<List<MovingLoadStation>>(list =>
                list[0].Radius == null &&
                list[1].Radius == 20 &&
                list[2].Radius == 30),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_PathStations_OrderPreserved()
    {
        SetupApiReturns();

        var path = new SgMovingLoadTravelPathData("Ordered",
            new[]
            {
                Station(0), Station(2.5), Station(5), Station(7.5), Station(10)
            });

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(new[] { path }));

        await _api.Received(1).SetMovingLoadTravelPathStationsAsync(
            Arg.Any<int>(),
            Arg.Is<List<MovingLoadStation>>(list =>
                list.Count == 5 &&
                list[0].X == 0 &&
                list[1].X == 2.5 &&
                list[2].X == 5 &&
                list[3].X == 7.5 &&
                list[4].X == 10),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — deduplication ───────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_DuplicatePathNames_Deduplicated()
    {
        SetupApiReturns();

        var paths = new[]
        {
            new SgMovingLoadTravelPathData("Lane", new[] { Station(0), Station(5) }),
            new SgMovingLoadTravelPathData("Lane", new[] { Station(0, 3), Station(5, 3) }),
            new SgMovingLoadTravelPathData("Other", new[] { Station(0), Station(5) })
        };

        var result = await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(paths));

        await _api.Received(1).CreateMovingLoadTravelPathsAsync(
            Arg.Is<List<MovingLoadTravelPathCreate>>(list => list.Count == 2),
            Arg.Any<CancellationToken>());

        Assert.Contains(result.Warnings,
            w => w.Contains("moving load travel path", StringComparison.OrdinalIgnoreCase) &&
                 w.Contains("Lane", StringComparison.OrdinalIgnoreCase));
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — dependency ordering ─────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_TravelPaths_CreatedBeforeScenarios_AndAfterVehicles()
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
        _api.SetMovingLoadTravelPathStationsAsync(
                Arg.Any<int>(), Arg.Any<List<MovingLoadStation>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("path-stations");
                return (List<MovingLoadStation>)args[1];
            });
        _api.CreateMovingLoadScenariosAsync(
                Arg.Any<List<MovingLoadScenarioCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("scenarios");
                var input = (List<MovingLoadScenarioCreate>)args[0];
                return input.Select((s, i) => new MovingLoadScenario { Id = 500 + i + 1, Name = s.Name }).ToList();
            });

        var placeholderVehicle = new SgMovingLoadVehicleData("V",
            new[] { new SgVehicleWheelLoadData(fz: -50) },
            ForceUnit.KN, LengthUnit.M, MomentUnit.KNm);
        var placeholderPressure = new SgMovingLoadPressureData("P", width: 2, length: 4, pz: -10);
        var path = new SgMovingLoadTravelPathData("Left", new[] { Station(0), Station(10) });

        // A single scenario references a vehicle, a pressure and a travel path via its Loads[].
        // The assembler must create resources first, then the scenario, then PUT scenario loads
        // — this is what the dependency ordering test verifies.
        var scenario = new SgMovingLoadScenarioData("Scen", loads: new[]
        {
            new SgMovingLoadData(path, vehicle: placeholderVehicle),
            new SgMovingLoadData(path, pressure: placeholderPressure)
        });

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: new[] { scenario });

        var vehiclesIdx = callOrder.IndexOf("vehicles");
        var pressuresIdx = callOrder.IndexOf("pressures");
        var pathsIdx = callOrder.IndexOf("paths");
        var pathStationsIdx = callOrder.IndexOf("path-stations");
        var scenariosIdx = callOrder.IndexOf("scenarios");

        Assert.True(vehiclesIdx >= 0 && pressuresIdx >= 0 && pathsIdx >= 0 &&
                    pathStationsIdx >= 0 && scenariosIdx >= 0);
        Assert.True(pathsIdx > vehiclesIdx, "paths must be created after vehicles");
        Assert.True(pathsIdx > pressuresIdx, "paths must be created after pressures");
        Assert.True(pathStationsIdx > pathsIdx, "path stations must be PUT after paths are created");
        Assert.True(scenariosIdx > pathStationsIdx, "scenarios must be created after path stations are set");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — error handling ──────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_TravelPathBulkCreateFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreateMovingLoadTravelPathsAsync(
                Arg.Any<List<MovingLoadTravelPathCreate>>(), Arg.Any<CancellationToken>())
            .Returns<List<MovingLoadTravelPath>>(_ =>
                throw new InvalidOperationException("boom"));

        var path = new SgMovingLoadTravelPathData("P", new[] { Station(0), Station(5) });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
                movingLoadScenarios: ScenariosFor(new[] { path })));

        Assert.Contains("moving load travel path", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Assemble_TravelPathStationsPutFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.SetMovingLoadTravelPathStationsAsync(
                Arg.Any<int>(), Arg.Any<List<MovingLoadStation>>(), Arg.Any<CancellationToken>())
            .Returns<List<MovingLoadStation>>(_ =>
                throw new InvalidOperationException("boom"));

        var path = new SgMovingLoadTravelPathData("P", new[] { Station(0), Station(5) });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
                movingLoadScenarios: ScenariosFor(new[] { path })));

        Assert.Contains("moving load travel path", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("station", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Assemble_TravelPathBulkCountMismatch_Throws()
    {
        SetupApiReturns();
        _api.CreateMovingLoadTravelPathsAsync(
                Arg.Any<List<MovingLoadTravelPathCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<MovingLoadTravelPath>());

        var path = new SgMovingLoadTravelPathData("P", new[] { Station(0), Station(5) });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
                movingLoadScenarios: ScenariosFor(new[] { path })));

        Assert.Contains("moving load travel path", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
