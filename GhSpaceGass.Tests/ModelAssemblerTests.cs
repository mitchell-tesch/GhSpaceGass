using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class ModelAssemblerTests
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

    /// <summary>
    ///     Configures the mock API to return sequential IDs for each bulk create call.
    /// </summary>
    private void SetupApiReturns()
    {
        // Materials: return Material with incrementing IDs
        _api.CreateMaterialsFromLibraryAsync(Arg.Any<List<MaterialLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MaterialLibraryCreate>)args[0];
                var result = new List<Material>();
                for (var i = 0; i < input.Count; i++)
                    result.Add(new Material { Id = i + 1 });
                return result;
            });

        // Sections: return Section with incrementing IDs
        _api.CreateSectionsFromLibraryAsync(Arg.Any<List<SectionLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<SectionLibraryCreate>)args[0];
                var result = new List<Section>();
                for (var i = 0; i < input.Count; i++)
                    result.Add(new Section { Id = i + 1 });
                return result;
            });

        // Nodes: return Node with incrementing IDs
        _api.CreateNodesAsync(Arg.Any<List<NodeCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<NodeCreate>)args[0];
                var result = new List<Node>();
                for (var i = 0; i < input.Count; i++)
                    result.Add(new Node { Id = i + 1, X = input[i].X, Y = input[i].Y, Z = input[i].Z });
                return result;
            });

        // Members: return Member with incrementing IDs
        _api.CreateMembersAsync(Arg.Any<List<MemberCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MemberCreate>)args[0];
                var result = new List<Member>();
                for (var i = 0; i < input.Count; i++)
                    result.Add(new Member { Id = i + 1, NodeA = input[i].NodeA, NodeB = input[i].NodeB });
                return result;
            });
    }

    // ── Clear & Rebuild (ADR-0001) ────────────────────────────────────

    [Fact]
    public async Task Assemble_ClearsExistingDataFirst()
    {
        SetupApiReturns();
        var members = new[] { MakeMember(0, 0, 0, 1, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).ClearJobDataAsync(Arg.Any<CancellationToken>());
    }

    // ── Dependency order ──────────────────────────────────────────────

    [Fact]
    public async Task Assemble_PushesInDependencyOrder()
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

        var members = new[] { MakeMember(0, 0, 0, 1, 0, 0) };
        await _assembler.AssembleAsync(_api, members, Tolerance);

        Assert.Equal(new[] { "clear", "materials", "sections", "nodes", "members" }, callOrder);
    }

    // ── Node deduplication ────────────────────────────────────────────

    [Fact]
    public async Task Assemble_DeduplicatesCoincidentNodes()
    {
        SetupApiReturns();

        // Two members sharing a common point at (1,0,0)
        var members = new[]
        {
            MakeMember(0, 0, 0, 1, 0, 0),
            MakeMember(1, 0, 0, 2, 0, 0)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        // Should create 3 unique nodes, not 4
        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MergesPointsWithinTolerance()
    {
        SetupApiReturns();

        // Two members with endpoints nearly coincident (within tolerance)
        var members = new[]
        {
            MakeMember(0, 0, 0, 1, 0, 0),
            MakeMember(1.0005, 0, 0, 2, 0, 0) // within 0.001 tolerance? No, 0.0005 < 0.001 → yes
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        // Points at (1,0,0) and (1.0005,0,0) are within tolerance → merged → 3 nodes
        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 3),
            Arg.Any<CancellationToken>());
    }

    // ── Section deduplication (ADR-0006) ──────────────────────────────

    [Fact]
    public async Task Assemble_DeduplicatesSections_KeepsUnique()
    {
        SetupApiReturns();

        // Two members with the same section name
        var members = new[]
        {
            MakeMember(0, 0, 0, 1, 0, 0, sectionName: "360 UB 44.7"),
            MakeMember(1, 0, 0, 2, 0, 0, sectionName: "360 UB 44.7")
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        // Should only create 1 section, not 2
        await _api.Received(1).CreateSectionsFromLibraryAsync(
            Arg.Is<List<SectionLibraryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_DuplicateSections_ReturnsWarning()
    {
        SetupApiReturns();

        var members = new[]
        {
            MakeMember(0, 0, 0, 1, 0, 0, sectionName: "360 UB 44.7"),
            MakeMember(1, 0, 0, 2, 0, 0, sectionName: "360 UB 44.7")
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance);

        Assert.Contains(result.Warnings,
            w => w.Contains("360 UB 44.7") && w.Contains("section", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assemble_SameSectionDifferentOverrides_KeptSeparate()
    {
        SetupApiReturns();

        var sec1 = new SgSectionData("Aust300", "360 UB 44.7") { AreaFactor = 0.8 };
        var sec2 = new SgSectionData("Aust300", "360 UB 44.7") { AreaFactor = 1.0 };
        var mat = new SgMaterialData("Aust", "STEEL");

        var members = new[]
        {
            new SgMemberData(new SgPoint3D(0, 0, 0), new SgPoint3D(1, 0, 0), sec1, mat),
            new SgMemberData(new SgPoint3D(1, 0, 0), new SgPoint3D(2, 0, 0), sec2, mat)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        // Two distinct sections despite same Library::Name — different AreaFactor
        await _api.Received(1).CreateSectionsFromLibraryAsync(
            Arg.Is<List<SectionLibraryCreate>>(list => list.Count == 2),
            Arg.Any<CancellationToken>());
    }

    // ── Material deduplication (ADR-0006) ─────────────────────────────

    [Fact]
    public async Task Assemble_DeduplicatesMaterials_KeepsUnique()
    {
        SetupApiReturns();

        var members = new[]
        {
            MakeMember(0, 0, 0, 1, 0, 0, materialName: "STEEL"),
            MakeMember(1, 0, 0, 2, 0, 0, materialName: "STEEL")
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        // Should only create 1 material, not 2
        await _api.Received(1).CreateMaterialsFromLibraryAsync(
            Arg.Is<List<MaterialLibraryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_DuplicateMaterials_ReturnsWarning()
    {
        SetupApiReturns();

        var members = new[]
        {
            MakeMember(0, 0, 0, 1, 0, 0, materialName: "STEEL"),
            MakeMember(1, 0, 0, 2, 0, 0, materialName: "STEEL")
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance);

        Assert.Contains(result.Warnings,
            w => w.Contains("STEEL") && w.Contains("material", StringComparison.OrdinalIgnoreCase));
    }

    // ── Member creation ──────────────────────────────────────────────

    [Fact]
    public async Task Assemble_CreatesMembersWithCorrectNodeReferences()
    {
        SetupApiReturns();

        // Single member from (0,0,0) to (5,0,0)
        var members = new[] { MakeMember(0, 0, 0, 5, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list.Count == 1 &&
                list[0].NodeA != null &&
                list[0].NodeB != null &&
                list[0].NodeA != list[0].NodeB),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MembersReferenceCorrectSectionAndMaterial()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 5, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list.Count == 1 &&
                list[0].Section != null &&
                list[0].Material != null),
            Arg.Any<CancellationToken>());
    }

    // ── Result model ─────────────────────────────────────────────────

    [Fact]
    public async Task Assemble_ReturnsModelWithNodeMap()
    {
        SetupApiReturns();

        var members = new[]
        {
            MakeMember(0, 0, 0, 1, 0, 0),
            MakeMember(1, 0, 0, 2, 0, 0)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance);

        // 3 unique nodes
        Assert.Equal(3, result.Model.NodeMap.Count);
    }

    [Fact]
    public async Task Assemble_ReturnsModelWithMemberMap()
    {
        SetupApiReturns();

        var members = new[]
        {
            MakeMember(0, 0, 0, 1, 0, 0),
            MakeMember(1, 0, 0, 2, 0, 0)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance);

        Assert.Equal(2, result.Model.MemberMap.Count);
    }

    [Fact]
    public async Task Assemble_ReturnsModelWithSectionAndMaterialMaps()
    {
        SetupApiReturns();

        var members = new[]
        {
            MakeMember(0, 0, 0, 1, 0, 0, sectionName: "360 UB 44.7", materialName: "STEEL"),
            MakeMember(1, 0, 0, 2, 0, 0, sectionName: "200 UB 25.4", materialName: "ALUMINIUM")
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance);

        Assert.Equal(2, result.Model.SectionMap.Count);
        Assert.Equal(2, result.Model.MaterialMap.Count);
        Assert.Contains(result.Model.SectionMap, kv => kv.Key.StartsWith("Aust300::360 UB 44.7"));
        Assert.Contains(result.Model.SectionMap, kv => kv.Key.StartsWith("Aust300::200 UB 25.4"));
        Assert.Contains(result.Model.MaterialMap, kv => kv.Key.StartsWith("Aust::STEEL"));
        Assert.Contains(result.Model.MaterialMap, kv => kv.Key.StartsWith("Aust::ALUMINIUM"));
    }

    // ── Empty input (#3) ─────────────────────────────────────────────

    [Fact]
    public async Task Assemble_EmptyMembers_ReturnsWarningAndEmptyModel()
    {
        var members = Array.Empty<SgMemberData>();

        var result = await _assembler.AssembleAsync(_api, members, Tolerance);

        Assert.Contains(result.Warnings, w => w.Contains("empty", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Model.NodeMap);
        Assert.Empty(result.Model.MemberMap);
        // Should NOT call any API methods
        await _api.DidNotReceive().ClearJobDataAsync(Arg.Any<CancellationToken>());
    }

    // ── Partial failure (#4) ─────────────────────────────────────────

    [Fact]
    public async Task Assemble_PartialMaterialFailure_ThrowsWithClearMessage()
    {
        // API returns fewer materials than sent
        _api.ClearJobDataAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _api.CreateMaterialsFromLibraryAsync(Arg.Any<List<MaterialLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Material>()); // returns 0 instead of 1

        var members = new[] { MakeMember(0, 0, 0, 1, 0, 0) };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, members, Tolerance));
        Assert.Contains("materials", ex.Message);
    }

    [Fact]
    public async Task Assemble_PartialNodeFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        // Override nodes to return fewer than sent
        _api.CreateNodesAsync(Arg.Any<List<NodeCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Node> { new() { Id = 1, X = 0, Y = 0, Z = 0 } }); // 1 instead of 2

        var members = new[] { MakeMember(0, 0, 0, 1, 0, 0) };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, members, Tolerance));
        Assert.Contains("nodes", ex.Message);
    }

    // ── Points outside tolerance stay separate ───────────────────────

    [Fact]
    public async Task Assemble_PointsBeyondTolerance_RemainSeparate()
    {
        SetupApiReturns();

        // Two members with endpoints just outside tolerance (0.002 apart > 0.001)
        var members = new[]
        {
            MakeMember(0, 0, 0, 1, 0, 0),
            MakeMember(1.002, 0, 0, 2, 0, 0)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        // Should create 4 separate nodes (no merge)
        await _api.Received(1).CreateNodesAsync(
            Arg.Is<List<NodeCreate>>(list => list.Count == 4),
            Arg.Any<CancellationToken>());
    }
}