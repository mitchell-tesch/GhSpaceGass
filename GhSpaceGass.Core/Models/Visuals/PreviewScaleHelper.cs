namespace GhSpaceGass.Core.Models.Visuals;

/// <summary>
///     Shared helper for computing auto-scale factors for results viewport preview (ADR-0009).
///     Reused across all preview-enabled results components.
/// </summary>
public static class PreviewScaleHelper
{
    /// <summary>
    ///     Compute a scale factor so that the largest result arrow/diagram is a reasonable
    ///     proportion of the model bounding box diagonal.
    /// </summary>
    /// <param name="bboxDiagonal">Diagonal length of the model bounding box.</param>
    /// <param name="maxMagnitude">Maximum absolute result magnitude.</param>
    /// <param name="proportion">Fraction of bbox diagonal for the max arrow (default 0.1 = 10%).</param>
    /// <returns>Scale factor. Returns 1.0 for degenerate inputs (zero bbox or zero magnitude).</returns>
    public static double ComputeAutoScale(double bboxDiagonal, double maxMagnitude, double proportion = 0.1)
    {
        if (bboxDiagonal <= 0 || maxMagnitude <= 0)
            return 1.0;

        return proportion * bboxDiagonal / maxMagnitude;
    }

    /// <summary>
    ///     Compute the bounding box diagonal from a set of points.
    /// </summary>
    public static double ComputeBboxDiagonal(IEnumerable<SgPoint3D> points)
    {
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        var any = false;

        foreach (var p in points)
        {
            any = true;
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Z < minZ) minZ = p.Z;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
            if (p.Z > maxZ) maxZ = p.Z;
        }

        if (!any) return 0.0;

        var dx = maxX - minX;
        var dy = maxY - minY;
        var dz = maxZ - minZ;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
