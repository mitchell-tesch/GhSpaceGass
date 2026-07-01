using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Helpers;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using SpaceGassApi.Models;

namespace GhSpaceGass.Components.Loads;

public class CreatePlatePressureLoadComponent : GH_Component
{
    private int _inAxes;
    private int _inLoadCase;
    private int _inLoadCategory;
    private int _inPlate;
    private int _inPx;
    private int _inPy;
    private int _inPz;

    private int _outPressureLoad;

    public CreatePlatePressureLoadComponent()
        : base("SG Plate Pressure Load", "sgPlatePressure",
            "Create a SpaceGass plate pressure load. Defaults to local plate axes.",
            "SpaceGass", "5 | Loads")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.quarternary;
    protected override Bitmap Icon => Icons.IconFactory.PlatePressureLoad();
    public override Guid ComponentGuid => new("A3E70C08-B8A3-4D22-9F1D-7E6A5C4B3D28");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inPlate = pManager.AddParameter(new Param_SgPlate(),
            "Plate", "P",
            "The plate element this pressure is applied to.",
            GH_ParamAccess.item);
        _inLoadCase = pManager.AddParameter(new Param_SgLoadCase(),
            "Load Case", "LC",
            "The load case this load belongs to.",
            GH_ParamAccess.item);
        _inLoadCategory = pManager.AddParameter(new Param_SgLoadCategory(),
            "Load Category", "Cat",
            "Optional load category.",
            GH_ParamAccess.item);
        _inPx = pManager.AddNumberParameter("Px", "Px",
            "Pressure in X direction (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inPy = pManager.AddNumberParameter("Py", "Py",
            "Pressure in Y direction (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inPz = pManager.AddNumberParameter("Pz", "Pz",
            "Pressure in Z direction (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inAxes = pManager.AddParameter(
            new Param_SgIntegerOption("Axes", ValueListHelper.LoadAxesOptions,
                defaultValue: 0, autoCreate: true),
            "Axes", "Ax",
            "Load axis system (Local=0, Global=1). Default: Local.",
            GH_ParamAccess.item);

        pManager[_inLoadCategory].Optional = true;
        pManager[_inPx].Optional = true;
        pManager[_inPy].Optional = true;
        pManager[_inPz].Optional = true;
        pManager[_inAxes].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outPressureLoad = pManager.AddParameter(new Param_SgPlatePressureLoad(),
            "Plate Pressure Load", "PPL",
            "The SpaceGass plate pressure load.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        GH_SgPlate plateGoo = null;
        GH_SgLoadCase loadCaseGoo = null;
        GH_SgLoadCategory loadCategoryGoo = null;
        double px = 0, py = 0, pz = 0;
        int axes = 0;

        if (!da.GetData(_inPlate, ref plateGoo) || plateGoo?.Value == null) return;
        if (!da.GetData(_inLoadCase, ref loadCaseGoo)) return;
        da.GetData(_inLoadCategory, ref loadCategoryGoo);
        da.GetData(_inPx, ref px);
        da.GetData(_inPy, ref py);
        da.GetData(_inPz, ref pz);
        da.GetData(_inAxes, ref axes);

        if (loadCaseGoo?.Value == null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid load case input.");
            return;
        }

        var loadAxes = axes == 0 ? LoadAxes.Local : LoadAxes.GlobalProjected;
        var category = loadCategoryGoo?.Value;

        var pressureLoad = new SgPlatePressureLoadData(
            plateGoo.Value.Nodes, loadCaseGoo.Value, px, py, pz, loadAxes, category);

        if (pressureLoad.IsZero)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "All pressure components are zero — this load will have no effect.");

        da.SetData(_outPressureLoad, new GH_SgPlatePressureLoad(pressureLoad));
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        document.ScheduleSolution(0, doc => ValueListHelper.AutoCreateOnPlacement(this, doc));
    }
}

