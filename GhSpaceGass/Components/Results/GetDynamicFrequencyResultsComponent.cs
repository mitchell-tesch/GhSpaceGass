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

public class GetDynamicFrequencyResultsComponent : GH_AsyncComponent<GetDynamicFrequencyResultsComponent>
{
    private int _inLoadCases;
    private int _inModel;
    private int _inModes;
    private int _inNodes;
    
    private int _outFrequency;
    private int _outLoadCases;
    private int _outMassPartX, _outMassPartY, _outMassPartZ;
    private int _outNodes;
    private int _outModes;
    private int _outPeriod;
    private int _outPoints;
    private int _outRx, _outRy, _outRz;
    private int _outTx, _outTy, _outTz;
    private int _outWarnings;
    private int _outStatus;

    public GetDynamicFrequencyResultsComponent()
        : base("SG Dynamic Frequencies", "sgDynFreq",
            "Query dynamic frequency analysis results (natural frequencies and mode shapes) " +
            "from a completed SpaceGass dynamic frequency analysis.",
            "SpaceGass", "8 | Results")
    {
        BaseWorker = new GetDynamicFrequencyResultsWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.quarternary;
    protected override Bitmap Icon => Icons.IconFactory.DynamicFrequencyResults();
    public override Guid ComponentGuid => new("B8F2A4C1-7D3E-4A9B-8E5F-1C6D9A0B3E72");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The assembled and analysed SpaceGass model.",
            GH_ParamAccess.item);
        _inNodes = pManager.AddIntegerParameter("Node IDs", "NIds",
            "Optional: filter mode shapes to specific node IDs.",
            GH_ParamAccess.list);
        _inModes = pManager.AddIntegerParameter("Modes", "Mo",
            "Optional: filter results to these mode numbers only.",
            GH_ParamAccess.list);
        _inLoadCases = pManager.AddTextParameter("Load Cases", "LC",
            "Optional: filter results to these load case names only.",
            GH_ParamAccess.list);

        pManager[_inNodes].Optional = true;
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
        _outFrequency = pManager.AddNumberParameter("Frequency", "F",
            "Natural frequency (Hz), branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outPeriod = pManager.AddNumberParameter("Period", "P",
            "Natural period (seconds), branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outMassPartX = pManager.AddNumberParameter("Mass Part. X", "MpX",
            "Mass participation ratio in X, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outMassPartY = pManager.AddNumberParameter("Mass Part. Y", "MpY",
            "Mass participation ratio in Y, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outMassPartZ = pManager.AddNumberParameter("Mass Part. Z", "MpZ",
            "Mass participation ratio in Z, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outNodes = pManager.AddIntegerParameter("Node IDs", "NIds",
            "Node IDs, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outPoints = pManager.AddPointParameter("Node Points", "Pt",
            "Node locations for mode shapes, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outTx = pManager.AddNumberParameter("Tx", "Tx",
            "Mode shape translation in X, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outTy = pManager.AddNumberParameter("Ty", "Ty",
            "Mode shape translation in Y, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outTz = pManager.AddNumberParameter("Tz", "Tz",
            "Mode shape translation in Z, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outRx = pManager.AddNumberParameter("Rx", "Rx",
            "Mode shape rotation about X, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outRy = pManager.AddNumberParameter("Ry", "Ry",
            "Mode shape rotation about Y, branched by {load_case; mode}.",
            GH_ParamAccess.tree);
        _outRz = pManager.AddNumberParameter("Rz", "Rz",
            "Mode shape rotation about Z, branched by {load_case; mode}.",
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

    private sealed class GetDynamicFrequencyResultsWorker : WorkerInstance<GetDynamicFrequencyResultsComponent>
    {
        public GetDynamicFrequencyResultsWorker(
            GetDynamicFrequencyResultsComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken)
        {
        }

        private SgModelData InputModel { get; set; }
        private List<int> NodesFilter { get; set; }
        private List<int> ModesFilter { get; set; }
        private List<string> LoadCaseFilter { get; set; }

        // Natural frequency outputs (branched by {load_case; mode})
        private GH_Structure<GH_Number> OutFrequency { get; set; }
        private GH_Structure<GH_Number> OutPeriod { get; set; }
        private GH_Structure<GH_Number> OutMassPartX { get; set; }
        private GH_Structure<GH_Number> OutMassPartY { get; set; }
        private GH_Structure<GH_Number> OutMassPartZ { get; set; }

        // Mode shape outputs (branched by {load_case; mode})
        private GH_Structure<GH_Point> OutPoints { get; set; }
        private GH_Structure<GH_Number> OutTx { get; set; }
        private GH_Structure<GH_Number> OutTy { get; set; }
        private GH_Structure<GH_Number> OutTz { get; set; }
        private GH_Structure<GH_Number> OutRx { get; set; }
        private GH_Structure<GH_Number> OutRy { get; set; }
        private GH_Structure<GH_Number> OutRz { get; set; }

        private GH_Structure<GH_Integer> OutModes { get; set; }
        private GH_Structure<GH_String> OutLoadCases { get; set; }
        private GH_Structure<GH_Integer> OutNodes { get; set; }
        private string OutWarningsText { get; set; }
        private string Status { get; set; } = string.Empty;

        public override WorkerInstance<GetDynamicFrequencyResultsComponent> Duplicate(
            string id, CancellationToken cancellationToken)
        {
            return new GetDynamicFrequencyResultsWorker(Parent, id, cancellationToken);
        }

        public override void GetData(IGH_DataAccess da, GH_ComponentParamServer paramServer)
        {
            var modelGoo = new GH_SgModel();
            if (!da.GetData(Parent._inModel, ref modelGoo) || modelGoo?.Value == null)
                return;
            InputModel = modelGoo.Value;

            var nodeIds = new List<GH_Integer>();
            da.GetDataList(Parent._inNodes, nodeIds);
            NodesFilter = new List<int>();
            foreach (var g in nodeIds)
                if (g != null)
                    NodesFilter.Add(g.Value);

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
                SetComponentMessage("No model");
                if (!CancellationToken.IsCancellationRequested) done();
                return;
            }

            try
            {
                SetComponentMessage("Querying...");
                await QueryDynamicFrequencyResultsAsync();
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                var message = ModelAssembler.FormatApiError(ex, "querying dynamic frequency results");
                Status = $"Error: {message}";
                SetComponentMessage("Error");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        private async Task QueryDynamicFrequencyResultsAsync()
        {
            var session = SpaceGassSessionManager.Current;
            if (session == null || !session.IsConnected)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Not connected. Place a SpaceGass Connect component and set Connect? to true.");
                SetComponentMessage("Not connected");
                return;
            }

            var result = await session.GetDynamicFrequencyResultsAsync(
                InputModel, ModesFilter, NodesFilter, LoadCaseFilter,
                CancellationToken).ConfigureAwait(false);

            foreach (var w in result.Warnings)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
            OutWarningsText = result.Warnings.Count > 0 ? string.Join(Environment.NewLine, result.Warnings) : "";

            // Initialize all outputs
            OutFrequency = new GH_Structure<GH_Number>();
            OutPeriod = new GH_Structure<GH_Number>();
            OutMassPartX = new GH_Structure<GH_Number>();
            OutMassPartY = new GH_Structure<GH_Number>();
            OutMassPartZ = new GH_Structure<GH_Number>();
            OutPoints = new GH_Structure<GH_Point>();
            OutTx = new GH_Structure<GH_Number>();
            OutTy = new GH_Structure<GH_Number>();
            OutTz = new GH_Structure<GH_Number>();
            OutRx = new GH_Structure<GH_Number>();
            OutRy = new GH_Structure<GH_Number>();
            OutRz = new GH_Structure<GH_Number>();
            OutModes = new GH_Structure<GH_Integer>();
            OutLoadCases = new GH_Structure<GH_String>();
            OutNodes = new GH_Structure<GH_Integer>();

            if (result.NaturalFrequencies.Count == 0 && result.ModeShapes.Count == 0)
            {
                SetComponentMessage("No dynamic frequency results");
                Status = "0 frequencies, 0 mode shapes queried.";
                return;
            }

            // Build reverse node map (ID → point)
            var idToPoint = new Dictionary<int, Point3d>();
            foreach (var kvp in InputModel.NodeMap)
                idToPoint[kvp.Value] = new Point3d(kvp.Key.X, kvp.Key.Y, kvp.Key.Z);

            // Build reverse ID→name map for load cases
            var idToLcName = InputModel.BuildLoadCaseIdToNameMap();

            // ── Collect all unique load cases and modes from both results ──
            var allLoadCaseIds = new SortedSet<int>();
            var allModes = new SortedSet<int>();
            foreach (var nf in result.NaturalFrequencies)
            {
                allLoadCaseIds.Add(nf.LoadCaseId);
                allModes.Add(nf.Mode);
            }
            foreach (var ms in result.ModeShapes)
            {
                allLoadCaseIds.Add(ms.LoadCaseId);
                allModes.Add(ms.Mode);
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

            // ── Natural Frequencies: tree by {load_case; mode} ────────
            var nfByLoadCase = result.NaturalFrequencies
                .GroupBy(nf => nf.LoadCaseId)
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var lcGroup in nfByLoadCase)
            {
                var li = lcIndexMap[lcGroup.Key];
                var lcNameStr = idToLcName.TryGetValue(lcGroup.Key, out var lcName)
                    ? lcName
                    : $"Load Case {lcGroup.Key}";

                foreach (var nf in lcGroup.OrderBy(nf => nf.Mode))
                {
                    var mi = modeIndexMap[nf.Mode];
                    var path = new GH_Path(li, mi);
                    OutLoadCases.Append(new GH_String(lcNameStr), path);
                    OutModes.Append(new GH_Integer(nf.Mode), path);
                    OutFrequency.Append(new GH_Number(nf.Frequency), path);
                    OutPeriod.Append(new GH_Number(nf.Period), path);
                    OutMassPartX.Append(new GH_Number(nf.MassPartX), path);
                    OutMassPartY.Append(new GH_Number(nf.MassPartY), path);
                    OutMassPartZ.Append(new GH_Number(nf.MassPartZ), path);
                }
            }

            // ── Mode Shapes: tree by {load_case; mode} with nodes as list items ──
            var msByLoadCase = result.ModeShapes
                .GroupBy(ms => ms.LoadCaseId)
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var lcGroup in msByLoadCase)
            {
                var li = lcIndexMap[lcGroup.Key];

                var byMode = lcGroup
                    .GroupBy(ms => ms.Mode)
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var modeGroup in byMode)
                {
                    var mi = modeIndexMap[modeGroup.Key];
                    var path = new GH_Path(li, mi);

                    foreach (var ms in modeGroup.OrderBy(ms => ms.NodeId))
                    {
                        OutNodes.Append(new GH_Integer(ms.NodeId), path);
                        OutPoints.Append(
                            idToPoint.TryGetValue(ms.NodeId, out var npt)
                                ? new GH_Point(npt)
                                : new GH_Point(Point3d.Unset), path);
                        OutTx.Append(new GH_Number(ms.Tx), path);
                        OutTy.Append(new GH_Number(ms.Ty), path);
                        OutTz.Append(new GH_Number(ms.Tz), path);
                        OutRx.Append(new GH_Number(ms.Rx), path);
                        OutRy.Append(new GH_Number(ms.Ry), path);
                        OutRz.Append(new GH_Number(ms.Rz), path);
                    }
                }
            }

            var nfCount = result.NaturalFrequencies.Count;
            var msCount = result.ModeShapes.Count;
            SetComponentMessage($"{nfCount} frequencies, {msCount} mode shapes");
            Status = $"{nfCount} frequencies, {msCount} mode shapes queried.";
        }

        public override void SetData(IGH_DataAccess da)
        {
            if (OutFrequency != null) da.SetDataTree(Parent._outFrequency, OutFrequency);
            if (OutPeriod != null) da.SetDataTree(Parent._outPeriod, OutPeriod);
            if (OutMassPartX != null) da.SetDataTree(Parent._outMassPartX, OutMassPartX);
            if (OutMassPartY != null) da.SetDataTree(Parent._outMassPartY, OutMassPartY);
            if (OutMassPartZ != null) da.SetDataTree(Parent._outMassPartZ, OutMassPartZ);
            if (OutPoints != null) da.SetDataTree(Parent._outPoints, OutPoints);
            if (OutTx != null) da.SetDataTree(Parent._outTx, OutTx);
            if (OutTy != null) da.SetDataTree(Parent._outTy, OutTy);
            if (OutTz != null) da.SetDataTree(Parent._outTz, OutTz);
            if (OutRx != null) da.SetDataTree(Parent._outRx, OutRx);
            if (OutRy != null) da.SetDataTree(Parent._outRy, OutRy);
            if (OutRz != null) da.SetDataTree(Parent._outRz, OutRz);
            if (OutModes != null) da.SetDataTree(Parent._outModes, OutModes);
            if (OutLoadCases != null) da.SetDataTree(Parent._outLoadCases, OutLoadCases);
            if (OutNodes != null) da.SetDataTree(Parent._outNodes, OutNodes);
            da.SetData(Parent._outWarnings, OutWarningsText ?? "");
            da.SetData(Parent._outStatus, Status);
        }
    }
}
