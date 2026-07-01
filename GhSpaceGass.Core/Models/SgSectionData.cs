using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a section (library or custom). No API call - pure data.
///     When Library is set, library section (looked up from SpaceGass library).
///     When Library is null, custom section (user-defined properties).
/// </summary>
public class SgSectionData
{
    /// <summary>Create a library section (looked up from SpaceGass).</summary>
    public SgSectionData(string library, string name)
    {
        Library = library ?? throw new ArgumentNullException(nameof(library));
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>Create a custom section (user-defined properties).</summary>
    public SgSectionData(string name,
        double? area = null, double? iy = null, double? iz = null, double? j = null,
        double? ay = null, double? az = null, double? principalAngle = null,
        double? areaFactor = null, double? iyFactor = null, double? izFactor = null,
        double? torsionFactor = null, string? mark = null)
    {
        Library = null; // custom mode
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Area = area;
        Iy = iy;
        Iz = iz;
        J = j;
        Ay = ay;
        Az = az;
        PrincipalAngle = principalAngle;
        AreaFactor = areaFactor;
        IyFactor = iyFactor;
        IzFactor = izFactor;
        TorsionFactor = torsionFactor;
        Mark = mark;
    }

    /// <summary>The SpaceGass section library (e.g., "Aust300"). Null = custom section.</summary>
    public string? Library { get; }

    /// <summary>The section name (library lookup name or custom label).</summary>
    public string Name { get; }

    /// <summary>True when sourced from a SpaceGass library; false when user-defined.</summary>
    public bool IsLibrary => Library != null;

    // Custom section properties (only used when IsLibrary == false)
    /// <summary>Cross-sectional area.</summary>
    public double? Area { get; set; }

    /// <summary>Second moment of area about local Y axis.</summary>
    public double? Iy { get; set; }

    /// <summary>Second moment of area about local Z axis.</summary>
    public double? Iz { get; set; }

    /// <summary>Torsion constant.</summary>
    public double? J { get; set; }

    /// <summary>Principal axis rotation angle.</summary>
    public double? PrincipalAngle { get; set; }

    // Properties that apply in BOTH library and custom modes
    /// <summary>Optional mark/label for the section in the model.</summary>
    public string? Mark { get; set; }

    /// <summary>Angle section orientation.</summary>
    public AngleType? AngleType { get; set; }

    /// <summary>Area modification factor (must be > 0 when set).</summary>
    public double? AreaFactor { get; set; }

    /// <summary>Shear area in local Y direction.</summary>
    public double? Ay { get; set; }

    /// <summary>Shear area in local Z direction.</summary>
    public double? Az { get; set; }

    /// <summary>Iy modification factor (must be > 0 when set).</summary>
    public double? IyFactor { get; set; }

    /// <summary>Iz modification factor (must be > 0 when set).</summary>
    public double? IzFactor { get; set; }

    /// <summary>Torsion constant modification factor (must be > 0 when set).</summary>
    public double? TorsionFactor { get; set; }

    /// <summary>Whether the section is transposed.</summary>
    public bool? Transposed { get; set; }

    /// <summary>Unique key for deduplication (ADR-0006).</summary>
    public string Key => IsLibrary
        ? $"{Library}::{Name}|{Mark}|{AngleType}|{AreaFactor}|{Ay}|{Az}|{IyFactor}|{IzFactor}|{TorsionFactor}|{Transposed}"
        : $"CUSTOM::{Name}|{Area}|{Iy}|{Iz}|{J}|{Ay}|{Az}|{PrincipalAngle}|{AreaFactor}|{IyFactor}|{IzFactor}|{TorsionFactor}|{Mark}";
}