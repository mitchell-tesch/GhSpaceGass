namespace GhSpaceGass.Core.Models;

/// <summary>
///     Per-DOF spring stiffness values for a node restraint.
///     When applied, the corresponding DOF code becomes 'S' (Spring).
///     Created by the Create Restraint Stiffness builder component.
/// </summary>
public class SgRestraintStiffnessData
{
    public SgRestraintStiffnessData(
        double? kTx = null, double? kTy = null, double? kTz = null,
        double? kRx = null, double? kRy = null, double? kRz = null)
    {
        KTx = kTx;
        KTy = kTy;
        KTz = kTz;
        KRx = kRx;
        KRy = kRy;
        KRz = kRz;
    }

    /// <summary>Spring stiffness for TX (translation X). Null = not a spring on this DOF.</summary>
    public double? KTx { get; }

    /// <summary>Spring stiffness for TY (translation Y).</summary>
    public double? KTy { get; }

    /// <summary>Spring stiffness for TZ (translation Z).</summary>
    public double? KTz { get; }

    /// <summary>Spring stiffness for RX (rotation X).</summary>
    public double? KRx { get; }

    /// <summary>Spring stiffness for RY (rotation Y).</summary>
    public double? KRy { get; }

    /// <summary>Spring stiffness for RZ (rotation Z).</summary>
    public double? KRz { get; }

    /// <summary>True if at least one stiffness value is set.</summary>
    public bool HasAnyStiffness =>
        KTx.HasValue || KTy.HasValue || KTz.HasValue ||
        KRx.HasValue || KRy.HasValue || KRz.HasValue;
}