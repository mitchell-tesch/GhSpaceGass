namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a lumped mass load.
///     No API call — pure data. Applied at a node for dynamic frequency analysis.
/// </summary>
public class SgLumpedMassLoadData
{
    public SgLumpedMassLoadData(
        SgPoint3D point,
        SgLoadCaseData loadCase,
        double tmx = 0, double tmy = 0, double tmz = 0,
        double rmx = 0, double rmy = 0, double rmz = 0,
        SgLoadCategoryData? loadCategory = null)
    {
        Point = point;
        LoadCase = loadCase ?? throw new ArgumentNullException(nameof(loadCase));
        Tmx = tmx;
        Tmy = tmy;
        Tmz = tmz;
        Rmx = rmx;
        Rmy = rmy;
        Rmz = rmz;
        LoadCategory = loadCategory;
    }

    /// <summary>The location where the lumped mass is applied.</summary>
    public SgPoint3D Point { get; }

    /// <summary>The load case this lumped mass load belongs to.</summary>
    public SgLoadCaseData LoadCase { get; }

    /// <summary>Optional load category for this lumped mass load.</summary>
    public SgLoadCategoryData? LoadCategory { get; }

    /// <summary>Translational mass in X direction.</summary>
    public double Tmx { get; }

    /// <summary>Translational mass in Y direction.</summary>
    public double Tmy { get; }

    /// <summary>Translational mass in Z direction.</summary>
    public double Tmz { get; }

    /// <summary>Rotational mass about X axis.</summary>
    public double Rmx { get; }

    /// <summary>Rotational mass about Y axis.</summary>
    public double Rmy { get; }

    /// <summary>Rotational mass about Z axis.</summary>
    public double Rmz { get; }

    /// <summary>Returns true if all translational and rotational mass components are zero.</summary>
    public bool IsZero => Tmx == 0 && Tmy == 0 && Tmz == 0 && Rmx == 0 && Rmy == 0 && Rmz == 0;
}

