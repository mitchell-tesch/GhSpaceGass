using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Helpers;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using SpaceGassApi.Models;

namespace GhSpaceGass.Components.Loads;

public class CreateMovingLoadVehicleComponent : GH_Component
{
    private int _inForceUnit;
    private int _inLengthUnit;
    private int _inLibrary;
    private int _inMomentUnit;
    private int _inName;
    private int _inWheelFx;
    private int _inWheelFy;
    private int _inWheelFz;
    private int _inWheelMx;
    private int _inWheelMy;
    private int _inWheelMz;
    private int _inWheelX;
    private int _inWheelY;

    private int _outVehicle;

    public CreateMovingLoadVehicleComponent()
        : base("SG Moving Load Vehicle", "sgMovLoadVeh",
            "Create a SpaceGass moving load vehicle. Connect Library to look up a vehicle from a " +
            "SpaceGass vehicle library; leave Library empty and provide Wheel X / Wheel Y (plus " +
            "optional Wheel Fx…Mz) to define a user vehicle. Force / Length / Moment units apply " +
            "to user-defined vehicles only.",
            "SpaceGass", "5 | Loads")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.senary;
    protected override Bitmap Icon => Icons.IconFactory.MovingLoadVehicle();
    public override Guid ComponentGuid => new("B47F2E1D-6C8A-4F1E-A9D3-7B2E5F8C1A4D");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inLibrary = pManager.AddTextParameter("Library", "Lib",
            "Optional SpaceGass vehicle library key (e.g., \"Aust\"). When connected, the vehicle " +
            "is loaded from the library and the Wheel inputs and unit selectors are ignored.",
            GH_ParamAccess.item);
        _inName = pManager.AddTextParameter("Name", "N",
            "Vehicle name. In library mode this must match a vehicle in the named library. In user " +
            "mode this is the name the vehicle will be stored under.",
            GH_ParamAccess.item);
        _inWheelX = pManager.AddNumberParameter("Wheel X", "X",
            "Position along the vehicle at each wheel. Values are conventionally negative — the " +
            "vehicle's reference point sits at the leading edge and wheels trail behind. The list " +
            "length defines the number of wheels. Required in user mode.",
            GH_ParamAccess.list);
        _inWheelY = pManager.AddNumberParameter("Wheel Y", "Y",
            "Transverse position of each wheel across the vehicle width, perpendicular to Wheel X. " +
            "In Y-vertical SpaceGass models this axis corresponds to the model's global Z; in " +
            "Z-vertical models it corresponds to global Y. Required in user mode. Must match the " +
            "length of Wheel X.",
            GH_ParamAccess.list);
        _inWheelFx = pManager.AddNumberParameter("Wheel Fx", "Fx",
            "Per-wheel force in the model's global X direction. Optional; defaults to zero for " +
            "every wheel when omitted. Must match the length of Wheel X when provided.",
            GH_ParamAccess.list);
        _inWheelFy = pManager.AddNumberParameter("Wheel Fy", "Fy",
            "Per-wheel force in the model's global Y direction (this is vertical in Y-vertical " +
            "models — check Job Info if unsure). Optional; defaults to zero when omitted.",
            GH_ParamAccess.list);
        _inWheelFz = pManager.AddNumberParameter("Wheel Fz", "Fz",
            "Per-wheel force in the model's global Z direction (this is vertical in Z-vertical " +
            "models — check Job Info if unsure). For gravity-style wheel loads use the vertical " +
            "axis with a negative value. Optional; defaults to zero when omitted.",
            GH_ParamAccess.list);
        _inWheelMx = pManager.AddNumberParameter("Wheel Mx", "Mx",
            "Per-wheel moment about the model's global X axis. Optional.",
            GH_ParamAccess.list);
        _inWheelMy = pManager.AddNumberParameter("Wheel My", "My",
            "Per-wheel moment about the model's global Y axis. Optional.",
            GH_ParamAccess.list);
        _inWheelMz = pManager.AddNumberParameter("Wheel Mz", "Mz",
            "Per-wheel moment about the model's global Z axis. Optional.",
            GH_ParamAccess.list);
        _inForceUnit = pManager.AddParameter(
            new Param_SgIntegerOption("Force Unit", ValueListHelper.ForceUnitOptions, defaultValue: 2),
            "Force Unit", "FU",
            "Force unit for user-defined wheel loads. Defaults to kN.",
            GH_ParamAccess.item);
        _inLengthUnit = pManager.AddParameter(
            new Param_SgIntegerOption("Length Unit", ValueListHelper.LengthUnitOptions, defaultValue: 2),
            "Length Unit", "LU",
            "Length unit for wheel X / Y positions. Defaults to m.",
            GH_ParamAccess.item);
        _inMomentUnit = pManager.AddParameter(
            new Param_SgIntegerOption("Moment Unit", ValueListHelper.MomentUnitOptions, defaultValue: 4),
            "Moment Unit", "MU",
            "Moment unit for user-defined wheel moments. Defaults to kN·m.",
            GH_ParamAccess.item);

        pManager[_inLibrary].Optional = true;
        pManager[_inWheelX].Optional = true;
        pManager[_inWheelY].Optional = true;
        pManager[_inWheelFx].Optional = true;
        pManager[_inWheelFy].Optional = true;
        pManager[_inWheelFz].Optional = true;
        pManager[_inWheelMx].Optional = true;
        pManager[_inWheelMy].Optional = true;
        pManager[_inWheelMz].Optional = true;
        pManager[_inForceUnit].Optional = true;
        pManager[_inLengthUnit].Optional = true;
        pManager[_inMomentUnit].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outVehicle = pManager.AddParameter(new Param_SgMovingLoadVehicle(),
            "Moving Load Vehicle", "MLV",
            "The SpaceGass moving load vehicle.",
            GH_ParamAccess.item);
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        document.ScheduleSolution(0, doc => ValueListHelper.AutoCreateOnPlacement(this, doc));
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        string library = null;
        string name = null;

        if (!da.GetData(_inName, ref name) || string.IsNullOrWhiteSpace(name))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Moving load vehicle name cannot be empty.");
            return;
        }

        var libraryProvided = da.GetData(_inLibrary, ref library) && !string.IsNullOrWhiteSpace(library);

        var wheelX = new List<double>();
        var wheelY = new List<double>();
        var wheelFx = new List<double>();
        var wheelFy = new List<double>();
        var wheelFz = new List<double>();
        var wheelMx = new List<double>();
        var wheelMy = new List<double>();
        var wheelMz = new List<double>();
        da.GetDataList(_inWheelX, wheelX);
        da.GetDataList(_inWheelY, wheelY);
        da.GetDataList(_inWheelFx, wheelFx);
        da.GetDataList(_inWheelFy, wheelFy);
        da.GetDataList(_inWheelFz, wheelFz);
        da.GetDataList(_inWheelMx, wheelMx);
        da.GetDataList(_inWheelMy, wheelMy);
        da.GetDataList(_inWheelMz, wheelMz);

        var wheelListsProvided = wheelX.Count > 0 || wheelY.Count > 0 ||
                                 wheelFx.Count > 0 || wheelFy.Count > 0 ||
                                 wheelFz.Count > 0 || wheelMx.Count > 0 ||
                                 wheelMy.Count > 0 || wheelMz.Count > 0;

        if (libraryProvided)
        {
            if (wheelListsProvided)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Library is connected — the Wheel inputs and unit selectors are ignored " +
                    "because SpaceGass supplies the wheel layout for library vehicles.");

            try
            {
                var libraryVehicle = new SgMovingLoadVehicleData(library, name);
                da.SetData(_outVehicle, new GH_SgMovingLoadVehicle(libraryVehicle));
            }
            catch (ArgumentException ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
            return;
        }

        // User mode
        if (wheelX.Count == 0)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "A user-defined vehicle needs at least one wheel — connect Wheel X (and Wheel Y).");
            return;
        }

        if (wheelY.Count != wheelX.Count)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                $"Wheel Y count ({wheelY.Count}) must match Wheel X count ({wheelX.Count}).");
            return;
        }

        if (!CheckOptionalWheelList(wheelFx, wheelX.Count, "Wheel Fx")) return;
        if (!CheckOptionalWheelList(wheelFy, wheelX.Count, "Wheel Fy")) return;
        if (!CheckOptionalWheelList(wheelFz, wheelX.Count, "Wheel Fz")) return;
        if (!CheckOptionalWheelList(wheelMx, wheelX.Count, "Wheel Mx")) return;
        if (!CheckOptionalWheelList(wheelMy, wheelX.Count, "Wheel My")) return;
        if (!CheckOptionalWheelList(wheelMz, wheelX.Count, "Wheel Mz")) return;

        var forceUnitInt = 2;
        var lengthUnitInt = 2;
        var momentUnitInt = 4;
        da.GetData(_inForceUnit, ref forceUnitInt);
        da.GetData(_inLengthUnit, ref lengthUnitInt);
        da.GetData(_inMomentUnit, ref momentUnitInt);

        var wheels = new List<SgVehicleWheelLoadData>(wheelX.Count);
        var anyNonZero = false;
        for (var i = 0; i < wheelX.Count; i++)
        {
            var fx = i < wheelFx.Count ? wheelFx[i] : 0;
            var fy = i < wheelFy.Count ? wheelFy[i] : 0;
            var fz = i < wheelFz.Count ? wheelFz[i] : 0;
            var mx = i < wheelMx.Count ? wheelMx[i] : 0;
            var my = i < wheelMy.Count ? wheelMy[i] : 0;
            var mz = i < wheelMz.Count ? wheelMz[i] : 0;
            var wheel = new SgVehicleWheelLoadData(wheelX[i], wheelY[i], fx, fy, fz, mx, my, mz);
            if (!wheel.IsZero) anyNonZero = true;
            wheels.Add(wheel);
        }

        if (!anyNonZero)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "All wheel loads have zero force and moment — the vehicle will have no effect on " +
                "the analysis.");

        try
        {
            var vehicle = new SgMovingLoadVehicleData(
                name, wheels,
                (ForceUnit)forceUnitInt,
                (LengthUnit)lengthUnitInt,
                (MomentUnit)momentUnitInt);
            da.SetData(_outVehicle, new GH_SgMovingLoadVehicle(vehicle));
        }
        catch (ArgumentException ex)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
        }
    }

    private bool CheckOptionalWheelList(List<double> list, int expected, string name)
    {
        if (list.Count > 0 && list.Count != expected)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                $"{name} count ({list.Count}) must match Wheel X count ({expected}).");
            return false;
        }
        return true;
    }
}
