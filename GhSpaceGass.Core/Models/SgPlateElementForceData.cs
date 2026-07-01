namespace GhSpaceGass.Core.Models;

/// <summary>
///     A single plate element force result — average forces/moments for a plate
///     at a specific load case. Returned from the SpaceGass API after analysis.
/// </summary>
public class SgPlateElementForceData
{
    public SgPlateElementForceData(int plateId, int loadCaseId,
        double fx = 0, double fy = 0, double fxy = 0,
        double mx = 0, double my = 0, double mxy = 0,
        double mxTop = 0, double mxBtm = 0, double myTop = 0, double myBtm = 0,
        double vxz = 0, double vyz = 0)
    {
        PlateId = plateId;
        LoadCaseId = loadCaseId;
        Fx = fx; Fy = fy; Fxy = fxy;
        Mx = mx; My = my; Mxy = mxy;
        MxTop = mxTop; MxBtm = mxBtm; MyTop = myTop; MyBtm = myBtm;
        Vxz = vxz; Vyz = vyz;
    }

    public int PlateId { get; }
    public int LoadCaseId { get; }
    public double Fx { get; }
    public double Fy { get; }
    public double Fxy { get; }
    public double Mx { get; }
    public double My { get; }
    public double Mxy { get; }
    public double MxTop { get; }
    public double MxBtm { get; }
    public double MyTop { get; }
    public double MyBtm { get; }
    public double Vxz { get; }
    public double Vyz { get; }
}

/// <summary>Container for plate element force query results.</summary>
public class SgPlateElementForcesResult
{
    public List<SgPlateElementForceData> Forces { get; } = new();
    public List<string> Warnings { get; } = new();
}

