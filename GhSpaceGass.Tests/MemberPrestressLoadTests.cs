using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class MemberPrestressLoadTests
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

        _api.CreateMemberPrestressLoadsAsync(Arg.Any<List<MemberPrestressLoadCreate>>(),
                Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MemberPrestressLoadCreate>)args[0];
                return input.Select((pl, i) => new MemberPrestressLoad
                {
                    Member = pl.Member, LoadCase = pl.LoadCase, Prestress = pl.Prestress
                }).ToList();
            });

        _api.CreateNodeLoadsAsync(Arg.Any<List<NodeLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<NodeLoadCreate>)args[0];
                return input.Select((nl, i) => new NodeLoad { Node = nl.Node, LoadCase = nl.LoadCase }).ToList();
            });
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgMemberPrestressLoadData construction ────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SgMemberPrestressLoadData_StoresAllProperties()
    {
        var lc = new SgLoadCaseData("Prestress");
        var cat = new SgLoadCategoryData("PT");
        var data = new SgMemberPrestressLoadData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
            prestress: 500, loadCategory: cat);

        Assert.Equal(new SgPoint3D(0, 0, 0), data.MemberStart);
        Assert.Equal(new SgPoint3D(10, 0, 0), data.MemberEnd);
        Assert.Same(lc, data.LoadCase);
        Assert.Same(cat, data.LoadCategory);
        Assert.Equal(500, data.Prestress);
    }

    [Fact]
    public void SgMemberPrestressLoadData_DefaultPrestress_IsZero()
    {
        var lc = new SgLoadCaseData("PS");
        var data = new SgMemberPrestressLoadData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc);

        Assert.Equal(0, data.Prestress);
        Assert.True(data.IsZero);
        Assert.Null(data.LoadCategory);
    }

    [Fact]
    public void SgMemberPrestressLoadData_NonZero_IsZeroFalse()
    {
        var lc = new SgLoadCaseData("PS");
        var data = new SgMemberPrestressLoadData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc, prestress: 100);
        Assert.False(data.IsZero);
    }

    [Fact]
    public void SgMemberPrestressLoadData_NullLoadCase_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SgMemberPrestressLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), null!));
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Assemble: Member Prestress Loads ──────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_MemberPrestressLoad_CreatesLoadCaseAndLoad()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Prestress");
        var prestressLoads = new[]
        {
            new SgMemberPrestressLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc, prestress: 500)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            memberPrestressLoads: prestressLoads);

        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1 && list[0].Title == "Prestress"),
            Arg.Any<CancellationToken>());

        await _api.Received(1).CreateMemberPrestressLoadsAsync(
            Arg.Is<List<MemberPrestressLoadCreate>>(list =>
                list.Count == 1 &&
                list[0].Member == 1 &&
                list[0].Prestress == 500),
            Arg.Any<CancellationToken>());

        Assert.Equal(1, result.Model.MemberPrestressLoadCount);
    }

    [Fact]
    public async Task Assemble_MemberPrestressLoad_WithCategory_PassesCategoryId()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("PS");
        var cat = new SgLoadCategoryData("PT");
        var prestressLoads = new[]
        {
            new SgMemberPrestressLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
                prestress: 500, loadCategory: cat)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            memberPrestressLoads: prestressLoads);

        await _api.Received(1).CreateLoadCategoriesAsync(
            Arg.Is<List<LoadCategoryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        await _api.Received(1).CreateMemberPrestressLoadsAsync(
            Arg.Is<List<MemberPrestressLoadCreate>>(list =>
                list[0].LoadCategory == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberPrestressLoad_UnmatchedMember_WarnsAndSkips()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("PS");
        var prestressLoads = new[]
        {
            new SgMemberPrestressLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(20, 0, 0), lc, prestress: 500)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            memberPrestressLoads: prestressLoads);

        Assert.Contains(result.Warnings, w => w.Contains("doesn't exist") || w.Contains("don't match"));
        await _api.DidNotReceive().CreateMemberPrestressLoadsAsync(
            Arg.Any<List<MemberPrestressLoadCreate>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberPrestressLoad_ZeroPrestress_Warns()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("PS");
        var prestressLoads = new[]
        {
            new SgMemberPrestressLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            memberPrestressLoads: prestressLoads);

        Assert.Contains(result.Warnings, w => w.Contains("zero", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assemble_MemberPrestressLoad_SharedLoadCase_Deduplicates()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("PS");

        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc, fz: -10)
        };
        var prestressLoads = new[]
        {
            new SgMemberPrestressLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc, prestress: 500)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: nodeLoads, memberPrestressLoads: prestressLoads);

        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberPrestressLoadApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreateMemberPrestressLoadsAsync(
                Arg.Any<List<MemberPrestressLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<MemberPrestressLoad>());

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("PS");
        var prestressLoads = new[]
        {
            new SgMemberPrestressLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc, prestress: 500)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, members, Tolerance,
                memberPrestressLoads: prestressLoads));
        Assert.Contains("prestress", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Assemble_NoPrestressLoads_DoesNotCallApi()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.DidNotReceive().CreateMemberPrestressLoadsAsync(
            Arg.Any<List<MemberPrestressLoadCreate>>(), Arg.Any<CancellationToken>());
    }
}

