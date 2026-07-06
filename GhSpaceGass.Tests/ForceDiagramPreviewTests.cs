using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Models.Visuals;
using Xunit;

namespace GhSpaceGass.Tests;

public class ForceDiagramPreviewTests
{
    private static Dictionary<int, (SgPoint3D Start, SgPoint3D End)> HorizontalMember() => new()
    {
        [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0))
    };

    private static List<SgMemberIntermediateForceData> ThreeStationFy(double fy0, double fy1, double fy2) =>
        new()
        {
            new(1, 1, 0, 0.0, 0, fy0, 0, 0, 0, 0),
            new(1, 1, 1, 5.0, 0, fy1, 0, 0, 0, 0),
            new(1, 1, 2, 10.0, 0, fy2, 0, 0, 0, 0)
        };

    // ── Empty / disabled ──────────────────────────────────────────────

    [Fact]
    public void Build_EmptyForces_ReturnsNoDiagrams()
    {
        var result = ForceDiagramBuilder.BuildSingleComponent(
            Array.Empty<SgMemberIntermediateForceData>(),
            new Dictionary<int, (SgPoint3D, SgPoint3D)>(),
            100.0, 1, null);

        Assert.Empty(result.Diagrams);
    }

    [Fact]
    public void Build_AllScalesZero_ReturnsNoDiagrams()
    {
        var result = ForceDiagramBuilder.BuildSingleComponent(
            ThreeStationFy(10, 20, 10), HorizontalMember(),
            100.0, 1, 0.0);

        Assert.Empty(result.Diagrams);
    }

    [Fact]
    public void Build_NegativeScale_DisablesGroup()
    {
        var result = ForceDiagramBuilder.BuildSingleComponent(
            ThreeStationFy(10, 20, 10), HorizontalMember(),
            100.0, 1, -1.0);

        // Fy is in Force group which is disabled, no other forces → no diagrams
        Assert.Empty(result.Diagrams);
    }

    // ── Diagram generation ────────────────────────────────────────────

    [Fact]
    public void Build_FyOnly_ProducesForceDiagram()
    {
        var forces = ThreeStationFy(10, 20, 10);
        var result = ForceDiagramBuilder.BuildSingleComponent(
            forces, HorizontalMember(), 100.0,
            1, 1.0);

        // One member → one diagram group with Fy
        Assert.NotEmpty(result.Diagrams);
        var diag = result.Diagrams[0];
        Assert.Equal(3, diag.DiagramPoints.Count);
        Assert.Equal(3, diag.BasePoints.Count);
    }

    [Fact]
    public void Build_FyOffset_PerpendicularToMember()
    {
        // Horizontal member along X. Fy uses perp1.
        // For member along X, perp1 = cross(X, Z) = -Y → or +Y depending on convention
        var forces = new List<SgMemberIntermediateForceData>
        {
            new(1, 1, 0, 0.0, 0, 0, 0, 0, 0, 0),
            new(1, 1, 1, 5.0, 0, 10, 0, 0, 0, 0),
            new(1, 1, 2, 10.0, 0, 0, 0, 0, 0, 0)
        };
        var result = ForceDiagramBuilder.BuildSingleComponent(
            forces, HorizontalMember(), 100.0,
            1, 1.0);

        var diag = result.Diagrams[0];
        var midBase = diag.BasePoints[1];
        var midDiag = diag.DiagramPoints[1];

        // Base point at station 5 along X member → (5, 0, 0)
        Assert.Equal(5.0, midBase.X, 6);
        Assert.Equal(0.0, midBase.Z, 6);

        // Diagram point offset perpendicular (in Y or Z direction, not along X)
        Assert.Equal(5.0, midDiag.X, 6); // same X as base
        // The offset is in the perpendicular direction, not along the member
        var offsetMagnitude = Math.Sqrt(
            Math.Pow(midDiag.X - midBase.X, 2) +
            Math.Pow(midDiag.Y - midBase.Y, 2) +
            Math.Pow(midDiag.Z - midBase.Z, 2));
        Assert.Equal(10.0, offsetMagnitude, 4); // Fy=10 × scale=1 = 10
    }

    [Fact]
    public void Build_FzOnPerp2_DifferentFromFyOnPerp1()
    {
        // Member along X. Fy on perp1, Fz on perp2 — perpendicular to each other
        var forces = new List<SgMemberIntermediateForceData>
        {
            new(1, 1, 0, 0.0, 0, 0, 0, 0, 0, 0),
            new(1, 1, 1, 5.0, 0, 10, 10, 0, 0, 0), // Fy=10, Fz=10
            new(1, 1, 2, 10.0, 0, 0, 0, 0, 0, 0)
        };

        // Build Fy diagram (component 1) and Fz diagram (component 2) separately
        var fyResult = ForceDiagramBuilder.BuildSingleComponent(
            forces, HorizontalMember(), 100.0, 1, 1.0);
        var fzResult = ForceDiagramBuilder.BuildSingleComponent(
            forces, HorizontalMember(), 100.0, 2, 1.0);

        Assert.Single(fyResult.Diagrams);
        Assert.Single(fzResult.Diagrams);

        var fyOffset = new SgPoint3D(
            fyResult.Diagrams[0].DiagramPoints[1].X - fyResult.Diagrams[0].BasePoints[1].X,
            fyResult.Diagrams[0].DiagramPoints[1].Y - fyResult.Diagrams[0].BasePoints[1].Y,
            fyResult.Diagrams[0].DiagramPoints[1].Z - fyResult.Diagrams[0].BasePoints[1].Z);
        var fzOffset = new SgPoint3D(
            fzResult.Diagrams[0].DiagramPoints[1].X - fzResult.Diagrams[0].BasePoints[1].X,
            fzResult.Diagrams[0].DiagramPoints[1].Y - fzResult.Diagrams[0].BasePoints[1].Y,
            fzResult.Diagrams[0].DiagramPoints[1].Z - fzResult.Diagrams[0].BasePoints[1].Z);

        // Fy and Fz offsets should be perpendicular to each other (dot product ≈ 0)
        var dot = fyOffset.X * fzOffset.X + fyOffset.Y * fzOffset.Y + fyOffset.Z * fzOffset.Z;
        Assert.True(Math.Abs(dot) < 0.001, $"Fy and Fz offsets should be perpendicular, dot={dot}");
    }

    // ── Auto-scale ────────────────────────────────────────────────────

    [Fact]
    public void Build_ForceAutoScale_FromMaxFyFz()
    {
        // Fy max=20 at station 1, Fz all zero → max magnitude = 20
        var result = ForceDiagramBuilder.BuildSingleComponent(
            ThreeStationFy(0, 20, 0), HorizontalMember(), 100.0,
            1, null);

        // auto = (0.1 × 100) / 20 = 0.5
        Assert.Equal(0.5, result.ForceScale, 6);
    }

    [Fact]
    public void Build_MomentAutoScale()
    {
        // My=100 at station 1
        var forces = new List<SgMemberIntermediateForceData>
        {
            new(1, 1, 0, 0.0, 0, 0, 0, 0, 0, 0),
            new(1, 1, 1, 5.0, 0, 0, 0, 0, 100, 0),
            new(1, 1, 2, 10.0, 0, 0, 0, 0, 0, 0)
        };
        var result = ForceDiagramBuilder.BuildSingleComponent(
            forces, HorizontalMember(), 100.0, 4, null);

        // auto = (0.1 × 100) / 100 = 0.1
        Assert.Equal(0.1, result.ForceScale, 6);
    }

    // ── All-zero skip ─────────────────────────────────────────────────

    [Fact]
    public void Build_AllZeroFy_SkipsDiagram()
    {
        var result = ForceDiagramBuilder.BuildSingleComponent(
            ThreeStationFy(0, 0, 0), HorizontalMember(), 100.0,
            1, 1.0);

        Assert.Empty(result.Diagrams);
    }

    // ── Unmatched member ──────────────────────────────────────────────

    [Fact]
    public void Build_UnmatchedMember_Skipped()
    {
        var forces = new List<SgMemberIntermediateForceData>
        {
            new(99, 1, 0, 0.0, 0, 10, 0, 0, 0, 0),
            new(99, 1, 1, 10.0, 0, 10, 0, 0, 0, 0)
        };
        var result = ForceDiagramBuilder.BuildSingleComponent(
            forces, HorizontalMember(), 100.0,
            1, 1.0);

        Assert.Empty(result.Diagrams);
    }

    // ── Multi-load-case ───────────────────────────────────────────────

    [Fact]
    public void Build_MultiLoadCase_SeparateDiagrams()
    {
        var forces = new List<SgMemberIntermediateForceData>
        {
            new(1, 1, 0, 0.0, 0, 10, 0, 0, 0, 0),
            new(1, 1, 1, 10.0, 0, 10, 0, 0, 0, 0),
            new(1, 2, 0, 0.0, 0, 20, 0, 0, 0, 0),
            new(1, 2, 1, 10.0, 0, 20, 0, 0, 0, 0)
        };
        var result = ForceDiagramBuilder.BuildSingleComponent(
            forces, HorizontalMember(), 100.0,
            1, 1.0);

        // 2 Fy diagrams: one per load case
        Assert.Equal(2, result.Diagrams.Count);
    }

    // ── Max value per diagram ─────────────────────────────────────────

    [Fact]
    public void Build_MaxAbsValue_ComputedPerDiagram()
    {
        var result = ForceDiagramBuilder.BuildSingleComponent(
            ThreeStationFy(-5, 20, -15), HorizontalMember(), 100.0,
            1, 1.0);

        Assert.Equal(20.0, result.Diagrams[0].MaxAbsValue, 6);
    }

    // ── Vertical member perpendicular ─────────────────────────────────

    [Fact]
    public void Build_VerticalMember_PerpComputedWithoutDegeneracy()
    {
        var memberMap = new Dictionary<int, (SgPoint3D Start, SgPoint3D End)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(0, 0, 10))
        };
        var forces = new List<SgMemberIntermediateForceData>
        {
            new(1, 1, 0, 0.0, 0, 10, 0, 0, 0, 0),
            new(1, 1, 1, 5.0, 0, 10, 0, 0, 0, 0),
            new(1, 1, 2, 10.0, 0, 10, 0, 0, 0, 0)
        };
        var result = ForceDiagramBuilder.BuildSingleComponent(
            forces, memberMap, 100.0,
            1, 1.0);

        // Should not crash, should produce a diagram
        Assert.NotEmpty(result.Diagrams);
        // Offset should be non-zero and perpendicular to Z-axis member
        var offset = Math.Sqrt(
            Math.Pow(result.Diagrams[0].DiagramPoints[0].X - result.Diagrams[0].BasePoints[0].X, 2) +
            Math.Pow(result.Diagrams[0].DiagramPoints[0].Y - result.Diagrams[0].BasePoints[0].Y, 2));
        Assert.True(offset > 0, "Perpendicular offset should be non-zero for vertical member");
    }

    // ── User scale override ───────────────────────────────────────────

    [Fact]
    public void Build_UserForceScaleOverride()
    {
        var result = ForceDiagramBuilder.BuildSingleComponent(
            ThreeStationFy(0, 10, 0), HorizontalMember(), 100.0,
            1, 3.0);

        Assert.Equal(3.0, result.ForceScale, 6);
    }

    // ── All-null auto-scale ───────────────────────────────────────────

    [Fact]
    public void Build_NullScale_AutoScalesForSelectedComponent()
    {
        // Mz=200 at station 1
        var forces = new List<SgMemberIntermediateForceData>
        {
            new(1, 1, 0, 0.0, 10, 20, 30, 5, 100, 200),
            new(1, 1, 1, 5.0, 10, 20, 30, 5, 100, 200),
            new(1, 1, 2, 10.0, 10, 20, 30, 5, 100, 200)
        };
        // Component 5 = Mz, null scale → auto
        var result = ForceDiagramBuilder.BuildSingleComponent(
            forces, HorizontalMember(), 100.0, 5, null);

        // auto = (0.1 × 100) / 200 = 0.05
        Assert.Single(result.Diagrams);
        Assert.Equal(0.05, result.ForceScale, 6);
        Assert.Equal(DiagramGroup.Moment, result.Diagrams[0].Group);
    }

    // ── Extrema detection ─────────────────────────────────────────────

    [Fact]
    public void FindExtrema_SimplePeak_DetectedAtMidpoint()
    {
        // Triangle: 0, 10, 0 → peak at index 1
        var indices = ForceDiagramBuilder.FindExtremaIndices(new List<double> { 0, 10, 0 });
        Assert.Single(indices);
        Assert.Equal(1, indices[0]);
    }

    [Fact]
    public void FindExtrema_SimpleTrough_DetectedAtMidpoint()
    {
        // Inverted: 0, -15, 0 → trough at index 1
        var indices = ForceDiagramBuilder.FindExtremaIndices(new List<double> { 0, -15, 0 });
        Assert.Single(indices);
        Assert.Equal(1, indices[0]);
    }

    [Fact]
    public void FindExtrema_PeakAndTrough_BothDetected()
    {
        // 0, 10, 0, -5, 0 → peak at 1, trough at 3
        var indices = ForceDiagramBuilder.FindExtremaIndices(new List<double> { 0, 10, 0, -5, 0 });
        Assert.Equal(2, indices.Count);
        Assert.Contains(1, indices);
        Assert.Contains(3, indices);
    }

    [Fact]
    public void FindExtrema_NonZeroEndpoints_Included()
    {
        // 5, 0, 10 → start (5) and end (10) are non-zero, peak at index 2
        var indices = ForceDiagramBuilder.FindExtremaIndices(new List<double> { 5, 0, 10 });
        Assert.Contains(0, indices); // start
        Assert.Contains(2, indices); // end (also peak)
    }

    [Fact]
    public void FindExtrema_AllZero_Empty()
    {
        var indices = ForceDiagramBuilder.FindExtremaIndices(new List<double> { 0, 0, 0 });
        Assert.Empty(indices);
    }

    [Fact]
    public void FindExtrema_Constant_EndpointsOnly()
    {
        // 5, 5, 5 → no interior extrema, but endpoints are non-zero
        var indices = ForceDiagramBuilder.FindExtremaIndices(new List<double> { 5, 5, 5 });
        Assert.Equal(2, indices.Count);
        Assert.Contains(0, indices);
        Assert.Contains(2, indices);
    }

    [Fact]
    public void Build_StationValues_StoredInDiagram()
    {
        var result = ForceDiagramBuilder.BuildSingleComponent(
            ThreeStationFy(-5, 20, -15), HorizontalMember(), 100.0,
            1, 1.0);

        var diag = result.Diagrams[0];
        Assert.Equal(3, diag.StationValues.Count);
        Assert.Equal(-5.0, diag.StationValues[0], 6);
        Assert.Equal(20.0, diag.StationValues[1], 6);
        Assert.Equal(-15.0, diag.StationValues[2], 6);
    }

    [Fact]
    public void Build_ExtremaIndices_MatchPeaksAndTroughs()
    {
        // Fy: -5, 20, -15 → all 3 are extrema (start non-zero, peak at 1, end non-zero)
        var result = ForceDiagramBuilder.BuildSingleComponent(
            ThreeStationFy(-5, 20, -15), HorizontalMember(), 100.0,
            1, 1.0);

        var diag = result.Diagrams[0];
        Assert.Equal(3, diag.ExtremaIndices.Count);
        Assert.Contains(0, diag.ExtremaIndices); // -5 (non-zero start)
        Assert.Contains(1, diag.ExtremaIndices); // 20 (peak)
        Assert.Contains(2, diag.ExtremaIndices); // -15 (non-zero end)
    }
}
