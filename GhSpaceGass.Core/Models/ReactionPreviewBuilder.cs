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

/// <summary>Result of building preview arrows — the arrows plus the computed scale factors.</summary>
public class PreviewArrowResult
{
    public PreviewArrowResult(List<PreviewArrow> arrows, double forceScale, double momentScale)
    {
        Arrows = arrows;
        ForceScale = forceScale;
        MomentScale = momentScale;
    }

    public List<PreviewArrow> Arrows { get; }

    /// <summary>Scale factor applied to force arrows (auto or user-provided).</summary>
    public double ForceScale { get; }

    /// <summary>Scale factor applied to moment arcs (auto or user-provided).</summary>
    public double MomentScale { get; }

    /// <summary>Convenience: the force scale (for backward-compatible test access).</summary>
    public double ComputedScale => ForceScale;
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
    /// <param name="userScale">Optional user-provided scale override. Null = auto-scale. Zero = preview disabled.</param>
    /// <returns>List of preview arrows and the computed scale factors.</returns>
    public static PreviewArrowResult Build(
        IReadOnlyList<SgNodeReactionData> reactions,
        IReadOnlyDictionary<int, SgPoint3D> nodeIdToPoint,
        double bboxDiagonal,
        double? userScale)
    {
        if (reactions.Count == 0 || (userScale.HasValue && userScale.Value <= 0))
            return new PreviewArrowResult(new List<PreviewArrow>(), 1.0, 1.0);

        // Compute separate max magnitudes for forces and moments (different units)
        var maxForceMag = 0.0;
        var maxMomentMag = 0.0;
        foreach (var r in reactions)
        {
            maxForceMag = Math.Max(maxForceMag, Math.Abs(r.Fx));
            maxForceMag = Math.Max(maxForceMag, Math.Abs(r.Fy));
            maxForceMag = Math.Max(maxForceMag, Math.Abs(r.Fz));
            maxMomentMag = Math.Max(maxMomentMag, Math.Abs(r.Mx));
            maxMomentMag = Math.Max(maxMomentMag, Math.Abs(r.My));
            maxMomentMag = Math.Max(maxMomentMag, Math.Abs(r.Mz));
        }

        // When user provides a scale, apply uniformly; otherwise auto-scale separately
        var forceScale = userScale ?? PreviewScaleHelper.ComputeAutoScale(bboxDiagonal, maxForceMag);
        var momentScale = userScale ?? PreviewScaleHelper.ComputeAutoScale(bboxDiagonal, maxMomentMag);
        var arrows = new List<PreviewArrow>();

        foreach (var r in reactions)
        {
            if (!nodeIdToPoint.TryGetValue(r.NodeId, out var origin))
                continue;

            // Force arrows (Fx, Fy, Fz)
            if (r.Fx != 0)
                arrows.Add(new PreviewArrow(origin, r.Fx * forceScale, 0, 0, r.Fx, ArrowType.Force, 0));
            if (r.Fy != 0)
                arrows.Add(new PreviewArrow(origin, 0, r.Fy * forceScale, 0, r.Fy, ArrowType.Force, 1));
            if (r.Fz != 0)
                arrows.Add(new PreviewArrow(origin, 0, 0, r.Fz * forceScale, r.Fz, ArrowType.Force, 2));

            // Moment arcs (Mx, My, Mz)
            if (r.Mx != 0)
                arrows.Add(new PreviewArrow(origin, r.Mx * momentScale, 0, 0, r.Mx, ArrowType.Moment, 0));
            if (r.My != 0)
                arrows.Add(new PreviewArrow(origin, 0, r.My * momentScale, 0, r.My, ArrowType.Moment, 1));
            if (r.Mz != 0)
                arrows.Add(new PreviewArrow(origin, 0, 0, r.Mz * momentScale, r.Mz, ArrowType.Moment, 2));
        }

        return new PreviewArrowResult(arrows, forceScale, momentScale);
    }
}
