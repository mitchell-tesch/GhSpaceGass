using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GhSpaceGass.Async;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Models.Visuals;
using GhSpaceGass.Core.Services;
using GhSpaceGass.Helpers;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Display;
using Rhino.Geometry;

namespace GhSpaceGass.Components.Results;

public class GetPlateForcesComponent : GH_AsyncComponent<GetPlateForcesComponent>
{
    private int _inLoadCases;
    private int _inMode;
    private int _inModel;
    private int _inPlates;
    private int _inVisual;
    private int _inShowValues;

    // Shared outputs
    private int _outLoadCases, _outPlates, _outMeshes;

    // Element Forces mode outputs
    private int _outFx, _outFy, _outFxy;
    private int _outMx, _outMy, _outMxy;
    private int _outMxTop, _outMxBtm, _outMyTop, _outMyBtm;
    private int _outVxz, _outVyz;

    // Nodal Forces mode outputs
    private int _outNodes, _outPoints;
    private int _outNFx, _outNFy, _outNFz, _outNMx, _outNMy, _outNMz;

    private int _outWarnings, _outStatus;

    // Preview state
    private List<(Mesh Mesh, DisplayMaterial Material, Point3d Centroid, double Value)> _previewContours = new();
    private bool _showValues;

    public GetPlateForcesComponent()
        : base("SG Plate Forces", "sgPlateForces",
            "Query plate forces from a SpaceGass analysis (Element Forces or Nodal Forces mode). " +
            "Draws colour contour on plates in Element Forces mode when preview is enabled.",
            "SpaceGass", "8 | Results")
    {
        BaseWorker = new GetPlateForcesWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.tertiary;
    protected override Bitmap Icon => Icons.IconFactory.PlateForces();
    public override Guid ComponentGuid => new("A3E70C10-B8A3-4D22-9F1D-7E6A5C4B3D30");
    public override bool IsPreviewCapable => true;

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The assembled and analysed SpaceGass model.",
            GH_ParamAccess.item);
        _inPlates = pManager.AddParameter(new Param_SgPlate(),
            "Plates", "P",
            "Optional — filter to specific plates.",
            GH_ParamAccess.list);
        _inLoadCases = pManager.AddTextParameter(
            "Load Cases", "LC",
            "Optional — filter to specific load case names.",
            GH_ParamAccess.list);
        _inMode = pManager.AddParameter(
            new Param_SgIntegerOption("Mode", ValueListHelper.PlateForceModeOptions,
                defaultValue: 0, autoCreate: true),
            "Mode", "Mo",
            "Element Forces=0 (average forces per plate), Nodal Forces=1 (forces at each corner node).\n" +
            "Default: Element Forces.",
            GH_ParamAccess.item);
        _inVisual = pManager.AddParameter(
            new Param_SgIntegerOption("Visual", ValueListHelper.PlateForceVisualOptions,
                defaultValue: 3, autoCreate: true),
            "Visual", "V",
            "Force component to contour in Element Forces mode.\n" +
            "Fx=0, Fy=1, Fxy=2, Mx=3, My=4, Mxy=5, Vxz=6, Vyz=7, MxTop=8, MxBtm=9, MyTop=10, MyBtm=11.\n" +
            "Default: Mx.",
            GH_ParamAccess.item);
        _inShowValues = pManager.AddBooleanParameter("Show Values?", "SV",
            "When true, display force/moment values at each plate centroid.",
            GH_ParamAccess.item, false);

        pManager[_inPlates].Optional = true;
        pManager[_inLoadCases].Optional = true;
        pManager[_inMode].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        // Shared
        _outLoadCases = pManager.AddTextParameter("Load Cases", "LC",
            "Load case names, one per branch matching the load case dimension of the results tree.",
            GH_ParamAccess.tree);
        _outPlates = pManager.AddIntegerParameter("Plate IDs", "PIds",
            "Plate IDs, branched by {load_case} in Element Forces mode or {load_case; plate} in Nodal Forces mode.",
            GH_ParamAccess.tree);
        _outMeshes = pManager.AddMeshParameter("PlateMeshes", "PMsh",
            "Plate meshes (first load case branch only — identical across load cases).",
            GH_ParamAccess.tree);

        // Element Forces mode (populated in Element Forces mode only)
        _outFx = pManager.AddNumberParameter("Fx", "Fx",
            "In-plane force X, branched by {load_case}.", GH_ParamAccess.tree);
        _outFy = pManager.AddNumberParameter("Fy", "Fy",
            "In-plane force Y, branched by {load_case}.", GH_ParamAccess.tree);
        _outFxy = pManager.AddNumberParameter("Fxy", "Fxy",
            "In-plane shear, branched by {load_case}.", GH_ParamAccess.tree);
        _outMx = pManager.AddNumberParameter("Mx", "Mx",
            "Bending moment X, branched by {load_case}.", GH_ParamAccess.tree);
        _outMy = pManager.AddNumberParameter("My", "My",
            "Bending moment Y, branched by {load_case}.", GH_ParamAccess.tree);
        _outMxy = pManager.AddNumberParameter("Mxy", "Mxy",
            "Twisting moment, branched by {load_case}.", GH_ParamAccess.tree);
        _outMxTop = pManager.AddNumberParameter("Mx Top", "MxT",
            "Bending moment X at top fibre, branched by {load_case}.", GH_ParamAccess.tree);
        _outMxBtm = pManager.AddNumberParameter("Mx Btm", "MxB",
            "Bending moment X at bottom fibre, branched by {load_case}.", GH_ParamAccess.tree);
        _outMyTop = pManager.AddNumberParameter("My Top", "MyT",
            "Bending moment Y at top fibre, branched by {load_case}.", GH_ParamAccess.tree);
        _outMyBtm = pManager.AddNumberParameter("My Btm", "MyB",
            "Bending moment Y at bottom fibre, branched by {load_case}.", GH_ParamAccess.tree);
        _outVxz = pManager.AddNumberParameter("Vxz", "Vxz",
            "Transverse shear XZ, branched by {load_case}.", GH_ParamAccess.tree);
        _outVyz = pManager.AddNumberParameter("Vyz", "Vyz",
            "Transverse shear YZ, branched by {load_case}.", GH_ParamAccess.tree);

        // Nodal Forces mode (populated in Nodal Forces mode only)
        _outNodes = pManager.AddIntegerParameter("Node IDs", "N",
            "Node IDs, branched by {load_case; plate}. Only populated in Nodal Forces mode.",
            GH_ParamAccess.tree);
        _outPoints = pManager.AddPointParameter("Points", "P",
            "Node locations, branched by {load_case; plate}. Only populated in Nodal Forces mode.",
            GH_ParamAccess.tree);
        _outNFx = pManager.AddNumberParameter("N Fx", "NFx",
            "Nodal force X, branched by {load_case; plate}.", GH_ParamAccess.tree);
        _outNFy = pManager.AddNumberParameter("N Fy", "NFy",
            "Nodal force Y, branched by {load_case; plate}.", GH_ParamAccess.tree);
        _outNFz = pManager.AddNumberParameter("N Fz", "NFz",
            "Nodal force Z, branched by {load_case; plate}.", GH_ParamAccess.tree);
        _outNMx = pManager.AddNumberParameter("N Mx", "NMx",
            "Nodal moment X, branched by {load_case; plate}.", GH_ParamAccess.tree);
        _outNMy = pManager.AddNumberParameter("N My", "NMy",
            "Nodal moment Y, branched by {load_case; plate}.", GH_ParamAccess.tree);
        _outNMz = pManager.AddNumberParameter("N Mz", "NMz",
            "Nodal moment Z, branched by {load_case; plate}.", GH_ParamAccess.tree);

        _outWarnings = pManager.AddTextParameter("Warnings", "W",
            "Warnings from the SpaceGass API query.", GH_ParamAccess.item);
        _outStatus = pManager.AddTextParameter("Status", "S",
            "Query status summary.", GH_ParamAccess.item);
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

    public override void DrawViewportMeshes(IGH_PreviewArgs args)
    {
        base.DrawViewportMeshes(args);
        foreach (var (mesh, material, _, _) in _previewContours)
        {
            args.Display.DrawMeshShaded(mesh, material);
            args.Display.DrawMeshWires(mesh, Color.FromArgb(80, 80, 80), 1);
        }
    }

    public override void DrawViewportWires(IGH_PreviewArgs args)
    {
        base.DrawViewportWires(args);
        if (!_showValues) return;
        foreach (var (_, _, centroid, value) in _previewContours)
        {
            args.Display.Draw2dText(
                value.ToString("G4"), Color.Black, centroid, false, 12);
        }
    }

    public override BoundingBox ClippingBox
    {
        get
        {
            var box = base.ClippingBox;
            foreach (var (mesh, _, _, _) in _previewContours)
                box.Union(mesh.GetBoundingBox(false));
            return box;
        }
    }

    private static Color ContourColor(double normalisedValue)
    {
        // Turbo colormap: remap [-1..1] → [0..1] then apply Turbo polynomial
        var t = Math.Clamp((normalisedValue + 1.0) * 0.5, 0.0, 1.0);
        return TurboColor(t);
    }

    /// <summary>Turbo colormap by Anton Mikhailov (Google). Maps t ∈ [0,1] → RGB.</summary>
    private static Color TurboColor(double t)
    {
        var r = Math.Clamp(0.13572138 + t * (4.61539260 + t * (-42.66032258 + t * (132.13108234 + t * (-152.94239396 + t * 59.28637943)))), 0.0, 1.0);
        var g = Math.Clamp(0.09140261 + t * (2.19418839 + t * (4.84296658 + t * (-14.18503333 + t * (4.27729857 + t * 2.82956604)))), 0.0, 1.0);
        var b = Math.Clamp(0.10667330 + t * (12.64194608 + t * (-60.58204836 + t * (110.36276771 + t * (-89.90310912 + t * 27.34824973)))), 0.0, 1.0);
        return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }

    // ── Worker ────────────────────────────────────────────────────

    private sealed class GetPlateForcesWorker : WorkerInstance<GetPlateForcesComponent>
    {
        public GetPlateForcesWorker(
            GetPlateForcesComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken)
        {
        }

        private SgModelData InputModel { get; set; }
        private List<SgPoint3D[]> PlateFilter { get; set; }
        private List<string> LoadCaseFilter { get; set; }
        private int Mode { get; set; }
        private int VisualIndex { get; set; } = 3;
        private bool ShowValues { get; set; }

        // Element Forces output trees
        private GH_Structure<GH_String> OutLoadCases { get; set; }
        private GH_Structure<GH_Integer> OutPlates { get; set; }
        private GH_Structure<GH_Mesh> OutMeshes { get; set; }
        private GH_Structure<GH_Number> OutFx { get; set; }
        private GH_Structure<GH_Number> OutFy { get; set; }
        private GH_Structure<GH_Number> OutFxy { get; set; }
        private GH_Structure<GH_Number> OutMx { get; set; }
        private GH_Structure<GH_Number> OutMy { get; set; }
        private GH_Structure<GH_Number> OutMxy { get; set; }
        private GH_Structure<GH_Number> OutMxTop { get; set; }
        private GH_Structure<GH_Number> OutMxBtm { get; set; }
        private GH_Structure<GH_Number> OutMyTop { get; set; }
        private GH_Structure<GH_Number> OutMyBtm { get; set; }
        private GH_Structure<GH_Number> OutVxz { get; set; }
        private GH_Structure<GH_Number> OutVyz { get; set; }

        // Nodal Forces output trees
        private GH_Structure<GH_Integer> OutNodes { get; set; }
        private GH_Structure<GH_Point> OutPoints { get; set; }
        private GH_Structure<GH_Number> OutNFx { get; set; }
        private GH_Structure<GH_Number> OutNFy { get; set; }
        private GH_Structure<GH_Number> OutNFz { get; set; }
        private GH_Structure<GH_Number> OutNMx { get; set; }
        private GH_Structure<GH_Number> OutNMy { get; set; }
        private GH_Structure<GH_Number> OutNMz { get; set; }

        private string OutWarningsText { get; set; }
        private string Status { get; set; } = string.Empty;
        private List<(Mesh Mesh, Color FaceColor, SgPoint3D Centroid, double Value)> PreviewContours { get; set; } = new();

        public override WorkerInstance<GetPlateForcesComponent> Duplicate(
            string id, CancellationToken cancellationToken)
        {
            return new GetPlateForcesWorker(Parent, id, cancellationToken);
        }

        public override void GetData(IGH_DataAccess da, GH_ComponentParamServer paramServer)
        {
            var modelGoo = new GH_SgModel();
            if (!da.GetData(Parent._inModel, ref modelGoo) || modelGoo?.Value == null) return;
            InputModel = modelGoo.Value;

            var plateGoos = new List<GH_SgPlate>();
            da.GetDataList(Parent._inPlates, plateGoos);
            PlateFilter = new List<SgPoint3D[]>();
            foreach (var g in plateGoos)
                if (g?.Value != null)
                    PlateFilter.Add(g.Value.Nodes);

            var lcNames = new List<GH_String>();
            if (da.GetDataList(Parent._inLoadCases, lcNames) && lcNames.Count > 0)
                LoadCaseFilter = lcNames
                    .Where(s => s?.Value != null)
                    .Select(s => s.Value)
                    .ToList();

            var mode = 0;
            da.GetData(Parent._inMode, ref mode);
            Mode = mode;

            var visual = 3;
            da.GetData(Parent._inVisual, ref visual);
            VisualIndex = visual;

            var showValues = false;
            da.GetData(Parent._inShowValues, ref showValues);
            ShowValues = showValues;
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
                if (Mode == 1)
                    await QueryNodalForcesAsync();
                else
                    await QueryElementForcesAsync();
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                var message = ModelAssembler.FormatApiError(ex, "querying plate forces");
                Status = $"Error: {message}";
                SetComponentMessage("Error");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        private void InitEmptyOutputs()
        {
            OutLoadCases = new GH_Structure<GH_String>();
            OutPlates = new GH_Structure<GH_Integer>();
            OutMeshes = new GH_Structure<GH_Mesh>();
            OutFx = new GH_Structure<GH_Number>();
            OutFy = new GH_Structure<GH_Number>();
            OutFxy = new GH_Structure<GH_Number>();
            OutMx = new GH_Structure<GH_Number>();
            OutMy = new GH_Structure<GH_Number>();
            OutMxy = new GH_Structure<GH_Number>();
            OutMxTop = new GH_Structure<GH_Number>();
            OutMxBtm = new GH_Structure<GH_Number>();
            OutMyTop = new GH_Structure<GH_Number>();
            OutMyBtm = new GH_Structure<GH_Number>();
            OutVxz = new GH_Structure<GH_Number>();
            OutVyz = new GH_Structure<GH_Number>();
            OutNodes = new GH_Structure<GH_Integer>();
            OutPoints = new GH_Structure<GH_Point>();
            OutNFx = new GH_Structure<GH_Number>();
            OutNFy = new GH_Structure<GH_Number>();
            OutNFz = new GH_Structure<GH_Number>();
            OutNMx = new GH_Structure<GH_Number>();
            OutNMy = new GH_Structure<GH_Number>();
            OutNMz = new GH_Structure<GH_Number>();
        }

        private async Task QueryElementForcesAsync()
        {
            var session = SpaceGassSessionManager.Current;
            if (session == null || !session.IsConnected)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Not connected. Place a SpaceGass Connect component and set Connect? to true.");
                SetComponentMessage("Not connected");
                return;
            }

            var plateFilterArr = PlateFilter?.Count > 0 ? PlateFilter : null;
            var lcFilterArr = LoadCaseFilter?.Count > 0 ? LoadCaseFilter : null;

            var result = await session.GetPlateElementForcesAsync(
                InputModel, plateFilterArr, lcFilterArr, CancellationToken).ConfigureAwait(false);

            foreach (var w in result.Warnings)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
            OutWarningsText = result.Warnings.Count > 0 ? string.Join(Environment.NewLine, result.Warnings) : "";

            if (result.Forces.Count == 0)
            {
                SetComponentMessage("No element forces");
                InitEmptyOutputs();
                return;
            }

            // Build reverse load case map
            var idToLcName = InputModel.BuildLoadCaseIdToNameMap();

            // Group by load case — tree {load_case} with plates as list items (ADR-0008)
            var byLoadCase = result.Forces
                .GroupBy(f => f.LoadCaseId)
                .OrderBy(g => g.Key)
                .ToList();

            InitEmptyOutputs();

            for (var lcIdx = 0; lcIdx < byLoadCase.Count; lcIdx++)
            {
                var lcGroup = byLoadCase[lcIdx];
                var path = new GH_Path(lcIdx);
                OutLoadCases.Append(
                    new GH_String(idToLcName.TryGetValue(lcGroup.Key, out var lcName)
                        ? lcName : $"Load Case {lcGroup.Key}"), path);

                foreach (var f in lcGroup.OrderBy(ef => ef.PlateId))
                {
                    OutPlates.Append(new GH_Integer(f.PlateId), path);
                    if (lcIdx == 0)
                        OutMeshes.Append(new GH_Mesh(BuildPlateMesh(InputModel, f.PlateId)), path);
                    OutFx.Append(new GH_Number(f.Fx), path);
                    OutFy.Append(new GH_Number(f.Fy), path);
                    OutFxy.Append(new GH_Number(f.Fxy), path);
                    OutMx.Append(new GH_Number(f.Mx), path);
                    OutMy.Append(new GH_Number(f.My), path);
                    OutMxy.Append(new GH_Number(f.Mxy), path);
                    OutMxTop.Append(new GH_Number(f.MxTop), path);
                    OutMxBtm.Append(new GH_Number(f.MxBtm), path);
                    OutMyTop.Append(new GH_Number(f.MyTop), path);
                    OutMyBtm.Append(new GH_Number(f.MyBtm), path);
                    OutVxz.Append(new GH_Number(f.Vxz), path);
                    OutVyz.Append(new GH_Number(f.Vyz), path);
                }
            }

            SetComponentMessage($"{result.Forces.Count} element forces");
            Status = $"{result.Forces.Count} element forces queried.";

            // Build plate contour preview (skip when Visual = None)
            if (VisualIndex >= 0)
            {
                var plateMapSg = new Dictionary<int, SgPoint3D[]>();
                foreach (var kvp in InputModel.PlateMap) plateMapSg[kvp.Key] = kvp.Value;
                var contourResult = PlateContourBuilder.Build(result.Forces, plateMapSg, VisualIndex);

                PreviewContours = contourResult.Contours
                    .Select(c =>
                    {
                        var mesh = BuildPlateMesh(InputModel, c.CornerPoints);
                        var color = ContourColor(c.NormalisedValue);
                        return (mesh, color, c.Centroid, c.Value);
                    })
                    .ToList();
            }
        }

        private async Task QueryNodalForcesAsync()
        {
            var session = SpaceGassSessionManager.Current;
            if (session == null || !session.IsConnected)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Not connected. Place a SpaceGass Connect component and set Connect? to true.");
                SetComponentMessage("Not connected");
                return;
            }

            var plateFilterArr = PlateFilter?.Count > 0 ? PlateFilter : null;
            var lcFilterArr = LoadCaseFilter?.Count > 0 ? LoadCaseFilter : null;

            var result = await session.GetPlateNodalForcesAsync(
                InputModel, plateFilterArr, lcFilterArr, CancellationToken).ConfigureAwait(false);

            foreach (var w in result.Warnings)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
            OutWarningsText = result.Warnings.Count > 0 ? string.Join(Environment.NewLine, result.Warnings) : "";

            if (result.Forces.Count == 0)
            {
                SetComponentMessage("No nodal forces");
                InitEmptyOutputs();
                return;
            }

            // Build reverse load case map
            var idToLcName = InputModel.BuildLoadCaseIdToNameMap();

            // Build reverse node map for point resolution
            var idToPoint = new Dictionary<int, Point3d>();
            foreach (var kvp in InputModel.NodeMap)
                idToPoint[kvp.Value] = kvp.Key.ToPoint3d();

            // Group by load case, then by plate — tree {load_case; plate} (ADR-0015)
            var byLoadCase = result.Forces
                .GroupBy(f => f.LoadCaseId)
                .OrderBy(g => g.Key)
                .ToList();

            InitEmptyOutputs();

            // Build plate index map across all load cases
            var plateIdOrder = result.Forces
                .Select(f => f.PlateId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            var plateIndexMap = new Dictionary<int, int>();
            for (var pi = 0; pi < plateIdOrder.Count; pi++)
                plateIndexMap[plateIdOrder[pi]] = pi;

            for (var lcIdx = 0; lcIdx < byLoadCase.Count; lcIdx++)
            {
                var lcGroup = byLoadCase[lcIdx];
                OutLoadCases.Append(
                    new GH_String(idToLcName.TryGetValue(lcGroup.Key, out var lcName)
                        ? lcName : $"Load Case {lcGroup.Key}"),
                    new GH_Path(lcIdx));

                var byPlate = lcGroup
                    .GroupBy(f => f.PlateId)
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var plateGroup in byPlate)
                {
                    var plateIdx = plateIndexMap[plateGroup.Key];
                    var path = new GH_Path(lcIdx, plateIdx);

                    OutPlates.Append(new GH_Integer(plateGroup.Key), path);
                    if (lcIdx == 0)
                        OutMeshes.Append(new GH_Mesh(BuildPlateMesh(InputModel, plateGroup.Key)), path);

                    foreach (var f in plateGroup.OrderBy(nf => nf.NodeId))
                    {
                        OutNodes.Append(new GH_Integer(f.NodeId), path);
                        OutPoints.Append(
                            idToPoint.TryGetValue(f.NodeId, out var pt)
                                ? new GH_Point(pt)
                                : new GH_Point(Point3d.Unset), path);
                        OutNFx.Append(new GH_Number(f.Fx), path);
                        OutNFy.Append(new GH_Number(f.Fy), path);
                        OutNFz.Append(new GH_Number(f.Fz), path);
                        OutNMx.Append(new GH_Number(f.Mx), path);
                        OutNMy.Append(new GH_Number(f.My), path);
                        OutNMz.Append(new GH_Number(f.Mz), path);
                    }
                }
            }

            SetComponentMessage($"{result.Forces.Count} nodal forces");
            Status = $"{result.Forces.Count} nodal forces queried.";
        }

        private static Mesh BuildPlateMesh(SgModelData model, int plateId)
        {
            if (!model.PlateMap.TryGetValue(plateId, out var corners))
                return new Mesh();
            return BuildPlateMesh(model, corners);
        }

        private static Mesh BuildPlateMesh(SgModelData _, SgPoint3D[] corners)
        {
            var mesh = new Mesh();
            foreach (var pt in corners)
                mesh.Vertices.Add(pt.ToPoint3d());

            if (corners.Length == 3)
                mesh.Faces.AddFace(0, 1, 2);
            else if (corners.Length >= 4)
                mesh.Faces.AddFace(0, 1, 2, 3);

            mesh.Normals.ComputeNormals();
            return mesh;
        }

        public override void SetData(IGH_DataAccess da)
        {
            if (OutLoadCases != null) da.SetDataTree(Parent._outLoadCases, OutLoadCases);
            if (OutPlates != null) da.SetDataTree(Parent._outPlates, OutPlates);
            if (OutMeshes != null) da.SetDataTree(Parent._outMeshes, OutMeshes);
            if (OutFx != null) da.SetDataTree(Parent._outFx, OutFx);
            if (OutFy != null) da.SetDataTree(Parent._outFy, OutFy);
            if (OutFxy != null) da.SetDataTree(Parent._outFxy, OutFxy);
            if (OutMx != null) da.SetDataTree(Parent._outMx, OutMx);
            if (OutMy != null) da.SetDataTree(Parent._outMy, OutMy);
            if (OutMxy != null) da.SetDataTree(Parent._outMxy, OutMxy);
            if (OutMxTop != null) da.SetDataTree(Parent._outMxTop, OutMxTop);
            if (OutMxBtm != null) da.SetDataTree(Parent._outMxBtm, OutMxBtm);
            if (OutMyTop != null) da.SetDataTree(Parent._outMyTop, OutMyTop);
            if (OutMyBtm != null) da.SetDataTree(Parent._outMyBtm, OutMyBtm);
            if (OutVxz != null) da.SetDataTree(Parent._outVxz, OutVxz);
            if (OutVyz != null) da.SetDataTree(Parent._outVyz, OutVyz);
            if (OutNodes != null) da.SetDataTree(Parent._outNodes, OutNodes);
            if (OutPoints != null) da.SetDataTree(Parent._outPoints, OutPoints);
            if (OutNFx != null) da.SetDataTree(Parent._outNFx, OutNFx);
            if (OutNFy != null) da.SetDataTree(Parent._outNFy, OutNFy);
            if (OutNFz != null) da.SetDataTree(Parent._outNFz, OutNFz);
            if (OutNMx != null) da.SetDataTree(Parent._outNMx, OutNMx);
            if (OutNMy != null) da.SetDataTree(Parent._outNMy, OutNMy);
            if (OutNMz != null) da.SetDataTree(Parent._outNMz, OutNMz);
            da.SetData(Parent._outWarnings, OutWarningsText ?? "");
            da.SetData(Parent._outStatus, Status);

            Parent._previewContours = PreviewContours
                .Select(c => (
                    c.Mesh,
                    new DisplayMaterial(c.FaceColor) { Transparency = 0.3 },
                    c.Centroid.ToPoint3d(),
                    c.Value))
                .ToList();
            Parent._showValues = ShowValues;
        }
    }
}
