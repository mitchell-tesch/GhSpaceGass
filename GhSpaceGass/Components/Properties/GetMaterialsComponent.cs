using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GhSpaceGass.Async;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using GhSpaceGass.Core.Services;

namespace GhSpaceGass.Components.Properties;

public class GetMaterialsComponent : GH_AsyncComponent<GetMaterialsComponent>
{
    private int _inModel;
    private int _outIds, _outNames, _outLibraries, _outSources;
    private int _outE, _outPoissons, _outDensity, _outThermal, _outConcrete;
    private int _outStatus;

    public GetMaterialsComponent()
        : base("SG Get Materials", "sgGetMaterials",
            "Query all material properties from the open SpaceGass job.",
            "SpaceGass", "2 | Properties")
    {
        BaseWorker = new GetMaterialPropertiesWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.last;
    protected override Bitmap Icon => Icons.IconFactory.GetMaterialProperties();
    public override Guid ComponentGuid => new("2BD9BCEE-1239-4E95-B516-6818CF2FD8A3");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inModel = pManager.AddParameter(new Param_SgModel(),
            "Model", "M",
            "The SpaceGass model (from Assemble or Disassemble).",
            GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outIds = pManager.AddIntegerParameter("IDs", "Id",
            "SpaceGass material IDs.",
            GH_ParamAccess.list);
        _outNames = pManager.AddTextParameter("Names", "N",
            "Material names.",
            GH_ParamAccess.list);
        _outSources = pManager.AddTextParameter("Sources", "Src",
            "Property source: \"Library\" or \"User\".",
            GH_ParamAccess.list);
        _outLibraries = pManager.AddTextParameter("Libraries", "Lib",
            "Library names (empty string for custom materials).",
            GH_ParamAccess.list);
        _outE = pManager.AddNumberParameter("Youngs Modulus", "E",
            "Young's modulus (E).",
            GH_ParamAccess.list);
        _outPoissons = pManager.AddNumberParameter("Poissons Ratio", "PR",
            "Poisson's ratio.",
            GH_ParamAccess.list);
        _outDensity = pManager.AddNumberParameter("Density", "D",
            "Mass density.",
            GH_ParamAccess.list);
        _outThermal = pManager.AddNumberParameter("Thermal Coefficient", "TC",
            "Thermal expansion coefficient.",
            GH_ParamAccess.list);
        _outConcrete = pManager.AddNumberParameter("Concrete Strength", "fc",
            "Concrete characteristic strength (0 if not applicable).",
            GH_ParamAccess.list);
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

    private sealed class GetMaterialPropertiesWorker : WorkerInstance<GetMaterialsComponent>
    {
        public GetMaterialPropertiesWorker(
            GetMaterialsComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken) { }

        private SgModelData Model { get; set; }
        private SgMaterialPropertiesResult Result { get; set; }
        private string Status { get; set; } = string.Empty;

        public override WorkerInstance<GetMaterialsComponent> Duplicate(
            string id, CancellationToken cancellationToken)
            => new GetMaterialPropertiesWorker(Parent, id, cancellationToken);

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

                Result = await session.GetMaterialPropertiesAsync(CancellationToken).ConfigureAwait(false);

                Status = $"{Result.Materials.Count} materials queried.";
                foreach (var w in Result.Warnings)
                {
                    Status += $"\nWarning: {w}";
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
                }

                SetComponentMessage($"{Result.Materials.Count} materials");
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                var message = ModelAssembler.FormatApiError(ex, "querying materials");
                Status = $"Error: {message}";
                SetComponentMessage("Error");
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
                if (!CancellationToken.IsCancellationRequested) done();
            }
        }

        public override void SetData(IGH_DataAccess da)
        {
            if (Result != null)
            {
                da.SetDataList(Parent._outIds, Result.Materials.ConvertAll(m => m.Id));
                da.SetDataList(Parent._outNames, Result.Materials.ConvertAll(m => m.Name));
                da.SetDataList(Parent._outLibraries, Result.Materials.ConvertAll(m => m.Library));
                da.SetDataList(Parent._outSources, Result.Materials.ConvertAll(m => m.Source));
                da.SetDataList(Parent._outE, Result.Materials.ConvertAll(m => m.YoungsModulus));
                da.SetDataList(Parent._outPoissons, Result.Materials.ConvertAll(m => m.PoissonsRatio));
                da.SetDataList(Parent._outDensity, Result.Materials.ConvertAll(m => m.Density));
                da.SetDataList(Parent._outThermal, Result.Materials.ConvertAll(m => m.ThermalCoefficient));
                da.SetDataList(Parent._outConcrete, Result.Materials.ConvertAll(m => m.ConcreteStrength));
            }

            da.SetData(Parent._outStatus, Status);
        }
    }
}
