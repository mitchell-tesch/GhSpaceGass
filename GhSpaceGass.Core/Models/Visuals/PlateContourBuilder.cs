namespace GhSpaceGass.Core.Models.Visuals;

/// <summary>A single plate contour entry — value and geometry for one plate element.</summary>
public class PlateContourData
{
    public PlateContourData(SgPoint3D[] cornerPoints, double value, double normalisedValue, SgPoint3D centroid)
    {
        CornerPoints = cornerPoints;
        Value = value;
        NormalisedValue = normalisedValue;
        Centroid = centroid;
    }

    /// <summary>Corner points of the plate (3 for tri, 4 for quad).</summary>
    public SgPoint3D[] CornerPoints { get; }

    /// <summary>Raw force/moment value for the selected component.</summary>
    public double Value { get; }

    /// <summary>Value normalised to [-1..1] range based on max absolute across all plates.</summary>
    public double NormalisedValue { get; }

    /// <summary>Centroid of the plate (average of corner points).</summary>
    public SgPoint3D Centroid { get; }
}

/// <summary>Result of building plate contour data.</summary>
public class PlateContourResult
{
    public PlateContourResult(List<PlateContourData> contours)
    {
        Contours = contours;
    }

    public List<PlateContourData> Contours { get; }
}

/// <summary>
///     Builds plate contour data from plate element force results.
///     Extracts a selected force component, computes normalised values for colour mapping,
///     and calculates plate centroids for value labels.
///     Pure data transformation — no Rhino dependency.
/// </summary>
public static class PlateContourBuilder
{
    private static readonly Func<SgPlateElementForceData, double>[] Selectors =
    {
        f => f.Fx,     // 0
        f => f.Fy,     // 1
        f => f.Fxy,    // 2
        f => f.Mx,     // 3
        f => f.My,     // 4
        f => f.Mxy,    // 5
        f => f.Vxz,    // 6
        f => f.Vyz,    // 7
        f => f.MxTop,  // 8
        f => f.MxBtm,  // 9
        f => f.MyTop,  // 10
        f => f.MyBtm   // 11
    };

    public static PlateContourResult Build(
        IReadOnlyList<SgPlateElementForceData> forces,
        IReadOnlyDictionary<int, SgPoint3D[]> plateMap,
        int componentIndex)
    {
        if (forces.Count == 0 || componentIndex < 0 || componentIndex >= Selectors.Length)
            return new PlateContourResult(new List<PlateContourData>());

        var selector = Selectors[componentIndex];

        // Extract values for plates that exist in the map
        var entries = new List<(SgPlateElementForceData Force, SgPoint3D[] Corners, double Value)>();
        foreach (var f in forces)
        {
            if (!plateMap.TryGetValue(f.PlateId, out var corners))
                continue;
            entries.Add((f, corners, selector(f)));
        }

        if (entries.Count == 0)
            return new PlateContourResult(new List<PlateContourData>());

        // Find max absolute value for normalisation
        var maxAbs = 0.0;
        foreach (var (_, _, val) in entries)
            maxAbs = Math.Max(maxAbs, Math.Abs(val));

        if (maxAbs < 1e-15)
            return new PlateContourResult(new List<PlateContourData>());

        var contours = new List<PlateContourData>(entries.Count);
        foreach (var (_, corners, val) in entries)
        {
            var normalised = val / maxAbs;
            var centroid = ComputeCentroid(corners);
            contours.Add(new PlateContourData(corners, val, normalised, centroid));
        }

        return new PlateContourResult(contours);
    }

    private static SgPoint3D ComputeCentroid(SgPoint3D[] corners)
    {
        double x = 0, y = 0, z = 0;
        foreach (var c in corners)
        {
            x += c.X;
            y += c.Y;
            z += c.Z;
        }
        var n = corners.Length;
        return new SgPoint3D(x / n, y / n, z / n);
    }
}
