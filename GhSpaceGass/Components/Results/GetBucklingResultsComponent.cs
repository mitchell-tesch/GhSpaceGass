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

namespace GhSpaceGass.Components.Results;

public class GetBucklingResultsComponent : GH_AsyncComponent<GetBucklingResultsComponent>
{
    private int _inLoadCases;
    private int _inMembers;
    private int _inModel;
    private int _inModes;
    
    private int _outLength;
    private int _outLey, _outLez;
    private int _outLines;
    private int _outLoadCases;
    private int _outLoadFactors;
    private int _outMembers;
    private int _outModes;
    private int _outNodeAtMaxRotn;
    private int _outNodeAtMaxTrans;
    private int _outPcr;
    private int _outRotnAxis;
    private int _outTransAxis;
    private int _outWarnings;

    public GetBucklingResultsComponent()
        : base("SG Buckling Results", "sgBuckling",
            "Query buckling analysis results (load factors and effective lengths) " +
            "from a completed SpaceGass buckling analysis.",
            "SpaceGass", "8 | Results")
    {
        BaseWorker = new GetBucklingResultsWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.quinary;
    protected override Bitmap Icon => Icons.IconFactory.BucklingResults();
    public override Guid ComponentGuid => new("E176E5DA-915E-43F6-8F6A-AAE7928F84DC");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The assembled and analysed SpaceGass model.",
            GH_ParamAccess.item);
        _inMembers = pManager.AddLineParameter("Members", "Mb",
            "Optional: filter effective lengths to these member geometries only.",
            GH_ParamAccess.list);
        _inModes = pManager.AddIntegerParameter("Modes", "Mo",
            "Optional: filter results to these buckling mode numbers only.",
            GH_ParamAccess.list);
        _inLoadCases = pManager.AddTextParameter("Load Cases", "LC",
            "Optional: filter results to these load case names only.",
            GH_ParamAccess.list);

        pManager[_inMembers].Optional = true;
        pManager[_inModes].Optional = true;
        pManager[_inLoadCases].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outLoadCases = pManager.AddTextParameter("Load Cases", "LC",
            "Load case names, one per branch matching the results tree.",
            GH_ParamAccess.tree);
        _outModes = pManager.AddIntegerParameter("Modes", "Mo",
            "Mode numbers, one per branch matching the results tree.",
            GH_ParamAccess.tree);
        _outLoadFactors = pManager.AddNumberParameter("Load Factors", "LF",
            "Buckling load factor, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outNodeAtMaxTrans = pManager.AddPointParameter("Node At Max Translation", "NT",
            "Point location of node at maximum translation, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outTransAxis = pManager.AddTextParameter("Translation Axis", "TA",
            "Axis of maximum translation, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outNodeAtMaxRotn = pManager.AddPointParameter("Node At Max Rotation", "NR",
            "Point location of node at maximum rotation, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outRotnAxis = pManager.AddTextParameter("Rotation Axis", "RA",
            "Axis of maximum rotation, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outMembers = pManager.AddIntegerParameter("Members", "Mb",
            "Member IDs, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outLines = pManager.AddLineParameter("Lines", "L",
            "Member geometry, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outLength = pManager.AddNumberParameter("Length", "Len",
            "Member length, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outPcr = pManager.AddNumberParameter("Pcr", "Pcr",
            "Critical buckling load, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outLey = pManager.AddNumberParameter("Ley", "Ley",
            "Effective length about Y, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outLez = pManager.AddNumberParameter("Lez", "Lez",
            "Effective length about Z, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outWarnings = pManager.AddTextParameter("Warnings", "W",
            "Warnings from the SpaceGass API query (multiline text).",
            GH_ParamAccess.item);
    }

    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
        base.AppendAdditionalMenuItems(menu);
        Menu_AppendItem(menu, "Cancel", (s, e) => { RequestCancellation(); });
    }

    // ── Worker ────────────────────────────────────────────────────

    private sealed class GetBucklingResultsWorker : WorkerInstance<GetBucklingResultsComponent>
    {
        public GetBucklingResultsWorker(
            GetBucklingResultsComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken)
        {
        }

        private SgModelData InputModel { get; set; }
        private List<(SgPoint3D Start, SgPoint3D End)> MemberFilter { get; set; }
        private List<int> ModesFilter { get; set; }
        private List<string> LoadCaseFilter { get; set; }

        // Load factor outputs (branched by {load_case; mode})
        private GH_Structure<GH_Number> OutLoadFactors { get; set; }
        private GH_Structure<GH_Point> OutNodeAtMaxTrans { get; set; }
        private GH_Structure<GH_String> OutTransAxis { get; set; }
        private GH_Structure<GH_Point> OutNodeAtMaxRotn { get; set; }
        private GH_Structure<GH_String> OutRotnAxis { get; set; }

        // Effective length outputs (branched by {load_case; mode; member})
        private GH_Structure<GH_Number> OutLey { get; set; }
        private GH_Structure<GH_Number> OutLez { get; set; }
        private GH_Structure<GH_Number> OutPcr { get; set; }
        private GH_Structure<GH_Number> OutLength { get; set; }

        private GH_Structure<GH_String> OutLoadCases { get; set; }
        private GH_Structure<GH_Integer> OutModes { get; set; }
        private GH_Structure<GH_Integer> OutMembers { get; set; }
        private GH_Structure<GH_Line> OutLines { get; set; }
        private string OutWarningsText { get; set; }

        public override WorkerInstance<GetBucklingResultsComponent> Duplicate(
            string id, CancellationToken cancellationToken)
        {
            return new GetBucklingResultsWorker(Parent, id, cancellationToken);
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

            var modes = new List<GH_Integer>();
            if (da.GetDataList(Parent._inModes, modes) && modes.Count > 0)
                ModesFilter = modes
                    .Where(m => m != null)
                    .Select(m => m.Value)
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
                await QueryBucklingResultsAsync();
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                Parent.Message = "Error";
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        private async Task QueryBucklingResultsAsync()
        {
            var session = SpaceGassSessionManager.Current;
            if (session == null || !session.IsConnected)
            {
                Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Not connected. Place a SpaceGass Connect component and set Connect? to true.");
                Parent.Message = "Not connected";
                return;
            }

            var result = await session.GetBucklingResultsAsync(
                InputModel, MemberFilter, ModesFilter, LoadCaseFilter,
                CancellationToken).ConfigureAwait(false);

            foreach (var w in result.Warnings)
                Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
            OutWarningsText = result.Warnings.Count > 0 ? string.Join(Environment.NewLine, result.Warnings) : "";

            // Initialize all outputs
            OutLoadFactors = new GH_Structure<GH_Number>();
            OutNodeAtMaxTrans = new GH_Structure<GH_Point>();
            OutTransAxis = new GH_Structure<GH_String>();
            OutNodeAtMaxRotn = new GH_Structure<GH_Point>();
            OutRotnAxis = new GH_Structure<GH_String>();
            OutLey = new GH_Structure<GH_Number>();
            OutLez = new GH_Structure<GH_Number>();
            OutPcr = new GH_Structure<GH_Number>();
            OutLength = new GH_Structure<GH_Number>();
            OutLoadCases = new GH_Structure<GH_String>();
            OutModes = new GH_Structure<GH_Integer>();
            OutMembers = new GH_Structure<GH_Integer>();
            OutLines = new GH_Structure<GH_Line>();

            if (result.LoadFactors.Count == 0 && result.EffectiveLengths.Count == 0)
            {
                Parent.Message = "No buckling results";
                return;
            }

            // Build reverse ID→name map for load cases
            var idToLcName = new Dictionary<int, string>();
            foreach (var kvp in InputModel.LoadCaseMap)
                idToLcName[kvp.Value] = kvp.Key;
            foreach (var kvp in InputModel.CombinationLoadCaseMap)
                idToLcName[kvp.Value] = kvp.Key;

            // Build reverse member ID→geometry map
            var idToMemberLine = new Dictionary<int, Line>();
            foreach (var kvp in InputModel.MemberMap)
            {
                var s = kvp.Value.Start;
                var e = kvp.Value.End;
                idToMemberLine[kvp.Key] = new Line(
                    new Point3d(s.X, s.Y, s.Z),
                    new Point3d(e.X, e.Y, e.Z));
            }

            // ── Collect all unique (load case, mode) pairs from both results ──
            var allLoadCaseIds = new SortedSet<int>();
            var allModes = new SortedSet<int>();
            foreach (var lf in result.LoadFactors)
            {
                allLoadCaseIds.Add(lf.LoadCaseId);
                allModes.Add(lf.Mode);
            }
            foreach (var el in result.EffectiveLengths)
            {
                allLoadCaseIds.Add(el.LoadCaseId);
                allModes.Add(el.Mode);
            }

            var lcIndexMap = new Dictionary<int, int>();
            var lcIdx = 0;
            foreach (var lcId in allLoadCaseIds)
            {
                lcIndexMap[lcId] = lcIdx;
                lcIdx++;
            }

            var modeIndexMap = new Dictionary<int, int>();
            var modeIdx = 0;
            foreach (var mode in allModes)
            {
                modeIndexMap[mode] = modeIdx;
                modeIdx++;
            }

            // ── Load Factors: tree by {load_case; mode} ─────────────
            var lfByLoadCase = result.LoadFactors
                .GroupBy(lf => lf.LoadCaseId)
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var lcGroup in lfByLoadCase)
            {
                var li = lcIndexMap[lcGroup.Key];
                var lcNameStr = idToLcName.TryGetValue(lcGroup.Key, out var lcName)
                    ? lcName
                    : $"Load Case {lcGroup.Key}";

                foreach (var lf in lcGroup.OrderBy(lf => lf.Mode))
                {
                    var mi = modeIndexMap[lf.Mode];
                    var path = new GH_Path(li, mi);
                    OutLoadCases.Append(new GH_String(lcNameStr), path);
                    OutModes.Append(new GH_Integer(lf.Mode), path);
                    OutLoadFactors.Append(new GH_Number(lf.LoadFactor), path);
                    OutNodeAtMaxTrans.Append(
                        lf.NodeAtMaxTranslation.HasValue
                            ? new GH_Point(new Point3d(lf.NodeAtMaxTranslation.Value.X,
                                lf.NodeAtMaxTranslation.Value.Y, lf.NodeAtMaxTranslation.Value.Z))
                            : new GH_Point(Point3d.Unset), path);
                    OutTransAxis.Append(new GH_String(lf.TranslationAxis), path);
                    OutNodeAtMaxRotn.Append(
                        lf.NodeAtMaxRotation.HasValue
                            ? new GH_Point(new Point3d(lf.NodeAtMaxRotation.Value.X,
                                lf.NodeAtMaxRotation.Value.Y, lf.NodeAtMaxRotation.Value.Z))
                            : new GH_Point(Point3d.Unset), path);
                    OutRotnAxis.Append(new GH_String(lf.RotationAxis), path);
                }
            }

            // ── Effective Lengths: tree by {load_case; mode} ─────
            // Group by load case, then mode — members are list items within each branch
            var elByLoadCase = result.EffectiveLengths
                .GroupBy(el => el.LoadCaseId)
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var lcGroup in elByLoadCase)
            {
                var li = lcIndexMap[lcGroup.Key];

                var byMode = lcGroup
                    .GroupBy(el => el.Mode)
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var modeGroup in byMode)
                {
                    var mi = modeIndexMap[modeGroup.Key];
                    var path = new GH_Path(li, mi);

                    foreach (var el in modeGroup.OrderBy(el => el.MemberId))
                    {
                        OutMembers.Append(new GH_Integer(el.MemberId), path);
                        OutLines.Append(
                            idToMemberLine.TryGetValue(el.MemberId, out var ml)
                                ? new GH_Line(ml)
                                : new GH_Line(Line.Unset), path);
                        OutLey.Append(new GH_Number(el.Ley), path);
                        OutLez.Append(new GH_Number(el.Lez), path);
                        OutPcr.Append(new GH_Number(el.Pcr), path);
                        OutLength.Append(new GH_Number(el.Length), path);
                    }
                }
            }

            var lfCount = result.LoadFactors.Count;
            var elCount = result.EffectiveLengths.Count;
            Parent.Message = $"{lfCount} load factors, {elCount} eff. lengths";
        }

        public override void SetData(IGH_DataAccess da)
        {
            if (OutLoadFactors != null) da.SetDataTree(Parent._outLoadFactors, OutLoadFactors);
            if (OutNodeAtMaxTrans != null) da.SetDataTree(Parent._outNodeAtMaxTrans, OutNodeAtMaxTrans);
            if (OutTransAxis != null) da.SetDataTree(Parent._outTransAxis, OutTransAxis);
            if (OutNodeAtMaxRotn != null) da.SetDataTree(Parent._outNodeAtMaxRotn, OutNodeAtMaxRotn);
            if (OutRotnAxis != null) da.SetDataTree(Parent._outRotnAxis, OutRotnAxis);
            if (OutLey != null) da.SetDataTree(Parent._outLey, OutLey);
            if (OutLez != null) da.SetDataTree(Parent._outLez, OutLez);
            if (OutPcr != null) da.SetDataTree(Parent._outPcr, OutPcr);
            if (OutLength != null) da.SetDataTree(Parent._outLength, OutLength);
            if (OutLoadCases != null) da.SetDataTree(Parent._outLoadCases, OutLoadCases);
            if (OutModes != null) da.SetDataTree(Parent._outModes, OutModes);
            if (OutMembers != null) da.SetDataTree(Parent._outMembers, OutMembers);
            if (OutLines != null) da.SetDataTree(Parent._outLines, OutLines);
            da.SetData(Parent._outWarnings, OutWarningsText ?? "");
        }
    }
}