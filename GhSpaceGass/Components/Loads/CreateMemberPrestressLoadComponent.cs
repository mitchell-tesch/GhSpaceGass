using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace GhSpaceGass.Components.Loads;

public class CreateMemberPrestressLoadComponent : GH_Component
{
    private int _inLine;
    private int _inLoadCase;
    private int _inLoadCategory;
    private int _inPrestress;

    private int _outPrestressLoad;

    public CreateMemberPrestressLoadComponent()
        : base("SG Member Prestress Load", "sgPrestress",
            "Create a SpaceGass member prestress load (axial prestress force on a member).",
            "SpaceGass", "5 | Loads")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.tertiary;
    protected override Bitmap Icon => Icons.IconFactory.PrestressLoad();
    public override Guid ComponentGuid => new("A3E70C04-B8A3-4D22-9F1D-7E6A5C4B3D24");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inLine = pManager.AddLineParameter("Member", "M",
            "The member line this prestress is applied to.",
            GH_ParamAccess.item);
        _inLoadCase = pManager.AddParameter(new Param_SgLoadCase(),
            "Load Case", "LC",
            "The load case this load belongs to.",
            GH_ParamAccess.item);
        _inLoadCategory = pManager.AddParameter(new Param_SgLoadCategory(),
            "Load Category", "Cat",
            "Optional load category.",
            GH_ParamAccess.item);
        _inPrestress = pManager.AddNumberParameter("Prestress", "P",
            "Prestress force applied to the member (default: 0).",
            GH_ParamAccess.item, 0.0);

        pManager[_inLoadCategory].Optional = true;
        pManager[_inPrestress].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outPrestressLoad = pManager.AddParameter(new Param_SgMemberPrestressLoad(),
            "Prestress Load", "PL",
            "The SpaceGass member prestress load.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        var line = Line.Unset;
        GH_SgLoadCase loadCaseGoo = null;
        GH_SgLoadCategory loadCategoryGoo = null;
        double prestress = 0;

        if (!da.GetData(_inLine, ref line)) return;
        if (!da.GetData(_inLoadCase, ref loadCaseGoo)) return;
        da.GetData(_inLoadCategory, ref loadCategoryGoo);
        da.GetData(_inPrestress, ref prestress);

        if (loadCaseGoo?.Value == null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid load case input.");
            return;
        }

        var memberStart = new SgPoint3D(line.From.X, line.From.Y, line.From.Z);
        var memberEnd = new SgPoint3D(line.To.X, line.To.Y, line.To.Z);
        var category = loadCategoryGoo?.Value;

        var prestressLoad = new SgMemberPrestressLoadData(
            memberStart, memberEnd, loadCaseGoo.Value, prestress, category);

        if (prestressLoad.IsZero)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "Prestress is zero — this load will have no effect.");

        da.SetData(_outPrestressLoad, new GH_SgMemberPrestressLoad(prestressLoad));
    }
}

