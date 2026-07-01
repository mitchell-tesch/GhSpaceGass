using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;

namespace GhSpaceGass.Components.Cases;

public class CreateLoadCaseComponent : GH_Component
{
    private int _inName;
    private int _inNotes;
    
    private int _outLoadCase;

    public CreateLoadCaseComponent()
        : base("SG Load Case", "sgLoadCase",
            "Create a SpaceGass load case definition.",
            "SpaceGass", "4 | Cases")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.primary;
    protected override Bitmap Icon => Icons.IconFactory.LoadCase();
    public override Guid ComponentGuid => new("0E398524-C47E-4D63-AB4E-95E1833343A8");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inName = pManager.AddTextParameter("Name", "N",
            "The load case title (e.g., \"Dead Load\", \"Live Load\").",
            GH_ParamAccess.item);
        _inNotes = pManager.AddTextParameter("Notes", "Nt",
            "Optional descriptive notes for the load case.",
            GH_ParamAccess.item);

        pManager[_inNotes].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outLoadCase = pManager.AddParameter(new Param_SgLoadCase(),
            "Load Case", "LC",
            "The SpaceGass load case.",
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
                "Load case name cannot be empty.");
            return;
        }

        var loadCase = new SgLoadCaseData(name, notes);
        da.SetData(_outLoadCase, new GH_SgLoadCase(loadCase));
    }
}