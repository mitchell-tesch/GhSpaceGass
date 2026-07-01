using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace GhSpaceGass.Components.Loads;

public class CreateLumpedMassLoadComponent : GH_Component
{
    private int _inLoadCase;
    private int _inLoadCategory;
    private int _inPoint;
    private int _inRmx;
    private int _inRmy;
    private int _inRmz;
    private int _inTmx;
    private int _inTmy;
    private int _inTmz;

    private int _outLumpedMassLoad;

    public CreateLumpedMassLoadComponent()
        : base("SG Lumped Mass Load", "sgLumpedMass",
            "Create a SpaceGass lumped mass load (mass at a node for dynamic frequency analysis).",
            "SpaceGass", "5 | Loads")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.secondary;
    protected override Bitmap Icon => Icons.IconFactory.LumpedMassLoad();
    public override Guid ComponentGuid => new("A3E70C01-B8A3-4D22-9F1D-7E6A5C4B3D21");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inPoint = pManager.AddPointParameter("Point", "P",
            "The location where the lumped mass is applied.",
            GH_ParamAccess.item);
        _inLoadCase = pManager.AddParameter(new Param_SgLoadCase(),
            "Load Case", "LC",
            "The load case this load belongs to.",
            GH_ParamAccess.item);
        _inLoadCategory = pManager.AddParameter(new Param_SgLoadCategory(),
            "Load Category", "Cat",
            "Optional load category (e.g., Dead, Live, Wind).",
            GH_ParamAccess.item);
        _inTmx = pManager.AddNumberParameter("Tmx", "Tmx",
            "Translational mass in X direction (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inTmy = pManager.AddNumberParameter("Tmy", "Tmy",
            "Translational mass in Y direction (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inTmz = pManager.AddNumberParameter("Tmz", "Tmz",
            "Translational mass in Z direction (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inRmx = pManager.AddNumberParameter("Rmx", "Rmx",
            "Rotational mass about X axis (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inRmy = pManager.AddNumberParameter("Rmy", "Rmy",
            "Rotational mass about Y axis (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inRmz = pManager.AddNumberParameter("Rmz", "Rmz",
            "Rotational mass about Z axis (default: 0).",
            GH_ParamAccess.item, 0.0);

        pManager[_inLoadCategory].Optional = true;
        pManager[_inTmx].Optional = true;
        pManager[_inTmy].Optional = true;
        pManager[_inTmz].Optional = true;
        pManager[_inRmx].Optional = true;
        pManager[_inRmy].Optional = true;
        pManager[_inRmz].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outLumpedMassLoad = pManager.AddParameter(new Param_SgLumpedMassLoad(),
            "Lumped Mass Load", "LM",
            "The SpaceGass lumped mass load.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        var point = Point3d.Unset;
        GH_SgLoadCase loadCaseGoo = null;
        GH_SgLoadCategory loadCategoryGoo = null;
        double tmx = 0, tmy = 0, tmz = 0;
        double rmx = 0, rmy = 0, rmz = 0;

        if (!da.GetData(_inPoint, ref point)) return;
        if (!da.GetData(_inLoadCase, ref loadCaseGoo)) return;
        da.GetData(_inLoadCategory, ref loadCategoryGoo);
        da.GetData(_inTmx, ref tmx);
        da.GetData(_inTmy, ref tmy);
        da.GetData(_inTmz, ref tmz);
        da.GetData(_inRmx, ref rmx);
        da.GetData(_inRmy, ref rmy);
        da.GetData(_inRmz, ref rmz);

        if (loadCaseGoo?.Value == null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Invalid load case input.");
            return;
        }

        var sgPoint = new SgPoint3D(point.X, point.Y, point.Z);
        var category = loadCategoryGoo?.Value;
        var lumpedMass = new SgLumpedMassLoadData(sgPoint, loadCaseGoo.Value,
            tmx, tmy, tmz, rmx, rmy, rmz, category);

        if (lumpedMass.IsZero)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "All mass components are zero — this load will have no effect.");

        da.SetData(_outLumpedMassLoad, new GH_SgLumpedMassLoad(lumpedMass));
    }
}

