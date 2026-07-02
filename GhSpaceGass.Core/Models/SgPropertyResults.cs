namespace GhSpaceGass.Core.Models;

/// <summary>
///     Result of querying section properties from SpaceGass.
/// </summary>
public class SgSectionPropertiesResult
{
    public List<SgSectionPropertyData> Sections { get; } = new();
    public List<string> Warnings { get; } = new();
}

/// <summary>
///     A single section's properties as returned by the SpaceGass API.
/// </summary>
public readonly record struct SgSectionPropertyData(
    int Id,
    string Name,
    string Library,
    string Source,
    double Area,
    double Iy,
    double Iz,
    double J,
    double Ay,
    double Az,
    double PrincipalAngle,
    string Mark,
    double AreaFactor,
    double IyFactor,
    double IzFactor,
    double TorsionFactor,
    bool Transposed,
    string AngleType);

/// <summary>
///     Result of querying material properties from SpaceGass.
/// </summary>
public class SgMaterialPropertiesResult
{
    public List<SgMaterialPropertyData> Materials { get; } = new();
    public List<string> Warnings { get; } = new();
}

/// <summary>
///     A single material's properties as returned by the SpaceGass API.
/// </summary>
public readonly record struct SgMaterialPropertyData(
    int Id,
    string Name,
    string Library,
    string Source,
    double YoungsModulus,
    double PoissonsRatio,
    double Density,
    double ThermalCoefficient,
    double ConcreteStrength);
