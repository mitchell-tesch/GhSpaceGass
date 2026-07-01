namespace GhSpaceGass.Core.Models;

/// <summary>
///     A single intermediate force result — forces and moments at one station along a member
///     for a specific load case. The API returns one record per member/load-case with lists
///     of station values; this model represents one flattened station.
/// </summary>
public class SgMemberIntermediateForceData
{
    public SgMemberIntermediateForceData(
        int memberId, int loadCaseId, int station, double location,
        double fx, double fy, double fz,
        double mx, double my, double mz)
    {
        MemberId = memberId;
        LoadCaseId = loadCaseId;
        Station = station;
        Location = location;
        Fx = fx;
        Fy = fy;
        Fz = fz;
        Mx = mx;
        My = my;
        Mz = mz;
    }

    public int MemberId { get; }
    public int LoadCaseId { get; }
    public int Station { get; }

    /// <summary>Position along the member length.</summary>
    public double Location { get; }

    /// <summary>Axial force at this station.</summary>
    public double Fx { get; }

    /// <summary>Shear force in Y at this station.</summary>
    public double Fy { get; }

    /// <summary>Shear force in Z at this station.</summary>
    public double Fz { get; }

    /// <summary>Torsion at this station.</summary>
    public double Mx { get; }

    /// <summary>Bending moment about Y at this station.</summary>
    public double My { get; }

    /// <summary>Bending moment about Z at this station.</summary>
    public double Mz { get; }
}