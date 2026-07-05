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

public class GetSectionsComponent : GH_AsyncComponent<GetSectionsComponent>
{
    private int _inModel;
    private int _outIds, _outNames, _outLibraries, _outSources;
    private int _outArea, _outIy, _outIz, _outJ, _outAy, _outAz;
    private int _outPrincipalAngle, _outMark;
    private int _outAreaFactor, _outIyFactor, _outIzFactor, _outTorsionFactor;
    private int _outTransposed, _outAngleType, _outStatus;

    public GetSectionsComponent()
        : base("SG Get Sections", "sgGetSections",
            "Query all section properties from the open SpaceGass job.",
            "SpaceGass", "2 | Properties")
    {
        BaseWorker = new GetSectionPropertiesWorker(this);
    }

    public override GH_Exposure Exposure => GH_Exposure.last;
    protected override Bitmap Icon => Icons.IconFactory.GetSectionProperties();
    public override Guid ComponentGuid => new("EFB84A07-DF40-4AB5-A060-69002A76B668");

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
            "SpaceGass section IDs.",
            GH_ParamAccess.list);
        _outNames = pManager.AddTextParameter("Names", "N",
            "Section names.",
            GH_ParamAccess.list);
        _outMark = pManager.AddTextParameter("Mark", "Mk",
            "Mark/label (empty string if none).",
            GH_ParamAccess.list);
        _outSources = pManager.AddTextParameter("Sources", "Src",
            "Property source: \"Library\" or \"User\".",
            GH_ParamAccess.list);
        _outLibraries = pManager.AddTextParameter("Libraries", "Lib",
            "Library names (empty string for custom sections).",
            GH_ParamAccess.list);
        _outArea = pManager.AddNumberParameter("Area", "A",
            "Cross-section area.",
            GH_ParamAccess.list);
        _outIy = pManager.AddNumberParameter("Iy", "Iy",
            "Second moment of area about Y.",
            GH_ParamAccess.list);
        _outIz = pManager.AddNumberParameter("Iz", "Iz",
            "Second moment of area about Z.",
            GH_ParamAccess.list);
        _outJ = pManager.AddNumberParameter("J", "J",
            "Torsion constant.",
            GH_ParamAccess.list);
        _outAy = pManager.AddNumberParameter("Ay", "Ay",
            "Shear area Y.",
            GH_ParamAccess.list);
        _outAz = pManager.AddNumberParameter("Az", "Az",
            "Shear area Z.",
            GH_ParamAccess.list);
        _outPrincipalAngle = pManager.AddNumberParameter("Principal Angle", "PA",
            "Principal axis angle (degrees).",
            GH_ParamAccess.list);
        _outAreaFactor = pManager.AddNumberParameter("Area Factor", "AF",
            "Area modification factor.",
            GH_ParamAccess.list);
        _outIyFactor = pManager.AddNumberParameter("Iy Factor", "IyF",
            "Iy modification factor.",
            GH_ParamAccess.list);
        _outIzFactor = pManager.AddNumberParameter("Iz Factor", "IzF",
            "Iz modification factor.",
            GH_ParamAccess.list);
        _outTorsionFactor = pManager.AddNumberParameter("Torsion Factor", "TF",
            "Torsion constant modification factor.",
            GH_ParamAccess.list);
        _outTransposed = pManager.AddBooleanParameter("Transposed", "Tr",
            "Whether the section is transposed.",
            GH_ParamAccess.list);
        _outAngleType = pManager.AddTextParameter("Angle Type", "AT",
            "Angle section type (Not Applicable, Single, Short-Short, Long-Long, Starred).",
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

    private sealed class GetSectionPropertiesWorker : WorkerInstance<GetSectionsComponent>
    {
        public GetSectionPropertiesWorker(
            GetSectionsComponent parent,
            string id = "baseWorker",
            CancellationToken cancellationToken = default)
            : base(parent, id, cancellationToken) { }

        private SgModelData Model { get; set; }
        private SgSectionPropertiesResult Result { get; set; }
        private string Status { get; set; } = string.Empty;

        public override WorkerInstance<GetSectionsComponent> Duplicate(
            string id, CancellationToken cancellationToken)
            => new GetSectionPropertiesWorker(Parent, id, cancellationToken);

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

                Result = await session.GetSectionPropertiesAsync(CancellationToken).ConfigureAwait(false);

                Status = $"{Result.Sections.Count} sections queried.";
                foreach (var w in Result.Warnings)
                {
                    Status += $"\nWarning: {w}";
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);
                }

                SetComponentMessage($"{Result.Sections.Count} sections");
                if (!CancellationToken.IsCancellationRequested) done();
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                var message = ModelAssembler.FormatApiError(ex, "querying sections");
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
                da.SetDataList(Parent._outIds, Result.Sections.ConvertAll(s => s.Id));
                da.SetDataList(Parent._outNames, Result.Sections.ConvertAll(s => s.Name));
                da.SetDataList(Parent._outLibraries, Result.Sections.ConvertAll(s => s.Library));
                da.SetDataList(Parent._outSources, Result.Sections.ConvertAll(s => s.Source));
                da.SetDataList(Parent._outArea, Result.Sections.ConvertAll(s => s.Area));
                da.SetDataList(Parent._outIy, Result.Sections.ConvertAll(s => s.Iy));
                da.SetDataList(Parent._outIz, Result.Sections.ConvertAll(s => s.Iz));
                da.SetDataList(Parent._outJ, Result.Sections.ConvertAll(s => s.J));
                da.SetDataList(Parent._outAy, Result.Sections.ConvertAll(s => s.Ay));
                da.SetDataList(Parent._outAz, Result.Sections.ConvertAll(s => s.Az));
                da.SetDataList(Parent._outPrincipalAngle, Result.Sections.ConvertAll(s => s.PrincipalAngle));
                da.SetDataList(Parent._outMark, Result.Sections.ConvertAll(s => s.Mark));
                da.SetDataList(Parent._outAreaFactor, Result.Sections.ConvertAll(s => s.AreaFactor));
                da.SetDataList(Parent._outIyFactor, Result.Sections.ConvertAll(s => s.IyFactor));
                da.SetDataList(Parent._outIzFactor, Result.Sections.ConvertAll(s => s.IzFactor));
                da.SetDataList(Parent._outTorsionFactor, Result.Sections.ConvertAll(s => s.TorsionFactor));
                da.SetDataList(Parent._outTransposed, Result.Sections.ConvertAll(s => s.Transposed));
                da.SetDataList(Parent._outAngleType, Result.Sections.ConvertAll(s => s.AngleType));
            }

            da.SetData(Parent._outStatus, Status);
        }
    }
}
