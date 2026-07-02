using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class DisassembleModelTests
{
    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ISpaceGassApiFactory _apiFactory = Substitute.For<ISpaceGassApiFactory>();
    private readonly IProcessManager _processManager = Substitute.For<IProcessManager>();

    private SpaceGassSession CreateConnectedSession()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        _api.GetServiceInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new ServiceInfo { SpaceGassVersion = "14.5" });
        var session = new SpaceGassSession(
            34560, @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe",
            TimeSpan.FromSeconds(5), _processManager, _apiFactory);
        session.ConnectAsync().GetAwaiter().GetResult();
        _api.NewJobAsync(Arg.Any<CancellationToken>())
            .Returns(new JobStatus { State = new JobState { IsOpen = true } });
        session.NewJobAsync().GetAwaiter().GetResult();
        return session;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private void SetupNodes(params (int id, double x, double y, double z)[] nodes)
    {
        _api.ListNodesAsync(Arg.Any<CancellationToken>())
            .Returns(nodes.Select(n => new Node { Id = n.id, X = n.x, Y = n.y, Z = n.z }).ToList());
    }

    private void SetupMembers(params (int id, int nodeA, int nodeB, int section, int material, MemberType type)[] members)
    {
        _api.ListMembersAsync(Arg.Any<CancellationToken>())
            .Returns(members.Select(m => new Member
            {
                Id = m.id, NodeA = m.nodeA, NodeB = m.nodeB,
                Section = m.section, Material = m.material, Type = m.type
            }).ToList());
    }

    private void SetupSections(params (int id, string name, string? library)[] sections)
    {
        _api.ListSectionsAsync(Arg.Any<CancellationToken>())
            .Returns(sections.Select(s => new Section { Id = s.id, Name = s.name, Library = s.library }).ToList());
    }

    private void SetupMaterials(params (int id, string name, string? library)[] materials)
    {
        _api.ListMaterialsAsync(Arg.Any<CancellationToken>())
            .Returns(materials.Select(m => new Material { Id = m.id, Name = m.name, Library = m.library }).ToList());
    }

    private void SetupPlates(params (int id, int nodeA, int nodeB, int nodeC, int? nodeD, int material)[] plates)
    {
        _api.ListPlatesAsync(Arg.Any<CancellationToken>())
            .Returns(plates.Select(p => new Plate
            {
                Id = p.id, NodeA = p.nodeA, NodeB = p.nodeB, NodeC = p.nodeC,
                NodeD = p.nodeD, Material = p.material
            }).ToList());
    }

    private void SetupLoadCases(params (int id, string title, LoadCaseType type)[] loadCases)
    {
        _api.ListLoadCasesAsync(Arg.Any<CancellationToken>())
            .Returns(loadCases.Select(lc => new LoadCase { Id = lc.id, Title = lc.title, Type = lc.type }).ToList());
    }

    private void SetupEmptyModel()
    {
        _api.ListNodesAsync(Arg.Any<CancellationToken>()).Returns(new List<Node>());
        _api.ListMembersAsync(Arg.Any<CancellationToken>()).Returns(new List<Member>());
        _api.ListPlatesAsync(Arg.Any<CancellationToken>()).Returns(new List<Plate>());
        _api.ListSectionsAsync(Arg.Any<CancellationToken>()).Returns(new List<Section>());
        _api.ListMaterialsAsync(Arg.Any<CancellationToken>()).Returns(new List<Material>());
        _api.ListLoadCasesAsync(Arg.Any<CancellationToken>()).Returns(new List<LoadCase>());
    }

    private void SetupSimpleModel()
    {
        SetupNodes((1, 0, 0, 0), (2, 1, 0, 0), (3, 2, 0, 0));
        SetupMembers(
            (1, 1, 2, 1, 1, MemberType.Normal),
            (2, 2, 3, 1, 1, MemberType.Truss));
        SetupSections((1, "360 UB 44.7", "Aust300"));
        SetupMaterials((1, "STEEL", "Aust"));
        SetupPlates();
        SetupLoadCases((1, "Dead Load", LoadCaseType.Primary));
    }

    // ── Connection guard ──────────────────────────────────────────────

    [Fact]
    public async Task DisassembleModel_WhenNotConnected_Throws()
    {
        _apiFactory.Create(Arg.Any<string>()).Returns(_api);
        var session = new SpaceGassSession(
            34560, @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe",
            TimeSpan.FromSeconds(5), _processManager, _apiFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.DisassembleModelAsync());
    }

    // ── Empty model ──────────────────────────────────────────────────

    [Fact]
    public async Task DisassembleModel_EmptyJob_ReturnsWarning()
    {
        var session = CreateConnectedSession();
        SetupEmptyModel();

        var result = await session.DisassembleModelAsync();

        Assert.Contains(result.Warnings, w => w.Contains("No structure found"));
        Assert.Empty(result.Nodes);
        Assert.Empty(result.Members);
        Assert.Empty(result.Plates);
    }

    // ── Node mapping ─────────────────────────────────────────────────

    [Fact]
    public async Task DisassembleModel_PopulatesNodeMap()
    {
        var session = CreateConnectedSession();
        SetupSimpleModel();

        var result = await session.DisassembleModelAsync();

        Assert.Equal(3, result.Model.NodeMap.Count);
        Assert.Equal(1, result.Model.NodeMap[new SgPoint3D(0, 0, 0)]);
        Assert.Equal(2, result.Model.NodeMap[new SgPoint3D(1, 0, 0)]);
        Assert.Equal(3, result.Model.NodeMap[new SgPoint3D(2, 0, 0)]);
    }

    [Fact]
    public async Task DisassembleModel_OutputsNodePointsAndIds()
    {
        var session = CreateConnectedSession();
        SetupSimpleModel();

        var result = await session.DisassembleModelAsync();

        Assert.Equal(3, result.Nodes.Count);
        Assert.Equal(new SgPoint3D(0, 0, 0), result.Nodes[0].Point);
        Assert.Equal(1, result.Nodes[0].Id);
        Assert.Equal(new SgPoint3D(2, 0, 0), result.Nodes[2].Point);
        Assert.Equal(3, result.Nodes[2].Id);
    }

    // ── Member mapping ───────────────────────────────────────────────

    [Fact]
    public async Task DisassembleModel_PopulatesMemberMap()
    {
        var session = CreateConnectedSession();
        SetupSimpleModel();

        var result = await session.DisassembleModelAsync();

        Assert.Equal(2, result.Model.MemberMap.Count);
        var m1 = result.Model.MemberMap[1];
        Assert.Equal(new SgPoint3D(0, 0, 0), m1.Start);
        Assert.Equal(new SgPoint3D(1, 0, 0), m1.End);
    }

    [Fact]
    public async Task DisassembleModel_OutputsMemberData()
    {
        var session = CreateConnectedSession();
        SetupSimpleModel();

        var result = await session.DisassembleModelAsync();

        Assert.Equal(2, result.Members.Count);

        Assert.Equal(1, result.Members[0].Id);
        Assert.Equal(new SgPoint3D(0, 0, 0), result.Members[0].Start);
        Assert.Equal(new SgPoint3D(1, 0, 0), result.Members[0].End);
        Assert.Equal(1, result.Members[0].SectionId);
        Assert.Equal(1, result.Members[0].MaterialId);
        Assert.Equal("Beam", result.Members[0].TypeName);

        Assert.Equal(2, result.Members[1].Id);
        Assert.Equal("Truss", result.Members[1].TypeName);
    }

    // ── Section and Material maps ────────────────────────────────────

    [Fact]
    public async Task DisassembleModel_PopulatesSectionMap()
    {
        var session = CreateConnectedSession();
        SetupSimpleModel();

        var result = await session.DisassembleModelAsync();

        Assert.Single(result.Model.SectionMap);
        Assert.Equal(1, result.Model.SectionMap["Aust300::360 UB 44.7"]);
    }

    [Fact]
    public async Task DisassembleModel_PopulatesMaterialMap()
    {
        var session = CreateConnectedSession();
        SetupSimpleModel();

        var result = await session.DisassembleModelAsync();

        Assert.Single(result.Model.MaterialMap);
        Assert.Equal(1, result.Model.MaterialMap["Aust::STEEL"]);
    }

    [Fact]
    public async Task DisassembleModel_CustomSection_UseNameOnlyAsKey()
    {
        var session = CreateConnectedSession();
        SetupNodes((1, 0, 0, 0), (2, 1, 0, 0));
        SetupMembers((1, 1, 2, 1, 1, MemberType.Normal));
        SetupSections((1, "Custom150x150", null));
        SetupMaterials((1, "Custom Steel", null));
        SetupPlates();
        SetupLoadCases();

        var result = await session.DisassembleModelAsync();

        Assert.Equal(1, result.Model.SectionMap["Custom150x150"]);
        Assert.Equal(1, result.Model.MaterialMap["Custom Steel"]);
    }

    // ── Load case maps ───────────────────────────────────────────────

    [Fact]
    public async Task DisassembleModel_PopulatesLoadCaseMaps()
    {
        var session = CreateConnectedSession();
        SetupNodes((1, 0, 0, 0), (2, 1, 0, 0));
        SetupMembers((1, 1, 2, 1, 1, MemberType.Normal));
        SetupSections((1, "360 UB 44.7", "Aust300"));
        SetupMaterials((1, "STEEL", "Aust"));
        SetupPlates();
        SetupLoadCases(
            (1, "Dead Load", LoadCaseType.Primary),
            (2, "Live Load", LoadCaseType.Primary),
            (3, "ULS", LoadCaseType.Combination));

        var result = await session.DisassembleModelAsync();

        Assert.Equal(2, result.Model.LoadCaseMap.Count);
        Assert.Equal(1, result.Model.LoadCaseMap["Dead Load"]);
        Assert.Equal(2, result.Model.LoadCaseMap["Live Load"]);
        Assert.Single(result.Model.CombinationLoadCaseMap);
        Assert.Equal(3, result.Model.CombinationLoadCaseMap["ULS"]);
    }

    // ── Plate mapping ────────────────────────────────────────────────

    [Fact]
    public async Task DisassembleModel_PopulatesPlateMap_QuadPlate()
    {
        var session = CreateConnectedSession();
        SetupNodes((1, 0, 0, 0), (2, 1, 0, 0), (3, 1, 1, 0), (4, 0, 1, 0));
        SetupMembers();
        SetupSections();
        SetupMaterials((1, "CONCRETE", "Aust"));
        SetupPlates((1, 1, 2, 3, 4, 1));
        SetupLoadCases();

        var result = await session.DisassembleModelAsync();

        Assert.Single(result.Model.PlateMap);
        var corners = result.Model.PlateMap[1];
        Assert.Equal(4, corners.Length);
        Assert.Equal(new SgPoint3D(0, 0, 0), corners[0]);
        Assert.Equal(new SgPoint3D(1, 1, 0), corners[2]);
    }

    [Fact]
    public async Task DisassembleModel_PopulatesPlateMap_TriPlate()
    {
        var session = CreateConnectedSession();
        SetupNodes((1, 0, 0, 0), (2, 1, 0, 0), (3, 0.5, 1, 0));
        SetupMembers();
        SetupSections();
        SetupMaterials((1, "CONCRETE", "Aust"));
        SetupPlates((1, 1, 2, 3, null, 1));
        SetupLoadCases();

        var result = await session.DisassembleModelAsync();

        Assert.Single(result.Model.PlateMap);
        var corners = result.Model.PlateMap[1];
        Assert.Equal(3, corners.Length);
    }

    [Fact]
    public async Task DisassembleModel_OutputsPlateData()
    {
        var session = CreateConnectedSession();
        SetupNodes((1, 0, 0, 0), (2, 1, 0, 0), (3, 1, 1, 0), (4, 0, 1, 0));
        SetupMembers();
        SetupSections();
        SetupMaterials((1, "CONCRETE", "Aust"));
        SetupPlates((1, 1, 2, 3, 4, 1));
        SetupLoadCases();

        var result = await session.DisassembleModelAsync();

        Assert.Single(result.Plates);
        Assert.Equal(1, result.Plates[0].Id);
        Assert.Equal(4, result.Plates[0].CornerPoints.Length);
        Assert.Equal(1, result.Plates[0].MaterialId);
    }

    // ── API call order ───────────────────────────────────────────────

    [Fact]
    public async Task DisassembleModel_QueriesAllEndpoints()
    {
        var session = CreateConnectedSession();
        SetupSimpleModel();

        await session.DisassembleModelAsync();

        await _api.Received(1).ListNodesAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).ListMembersAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).ListPlatesAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).ListSectionsAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).ListMaterialsAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).ListLoadCasesAsync(Arg.Any<CancellationToken>());
    }

    // ── API error wrapping ───────────────────────────────────────────

    [Fact]
    public async Task DisassembleModel_ApiException_WrappedInInvalidOperationException()
    {
        var session = CreateConnectedSession();
        _api.ListNodesAsync(Arg.Any<CancellationToken>())
            .Returns<List<Node>>(x => throw new Exception("API error"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.DisassembleModelAsync());

        Assert.Contains("querying nodes", ex.Message);
    }

    // ── Member type mapping ──────────────────────────────────────────

    [Theory]
    [InlineData(MemberType.Normal, "Beam")]
    [InlineData(MemberType.Truss, "Truss")]
    [InlineData(MemberType.Cable, "Cable")]
    [InlineData(MemberType.CompressionOnly, "Compression Only")]
    [InlineData(MemberType.TensionOnly, "Tension Only")]
    [InlineData(MemberType.Gap, "Gap")]
    [InlineData(MemberType.BrittleFuse, "Brittle Fuse")]
    [InlineData(MemberType.PlasticFuse, "Plastic Fuse")]
    public async Task DisassembleModel_MemberType_MappedToDisplayName(MemberType apiType, string expectedName)
    {
        var session = CreateConnectedSession();
        SetupNodes((1, 0, 0, 0), (2, 1, 0, 0));
        SetupMembers((1, 1, 2, 1, 1, apiType));
        SetupSections((1, "UB", "Aust300"));
        SetupMaterials((1, "STEEL", "Aust"));
        SetupPlates();
        SetupLoadCases();

        var result = await session.DisassembleModelAsync();

        Assert.Equal(expectedName, result.Members[0].TypeName);
    }

    // ── Members-only and plates-only models ──────────────────────────

    [Fact]
    public async Task DisassembleModel_MembersOnly_NoPlates()
    {
        var session = CreateConnectedSession();
        SetupNodes((1, 0, 0, 0), (2, 1, 0, 0));
        SetupMembers((1, 1, 2, 1, 1, MemberType.Normal));
        SetupSections((1, "UB", "Aust300"));
        SetupMaterials((1, "STEEL", "Aust"));
        SetupPlates();
        SetupLoadCases();

        var result = await session.DisassembleModelAsync();

        Assert.Single(result.Members);
        Assert.Empty(result.Plates);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task DisassembleModel_PlatesOnly_NoMembers()
    {
        var session = CreateConnectedSession();
        SetupNodes((1, 0, 0, 0), (2, 1, 0, 0), (3, 1, 1, 0), (4, 0, 1, 0));
        SetupMembers();
        SetupSections();
        SetupMaterials((1, "CONCRETE", "Aust"));
        SetupPlates((1, 1, 2, 3, 4, 1));
        SetupLoadCases();

        var result = await session.DisassembleModelAsync();

        Assert.Empty(result.Members);
        Assert.Single(result.Plates);
        Assert.Empty(result.Warnings);
    }
}
