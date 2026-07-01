using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Helpers;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using SpaceGassApi.Models;

namespace GhSpaceGass.Components.Analysis;

public class CreateStaticAnalysisSettingsComponent : GH_Component
{
    private int _inConvergence, _inDeflConv, _inResidConv, _inDampFactor, _inDampSteps;
    private int _inDrilling, _inFrameBuckling, _inTensComp, _inRotateLocal;
    private int _inLoadCases, _inLoading, _inLoadSteps, _inLoadStepIter, _inReversalIter;
    private int _inOptMethod, _inOptAxis, _inOptX, _inOptY, _inOptZ;
    private int _inPDeltaBig, _inPDeltaSmall, _inMatrixType, _inPlateType, _inSolverType;
    private int _inStabilize, _inCheckNonExist, _inRetainLc;
    
    private int _outSettings;

    public CreateStaticAnalysisSettingsComponent()
        : base("SG Static Settings", "sgStaticSet",
            "Create settings for Linear Static or Non-linear Static analysis.",
            "SpaceGass", "7 | Analysis")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.secondary;
    protected override Bitmap Icon => Icons.IconFactory.StaticSettings();
    public override Guid ComponentGuid => new("D5172A4E-E14B-439B-8D13-D4A566616265");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inLoadCases = pManager.AddTextParameter("Load Cases", "LC", 
            "Comma-separated load case IDs to analyse.", 
            GH_ParamAccess.item);
        _inLoading = pManager.AddParameter(
            new Param_SgIntegerOption("Loading", ValueListHelper.LoadingTypeOptions),
            "Loading", "Ld",
            "Loading type (Full=0, Residual=1).",
            GH_ParamAccess.item);
        _inLoadSteps = pManager.AddIntegerParameter("Load Steps", "LS",
            "Number of load steps.",
            GH_ParamAccess.item);
        _inLoadStepIter = pManager.AddIntegerParameter("Load Step Iterations", "LSI",
            "Max iterations per load step.",
            GH_ParamAccess.item);
        _inReversalIter = pManager.AddIntegerParameter("Reversal Iterations", "RI",
                "Reversal iterations.",
                GH_ParamAccess.item);
        _inConvergence = pManager.AddNumberParameter("Convergence Accuracy", "CA",
                "Convergence accuracy.",
                GH_ParamAccess.item);
        _inDeflConv = pManager.AddBooleanParameter("Deflections Convergence", "DC",
            "Check deflections convergence.",
            GH_ParamAccess.item);
        _inResidConv = pManager.AddBooleanParameter("Residuals Convergence", "RC",
            "Check residuals convergence.",
            GH_ParamAccess.item);
        _inDampFactor = pManager.AddNumberParameter("Damping Factor", "DF",
            "Damping factor.",
            GH_ParamAccess.item);
        _inDampSteps = pManager.AddIntegerParameter("Damping Steps", "DS",
            "Damping steps.",
            GH_ParamAccess.item);
        _inPDeltaBig = pManager.AddBooleanParameter("P-Delta Big", "PDB",
            "P-Delta (big) second-order effect.",
            GH_ParamAccess.item);
        _inPDeltaSmall = pManager.AddBooleanParameter("P-Delta Small", "PDs",
            "P-Delta (small) second-order effect.",
            GH_ParamAccess.item);
        _inMatrixType = pManager.AddParameter(
            new Param_SgIntegerOption("Matrix Type", ValueListHelper.MatrixTypeOptions),
            "Matrix Type", "MT",
            "Matrix type (Secant=0, Tangent=1).",
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
        _inFrameBuckling = pManager.AddBooleanParameter("Frame Buckling Check", "FBC",
            "Frame buckling check.",
            GH_ParamAccess.item);
        _inTensComp = pManager.AddParameter(
            new Param_SgIntegerOption("Tension/Compression Only", ValueListHelper.TensionCompressionOptions),
            "Tension/Compression Only", "TC",
            "T/C mode (Activated=0, No Reversal=1, Deactivated=2, Gradual Activation=3).",
            GH_ParamAccess.item);
        _inRotateLocal = pManager.AddBooleanParameter("Rotate Local Loads", "RLL",
            "Rotate local loads with deformation.",
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
            "Static analysis settings.",
            GH_ParamAccess.item);
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        document.ScheduleSolution(0, doc => ValueListHelper.AutoCreateOnPlacement(this, doc));
    }


    protected override void SolveInstance(IGH_DataAccess da)
    {
        var s = new StaticSettingsUpdate();
        string sv = null;
        if (da.GetData(_inLoadCases, ref sv)) s.LoadCases = sv;
        var iv = 0;
        if (da.GetData(_inLoading, ref iv)) s.Loading = (LoadingType)iv;
        if (da.GetData(_inLoadSteps, ref iv)) s.LoadSteps = iv;
        if (da.GetData(_inLoadStepIter, ref iv)) s.LoadStepIterations = iv;
        if (da.GetData(_inReversalIter, ref iv)) s.ReversalIterations = iv;
        double dv = 0;
        if (da.GetData(_inConvergence, ref dv)) s.ConvergenceAccuracy = (float?)dv;
        var bv = false;
        if (da.GetData(_inDeflConv, ref bv)) s.DeflectionsConvergence = bv;
        if (da.GetData(_inResidConv, ref bv)) s.ResidualsConvergence = bv;
        if (da.GetData(_inDampFactor, ref dv)) s.DampingFactor = (float?)dv;
        if (da.GetData(_inDampSteps, ref iv)) s.DampingSteps = iv;
        if (da.GetData(_inPDeltaBig, ref bv)) s.PDeltaBig = bv;
        if (da.GetData(_inPDeltaSmall, ref bv)) s.PDeltaSmall = bv;
        if (da.GetData(_inMatrixType, ref iv)) s.MatrixType = (MatrixType)iv;
        if (da.GetData(_inPlateType, ref iv)) s.PlateType = (PlateType)iv;
        if (da.GetData(_inSolverType, ref iv)) s.SolverType = (SolverType)iv;
        if (da.GetData(_inDrilling, ref dv)) s.DrillingStiffness = (float?)dv;
        if (da.GetData(_inFrameBuckling, ref bv)) s.FrameBucklingCheck = bv;
        if (da.GetData(_inTensComp, ref iv)) s.TensionCompressionOnly = (TensionCompressionOnlyMode)iv;
        if (da.GetData(_inRotateLocal, ref bv)) s.RotateLocalLoads = bv;
        if (da.GetData(_inStabilize, ref bv)) s.StabilizeUnrestrainedNodes = bv;
        if (da.GetData(_inCheckNonExist, ref bv)) s.CheckNonExistentLoadCases = bv;
        if (da.GetData(_inRetainLc, ref bv)) s.RetainLoadCases = bv;
        if (da.GetData(_inOptMethod, ref iv)) s.OptimizationMethod = (AnalysisOptimizationMethod)iv;
        if (da.GetData(_inOptAxis, ref iv)) s.OptimizationAxis = (OptimizationAxis)iv;
        if (da.GetData(_inOptX, ref dv)) s.OptimizationX = (float?)dv;
        if (da.GetData(_inOptY, ref dv)) s.OptimizationY = (float?)dv;
        if (da.GetData(_inOptZ, ref dv)) s.OptimizationZ = (float?)dv;
        
        da.SetData(_outSettings, new GH_SgAnalysisSettings(SgAnalysisSettingsData.ForLinearStatic(s)));
    }
}