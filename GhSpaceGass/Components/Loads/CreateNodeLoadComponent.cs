using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace GhSpaceGass.Components.Loads;

public class CreateNodeLoadComponent : GH_Component
{
    private int _inFx;
    private int _inFy;
    private int _inFz;
    private int _inLoadCase;
    private int _inLoadCategory;
    private int _inMx;
    private int _inMy;
    private int _inMz;
    private int _inPoint;
    
    private int _outNodeLoad;

    public CreateNodeLoadComponent()
        : base("SG Node Load", "sgNodeLoad",
            "Create a SpaceGass node load (concentrated force/moment at a point). Always in global axes.",
            "SpaceGass", "5 | Loads")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.secondary;
    protected override Bitmap Icon => Icons.IconFactory.NodeLoad();
    public override Guid ComponentGuid => new("B26080BA-D51A-458D-8F92-12B74814AA0F");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inPoint = pManager.AddPointParameter("Point", "P",
            "The location where the load is applied.",
            GH_ParamAccess.item);
        _inLoadCase = pManager.AddParameter(new Param_SgLoadCase(),
            "Load Case", "LC",
            "The load case this load belongs to.",
            GH_ParamAccess.item);
        _inLoadCategory = pManager.AddParameter(new Param_SgLoadCategory(),
            "Load Category", "Cat",
            "Optional load category (e.g., Dead, Live, Wind).",
            GH_ParamAccess.item);
        _inFx = pManager.AddNumberParameter("Fx", "Fx",
            "Force in global X direction (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inFy = pManager.AddNumberParameter("Fy", "Fy",
            "Force in global Y direction (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inFz = pManager.AddNumberParameter("Fz", "Fz",
            "Force in global Z direction (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inMx = pManager.AddNumberParameter("Mx", "Mx",
            "Moment about global X axis (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inMy = pManager.AddNumberParameter("My", "My",
            "Moment about global Y axis (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inMz = pManager.AddNumberParameter("Mz", "Mz",
            "Moment about global Z axis (default: 0).",
            GH_ParamAccess.item, 0.0);

        pManager[_inLoadCategory].Optional = true;
        pManager[_inFx].Optional = true;
        pManager[_inFy].Optional = true;
        pManager[_inFz].Optional = true;
        pManager[_inMx].Optional = true;
        pManager[_inMy].Optional = true;
        pManager[_inMz].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outNodeLoad = pManager.AddParameter(new Param_SgNodeLoad(),
            "Node Load", "NL",
            "The SpaceGass node load.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        var point = Point3d.Unset;
        GH_SgLoadCase loadCaseGoo = null;
        GH_SgLoadCategory loadCategoryGoo = null;
        double fx = 0, fy = 0, fz = 0;
        double mx = 0, my = 0, mz = 0;

        if (!da.GetData(_inPoint, ref point)) return;
        if (!da.GetData(_inLoadCase, ref loadCaseGoo)) return;
        da.GetData(_inLoadCategory, ref loadCategoryGoo);
        da.GetData(_inFx, ref fx);
        da.GetData(_inFy, ref fy);
        da.GetData(_inFz, ref fz);
        da.GetData(_inMx, ref mx);
        da.GetData(_inMy, ref my);
        da.GetData(_inMz, ref mz);

        if (loadCaseGoo?.Value == null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Invalid load case input.");
            return;
        }

        var sgPoint = new SgPoint3D(point.X, point.Y, point.Z);
        var category = loadCategoryGoo?.Value;
        var nodeLoad = new SgNodeLoadData(sgPoint, loadCaseGoo.Value, fx, fy, fz, mx, my, mz, category);

        if (nodeLoad.IsZero)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "All force and moment components are zero — this load will have no effect.");

        da.SetData(_outNodeLoad, new GH_SgNodeLoad(nodeLoad));
    }
}