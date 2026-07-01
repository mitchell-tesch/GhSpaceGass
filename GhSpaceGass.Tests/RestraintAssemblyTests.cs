using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class RestraintAssemblyTests
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

    private static SgRestraintData MakeRestraint(
        double x, double y, double z,
        string code = "FFFRRR")
    {
        return new SgRestraintData(new SgPoint3D(x, y, z), code);
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
    }

    // ── SgRestraintData construction ──────────────────────────────────

    [Fact]
    public void SgRestraintData_ValidCode_Stores()
    {
        var r = new SgRestraintData(new SgPoint3D(0, 0, 0), "FFFRRR");

        Assert.Equal("FFFRRR", r.RestraintCode);
        Assert.Equal(new SgPoint3D(0, 0, 0), r.Point);
    }

    [Fact]
    public void SgRestraintData_LowercaseCode_NormalisedToUpper()
    {
        var r = new SgRestraintData(new SgPoint3D(0, 0, 0), "fffrrr");
        Assert.Equal("FFFRRR", r.RestraintCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("FFF")]
    [InlineData("FFFFFFR")] // 7 chars
    public void SgRestraintData_InvalidCodeLength_Throws(string code)
    {
        Assert.Throws<ArgumentException>(() => new SgRestraintData(new SgPoint3D(0, 0, 0), code));
    }

    [Fact]
    public void SgRestraintData_NullCode_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SgRestraintData(new SgPoint3D(0, 0, 0), null!));
    }

    [Theory]
    [InlineData("XYZABC")]
    [InlineData("FFF123")]
    [InlineData("FFFFF ")]
    public void SgRestraintData_InvalidCodeCharacters_Throws(string code)
    {
        Assert.Throws<ArgumentException>(() => new SgRestraintData(new SgPoint3D(0, 0, 0), code));
    }

    // ── Restraint pushed to API ───────────────────────────────────────

    [Fact]
    public async Task Assemble_WithRestraints_CallsCreateNodeRestraintsAsync()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var restraints = new[] { MakeRestraint(0, 0, 0, "FFFFFF") };

        await _assembler.AssembleAsync(_api, members, Tolerance, restraints);

        await _api.Received(1).CreateNodeRestraintsAsync(
            Arg.Is<List<NodeRestraintCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_WithRestraints_SendsCorrectRestraintCode()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var restraints = new[] { MakeRestraint(0, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance, restraints);

        await _api.Received(1).CreateNodeRestraintsAsync(
            Arg.Is<List<NodeRestraintCreate>>(list =>
                list[0].RestraintCode == "FFFRRR"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_RestraintAtMemberEndpoint_ResolvesToSameNode()
    {
        SetupApiReturns();

        // Member from (0,0,0) to (10,0,0) — nodes will get IDs 1 and 2
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        // Restraint at start of member
        var restraints = new[] { MakeRestraint(0, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance, restraints);

        // Restraint should reference node 1 (the start node)
        await _api.Received(1).CreateNodeRestraintsAsync(
            Arg.Is<List<NodeRestraintCreate>>(list =>
                list.Count == 1 && list[0].Node == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_RestraintWithinTolerance_ResolvesToExistingNode()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        // Restraint slightly off from (0,0,0) but within tolerance
        var restraints = new[] { MakeRestraint(0.0005, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance, restraints);

        // Should NOT create extra nodes — still 2 nodes
        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 2),
            Arg.Any<CancellationToken>());

        // Should resolve to node 1
        await _api.Received(1).CreateNodeRestraintsAsync(
            Arg.Is<List<NodeRestraintCreate>>(list =>
                list.Count == 1 && list[0].Node == 1),
            Arg.Any<CancellationToken>());
    }

    // ── Orphan points (ADR-0002) ──────────────────────────────────────

    [Fact]
    public async Task Assemble_OrphanRestraintPoint_CreatesStandaloneNode()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        // Restraint at (5,5,0) — not near any member endpoint
        var restraints = new[] { MakeRestraint(5, 5, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance, restraints);

        // Should create 3 nodes: 2 from member + 1 orphan
        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_OrphanRestraintPoint_EmitsWarning()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var restraints = new[] { MakeRestraint(5, 5, 0) };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance, restraints);

        Assert.Contains(result.Warnings,
            w => w.Contains("orphan", StringComparison.OrdinalIgnoreCase) ||
                 w.Contains("5", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assemble_MultipleOrphanRestraintPoints_AllCreateNodes()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var restraints = new[]
        {
            MakeRestraint(5, 5, 0),
            MakeRestraint(5, -5, 0)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance, restraints);

        // 2 member endpoints + 2 orphans = 4 nodes
        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 4),
            Arg.Any<CancellationToken>());
    }

    // ── Dependency order ──────────────────────────────────────────────

    [Fact]
    public async Task Assemble_WithRestraints_PushesRestraintsAfterNodesAndMembers()
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
        _api.CreateNodeRestraintsAsync(Arg.Any<List<NodeRestraintCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                callOrder.Add("restraints");
                var input = (List<NodeRestraintCreate>)args[0];
                return input.Select((r, i) => new NodeRestraint { Node = r.Node, RestraintCode = r.RestraintCode })
                    .ToList();
            });

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var restraints = new[] { MakeRestraint(0, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance, restraints);

        Assert.Equal(new[] { "clear", "materials", "sections", "nodes", "members", "restraints" }, callOrder);
    }

    // ── No restraints ─────────────────────────────────────────────────

    [Fact]
    public async Task Assemble_NoRestraints_DoesNotCallRestraintApi()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.DidNotReceive().CreateNodeRestraintsAsync(
            Arg.Any<List<NodeRestraintCreate>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_EmptyRestraintList_DoesNotCallRestraintApi()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var restraints = Array.Empty<SgRestraintData>();

        await _assembler.AssembleAsync(_api, members, Tolerance, restraints);

        await _api.DidNotReceive().CreateNodeRestraintsAsync(
            Arg.Any<List<NodeRestraintCreate>>(),
            Arg.Any<CancellationToken>());
    }

    // ── Model output includes restraints ──────────────────────────────

    [Fact]
    public async Task Assemble_WithRestraints_RestraintMapPopulated()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var restraints = new[]
        {
            MakeRestraint(0, 0, 0, "FFFFFF"),
            MakeRestraint(10, 0, 0)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance, restraints);

        Assert.Equal(2, result.Model.RestraintMap.Count);
        Assert.Equal("FFFFFF", result.Model.RestraintMap[new SgPoint3D(0, 0, 0)]);
        Assert.Equal("FFFRRR", result.Model.RestraintMap[new SgPoint3D(10, 0, 0)]);
    }

    // ── Multiple restraints at same node ──────────────────────────────

    [Fact]
    public async Task Assemble_MultipleRestraintsAtSamePoint_AllSentToApi()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        // Two restraints at the same point — both should be sent
        var restraints = new[]
        {
            MakeRestraint(0, 0, 0, "FFFFFF"),
            MakeRestraint(0, 0, 0)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance, restraints);

        await _api.Received(1).CreateNodeRestraintsAsync(
            Arg.Is<List<NodeRestraintCreate>>(list => list.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MultipleRestraintsAtSamePoint_EmitsWarning()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var restraints = new[]
        {
            MakeRestraint(0, 0, 0, "FFFFFF"),
            MakeRestraint(0, 0, 0)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance, restraints);

        Assert.Contains(result.Warnings,
            w => w.Contains("Multiple restraints", StringComparison.OrdinalIgnoreCase) &&
                 w.Contains("same node", StringComparison.OrdinalIgnoreCase));
    }

    // ── Partial failure ───────────────────────────────────────────────

    [Fact]
    public async Task Assemble_RestraintApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreateNodeRestraintsAsync(Arg.Any<List<NodeRestraintCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<NodeRestraint>()); // returns 0 instead of 1

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var restraints = new[] { MakeRestraint(0, 0, 0) };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, members, Tolerance, restraints));
        Assert.Contains("restraint", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}