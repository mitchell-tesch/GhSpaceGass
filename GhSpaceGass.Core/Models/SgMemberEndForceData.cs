namespace GhSpaceGass.Core.Models;

/// <summary>
///     A single member end force result — forces and moments at one end of a member
///     for a specific load case. Each API record contains 2 ends; this model represents
///     one flattened end (Node A or Node B).
/// </summary>
public class SgMemberEndForceData
{
    public SgMemberEndForceData(
        int memberId, int loadCaseId, int nodeId,
        double fx, double fy, double fz,
        double mx, double my, double mz)
    {
        MemberId = memberId;
        LoadCaseId = loadCaseId;
        NodeId = nodeId;
        Fx = fx;
        Fy = fy;
        Fz = fz;
        Mx = mx;
        My = my;
        Mz = mz;
    }

    public int MemberId { get; }
    public int LoadCaseId { get; }
    public int NodeId { get; }

    /// <summary>Axial force at this end.</summary>
    public double Fx { get; }

    /// <summary>Shear force in Y at this end.</summary>
    public double Fy { get; }

    /// <summary>Shear force in Z at this end.</summary>
    public double Fz { get; }

    /// <summary>Torsion at this end.</summary>
    public double Mx { get; }

    /// <summary>Bending moment about Y at this end.</summary>
    public double My { get; }

    /// <summary>Bending moment about Z at this end.</summary>
    public double Mz { get; }
}