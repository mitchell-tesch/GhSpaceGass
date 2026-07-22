using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Models;

/// <summary>
///     A single wheel load on a moving-load vehicle — a position (X, Y) on the vehicle
///     footprint plus optional force / moment components. Used as an internal record inside
///     <see cref="SgMovingLoadVehicleData"/> and is not exposed to Grasshopper as its own Goo.
/// </summary>
public class SgVehicleWheelLoadData
{
    public SgVehicleWheelLoadData(
        double x = 0, double y = 0,
        double fx = 0, double fy = 0, double fz = 0,
        double mx = 0, double my = 0, double mz = 0)
    {
        X = x;
        Y = y;
        Fx = fx;
        Fy = fy;
        Fz = fz;
        Mx = mx;
        My = my;
        Mz = mz;
    }

    public double X { get; }
    public double Y { get; }
    public double Fx { get; }
    public double Fy { get; }
    public double Fz { get; }
    public double Mx { get; }
    public double My { get; }
    public double Mz { get; }

    /// <summary>True when all force and moment components are zero (position is not considered).</summary>
    public bool IsZero =>
        Fx == 0 && Fy == 0 && Fz == 0 && Mx == 0 && My == 0 && Mz == 0;
}

/// <summary>
///     In-memory representation of a moving-load vehicle definition. Two modes:
///     library (only Library + Name are meaningful — SpaceGass supplies the wheel layout) or
///     user-defined (a list of <see cref="SgVehicleWheelLoadData"/> plus load units). No API
///     call — pure data.
/// </summary>
public class SgMovingLoadVehicleData
{
    /// <summary>Create a user-defined vehicle.</summary>
    public SgMovingLoadVehicleData(
        string name,
        IReadOnlyList<SgVehicleWheelLoadData> wheelLoads,
        ForceUnit forceUnit,
        LengthUnit lengthUnit,
        MomentUnit momentUnit)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Moving load vehicle name cannot be null or empty.", nameof(name));
        if (wheelLoads == null || wheelLoads.Count == 0)
            throw new ArgumentException(
                "A user-defined moving load vehicle must have at least one wheel load.",
                nameof(wheelLoads));

        Name = name;
        Library = null;
        WheelLoads = wheelLoads;
        ForceUnit = forceUnit;
        LengthUnit = lengthUnit;
        MomentUnit = momentUnit;
    }

    /// <summary>Create a library-sourced vehicle.</summary>
    public SgMovingLoadVehicleData(string library, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Moving load vehicle name cannot be null or empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(library))
            throw new ArgumentException(
                "Library name cannot be null or empty for a library moving load vehicle.",
                nameof(library));

        Name = name;
        Library = library;
        WheelLoads = Array.Empty<SgVehicleWheelLoadData>();
    }

    public string Name { get; }
    public string? Library { get; }
    public IReadOnlyList<SgVehicleWheelLoadData> WheelLoads { get; }
    public ForceUnit ForceUnit { get; }
    public LengthUnit LengthUnit { get; }
    public MomentUnit MomentUnit { get; }

    public bool IsLibrary => Library != null;

    /// <summary>
    ///     Deduplication key. Library vehicles keyed by <c>Library::Name</c>; user vehicles keyed
    ///     by <c>Name</c>. Case-insensitivity is enforced by consuming dictionaries.
    /// </summary>
    public string Key => IsLibrary ? $"{Library}::{Name}" : Name;
}

/// <summary>
///     In-memory representation of a moving-load pressure definition — a rectangular
///     patch load (width × length) with per-axis pressure components and an optional load
///     spacing along the travel path. Pressures are always user-defined; there is no SpaceGass
///     library equivalent. No API call — pure data.
/// </summary>
public class SgMovingLoadPressureData
{
    public SgMovingLoadPressureData(
        string name,
        double width,
        double length,
        double? loadSpacing = null,
        double px = 0,
        double py = 0,
        double pz = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Moving load pressure name cannot be null or empty.", nameof(name));
        if (width <= 0)
            throw new ArgumentException(
                "Moving load pressure width must be greater than zero.", nameof(width));
        if (length <= 0)
            throw new ArgumentException(
                "Moving load pressure length must be greater than zero.", nameof(length));
        if (loadSpacing is <= 0)
            throw new ArgumentException(
                "Moving load pressure load spacing must be greater than zero when provided.",
                nameof(loadSpacing));

        Name = name;
        Width = width;
        Length = length;
        LoadSpacing = loadSpacing;
        Px = px;
        Py = py;
        Pz = pz;
    }

    public string Name { get; }
    public double Width { get; }
    public double Length { get; }
    public double? LoadSpacing { get; }
    public double Px { get; }
    public double Py { get; }
    public double Pz { get; }

    /// <summary>True when all pressure components are zero.</summary>
    public bool IsZero => Px == 0 && Py == 0 && Pz == 0;

    /// <summary>Deduplication key.</summary>
    public string Key => Name;
}
