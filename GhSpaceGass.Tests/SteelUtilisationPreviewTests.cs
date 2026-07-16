using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Models.Visuals;
using Xunit;

namespace GhSpaceGass.Tests;

public class SteelUtilisationPreviewTests
{
    private static Dictionary<int, (SgPoint3D Start, SgPoint3D End)> TwoMemberMap() => new()
    {
        [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0)),
        [2] = (new SgPoint3D(10, 0, 0), new SgPoint3D(20, 0, 0))
    };

    private static SgSteelMemberCheckData Check(int designGroupId, double loadFactor)
        => new(designGroupId, section: "S1", flag: "PASS", loadFactor: loadFactor,
            criticalCaseId: 1, failureMode: "", segmentLength: 0, totalLength: 0, yield: 0);

    // ── Empty / degenerate ───────────────────────────────────────────

    [Fact]
    public void Build_EmptyChecks_ReturnsNoMembers()
    {
        var result = SteelUtilisationPreviewBuilder.Build(
            Array.Empty<SgSteelMemberCheckData>(),
            new Dictionary<int, (SgPoint3D, SgPoint3D)>());

        Assert.Empty(result.Members);
    }

    [Fact]
    public void Build_UnmatchedDesignGroupId_Skipped()
    {
        var checks = new[] { Check(99, 1.5) };
        var result = SteelUtilisationPreviewBuilder.Build(checks, TwoMemberMap());

        Assert.Empty(result.Members);
    }

    [Fact]
    public void Build_ZeroLoadFactor_Skipped()
    {
        // LoadFactor <= 0 is malformed (no capacity data) — skip regardless of direction convention
        var checks = new[] { Check(1, 0.0) };
        var result = SteelUtilisationPreviewBuilder.Build(checks, TwoMemberMap());

        Assert.Empty(result.Members);
    }

    [Fact]
    public void Build_NegativeLoadFactor_Skipped()
    {
        var checks = new[] { Check(1, -0.1) };
        var result = SteelUtilisationPreviewBuilder.Build(checks, TwoMemberMap());

        Assert.Empty(result.Members);
    }

    // ── Geometry ─────────────────────────────────────────────────────

    [Fact]
    public void Build_ProducesOneMemberPerCheck_WithMatchedGeometry()
    {
        // Use load factors that survive the "skip below 0" rule
        var checks = new[] { Check(1, 1.5), Check(2, 1.2) };
        var result = SteelUtilisationPreviewBuilder.Build(checks, TwoMemberMap());

        Assert.Equal(2, result.Members.Count);
        Assert.Equal(new SgPoint3D(0, 0, 0), result.Members[0].Start);
        Assert.Equal(new SgPoint3D(10, 0, 0), result.Members[0].End);
        Assert.Equal(new SgPoint3D(10, 0, 0), result.Members[1].Start);
        Assert.Equal(new SgPoint3D(20, 0, 0), result.Members[1].End);
    }

    [Fact]
    public void Build_LoadFactor_CopiedOntoMember()
    {
        var checks = new[] { Check(1, 1.22) };
        var result = SteelUtilisationPreviewBuilder.Build(checks, TwoMemberMap());

        Assert.Equal(1.22, result.Members[0].LoadFactor, 6);
    }

    [Fact]
    public void Build_DesignGroupId_CopiedOntoMember()
    {
        var checks = new[] { Check(2, 1.5) };
        var result = SteelUtilisationPreviewBuilder.Build(checks, TwoMemberMap());

        Assert.Equal(2, result.Members[0].DesignGroupId);
    }

    // ── Colour anchor stops (capacity/action semantics) ──────────────

    [Fact]
    public void Build_LoadFactorAtHalf_MapsToDeepRed()
    {
        // Anchor: 0.5 → Deep Red (183, 28, 28) — severely overloaded
        var checks = new[] { Check(1, 0.5) };
        var result = SteelUtilisationPreviewBuilder.Build(checks, TwoMemberMap());
        var (r, g, b) = result.Members[0].Rgb;

        Assert.Equal(183, r);
        Assert.Equal(28, g);
        Assert.Equal(28, b);
    }

    [Fact]
    public void Build_LoadFactorAtCapacity_MapsToRed()
    {
        // Anchor: 1.0 → Red (244, 67, 54) — exactly at capacity
        var checks = new[] { Check(1, 1.0) };
        var result = SteelUtilisationPreviewBuilder.Build(checks, TwoMemberMap());
        var (r, g, b) = result.Members[0].Rgb;

        Assert.Equal(244, r);
        Assert.Equal(67, g);
        Assert.Equal(54, b);
    }

    [Fact]
    public void Build_LoadFactorAtTenPercentMargin_MapsToOrange()
    {
        // Anchor: 1.11 → Orange (255, 152, 0) — ~10% capacity margin
        var checks = new[] { Check(1, 1.11) };
        var result = SteelUtilisationPreviewBuilder.Build(checks, TwoMemberMap());
        var (r, g, b) = result.Members[0].Rgb;

        Assert.Equal(255, r);
        Assert.Equal(152, g);
        Assert.Equal(0, b);
    }

    [Fact]
    public void Build_LoadFactorAtThirtyThreePercentMargin_MapsToYellow()
    {
        // Anchor: 1.33 → Yellow (255, 235, 59) — ~33% capacity margin
        var checks = new[] { Check(1, 1.33) };
        var result = SteelUtilisationPreviewBuilder.Build(checks, TwoMemberMap());
        var (r, g, b) = result.Members[0].Rgb;

        Assert.Equal(255, r);
        Assert.Equal(235, g);
        Assert.Equal(59, b);
    }

    [Fact]
    public void Build_LoadFactorAtDoubleCapacity_MapsToGreen()
    {
        // Anchor: 2.0 → Green (76, 175, 80) — plenty of margin (double the required capacity)
        var checks = new[] { Check(1, 2.0) };
        var result = SteelUtilisationPreviewBuilder.Build(checks, TwoMemberMap());
        var (r, g, b) = result.Members[0].Rgb;

        Assert.Equal(76, r);
        Assert.Equal(175, g);
        Assert.Equal(80, b);
    }

    // ── Clamps ──────────────────────────────────────────────────────

    [Fact]
    public void Build_LoadFactorFarBelowHalf_ClampsToDeepRed()
    {
        // Below 0.5 anchor → clamp to Deep Red (183, 28, 28)
        var checks = new[] { Check(1, 0.1) };
        var result = SteelUtilisationPreviewBuilder.Build(checks, TwoMemberMap());
        var (r, g, b) = result.Members[0].Rgb;

        Assert.Equal(183, r);
        Assert.Equal(28, g);
        Assert.Equal(28, b);
    }

    [Fact]
    public void Build_LoadFactorFarAboveTwo_ClampsToGreen()
    {
        // Above 2.0 anchor → clamp to Green (76, 175, 80)
        var checks = new[] { Check(1, 5.0) };
        var result = SteelUtilisationPreviewBuilder.Build(checks, TwoMemberMap());
        var (r, g, b) = result.Members[0].Rgb;

        Assert.Equal(76, r);
        Assert.Equal(175, g);
        Assert.Equal(80, b);
    }

    // ── Interpolation between anchors ────────────────────────────────

    [Fact]
    public void Build_LoadFactorInterpolatesBetweenOrangeAndYellow()
    {
        // Midway between Orange (1.11, 255,152,0) and Yellow (1.33, 255,235,59)
        // At 1.22 → R stays 255, G ≈ (152+235)/2 = 193.5, B ≈ (0+59)/2 = 29.5
        var checks = new[] { Check(1, 1.22) };
        var result = SteelUtilisationPreviewBuilder.Build(checks, TwoMemberMap());
        var (r, g, b) = result.Members[0].Rgb;

        Assert.Equal(255, r);
        Assert.InRange(g, 188, 199);
        Assert.InRange(b, 25, 35);
    }

    [Fact]
    public void Build_LoadFactorBetweenDeepRedAndRed_InterpolatesToDarkRed()
    {
        // Midway between Deep Red (0.5, 183,28,28) and Red (1.0, 244,67,54)
        // At 0.75 → R ≈ (183+244)/2 = 213.5, G ≈ (28+67)/2 = 47.5, B ≈ (28+54)/2 = 41
        var checks = new[] { Check(1, 0.75) };
        var result = SteelUtilisationPreviewBuilder.Build(checks, TwoMemberMap());
        var (r, g, b) = result.Members[0].Rgb;

        Assert.InRange(r, 208, 219);
        Assert.InRange(g, 43, 53);
        Assert.InRange(b, 36, 46);
    }
}
