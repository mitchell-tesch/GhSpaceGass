using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GhSpaceGass.Async;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Helpers;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using GhSpaceGass.Core.Services;

namespace GhSpaceGass.Components.Analysis;

public class RunAnalysisComponent : GH_AsyncComponent<RunAnalysisComponent>
{
    private int _inModel;
    private int _inRun;
    private int _inSettings;
    private int _inType;

    private int _outModel;
    private int _outStatus;
    private int _outSuccess;

    public RunAnalysisComponent()
        : base("Run Analysis", "sgAnalysis",
            "Run an analysis on the assembled SpaceGass model (Linear Static, Non-linear Static, Buckling, or Dynamic Frequency).",
            "SpaceGass", "7 | Analysis")
    {
        BaseWorker = new RunAnalysisWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.primary;
    protected override Bitmap Icon => Icons.IconFactory.RunAnalysis();
    public override Guid ComponentGuid => new("26525467-FA6A-4BAB-883F-38E4AE15060A");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The assembled SpaceGass model from Assemble Model.",
            GH_ParamAccess.item);
        _inRun = pManager.AddBooleanParameter("Run?", "R?",
            "Set to true to trigger analysis.",
            GH_ParamAccess.item, false);
        _inType = pManager.AddParameter(
            new Param_SgIntegerOption("Type", ValueListHelper.AnalysisTypeOptions, defaultValue: 0),
            "Type", "T",
            "Analysis type (Linear Static=0, Non-linear Static=1, Buckling=2, Dynamic Frequency=3).\n" +
            "Default = Linear Static.",
            GH_ParamAccess.item);
        _inSettings = pManager.AddParameter(new Param_SgAnalysisSettings(),
            "Settings", "Set",
            "Optional analysis settings from a settings component.",
            GH_ParamAccess.item);

        pManager[_inType].Optional = true;
        pManager[_inSettings].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "Pass-through of the input model for downstream chaining.",
            GH_ParamAccess.item);
        _outSuccess = pManager.AddBooleanParameter("Success", "OK",
            "True if the analysis completed successfully.",
            GH_ParamAccess.item);
        _outStatus = pManager.AddTextParameter("Status", "S",
            "Analysis status, timing, and any warnings or errors.",
            GH_ParamAccess.item);
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        document.ScheduleSolution(0, doc => ValueListHelper.AutoCreateOnPlacement(this, doc));
    }

    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
        base.AppendAdditionalMenuItems(menu);
        Menu_AppendItem(menu, "Cancel", (_, _) => { RequestCancellation(); });
    }


    // ── Worker ────────────────────────────────────────────────────

    private sealed class RunAnalysisWorker : WorkerInstance<RunAnalysisComponent>
    {
        public RunAnalysisWorker(
            RunAnalysisComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken)
        {
        }

        private SgModelData InputModel { get; set; }
        private bool RunEnabled { get; set; }
        private SgAnalysisType AnalysisType { get; set; }
        private SgAnalysisSettingsData SettingsData { get; set; }

        private SgModelData OutputModel { get; set; }
        private bool Success { get; set; }
        private string Status { get; set; } = string.Empty;

        public override WorkerInstance<RunAnalysisComponent> Duplicate(
            string id, CancellationToken cancellationToken)
        {
            return new RunAnalysisWorker(Parent, id, cancellationToken);
        }

        public override void GetData(IGH_DataAccess da, GH_ComponentParamServer paramServer)
        {
            var modelGoo = new GH_SgModel();
            if (!da.GetData(Parent._inModel, ref modelGoo) || modelGoo?.Value == null)
            {
                Status = "No model provided.";
                return;
            }

            InputModel = modelGoo.Value;

            var run = false;
            da.GetData(Parent._inRun, ref run);
            RunEnabled = run;

            var typeInt = 0;
            da.GetData(Parent._inType, ref typeInt);
            AnalysisType = (SgAnalysisType)typeInt;

            GH_SgAnalysisSettings settingsGoo = null;
            da.GetData(Parent._inSettings, ref settingsGoo);
            SettingsData = settingsGoo?.Value;
        }

        public override async Task DoWork(Action<string, double> reportProgress, Action done)
        {
            if (!RunEnabled)
            {
                OutputModel = InputModel;
                Success = false;
                Status = "Analysis not triggered. Set Run? to true.";
                SetComponentMessage("Idle");
                if (!CancellationToken.IsCancellationRequested) done();
                return;
            }

            try
            {
                SetComponentMessage("Analysing...");
                await RunAnalysisAsync();
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                // Cancelled — don't call done()
            }
            catch (Exception ex)
            {
                OutputModel = InputModel;
                Success = false;
                var message = ModelAssembler.FormatApiError(ex, "running analysis");
                Status = $"Error: {message}";
                SetComponentMessage("Error");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        private async Task RunAnalysisAsync()
        {
            var session = SpaceGassSessionManager.Current;
            if (session == null || !session.IsConnected)
            {
                OutputModel = InputModel;
                Success = false;
                Status = "Not connected. Place a SpaceGass Connect component and set Connect? to true.";
                SetComponentMessage("Not connected");
                return;
            }

            var result = await session.RunAnalysisAsync(
                AnalysisType, SettingsData,
                msg => SetComponentMessage(msg),
                CancellationToken).ConfigureAwait(false);

            OutputModel = InputModel;
            Success = result.Succeeded;

            // Build status string
            if (result.Succeeded)
            {
                Status = $"Analysis completed in {result.ElapsedTime}. Run ID: {result.RunId}.";
                SetComponentMessage($"Completed ({result.ElapsedTime})");
            }
            else
            {
                Status = !string.IsNullOrEmpty(result.ErrorMessage)
                    ? $"Analysis failed: {result.ErrorMessage}"
                    : "Analysis did not complete successfully.";
                SetComponentMessage("Failed");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, Status);
            }

            foreach (var warning in result.Warnings)
            {
                Status += $"\nWarning: {warning}";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, warning);
            }
        }

        public override void SetData(IGH_DataAccess da)
        {
            if (OutputModel != null)
                da.SetData(Parent._outModel, new GH_SgModel(OutputModel));
            da.SetData(Parent._outSuccess, Success);
            da.SetData(Parent._outStatus, Status);
        }
    }
}