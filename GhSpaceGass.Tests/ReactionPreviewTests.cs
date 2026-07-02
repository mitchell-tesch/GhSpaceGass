using GhSpaceGass.Core.Models;
using Xunit;

namespace GhSpaceGass.Tests;

public class ReactionPreviewTests
{
    // ── Auto-scale computation ────────────────────────────────────────

    [Fact]
    public void ComputeAutoScale_NormalCase_ReturnsExpectedScale()
    {
        // bbox diagonal = 100, max magnitude = 50
        // expected = (0.1 × 100) / 50 = 0.2
        var scale = PreviewScaleHelper.ComputeAutoScale(100.0, 50.0);
        Assert.Equal(0.2, scale, 6);
    }

    [Fact]
    public void ComputeAutoScale_ZeroMaxMagnitude_ReturnsOne()
    {
        // No reactions to draw — return 1.0 to avoid division by zero
        var scale = PreviewScaleHelper.ComputeAutoScale(100.0, 0.0);
        Assert.Equal(1.0, scale);
    }

    [Fact]
    public void ComputeAutoScale_VerySmallMaxMagnitude_CapsScale()
    {
        // Very tiny forces on a large model — scale shouldn't explode
        var scale = PreviewScaleHelper.ComputeAutoScale(100.0, 0.0001);
        Assert.True(scale > 0, "Scale should be positive");
        Assert.True(double.IsFinite(scale), "Scale should be finite");
    }

    [Fact]
    public void ComputeAutoScale_ZeroBboxDiagonal_ReturnsOne()
    {
        // Degenerate model (all nodes coincident)
        var scale = PreviewScaleHelper.ComputeAutoScale(0.0, 50.0);
        Assert.Equal(1.0, scale);
    }

    [Fact]
    public void ComputeAutoScale_CustomProportion_UsesProvided()
    {
        // proportion = 0.2 → (0.2 × 100) / 50 = 0.4
        var scale = PreviewScaleHelper.ComputeAutoScale(100.0, 50.0, 0.2);
        Assert.Equal(0.4, scale, 6);
    }

    // ── ReactionPreviewBuilder — arrow generation ─────────────────────

    [Fact]
    public void Build_EmptyReactions_ReturnsNoArrows()
    {
        var result = ReactionPreviewBuilder.Build(
            Array.Empty<SgNodeReactionData>(),
            new Dictionary<int, SgPoint3D>(),
            100.0,
            userScale: null);

        Assert.Empty(result.Arrows);
    }

    [Fact]
    public void Build_SingleForceComponent_ProducesOneArrow()
    {
        var reactions = new[]
        {
            new SgNodeReactionData(nodeId: 1, loadCaseId: 1,
                fx: 100.0, fy: 0, fz: 0, mx: 0, my: 0, mz: 0)
        };
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(0, 0, 0) };

        var result = ReactionPreviewBuilder.Build(reactions, nodeMap, 100.0, userScale: null);

        Assert.Single(result.Arrows);
        var arrow = result.Arrows[0];
        Assert.Equal(ArrowType.Force, arrow.Type);
        Assert.Equal(0, arrow.Axis); // X
        Assert.Equal(0, arrow.Origin.X);
        Assert.Equal(0, arrow.Origin.Y);
        Assert.Equal(0, arrow.Origin.Z);
    }

    [Fact]
    public void Build_AllSixComponents_ProducesSixArrows()
    {
        var reactions = new[]
        {
            new SgNodeReactionData(nodeId: 1, loadCaseId: 1,
                fx: 10, fy: 20, fz: 30, mx: 1, my: 2, mz: 3)
        };
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(0, 0, 0) };

        var result = ReactionPreviewBuilder.Build(reactions, nodeMap, 100.0, userScale: null);

        // 3 force arrows + 3 moment arcs
        Assert.Equal(6, result.Arrows.Count);
        Assert.Equal(3, result.Arrows.Count(a => a.Type == ArrowType.Force));
        Assert.Equal(3, result.Arrows.Count(a => a.Type == ArrowType.Moment));
    }

    [Fact]
    public void Build_ZeroComponents_AreSkipped()
    {
        var reactions = new[]
        {
            new SgNodeReactionData(nodeId: 1, loadCaseId: 1,
                fx: 0, fy: 50, fz: 0, mx: 0, my: 0, mz: 10)
        };
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(5, 0, 0) };

        var result = ReactionPreviewBuilder.Build(reactions, nodeMap, 100.0, userScale: null);

        // Only Fy (force, Y) and Mz (moment, Z)
        Assert.Equal(2, result.Arrows.Count);
        Assert.Contains(result.Arrows, a => a.Type == ArrowType.Force && a.Axis == 1);
        Assert.Contains(result.Arrows, a => a.Type == ArrowType.Moment && a.Axis == 2);
    }

    [Fact]
    public void Build_ArrowDirectionMatchesForceSign()
    {
        var reactions = new[]
        {
            new SgNodeReactionData(nodeId: 1, loadCaseId: 1,
                fx: -50, fy: 0, fz: 0, mx: 0, my: 0, mz: 0)
        };
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(0, 0, 0) };

        var result = ReactionPreviewBuilder.Build(reactions, nodeMap, 100.0, userScale: null);

        var arrow = result.Arrows[0];
        // Negative Fx → arrow in negative X direction
        Assert.True(arrow.Dx < 0, "Arrow direction should be negative X for negative Fx");
        Assert.Equal(0, arrow.Dy);
        Assert.Equal(0, arrow.Dz);
    }

    [Fact]
    public void Build_ArrowMagnitudeScaledCorrectly()
    {
        var reactions = new[]
        {
            new SgNodeReactionData(nodeId: 1, loadCaseId: 1,
                fx: 100, fy: 0, fz: 0, mx: 0, my: 0, mz: 0)
        };
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(0, 0, 0) };

        // bbox diagonal = 100, max magnitude = 100
        // auto-scale = (0.1 × 100) / 100 = 0.1
        var result = ReactionPreviewBuilder.Build(reactions, nodeMap, 100.0, userScale: null);

        var arrow = result.Arrows[0];
        // Arrow Dx should be magnitude × scale = 100 × 0.1 = 10
        Assert.Equal(10.0, arrow.Dx, 6);
        Assert.Equal(result.ComputedScale, 0.1, 6);
    }

    [Fact]
    public void Build_UserScaleOverride_UsesProvidedScale()
    {
        var reactions = new[]
        {
            new SgNodeReactionData(nodeId: 1, loadCaseId: 1,
                fx: 100, fy: 0, fz: 0, mx: 0, my: 0, mz: 0)
        };
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(0, 0, 0) };

        var result = ReactionPreviewBuilder.Build(reactions, nodeMap, 100.0, userScale: 0.5);

        var arrow = result.Arrows[0];
        // Arrow Dx should be magnitude × userScale = 100 × 0.5 = 50
        Assert.Equal(50.0, arrow.Dx, 6);
        Assert.Equal(0.5, result.ComputedScale, 6);
    }

    [Fact]
    public void Build_UnmatchedNodeId_SkipsReaction()
    {
        var reactions = new[]
        {
            new SgNodeReactionData(nodeId: 99, loadCaseId: 1,
                fx: 100, fy: 0, fz: 0, mx: 0, my: 0, mz: 0)
        };
        // Node 99 not in map
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(0, 0, 0) };

        var result = ReactionPreviewBuilder.Build(reactions, nodeMap, 100.0, userScale: null);

        Assert.Empty(result.Arrows);
    }

    [Fact]
    public void Build_MultipleReactionsAtDifferentNodes_ProducesArrowsForAll()
    {
        var reactions = new[]
        {
            new SgNodeReactionData(nodeId: 1, loadCaseId: 1,
                fx: 10, fy: 0, fz: 0, mx: 0, my: 0, mz: 0),
            new SgNodeReactionData(nodeId: 2, loadCaseId: 1,
                fx: 0, fy: 20, fz: 0, mx: 0, my: 0, mz: 0)
        };
        var nodeMap = new Dictionary<int, SgPoint3D>
        {
            [1] = new(0, 0, 0),
            [2] = new(10, 0, 0)
        };

        var result = ReactionPreviewBuilder.Build(reactions, nodeMap, 100.0, userScale: null);

        Assert.Equal(2, result.Arrows.Count);
        Assert.Contains(result.Arrows, a => a.Origin.X == 0 && a.Axis == 0); // Fx at node 1
        Assert.Contains(result.Arrows, a => a.Origin.X == 10 && a.Axis == 1); // Fy at node 2
    }

    [Fact]
    public void Build_AxisAssignment_ForcesAndMoments()
    {
        var reactions = new[]
        {
            new SgNodeReactionData(nodeId: 1, loadCaseId: 1,
                fx: 1, fy: 2, fz: 3, mx: 4, my: 5, mz: 6)
        };
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(0, 0, 0) };

        var result = ReactionPreviewBuilder.Build(reactions, nodeMap, 100.0, userScale: 1.0);

        // Forces: axis 0=X, 1=Y, 2=Z
        var forces = result.Arrows.Where(a => a.Type == ArrowType.Force).OrderBy(a => a.Axis).ToList();
        Assert.Equal(0, forces[0].Axis); // Fx → X
        Assert.Equal(1, forces[1].Axis); // Fy → Y
        Assert.Equal(2, forces[2].Axis); // Fz → Z

        // Moments: axis 0=X, 1=Y, 2=Z
        var moments = result.Arrows.Where(a => a.Type == ArrowType.Moment).OrderBy(a => a.Axis).ToList();
        Assert.Equal(0, moments[0].Axis); // Mx → X
        Assert.Equal(1, moments[1].Axis); // My → Y
        Assert.Equal(2, moments[2].Axis); // Mz → Z
    }

    [Fact]
    public void Build_MomentArrow_HasCorrectMagnitudeInDirection()
    {
        var reactions = new[]
        {
            new SgNodeReactionData(nodeId: 1, loadCaseId: 1,
                fx: 0, fy: 0, fz: 0, mx: 0, my: 0, mz: 100)
        };
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(0, 0, 0) };

        var result = ReactionPreviewBuilder.Build(reactions, nodeMap, 100.0, userScale: 0.1);

        var arrow = result.Arrows[0];
        Assert.Equal(ArrowType.Moment, arrow.Type);
        Assert.Equal(2, arrow.Axis); // Z
        // Moment magnitude in Dz = 100 × 0.1 = 10
        Assert.Equal(10.0, arrow.Dz, 6);
    }

    // ── BboxDiagonal helper ───────────────────────────────────────────

    [Fact]
    public void ComputeBboxDiagonal_FromNodeMap_ReturnsCorrectDiagonal()
    {
        var nodeMap = new Dictionary<SgPoint3D, int>
        {
            [new SgPoint3D(0, 0, 0)] = 1,
            [new SgPoint3D(10, 0, 0)] = 2,
            [new SgPoint3D(10, 10, 0)] = 3,
            [new SgPoint3D(0, 0, 10)] = 4
        };

        var diagonal = PreviewScaleHelper.ComputeBboxDiagonal(nodeMap.Keys);

        // bbox = (0,0,0) to (10,10,10), diagonal = sqrt(300) ≈ 17.3205
        Assert.Equal(17.3205, diagonal, 2);
    }

    [Fact]
    public void ComputeBboxDiagonal_SingleNode_ReturnsZero()
    {
        var nodeMap = new Dictionary<SgPoint3D, int>
        {
            [new SgPoint3D(5, 5, 5)] = 1
        };

        var diagonal = PreviewScaleHelper.ComputeBboxDiagonal(nodeMap.Keys);

        Assert.Equal(0.0, diagonal);
    }

    [Fact]
    public void ComputeBboxDiagonal_EmptyNodes_ReturnsZero()
    {
        var diagonal = PreviewScaleHelper.ComputeBboxDiagonal(Array.Empty<SgPoint3D>());
        Assert.Equal(0.0, diagonal);
    }
}
