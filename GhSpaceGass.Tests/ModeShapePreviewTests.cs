using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Models.Visuals;
using Xunit;

namespace GhSpaceGass.Tests;

public class ModeShapePreviewTests
{
    private static Dictionary<int, (SgPoint3D Start, SgPoint3D End)> TwoMemberMap() => new()
    {
        [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0)),
        [2] = (new SgPoint3D(10, 0, 0), new SgPoint3D(20, 0, 0))
    };

    private static Dictionary<int, SgPoint3D> ThreeNodeMap() => new()
    {
        [1] = new SgPoint3D(0, 0, 0),
        [2] = new SgPoint3D(10, 0, 0),
        [3] = new SgPoint3D(20, 0, 0)
    };

    // ── Empty / disabled ──────────────────────────────────────────────

    [Fact]
    public void Build_EmptyModeShapes_ReturnsNoPolylines()
    {
        var result = ModeShapeBuilder.Build(
            Array.Empty<SgModeShapeNodeData>(),
            Array.Empty<SgNaturalFrequencyData>(),
            new Dictionary<int, (SgPoint3D, SgPoint3D)>(),
            new Dictionary<int, SgPoint3D>(),
            100.0, userScale: null);

        Assert.Empty(result.Polylines);
    }

    [Fact]
    public void Build_ZeroScale_DisablesPreview()
    {
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 1, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 1, 2, 1, 0, 0, 0, 0, 0)
        };
        var result = ModeShapeBuilder.Build(modeShapes, Array.Empty<SgNaturalFrequencyData>(),
            TwoMemberMap(), ThreeNodeMap(), 100.0, userScale: 0.0);

        Assert.Empty(result.Polylines);
    }

    [Fact]
    public void Build_NegativeScale_DisablesPreview()
    {
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 1, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 1, 2, 1, 0, 0, 0, 0, 0)
        };
        var result = ModeShapeBuilder.Build(modeShapes, Array.Empty<SgNaturalFrequencyData>(),
            TwoMemberMap(), ThreeNodeMap(), 100.0, userScale: -1.0);

        Assert.Empty(result.Polylines);
    }

    // ── Polyline generation ───────────────────────────────────────────

    [Fact]
    public void Build_SingleMode_SingleMember_ProducesOnePolyline()
    {
        // Member 1: nodes 1 (0,0,0) → 2 (10,0,0)
        // Mode 1: Tx=0 at node 1, Ty=0.5 at node 2
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 1, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 1, 2, 0, 0.5, 0, 0, 0, 0)
        };
        var memberMap = new Dictionary<int, (SgPoint3D, SgPoint3D)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0))
        };
        var result = ModeShapeBuilder.Build(modeShapes, Array.Empty<SgNaturalFrequencyData>(),
            memberMap, ThreeNodeMap(), 100.0, userScale: 1.0);

        Assert.Single(result.Polylines);
        Assert.Equal(2, result.Polylines[0].Points.Count);
    }

    [Fact]
    public void Build_DeformedEndpoints_OriginalPlusScaledDisplacement()
    {
        // Member from (0,0,0)→(10,0,0). Node 1 Ty=0; Node 2 Ty=2 with scale=3 → deformed end Y=6
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 1, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 1, 2, 0, 2, 0, 0, 0, 0)
        };
        var memberMap = new Dictionary<int, (SgPoint3D, SgPoint3D)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0))
        };
        var result = ModeShapeBuilder.Build(modeShapes, Array.Empty<SgNaturalFrequencyData>(),
            memberMap, ThreeNodeMap(), 100.0, userScale: 3.0);

        var poly = result.Polylines[0];
        Assert.Equal(0.0, poly.Points[0].X, 6);
        Assert.Equal(0.0, poly.Points[0].Y, 6);
        Assert.Equal(0.0, poly.Points[0].Z, 6);
        Assert.Equal(10.0, poly.Points[1].X, 6);
        Assert.Equal(6.0, poly.Points[1].Y, 6);
        Assert.Equal(0.0, poly.Points[1].Z, 6);
    }

    [Fact]
    public void Build_MultipleMembers_ProducesPolylinePerMember()
    {
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 1, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 1, 2, 0, 0.5, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 1, 3, 0, 0.7, 0, 0, 0, 0)
        };
        var result = ModeShapeBuilder.Build(modeShapes, Array.Empty<SgNaturalFrequencyData>(),
            TwoMemberMap(), ThreeNodeMap(), 100.0, userScale: 1.0);

        Assert.Equal(2, result.Polylines.Count);
    }

    [Fact]
    public void Build_AllZeroModeShapeOnBothEnds_SkipsMember()
    {
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 1, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 1, 2, 0, 0, 0, 0, 0, 0)
        };
        var memberMap = new Dictionary<int, (SgPoint3D, SgPoint3D)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0))
        };
        var result = ModeShapeBuilder.Build(modeShapes, Array.Empty<SgNaturalFrequencyData>(),
            memberMap, ThreeNodeMap(), 100.0, userScale: 1.0);

        Assert.Empty(result.Polylines);
    }

    [Fact]
    public void Build_NonZeroAtOneEnd_DrawsMember()
    {
        // Only end B has displacement — should still draw
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 1, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 1, 2, 0, 0.3, 0, 0, 0, 0)
        };
        var memberMap = new Dictionary<int, (SgPoint3D, SgPoint3D)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0))
        };
        var result = ModeShapeBuilder.Build(modeShapes, Array.Empty<SgNaturalFrequencyData>(),
            memberMap, ThreeNodeMap(), 100.0, userScale: 1.0);

        Assert.Single(result.Polylines);
    }

    [Fact]
    public void Build_MissingModeShapeForEnd_TreatsAsZero()
    {
        // Only node 2 has mode shape data — node 1 endpoint stays at original position
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 1, 2, 0, 0.5, 0, 0, 0, 0)
        };
        var memberMap = new Dictionary<int, (SgPoint3D, SgPoint3D)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0))
        };
        var result = ModeShapeBuilder.Build(modeShapes, Array.Empty<SgNaturalFrequencyData>(),
            memberMap, ThreeNodeMap(), 100.0, userScale: 1.0);

        Assert.Single(result.Polylines);
        var poly = result.Polylines[0];
        // Start end: no mode shape → original (0,0,0)
        Assert.Equal(0.0, poly.Points[0].X, 6);
        Assert.Equal(0.0, poly.Points[0].Y, 6);
        // End: original (10,0,0) + Ty=0.5 * scale=1 → (10, 0.5, 0)
        Assert.Equal(10.0, poly.Points[1].X, 6);
        Assert.Equal(0.5, poly.Points[1].Y, 6);
    }

    // ── Multi-mode ────────────────────────────────────────────────────

    [Fact]
    public void Build_TwoModes_ProducesSeparatePolylinesPerMode()
    {
        // Same member, two modes with different shape magnitudes
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 1, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 1, 2, 0, 0.5, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 2, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 2, 2, 0, 0, 0.8, 0, 0, 0)
        };
        var memberMap = new Dictionary<int, (SgPoint3D, SgPoint3D)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0))
        };
        var result = ModeShapeBuilder.Build(modeShapes, Array.Empty<SgNaturalFrequencyData>(),
            memberMap, ThreeNodeMap(), 100.0, userScale: 1.0);

        Assert.Equal(2, result.Polylines.Count);
    }

    [Fact]
    public void Build_PaletteIndex_CyclesByModeNumber()
    {
        // Modes 1 → PaletteIndex 0, Mode 2 → 1, Mode 9 → 0 (cycles at 8)
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 1, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 1, 2, 0, 0.5, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 2, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 2, 2, 0, 0.5, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 9, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 9, 2, 0, 0.5, 0, 0, 0, 0)
        };
        var memberMap = new Dictionary<int, (SgPoint3D, SgPoint3D)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0))
        };
        var result = ModeShapeBuilder.Build(modeShapes, Array.Empty<SgNaturalFrequencyData>(),
            memberMap, ThreeNodeMap(), 100.0, userScale: 1.0);

        var mode1 = result.Polylines.Single(p => p.Mode == 1);
        var mode2 = result.Polylines.Single(p => p.Mode == 2);
        var mode9 = result.Polylines.Single(p => p.Mode == 9);
        Assert.Equal(0, mode1.PaletteIndex);
        Assert.Equal(1, mode2.PaletteIndex);
        Assert.Equal(0, mode9.PaletteIndex); // (9 - 1) % 8 = 0
    }

    // ── Frequency lookup ──────────────────────────────────────────────

    [Fact]
    public void Build_FrequencyLookedUpByMode_PopulatedOnPolyline()
    {
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 1, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 1, 2, 0, 0.5, 0, 0, 0, 0)
        };
        var frequencies = new[]
        {
            new SgNaturalFrequencyData(1, 1, 2.5, 0.4, 0.8, 0.1, 0.05),
            new SgNaturalFrequencyData(1, 2, 5.0, 0.2, 0.1, 0.7, 0.02)
        };
        var memberMap = new Dictionary<int, (SgPoint3D, SgPoint3D)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0))
        };
        var result = ModeShapeBuilder.Build(modeShapes, frequencies,
            memberMap, ThreeNodeMap(), 100.0, userScale: 1.0);

        Assert.Single(result.Polylines);
        Assert.Equal(2.5, result.Polylines[0].Frequency);
    }

    [Fact]
    public void Build_FrequencyMissing_FrequencyIsNull()
    {
        // No matching natural frequency for mode 3
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 3, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 3, 2, 0, 0.5, 0, 0, 0, 0)
        };
        var frequencies = new[]
        {
            new SgNaturalFrequencyData(1, 1, 2.5, 0.4, 0, 0, 0)
        };
        var memberMap = new Dictionary<int, (SgPoint3D, SgPoint3D)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0))
        };
        var result = ModeShapeBuilder.Build(modeShapes, frequencies,
            memberMap, ThreeNodeMap(), 100.0, userScale: 1.0);

        Assert.Null(result.Polylines[0].Frequency);
    }

    [Fact]
    public void Build_FrequencyMatchedByLoadCaseAndMode()
    {
        // Frequencies live under load case 1 mode 1 AND load case 2 mode 1 — different values
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(2, 1, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(2, 1, 2, 0, 0.5, 0, 0, 0, 0)
        };
        var frequencies = new[]
        {
            new SgNaturalFrequencyData(1, 1, 2.5, 0.4, 0, 0, 0),
            new SgNaturalFrequencyData(2, 1, 7.0, 0.14, 0, 0, 0)
        };
        var memberMap = new Dictionary<int, (SgPoint3D, SgPoint3D)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0))
        };
        var result = ModeShapeBuilder.Build(modeShapes, frequencies,
            memberMap, ThreeNodeMap(), 100.0, userScale: 1.0);

        Assert.Equal(7.0, result.Polylines[0].Frequency);
    }

    // ── Filtering and skipping ────────────────────────────────────────

    [Fact]
    public void Build_UnmatchedMemberEndNode_Skipped()
    {
        // Member endpoints reference nodes 99 & 100 that are not in ThreeNodeMap
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 1, 99, 0, 0.5, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 1, 100, 0, 0.5, 0, 0, 0, 0)
        };
        var memberMap = new Dictionary<int, (SgPoint3D, SgPoint3D)>
        {
            // Member endpoints do not match any node in the nodeIdToPoint map
            [1] = (new SgPoint3D(500, 500, 500), new SgPoint3D(600, 600, 600))
        };
        var result = ModeShapeBuilder.Build(modeShapes, Array.Empty<SgNaturalFrequencyData>(),
            memberMap, ThreeNodeMap(), 100.0, userScale: 1.0);

        Assert.Empty(result.Polylines);
    }

    // ── Auto-scale ────────────────────────────────────────────────────

    [Fact]
    public void Build_AutoScale_UsesMaxResultantModeShapeMagnitude()
    {
        // Max resultant = sqrt(6² + 8²) = 10; bbox diag = 100 → scale = 1.0
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 1, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 1, 2, 6, 8, 0, 0, 0, 0)
        };
        var memberMap = new Dictionary<int, (SgPoint3D, SgPoint3D)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0))
        };
        var result = ModeShapeBuilder.Build(modeShapes, Array.Empty<SgNaturalFrequencyData>(),
            memberMap, ThreeNodeMap(), 100.0, userScale: null);

        Assert.Equal(1.0, result.ComputedScale, 6);
    }

    [Fact]
    public void Build_UserScaleOverride()
    {
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 1, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 1, 2, 0, 0.5, 0, 0, 0, 0)
        };
        var memberMap = new Dictionary<int, (SgPoint3D, SgPoint3D)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0))
        };
        var result = ModeShapeBuilder.Build(modeShapes, Array.Empty<SgNaturalFrequencyData>(),
            memberMap, ThreeNodeMap(), 100.0, userScale: 5.0);

        Assert.Equal(5.0, result.ComputedScale, 6);
    }

    // ── Polyline metadata ─────────────────────────────────────────────

    [Fact]
    public void Build_PolylineCarriesLoadCaseAndMode()
    {
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(3, 2, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(3, 2, 2, 0, 0.5, 0, 0, 0, 0)
        };
        var memberMap = new Dictionary<int, (SgPoint3D, SgPoint3D)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0))
        };
        var result = ModeShapeBuilder.Build(modeShapes, Array.Empty<SgNaturalFrequencyData>(),
            memberMap, ThreeNodeMap(), 100.0, userScale: 1.0);

        Assert.Equal(3, result.Polylines[0].LoadCaseId);
        Assert.Equal(2, result.Polylines[0].Mode);
    }

    [Fact]
    public void Build_MaxResultant_IncludedPerPolyline()
    {
        // Node 2 has resultant sqrt(3² + 4²) = 5; node 1 is zero — max should be 5
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 1, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 1, 2, 3, 4, 0, 0, 0, 0)
        };
        var memberMap = new Dictionary<int, (SgPoint3D, SgPoint3D)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0))
        };
        var result = ModeShapeBuilder.Build(modeShapes, Array.Empty<SgNaturalFrequencyData>(),
            memberMap, ThreeNodeMap(), 100.0, userScale: 1.0);

        Assert.Equal(5.0, result.Polylines[0].MaxResultant, 6);
    }

    // ── Peak point (label anchor) ─────────────────────────────────────

    [Fact]
    public void Build_PeakPoint_IsDeformedEndWithHigherResultant()
    {
        // Node 1 has zero displacement; node 2 has resultant sqrt(3² + 4²) = 5
        // Peak point should be the deformed end (node 2's deformed position)
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 1, 1, 0, 0, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 1, 2, 3, 4, 0, 0, 0, 0)
        };
        var memberMap = new Dictionary<int, (SgPoint3D, SgPoint3D)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0))
        };
        var result = ModeShapeBuilder.Build(modeShapes, Array.Empty<SgNaturalFrequencyData>(),
            memberMap, ThreeNodeMap(), 100.0, userScale: 1.0);

        var peak = result.Polylines[0].PeakPoint;
        // Deformed end = (10 + 3, 0 + 4, 0) = (13, 4, 0)
        Assert.Equal(13.0, peak.X, 6);
        Assert.Equal(4.0, peak.Y, 6);
        Assert.Equal(0.0, peak.Z, 6);
    }

    [Fact]
    public void Build_PeakPoint_PicksStartWhenStartHasHigherResultant()
    {
        // Start has resultant 5, end has resultant 1 → peak = start's deformed point
        var modeShapes = new[]
        {
            new SgModeShapeNodeData(1, 1, 1, 3, 4, 0, 0, 0, 0),
            new SgModeShapeNodeData(1, 1, 2, 1, 0, 0, 0, 0, 0)
        };
        var memberMap = new Dictionary<int, (SgPoint3D, SgPoint3D)>
        {
            [1] = (new SgPoint3D(0, 0, 0), new SgPoint3D(10, 0, 0))
        };
        var result = ModeShapeBuilder.Build(modeShapes, Array.Empty<SgNaturalFrequencyData>(),
            memberMap, ThreeNodeMap(), 100.0, userScale: 1.0);

        var peak = result.Polylines[0].PeakPoint;
        // Deformed start = (0 + 3, 0 + 4, 0) = (3, 4, 0)
        Assert.Equal(3.0, peak.X, 6);
        Assert.Equal(4.0, peak.Y, 6);
        Assert.Equal(0.0, peak.Z, 6);
    }
}
