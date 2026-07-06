using System;
using System.Drawing;
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

namespace GhSpaceGass.Components.Loads;

public class GetNodeLoadsComponent : GH_AsyncComponent<GetNodeLoadsComponent>
{
    private int _inModel;

    // Shared
    private int _outNodeIds, _outPoints;

    // Node loads
    private int _outNlLcId, _outNlLc, _outNlCat, _outNlFx, _outNlFy, _outNlFz, _outNlMx, _outNlMy, _outNlMz;

    // Lumped mass
    private int _outLmLcId, _outLmLc, _outLmCat, _outLmTmx, _outLmTmy, _outLmTmz, _outLmRmx, _outLmRmy, _outLmRmz;

    // Prescribed displacements
    private int _outPdLcId, _outPdLc, _outPdCat, _outPdTx, _outPdTy, _outPdTz, _outPdRx, _outPdRy, _outPdRz;

    private int _outStatus;

    public GetNodeLoadsComponent()
        : base("SG Get Node Loads", "sgGetNodeLoads",
            "Query all node-based loads (node loads, lumped mass, prescribed displacements)\n" +
            "from the open SpaceGass job, grouped by node.",
            "SpaceGass", "5 | Loads")
    {
        BaseWorker = new GetNodeLoadsWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.last;
    protected override Bitmap Icon => Icons.IconFactory.GetNodeLoads();
    public override Guid ComponentGuid => new("909B5EF4-1A12-44AE-8FDB-E6FF19ECDCCF");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The SpaceGass model (from Assemble or Disassemble).",
            GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        // Shared (one branch per unique node)
        _outNodeIds = pManager.AddIntegerParameter("Node ID", "NId",
            "Node ID per branch (one branch per loaded node, ordered by ID).",
            GH_ParamAccess.tree);
        _outPoints = pManager.AddPointParameter("Node Point", "Pts",
            "Node location per branch.",
            GH_ParamAccess.tree);

        // Node loads
        _outNlLcId = pManager.AddIntegerParameter("NL Load Case IDs", "NL-LCIds",
            "Node load: load case ID per entry.",
            GH_ParamAccess.tree);
        _outNlLc = pManager.AddTextParameter("NL Load Case Names", "NL-LCs",
            "Node load: load case name per entry.",
            GH_ParamAccess.tree);
        _outNlCat = pManager.AddIntegerParameter("NL Category IDs", "NL-CatIds",
            "Node load: category ID per entry (0 if none).",
            GH_ParamAccess.tree);
        _outNlFx = pManager.AddNumberParameter("NL Fx", "NL-Fx",
            "Node load: force X.",
            GH_ParamAccess.tree);
        _outNlFy = pManager.AddNumberParameter("NL Fy", "NL-Fy",
            "Node load: force Y.",
            GH_ParamAccess.tree);
        _outNlFz = pManager.AddNumberParameter("NL Fz", "NL-Fz",
            "Node load: force Z.",
            GH_ParamAccess.tree);
        _outNlMx = pManager.AddNumberParameter("NL Mx", "NL-Mx",
            "Node load: moment X.",
            GH_ParamAccess.tree);
        _outNlMy = pManager.AddNumberParameter("NL My", "NL-My",
            "Node load: moment Y.",
            GH_ParamAccess.tree);
        _outNlMz = pManager.AddNumberParameter("NL Mz", "NL-Mz",
            "Node load: moment Z.",
            GH_ParamAccess.tree);

        // Lumped mass
        _outLmLcId = pManager.AddIntegerParameter("LM Load Case IDs", "LM-LCId",
            "Lumped mass: load case ID per entry.",
            GH_ParamAccess.tree);
        _outLmLc = pManager.AddTextParameter("LM Load Cases", "LM-LC",
            "Lumped mass: load case name per entry.",
            GH_ParamAccess.tree);
        _outLmCat = pManager.AddIntegerParameter("LM Categories", "LM-Cat",
            "Lumped mass: category ID per entry.",
            GH_ParamAccess.tree);
        _outLmTmx = pManager.AddNumberParameter("LM Tmx", "LM-Tmx",
            "Lumped mass: translational mass X.",
            GH_ParamAccess.tree);
        _outLmTmy = pManager.AddNumberParameter("LM Tmy", "LM-Tmy",
            "Lumped mass: translational mass Y.",
            GH_ParamAccess.tree);
        _outLmTmz = pManager.AddNumberParameter("LM Tmz", "LM-Tmz",
            "Lumped mass: translational mass Z.",
            GH_ParamAccess.tree);
        _outLmRmx = pManager.AddNumberParameter("LM Rmx", "LM-Rmx",
            "Lumped mass: rotational mass X.",
            GH_ParamAccess.tree);
        _outLmRmy = pManager.AddNumberParameter("LM Rmy", "LM-Rmy",
            "Lumped mass: rotational mass Y.",
            GH_ParamAccess.tree);
        _outLmRmz = pManager.AddNumberParameter("LM Rmz", "LM-Rmz",
            "Lumped mass: rotational mass Z.",
            GH_ParamAccess.tree);

        // Prescribed displacements
        _outPdLcId = pManager.AddIntegerParameter("PD Load Case IDs", "PDLCId",
            "Prescribed displacement: load case ID per entry.",
            GH_ParamAccess.tree);
        _outPdLc = pManager.AddTextParameter("PD Load Cases", "PDLC",
            "Prescribed displacement: load case name per entry.",
            GH_ParamAccess.tree);
        _outPdCat = pManager.AddIntegerParameter("PD Categories", "PDCat",
            "Prescribed displacement: category ID per entry.",
            GH_ParamAccess.tree);
        _outPdTx = pManager.AddNumberParameter("PD Tx", "PDTx",
            "Prescribed displacement: translation X.",
            GH_ParamAccess.tree);
        _outPdTy = pManager.AddNumberParameter("PD Ty", "PDTy",
            "Prescribed displacement: translation Y.",
            GH_ParamAccess.tree);
        _outPdTz = pManager.AddNumberParameter("PD Tz", "PDTz",
            "Prescribed displacement: translation Z.",
            GH_ParamAccess.tree);
        _outPdRx = pManager.AddNumberParameter("PD Rx", "PDRx",
            "Prescribed displacement: rotation X.",
            GH_ParamAccess.tree);
        _outPdRy = pManager.AddNumberParameter("PD Ry", "PDRy",
            "Prescribed displacement: rotation Y.",
            GH_ParamAccess.tree);
        _outPdRz = pManager.AddNumberParameter("PD Rz", "PDRz",
            "Prescribed displacement: rotation Z.",
            GH_ParamAccess.tree);

        _outStatus = pManager.AddTextParameter("Status", "S",
            "Query status and warnings.", GH_ParamAccess.item);
    }

    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
        base.AppendAdditionalMenuItems(menu);
        Menu_AppendItem(menu, "Cancel", (_, _) => { RequestCancellation(); });
    }

    // ── Worker ────────────────────────────────────────────────────

    private sealed class GetNodeLoadsWorker : WorkerInstance<GetNodeLoadsComponent>
    {
        public GetNodeLoadsWorker(
            GetNodeLoadsComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken) { }

        private SgModelData Model { get; set; }
        private SgNodeLoadsDataResult Result { get; set; }
        private string Status { get; set; } = string.Empty;

        public override WorkerInstance<GetNodeLoadsComponent> Duplicate(
            string id, CancellationToken cancellationToken)
            => new GetNodeLoadsWorker(Parent, id, cancellationToken);

        public override void GetData(IGH_DataAccess da, GH_ComponentParamServer paramServer)
        {
            GH_SgModel modelGoo = null;
            da.GetData(Parent._inModel, ref modelGoo);
            Model = modelGoo?.Value;
        }

        public override async Task DoWork(Action<string, double> reportProgress, Action done)
        {
            try
            {
                if (Model == null)
                {
                    Status = "No model provided.";
                    SetComponentMessage("No model");
                    if (!CancellationToken.IsCancellationRequested) done();
                    return;
                }

                SetComponentMessage("Querying...");
                var session = SpaceGassSessionManager.Current;
                if (session == null || !session.IsConnected)
                {
                    Status = "Not connected.";
                    SetComponentMessage("Not connected");
                    if (!CancellationToken.IsCancellationRequested) done();
                    return;
                }

                Result = await session.GetNodeLoadsDataAsync(Model, CancellationToken)
                    .ConfigureAwait(false);

                var nlCount = 0;
                var lmCount = 0;
                var pdCount = 0;
                foreach (var e in Result.NodeEntries)
                {
                    nlCount += e.NodeLoads.Count;
                    lmCount += e.LumpedMassLoads.Count;
                    pdCount += e.PrescribedDisplacements.Count;
                }

                Status = $"{Result.NodeEntries.Count} nodes: {nlCount} node loads, " +
                         $"{lmCount} lumped mass, {pdCount} prescribed displacements.";
                foreach (var w in Result.Warnings)
                {
                    Status += $"\nWarning: {w}";
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
                }

                SetComponentMessage($"{Result.NodeEntries.Count} nodes loaded");
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                var message = ModelAssembler.FormatApiError(ex, "querying node loads");
                Status = $"Error: {message}";
                SetComponentMessage("Error");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        public override void SetData(IGH_DataAccess da)
        {
            if (Result == null)
            {
                da.SetData(Parent._outStatus, Status);
                return;
            }

            // Build trees — one branch per node entry
            var treeNId = new GH_Structure<GH_Integer>();
            var treePts = new GH_Structure<GH_Point>();

            var treeNlLcId = new GH_Structure<GH_Integer>();
            var treeNlLc = new GH_Structure<GH_String>();
            var treeNlCat = new GH_Structure<GH_Integer>();
            var treeNlFx = new GH_Structure<GH_Number>();
            var treeNlFy = new GH_Structure<GH_Number>();
            var treeNlFz = new GH_Structure<GH_Number>();
            var treeNlMx = new GH_Structure<GH_Number>();
            var treeNlMy = new GH_Structure<GH_Number>();
            var treeNlMz = new GH_Structure<GH_Number>();

            var treeLmLcId = new GH_Structure<GH_Integer>();
            var treeLmLc = new GH_Structure<GH_String>();
            var treeLmCat = new GH_Structure<GH_Integer>();
            var treeLmTmx = new GH_Structure<GH_Number>();
            var treeLmTmy = new GH_Structure<GH_Number>();
            var treeLmTmz = new GH_Structure<GH_Number>();
            var treeLmRmx = new GH_Structure<GH_Number>();
            var treeLmRmy = new GH_Structure<GH_Number>();
            var treeLmRmz = new GH_Structure<GH_Number>();

            var treePdLcId = new GH_Structure<GH_Integer>();
            var treePdLc = new GH_Structure<GH_String>();
            var treePdCat = new GH_Structure<GH_Integer>();
            var treePdTx = new GH_Structure<GH_Number>();
            var treePdTy = new GH_Structure<GH_Number>();
            var treePdTz = new GH_Structure<GH_Number>();
            var treePdRx = new GH_Structure<GH_Number>();
            var treePdRy = new GH_Structure<GH_Number>();
            var treePdRz = new GH_Structure<GH_Number>();

            for (var i = 0; i < Result.NodeEntries.Count; i++)
            {
                var entry = Result.NodeEntries[i];
                var path = new GH_Path(i);

                treeNId.Append(new GH_Integer(entry.NodeId), path);
                treePts.Append(new GH_Point(new Point3d(entry.Point.X, entry.Point.Y, entry.Point.Z)), path);

                // Node loads
                foreach (var nl in entry.NodeLoads)
                {
                    treeNlLcId.Append(new GH_Integer(nl.LoadCaseId), path);
                    treeNlLc.Append(new GH_String(nl.LoadCaseName), path);
                    treeNlCat.Append(new GH_Integer(nl.LoadCategoryId), path);
                    treeNlFx.Append(new GH_Number(nl.Fx), path);
                    treeNlFy.Append(new GH_Number(nl.Fy), path);
                    treeNlFz.Append(new GH_Number(nl.Fz), path);
                    treeNlMx.Append(new GH_Number(nl.Mx), path);
                    treeNlMy.Append(new GH_Number(nl.My), path);
                    treeNlMz.Append(new GH_Number(nl.Mz), path);
                }

                // Ensure empty branches exist for nodes without this load type
                if (entry.NodeLoads.Count == 0)
                {
                    treeNlLcId.EnsurePath(path);
                    treeNlLc.EnsurePath(path);
                    treeNlCat.EnsurePath(path);
                    treeNlFx.EnsurePath(path);
                    treeNlFy.EnsurePath(path);
                    treeNlFz.EnsurePath(path);
                    treeNlMx.EnsurePath(path);
                    treeNlMy.EnsurePath(path);
                    treeNlMz.EnsurePath(path);
                }

                // Lumped mass
                foreach (var lm in entry.LumpedMassLoads)
                {
                    treeLmLcId.Append(new GH_Integer(lm.LoadCaseId), path);
                    treeLmLc.Append(new GH_String(lm.LoadCaseName), path);
                    treeLmCat.Append(new GH_Integer(lm.LoadCategoryId), path);
                    treeLmTmx.Append(new GH_Number(lm.Tmx), path);
                    treeLmTmy.Append(new GH_Number(lm.Tmy), path);
                    treeLmTmz.Append(new GH_Number(lm.Tmz), path);
                    treeLmRmx.Append(new GH_Number(lm.Rmx), path);
                    treeLmRmy.Append(new GH_Number(lm.Rmy), path);
                    treeLmRmz.Append(new GH_Number(lm.Rmz), path);
                }

                if (entry.LumpedMassLoads.Count == 0)
                {
                    treeLmLcId.EnsurePath(path);
                    treeLmLc.EnsurePath(path);
                    treeLmCat.EnsurePath(path);
                    treeLmTmx.EnsurePath(path);
                    treeLmTmy.EnsurePath(path);
                    treeLmTmz.EnsurePath(path);
                    treeLmRmx.EnsurePath(path);
                    treeLmRmy.EnsurePath(path);
                    treeLmRmz.EnsurePath(path);
                }

                // Prescribed displacements
                foreach (var pd in entry.PrescribedDisplacements)
                {
                    treePdLcId.Append(new GH_Integer(pd.LoadCaseId), path);
                    treePdLc.Append(new GH_String(pd.LoadCaseName), path);
                    treePdCat.Append(new GH_Integer(pd.LoadCategoryId), path);
                    treePdTx.Append(new GH_Number(pd.Tx), path);
                    treePdTy.Append(new GH_Number(pd.Ty), path);
                    treePdTz.Append(new GH_Number(pd.Tz), path);
                    treePdRx.Append(new GH_Number(pd.Rx), path);
                    treePdRy.Append(new GH_Number(pd.Ry), path);
                    treePdRz.Append(new GH_Number(pd.Rz), path);
                }

                if (entry.PrescribedDisplacements.Count == 0)
                {
                    treePdLcId.EnsurePath(path);
                    treePdLc.EnsurePath(path);
                    treePdCat.EnsurePath(path);
                    treePdTx.EnsurePath(path);
                    treePdTy.EnsurePath(path);
                    treePdTz.EnsurePath(path);
                    treePdRx.EnsurePath(path);
                    treePdRy.EnsurePath(path);
                    treePdRz.EnsurePath(path);
                }
            }

            da.SetDataTree(Parent._outNodeIds, treeNId);
            da.SetDataTree(Parent._outPoints, treePts);

            da.SetDataTree(Parent._outNlLcId, treeNlLcId);
            da.SetDataTree(Parent._outNlLc, treeNlLc);
            da.SetDataTree(Parent._outNlCat, treeNlCat);
            da.SetDataTree(Parent._outNlFx, treeNlFx);
            da.SetDataTree(Parent._outNlFy, treeNlFy);
            da.SetDataTree(Parent._outNlFz, treeNlFz);
            da.SetDataTree(Parent._outNlMx, treeNlMx);
            da.SetDataTree(Parent._outNlMy, treeNlMy);
            da.SetDataTree(Parent._outNlMz, treeNlMz);

            da.SetDataTree(Parent._outLmLcId, treeLmLcId);
            da.SetDataTree(Parent._outLmLc, treeLmLc);
            da.SetDataTree(Parent._outLmCat, treeLmCat);
            da.SetDataTree(Parent._outLmTmx, treeLmTmx);
            da.SetDataTree(Parent._outLmTmy, treeLmTmy);
            da.SetDataTree(Parent._outLmTmz, treeLmTmz);
            da.SetDataTree(Parent._outLmRmx, treeLmRmx);
            da.SetDataTree(Parent._outLmRmy, treeLmRmy);
            da.SetDataTree(Parent._outLmRmz, treeLmRmz);

            da.SetDataTree(Parent._outPdLcId, treePdLcId);
            da.SetDataTree(Parent._outPdLc, treePdLc);
            da.SetDataTree(Parent._outPdCat, treePdCat);
            da.SetDataTree(Parent._outPdTx, treePdTx);
            da.SetDataTree(Parent._outPdTy, treePdTy);
            da.SetDataTree(Parent._outPdTz, treePdTz);
            da.SetDataTree(Parent._outPdRx, treePdRx);
            da.SetDataTree(Parent._outPdRy, treePdRy);
            da.SetDataTree(Parent._outPdRz, treePdRz);

            da.SetData(Parent._outStatus, Status);
        }
    }
}
