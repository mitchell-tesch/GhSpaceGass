using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GhSpaceGass.Async;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace GhSpaceGass.Components.Model;

public class DisassembleModelComponent : GH_AsyncComponent<DisassembleModelComponent>
{
    private int _inDisassemble;

    private int _outLines;
    private int _outMemberIds;
    private int _outMemberMaterials;
    private int _outMemberSections;
    private int _outMemberTypes;
    private int _outMeshes;
    private int _outModel;
    private int _outNodeIds;
    private int _outPlateIds;
    private int _outPoints;
    private int _outStatus;

    public DisassembleModelComponent()
        : base("SG Disassemble Model", "sgDisassemble",
            "Read structural data from an existing SpaceGass model in the open job.\n" +
            "Outputs geometry and an SgModel for downstream chaining to Run Analysis and Results.",
            "SpaceGass", "6 | Model")
    {
        BaseWorker = new DisassembleWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.secondary;
    protected override Bitmap Icon => Icons.IconFactory.DisassembleModel();
    public override Guid ComponentGuid => new("991C56A7-EF1B-466B-8B58-0232F7BC75C2");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inDisassemble = pManager.AddBooleanParameter("Disassemble?", "D?",
            "Set to true to read the model from SpaceGass.",
            GH_ParamAccess.item, false);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The SpaceGass model with ID ↔ geometry mappings for downstream chaining.",
            GH_ParamAccess.item);
        _outPoints = pManager.AddPointParameter("Points", "Pts",
            "All node locations, ordered by node ID.",
            GH_ParamAccess.list);
        _outNodeIds = pManager.AddIntegerParameter("Node IDs", "NId",
            "Node IDs, matching Points order.",
            GH_ParamAccess.list);
        _outLines = pManager.AddLineParameter("Lines", "Ln",
            "Member geometry (node A → node B), ordered by member ID.",
            GH_ParamAccess.list);
        _outMemberIds = pManager.AddIntegerParameter("Member IDs", "MId",
            "Member IDs, matching Lines order.",
            GH_ParamAccess.list);
        _outMemberTypes = pManager.AddTextParameter("Member Types", "MT",
            "Type name per member (Beam, Truss, etc.), matching Lines order.",
            GH_ParamAccess.list);
        _outMemberSections = pManager.AddIntegerParameter("Member Sections", "MS",
            "Section ID per member, matching Lines order. Resolve to names via the SectionMap on the Model output.",
            GH_ParamAccess.list);
        _outMemberMaterials = pManager.AddIntegerParameter("Member Materials", "MM",
            "Material ID per member, matching Lines order. Resolve to names via the MaterialMap on the Model output.",
            GH_ParamAccess.list);
        _outMeshes = pManager.AddMeshParameter("Meshes", "Msh",
            "One mesh per plate element (tri or quad face), ordered by plate ID.",
            GH_ParamAccess.list);
        _outPlateIds = pManager.AddIntegerParameter("Plate IDs", "PId",
            "Plate IDs, matching Meshes order.",
            GH_ParamAccess.list);
        _outStatus = pManager.AddTextParameter("Status", "S",
            "Disassembly status and warnings.",
            GH_ParamAccess.item);
    }

    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
        base.AppendAdditionalMenuItems(menu);
        Menu_AppendItem(menu, "Cancel", (_, _) => { RequestCancellation(); });
    }

    // ── Worker ────────────────────────────────────────────────────

    private sealed class DisassembleWorker : WorkerInstance<DisassembleModelComponent>
    {
        public DisassembleWorker(
            DisassembleModelComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken)
        {
        }

        private bool DisassembleEnabled { get; set; }
        private SgModelData Model { get; set; }
        private List<Point3d> Points { get; set; } = new();
        private List<int> NodeIds { get; set; } = new();
        private List<Line> Lines { get; set; } = new();
        private List<int> MemberIds { get; set; } = new();
        private List<string> MemberTypes { get; set; } = new();
        private List<int> MemberSections { get; set; } = new();
        private List<int> MemberMaterials { get; set; } = new();
        private List<Mesh> Meshes { get; set; } = new();
        private List<int> PlateIds { get; set; } = new();
        private string Status { get; set; } = string.Empty;

        public override WorkerInstance<DisassembleModelComponent> Duplicate(
            string id, CancellationToken cancellationToken)
        {
            return new DisassembleWorker(Parent, id, cancellationToken);
        }

        public override void GetData(IGH_DataAccess da, GH_ComponentParamServer paramServer)
        {
            var disassemble = false;
            da.GetData(Parent._inDisassemble, ref disassemble);
            DisassembleEnabled = disassemble;
        }

        public override async Task DoWork(Action<string, double> reportProgress, Action done)
        {
            if (!DisassembleEnabled)
            {
                Status = "Disassembly not triggered. Set Disassemble? to true.";
                Parent.Message = "Idle";
                if (!CancellationToken.IsCancellationRequested) done();
                return;
            }

            try
            {
                Parent.Message = "Disassembling...";
                await DisassembleAsync();
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

        private async Task DisassembleAsync()
        {
            var session = SpaceGassSessionManager.Current;
            if (session == null || !session.IsConnected)
            {
                Status = "Not connected. Place a SpaceGass Connect component and set Connect? to true.";
                Parent.Message = "Not connected";
                return;
            }

            var result = await session.DisassembleModelAsync(CancellationToken).ConfigureAwait(false);
            Model = result.Model;

            // Map domain data to Rhino geometry
            Points = new List<Point3d>(result.Nodes.Count);
            NodeIds = new List<int>(result.Nodes.Count);
            foreach (var n in result.Nodes)
            {
                Points.Add(new Point3d(n.Point.X, n.Point.Y, n.Point.Z));
                NodeIds.Add(n.Id);
            }

            Lines = new List<Line>(result.Members.Count);
            MemberIds = new List<int>(result.Members.Count);
            MemberTypes = new List<string>(result.Members.Count);
            MemberSections = new List<int>(result.Members.Count);
            MemberMaterials = new List<int>(result.Members.Count);
            foreach (var m in result.Members)
            {
                Lines.Add(new Line(
                    new Point3d(m.Start.X, m.Start.Y, m.Start.Z),
                    new Point3d(m.End.X, m.End.Y, m.End.Z)));
                MemberIds.Add(m.Id);
                MemberTypes.Add(m.TypeName);
                MemberSections.Add(m.SectionId);
                MemberMaterials.Add(m.MaterialId);
            }

            Meshes = new List<Mesh>(result.Plates.Count);
            PlateIds = new List<int>(result.Plates.Count);
            foreach (var p in result.Plates)
            {
                var mesh = new Mesh();
                foreach (var pt in p.CornerPoints)
                    mesh.Vertices.Add(new Point3d(pt.X, pt.Y, pt.Z));

                if (p.CornerPoints.Length == 3)
                    mesh.Faces.AddFace(0, 1, 2);
                else
                    mesh.Faces.AddFace(0, 1, 2, 3);

                mesh.Normals.ComputeNormals();
                Meshes.Add(mesh);
                PlateIds.Add(p.Id);
            }

            // Build status
            var statusParts = new List<string>
            {
                $"Disassembled: {result.Nodes.Count} nodes, {result.Members.Count} members, " +
                $"{result.Plates.Count} plates, " +
                $"{Model.SectionMap.Count} sections, {Model.MaterialMap.Count} materials, " +
                $"{Model.LoadCaseMap.Count} load cases, {Model.CombinationLoadCaseMap.Count} combination load cases."
            };

            foreach (var warning in result.Warnings)
            {
                statusParts.Add($"Warning: {warning}");
                Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, warning);
            }

            Status = string.Join("\n", statusParts);
            Parent.Message =
                $"Disassembled ({result.Nodes.Count}N, {result.Members.Count}M, {result.Plates.Count}P)";
        }

        public override void SetData(IGH_DataAccess da)
        {
            if (Model != null)
                da.SetData(Parent._outModel, new GH_SgModel(Model));
            da.SetDataList(Parent._outPoints, Points);
            da.SetDataList(Parent._outNodeIds, NodeIds);
            da.SetDataList(Parent._outLines, Lines);
            da.SetDataList(Parent._outMemberIds, MemberIds);
            da.SetDataList(Parent._outMemberTypes, MemberTypes);
            da.SetDataList(Parent._outMemberSections, MemberSections);
            da.SetDataList(Parent._outMemberMaterials, MemberMaterials);
            da.SetDataList(Parent._outMeshes, Meshes);
            da.SetDataList(Parent._outPlateIds, PlateIds);
            da.SetData(Parent._outStatus, Status);
        }
    }
}
