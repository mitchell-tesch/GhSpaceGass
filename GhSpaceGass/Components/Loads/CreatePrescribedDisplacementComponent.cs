using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace GhSpaceGass.Components.Loads;

public class CreatePrescribedDisplacementComponent : GH_Component
{
    private int _inLoadCase;
    private int _inLoadCategory;
    private int _inPoint;
    private int _inRx;
    private int _inRy;
    private int _inRz;
    private int _inTx;
    private int _inTy;
    private int _inTz;

    private int _outPrescribedDisplacement;

    public CreatePrescribedDisplacementComponent()
        : base("SG Prescribed Displacement", "sgPrescDisp",
            "Create a SpaceGass prescribed node displacement (imposed displacement/rotation at a point).",
            "SpaceGass", "5 | Loads")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.secondary;
    protected override Bitmap Icon => Icons.IconFactory.PrescribedDisplacement();
    public override Guid ComponentGuid => new("A3E70C02-B8A3-4D22-9F1D-7E6A5C4B3D22");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inPoint = pManager.AddPointParameter("Point", "P",
            "The location where the displacement is prescribed.",
            GH_ParamAccess.item);
        _inLoadCase = pManager.AddParameter(new Param_SgLoadCase(),
            "Load Case", "LC",
            "The load case this prescribed displacement belongs to.",
            GH_ParamAccess.item);
        _inLoadCategory = pManager.AddParameter(new Param_SgLoadCategory(),
            "Load Category", "Cat",
            "Optional load category (e.g., Dead, Live, Wind).",
            GH_ParamAccess.item);
        _inTx = pManager.AddNumberParameter("Tx", "Tx",
            "Prescribed translation in X direction (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inTy = pManager.AddNumberParameter("Ty", "Ty",
            "Prescribed translation in Y direction (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inTz = pManager.AddNumberParameter("Tz", "Tz",
            "Prescribed translation in Z direction (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inRx = pManager.AddNumberParameter("Rx", "Rx",
            "Prescribed rotation about X axis (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inRy = pManager.AddNumberParameter("Ry", "Ry",
            "Prescribed rotation about Y axis (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inRz = pManager.AddNumberParameter("Rz", "Rz",
            "Prescribed rotation about Z axis (default: 0).",
            GH_ParamAccess.item, 0.0);

        pManager[_inLoadCategory].Optional = true;
        pManager[_inTx].Optional = true;
        pManager[_inTy].Optional = true;
        pManager[_inTz].Optional = true;
        pManager[_inRx].Optional = true;
        pManager[_inRy].Optional = true;
        pManager[_inRz].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outPrescribedDisplacement = pManager.AddParameter(new Param_SgPrescribedDisplacement(),
            "Prescribed Displacement", "PD",
            "The SpaceGass prescribed displacement.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        var point = Point3d.Unset;
        GH_SgLoadCase loadCaseGoo = null;
        GH_SgLoadCategory loadCategoryGoo = null;
        double tx = 0, ty = 0, tz = 0;
        double rx = 0, ry = 0, rz = 0;

        if (!da.GetData(_inPoint, ref point)) return;
        if (!da.GetData(_inLoadCase, ref loadCaseGoo)) return;
        da.GetData(_inLoadCategory, ref loadCategoryGoo);
        da.GetData(_inTx, ref tx);
        da.GetData(_inTy, ref ty);
        da.GetData(_inTz, ref tz);
        da.GetData(_inRx, ref rx);
        da.GetData(_inRy, ref ry);
        da.GetData(_inRz, ref rz);

        if (loadCaseGoo?.Value == null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Invalid load case input.");
            return;
        }

        var sgPoint = new SgPoint3D(point.X, point.Y, point.Z);
        var category = loadCategoryGoo?.Value;
        var prescribedDisplacement = new SgPrescribedDisplacementData(sgPoint, loadCaseGoo.Value,
            tx, ty, tz, rx, ry, rz, category);

        if (prescribedDisplacement.IsZero)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "All displacement components are zero — this load will have no effect.");

        da.SetData(_outPrescribedDisplacement, new GH_SgPrescribedDisplacement(prescribedDisplacement));
    }
}

