using System;
using System.Collections.Generic;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Components.Cases;

public class CreateCombinationLoadCaseComponent : GH_Component
{
    private int _inFactors;
    private int _inLoadCases;
    private int _inName;
    private int _inNotes;
    
    private int _outCombo;

    public CreateCombinationLoadCaseComponent()
        : base("SG Combination Load Case", "sgCombLC",
            "Create a SpaceGass combination load case from primary load cases, other combinations, and scale factors.",
            "SpaceGass", "4 | Cases")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.secondary;
    protected override Bitmap Icon => Icons.IconFactory.CombinationLoadCase();
    public override Guid ComponentGuid => new("C713A7A6-EF88-48A6-8924-E15D35CD630F");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inName = pManager.AddTextParameter("Name", "N",
            "The combination load case name (e.g., \"ULS\").",
            GH_ParamAccess.item);
        _inLoadCases = pManager.AddGenericParameter("Load Cases", "LC",
            "The constituent load cases (primary Load Cases and/or Combination Load Cases).",
            GH_ParamAccess.list);
        _inFactors = pManager.AddNumberParameter("Factors", "F",
            "The scale factors matching each load case (e.g., 1.2, 1.5).",
            GH_ParamAccess.list);
        _inNotes = pManager.AddTextParameter("Notes", "Nt",
            "Optional descriptive notes.",
            GH_ParamAccess.item);

        pManager[_inNotes].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outCombo = pManager.AddParameter(new Param_SgCombinationLoadCase(), 
            "Combination Load Case", "CLC",
            "The SpaceGass combination load case.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        string name = null;
        var gooList = new List<IGH_Goo>();
        var factors = new List<double>();
        string notes = null;

        if (!da.GetData(_inName, ref name) || string.IsNullOrWhiteSpace(name))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Name is required.");
            return;
        }

        if (!da.GetDataList(_inLoadCases, gooList) || gooList.Count == 0)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "At least one Load Case is required.");
            return;
        }

        if (!da.GetDataList(_inFactors, factors) || factors.Count == 0)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "At least one Factor is required.");
            return;
        }

        da.GetData(_inNotes, ref notes);

        if (gooList.Count != factors.Count)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                $"Load Cases count ({gooList.Count}) must match Factors count ({factors.Count}).");
            return;
        }

        // Build constituents — accept both GH_SgLoadCase and GH_SgCombinationLoadCase
        var constituents = new List<SgCombinationConstituent>(gooList.Count);
        for (var i = 0; i < gooList.Count; i++)
        {
            var goo = gooList[i];
            if (goo is GH_SgLoadCase lcGoo && lcGoo.Value != null)
            {
                constituents.Add(new SgCombinationConstituent(lcGoo.Value, factors[i]));
            }
            else if (goo is GH_SgCombinationLoadCase clcGoo && clcGoo.Value != null)
            {
                constituents.Add(new SgCombinationConstituent(clcGoo.Value, factors[i]));
            }
            else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Input at index {i} is not a valid Load Case or Combination Load Case.");
                return;
            }
        }

        var combo = new SgCombinationLoadCaseData(name, constituents, notes);
        da.SetData(_outCombo, new GH_SgCombinationLoadCase(combo));
    }
}