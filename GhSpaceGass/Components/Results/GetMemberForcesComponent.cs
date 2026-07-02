using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GhSpaceGass.Async;
using GhSpaceGass.Core.Models;
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
    private int _inLoadCases;
    private int _inMembers;
    private int _inMode;
    private int _inModel;
    
    private int _outFx, _outFy, _outFz;
    private int _outLines;
    private int _outLoadCases;
    private int _outMembers;
    private int _outMx, _outMy, _outMz;
    private int _outNodes;
    private int _outPoints;
    private int _outStations;
    private int _outWarnings;

    public GetMemberForcesComponent()
        : base("SG Member Forces", "sgMemberForces",
            "Query member force results from a completed SpaceGass analysis. " +
            "Supports end forces and intermediate station forces.",
            "SpaceGass", "8 | Results")
    {
        BaseWorker = new GetMemberForcesWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.secondary;
    protected override Bitmap Icon => Icons.IconFactory.MemberForces();
    public override Guid ComponentGuid => new("A9DB1524-BAE6-4EBA-B0EF-93DA9090D0DF");

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
            new Param_SgIntegerOption("Mode", ValueListHelper.ForceModeOptions, defaultValue: 0, autoCreate: true),
            "Mode", "Mo",
            "End Forces=0 (forces at each member end), Intermediate=1 (forces at stations along member).\n" +
            "Default: End Forces",
            GH_ParamAccess.item);

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
            "Member geometry, branched by {load_case; member}. Only populated in Intermediate mode.",
            GH_ParamAccess.tree);
        _outNodes = pManager.AddIntegerParameter("Nodes", "N",
            "Node IDs, branched by {load_case; member}. Only populated in End Forces mode.",
            GH_ParamAccess.tree);
        _outPoints = pManager.AddPointParameter("Points", "P",
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
        private int Mode { get; set; } // 0 = End Forces, 1 = Intermediate

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

            var mode = 0;
            da.GetData(Parent._inMode, ref mode);
            Mode = mode;
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
                Parent.Message = "Error";
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
                Parent.Message = "Not connected";
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
                Parent.Message = "No end forces";
                InitEmptyOutputs();
                return;
            }

            // Build reverse maps
            var idToPoint = new Dictionary<int, Point3d>();
            foreach (var kvp in InputModel.NodeMap)
                idToPoint[kvp.Value] = new Point3d(kvp.Key.X, kvp.Key.Y, kvp.Key.Z);

            var idToLcName = new Dictionary<int, string>();
            foreach (var kvp in InputModel.LoadCaseMap)
                idToLcName[kvp.Value] = kvp.Key;
            foreach (var kvp in InputModel.CombinationLoadCaseMap)
                idToLcName[kvp.Value] = kvp.Key;

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

            Parent.Message = $"{result.EndForces.Count} end forces";
        }

        private async Task QueryIntermediateForcesAsync()
        {
            var session = SpaceGassSessionManager.Current;
            if (session == null || !session.IsConnected)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Not connected. Place a SpaceGass Connect component and set Connect? to true.");
                Parent.Message = "Not connected";
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
                Parent.Message = "No intermediate forces";
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
                    new Point3d(s.X, s.Y, s.Z),
                    new Point3d(e.X, e.Y, e.Z));
            }

            var idToLcName = new Dictionary<int, string>();
            foreach (var kvp in InputModel.LoadCaseMap)
                idToLcName[kvp.Value] = kvp.Key;
            foreach (var kvp in InputModel.CombinationLoadCaseMap)
                idToLcName[kvp.Value] = kvp.Key;

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

            Parent.Message = $"{result.Forces.Count} intermediate forces";
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
        }
    }
}