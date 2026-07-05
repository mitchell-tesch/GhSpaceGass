using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GhSpaceGass.Async;
using GhSpaceGass.Core.Services;
using GhSpaceGass.Helpers;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using SpaceGassApi.Models;

namespace GhSpaceGass.Components.Connection;

public class ConnectComponent : GH_AsyncComponent<ConnectComponent>
{
    private int _inConnect;
    private int _inFilePath;
    private int _inForce;
    private int _inInstallPath;
    private int _inPort;
    private int _inShowConsole;

    private int _outIsConnected;
    private int _outStatus;
    private int _outUrl;
    private int _outVersion;

    public ConnectComponent()
        : base("SG Connect", "sgConnect",
            "Launch the SpaceGass API service and create or open a job.",
            "SpaceGass", "1 | Connection")
    {
        BaseWorker = new ConnectWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.primary;
    protected override Bitmap Icon => Icons.IconFactory.Connect();
    public override Guid ComponentGuid => new("8E88B539-1C3D-4DD8-8E26-B92B37399B4A");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inConnect = pManager.AddBooleanParameter("Connect?", "C?",
            "Set to true to connect.",
            GH_ParamAccess.item, false);
        _inPort = pManager.AddIntegerParameter("Port", "P",
            "API service port (default: 34560).",
            GH_ParamAccess.item, SpaceGassSession.DefaultPort);
        _inFilePath = pManager.AddTextParameter("File Path", "F",
            "Path to a SpaceGass job file (.sg). Opens if it exists, creates a new job otherwise. " +
            "If not provided, a temporary job file is used.",
            GH_ParamAccess.item);
        _inShowConsole = pManager.AddBooleanParameter("Show Console?", "SC?",
            "Show the SpaceGass API console window when launching the service.",
            GH_ParamAccess.item, true);
        _inForce = pManager.AddParameter(
            new Param_SgIntegerOption("Force Option", ValueListHelper.ForceAccessOptions, defaultValue: 0),
            "Force Option", "FO",
            "Force open option when the file is locked or has unsaved changes.\n" +
            "None=0 (default), Open Previous Saved=1, Open Unsaved Most Recent=2.",
            GH_ParamAccess.item);
        _inInstallPath = pManager.AddTextParameter("Install Path", "IP",
            "Path to SpaceGassApi.exe. If not provided, uses the default install location.",
            GH_ParamAccess.item);

        pManager[_inPort].Optional = true;
        pManager[_inFilePath].Optional = true;
        pManager[_inShowConsole].Optional = true;
        pManager[_inForce].Optional = true;
        pManager[_inInstallPath].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outIsConnected = pManager.AddBooleanParameter("Connected?", "C",
            "True if connected to SpaceGass.",
            GH_ParamAccess.item);
        _outUrl = pManager.AddTextParameter("URL", "URL",
            "The SpaceGass API service URL.",
            GH_ParamAccess.item);
        _outVersion = pManager.AddTextParameter("Version", "V",
            "The SpaceGass version reported by the API service.",
            GH_ParamAccess.item);
        _outStatus = pManager.AddTextParameter("Status", "S",
            "Connection and job status message.",
            GH_ParamAccess.item);
    }

    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
        base.AppendAdditionalMenuItems(menu);
        Menu_AppendItem(
            menu,
            "Cancel",
            (_, _) => { RequestCancellation(); }
        );
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        document.ScheduleSolution(0, doc => ValueListHelper.AutoCreateOnPlacement(this, doc));
    }

    // ── Worker ────────────────────────────────────────────────────

    private sealed class ConnectWorker : WorkerInstance<ConnectComponent>
    {
        public ConnectWorker(
            ConnectComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default
        )
            : base(parent, id, cancellationToken)
        {
        }

        private bool ConnectEnabled { get; set; }
        private int Port { get; set; }
        private string FilePath { get; set; } = string.Empty;
        private bool ShowConsole { get; set; } = true;
        private int ForceOption { get; set; }
        private string InstallPath { get; set; } = string.Empty;

        private bool IsConnected { get; set; }
        private string Url { get; set; } = string.Empty;
        private string Version { get; set; } = string.Empty;
        private string Status { get; set; } = string.Empty;

        public override WorkerInstance<ConnectComponent> Duplicate(
            string id,
            CancellationToken cancellationToken
        )
        {
            return new ConnectWorker(Parent, id, cancellationToken);
        }

        public override void GetData(IGH_DataAccess da, GH_ComponentParamServer paramServer)
        {
            var connectEnabled = false;
            da.GetData(Parent._inConnect, ref connectEnabled);

            if (!connectEnabled)
            {
                IsConnected = false;
                Status = "Disconnected, set Connect? to true to connect.";
                return;
            }

            ConnectEnabled = true;

            var port = SpaceGassSession.DefaultPort;
            da.GetData(Parent._inPort, ref port);
            if (port < 1 || port > 65535)
            {
                ConnectEnabled = false;
                IsConnected = false;
                Status = $"Error: Port {port} is invalid. Must be between 1 and 65535.";
                return;
            }

            Port = port;

            var filePath = string.Empty;
            da.GetData(Parent._inFilePath, ref filePath);
            FilePath = filePath;

            var showConsole = true;
            da.GetData(Parent._inShowConsole, ref showConsole);
            ShowConsole = showConsole;

            var forceOption = 0;
            da.GetData(Parent._inForce, ref forceOption);
            ForceOption = forceOption;

            var installPath = string.Empty;
            da.GetData(Parent._inInstallPath, ref installPath);
            InstallPath = installPath;
        }

        public override async Task DoWork(Action<string, double> reportProgress, Action done)
        {
            if (!ConnectEnabled)
            {
                // Disconnect: close the open job, then dispose the session (ADR-0007)
                var session = SpaceGassSessionManager.Current;
                if (session is { IsConnected: true })
                {
                    try
                    {
                        if (await session.IsJobOpenAsync(CancellationToken).ConfigureAwait(false))
                            await session.CloseJobAsync(CancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Best-effort — don't block disconnect if close fails
                    }
                }

                SpaceGassSessionManager.DisposeSession();
                SetComponentMessage("Disconnected");
                if (!CancellationToken.IsCancellationRequested) done();
                return;
            }

            try
            {
                SetComponentMessage("Connecting...");
                await ConnectAsync();
                SetComponentMessage("Connected");
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                // Cancelled — don't call done()
            }
            catch (Exception ex)
            {
                IsConnected = false;
                var message = ModelAssembler.FormatApiError(ex, "connecting");
                Status = $"Error: {message}";
                SetComponentMessage("Error");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        private async Task ConnectAsync()
        {
            var session = SpaceGassSessionManager.Current;

            // Reuse the existing session if it's still connected on the same port.
            // This avoids killing and relaunching the service when only the file path changed.
            if (session is { IsConnected: true } && session.Port == Port)
            {
                // Verify the service is still responsive (it could have crashed)
                if (!await session.IsServiceResponsiveAsync(CancellationToken).ConfigureAwait(false))
                {
                    // Service died — tear down and reconnect from scratch
                    SpaceGassSessionManager.DisposeSession();
                    session = null;
                }
            }
            else
            {
                // Port changed or no session exists — full teardown
                SpaceGassSessionManager.DisposeSession();
                session = null;
            }

            // Create and connect a new session if needed
            if (session == null)
            {
                var resolvedInstallPath = string.IsNullOrWhiteSpace(InstallPath)
                    ? SpaceGassSession.DefaultInstallPath
                    : InstallPath;
                session = SpaceGassSessionManager.GetOrCreate(Port, resolvedInstallPath);
                await session.ConnectAsync(ShowConsole, CancellationToken).ConfigureAwait(false);

                if (!session.IsConnected)
                {
                    IsConnected = false;
                    Status = "Failed to connect.";
                    SetComponentMessage("Error");
                    return;
                }
            }

            CancellationToken.ThrowIfCancellationRequested();

            IsConnected = session.IsConnected;
            Url = session.ServiceUrl;
            Version = session.SpaceGassVersion;
            var baseUrl = session.ServiceUrl;

            // Resolve file path — use temp directory if not provided
            var jobPath = FilePath;
            if (string.IsNullOrWhiteSpace(jobPath))
            {
                var tempDir = Path.GetTempPath();
                jobPath = Path.Combine(tempDir, $"GhSpaceGass_{Guid.NewGuid():N}.sg");
            }

            // Close any existing open job before opening/creating a new one.
            // This is critical when connecting to an already-running service (ADR-0004)
            // which may have a job open from a previous session or manual use.
            if (await session.IsJobOpenAsync(CancellationToken).ConfigureAwait(false))
                await session.CloseJobAsync(CancellationToken).ConfigureAwait(false);

            CancellationToken.ThrowIfCancellationRequested();

            // Open or create job based on file path
            var forceAccessOption = (JobForceAccessOption)ForceOption;
            if (File.Exists(jobPath))
            {
                var jobStatus = await session.OpenJobAsync(jobPath, forceAccessOption, CancellationToken)
                    .ConfigureAwait(false);

                // Check access mode
                if (jobStatus.AccessMode == AccessMode.ReadOnly)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Job opened in read-only mode. Changes cannot be saved.");
                else if (jobStatus.AccessMode == AccessMode.NoAccess)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        "Job has no access — file may be locked by another process.");

                var accessInfo = jobStatus.AccessMode != null
                    ? $" (Access: {jobStatus.AccessMode})"
                    : "";
                Status = $"Connected: {baseUrl}.\n" +
                         $"Opened: {jobPath}{accessInfo}";
            }
            else
            {
                await session.NewJobAsync(CancellationToken).ConfigureAwait(false);
                await session.SaveJobAsync(jobPath, CancellationToken).ConfigureAwait(false);
                Status = $"Connected: {baseUrl}.\n" +
                         $"Created: {jobPath}";
            }
        }

        public override void SetData(IGH_DataAccess da)
        {
            da.SetData(Parent._outIsConnected, IsConnected);
            da.SetData(Parent._outUrl, Url);
            da.SetData(Parent._outVersion, Version);
            da.SetData(Parent._outStatus, Status);
        }
    }
}