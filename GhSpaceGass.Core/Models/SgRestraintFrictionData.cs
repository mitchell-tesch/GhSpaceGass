using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Models;

/// <summary>
///     Friction parameters for a single translational axis of a node restraint.
/// </summary>
public class SgFrictionAxisData
{
    public SgFrictionAxisData(double factor, FrictionNormalAxis normalAxis, FrictionNormalDirection normalDirection)
    {
        Factor = factor;
        NormalAxis = normalAxis;
        NormalDirection = normalDirection;
    }

    /// <summary>Friction factor (coefficient).</summary>
    public double Factor { get; }

    /// <summary>The axis of the normal force for friction calculation.</summary>
    public FrictionNormalAxis NormalAxis { get; }

    /// <summary>The direction of the normal force.</summary>
    public FrictionNormalDirection NormalDirection { get; }
}

/// <summary>
///     Friction parameters for a node restraint (per translational axis: X, Y, Z).
///     When applied, the corresponding translational DOF code becomes 'N' (Friction).
///     Created by the Create Restraint Friction builder component.
/// </summary>
public class SgRestraintFrictionData
{
    public SgRestraintFrictionData(
        SgFrictionAxisData? x = null,
        SgFrictionAxisData? y = null,
        SgFrictionAxisData? z = null)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>Friction parameters for X-translation. Null = no friction on X.</summary>
    public SgFrictionAxisData? X { get; }

    /// <summary>Friction parameters for Y-translation.</summary>
    public SgFrictionAxisData? Y { get; }

    /// <summary>Friction parameters for Z-translation.</summary>
    public SgFrictionAxisData? Z { get; }

    /// <summary>True if at least one axis has friction defined.</summary>
    public bool HasAnyFriction => X != null || Y != null || Z != null;
}