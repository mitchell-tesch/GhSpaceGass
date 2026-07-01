using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class ThermalLoadTests
{
    private const double Tolerance = 0.001;

    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ModelAssembler _assembler = new();

    private static SgMemberData MakeMember(
        double x1, double y1, double z1,
        double x2, double y2, double z2)
    {
        return new SgMemberData(
            new SgPoint3D(x1, y1, z1),
            new SgPoint3D(x2, y2, z2),
            new SgSectionData("Aust300", "360 UB 44.7"),
            new SgMaterialData("Aust", "STEEL"));
    }

    private static SgPlateData MakeQuadPlate()
    {
        return new SgPlateData(
            new[]
            {
                new SgPoint3D(0, 0, 0), new SgPoint3D(5, 0, 0),
                new SgPoint3D(5, 5, 0), new SgPoint3D(0, 5, 0)
            },
            new SgMaterialData("Aust", "CONC32"), actualThickness: 200);
    }

    private void SetupApiReturns()
    {
        _api.ClearJobDataAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _api.CreateMaterialsFromLibraryAsync(Arg.Any<List<MaterialLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args => ((List<MaterialLibraryCreate>)args[0]).Select((m, i) => new Material { Id = i + 1 }).ToList());
        _api.CreateSectionsFromLibraryAsync(Arg.Any<List<SectionLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args => ((List<SectionLibraryCreate>)args[0]).Select((s, i) => new Section { Id = i + 1 }).ToList());
        _api.CreateNodesAsync(Arg.Any<List<NodeCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args => ((List<NodeCreate>)args[0]).Select((n, i) => new Node { Id = i + 1, X = n.X, Y = n.Y, Z = n.Z }).ToList());
        _api.CreateMembersAsync(Arg.Any<List<MemberCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args => ((List<MemberCreate>)args[0]).Select((m, i) => new Member { Id = i + 1, NodeA = m.NodeA, NodeB = m.NodeB }).ToList());
        _api.CreatePlatesAsync(Arg.Any<List<PlateCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args => ((List<PlateCreate>)args[0]).Select((p, i) => new Plate { Id = i + 1, NodeA = p.NodeA, NodeB = p.NodeB, NodeC = p.NodeC, NodeD = p.NodeD }).ToList());
        _api.CreateLoadCasesAsync(Arg.Any<List<LoadCaseCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args => ((List<LoadCaseCreate>)args[0]).Select((lc, i) => new LoadCase { Id = i + 1, Title = lc.Title }).ToList());
        _api.CreateLoadCategoriesAsync(Arg.Any<List<LoadCategoryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args => ((List<LoadCategoryCreate>)args[0]).Select((cat, i) => new LoadCategory { Id = i + 1, Title = cat.Title }).ToList());
        _api.CreateThermalLoadsAsync(Arg.Any<List<ThermalLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args => ((List<ThermalLoadCreate>)args[0]).Select((tl, i) => new ThermalLoad { ElementId = tl.ElementId, LoadCase = tl.LoadCase }).ToList());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgThermalLoadData construction ────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SgThermalLoadData_ForMember_StoresProperties()
    {
        var lc = new SgLoadCaseData("Thermal");
        var data = SgThermalLoadData.ForMember(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
            thermalLoad: 30, yGradient: 5, zGradient: 2);

        Assert.Equal(ThermalElementType.Member, data.ElementType);
        Assert.Equal(new SgPoint3D(0, 0, 0), data.MemberStart);
        Assert.Equal(new SgPoint3D(10, 0, 0), data.MemberEnd);
        Assert.Null(data.PlateNodes);
        Assert.Same(lc, data.LoadCase);
        Assert.Equal(30, data.ThermalLoad);
        Assert.Equal(5, data.YGradient);
        Assert.Equal(2, data.ZGradient);
    }

    [Fact]
    public void SgThermalLoadData_ForPlate_StoresProperties()
    {
        var lc = new SgLoadCaseData("Thermal");
        var nodes = new[] { new SgPoint3D(0, 0, 0), new SgPoint3D(5, 0, 0), new SgPoint3D(5, 5, 0), new SgPoint3D(0, 5, 0) };
        var data = SgThermalLoadData.ForPlate(nodes, lc, thermalLoad: 20);

        Assert.Equal(ThermalElementType.Plate, data.ElementType);
        Assert.Equal(4, data.PlateNodes!.Length);
        Assert.Null(data.MemberStart);
        Assert.Null(data.MemberEnd);
        Assert.Equal(20, data.ThermalLoad);
    }

    [Fact]
    public void SgThermalLoadData_DefaultValues_IsZeroTrue()
    {
        var lc = new SgLoadCaseData("Thermal");
        var data = SgThermalLoadData.ForMember(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc);
        Assert.True(data.IsZero);
    }

    [Fact]
    public void SgThermalLoadData_NonZero_IsZeroFalse()
    {
        var lc = new SgLoadCaseData("Thermal");
        var data = SgThermalLoadData.ForMember(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc, thermalLoad: 30);
        Assert.False(data.IsZero);
    }

    [Fact]
    public void SgThermalLoadData_NullLoadCase_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SgThermalLoadData.ForMember(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), null!));
    }

    [Fact]
    public void SgThermalLoadData_WithCategory_StoresCategory()
    {
        var lc = new SgLoadCaseData("Thermal");
        var cat = new SgLoadCategoryData("Temperature");
        var data = SgThermalLoadData.ForMember(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
            thermalLoad: 30, loadCategory: cat);
        Assert.Same(cat, data.LoadCategory);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Assemble: Member Thermal Loads ────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_MemberThermalLoad_CreatesLoadCaseAndLoad()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Thermal");
        var thermalLoads = new[]
        {
            SgThermalLoadData.ForMember(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
                thermalLoad: 30, yGradient: 5)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            thermalLoads: thermalLoads);

        await _api.Received(1).CreateThermalLoadsAsync(
            Arg.Is<List<ThermalLoadCreate>>(list =>
                list.Count == 1 &&
                list[0].ElementId == 1 &&
                list[0].ElementType == ThermalElementType.Member &&
                list[0].ThermalLoad == 30 &&
                list[0].YThermalGradient == 5),
            Arg.Any<CancellationToken>());

        Assert.Equal(1, result.Model.ThermalLoadCount);
    }

    [Fact]
    public async Task Assemble_MemberThermalLoad_UnmatchedMember_WarnsAndSkips()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Thermal");
        var thermalLoads = new[]
        {
            SgThermalLoadData.ForMember(
                new SgPoint3D(0, 0, 0), new SgPoint3D(20, 0, 0), lc, thermalLoad: 30)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            thermalLoads: thermalLoads);

        Assert.Contains(result.Warnings, w => w.Contains("doesn't exist") || w.Contains("don't match"));
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Assemble: Plate Thermal Loads ─────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_PlateThermalLoad_CreatesLoadCaseAndLoad()
    {
        SetupApiReturns();

        var plate = MakeQuadPlate();
        var lc = new SgLoadCaseData("Thermal");
        var thermalLoads = new[]
        {
            SgThermalLoadData.ForPlate(plate.Nodes, lc, thermalLoad: 20)
        };

        var result = await _assembler.AssembleAsync(_api, Array.Empty<SgMemberData>(), Tolerance,
            plates: new[] { plate }, thermalLoads: thermalLoads);

        await _api.Received(1).CreateThermalLoadsAsync(
            Arg.Is<List<ThermalLoadCreate>>(list =>
                list.Count == 1 &&
                list[0].ElementId == 1 &&
                list[0].ElementType == ThermalElementType.Plate &&
                list[0].ThermalLoad == 20),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Common tests ─────────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_ThermalLoad_WithCategory_PassesCategoryId()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Thermal");
        var cat = new SgLoadCategoryData("Temperature");
        var thermalLoads = new[]
        {
            SgThermalLoadData.ForMember(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
                thermalLoad: 30, loadCategory: cat)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance, thermalLoads: thermalLoads);

        await _api.Received(1).CreateLoadCategoriesAsync(
            Arg.Is<List<LoadCategoryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        await _api.Received(1).CreateThermalLoadsAsync(
            Arg.Is<List<ThermalLoadCreate>>(list => list[0].LoadCategory == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_ThermalLoad_ZeroValues_Warns()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Thermal");
        var thermalLoads = new[]
        {
            SgThermalLoadData.ForMember(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc) // all zero
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            thermalLoads: thermalLoads);

        Assert.Contains(result.Warnings, w => w.Contains("zero", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assemble_ThermalLoadApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreateThermalLoadsAsync(Arg.Any<List<ThermalLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ThermalLoad>());

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Thermal");
        var thermalLoads = new[]
        {
            SgThermalLoadData.ForMember(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc, thermalLoad: 30)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, members, Tolerance, thermalLoads: thermalLoads));
        Assert.Contains("thermal", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Assemble_NoThermalLoads_DoesNotCallApi()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.DidNotReceive().CreateThermalLoadsAsync(
            Arg.Any<List<ThermalLoadCreate>>(), Arg.Any<CancellationToken>());
    }
}

