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

public class GetNodeReactionsComponent : GH_AsyncComponent<GetNodeReactionsComponent>
{
    private int _inModel, _inPoints, _inLoadCases;
    private int _outPoints, _outFx, _outFy, _outFz, _outMx, _outMy, _outMz, _outLoadCases, _outNodes, _outWarnings, _outStatus;

    public GetNodeReactionsComponent()
        : base("SG Node Reactions", "sgReactions",
            "Query node reaction forces and moments from a completed SpaceGass analysis.",
            "SpaceGass", "8 | Results")
    {
        BaseWorker = new GetNodeReactionsWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.primary;
    protected override Bitmap Icon => Icons.IconFactory.NodeReactions();
    public override Guid ComponentGuid => new("1D869AC5-109C-4D52-856C-EE5C1803CEBC");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The assembled and analysed SpaceGass model.",
            GH_ParamAccess.item);
        _inPoints = pManager.AddPointParameter("Points", "P",
            "Optional: filter reactions to these node locations only.",
            GH_ParamAccess.list);
        _inLoadCases = pManager.AddTextParameter("Load Cases", "LC",
            "Optional: filter reactions to these load case names only.",
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
        _outPoints = pManager.AddPointParameter("Node Points", "P",
            "Reaction node locations, branched by load case.",
            GH_ParamAccess.tree);
        _outFx = pManager.AddNumberParameter("Fx", "Fx",
            "Reaction force in global X, branched by load case.",
            GH_ParamAccess.tree);
        _outFy = pManager.AddNumberParameter("Fy", "Fy",
            "Reaction force in global Y, branched by load case.",
            GH_ParamAccess.tree);
        _outFz = pManager.AddNumberParameter("Fz", "Fz",
            "Reaction force in global Z, branched by load case.",
            GH_ParamAccess.tree);
        _outMx = pManager.AddNumberParameter("Mx", "Mx",
            "Reaction moment about global X, branched by load case.",
            GH_ParamAccess.tree);
        _outMy = pManager.AddNumberParameter("My", "My",
            "Reaction moment about global Y, branched by load case.",
            GH_ParamAccess.tree);
        _outMz = pManager.AddNumberParameter("Mz", "Mz",
            "Reaction moment about global Z, branched by load case.",
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

    private sealed class GetNodeReactionsWorker : WorkerInstance<GetNodeReactionsComponent>
    {
        public GetNodeReactionsWorker(GetNodeReactionsComponent parent, string id = "baseWorker",
            CancellationToken cancellationToken = default) : base(parent, id, cancellationToken)
        {
        }

        private SgModelData InputModel { get; set; }
        private List<SgPoint3D> NodeFilter { get; set; }
        private List<string> LoadCaseFilter { get; set; }
        private GH_Structure<GH_Point> OutPoints { get; set; }
        private GH_Structure<GH_Number> OutFx { get; set; }
        private GH_Structure<GH_Number> OutFy { get; set; }
        private GH_Structure<GH_Number> OutFz { get; set; }
        private GH_Structure<GH_Number> OutMx { get; set; }
        private GH_Structure<GH_Number> OutMy { get; set; }
        private GH_Structure<GH_Number> OutMz { get; set; }
        private GH_Structure<GH_String> OutLoadCases { get; set; }
        private GH_Structure<GH_Integer> OutNodes { get; set; }
        private string OutWarningsText { get; set; }
        private string Status { get; set; } = string.Empty;

        public override WorkerInstance<GetNodeReactionsComponent> Duplicate(string id,
            CancellationToken cancellationToken)
        {
            return new GetNodeReactionsWorker(Parent, id, cancellationToken);
        }

        public override void GetData(IGH_DataAccess da, GH_ComponentParamServer paramServer)
        {
            var modelGoo = new GH_SgModel();
            if (!da.GetData(Parent._inModel, ref modelGoo) || modelGoo?.Value == null) return;
            InputModel = modelGoo.Value;
            var points = new List<GH_Point>();
            if (da.GetDataList(Parent._inPoints, points) && points.Count > 0)
                NodeFilter = points.Where(p => p?.Value != null)
                    .Select(p => new SgPoint3D(p.Value.X, p.Value.Y, p.Value.Z)).ToList();
            var lcNames = new List<GH_String>();
            if (da.GetDataList(Parent._inLoadCases, lcNames) && lcNames.Count > 0)
                LoadCaseFilter = lcNames.Where(s => s?.Value != null).Select(s => s.Value).ToList();
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
                await QueryAsync();
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                var message = ModelAssembler.FormatApiError(ex, "querying node reactions");
                Status = $"Error: {message}";
                Parent.Message = "Error";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        private async Task QueryAsync()
        {
            var session = SpaceGassSessionManager.Current;
            if (session == null || !session.IsConnected)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Not connected. Place a SpaceGass Connect component and set Connect? to true.");
                Parent.Message = "Not connected";
                return;
            }

            var result = await session.GetNodeReactionsAsync(InputModel, NodeFilter, LoadCaseFilter, CancellationToken)
                .ConfigureAwait(false);
            foreach (var w in result.Warnings) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
            OutWarningsText = result.Warnings.Count > 0 ? string.Join(Environment.NewLine, result.Warnings) : "";
            if (result.Reactions.Count == 0)
            {
                Parent.Message = "No reactions";
                Status = "0 node reactions queried.";
                OutPoints = new GH_Structure<GH_Point>();
                OutFx = new GH_Structure<GH_Number>();
                OutFy = new GH_Structure<GH_Number>();
                OutFz = new GH_Structure<GH_Number>();
                OutMx = new GH_Structure<GH_Number>();
                OutMy = new GH_Structure<GH_Number>();
                OutMz = new GH_Structure<GH_Number>();
                OutLoadCases = new GH_Structure<GH_String>();
                OutNodes = new GH_Structure<GH_Integer>();
                return;
            }

            var idToPoint = new Dictionary<int, Point3d>();
            foreach (var kvp in InputModel.NodeMap) idToPoint[kvp.Value] = new Point3d(kvp.Key.X, kvp.Key.Y, kvp.Key.Z);
            var idToLcName = new Dictionary<int, string>();
            foreach (var kvp in InputModel.LoadCaseMap) idToLcName[kvp.Value] = kvp.Key;
            foreach (var kvp in InputModel.CombinationLoadCaseMap) idToLcName[kvp.Value] = kvp.Key;
            var grouped = result.Reactions.GroupBy(r => r.LoadCaseId).OrderBy(g => g.Key).ToList();
            OutPoints = new GH_Structure<GH_Point>();
            OutFx = new GH_Structure<GH_Number>();
            OutFy = new GH_Structure<GH_Number>();
            OutFz = new GH_Structure<GH_Number>();
            OutMx = new GH_Structure<GH_Number>();
            OutMy = new GH_Structure<GH_Number>();
            OutMz = new GH_Structure<GH_Number>();
            OutLoadCases = new GH_Structure<GH_String>();
            OutNodes = new GH_Structure<GH_Integer>();
            for (var i = 0; i < grouped.Count; i++)
            {
                var group = grouped[i];
                var path = new GH_Path(i);
                OutLoadCases.Append(
                    new GH_String(idToLcName.TryGetValue(group.Key, out var lcName) ? lcName : $"Load Case {group.Key}"),
                    path);
                foreach (var r in group.OrderBy(r => r.NodeId))
                {
                    OutPoints.Append(
                        idToPoint.TryGetValue(r.NodeId, out var pt) ? new GH_Point(pt) : new GH_Point(Point3d.Unset),
                        path);
                    OutNodes.Append(new GH_Integer(r.NodeId), path);
                    OutFx.Append(new GH_Number(r.Fx), path);
                    OutFy.Append(new GH_Number(r.Fy), path);
                    OutFz.Append(new GH_Number(r.Fz), path);
                    OutMx.Append(new GH_Number(r.Mx), path);
                    OutMy.Append(new GH_Number(r.My), path);
                    OutMz.Append(new GH_Number(r.Mz), path);
                }
            }

            Parent.Message = $"{result.Reactions.Count} reactions";
            Status = $"{result.Reactions.Count} node reactions queried.";
        }

        public override void SetData(IGH_DataAccess da)
        {
            if (OutPoints != null) da.SetDataTree(Parent._outPoints, OutPoints);
            if (OutFx != null) da.SetDataTree(Parent._outFx, OutFx);
            if (OutFy != null) da.SetDataTree(Parent._outFy, OutFy);
            if (OutFz != null) da.SetDataTree(Parent._outFz, OutFz);
            if (OutMx != null) da.SetDataTree(Parent._outMx, OutMx);
            if (OutMy != null) da.SetDataTree(Parent._outMy, OutMy);
            if (OutMz != null) da.SetDataTree(Parent._outMz, OutMz);
            if (OutLoadCases != null) da.SetDataTree(Parent._outLoadCases, OutLoadCases);
            if (OutNodes != null) da.SetDataTree(Parent._outNodes, OutNodes);
            da.SetData(Parent._outWarnings, OutWarningsText ?? "");
            da.SetData(Parent._outStatus, Status);
        }
    }
}