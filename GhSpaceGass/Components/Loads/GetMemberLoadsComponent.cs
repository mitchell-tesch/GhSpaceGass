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

public class GetMemberLoadsComponent : GH_AsyncComponent<GetMemberLoadsComponent>
{
    private int _inModel;

    // Shared
    private int _outMemberIds, _outLines;

    // Concentrated (CL)
    private int _outClLcId, _outClLc, _outClCat;
    private int _outClFx, _outClFy, _outClFz, _outClMx, _outClMy, _outClMz;
    private int _outClPos, _outClPu, _outClAx;

    // Distributed (DL)
    private int _outDlLcId, _outDlLc, _outDlCat;
    private int _outDlFxS, _outDlFyS, _outDlFzS, _outDlFxE, _outDlFyE, _outDlFzE;
    private int _outDlSp, _outDlFp, _outDlPu, _outDlAx;

    // Distributed moment (DM)
    private int _outDmLcId, _outDmLc, _outDmCat;
    private int _outDmMxS, _outDmMyS, _outDmMzS, _outDmMxE, _outDmMyE, _outDmMzE;
    private int _outDmSp, _outDmFp, _outDmPu, _outDmAx;

    // Prestress (PL)
    private int _outPlLcId, _outPlLc, _outPlCat, _outPlPr;

    // Thermal (TL)
    private int _outTlLcId, _outTlLc, _outTlCat, _outTlT, _outTlYg, _outTlZg;

    private int _outStatus;

    public GetMemberLoadsComponent()
        : base("SG Get Member Loads", "sgGetMemberLoads",
            "Query all member-based loads (concentrated, distributed, distributed moments,\n" +
            "prestress, and member thermal) from the open SpaceGass job, grouped by member.",
            "SpaceGass", "5 | Loads")
    {
        BaseWorker = new GetMemberLoadsWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.last;
    protected override Bitmap Icon => Icons.IconFactory.GetMemberLoads();
    public override Guid ComponentGuid => new("9FA05AF1-7305-4907-8C21-924A4FC4D44B");

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
        _outMemberIds = pManager.AddIntegerParameter("Member IDs", "MId",
            "Member ID per branch (one branch per loaded member, ordered by ID).", GH_ParamAccess.tree);
        _outLines = pManager.AddLineParameter("Lines", "Ln",
            "Member geometry per branch.", GH_ParamAccess.tree);

        // Concentrated loads
        _outClLcId = pManager.AddIntegerParameter("CL Load Case IDs", "CL-LCId",
            "Concentrated: load case ID.",
            GH_ParamAccess.tree);
        _outClLc = pManager.AddTextParameter("CL Load Cases", "CL-LC",
            "Concentrated: load case name.",
            GH_ParamAccess.tree);
        _outClCat = pManager.AddIntegerParameter("CL Categories", "CL-Cat",
            "Concentrated: category ID.",
            GH_ParamAccess.tree);
        _outClFx = pManager.AddNumberParameter("CL Fx", "CL-Fx",
            "Concentrated: force X.",
            GH_ParamAccess.tree);
        _outClFy = pManager.AddNumberParameter("CL Fy", "CL-Fy",
            "Concentrated: force Y.",
            GH_ParamAccess.tree);
        _outClFz = pManager.AddNumberParameter("CL Fz", "CL-Fz",
            "Concentrated: force Z.",
            GH_ParamAccess.tree);
        _outClMx = pManager.AddNumberParameter("CL Mx", "CL-Mx",
            "Concentrated: moment X.",
            GH_ParamAccess.tree);
        _outClMy = pManager.AddNumberParameter("CL My", "CL-My",
            "Concentrated: moment Y.",
            GH_ParamAccess.tree);
        _outClMz = pManager.AddNumberParameter("CL Mz", "CL-Mz",
            "Concentrated: moment Z.",
            GH_ParamAccess.tree);
        _outClPos = pManager.AddNumberParameter("CL Position", "CL-Pos",
            "Concentrated: position along member.",
            GH_ParamAccess.tree);
        _outClPu = pManager.AddTextParameter("CL Position Units", "CL-Pu",
            "Concentrated: Actual or Percent.",
            GH_ParamAccess.tree);
        _outClAx = pManager.AddTextParameter("CL Axes", "CL-Ax",
            "Concentrated: Local, Global Inclined, or Global Projected.",
            GH_ParamAccess.tree);

        // Distributed loads
        _outDlLcId = pManager.AddIntegerParameter("DL Load Case IDs", "DL-LCId",
            "Distributed: load case ID.",
            GH_ParamAccess.tree);
        _outDlLc = pManager.AddTextParameter("DL Load Cases", "DL-LC",
            "Distributed: load case name.",
            GH_ParamAccess.tree);
        _outDlCat = pManager.AddIntegerParameter("DL Categories", "DL-Cat",
            "Distributed: category ID.",
            GH_ParamAccess.tree);
        _outDlFxS = pManager.AddNumberParameter("DL Fx Start", "DL-FxS",
            "Distributed: force X at start.",
            GH_ParamAccess.tree);
        _outDlFyS = pManager.AddNumberParameter("DL Fy Start", "DL-FyS",
            "Distributed: force Y at start.",
            GH_ParamAccess.tree);
        _outDlFzS = pManager.AddNumberParameter("DL Fz Start", "DL-FzS",
            "Distributed: force Z at start.",
            GH_ParamAccess.tree);
        _outDlFxE = pManager.AddNumberParameter("DL Fx End", "DL-FxE",
            "Distributed: force X at end.",
            GH_ParamAccess.tree);
        _outDlFyE = pManager.AddNumberParameter("DL Fy End", "DL-FyE",
            "Distributed: force Y at end.",
            GH_ParamAccess.tree);
        _outDlFzE = pManager.AddNumberParameter("DL Fz End", "DL-FzE",
            "Distributed: force Z at end.",
            GH_ParamAccess.tree);
        _outDlSp = pManager.AddNumberParameter("DL Start Position", "DL-Sp",
            "Distributed: start position.",
            GH_ParamAccess.tree);
        _outDlFp = pManager.AddNumberParameter("DL Finish Position", "DL-Fp",
            "Distributed: finish position.",
            GH_ParamAccess.tree);
        _outDlPu = pManager.AddTextParameter("DL Position Units", "DL-Pu",
            "Distributed: Actual or Percent.",
            GH_ParamAccess.tree);
        _outDlAx = pManager.AddTextParameter("DL Axes", "DL-Ax",
            "Distributed: Local, Global Inclined, or Global Projected.",
            GH_ParamAccess.tree);

        // Distributed moments
        _outDmLcId = pManager.AddIntegerParameter("DM Load Case IDs", "DM-LCId",
            "Dist. moment: load case ID.",
            GH_ParamAccess.tree);
        _outDmLc = pManager.AddTextParameter("DM Load Cases", "DM-LC",
            "Dist. moment: load case name.",
            GH_ParamAccess.tree);
        _outDmCat = pManager.AddIntegerParameter("DM Categories", "DM-Cat",
            "Dist. moment: category ID.",
            GH_ParamAccess.tree);
        _outDmMxS = pManager.AddNumberParameter("DM Mx Start", "DM-MxS",
            "Dist. moment: Mx at start.",
            GH_ParamAccess.tree);
        _outDmMyS = pManager.AddNumberParameter("DM My Start", "DM-MyS",
            "Dist. moment: My at start.",
            GH_ParamAccess.tree);
        _outDmMzS = pManager.AddNumberParameter("DM Mz Start", "DM-MzS",
            "Dist. moment: Mz at start.",
            GH_ParamAccess.tree);
        _outDmMxE = pManager.AddNumberParameter("DM Mx End", "DM-MxE",
            "Dist. moment: Mx at end.",
            GH_ParamAccess.tree);
        _outDmMyE = pManager.AddNumberParameter("DM My End", "DM-MyE",
            "Dist. moment: My at end.",
            GH_ParamAccess.tree);
        _outDmMzE = pManager.AddNumberParameter("DM Mz End", "DM-MzE",
            "Dist. moment: Mz at end.",
            GH_ParamAccess.tree);
        _outDmSp = pManager.AddNumberParameter("DM Start Position", "DM-Sp",
            "Dist. moment: start position.",
            GH_ParamAccess.tree);
        _outDmFp = pManager.AddNumberParameter("DM Finish Position", "DM-Fp",
            "Dist. moment: finish position.",
            GH_ParamAccess.tree);
        _outDmPu = pManager.AddTextParameter("DM Position Units", "DM-Pu",
            "Dist. moment: Actual or Percent.",
            GH_ParamAccess.tree);
        _outDmAx = pManager.AddTextParameter("DM Axes", "DM-Ax",
            "Dist. moment: Local, Global Inclined, or Global Projected.",
            GH_ParamAccess.tree);

        // Prestress
        _outPlLcId = pManager.AddIntegerParameter("PL Load Case IDs", "PL-LCId",
            "Prestress: load case ID.",
            GH_ParamAccess.tree);
        _outPlLc = pManager.AddTextParameter("PL Load Cases", "PL-LC",
            "Prestress: load case name.",
            GH_ParamAccess.tree);
        _outPlCat = pManager.AddIntegerParameter("PL Categories", "PL-Cat",
            "Prestress: category ID.",
            GH_ParamAccess.tree);
        _outPlPr = pManager.AddNumberParameter("PL Prestress", "PL-Pr",
            "Prestress: axial force.",
            GH_ParamAccess.tree);

        // Thermal
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

    private sealed class GetMemberLoadsWorker : WorkerInstance<GetMemberLoadsComponent>
    {
        public GetMemberLoadsWorker(GetMemberLoadsComponent parent, string id = "baseWorker",
            CancellationToken cancellationToken = default) : base(parent, id, cancellationToken) { }

        private SgModelData Model { get; set; }
        private SgMemberLoadsDataResult Result { get; set; }
        private string Status { get; set; } = string.Empty;

        public override WorkerInstance<GetMemberLoadsComponent> Duplicate(string id, CancellationToken ct)
            => new GetMemberLoadsWorker(Parent, id, ct);

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
                if (Model == null) { Status = "No model provided."; SetComponentMessage("No model"); if (!CancellationToken.IsCancellationRequested) done(); return; }
                SetComponentMessage("Querying...");
                var session = SpaceGassSessionManager.Current;
                if (session == null || !session.IsConnected) { Status = "Not connected."; SetComponentMessage("Not connected"); if (!CancellationToken.IsCancellationRequested) done(); return; }

                Result = await session.GetMemberLoadsDataAsync(Model, CancellationToken).ConfigureAwait(false);

                int cl = 0, dl = 0, dm = 0, pl = 0, tl = 0;
                foreach (var e in Result.MemberEntries) { cl += e.ConcentratedLoads.Count; dl += e.DistributedLoads.Count; dm += e.DistributedMoments.Count; pl += e.PrestressLoads.Count; tl += e.ThermalLoads.Count; }
                Status = $"{Result.MemberEntries.Count} members: {cl} concentrated, {dl} distributed, {dm} dist. moments, {pl} prestress, {tl} thermal.";
                foreach (var w in Result.Warnings) { Status += $"\nWarning: {w}"; AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w); }
                SetComponentMessage($"{Result.MemberEntries.Count} members loaded");
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                var message = ModelAssembler.FormatApiError(ex, "querying member loads");
                Status = $"Error: {message}";
                SetComponentMessage("Error");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        public override void SetData(IGH_DataAccess da)
        {
            if (Result == null) { da.SetData(Parent._outStatus, Status); return; }

            var p = Parent;
            var treeMId = new GH_Structure<GH_Integer>(); var treeLn = new GH_Structure<GH_Line>();
            var tClLcId = new GH_Structure<GH_Integer>(); var tClLc = new GH_Structure<GH_String>(); var tClCat = new GH_Structure<GH_Integer>();
            var tClFx = new GH_Structure<GH_Number>(); var tClFy = new GH_Structure<GH_Number>(); var tClFz = new GH_Structure<GH_Number>();
            var tClMx = new GH_Structure<GH_Number>(); var tClMy = new GH_Structure<GH_Number>(); var tClMz = new GH_Structure<GH_Number>();
            var tClPos = new GH_Structure<GH_Number>(); var tClPu = new GH_Structure<GH_String>(); var tClAx = new GH_Structure<GH_String>();
            var tDlLcId = new GH_Structure<GH_Integer>(); var tDlLc = new GH_Structure<GH_String>(); var tDlCat = new GH_Structure<GH_Integer>();
            var tDlFxS = new GH_Structure<GH_Number>(); var tDlFyS = new GH_Structure<GH_Number>(); var tDlFzS = new GH_Structure<GH_Number>();
            var tDlFxE = new GH_Structure<GH_Number>(); var tDlFyE = new GH_Structure<GH_Number>(); var tDlFzE = new GH_Structure<GH_Number>();
            var tDlSp = new GH_Structure<GH_Number>(); var tDlFp = new GH_Structure<GH_Number>(); var tDlPu = new GH_Structure<GH_String>(); var tDlAx = new GH_Structure<GH_String>();
            var tDmLcId = new GH_Structure<GH_Integer>(); var tDmLc = new GH_Structure<GH_String>(); var tDmCat = new GH_Structure<GH_Integer>();
            var tDmMxS = new GH_Structure<GH_Number>(); var tDmMyS = new GH_Structure<GH_Number>(); var tDmMzS = new GH_Structure<GH_Number>();
            var tDmMxE = new GH_Structure<GH_Number>(); var tDmMyE = new GH_Structure<GH_Number>(); var tDmMzE = new GH_Structure<GH_Number>();
            var tDmSp = new GH_Structure<GH_Number>(); var tDmFp = new GH_Structure<GH_Number>(); var tDmPu = new GH_Structure<GH_String>(); var tDmAx = new GH_Structure<GH_String>();
            var tPlLcId = new GH_Structure<GH_Integer>(); var tPlLc = new GH_Structure<GH_String>(); var tPlCat = new GH_Structure<GH_Integer>(); var tPlPr = new GH_Structure<GH_Number>();
            var tTlLcId = new GH_Structure<GH_Integer>(); var tTlLc = new GH_Structure<GH_String>(); var tTlCat = new GH_Structure<GH_Integer>();
            var tTlT = new GH_Structure<GH_Number>(); var tTlYg = new GH_Structure<GH_Number>(); var tTlZg = new GH_Structure<GH_Number>();

            for (var i = 0; i < Result.MemberEntries.Count; i++)
            {
                var e = Result.MemberEntries[i];
                var path = new GH_Path(i);
                treeMId.Append(new GH_Integer(e.MemberId), path);
                treeLn.Append(new GH_Line(new Line(
                    new Point3d(e.Start.X, e.Start.Y, e.Start.Z),
                    new Point3d(e.End.X, e.End.Y, e.End.Z))), path);

                // Concentrated
                foreach (var c in e.ConcentratedLoads)
                {
                    tClLcId.Append(new GH_Integer(c.LoadCaseId), path); tClLc.Append(new GH_String(c.LoadCaseName), path); tClCat.Append(new GH_Integer(c.LoadCategoryId), path);
                    tClFx.Append(new GH_Number(c.Fx), path); tClFy.Append(new GH_Number(c.Fy), path); tClFz.Append(new GH_Number(c.Fz), path);
                    tClMx.Append(new GH_Number(c.Mx), path); tClMy.Append(new GH_Number(c.My), path); tClMz.Append(new GH_Number(c.Mz), path);
                    tClPos.Append(new GH_Number(c.Position), path); tClPu.Append(new GH_String(c.PositionUnits), path); tClAx.Append(new GH_String(c.Axes), path);
                }
                if (e.ConcentratedLoads.Count == 0) { tClLcId.EnsurePath(path); tClLc.EnsurePath(path); tClCat.EnsurePath(path); tClFx.EnsurePath(path); tClFy.EnsurePath(path); tClFz.EnsurePath(path); tClMx.EnsurePath(path); tClMy.EnsurePath(path); tClMz.EnsurePath(path); tClPos.EnsurePath(path); tClPu.EnsurePath(path); tClAx.EnsurePath(path); }

                // Distributed
                foreach (var d in e.DistributedLoads)
                {
                    tDlLcId.Append(new GH_Integer(d.LoadCaseId), path); tDlLc.Append(new GH_String(d.LoadCaseName), path); tDlCat.Append(new GH_Integer(d.LoadCategoryId), path);
                    tDlFxS.Append(new GH_Number(d.FxStart), path); tDlFyS.Append(new GH_Number(d.FyStart), path); tDlFzS.Append(new GH_Number(d.FzStart), path);
                    tDlFxE.Append(new GH_Number(d.FxFinish), path); tDlFyE.Append(new GH_Number(d.FyFinish), path); tDlFzE.Append(new GH_Number(d.FzFinish), path);
                    tDlSp.Append(new GH_Number(d.StartPosition), path); tDlFp.Append(new GH_Number(d.FinishPosition), path);
                    tDlPu.Append(new GH_String(d.PositionUnits), path); tDlAx.Append(new GH_String(d.Axes), path);
                }
                if (e.DistributedLoads.Count == 0) { tDlLcId.EnsurePath(path); tDlLc.EnsurePath(path); tDlCat.EnsurePath(path); tDlFxS.EnsurePath(path); tDlFyS.EnsurePath(path); tDlFzS.EnsurePath(path); tDlFxE.EnsurePath(path); tDlFyE.EnsurePath(path); tDlFzE.EnsurePath(path); tDlSp.EnsurePath(path); tDlFp.EnsurePath(path); tDlPu.EnsurePath(path); tDlAx.EnsurePath(path); }

                // Distributed moments
                foreach (var m in e.DistributedMoments)
                {
                    tDmLcId.Append(new GH_Integer(m.LoadCaseId), path); tDmLc.Append(new GH_String(m.LoadCaseName), path); tDmCat.Append(new GH_Integer(m.LoadCategoryId), path);
                    tDmMxS.Append(new GH_Number(m.MxStart), path); tDmMyS.Append(new GH_Number(m.MyStart), path); tDmMzS.Append(new GH_Number(m.MzStart), path);
                    tDmMxE.Append(new GH_Number(m.MxFinish), path); tDmMyE.Append(new GH_Number(m.MyFinish), path); tDmMzE.Append(new GH_Number(m.MzFinish), path);
                    tDmSp.Append(new GH_Number(m.StartPosition), path); tDmFp.Append(new GH_Number(m.FinishPosition), path);
                    tDmPu.Append(new GH_String(m.PositionUnits), path); tDmAx.Append(new GH_String(m.Axes), path);
                }
                if (e.DistributedMoments.Count == 0) { tDmLcId.EnsurePath(path); tDmLc.EnsurePath(path); tDmCat.EnsurePath(path); tDmMxS.EnsurePath(path); tDmMyS.EnsurePath(path); tDmMzS.EnsurePath(path); tDmMxE.EnsurePath(path); tDmMyE.EnsurePath(path); tDmMzE.EnsurePath(path); tDmSp.EnsurePath(path); tDmFp.EnsurePath(path); tDmPu.EnsurePath(path); tDmAx.EnsurePath(path); }

                // Prestress
                foreach (var pr in e.PrestressLoads)
                {
                    tPlLcId.Append(new GH_Integer(pr.LoadCaseId), path); tPlLc.Append(new GH_String(pr.LoadCaseName), path); tPlCat.Append(new GH_Integer(pr.LoadCategoryId), path);
                    tPlPr.Append(new GH_Number(pr.Prestress), path);
                }
                if (e.PrestressLoads.Count == 0) { tPlLcId.EnsurePath(path); tPlLc.EnsurePath(path); tPlCat.EnsurePath(path); tPlPr.EnsurePath(path); }

                // Thermal
                foreach (var t in e.ThermalLoads)
                {
                    tTlLcId.Append(new GH_Integer(t.LoadCaseId), path); tTlLc.Append(new GH_String(t.LoadCaseName), path); tTlCat.Append(new GH_Integer(t.LoadCategoryId), path);
                    tTlT.Append(new GH_Number(t.Temperature), path); tTlYg.Append(new GH_Number(t.YGradient), path); tTlZg.Append(new GH_Number(t.ZGradient), path);
                }
                if (e.ThermalLoads.Count == 0) { tTlLcId.EnsurePath(path); tTlLc.EnsurePath(path); tTlCat.EnsurePath(path); tTlT.EnsurePath(path); tTlYg.EnsurePath(path); tTlZg.EnsurePath(path); }
            }

            da.SetDataTree(p._outMemberIds, treeMId); da.SetDataTree(p._outLines, treeLn);
            da.SetDataTree(p._outClLcId, tClLcId); da.SetDataTree(p._outClLc, tClLc); da.SetDataTree(p._outClCat, tClCat);
            da.SetDataTree(p._outClFx, tClFx); da.SetDataTree(p._outClFy, tClFy); da.SetDataTree(p._outClFz, tClFz);
            da.SetDataTree(p._outClMx, tClMx); da.SetDataTree(p._outClMy, tClMy); da.SetDataTree(p._outClMz, tClMz);
            da.SetDataTree(p._outClPos, tClPos); da.SetDataTree(p._outClPu, tClPu); da.SetDataTree(p._outClAx, tClAx);
            da.SetDataTree(p._outDlLcId, tDlLcId); da.SetDataTree(p._outDlLc, tDlLc); da.SetDataTree(p._outDlCat, tDlCat);
            da.SetDataTree(p._outDlFxS, tDlFxS); da.SetDataTree(p._outDlFyS, tDlFyS); da.SetDataTree(p._outDlFzS, tDlFzS);
            da.SetDataTree(p._outDlFxE, tDlFxE); da.SetDataTree(p._outDlFyE, tDlFyE); da.SetDataTree(p._outDlFzE, tDlFzE);
            da.SetDataTree(p._outDlSp, tDlSp); da.SetDataTree(p._outDlFp, tDlFp); da.SetDataTree(p._outDlPu, tDlPu); da.SetDataTree(p._outDlAx, tDlAx);
            da.SetDataTree(p._outDmLcId, tDmLcId); da.SetDataTree(p._outDmLc, tDmLc); da.SetDataTree(p._outDmCat, tDmCat);
            da.SetDataTree(p._outDmMxS, tDmMxS); da.SetDataTree(p._outDmMyS, tDmMyS); da.SetDataTree(p._outDmMzS, tDmMzS);
            da.SetDataTree(p._outDmMxE, tDmMxE); da.SetDataTree(p._outDmMyE, tDmMyE); da.SetDataTree(p._outDmMzE, tDmMzE);
            da.SetDataTree(p._outDmSp, tDmSp); da.SetDataTree(p._outDmFp, tDmFp); da.SetDataTree(p._outDmPu, tDmPu); da.SetDataTree(p._outDmAx, tDmAx);
            da.SetDataTree(p._outPlLcId, tPlLcId); da.SetDataTree(p._outPlLc, tPlLc); da.SetDataTree(p._outPlCat, tPlCat); da.SetDataTree(p._outPlPr, tPlPr);
            da.SetDataTree(p._outTlLcId, tTlLcId); da.SetDataTree(p._outTlLc, tTlLc); da.SetDataTree(p._outTlCat, tTlCat);
            da.SetDataTree(p._outTlT, tTlT); da.SetDataTree(p._outTlYg, tTlYg); da.SetDataTree(p._outTlZg, tTlZg);
            da.SetData(p._outStatus, Status);
        }
    }
}
