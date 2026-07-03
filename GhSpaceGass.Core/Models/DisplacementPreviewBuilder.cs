namespace GhSpaceGass.Core.Models;

/// <summary>
///     Builds preview arrow descriptors from node displacement data.
///     One combined vector per node (Tx, Ty, Tz). Rotations are not drawn.
///     Pure data transformation — no Rhino dependency.
/// </summary>
public static class DisplacementPreviewBuilder
{
    /// <summary>
    ///     Build preview arrows from displacement results.
    /// </summary>
    /// <param name="displacements">Displacement results to visualise.</param>
    /// <param name="nodeIdToPoint">Map of SpaceGass node ID → point location.</param>
    /// <param name="bboxDiagonal">Model bounding box diagonal for auto-scale.</param>
    /// <param name="userScale">Optional user-provided scale override. Null = auto-scale. Zero/negative = preview disabled.</param>
    public static PreviewArrowResult Build(
        IReadOnlyList<SgNodeDisplacementData> displacements,
        IReadOnlyDictionary<int, SgPoint3D> nodeIdToPoint,
        double bboxDiagonal,
        double? userScale)
    {
        if (displacements.Count == 0 || (userScale.HasValue && userScale.Value <= 0))
            return new PreviewArrowResult(new List<PreviewArrow>(), 1.0, 1.0);

        // Max resultant displacement magnitude for auto-scale
        var maxMag = 0.0;
        foreach (var d in displacements)
        {
            var mag = Math.Sqrt(d.Tx * d.Tx + d.Ty * d.Ty + d.Tz * d.Tz);
            if (mag > maxMag) maxMag = mag;
        }

        var scale = userScale ?? PreviewScaleHelper.ComputeAutoScale(bboxDiagonal, maxMag);
        var arrows = new List<PreviewArrow>();

        foreach (var d in displacements)
        {
            if (!nodeIdToPoint.TryGetValue(d.NodeId, out var origin))
                continue;

            var mag = Math.Sqrt(d.Tx * d.Tx + d.Ty * d.Ty + d.Tz * d.Tz);
            if (mag < 1e-15)
                continue;

            // Axis = -1 signals "use displacement colour" (not per-axis RGB)
            arrows.Add(new PreviewArrow(origin,
                d.Tx * scale, d.Ty * scale, d.Tz * scale,
                mag, ArrowType.Force, axis: -1));
        }

        return new PreviewArrowResult(arrows, scale, scale);
    }
}
