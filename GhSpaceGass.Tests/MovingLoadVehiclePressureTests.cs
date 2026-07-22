using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

/// <summary>
///     Tests for moving load vehicles and pressures (Slice 41).
///     Covers <see cref="SgVehicleWheelLoadData"/>, <see cref="SgMovingLoadVehicleData"/> and
///     <see cref="SgMovingLoadPressureData"/> construction, and <see cref="ModelAssembler"/>
///     mapping to the SpaceGass bulk + library endpoints, deduplication, ID map population,
///     dependency ordering, and API error handling.
/// </summary>
public class MovingLoadVehiclePressureTests
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

    private static SgVehicleWheelLoadData Wheel(
        double x = 0, double y = 0,
        double fx = 0, double fy = 0, double fz = 0,
        double mx = 0, double my = 0, double mz = 0)
    {
        return new SgVehicleWheelLoadData(x, y, fx, fy, fz, mx, my, mz);
    }

    private static SgMovingLoadTravelPathData WrapperPath =>
        new("__wrap_path", new[]
        {
            new SgMovingLoadStationData(new SgPoint3D(0, 0, 0)),
            new SgMovingLoadStationData(new SgPoint3D(1, 0, 0))
        });

    private static SgMovingLoadPressureData WrapperPressureForVehicleTests =>
        new("__wrap_pressure", width: 1, length: 1, pz: -1);

    private static SgMovingLoadVehicleData WrapperVehicleForPressureTests =>
        new("__wrap_vehicle", new[] { new SgVehicleWheelLoadData(fz: -1) },
            ForceUnit.KN, LengthUnit.M, MomentUnit.KNm);

    /// <summary>
    ///     Wraps a collection of vehicles into a single scenario so Slice 41's
    ///     vehicle-focused tests still exercise the assembler through the "scenarios contain
    ///     everything" entry point introduced in Slice 43.1.
    /// </summary>
    private static SgMovingLoadScenarioData[] ScenariosFor(IEnumerable<SgMovingLoadVehicleData> vehicles)
    {
        var loads = vehicles
            .Select(v => new SgMovingLoadData(WrapperPath, vehicle: v))
            .ToArray();
        return new[] { new SgMovingLoadScenarioData("__wrap_scen", loads: loads) };
    }

    /// <summary>Wraps a collection of pressures into a single scenario (Slice 43.1 refactor).</summary>
    private static SgMovingLoadScenarioData[] ScenariosFor(IEnumerable<SgMovingLoadPressureData> pressures)
    {
        var loads = pressures
            .Select(p => new SgMovingLoadData(WrapperPath, pressure: p))
            .ToArray();
        return new[] { new SgMovingLoadScenarioData("__wrap_scen", loads: loads) };
    }

    /// <summary>Wraps a mix of vehicles and pressures into one scenario (Slice 43.1 refactor).</summary>
    private static SgMovingLoadScenarioData[] ScenariosFor(
        IEnumerable<SgMovingLoadVehicleData> vehicles,
        IEnumerable<SgMovingLoadPressureData> pressures)
    {
        var loads = vehicles
            .Select(v => new SgMovingLoadData(WrapperPath, vehicle: v))
            .Concat(pressures.Select(p => new SgMovingLoadData(WrapperPath, pressure: p)))
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

        _api.CreateLoadCasesAsync(Arg.Any<List<LoadCaseCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<LoadCaseCreate>)args[0];
                return input.Select((lc, i) => new LoadCase { Id = i + 1, Title = lc.Title }).ToList();
            });

        _api.CreateMovingLoadVehiclesFromUserAsync(
                Arg.Any<List<MovingLoadVehicleCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MovingLoadVehicleCreate>)args[0];
                return input.Select((v, i) => new MovingLoadVehicle { Id = 700 + i + 1, Name = v.Name })
                    .ToList();
            });

        _api.CreateMovingLoadVehicleFromLibraryAsync(
                Arg.Any<MovingLoadVehicleLibraryCreate>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (MovingLoadVehicleLibraryCreate)args[0];
                // Encode library+name into the ID so tests can assert distinct IDs per library call.
                var idBase = 800 + Math.Abs((input.Library + "::" + input.Name).GetHashCode() % 100);
                return new MovingLoadVehicle { Id = idBase, Name = input.Name, Library = input.Library };
            });

        _api.CreateMovingLoadPressuresAsync(
                Arg.Any<List<MovingLoadPressureCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MovingLoadPressureCreate>)args[0];
                return input.Select((p, i) => new MovingLoadPressure { Id = 900 + i + 1, Name = p.Name })
                    .ToList();
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
                return input.Select((s, i) => new MovingLoadScenario { Id = 500 + i + 1, Name = s.Name })
                    .ToList();
            });

        _api.SetMovingLoadScenarioLoadsAsync(
                Arg.Any<int>(), Arg.Any<List<MovingLoadScenarioLoad>>(), Arg.Any<CancellationToken>())
            .Returns(args => (List<MovingLoadScenarioLoad>)args[1]);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgVehicleWheelLoadData construction ───────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void VehicleWheelLoad_DefaultConstruction_AllZero()
    {
        var w = new SgVehicleWheelLoadData();

        Assert.Equal(0, w.X);
        Assert.Equal(0, w.Y);
        Assert.Equal(0, w.Fx);
        Assert.Equal(0, w.Fy);
        Assert.Equal(0, w.Fz);
        Assert.Equal(0, w.Mx);
        Assert.Equal(0, w.My);
        Assert.Equal(0, w.Mz);
        Assert.True(w.IsZero);
    }

    [Fact]
    public void VehicleWheelLoad_StoresAllComponents()
    {
        var w = new SgVehicleWheelLoadData(1, 2, 3, 4, 5, 6, 7, 8);

        Assert.Equal(1, w.X);
        Assert.Equal(2, w.Y);
        Assert.Equal(3, w.Fx);
        Assert.Equal(4, w.Fy);
        Assert.Equal(5, w.Fz);
        Assert.Equal(6, w.Mx);
        Assert.Equal(7, w.My);
        Assert.Equal(8, w.Mz);
        Assert.False(w.IsZero);
    }

    [Fact]
    public void VehicleWheelLoad_IsZero_IgnoresPositionOnly()
    {
        // Position only (wheel at (1, 2)) but no force/moment — still zero-effect
        var w = new SgVehicleWheelLoadData(x: 1, y: 2);
        Assert.True(w.IsZero);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgMovingLoadVehicleData construction ──────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Vehicle_User_StoresProperties()
    {
        var wheels = new[] { Wheel(0, 1, fz: -50), Wheel(0, -1, fz: -50) };
        var v = new SgMovingLoadVehicleData(
            name: "TestTruck",
            wheelLoads: wheels,
            forceUnit: ForceUnit.KN,
            lengthUnit: LengthUnit.M,
            momentUnit: MomentUnit.KNm);

        Assert.Equal("TestTruck", v.Name);
        Assert.Null(v.Library);
        Assert.False(v.IsLibrary);
        Assert.Equal(2, v.WheelLoads.Count);
        Assert.Equal(ForceUnit.KN, v.ForceUnit);
        Assert.Equal(LengthUnit.M, v.LengthUnit);
        Assert.Equal(MomentUnit.KNm, v.MomentUnit);
        Assert.Equal("TestTruck", v.Key);
    }

    [Fact]
    public void Vehicle_Library_StoresLibraryKey()
    {
        var v = new SgMovingLoadVehicleData(library: "Aust", name: "T44");

        Assert.Equal("T44", v.Name);
        Assert.Equal("Aust", v.Library);
        Assert.True(v.IsLibrary);
        Assert.Empty(v.WheelLoads);
        Assert.Equal("Aust::T44", v.Key);
    }

    [Fact]
    public void Vehicle_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SgMovingLoadVehicleData(name: "", wheelLoads: new[] { Wheel(fz: -1) },
                forceUnit: ForceUnit.KN, lengthUnit: LengthUnit.M, momentUnit: MomentUnit.KNm));
    }

    [Fact]
    public void Vehicle_UserModeWithNoWheels_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SgMovingLoadVehicleData(name: "Empty", wheelLoads: Array.Empty<SgVehicleWheelLoadData>(),
                forceUnit: ForceUnit.KN, lengthUnit: LengthUnit.M, momentUnit: MomentUnit.KNm));
    }

    [Fact]
    public void Vehicle_LibraryModeWithEmptyLibraryKey_Throws()
    {
        // Library="" or whitespace on the library constructor must be rejected —
        // enforces that library vehicles genuinely come from a named library.
        Assert.Throws<ArgumentException>(() =>
            new SgMovingLoadVehicleData(library: "", name: "T44"));
        Assert.Throws<ArgumentException>(() =>
            new SgMovingLoadVehicleData(library: "   ", name: "T44"));
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgMovingLoadPressureData construction ─────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Pressure_StoresProperties()
    {
        var p = new SgMovingLoadPressureData(
            name: "TruckPressure",
            width: 2.0, length: 4.0,
            loadSpacing: 0.5,
            px: 0, py: 0, pz: -10);

        Assert.Equal("TruckPressure", p.Name);
        Assert.Equal(2.0, p.Width);
        Assert.Equal(4.0, p.Length);
        Assert.Equal(0.5, p.LoadSpacing);
        Assert.Equal(0, p.Px);
        Assert.Equal(0, p.Py);
        Assert.Equal(-10, p.Pz);
        Assert.Equal("TruckPressure", p.Key);
    }

    [Fact]
    public void Pressure_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SgMovingLoadPressureData(name: "", width: 2, length: 4));
    }

    [Fact]
    public void Pressure_NonPositiveWidth_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SgMovingLoadPressureData(name: "P", width: 0, length: 4));
    }

    [Fact]
    public void Pressure_NonPositiveLength_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SgMovingLoadPressureData(name: "P", width: 2, length: -1));
    }

    [Fact]
    public void Pressure_NoLoadSpacing_IsAllowed()
    {
        var p = new SgMovingLoadPressureData(name: "P", width: 2, length: 4);
        Assert.Null(p.LoadSpacing);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — no vehicles / pressures ──────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_NoVehiclesOrPressures_DoesNotCallApi()
    {
        SetupApiReturns();

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance);

        await _api.DidNotReceive().CreateMovingLoadVehiclesFromUserAsync(
            Arg.Any<List<MovingLoadVehicleCreate>>(), Arg.Any<CancellationToken>());
        await _api.DidNotReceive().CreateMovingLoadVehicleFromLibraryAsync(
            Arg.Any<MovingLoadVehicleLibraryCreate>(), Arg.Any<CancellationToken>());
        await _api.DidNotReceive().CreateMovingLoadPressuresAsync(
            Arg.Any<List<MovingLoadPressureCreate>>(), Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — user-defined vehicles pushed ────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_SingleUserVehicle_PushedViaBulkEndpoint()
    {
        SetupApiReturns();

        var wheels = new[] { Wheel(0, 1, fz: -50), Wheel(0, -1, fz: -50) };
        var v = new SgMovingLoadVehicleData(
            name: "T44", wheelLoads: wheels,
            forceUnit: ForceUnit.KN, lengthUnit: LengthUnit.M, momentUnit: MomentUnit.KNm);

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(new[] { v }));

        await _api.Received(1).CreateMovingLoadVehiclesFromUserAsync(
            Arg.Is<List<MovingLoadVehicleCreate>>(list =>
                list.Count == 1 &&
                list[0].Name == "T44" &&
                list[0].Loads.Count == 2 &&
                list[0].LoadUnits.Force == ForceUnit.KN &&
                list[0].LoadUnits.Length == LengthUnit.M &&
                list[0].LoadUnits.Moment == MomentUnit.KNm),
            Arg.Any<CancellationToken>());

        await _api.DidNotReceive().CreateMovingLoadVehicleFromLibraryAsync(
            Arg.Any<MovingLoadVehicleLibraryCreate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_UserVehicle_WheelLoadsMappedCorrectly()
    {
        SetupApiReturns();

        var v = new SgMovingLoadVehicleData(
            name: "TestTruck",
            wheelLoads: new[] { Wheel(3, 1.2, fx: 1, fy: 2, fz: -50, mx: 4, my: 5, mz: 6) },
            forceUnit: ForceUnit.KN, lengthUnit: LengthUnit.M, momentUnit: MomentUnit.KNm);

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(new[] { v }));

        await _api.Received(1).CreateMovingLoadVehiclesFromUserAsync(
            Arg.Is<List<MovingLoadVehicleCreate>>(list =>
                list[0].Loads[0].X == 3 &&
                list[0].Loads[0].Y == 1.2 &&
                list[0].Loads[0].Fx == 1 &&
                list[0].Loads[0].Fy == 2 &&
                list[0].Loads[0].Fz == -50 &&
                list[0].Loads[0].Mx == 4 &&
                list[0].Loads[0].My == 5 &&
                list[0].Loads[0].Mz == 6),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_UserVehicle_PopulatesMap()
    {
        SetupApiReturns();

        var vehicles = new[]
        {
            new SgMovingLoadVehicleData("V1", new[] { Wheel(fz: -50) },
                ForceUnit.KN, LengthUnit.M, MomentUnit.KNm),
            new SgMovingLoadVehicleData("V2", new[] { Wheel(fz: -50) },
                ForceUnit.KN, LengthUnit.M, MomentUnit.KNm)
        };

        var result = await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(vehicles));

        Assert.Equal(2, result.Model.MovingLoadVehicleMap.Count);
        Assert.Equal(701, result.Model.MovingLoadVehicleMap["V1"]);
        Assert.Equal(702, result.Model.MovingLoadVehicleMap["V2"]);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — library vehicles pushed one-at-a-time ────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_SingleLibraryVehicle_PushedViaLibraryEndpoint()
    {
        SetupApiReturns();

        var v = new SgMovingLoadVehicleData(library: "Aust", name: "T44");

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(new[] { v }));

        await _api.Received(1).CreateMovingLoadVehicleFromLibraryAsync(
            Arg.Is<MovingLoadVehicleLibraryCreate>(c =>
                c.Library == "Aust" && c.Name == "T44"),
            Arg.Any<CancellationToken>());

        await _api.DidNotReceive().CreateMovingLoadVehiclesFromUserAsync(
            Arg.Any<List<MovingLoadVehicleCreate>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MultipleLibraryVehicles_CalledOncePerVehicle()
    {
        SetupApiReturns();

        var vehicles = new[]
        {
            new SgMovingLoadVehicleData(library: "Aust", name: "T44"),
            new SgMovingLoadVehicleData(library: "Aust", name: "M1600"),
            new SgMovingLoadVehicleData(library: "US", name: "HS20")
        };

        var result = await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(vehicles));

        await _api.Received(3).CreateMovingLoadVehicleFromLibraryAsync(
            Arg.Any<MovingLoadVehicleLibraryCreate>(), Arg.Any<CancellationToken>());

        Assert.Equal(3, result.Model.MovingLoadVehicleMap.Count);
        Assert.True(result.Model.MovingLoadVehicleMap.ContainsKey("Aust::T44"));
        Assert.True(result.Model.MovingLoadVehicleMap.ContainsKey("Aust::M1600"));
        Assert.True(result.Model.MovingLoadVehicleMap.ContainsKey("US::HS20"));
    }

    [Fact]
    public async Task Assemble_MixedLibraryAndUserVehicles_BothEndpointsCalled()
    {
        SetupApiReturns();

        var vehicles = new[]
        {
            new SgMovingLoadVehicleData(library: "Aust", name: "T44"),
            new SgMovingLoadVehicleData("CustomTruck", new[] { Wheel(fz: -50) },
                ForceUnit.KN, LengthUnit.M, MomentUnit.KNm)
        };

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(vehicles));

        await _api.Received(1).CreateMovingLoadVehicleFromLibraryAsync(
            Arg.Any<MovingLoadVehicleLibraryCreate>(), Arg.Any<CancellationToken>());
        await _api.Received(1).CreateMovingLoadVehiclesFromUserAsync(
            Arg.Is<List<MovingLoadVehicleCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — deduplication ───────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_DuplicateVehicleNames_Deduplicated()
    {
        SetupApiReturns();

        var wheels = new[] { Wheel(fz: -50) };
        var vehicles = new[]
        {
            new SgMovingLoadVehicleData("T44", wheels, ForceUnit.KN, LengthUnit.M, MomentUnit.KNm),
            new SgMovingLoadVehicleData("T44", wheels, ForceUnit.KN, LengthUnit.M, MomentUnit.KNm),
            new SgMovingLoadVehicleData("M1600", wheels, ForceUnit.KN, LengthUnit.M, MomentUnit.KNm)
        };

        var result = await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(vehicles));

        await _api.Received(1).CreateMovingLoadVehiclesFromUserAsync(
            Arg.Is<List<MovingLoadVehicleCreate>>(list => list.Count == 2),
            Arg.Any<CancellationToken>());

        Assert.Contains(result.Warnings,
            w => w.Contains("moving load vehicle", StringComparison.OrdinalIgnoreCase) &&
                 w.Contains("T44", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assemble_LibraryVehicleWithSameNameDifferentLibrary_TreatedAsDistinct()
    {
        SetupApiReturns();

        var vehicles = new[]
        {
            new SgMovingLoadVehicleData(library: "Aust", name: "T44"),
            new SgMovingLoadVehicleData(library: "US", name: "T44")
        };

        var result = await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(vehicles));

        // Two calls — one per library
        await _api.Received(2).CreateMovingLoadVehicleFromLibraryAsync(
            Arg.Any<MovingLoadVehicleLibraryCreate>(), Arg.Any<CancellationToken>());
        Assert.Equal(2, result.Model.MovingLoadVehicleMap.Count);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — pressures pushed ────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_SinglePressure_PushedViaBulkEndpoint()
    {
        SetupApiReturns();

        var p = new SgMovingLoadPressureData("TruckPressure",
            width: 2.5, length: 5.0, loadSpacing: 0.5, px: 0, py: 0, pz: -12);

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(new[] { p }));

        await _api.Received(1).CreateMovingLoadPressuresAsync(
            Arg.Is<List<MovingLoadPressureCreate>>(list =>
                list.Count == 1 &&
                list[0].Name == "TruckPressure" &&
                list[0].Width == 2.5 &&
                list[0].Length == 5.0 &&
                list[0].LoadSpacing == 0.5 &&
                list[0].Pz == -12),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_Pressures_PopulateMap()
    {
        SetupApiReturns();

        var pressures = new[]
        {
            new SgMovingLoadPressureData("P1", width: 2, length: 4, pz: -10),
            new SgMovingLoadPressureData("P2", width: 3, length: 5, pz: -20)
        };

        var result = await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(pressures));

        Assert.Equal(2, result.Model.MovingLoadPressureMap.Count);
        Assert.Equal(901, result.Model.MovingLoadPressureMap["P1"]);
        Assert.Equal(902, result.Model.MovingLoadPressureMap["P2"]);
    }

    [Fact]
    public async Task Assemble_DuplicatePressureNames_Deduplicated()
    {
        SetupApiReturns();

        var pressures = new[]
        {
            new SgMovingLoadPressureData("P", width: 2, length: 4, pz: -10),
            new SgMovingLoadPressureData("P", width: 3, length: 5, pz: -20),
            new SgMovingLoadPressureData("Q", width: 2, length: 4, pz: -10)
        };

        var result = await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(pressures));

        await _api.Received(1).CreateMovingLoadPressuresAsync(
            Arg.Is<List<MovingLoadPressureCreate>>(list => list.Count == 2),
            Arg.Any<CancellationToken>());

        Assert.Contains(result.Warnings,
            w => w.Contains("moving load pressure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assemble_Pressure_LoadSpacingOmitted_SendsNull()
    {
        SetupApiReturns();

        var p = new SgMovingLoadPressureData("P", width: 2, length: 4, pz: -10);

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: ScenariosFor(new[] { p }));

        await _api.Received(1).CreateMovingLoadPressuresAsync(
            Arg.Is<List<MovingLoadPressureCreate>>(list =>
                list[0].LoadSpacing == null),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — dependency ordering ─────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_VehiclesAndPressures_CreatedBeforeScenarios()
    {
        SetupApiReturns();

        var callOrder = new List<string>();
        _api.CreateMovingLoadVehiclesFromUserAsync(
                Arg.Any<List<MovingLoadVehicleCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("vehicles-user");
                var input = (List<MovingLoadVehicleCreate>)args[0];
                return input.Select((v, i) => new MovingLoadVehicle { Id = 700 + i + 1, Name = v.Name }).ToList();
            });
        _api.CreateMovingLoadVehicleFromLibraryAsync(
                Arg.Any<MovingLoadVehicleLibraryCreate>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("vehicles-library");
                var input = (MovingLoadVehicleLibraryCreate)args[0];
                return new MovingLoadVehicle { Id = 800, Name = input.Name, Library = input.Library };
            });
        _api.CreateMovingLoadPressuresAsync(
                Arg.Any<List<MovingLoadPressureCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("pressures");
                var input = (List<MovingLoadPressureCreate>)args[0];
                return input.Select((p, i) => new MovingLoadPressure { Id = 900 + i + 1, Name = p.Name }).ToList();
            });
        _api.CreateMovingLoadScenariosAsync(
                Arg.Any<List<MovingLoadScenarioCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("scenarios");
                var input = (List<MovingLoadScenarioCreate>)args[0];
                return input.Select((s, i) => new MovingLoadScenario { Id = 500 + i + 1, Name = s.Name }).ToList();
            });

        var libraryVehicle = new SgMovingLoadVehicleData(library: "Aust", name: "T44");
        var userVehicle = new SgMovingLoadVehicleData("CustomV", new[] { Wheel(fz: -50) },
            ForceUnit.KN, LengthUnit.M, MomentUnit.KNm);
        var pressure = new SgMovingLoadPressureData("P", width: 2, length: 4, pz: -10);

        // One scenario references a library vehicle, a user vehicle and a pressure via its Loads[]
        // — the assembler must partition and create all three before creating the scenario itself.
        var scenario = new SgMovingLoadScenarioData("Scen", loads: new[]
        {
            new SgMovingLoadData(WrapperPath, vehicle: libraryVehicle),
            new SgMovingLoadData(WrapperPath, vehicle: userVehicle),
            new SgMovingLoadData(WrapperPath, pressure: pressure)
        });

        await _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
            movingLoadScenarios: new[] { scenario });

        // Library vehicles first, then user vehicles, then pressures, then scenarios.
        // Exact order within vehicles doesn't matter — just that both are before pressures + scenarios.
        var vehicleUserIdx = callOrder.IndexOf("vehicles-user");
        var vehicleLibIdx = callOrder.IndexOf("vehicles-library");
        var pressuresIdx = callOrder.IndexOf("pressures");
        var scenariosIdx = callOrder.IndexOf("scenarios");

        Assert.True(vehicleUserIdx >= 0);
        Assert.True(vehicleLibIdx >= 0);
        Assert.True(pressuresIdx >= 0);
        Assert.True(scenariosIdx >= 0);
        Assert.True(vehicleUserIdx < scenariosIdx, "user vehicles must be pushed before scenarios");
        Assert.True(vehicleLibIdx < scenariosIdx, "library vehicles must be pushed before scenarios");
        Assert.True(pressuresIdx < scenariosIdx, "pressures must be pushed before scenarios");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── ModelAssembler — error handling ──────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_UserVehicleApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreateMovingLoadVehiclesFromUserAsync(
                Arg.Any<List<MovingLoadVehicleCreate>>(), Arg.Any<CancellationToken>())
            .Returns<List<MovingLoadVehicle>>(_ =>
                throw new InvalidOperationException("boom"));

        var v = new SgMovingLoadVehicleData("T", new[] { Wheel(fz: -50) },
            ForceUnit.KN, LengthUnit.M, MomentUnit.KNm);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
                movingLoadScenarios: ScenariosFor(new[] { v })));

        Assert.Contains("moving load vehicle", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Assemble_LibraryVehicleApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreateMovingLoadVehicleFromLibraryAsync(
                Arg.Any<MovingLoadVehicleLibraryCreate>(), Arg.Any<CancellationToken>())
            .Returns<MovingLoadVehicle>(_ =>
                throw new InvalidOperationException("boom"));

        var v = new SgMovingLoadVehicleData(library: "Aust", name: "T44");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
                movingLoadScenarios: ScenariosFor(new[] { v })));

        Assert.Contains("moving load vehicle", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Assemble_PressureApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreateMovingLoadPressuresAsync(
                Arg.Any<List<MovingLoadPressureCreate>>(), Arg.Any<CancellationToken>())
            .Returns<List<MovingLoadPressure>>(_ =>
                throw new InvalidOperationException("boom"));

        var p = new SgMovingLoadPressureData("P", width: 2, length: 4, pz: -10);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, new[] { MakeMember() }, Tolerance,
                movingLoadScenarios: ScenariosFor(new[] { p })));

        Assert.Contains("moving load pressure", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
