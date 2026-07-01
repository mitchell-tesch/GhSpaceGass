using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class MemberConcentratedAndDistributedMomentTests
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

        _api.CreateMemberConcentratedLoadsAsync(Arg.Any<List<MemberConcentratedLoadCreate>>(),
                Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MemberConcentratedLoadCreate>)args[0];
                return input.Select((cl, i) => new MemberConcentratedLoad
                {
                    Member = cl.Member, LoadCase = cl.LoadCase
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

        _api.CreateMemberDistributedMomentsAsync(Arg.Any<List<MemberDistributedMomentCreate>>(),
                Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MemberDistributedMomentCreate>)args[0];
                return input.Select((dm, i) => new MemberDistributedMoment
                {
                    Member = dm.Member, LoadCase = dm.LoadCase
                }).ToList();
            });

        _api.CreateNodeRestraintsAsync(Arg.Any<List<NodeRestraintCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<NodeRestraintCreate>)args[0];
                return input.Select((r, i) => new NodeRestraint { Node = r.Node }).ToList();
            });

        _api.CreateSelfWeightLoadsAsync(Arg.Any<List<SelfWeightLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<SelfWeightLoadCreate>)args[0];
                return input.Select((sw, i) => new SelfWeightLoad { LoadCase = sw.LoadCase }).ToList();
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
                return input.Select((lm, i) => new LumpedMassLoad { Node = lm.Node, LoadCase = lm.LoadCase }).ToList();
            });

        _api.CreatePrescribedDisplacementsAsync(Arg.Any<List<PrescribedDisplacementCreate>>(),
                Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<PrescribedDisplacementCreate>)args[0];
                return input.Select((pd, i) => new PrescribedDisplacement { Node = pd.Node, LoadCase = pd.LoadCase })
                    .ToList();
            });
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgMemberConcentratedLoadData construction ─────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SgMemberConcentratedLoadData_StoresAllProperties()
    {
        var lc = new SgLoadCaseData("Dead Load");
        var data = new SgMemberConcentratedLoadData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
            fx: 5, fy: -10, fz: 3, mx: 1, my: 2, mz: 3,
            position: 50, positionUnits: LoadPositionUnits.Percent,
            axes: LoadAxes.Local);

        Assert.Equal(new SgPoint3D(0, 0, 0), data.MemberStart);
        Assert.Equal(new SgPoint3D(10, 0, 0), data.MemberEnd);
        Assert.Same(lc, data.LoadCase);
        Assert.Equal(5, data.Fx);
        Assert.Equal(-10, data.Fy);
        Assert.Equal(3, data.Fz);
        Assert.Equal(1, data.Mx);
        Assert.Equal(2, data.My);
        Assert.Equal(3, data.Mz);
        Assert.Equal(50, data.Position);
        Assert.Equal(LoadPositionUnits.Percent, data.PositionUnits);
        Assert.Equal(LoadAxes.Local, data.Axes);
        Assert.Null(data.LoadCategory);
    }

    [Fact]
    public void SgMemberConcentratedLoadData_DefaultValues()
    {
        var lc = new SgLoadCaseData("DL");
        var data = new SgMemberConcentratedLoadData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc);

        Assert.Equal(0, data.Fx);
        Assert.Equal(0, data.Fy);
        Assert.Equal(0, data.Fz);
        Assert.Equal(0, data.Mx);
        Assert.Equal(0, data.My);
        Assert.Equal(0, data.Mz);
        Assert.Equal(50, data.Position);
        Assert.Equal(LoadPositionUnits.Percent, data.PositionUnits);
        Assert.Equal(LoadAxes.Local, data.Axes);
        Assert.True(data.IsZero);
    }

    [Fact]
    public void SgMemberConcentratedLoadData_NonZeroForce_IsZeroFalse()
    {
        var lc = new SgLoadCaseData("DL");
        var data = new SgMemberConcentratedLoadData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc, fy: -10);
        Assert.False(data.IsZero);
    }

    [Fact]
    public void SgMemberConcentratedLoadData_NonZeroMoment_IsZeroFalse()
    {
        var lc = new SgLoadCaseData("DL");
        var data = new SgMemberConcentratedLoadData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc, mx: 5);
        Assert.False(data.IsZero);
    }

    [Fact]
    public void SgMemberConcentratedLoadData_NullLoadCase_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SgMemberConcentratedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), null!));
    }

    [Fact]
    public void SgMemberConcentratedLoadData_WithCategory_StoresCategory()
    {
        var lc = new SgLoadCaseData("DL");
        var cat = new SgLoadCategoryData("Dead");
        var data = new SgMemberConcentratedLoadData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc, fy: -10, loadCategory: cat);
        Assert.Same(cat, data.LoadCategory);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgMemberDistributedLoadData moment extensions ─────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SgMemberDistributedLoadData_WithMoments_StoresMomentProperties()
    {
        var lc = new SgLoadCaseData("DL");
        var dl = new SgMemberDistributedLoadData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
            mxStart: 1, myStart: 2, mzStart: 3,
            mxEnd: 4, myEnd: 5, mzEnd: 6);

        Assert.Equal(1, dl.MxStart);
        Assert.Equal(2, dl.MyStart);
        Assert.Equal(3, dl.MzStart);
        Assert.Equal(4, dl.MxEnd);
        Assert.Equal(5, dl.MyEnd);
        Assert.Equal(6, dl.MzEnd);
    }

    [Fact]
    public void SgMemberDistributedLoadData_DefaultMoments_AreZero()
    {
        var lc = new SgLoadCaseData("DL");
        var dl = new SgMemberDistributedLoadData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc);

        Assert.Equal(0, dl.MxStart);
        Assert.Equal(0, dl.MyStart);
        Assert.Equal(0, dl.MzStart);
        Assert.Equal(0, dl.MxEnd);
        Assert.Equal(0, dl.MyEnd);
        Assert.Equal(0, dl.MzEnd);
    }

    [Fact]
    public void SgMemberDistributedLoadData_HasForces_TrueWhenForceNonZero()
    {
        var lc = new SgLoadCaseData("DL");
        var dl = new SgMemberDistributedLoadData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc, fyStart: -5, fyEnd: -5);
        Assert.True(dl.HasForces);
        Assert.False(dl.HasMoments);
    }

    [Fact]
    public void SgMemberDistributedLoadData_HasMoments_TrueWhenMomentNonZero()
    {
        var lc = new SgLoadCaseData("DL");
        var dl = new SgMemberDistributedLoadData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc, mxStart: 5);
        Assert.False(dl.HasForces);
        Assert.True(dl.HasMoments);
    }

    [Fact]
    public void SgMemberDistributedLoadData_IsZero_ChecksBothForcesAndMoments()
    {
        var lc = new SgLoadCaseData("DL");
        // All zero
        var dl1 = new SgMemberDistributedLoadData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc);
        Assert.True(dl1.IsZero);

        // Only moments non-zero
        var dl2 = new SgMemberDistributedLoadData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc, mxStart: 5);
        Assert.False(dl2.IsZero);

        // Only forces non-zero
        var dl3 = new SgMemberDistributedLoadData(
            new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc, fyStart: -5);
        Assert.False(dl3.IsZero);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Assemble: Member Concentrated Loads ──────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_MemberConcentratedLoad_CreatesLoadCaseAndLoad()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("Dead Load");
        var concLoads = new[]
        {
            new SgMemberConcentratedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
                fy: -10, position: 50, positionUnits: LoadPositionUnits.Percent,
                axes: LoadAxes.Local)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            memberConcentratedLoads: concLoads);

        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1 && list[0].Title == "Dead Load"),
            Arg.Any<CancellationToken>());

        await _api.Received(1).CreateMemberConcentratedLoadsAsync(
            Arg.Is<List<MemberConcentratedLoadCreate>>(list =>
                list.Count == 1 &&
                list[0].Member == 1 &&
                list[0].Fy == -10 &&
                list[0].Position == 50 &&
                list[0].PositionUnits == LoadPositionUnits.Percent &&
                list[0].Axes == LoadAxes.Local),
            Arg.Any<CancellationToken>());

        Assert.Equal(1, result.Model.MemberConcentratedLoadCount);
    }

    [Fact]
    public async Task Assemble_MemberConcentratedLoad_SendsAllForceAndMomentValues()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("DL");
        var concLoads = new[]
        {
            new SgMemberConcentratedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
                fx: 1, fy: 2, fz: 3, mx: 4, my: 5, mz: 6)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            memberConcentratedLoads: concLoads);

        await _api.Received(1).CreateMemberConcentratedLoadsAsync(
            Arg.Is<List<MemberConcentratedLoadCreate>>(list =>
                list[0].Fx == 1 && list[0].Fy == 2 && list[0].Fz == 3 &&
                list[0].Mx == 4 && list[0].My == 5 && list[0].Mz == 6),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberConcentratedLoad_WithCategory_PassesCategoryId()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("DL");
        var cat = new SgLoadCategoryData("Dead");
        var concLoads = new[]
        {
            new SgMemberConcentratedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
                fy: -10, loadCategory: cat)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            memberConcentratedLoads: concLoads);

        await _api.Received(1).CreateLoadCategoriesAsync(
            Arg.Is<List<LoadCategoryCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());

        await _api.Received(1).CreateMemberConcentratedLoadsAsync(
            Arg.Is<List<MemberConcentratedLoadCreate>>(list =>
                list[0].LoadCategory == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberConcentratedLoad_UnmatchedMember_WarnsAndSkips()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("DL");
        var concLoads = new[]
        {
            new SgMemberConcentratedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(20, 0, 0), lc,
                fy: -10)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            memberConcentratedLoads: concLoads);

        Assert.Contains(result.Warnings, w => w.Contains("doesn't exist") || w.Contains("don't match"));
        await _api.DidNotReceive().CreateMemberConcentratedLoadsAsync(
            Arg.Any<List<MemberConcentratedLoadCreate>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberConcentratedLoad_ZeroValues_Warns()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("DL");
        var concLoads = new[]
        {
            new SgMemberConcentratedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            memberConcentratedLoads: concLoads);

        Assert.Contains(result.Warnings, w => w.Contains("zero", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Assemble_MemberConcentratedLoad_SharedLoadCase_Deduplicates()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("DL");

        var nodeLoads = new[]
        {
            new SgNodeLoadData(new SgPoint3D(0, 0, 0), lc, fz: -10)
        };
        var concLoads = new[]
        {
            new SgMemberConcentratedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
                fy: -5)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            nodeLoads: nodeLoads, memberConcentratedLoads: concLoads);

        await _api.Received(1).CreateLoadCasesAsync(
            Arg.Is<List<LoadCaseCreate>>(list => list.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberConcentratedLoadApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreateMemberConcentratedLoadsAsync(
                Arg.Any<List<MemberConcentratedLoadCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<MemberConcentratedLoad>());

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("DL");
        var concLoads = new[]
        {
            new SgMemberConcentratedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc, fy: -10)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, members, Tolerance,
                memberConcentratedLoads: concLoads));
        Assert.Contains("concentrated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Assemble: Distributed Moments ────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_DistributedLoadWithMomentsOnly_CreatesMomentApiCall()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("DL");
        var distLoads = new[]
        {
            new SgMemberDistributedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
                mxStart: 5, mxEnd: 5)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            memberDistributedLoads: distLoads);

        // Should NOT call distributed force API (no forces)
        await _api.DidNotReceive().CreateMemberDistributedLoadsAsync(
            Arg.Any<List<MemberDistributedLoadCreate>>(),
            Arg.Any<CancellationToken>());

        // SHOULD call distributed moment API
        await _api.Received(1).CreateMemberDistributedMomentsAsync(
            Arg.Is<List<MemberDistributedMomentCreate>>(list =>
                list.Count == 1 &&
                list[0].Member == 1 &&
                list[0].MxStart == 5 &&
                list[0].MxFinish == 5),
            Arg.Any<CancellationToken>());

        Assert.Equal(1, result.Model.MemberDistributedMomentCount);
        Assert.Equal(0, result.Model.MemberDistributedLoadCount);
    }

    [Fact]
    public async Task Assemble_DistributedLoadWithBothForcesAndMoments_CreatesBothApiCalls()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("DL");
        var distLoads = new[]
        {
            new SgMemberDistributedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
                fyStart: -5, fyEnd: -5,
                mxStart: 3, mxEnd: 3)
        };

        var result = await _assembler.AssembleAsync(_api, members, Tolerance,
            memberDistributedLoads: distLoads);

        // SHOULD call distributed force API
        await _api.Received(1).CreateMemberDistributedLoadsAsync(
            Arg.Is<List<MemberDistributedLoadCreate>>(list =>
                list.Count == 1 && list[0].FyStart == -5),
            Arg.Any<CancellationToken>());

        // SHOULD call distributed moment API
        await _api.Received(1).CreateMemberDistributedMomentsAsync(
            Arg.Is<List<MemberDistributedMomentCreate>>(list =>
                list.Count == 1 && list[0].MxStart == 3),
            Arg.Any<CancellationToken>());

        Assert.Equal(1, result.Model.MemberDistributedLoadCount);
        Assert.Equal(1, result.Model.MemberDistributedMomentCount);
    }

    [Fact]
    public async Task Assemble_DistributedLoadWithForcesOnly_NoMomentApiCall()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("DL");
        var distLoads = new[]
        {
            new SgMemberDistributedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
                fyStart: -5, fyEnd: -5)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            memberDistributedLoads: distLoads);

        await _api.Received(1).CreateMemberDistributedLoadsAsync(
            Arg.Any<List<MemberDistributedLoadCreate>>(),
            Arg.Any<CancellationToken>());

        await _api.DidNotReceive().CreateMemberDistributedMomentsAsync(
            Arg.Any<List<MemberDistributedMomentCreate>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_DistributedMoment_PassesPositionAndAxes()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("DL");
        var distLoads = new[]
        {
            new SgMemberDistributedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
                mxStart: 5, mxEnd: 10,
                startPosition: 25, endPosition: 75,
                positionUnits: LoadPositionUnits.Percent,
                axes: LoadAxes.Local)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            memberDistributedLoads: distLoads);

        await _api.Received(1).CreateMemberDistributedMomentsAsync(
            Arg.Is<List<MemberDistributedMomentCreate>>(list =>
                list[0].StartPosition == 25 &&
                list[0].FinishPosition == 75 &&
                list[0].PositionUnits == LoadPositionUnits.Percent &&
                list[0].Axes == LoadAxes.Local),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_DistributedMoment_WithCategory_PassesCategoryId()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("DL");
        var cat = new SgLoadCategoryData("Dead");
        var distLoads = new[]
        {
            new SgMemberDistributedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
                mxStart: 5, mxEnd: 5,
                loadCategory: cat)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance,
            memberDistributedLoads: distLoads);

        await _api.Received(1).CreateMemberDistributedMomentsAsync(
            Arg.Is<List<MemberDistributedMomentCreate>>(list =>
                list[0].LoadCategory == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_DistributedMomentApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreateMemberDistributedMomentsAsync(
                Arg.Any<List<MemberDistributedMomentCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<MemberDistributedMoment>());

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        var lc = new SgLoadCaseData("DL");
        var distLoads = new[]
        {
            new SgMemberDistributedLoadData(
                new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0), lc,
                mxStart: 5, mxEnd: 5)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, members, Tolerance,
                memberDistributedLoads: distLoads));
        Assert.Contains("distributed moment", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── No new loads = no new API calls ──────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_NoConcentratedLoads_DoesNotCallApi()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.DidNotReceive().CreateMemberConcentratedLoadsAsync(
            Arg.Any<List<MemberConcentratedLoadCreate>>(), Arg.Any<CancellationToken>());
        await _api.DidNotReceive().CreateMemberDistributedMomentsAsync(
            Arg.Any<List<MemberDistributedMomentCreate>>(), Arg.Any<CancellationToken>());
    }
}

