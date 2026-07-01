using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class MemberOffsetTests
{
    private const double Tolerance = 0.001;

    private readonly ISpaceGassApi _api = Substitute.For<ISpaceGassApi>();
    private readonly ModelAssembler _assembler = new();

    private static SgMemberData MakeMember(
        double x1, double y1, double z1,
        double x2, double y2, double z2,
        SgMemberOffsetData? offset = null)
    {
        return new SgMemberData(
            new SgPoint3D(x1, y1, z1),
            new SgPoint3D(x2, y2, z2),
            new SgSectionData("Aust300", "360 UB 44.7"),
            new SgMaterialData("Aust", "STEEL"),
            offset: offset);
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

        _api.CreateMemberOffsetsAsync(Arg.Any<List<MemberOffsetCreate>>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var input = (List<MemberOffsetCreate>)args[0];
                return input.Select((o, i) => new MemberOffset { Member = o.Member }).ToList();
            });
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgMemberOffsetData construction ───────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SgMemberOffsetData_StoresAllProperties()
    {
        var data = new SgMemberOffsetData(
            xOffsetAtA: 10, yOffsetAtA: 20, zOffsetAtA: 30,
            xOffsetAtB: 40, yOffsetAtB: 50, zOffsetAtB: 60,
            axes: AxesType.Local);

        Assert.Equal(10, data.XOffsetAtA);
        Assert.Equal(20, data.YOffsetAtA);
        Assert.Equal(30, data.ZOffsetAtA);
        Assert.Equal(40, data.XOffsetAtB);
        Assert.Equal(50, data.YOffsetAtB);
        Assert.Equal(60, data.ZOffsetAtB);
        Assert.Equal(AxesType.Local, data.Axes);
    }

    [Fact]
    public void SgMemberOffsetData_DefaultValues_AllZero()
    {
        var data = new SgMemberOffsetData();

        Assert.Equal(0, data.XOffsetAtA);
        Assert.Equal(0, data.YOffsetAtA);
        Assert.Equal(0, data.ZOffsetAtA);
        Assert.Equal(0, data.XOffsetAtB);
        Assert.Equal(0, data.YOffsetAtB);
        Assert.Equal(0, data.ZOffsetAtB);
        Assert.Equal(AxesType.Local, data.Axes);
        Assert.True(data.IsZero);
    }

    [Fact]
    public void SgMemberOffsetData_NonZero_IsZeroFalse()
    {
        var data = new SgMemberOffsetData(yOffsetAtA: -150);
        Assert.False(data.IsZero);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── SgMemberData carries offset ──────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SgMemberData_WithOffset_StoresOffset()
    {
        var offset = new SgMemberOffsetData(yOffsetAtA: -150, yOffsetAtB: -150);
        var member = MakeMember(0, 0, 0, 10, 0, 0, offset);
        Assert.Same(offset, member.Offset);
    }

    [Fact]
    public void SgMemberData_WithoutOffset_OffsetIsNull()
    {
        var member = MakeMember(0, 0, 0, 10, 0, 0);
        Assert.Null(member.Offset);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Assemble: Member Offsets ──────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_MemberWithOffset_CallsCreateMemberOffsetsAsync()
    {
        SetupApiReturns();

        var offset = new SgMemberOffsetData(yOffsetAtA: -150, yOffsetAtB: -150, axes: AxesType.Local);
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, offset) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMemberOffsetsAsync(
            Arg.Is<List<MemberOffsetCreate>>(list =>
                list.Count == 1 &&
                list[0].Member == 1 &&
                list[0].YOffsetAtA == -150 &&
                list[0].YOffsetAtB == -150 &&
                list[0].Axes == AxesType.Local),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberWithOffset_SendsAllSixValues()
    {
        SetupApiReturns();

        var offset = new SgMemberOffsetData(
            xOffsetAtA: 1, yOffsetAtA: 2, zOffsetAtA: 3,
            xOffsetAtB: 4, yOffsetAtB: 5, zOffsetAtB: 6);
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, offset) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMemberOffsetsAsync(
            Arg.Is<List<MemberOffsetCreate>>(list =>
                list[0].XOffsetAtA == 1 && list[0].YOffsetAtA == 2 && list[0].ZOffsetAtA == 3 &&
                list[0].XOffsetAtB == 4 && list[0].YOffsetAtB == 5 && list[0].ZOffsetAtB == 6),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberWithGlobalAxesOffset_PassesGlobalAxes()
    {
        SetupApiReturns();

        var offset = new SgMemberOffsetData(yOffsetAtA: -150, axes: AxesType.Global);
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, offset) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMemberOffsetsAsync(
            Arg.Is<List<MemberOffsetCreate>>(list =>
                list[0].Axes == AxesType.Global),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberWithZeroOffset_SkipsOffsetCreation()
    {
        SetupApiReturns();

        var offset = new SgMemberOffsetData(); // all zero
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, offset) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.DidNotReceive().CreateMemberOffsetsAsync(
            Arg.Any<List<MemberOffsetCreate>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberWithoutOffset_DoesNotCallOffsetsApi()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.DidNotReceive().CreateMemberOffsetsAsync(
            Arg.Any<List<MemberOffsetCreate>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MixedMembersWithAndWithoutOffset_OnlyCreatesForOffsetMembers()
    {
        SetupApiReturns();

        var offset = new SgMemberOffsetData(yOffsetAtA: -150);
        var members = new[]
        {
            MakeMember(0, 0, 0, 10, 0, 0, offset), // has offset
            MakeMember(10, 0, 0, 20, 0, 0) // no offset
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMemberOffsetsAsync(
            Arg.Is<List<MemberOffsetCreate>>(list => list.Count == 1 && list[0].Member == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberOffsetApiFailure_ThrowsWithClearMessage()
    {
        SetupApiReturns();
        _api.CreateMemberOffsetsAsync(
                Arg.Any<List<MemberOffsetCreate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<MemberOffset>());

        var offset = new SgMemberOffsetData(yOffsetAtA: -150);
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, offset) };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _assembler.AssembleAsync(_api, members, Tolerance));
        Assert.Contains("offset", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

