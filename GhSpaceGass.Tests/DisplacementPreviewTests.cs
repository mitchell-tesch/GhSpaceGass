using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Models.Visuals;
using Xunit;

namespace GhSpaceGass.Tests;

public class DisplacementPreviewTests
{
    // ── Empty / disabled ──────────────────────────────────────────────

    [Fact]
    public void Build_EmptyDisplacements_ReturnsNoArrows()
    {
        var result = DisplacementPreviewBuilder.Build(
            Array.Empty<SgNodeDisplacementData>(),
            new Dictionary<int, SgPoint3D>(),
            100.0,
            userScale: null);

        Assert.Empty(result.Arrows);
    }

    [Fact]
    public void Build_ZeroScale_DisablesPreview()
    {
        var displacements = new[]
        {
            new SgNodeDisplacementData(nodeId: 1, loadCaseId: 1,
                tx: 5, ty: 10, tz: 0, rx: 0, ry: 0, rz: 0)
        };
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(0, 0, 0) };

        var result = DisplacementPreviewBuilder.Build(displacements, nodeMap, 100.0, userScale: 0.0);

        Assert.Empty(result.Arrows);
    }

    [Fact]
    public void Build_NegativeScale_DisablesPreview()
    {
        var displacements = new[]
        {
            new SgNodeDisplacementData(nodeId: 1, loadCaseId: 1,
                tx: 5, ty: 10, tz: 0, rx: 0, ry: 0, rz: 0)
        };
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(0, 0, 0) };

        var result = DisplacementPreviewBuilder.Build(displacements, nodeMap, 100.0, userScale: -1.0);

        Assert.Empty(result.Arrows);
    }

    // ── Arrow generation ──────────────────────────────────────────────

    [Fact]
    public void Build_SingleDisplacement_ProducesOneArrow()
    {
        var displacements = new[]
        {
            new SgNodeDisplacementData(nodeId: 1, loadCaseId: 1,
                tx: 5, ty: 10, tz: 0, rx: 1, ry: 2, rz: 3)
        };
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(0, 0, 0) };

        var result = DisplacementPreviewBuilder.Build(displacements, nodeMap, 100.0, userScale: null);

        // One combined vector per node (not per-axis, not for rotations)
        Assert.Single(result.Arrows);
        Assert.Equal(ArrowType.Force, result.Arrows[0].Type);
    }

    [Fact]
    public void Build_ZeroTranslation_Skipped()
    {
        var displacements = new[]
        {
            new SgNodeDisplacementData(nodeId: 1, loadCaseId: 1,
                tx: 0, ty: 0, tz: 0, rx: 1, ry: 2, rz: 3)
        };
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(0, 0, 0) };

        var result = DisplacementPreviewBuilder.Build(displacements, nodeMap, 100.0, userScale: null);

        Assert.Empty(result.Arrows);
    }

    [Fact]
    public void Build_CombinedVectorDirection_IsCorrect()
    {
        var displacements = new[]
        {
            new SgNodeDisplacementData(nodeId: 1, loadCaseId: 1,
                tx: 3, ty: 4, tz: 0, rx: 0, ry: 0, rz: 0)
        };
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(10, 0, 0) };

        // magnitude = sqrt(9+16) = 5
        // auto-scale = (0.1 × 100) / 5 = 2.0
        var result = DisplacementPreviewBuilder.Build(displacements, nodeMap, 100.0, userScale: null);

        var arrow = result.Arrows[0];
        Assert.Equal(10.0, arrow.Origin.X);
        // Dx = tx × scale = 3 × 2 = 6
        Assert.Equal(6.0, arrow.Dx, 6);
        // Dy = ty × scale = 4 × 2 = 8
        Assert.Equal(8.0, arrow.Dy, 6);
        Assert.Equal(0.0, arrow.Dz, 6);
    }

    [Fact]
    public void Build_MagnitudeIsResultant()
    {
        var displacements = new[]
        {
            new SgNodeDisplacementData(nodeId: 1, loadCaseId: 1,
                tx: 3, ty: 4, tz: 0, rx: 0, ry: 0, rz: 0)
        };
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(0, 0, 0) };

        var result = DisplacementPreviewBuilder.Build(displacements, nodeMap, 100.0, userScale: null);

        // Magnitude should be resultant = sqrt(9+16) = 5
        Assert.Equal(5.0, result.Arrows[0].Magnitude, 6);
    }

    [Fact]
    public void Build_AutoScale_UsesResultantMagnitude()
    {
        var displacements = new[]
        {
            new SgNodeDisplacementData(nodeId: 1, loadCaseId: 1,
                tx: 3, ty: 4, tz: 0, rx: 0, ry: 0, rz: 0),
            new SgNodeDisplacementData(nodeId: 2, loadCaseId: 1,
                tx: 6, ty: 8, tz: 0, rx: 0, ry: 0, rz: 0)
        };
        var nodeMap = new Dictionary<int, SgPoint3D>
        {
            [1] = new(0, 0, 0),
            [2] = new(10, 0, 0)
        };

        // Max resultant = sqrt(36+64) = 10
        // auto-scale = (0.1 × 100) / 10 = 1.0
        var result = DisplacementPreviewBuilder.Build(displacements, nodeMap, 100.0, userScale: null);

        Assert.Equal(1.0, result.ForceScale, 6);
    }

    [Fact]
    public void Build_UserScaleOverride()
    {
        var displacements = new[]
        {
            new SgNodeDisplacementData(nodeId: 1, loadCaseId: 1,
                tx: 10, ty: 0, tz: 0, rx: 0, ry: 0, rz: 0)
        };
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(0, 0, 0) };

        var result = DisplacementPreviewBuilder.Build(displacements, nodeMap, 100.0, userScale: 3.0);

        // Dx = 10 × 3.0 = 30
        Assert.Equal(30.0, result.Arrows[0].Dx, 6);
        Assert.Equal(3.0, result.ForceScale, 6);
    }

    [Fact]
    public void Build_UnmatchedNodeId_Skipped()
    {
        var displacements = new[]
        {
            new SgNodeDisplacementData(nodeId: 99, loadCaseId: 1,
                tx: 5, ty: 0, tz: 0, rx: 0, ry: 0, rz: 0)
        };
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(0, 0, 0) };

        var result = DisplacementPreviewBuilder.Build(displacements, nodeMap, 100.0, userScale: null);

        Assert.Empty(result.Arrows);
    }

    [Fact]
    public void Build_MultipleNodes_ProducesArrowForEach()
    {
        var displacements = new[]
        {
            new SgNodeDisplacementData(nodeId: 1, loadCaseId: 1,
                tx: 5, ty: 0, tz: 0, rx: 0, ry: 0, rz: 0),
            new SgNodeDisplacementData(nodeId: 2, loadCaseId: 1,
                tx: 0, ty: 3, tz: 0, rx: 0, ry: 0, rz: 0),
            new SgNodeDisplacementData(nodeId: 3, loadCaseId: 1,
                tx: 0, ty: 0, tz: 0, rx: 1, ry: 0, rz: 0)
        };
        var nodeMap = new Dictionary<int, SgPoint3D>
        {
            [1] = new(0, 0, 0),
            [2] = new(5, 0, 0),
            [3] = new(10, 0, 0)
        };

        var result = DisplacementPreviewBuilder.Build(displacements, nodeMap, 100.0, userScale: null);

        // Node 1 and 2 have non-zero translations, node 3 has zero translations (only rotation)
        Assert.Equal(2, result.Arrows.Count);
    }

    [Fact]
    public void Build_AxisIsNegativeOne_ForDisplacementColour()
    {
        var displacements = new[]
        {
            new SgNodeDisplacementData(nodeId: 1, loadCaseId: 1,
                tx: 5, ty: 0, tz: 0, rx: 0, ry: 0, rz: 0)
        };
        var nodeMap = new Dictionary<int, SgPoint3D> { [1] = new(0, 0, 0) };

        var result = DisplacementPreviewBuilder.Build(displacements, nodeMap, 100.0, userScale: null);

        // Axis = -1 signals displacement colour (magenta), not per-axis RGB
        Assert.Equal(-1, result.Arrows[0].Axis);
    }
}
