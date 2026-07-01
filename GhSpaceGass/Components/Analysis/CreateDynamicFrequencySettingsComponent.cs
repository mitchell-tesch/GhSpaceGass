using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Helpers;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using SpaceGassApi.Models;

namespace GhSpaceGass.Components.Analysis;

public class CreateDynamicFrequencySettingsComponent : GH_Component
{
    private int _inDrilling, _inStabilize, _inCheckNonExist, _inRetainLc;
    private int _inExtraIter, _inFreqShift, _inTolerance, _inUpperLimit, _inLowerLimit;
    private int _inModes, _inLoadCases, _inPlateType, _inSolverType;
    private int _inOptMethod, _inOptAxis, _inOptX, _inOptY, _inOptZ;
    
    private int _outSettings;

    public CreateDynamicFrequencySettingsComponent()
        : base("SG Dynamic Frequency Settings", "sgDynSet",
            "Create settings for Dynamic Frequency analysis.",
            "SpaceGass", "7 | Analysis")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.secondary;
    protected override Bitmap Icon => Icons.IconFactory.DynamicFrequencySettings();
    public override Guid ComponentGuid => new("6A4C38C9-DC5D-4B18-AE34-5C28F6D7F9E5");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModes = pManager.AddIntegerParameter("Modes", "M", 
            "Number of frequency modes to find.",
            GH_ParamAccess.item);
        _inLoadCases = pManager.AddTextParameter("Load Cases", "LC",
            "Comma-separated load case IDs.",
            GH_ParamAccess.item);
        _inFreqShift = pManager.AddNumberParameter("Frequency Shift", "FS",
            "Frequency shift value.",
            GH_ParamAccess.item);
        _inTolerance = pManager.AddNumberParameter("Tolerance", "Tol",
            "Convergence tolerance.",
            GH_ParamAccess.item);
        _inUpperLimit = pManager.AddNumberParameter("Upper Limit", "UL",
            "Upper frequency limit.",
            GH_ParamAccess.item);
        _inLowerLimit = pManager.AddNumberParameter("Lower Limit", "LL",
            "Lower frequency limit.",
            GH_ParamAccess.item);
        _inExtraIter = pManager.AddIntegerParameter("Extra Iterations", "EI",
            "Extra iterations.",
            GH_ParamAccess.item);
        _inPlateType = pManager.AddParameter(
            new Param_SgIntegerOption("Plate Type", ValueListHelper.PlateTypeOptions),
            "Plate Type", "PT",
            "Plate type (BCPlates=0, DLPlates=1).",
            GH_ParamAccess.item);
        _inSolverType = pManager.AddParameter(
            new Param_SgIntegerOption("Solver Type", ValueListHelper.SolverTypeOptions),
            "Solver Type", "ST",
            "Solver (Paradise=0, Wavefront=1, SGX=2).",
            GH_ParamAccess.item);
        _inDrilling = pManager.AddNumberParameter("Drilling Stiffness", "DrK",
            "Drilling stiffness.",
            GH_ParamAccess.item);
        _inStabilize = pManager.AddIntegerParameter("Stabilize Unrestrained", "SU",
            "Stabilize unrestrained nodes (0 = No, 1 = Yes).",
            GH_ParamAccess.item);
        _inCheckNonExist = pManager.AddIntegerParameter("Check Non-Existent LC", "CNE",
            "Check non-existent load cases (0 = No, 1 = Yes).",
            GH_ParamAccess.item);
        _inRetainLc = pManager.AddIntegerParameter("Retain Load Cases", "RLC",
            "Retain load cases (0 = No, 1 = Yes).",
            GH_ParamAccess.item);
        _inOptMethod = pManager.AddParameter(
            new Param_SgIntegerOption("Optimization Method", ValueListHelper.OptimizationMethodOptions),
            "Optimization Method", "OM",
            "Optimization (None=0, Auto=1, General=2, Linear=3, Circular=4).",
            GH_ParamAccess.item);
        _inOptAxis = pManager.AddParameter(
            new Param_SgIntegerOption("Optimization Axis", ValueListHelper.OptimizationAxisOptions),
            "Optimization Axis", "OA",
            "Optimization axis (X=0, Y=1, Z=2, Vector=3).",
            GH_ParamAccess.item);
        _inOptX = pManager.AddNumberParameter("Optimization X", "OX",
            "Optimization X coordinate.",
            GH_ParamAccess.item);
        _inOptY = pManager.AddNumberParameter("Optimization Y", "OY",
            "Optimization Y coordinate.",
            GH_ParamAccess.item);
        _inOptZ = pManager.AddNumberParameter("Optimization Z", "OZ",
            "Optimization Z coordinate.",
            GH_ParamAccess.item);
        
        for (var i = 0; i < pManager.ParamCount; i++)
            pManager[i].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outSettings = pManager.AddParameter(new Param_SgAnalysisSettings(),
            "Settings", "S",
            "Dynamic frequency analysis settings.",
            GH_ParamAccess.item);
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        document.ScheduleSolution(0, doc => ValueListHelper.AutoCreateOnPlacement(this, doc));
    }


    protected override void SolveInstance(IGH_DataAccess da)
    {
        var s = new DynamicFrequencySettingsUpdate();
        var iv = 0;
        double dv = 0;
        string sv = null;
        
        if (da.GetData(_inModes, ref iv)) s.Modes = iv;
        if (da.GetData(_inLoadCases, ref sv)) s.LoadCases = sv;
        if (da.GetData(_inFreqShift, ref dv)) s.FrequencyShift = (float?)dv;
        if (da.GetData(_inTolerance, ref dv)) s.Tolerance = (float?)dv;
        if (da.GetData(_inUpperLimit, ref dv)) s.UpperLimit = (float?)dv;
        if (da.GetData(_inLowerLimit, ref dv)) s.LowerLimit = (float?)dv;
        if (da.GetData(_inExtraIter, ref iv)) s.ExtraIterations = iv != 0;
        if (da.GetData(_inPlateType, ref iv)) s.PlateType = (PlateType)iv;
        if (da.GetData(_inSolverType, ref iv)) s.SolverType = (SolverType)iv;
        if (da.GetData(_inDrilling, ref dv)) s.DrillingStiffness = (float?)dv;
        if (da.GetData(_inStabilize, ref iv)) s.StabilizeUnrestrainedNodes = iv != 0;
        if (da.GetData(_inCheckNonExist, ref iv)) s.CheckNonExistentLoadCases = iv != 0;
        if (da.GetData(_inRetainLc, ref iv)) s.RetainLoadCases = iv != 0;
        if (da.GetData(_inOptMethod, ref iv)) s.OptimizationMethod = (AnalysisOptimizationMethod)iv;
        if (da.GetData(_inOptAxis, ref iv)) s.OptimizationAxis = (OptimizationAxis)iv;
        if (da.GetData(_inOptX, ref dv)) s.OptimizationX = (float?)dv;
        if (da.GetData(_inOptY, ref dv)) s.OptimizationY = (float?)dv;
        if (da.GetData(_inOptZ, ref dv)) s.OptimizationZ = (float?)dv;
        
        da.SetData(_outSettings, new GH_SgAnalysisSettings(SgAnalysisSettingsData.ForDynamicFrequency(s)));
    }
}