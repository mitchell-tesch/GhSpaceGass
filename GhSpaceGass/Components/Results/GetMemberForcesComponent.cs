using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GhSpaceGass.Async;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Models.Visuals;
using GhSpaceGass.Helpers;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using GhSpaceGass.Core.Services;

namespace GhSpaceGass.Components.Results;

public class GetMemberForcesComponent : GH_AsyncComponent<GetMemberForcesComponent>
{
    // Per-component colours: Fx=Brown, Fy=Orange, Fz=DarkOrange, Mx=Teal, My=DarkPurple, Mz=Purple
    private static readonly Color[] ComponentColors =
    {
        Color.FromArgb(233, 30, 99),   // Fx - Pink
        Color.FromArgb(255, 152, 0),   // Fy - Orange
        Color.FromArgb(200, 117, 0),   // Fz - Dark Orange
        Color.FromArgb(0, 150, 136),   // Mx - Teal
        Color.FromArgb(74, 40, 135),   // My - Dark Purple
        Color.FromArgb(103, 58, 183)   // Mz - Purple
    };

    private int _inLoadCases;
    private int _inMembers;
    private int _inMode;
    private int _inModel;
    private int _inVisual;
    private int _inScale;
    private int _inShowValues;
    
    private int _outFx, _outFy, _outFz;
    private int _outLines;
    private int _outLoadCases;
    private int _outMembers;
    private int _outMx, _outMy, _outMz;
    private int _outNodes;
    private int _outPoints;
    private int _outStations;
    private int _outStatus;
    private int _outWarnings;

    // Preview state
    private List<PreviewDiagramGeometry> _previewDiagrams = new();
    private bool _showValues;

    private sealed record PreviewDiagramGeometry(
        Polyline Outline,
        Point3d[] BasePoints,
        Point3d[] DiagramPoints,
        Color Color,
        double[] StationValues,
        int[] ExtremaIndices);

    public GetMemberForcesComponent()
        : base("SG Member Forces", "sgMemberForces",
            "Query member force results from a completed SpaceGass analysis. " +
            "Supports end forces and intermediate station forces. " +
            "Draws force diagrams in the viewport when in Intermediate mode and preview is enabled.",
            "SpaceGass", "8 | Results")
    {
        BaseWorker = new GetMemberForcesWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.secondary;
    protected override Bitmap Icon => Icons.IconFactory.MemberForces();
    public override Guid ComponentGuid => new("A9DB1524-BAE6-4EBA-B0EF-93DA9090D0DF");
    public override bool IsPreviewCapable => true;

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The assembled and analysed SpaceGass model.",
            GH_ParamAccess.item);
        _inMembers = pManager.AddLineParameter("Members", "Mb",
            "Optional: filter forces to these member geometries only.",
            GH_ParamAccess.list);
        _inLoadCases = pManager.AddTextParameter("Load Cases", "LC",
            "Optional: filter forces to these load case names only.",
            GH_ParamAccess.list);
        _inMode = pManager.AddParameter(
            new Param_SgIntegerOption("Mode", ValueListHelper.ForceModeOptions, defaultValue: 1, autoCreate: true),
            "Mode", "Mo",
            "End Forces=0 (forces at each member end), Intermediate=1 (forces at stations along member).\n" +
            "Default: Intermediate",
            GH_ParamAccess.item);
        _inVisual = pManager.AddParameter(
            new Param_SgIntegerOption("Visual", ValueListHelper.MemberForceVisualOptions,
                defaultValue: 5, autoCreate: true),
            "Visual", "V",
            "Force component to display as a diagram (Intermediate mode).\n" +
            "Fx=0, Fy=1, Fz=2, Mx=3, My=4, Mz=5.\nDefault: Mz.",
            GH_ParamAccess.item);
        _inScale = pManager.AddNumberParameter("Visual Scale", "VSc",
            "Scale factor for force diagram (Intermediate mode). Auto-scale when omitted. 0=off.",
            GH_ParamAccess.item);
        _inShowValues = pManager.AddBooleanParameter("Show Values?", "SV?",
            "When true, display values at local extrema on the diagram.",
            GH_ParamAccess.item, true);

        pManager[_inMembers].Optional = true;
        pManager[_inLoadCases].Optional = true;
        pManager[_inScale].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outLoadCases = pManager.AddTextParameter("Load Cases", "LC",
            "Load case names, one per branch matching the load case dimension of the results tree.",
            GH_ParamAccess.tree);
        _outMembers = pManager.AddIntegerParameter("Member IDs", "MbId",
            "Member IDs, branched by {load_case; member}.",
            GH_ParamAccess.tree);
        _outLines = pManager.AddLineParameter("Member Lines", "MLns",
            "Member geometry, branched by {load_case; member}. Only populated in Intermediate mode.",
            GH_ParamAccess.tree);
        _outNodes = pManager.AddIntegerParameter("End Node IDs", "NIds",
            "Node IDs, branched by {load_case; member}. Only populated in End Forces mode.",
            GH_ParamAccess.tree);
        _outPoints = pManager.AddPointParameter("End Node Points", "P",
            "Node location at each member end, branched by {load_case; member}. Only populated in End Forces mode.",
            GH_ParamAccess.tree);
        _outStations = pManager.AddNumberParameter("Stations", "S",
            "Position along member (intermediate mode), branched by {load_case; member}.",
            GH_ParamAccess.tree);
        _outFx = pManager.AddNumberParameter("Fx", "Fx",
            "Axial force, branched by {load_case; member}.",
            GH_ParamAccess.tree);
        _outFy = pManager.AddNumberParameter("Fy", "Fy",
            "Shear force in Y, branched by {load_case; member}.",
            GH_ParamAccess.tree);
        _outFz = pManager.AddNumberParameter("Fz", "Fz",
            "Shear force in Z, branched by {load_case; member}.",
            GH_ParamAccess.tree);
        _outMx = pManager.AddNumberParameter("Mx", "Mx",
            "Torsion, branched by {load_case; member}.",
            GH_ParamAccess.tree);
        _outMy = pManager.AddNumberParameter("My", "My",
            "Bending moment about Y, branched by {load_case; member}.",
            GH_ParamAccess.tree);
        _outMz = pManager.AddNumberParameter("Mz", "Mz",
            "Bending moment about Z, branched by {load_case; member}.",
            GH_ParamAccess.tree);
        _outWarnings = pManager.AddTextParameter("Warnings", "W",
            "Warnings from the SpaceGass API query (multiline text).",
            GH_ParamAccess.item);
        _outStatus = pManager.AddTextParameter("Status", "S",
            "Query status summary.",
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

    public override void DrawViewportWires(IGH_PreviewArgs args)
    {
        base.DrawViewportWires(args);
        if (_previewDiagrams.Count == 0) return;

        foreach (var diag in _previewDiagrams)
        {
            if (diag.DiagramPoints.Length < 2) continue;

            args.Display.DrawPolyline(diag.Outline, diag.Color, 2);

            // Fill lines from base to diagram
            var pointCount = Math.Min(diag.BasePoints.Length, diag.DiagramPoints.Length);
            for (var i = 1; i < pointCount - 1; i++)
            {
                args.Display.DrawLine(
                    new Line(diag.BasePoints[i], diag.DiagramPoints[i]),
                    diag.Color, 1);
            }

            // Closing lines
            if (pointCount > 0)
            {
                args.Display.DrawLine(
                    new Line(diag.BasePoints[0], diag.DiagramPoints[0]), diag.Color, 2);
                if (pointCount > 1)
                    args.Display.DrawLine(
                        new Line(diag.BasePoints[pointCount - 1], diag.DiagramPoints[pointCount - 1]), diag.Color, 2);
            }

            if (_showValues && diag.ExtremaIndices.Length > 0)
            {
                foreach (var idx in diag.ExtremaIndices)
                {
                    if (idx >= diag.DiagramPoints.Length || idx >= diag.StationValues.Length) continue;
                    var pt = diag.DiagramPoints[idx];
                    args.Display.Draw2dText(
                        diag.StationValues[idx].ToString("G4"),
                        diag.Color, pt, false, 12);
                }
            }
        }
    }

    public override BoundingBox ClippingBox
    {
        get
        {
            var box = base.ClippingBox;
            foreach (var diag in _previewDiagrams)
                foreach (var pt in diag.DiagramPoints)
                    box.Union(pt);
            return box;
        }
    }

    // ── Worker ────────────────────────────────────────────────────

    private sealed class GetMemberForcesWorker : WorkerInstance<GetMemberForcesComponent>
    {
        public GetMemberForcesWorker(
            GetMemberForcesComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken)
        {
        }

        private SgModelData InputModel { get; set; }
        private List<(SgPoint3D Start, SgPoint3D End)> MemberFilter { get; set; }
        private List<string> LoadCaseFilter { get; set; }
        private int Mode { get; set; }
        private int VisualIndex { get; set; } = 5;
        private double? UserScale { get; set; }
        private bool ShowValues { get; set; }

        private GH_Structure<GH_Line> OutLines { get; set; }
        private GH_Structure<GH_Point> OutPoints { get; set; }
        private GH_Structure<GH_Number> OutStations { get; set; }
        private GH_Structure<GH_Number> OutFx { get; set; }
        private GH_Structure<GH_Number> OutFy { get; set; }
        private GH_Structure<GH_Number> OutFz { get; set; }
        private GH_Structure<GH_Number> OutMx { get; set; }
        private GH_Structure<GH_Number> OutMy { get; set; }
        private GH_Structure<GH_Number> OutMz { get; set; }
        private GH_Structure<GH_String> OutLoadCases { get; set; }
        private GH_Structure<GH_Integer> OutMembers { get; set; }
        private GH_Structure<GH_Integer> OutNodes { get; set; }
        private string OutWarningsText { get; set; }
        private string Status { get; set; } = string.Empty;
        private List<(ForceDiagramData Data, Color Color)> PreviewDiagrams { get; set; } = new();

        public override WorkerInstance<GetMemberForcesComponent> Duplicate(
            string id, CancellationToken cancellationToken)
        {
            return new GetMemberForcesWorker(Parent, id, cancellationToken);
        }

        public override void GetData(IGH_DataAccess da, GH_ComponentParamServer paramServer)
        {
            var modelGoo = new GH_SgModel();
            if (!da.GetData(Parent._inModel, ref modelGoo) || modelGoo?.Value == null)
                return;
            InputModel = modelGoo.Value;

            var lines = new List<GH_Line>();
            if (da.GetDataList(Parent._inMembers, lines) && lines.Count > 0)
                MemberFilter = lines
                    .Where(l => l?.Value != null)
                    .Select(l => (
                        new SgPoint3D(l.Value.From.X, l.Value.From.Y, l.Value.From.Z),
                        new SgPoint3D(l.Value.To.X, l.Value.To.Y, l.Value.To.Z)))
                    .ToList();

            var lcNames = new List<GH_String>();
            if (da.GetDataList(Parent._inLoadCases, lcNames) && lcNames.Count > 0)
                LoadCaseFilter = lcNames
                    .Where(s => s?.Value != null)
                    .Select(s => s.Value)
                    .ToList();

            var mode = 1;
            da.GetData(Parent._inMode, ref mode);
            Mode = mode;

            var visual = 5;
            da.GetData(Parent._inVisual, ref visual);
            VisualIndex = Math.Clamp(visual, -1, 5);

            var scaleVal = 0.0;
            if (da.GetData(Parent._inScale, ref scaleVal))
            {
                if (scaleVal < 0)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Scale must be ≥ 0. Preview disabled.");
                UserScale = scaleVal;
            }

            var showValues = true;
            da.GetData(Parent._inShowValues, ref showValues);
            ShowValues = showValues;
        }

        public override async Task DoWork(Action<string, double> reportProgress, Action done)
        {
            if (InputModel == null)
            {
                SetComponentMessage("No model");
                if (!CancellationToken.IsCancellationRequested) done();
                return;
            }

            try
            {
                SetComponentMessage("Querying...");
                if (Mode == 1)
                    await QueryIntermediateForcesAsync();
                else
                    await QueryEndForcesAsync();
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                var message = ModelAssembler.FormatApiError(ex, "querying member forces");
                Status = $"Error: {message}";
                SetComponentMessage("Error");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        private void InitEmptyOutputs()
        {
            OutLines = new GH_Structure<GH_Line>();
            OutPoints = new GH_Structure<GH_Point>();
            OutStations = new GH_Structure<GH_Number>();
            OutFx = new GH_Structure<GH_Number>();
            OutFy = new GH_Structure<GH_Number>();
            OutFz = new GH_Structure<GH_Number>();
            OutMx = new GH_Structure<GH_Number>();
            OutMy = new GH_Structure<GH_Number>();
            OutMz = new GH_Structure<GH_Number>();
            OutLoadCases = new GH_Structure<GH_String>();
            OutMembers = new GH_Structure<GH_Integer>();
            OutNodes = new GH_Structure<GH_Integer>();
        }

        private async Task QueryEndForcesAsync()
        {
            var session = SpaceGassSessionManager.Current;
            if (session == null || !session.IsConnected)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Not connected. Place a SpaceGass Connect component and set Connect? to true.");
                SetComponentMessage("Not connected");
                return;
            }

            var result = await session.GetMemberEndForcesAsync(
                InputModel, MemberFilter, LoadCaseFilter,
                CancellationToken).ConfigureAwait(false);

            foreach (var w in result.Warnings)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
            OutWarningsText = result.Warnings.Count > 0 ? string.Join(Environment.NewLine, result.Warnings) : "";

            if (result.EndForces.Count == 0)
            {
                SetComponentMessage("No end forces");
                Status = "0 end forces queried.";
                InitEmptyOutputs();
                return;
            }

            // Build reverse maps
            var idToPoint = new Dictionary<int, Point3d>();
            foreach (var kvp in InputModel.NodeMap)
                idToPoint[kvp.Value] = kvp.Key.ToPoint3d();

            var idToLcName = InputModel.BuildLoadCaseIdToNameMap();

            // Group by load case, then by member — two-level tree {load_case; member}
            var byLoadCase = result.EndForces
                .GroupBy(ef => ef.LoadCaseId)
                .OrderBy(g => g.Key)
                .ToList();

            InitEmptyOutputs();

            // Build member index map across all load cases
            var memberIdOrder = result.EndForces
                .Select(ef => ef.MemberId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            var memberIndexMap = new Dictionary<int, int>();
            for (var mi = 0; mi < memberIdOrder.Count; mi++)
                memberIndexMap[memberIdOrder[mi]] = mi;

            for (var lcIdx = 0; lcIdx < byLoadCase.Count; lcIdx++)
            {
                var lcGroup = byLoadCase[lcIdx];
                OutLoadCases.Append(
                    new GH_String(idToLcName.TryGetValue(lcGroup.Key, out var lcName)
                        ? lcName
                        : $"Load Case {lcGroup.Key}"),
                    new GH_Path(lcIdx));

                var byMember = lcGroup
                    .GroupBy(ef => ef.MemberId)
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var memberGroup in byMember)
                {
                    var memberIdx = memberIndexMap[memberGroup.Key];
                    var path = new GH_Path(lcIdx, memberIdx);

                    // One member ID per branch
                    OutMembers.Append(new GH_Integer(memberGroup.Key), path);

                    foreach (var ef in memberGroup.OrderBy(ef => ef.NodeId))
                    {
                        OutPoints.Append(
                            idToPoint.TryGetValue(ef.NodeId, out var pt)
                                ? new GH_Point(pt)
                                : new GH_Point(Point3d.Unset), path);
                        OutNodes.Append(new GH_Integer(ef.NodeId), path);

                        OutFx.Append(new GH_Number(ef.Fx), path);
                        OutFy.Append(new GH_Number(ef.Fy), path);
                        OutFz.Append(new GH_Number(ef.Fz), path);
                        OutMx.Append(new GH_Number(ef.Mx), path);
                        OutMy.Append(new GH_Number(ef.My), path);
                        OutMz.Append(new GH_Number(ef.Mz), path);
                    }
                }
            }

            SetComponentMessage($"{result.EndForces.Count} end forces");
            Status = $"{result.EndForces.Count} end forces queried.";
        }

        private async Task QueryIntermediateForcesAsync()
        {
            var session = SpaceGassSessionManager.Current;
            if (session == null || !session.IsConnected)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Not connected. Place a SpaceGass Connect component and set Connect? to true.");
                SetComponentMessage("Not connected");
                return;
            }

            var result = await session.GetMemberIntermediateForcesAsync(
                InputModel, MemberFilter, LoadCaseFilter,
                CancellationToken).ConfigureAwait(false);

            foreach (var w in result.Warnings)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
            OutWarningsText = result.Warnings.Count > 0 ? string.Join(Environment.NewLine, result.Warnings) : "";

            if (result.Forces.Count == 0)
            {
                SetComponentMessage("No intermediate forces");
                Status = "0 intermediate forces queried.";
                InitEmptyOutputs();
                return;
            }

            // Build reverse maps
            var idToMemberLine = new Dictionary<int, Line>();
            foreach (var kvp in InputModel.MemberMap)
            {
                var s = kvp.Value.Start;
                var e = kvp.Value.End;
                idToMemberLine[kvp.Key] = new Line(
                    s.ToPoint3d(),
                    e.ToPoint3d());
            }

            var idToLcName = InputModel.BuildLoadCaseIdToNameMap();

            // Group by load case, then by member — two-level tree {load_case; member}
            var byLoadCase = result.Forces
                .GroupBy(f => f.LoadCaseId)
                .OrderBy(g => g.Key)
                .ToList();

            InitEmptyOutputs();

            // Build member index map across all load cases
            var memberIdOrder = result.Forces
                .Select(f => f.MemberId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            var memberIndexMap = new Dictionary<int, int>();
            for (var mi = 0; mi < memberIdOrder.Count; mi++)
                memberIndexMap[memberIdOrder[mi]] = mi;

            for (var lcIdx = 0; lcIdx < byLoadCase.Count; lcIdx++)
            {
                var lcGroup = byLoadCase[lcIdx];
                OutLoadCases.Append(
                    new GH_String(idToLcName.TryGetValue(lcGroup.Key, out var lcName)
                        ? lcName
                        : $"Load Case {lcGroup.Key}"),
                    new GH_Path(lcIdx));

                var byMember = lcGroup
                    .GroupBy(f => f.MemberId)
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var memberGroup in byMember)
                {
                    var memberIdx = memberIndexMap[memberGroup.Key];
                    var path = new GH_Path(lcIdx, memberIdx);

                    // One line and one member ID per branch
                    OutLines.Append(
                        idToMemberLine.TryGetValue(memberGroup.Key, out var memberLine)
                            ? new GH_Line(memberLine)
                            : new GH_Line(Line.Unset), path);
                    OutMembers.Append(new GH_Integer(memberGroup.Key), path);

                    // Station results ordered by position along member (Location)
                    foreach (var f in memberGroup.OrderBy(f => f.Location))
                    {
                        OutStations.Append(new GH_Number(f.Location), path);
                        OutFx.Append(new GH_Number(f.Fx), path);
                        OutFy.Append(new GH_Number(f.Fy), path);
                        OutFz.Append(new GH_Number(f.Fz), path);
                        OutMx.Append(new GH_Number(f.Mx), path);
                        OutMy.Append(new GH_Number(f.My), path);
                        OutMz.Append(new GH_Number(f.Mz), path);
                    }
                }
            }

            SetComponentMessage($"{result.Forces.Count} intermediate forces");
            Status = $"{result.Forces.Count} intermediate forces queried.";

            // Build force diagram preview for selected component (Intermediate mode only)
            if (VisualIndex >= 0)
            {
                var memberMapSg = new Dictionary<int, (SgPoint3D Start, SgPoint3D End)>();
                foreach (var kvp in InputModel.MemberMap) memberMapSg[kvp.Key] = kvp.Value;
                var bboxDiag = PreviewScaleHelper.ComputeBboxDiagonal(InputModel.NodeMap.Keys);
                var diagramResult = ForceDiagramBuilder.BuildSingleComponent(
                    result.Forces, memberMapSg, bboxDiag, VisualIndex, UserScale);

                var color = ComponentColors[Math.Clamp(VisualIndex, 0, ComponentColors.Length - 1)];
                PreviewDiagrams = diagramResult.Diagrams
                    .Select(d => (d, color))
                    .ToList();
            }
        }

        public override void SetData(IGH_DataAccess da)
        {
            if (OutLines != null) da.SetDataTree(Parent._outLines, OutLines);
            if (OutPoints != null) da.SetDataTree(Parent._outPoints, OutPoints);
            if (OutStations != null) da.SetDataTree(Parent._outStations, OutStations);
            if (OutFx != null) da.SetDataTree(Parent._outFx, OutFx);
            if (OutFy != null) da.SetDataTree(Parent._outFy, OutFy);
            if (OutFz != null) da.SetDataTree(Parent._outFz, OutFz);
            if (OutMx != null) da.SetDataTree(Parent._outMx, OutMx);
            if (OutMy != null) da.SetDataTree(Parent._outMy, OutMy);
            if (OutMz != null) da.SetDataTree(Parent._outMz, OutMz);
            if (OutLoadCases != null) da.SetDataTree(Parent._outLoadCases, OutLoadCases);
            if (OutMembers != null) da.SetDataTree(Parent._outMembers, OutMembers);
            if (OutNodes != null) da.SetDataTree(Parent._outNodes, OutNodes);
            da.SetData(Parent._outWarnings, OutWarningsText ?? "");
            da.SetData(Parent._outStatus, Status);

            Parent._previewDiagrams = PreviewDiagrams
                .Select(diagram =>
                {
                    var basePoints = diagram.Data.BasePoints.Select(p => p.ToPoint3d()).ToArray();
                    var diagramPoints = diagram.Data.DiagramPoints.Select(p => p.ToPoint3d()).ToArray();
                    return new PreviewDiagramGeometry(
                        new Polyline(diagramPoints),
                        basePoints,
                        diagramPoints,
                        diagram.Color,
                        diagram.Data.StationValues.ToArray(),
                        diagram.Data.ExtremaIndices.ToArray());
                })
                .ToList();
            Parent._showValues = ShowValues;
        }
    }
}