using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GhSpaceGass.Async;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Core.Services;
using GhSpaceGass.Types;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GhSpaceGass.Components.Loads;

public class GenerateMovingLoadsComponent : GH_AsyncComponent<GenerateMovingLoadsComponent>
{
    private int _inGenerate;
    private int _inLoadCategory;
    private int _inMembersToLoad;
    private int _inModel;
    private int _inPlatesToLoad;
    private int _inScenariosToApply;
    private int _inSettings;

    private int _outGeneratedGroups;
    private int _outGeneratedLoadCaseIds;
    private int _outStatus;
    private int _outSuccess;

    public GenerateMovingLoadsComponent()
        : base("SG Generate Moving Loads", "sgGenMovLoad",
            "Generate the moving-load load cases for the assembled model. Wire matching " +
            "DataTree branches of Members To Load, Plates To Load, and Scenarios To Apply — " +
            "each branch is one subgroup. For each subgroup this component sets which elements " +
            "receive loads, enables that subgroup's scenarios (and disables the others), and " +
            "runs SpaceGass Generate.\n" +
            "Tree matching: the component iterates the union of branch paths across the three " +
            "trees — a branch that only exists in one tree is still attempted (with the missing " +
            "trees treated as empty for that branch). Scenario Include flags are snapshotted " +
            "before the run and restored afterwards, but the SpaceGass job's Elements-To-Load " +
            "selection is left in whatever state the last branch set it to.",
            "SpaceGass", "5 | Loads")
    {
        BaseWorker = new GenerateWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.senary;
    protected override Bitmap Icon => Icons.IconFactory.GenerateMovingLoads();
    public override Guid ComponentGuid => new("6E9C2A4B-7D8F-4E1A-B5C3-8F1D2A5E9B4C");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The assembled SpaceGass model from Assemble Model.",
            GH_ParamAccess.item);
        _inGenerate = pManager.AddBooleanParameter("Generate?", "G?",
            "Set to true to trigger the moving-load generation.",
            GH_ParamAccess.item, false);
        _inMembersToLoad = pManager.AddLineParameter("Members To Load", "MTL",
            "DataTree of member lines that receive moving-load distribution — one branch per " +
            "subgroup. Matches the branch structure of Plates To Load and Scenarios To Apply.",
            GH_ParamAccess.tree);
        _inPlatesToLoad = pManager.AddParameter(new Param_SgPlate(),
            "Plates To Load", "PTL",
            "DataTree of plates that receive moving-load distribution — one branch per subgroup. " +
            "Matches the branch structure of Members To Load and Scenarios To Apply.",
            GH_ParamAccess.tree);
        _inScenariosToApply = pManager.AddParameter(new Param_SgMovingLoadScenario(),
            "Scenarios To Apply", "SA",
            "DataTree of moving load scenarios to enable — one branch per subgroup. Every " +
            "scenario that appears somewhere in this tree gets its Include flag toggled per " +
            "subgroup and restored at the end of the run.",
            GH_ParamAccess.tree);
        _inSettings = pManager.AddParameter(new Param_SgMovingLoadSettings(),
            "Settings", "Set",
            "Optional moving-load engine settings, applied once at the start of the run.",
            GH_ParamAccess.item);
        _inLoadCategory = pManager.AddParameter(new Param_SgLoadCategory(),
            "Load Category", "Cat",
            "Optional load category — the generated moving-load load cases are tagged with " +
            "this category. When omitted the generated cases have no category.",
            GH_ParamAccess.item);

        pManager[_inMembersToLoad].Optional = true;
        pManager[_inPlatesToLoad].Optional = true;
        pManager[_inScenariosToApply].Optional = true;
        pManager[_inSettings].Optional = true;
        pManager[_inLoadCategory].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outGeneratedLoadCaseIds = pManager.AddIntegerParameter("Generated Load Case IDs", "IDs",
            "The SpaceGass load-case IDs the moving-load engine produced, in a DataTree with " +
            "one branch per input subgroup.",
            GH_ParamAccess.tree);
        _outGeneratedGroups = pManager.AddTextParameter("Generated Groups", "Grp",
            "The group labels SpaceGass tagged the generated cases with, per subgroup.",
            GH_ParamAccess.tree);
        _outSuccess = pManager.AddBooleanParameter("Success?", "OK?",
            "True when every subgroup's Generate call succeeded.",
            GH_ParamAccess.item);
        _outStatus = pManager.AddTextParameter("Status", "S",
            "Multiline status: elapsed time, subgroup count, total generated cases, warnings.",
            GH_ParamAccess.item);
    }

    private sealed class GenerateWorker : WorkerInstance<GenerateMovingLoadsComponent>
    {
        public GenerateWorker(
            GenerateMovingLoadsComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken)
        {
        }

        private SgModelData InputModel { get; set; }
        private bool GenerateEnabled { get; set; }
        private Dictionary<string, IReadOnlyList<int>> MembersToLoad { get; set; } = new();
        private Dictionary<string, IReadOnlyList<int>> PlatesToLoad { get; set; } = new();
        private Dictionary<string, IReadOnlyList<string>> ScenariosToApply { get; set; } = new();
        private SgMovingLoadSettingsData SettingsData { get; set; }
        private string LoadCategoryName { get; set; }
        private List<string> ResolutionWarnings { get; } = new();

        private SgMovingLoadGenerationResult Result { get; set; }
        private bool Success { get; set; }
        private string Status { get; set; } = string.Empty;

        public override WorkerInstance<GenerateMovingLoadsComponent> Duplicate(
            string id, CancellationToken cancellationToken)
        {
            return new GenerateWorker(Parent, id, cancellationToken);
        }

        public override void GetData(IGH_DataAccess da, GH_ComponentParamServer paramServer)
        {
            ResolutionWarnings.Clear();

            var modelGoo = new GH_SgModel();
            if (!da.GetData(Parent._inModel, ref modelGoo) || modelGoo?.Value == null)
            {
                Status = "No model provided.";
                return;
            }
            InputModel = modelGoo.Value;

            var run = false;
            da.GetData(Parent._inGenerate, ref run);
            GenerateEnabled = run;

            // Members To Load — DataTree of Line
            da.GetDataTree<GH_Line>(Parent._inMembersToLoad, out var membersTree);
            MembersToLoad = new Dictionary<string, IReadOnlyList<int>>();
            if (membersTree != null)
            {
                foreach (var path in membersTree.Paths)
                {
                    var pathKey = path.ToString();
                    var list = new List<int>();
                    foreach (var lineGoo in membersTree.get_Branch(path))
                    {
                        if (lineGoo is GH_Line gl)
                        {
                            var line = gl.Value;
                            var start = new SgPoint3D(line.From.X, line.From.Y, line.From.Z);
                            var end = new SgPoint3D(line.To.X, line.To.Y, line.To.Z);
                            var found = false;
                            foreach (var (id, seg) in InputModel.MemberMap)
                            {
                                if ((seg.Start.Equals(start) && seg.End.Equals(end)) ||
                                    (seg.Start.Equals(end) && seg.End.Equals(start)))
                                {
                                    list.Add(id);
                                    found = true;
                                    break;
                                }
                            }
                            if (!found)
                                ResolutionWarnings.Add(
                                    $"Members To Load branch {pathKey}: line " +
                                    $"({line.From.X:F3},{line.From.Y:F3},{line.From.Z:F3}) → " +
                                    $"({line.To.X:F3},{line.To.Y:F3},{line.To.Z:F3}) did not match " +
                                    "any member in the model — skipping.");
                        }
                    }
                    MembersToLoad[pathKey] = list;
                }
            }

            // Plates To Load — DataTree of Plate Goo
            da.GetDataTree<IGH_Goo>(Parent._inPlatesToLoad, out var platesTree);
            PlatesToLoad = new Dictionary<string, IReadOnlyList<int>>();
            if (platesTree != null)
            {
                foreach (var path in platesTree.Paths)
                {
                    var pathKey = path.ToString();
                    var list = new List<int>();
                    foreach (var goo in platesTree.get_Branch(path))
                    {
                        if (goo is GH_SgPlate pg && pg.Value != null)
                        {
                            var found = false;
                            var wanted = pg.Value.Nodes;
                            foreach (var (id, corners) in InputModel.PlateMap)
                            {
                                if (corners.Length != wanted.Length) continue;
                                var allMatch = true;
                                for (var i = 0; i < corners.Length; i++)
                                    if (!corners[i].Equals(wanted[i])) { allMatch = false; break; }
                                if (allMatch) { list.Add(id); found = true; break; }
                            }
                            if (!found)
                                ResolutionWarnings.Add(
                                    $"Plates To Load branch {pathKey}: plate did not match any " +
                                    "plate in the model — skipping.");
                        }
                    }
                    PlatesToLoad[pathKey] = list;
                }
            }

            // Scenarios To Apply — DataTree of Scenario Goo, mapped to scenario names
            da.GetDataTree<GH_SgMovingLoadScenario>(Parent._inScenariosToApply, out var scenariosTree);
            ScenariosToApply = new Dictionary<string, IReadOnlyList<string>>();
            if (scenariosTree != null)
            {
                foreach (var path in scenariosTree.Paths)
                {
                    var pathKey = path.ToString();
                    var names = new List<string>();
                    foreach (var goo in scenariosTree.get_Branch(path))
                    {
                        if (goo is GH_SgMovingLoadScenario sg && sg.Value != null)
                            names.Add(sg.Value.Name);
                    }
                    ScenariosToApply[pathKey] = names;
                }
            }

            GH_SgMovingLoadSettings settingsGoo = null;
            da.GetData(Parent._inSettings, ref settingsGoo);
            SettingsData = settingsGoo?.Value;

            GH_SgLoadCategory categoryGoo = null;
            da.GetData(Parent._inLoadCategory, ref categoryGoo);
            LoadCategoryName = categoryGoo?.Value?.Name;
        }

        public override async Task DoWork(Action<string, double> reportProgress, Action done)
        {
            if (!GenerateEnabled)
            {
                Success = false;
                Status = "Generate not triggered. Set Generate? to true.";
                SetComponentMessage("Idle");
                if (!CancellationToken.IsCancellationRequested) done();
                return;
            }

            if (InputModel == null)
            {
                Success = false;
                Status = "No model provided.";
                SetComponentMessage("No model");
                if (!CancellationToken.IsCancellationRequested) done();
                return;
            }

            try
            {
                SetComponentMessage("Generating...");
                await GenerateAsync();
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                // Cancelled
            }
            catch (Exception ex)
            {
                Success = false;
                Result = null;
                var message = ModelAssembler.FormatApiError(ex, "generating moving loads");
                Status = $"Error: {message}";
                SetComponentMessage("Error");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        private async Task GenerateAsync()
        {
            var session = SpaceGassSessionManager.Current;
            if (session == null || !session.IsConnected)
            {
                Success = false;
                Status = "Not connected. Place a SpaceGass Connect component and set Connect? to true.";
                SetComponentMessage("Not connected");
                return;
            }

            Result = await session.GenerateMovingLoadsAsync(
                    InputModel, MembersToLoad, PlatesToLoad, ScenariosToApply,
                    SettingsData, LoadCategoryName, CancellationToken)
                .ConfigureAwait(false);

            Success = Result.Success;

            var branchCount = Result.Branches.Count;
            var totalCases = Result.Branches.Values.Sum(b => b.LoadCaseIds.Count);
            var statusParts = new List<string>
            {
                $"Generated {totalCases} moving-load load case(s) across {branchCount} subgroup(s) " +
                $"in {Result.ElapsedTime}."
            };

            foreach (var w in ResolutionWarnings)
            {
                statusParts.Add($"Warning: {w}");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
            }
            foreach (var w in Result.Warnings)
            {
                statusParts.Add($"Warning: {w}");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
            }

            Status = string.Join("\n", statusParts);
            SetComponentMessage($"Generated ({totalCases} cases)");
        }

        public override void SetData(IGH_DataAccess da)
        {
            var idsTree = new GH_Structure<GH_Integer>();
            var groupsTree = new GH_Structure<GH_String>();

            if (Result != null)
            {
                var branchIndex = 0;
                foreach (var kvp in Result.Branches)
                {
                    var path = new GH_Path(branchIndex++);
                    idsTree.AppendRange(kvp.Value.LoadCaseIds.Select(i => new GH_Integer(i)), path);
                    groupsTree.AppendRange(kvp.Value.Groups.Select(g => new GH_String(g)), path);
                }
            }

            da.SetDataTree(Parent._outGeneratedLoadCaseIds, idsTree);
            da.SetDataTree(Parent._outGeneratedGroups, groupsTree);
            da.SetData(Parent._outSuccess, Success);
            da.SetData(Parent._outStatus, Status);
        }
    }
}
