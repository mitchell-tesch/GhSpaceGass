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

public class GetPlateLoadsComponent : GH_AsyncComponent<GetPlateLoadsComponent>
{
    private int _inModel;
    private int _outPlateIds, _outPlatePoints;
    private int _outPpLcId, _outPpLc, _outPpCat, _outPpPx, _outPpPy, _outPpPz, _outPpAx;
    private int _outTlLcId, _outTlLc, _outTlCat, _outTlT, _outTlYg, _outTlZg;
    private int _outStatus;

    public GetPlateLoadsComponent()
        : base("SG Get Plate Loads", "sgGetPlateLoads",
            "Query all plate-based loads (pressure and plate thermal)\n" +
            "from the open SpaceGass job, grouped by plate.",
            "SpaceGass", "5 | Loads")
    {
        BaseWorker = new GetPlateLoadsWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.last;
    protected override Bitmap Icon => Icons.IconFactory.GetPlateLoads();
    public override Guid ComponentGuid => new("C4735D61-D154-4DEF-96C0-D9164B8E5A5C");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The SpaceGass model (from Assemble or Disassemble).",
            GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        // Shared
        _outPlateIds = pManager.AddIntegerParameter("Plate IDs", "PId",
            "Plate ID per branch (one branch per loaded plate, ordered by ID).",
            GH_ParamAccess.tree);
        _outPlatePoints = pManager.AddPointParameter("Plate Points", "PPt",
            "Corner points per branch (3 or 4 per plate).",
            GH_ParamAccess.tree);

        // Pressure loads (PP)
        _outPpLcId = pManager.AddIntegerParameter("PP Load Case IDs", "PP-LCId",
            "Pressure: load case ID.",
            GH_ParamAccess.tree);
        _outPpLc = pManager.AddTextParameter("PP Load Cases", "PP-LC",
            "Pressure: load case name.",
            GH_ParamAccess.tree);
        _outPpCat = pManager.AddIntegerParameter("PP Categories", "PP-Cat",
            "Pressure: category ID.",
            GH_ParamAccess.tree);
        _outPpPx = pManager.AddNumberParameter("PP Px", "PP-Px", 
            "Pressure: X component.",
            GH_ParamAccess.tree);
        _outPpPy = pManager.AddNumberParameter("PP Py", "PP-Py",
            "Pressure: Y component.",
            GH_ParamAccess.tree);
        _outPpPz = pManager.AddNumberParameter("PP Pz", "PP-Pz",
            "Pressure: Z component.",
            GH_ParamAccess.tree);
        _outPpAx = pManager.AddTextParameter("PP Axes", "PP-Ax",
            "Pressure: Local, Global Inclined, or Global Projected.",
            GH_ParamAccess.tree);

        // Thermal loads (TL)
        _outTlLcId = pManager.AddIntegerParameter("TL Load Case IDs", "TL-LCId",
            "Thermal: load case ID.",
            GH_ParamAccess.tree);
        _outTlLc = pManager.AddTextParameter("TL Load Cases", "TL-LC",
            "Thermal: load case name.",
            GH_ParamAccess.tree);
        _outTlCat = pManager.AddIntegerParameter("TL Categories", "TL-Cat",
            "Thermal: category ID.",
            GH_ParamAccess.tree);
        _outTlT = pManager.AddNumberParameter("TL Temperature", "TL-T",
            "Thermal: temperature change.",
            GH_ParamAccess.tree);
        _outTlYg = pManager.AddNumberParameter("TL Y Gradient", "TL-YG",
            "Thermal: Y gradient.",
            GH_ParamAccess.tree);
        _outTlZg = pManager.AddNumberParameter("TL Z Gradient", "TL-ZG",
            "Thermal: Z gradient.",
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

    private sealed class GetPlateLoadsWorker : WorkerInstance<GetPlateLoadsComponent>
    {
        public GetPlateLoadsWorker(GetPlateLoadsComponent parent, string id = "baseWorker",
            CancellationToken cancellationToken = default) : base(parent, id, cancellationToken) { }

        private SgModelData Model { get; set; }
        private SgPlateLoadsDataResult Result { get; set; }
        private string Status { get; set; } = string.Empty;

        public override WorkerInstance<GetPlateLoadsComponent> Duplicate(string id, CancellationToken ct)
            => new GetPlateLoadsWorker(Parent, id, ct);

        public override void GetData(IGH_DataAccess da, GH_ComponentParamServer ps)
        {
            GH_SgModel goo = null;
            da.GetData(Parent._inModel, ref goo);
            Model = goo?.Value;
        }

        public override async Task DoWork(Action<string, double> reportProgress, Action done)
        {
            try
            {
                if (Model == null) { Status = "No model provided."; Parent.Message = "No model"; if (!CancellationToken.IsCancellationRequested) done(); return; }
                Parent.Message = "Querying...";
                var session = SpaceGassSessionManager.Current;
                if (session == null || !session.IsConnected) { Status = "Not connected."; Parent.Message = "Not connected"; if (!CancellationToken.IsCancellationRequested) done(); return; }

                Result = await session.GetPlateLoadsDataAsync(Model, CancellationToken).ConfigureAwait(false);

                int pp = 0, tl = 0;
                foreach (var e in Result.PlateEntries) { pp += e.PressureLoads.Count; tl += e.ThermalLoads.Count; }
                Status = $"{Result.PlateEntries.Count} plates: {pp} pressure, {tl} thermal.";
                foreach (var w in Result.Warnings) { Status += $"\nWarning: {w}"; AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w); }
                Parent.Message = $"{Result.PlateEntries.Count} plates";
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                var message = ModelAssembler.FormatApiError(ex, "querying plate loads");
                Status = $"Error: {message}";
                Parent.Message = "Error";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        public override void SetData(IGH_DataAccess da)
        {
            if (Result == null) { da.SetData(Parent._outStatus, Status); return; }

            var p = Parent;
            var treePId = new GH_Structure<GH_Integer>();
            var treePPt = new GH_Structure<GH_Point>();
            var tPpLcId = new GH_Structure<GH_Integer>(); var tPpLc = new GH_Structure<GH_String>(); var tPpCat = new GH_Structure<GH_Integer>();
            var tPpPx = new GH_Structure<GH_Number>(); var tPpPy = new GH_Structure<GH_Number>(); var tPpPz = new GH_Structure<GH_Number>();
            var tPpAx = new GH_Structure<GH_String>();
            var tTlLcId = new GH_Structure<GH_Integer>(); var tTlLc = new GH_Structure<GH_String>(); var tTlCat = new GH_Structure<GH_Integer>();
            var tTlT = new GH_Structure<GH_Number>(); var tTlYg = new GH_Structure<GH_Number>(); var tTlZg = new GH_Structure<GH_Number>();

            for (var i = 0; i < Result.PlateEntries.Count; i++)
            {
                var e = Result.PlateEntries[i];
                var path = new GH_Path(i);

                treePId.Append(new GH_Integer(e.PlateId), path);
                foreach (var pt in e.CornerPoints)
                    treePPt.Append(new GH_Point(new Point3d(pt.X, pt.Y, pt.Z)), path);

                // Pressure loads
                foreach (var pp in e.PressureLoads)
                {
                    tPpLcId.Append(new GH_Integer(pp.LoadCaseId), path);
                    tPpLc.Append(new GH_String(pp.LoadCaseName), path);
                    tPpCat.Append(new GH_Integer(pp.LoadCategoryId), path);
                    tPpPx.Append(new GH_Number(pp.Px), path);
                    tPpPy.Append(new GH_Number(pp.Py), path);
                    tPpPz.Append(new GH_Number(pp.Pz), path);
                    tPpAx.Append(new GH_String(pp.Axes), path);
                }
                if (e.PressureLoads.Count == 0) { tPpLcId.EnsurePath(path); tPpLc.EnsurePath(path); tPpCat.EnsurePath(path); tPpPx.EnsurePath(path); tPpPy.EnsurePath(path); tPpPz.EnsurePath(path); tPpAx.EnsurePath(path); }

                // Thermal loads
                foreach (var tl in e.ThermalLoads)
                {
                    tTlLcId.Append(new GH_Integer(tl.LoadCaseId), path);
                    tTlLc.Append(new GH_String(tl.LoadCaseName), path);
                    tTlCat.Append(new GH_Integer(tl.LoadCategoryId), path);
                    tTlT.Append(new GH_Number(tl.Temperature), path);
                    tTlYg.Append(new GH_Number(tl.YGradient), path);
                    tTlZg.Append(new GH_Number(tl.ZGradient), path);
                }
                if (e.ThermalLoads.Count == 0) { tTlLcId.EnsurePath(path); tTlLc.EnsurePath(path); tTlCat.EnsurePath(path); tTlT.EnsurePath(path); tTlYg.EnsurePath(path); tTlZg.EnsurePath(path); }
            }

            da.SetDataTree(p._outPlateIds, treePId);
            da.SetDataTree(p._outPlatePoints, treePPt);
            da.SetDataTree(p._outPpLcId, tPpLcId); da.SetDataTree(p._outPpLc, tPpLc); da.SetDataTree(p._outPpCat, tPpCat);
            da.SetDataTree(p._outPpPx, tPpPx); da.SetDataTree(p._outPpPy, tPpPy); da.SetDataTree(p._outPpPz, tPpPz);
            da.SetDataTree(p._outPpAx, tPpAx);
            da.SetDataTree(p._outTlLcId, tTlLcId); da.SetDataTree(p._outTlLc, tTlLc); da.SetDataTree(p._outTlCat, tTlCat);
            da.SetDataTree(p._outTlT, tTlT); da.SetDataTree(p._outTlYg, tTlYg); da.SetDataTree(p._outTlZg, tTlZg);
            da.SetData(p._outStatus, Status);
        }
    }
}
