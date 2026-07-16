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
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using GhSpaceGass.Core.Services;

namespace GhSpaceGass.Components.Results;

public class GetSteelMemberCheckSummaryComponent : GH_AsyncComponent<GetSteelMemberCheckSummaryComponent>
{
    private int _inDesignGroups;
    private int _inModel;
    private int _inShowValues;

    private int _outCriticalCaseNames;
    private int _outCriticalCases;
    private int _outDesignGroups;
    private int _outFailureModes;
    private int _outFlags;
    private int _outLoadFactors;
    private int _outRepresentativeLines;
    private int _outSections;
    private int _outSegmentLengths;
    private int _outStatus;
    private int _outTotalLengths;
    private int _outWarnings;
    private int _outYields;

    private List<PreviewSteelMemberLine> _previewMembers = new();
    private bool _showValues;

    public GetSteelMemberCheckSummaryComponent()
        : base("SG Steel Member Check Summary", "sgSteelCheck",
            "Query the SpaceGass steel-design member check summary — one record per designed steel " +
            "design group showing the critical load case, capacity ratio (Load Factor = capacity/action), " +
            "failure mode, and geometric/material properties. Steel Design must be run in SpaceGass first. " +
            "Each returned record represents a design group (aggregate of one or more physical members); " +
            "the Representative Line is drawn on the model member matching the returned ID as a best-effort " +
            "placeholder until per-member resolution is added in a later slice. " +
            "Colours each design group by capacity ratio (red = overloaded → green = safe margin).",
            "SpaceGass", "8 | Results")
    {
        BaseWorker = new GetSteelMemberCheckSummaryWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.senary;
    protected override Bitmap Icon => Icons.IconFactory.SteelMemberCheck();
    public override Guid ComponentGuid => new("1045CA6F-021A-4BB3-A321-52FFD6B465AF");
    public override bool IsPreviewCapable => true;

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The assembled and analysed SpaceGass model.",
            GH_ParamAccess.item);
        _inDesignGroups = pManager.AddIntegerParameter("Design Group IDs", "DGIds",
            "Optional: filter check results to specific design-group IDs. " +
            "(The API's Members query parameter is a design-group filter; the parameter name " +
            "preserves the API's terminology.)",
            GH_ParamAccess.list);
        _inShowValues = pManager.AddBooleanParameter("Show Values?", "SV?",
            "When true, display the load factor value at each design group's representative midpoint.",
            GH_ParamAccess.item, true);

        pManager[_inDesignGroups].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outDesignGroups = pManager.AddIntegerParameter("Design Group IDs", "DGIds",
            "SpaceGass design-group IDs, one per queried record. " +
            "Each group aggregates one or more physical members.",
            GH_ParamAccess.list);
        _outRepresentativeLines = pManager.AddLineParameter("Representative Lines", "RL",
            "Best-effort geometry per design group — the model member matching the returned ID " +
            "(Line.Unset when no matching member exists in the model). Not necessarily the physical " +
            "member the design engine used for the critical check; per-member resolution is deferred " +
            "to a follow-up slice.",
            GH_ParamAccess.list);
        _outSections = pManager.AddTextParameter("Sections", "Sec",
            "Section name assigned to each design group (from the steel design engine).",
            GH_ParamAccess.list);
        _outFlags = pManager.AddTextParameter("Flags", "F",
            "Design status flag (e.g. PASS, FAIL).",
            GH_ParamAccess.list);
        _outLoadFactors = pManager.AddNumberParameter("Load Factors", "LF",
            "Design capacity ratio for the critical load case (capacity / action). " +
            "Values ≥ 1 indicate an adequate group; values < 1 indicate an overloaded (failed) group.",
            GH_ParamAccess.list);
        _outCriticalCases = pManager.AddIntegerParameter("Critical Cases", "CC",
            "Load case ID that governs the design (0 when not reported).",
            GH_ParamAccess.list);
        _outCriticalCaseNames = pManager.AddTextParameter("Critical Case Names", "CCN",
            "Critical load case name resolved from the model (falls back to 'Load Case {id}' " +
            "when unresolved; empty when not reported).",
            GH_ParamAccess.list);
        _outFailureModes = pManager.AddTextParameter("Failure Modes", "FM",
            "Failure mode description from the design engine.",
            GH_ParamAccess.list);
        _outSegmentLengths = pManager.AddNumberParameter("Segment Lengths", "SegL",
            "Length of the critical segment along the governing member.",
            GH_ParamAccess.list);
        _outTotalLengths = pManager.AddNumberParameter("Total Lengths", "TotL",
            "Total length of the governing member.",
            GH_ParamAccess.list);
        _outYields = pManager.AddNumberParameter("Yield Strengths", "Fy",
            "Yield stress of the steel section.",
            GH_ParamAccess.list);
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
        if (_previewMembers.Count == 0) return;

        for (var i = 0; i < _previewMembers.Count; i++)
        {
            var m = _previewMembers[i];
            args.Display.DrawLine(new Line(m.Start, m.End), m.Color, 4);
        }

        if (_showValues)
            for (var i = 0; i < _previewMembers.Count; i++)
            {
                var m = _previewMembers[i];
                var mid = new Point3d(
                    (m.Start.X + m.End.X) * 0.5,
                    (m.Start.Y + m.End.Y) * 0.5,
                    (m.Start.Z + m.End.Z) * 0.5);
                args.Display.Draw2dText(m.LoadFactor.ToString("G4"), m.Color, mid, false, 12);
            }
    }

    public override BoundingBox ClippingBox
    {
        get
        {
            var box = base.ClippingBox;
            foreach (var m in _previewMembers)
            {
                box.Union(m.Start);
                box.Union(m.End);
            }
            return box;
        }
    }

    private sealed class PreviewSteelMemberLine
    {
        public PreviewSteelMemberLine(Point3d start, Point3d end, Color color, double loadFactor)
        {
            Start = start;
            End = end;
            Color = color;
            LoadFactor = loadFactor;
        }

        public Point3d Start { get; }
        public Point3d End { get; }
        public Color Color { get; }
        public double LoadFactor { get; }
    }

    // ── Worker ────────────────────────────────────────────────────

    private sealed class GetSteelMemberCheckSummaryWorker : WorkerInstance<GetSteelMemberCheckSummaryComponent>
    {
        public GetSteelMemberCheckSummaryWorker(
            GetSteelMemberCheckSummaryComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken)
        {
        }

        private SgModelData InputModel { get; set; }
        private List<int> DesignGroupFilter { get; set; }
        private bool ShowValues { get; set; }

        private List<int> OutDesignGroupIds { get; set; } = new();
        private List<Line> OutRepresentativeLines { get; set; } = new();
        private List<string> OutSections { get; set; } = new();
        private List<string> OutFlags { get; set; } = new();
        private List<double> OutLoadFactors { get; set; } = new();
        private List<int> OutCriticalCases { get; set; } = new();
        private List<string> OutCriticalCaseNames { get; set; } = new();
        private List<string> OutFailureModes { get; set; } = new();
        private List<double> OutSegmentLengths { get; set; } = new();
        private List<double> OutTotalLengths { get; set; } = new();
        private List<double> OutYields { get; set; } = new();
        private string OutWarningsText { get; set; }
        private string Status { get; set; } = string.Empty;
        private List<PreviewSteelMemberLine> PreviewMembers { get; set; } = new();

        public override WorkerInstance<GetSteelMemberCheckSummaryComponent> Duplicate(
            string id, CancellationToken cancellationToken)
        {
            return new GetSteelMemberCheckSummaryWorker(Parent, id, cancellationToken);
        }

        public override void GetData(IGH_DataAccess da, GH_ComponentParamServer paramServer)
        {
            var modelGoo = new GH_SgModel();
            if (!da.GetData(Parent._inModel, ref modelGoo) || modelGoo?.Value == null)
                return;
            InputModel = modelGoo.Value;

            var designGroupIds = new List<GH_Integer>();
            da.GetDataList(Parent._inDesignGroups, designGroupIds);
            DesignGroupFilter = new List<int>();
            foreach (var g in designGroupIds)
                if (g != null)
                    DesignGroupFilter.Add(g.Value);

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
                await QuerySteelChecksAsync();
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                var message = ModelAssembler.FormatApiError(ex, "querying steel member check summary");
                Status = $"Error: {message}";
                SetComponentMessage("Error");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        private async Task QuerySteelChecksAsync()
        {
            var session = SpaceGassSessionManager.Current;
            if (session == null || !session.IsConnected)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Not connected. Place a SpaceGass Connect component and set Connect? to true.");
                SetComponentMessage("Not connected");
                return;
            }

            var result = await session.GetSteelMemberCheckSummaryAsync(
                InputModel, DesignGroupFilter, CancellationToken).ConfigureAwait(false);

            foreach (var w in result.Warnings)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
            OutWarningsText = result.Warnings.Count > 0 ? string.Join(Environment.NewLine, result.Warnings) : "";

            if (result.Checks.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "No steel member check summary results found. Run Steel Design in SpaceGass first.");
                SetComponentMessage("No steel design");
                Status = "0 steel design-group checks queried.";
                return;
            }

            // Build reverse ID→geometry map for representative-line output
            var idToLine = new Dictionary<int, Line>();
            foreach (var kvp in InputModel.MemberMap)
                idToLine[kvp.Key] = new Line(kvp.Value.Start.ToPoint3d(), kvp.Value.End.ToPoint3d());

            var idToLcName = InputModel.BuildLoadCaseIdToNameMap();

            foreach (var c in result.Checks)
            {
                OutDesignGroupIds.Add(c.DesignGroupId);
                OutRepresentativeLines.Add(idToLine.TryGetValue(c.DesignGroupId, out var ln) ? ln : Line.Unset);
                OutSections.Add(c.Section);
                OutFlags.Add(c.Flag);
                OutLoadFactors.Add(c.LoadFactor);
                OutCriticalCases.Add(c.CriticalCaseId ?? 0);
                OutCriticalCaseNames.Add(c.CriticalCaseId.HasValue
                    ? (idToLcName.TryGetValue(c.CriticalCaseId.Value, out var lcName)
                        ? lcName
                        : $"Load Case {c.CriticalCaseId.Value}")
                    : string.Empty);
                OutFailureModes.Add(c.FailureMode);
                OutSegmentLengths.Add(c.SegmentLength);
                OutTotalLengths.Add(c.TotalLength);
                OutYields.Add(c.Yield);
            }

            // Preview: one coloured representative line per design group
            var memberMapSg = new Dictionary<int, (SgPoint3D Start, SgPoint3D End)>();
            foreach (var kvp in InputModel.MemberMap) memberMapSg[kvp.Key] = kvp.Value;

            var previewResult = SteelUtilisationPreviewBuilder.Build(result.Checks, memberMapSg);
            PreviewMembers = previewResult.Members
                .Select(m => new PreviewSteelMemberLine(
                    m.Start.ToPoint3d(),
                    m.End.ToPoint3d(),
                    Color.FromArgb(m.Rgb.R, m.Rgb.G, m.Rgb.B),
                    m.LoadFactor))
                .ToList();

            SetComponentMessage($"{result.Checks.Count} design-group checks");
            Status = $"{result.Checks.Count} steel design-group checks queried.";
        }

        public override void SetData(IGH_DataAccess da)
        {
            da.SetDataList(Parent._outDesignGroups, OutDesignGroupIds);
            da.SetDataList(Parent._outRepresentativeLines, OutRepresentativeLines);
            da.SetDataList(Parent._outSections, OutSections);
            da.SetDataList(Parent._outFlags, OutFlags);
            da.SetDataList(Parent._outLoadFactors, OutLoadFactors);
            da.SetDataList(Parent._outCriticalCases, OutCriticalCases);
            da.SetDataList(Parent._outCriticalCaseNames, OutCriticalCaseNames);
            da.SetDataList(Parent._outFailureModes, OutFailureModes);
            da.SetDataList(Parent._outSegmentLengths, OutSegmentLengths);
            da.SetDataList(Parent._outTotalLengths, OutTotalLengths);
            da.SetDataList(Parent._outYields, OutYields);
            da.SetData(Parent._outWarnings, OutWarningsText ?? "");
            da.SetData(Parent._outStatus, Status);

            Parent._previewMembers = PreviewMembers;
            Parent._showValues = ShowValues;
        }
    }
}
