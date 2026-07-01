namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a node load (concentrated force/moment at a point).
///     No API call — pure data. Always in global axes (ADR-0011).
/// </summary>
public class SgNodeLoadData
{
    public SgNodeLoadData(
        SgPoint3D point,
        SgLoadCaseData loadCase,
        double fx = 0, double fy = 0, double fz = 0,
        double mx = 0, double my = 0, double mz = 0,
        SgLoadCategoryData? loadCategory = null)
    {
        Point = point;
        LoadCase = loadCase ?? throw new ArgumentNullException(nameof(loadCase));
        Fx = fx;
        Fy = fy;
        Fz = fz;
        Mx = mx;
        My = my;
        Mz = mz;
        LoadCategory = loadCategory;
    }

    /// <summary>The location where the load is applied.</summary>
    public SgPoint3D Point { get; }

    /// <summary>The load case this node load belongs to.</summary>
    public SgLoadCaseData LoadCase { get; }

    /// <summary>Optional load category for this node load.</summary>
    public SgLoadCategoryData? LoadCategory { get; }

    /// <summary>Force in global X direction.</summary>
    public double Fx { get; }

    /// <summary>Force in global Y direction.</summary>
    public double Fy { get; }

    /// <summary>Force in global Z direction.</summary>
    public double Fz { get; }

    /// <summary>Moment about global X axis.</summary>
    public double Mx { get; }

    /// <summary>Moment about global Y axis.</summary>
    public double My { get; }

    /// <summary>Moment about global Z axis.</summary>
    public double Mz { get; }

    /// <summary>Returns true if all force and moment components are zero.</summary>
    public bool IsZero => Fx == 0 && Fy == 0 && Fz == 0 && Mx == 0 && My == 0 && Mz == 0;
}