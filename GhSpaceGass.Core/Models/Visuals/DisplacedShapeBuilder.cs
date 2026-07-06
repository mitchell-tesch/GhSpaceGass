namespace GhSpaceGass.Core.Models.Visuals;

/// <summary>A single displaced shape polyline with its max displacement magnitude.</summary>
public class DisplacedMemberPolyline
{
    public DisplacedMemberPolyline(List<SgPoint3D> points, double maxDisplacement)
    {
        Points = points;
        MaxDisplacement = maxDisplacement;
    }

    public List<SgPoint3D> Points { get; }

    /// <summary>Maximum resultant displacement magnitude across all stations in this polyline.</summary>
    public double MaxDisplacement { get; }
}

/// <summary>Result of building displaced shape polylines.</summary>
public class DisplacedShapeResult
{
    public DisplacedShapeResult(List<DisplacedMemberPolyline> polylines, double computedScale)
    {
        Polylines = polylines;
        ComputedScale = computedScale;
    }

    /// <summary>One polyline per (load case, member) pair.</summary>
    public List<DisplacedMemberPolyline> Polylines { get; }

    public double ComputedScale { get; }
}

/// <summary>
///     Builds displaced shape polylines from intermediate member displacement data.
///     Each (load case, member) pair produces a polyline through displaced station positions.
///     Pure data transformation — no Rhino dependency.
/// </summary>
public static class DisplacedShapeBuilder
{
    public static DisplacedShapeResult Build(
        IReadOnlyList<SgMemberDisplacementData> displacements,
        IReadOnlyDictionary<int, (SgPoint3D Start, SgPoint3D End)> memberMap,
        double bboxDiagonal,
        double? userScale)
    {
        if (displacements.Count == 0 || (userScale.HasValue && userScale.Value <= 0))
            return new DisplacedShapeResult(new List<DisplacedMemberPolyline>(), 1.0);

        // Max resultant displacement across all stations
        var maxMag = 0.0;
        foreach (var d in displacements)
        {
            var mag = Math.Sqrt(d.TxGlobal * d.TxGlobal + d.TyGlobal * d.TyGlobal + d.TzGlobal * d.TzGlobal);
            if (mag > maxMag) maxMag = mag;
        }

        var scale = userScale ?? PreviewScaleHelper.ComputeAutoScale(bboxDiagonal, maxMag);
        var polylines = new List<DisplacedMemberPolyline>();

        // Group by (load case, member) to avoid mixing load cases
        var byLcMember = displacements
            .GroupBy(d => (d.LoadCaseId, d.MemberId))
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

            var stations = group.OrderBy(d => d.Location).ToList();

            // Check if all stations have zero global displacement
            var hasNonZero = false;
            var memberMaxMag = 0.0;
            foreach (var s in stations)
            {
                var mag = Math.Sqrt(s.TxGlobal * s.TxGlobal + s.TyGlobal * s.TyGlobal + s.TzGlobal * s.TzGlobal);
                if (mag > memberMaxMag) memberMaxMag = mag;
                if (mag > 1e-15) hasNonZero = true;
            }
            if (!hasNonZero) continue;

            var poly = new List<SgPoint3D>(stations.Count);
            foreach (var s in stations)
            {
                var px = start.X + dirX * s.Location;
                var py = start.Y + dirY * s.Location;
                var pz = start.Z + dirZ * s.Location;

                poly.Add(new SgPoint3D(
                    px + s.TxGlobal * scale,
                    py + s.TyGlobal * scale,
                    pz + s.TzGlobal * scale));
            }

            polylines.Add(new DisplacedMemberPolyline(poly, memberMaxMag));
        }

        return new DisplacedShapeResult(polylines, scale);
    }
}
