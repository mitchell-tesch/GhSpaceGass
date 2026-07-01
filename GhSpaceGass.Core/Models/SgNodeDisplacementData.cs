namespace GhSpaceGass.Core.Models;

/// <summary>
///     A single node displacement result — translations and rotations at a node
///     for a specific load case. Returned from the SpaceGass API after analysis.
/// </summary>
public class SgNodeDisplacementData
{
    public SgNodeDisplacementData(
        int nodeId, int loadCaseId,
        double tx, double ty, double tz,
        double rx, double ry, double rz)
    {
        NodeId = nodeId;
        LoadCaseId = loadCaseId;
        Tx = tx;
        Ty = ty;
        Tz = tz;
        Rx = rx;
        Ry = ry;
        Rz = rz;
    }

    public int NodeId { get; }
    public int LoadCaseId { get; }

    /// <summary>Translation in global X direction.</summary>
    public double Tx { get; }

    /// <summary>Translation in global Y direction.</summary>
    public double Ty { get; }

    /// <summary>Translation in global Z direction.</summary>
    public double Tz { get; }

    /// <summary>Rotation about global X axis.</summary>
    public double Rx { get; }

    /// <summary>Rotation about global Y axis.</summary>
    public double Ry { get; }

    /// <summary>Rotation about global Z axis.</summary>
    public double Rz { get; }
}