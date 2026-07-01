using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a node constraint (master-slave link).
///     No API call — pure data. Links a slave node's DOFs to a master node.
/// </summary>
public class SgNodeConstraintData
{
    public SgNodeConstraintData(
        SgPoint3D slavePoint,
        SgPoint3D masterPoint,
        string constraintCode,
        ConstraintAxes axes = ConstraintAxes.Global,
        double? xVector = null, double? yVector = null, double? zVector = null)
    {
        if (string.IsNullOrEmpty(constraintCode) || constraintCode.Length != 6)
            throw new ArgumentException(
                "Constraint code must be exactly 6 characters (one per DOF: TX,TY,TZ,RX,RY,RZ).",
                nameof(constraintCode));
        var upper = constraintCode.ToUpperInvariant();
        foreach (var c in upper)
            if (c != 'F' && c != 'R')
                throw new ArgumentException(
                    $"Invalid character '{c}' in constraint code. Valid: F(Constrained), R(Free).",
                    nameof(constraintCode));

        SlavePoint = slavePoint;
        MasterPoint = masterPoint;
        ConstraintCode = upper;
        Axes = axes;
        XVector = xVector;
        YVector = yVector;
        ZVector = zVector;
    }

    /// <summary>The location of the constrained (slave) node.</summary>
    public SgPoint3D SlavePoint { get; }

    /// <summary>The location of the master node.</summary>
    public SgPoint3D MasterPoint { get; }

    /// <summary>
    ///     6-character constraint code for (TX, TY, TZ, RX, RY, RZ).
    ///     Each character: F=Constrained (linked to master), R=Free (independent).
    ///     Example: "FFFFFF" = fully rigid link, "FFFRRR" = translations constrained only.
    /// </summary>
    public string ConstraintCode { get; }

    /// <summary>Axis system: Global or Inclined.</summary>
    public ConstraintAxes Axes { get; }

    /// <summary>X component of direction vector (for inclined axes).</summary>
    public double? XVector { get; }

    /// <summary>Y component of direction vector (for inclined axes).</summary>
    public double? YVector { get; }

    /// <summary>Z component of direction vector (for inclined axes).</summary>
    public double? ZVector { get; }
}

