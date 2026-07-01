using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GhSpaceGass.Async;
using GhSpaceGass.Core.Models;
using Grasshopper.Kernel;

namespace GhSpaceGass.Components.Connection;

public class SaveJobComponent : GH_AsyncComponent<SaveJobComponent>
{
    private int _inFilePath;
    private int _inSave;

    private int _outFilePath;
    private int _outSaved;
    private int _outStatus;

    public SaveJobComponent()
        : base("SG Save Job", "sgSaveJob",
            "Save the current SpaceGass job file and report a summary.",
            "SpaceGass", "1 | Connection")
    {
        BaseWorker = new SaveJobWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.tertiary;
    protected override Bitmap Icon => Icons.IconFactory.SaveJob();
    public override Guid ComponentGuid => new("A3F2D81E-6B47-4C9A-B5E3-1D8F0A2C7E94");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inSave = pManager.AddBooleanParameter("Save?", "S?",
            "Set to true to save the job.",
            GH_ParamAccess.item, false);
        _inFilePath = pManager.AddTextParameter("File Path", "F",
            "Path to save the job file (.sg). If not provided, saves to the current file path.",
            GH_ParamAccess.item);

        pManager[_inFilePath].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outSaved = pManager.AddBooleanParameter("Saved?", "Sd?",
            "True if the save completed successfully.",
            GH_ParamAccess.item);
        _outFilePath = pManager.AddTextParameter("File Path", "FD",
            "The path the job was saved to.",
            GH_ParamAccess.item);
        _outStatus = pManager.AddTextParameter("Status", "St",
            "Summary message with file path and model counts.",
            GH_ParamAccess.item);
    }

    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
        base.AppendAdditionalMenuItems(menu);
        Menu_AppendItem(menu, "Cancel", (s, e) => { RequestCancellation(); });
    }

    // ── Worker ────────────────────────────────────────────────────

    private sealed class SaveJobWorker : WorkerInstance<SaveJobComponent>
    {
        public SaveJobWorker(
            SaveJobComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken)
        {
        }

        private bool SaveEnabled { get; set; }
        private string FilePath { get; set; } = string.Empty;

        private bool Saved { get; set; }
        private string OutputFilePath { get; set; } = string.Empty;
        private string Status { get; set; } = string.Empty;

        public override WorkerInstance<SaveJobComponent> Duplicate(
            string id, CancellationToken cancellationToken)
        {
            return new SaveJobWorker(Parent, id, cancellationToken);
        }

        public override void GetData(IGH_DataAccess da, GH_ComponentParamServer paramServer)
        {
            var save = false;
            da.GetData(Parent._inSave, ref save);

            if (!save)
            {
                Saved = false;
                Status = "Set Save? to true to save.";
                return;
            }

            SaveEnabled = true;

            var filePath = string.Empty;
            da.GetData(Parent._inFilePath, ref filePath);
            FilePath = filePath;
        }

        public override async Task DoWork(Action<string, double> reportProgress, Action done)
        {
            if (!SaveEnabled)
            {
                Parent.Message = "";
                if (!CancellationToken.IsCancellationRequested) done();
                return;
            }

            try
            {
                Parent.Message = "Saving...";
                await SaveAsync();
                Parent.Message = "Saved";
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                // Cancelled — don't call done()
            }
            catch (Exception ex)
            {
                Saved = false;
                Status = $"Error: {ex.Message}";
                Parent.Message = "Error";
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        private async Task SaveAsync()
        {
            var session = SpaceGassSessionManager.Current;
            if (session == null || !session.IsConnected)
            {
                Saved = false;
                Status = "Not connected. Place a SpaceGass Connect component and set Connect? to true.";
                Parent.Message = "Not connected";
                return;
            }

            // Pass null if empty to trigger current-path resolution in the session
            var pathArg = string.IsNullOrWhiteSpace(FilePath) ? null : FilePath;

            var info = await session.SaveAndGetInfoAsync(pathArg, CancellationToken)
                .ConfigureAwait(false);

            Saved = true;
            OutputFilePath = info.FilePath;
            Status = FormatSaveStatus(info);
        }

        private static string FormatSaveStatus(SgJobInfo info)
        {
            var lines = new System.Collections.Generic.List<string>();

            lines.Add($"Saved: {info.FilePath}");
            lines.Add(
                $"Structure: {info.NodeCount} nodes, {info.MemberCount} members, {info.PlateCount} plates");
            lines.Add(
                $"Loads: {info.LoadCaseCount} cases, {info.NodeLoadCount} node loads, " +
                $"{info.MemberDistributedLoadCount} distributed loads");

            if (info.HasStaticResults || info.HasBucklingResults || info.HasDynamicResults)
            {
                var results = new System.Collections.Generic.List<string>();
                if (info.HasStaticResults) results.Add("Static");
                if (info.HasBucklingResults) results.Add("Buckling");
                if (info.HasDynamicResults) results.Add("Dynamic");
                lines.Add($"Analysis: {string.Join(", ", results)}");
            }

            return string.Join("\n", lines);
        }

        public override void SetData(IGH_DataAccess da)
        {
            da.SetData(Parent._outSaved, Saved);
            da.SetData(Parent._outFilePath, OutputFilePath);
            da.SetData(Parent._outStatus, Status);
        }
    }
}

