using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GhSpaceGass.Async;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;

namespace GhSpaceGass.Components.Model;

public class AssembleModelComponent : GH_AsyncComponent<AssembleModelComponent>
{
    private int _inAssemble;
    private int _inCombinationLoadCases;
    private int _inConstraints;
    private int _inLoads;
    private int _inMembers;
    private int _inPlates;
    private int _inRestraints;
    private int _inTolerance;

    private int _outModel;
    private int _outStatus;

    public AssembleModelComponent()
        : base("SG Assemble Model", "sgAssemble",
            "Compile structural data (members, restraints, loads) into a SpaceGass model and push to the open job.",
            "SpaceGass", "6 | Model")
    {
        BaseWorker = new AssembleWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.primary;
    protected override Bitmap Icon => Icons.IconFactory.AssembleModel();
    public override Guid ComponentGuid => new("2E523472-7566-4F79-BC77-4EA83EDDFB2F");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inAssemble = pManager.AddBooleanParameter("Assemble?", "A?",
            "Set to true to trigger model assembly.",
            GH_ParamAccess.item, false);
        _inMembers = pManager.AddParameter(new Param_SgMember(),
            "Members", "M",
            "List of members to assemble.",
            GH_ParamAccess.list);
        _inPlates = pManager.AddParameter(new Param_SgPlate(),
            "Plates", "P",
            "List of plate elements to assemble (optional).",
            GH_ParamAccess.list);
        _inRestraints = pManager.AddParameter(new Param_SgRestraint(),
            "Restraints", "R",
            "List of restraints to apply (optional).",
            GH_ParamAccess.list);
        _inConstraints = pManager.AddParameter(new Param_SgNodeConstraint(),
            "Constraints", "C",
            "List of node constraints (master-slave links) to apply (optional).",
            GH_ParamAccess.list);
        _inLoads = pManager.AddGenericParameter("Loads", "L",
            "List of loads to apply — accepts Node Loads, Member Distributed Loads, Member Concentrated Loads,\n" +
            "Member Prestress Loads, Self-Weight Loads, Lumped Mass Loads, Prescribed Displacements,\n" +
            "Plate Pressure Loads, and Thermal Loads (optional).",
            GH_ParamAccess.list);
        _inCombinationLoadCases = pManager.AddParameter(new Param_SgCombinationLoadCase(),
            "Combination Load Cases",
            "CLC",
            "List of combination load cases to create (optional).",
            GH_ParamAccess.list);
        _inTolerance = pManager.AddNumberParameter("Tolerance", "T",
            "Coincidence tolerance for node deduplication. Defaults to Rhino document tolerance.",
            GH_ParamAccess.item);

        pManager[_inMembers].Optional = true;
        pManager[_inPlates].Optional = true;
        pManager[_inRestraints].Optional = true;
        pManager[_inConstraints].Optional = true;
        pManager[_inLoads].Optional = true;
        pManager[_inCombinationLoadCases].Optional = true;
        pManager[_inTolerance].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The compiled SpaceGass model with ID mappings.",
            GH_ParamAccess.item);
        _outStatus = pManager.AddTextParameter("Status", "S",
            "Assembly status and warnings.",
            GH_ParamAccess.item);
    }

    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
        base.AppendAdditionalMenuItems(menu);
        Menu_AppendItem(menu, "Cancel", (s, e) => { RequestCancellation(); });
    }

    // ── Worker ────────────────────────────────────────────────────

    private sealed class AssembleWorker : WorkerInstance<AssembleModelComponent>
    {
        public AssembleWorker(
            AssembleModelComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken)
        {
        }

        private bool AssembleEnabled { get; set; }
        private List<SgMemberData> Members { get; set; } = new();
        private List<SgPlateData> Plates { get; set; } = new();
        private List<SgRestraintData> Restraints { get; set; } = new();
        private List<SgNodeConstraintData> Constraints { get; set; } = new();
        private List<SgNodeLoadData> NodeLoads { get; set; } = new();
        private List<SgMemberDistributedLoadData> DistLoads { get; set; } = new();
        private List<SgSelfWeightLoadData> SelfWeightLoads { get; set; } = new();
        private List<SgLumpedMassLoadData> LumpedMassLoads { get; set; } = new();
        private List<SgPrescribedDisplacementData> PrescribedDisplacements { get; set; } = new();
        private List<SgMemberConcentratedLoadData> MemberConcentratedLoads { get; set; } = new();
        private List<SgMemberPrestressLoadData> MemberPrestressLoads { get; set; } = new();
        private List<SgPlatePressureLoadData> PlatePressureLoads { get; set; } = new();
        private List<SgThermalLoadData> ThermalLoads { get; set; } = new();
        private List<SgCombinationLoadCaseData> CombinationLoadCases { get; set; } = new();
        private double Tolerance { get; set; }

        private SgModelData Model { get; set; }
        private string Status { get; set; } = string.Empty;

        public override WorkerInstance<AssembleModelComponent> Duplicate(
            string id, CancellationToken cancellationToken)
        {
            return new AssembleWorker(Parent, id, cancellationToken);
        }

        public override void GetData(IGH_DataAccess da, GH_ComponentParamServer paramServer)
        {
            var assemble = false;
            da.GetData(Parent._inAssemble, ref assemble);
            AssembleEnabled = assemble;

            var memberGoos = new List<GH_SgMember>();
            da.GetDataList(Parent._inMembers, memberGoos);

            Members = new List<SgMemberData>(memberGoos.Count);
            foreach (var goo in memberGoos)
                if (goo?.Value != null)
                    Members.Add(goo.Value);

            // Plates (optional)
            var plateGoos = new List<GH_SgPlate>();
            da.GetDataList(Parent._inPlates, plateGoos);
            Plates = new List<SgPlateData>();
            foreach (var goo in plateGoos)
                if (goo?.Value != null)
                    Plates.Add(goo.Value);

            // Restraints (optional)
            var restraintGoos = new List<GH_SgRestraint>();
            da.GetDataList(Parent._inRestraints, restraintGoos);
            Restraints = new List<SgRestraintData>();
            foreach (var goo in restraintGoos)
                if (goo?.Value != null)
                    Restraints.Add(goo.Value);

            // Constraints (optional)
            var constraintGoos = new List<GH_SgNodeConstraint>();
            da.GetDataList(Parent._inConstraints, constraintGoos);
            Constraints = new List<SgNodeConstraintData>();
            foreach (var goo in constraintGoos)
                if (goo?.Value != null)
                    Constraints.Add(goo.Value);

            // Loads (optional) — unified input accepting all load types
            var loadGoos = new List<IGH_Goo>();
            da.GetDataList(Parent._inLoads, loadGoos);
            NodeLoads = new List<SgNodeLoadData>();
            DistLoads = new List<SgMemberDistributedLoadData>();
            SelfWeightLoads = new List<SgSelfWeightLoadData>();
            LumpedMassLoads = new List<SgLumpedMassLoadData>();
            PrescribedDisplacements = new List<SgPrescribedDisplacementData>();
            MemberConcentratedLoads = new List<SgMemberConcentratedLoadData>();
            MemberPrestressLoads = new List<SgMemberPrestressLoadData>();
            PlatePressureLoads = new List<SgPlatePressureLoadData>();
            ThermalLoads = new List<SgThermalLoadData>();
            foreach (var goo in loadGoos)
            {
                switch (goo)
                {
                    case GH_SgNodeLoad nlGoo when nlGoo.Value != null:
                        NodeLoads.Add(nlGoo.Value);
                        break;
                    case GH_SgMemberDistributedLoad dlGoo when dlGoo.Value != null:
                        DistLoads.Add(dlGoo.Value);
                        break;
                    case GH_SgSelfWeightLoad swGoo when swGoo.Value != null:
                        SelfWeightLoads.Add(swGoo.Value);
                        break;
                    case GH_SgLumpedMassLoad lmGoo when lmGoo.Value != null:
                        LumpedMassLoads.Add(lmGoo.Value);
                        break;
                    case GH_SgPrescribedDisplacement pdGoo when pdGoo.Value != null:
                        PrescribedDisplacements.Add(pdGoo.Value);
                        break;
                    case GH_SgMemberConcentratedLoad clGoo when clGoo.Value != null:
                        MemberConcentratedLoads.Add(clGoo.Value);
                        break;
                    case GH_SgMemberPrestressLoad plGoo when plGoo.Value != null:
                        MemberPrestressLoads.Add(plGoo.Value);
                        break;
                    case GH_SgPlatePressureLoad ppGoo when ppGoo.Value != null:
                        PlatePressureLoads.Add(ppGoo.Value);
                        break;
                    case GH_SgThermalLoad tlGoo when tlGoo.Value != null:
                        ThermalLoads.Add(tlGoo.Value);
                        break;
                    default:
                        if (goo != null)
                            Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                                $"Unrecognised load type '{goo.TypeName}' — skipped.");
                        break;
                }
            }

            // Combination Load Cases (optional)
            var clcGoos = new List<GH_SgCombinationLoadCase>();
            da.GetDataList(Parent._inCombinationLoadCases, clcGoos);
            CombinationLoadCases = new List<SgCombinationLoadCaseData>();
            foreach (var goo in clcGoos)
                if (goo?.Value != null)
                    CombinationLoadCases.Add(goo.Value);

            // Default to Rhino document tolerance
            var tolerance = RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.001;
            da.GetData(Parent._inTolerance, ref tolerance);
            Tolerance = tolerance;
        }

        public override async Task DoWork(Action<string, double> reportProgress, Action done)
        {
            if (!AssembleEnabled)
            {
                Status = "Assembly not triggered. Set Assemble? to true.";
                Parent.Message = "Idle";
                if (!CancellationToken.IsCancellationRequested) done();
                return;
            }

            try
            {
                Parent.Message = "Assembling...";
                await AssembleAsync();
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                // Cancelled
            }
            catch (Exception ex)
            {
                Model = null;
                Status = ex.Message;
                Parent.Message = "Error";
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        private async Task AssembleAsync()
        {
            var session = SpaceGassSessionManager.Current;
            if (session == null || !session.IsConnected)
            {
                Status = "Not connected. Place a SpaceGass Connect component and set Connect? to true.";
                Parent.Message = "Not connected";
                return;
            }

            // Check for multiple Assemble Model instances (ADR-0005)
            var doc = Parent.OnPingDocument();
            if (doc != null)
            {
                var count = 0;
                foreach (var obj in doc.Objects)
                    if (obj is AssembleModelComponent)
                        count++;
                if (count > 1)
                    Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"{count} Assemble Model components detected. Only the last to solve owns the job.");
            }

            var result = await session.AssembleModelAsync(
                    Members, Tolerance, Restraints, NodeLoads, DistLoads, SelfWeightLoads, CombinationLoadCases,
                    LumpedMassLoads, PrescribedDisplacements, MemberConcentratedLoads, MemberPrestressLoads,
                    Constraints, Plates, PlatePressureLoads, ThermalLoads, CancellationToken)
                .ConfigureAwait(false);
            Model = result.Model;

            // Build status string
            var statusParts = new List<string>
            {
                $"Assembled: {Model.NodeMap.Count} nodes, {Model.MemberMap.Count} members, " +
                $"{Model.PlateMap.Count} plates, " +
                $"{Model.SectionMap.Count} sections, {Model.MaterialMap.Count} materials, " +
                $"{Model.RestraintMap.Count} restraints, " +
                $"{Model.ConstraintCount} constraints, " +
                $"{Model.LoadCaseMap.Count} load cases, {Model.CombinationLoadCaseMap.Count} combination load cases, " +
                $"{Model.LoadCategoryMap.Count} load categories, " +
                $"{Model.NodeLoadCount} node loads, " +
                $"{Model.MemberDistributedLoadCount} distributed loads, " +
                $"{Model.MemberDistributedMomentCount} distributed moments, " +
                $"{Model.MemberConcentratedLoadCount} concentrated loads, " +
                $"{Model.MemberPrestressLoadCount} prestress loads, " +
                $"{Model.SelfWeightLoadCount} self-weight loads, " +
                $"{Model.LumpedMassLoadCount} lumped mass loads, " +
                $"{Model.PrescribedDisplacementCount} prescribed displacements, " +
                $"{Model.PlatePressureLoadCount} plate pressure loads, " +
                $"{Model.ThermalLoadCount} thermal loads."
            };

            foreach (var warning in result.Warnings)
            {
                statusParts.Add($"Warning: {warning}");
                Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, warning);
            }

            Status = string.Join("\n", statusParts);
            Parent.Message = $"Assembled ({Model.NodeMap.Count}N, {Model.MemberMap.Count}M)";
        }

        public override void SetData(IGH_DataAccess da)
        {
            if (Model != null)
                da.SetData(Parent._outModel, new GH_SgModel(Model));
            da.SetData(Parent._outStatus, Status);
        }
    }
}