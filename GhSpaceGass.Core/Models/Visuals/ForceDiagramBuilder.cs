namespace GhSpaceGass.Core.Models.Visuals;

/// <summary>Identifies which force group a diagram belongs to (for colour assignment).</summary>
public enum DiagramGroup
{
    Axial,   // Fx
    Force,   // Fy, Fz
    Moment,  // My, Mz
    Torsion  // Mx
}

/// <summary>A single force diagram for one component on one (load case, member).</summary>
public class ForceDiagramData
{
    public ForceDiagramData(List<SgPoint3D> diagramPoints, List<SgPoint3D> basePoints,
        double maxAbsValue, DiagramGroup group, int perpAxis,
        List<double> stationValues, List<int> extremaIndices)
    {
        DiagramPoints = diagramPoints;
        BasePoints = basePoints;
        MaxAbsValue = maxAbsValue;
        Group = group;
        PerpAxis = perpAxis;
        StationValues = stationValues;
        ExtremaIndices = extremaIndices;
    }

    /// <summary>Offset points forming the diagram outline.</summary>
    public List<SgPoint3D> DiagramPoints { get; }

    /// <summary>Corresponding points on the member axis (for fill lines).</summary>
    public List<SgPoint3D> BasePoints { get; }

    /// <summary>Maximum absolute value of the plotted component in this diagram.</summary>
    public double MaxAbsValue { get; }

    /// <summary>Which scale group this diagram belongs to (for colour mapping).</summary>
    public DiagramGroup Group { get; }

    /// <summary>Which perpendicular axis: 1 (Fy/Mz/Fx) or 2 (Fz/My/Mx). Used for shade variation.</summary>
    public int PerpAxis { get; }

    /// <summary>Raw (unscaled) force/moment values at each station, aligned with DiagramPoints.</summary>
    public List<double> StationValues { get; }

    /// <summary>Indices into StationValues/DiagramPoints where local extrema occur (peaks and troughs).</summary>
    public List<int> ExtremaIndices { get; }
}

/// <summary>Result of building force diagrams.</summary>
public class ForceDiagramResult
{
    public ForceDiagramResult(List<ForceDiagramData> diagrams,
        double axialScale, double forceScale, double momentScale, double torsionScale)
    {
        Diagrams = diagrams;
        AxialScale = axialScale;
        ForceScale = forceScale;
        MomentScale = momentScale;
        TorsionScale = torsionScale;
    }

    public List<ForceDiagramData> Diagrams { get; }
    public double AxialScale { get; }
    public double ForceScale { get; }
    public double MomentScale { get; }
    public double TorsionScale { get; }
}

/// <summary>
///     Builds force diagram polylines from intermediate member force data.
///     4 scale groups: Axial (Fx), Force (Fy+Fz), Moment (My+Mz), Torsion (Mx).
///     Fy/Mz/Fx on perp1, Fz/My/Mx on perp2.
///     Pure data transformation — no Rhino dependency.
/// </summary>
public static class ForceDiagramBuilder
{
    private static readonly Func<SgMemberIntermediateForceData, double>[] ComponentSelectors =
    {
        f => f.Fx,  // 0
        f => f.Fy,  // 1
        f => f.Fz,  // 2
        f => f.Mx,  // 3
        f => f.My,  // 4
        f => f.Mz   // 5
    };

    // perp assignment: Fx=1, Fy=1, Fz=2, Mx=2, My=2, Mz=1
    private static readonly int[] ComponentPerpAxis = { 1, 1, 2, 2, 2, 1 };

    // group assignment per component index
    private static readonly DiagramGroup[] ComponentGroup =
    {
        DiagramGroup.Axial, DiagramGroup.Force, DiagramGroup.Force,
        DiagramGroup.Torsion, DiagramGroup.Moment, DiagramGroup.Moment
    };

    /// <summary>
    ///     Build diagrams for a single selected force component.
    /// </summary>
    public static ForceDiagramResult BuildSingleComponent(
        IReadOnlyList<SgMemberIntermediateForceData> forces,
        IReadOnlyDictionary<int, (SgPoint3D Start, SgPoint3D End)> memberMap,
        double bboxDiagonal,
        int componentIndex,
        double? userScale)
    {
        var empty = new ForceDiagramResult(new List<ForceDiagramData>(), 1, 1, 1, 1);
        if (forces.Count == 0 || componentIndex < 0 || componentIndex >= ComponentSelectors.Length)
            return empty;
        if (userScale.HasValue && userScale.Value <= 0)
            return empty;

        var selector = ComponentSelectors[componentIndex];
        var perpIdx = ComponentPerpAxis[componentIndex];
        var group = ComponentGroup[componentIndex];

        // Compute max magnitude for auto-scale
        var maxMag = 0.0;
        foreach (var f in forces)
            maxMag = Math.Max(maxMag, Math.Abs(selector(f)));

        var scale = userScale ?? PreviewScaleHelper.ComputeAutoScale(bboxDiagonal, maxMag);
        var diagrams = new List<ForceDiagramData>();

        var byLcMember = forces
            .GroupBy(f => (f.LoadCaseId, f.MemberId))
            .OrderBy(g => g.Key.LoadCaseId)
            .ThenBy(g => g.Key.MemberId);

        foreach (var grp in byLcMember)
        {
            if (!memberMap.TryGetValue(grp.Key.MemberId, out var endpoints))
                continue;

            var start = endpoints.Start;
            var end = endpoints.End;
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var dz = end.Z - start.Z;
            var memberLength = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (memberLength < 1e-15) continue;

            var dirX = dx / memberLength;
            var dirY = dy / memberLength;
            var dirZ = dz / memberLength;

            var (p1x, p1y, p1z) = ComputePerp1(dirX, dirY, dirZ);
            var (p2x, p2y, p2z) = CrossNormalize(dirX, dirY, dirZ, p1x, p1y, p1z);

            var perpX = perpIdx == 1 ? p1x : p2x;
            var perpY = perpIdx == 1 ? p1y : p2y;
            var perpZ = perpIdx == 1 ? p1z : p2z;

            var stations = grp.OrderBy(f => f.Location).ToList();
            TryAddDiagram(diagrams, stations, start, dirX, dirY, dirZ,
                perpX, perpY, perpZ, scale, selector, group, perpIdx);
        }

        return new ForceDiagramResult(diagrams, scale, scale, scale, scale);
    }

    public static ForceDiagramResult Build(
        IReadOnlyList<SgMemberIntermediateForceData> forces,
        IReadOnlyDictionary<int, (SgPoint3D Start, SgPoint3D End)> memberMap,
        double bboxDiagonal,
        double? axialScale, double? forceScale, double? momentScale, double? torsionScale)
    {
        var empty = new ForceDiagramResult(new List<ForceDiagramData>(), 1, 1, 1, 1);
        if (forces.Count == 0) return empty;

        // Check if all 4 groups are explicitly disabled
        if (axialScale is <= 0 && forceScale is <= 0 && momentScale is <= 0 && torsionScale is <= 0)
            return empty;

        // Compute max magnitudes per group for auto-scale
        double maxAxial = 0, maxForce = 0, maxMoment = 0, maxTorsion = 0;
        foreach (var f in forces)
        {
            maxAxial = Math.Max(maxAxial, Math.Abs(f.Fx));
            maxForce = Math.Max(maxForce, Math.Max(Math.Abs(f.Fy), Math.Abs(f.Fz)));
            maxMoment = Math.Max(maxMoment, Math.Max(Math.Abs(f.My), Math.Abs(f.Mz)));
            maxTorsion = Math.Max(maxTorsion, Math.Abs(f.Mx));
        }

        var aScale = ResolveScale(axialScale, bboxDiagonal, maxAxial);
        var fScale = ResolveScale(forceScale, bboxDiagonal, maxForce);
        var mScale = ResolveScale(momentScale, bboxDiagonal, maxMoment);
        var tScale = ResolveScale(torsionScale, bboxDiagonal, maxTorsion);

        var diagrams = new List<ForceDiagramData>();

        var byLcMember = forces
            .GroupBy(f => (f.LoadCaseId, f.MemberId))
            .OrderBy(g => g.Key.LoadCaseId)
            .ThenBy(g => g.Key.MemberId);

        foreach (var group in byLcMember)
        {
            if (!memberMap.TryGetValue(group.Key.MemberId, out var endpoints))
                continue;

            var start = endpoints.Start;
            var end = endpoints.End;
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var dz = end.Z - start.Z;
            var memberLength = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (memberLength < 1e-15) continue;

            var dirX = dx / memberLength;
            var dirY = dy / memberLength;
            var dirZ = dz / memberLength;

            // Compute two orthogonal perpendiculars
            var (p1x, p1y, p1z) = ComputePerp1(dirX, dirY, dirZ);
            var (p2x, p2y, p2z) = CrossNormalize(dirX, dirY, dirZ, p1x, p1y, p1z);

            var stations = group.OrderBy(f => f.Location).ToList();

            // Build diagrams for enabled groups
            // perp1: Fy, Mz, Fx  |  perp2: Fz, My, Mx
            if (fScale > 0)
            {
                TryAddDiagram(diagrams, stations, start, dirX, dirY, dirZ,
                    p1x, p1y, p1z, fScale, f => f.Fy, DiagramGroup.Force, 1);
                TryAddDiagram(diagrams, stations, start, dirX, dirY, dirZ,
                    p2x, p2y, p2z, fScale, f => f.Fz, DiagramGroup.Force, 2);
            }
            if (mScale > 0)
            {
                TryAddDiagram(diagrams, stations, start, dirX, dirY, dirZ,
                    p1x, p1y, p1z, mScale, f => f.Mz, DiagramGroup.Moment, 1);
                TryAddDiagram(diagrams, stations, start, dirX, dirY, dirZ,
                    p2x, p2y, p2z, mScale, f => f.My, DiagramGroup.Moment, 2);
            }
            if (aScale > 0)
            {
                TryAddDiagram(diagrams, stations, start, dirX, dirY, dirZ,
                    p1x, p1y, p1z, aScale, f => f.Fx, DiagramGroup.Axial, 1);
            }
            if (tScale > 0)
            {
                TryAddDiagram(diagrams, stations, start, dirX, dirY, dirZ,
                    p2x, p2y, p2z, tScale, f => f.Mx, DiagramGroup.Torsion, 2);
            }
        }

        return new ForceDiagramResult(diagrams, aScale, fScale, mScale, tScale);
    }

    private static double ResolveScale(double? userScale, double bboxDiagonal, double maxMag)
    {
        if (userScale.HasValue) return Math.Max(0, userScale.Value);
        return PreviewScaleHelper.ComputeAutoScale(bboxDiagonal, maxMag);
    }

    private static void TryAddDiagram(
        List<ForceDiagramData> diagrams,
        List<SgMemberIntermediateForceData> stations,
        SgPoint3D start, double dirX, double dirY, double dirZ,
        double perpX, double perpY, double perpZ,
        double scale,
        Func<SgMemberIntermediateForceData, double> selector,
        DiagramGroup group, int perpAxis)
    {
        var maxAbs = 0.0;
        foreach (var s in stations)
            maxAbs = Math.Max(maxAbs, Math.Abs(selector(s)));
        if (maxAbs < 1e-15) return;

        var basePoints = new List<SgPoint3D>(stations.Count);
        var diagPoints = new List<SgPoint3D>(stations.Count);
        var values = new List<double>(stations.Count);

        foreach (var s in stations)
        {
            var px = start.X + dirX * s.Location;
            var py = start.Y + dirY * s.Location;
            var pz = start.Z + dirZ * s.Location;
            basePoints.Add(new SgPoint3D(px, py, pz));

            var rawVal = selector(s);
            values.Add(rawVal);

            var val = rawVal * scale;
            diagPoints.Add(new SgPoint3D(
                px + perpX * val,
                py + perpY * val,
                pz + perpZ * val));
        }

        var extrema = FindExtremaIndices(values);
        diagrams.Add(new ForceDiagramData(diagPoints, basePoints, maxAbs, group, perpAxis, values, extrema));
    }

    /// <summary>Find indices of local extrema (peaks and troughs) including endpoints if non-zero.</summary>
    internal static List<int> FindExtremaIndices(List<double> values)
    {
        var indices = new List<int>();
        if (values.Count == 0) return indices;

        // First point if non-zero
        if (Math.Abs(values[0]) > 1e-15)
            indices.Add(0);

        // Interior points: sign change or direction change
        for (var i = 1; i < values.Count - 1; i++)
        {
            var prev = values[i - 1];
            var curr = values[i];
            var next = values[i + 1];

            // Local max: curr >= both neighbours and not all equal
            // Local min: curr <= both neighbours and not all equal
            if ((curr >= prev && curr >= next && (curr > prev || curr > next)) ||
                (curr <= prev && curr <= next && (curr < prev || curr < next)))
            {
                if (Math.Abs(curr) > 1e-15)
                    indices.Add(i);
            }
        }

        // Last point if non-zero
        if (values.Count > 1 && Math.Abs(values[^1]) > 1e-15)
            indices.Add(values.Count - 1);

        return indices;
    }

    /// <summary>Compute first perpendicular via cross product with a reference axis.</summary>
    private static (double X, double Y, double Z) ComputePerp1(double dx, double dy, double dz)
    {
        // If member is roughly vertical (parallel to Z), use X as reference
        double rx, ry, rz;
        if (Math.Abs(dz) > 0.9)
            (rx, ry, rz) = (1, 0, 0);
        else
            (rx, ry, rz) = (0, 0, 1);

        return CrossNormalize(dx, dy, dz, rx, ry, rz);
    }

    private static (double X, double Y, double Z) CrossNormalize(
        double ax, double ay, double az,
        double bx, double by, double bz)
    {
        var cx = ay * bz - az * by;
        var cy = az * bx - ax * bz;
        var cz = ax * by - ay * bx;
        var len = Math.Sqrt(cx * cx + cy * cy + cz * cz);
        if (len < 1e-15) return (0, 1, 0); // fallback
        return (cx / len, cy / len, cz / len);
    }
}
