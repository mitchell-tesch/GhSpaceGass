namespace GhSpaceGass.Core.Models;

/// <summary>
///     Lightweight 3D point for use in Core (no RhinoCommon dependency).
///     Converted to/from Rhino.Geometry.Point3d at the Grasshopper boundary.
/// </summary>
public readonly record struct SgPoint3D(double X, double Y, double Z)
{
    /// <summary>
    ///     Returns true if this point is within <paramref name="tolerance" /> of <paramref name="other" />.
    /// </summary>
    public bool IsCoincident(SgPoint3D other, double tolerance)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return dx * dx + dy * dy + dz * dz <= tolerance * tolerance;
    }
}