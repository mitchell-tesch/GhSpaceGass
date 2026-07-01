using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using NSubstitute;
using SpaceGassApi.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class ReleaseAssemblyTests
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
            releaseA: releaseA,
            releaseB: releaseB);
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
    }

    // ═══════════════════════════════════════════════════════════════════
    // SgReleaseData construction tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void SgReleaseData_AllFixed_StoresFFFFFF()
    {
        var r = new SgReleaseData("FFFFFF");
        Assert.Equal("FFFFFF", r.ReleaseCode);
    }

    [Fact]
    public void SgReleaseData_AllReleased_StoresRRRRRR()
    {
        var r = new SgReleaseData("RRRRRR");
        Assert.Equal("RRRRRR", r.ReleaseCode);
    }

    [Fact]
    public void SgReleaseData_PinnedEnd_StoresFFFRRR()
    {
        var r = new SgReleaseData("FFFRRR");
        Assert.Equal("FFFRRR", r.ReleaseCode);
    }

    [Fact]
    public void SgReleaseData_LowercaseCode_NormalisedToUpper()
    {
        var r = new SgReleaseData("fffrrr");
        Assert.Equal("FFFRRR", r.ReleaseCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("FFF")]
    [InlineData("FFFFFFR")] // 7 chars
    public void SgReleaseData_InvalidCodeLength_Throws(string code)
    {
        Assert.Throws<ArgumentException>(() => new SgReleaseData(code));
    }

    [Fact]
    public void SgReleaseData_NullCode_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SgReleaseData(null!));
    }

    [Theory]
    [InlineData("XYZABC")]
    [InlineData("FFF123")]
    [InlineData("FFFFF ")]
    public void SgReleaseData_InvalidCodeCharacters_Throws(string code)
    {
        Assert.Throws<ArgumentException>(() => new SgReleaseData(code));
    }

    // ── Spring code 'S' ───────────────────────────────────────────────

    [Fact]
    public void SgReleaseData_SpringCode_AcceptsS()
    {
        var r = new SgReleaseData("FFFFFS", kRz: 100.0);
        Assert.Equal("FFFFFS", r.ReleaseCode);
        Assert.Equal(100.0, r.KRz);
    }

    [Fact]
    public void SgReleaseData_MixedFRS_AcceptsAll()
    {
        var r = new SgReleaseData("FRSFFR");
        Assert.Equal("FRSFFR", r.ReleaseCode);
    }

    [Fact]
    public void SgReleaseData_LowercaseS_NormalisedToUpper()
    {
        var r = new SgReleaseData("fffSss");
        Assert.Equal("FFFSSS", r.ReleaseCode);
    }

    // ── Stiffness values ──────────────────────────────────────────────

    [Fact]
    public void SgReleaseData_NoStiffness_AllNull()
    {
        var r = new SgReleaseData("FFFFFR");

        Assert.Null(r.KTx);
        Assert.Null(r.KTy);
        Assert.Null(r.KTz);
        Assert.Null(r.KRx);
        Assert.Null(r.KRy);
        Assert.Null(r.KRz);
    }

    [Fact]
    public void SgReleaseData_WithStiffness_StoresValues()
    {
        var r = new SgReleaseData("FFFFFS", kRz: 100.0);

        Assert.Null(r.KTx);
        Assert.Null(r.KTy);
        Assert.Null(r.KTz);
        Assert.Null(r.KRx);
        Assert.Null(r.KRy);
        Assert.Equal(100.0, r.KRz);
    }

    [Fact]
    public void SgReleaseData_AllStiffnessValues_StoredCorrectly()
    {
        var r = new SgReleaseData("SSSSSS",
            1.0, 2.0, 3.0,
            4.0, 5.0, 6.0);

        Assert.Equal(1.0, r.KTx);
        Assert.Equal(2.0, r.KTy);
        Assert.Equal(3.0, r.KTz);
        Assert.Equal(4.0, r.KRx);
        Assert.Equal(5.0, r.KRy);
        Assert.Equal(6.0, r.KRz);
    }

    // ── IsFullyFixed ──────────────────────────────────────────────────

    [Fact]
    public void SgReleaseData_FFFFFF_NoStiffness_IsFullyFixed()
    {
        var r = new SgReleaseData("FFFFFF");
        Assert.True(r.IsFullyFixed);
    }

    [Fact]
    public void SgReleaseData_FFFFFR_IsNotFullyFixed()
    {
        var r = new SgReleaseData("FFFFFR");
        Assert.False(r.IsFullyFixed);
    }

    [Fact]
    public void SgReleaseData_FFFFFS_WithStiffness_IsNotFullyFixed()
    {
        var r = new SgReleaseData("FFFFFS", kRz: 100.0);
        Assert.False(r.IsFullyFixed);
    }

    // ═══════════════════════════════════════════════════════════════════
    // SgMemberData with releases
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void SgMemberData_NoReleases_DefaultsToNull()
    {
        var m = new SgMemberData(
            new SgPoint3D(0, 0, 0),
            new SgPoint3D(10, 0, 0),
            new SgSectionData("Aust300", "360 UB 44.7"),
            new SgMaterialData("Aust", "STEEL"));

        Assert.Null(m.ReleaseA);
        Assert.Null(m.ReleaseB);
    }

    [Fact]
    public void SgMemberData_WithReleaseA_StoresRelease()
    {
        var release = new SgReleaseData("FFFFFR");
        var m = new SgMemberData(
            new SgPoint3D(0, 0, 0),
            new SgPoint3D(10, 0, 0),
            new SgSectionData("Aust300", "360 UB 44.7"),
            new SgMaterialData("Aust", "STEEL"),
            releaseA: release);

        Assert.NotNull(m.ReleaseA);
        Assert.Equal("FFFFFR", m.ReleaseA!.ReleaseCode);
        Assert.Null(m.ReleaseB);
    }

    [Fact]
    public void SgMemberData_WithBothReleases_StoresBoth()
    {
        var relA = new SgReleaseData("FFFFFR");
        var relB = new SgReleaseData("FFFRRR");
        var m = new SgMemberData(
            new SgPoint3D(0, 0, 0),
            new SgPoint3D(10, 0, 0),
            new SgSectionData("Aust300", "360 UB 44.7"),
            new SgMaterialData("Aust", "STEEL"),
            releaseA: relA,
            releaseB: relB);

        Assert.Equal("FFFFFR", m.ReleaseA!.ReleaseCode);
        Assert.Equal("FFFRRR", m.ReleaseB!.ReleaseCode);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ModelAssembler — releases pushed to API
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Assemble_MemberWithNoReleases_ReleasesIsNull()
    {
        SetupApiReturns();

        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0) };
        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list.Count == 1 && list[0].Releases == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberWithFullyFixedRelease_SkipsReleases()
    {
        SetupApiReturns();

        // FFFFFF with no stiffness = no-op, should be skipped
        var releaseA = new SgReleaseData("FFFFFF");
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, releaseA) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list.Count == 1 && list[0].Releases == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberWithBothFullyFixed_SkipsReleases()
    {
        SetupApiReturns();

        var releaseA = new SgReleaseData("FFFFFF");
        var releaseB = new SgReleaseData("FFFFFF");
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, releaseA, releaseB) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list[0].Releases == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberWithReleaseA_SetsFixityCodeAtA()
    {
        SetupApiReturns();

        var releaseA = new SgReleaseData("FFFFFR");
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, releaseA) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list.Count == 1 &&
                list[0].Releases != null &&
                list[0].Releases.FixityCodeAtA == "FFFFFR"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberWithReleaseB_SetsFixityCodeAtB()
    {
        SetupApiReturns();

        var releaseB = new SgReleaseData("FFFRRR");
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, releaseB: releaseB) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list.Count == 1 &&
                list[0].Releases != null &&
                list[0].Releases.FixityCodeAtB == "FFFRRR"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberWithBothReleases_SetsBothFixityCodes()
    {
        SetupApiReturns();

        var releaseA = new SgReleaseData("FFFFFR");
        var releaseB = new SgReleaseData("FFFRRR");
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, releaseA, releaseB) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list.Count == 1 &&
                list[0].Releases != null &&
                list[0].Releases.FixityCodeAtA == "FFFFFR" &&
                list[0].Releases.FixityCodeAtB == "FFFRRR"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_MemberWithReleaseAOnly_FixityCodeAtBIsNull()
    {
        SetupApiReturns();

        var releaseA = new SgReleaseData("FFFFFR");
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, releaseA) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list[0].Releases != null &&
                list[0].Releases.FixityCodeAtA == "FFFFFR" &&
                list[0].Releases.FixityCodeAtB == null),
            Arg.Any<CancellationToken>());
    }

    // ── Stiffness mapped to API ───────────────────────────────────────

    [Fact]
    public async Task Assemble_ReleaseWithStiffness_MapsToApiStiffnessProperties()
    {
        SetupApiReturns();

        var releaseA = new SgReleaseData("SSSSSS",
            1.0, 2.0, 3.0,
            4.0, 5.0, 6.0);
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, releaseA) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list[0].Releases != null &&
                list[0].Releases.TxStiffnessAtA == 1.0 &&
                list[0].Releases.TyStiffnessAtA == 2.0 &&
                list[0].Releases.TzStiffnessAtA == 3.0 &&
                list[0].Releases.RxStiffnessAtA == 4.0 &&
                list[0].Releases.RyStiffnessAtA == 5.0 &&
                list[0].Releases.RzStiffnessAtA == 6.0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_ReleaseBWithSpringCode_MapsToApiStiffnessAtB()
    {
        SetupApiReturns();

        var releaseB = new SgReleaseData("FFFFFS", kRz: 100.0);
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, releaseB: releaseB) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list[0].Releases != null &&
                list[0].Releases.FixityCodeAtB == "FFFFFS" &&
                list[0].Releases.RzStiffnessAtB == 100.0 &&
                list[0].Releases.TxStiffnessAtB == null &&
                list[0].Releases.TyStiffnessAtB == null &&
                list[0].Releases.TzStiffnessAtB == null &&
                list[0].Releases.RxStiffnessAtB == null &&
                list[0].Releases.RyStiffnessAtB == null),
            Arg.Any<CancellationToken>());
    }

    // ── Mixed: one end effective, other fully fixed → still sends ─────

    [Fact]
    public async Task Assemble_OneEndEffectiveOtherFullyFixed_SendsReleases()
    {
        SetupApiReturns();

        var releaseA = new SgReleaseData("FFFFFR"); // effective
        var releaseB = new SgReleaseData("FFFFFF"); // fully fixed (no-op)
        var members = new[] { MakeMember(0, 0, 0, 10, 0, 0, releaseA, releaseB) };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        // Releases object should be created because releaseA is effective
        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list[0].Releases != null &&
                list[0].Releases.FixityCodeAtA == "FFFFFR" &&
                list[0].Releases.FixityCodeAtB == "FFFFFF"),
            Arg.Any<CancellationToken>());
    }

    // ── Multiple members with different releases ──────────────────────

    [Fact]
    public async Task Assemble_MultipleMembersWithDifferentReleases_EachMemberGetsCorrectReleases()
    {
        SetupApiReturns();

        var relA = new SgReleaseData("FFFFFR");
        var relB = new SgReleaseData("FFFRRR");

        var members = new[]
        {
            MakeMember(0, 0, 0, 10, 0, 0, relA, relB),
            MakeMember(10, 0, 0, 20, 0, 0) // no releases
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list.Count == 2 &&
                list[0].Releases != null &&
                list[0].Releases.FixityCodeAtA == "FFFFFR" &&
                list[0].Releases.FixityCodeAtB == "FFFRRR" &&
                list[1].Releases == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assemble_SharedReleaseAcrossMembers_BothMembersGetSameRelease()
    {
        SetupApiReturns();

        // Same Release Goo reused across two members
        var pinRelease = new SgReleaseData("FFFFFR");

        var members = new[]
        {
            MakeMember(0, 0, 0, 10, 0, 0, releaseB: pinRelease),
            MakeMember(10, 0, 0, 20, 0, 0, pinRelease)
        };

        await _assembler.AssembleAsync(_api, members, Tolerance);

        await _api.Received(1).CreateMembersAsync(
            Arg.Is<List<MemberCreate>>(list =>
                list.Count == 2 &&
                list[0].Releases != null &&
                list[0].Releases.FixityCodeAtB == "FFFFFR" &&
                list[1].Releases != null &&
                list[1].Releases.FixityCodeAtA == "FFFFFR"),
            Arg.Any<CancellationToken>());
    }
}