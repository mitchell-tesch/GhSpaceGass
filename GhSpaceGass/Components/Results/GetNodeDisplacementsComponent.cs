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
using Rhino.Display;
using Rhino.Geometry;
using GhSpaceGass.Core.Services;

namespace GhSpaceGass.Components.Results;

public class GetNodeDisplacementsComponent : GH_AsyncComponent<GetNodeDisplacementsComponent>
{
    private static readonly Color DisplacementColor = Color.FromArgb(200, 0, 200);

    private int _inLoadCases;
    private int _inModel;
    private int _inPoints;
    private int _inScale;
    private int _inShowValues;
    
    private int _outLoadCases;
    private int _outNodes;
    private int _outPoints;
    private int _outRx, _outRy, _outRz;
    private int _outTx, _outTy, _outTz;
    private int _outWarnings;
    private int _outStatus;

    // Preview state
    private List<PreviewArrow> _previewArrows = new();
    private bool _showValues;

    public GetNodeDisplacementsComponent()
        : base("SG Node Displacements", "sgDisplacements",
            "Query node displacement results (translations and rotations) from a completed SpaceGass analysis. " +
            "Draws displacement vectors in the viewport when preview is enabled.",
            "SpaceGass", "8 | Results")
    {
        BaseWorker = new GetNodeDisplacementsWorker(this);
    }
    
    public override GH_Exposure Exposure => GH_Exposure.primary;
    protected override Bitmap Icon => Icons.IconFactory.NodeDisplacements();
    public override Guid ComponentGuid => new("B3382A2A-3404-4646-A086-4C0B6A0DA957");
    public override bool IsPreviewCapable => true;

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
        _inScale = pManager.AddNumberParameter("Scale", "Sc",
            "Optional: scale factor for viewport preview vectors. " +
            "When omitted, auto-scale is computed (ADR-0009). Set to 0 to disable preview.",
            GH_ParamAccess.item);
        _inShowValues = pManager.AddBooleanParameter("Show Values", "V",
            "When true, display resultant displacement magnitude adjacent to each vector.",
            GH_ParamAccess.item, false);

        pManager[_inPoints].Optional = true;
        pManager[_inLoadCases].Optional = true;
        pManager[_inScale].Optional = true;
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
            "Node locations (first branch only — identical across load cases).",
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

    public override void DrawViewportWires(IGH_PreviewArgs args)
    {
        base.DrawViewportWires(args);
        if (_previewArrows.Count == 0) return;

        foreach (var arrow in _previewArrows)
        {
            var origin = new Point3d(arrow.Origin.X, arrow.Origin.Y, arrow.Origin.Z);
            var tip = new Point3d(origin.X + arrow.Dx, origin.Y + arrow.Dy, origin.Z + arrow.Dz);
            var line = new Line(origin, tip);
            args.Display.DrawLine(line, DisplacementColor, 2);
            DrawArrowHead(args.Display, origin, tip, DisplacementColor);

            if (_showValues)
                args.Display.Draw2dText(
                    arrow.Magnitude.ToString("G4"),
                    DisplacementColor, tip, false, 12);
        }
    }

    public override BoundingBox ClippingBox
    {
        get
        {
            var box = base.ClippingBox;
            foreach (var arrow in _previewArrows)
            {
                var origin = new Point3d(arrow.Origin.X, arrow.Origin.Y, arrow.Origin.Z);
                box.Union(origin);
                box.Union(new Point3d(origin.X + arrow.Dx, origin.Y + arrow.Dy, origin.Z + arrow.Dz));
            }
            return box;
        }
    }

    private static void DrawArrowHead(DisplayPipeline display, Point3d from, Point3d tip, Color color)
    {
        var dir = tip - from;
        var length = dir.Length;
        if (length < 1e-10) return;

        var headLength = length * 0.15;
        dir.Unitize();

        var perp = Math.Abs(dir.Z) < 0.9
            ? Vector3d.CrossProduct(dir, Vector3d.ZAxis)
            : Vector3d.CrossProduct(dir, Vector3d.XAxis);
        perp.Unitize();

        var headWidth = headLength * 0.4;
        var basePoint = tip - dir * headLength;
        display.DrawLine(new Line(tip, basePoint + perp * headWidth), color, 2);
        display.DrawLine(new Line(tip, basePoint - perp * headWidth), color, 2);
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
        private double? UserScale { get; set; }
        private bool ShowValues { get; set; }

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
        private List<PreviewArrow> PreviewArrows { get; set; } = new();

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

            var scaleValue = 0.0;
            if (da.GetData(Parent._inScale, ref scaleValue))
            {
                if (scaleValue < 0)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Scale must be ≥ 0. Preview disabled.");
                UserScale = scaleValue;
            }

            var showValues = false;
            da.GetData(Parent._inShowValues, ref showValues);
            ShowValues = showValues;
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
                    if (i == 0)
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

            // Build preview vectors from queried results
            var idToSgPoint = new Dictionary<int, SgPoint3D>();
            foreach (var kvp in InputModel.NodeMap) idToSgPoint[kvp.Value] = kvp.Key;
            var bboxDiag = PreviewScaleHelper.ComputeBboxDiagonal(InputModel.NodeMap.Keys);
            var previewResult = DisplacementPreviewBuilder.Build(
                result.Displacements, idToSgPoint, bboxDiag, UserScale);
            PreviewArrows = previewResult.Arrows;
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

            Parent._previewArrows = PreviewArrows;
            Parent._showValues = ShowValues;
        }
    }
}