namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a self-weight load.
///     No API call — pure data. Applies gravity acceleration to all members
///     in the specified load case.
/// </summary>
public class SgSelfWeightLoadData
{
    public SgSelfWeightLoadData(
        SgLoadCaseData loadCase,
        double accelerationX = 0,
        double accelerationY = -1,
        double accelerationZ = 0,
        SgLoadCategoryData? loadCategory = null)
    {
        LoadCase = loadCase ?? throw new ArgumentNullException(nameof(loadCase));
        AccelerationX = accelerationX;
        AccelerationY = accelerationY;
        AccelerationZ = accelerationZ;
        LoadCategory = loadCategory;
    }

    /// <summary>The load case this self-weight load belongs to.</summary>
    public SgLoadCaseData LoadCase { get; }

    /// <summary>Optional load category for this self-weight load.</summary>
    public SgLoadCategoryData? LoadCategory { get; }

    /// <summary>Gravity acceleration in global X direction (in g's).</summary>
    public double AccelerationX { get; }

    /// <summary>Gravity acceleration in global Y direction (in g's). Default: -1</summary>
    public double AccelerationY { get; }

    /// <summary>Gravity acceleration in global Z direction (in g's).</summary>
    public double AccelerationZ { get; }

    /// <summary>Returns true if all acceleration components are zero.</summary>
    public bool IsZero => AccelerationX == 0 && AccelerationY == 0 && AccelerationZ == 0;
}