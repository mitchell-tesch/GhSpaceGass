using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Models.Visuals;
using Xunit;

namespace GhSpaceGass.Tests;

public class DisplacedShapePreviewTests
{
    private static Dictionary<int, (SgPoint3D Start, SgPoint3D End)> TwoMemberMap() => new()
    {
        [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0)),
        [2] = (new SgPoint3D(10, 0, 0), new SgPoint3D(20, 0, 0))
    };

    // ── Empty / disabled ──────────────────────────────────────────────

    [Fact]
    public void Build_EmptyDisplacements_ReturnsNoPolylines()
    {
        var result = DisplacedShapeBuilder.Build(
            Array.Empty<SgMemberDisplacementData>(),
            new Dictionary<int, (SgPoint3D, SgPoint3D)>(),
            100.0, userScale: null);

        Assert.Empty(result.Polylines);
    }

    [Fact]
    public void Build_ZeroScale_DisablesPreview()
    {
        var displacements = new[]
        {
            new SgMemberDisplacementData(1, 1, 0, 0.0, 0, 1, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 1, 10.0, 0, 2, 0, 0, 0, 0)
        };
        var result = DisplacedShapeBuilder.Build(displacements, TwoMemberMap(), 100.0, userScale: 0.0);

        Assert.Empty(result.Polylines);
    }

    [Fact]
    public void Build_NegativeScale_DisablesPreview()
    {
        var displacements = new[]
        {
            new SgMemberDisplacementData(1, 1, 0, 0.0, 0, 1, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 1, 10.0, 0, 2, 0, 0, 0, 0)
        };
        var result = DisplacedShapeBuilder.Build(displacements, TwoMemberMap(), 100.0, userScale: -1.0);

        Assert.Empty(result.Polylines);
    }

    // ── Polyline generation ───────────────────────────────────────────

    [Fact]
    public void Build_SingleMember_ProducesOnePolyline()
    {
        // Member 1: (0,0,0)→(10,0,0), 3 stations at locations 0, 5, 10
        var displacements = new[]
        {
            new SgMemberDisplacementData(1, 1, 0, 0.0, 0, 1, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 1, 5.0, 0, 2, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 2, 10.0, 0, 1, 0, 0, 0, 0)
        };
        var result = DisplacedShapeBuilder.Build(displacements, TwoMemberMap(), 100.0, userScale: 1.0);

        Assert.Single(result.Polylines);
        Assert.Equal(3, result.Polylines[0].Points.Count);
    }

    [Fact]
    public void Build_StationPosition_InterpolatedAlongMember()
    {
        // Member 1: (0,0,0)→(10,0,0), station at location=5 → midpoint (5,0,0)
        // Small TzGlobal to avoid all-zero skip, scale=1
        var displacements = new[]
        {
            new SgMemberDisplacementData(1, 1, 0, 0.0, 0, 0, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 1, 5.0, 0, 0, 0.001, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 2, 10.0, 0, 0, 0, 0, 0, 0)
        };
        var result = DisplacedShapeBuilder.Build(displacements, TwoMemberMap(), 100.0, userScale: 1.0);

        // Station at location 5: original (5,0,0) + displacement (0,0,0.001) ≈ (5,0,0.001)
        var midPt = result.Polylines[0].Points[1];
        Assert.Equal(5.0, midPt.X, 6);
        Assert.Equal(0.0, midPt.Y, 6);
        Assert.Equal(0.001, midPt.Z, 6);
    }

    [Fact]
    public void Build_DisplacedPosition_OriginalPlusScaledDisplacement()
    {
        // Member 1: (0,0,0)→(10,0,0), station at location=5 → midpoint (5,0,0)
        // TyGlobal=3, scale=2 → displaced = (5, 6, 0)
        var displacements = new[]
        {
            new SgMemberDisplacementData(1, 1, 0, 0.0, 0, 0, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 1, 5.0, 0, 3, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 2, 10.0, 0, 0, 0, 0, 0, 0)
        };
        var result = DisplacedShapeBuilder.Build(displacements, TwoMemberMap(), 100.0, userScale: 2.0);

        var midPt = result.Polylines[0].Points[1];
        Assert.Equal(5.0, midPt.X, 6);
        Assert.Equal(6.0, midPt.Y, 6); // 3 × 2
        Assert.Equal(0.0, midPt.Z, 6);
    }

    [Fact]
    public void Build_TwoMembers_ProducesTwoPolylines()
    {
        var displacements = new[]
        {
            new SgMemberDisplacementData(1, 1, 0, 0.0, 0, 1, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 1, 10.0, 0, 1, 0, 0, 0, 0),
            new SgMemberDisplacementData(2, 1, 0, 0.0, 0, 2, 0, 0, 0, 0),
            new SgMemberDisplacementData(2, 1, 1, 10.0, 0, 2, 0, 0, 0, 0)
        };
        var result = DisplacedShapeBuilder.Build(displacements, TwoMemberMap(), 100.0, userScale: 1.0);

        Assert.Equal(2, result.Polylines.Count);
    }

    [Fact]
    public void Build_AllZeroDisplacements_SkipsMember()
    {
        var displacements = new[]
        {
            new SgMemberDisplacementData(1, 1, 0, 0.0, 0, 0, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 1, 10.0, 0, 0, 0, 0, 0, 0)
        };
        var result = DisplacedShapeBuilder.Build(displacements, TwoMemberMap(), 100.0, userScale: 1.0);

        Assert.Empty(result.Polylines);
    }

    [Fact]
    public void Build_UnmatchedMemberId_Skipped()
    {
        var displacements = new[]
        {
            new SgMemberDisplacementData(99, 1, 0, 0.0, 0, 5, 0, 0, 0, 0),
            new SgMemberDisplacementData(99, 1, 1, 10.0, 0, 5, 0, 0, 0, 0)
        };
        var result = DisplacedShapeBuilder.Build(displacements, TwoMemberMap(), 100.0, userScale: 1.0);

        Assert.Empty(result.Polylines);
    }

    // ── Auto-scale ────────────────────────────────────────────────────

    [Fact]
    public void Build_AutoScale_UsesMaxResultantStationMagnitude()
    {
        // Station at (5,0,0) has TyGlobal=6, TzGlobal=8 → resultant = 10
        var displacements = new[]
        {
            new SgMemberDisplacementData(1, 1, 0, 0.0, 0, 0, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 1, 5.0, 0, 6, 8, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 2, 10.0, 0, 0, 0, 0, 0, 0)
        };
        // bbox diagonal = 100, max mag = 10 → scale = (0.1 × 100) / 10 = 1.0
        var result = DisplacedShapeBuilder.Build(displacements, TwoMemberMap(), 100.0, userScale: null);

        Assert.Equal(1.0, result.ComputedScale, 6);
    }

    [Fact]
    public void Build_UserScaleOverride()
    {
        var displacements = new[]
        {
            new SgMemberDisplacementData(1, 1, 0, 0.0, 0, 5, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 1, 10.0, 0, 5, 0, 0, 0, 0)
        };
        var result = DisplacedShapeBuilder.Build(displacements, TwoMemberMap(), 100.0, userScale: 3.0);

        Assert.Equal(3.0, result.ComputedScale, 6);
    }

    // ── Station ordering ──────────────────────────────────────────────

    [Fact]
    public void Build_StationsOrderedByLocation()
    {
        // Stations given out of order — builder should sort by Location
        var displacements = new[]
        {
            new SgMemberDisplacementData(1, 1, 2, 10.0, 0, 1, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 0, 0.0, 0, 1, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 1, 5.0, 0, 3, 0, 0, 0, 0)
        };
        var result = DisplacedShapeBuilder.Build(displacements, TwoMemberMap(), 100.0, userScale: 1.0);

        var poly = result.Polylines[0].Points;
        // First point at location 0 → X=0, last at location 10 → X=10
        Assert.Equal(0.0, poly[0].X, 6);
        Assert.Equal(5.0, poly[1].X, 6);
        Assert.Equal(10.0, poly[2].X, 6);
    }

    // ── Diagonal member ───────────────────────────────────────────────

    [Fact]
    public void Build_DiagonalMember_StationInterpolatedCorrectly()
    {
        // Member from (0,0,0) to (6,8,0), length=10
        // Station at location=5 → midpoint (3,4,0)
        var memberMap = new Dictionary<int, (SgPoint3D Start, SgPoint3D End)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(6, 8, 0))
        };
        var displacements = new[]
        {
            new SgMemberDisplacementData(1, 1, 0, 0.0, 0, 0, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 1, 5.0, 0, 0, 1, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 2, 10.0, 0, 0, 0, 0, 0, 0)
        };
        var result = DisplacedShapeBuilder.Build(displacements, memberMap, 100.0, userScale: 1.0);

        var midPt = result.Polylines[0].Points[1];
        Assert.Equal(3.0, midPt.X, 6);   // 6 × (5/10) = 3
        Assert.Equal(4.0, midPt.Y, 6);   // 8 × (5/10) = 4
        Assert.Equal(1.0, midPt.Z, 6);   // 0 + TzGlobal×1 = 1
    }

    // ── Multi-load-case ───────────────────────────────────────────────

    [Fact]
    public void Build_MultipleLoadCases_ProducesSeparatePolylinesPerCase()
    {
        // Same member, two load cases with different displacements
        var displacements = new[]
        {
            new SgMemberDisplacementData(1, 1, 0, 0.0, 0, 1, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 1, 10.0, 0, 1, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 2, 0, 0.0, 0, 5, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 2, 1, 10.0, 0, 5, 0, 0, 0, 0)
        };
        var result = DisplacedShapeBuilder.Build(displacements, TwoMemberMap(), 100.0, userScale: 1.0);

        // Two polylines: one for LC1, one for LC2
        Assert.Equal(2, result.Polylines.Count);

        // LC1 has Ty=1, LC2 has Ty=5
        var lc1Start = result.Polylines[0].Points[0];
        var lc2Start = result.Polylines[1].Points[0];
        Assert.Equal(1.0, lc1Start.Y, 6);
        Assert.Equal(5.0, lc2Start.Y, 6);
    }

    [Fact]
    public void Build_MaxDisplacement_IncludedPerPolyline()
    {
        var displacements = new[]
        {
            new SgMemberDisplacementData(1, 1, 0, 0.0, 0, 0, 0, 0, 0, 0),
            new SgMemberDisplacementData(1, 1, 1, 5.0, 0, 3, 4, 0, 0, 0), // mag = 5
            new SgMemberDisplacementData(1, 1, 2, 10.0, 0, 0, 0, 0, 0, 0)
        };
        var result = DisplacedShapeBuilder.Build(displacements, TwoMemberMap(), 100.0, userScale: 1.0);

        Assert.Equal(5.0, result.Polylines[0].MaxDisplacement, 6);
    }
}
