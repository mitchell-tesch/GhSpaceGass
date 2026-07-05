using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Models.Visuals;
using Xunit;

namespace GhSpaceGass.Tests;

public class PlateContourPreviewTests
{
    private static Dictionary<int, SgPoint3D[]> QuadPlateMap() => new()
    {
        [1] = new[] { new SgPoint3D(0, 0, 0), new SgPoint3D(1, 0, 0), new SgPoint3D(1, 1, 0), new SgPoint3D(0, 1, 0) },
        [2] = new[] { new SgPoint3D(1, 0, 0), new SgPoint3D(2, 0, 0), new SgPoint3D(2, 1, 0), new SgPoint3D(1, 1, 0) }
    };

    private static Dictionary<int, SgPoint3D[]> TriPlateMap() => new()
    {
        [1] = new[] { new SgPoint3D(0, 0, 0), new SgPoint3D(1, 0, 0), new SgPoint3D(0.5, 1, 0) }
    };

    // ── Empty ─────────────────────────────────────────────────────────

    [Fact]
    public void Build_EmptyForces_ReturnsNoContours()
    {
        var result = PlateContourBuilder.Build(
            Array.Empty<SgPlateElementForceData>(),
            new Dictionary<int, SgPoint3D[]>(),
            componentIndex: 0);

        Assert.Empty(result.Contours);
    }

    // ── Component selection ───────────────────────────────────────────

    [Fact]
    public void Build_ComponentIndex0_ExtractsFx()
    {
        var forces = new[]
        {
            new SgPlateElementForceData(1, 1, fx: 100, fy: 200)
        };
        var result = PlateContourBuilder.Build(forces, QuadPlateMap(), componentIndex: 0);

        Assert.Single(result.Contours);
        Assert.Equal(100.0, result.Contours[0].Value, 6);
    }

    [Fact]
    public void Build_ComponentIndex1_ExtractsFy()
    {
        var forces = new[]
        {
            new SgPlateElementForceData(1, 1, fx: 100, fy: 200)
        };
        var result = PlateContourBuilder.Build(forces, QuadPlateMap(), componentIndex: 1);

        Assert.Single(result.Contours);
        Assert.Equal(200.0, result.Contours[0].Value, 6);
    }

    [Fact]
    public void Build_ComponentIndex3_ExtractsMx()
    {
        var forces = new[]
        {
            new SgPlateElementForceData(1, 1, mx: 50)
        };
        var result = PlateContourBuilder.Build(forces, QuadPlateMap(), componentIndex: 3);

        Assert.Single(result.Contours);
        Assert.Equal(50.0, result.Contours[0].Value, 6);
    }

    // ── Normalised value ──────────────────────────────────────────────

    [Fact]
    public void Build_NormalisedValue_MapsToMinusOneToOne()
    {
        var forces = new[]
        {
            new SgPlateElementForceData(1, 1, fx: -10),
            new SgPlateElementForceData(2, 1, fx: 20)
        };
        var result = PlateContourBuilder.Build(forces, QuadPlateMap(), componentIndex: 0);

        // Range: -10 to 20. Plate 1: -10 → -10/20 = -0.5, Plate 2: 20 → 20/20 = 1.0
        var p1 = result.Contours.First(c => c.Value == -10.0);
        var p2 = result.Contours.First(c => c.Value == 20.0);
        Assert.Equal(-0.5, p1.NormalisedValue, 6);
        Assert.Equal(1.0, p2.NormalisedValue, 6);
    }

    [Fact]
    public void Build_AllZeroValues_ReturnsNoContours()
    {
        var forces = new[]
        {
            new SgPlateElementForceData(1, 1, fx: 0),
            new SgPlateElementForceData(2, 1, fx: 0)
        };
        var result = PlateContourBuilder.Build(forces, QuadPlateMap(), componentIndex: 0);

        Assert.Empty(result.Contours);
    }

    [Fact]
    public void Build_AllPositive_NormalisedZeroToOne()
    {
        var forces = new[]
        {
            new SgPlateElementForceData(1, 1, fx: 5),
            new SgPlateElementForceData(2, 1, fx: 10)
        };
        var result = PlateContourBuilder.Build(forces, QuadPlateMap(), componentIndex: 0);

        // maxAbs = 10. Plate 1: 5/10 = 0.5, Plate 2: 10/10 = 1.0
        var p1 = result.Contours.First(c => c.Value == 5.0);
        var p2 = result.Contours.First(c => c.Value == 10.0);
        Assert.Equal(0.5, p1.NormalisedValue, 6);
        Assert.Equal(1.0, p2.NormalisedValue, 6);
    }

    // ── Centroid ──────────────────────────────────────────────────────

    [Fact]
    public void Build_QuadCentroid_AverageOfCorners()
    {
        var forces = new[]
        {
            new SgPlateElementForceData(1, 1, fx: 10)
        };
        var result = PlateContourBuilder.Build(forces, QuadPlateMap(), componentIndex: 0);

        // Quad corners: (0,0), (1,0), (1,1), (0,1) → centroid (0.5, 0.5, 0)
        Assert.Equal(0.5, result.Contours[0].Centroid.X, 6);
        Assert.Equal(0.5, result.Contours[0].Centroid.Y, 6);
        Assert.Equal(0.0, result.Contours[0].Centroid.Z, 6);
    }

    [Fact]
    public void Build_TriCentroid_AverageOfCorners()
    {
        var forces = new[]
        {
            new SgPlateElementForceData(1, 1, fx: 10)
        };
        var result = PlateContourBuilder.Build(forces, TriPlateMap(), componentIndex: 0);

        // Tri corners: (0,0), (1,0), (0.5,1) → centroid (0.5, 0.333, 0)
        Assert.Equal(0.5, result.Contours[0].Centroid.X, 6);
        Assert.Equal(1.0 / 3.0, result.Contours[0].Centroid.Y, 4);
    }

    // ── Unmatched plate ───────────────────────────────────────────────

    [Fact]
    public void Build_UnmatchedPlateId_Skipped()
    {
        var forces = new[]
        {
            new SgPlateElementForceData(99, 1, fx: 10)
        };
        var result = PlateContourBuilder.Build(forces, QuadPlateMap(), componentIndex: 0);

        Assert.Empty(result.Contours);
    }

    // ── Corner points ─────────────────────────────────────────────────

    [Fact]
    public void Build_CornerPoints_FromPlateMap()
    {
        var forces = new[]
        {
            new SgPlateElementForceData(1, 1, fx: 10)
        };
        var result = PlateContourBuilder.Build(forces, QuadPlateMap(), componentIndex: 0);

        Assert.Equal(4, result.Contours[0].CornerPoints.Length);
        Assert.Equal(0.0, result.Contours[0].CornerPoints[0].X);
        Assert.Equal(1.0, result.Contours[0].CornerPoints[1].X);
    }

    // ── Multi-load-case ───────────────────────────────────────────────

    [Fact]
    public void Build_MultiLoadCase_AllIncluded()
    {
        var forces = new[]
        {
            new SgPlateElementForceData(1, 1, fx: 10),
            new SgPlateElementForceData(1, 2, fx: 20)
        };
        var result = PlateContourBuilder.Build(forces, QuadPlateMap(), componentIndex: 0);

        Assert.Equal(2, result.Contours.Count);
    }

    // ── All 12 component indices ──────────────────────────────────────

    [Theory]
    [InlineData(0, 1)]   // Fx
    [InlineData(1, 2)]   // Fy
    [InlineData(2, 3)]   // Fxy
    [InlineData(3, 4)]   // Mx
    [InlineData(4, 5)]   // My
    [InlineData(5, 6)]   // Mxy
    [InlineData(6, 7)]   // Vxz
    [InlineData(7, 8)]   // Vyz
    [InlineData(8, 9)]   // MxTop
    [InlineData(9, 10)]  // MxBtm
    [InlineData(10, 11)] // MyTop
    [InlineData(11, 12)] // MyBtm
    public void Build_AllComponentIndices_ExtractCorrectValue(int componentIndex, double expected)
    {
        // Each property gets a unique value: Fx=1, Fy=2, ... MyBtm=12
        var forces = new[]
        {
            new SgPlateElementForceData(1, 1,
                fx: 1, fy: 2, fxy: 3, mx: 4, my: 5, mxy: 6,
                vxz: 7, vyz: 8, mxTop: 9, mxBtm: 10, myTop: 11, myBtm: 12)
        };
        var result = PlateContourBuilder.Build(forces, QuadPlateMap(), componentIndex);

        Assert.Single(result.Contours);
        Assert.Equal(expected, result.Contours[0].Value, 6);
    }
}
