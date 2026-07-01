using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Helpers;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Rhino.Geometry;
using SpaceGassApi.Models;

namespace GhSpaceGass.Components.Loads;

public class CreateMemberConcentratedLoadComponent : GH_Component
{
    private int _inAxes;
    private int _inFx;
    private int _inFy;
    private int _inFz;
    private int _inLine;
    private int _inLoadCase;
    private int _inLoadCategory;
    private int _inMx;
    private int _inMy;
    private int _inMz;
    private int _inPosition;
    private int _inPosUnits;

    private int _outConcLoad;

    public CreateMemberConcentratedLoadComponent()
        : base("SG Member Concentrated Load", "sgConcLoad",
            "Create a SpaceGass member concentrated load (point force/moment at a position along a member). Defaults to local member axes.",
            "SpaceGass", "5 | Loads")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.tertiary;
    protected override Bitmap Icon => Icons.IconFactory.ConcentratedLoad();
    public override Guid ComponentGuid => new("A3E70C03-B8A3-4D22-9F1D-7E6A5C4B3D23");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inLine = pManager.AddLineParameter("Member", "M",
            "The member line this load is applied to.",
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
            "Concentrated force in X direction (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inFy = pManager.AddNumberParameter("Fy", "Fy",
            "Concentrated force in Y direction (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inFz = pManager.AddNumberParameter("Fz", "Fz",
            "Concentrated force in Z direction (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inMx = pManager.AddNumberParameter("Mx", "Mx",
            "Concentrated moment about X axis (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inMy = pManager.AddNumberParameter("My", "My",
            "Concentrated moment about Y axis (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inMz = pManager.AddNumberParameter("Mz", "Mz",
            "Concentrated moment about Z axis (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inPosition = pManager.AddNumberParameter("Position", "Pos",
            "Position along the member where the load is applied (default: 50).",
            GH_ParamAccess.item, 50.0);
        _inPosUnits = pManager.AddParameter(
            new Param_SgIntegerOption("Position Units", ValueListHelper.PositionUnitsOptions,
                defaultIndex: 1, defaultValue: 1, autoCreate: true),
            "Position Units", "PU",
            "Position units (Actual=0, Percent=1).\n" +
            "Default: Percent.",
            GH_ParamAccess.item);
        _inAxes = pManager.AddParameter(
            new Param_SgIntegerOption("Axes", ValueListHelper.LoadAxesOptions,
                defaultValue: 0, autoCreate: true),
            "Axes", "Ax",
            "Load axis system (Local=0, Global=1). Default: Local.",
            GH_ParamAccess.item);

        pManager[_inLoadCategory].Optional = true;
        pManager[_inFx].Optional = true;
        pManager[_inFy].Optional = true;
        pManager[_inFz].Optional = true;
        pManager[_inMx].Optional = true;
        pManager[_inMy].Optional = true;
        pManager[_inMz].Optional = true;
        pManager[_inPosition].Optional = true;
        pManager[_inPosUnits].Optional = true;
        pManager[_inAxes].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outConcLoad = pManager.AddParameter(new Param_SgMemberConcentratedLoad(),
            "Concentrated Load", "CL",
            "The SpaceGass member concentrated load.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        var line = Line.Unset;
        GH_SgLoadCase loadCaseGoo = null;
        GH_SgLoadCategory loadCategoryGoo = null;
        double fx = 0, fy = 0, fz = 0;
        double mx = 0, my = 0, mz = 0;
        double position = 50;
        int posUnits = 1, axes = 0;

        if (!da.GetData(_inLine, ref line)) return;
        if (!da.GetData(_inLoadCase, ref loadCaseGoo)) return;
        da.GetData(_inLoadCategory, ref loadCategoryGoo);
        da.GetData(_inFx, ref fx);
        da.GetData(_inFy, ref fy);
        da.GetData(_inFz, ref fz);
        da.GetData(_inMx, ref mx);
        da.GetData(_inMy, ref my);
        da.GetData(_inMz, ref mz);
        da.GetData(_inPosition, ref position);
        da.GetData(_inPosUnits, ref posUnits);
        da.GetData(_inAxes, ref axes);

        if (loadCaseGoo?.Value == null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid load case input.");
            return;
        }

        var positionUnits = posUnits == 0 ? LoadPositionUnits.Actual : LoadPositionUnits.Percent;
        var loadAxes = axes == 0 ? LoadAxes.Local : LoadAxes.GlobalProjected;

        var memberStart = new SgPoint3D(line.From.X, line.From.Y, line.From.Z);
        var memberEnd = new SgPoint3D(line.To.X, line.To.Y, line.To.Z);
        var category = loadCategoryGoo?.Value;

        var concLoad = new SgMemberConcentratedLoadData(
            memberStart, memberEnd, loadCaseGoo.Value,
            fx, fy, fz, mx, my, mz,
            position, positionUnits, loadAxes, category);

        if (concLoad.IsZero)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "All force and moment components are zero — this load will have no effect.");

        da.SetData(_outConcLoad, new GH_SgMemberConcentratedLoad(concLoad));
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        document.ScheduleSolution(0, doc => ValueListHelper.AutoCreateOnPlacement(this, doc));
    }
}

