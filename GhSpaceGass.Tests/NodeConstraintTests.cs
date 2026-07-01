using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class NodeConstraintTests
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
                return input.Select((r, i) => new NodeRestraint { Node = r.Node }).ToList();
            });

        _api.CreateNodeConstraintsAsync(Arg.Any<List<NodeConstraintCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<NodeConstraintCreate>)args[0];
                return input.Select((c, i) => new NodeConstraint
                {
                    SlaveNode = c.SlaveNode, MasterNode = c.MasterNode, ConstraintCode = c.ConstraintCode
                }).ToList();
            });
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgNodeConstraintData construction ─────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SgNodeConstraintData_StoresAllProperties()
    {
        var data = new SgNodeConstraintData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), "FFFFFF");

        Assert.Equal(new SgPoint3D(0, 0, 0), data.SlavePoint);
        Assert.Equal(new SgPoint3D(10, 0, 0), data.MasterPoint);
        Assert.Equal("FFFFFF", data.ConstraintCode);
        Assert.Equal(ConstraintAxes.Global, data.Axes);
        Assert.Null(data.XVector);
        Assert.Null(data.YVector);
        Assert.Null(data.ZVector);
    }

    [Fact]
    public void SgNodeConstraintData_WithInclinedAxes_StoresDirectionVector()
    {
        var data = new SgNodeConstraintData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), "FFFRRR",
            axes: ConstraintAxes.Inclined, xVector: 1, yVector: 0, zVector: 0);

        Assert.Equal(ConstraintAxes.Inclined, data.Axes);
        Assert.Equal(1, data.XVector);
        Assert.Equal(0, data.YVector);
        Assert.Equal(0, data.ZVector);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("FFF")]
    [InlineData("FFFFFFX")]
    public void SgNodeConstraintData_InvalidCode_Throws(string? code)
    {
        Assert.Throws<ArgumentException>(() =>
            new SgNodeConstraintData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), code!));
    }

    [Fact]
    public void SgNodeConstraintData_InvalidCharInCode_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SgNodeConstraintData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), "FFFXRR"));
    }

    [Fact]
    public void SgNodeConstraintData_LowercaseCode_NormalisedToUppercase()
    {
        var data = new SgNodeConstraintData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), "fffrrr");
        Assert.Equal("FFFRRR", data.ConstraintCode);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Assemble: Node Constraints ───────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_WithConstraints_CallsCreateNodeConstraintsAsync()
    {
        SetupApiReturns();

        // Two-member frame: (0,0,0)→(10,0,0) and (10,0,0)→(10,10,0)
        var members = new[]
        {
            MakeMember(0, 0, 0, 10, 0, 0),
            MakeMember(10, 0, 0, 10, 10, 0)
        };
        var constraints = new[]
        {
            new SgNodeConstraintData(
                new SgPoint3D(10, 0, 0), // slave — shared node
                new SgPoint3D(0, 0, 0), // master
                "FFFFFF")
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeConstraints: constraints);

        await _api.Received(1).CreateNodeConstraintsAsync(
            Arg.Is<List<NodeConstraintCreate>>(list =>
                list.Count == 1 &&
                list[0].ConstraintCode == "FFFFFF"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_Constraint_ResolvesSlaveAndMasterToCorrectNodeIds()
    {
        SetupApiReturns();

        // Member from (0,0,0)[node 1] to (10,0,0)[node 2]
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var constraints = new[]
        {
            new SgNodeConstraintData(
                new SgPoint3D(10, 0, 0), // slave = node 2
                new SgPoint3D(0, 0, 0), // master = node 1
                "FFFRRR")
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeConstraints: constraints);

        await _api.Received(1).CreateNodeConstraintsAsync(
            Arg.Is<List<NodeConstraintCreate>>(list =>
                list[0].SlaveNode == 2 && list[0].MasterNode == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_Constraint_WithInclinedAxes_PassesDirection()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var constraints = new[]
        {
            new SgNodeConstraintData(
                new SgPoint3D(10, 0, 0), new SgPoint3D(0, 0, 0), "FFFFFF",
                axes: ConstraintAxes.Inclined, xVector: 1, yVector: 0, zVector: 0)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeConstraints: constraints);

        await _api.Received(1).CreateNodeConstraintsAsync(
            Arg.Is<List<NodeConstraintCreate>>(list =>
                list[0].Axes == ConstraintAxes.Inclined &&
                list[0].XVector == 1 &&
                list[0].YVector == 0 &&
                list[0].ZVector == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_OrphanConstraintPoint_CreatesStandaloneNode()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        // Slave point not on any member endpoint
        var constraints = new[]
        {
            new SgNodeConstraintData(
                new SgPoint3D(5, 5, 0), // orphan slave
                new SgPoint3D(0, 0, 0), // master at member start
                "FFFFFF")
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeConstraints: constraints);

        // 3 nodes: 2 from member + 1 orphan
        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 3),
            Arg.Any<CancellationToken>());

        Assert.Contains(result.Warnings,
            w => w.Contains("orphan", StringComparison.OrdinalIgnoreCase) ||
                 w.Contains("5.000", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assemble_DuplicateConstraintOnSameSlaveNode_Warns()
    {
        SetupApiReturns();

        var members = new[]
        {
            MakeMember(0, 0, 0, 10, 0, 0),
            MakeMember(10, 0, 0, 20, 0, 0)
        };
        var constraints = new[]
        {
            new SgNodeConstraintData(new SgPoint3D(10, 0, 0), new SgPoint3D(0, 0, 0), "FFFFFF"),
            new SgNodeConstraintData(new SgPoint3D(10, 0, 0), new SgPoint3D(20, 0, 0), "FFFRRR")
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeConstraints: constraints);

        Assert.Contains(result.Warnings,
            w => w.Contains("Multiple constraints", StringComparison.OrdinalIgnoreCase) ||
                 w.Contains("same", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assemble_Constraint_DependencyOrder_AfterRestraintsBeforeLoads()
    {
        SetupApiReturns();

        _api.CreateLoadCasesAsync(Arg.Any<List<LoadCaseCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<LoadCaseCreate>)args[0];
                return input.Select((lc, i) => new LoadCase { Id = i + 1, Title = lc.Title }).ToList();
            });
        _api.CreateNodeLoadsAsync(Arg.Any<List<NodeLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<NodeLoadCreate>)args[0];
                return input.Select((nl, i) => new NodeLoad { Node = nl.Node, LoadCase = nl.LoadCase }).ToList();
            });

        var callOrder = new List<string>();
        _api.CreateNodeRestraintsAsync(Arg.Any<List<NodeRestraintCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("restraints");
                var input = (List<NodeRestraintCreate>)args[0];
                return input.Select((r, i) => new NodeRestraint { Node = r.Node }).ToList();
            });
        _api.CreateNodeConstraintsAsync(Arg.Any<List<NodeConstraintCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("constraints");
                var input = (List<NodeConstraintCreate>)args[0];
                return input.Select((c, i) => new NodeConstraint { SlaveNode = c.SlaveNode }).ToList();
            });
        _api.CreateLoadCasesAsync(Arg.Any<List<LoadCaseCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("loadcases");
                var input = (List<LoadCaseCreate>)args[0];
                return input.Select((lc, i) => new LoadCase { Id = i + 1, Title = lc.Title }).ToList();
            });

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var restraints = new[] { new SgRestraintData(new SgPoint3D(0, 0, 0), "FFFRRR") };
        var constraints = new[]
        {
            new SgNodeConstraintData(new SgPoint3D(10, 0, 0), new SgPoint3D(0, 0, 0), "FFFFFF")
        };
        var lc = new SgLoadCaseData("DL");
        var nodeLoads = new[] { new SgNodeLoadData(new SgPoint3D(10, 0, 0), lc, fz: -10) };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            restraints, nodeLoads, nodeConstraints: constraints);

        Assert.Equal(new[] { "restraints", "constraints", "loadcases" }, callOrder);
    }

    [Fact]
    public async Task Assemble_ConstraintApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreateNodeConstraintsAsync(
                Arg.Any<List<NodeConstraintCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<NodeConstraint>());

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var constraints = new[]
        {
            new SgNodeConstraintData(new SgPoint3D(10, 0, 0), new SgPoint3D(0, 0, 0), "FFFFFF")
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, members, Tolerance,
                nodeConstraints: constraints));
        Assert.Contains("constraint", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Assemble_NoConstraints_DoesNotCallApi()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.DidNotReceive().CreateNodeConstraintsAsync(
            Arg.Any<List<NodeConstraintCreate>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_WithConstraints_ConstraintCountSet()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var constraints = new[]
        {
            new SgNodeConstraintData(new SgPoint3D(10, 0, 0), new SgPoint3D(0, 0, 0), "FFFFFF")
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeConstraints: constraints);

        Assert.Equal(1, result.Model.ConstraintCount);
    }
}

