using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using GhSpaceGass.Async;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Helpers;
using GhSpaceGass.Types;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;

namespace GhSpaceGass.Components.Results;

public class GetPlateForcesComponent : GH_AsyncComponent<GetPlateForcesComponent>
{
    private int _inLoadCases;
    private int _inMode;
    private int _inModel;
    private int _inPlates;
    
    private int _outLoadCases, _outPlates;
    private int _outFx, _outFy, _outFxy, _outMx, _outMy, _outMxy, _outVxz, _outVyz;
    private int _outNodes;
    private int _outNFx, _outNFy, _outNFz, _outNMx, _outNMy, _outNMz;

    public GetPlateForcesComponent()
        : base("SG Plate Forces", "sgPlateForces",
            "Query plate forces from a SpaceGass analysis (Element Forces or Nodal Forces mode).",
            "SpaceGass", "8 | Results")
    {
        BaseWorker = new GetPlateForcesWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.tertiary;
    protected override Bitmap Icon => Icons.IconFactory.PlateForces();
    public override Guid ComponentGuid => new("A3E70C10-B8A3-4D22-9F1D-7E6A5C4B3D30");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The assembled SpaceGass model.",
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
            "Mode", "Mode",
            "Result mode (Element Forces=0, Nodal Forces=1). Default: Element Forces.",
            GH_ParamAccess.item);

        pManager[_inPlates].Optional = true;
        pManager[_inLoadCases].Optional = true;
        pManager[_inMode].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        // Shared outputs (indices 0-1)
        _outLoadCases = pManager.AddTextParameter("Load Cases", "LC", 
            "Load case names.",
            GH_ParamAccess.tree);
        _outPlates = pManager.AddIntegerParameter("Plates", "P",
            "Plate IDs.",
            GH_ParamAccess.tree);

        // Element Forces mode outputs (indices 2-9) — populated in Element Forces mode only
        _outFx = pManager.AddNumberParameter("Fx", "Fx",
            "In-plane force X (Element Forces mode).",
            GH_ParamAccess.tree);
        _outFy = pManager.AddNumberParameter("Fy", "Fy",
            "In-plane force Y (Element Forces mode).",
            GH_ParamAccess.tree);
        _outFxy = pManager.AddNumberParameter("Fxy", "Fxy",
            "In-plane shear (Element Forces mode).",
            GH_ParamAccess.tree);
        _outMx = pManager.AddNumberParameter("Mx", "Mx",
            "Bending moment X (Element Forces mode).",
            GH_ParamAccess.tree);
        _outMy = pManager.AddNumberParameter("My", "My",
            "Bending moment Y (Element Forces mode).",
            GH_ParamAccess.tree);
        _outMxy = pManager.AddNumberParameter("Mxy", "Mxy",
            "Twisting moment (Element Forces mode).",
            GH_ParamAccess.tree);
        _outVxz = pManager.AddNumberParameter("Vxz", "Vxz",
            "Transverse shear XZ (Element Forces mode).",
            GH_ParamAccess.tree);
        _outVyz = pManager.AddNumberParameter("Vyz", "Vyz",
            "Transverse shear YZ (Element Forces mode).",
            GH_ParamAccess.tree);

        // Nodal Forces mode outputs (indices 10-16) — populated in Nodal Forces mode only
        _outNodes = pManager.AddIntegerParameter("Nodes", "N",
            "Node IDs (Nodal Forces mode).",
            GH_ParamAccess.tree);
        _outNFx = pManager.AddNumberParameter("N Fx", "NFx",
            "Nodal force X (Nodal Forces mode).",
            GH_ParamAccess.tree);
        _outNFy = pManager.AddNumberParameter("N Fy", "NFy",
            "Nodal force Y (Nodal Forces mode).",
            GH_ParamAccess.tree);
        _outNFz = pManager.AddNumberParameter("N Fz", "NFz",
            "Nodal force Z (Nodal Forces mode).",
            GH_ParamAccess.tree);
        _outNMx = pManager.AddNumberParameter("N Mx", "NMx",
            "Nodal moment X (Nodal Forces mode).", GH_ParamAccess.tree);
        _outNMy = pManager.AddNumberParameter("N My", "NMy",
            "Nodal moment Y (Nodal Forces mode).", GH_ParamAccess.tree);
        _outNMz = pManager.AddNumberParameter("N Mz", "NMz",
            "Nodal moment Z (Nodal Forces mode).", GH_ParamAccess.tree);
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        document.ScheduleSolution(0, doc => ValueListHelper.AutoCreateOnPlacement(this, doc));
    }

    private sealed class GetPlateForcesWorker : WorkerInstance<GetPlateForcesComponent>
    {
        public GetPlateForcesWorker(
            GetPlateForcesComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken)
        {
        }

        private SgModelData Model { get; set; }
        private List<SgPoint3D[]> PlateFilter { get; set; }
        private List<string> LoadCaseFilter { get; set; }
        private int Mode { get; set; }

        // Results
        private SgPlateElementForcesResult ElementResult { get; set; }
        private SgPlateNodalForcesResult NodalResult { get; set; }

        public override WorkerInstance<GetPlateForcesComponent> Duplicate(
            string id, CancellationToken cancellationToken)
        {
            return new GetPlateForcesWorker(Parent, id, cancellationToken);
        }

        public override void GetData(IGH_DataAccess da, GH_ComponentParamServer paramServer)
        {
            GH_SgModel modelGoo = null;
            if (!da.GetData(Parent._inModel, ref modelGoo) || modelGoo?.Value == null) return;
            Model = modelGoo.Value;

            var plateGoos = new List<GH_SgPlate>();
            da.GetDataList(Parent._inPlates, plateGoos);
            PlateFilter = new List<SgPoint3D[]>();
            foreach (var g in plateGoos)
                if (g?.Value != null)
                    PlateFilter.Add(g.Value.Nodes);

            var lcNames = new List<string>();
            da.GetDataList(Parent._inLoadCases, lcNames);
            LoadCaseFilter = lcNames;

            var mode = 0;
            da.GetData(Parent._inMode, ref mode);
            Mode = mode;
        }

        public override async Task DoWork(Action<string, double> reportProgress, Action done)
        {
            if (Model == null)
            {
                if (!CancellationToken.IsCancellationRequested) done();
                return;
            }

            try
            {
                var session = SpaceGassSessionManager.Current;
                if (session == null || !session.IsConnected)
                {
                    Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        "Not connected to SpaceGass.");
                    if (!CancellationToken.IsCancellationRequested) done();
                    return;
                }

                var plateFilterArr = PlateFilter.Count > 0 ? PlateFilter : null;
                var lcFilterArr = LoadCaseFilter.Count > 0 ? LoadCaseFilter : null;

                if (Mode == 0) // Element Forces
                {
                    ElementResult = await session.GetPlateElementForcesAsync(
                        Model, plateFilterArr, lcFilterArr, CancellationToken);

                    foreach (var w in ElementResult.Warnings)
                        Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
                }
                else // Nodal Forces
                {
                    NodalResult = await session.GetPlateNodalForcesAsync(
                        Model, plateFilterArr, lcFilterArr, CancellationToken);

                    foreach (var w in NodalResult.Warnings)
                        Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
                }

                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        public override void SetData(IGH_DataAccess da)
        {
            if (Mode == 0 && ElementResult != null)
                SetElementForceData(da);
            else if (Mode == 1 && NodalResult != null)
                SetNodalForceData(da);
        }

        private void SetElementForceData(IGH_DataAccess da)
        {
            // Reverse load case map for name lookup
            var lcIdToName = new Dictionary<int, string>();
            foreach (var kvp in Model.LoadCaseMap) lcIdToName[kvp.Value] = kvp.Key;
            foreach (var kvp in Model.CombinationLoadCaseMap) lcIdToName[kvp.Value] = kvp.Key;

            // Group by load case
            var grouped = new Dictionary<int, List<SgPlateElementForceData>>();
            foreach (var f in ElementResult.Forces)
            {
                if (!grouped.ContainsKey(f.LoadCaseId))
                    grouped[f.LoadCaseId] = new List<SgPlateElementForceData>();
                grouped[f.LoadCaseId].Add(f);
            }

            var lcTree = new DataTree<string>();
            var plateTree = new DataTree<int>();
            var fxTree = new DataTree<double>();
            var fyTree = new DataTree<double>();
            var fxyTree = new DataTree<double>();
            var mxTree = new DataTree<double>();
            var myTree = new DataTree<double>();
            var mxyTree = new DataTree<double>();
            var vxzTree = new DataTree<double>();
            var vyzTree = new DataTree<double>();

            var branchIndex = 0;
            foreach (var kvp in grouped)
            {
                var path = new GH_Path(branchIndex);
                var lcName = lcIdToName.TryGetValue(kvp.Key, out var n) ? n : $"LC {kvp.Key}";

                foreach (var f in kvp.Value)
                {
                    lcTree.Add(lcName, path);
                    plateTree.Add(f.PlateId, path);
                    fxTree.Add(f.Fx, path);
                    fyTree.Add(f.Fy, path);
                    fxyTree.Add(f.Fxy, path);
                    mxTree.Add(f.Mx, path);
                    myTree.Add(f.My, path);
                    mxyTree.Add(f.Mxy, path);
                    vxzTree.Add(f.Vxz, path);
                    vyzTree.Add(f.Vyz, path);
                }
                branchIndex++;
            }

            da.SetDataTree(Parent._outLoadCases, lcTree);
            da.SetDataTree(Parent._outPlates, plateTree);
            da.SetDataTree(Parent._outFx, fxTree);
            da.SetDataTree(Parent._outFy, fyTree);
            da.SetDataTree(Parent._outFxy, fxyTree);
            da.SetDataTree(Parent._outMx, mxTree);
            da.SetDataTree(Parent._outMy, myTree);
            da.SetDataTree(Parent._outMxy, mxyTree);
            da.SetDataTree(Parent._outVxz, vxzTree);
            da.SetDataTree(Parent._outVyz, vyzTree);
        }

        private void SetNodalForceData(IGH_DataAccess da)
        {
            var lcIdToName = new Dictionary<int, string>();
            foreach (var kvp in Model.LoadCaseMap) lcIdToName[kvp.Value] = kvp.Key;
            foreach (var kvp in Model.CombinationLoadCaseMap) lcIdToName[kvp.Value] = kvp.Key;

            // Group by (loadCase, plate)
            var grouped = new Dictionary<(int lc, int plate), List<SgPlateNodalForceData>>();
            foreach (var f in NodalResult.Forces)
            {
                var key = (f.LoadCaseId, f.PlateId);
                if (!grouped.ContainsKey(key))
                    grouped[key] = new List<SgPlateNodalForceData>();
                grouped[key].Add(f);
            }

            var lcTree = new DataTree<string>();
            var plateTree = new DataTree<int>();
            var nodeTree = new DataTree<int>();
            var fxTree = new DataTree<double>();
            var fyTree = new DataTree<double>();
            var fzTree = new DataTree<double>();
            var mxTree = new DataTree<double>();
            var myTree = new DataTree<double>();
            var mzTree = new DataTree<double>();

            var branchIndex = 0;
            foreach (var kvp in grouped)
            {
                var path = new GH_Path(branchIndex);
                var lcName = lcIdToName.TryGetValue(kvp.Key.lc, out var n) ? n : $"LC {kvp.Key.lc}";

                foreach (var f in kvp.Value)
                {
                    lcTree.Add(lcName, path);
                    plateTree.Add(f.PlateId, path);
                    nodeTree.Add(f.NodeId, path);
                    fxTree.Add(f.Fx, path);
                    fyTree.Add(f.Fy, path);
                    fzTree.Add(f.Fz, path);
                    mxTree.Add(f.Mx, path);
                    myTree.Add(f.My, path);
                    mzTree.Add(f.Mz, path);
                }
                branchIndex++;
            }

            da.SetDataTree(Parent._outLoadCases, lcTree);
            da.SetDataTree(Parent._outPlates, plateTree);
            da.SetDataTree(Parent._outNodes, nodeTree);
            da.SetDataTree(Parent._outNFx, fxTree);
            da.SetDataTree(Parent._outNFy, fyTree);
            da.SetDataTree(Parent._outNFz, fzTree);
            da.SetDataTree(Parent._outNMx, mxTree);
            da.SetDataTree(Parent._outNMy, myTree);
            da.SetDataTree(Parent._outNMz, mzTree);
        }
    }
}



