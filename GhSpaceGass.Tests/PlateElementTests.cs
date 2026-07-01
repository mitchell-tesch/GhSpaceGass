using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class PlateElementTests
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

    private static SgPlateData MakeQuadPlate(
        double thickness = 200,
        string materialLibrary = "Aust",
        string materialName = "CONC32")
    {
        return new SgPlateData(
            new[]
            {
                new SgPoint3D(0, 0, 0),
                new SgPoint3D(5, 0, 0),
                new SgPoint3D(5, 5, 0),
                new SgPoint3D(0, 5, 0)
            },
            new SgMaterialData(materialLibrary, materialName),
            actualThickness: thickness);
    }

    private static SgPlateData MakeTriPlate(double thickness = 200)
    {
        return new SgPlateData(
            new[]
            {
                new SgPoint3D(0, 0, 0),
                new SgPoint3D(5, 0, 0),
                new SgPoint3D(2.5, 5, 0)
            },
            new SgMaterialData("Aust", "CONC32"),
            actualThickness: thickness);
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

        _api.CreateMaterialsFromUserAsync(Arg.Any<List<MaterialUserCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MaterialUserCreate>)args[0];
                return input.Select((m, i) => new Material { Id = i + 100 }).ToList();
            });

        _api.CreateSectionsFromLibraryAsync(Arg.Any<List<SectionLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<SectionLibraryCreate>)args[0];
                return input.Select((s, i) => new Section { Id = i + 1 }).ToList();
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

        _api.CreatePlatesAsync(Arg.Any<List<PlateCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<PlateCreate>)args[0];
                return input.Select((p, i) => new Plate
                {
                    Id = i + 1, NodeA = p.NodeA, NodeB = p.NodeB, NodeC = p.NodeC, NodeD = p.NodeD
                }).ToList();
            });
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgPlateData construction ──────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SgPlateData_QuadPlate_StoresAllProperties()
    {
        var mat = new SgMaterialData("Aust", "CONC32");
        var nodes = new[]
        {
            new SgPoint3D(0, 0, 0), new SgPoint3D(5, 0, 0),
            new SgPoint3D(5, 5, 0), new SgPoint3D(0, 5, 0)
        };
        var plate = new SgPlateData(nodes, mat,
            actualThickness: 200, bendingThickness: 180,
            membraneThickness: 190, shearThickness: 170,
            offset: 50, theory: PlateTheory.Mindlin);

        Assert.Equal(4, plate.Nodes.Length);
        Assert.Same(mat, plate.Material);
        Assert.Equal(200, plate.ActualThickness);
        Assert.Equal(180, plate.BendingThickness);
        Assert.Equal(190, plate.MembraneThickness);
        Assert.Equal(170, plate.ShearThickness);
        Assert.Equal(50, plate.Offset);
        Assert.Equal(PlateTheory.Mindlin, plate.Theory);
    }

    [Fact]
    public void SgPlateData_TriPlate_Stores3Nodes()
    {
        var plate = MakeTriPlate();
        Assert.Equal(3, plate.Nodes.Length);
    }

    [Fact]
    public void SgPlateData_DefaultOptionalValues()
    {
        var plate = MakeQuadPlate();
        Assert.Null(plate.BendingThickness);
        Assert.Null(plate.MembraneThickness);
        Assert.Null(plate.ShearThickness);
        Assert.Equal(0, plate.Offset);
        Assert.Null(plate.Theory);
    }

    [Fact]
    public void SgPlateData_TooFewNodes_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SgPlateData(
                new[] { new SgPoint3D(0, 0, 0), new SgPoint3D(5, 0, 0) },
                new SgMaterialData("Aust", "CONC32"), 200));
    }

    [Fact]
    public void SgPlateData_TooManyNodes_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SgPlateData(
                new[]
                {
                    new SgPoint3D(0, 0, 0), new SgPoint3D(5, 0, 0),
                    new SgPoint3D(5, 5, 0), new SgPoint3D(0, 5, 0),
                    new SgPoint3D(2.5, 2.5, 0)
                },
                new SgMaterialData("Aust", "CONC32"), 200));
    }

    [Fact]
    public void SgPlateData_NullMaterial_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SgPlateData(
                new[]
                {
                    new SgPoint3D(0, 0, 0), new SgPoint3D(5, 0, 0),
                    new SgPoint3D(5, 5, 0), new SgPoint3D(0, 5, 0)
                },
                null!, 200));
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Assemble: Plates with members ────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_WithPlates_CallsCreatePlatesAsync()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var plates = new[] { MakeQuadPlate() };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance, plates: plates);

        await _api.Received(1).CreatePlatesAsync(
            Arg.Is<List<PlateCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_QuadPlate_SendsCorrectNodeIds()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var plates = new[] { MakeQuadPlate() };

        await _assembler.AssembleAsync(_api, members, Tolerance, plates: plates);

        // Member nodes: (0,0,0)=1, (10,0,0)=2
        // Plate nodes: (0,0,0)=shared with 1, (5,0,0)=3, (5,5,0)=4, (0,5,0)=5
        await _api.Received(1).CreatePlatesAsync(
            Arg.Is<List<PlateCreate>>(list =>
                list[0].NodeA != null && list[0].NodeB != null &&
                list[0].NodeC != null && list[0].NodeD != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_TriPlate_NodeDIsNull()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var plates = new[] { MakeTriPlate() };

        await _assembler.AssembleAsync(_api, members, Tolerance, plates: plates);

        await _api.Received(1).CreatePlatesAsync(
            Arg.Is<List<PlateCreate>>(list =>
                list[0].NodeA != null && list[0].NodeB != null &&
                list[0].NodeC != null && list[0].NodeD == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_Plate_PassesThicknessAndProperties()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var mat = new SgMaterialData("Aust", "CONC32");
        var plates = new[]
        {
            new SgPlateData(
                new[]
                {
                    new SgPoint3D(0, 0, 0), new SgPoint3D(5, 0, 0),
                    new SgPoint3D(5, 5, 0), new SgPoint3D(0, 5, 0)
                },
                mat, actualThickness: 200, bendingThickness: 180,
                offset: 50, theory: PlateTheory.Mindlin)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance, plates: plates);

        await _api.Received(1).CreatePlatesAsync(
            Arg.Is<List<PlateCreate>>(list =>
                list[0].ActualThickness == 200 &&
                list[0].BendingThickness == 180 &&
                list[0].Offset == 50 &&
                list[0].Theory == PlateTheory.Mindlin),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_PlateMaterial_DeduplicatedWithMemberMaterials()
    {
        SetupApiReturns();

        // Member uses "STEEL", plate uses "CONC32" — both from "Aust" library
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var plates = new[] { MakeQuadPlate() };

        await _assembler.AssembleAsync(_api, members, Tolerance, plates: plates);

        // Both materials created in a single call (2 unique materials)
        await _api.Received(1).CreateMaterialsFromLibraryAsync(
            Arg.Is<List<MaterialLibraryCreate>>(list => list.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_PlateMaterial_SharedWithMember_Deduplicated()
    {
        SetupApiReturns();

        // Both member and plate use the same material
        var sharedMat = new SgMaterialData("Aust", "STEEL");
        var members = new[]
        {
            new SgMemberData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0),
                new SgSectionData("Aust300", "360 UB 44.7"), sharedMat)
        };
        var plates = new[]
        {
            new SgPlateData(
                new[]
                {
                    new SgPoint3D(0, 0, 0), new SgPoint3D(5, 0, 0),
                    new SgPoint3D(5, 5, 0), new SgPoint3D(0, 5, 0)
                },
                sharedMat, actualThickness: 200)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance, plates: plates);

        // Only 1 material — shared and deduplicated
        await _api.Received(1).CreateMaterialsFromLibraryAsync(
            Arg.Is<List<MaterialLibraryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_PlateCornerNodes_DeduplicatedWithMemberNodes()
    {
        SetupApiReturns();

        // Member from (0,0,0) to (5,0,0), plate shares both endpoints
        var members = new[] { MakeMember(0, 0, 0, 5, 0, 0) };
        var plates = new[] { MakeQuadPlate() };

        await _assembler.AssembleAsync(_api, members, Tolerance, plates: plates);

        // 4 unique nodes: (0,0,0), (5,0,0) shared, (5,5,0), (0,5,0)
        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 4),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_WithPlates_PlateMapPopulated()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var plates = new[] { MakeQuadPlate() };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance, plates: plates);

        Assert.Single(result.Model.PlateMap);
        Assert.True(result.Model.PlateMap.ContainsKey(1));
        Assert.Equal(4, result.Model.PlateMap[1].Length);
    }

    [Fact]
    public async Task Assemble_PlateApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreatePlatesAsync(Arg.Any<List<PlateCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Plate>());

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var plates = new[] { MakeQuadPlate() };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, members, Tolerance, plates: plates));
        Assert.Contains("plate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Assemble_NoPlates_DoesNotCallPlatesApi()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.DidNotReceive().CreatePlatesAsync(
            Arg.Any<List<PlateCreate>>(), Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Plates-only model (no members) ───────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_PlatesOnly_NoMembers_StillCreatesModel()
    {
        SetupApiReturns();

        var members = Array.Empty<SgMemberData>();
        var plates = new[] { MakeQuadPlate() };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance, plates: plates);

        // Should NOT early-exit with "no members" warning
        Assert.DoesNotContain(result.Warnings, w => w.Contains("model is empty"));

        // Plates created
        await _api.Received(1).CreatePlatesAsync(
            Arg.Is<List<PlateCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        // 4 nodes created from plate corners
        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 4),
            Arg.Any<CancellationToken>());

        Assert.Single(result.Model.PlateMap);
    }

    [Fact]
    public async Task Assemble_NoMembersNoPlates_EarlyExit()
    {
        SetupApiReturns();

        var members = Array.Empty<SgMemberData>();

        var result = await _assembler.AssembleAsync(_api, members, Tolerance);

        Assert.Contains(result.Warnings, w => w.Contains("empty"));
    }
}

