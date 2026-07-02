namespace GhSpaceGass.Core.Models;

/// <summary>Type of preview arrow — force (straight) or moment (arc).</summary>
public enum ArrowType
{
    Force,
    Moment
}

/// <summary>
///     A single preview arrow descriptor — computed in Core, drawn by the GH component.
///     Contains position, scaled direction vector, type, and axis assignment.
/// </summary>
public class PreviewArrow
{
    public PreviewArrow(SgPoint3D origin, double dx, double dy, double dz,
        double magnitude, ArrowType type, int axis)
    {
        Origin = origin;
        Dx = dx;
        Dy = dy;
        Dz = dz;
        Magnitude = magnitude;
        Type = type;
        Axis = axis;
    }

    /// <summary>Arrow origin point (node location).</summary>
    public SgPoint3D Origin { get; }

    /// <summary>Scaled direction X component (magnitude × scale, in arrow direction).</summary>
    public double Dx { get; }

    /// <summary>Scaled direction Y component.</summary>
    public double Dy { get; }

    /// <summary>Scaled direction Z component.</summary>
    public double Dz { get; }

    /// <summary>Raw (unscaled) magnitude of the force or moment component.</summary>
    public double Magnitude { get; }

    /// <summary>Whether this is a force arrow (straight) or moment arc (curved).</summary>
    public ArrowType Type { get; }

    /// <summary>Axis index: 0=X, 1=Y, 2=Z (used for colour assignment).</summary>
    public int Axis { get; }
}

/// <summary>Result of building preview arrows — the arrows plus the computed scale factor.</summary>
public class PreviewArrowResult
{
    public PreviewArrowResult(List<PreviewArrow> arrows, double computedScale)
    {
        Arrows = arrows;
        ComputedScale = computedScale;
    }

    public List<PreviewArrow> Arrows { get; }
    public double ComputedScale { get; }
}

/// <summary>
///     Builds preview arrow descriptors from reaction data.
///     Pure data transformation — no Rhino dependency.
/// </summary>
public static class ReactionPreviewBuilder
{
    /// <summary>
    ///     Build preview arrows from reaction results.
    /// </summary>
    /// <param name="reactions">Reaction results to visualise.</param>
    /// <param name="nodeIdToPoint">Map of SpaceGass node ID → point location.</param>
    /// <param name="bboxDiagonal">Model bounding box diagonal for auto-scale.</param>
    /// <param name="userScale">Optional user-provided scale override. Null = auto-scale.</param>
    /// <returns>List of preview arrows and the computed scale factor.</returns>
    public static PreviewArrowResult Build(
        IReadOnlyList<SgNodeReactionData> reactions,
        IReadOnlyDictionary<int, SgPoint3D> nodeIdToPoint,
        double bboxDiagonal,
        double? userScale)
    {
        if (reactions.Count == 0)
            return new PreviewArrowResult(new List<PreviewArrow>(), 1.0);

        // Find max magnitude across all force/moment components
        var maxMagnitude = 0.0;
        foreach (var r in reactions)
        {
            maxMagnitude = Math.Max(maxMagnitude, Math.Abs(r.Fx));
            maxMagnitude = Math.Max(maxMagnitude, Math.Abs(r.Fy));
            maxMagnitude = Math.Max(maxMagnitude, Math.Abs(r.Fz));
            maxMagnitude = Math.Max(maxMagnitude, Math.Abs(r.Mx));
            maxMagnitude = Math.Max(maxMagnitude, Math.Abs(r.My));
            maxMagnitude = Math.Max(maxMagnitude, Math.Abs(r.Mz));
        }

        var scale = userScale ?? PreviewScaleHelper.ComputeAutoScale(bboxDiagonal, maxMagnitude);
        var arrows = new List<PreviewArrow>();

        foreach (var r in reactions)
        {
            if (!nodeIdToPoint.TryGetValue(r.NodeId, out var origin))
                continue;

            // Force arrows (Fx, Fy, Fz)
            if (r.Fx != 0)
                arrows.Add(new PreviewArrow(origin, r.Fx * scale, 0, 0, r.Fx, ArrowType.Force, 0));
            if (r.Fy != 0)
                arrows.Add(new PreviewArrow(origin, 0, r.Fy * scale, 0, r.Fy, ArrowType.Force, 1));
            if (r.Fz != 0)
                arrows.Add(new PreviewArrow(origin, 0, 0, r.Fz * scale, r.Fz, ArrowType.Force, 2));

            // Moment arcs (Mx, My, Mz)
            if (r.Mx != 0)
                arrows.Add(new PreviewArrow(origin, r.Mx * scale, 0, 0, r.Mx, ArrowType.Moment, 0));
            if (r.My != 0)
                arrows.Add(new PreviewArrow(origin, 0, r.My * scale, 0, r.My, ArrowType.Moment, 1));
            if (r.Mz != 0)
                arrows.Add(new PreviewArrow(origin, 0, 0, r.Mz * scale, r.Mz, ArrowType.Moment, 2));
        }

        return new PreviewArrowResult(arrows, scale);
    }
}
