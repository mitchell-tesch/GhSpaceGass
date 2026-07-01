using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

/// <summary>
///     Tests verifying that the polyline-splitting pattern (multiple sequential SgMemberData
///     objects sharing intermediate endpoints) assembles correctly via ModelAssembler.
///     These tests pin the core behaviour that the Create Member component relies on
///     when splitting a polyline into N-1 members (Slice 12 / ADR-0003).
/// </summary>
public class PolylineMemberAssemblyTests
{
    private const double Tolerance = 0.001;

    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ModelAssembler _assembler = new();

    // ── Helpers ───────────────────────────────────────────────────────

    private static SgMemberData MakeMember(
        double x1, double y1, double z1,
        double x2, double y2, double z2,
        SgReleaseData? releaseA = null,
        SgReleaseData? releaseB = null,
        MemberType? type = null,
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
            type,
            releaseA,
            releaseB);
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
    // Polyline chain pattern: N-1 members from N vertices
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_ThreeSegmentChain_Creates4NodesAnd3Members()
    {
        SetupApiReturns();

        // Polyline: (0,0,0) → (3,0,0) → (6,0,0) → (9,0,0) = 4 vertices, 3 segments
        var members = new[]
        {
            MakeMember(0, 0, 0, 3, 0, 0),
            MakeMember(3, 0, 0, 6, 0, 0),
            MakeMember(6, 0, 0, 9, 0, 0)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        // 4 unique nodes (intermediate nodes shared)
        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 4),
            Arg.Any<CancellationToken>());

        // 3 members
        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list => list.Count == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_ThreeSegmentChain_IntermediateNodesConnectCorrectly()
    {
        SetupApiReturns();

        var members = new[]
        {
            MakeMember(0, 0, 0, 3, 0, 0),
            MakeMember(3, 0, 0, 6, 0, 0),
            MakeMember(6, 0, 0, 9, 0, 0)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        // Verify member connectivity: member 1 ends at the same node member 2 starts at
        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list[0].NodeB == list[1].NodeA &&
                list[1].NodeB == list[2].NodeA),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_ChainMembersShareSectionAndMaterial_SingleSectionAndMaterial()
    {
        SetupApiReturns();

        // All 3 segments from same polyline share the same section and material
        var members = new[]
        {
            MakeMember(0, 0, 0, 3, 0, 0),
            MakeMember(3, 0, 0, 6, 0, 0),
            MakeMember(6, 0, 0, 9, 0, 0)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        // Only 1 unique section and 1 unique material (deduplicated)
        await _api.Received(1).CreateSectionsFromLibraryAsync(
            Arg.Is<List<SectionLibraryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
        await _api.Received(1).CreateMaterialsFromLibraryAsync(
            Arg.Is<List<MaterialLibraryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_ChainMembersWithType_AllMembersGetSameType()
    {
        SetupApiReturns();

        // All segments from a polyline inherit the same type
        var members = new[]
        {
            MakeMember(0, 0, 0, 3, 0, 0, type: MemberType.Truss),
            MakeMember(3, 0, 0, 6, 0, 0, type: MemberType.Truss),
            MakeMember(6, 0, 0, 9, 0, 0, type: MemberType.Truss)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list.All(m => m.Type == MemberType.Truss)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_ChainMembersWithReleases_AllMembersGetSameReleases()
    {
        SetupApiReturns();

        // All segments inherit the same releases from the polyline
        var releaseA = new SgReleaseData("FFFFFR");
        var releaseB = new SgReleaseData("FFFRRR");

        var members = new[]
        {
            MakeMember(0, 0, 0, 3, 0, 0, releaseA, releaseB),
            MakeMember(3, 0, 0, 6, 0, 0, releaseA, releaseB),
            MakeMember(6, 0, 0, 9, 0, 0, releaseA, releaseB)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list.All(m =>
                    m.Releases != null &&
                    m.Releases.FixityCodeAtA == "FFFFFR" &&
                    m.Releases.FixityCodeAtB == "FFFRRR")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_ChainMemberMap_AllSegmentsRecordedInMemberMap()
    {
        SetupApiReturns();

        var members = new[]
        {
            MakeMember(0, 0, 0, 3, 0, 0),
            MakeMember(3, 0, 0, 6, 0, 0),
            MakeMember(6, 0, 0, 9, 0, 0)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance);

        // All 3 members should be in the member map
        Assert.Equal(3, result.Model.MemberMap.Count);
    }

    // ── Non-collinear polyline (L-shape) ──────────────────────────────

    [Fact]
    public async Task Assemble_LShapePolyline_Creates3NodesAnd2Members()
    {
        SetupApiReturns();

        // L-shape: (0,0,0) → (5,0,0) → (5,5,0) = 3 vertices, 2 segments
        var members = new[]
        {
            MakeMember(0, 0, 0, 5, 0, 0),
            MakeMember(5, 0, 0, 5, 5, 0)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 3),
            Arg.Any<CancellationToken>());
        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list => list.Count == 2),
            Arg.Any<CancellationToken>());
    }

    // ── Two-vertex polyline (equivalent to a single line) ─────────────

    [Fact]
    public async Task Assemble_SingleSegmentFromPolyline_Creates2NodesAnd1Member()
    {
        SetupApiReturns();

        // 2-vertex polyline = single segment
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 2),
            Arg.Any<CancellationToken>());
        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }
}