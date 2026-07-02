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

public class GetNodeReactionsComponent : GH_AsyncComponent<GetNodeReactionsComponent>
{
    private static readonly Color ColorX = Color.FromArgb(255, 0, 0);
    private static readonly Color ColorY = Color.FromArgb(0, 150, 0);
    private static readonly Color ColorZ = Color.FromArgb(0, 0, 255);

    private int _inModel, _inPoints, _inLoadCases, _inScale, _inShowValues;
    private int _outPoints, _outFx, _outFy, _outFz, _outMx, _outMy, _outMz, _outLoadCases, _outNodes, _outWarnings, _outStatus;

    // Preview state — populated during SetData, drawn in DrawViewportWires
    private List<PreviewArrow> _previewArrows = new();
    private bool _showValues;

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
    public override bool IsPreviewCapable => true;

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
        _inScale = pManager.AddNumberParameter("Scale", "Sc",
            "Optional: scale factor for viewport preview arrows. " +
            "When omitted, auto-scale is computed from model extents and max reaction magnitude (ADR-0009).",
            GH_ParamAccess.item);
        _inShowValues = pManager.AddBooleanParameter("Show Values", "V",
            "When true, display numeric reaction values adjacent to each arrow.",
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

    public override void DrawViewportWires(IGH_PreviewArgs args)
    {
        base.DrawViewportWires(args);
        if (_previewArrows.Count == 0) return;

        foreach (var arrow in _previewArrows)
        {
            var origin = new Point3d(arrow.Origin.X, arrow.Origin.Y, arrow.Origin.Z);
            var color = GetAxisColor(arrow.Axis);

            if (arrow.Type == ArrowType.Force)
                DrawForceArrow(args.Display, origin, arrow, color);
            else
                DrawMomentArc(args.Display, origin, arrow, color);

            if (_showValues)
            {
                var tip = arrow.Type == ArrowType.Force
                    ? new Point3d(origin.X + arrow.Dx, origin.Y + arrow.Dy, origin.Z + arrow.Dz)
                    : GetArcEndPoint(origin, arrow);
                args.Display.Draw2dText(
                    Math.Abs(arrow.Magnitude).ToString("G4"),
                    color, tip, false, 12);
            }
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

    private static Color GetAxisColor(int axis) =>
        axis switch { 0 => ColorX, 1 => ColorY, _ => ColorZ };

    private static void DrawForceArrow(DisplayPipeline display, Point3d origin, PreviewArrow arrow, Color color)
    {
        var tip = new Point3d(origin.X + arrow.Dx, origin.Y + arrow.Dy, origin.Z + arrow.Dz);
        var line = new Line(origin, tip);
        display.DrawLine(line, color, 2);
        DrawArrowHead(display, origin, tip, color);
    }

    private static void DrawArrowHead(DisplayPipeline display, Point3d from, Point3d tip, Color color)
    {
        var dir = tip - from;
        var length = dir.Length;
        if (length < 1e-10) return;

        var headLength = length * 0.15;
        dir.Unitize();

        // Find a perpendicular vector
        var perp = Math.Abs(dir.Z) < 0.9
            ? Vector3d.CrossProduct(dir, Vector3d.ZAxis)
            : Vector3d.CrossProduct(dir, Vector3d.XAxis);
        perp.Unitize();

        var headWidth = headLength * 0.4;
        var basePoint = tip - dir * headLength;
        var wing1 = basePoint + perp * headWidth;
        var wing2 = basePoint - perp * headWidth;

        display.DrawLine(new Line(tip, wing1), color, 2);
        display.DrawLine(new Line(tip, wing2), color, 2);
    }

    private static void DrawMomentArc(DisplayPipeline display, Point3d origin, PreviewArrow arrow, Color color)
    {
        var radius = Math.Sqrt(arrow.Dx * arrow.Dx + arrow.Dy * arrow.Dy + arrow.Dz * arrow.Dz);
        if (radius < 1e-10) return;

        // Determine arc plane normal from axis
        var normal = arrow.Axis switch
        {
            0 => Vector3d.XAxis,
            1 => Vector3d.YAxis,
            _ => Vector3d.ZAxis
        };

        // Positive moment = counterclockwise (right-hand rule)
        // Negative moment = clockwise → flip normal
        var magnitude = arrow.Axis switch
        {
            0 => arrow.Dx,
            1 => arrow.Dy,
            _ => arrow.Dz
        };
        if (magnitude < 0) normal = -normal;

        var plane = new Plane(origin, normal);
        var arc = new Arc(plane, radius, Math.PI * 1.5); // 270°

        display.DrawArc(arc, color, 2);

        // Arrowhead at arc endpoint
        var endPt = arc.EndPoint;
        var tangent = arc.TangentAt(arc.AngleDomain.T1);
        tangent.Unitize();
        var headLength = radius * 0.2;
        var headPerp = Vector3d.CrossProduct(tangent, normal);
        headPerp.Unitize();
        var headWidth = headLength * 0.4;
        var basePoint = endPt - tangent * headLength;
        display.DrawLine(new Line(endPt, basePoint + headPerp * headWidth), color, 2);
        display.DrawLine(new Line(endPt, basePoint - headPerp * headWidth), color, 2);
    }

    private static Point3d GetArcEndPoint(Point3d origin, PreviewArrow arrow)
    {
        var radius = Math.Sqrt(arrow.Dx * arrow.Dx + arrow.Dy * arrow.Dy + arrow.Dz * arrow.Dz);
        if (radius < 1e-10) return origin;

        var normal = arrow.Axis switch
        {
            0 => Vector3d.XAxis,
            1 => Vector3d.YAxis,
            _ => Vector3d.ZAxis
        };
        var magnitude = arrow.Axis switch
        {
            0 => arrow.Dx,
            1 => arrow.Dy,
            _ => arrow.Dz
        };
        if (magnitude < 0) normal = -normal;
        var plane = new Plane(origin, normal);
        var arc = new Arc(plane, radius, Math.PI * 1.5);
        return arc.EndPoint;
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
        private double? UserScale { get; set; }
        private bool ShowValues { get; set; }
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
        private List<PreviewArrow> PreviewArrows { get; set; } = new();

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

            var scaleValue = 0.0;
            if (da.GetData(Parent._inScale, ref scaleValue))
            {
                if (scaleValue <= 0)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Scale must be > 0. Falling back to auto-scale.");
                else
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

            // Build preview arrows from queried results
            var idToSgPoint = new Dictionary<int, SgPoint3D>();
            foreach (var kvp in InputModel.NodeMap) idToSgPoint[kvp.Value] = kvp.Key;
            var bboxDiag = PreviewScaleHelper.ComputeBboxDiagonal(InputModel.NodeMap.Keys);
            var previewResult = ReactionPreviewBuilder.Build(result.Reactions, idToSgPoint, bboxDiag, UserScale);
            PreviewArrows = previewResult.Arrows;
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

            // Copy preview state to the component for DrawViewportWires
            Parent._previewArrows = PreviewArrows;
            Parent._showValues = ShowValues;
        }
    }
}