using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;

namespace GhSpaceGass.Components.Structure;

public class CreateMemberReleaseComponent : GH_Component
{
    private int _inFx, _inFy, _inFz, _inMx, _inMy, _inMz;
    private int _inKx, _inKy, _inKz, _inKmx, _inKmy, _inKmz;
    
    private int _outRelease;

    public CreateMemberReleaseComponent()
        : base("SG Member Release", "sgRelease",
            "Create a SpaceGass member end release (per-DOF fixity with optional spring stiffness).",
            "SpaceGass", "3 | Structure")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.secondary;
    protected override Bitmap Icon => Icons.IconFactory.Release();
    public override Guid ComponentGuid => new("DFC37619-F602-41B9-A5D1-A61F81AABFB3");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inFx = pManager.AddBooleanParameter("Fx", "Fx",
            "Fix X-translation (default: true = Fixed).",
            GH_ParamAccess.item, true);
        _inFy = pManager.AddBooleanParameter("Fy", "Fy",
            "Fix Y-translation (default: true = Fixed).",
            GH_ParamAccess.item, true);
        _inFz = pManager.AddBooleanParameter("Fz", "Fz",
            "Fix Z-translation (default: true = Fixed).",
            GH_ParamAccess.item, true);
        _inMx = pManager.AddBooleanParameter("Mx", "Mx",
            "Fix X-rotation (default: true = Fixed).",
            GH_ParamAccess.item, true);
        _inMy = pManager.AddBooleanParameter("My", "My",
            "Fix Y-rotation (default: true = Fixed).",
            GH_ParamAccess.item, true);
        _inMz = pManager.AddBooleanParameter("Mz", "Mz",
            "Fix Z-rotation (default: true = Fixed).",
            GH_ParamAccess.item, true);

        _inKx = pManager.AddNumberParameter("Kx", "Kx",
            "Spring stiffness for Fx (optional — overrides Fx bool to Spring).",
            GH_ParamAccess.item);
        _inKy = pManager.AddNumberParameter("Ky", "Ky",
            "Spring stiffness for Fy (optional — overrides Fy bool to Spring).",
            GH_ParamAccess.item);
        _inKz = pManager.AddNumberParameter("Kz", "Kz",
            "Spring stiffness for Fz (optional — overrides Fz bool to Spring).",
            GH_ParamAccess.item);
        _inKmx = pManager.AddNumberParameter("Kmx", "Kmx",
            "Spring stiffness for Mx (optional — overrides Mx bool to Spring).",
            GH_ParamAccess.item);
        _inKmy = pManager.AddNumberParameter("Kmy", "Kmy",
            "Spring stiffness for My (optional — overrides My bool to Spring).",
            GH_ParamAccess.item);
        _inKmz = pManager.AddNumberParameter("Kmz", "Kmz",
            "Spring stiffness for Mz (optional — overrides Mz bool to Spring).",
            GH_ParamAccess.item);

        pManager[_inFx].Optional = true;
        pManager[_inFy].Optional = true;
        pManager[_inFz].Optional = true;
        pManager[_inMx].Optional = true;
        pManager[_inMy].Optional = true;
        pManager[_inMz].Optional = true;
        pManager[_inKx].Optional = true;
        pManager[_inKy].Optional = true;
        pManager[_inKz].Optional = true;
        pManager[_inKmx].Optional = true;
        pManager[_inKmy].Optional = true;
        pManager[_inKmz].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outRelease = pManager.AddParameter(new Param_SgRelease(),
            "Release", "R",
            "The SpaceGass member end release.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        bool fx = true, fy = true, fz = true;
        bool mx = true, my = true, mz = true;

        da.GetData(_inFx, ref fx);
        da.GetData(_inFy, ref fy);
        da.GetData(_inFz, ref fz);
        da.GetData(_inMx, ref mx);
        da.GetData(_inMy, ref my);
        da.GetData(_inMz, ref mz);

        // Read optional stiffness values
        double kx = 0, ky = 0, kz = 0, kmx = 0, kmy = 0, kmz = 0;
        var hasKx = da.GetData(_inKx, ref kx);
        var hasKy = da.GetData(_inKy, ref ky);
        var hasKz = da.GetData(_inKz, ref kz);
        var hasKmx = da.GetData(_inKmx, ref kmx);
        var hasKmy = da.GetData(_inKmy, ref kmy);
        var hasKmz = da.GetData(_inKmz, ref kmz);

        // Build 6-character release code: F = Fixed, R = Released, S = Spring.
        // Stiffness overrides the boolean — if stiffness is provided, code is 'S'
        static string DofCode(bool isFixed, bool hasStiffness)
        {
            return hasStiffness ? "S" : isFixed ? "F" : "R";
        }

        var code = string.Concat(
            DofCode(fx, hasKx),
            DofCode(fy, hasKy),
            DofCode(fz, hasKz),
            DofCode(mx, hasKmx),
            DofCode(my, hasKmy),
            DofCode(mz, hasKmz));

        double? kTx = hasKx ? kx : null;
        double? kTy = hasKy ? ky : null;
        double? kTz = hasKz ? kz : null;
        double? kRx = hasKmx ? kmx : null;
        double? kRy = hasKmy ? kmy : null;
        double? kRz = hasKmz ? kmz : null;

        var release = new SgReleaseData(code, kTx, kTy, kTz, kRx, kRy, kRz);
        da.SetData(_outRelease, new GH_SgRelease(release));
    }
}