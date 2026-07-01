namespace GhSpaceGass.Core.Models;

/// <summary>
///     A single node reaction result — force and moment components at a restrained node
///     for a specific load case. Returned from the SpaceGass API after analysis.
/// </summary>
public class SgNodeReactionData
{
    public SgNodeReactionData(
        int nodeId, int loadCaseId,
        double fx, double fy, double fz,
        double mx, double my, double mz)
    {
        NodeId = nodeId;
        LoadCaseId = loadCaseId;
        Fx = fx;
        Fy = fy;
        Fz = fz;
        Mx = mx;
        My = my;
        Mz = mz;
    }

    /// <summary>SpaceGass node ID.</summary>
    public int NodeId { get; }

    /// <summary>SpaceGass load case ID.</summary>
    public int LoadCaseId { get; }

    /// <summary>Reaction force in global X direction.</summary>
    public double Fx { get; }

    /// <summary>Reaction force in global Y direction.</summary>
    public double Fy { get; }

    /// <summary>Reaction force in global Z direction.</summary>
    public double Fz { get; }

    /// <summary>Reaction moment about global X axis.</summary>
    public double Mx { get; }

    /// <summary>Reaction moment about global Y axis.</summary>
    public double My { get; }

    /// <summary>Reaction moment about global Z axis.</summary>
    public double Mz { get; }
}