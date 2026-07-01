using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Helpers;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using SpaceGassApi.Models;

namespace GhSpaceGass.Components.Analysis;

public class CreateBucklingAnalysisSettingsComponent : GH_Component
{
    private int _inModes, _inTheory, _inAxialForce, _inLoadCases;
    private int _inOptMethod, _inOptAxis, _inOptX, _inOptY, _inOptZ;
    private int _inPlateType, _inSolverType, _inDrilling, _inTensComp;
    private int _inStabilize, _inCheckNonExist, _inRetainLc, _inReversalIter, _inExtraIter;
    private int _inTolerance, _inUpperLimit, _inLowerLimit;

    private int _outSettings;

    public CreateBucklingAnalysisSettingsComponent()
        : base("SG Buckling Analysis Settings", "sgBuckSet",
            "Create settings for Buckling analysis.",
            "SpaceGass", "7 | Analysis")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.secondary;
    protected override Bitmap Icon => Icons.IconFactory.BucklingSettings();
    public override Guid ComponentGuid => new("2C019FDA-458E-476A-9B1F-6EE37CA28953");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModes = pManager.AddIntegerParameter("Modes", "M",
            "Number of buckling modes to find.",
            GH_ParamAccess.item);
        _inTheory = pManager.AddParameter(
            new Param_SgIntegerOption("Theory", ValueListHelper.BucklingTheoryOptions),
            "Theory", "Th",
            "Buckling theory (Signcount Eigensolver=0, Classic Eigensolver=1).",
            GH_ParamAccess.item);
        _inAxialForce = pManager.AddParameter(
            new Param_SgIntegerOption("Axial Force Distribution", ValueListHelper.AxialForceDistributionOptions),
            "Axial Force Distribution", "AF",
            "Axial force distribution (Linear=0, NonLinear=1).",
            GH_ParamAccess.item);
        _inLoadCases = pManager.AddTextParameter("Load Cases", "LC",
            "Comma-separated load case IDs to analyse.",
            GH_ParamAccess.item);
        _inTolerance = pManager.AddNumberParameter("Tolerance", "Tol",
            "Convergence tolerance.",
            GH_ParamAccess.item);
        _inUpperLimit = pManager.AddNumberParameter("Upper Limit", "UL",
            "Upper limit for load factor.",
            GH_ParamAccess.item);
        _inLowerLimit = pManager.AddNumberParameter("Lower Limit", "LL",
            "Lower limit for load factor.",
            GH_ParamAccess.item);
        _inExtraIter = pManager.AddIntegerParameter("Extra Iterations", "EI",
            "Extra iterations.",
            GH_ParamAccess.item);
        _inReversalIter = pManager.AddIntegerParameter("Reversal Iterations", "RI",
            "Reversal iterations.",
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
        _inTensComp = pManager.AddParameter(
            new Param_SgIntegerOption("Tension/Compression Only", ValueListHelper.TensionCompressionOptions),
            "Tension/Compression Only", "TC",
            "T/C mode (Activated=0, No Reversal=1, Deactivated=2, Gradual Activation=3).",
            GH_ParamAccess.item);
        _inStabilize = pManager.AddBooleanParameter("Stabilize Unrestrained", "SU",
            "Stabilize unrestrained nodes.",
            GH_ParamAccess.item);
        _inCheckNonExist = pManager.AddBooleanParameter("Check Non-Existent LC", "CNE",
            "Check non-existent load cases.",
            GH_ParamAccess.item);
        _inRetainLc = pManager.AddBooleanParameter("Retain Load Cases", "RLC",
            "Retain load cases from previous run.",
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
            "Buckling analysis settings.",
            GH_ParamAccess.item);
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        document.ScheduleSolution(0, doc => ValueListHelper.AutoCreateOnPlacement(this, doc));
    }
    
    protected override void SolveInstance(IGH_DataAccess da)
    {
        var s = new BucklingSettingsUpdate();
        var iv = 0;
        double dv = 0;
        var bv = false;
        string sv = null;
        
        if (da.GetData(_inModes, ref iv)) s.Modes = iv;
        if (da.GetData(_inTheory, ref iv)) s.Theory = (BucklingTheory)iv;
        if (da.GetData(_inAxialForce, ref iv)) s.AxialForceDistribution = (AxialForceDistribution)iv;
        if (da.GetData(_inLoadCases, ref sv)) s.LoadCases = sv;
        if (da.GetData(_inTolerance, ref dv)) s.Tolerance = (float?)dv;
        if (da.GetData(_inUpperLimit, ref dv)) s.UpperLimit = (float?)dv;
        if (da.GetData(_inLowerLimit, ref dv)) s.LowerLimit = (float?)dv;
        if (da.GetData(_inExtraIter, ref iv)) s.ExtraIterations = iv != 0;
        if (da.GetData(_inReversalIter, ref iv)) s.ReversalIterations = iv;
        if (da.GetData(_inPlateType, ref iv)) s.PlateType = (PlateType)iv;
        if (da.GetData(_inSolverType, ref iv)) s.SolverType = (SolverType)iv;
        if (da.GetData(_inDrilling, ref dv)) s.DrillingStiffness = (float?)dv;
        if (da.GetData(_inTensComp, ref iv)) s.TensionCompressionOnly = (TensionCompressionOnlyMode)iv;
        if (da.GetData(_inStabilize, ref bv)) s.StabilizeUnrestrainedNodes = bv;
        if (da.GetData(_inCheckNonExist, ref bv)) s.CheckNonExistentLoadCases = bv;
        if (da.GetData(_inRetainLc, ref bv)) s.RetainLoadCases = bv;
        if (da.GetData(_inOptMethod, ref iv)) s.OptimizationMethod = (AnalysisOptimizationMethod)iv;
        if (da.GetData(_inOptAxis, ref iv)) s.OptimizationAxis = (OptimizationAxis)iv;
        if (da.GetData(_inOptX, ref dv)) s.OptimizationX = (float?)dv;
        if (da.GetData(_inOptY, ref dv)) s.OptimizationY = (float?)dv;
        if (da.GetData(_inOptZ, ref dv)) s.OptimizationZ = (float?)dv;
        
        da.SetData(_outSettings, new GH_SgAnalysisSettings(SgAnalysisSettingsData.ForBuckling(s)));
    }
}