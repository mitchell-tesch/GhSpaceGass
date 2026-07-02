namespace GhSpaceGass.Core.Models;

/// <summary>
///     Result of querying all member-based loads from SpaceGass.
///     Grouped by unique member — each entry contains its concentrated, distributed,
///     distributed moment, prestress, and thermal loads.
/// </summary>
public class SgMemberLoadsDataResult
{
    public List<SgMemberLoadEntry> MemberEntries { get; } = new();
    public List<string> Warnings { get; } = new();
}

/// <summary>All loads applied to a single member.</summary>
public class SgMemberLoadEntry
{
    public SgMemberLoadEntry(int memberId, SgPoint3D start, SgPoint3D end)
    {
        MemberId = memberId;
        Start = start;
        End = end;
    }

    public int MemberId { get; }
    public SgPoint3D Start { get; }
    public SgPoint3D End { get; }
    public List<SgConcentratedLoadInfo> ConcentratedLoads { get; } = new();
    public List<SgDistributedLoadInfo> DistributedLoads { get; } = new();
    public List<SgDistributedMomentInfo> DistributedMoments { get; } = new();
    public List<SgPrestressLoadInfo> PrestressLoads { get; } = new();
    public List<SgMemberThermalLoadInfo> ThermalLoads { get; } = new();
}

public readonly record struct SgConcentratedLoadInfo(
    int LoadCaseId, string LoadCaseName, int LoadCategoryId,
    double Fx, double Fy, double Fz,
    double Mx, double My, double Mz,
    double Position, string PositionUnits, string Axes);

public readonly record struct SgDistributedLoadInfo(
    int LoadCaseId, string LoadCaseName, int LoadCategoryId,
    double FxStart, double FyStart, double FzStart,
    double FxFinish, double FyFinish, double FzFinish,
    double StartPosition, double FinishPosition,
    string PositionUnits, string Axes);

public readonly record struct SgDistributedMomentInfo(
    int LoadCaseId, string LoadCaseName, int LoadCategoryId,
    double MxStart, double MyStart, double MzStart,
    double MxFinish, double MyFinish, double MzFinish,
    double StartPosition, double FinishPosition,
    string PositionUnits, string Axes);

public readonly record struct SgPrestressLoadInfo(
    int LoadCaseId, string LoadCaseName, int LoadCategoryId,
    double Prestress);

public readonly record struct SgMemberThermalLoadInfo(
    int LoadCaseId, string LoadCaseName, int LoadCategoryId,
    double Temperature, double YGradient, double ZGradient);
