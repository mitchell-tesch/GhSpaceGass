using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GhSpaceGass.Async;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using GhSpaceGass.Core.Services;

namespace GhSpaceGass.Components.Results;

public class GetNodeDisplacementsComponent : GH_AsyncComponent<GetNodeDisplacementsComponent>
{
    private int _inLoadCases;
    private int _inModel;
    private int _inPoints;
    
    private int _outLoadCases;
    private int _outNodes;
    private int _outPoints;
    private int _outRx, _outRy, _outRz;
    private int _outTx, _outTy, _outTz;
    private int _outWarnings;
    private int _outStatus;

    public GetNodeDisplacementsComponent()
        : base("SG Node Displacements", "sgDisplacements",
            "Query node displacement results (translations and rotations) from a completed SpaceGass analysis.",
            "SpaceGass", "8 | Results")
    {
        BaseWorker = new GetNodeDisplacementsWorker(this);
    }
    
    public override GH_Exposure Exposure => GH_Exposure.primary;
    protected override Bitmap Icon => Icons.IconFactory.NodeDisplacements();
    public override Guid ComponentGuid => new("B3382A2A-3404-4646-A086-4C0B6A0DA957");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The assembled and analysed SpaceGass model.",
            GH_ParamAccess.item);
        _inPoints = pManager.AddPointParameter("Points", "P",
            "Optional: filter displacements to these node locations only.",
            GH_ParamAccess.list);
        _inLoadCases = pManager.AddTextParameter("Load Cases", "LC",
            "Optional: filter displacements to these load case names only.",
            GH_ParamAccess.list);

        pManager[_inPoints].Optional = true;
        pManager[_inLoadCases].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outLoadCases = pManager.AddTextParameter("Load Cases", "LC",
            "Load case names, one per branch matching the results tree.",
            GH_ParamAccess.tree);
        _outNodes = pManager.AddIntegerParameter("Node IDs", "NIds",
            "Node IDs, branched by load case.",
            GH_ParamAccess.tree);
        _outPoints = pManager.AddPointParameter("Node Points", "NP",
            "Node locations, branched by load case.",
            GH_ParamAccess.tree);
        _outTx = pManager.AddNumberParameter("Tx", "Tx",
            "Translation in global X, branched by load case.",
            GH_ParamAccess.tree);
        _outTy = pManager.AddNumberParameter("Ty", "Ty",
            "Translation in global Y, branched by load case.",
            GH_ParamAccess.tree);
        _outTz = pManager.AddNumberParameter("Tz", "Tz",
            "Translation in global Z, branched by load case.",
            GH_ParamAccess.tree);
        _outRx = pManager.AddNumberParameter("Rx", "Rx",
            "Rotation about global X, branched by load case.",
            GH_ParamAccess.tree);
        _outRy = pManager.AddNumberParameter("Ry", "Ry",
            "Rotation about global Y, branched by load case.",
            GH_ParamAccess.tree);
        _outRz = pManager.AddNumberParameter("Rz", "Rz",
            "Rotation about global Z, branched by load case.",
            GH_ParamAccess.tree);
        _outWarnings = pManager.AddTextParameter("Warnings", "W",
            "Warnings from the SpaceGass API query (multiline text).",
            GH_ParamAccess.item);
        _outStatus = pManager.AddTextParameter("Status", "S",
            "Query status summary.", GH_ParamAccess.item);
    }

    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
        base.AppendAdditionalMenuItems(menu);
        Menu_AppendItem(menu, "Cancel", (_, _) => { RequestCancellation(); });
    }

    // ── Worker ────────────────────────────────────────────────────

    private sealed class GetNodeDisplacementsWorker : WorkerInstance<GetNodeDisplacementsComponent>
    {
        public GetNodeDisplacementsWorker(
            GetNodeDisplacementsComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken)
        {
        }

        private SgModelData InputModel { get; set; }
        private List<SgPoint3D> NodeFilter { get; set; }
        private List<string> LoadCaseFilter { get; set; }

        private GH_Structure<GH_Point> OutPoints { get; set; }
        private GH_Structure<GH_Number> OutTx { get; set; }
        private GH_Structure<GH_Number> OutTy { get; set; }
        private GH_Structure<GH_Number> OutTz { get; set; }
        private GH_Structure<GH_Number> OutRx { get; set; }
        private GH_Structure<GH_Number> OutRy { get; set; }
        private GH_Structure<GH_Number> OutRz { get; set; }
        private GH_Structure<GH_String> OutLoadCases { get; set; }
        private GH_Structure<GH_Integer> OutNodes { get; set; }
        private string OutWarningsText { get; set; }
        private string Status { get; set; } = string.Empty;

        public override WorkerInstance<GetNodeDisplacementsComponent> Duplicate(
            string id, CancellationToken cancellationToken)
        {
            return new GetNodeDisplacementsWorker(Parent, id, cancellationToken);
        }

        public override void GetData(IGH_DataAccess da, GH_ComponentParamServer paramServer)
        {
            var modelGoo = new GH_SgModel();
            if (!da.GetData(Parent._inModel, ref modelGoo) || modelGoo?.Value == null)
                return;
            InputModel = modelGoo.Value;

            var points = new List<GH_Point>();
            if (da.GetDataList(Parent._inPoints, points) && points.Count > 0)
                NodeFilter = points
                    .Where(p => p?.Value != null)
                    .Select(p => new SgPoint3D(p.Value.X, p.Value.Y, p.Value.Z))
                    .ToList();

            var lcNames = new List<GH_String>();
            if (da.GetDataList(Parent._inLoadCases, lcNames) && lcNames.Count > 0)
                LoadCaseFilter = lcNames
                    .Where(s => s?.Value != null)
                    .Select(s => s.Value)
                    .ToList();
        }

        public override async Task DoWork(Action<string, double> reportProgress, Action done)
        {
            if (InputModel == null)
            {
                Parent.Message = "No model";
                if (!CancellationToken.IsCancellationRequested) done();
                return;
            }

            try
            {
                Parent.Message = "Querying...";
                await QueryDisplacementsAsync();
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                var message = ModelAssembler.FormatApiError(ex, "querying node displacements");
                Status = $"Error: {message}";
                Parent.Message = "Error";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        private async Task QueryDisplacementsAsync()
        {
            var session = SpaceGassSessionManager.Current;
            if (session == null || !session.IsConnected)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Not connected. Place a SpaceGass Connect component and set Connect? to true.");
                Parent.Message = "Not connected";
                return;
            }

            var result = await session.GetNodeDisplacementsAsync(
                InputModel, NodeFilter, LoadCaseFilter,
                CancellationToken).ConfigureAwait(false);

            foreach (var w in result.Warnings)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);

            OutWarningsText = result.Warnings.Count > 0 ? string.Join(Environment.NewLine, result.Warnings) : "";

            if (result.Displacements.Count == 0)
            {
                Parent.Message = "No displacements";
                Status = "0 node displacements queried.";
                OutPoints = new GH_Structure<GH_Point>();
                OutTx = new GH_Structure<GH_Number>();
                OutTy = new GH_Structure<GH_Number>();
                OutTz = new GH_Structure<GH_Number>();
                OutRx = new GH_Structure<GH_Number>();
                OutRy = new GH_Structure<GH_Number>();
                OutRz = new GH_Structure<GH_Number>();
                OutLoadCases = new GH_Structure<GH_String>();
                OutNodes = new GH_Structure<GH_Integer>();
                return;
            }

            var idToPoint = new Dictionary<int, Point3d>();
            foreach (var kvp in InputModel.NodeMap)
                idToPoint[kvp.Value] = new Point3d(kvp.Key.X, kvp.Key.Y, kvp.Key.Z);

            var idToLcName = new Dictionary<int, string>();
            foreach (var kvp in InputModel.LoadCaseMap)
                idToLcName[kvp.Value] = kvp.Key;
            foreach (var kvp in InputModel.CombinationLoadCaseMap)
                idToLcName[kvp.Value] = kvp.Key;

            var grouped = result.Displacements
                .GroupBy(d => d.LoadCaseId)
                .OrderBy(g => g.Key)
                .ToList();

            OutPoints = new GH_Structure<GH_Point>();
            OutTx = new GH_Structure<GH_Number>();
            OutTy = new GH_Structure<GH_Number>();
            OutTz = new GH_Structure<GH_Number>();
            OutRx = new GH_Structure<GH_Number>();
            OutRy = new GH_Structure<GH_Number>();
            OutRz = new GH_Structure<GH_Number>();
            OutLoadCases = new GH_Structure<GH_String>();
            OutNodes = new GH_Structure<GH_Integer>();

            for (var i = 0; i < grouped.Count; i++)
            {
                var group = grouped[i];
                var path = new GH_Path(i);

                OutLoadCases.Append(
                    new GH_String(idToLcName.TryGetValue(group.Key, out var lcName)
                        ? lcName
                        : $"Load Case {group.Key}"),
                    path);

                foreach (var d in group.OrderBy(d => d.NodeId))
                {
                    OutPoints.Append(
                        idToPoint.TryGetValue(d.NodeId, out var pt)
                            ? new GH_Point(pt)
                            : new GH_Point(Point3d.Unset), path);
                    OutNodes.Append(new GH_Integer(d.NodeId), path);
                    OutTx.Append(new GH_Number(d.Tx), path);
                    OutTy.Append(new GH_Number(d.Ty), path);
                    OutTz.Append(new GH_Number(d.Tz), path);
                    OutRx.Append(new GH_Number(d.Rx), path);
                    OutRy.Append(new GH_Number(d.Ry), path);
                    OutRz.Append(new GH_Number(d.Rz), path);
                }
            }

            Parent.Message = $"{result.Displacements.Count} displacements";
            Status = $"{result.Displacements.Count} node displacements queried.";
        }

        public override void SetData(IGH_DataAccess da)
        {
            if (OutPoints != null) da.SetDataTree(Parent._outPoints, OutPoints);
            if (OutTx != null) da.SetDataTree(Parent._outTx, OutTx);
            if (OutTy != null) da.SetDataTree(Parent._outTy, OutTy);
            if (OutTz != null) da.SetDataTree(Parent._outTz, OutTz);
            if (OutRx != null) da.SetDataTree(Parent._outRx, OutRx);
            if (OutRy != null) da.SetDataTree(Parent._outRy, OutRy);
            if (OutRz != null) da.SetDataTree(Parent._outRz, OutRz);
            if (OutLoadCases != null) da.SetDataTree(Parent._outLoadCases, OutLoadCases);
            if (OutNodes != null) da.SetDataTree(Parent._outNodes, OutNodes);
            da.SetData(Parent._outWarnings, OutWarningsText ?? "");
            da.SetData(Parent._outStatus, Status);
        }
    }
}