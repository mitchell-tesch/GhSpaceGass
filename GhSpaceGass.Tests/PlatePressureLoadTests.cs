using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class PlatePressureLoadTests
{
    private const double Tolerance = 0.001;

    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ModelAssembler _assembler = new();

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
            .Returns(args =>
            {
                var input = (List<MaterialLibraryCreate>)args[0];
                return input.Select((m, i) => new Material { Id = i + 1 }).ToList();
            });

        _api.CreateNodesAsync(Arg.Any<List<NodeCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<NodeCreate>)args[0];
                return input.Select((n, i) => new Node { Id = i + 1, X = n.X, Y = n.Y, Z = n.Z }).ToList();
            });

        _api.CreatePlatesAsync(Arg.Any<List<PlateCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<PlateCreate>)args[0];
                return input.Select((p, i) => new Plate
                {
                    Id = i + 1, NodeA = p.NodeA, NodeB = p.NodeB, NodeC = p.NodeC, NodeD = p.NodeD
                }).ToList();
            });

        _api.CreateLoadCasesAsync(Arg.Any<List<LoadCaseCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<LoadCaseCreate>)args[0];
                return input.Select((lc, i) => new LoadCase { Id = i + 1, Title = lc.Title }).ToList();
            });

        _api.CreateLoadCategoriesAsync(Arg.Any<List<LoadCategoryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<LoadCategoryCreate>)args[0];
                return input.Select((cat, i) => new LoadCategory { Id = i + 1, Title = cat.Title }).ToList();
            });

        _api.CreatePlatePressureLoadsAsync(Arg.Any<List<PlatePressureLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<PlatePressureLoadCreate>)args[0];
                return input.Select((pl, i) => new PlatePressureLoad
                {
                    Plate = pl.Plate, LoadCase = pl.LoadCase
                }).ToList();
            });
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgPlatePressureLoadData construction ──────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SgPlatePressureLoadData_StoresAllProperties()
    {
        var lc = new SgLoadCaseData("Wind");
        var cat = new SgLoadCategoryData("Wind");
        var nodes = new[] { new SgPoint3D(0, 0, 0), new SgPoint3D(5, 0, 0), new SgPoint3D(5, 5, 0), new SgPoint3D(0, 5, 0) };
        var data = new SgPlatePressureLoadData(nodes, lc,
            px: 1, py: 2, pz: -3, axes: LoadAxes.Local, loadCategory: cat);

        Assert.Equal(4, data.PlateNodes.Length);
        Assert.Same(lc, data.LoadCase);
        Assert.Same(cat, data.LoadCategory);
        Assert.Equal(1, data.Px);
        Assert.Equal(2, data.Py);
        Assert.Equal(-3, data.Pz);
        Assert.Equal(LoadAxes.Local, data.Axes);
    }

    [Fact]
    public void SgPlatePressureLoadData_DefaultValues()
    {
        var lc = new SgLoadCaseData("DL");
        var nodes = new[] { new SgPoint3D(0, 0, 0), new SgPoint3D(5, 0, 0), new SgPoint3D(5, 5, 0), new SgPoint3D(0, 5, 0) };
        var data = new SgPlatePressureLoadData(nodes, lc);

        Assert.Equal(0, data.Px);
        Assert.Equal(0, data.Py);
        Assert.Equal(0, data.Pz);
        Assert.Equal(LoadAxes.Local, data.Axes);
        Assert.Null(data.LoadCategory);
        Assert.True(data.IsZero);
    }

    [Fact]
    public void SgPlatePressureLoadData_NonZero_IsZeroFalse()
    {
        var lc = new SgLoadCaseData("DL");
        var nodes = new[] { new SgPoint3D(0, 0, 0), new SgPoint3D(5, 0, 0), new SgPoint3D(5, 5, 0), new SgPoint3D(0, 5, 0) };
        var data = new SgPlatePressureLoadData(nodes, lc, pz: -5);
        Assert.False(data.IsZero);
    }

    [Fact]
    public void SgPlatePressureLoadData_NullLoadCase_Throws()
    {
        var nodes = new[] { new SgPoint3D(0, 0, 0), new SgPoint3D(5, 0, 0), new SgPoint3D(5, 5, 0), new SgPoint3D(0, 5, 0) };
        Assert.Throws<ArgumentNullException>(() =>
            new SgPlatePressureLoadData(nodes, null!));
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Assemble: Plate Pressure Loads ────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_PlatePressureLoad_CreatesLoadCaseAndLoad()
    {
        SetupApiReturns();

        var members = Array.Empty<SgMemberData>();
        var plate = MakeQuadPlate();
        var lc = new SgLoadCaseData("Wind");
        var pressureLoads = new[]
        {
            new SgPlatePressureLoadData(plate.Nodes, lc, pz: -5, axes: LoadAxes.Local)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            plates: new[] { plate }, platePressureLoads: pressureLoads);

        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1 && list[0].Title == "Wind"),
            Arg.Any<CancellationToken>());

        await _api.Received(1).CreatePlatePressureLoadsAsync(
            Arg.Is<List<PlatePressureLoadCreate>>(list =>
                list.Count == 1 &&
                list[0].Plate == 1 &&
                list[0].Pz == -5 &&
                list[0].Axes == LoadAxes.Local),
            Arg.Any<CancellationToken>());

        Assert.Equal(1, result.Model.PlatePressureLoadCount);
    }

    [Fact]
    public async Task Assemble_PlatePressureLoad_SendsAllPressureValues()
    {
        SetupApiReturns();

        var plate = MakeQuadPlate();
        var lc = new SgLoadCaseData("Wind");
        var pressureLoads = new[]
        {
            new SgPlatePressureLoadData(plate.Nodes, lc, px: 1, py: 2, pz: 3, axes: LoadAxes.GlobalProjected)
        };

        await _assembler.AssembleAsync(_api, Array.Empty<SgMemberData>(), Tolerance,
            plates: new[] { plate }, platePressureLoads: pressureLoads);

        await _api.Received(1).CreatePlatePressureLoadsAsync(
            Arg.Is<List<PlatePressureLoadCreate>>(list =>
                list[0].Px == 1 && list[0].Py == 2 && list[0].Pz == 3 &&
                list[0].Axes == LoadAxes.GlobalProjected),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_PlatePressureLoad_WithCategory_PassesCategoryId()
    {
        SetupApiReturns();

        var plate = MakeQuadPlate();
        var lc = new SgLoadCaseData("Wind");
        var cat = new SgLoadCategoryData("Wind");
        var pressureLoads = new[]
        {
            new SgPlatePressureLoadData(plate.Nodes, lc, pz: -5, loadCategory: cat)
        };

        await _assembler.AssembleAsync(_api, Array.Empty<SgMemberData>(), Tolerance,
            plates: new[] { plate }, platePressureLoads: pressureLoads);

        await _api.Received(1).CreateLoadCategoriesAsync(
            Arg.Is<List<LoadCategoryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        await _api.Received(1).CreatePlatePressureLoadsAsync(
            Arg.Is<List<PlatePressureLoadCreate>>(list => list[0].LoadCategory == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_PlatePressureLoad_UnmatchedPlate_WarnsAndSkips()
    {
        SetupApiReturns();

        var plate = MakeQuadPlate();
        var lc = new SgLoadCaseData("Wind");
        // Pressure load references a plate with different corner points
        var wrongNodes = new[]
        {
            new SgPoint3D(100, 0, 0), new SgPoint3D(105, 0, 0),
            new SgPoint3D(105, 5, 0), new SgPoint3D(100, 5, 0)
        };
        var pressureLoads = new[]
        {
            new SgPlatePressureLoadData(wrongNodes, lc, pz: -5)
        };

        var result = await _assembler.AssembleAsync(_api, Array.Empty<SgMemberData>(), Tolerance,
            plates: new[] { plate }, platePressureLoads: pressureLoads);

        Assert.Contains(result.Warnings,
            w => w.Contains("doesn't match", StringComparison.OrdinalIgnoreCase) ||
                 w.Contains("skipped", StringComparison.OrdinalIgnoreCase));
        await _api.DidNotReceive().CreatePlatePressureLoadsAsync(
            Arg.Any<List<PlatePressureLoadCreate>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_PlatePressureLoad_ZeroValues_Warns()
    {
        SetupApiReturns();

        var plate = MakeQuadPlate();
        var lc = new SgLoadCaseData("Wind");
        var pressureLoads = new[]
        {
            new SgPlatePressureLoadData(plate.Nodes, lc) // all zero
        };

        var result = await _assembler.AssembleAsync(_api, Array.Empty<SgMemberData>(), Tolerance,
            plates: new[] { plate }, platePressureLoads: pressureLoads);

        Assert.Contains(result.Warnings, w => w.Contains("zero", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assemble_PlatePressureLoadApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreatePlatePressureLoadsAsync(
                Arg.Any<List<PlatePressureLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<PlatePressureLoad>());

        var plate = MakeQuadPlate();
        var lc = new SgLoadCaseData("Wind");
        var pressureLoads = new[]
        {
            new SgPlatePressureLoadData(plate.Nodes, lc, pz: -5)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, Array.Empty<SgMemberData>(), Tolerance,
                plates: new[] { plate }, platePressureLoads: pressureLoads));
        Assert.Contains("plate pressure", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Assemble_NoPlatePressureLoads_DoesNotCallApi()
    {
        SetupApiReturns();

        var plate = MakeQuadPlate();

        await _assembler.AssembleAsync(_api, Array.Empty<SgMemberData>(), Tolerance,
            plates: new[] { plate });

        await _api.DidNotReceive().CreatePlatePressureLoadsAsync(
            Arg.Any<List<PlatePressureLoadCreate>>(), Arg.Any<CancellationToken>());
    }
}

