using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class LumpedMassAndPrescribedDisplacementTests
{
    private const double Tolerance = 0.001;

    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ModelAssembler _assembler = new();

    // ── Helpers ───────────────────────────────────────────────────────

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

    private void SetupApiReturns()
    {
        _api.ClearJobDataAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        _api.CreateMaterialsFromLibraryAsync(Arg.Any<List<MaterialLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MaterialLibraryCreate>)args[0];
                return input.Select((m, i) => new Material { Id = i + 1 }).ToList();
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

        _api.CreateNodeLoadsAsync(Arg.Any<List<NodeLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<NodeLoadCreate>)args[0];
                return input.Select((nl, i) => new NodeLoad { Node = nl.Node, LoadCase = nl.LoadCase }).ToList();
            });

        _api.CreateLumpedMassLoadsAsync(Arg.Any<List<LumpedMassLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<LumpedMassLoadCreate>)args[0];
                return input.Select((lm, i) => new LumpedMassLoad
                {
                    Node = lm.Node, LoadCase = lm.LoadCase,
                    Tmx = lm.Tmx, Tmy = lm.Tmy, Tmz = lm.Tmz,
                    Rmx = lm.Rmx, Rmy = lm.Rmy, Rmz = lm.Rmz
                }).ToList();
            });

        _api.CreatePrescribedDisplacementsAsync(Arg.Any<List<PrescribedDisplacementCreate>>(),
                Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<PrescribedDisplacementCreate>)args[0];
                return input.Select((pd, i) => new PrescribedDisplacement
                {
                    Node = pd.Node, LoadCase = pd.LoadCase,
                    Tx = pd.Tx, Ty = pd.Ty, Tz = pd.Tz,
                    Rx = pd.Rx, Ry = pd.Ry, Rz = pd.Rz
                }).ToList();
            });

        _api.CreateMemberDistributedLoadsAsync(Arg.Any<List<MemberDistributedLoadCreate>>(),
                Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MemberDistributedLoadCreate>)args[0];
                return input.Select((dl, i) => new MemberDistributedLoad
                {
                    Member = dl.Member, LoadCase = dl.LoadCase
                }).ToList();
            });

        _api.CreateSelfWeightLoadsAsync(Arg.Any<List<SelfWeightLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<SelfWeightLoadCreate>)args[0];
                return input.Select((sw, i) => new SelfWeightLoad
                {
                    LoadCase = sw.LoadCase
                }).ToList();
            });

        _api.CreateNodeRestraintsAsync(Arg.Any<List<NodeRestraintCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<NodeRestraintCreate>)args[0];
                return input.Select((r, i) => new NodeRestraint { Node = r.Node, RestraintCode = r.RestraintCode })
                    .ToList();
            });
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgLumpedMassLoadData construction ─────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SgLumpedMassLoadData_StoresAllProperties()
    {
        var lc = new SgLoadCaseData("Dynamic");
        var data = new SgLumpedMassLoadData(
            new SgPoint3D(1, 2, 3), lc,
            tmx: 100, tmy: 200, tmz: 300,
            rmx: 10, rmy: 20, rmz: 30);

        Assert.Equal(new SgPoint3D(1, 2, 3), data.Point);
        Assert.Same(lc, data.LoadCase);
        Assert.Equal(100, data.Tmx);
        Assert.Equal(200, data.Tmy);
        Assert.Equal(300, data.Tmz);
        Assert.Equal(10, data.Rmx);
        Assert.Equal(20, data.Rmy);
        Assert.Equal(30, data.Rmz);
        Assert.Null(data.LoadCategory);
    }

    [Fact]
    public void SgLumpedMassLoadData_DefaultValues_AllZero()
    {
        var lc = new SgLoadCaseData("Dynamic");
        var data = new SgLumpedMassLoadData(new SgPoint3D(0, 0, 0), lc);

        Assert.Equal(0, data.Tmx);
        Assert.Equal(0, data.Tmy);
        Assert.Equal(0, data.Tmz);
        Assert.Equal(0, data.Rmx);
        Assert.Equal(0, data.Rmy);
        Assert.Equal(0, data.Rmz);
        Assert.True(data.IsZero);
    }

    [Fact]
    public void SgLumpedMassLoadData_NonZero_IsZeroReturnsFalse()
    {
        var lc = new SgLoadCaseData("Dynamic");
        var data = new SgLumpedMassLoadData(new SgPoint3D(0, 0, 0), lc, tmx: 100);
        Assert.False(data.IsZero);
    }

    [Fact]
    public void SgLumpedMassLoadData_NullLoadCase_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SgLumpedMassLoadData(new SgPoint3D(0, 0, 0), null!));
    }

    [Fact]
    public void SgLumpedMassLoadData_WithCategory_StoresCategory()
    {
        var lc = new SgLoadCaseData("Dynamic");
        var cat = new SgLoadCategoryData("Dead");
        var data = new SgLumpedMassLoadData(new SgPoint3D(0, 0, 0), lc, tmx: 100, loadCategory: cat);
        Assert.Same(cat, data.LoadCategory);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgPrescribedDisplacementData construction ─────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SgPrescribedDisplacementData_StoresAllProperties()
    {
        var lc = new SgLoadCaseData("Settlement");
        var data = new SgPrescribedDisplacementData(
            new SgPoint3D(4, 5, 6), lc,
            tx: 1, ty: -2, tz: 3,
            rx: 0.01, ry: 0.02, rz: 0.03);

        Assert.Equal(new SgPoint3D(4, 5, 6), data.Point);
        Assert.Same(lc, data.LoadCase);
        Assert.Equal(1, data.Tx);
        Assert.Equal(-2, data.Ty);
        Assert.Equal(3, data.Tz);
        Assert.Equal(0.01, data.Rx);
        Assert.Equal(0.02, data.Ry);
        Assert.Equal(0.03, data.Rz);
        Assert.Null(data.LoadCategory);
    }

    [Fact]
    public void SgPrescribedDisplacementData_DefaultValues_AllZero()
    {
        var lc = new SgLoadCaseData("Settlement");
        var data = new SgPrescribedDisplacementData(new SgPoint3D(0, 0, 0), lc);

        Assert.Equal(0, data.Tx);
        Assert.Equal(0, data.Ty);
        Assert.Equal(0, data.Tz);
        Assert.Equal(0, data.Rx);
        Assert.Equal(0, data.Ry);
        Assert.Equal(0, data.Rz);
        Assert.True(data.IsZero);
    }

    [Fact]
    public void SgPrescribedDisplacementData_NonZero_IsZeroReturnsFalse()
    {
        var lc = new SgLoadCaseData("Settlement");
        var data = new SgPrescribedDisplacementData(new SgPoint3D(0, 0, 0), lc, ty: -5);
        Assert.False(data.IsZero);
    }

    [Fact]
    public void SgPrescribedDisplacementData_NullLoadCase_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SgPrescribedDisplacementData(new SgPoint3D(0, 0, 0), null!));
    }

    [Fact]
    public void SgPrescribedDisplacementData_WithCategory_StoresCategory()
    {
        var lc = new SgLoadCaseData("Settlement");
        var cat = new SgLoadCategoryData("Dead");
        var data = new SgPrescribedDisplacementData(new SgPoint3D(0, 0, 0), lc, ty: -5, loadCategory: cat);
        Assert.Same(cat, data.LoadCategory);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Assemble: Lumped Mass Loads ───────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_LumpedMassLoad_CreatesLoadCaseAndLoad()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dynamic");
        var lumpedMassLoads = new[]
        {
            new SgLumpedMassLoadData(new SgPoint3D(0, 0, 0), lc, tmx: 100, tmy: 200, tmz: 300)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            lumpedMassLoads: lumpedMassLoads);

        // Load case created
        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1 && list[0].Title == "Dynamic"),
            Arg.Any<CancellationToken>());

        // Lumped mass load created with correct node and values
        await _api.Received(1).CreateLumpedMassLoadsAsync(
            Arg.Is<List<LumpedMassLoadCreate>>(list =>
                list.Count == 1 &&
                list[0].Node == 1 &&
                list[0].Tmx == 100 &&
                list[0].Tmy == 200 &&
                list[0].Tmz == 300),
            Arg.Any<CancellationToken>());

        Assert.Equal(1, result.Model.LumpedMassLoadCount);
    }

    [Fact]
    public async Task Assemble_LumpedMassLoad_SendsRotationalMassValues()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dynamic");
        var lumpedMassLoads = new[]
        {
            new SgLumpedMassLoadData(new SgPoint3D(0, 0, 0), lc,
                tmx: 100, tmy: 200, tmz: 300,
                rmx: 10, rmy: 20, rmz: 30)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            lumpedMassLoads: lumpedMassLoads);

        await _api.Received(1).CreateLumpedMassLoadsAsync(
            Arg.Is<List<LumpedMassLoadCreate>>(list =>
                list[0].Rmx == 10 &&
                list[0].Rmy == 20 &&
                list[0].Rmz == 30),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_LumpedMassLoad_WithCategory_PassesCategoryId()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dynamic");
        var cat = new SgLoadCategoryData("Mass");
        var lumpedMassLoads = new[]
        {
            new SgLumpedMassLoadData(new SgPoint3D(0, 0, 0), lc, tmx: 100, loadCategory: cat)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            lumpedMassLoads: lumpedMassLoads);

        await _api.Received(1).CreateLoadCategoriesAsync(
            Arg.Is<List<LoadCategoryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        await _api.Received(1).CreateLumpedMassLoadsAsync(
            Arg.Is<List<LumpedMassLoadCreate>>(list =>
                list[0].LoadCategory == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_LumpedMassLoad_ZeroMass_Warns()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dynamic");
        var lumpedMassLoads = new[]
        {
            new SgLumpedMassLoadData(new SgPoint3D(0, 0, 0), lc) // all zero
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            lumpedMassLoads: lumpedMassLoads);

        Assert.Contains(result.Warnings,
            w => w.Contains("zero", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assemble_LumpedMassLoad_OrphanPoint_CreatesStandaloneNode()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dynamic");
        var lumpedMassLoads = new[]
        {
            new SgLumpedMassLoadData(new SgPoint3D(5, 5, 0), lc, tmx: 100)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            lumpedMassLoads: lumpedMassLoads);

        // Should create 3 nodes: 2 from member + 1 orphan
        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 3),
            Arg.Any<CancellationToken>());

        Assert.Contains(result.Warnings,
            w => w.Contains("orphan", StringComparison.OrdinalIgnoreCase) ||
                 w.Contains("5.000", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assemble_LumpedMassLoad_SharedLoadCase_Deduplicates()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dynamic");

        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc, fz: -10)
        };
        var lumpedMassLoads = new[]
        {
            new SgLumpedMassLoadData(new SgPoint3D(10, 0, 0), lc, tmx: 100)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: nodeLoads, lumpedMassLoads: lumpedMassLoads);

        // Only 1 load case — shared
        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Assemble: Prescribed Displacements ────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_PrescribedDisplacement_CreatesLoadCaseAndLoad()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Settlement");
        var prescribedDisplacements = new[]
        {
            new SgPrescribedDisplacementData(new SgPoint3D(0, 0, 0), lc, ty: -5)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            prescribedDisplacements: prescribedDisplacements);

        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1 && list[0].Title == "Settlement"),
            Arg.Any<CancellationToken>());

        await _api.Received(1).CreatePrescribedDisplacementsAsync(
            Arg.Is<List<PrescribedDisplacementCreate>>(list =>
                list.Count == 1 &&
                list[0].Node == 1 &&
                list[0].Ty == -5),
            Arg.Any<CancellationToken>());

        Assert.Equal(1, result.Model.PrescribedDisplacementCount);
    }

    [Fact]
    public async Task Assemble_PrescribedDisplacement_SendsAllComponents()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Settlement");
        var prescribedDisplacements = new[]
        {
            new SgPrescribedDisplacementData(new SgPoint3D(0, 0, 0), lc,
                tx: 1, ty: -2, tz: 3,
                rx: 0.01, ry: 0.02, rz: 0.03)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            prescribedDisplacements: prescribedDisplacements);

        await _api.Received(1).CreatePrescribedDisplacementsAsync(
            Arg.Is<List<PrescribedDisplacementCreate>>(list =>
                list[0].Tx == 1 &&
                list[0].Ty == -2 &&
                list[0].Tz == 3 &&
                list[0].Rx == 0.01 &&
                list[0].Ry == 0.02 &&
                list[0].Rz == 0.03),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_PrescribedDisplacement_WithCategory_PassesCategoryId()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Settlement");
        var cat = new SgLoadCategoryData("Dead");
        var prescribedDisplacements = new[]
        {
            new SgPrescribedDisplacementData(new SgPoint3D(0, 0, 0), lc, ty: -5, loadCategory: cat)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            prescribedDisplacements: prescribedDisplacements);

        await _api.Received(1).CreateLoadCategoriesAsync(
            Arg.Is<List<LoadCategoryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        await _api.Received(1).CreatePrescribedDisplacementsAsync(
            Arg.Is<List<PrescribedDisplacementCreate>>(list =>
                list[0].LoadCategory == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_PrescribedDisplacement_ZeroValues_Warns()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Settlement");
        var prescribedDisplacements = new[]
        {
            new SgPrescribedDisplacementData(new SgPoint3D(0, 0, 0), lc) // all zero
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            prescribedDisplacements: prescribedDisplacements);

        Assert.Contains(result.Warnings,
            w => w.Contains("zero", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assemble_PrescribedDisplacement_OrphanPoint_CreatesStandaloneNode()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Settlement");
        var prescribedDisplacements = new[]
        {
            new SgPrescribedDisplacementData(new SgPoint3D(5, 5, 0), lc, ty: -5)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            prescribedDisplacements: prescribedDisplacements);

        // Should create 3 nodes: 2 from member + 1 orphan
        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 3),
            Arg.Any<CancellationToken>());

        Assert.Contains(result.Warnings,
            w => w.Contains("orphan", StringComparison.OrdinalIgnoreCase) ||
                 w.Contains("5.000", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assemble_PrescribedDisplacement_SharedLoadCase_Deduplicates()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Settlement");

        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc, fz: -10)
        };
        var prescribedDisplacements = new[]
        {
            new SgPrescribedDisplacementData(new SgPoint3D(10, 0, 0), lc, ty: -5)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: nodeLoads, prescribedDisplacements: prescribedDisplacements);

        // Only 1 load case — shared
        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Combined: All load types share categories ────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_AllFiveLoadTypes_SharedLoadCase_OnlyOneCreated()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Combined");

        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc, fz: -10)
        };
        var distLoads = new[]
        {
            new SgMemberDistributedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
                fyStart: -5, fyEnd: -5)
        };
        var swLoads = new[]
        {
            new SgSelfWeightLoadData(lc)
        };
        var lumpedMassLoads = new[]
        {
            new SgLumpedMassLoadData(new SgPoint3D(10, 0, 0), lc, tmx: 100)
        };
        var prescribedDisplacements = new[]
        {
            new SgPrescribedDisplacementData(new SgPoint3D(0, 0, 0), lc, ty: -5)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: nodeLoads,
            memberDistributedLoads: distLoads,
            selfWeightLoads: swLoads,
            lumpedMassLoads: lumpedMassLoads,
            prescribedDisplacements: prescribedDisplacements);

        // Only 1 load case — shared across all five
        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        Assert.Equal(1, result.Model.NodeLoadCount);
        Assert.Equal(1, result.Model.MemberDistributedLoadCount);
        Assert.Equal(1, result.Model.SelfWeightLoadCount);
        Assert.Equal(1, result.Model.LumpedMassLoadCount);
        Assert.Equal(1, result.Model.PrescribedDisplacementCount);
    }

    [Fact]
    public async Task Assemble_SharedCategoryAcrossAllLoadTypes_OnlyOneCreated()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Mixed");
        var cat = new SgLoadCategoryData("Dead");

        var lumpedMassLoads = new[]
        {
            new SgLumpedMassLoadData(new SgPoint3D(0, 0, 0), lc, tmx: 100, loadCategory: cat)
        };
        var prescribedDisplacements = new[]
        {
            new SgPrescribedDisplacementData(new SgPoint3D(10, 0, 0), lc, ty: -5, loadCategory: cat)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            lumpedMassLoads: lumpedMassLoads,
            prescribedDisplacements: prescribedDisplacements);

        // Only 1 category — shared
        await _api.Received(1).CreateLoadCategoriesAsync(
            Arg.Is<List<LoadCategoryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── No new loads = no new API calls ──────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_NoLumpedMassOrPrescribed_DoesNotCallNewApis()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.DidNotReceive().CreateLumpedMassLoadsAsync(
            Arg.Any<List<LumpedMassLoadCreate>>(), Arg.Any<CancellationToken>());
        await _api.DidNotReceive().CreatePrescribedDisplacementsAsync(
            Arg.Any<List<PrescribedDisplacementCreate>>(), Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── API failure ──────────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_LumpedMassLoadApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreateLumpedMassLoadsAsync(Arg.Any<List<LumpedMassLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<LumpedMassLoad>()); // returns 0 instead of 1

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dynamic");
        var lumpedMassLoads = new[]
        {
            new SgLumpedMassLoadData(new SgPoint3D(0, 0, 0), lc, tmx: 100)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, members, Tolerance, lumpedMassLoads: lumpedMassLoads));
        Assert.Contains("lumped mass", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Assemble_PrescribedDisplacementApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreatePrescribedDisplacementsAsync(
                Arg.Any<List<PrescribedDisplacementCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<PrescribedDisplacement>()); // returns 0 instead of 1

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Settlement");
        var prescribedDisplacements = new[]
        {
            new SgPrescribedDisplacementData(new SgPoint3D(0, 0, 0), lc, ty: -5)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, members, Tolerance,
                prescribedDisplacements: prescribedDisplacements));
        Assert.Contains("prescribed displacement", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Lumped mass only (no node loads) — still creates load case ───────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_LumpedMassOnly_NoNodeLoads_StillCreatesLoadCase()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dynamic");
        var lumpedMassLoads = new[]
        {
            new SgLumpedMassLoadData(new SgPoint3D(0, 0, 0), lc, tmx: 100)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            lumpedMassLoads: lumpedMassLoads);

        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        await _api.DidNotReceive().CreateNodeLoadsAsync(
            Arg.Any<List<NodeLoadCreate>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_PrescribedDisplacementOnly_NoNodeLoads_StillCreatesLoadCase()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Settlement");
        var prescribedDisplacements = new[]
        {
            new SgPrescribedDisplacementData(new SgPoint3D(0, 0, 0), lc, ty: -5)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            prescribedDisplacements: prescribedDisplacements);

        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        await _api.DidNotReceive().CreateNodeLoadsAsync(
            Arg.Any<List<NodeLoadCreate>>(), Arg.Any<CancellationToken>());
    }
}

