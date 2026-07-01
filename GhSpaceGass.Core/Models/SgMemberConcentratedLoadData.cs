using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a member concentrated load.
///     No API call — pure data. Defaults to local axes (ADR-0011).
/// </summary>
public class SgMemberConcentratedLoadData
{
    public SgMemberConcentratedLoadData(
        SgPoint3D memberStart,
        SgPoint3D memberEnd,
        SgLoadCaseData loadCase,
        double fx = 0, double fy = 0, double fz = 0,
        double mx = 0, double my = 0, double mz = 0,
        double position = 50, LoadPositionUnits positionUnits = LoadPositionUnits.Percent,
        LoadAxes axes = LoadAxes.Local,
        SgLoadCategoryData? loadCategory = null)
    {
        MemberStart = memberStart;
        MemberEnd = memberEnd;
        LoadCase = loadCase ?? throw new ArgumentNullException(nameof(loadCase));
        Fx = fx;
        Fy = fy;
        Fz = fz;
        Mx = mx;
        My = my;
        Mz = mz;
        Position = position;
        PositionUnits = positionUnits;
        Axes = axes;
        LoadCategory = loadCategory;
    }

    /// <summary>Start point of the member this load is applied to.</summary>
    public SgPoint3D MemberStart { get; }

    /// <summary>End point of the member this load is applied to.</summary>
    public SgPoint3D MemberEnd { get; }

    /// <summary>The load case this concentrated load belongs to.</summary>
    public SgLoadCaseData LoadCase { get; }

    /// <summary>Optional load category for this concentrated load.</summary>
    public SgLoadCategoryData? LoadCategory { get; }

    /// <summary>Concentrated force in X direction.</summary>
    public double Fx { get; }

    /// <summary>Concentrated force in Y direction.</summary>
    public double Fy { get; }

    /// <summary>Concentrated force in Z direction.</summary>
    public double Fz { get; }

    /// <summary>Concentrated moment about X axis.</summary>
    public double Mx { get; }

    /// <summary>Concentrated moment about Y axis.</summary>
    public double My { get; }

    /// <summary>Concentrated moment about Z axis.</summary>
    public double Mz { get; }

    /// <summary>Position along the member where the load is applied.</summary>
    public double Position { get; }

    /// <summary>Units for position (Percent or Actual).</summary>
    public LoadPositionUnits PositionUnits { get; }

    /// <summary>Axis system for the load (Local or Global).</summary>
    public LoadAxes Axes { get; }

    /// <summary>Returns true if all force and moment components are zero.</summary>
    public bool IsZero => Fx == 0 && Fy == 0 && Fz == 0 && Mx == 0 && My == 0 && Mz == 0;
}

