namespace GhSpaceGass.Core.Models;

/// <summary>
///     A single plate nodal force result — forces/moments at a specific node
///     of a plate for a specific load case. Returned from the SpaceGass API.
/// </summary>
public class SgPlateNodalForceData
{
    public SgPlateNodalForceData(int plateId, int loadCaseId, int nodeId,
        double fx = 0, double fy = 0, double fz = 0,
        double mx = 0, double my = 0, double mz = 0)
    {
        PlateId = plateId;
        LoadCaseId = loadCaseId;
        NodeId = nodeId;
        Fx = fx; Fy = fy; Fz = fz;
        Mx = mx; My = my; Mz = mz;
    }

    public int PlateId { get; }
    public int LoadCaseId { get; }
    public int NodeId { get; }
    public double Fx { get; }
    public double Fy { get; }
    public double Fz { get; }
    public double Mx { get; }
    public double My { get; }
    public double Mz { get; }
}

/// <summary>Container for plate nodal force query results.</summary>
public class SgPlateNodalForcesResult
{
    public List<SgPlateNodalForceData> Forces { get; } = new();
    public List<string> Warnings { get; } = new();
}

