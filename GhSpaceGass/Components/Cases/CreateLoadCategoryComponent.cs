using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;

namespace GhSpaceGass.Components.Cases;

public class CreateLoadCategoryComponent : GH_Component
{
    private int _inName;
    private int _inNotes;
    
    private int _outLoadCategory;

    public CreateLoadCategoryComponent()
        : base("SG Load Category", "sgLoadCat",
            "Create a SpaceGass load category — a classification label for loads (e.g. Dead, Live, Wind).",
            "SpaceGass", "4 | Cases")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.tertiary;
    protected override Bitmap Icon => Icons.IconFactory.LoadCategory();
    public override Guid ComponentGuid => new("D57548D7-849D-4473-AEB0-25787A0A4BBB");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inName = pManager.AddTextParameter("Name", "N",
            "The load category title (e.g., \"Dead\", \"Live\", \"Wind\").",
            GH_ParamAccess.item);
        _inNotes = pManager.AddTextParameter("Notes", "Nt",
            "Optional descriptive notes for the load category.",
            GH_ParamAccess.item);

        pManager[_inNotes].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outLoadCategory = pManager.AddParameter(new Param_SgLoadCategory(),
            "Load Category", "Cat",
            "The SpaceGass load category.", 
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        string name = null;
        string notes = null;

        if (!da.GetData(_inName, ref name)) return;
        da.GetData(_inNotes, ref notes);

        if (string.IsNullOrWhiteSpace(name))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Load category name cannot be empty.");
            return;
        }

        var category = new SgLoadCategoryData(name, notes);
        da.SetData(_outLoadCategory, new GH_SgLoadCategory(category));
    }
}