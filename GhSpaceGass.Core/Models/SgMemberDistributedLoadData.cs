using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a member distributed load.
///     No API call — pure data. Defaults to global axes (ADR-0011).
/// </summary>
public class SgMemberDistributedLoadData
{
    public SgMemberDistributedLoadData(
        SgPoint3D memberStart,
        SgPoint3D memberEnd,
        SgLoadCaseData loadCase,
        double fxStart = 0, double fyStart = 0, double fzStart = 0,
        double fxEnd = 0, double fyEnd = 0, double fzEnd = 0,
        double startPosition = 0, double endPosition = 100,
        LoadPositionUnits positionUnits = LoadPositionUnits.Percent,
        LoadAxes axes = LoadAxes.GlobalProjected,
        SgLoadCategoryData? loadCategory = null,
        double mxStart = 0, double myStart = 0, double mzStart = 0,
        double mxEnd = 0, double myEnd = 0, double mzEnd = 0)
    {
        MemberStart = memberStart;
        MemberEnd = memberEnd;
        LoadCase = loadCase ?? throw new ArgumentNullException(nameof(loadCase));
        FxStart = fxStart;
        FyStart = fyStart;
        FzStart = fzStart;
        FxEnd = fxEnd;
        FyEnd = fyEnd;
        FzEnd = fzEnd;
        StartPosition = startPosition;
        EndPosition = endPosition;
        PositionUnits = positionUnits;
        Axes = axes;
        LoadCategory = loadCategory;
        MxStart = mxStart;
        MyStart = myStart;
        MzStart = mzStart;
        MxEnd = mxEnd;
        MyEnd = myEnd;
        MzEnd = mzEnd;
    }

    /// <summary>Start point of the member this load is applied to.</summary>
    public SgPoint3D MemberStart { get; }

    /// <summary>End point of the member this load is applied to.</summary>
    public SgPoint3D MemberEnd { get; }

    /// <summary>The load case this distributed load belongs to.</summary>
    public SgLoadCaseData LoadCase { get; }

    /// <summary>Optional load category for this distributed load.</summary>
    public SgLoadCategoryData? LoadCategory { get; }

    /// <summary>Force intensity in X direction at start of loaded region.</summary>
    public double FxStart { get; }

    /// <summary>Force intensity in Y direction at start of loaded region.</summary>
    public double FyStart { get; }

    /// <summary>Force intensity in Z direction at start of loaded region.</summary>
    public double FzStart { get; }

    /// <summary>Force intensity in X direction at end of loaded region.</summary>
    public double FxEnd { get; }

    /// <summary>Force intensity in Y direction at end of loaded region.</summary>
    public double FyEnd { get; }

    /// <summary>Force intensity in Z direction at end of loaded region.</summary>
    public double FzEnd { get; }

    /// <summary>Start position of the loaded region along the member.</summary>
    public double StartPosition { get; }

    /// <summary>End position of the loaded region along the member.</summary>
    public double EndPosition { get; }

    /// <summary>Units for start/end position (Percent or Actual).</summary>
    public LoadPositionUnits PositionUnits { get; }

    /// <summary>Axis system for the load (Global or Local).</summary>
    public LoadAxes Axes { get; }

    /// <summary>Moment intensity about X axis at start of loaded region.</summary>
    public double MxStart { get; }

    /// <summary>Moment intensity about Y axis at start of loaded region.</summary>
    public double MyStart { get; }

    /// <summary>Moment intensity about Z axis at start of loaded region.</summary>
    public double MzStart { get; }

    /// <summary>Moment intensity about X axis at end of loaded region.</summary>
    public double MxEnd { get; }

    /// <summary>Moment intensity about Y axis at end of loaded region.</summary>
    public double MyEnd { get; }

    /// <summary>Moment intensity about Z axis at end of loaded region.</summary>
    public double MzEnd { get; }

    /// <summary>Returns true if any force component is non-zero.</summary>
    public bool HasForces => FxStart != 0 || FyStart != 0 || FzStart != 0 ||
                             FxEnd != 0 || FyEnd != 0 || FzEnd != 0;

    /// <summary>Returns true if any moment component is non-zero.</summary>
    public bool HasMoments => MxStart != 0 || MyStart != 0 || MzStart != 0 ||
                              MxEnd != 0 || MyEnd != 0 || MzEnd != 0;

    /// <summary>Returns true if all force and moment components are zero at both start and end.</summary>
    public bool IsZero => !HasForces && !HasMoments;
}