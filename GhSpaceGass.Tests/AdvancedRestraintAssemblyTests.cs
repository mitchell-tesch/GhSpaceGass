using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

/// <summary>
///     Tests for advanced restraints: spring stiffness and friction (Slice 16).
/// </summary>
public class AdvancedRestraintAssemblyTests
{
    private const double Tolerance = 0.001;

    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ModelAssembler _assembler = new();

    private static SgMemberData MakeMember()
    {
        return new SgMemberData(new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0),
            new SgSectionData("Aust300", "360 UB 44.7"),
            new SgMaterialData("Aust", "STEEL"));
    }

    private void SetupApiReturns()
    {
        _api.ClearJobDataAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _api.CreateMaterialsFromLibraryAsync(Arg.Any<List<MaterialLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
                ((List<MaterialLibraryCreate>)args[0]).Select((m, i) => new Material { Id = i + 1 }).ToList());
        _api.CreateSectionsFromLibraryAsync(Arg.Any<List<SectionLibraryCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
                ((List<SectionLibraryCreate>)args[0]).Select((s, i) => new Section { Id = i + 1 }).ToList());
        _api.CreateNodesAsync(Arg.Any<List<NodeCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
                ((List<NodeCreate>)args[0]).Select((n, i) => new Node { Id = i + 1, X = n.X, Y = n.Y, Z = n.Z })
                .ToList());
        _api.CreateMembersAsync(Arg.Any<List<MemberCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
                ((List<MemberCreate>)args[0])
                .Select((m, i) => new Member { Id = i + 1, NodeA = m.NodeA, NodeB = m.NodeB }).ToList());
        _api.CreateNodeRestraintsAsync(Arg.Any<List<NodeRestraintCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
                ((List<NodeRestraintCreate>)args[0]).Select((r, i) => new NodeRestraint
                    { Node = r.Node, RestraintCode = r.RestraintCode }).ToList());
    }

    // ═══════════════════════════════════════════════════════════════════
    // SgRestraintData — extended code validation
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("FFFFFF")]
    [InlineData("FFFRRR")]
    [InlineData("SSSRRR")]
    [InlineData("NNNFFF")]
    [InlineData("PPPRRR")]
    [InlineData("VVVFFF")]
    [InlineData("FSNPRV")]
    public void SgRestraintData_ValidCodes_Accepted(string code)
    {
        var r = new SgRestraintData(new SgPoint3D(0, 0, 0), code);
        Assert.Equal(code, r.RestraintCode);
    }

    [Theory]
    [InlineData("XXXXXX")]
    [InlineData("FFF12R")]
    public void SgRestraintData_InvalidCodes_Throws(string code)
    {
        Assert.Throws<ArgumentException>(() => new SgRestraintData(new SgPoint3D(0, 0, 0), code));
    }

    [Fact]
    public void SgRestraintData_WithStiffness_StoresStiffness()
    {
        var stiffness = new SgRestraintStiffnessData(1000, 2000);
        var r = new SgRestraintData(new SgPoint3D(0, 0, 0), "SSFFFF", stiffness);
        Assert.NotNull(r.Stiffness);
        Assert.Equal(1000, r.Stiffness!.KTx);
        Assert.Equal(2000, r.Stiffness.KTy);
    }

    [Fact]
    public void SgRestraintData_WithFriction_StoresFriction()
    {
        var friction = new SgRestraintFrictionData(
            new SgFrictionAxisData(0.3, FrictionNormalAxis.YAxis, FrictionNormalDirection.Either));
        var r = new SgRestraintData(new SgPoint3D(0, 0, 0), "NFFFFF", friction: friction);
        Assert.NotNull(r.Friction);
        Assert.NotNull(r.Friction!.X);
        Assert.Equal(0.3, r.Friction.X!.Factor);
        Assert.Equal(FrictionNormalAxis.YAxis, r.Friction.X.NormalAxis);
    }

    // ═══════════════════════════════════════════════════════════════════
    // SgRestraintStiffnessData
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void SgRestraintStiffnessData_AllNull_HasAnyStiffnessFalse()
    {
        var s = new SgRestraintStiffnessData();
        Assert.False(s.HasAnyStiffness);
    }

    [Fact]
    public void SgRestraintStiffnessData_OneSet_HasAnyStiffnessTrue()
    {
        var s = new SgRestraintStiffnessData(500);
        Assert.True(s.HasAnyStiffness);
        Assert.Equal(500, s.KTx);
    }

    // ═══════════════════════════════════════════════════════════════════
    // SgRestraintFrictionData
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void SgRestraintFrictionData_AllNull_HasAnyFrictionFalse()
    {
        var f = new SgRestraintFrictionData();
        Assert.False(f.HasAnyFriction);
    }

    [Fact]
    public void SgRestraintFrictionData_OneAxisSet_HasAnyFrictionTrue()
    {
        var f = new SgRestraintFrictionData(
            y: new SgFrictionAxisData(0.5, FrictionNormalAxis.ZAxis, FrictionNormalDirection.PositiveOnly));
        Assert.True(f.HasAnyFriction);
        Assert.Null(f.X);
        Assert.NotNull(f.Y);
        Assert.Equal(0.5, f.Y!.Factor);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ModelAssembler — stiffness mapped to API
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_RestraintWithStiffness_MapsToApiStiffnessProperties()
    {
        SetupApiReturns();

        var stiffness = new SgRestraintStiffnessData(1000, 2000, kRz: 500);
        var restraint = new SgRestraintData(new SgPoint3D(0, 0, 0), "SSFFFS", stiffness);
        var members = new[] { MakeMember() };

        await _assembler.AssembleAsync(_api, members, Tolerance, new[] { restraint });

        await _api.Received(1).CreateNodeRestraintsAsync(
            Arg.Is<List<NodeRestraintCreate>>(list =>
                list[0].RestraintCode == "SSFFFS" &&
                list[0].TxStiffness == 1000 &&
                list[0].TyStiffness == 2000 &&
                list[0].RzStiffness == 500 &&
                list[0].TzStiffness == null &&
                list[0].RxStiffness == null &&
                list[0].RyStiffness == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_RestraintWithNoStiffness_StiffnessPropertiesNull()
    {
        SetupApiReturns();

        var restraint = new SgRestraintData(new SgPoint3D(0, 0, 0), "FFFFFF");
        var members = new[] { MakeMember() };

        await _assembler.AssembleAsync(_api, members, Tolerance, new[] { restraint });

        await _api.Received(1).CreateNodeRestraintsAsync(
            Arg.Is<List<NodeRestraintCreate>>(list =>
                list[0].TxStiffness == null &&
                list[0].TyStiffness == null &&
                list[0].TzStiffness == null),
            Arg.Any<CancellationToken>());
    }

    // ═══════════════════════════════════════════════════════════════════
    // ModelAssembler — friction mapped to API
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_RestraintWithFriction_MapsToApiFrictionProperties()
    {
        SetupApiReturns();

        var friction = new SgRestraintFrictionData(
            new SgFrictionAxisData(0.3, FrictionNormalAxis.YAxis, FrictionNormalDirection.Either),
            z: new SgFrictionAxisData(0.5, FrictionNormalAxis.XAxis, FrictionNormalDirection.NegativeOnly));
        var restraint = new SgRestraintData(new SgPoint3D(0, 0, 0), "NFNFFF", friction: friction);
        var members = new[] { MakeMember() };

        await _assembler.AssembleAsync(_api, members, Tolerance, new[] { restraint });

        await _api.Received(1).CreateNodeRestraintsAsync(
            Arg.Is<List<NodeRestraintCreate>>(list =>
                list[0].RestraintCode == "NFNFFF" &&
                list[0].XFrictionFactor == 0.3 &&
                list[0].XFrictionNormalAxis == FrictionNormalAxis.YAxis &&
                list[0].XFrictionNormalDirection == FrictionNormalDirection.Either &&
                list[0].YFrictionFactor == null &&
                list[0].ZFrictionFactor == 0.5 &&
                list[0].ZFrictionNormalAxis == FrictionNormalAxis.XAxis &&
                list[0].ZFrictionNormalDirection == FrictionNormalDirection.NegativeOnly),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_RestraintWithBothStiffnessAndFriction_BothMapped()
    {
        SetupApiReturns();

        var stiffness = new SgRestraintStiffnessData(kRx: 100);
        var friction = new SgRestraintFrictionData(
            new SgFrictionAxisData(0.4, FrictionNormalAxis.ZAxis, FrictionNormalDirection.PositiveOnly));
        var restraint = new SgRestraintData(new SgPoint3D(0, 0, 0), "NFFSFF",
            stiffness, friction);
        var members = new[] { MakeMember() };

        await _assembler.AssembleAsync(_api, members, Tolerance, new[] { restraint });

        await _api.Received(1).CreateNodeRestraintsAsync(
            Arg.Is<List<NodeRestraintCreate>>(list =>
                list[0].XFrictionFactor == 0.4 &&
                list[0].RxStiffness == 100),
            Arg.Any<CancellationToken>());
    }
}