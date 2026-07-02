using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GhSpaceGass.Async;
using GhSpaceGass.Core.Models;
using Grasshopper.Kernel;
using GhSpaceGass.Core.Services;

namespace GhSpaceGass.Components.Connection;

public class JobInfoComponent : GH_AsyncComponent<JobInfoComponent>
{
    private int _inDesignerInitials;
    private int _inHeading;
    private int _inNotes;
    private int _inProjectHeading;

    private int _inRefresh;
    private int _outDesignerInitials;

    private int _outHeading;
    private int _outNotes;
    private int _outProjectHeading;
    private int _outStatus;
    private int _outUnits;
    private int _outVerticalAxis;

    public JobInfoComponent()
        : base("SG Job Info", "sgJobInfo",
            "Query the current SpaceGass job status, units, settings, and headings. " +
            "Optionally update job headings.",
            "SpaceGass", "1 | Connection")
    {
        BaseWorker = new JobInfoWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.secondary;
    protected override Bitmap Icon => Icons.IconFactory.JobInfo();
    public override Guid ComponentGuid => new("6AA1B3FB-2C89-41AC-B3DA-DCD5153FFE98");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inRefresh = pManager.AddBooleanParameter("Refresh?", "R?",
            "Set to true to query the current job status. Toggle to refresh.",
            GH_ParamAccess.item, true);
        _inHeading = pManager.AddTextParameter("Heading", "H",
            "Optional: set the job heading.",
            GH_ParamAccess.item);
        _inProjectHeading = pManager.AddTextParameter("Project Heading", "PH",
            "Optional: set the project heading.",
            GH_ParamAccess.item);
        _inDesignerInitials = pManager.AddTextParameter("Designer Initials", "DI",
            "Optional: set the designer initials.",
            GH_ParamAccess.item);
        _inNotes = pManager.AddTextParameter("Notes", "N",
            "Optional: set the job notes.",
            GH_ParamAccess.item);

        pManager[_inHeading].Optional = true;
        pManager[_inProjectHeading].Optional = true;
        pManager[_inDesignerInitials].Optional = true;
        pManager[_inNotes].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outHeading = pManager.AddTextParameter("Heading", "H",
            "Current job heading.",
            GH_ParamAccess.item);
        _outProjectHeading = pManager.AddTextParameter("Project Heading", "PH",
            "Current project heading.",
            GH_ParamAccess.item);
        _outDesignerInitials = pManager.AddTextParameter("Designer Initials", "DI",
            "Current designer initials.",
            GH_ParamAccess.item);
        _outNotes = pManager.AddTextParameter("Notes", "N",
            "Current job notes.",
            GH_ParamAccess.item);
        _outVerticalAxis = pManager.AddTextParameter("Vertical Axis", "VA",
            "Vertical axis setting (YAxis or ZAxis).",
            GH_ParamAccess.item);
        _outUnits = pManager.AddTextParameter("Units", "U",
            "Formatted summary of the current job units.",
            GH_ParamAccess.item);
        _outStatus = pManager.AddTextParameter("Status", "S",
            "Full job status summary (file, structure, loads, analysis).",
            GH_ParamAccess.item);
    }

    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
        base.AppendAdditionalMenuItems(menu);
        Menu_AppendItem(menu, "Cancel", (_, _) => { RequestCancellation(); });
    }

    // ── Worker ────────────────────────────────────────────────────

    private sealed class JobInfoWorker : WorkerInstance<JobInfoComponent>
    {
        public JobInfoWorker(
            JobInfoComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken)
        {
        }

        private bool RefreshEnabled { get; set; }
        private string InputHeading { get; set; }
        private string InputProjectHeading { get; set; }
        private string InputDesignerInitials { get; set; }
        private string InputNotes { get; set; }

        private SgJobInfo JobInfo { get; set; }
        private string Status { get; set; } = string.Empty;

        public override WorkerInstance<JobInfoComponent> Duplicate(
            string id, CancellationToken cancellationToken)
        {
            return new JobInfoWorker(Parent, id, cancellationToken);
        }

        public override void GetData(IGH_DataAccess da, GH_ComponentParamServer paramServer)
        {
            var refresh = true;
            da.GetData(Parent._inRefresh, ref refresh);
            RefreshEnabled = refresh;

            var heading = string.Empty;
            if (da.GetData(Parent._inHeading, ref heading) && !string.IsNullOrEmpty(heading))
                InputHeading = heading;

            var projectHeading = string.Empty;
            if (da.GetData(Parent._inProjectHeading, ref projectHeading) && !string.IsNullOrEmpty(projectHeading))
                InputProjectHeading = projectHeading;

            var designerInitials = string.Empty;
            if (da.GetData(Parent._inDesignerInitials, ref designerInitials) && !string.IsNullOrEmpty(designerInitials))
                InputDesignerInitials = designerInitials;

            var notes = string.Empty;
            if (da.GetData(Parent._inNotes, ref notes) && !string.IsNullOrEmpty(notes))
                InputNotes = notes;
        }

        public override async Task DoWork(Action<string, double> reportProgress, Action done)
        {
            if (!RefreshEnabled)
            {
                Status = "Idle. Set Refresh? to true to query job status.";
                Parent.Message = "Idle";
                if (!CancellationToken.IsCancellationRequested) done();
                return;
            }

            try
            {
                Parent.Message = "Querying...";
                await QueryJobInfoAsync();
                Parent.Message = "Done";
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                // Cancelled — don't call done()
            }
            catch (Exception ex)
            {
                var message = ModelAssembler.FormatApiError(ex, "querying job info");
                Status = $"Error: {message}";
                Parent.Message = "Error";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        private async Task QueryJobInfoAsync()
        {
            var session = SpaceGassSessionManager.Current;
            if (session == null || !session.IsConnected)
            {
                Status = "Not connected. Place a SpaceGass Connect component and set Connect? to true.";
                Parent.Message = "Not connected";
                return;
            }

            // If any heading inputs are provided, update headings first
            var hasHeadingInput = InputHeading != null
                                  || InputProjectHeading != null
                                  || InputDesignerInitials != null
                                  || InputNotes != null;

            if (hasHeadingInput)
            {
                Parent.Message = "Updating headings...";
                JobInfo = await session.UpdateHeadingsAsync(
                    InputHeading,
                    InputProjectHeading,
                    InputDesignerInitials,
                    InputNotes,
                    CancellationToken).ConfigureAwait(false);
            }
            else
            {
                JobInfo = await session.GetJobInfoAsync(CancellationToken).ConfigureAwait(false);
            }

            Status = JobInfo.FormatStatus();
        }

        public override void SetData(IGH_DataAccess da)
        {
            da.SetData(Parent._outHeading, JobInfo != null ? JobInfo.Heading : string.Empty);
            da.SetData(Parent._outProjectHeading, JobInfo != null ? JobInfo.ProjectHeading : string.Empty);
            da.SetData(Parent._outDesignerInitials, JobInfo != null ? JobInfo.DesignerInitials : string.Empty);
            da.SetData(Parent._outNotes, JobInfo != null ? JobInfo.Notes : string.Empty);
            da.SetData(Parent._outVerticalAxis, JobInfo != null ? JobInfo.DisplayVerticalAxis : string.Empty);
            da.SetData(Parent._outUnits, JobInfo != null ? JobInfo.FormatUnits() : string.Empty);
            da.SetData(Parent._outStatus, Status);
        }
    }
}