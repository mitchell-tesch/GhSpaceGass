using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class LoadAssemblyTests
{
    private const double Tolerance = 0.001;

    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ModelAssembler _assembler = new();

    // ── Helpers ───────────────────────────────────────────────────────

    private static SgMemberData MakeMember(
        double x1, double y1, double z1,
        double x2, double y2, double z2,
        string sectionLibrary = "Aust300",
        string sectionName = "360 UB 44.7",
        string materialLibrary = "Aust",
        string materialName = "STEEL")
    {
        return new SgMemberData(
            new SgPoint3D(x1, y1, z1),
            new SgPoint3D(x2, y2, z2),
            new SgSectionData(sectionLibrary, sectionName),
            new SgMaterialData(materialLibrary, materialName));
    }

    private static SgNodeLoadData MakeNodeLoad(
        double x, double y, double z,
        string loadCaseName = "Dead Load",
        double fx = 0, double fy = 0, double fz = -10,
        double mx = 0, double my = 0, double mz = 0)
    {
        var lc = new SgLoadCaseData(loadCaseName);
        return new SgNodeLoadData(new SgPoint3D(x, y, z), lc, fx, fy, fz, mx, my, mz);
    }

    /// <summary>
    ///     Configures the mock API to return sequential IDs for each bulk create call.
    /// </summary>
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

        _api.CreateNodeRestraintsAsync(Arg.Any<List<NodeRestraintCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<NodeRestraintCreate>)args[0];
                return input.Select((r, i) => new NodeRestraint { Node = r.Node, RestraintCode = r.RestraintCode })
                    .ToList();
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
                return input.Select((nl, i) => new NodeLoad
                {
                    Node = nl.Node, LoadCase = nl.LoadCase,
                    Fx = nl.Fx, Fy = nl.Fy, Fz = nl.Fz,
                    Mx = nl.Mx, My = nl.My, Mz = nl.Mz
                }).ToList();
            });

        _api.CreateMemberDistributedLoadsAsync(Arg.Any<List<MemberDistributedLoadCreate>>(),
                Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MemberDistributedLoadCreate>)args[0];
                return input.Select((dl, i) => new MemberDistributedLoad
                {
                    Member = dl.Member, LoadCase = dl.LoadCase,
                    FxStart = dl.FxStart, FyStart = dl.FyStart, FzStart = dl.FzStart,
                    FxFinish = dl.FxFinish, FyFinish = dl.FyFinish, FzFinish = dl.FzFinish
                }).ToList();
            });

        _api.CreateSelfWeightLoadsAsync(Arg.Any<List<SelfWeightLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<SelfWeightLoadCreate>)args[0];
                return input.Select((sw, i) => new SelfWeightLoad
                {
                    LoadCase = sw.LoadCase,
                    AccelerationX = sw.AccelerationX,
                    AccelerationY = sw.AccelerationY,
                    AccelerationZ = sw.AccelerationZ
                }).ToList();
            });
    }

    // ── SgLoadCaseData construction ────────────────────────────────────

    [Fact]
    public void SgLoadCaseData_ValidName_Stores()
    {
        var lc = new SgLoadCaseData("Dead Load");
        Assert.Equal("Dead Load", lc.Name);
        Assert.Null(lc.Notes);
    }

    [Fact]
    public void SgLoadCaseData_WithNotes_StoresBoth()
    {
        var lc = new SgLoadCaseData("Live Load", "Floor live load");
        Assert.Equal("Live Load", lc.Name);
        Assert.Equal("Floor live load", lc.Notes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SgLoadCaseData_InvalidName_Throws(string? name)
    {
        Assert.Throws<ArgumentException>(() => new SgLoadCaseData(name!));
    }

    [Fact]
    public void SgLoadCaseData_Key_IsName()
    {
        var lc = new SgLoadCaseData("Dead Load");
        Assert.Equal("Dead Load", lc.Key);
    }

    // ── SgNodeLoadData construction ────────────────────────────────────

    [Fact]
    public void SgNodeLoadData_StoresAllProperties()
    {
        var lc = new SgLoadCaseData("Dead Load");
        var nl = new SgNodeLoadData(new SgPoint3D(1, 2, 3), lc, -5, fz: -10, my: 3);

        Assert.Equal(new SgPoint3D(1, 2, 3), nl.Point);
        Assert.Same(lc, nl.LoadCase);
        Assert.Equal(-5, nl.Fx);
        Assert.Equal(0, nl.Fy);
        Assert.Equal(-10, nl.Fz);
        Assert.Equal(0, nl.Mx);
        Assert.Equal(3, nl.My);
        Assert.Equal(0, nl.Mz);
    }

    [Fact]
    public void SgNodeLoadData_NullLoadCase_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SgNodeLoadData(new SgPoint3D(0, 0, 0), null!));
    }

    [Fact]
    public void SgNodeLoadData_AllZero_IsZeroReturnsTrue()
    {
        var lc = new SgLoadCaseData("Dead Load");
        var nl = new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc);
        Assert.True(nl.IsZero);
    }

    [Fact]
    public void SgNodeLoadData_NonZero_IsZeroReturnsFalse()
    {
        var lc = new SgLoadCaseData("Dead Load");
        var nl = new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc, fz: -10);
        Assert.False(nl.IsZero);
    }

    // ── Node loads pushed to API ──────────────────────────────────────

    [Fact]
    public async Task Assemble_WithNodeLoads_CallsCreateLoadCasesAsync()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var nodeLoads = new[] { MakeNodeLoad(0, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_WithNodeLoads_CallsCreateNodeLoadsAsync()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var nodeLoads = new[] { MakeNodeLoad(0, 0, 0, fz: -10) };

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        await _api.Received(1).CreateNodeLoadsAsync(
            Arg.Is<List<NodeLoadCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_WithNodeLoads_SendsCorrectForceValues()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var nodeLoads = new[] { MakeNodeLoad(0, 0, 0, fx: 5, fy: -3, fz: -10, mx: 1, my: 2, mz: 3) };

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        await _api.Received(1).CreateNodeLoadsAsync(
            Arg.Is<List<NodeLoadCreate>>(list =>
                list.Count == 1 &&
                list[0].Fx == 5 &&
                list[0].Fy == -3 &&
                list[0].Fz == -10 &&
                list[0].Mx == 1 &&
                list[0].My == 2 &&
                list[0].Mz == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_NodeLoadAtMemberEndpoint_ResolvesToCorrectNode()
    {
        SetupApiReturns();

        // Member from (0,0,0) to (10,0,0) — nodes get IDs 1 and 2
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        // Node load at end of member
        var nodeLoads = new[] { MakeNodeLoad(10, 0, 0, fz: -10) };

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        await _api.Received(1).CreateNodeLoadsAsync(
            Arg.Is<List<NodeLoadCreate>>(list =>
                list.Count == 1 && list[0].Node == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_NodeLoadWithinTolerance_ResolvesToExistingNode()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        // Node load slightly off from (10,0,0) but within tolerance
        var nodeLoads = new[] { MakeNodeLoad(10.0005, 0, 0, fz: -10) };

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        // Should NOT create extra nodes — still 2 nodes
        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 2),
            Arg.Any<CancellationToken>());

        // Should resolve to node 2
        await _api.Received(1).CreateNodeLoadsAsync(
            Arg.Is<List<NodeLoadCreate>>(list =>
                list.Count == 1 && list[0].Node == 2),
            Arg.Any<CancellationToken>());
    }

    // ── Load case deduplication (ADR-0006 pattern) ─────────────────────

    [Fact]
    public async Task Assemble_DuplicateLoadCaseNames_DeduplicatedAndWarns()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var nodeLoads = new[]
        {
            MakeNodeLoad(0, 0, 0, "Dead Load", fz: -10),
            MakeNodeLoad(10, 0, 0, "Dead Load", fz: -20)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        // Only 1 unique load case created
        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        // Both node loads still sent (same case, different nodes)
        await _api.Received(1).CreateNodeLoadsAsync(
            Arg.Is<List<NodeLoadCreate>>(list => list.Count == 2),
            Arg.Any<CancellationToken>());

        // Warning about duplicate load case
        Assert.Contains(result.Warnings,
            w => w.Contains("Dead Load", StringComparison.OrdinalIgnoreCase) &&
                 w.Contains("load case", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assemble_MultipleDistinctLoadCases_AllCreated()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc1 = new SgLoadCaseData("Dead Load");
        var lc2 = new SgLoadCaseData("Live Load");
        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc1, fz: -10),
            new SgNodeLoadData(new SgPoint3D(10, 0, 0), lc2, fz: -20)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_LoadCaseWithNotes_PassesNotesToApi()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dead Load", "Self-weight of structure");
        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc, fz: -10)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list =>
                list.Count == 1 &&
                list[0].Title == "Dead Load" &&
                list[0].Notes == "Self-weight of structure"),
            Arg.Any<CancellationToken>());
    }

    // ── Orphan load points (ADR-0002) ──────────────────────────────────

    [Fact]
    public async Task Assemble_OrphanNodeLoadPoint_CreatesStandaloneNode()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        // Node load at (5,5,0) — not near any member endpoint
        var nodeLoads = new[] { MakeNodeLoad(5, 5, 0, fz: -10) };

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        // Should create 3 nodes: 2 from member + 1 orphan
        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_OrphanNodeLoadPoint_EmitsWarning()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var nodeLoads = new[] { MakeNodeLoad(5, 5, 0, fz: -10) };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        Assert.Contains(result.Warnings,
            w => w.Contains("orphan", StringComparison.OrdinalIgnoreCase) ||
                 w.Contains("5", StringComparison.OrdinalIgnoreCase));
    }

    // ── Dependency order ──────────────────────────────────────────────

    [Fact]
    public async Task Assemble_WithNodeLoads_PushesInCorrectOrder()
    {
        SetupApiReturns();
        var callOrder = new List<string>();

        _api.ClearJobDataAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("clear"));
        _api.CreateMaterialsFromLibraryAsync(Arg.Any<List<MaterialLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("materials");
                var input = (List<MaterialLibraryCreate>)args[0];
                return input.Select((m, i) => new Material { Id = i + 1 }).ToList();
            });
        _api.CreateSectionsFromLibraryAsync(Arg.Any<List<SectionLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("sections");
                var input = (List<SectionLibraryCreate>)args[0];
                return input.Select((s, i) => new Section { Id = i + 1 }).ToList();
            });
        _api.CreateNodesAsync(Arg.Any<List<NodeCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("nodes");
                var input = (List<NodeCreate>)args[0];
                return input.Select((n, i) => new Node { Id = i + 1, X = n.X, Y = n.Y, Z = n.Z }).ToList();
            });
        _api.CreateMembersAsync(Arg.Any<List<MemberCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("members");
                var input = (List<MemberCreate>)args[0];
                return input.Select((m, i) => new Member { Id = i + 1, NodeA = m.NodeA, NodeB = m.NodeB }).ToList();
            });
        _api.CreateLoadCasesAsync(Arg.Any<List<LoadCaseCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("loadcases");
                var input = (List<LoadCaseCreate>)args[0];
                return input.Select((lc, i) => new LoadCase { Id = i + 1, Title = lc.Title }).ToList();
            });
        _api.CreateNodeLoadsAsync(Arg.Any<List<NodeLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("nodeloads");
                var input = (List<NodeLoadCreate>)args[0];
                return input.Select((nl, i) => new NodeLoad { Node = nl.Node, LoadCase = nl.LoadCase }).ToList();
            });

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var nodeLoads = new[] { MakeNodeLoad(0, 0, 0, fz: -10) };

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        Assert.Equal(new[] { "clear", "materials", "sections", "nodes", "members", "loadcases", "nodeloads" },
            callOrder);
    }

    // ── No node loads ─────────────────────────────────────────────────

    [Fact]
    public async Task Assemble_NoNodeLoads_DoesNotCallLoadApis()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.DidNotReceive().CreateLoadCasesAsync(
            Arg.Any<List<LoadCaseCreate>>(), Arg.Any<CancellationToken>());
        await _api.DidNotReceive().CreateNodeLoadsAsync(
            Arg.Any<List<NodeLoadCreate>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_EmptyNodeLoadList_DoesNotCallLoadApis()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var nodeLoads = Array.Empty<SgNodeLoadData>();

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        await _api.DidNotReceive().CreateLoadCasesAsync(
            Arg.Any<List<LoadCaseCreate>>(), Arg.Any<CancellationToken>());
        await _api.DidNotReceive().CreateNodeLoadsAsync(
            Arg.Any<List<NodeLoadCreate>>(), Arg.Any<CancellationToken>());
    }

    // ── Model output includes load data ───────────────────────────────

    [Fact]
    public async Task Assemble_WithNodeLoads_LoadCaseMapPopulated()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc1 = new SgLoadCaseData("Dead Load");
        var lc2 = new SgLoadCaseData("Live Load");
        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc1, fz: -10),
            new SgNodeLoadData(new SgPoint3D(10, 0, 0), lc2, fz: -20)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        Assert.Equal(2, result.Model.LoadCaseMap.Count);
        Assert.True(result.Model.LoadCaseMap.ContainsKey("Dead Load"));
        Assert.True(result.Model.LoadCaseMap.ContainsKey("Live Load"));
    }

    [Fact]
    public async Task Assemble_WithNodeLoads_NodeLoadCountSet()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var nodeLoads = new[]
        {
            MakeNodeLoad(0, 0, 0, fz: -10),
            MakeNodeLoad(10, 0, 0, fz: -20)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        Assert.Equal(2, result.Model.NodeLoadCount);
    }

    // ── Partial failure ───────────────────────────────────────────────

    [Fact]
    public async Task Assemble_LoadCaseApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreateLoadCasesAsync(Arg.Any<List<LoadCaseCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<LoadCase>()); // returns 0 instead of 1

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var nodeLoads = new[] { MakeNodeLoad(0, 0, 0, fz: -10) };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads));
        Assert.Contains("load case", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Assemble_NodeLoadApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreateNodeLoadsAsync(Arg.Any<List<NodeLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<NodeLoad>()); // returns 0 instead of 1

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var nodeLoads = new[] { MakeNodeLoad(0, 0, 0, fz: -10) };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads));
        Assert.Contains("node load", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Zero load warning ─────────────────────────────────────────────

    [Fact]
    public async Task Assemble_ZeroNodeLoad_EmitsWarning()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dead Load");
        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc) // all zero
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        Assert.Contains(result.Warnings,
            w => w.Contains("zero", StringComparison.OrdinalIgnoreCase));
    }

    // ── Node loads with restraints ────────────────────────────────────

    [Fact]
    public async Task Assemble_WithRestraintsAndNodeLoads_BothApplied()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var restraints = new[] { new SgRestraintData(new SgPoint3D(0, 0, 0), "FFFRRR") };
        var nodeLoads = new[] { MakeNodeLoad(10, 0, 0, fz: -10) };

        await _assembler.AssembleAsync(_api, members, Tolerance, restraints, nodeLoads);

        await _api.Received(1).CreateNodeRestraintsAsync(
            Arg.Is<List<NodeRestraintCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
        await _api.Received(1).CreateNodeLoadsAsync(
            Arg.Is<List<NodeLoadCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    // ── Node load references correct load case ID ─────────────────────

    [Fact]
    public async Task Assemble_NodeLoad_ReferencesCorrectLoadCaseId()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc1 = new SgLoadCaseData("Dead Load");
        var lc2 = new SgLoadCaseData("Live Load");
        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc1, fz: -10),
            new SgNodeLoadData(new SgPoint3D(10, 0, 0), lc2, fz: -20)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        // Load cases are created in order: Dead=1, Live=2
        await _api.Received(1).CreateNodeLoadsAsync(
            Arg.Is<List<NodeLoadCreate>>(list =>
                list.Count == 2 &&
                list[0].LoadCase == 1 && list[0].Node == 1 &&
                list[1].LoadCase == 2 && list[1].Node == 2),
            Arg.Any<CancellationToken>());
    }

    // ── SgLoadCategoryData construction ────────────────────────────────

    [Fact]
    public void SgLoadCategoryData_ValidName_Stores()
    {
        var cat = new SgLoadCategoryData("Dead");
        Assert.Equal("Dead", cat.Name);
        Assert.Null(cat.Notes);
    }

    [Fact]
    public void SgLoadCategoryData_WithNotes_StoresBoth()
    {
        var cat = new SgLoadCategoryData("Live", "Floor live loads");
        Assert.Equal("Live", cat.Name);
        Assert.Equal("Floor live loads", cat.Notes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SgLoadCategoryData_InvalidName_Throws(string? name)
    {
        Assert.Throws<ArgumentException>(() => new SgLoadCategoryData(name!));
    }

    // ── Load category on node loads ───────────────────────────────────

    [Fact]
    public void SgNodeLoadData_WithCategory_StoresCategory()
    {
        var lc = new SgLoadCaseData("Dead Load");
        var cat = new SgLoadCategoryData("Dead");
        var nl = new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc, fz: -10, loadCategory: cat);

        Assert.Same(cat, nl.LoadCategory);
    }

    [Fact]
    public void SgNodeLoadData_WithoutCategory_CategoryIsNull()
    {
        var lc = new SgLoadCaseData("Dead Load");
        var nl = new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc, fz: -10);

        Assert.Null(nl.LoadCategory);
    }

    // ── Load categories pushed to API ─────────────────────────────────

    [Fact]
    public async Task Assemble_WithLoadCategory_CallsCreateLoadCategoriesAsync()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dead Load");
        var cat = new SgLoadCategoryData("Dead");
        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc, fz: -10, loadCategory: cat)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        await _api.Received(1).CreateLoadCategoriesAsync(
            Arg.Is<List<LoadCategoryCreate>>(list => list.Count == 1 && list[0].Title == "Dead"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_WithLoadCategoryNotes_PassesNotesToApi()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dead Load");
        var cat = new SgLoadCategoryData("Dead", "Permanent dead loads");
        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc, fz: -10, loadCategory: cat)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        await _api.Received(1).CreateLoadCategoriesAsync(
            Arg.Is<List<LoadCategoryCreate>>(list =>
                list.Count == 1 &&
                list[0].Title == "Dead" &&
                list[0].Notes == "Permanent dead loads"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_WithoutLoadCategory_DoesNotCallCreateLoadCategoriesAsync()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var nodeLoads = new[] { MakeNodeLoad(0, 0, 0, fz: -10) };

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        await _api.DidNotReceive().CreateLoadCategoriesAsync(
            Arg.Any<List<LoadCategoryCreate>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_DuplicateLoadCategories_DeduplicatedAndWarns()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dead Load");
        var cat = new SgLoadCategoryData("Dead");
        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc, fz: -10, loadCategory: cat),
            new SgNodeLoadData(new SgPoint3D(10, 0, 0), lc, fz: -20, loadCategory: cat)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        // Only 1 unique category created
        await _api.Received(1).CreateLoadCategoriesAsync(
            Arg.Is<List<LoadCategoryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        // Warning about duplicate category
        Assert.Contains(result.Warnings,
            w => w.Contains("Dead", StringComparison.OrdinalIgnoreCase) &&
                 w.Contains("load category", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assemble_NodeLoadWithCategory_ReferencesCorrectCategoryId()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dead Load");
        var cat = new SgLoadCategoryData("Dead");
        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc, fz: -10, loadCategory: cat)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        await _api.Received(1).CreateNodeLoadsAsync(
            Arg.Is<List<NodeLoadCreate>>(list =>
                list.Count == 1 && list[0].LoadCategory == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_NodeLoadWithoutCategory_LoadCategoryIsNull()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var nodeLoads = new[] { MakeNodeLoad(0, 0, 0, fz: -10) };

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        await _api.Received(1).CreateNodeLoadsAsync(
            Arg.Is<List<NodeLoadCreate>>(list =>
                list.Count == 1 && list[0].LoadCategory == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_WithLoadCategory_LoadCategoryMapPopulated()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dead Load");
        var cat = new SgLoadCategoryData("Dead");
        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc, fz: -10, loadCategory: cat)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        Assert.Single(result.Model.LoadCategoryMap);
        Assert.True(result.Model.LoadCategoryMap.ContainsKey("Dead"));
    }

    [Fact]
    public async Task Assemble_LoadCategoryApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreateLoadCategoriesAsync(Arg.Any<List<LoadCategoryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<LoadCategory>()); // returns 0 instead of 1

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dead Load");
        var cat = new SgLoadCategoryData("Dead");
        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc, fz: -10, loadCategory: cat)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads));
        Assert.Contains("load categor", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Assemble_MixedCategoryAndNone_OnlyCategorisedCreatesCategories()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dead Load");
        var cat = new SgLoadCategoryData("Dead");
        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc, fz: -10, loadCategory: cat),
            new SgNodeLoadData(new SgPoint3D(10, 0, 0), lc, fz: -20) // no category
        };

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads);

        // Only 1 category created (from first load)
        await _api.Received(1).CreateLoadCategoriesAsync(
            Arg.Is<List<LoadCategoryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        // First node load has category, second doesn't
        await _api.Received(1).CreateNodeLoadsAsync(
            Arg.Is<List<NodeLoadCreate>>(list =>
                list.Count == 2 &&
                list[0].LoadCategory == 1 &&
                list[1].LoadCategory == null),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgMemberDistributedLoadData construction ──────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SgMemberDistributedLoadData_DefaultValues_AreCorrect()
    {
        var lc = new SgLoadCaseData("DL");
        var dl = new SgMemberDistributedLoadData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc);

        Assert.Equal(0, dl.FxStart);
        Assert.Equal(0, dl.FyStart);
        Assert.Equal(0, dl.FzStart);
        Assert.Equal(0, dl.FxEnd);
        Assert.Equal(0, dl.FyEnd);
        Assert.Equal(0, dl.FzEnd);
        Assert.Equal(0, dl.StartPosition);
        Assert.Equal(100, dl.EndPosition);
        Assert.Equal(LoadPositionUnits.Percent, dl.PositionUnits);
        Assert.Equal(LoadAxes.GlobalProjected, dl.Axes);
        Assert.Null(dl.LoadCategory);
        Assert.True(dl.IsZero);
    }

    [Fact]
    public void SgMemberDistributedLoadData_WithValues_StoresAll()
    {
        var lc = new SgLoadCaseData("DL");
        var cat = new SgLoadCategoryData("Dead");
        var dl = new SgMemberDistributedLoadData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
            1, -5, 0,
            2, -10, 0,
            20, 80,
            LoadPositionUnits.Actual,
            LoadAxes.Local,
            cat);

        Assert.Equal(1, dl.FxStart);
        Assert.Equal(-5, dl.FyStart);
        Assert.Equal(2, dl.FxEnd);
        Assert.Equal(-10, dl.FyEnd);
        Assert.Equal(20, dl.StartPosition);
        Assert.Equal(80, dl.EndPosition);
        Assert.Equal(LoadPositionUnits.Actual, dl.PositionUnits);
        Assert.Equal(LoadAxes.Local, dl.Axes);
        Assert.Equal("Dead", dl.LoadCategory!.Name);
        Assert.False(dl.IsZero);
    }

    [Fact]
    public void SgMemberDistributedLoadData_NullLoadCase_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SgMemberDistributedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), null!));
    }

    // ── SgSelfWeightLoadData construction ────────────────────────────────

    [Fact]
    public void SgSelfWeightLoadData_DefaultValues_AreCorrect()
    {
        var lc = new SgLoadCaseData("DL");
        var sw = new SgSelfWeightLoadData(lc);

        Assert.Equal(0, sw.AccelerationX);
        Assert.Equal(-9.81, sw.AccelerationY);
        Assert.Equal(0, sw.AccelerationZ);
        Assert.Null(sw.LoadCategory);
        Assert.False(sw.IsZero);
    }

    [Fact]
    public void SgSelfWeightLoadData_AllZero_IsZeroTrue()
    {
        var lc = new SgLoadCaseData("DL");
        var sw = new SgSelfWeightLoadData(lc, 0, 0, 0);

        Assert.True(sw.IsZero);
    }

    [Fact]
    public void SgSelfWeightLoadData_NullLoadCase_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SgSelfWeightLoadData(null!));
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Assemble: Member Distributed Loads ───────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_MemberDistributedLoad_CreatesLoadCaseAndLoad()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dead Load");
        var distLoads = new[]
        {
            new SgMemberDistributedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
                fyStart: -5, fyEnd: -5)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            memberDistributedLoads: distLoads);

        // Load case created
        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1 && list[0].Title == "Dead Load"),
            Arg.Any<CancellationToken>());

        // Distributed load created with correct member ID and values
        await _api.Received(1).CreateMemberDistributedLoadsAsync(
            Arg.Is<List<MemberDistributedLoadCreate>>(list =>
                list.Count == 1 &&
                list[0].Member == 1 &&
                list[0].FyStart == -5 &&
                list[0].FyFinish == -5),
            Arg.Any<CancellationToken>());

        Assert.Equal(1, result.Model.MemberDistributedLoadCount);
    }

    [Fact]
    public async Task Assemble_MemberDistributedLoad_PassesPositionAndAxes()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("DL");
        var distLoads = new[]
        {
            new SgMemberDistributedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
                fyStart: -10, fyEnd: -10,
                startPosition: 25, endPosition: 75,
                positionUnits: LoadPositionUnits.Percent,
                axes: LoadAxes.Local)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            memberDistributedLoads: distLoads);

        await _api.Received(1).CreateMemberDistributedLoadsAsync(
            Arg.Is<List<MemberDistributedLoadCreate>>(list =>
                list[0].StartPosition == 25 &&
                list[0].FinishPosition == 75 &&
                list[0].PositionUnits == LoadPositionUnits.Percent &&
                list[0].Axes == LoadAxes.Local),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberDistributedLoad_WithCategory_PassesCategoryId()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("DL");
        var cat = new SgLoadCategoryData("Dead");
        var distLoads = new[]
        {
            new SgMemberDistributedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
                fyStart: -5, fyEnd: -5,
                loadCategory: cat)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            memberDistributedLoads: distLoads);

        await _api.Received(1).CreateLoadCategoriesAsync(
            Arg.Is<List<LoadCategoryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        await _api.Received(1).CreateMemberDistributedLoadsAsync(
            Arg.Is<List<MemberDistributedLoadCreate>>(list =>
                list[0].LoadCategory == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberDistributedLoad_UnmatchedMember_WarnsAndSkips()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("DL");
        // Load on a member that doesn't exist in the model
        var distLoads = new[]
        {
            new SgMemberDistributedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(20, 0, 0), lc,
                fyStart: -5, fyEnd: -5)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            memberDistributedLoads: distLoads);

        Assert.Contains(result.Warnings, w => w.Contains("doesn't exist") || w.Contains("don't match"));
        // No distributed loads created
        await _api.DidNotReceive().CreateMemberDistributedLoadsAsync(
            Arg.Any<List<MemberDistributedLoadCreate>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberDistributedLoad_ZeroForce_Warns()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("DL");
        var distLoads = new[]
        {
            new SgMemberDistributedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            memberDistributedLoads: distLoads);

        Assert.Contains(result.Warnings, w => w.Contains("zero force"));
    }

    [Fact]
    public async Task Assemble_SharedLoadCase_BetweenNodeLoadAndDistLoad_DeduplicatesCorrectly()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dead Load");

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

        await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: nodeLoads, memberDistributedLoads: distLoads);

        // Only 1 load case created (shared between both load types)
        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Assemble: Self-Weight Loads ──────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_SelfWeightLoad_CreatesLoadCaseAndLoad()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Self Weight");
        var swLoads = new[]
        {
            new SgSelfWeightLoadData(lc, accelerationY: -9.81)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            selfWeightLoads: swLoads);

        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1 && list[0].Title == "Self Weight"),
            Arg.Any<CancellationToken>());

        await _api.Received(1).CreateSelfWeightLoadsAsync(
            Arg.Is<List<SelfWeightLoadCreate>>(list =>
                list.Count == 1 &&
                list[0].AccelerationX == 0 &&
                list[0].AccelerationY == -9.81 &&
                list[0].AccelerationZ == 0),
            Arg.Any<CancellationToken>());

        Assert.Equal(1, result.Model.SelfWeightLoadCount);
    }

    [Fact]
    public async Task Assemble_SelfWeightLoad_WithCategory_PassesCategoryId()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("SW");
        var cat = new SgLoadCategoryData("Dead");
        var swLoads = new[]
        {
            new SgSelfWeightLoadData(lc, accelerationY: -9.81, loadCategory: cat)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            selfWeightLoads: swLoads);

        await _api.Received(1).CreateLoadCategoriesAsync(
            Arg.Is<List<LoadCategoryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        await _api.Received(1).CreateSelfWeightLoadsAsync(
            Arg.Is<List<SelfWeightLoadCreate>>(list =>
                list[0].LoadCategory == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_SelfWeightLoad_ZeroAcceleration_Warns()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("SW");
        var swLoads = new[]
        {
            new SgSelfWeightLoadData(lc, 0, 0, 0)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            selfWeightLoads: swLoads);

        Assert.Contains(result.Warnings, w => w.Contains("zero acceleration"));
    }

    [Fact]
    public async Task Assemble_SelfWeightOnly_NoNodeLoads_StillCreatesLoadCase()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("SW");
        var swLoads = new[]
        {
            new SgSelfWeightLoadData(lc)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            selfWeightLoads: swLoads);

        // Load case still created even without node loads
        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        // No node loads created
        await _api.DidNotReceive().CreateNodeLoadsAsync(
            Arg.Any<List<NodeLoadCreate>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_AllThreeLoadTypes_SharedLoadCase_OnlyOneCreated()
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

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: nodeLoads, memberDistributedLoads: distLoads, selfWeightLoads: swLoads);

        // Only 1 load case — shared across all three
        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        Assert.Equal(1, result.Model.NodeLoadCount);
        Assert.Equal(1, result.Model.MemberDistributedLoadCount);
        Assert.Equal(1, result.Model.SelfWeightLoadCount);
    }
}