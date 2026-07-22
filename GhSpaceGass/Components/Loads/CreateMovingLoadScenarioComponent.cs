using System;
using System.Collections.Generic;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Components.Loads;

public class CreateMovingLoadScenarioComponent : GH_Component
{
    private int _inCombineWith;
    private int _inInclude;
    private int _inLoadCaseFactors;
    private int _inLoads;
    private int _inName;
    private int _inScenarioFactors;
    private int _inStartingCombinationCases;
    private int _inStartingLoadCase;
    private int _inTimeInterval;

    private int _outScenario;

    public CreateMovingLoadScenarioComponent()
        : base("SG Moving Load Scenario", "sgMovLoadScen",
            "Create a SpaceGass moving load scenario — a named container for one or more moving " +
            "loads (each a vehicle or pressure travelling along a travel path). Feed the moving " +
            "loads in via the Loads input. Optional combination entries (Combine With Load " +
            "Cases + Load Case Factors + Scenario Factors) describe how the generated moving-load " +
            "load cases combine with existing load cases in the model.",
            "SpaceGass", "5 | Loads")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.senary;
    protected override Bitmap Icon => Icons.IconFactory.MovingLoadScenario();
    public override Guid ComponentGuid => new("A3F5C1B2-7D6E-4A2C-9E4B-8F1C6B5A3D2E");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inName = pManager.AddTextParameter("Name", "N",
            "The moving load scenario title (e.g., \"Truck Left Lane\").",
            GH_ParamAccess.item);
        _inLoads = pManager.AddParameter(new Param_SgMovingLoad(),
            "Loads", "L",
            "The moving loads (vehicle or pressure + travel path + optional factors) that make " +
            "up this scenario. A scenario with no moving loads is legal but produces no " +
            "generated load cases and Assemble Model warns about it.",
            GH_ParamAccess.list);
        _inStartingLoadCase = pManager.AddParameter(new Param_SgLoadCase(),
            "Starting Load Case", "LC",
            "Optional load case that the first generated moving-load load case is anchored to. " +
            "When omitted, SpaceGass picks the anchor automatically.",
            GH_ParamAccess.item);
        _inTimeInterval = pManager.AddNumberParameter("Time Interval", "TI",
            "Optional time step (> 0) between the load cases SpaceGass generates for this " +
            "scenario. Only used when the analysis engine treats the scenario as a time series.",
            GH_ParamAccess.item);
        _inInclude = pManager.AddBooleanParameter("Include", "I?",
            "When true, the scenario is picked up by analysis. When false, SpaceGass keeps the " +
            "scenario definition but skips it during runs. Defaults to true.",
            GH_ParamAccess.item, true);
        _inCombineWith = pManager.AddGenericParameter("Combine With Load Cases", "cLC",
            "Optional list of load cases (primary Load Cases and/or Combination Load Cases) that " +
            "the generated moving-load load cases combine with.",
            GH_ParamAccess.list);
        _inLoadCaseFactors = pManager.AddNumberParameter("Load Case Factors", "LCF",
            "Factors applied to each combined load case. Must match the length of Combine With Load " +
            "Cases when provided. Defaults to 1.0 for every entry when omitted.",
            GH_ParamAccess.list);
        _inScenarioFactors = pManager.AddNumberParameter("Scenario Factors", "SF",
            "Factors applied to the scenario at each combination entry. Must match the length of " +
            "Combine With Load Cases when provided. Defaults to 1.0 for every entry when omitted.",
            GH_ParamAccess.list);
        _inStartingCombinationCases = pManager.AddIntegerParameter("Starting Combination Cases", "SCC",
            "Optional starting SpaceGass combination-case ID for each combination entry. Must match " +
            "the length of Combine With Load Cases when provided. Use 0 (or omit the list) to let " +
            "SpaceGass assign the ID. Useful when appending to an existing model to avoid " +
            "collisions with pre-existing combination cases (ADR-0017).",
            GH_ParamAccess.list);

        pManager[_inLoads].Optional = true;
        pManager[_inStartingLoadCase].Optional = true;
        pManager[_inTimeInterval].Optional = true;
        pManager[_inInclude].Optional = true;
        pManager[_inCombineWith].Optional = true;
        pManager[_inLoadCaseFactors].Optional = true;
        pManager[_inScenarioFactors].Optional = true;
        pManager[_inStartingCombinationCases].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outScenario = pManager.AddParameter(new Param_SgMovingLoadScenario(),
            "Moving Load Scenario", "MLS",
            "The SpaceGass moving load scenario.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        string name = null;
        GH_SgLoadCase startingLcGoo = null;
        double timeInterval = 0;
        var timeIntervalProvided = false;
        var include = true;

        if (!da.GetData(_inName, ref name) || string.IsNullOrWhiteSpace(name))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Moving load scenario name cannot be empty.");
            return;
        }

        da.GetData(_inStartingLoadCase, ref startingLcGoo);
        timeIntervalProvided = da.GetData(_inTimeInterval, ref timeInterval);
        da.GetData(_inInclude, ref include);

        if (timeIntervalProvided && timeInterval <= 0)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Time Interval must be greater than zero.");
            return;
        }

        // Loads (list of Moving Load Goo)
        var loadGoos = new List<GH_SgMovingLoad>();
        da.GetDataList(_inLoads, loadGoos);
        var loads = new List<SgMovingLoadData>();
        foreach (var goo in loadGoos)
            if (goo?.Value != null)
                loads.Add(goo.Value);

        // Combinations (parallel lists)
        var combineGoos = new List<IGH_Goo>();
        var loadCaseFactors = new List<double>();
        var scenarioFactors = new List<double>();
        var startingCombinationCases = new List<int>();
        da.GetDataList(_inCombineWith, combineGoos);
        da.GetDataList(_inLoadCaseFactors, loadCaseFactors);
        da.GetDataList(_inScenarioFactors, scenarioFactors);
        da.GetDataList(_inStartingCombinationCases, startingCombinationCases);

        var combinations = new List<SgMovingLoadCombinationEntry>();
        if (combineGoos.Count > 0)
        {
            if (loadCaseFactors.Count > 0 && loadCaseFactors.Count != combineGoos.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Load Case Factors count ({loadCaseFactors.Count}) must match " +
                    $"Combine With Load Cases count ({combineGoos.Count}).");
                return;
            }

            if (scenarioFactors.Count > 0 && scenarioFactors.Count != combineGoos.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Scenario Factors count ({scenarioFactors.Count}) must match " +
                    $"Combine With Load Cases count ({combineGoos.Count}).");
                return;
            }

            if (startingCombinationCases.Count > 0 && startingCombinationCases.Count != combineGoos.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Starting Combination Cases count ({startingCombinationCases.Count}) must match " +
                    $"Combine With Load Cases count ({combineGoos.Count}).");
                return;
            }

            for (var i = 0; i < startingCombinationCases.Count; i++)
                if (startingCombinationCases[i] < 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        $"Starting Combination Case at index {i} must be \u2265 0.");
                    return;
                }

            for (var i = 0; i < combineGoos.Count; i++)
            {
                var lcf = i < loadCaseFactors.Count ? loadCaseFactors[i] : 1.0;
                var sf = i < scenarioFactors.Count ? scenarioFactors[i] : 1.0;
                int? scc = null;
                if (i < startingCombinationCases.Count && startingCombinationCases[i] > 0)
                    scc = startingCombinationCases[i];
                var goo = combineGoos[i];

                if (goo is GH_SgLoadCase lcGoo && lcGoo.Value != null)
                {
                    combinations.Add(new SgMovingLoadCombinationEntry(lcGoo.Value, lcf, sf, scc));
                }
                else if (goo is GH_SgCombinationLoadCase clcGoo && clcGoo.Value != null)
                {
                    combinations.Add(new SgMovingLoadCombinationEntry(clcGoo.Value, lcf, sf, scc));
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        $"Combine With Load Cases entry at index {i} is not a valid Load Case or " +
                        "Combination Load Case.");
                    return;
                }
            }
        }
        else if (loadCaseFactors.Count > 0 || scenarioFactors.Count > 0 ||
                 startingCombinationCases.Count > 0)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "Load Case Factors, Scenario Factors and/or Starting Combination Cases were provided " +
                "but Combine With Load Cases is empty — they will be ignored.");
        }

        var scenario = new SgMovingLoadScenarioData(
            name,
            startingLcGoo?.Value,
            timeIntervalProvided ? timeInterval : null,
            include,
            combinations.Count > 0 ? combinations : null,
            loads.Count > 0 ? loads : null);

        da.SetData(_outScenario, new GH_SgMovingLoadScenario(scenario));
    }
}
