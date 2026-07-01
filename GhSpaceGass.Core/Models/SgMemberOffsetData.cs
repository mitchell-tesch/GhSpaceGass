using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a member offset.
///     No API call — pure data. Offsets at each end of a member in local or global axes.
/// </summary>
public class SgMemberOffsetData
{
    public SgMemberOffsetData(
        double xOffsetAtA = 0, double yOffsetAtA = 0, double zOffsetAtA = 0,
        double xOffsetAtB = 0, double yOffsetAtB = 0, double zOffsetAtB = 0,
        AxesType axes = AxesType.Local)
    {
        XOffsetAtA = xOffsetAtA;
        YOffsetAtA = yOffsetAtA;
        ZOffsetAtA = zOffsetAtA;
        XOffsetAtB = xOffsetAtB;
        YOffsetAtB = yOffsetAtB;
        ZOffsetAtB = zOffsetAtB;
        Axes = axes;
    }

    /// <summary>X offset at end A.</summary>
    public double XOffsetAtA { get; }

    /// <summary>Y offset at end A.</summary>
    public double YOffsetAtA { get; }

    /// <summary>Z offset at end A.</summary>
    public double ZOffsetAtA { get; }

    /// <summary>X offset at end B.</summary>
    public double XOffsetAtB { get; }

    /// <summary>Y offset at end B.</summary>
    public double YOffsetAtB { get; }

    /// <summary>Z offset at end B.</summary>
    public double ZOffsetAtB { get; }

    /// <summary>Axis system for offsets (Local or Global).</summary>
    public AxesType Axes { get; }

    /// <summary>Returns true if all offset components are zero.</summary>
    public bool IsZero => XOffsetAtA == 0 && YOffsetAtA == 0 && ZOffsetAtA == 0 &&
                          XOffsetAtB == 0 && YOffsetAtB == 0 && ZOffsetAtB == 0;
}

