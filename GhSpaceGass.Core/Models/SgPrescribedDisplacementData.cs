namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a prescribed node displacement (imposed displacement/rotation).
///     No API call — pure data. Applied at a node within a load case.
/// </summary>
public class SgPrescribedDisplacementData
{
    public SgPrescribedDisplacementData(
        SgPoint3D point,
        SgLoadCaseData loadCase,
        double tx = 0, double ty = 0, double tz = 0,
        double rx = 0, double ry = 0, double rz = 0,
        SgLoadCategoryData? loadCategory = null)
    {
        Point = point;
        LoadCase = loadCase ?? throw new ArgumentNullException(nameof(loadCase));
        Tx = tx;
        Ty = ty;
        Tz = tz;
        Rx = rx;
        Ry = ry;
        Rz = rz;
        LoadCategory = loadCategory;
    }

    /// <summary>The location where the prescribed displacement is applied.</summary>
    public SgPoint3D Point { get; }

    /// <summary>The load case this prescribed displacement belongs to.</summary>
    public SgLoadCaseData LoadCase { get; }

    /// <summary>Optional load category for this prescribed displacement.</summary>
    public SgLoadCategoryData? LoadCategory { get; }

    /// <summary>Prescribed translation in X direction.</summary>
    public double Tx { get; }

    /// <summary>Prescribed translation in Y direction.</summary>
    public double Ty { get; }

    /// <summary>Prescribed translation in Z direction.</summary>
    public double Tz { get; }

    /// <summary>Prescribed rotation about X axis.</summary>
    public double Rx { get; }

    /// <summary>Prescribed rotation about Y axis.</summary>
    public double Ry { get; }

    /// <summary>Prescribed rotation about Z axis.</summary>
    public double Rz { get; }

    /// <summary>Returns true if all translation and rotation components are zero.</summary>
    public bool IsZero => Tx == 0 && Ty == 0 && Tz == 0 && Rx == 0 && Ry == 0 && Rz == 0;
}

