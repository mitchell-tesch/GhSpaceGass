using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Helpers;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Rhino.Geometry;
using SpaceGassApi.Models;

namespace GhSpaceGass.Components.Loads;

public class CreateMemberDistributedLoadComponent : GH_Component
{
    private int _inAxes;
    private int _inEndPos;
    private int _inFxEnd;
    private int _inFxStart;
    private int _inFyEnd;
    private int _inFyStart;
    private int _inFzEnd;
    private int _inFzStart;
    private int _inLine;
    private int _inLoadCase;
    private int _inLoadCategory;
    private int _inMxEnd;
    private int _inMxStart;
    private int _inMyEnd;
    private int _inMyStart;
    private int _inMzEnd;
    private int _inMzStart;
    private int _inPosUnits;
    private int _inStartPos;
    
    private int _outDistLoad;

    public CreateMemberDistributedLoadComponent()
        : base("SG Member Distributed Load", "sgDistLoad",
            "Create a SpaceGass member distributed load. Defaults to local member axes.",
            "SpaceGass", "5 | Loads")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.tertiary;
    protected override Bitmap Icon => Icons.IconFactory.DistributedLoad();
    public override Guid ComponentGuid => new("89FCACC5-8DF1-4C6A-84AB-E28FB3BAD9CF");

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
        _inFxStart = pManager.AddNumberParameter("Fx Start", "FxS",
            "Force intensity in X direction at start of loaded region (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inFyStart = pManager.AddNumberParameter("Fy Start", "FyS",
            "Force intensity in Y direction at start of loaded region (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inFzStart = pManager.AddNumberParameter("Fz Start", "FzS",
            "Force intensity in Z direction at start of loaded region (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inFxEnd = pManager.AddNumberParameter("Fx End", "FxE",
            "Force intensity in X direction at end of loaded region (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inFyEnd = pManager.AddNumberParameter("Fy End", "FyE",
            "Force intensity in Y direction at end of loaded region (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inFzEnd = pManager.AddNumberParameter("Fz End", "FzE",
            "Force intensity in Z direction at end of loaded region (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inMxStart = pManager.AddNumberParameter("Mx Start", "MxS",
            "Moment intensity about X axis at start of loaded region (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inMyStart = pManager.AddNumberParameter("My Start", "MyS",
            "Moment intensity about Y axis at start of loaded region (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inMzStart = pManager.AddNumberParameter("Mz Start", "MzS",
            "Moment intensity about Z axis at start of loaded region (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inMxEnd = pManager.AddNumberParameter("Mx End", "MxE",
            "Moment intensity about X axis at end of loaded region (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inMyEnd = pManager.AddNumberParameter("My End", "MyE",
            "Moment intensity about Y axis at end of loaded region (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inMzEnd = pManager.AddNumberParameter("Mz End", "MzE",
            "Moment intensity about Z axis at end of loaded region (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inStartPos = pManager.AddNumberParameter("Start Position", "SP",
            "Start position of loaded region along the member (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inEndPos = pManager.AddNumberParameter("End Position", "EP",
            "End position of loaded region along the member (default: 100).",
            GH_ParamAccess.item, 100.0);
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
        pManager[_inFxStart].Optional = true;
        pManager[_inFyStart].Optional = true;
        pManager[_inFzStart].Optional = true;
        pManager[_inFxEnd].Optional = true;
        pManager[_inFyEnd].Optional = true;
        pManager[_inFzEnd].Optional = true;
        pManager[_inMxStart].Optional = true;
        pManager[_inMyStart].Optional = true;
        pManager[_inMzStart].Optional = true;
        pManager[_inMxEnd].Optional = true;
        pManager[_inMyEnd].Optional = true;
        pManager[_inMzEnd].Optional = true;
        pManager[_inStartPos].Optional = true;
        pManager[_inEndPos].Optional = true;
        pManager[_inPosUnits].Optional = true;
        pManager[_inAxes].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outDistLoad = pManager.AddParameter(new Param_SgMemberDistributedLoad(),
            "Distributed Load", "DL",
            "The SpaceGass member distributed load.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        var line = Line.Unset;
        GH_SgLoadCase loadCaseGoo = null;
        GH_SgLoadCategory loadCategoryGoo = null;
        double fxStart = 0, fyStart = 0, fzStart = 0;
        double fxEnd = 0, fyEnd = 0, fzEnd = 0;
        double mxStart = 0, myStart = 0, mzStart = 0;
        double mxEnd = 0, myEnd = 0, mzEnd = 0;
        double startPos = 0, endPos = 100;
        int posUnits = 1, axes = 1;

        if (!da.GetData(_inLine, ref line)) return;
        if (!da.GetData(_inLoadCase, ref loadCaseGoo)) return;
        da.GetData(_inLoadCategory, ref loadCategoryGoo);
        da.GetData(_inFxStart, ref fxStart);
        da.GetData(_inFyStart, ref fyStart);
        da.GetData(_inFzStart, ref fzStart);
        da.GetData(_inFxEnd, ref fxEnd);
        da.GetData(_inFyEnd, ref fyEnd);
        da.GetData(_inFzEnd, ref fzEnd);
        da.GetData(_inMxStart, ref mxStart);
        da.GetData(_inMyStart, ref myStart);
        da.GetData(_inMzStart, ref mzStart);
        da.GetData(_inMxEnd, ref mxEnd);
        da.GetData(_inMyEnd, ref myEnd);
        da.GetData(_inMzEnd, ref mzEnd);
        da.GetData(_inStartPos, ref startPos);
        da.GetData(_inEndPos, ref endPos);
        da.GetData(_inPosUnits, ref posUnits);
        da.GetData(_inAxes, ref axes);

        if (loadCaseGoo?.Value == null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid load case input.");
            return;
        }

        // Map integer inputs to API enums
        var positionUnits = posUnits == 0 ? LoadPositionUnits.Actual : LoadPositionUnits.Percent;
        var loadAxes = axes == 0 ? LoadAxes.Local : LoadAxes.GlobalProjected;

        var memberStart = new SgPoint3D(line.From.X, line.From.Y, line.From.Z);
        var memberEnd = new SgPoint3D(line.To.X, line.To.Y, line.To.Z);
        var category = loadCategoryGoo?.Value;

        var distLoad = new SgMemberDistributedLoadData(
            memberStart, memberEnd, loadCaseGoo.Value,
            fxStart, fyStart, fzStart,
            fxEnd, fyEnd, fzEnd,
            startPos, endPos,
            positionUnits, loadAxes, category,
            mxStart, myStart, mzStart,
            mxEnd, myEnd, mzEnd);

        if (distLoad.IsZero)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "All force and moment components are zero — this load will have no effect.");

        da.SetData(_outDistLoad, new GH_SgMemberDistributedLoad(distLoad));
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        document.ScheduleSolution(0, doc => ValueListHelper.AutoCreateOnPlacement(this, doc));
    }

}