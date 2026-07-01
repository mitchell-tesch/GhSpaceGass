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

public class GetMemberDisplacementsComponent : GH_AsyncComponent<GetMemberDisplacementsComponent>
{
    private int _inLoadCases;
    private int _inMembers;
    private int _inModel;

    private int _outLines;
    private int _outLoadCases;
    private int _outMembers;
    private int _outStations;
    private int _outTxGlobal, _outTyGlobal, _outTzGlobal;
    private int _outTxLocal, _outTyLocal, _outTzLocal;
    private int _outWarnings;

    public GetMemberDisplacementsComponent()
        : base("SG Member Displacements", "sgMemberDisp",
            "Query intermediate member displacement results (global and local translations) " +
            "from a completed SpaceGass analysis.",
            "SpaceGass", "8 | Results")
    {
        BaseWorker = new GetMemberDisplacementsWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.secondary;
    protected override Bitmap Icon => Icons.IconFactory.MemberDisplacements();
    public override Guid ComponentGuid => new("5E830EB3-F118-417E-95B0-E0CA8484D8DC");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The assembled and analysed SpaceGass model.",
            GH_ParamAccess.item);
        _inMembers = pManager.AddLineParameter("Members", "Mb",
            "Optional: filter displacements to these member geometries only.",
            GH_ParamAccess.list);
        _inLoadCases = pManager.AddTextParameter("Load Cases", "LC",
            "Optional: filter displacements to these load case names only.",
            GH_ParamAccess.list);

        pManager[_inMembers].Optional = true;
        pManager[_inLoadCases].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outLoadCases = pManager.AddTextParameter("Load Cases", "LC",
            "Load case names, one per branch matching the load case dimension of the results tree.",
            GH_ParamAccess.tree);
        _outMembers = pManager.AddIntegerParameter("Members", "Mb",
            "Member IDs, branched by {load_case; member}.",
            GH_ParamAccess.tree);
        _outLines = pManager.AddLineParameter("Lines", "L",
            "Member geometry, branched by {load_case; member}.",
            GH_ParamAccess.tree);
        _outStations = pManager.AddNumberParameter("Stations", "S",
            "Position along member, branched by {load_case; member}.",
            GH_ParamAccess.tree);
        _outTxGlobal = pManager.AddNumberParameter("TxGlobal", "TxG",
            "Global translation in X, branched by {load_case; member}.",
            GH_ParamAccess.tree);
        _outTyGlobal = pManager.AddNumberParameter("TyGlobal", "TyG",
            "Global translation in Y, branched by {load_case; member}.",
            GH_ParamAccess.tree);
        _outTzGlobal = pManager.AddNumberParameter("TzGlobal", "TzG",
            "Global translation in Z, branched by {load_case; member}.",
            GH_ParamAccess.tree);
        _outTxLocal = pManager.AddNumberParameter("TxLocal", "TxL",
            "Local translation in X, branched by {load_case; member}.",
            GH_ParamAccess.tree);
        _outTyLocal = pManager.AddNumberParameter("TyLocal", "TyL",
            "Local translation in Y, branched by {load_case; member}.",
            GH_ParamAccess.tree);
        _outTzLocal = pManager.AddNumberParameter("TzLocal", "TzL",
            "Local translation in Z, branched by {load_case; member}.",
            GH_ParamAccess.tree);
        _outWarnings = pManager.AddTextParameter("Warnings", "W",
            "Warnings from the SpaceGass API query (multiline text).",
            GH_ParamAccess.item);
    }

    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
        base.AppendAdditionalMenuItems(menu);
        Menu_AppendItem(menu, "Cancel", (_, _) => { RequestCancellation(); });
    }

    // ── Worker ────────────────────────────────────────────────────

    private sealed class GetMemberDisplacementsWorker : WorkerInstance<GetMemberDisplacementsComponent>
    {
        public GetMemberDisplacementsWorker(
            GetMemberDisplacementsComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken)
        {
        }

        private SgModelData InputModel { get; set; }
        private List<(SgPoint3D Start, SgPoint3D End)> MemberFilter { get; set; }
        private List<string> LoadCaseFilter { get; set; }

        private GH_Structure<GH_Line> OutLines { get; set; }
        private GH_Structure<GH_Number> OutStations { get; set; }
        private GH_Structure<GH_Number> OutTxGlobal { get; set; }
        private GH_Structure<GH_Number> OutTyGlobal { get; set; }
        private GH_Structure<GH_Number> OutTzGlobal { get; set; }
        private GH_Structure<GH_Number> OutTxLocal { get; set; }
        private GH_Structure<GH_Number> OutTyLocal { get; set; }
        private GH_Structure<GH_Number> OutTzLocal { get; set; }
        private GH_Structure<GH_String> OutLoadCases { get; set; }
        private GH_Structure<GH_Integer> OutMembers { get; set; }
        private string OutWarningsText { get; set; }

        public override WorkerInstance<GetMemberDisplacementsComponent> Duplicate(
            string id, CancellationToken cancellationToken)
        {
            return new GetMemberDisplacementsWorker(Parent, id, cancellationToken);
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
                Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                Parent.Message = "Error";
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        private async Task QueryDisplacementsAsync()
        {
            var session = SpaceGassSessionManager.Current;
            if (session == null || !session.IsConnected)
            {
                Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Not connected. Place a SpaceGass Connect component and set Connect? to true.");
                Parent.Message = "Not connected";
                return;
            }

            var result = await session.GetMemberDisplacementsAsync(
                InputModel, MemberFilter, LoadCaseFilter,
                CancellationToken).ConfigureAwait(false);

            foreach (var w in result.Warnings)
                Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
            OutWarningsText = result.Warnings.Count > 0 ? string.Join(Environment.NewLine, result.Warnings) : "";

            if (result.Displacements.Count == 0)
            {
                Parent.Message = "No displacements";
                OutLines = new GH_Structure<GH_Line>();
                OutStations = new GH_Structure<GH_Number>();
                OutTxGlobal = new GH_Structure<GH_Number>();
                OutTyGlobal = new GH_Structure<GH_Number>();
                OutTzGlobal = new GH_Structure<GH_Number>();
                OutTxLocal = new GH_Structure<GH_Number>();
                OutTyLocal = new GH_Structure<GH_Number>();
                OutTzLocal = new GH_Structure<GH_Number>();
                OutLoadCases = new GH_Structure<GH_String>();
                OutMembers = new GH_Structure<GH_Integer>();
                return;
            }

            // Build reverse maps
            var idToMemberLine = new Dictionary<int, Line>();
            foreach (var kvp in InputModel.MemberMap)
            {
                var s = kvp.Value.Start;
                var e = kvp.Value.End;
                idToMemberLine[kvp.Key] = new Line(
                    new Point3d(s.X, s.Y, s.Z),
                    new Point3d(e.X, e.Y, e.Z));
            }

            var idToLcName = new Dictionary<int, string>();
            foreach (var kvp in InputModel.LoadCaseMap)
                idToLcName[kvp.Value] = kvp.Key;
            foreach (var kvp in InputModel.CombinationLoadCaseMap)
                idToLcName[kvp.Value] = kvp.Key;

            // Group by load case, then by member — two-level tree {load_case; member}
            var byLoadCase = result.Displacements
                .GroupBy(d => d.LoadCaseId)
                .OrderBy(g => g.Key)
                .ToList();

            OutLines = new GH_Structure<GH_Line>();
            OutStations = new GH_Structure<GH_Number>();
            OutTxGlobal = new GH_Structure<GH_Number>();
            OutTyGlobal = new GH_Structure<GH_Number>();
            OutTzGlobal = new GH_Structure<GH_Number>();
            OutTxLocal = new GH_Structure<GH_Number>();
            OutTyLocal = new GH_Structure<GH_Number>();
            OutTzLocal = new GH_Structure<GH_Number>();
            OutLoadCases = new GH_Structure<GH_String>();
            OutMembers = new GH_Structure<GH_Integer>();

            // Build member index map across all load cases
            var memberIdOrder = result.Displacements
                .Select(d => d.MemberId)
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
                    .GroupBy(d => d.MemberId)
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
                    foreach (var d in memberGroup.OrderBy(d => d.Location))
                    {
                        OutStations.Append(new GH_Number(d.Location), path);
                        OutTxGlobal.Append(new GH_Number(d.TxGlobal), path);
                        OutTyGlobal.Append(new GH_Number(d.TyGlobal), path);
                        OutTzGlobal.Append(new GH_Number(d.TzGlobal), path);
                        OutTxLocal.Append(new GH_Number(d.TxLocal), path);
                        OutTyLocal.Append(new GH_Number(d.TyLocal), path);
                        OutTzLocal.Append(new GH_Number(d.TzLocal), path);
                    }
                }
            }

            Parent.Message = $"{result.Displacements.Count} displacements";
        }

        public override void SetData(IGH_DataAccess da)
        {
            if (OutLines != null) da.SetDataTree(Parent._outLines, OutLines);
            if (OutStations != null) da.SetDataTree(Parent._outStations, OutStations);
            if (OutTxGlobal != null) da.SetDataTree(Parent._outTxGlobal, OutTxGlobal);
            if (OutTyGlobal != null) da.SetDataTree(Parent._outTyGlobal, OutTyGlobal);
            if (OutTzGlobal != null) da.SetDataTree(Parent._outTzGlobal, OutTzGlobal);
            if (OutTxLocal != null) da.SetDataTree(Parent._outTxLocal, OutTxLocal);
            if (OutTyLocal != null) da.SetDataTree(Parent._outTyLocal, OutTyLocal);
            if (OutTzLocal != null) da.SetDataTree(Parent._outTzLocal, OutTzLocal);
            if (OutLoadCases != null) da.SetDataTree(Parent._outLoadCases, OutLoadCases);
            if (OutMembers != null) da.SetDataTree(Parent._outMembers, OutMembers);
            da.SetData(Parent._outWarnings, OutWarningsText ?? "");
        }
    }
}