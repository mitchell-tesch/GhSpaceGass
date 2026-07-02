using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class AppendModeAssemblyTests
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

    private void SetupApiReturns()
    {
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

    // ── Append mode: skips clear ──────────────────────────────────────

    [Fact]
    public async Task AppendMode_DoesNotClearJobData()
    {
        SetupApiReturns();
        var members = new[] { MakeMember(0, 0, 0, 1, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance, appendMode: true);

        await _api.DidNotReceive().ClearJobDataAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RebuildMode_StillClearsJobData()
    {
        SetupApiReturns();
        var members = new[] { MakeMember(0, 0, 0, 1, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance, appendMode: false);

        await _api.Received(1).ClearJobDataAsync(Arg.Any<CancellationToken>());
    }

    // ── Append mode: still pushes all data ────────────────────────────

    [Fact]
    public async Task AppendMode_StillPushesMaterialsSectionsNodesMembers()
    {
        SetupApiReturns();
        var members = new[] { MakeMember(0, 0, 0, 1, 0, 0) };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance, appendMode: true);

        await _api.Received(1).CreateMaterialsFromLibraryAsync(
            Arg.Any<List<MaterialLibraryCreate>>(), Arg.Any<CancellationToken>());
        await _api.Received(1).CreateSectionsFromLibraryAsync(
            Arg.Any<List<SectionLibraryCreate>>(), Arg.Any<CancellationToken>());
        await _api.Received(1).CreateNodesAsync(
            Arg.Any<List<NodeCreate>>(), Arg.Any<CancellationToken>());
        await _api.Received(1).CreateMembersAsync(
            Arg.Any<List<MemberCreate>>(), Arg.Any<CancellationToken>());

        Assert.Equal(1, result.Model.MemberMap.Count);
        Assert.Equal(2, result.Model.NodeMap.Count);
    }

    // ── Append mode: dependency order skips clear ─────────────────────

    [Fact]
    public async Task AppendMode_DependencyOrder_NoClearStep()
    {
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
        await _assembler.AssembleAsync(_api, members, Tolerance, appendMode: true);

        // "clear" must NOT appear; everything else in order
        Assert.Equal(new[] { "materials", "sections", "nodes", "members" }, callOrder);
    }

    // ── Append mode: restraints and loads still work ──────────────────

    [Fact]
    public async Task AppendMode_PushesRestraints()
    {
        SetupApiReturns();
        _api.CreateNodeRestraintsAsync(Arg.Any<List<NodeRestraintCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<NodeRestraintCreate>)args[0];
                return input.Select((r, i) => new NodeRestraint { Node = r.Node }).ToList();
            });

        var members = new[] { MakeMember(0, 0, 0, 1, 0, 0) };
        var restraints = new[]
        {
            new SgRestraintData(new SgPoint3D(0, 0, 0), "FFFRRR")
        };

        await _assembler.AssembleAsync(_api, members, Tolerance, restraints: restraints, appendMode: true);

        await _api.DidNotReceive().ClearJobDataAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).CreateNodeRestraintsAsync(
            Arg.Any<List<NodeRestraintCreate>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppendMode_PushesNodeLoads()
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

        var members = new[] { MakeMember(0, 0, 0, 1, 0, 0) };
        var loadCase = new SgLoadCaseData("Dead Load");
        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), loadCase, fy: -10.0)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance, nodeLoads: nodeLoads, appendMode: true);

        await _api.DidNotReceive().ClearJobDataAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).CreateNodeLoadsAsync(
            Arg.Any<List<NodeLoadCreate>>(), Arg.Any<CancellationToken>());
    }

    // ── Append mode: empty model still returns early ──────────────────

    [Fact]
    public async Task AppendMode_EmptyModel_ReturnsWarning()
    {
        var members = Array.Empty<SgMemberData>();

        var result = await _assembler.AssembleAsync(_api, members, Tolerance, appendMode: true);

        Assert.Contains(result.Warnings, w => w.Contains("empty"));
        await _api.DidNotReceive().ClearJobDataAsync(Arg.Any<CancellationToken>());
    }

    // ── Default appendMode is false (rebuild) ─────────────────────────

    [Fact]
    public async Task DefaultMode_IsRebuild_ClearsJobData()
    {
        SetupApiReturns();
        var members = new[] { MakeMember(0, 0, 0, 1, 0, 0) };

        // Call without specifying appendMode — should default to rebuild (clear)
        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).ClearJobDataAsync(Arg.Any<CancellationToken>());
    }

    // ── Session layer passes through appendMode ──────────────────────

    [Fact]
    public async Task Session_AssembleModelAsync_AcceptsAppendMode()
    {
        // This test verifies the SpaceGassSession.AssembleModelAsync signature
        // accepts appendMode parameter and passes it through.
        // It will fail at compile time until the parameter is added.
        var apiFactory = Substitute.For<ISpaceGassApiFactory>();
        var api = Substitute.For<ISpaceGassApi>();
        var processManager = Substitute.For<IProcessManager>();

        apiFactory.Create(Arg.Any<string>()).Returns(api);
        api.GetServiceInfoAsync(Arg.Any<CancellationToken>())
            .Returns(new ServiceInfo { SpaceGassVersion = "14.5" });

        SetupApiReturnsOn(api);

        var session = new SpaceGassSession(
            34560, @"C:\Program Files\SPACE GASS 14.5\SpaceGassApi.exe",
            TimeSpan.FromSeconds(5), processManager, apiFactory);
        await session.ConnectAsync();

        api.NewJobAsync(Arg.Any<CancellationToken>())
            .Returns(new JobStatus { State = new JobState { IsOpen = true } });
        await session.NewJobAsync();

        var members = new List<SgMemberData> { MakeMember(0, 0, 0, 1, 0, 0) };

        await session.AssembleModelAsync(members, Tolerance, appendMode: true);

        // In append mode, ClearJobDataAsync should NOT be called
        await api.DidNotReceive().ClearJobDataAsync(Arg.Any<CancellationToken>());
    }

    private static void SetupApiReturnsOn(ISpaceGassApi api)
    {
        api.CreateMaterialsFromLibraryAsync(Arg.Any<List<MaterialLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MaterialLibraryCreate>)args[0];
                return input.Select((m, i) => new Material { Id = i + 1 }).ToList();
            });
        api.CreateSectionsFromLibraryAsync(Arg.Any<List<SectionLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<SectionLibraryCreate>)args[0];
                return input.Select((s, i) => new Section { Id = i + 1 }).ToList();
            });
        api.CreateNodesAsync(Arg.Any<List<NodeCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<NodeCreate>)args[0];
                return input.Select((n, i) => new Node { Id = i + 1, X = n.X, Y = n.Y, Z = n.Z }).ToList();
            });
        api.CreateMembersAsync(Arg.Any<List<MemberCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MemberCreate>)args[0];
                return input.Select((m, i) => new Member { Id = i + 1, NodeA = m.NodeA, NodeB = m.NodeB }).ToList();
            });
    }
}
