using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GhSpaceGass.Async;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using GhSpaceGass.Core.Services;

namespace GhSpaceGass.Components.Loads;

public class GetSelfWeightLoadsComponent : GH_AsyncComponent<GetSelfWeightLoadsComponent>
{
    private int _inModel;
    private int _outLoadCases, _outLoadCaseNames, _outCategories;
    private int _outAx, _outAy, _outAz;
    private int _outStatus;

    public GetSelfWeightLoadsComponent()
        : base("SG Get Self-Weight Loads", "sgGetSW Loads",
            "Query all self-weight loads from the open SpaceGass job.",
            "SpaceGass", "5 | Loads")
    {
        BaseWorker = new GetSelfWeightLoadsWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.last;
    protected override Bitmap Icon => Icons.IconFactory.GetSelfWeightLoads();
    public override Guid ComponentGuid => new("8733C2A4-3B6E-4EE2-BB2B-B31D011A6EE5");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The SpaceGass model (from Assemble or Disassemble).",
            GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outLoadCases = pManager.AddIntegerParameter("Load Case ID", "LCId",
            "Load case ID per entry.", GH_ParamAccess.list);
        _outLoadCaseNames = pManager.AddTextParameter("Load Case Name", "LCN",
            "Resolved load case name per entry.", GH_ParamAccess.list);
        _outCategories = pManager.AddIntegerParameter("Load Category ID", "Cat",
            "Load category ID per entry (0 if none).", GH_ParamAccess.list);
        _outAx = pManager.AddNumberParameter("Acceleration X", "AccX",
            "Acceleration in X.", GH_ParamAccess.list);
        _outAy = pManager.AddNumberParameter("Acceleration Y", "AccY",
            "Acceleration in Y.", GH_ParamAccess.list);
        _outAz = pManager.AddNumberParameter("Acceleration Z", "AccZ",
            "Acceleration in Z.", GH_ParamAccess.list);
        _outStatus = pManager.AddTextParameter("Status", "S",
            "Query status and warnings.", GH_ParamAccess.item);
    }

    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
        base.AppendAdditionalMenuItems(menu);
        Menu_AppendItem(menu, "Cancel", (_, _) => { RequestCancellation(); });
    }

    // ── Worker ────────────────────────────────────────────────────

    private sealed class GetSelfWeightLoadsWorker : WorkerInstance<GetSelfWeightLoadsComponent>
    {
        public GetSelfWeightLoadsWorker(
            GetSelfWeightLoadsComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken) { }

        private SgModelData Model { get; set; }
        private SgSelfWeightLoadsDataResult Result { get; set; }
        private string Status { get; set; } = string.Empty;

        public override WorkerInstance<GetSelfWeightLoadsComponent> Duplicate(
            string id, CancellationToken cancellationToken)
            => new GetSelfWeightLoadsWorker(Parent, id, cancellationToken);

        public override void GetData(IGH_DataAccess da, GH_ComponentParamServer paramServer)
        {
            GH_SgModel modelGoo = null;
            da.GetData(Parent._inModel, ref modelGoo);
            Model = modelGoo?.Value;
        }

        public override async Task DoWork(Action<string, double> reportProgress, Action done)
        {
            try
            {
                if (Model == null)
                {
                    Status = "No model provided.";
                    Parent.Message = "No model";
                    if (!CancellationToken.IsCancellationRequested) done();
                    return;
                }

                Parent.Message = "Querying...";
                var session = SpaceGassSessionManager.Current;
                if (session == null || !session.IsConnected)
                {
                    Status = "Not connected.";
                    Parent.Message = "Not connected";
                    if (!CancellationToken.IsCancellationRequested) done();
                    return;
                }

                Result = await session.GetSelfWeightLoadsDataAsync(Model, CancellationToken)
                    .ConfigureAwait(false);

                Status = $"{Result.Loads.Count} self-weight loads queried.";
                foreach (var w in Result.Warnings)
                {
                    Status += $"\nWarning: {w}";
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
                }

                Parent.Message = $"{Result.Loads.Count} self-weight loads";
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                var message = ModelAssembler.FormatApiError(ex, "querying self-weight loads");
                Status = $"Error: {message}";
                Parent.Message = "Error";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        public override void SetData(IGH_DataAccess da)
        {
            if (Result != null)
            {
                da.SetDataList(Parent._outLoadCases, Result.Loads.ConvertAll(l => l.LoadCaseId));
                da.SetDataList(Parent._outLoadCaseNames, Result.Loads.ConvertAll(l => l.LoadCaseName));
                da.SetDataList(Parent._outCategories, Result.Loads.ConvertAll(l => l.LoadCategoryId));
                da.SetDataList(Parent._outAx, Result.Loads.ConvertAll(l => l.AccelerationX));
                da.SetDataList(Parent._outAy, Result.Loads.ConvertAll(l => l.AccelerationY));
                da.SetDataList(Parent._outAz, Result.Loads.ConvertAll(l => l.AccelerationZ));
            }

            da.SetData(Parent._outStatus, Status);
        }
    }
}
