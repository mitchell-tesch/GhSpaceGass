namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a material (library or custom). No API call - pure data.
///     When Library is set, library material (looked up from SpaceGass library).
///     When Library is null, custom material (user-defined properties).
/// </summary>
public class SgMaterialData
{
    /// <summary>Create a library material (looked up from SpaceGass).</summary>
    public SgMaterialData(string library, string name)
    {
        Library = library ?? throw new ArgumentNullException(nameof(library));
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>Create a custom material (user-defined properties).</summary>
    public SgMaterialData(string name,
        double? youngsModulus = null, double? poissonsRatio = null,
        double? massDensity = null, double? thermalCoeff = null,
        double? concreteStrength = null)
    {
        Library = null; // custom mode
        Name = name ?? throw new ArgumentNullException(nameof(name));
        YoungsModulus = youngsModulus;
        PoissonsRatio = poissonsRatio;
        MassDensity = massDensity;
        ThermalCoeff = thermalCoeff;
        ConcreteStrength = concreteStrength;
    }

    /// <summary>The SpaceGass material library (e.g., "Aust"). Null = custom material.</summary>
    public string? Library { get; }

    /// <summary>The material name (library lookup name or custom label).</summary>
    public string Name { get; }

    /// <summary>True when sourced from a SpaceGass library; false when user-defined.</summary>
    public bool IsLibrary => Library != null;

    // Custom material properties (only used when IsLibrary == false)
    /// <summary>Young's modulus (E).</summary>
    public double? YoungsModulus { get; set; }

    /// <summary>Poisson's ratio.</summary>
    public double? PoissonsRatio { get; set; }

    /// <summary>Mass density.</summary>
    public double? MassDensity { get; set; }

    /// <summary>Thermal expansion coefficient.</summary>
    public double? ThermalCoeff { get; set; }

    /// <summary>Concrete compressive strength.</summary>
    public double? ConcreteStrength { get; set; }

    /// <summary>Unique key for deduplication (ADR-0006).</summary>
    public string Key => IsLibrary
        ? $"{Library}::{Name}"
        : $"CUSTOM::{Name}|{YoungsModulus}|{PoissonsRatio}|{MassDensity}|{ThermalCoeff}|{ConcreteStrength}";
}