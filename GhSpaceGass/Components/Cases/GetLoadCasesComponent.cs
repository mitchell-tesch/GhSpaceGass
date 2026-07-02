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

namespace GhSpaceGass.Components.Cases;

public class GetLoadCasesComponent : GH_AsyncComponent<GetLoadCasesComponent>
{
    private int _inModel;
    private int _outIds, _outNames, _outTypes, _outNotes, _outCombinationItems;
    private int _outCatIds, _outCatNames, _outCatNotes;
    private int _outGrpIds, _outGrpNames, _outGrpCases;
    private int _outStatus;

    public GetLoadCasesComponent()
        : base("SG Get Load Cases", "sgGetLoadCases",
            "Query all load cases, load categories, and load case groups from the open SpaceGass job.",
            "SpaceGass", "4 | Cases")
    {
        BaseWorker = new GetLoadCasesWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.last;
    protected override Bitmap Icon => Icons.IconFactory.GetLoadCases();
    public override Guid ComponentGuid => new("0F776D3B-DA87-4DDE-AC8D-6D0ADFFA5792");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The SpaceGass model (from Assemble or Disassemble).",
            GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        // Load Cases
        _outIds = pManager.AddIntegerParameter("Case ID", "LC-Id",
            "Load case IDs.",
            GH_ParamAccess.list);
        _outNames = pManager.AddTextParameter("Case Name", "LC-Ns",
            "Load case titles.",
            GH_ParamAccess.list);
        _outTypes = pManager.AddTextParameter("Case Type", "LC-Ty",
            "Load case type: Primary, Combination, Step, or Unused.",
            GH_ParamAccess.list);
        _outNotes = pManager.AddTextParameter("Case Notes", "LC-Nt",
            "Load case notes (empty if none).",
            GH_ParamAccess.list);
        _outCombinationItems = pManager.AddTextParameter("Combination Case Items", "LC-CI",
            "Combination constituents per load case (branched). Empty branch for non-combination cases.\n" +
            "Format: \"Factor×LoadCaseName\".",
            GH_ParamAccess.tree);
        
        // Load Categories
        _outCatIds = pManager.AddIntegerParameter("Category ID", "C-Id",
            "Load category IDs.",
            GH_ParamAccess.list);
        _outCatNames = pManager.AddTextParameter("Category Name", "C-N",
            "Load category names.",
            GH_ParamAccess.list);
        _outCatNotes = pManager.AddTextParameter("Category Note", "C-Nt",
            "Load category notes (empty if none).",
            GH_ParamAccess.list);
        
        // Load Groups
        _outGrpIds = pManager.AddIntegerParameter("Group ID", "G-Id",
            "Load case group IDs.",
            GH_ParamAccess.list);
        _outGrpNames = pManager.AddTextParameter("Group Name", "G-N",
            "Load case group titles.",
            GH_ParamAccess.list);
        _outGrpCases = pManager.AddTextParameter("Group Cases", "G-C",
            "Load case list per group (branched). Comma-separated ID list from SpaceGass.",
            GH_ParamAccess.tree);
        
        _outStatus = pManager.AddTextParameter("Status", "S",
            "Query status and warnings.",
            GH_ParamAccess.item);
    }

    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
        base.AppendAdditionalMenuItems(menu);
        Menu_AppendItem(menu, "Cancel", (_, _) => { RequestCancellation(); });
    }

    // ── Worker ────────────────────────────────────────────────────

    private sealed class GetLoadCasesWorker : WorkerInstance<GetLoadCasesComponent>
    {
        public GetLoadCasesWorker(
            GetLoadCasesComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken) { }

        private SgModelData Model { get; set; }
        private SgLoadCaseDataResult Result { get; set; }
        private string Status { get; set; } = string.Empty;

        public override WorkerInstance<GetLoadCasesComponent> Duplicate(
            string id, CancellationToken cancellationToken)
            => new GetLoadCasesWorker(Parent, id, cancellationToken);

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
                    Parent.Message = "No model";
                    if (!CancellationToken.IsCancellationRequested) done();
                    return;
                }

                Parent.Message = "Querying...";
                var session = SpaceGassSessionManager.Current;
                if (session == null || !session.IsConnected)
                {
                    Status = "Not connected.";
                    Parent.Message = "Not connected";
                    if (!CancellationToken.IsCancellationRequested) done();
                    return;
                }

                Result = await session.GetLoadCaseDataAsync(CancellationToken).ConfigureAwait(false);

                Status = $"{Result.LoadCases.Count} load cases, {Result.Categories.Count} categories, " +
                         $"{Result.Groups.Count} groups queried.";
                foreach (var w in Result.Warnings)
                {
                    Status += $"\nWarning: {w}";
                    Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
                }

                Parent.Message = $"{Result.LoadCases.Count} load cases";
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                Status = ex.Message;
                Parent.Message = "Error";
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        public override void SetData(IGH_DataAccess da)
        {
            if (Result != null)
            {
                da.SetDataList(Parent._outIds, Result.LoadCases.ConvertAll(lc => lc.Id));
                da.SetDataList(Parent._outNames, Result.LoadCases.ConvertAll(lc => lc.Name));
                da.SetDataList(Parent._outTypes, Result.LoadCases.ConvertAll(lc => lc.Type));
                da.SetDataList(Parent._outNotes, Result.LoadCases.ConvertAll(lc => lc.Notes));

                // Combination items as data tree: one branch per load case
                var ciTree = new GH_Structure<GH_String>();
                for (var i = 0; i < Result.LoadCases.Count; i++)
                {
                    var path = new GH_Path(i);
                    foreach (var item in Result.LoadCases[i].CombinationItems)
                        ciTree.Append(new GH_String(item), path);
                }

                da.SetDataTree(Parent._outCombinationItems, ciTree);

                // Categories
                da.SetDataList(Parent._outCatIds, Result.Categories.ConvertAll(c => c.Id));
                da.SetDataList(Parent._outCatNames, Result.Categories.ConvertAll(c => c.Name));
                da.SetDataList(Parent._outCatNotes, Result.Categories.ConvertAll(c => c.Notes));

                // Groups
                da.SetDataList(Parent._outGrpIds, Result.Groups.ConvertAll(g => g.Id));
                da.SetDataList(Parent._outGrpNames, Result.Groups.ConvertAll(g => g.Name));

                var gcTree = new GH_Structure<GH_String>();
                for (var i = 0; i < Result.Groups.Count; i++)
                    gcTree.Append(new GH_String(Result.Groups[i].LoadCaseList), new GH_Path(i));

                da.SetDataTree(Parent._outGrpCases, gcTree);
            }

            da.SetData(Parent._outStatus, Status);
        }
    }
}
