using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;

namespace GhSpaceGass.Components.Loads;

public class CreateSelfWeightLoadComponent : GH_Component
{
    private int _inAccX;
    private int _inAccY;
    private int _inAccZ;
    private int _inLoadCase;
    private int _inLoadCategory;
    
    private int _outSelfWeight;

    public CreateSelfWeightLoadComponent()
        : base("SG Self-Weight Load", "sgSelfWeight",
            "Create a SpaceGass self-weight load. Applies gravity acceleration to all members in the load case.",
            "SpaceGass", "5 | Loads")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.primary;
    protected override Bitmap Icon => Icons.IconFactory.SelfWeight();
    public override Guid ComponentGuid => new("C95D2CFD-BD8A-43EB-A0DA-54AF690B194C");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inLoadCase = pManager.AddParameter(new Param_SgLoadCase(),
            "Load Case", "LC",
            "The load case this self-weight load belongs to.",
            GH_ParamAccess.item);
        _inLoadCategory = pManager.AddParameter(new Param_SgLoadCategory(),
            "Load Category", "Cat",
            "Optional load category (e.g., Dead).",
            GH_ParamAccess.item);
        _inAccX = pManager.AddNumberParameter("Acceleration X", "AccX",
            "Gravity acceleration in global X direction, in job acceleration units (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inAccY = pManager.AddNumberParameter("Acceleration Y", "AccY",
            "Gravity acceleration in global Y direction, in job acceleration units (default: -9.81).",
            GH_ParamAccess.item, -9.81);
        _inAccZ = pManager.AddNumberParameter("Acceleration Z", "AccZ",
            "Gravity acceleration in global Z direction, in job acceleration units (default: 0).",
            GH_ParamAccess.item, 0.0);

        pManager[_inLoadCategory].Optional = true;
        pManager[_inAccX].Optional = true;
        pManager[_inAccY].Optional = true;
        pManager[_inAccZ].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outSelfWeight = pManager.AddParameter(new Param_SgSelfWeightLoad(),
            "Self-Weight Load", "SW",
            "The SpaceGass self-weight load.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        GH_SgLoadCase loadCaseGoo = null;
        GH_SgLoadCategory loadCategoryGoo = null;
        double accX = 0, accY = -9.81, accZ = 0;

        if (!da.GetData(_inLoadCase, ref loadCaseGoo)) return;
        da.GetData(_inLoadCategory, ref loadCategoryGoo);
        da.GetData(_inAccX, ref accX);
        da.GetData(_inAccY, ref accY);
        da.GetData(_inAccZ, ref accZ);

        if (loadCaseGoo?.Value == null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid load case input.");
            return;
        }

        var category = loadCategoryGoo?.Value;
        var selfWeight = new SgSelfWeightLoadData(
            loadCaseGoo.Value, accX, accY, accZ, category);

        if (selfWeight.IsZero)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "All acceleration components are zero — this load will have no effect.");

        da.SetData(_outSelfWeight, new GH_SgSelfWeightLoad(selfWeight));
    }
}