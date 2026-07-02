namespace GhSpaceGass.Core.Models;

/// <summary>
///     Result of querying all plate-based loads from SpaceGass.
///     Grouped by unique plate — each entry contains pressure and thermal loads.
/// </summary>
public class SgPlateLoadsDataResult
{
    public List<SgPlateLoadEntry> PlateEntries { get; } = new();
    public List<string> Warnings { get; } = new();
}

/// <summary>All loads applied to a single plate.</summary>
public class SgPlateLoadEntry
{
    public SgPlateLoadEntry(int plateId, SgPoint3D[] cornerPoints)
    {
        PlateId = plateId;
        CornerPoints = cornerPoints;
    }

    public int PlateId { get; }
    public SgPoint3D[] CornerPoints { get; }
    public List<SgPlatePressureLoadInfo> PressureLoads { get; } = new();
    public List<SgPlateThermalLoadInfo> ThermalLoads { get; } = new();
}

public readonly record struct SgPlatePressureLoadInfo(
    int LoadCaseId, string LoadCaseName, int LoadCategoryId,
    double Px, double Py, double Pz, string Axes);

public readonly record struct SgPlateThermalLoadInfo(
    int LoadCaseId, string LoadCaseName, int LoadCategoryId,
    double Temperature, double YGradient, double ZGradient);
