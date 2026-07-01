namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a node restraint (boundary condition). No API call - pure data.
///     References geometry (point) and a 6-character restraint code for the 6 DOFs.
///     Code characters: F=Fixed, R=Released, S=Spring, P=Plastic, N=Friction, V=Variable.
/// </summary>
public class SgRestraintData
{
    public SgRestraintData(SgPoint3D point, string restraintCode,
        SgRestraintStiffnessData? stiffness = null,
        SgRestraintFrictionData? friction = null)
    {
        if (string.IsNullOrEmpty(restraintCode) || restraintCode.Length != 6)
            throw new ArgumentException(
                "Restraint code must be exactly 6 characters (one per DOF: TX,TY,TZ,RX,RY,RZ).",
                nameof(restraintCode));
        var upper = restraintCode.ToUpperInvariant();
        foreach (var c in upper)
            if (c != 'F' && c != 'R' && c != 'S' && c != 'P' && c != 'N' && c != 'V')
                throw new ArgumentException(
                    $"Invalid character '{c}' in restraint code. Valid: F(Fixed), R(Released), S(Spring), P(Plastic), N(Friction), V(Variable).",
                    nameof(restraintCode));
        Point = point;
        RestraintCode = upper;
        Stiffness = stiffness;
        Friction = friction;
    }

    /// <summary>The location where the restraint is applied.</summary>
    public SgPoint3D Point { get; }

    /// <summary>
    ///     6-character restraint code for (TX, TY, TZ, RX, RY, RZ).
    ///     Each character: F=Fixed, R=Released, S=Spring, P=Plastic, N=Friction, V=Variable.
    ///     Example: "FFFRRR" = pinned, "SSSFFF" = springs on translations.
    /// </summary>
    public string RestraintCode { get; }

    /// <summary>Optional spring stiffness parameters (for 'S' coded DOFs).</summary>
    public SgRestraintStiffnessData? Stiffness { get; }

    /// <summary>Optional friction parameters (for 'N' coded translational DOFs).</summary>
    public SgRestraintFrictionData? Friction { get; }
}