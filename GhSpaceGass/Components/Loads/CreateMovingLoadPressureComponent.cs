using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;

namespace GhSpaceGass.Components.Loads;

public class CreateMovingLoadPressureComponent : GH_Component
{
    private int _inLength;
    private int _inLoadSpacing;
    private int _inName;
    private int _inPx;
    private int _inPy;
    private int _inPz;
    private int _inWidth;

    private int _outPressure;

    public CreateMovingLoadPressureComponent()
        : base("SG Moving Load Pressure", "sgMovLoadPress",
            "Create a SpaceGass moving load pressure — a rectangular pressure patch (Width × " +
            "Length) that SpaceGass drags along a travel path.",
            "SpaceGass", "5 | Loads")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.senary;
    protected override Bitmap Icon => Icons.IconFactory.MovingLoadPressure();
    public override Guid ComponentGuid => new("D7A3C5F2-8B6E-4D1A-9F5C-2E4B7A9D8C1F");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inName = pManager.AddTextParameter("Name", "N",
            "The pressure name (e.g., \"UDL 5kPa\").",
            GH_ParamAccess.item);
        _inWidth = pManager.AddNumberParameter("Width", "W",
            "Pressure patch width in the transverse direction (aligned with Wheel Y on vehicles). " +
            "Must be greater than zero.",
            GH_ParamAccess.item);
        _inLength = pManager.AddNumberParameter("Length", "L",
            "Pressure patch length in the along-vehicle direction (aligned with Wheel X on " +
            "vehicles). Must be greater than zero.",
            GH_ParamAccess.item);
        _inLoadSpacing = pManager.AddNumberParameter("Load Spacing", "LS",
            "Optional grid resolution used to discretise the pressure patch into a set of point " +
            "loads. Smaller values produce a finer approximation of the uniform pressure. Must be " +
            "greater than zero when provided.",
            GH_ParamAccess.item);
        _inPx = pManager.AddNumberParameter("Px", "Px",
            "Pressure component in the model's global X direction. Defaults to zero.",
            GH_ParamAccess.item, 0);
        _inPy = pManager.AddNumberParameter("Py", "Py",
            "Pressure component in the model's global Y direction (vertical in Y-vertical models " +
            "— check Job Info if unsure). Defaults to zero.",
            GH_ParamAccess.item, 0);
        _inPz = pManager.AddNumberParameter("Pz", "Pz",
            "Pressure component in the model's global Z direction (vertical in Z-vertical models " +
            "— check Job Info if unsure). Defaults to zero.",
            GH_ParamAccess.item, 0);

        pManager[_inLoadSpacing].Optional = true;
        pManager[_inPx].Optional = true;
        pManager[_inPy].Optional = true;
        pManager[_inPz].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outPressure = pManager.AddParameter(new Param_SgMovingLoadPressure(),
            "Moving Load Pressure", "MLP",
            "The SpaceGass moving load pressure.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        string name = null;
        double width = 0;
        double length = 0;
        double loadSpacing = 0;
        var loadSpacingProvided = false;
        double px = 0, py = 0, pz = 0;

        if (!da.GetData(_inName, ref name) || string.IsNullOrWhiteSpace(name))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Moving load pressure name cannot be empty.");
            return;
        }

        if (!da.GetData(_inWidth, ref width) || width <= 0)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Width must be greater than zero.");
            return;
        }

        if (!da.GetData(_inLength, ref length) || length <= 0)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Length must be greater than zero.");
            return;
        }

        loadSpacingProvided = da.GetData(_inLoadSpacing, ref loadSpacing);
        if (loadSpacingProvided && loadSpacing <= 0)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Load Spacing must be greater than zero when provided.");
            return;
        }

        da.GetData(_inPx, ref px);
        da.GetData(_inPy, ref py);
        da.GetData(_inPz, ref pz);

        if (px == 0 && py == 0 && pz == 0)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "All pressure components are zero — the pressure will have no effect on the analysis.");

        try
        {
            var pressure = new SgMovingLoadPressureData(
                name, width, length,
                loadSpacingProvided ? loadSpacing : (double?)null,
                px, py, pz);
            da.SetData(_outPressure, new GH_SgMovingLoadPressure(pressure));
        }
        catch (ArgumentException ex)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
        }
    }
}
