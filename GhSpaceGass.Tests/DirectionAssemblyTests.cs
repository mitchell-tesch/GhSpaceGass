using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

/// <summary>
///     Tests for member direction/orientation support (Slice 13 / ADR-0010).
///     Covers SgDirectionData construction, SgMemberData direction property,
///     and ModelAssembler mapping of direction to MemberCreate.Direction.
/// </summary>
public class DirectionAssemblyTests
{
    private const double Tolerance = 0.001;

    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ModelAssembler _assembler = new();

    // ── Helpers ───────────────────────────────────────────────────────

    private static SgMemberData MakeMember(
        double x1, double y1, double z1,
        double x2, double y2, double z2,
        SgDirectionData? direction = null,
        string sectionLibrary = "Aust300",
        string sectionName = "360 UB 44.7",
        string materialLibrary = "Aust",
        string materialName = "STEEL")
    {
        return new SgMemberData(
            new SgPoint3D(x1, y1, z1),
            new SgPoint3D(x2, y2, z2),
            new SgSectionData(sectionLibrary, sectionName),
            new SgMaterialData(materialLibrary, materialName),
            direction: direction);
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
    }

    // ═══════════════════════════════════════════════════════════════════
    // SgDirectionData construction tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void SgDirectionData_DefaultAngle_StoresZeroAngle()
    {
        var d = SgDirectionData.FromAngle(0);
        Assert.Equal(0.0, d.Angle);
        Assert.Equal(DirectionAxis.NotApplicable, d.Axis);
        Assert.Null(d.NodePoint);
    }

    [Fact]
    public void SgDirectionData_CustomAngle_StoresAngle()
    {
        var d = SgDirectionData.FromAngle(45.0);
        Assert.Equal(45.0, d.Angle);
        Assert.Equal(DirectionAxis.NotApplicable, d.Axis);
        Assert.Null(d.NodePoint);
    }

    [Fact]
    public void SgDirectionData_FromAxis_StoresAxis()
    {
        var d = SgDirectionData.FromAxis(DirectionAxis.YAxis);
        Assert.Equal(DirectionAxis.YAxis, d.Axis);
        Assert.Equal(0.0, d.Angle);
        Assert.Null(d.NodePoint);
    }

    [Fact]
    public void SgDirectionData_FromNode_StoresNodePoint()
    {
        var pt = new SgPoint3D(5, 5, 0);
        var d = SgDirectionData.FromNode(pt);
        Assert.NotNull(d.NodePoint);
        Assert.Equal(pt, d.NodePoint!.Value);
        Assert.Equal(DirectionAxis.NotApplicable, d.Axis);
        Assert.Equal(0.0, d.Angle);
    }

    // ═══════════════════════════════════════════════════════════════════
    // SgMemberData with direction
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void SgMemberData_NoDirection_DefaultsToNull()
    {
        var m = new SgMemberData(
            new SgPoint3D(0, 0, 0),
            new SgPoint3D(10, 0, 0),
            new SgSectionData("Aust300", "360 UB 44.7"),
            new SgMaterialData("Aust", "STEEL"));

        Assert.Null(m.Direction);
    }

    [Fact]
    public void SgMemberData_WithDirection_StoresDirection()
    {
        var dir = SgDirectionData.FromAngle(30.0);
        var m = new SgMemberData(
            new SgPoint3D(0, 0, 0),
            new SgPoint3D(10, 0, 0),
            new SgSectionData("Aust300", "360 UB 44.7"),
            new SgMaterialData("Aust", "STEEL"),
            direction: dir);

        Assert.NotNull(m.Direction);
        Assert.Equal(30.0, m.Direction!.Angle);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ModelAssembler — direction mapped to API
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_MemberWithNoDirection_DirectionIsNull()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list.Count == 1 && list[0].Direction == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberWithAngleDirection_SetsDirAngle()
    {
        SetupApiReturns();

        var dir = SgDirectionData.FromAngle(45.0);
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, dir) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list[0].Direction != null &&
                list[0].Direction.DirAngle == 45.0 &&
                list[0].Direction.DirAxis == DirectionAxis.NotApplicable),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberWithAxisDirection_SetsDirAxis()
    {
        SetupApiReturns();

        var dir = SgDirectionData.FromAxis(DirectionAxis.ZAxis);
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, dir) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list[0].Direction != null &&
                list[0].Direction.DirAxis == DirectionAxis.ZAxis &&
                list[0].Direction.DirAngle == 0.0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberWithNodeDirection_SetsDirNode()
    {
        SetupApiReturns();

        // Direction node at (5,5,0) — not a member endpoint
        var dir = SgDirectionData.FromNode(new SgPoint3D(5, 5, 0));
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, dir) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        // Should create 3 nodes: 2 member endpoints + 1 direction node
        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 3),
            Arg.Any<CancellationToken>());

        // Direction node should be referenced in MemberCreate.Direction.DirNode
        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list[0].Direction != null &&
                list[0].Direction.DirNode != null &&
                list[0].Direction.DirNode > 0 &&
                list[0].Direction.DirAxis == DirectionAxis.NotApplicable),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberWithNodeDirection_DirNodeResolvesToCorrectNodeId()
    {
        SetupApiReturns();

        // Direction node at (5,5,0) will be the 3rd node created (IDs: 1,2,3)
        var dir = SgDirectionData.FromNode(new SgPoint3D(5, 5, 0));
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, dir) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list[0].Direction!.DirNode == 3), // 3rd node
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_DirectionNodeCoincidentWithMemberEndpoint_DoesNotCreateExtraNode()
    {
        SetupApiReturns();

        // Direction node coincident with member start (0,0,0)
        var dir = SgDirectionData.FromNode(new SgPoint3D(0, 0, 0));
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, dir) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        // Only 2 nodes — direction node merged with member start
        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_TwoMembersShareDirectionNode_OneNodeCreated()
    {
        SetupApiReturns();

        var dirPt = new SgPoint3D(5, 5, 0);
        var dir = SgDirectionData.FromNode(dirPt);

        var members = new[]
        {
            MakeMember(0, 0, 0, 10, 0, 0, dir),
            MakeMember(10, 0, 0, 20, 0, 0, dir)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        // 3 member endpoints + 1 shared direction node = 4 unique nodes
        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 4),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberWithDefaultAngleZero_DirectionIsNull()
    {
        SetupApiReturns();

        // Angle=0, Axis=NotApplicable, no node — this is the SpaceGass default, skip it
        var dir = SgDirectionData.FromAngle(0.0);
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, dir) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        // Default direction (angle=0, axis=NA, no node) should be skipped
        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list[0].Direction == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberWithNonZeroAngle_DirectionIsSet()
    {
        SetupApiReturns();

        var dir = SgDirectionData.FromAngle(90.0);
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, dir) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list[0].Direction != null &&
                list[0].Direction.DirAngle == 90.0),
            Arg.Any<CancellationToken>());
    }
}