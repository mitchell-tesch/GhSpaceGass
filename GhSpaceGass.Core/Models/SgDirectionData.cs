using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a member direction/orientation. No API call — pure data.
///     Defines how the member's local axes are oriented via one of three methods:
///     angle (rotation about longitudinal axis), axis (align with global axis),
///     or node (orient toward a point that resolves to a node ID during assembly).
///     Priority: Node > Axis > Angle (ADR-0010).
/// </summary>
public class SgDirectionData
{
    private SgDirectionData(double angle, DirectionAxis axis, SgPoint3D? nodePoint)
    {
        Angle = angle;
        Axis = axis;
        NodePoint = nodePoint;
    }

    /// <summary>Rotation angle in degrees about the member's longitudinal axis. Default: 0.</summary>
    public double Angle { get; }

    /// <summary>Global axis alignment. NotApplicable when using Angle or Node modes.</summary>
    public DirectionAxis Axis { get; }

    /// <summary>
    ///     Direction node point. When set, the member orients its local axis toward this point.
    ///     Resolved to a SpaceGass node ID during assembly. Null = not using node mode.
    /// </summary>
    public SgPoint3D? NodePoint { get; }

    /// <summary>
    ///     True when the direction has no effect — angle is 0, axis is NotApplicable,
    ///     and no direction node is set. Used to skip unnecessary API payload.
    /// </summary>
    public bool IsDefault =>
        Math.Abs(Angle) < 1e-10 &&
        Axis == DirectionAxis.NotApplicable &&
        NodePoint == null;

    /// <summary>Create a direction from a rotation angle (degrees).</summary>
    public static SgDirectionData FromAngle(double angleDegrees)
    {
        return new SgDirectionData(angleDegrees, DirectionAxis.NotApplicable, null);
    }

    /// <summary>Create a direction from a global axis reference.</summary>
    public static SgDirectionData FromAxis(DirectionAxis axis)
    {
        return new SgDirectionData(0.0, axis, null);
    }

    /// <summary>Create a direction from a node point (resolved to node ID during assembly).</summary>
    public static SgDirectionData FromNode(SgPoint3D nodePoint)
    {
        return new SgDirectionData(0.0, DirectionAxis.NotApplicable, nodePoint);
    }
}