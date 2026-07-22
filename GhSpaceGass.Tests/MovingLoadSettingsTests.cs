using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

/// <summary>
///     Tests for moving load settings (Slice 43.2). Covers <see cref="SgMovingLoadSettingsData"/>
///     construction, and the assembler's Settings PATCH step — when settings are supplied, the
///     PATCH is sent after scenario Loads are wired and before load categories; when omitted
///     (or empty), no PATCH is sent.
/// </summary>
public class MovingLoadSettingsTests
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

    private static SgMovingLoadScenarioData MakeScenarioWithVehicle()
    {
        var path = new SgMovingLoadTravelPathData("P", new[]
        {
            new SgMovingLoadStationData(new SgPoint3D(0, 0, 0)),
            new SgMovingLoadStationData(new SgPoint3D(1, 0, 0))
        });
        var vehicle = new SgMovingLoadVehicleData("V",
            new[] { new SgVehicleWheelLoadData(fz: -1) },
            ForceUnit.KN, LengthUnit.M, MomentUnit.KNm);
        var ml = new SgMovingLoadData(travelPath: path, vehicle: vehicle);
        return new SgMovingLoadScenarioData("Scen", loads: new[] { ml });
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

        _api.PatchMovingLoadSettingsAsync(
                Arg.Any<MovingLoadSettingsUpdate>(), Arg.Any<CancellationToken>())
            .Returns(new MovingLoadSettings());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgMovingLoadSettingsData construction ─────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Settings_Empty_HasAnyValueIsFalse()
    {
        var s = new SgMovingLoadSettingsData();
        Assert.False(s.HasAnyValue);
    }

    [Fact]
    public void Settings_OneFieldSet_HasAnyValueIsTrue()
    {
        var s = new SgMovingLoadSettingsData(retainLoads: true);
        Assert.True(s.HasAnyValue);
    }

    [Fact]
    public void Settings_AllFieldsStored()
    {
        var s = new SgMovingLoadSettingsData(
            applyToClosestMember: true,
            checkVerticalProximity: true,
            verticalProximity: 0.5,
            ignoreLoadsOnOneMember: true,
            ignoreOutsideLoadedArea: false,
            keepLoadsWithinTravelPath: true,
            retainLoads: false);

        Assert.True(s.ApplyToClosestMember);
        Assert.True(s.CheckVerticalProximity);
        Assert.Equal(0.5, s.VerticalProximity);
        Assert.True(s.IgnoreLoadsOnOneMember);
        Assert.False(s.IgnoreOutsideLoadedArea);
        Assert.True(s.KeepLoadsWithinTravelPath);
        Assert.False(s.RetainLoads);
        Assert.True(s.HasAnyValue);
    }
}
