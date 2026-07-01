namespace GhSpaceGass.Core.Models;

/// <summary>
///     A single buckling effective length result for a specific load case, member, and mode.
/// </summary>
public class SgBucklingEffectiveLengthData
{
    public SgBucklingEffectiveLengthData(
        int loadCaseId, int memberId, int mode,
        double ley, double lez, double pcr, double length)
    {
        LoadCaseId = loadCaseId;
        MemberId = memberId;
        Mode = mode;
        Ley = ley;
        Lez = lez;
        Pcr = pcr;
        Length = length;
    }

    public int LoadCaseId { get; }
    public int MemberId { get; }
    public int Mode { get; }

    /// <summary>Effective length about Y axis.</summary>
    public double Ley { get; }

    /// <summary>Effective length about Z axis.</summary>
    public double Lez { get; }

    /// <summary>Critical buckling load.</summary>
    public double Pcr { get; }

    /// <summary>Member length.</summary>
    public double Length { get; }
}