namespace GhSpaceGass.Core.Models;

/// <summary>
///     A single mode shape displacement at a specific node for a specific mode.
/// </summary>
public class SgModeShapeNodeData
{
    public SgModeShapeNodeData(
        int loadCaseId, int mode, int nodeId,
        double tx, double ty, double tz,
        double rx, double ry, double rz)
    {
        LoadCaseId = loadCaseId;
        Mode = mode;
        NodeId = nodeId;
        Tx = tx;
        Ty = ty;
        Tz = tz;
        Rx = rx;
        Ry = ry;
        Rz = rz;
    }

    public int LoadCaseId { get; }
    public int Mode { get; }
    public int NodeId { get; }

    /// <summary>Translation in X.</summary>
    public double Tx { get; }

    /// <summary>Translation in Y.</summary>
    public double Ty { get; }

    /// <summary>Translation in Z.</summary>
    public double Tz { get; }

    /// <summary>Rotation about X.</summary>
    public double Rx { get; }

    /// <summary>Rotation about Y.</summary>
    public double Ry { get; }

    /// <summary>Rotation about Z.</summary>
    public double Rz { get; }
}

