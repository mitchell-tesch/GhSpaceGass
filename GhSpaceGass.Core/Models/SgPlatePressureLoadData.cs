using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a plate pressure load.
///     No API call — pure data. Applies pressure to a plate element.
/// </summary>
public class SgPlatePressureLoadData
{
    public SgPlatePressureLoadData(
        SgPoint3D[] plateNodes,
        SgLoadCaseData loadCase,
        double px = 0, double py = 0, double pz = 0,
        LoadAxes axes = LoadAxes.Local,
        SgLoadCategoryData? loadCategory = null)
    {
        PlateNodes = (SgPoint3D[])(plateNodes ?? throw new ArgumentNullException(nameof(plateNodes))).Clone();
        LoadCase = loadCase ?? throw new ArgumentNullException(nameof(loadCase));
        Px = px;
        Py = py;
        Pz = pz;
        Axes = axes;
        LoadCategory = loadCategory;
    }

    /// <summary>Corner nodes of the plate this load is applied to (for plate ID resolution).</summary>
    public SgPoint3D[] PlateNodes { get; }

    /// <summary>The load case this pressure load belongs to.</summary>
    public SgLoadCaseData LoadCase { get; }

    /// <summary>Optional load category for this pressure load.</summary>
    public SgLoadCategoryData? LoadCategory { get; }

    /// <summary>Pressure in X direction.</summary>
    public double Px { get; }

    /// <summary>Pressure in Y direction.</summary>
    public double Py { get; }

    /// <summary>Pressure in Z direction.</summary>
    public double Pz { get; }

    /// <summary>Axis system for the pressure (Local or Global).</summary>
    public LoadAxes Axes { get; }

    /// <summary>Returns true if all pressure components are zero.</summary>
    public bool IsZero => Px == 0 && Py == 0 && Pz == 0;
}

