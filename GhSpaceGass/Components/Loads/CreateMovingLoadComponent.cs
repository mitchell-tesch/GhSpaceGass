using System;
using System.Collections.Generic;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Helpers;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using SpaceGassApi.Models;

namespace GhSpaceGass.Components.Loads;

public class CreateMovingLoadComponent : GH_Component
{
    private int _inDelay;
    private int _inDynamicFactor;
    private int _inGenerateStationaryLc;
    private int _inLaneFactor;
    private int _inLoadFactor;
    private int _inPressure;
    private int _inSpeed;
    private int _inStartPosition;
    private int _inTravelPath;
    private int _inVehicle;

    private int _outMovingLoad;

    public CreateMovingLoadComponent()
        : base("SG Moving Load", "sgMovLoad",
            "Create one moving load entry for a moving load scenario — a single vehicle or " +
            "pressure travelling along one travel path, with optional speed, start position, " +
            "delay and factor overrides. Wire one or more of these into the Loads input on a " +
            "Moving Load Scenario.",
            "SpaceGass", "5 | Loads")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.senary;
    protected override Bitmap Icon => Icons.IconFactory.MovingLoad();
    public override Guid ComponentGuid => new("A9F2D5C7-1E4B-4A83-9C6F-8D2E5B7A1F3C");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inVehicle = pManager.AddParameter(new Param_SgMovingLoadVehicle(),
            "Vehicle", "V",
            "The vehicle running along the travel path. Connect either Vehicle or Pressure, " +
            "not both.",
            GH_ParamAccess.item);
        _inPressure = pManager.AddParameter(new Param_SgMovingLoadPressure(),
            "Pressure", "PR",
            "The pressure patch running along the travel path. Connect either Vehicle or " +
            "Pressure, not both.",
            GH_ParamAccess.item);
        _inTravelPath = pManager.AddParameter(new Param_SgMovingLoadTravelPath(),
            "Travel Path", "P",
            "The travel path the vehicle or pressure moves along. Required.",
            GH_ParamAccess.item);
        _inSpeed = pManager.AddNumberParameter("Speed", "S",
            "Travel speed along the path in metres per second (optional). Set to 0 for a " +
            "stationary load — Start Position and Delay still apply, and Generate Stationary " +
            "Load Case controls where the stationary load lands.",
            GH_ParamAccess.item);
        _inStartPosition = pManager.AddNumberParameter("Start Position", "SP",
            "Distance from the first station along the travel path to the vehicle or pressure " +
            "at the start of the moving-load run, in metres. Must be zero or positive.",
            GH_ParamAccess.item);
        _inDelay = pManager.AddNumberParameter("Delay", "D",
            "Time delay before this load enters the analysis, in seconds. Only meaningful when " +
            "the parent scenario has a Time Interval set.",
            GH_ParamAccess.item);
        _inLoadFactor = pManager.AddNumberParameter("Load Factor", "LF",
            "Scale applied to the load magnitude (optional).",
            GH_ParamAccess.item);
        _inLaneFactor = pManager.AddNumberParameter("Lane Factor", "LnF",
            "Reduction factor for multi-lane loading rules (optional).",
            GH_ParamAccess.item);
        _inDynamicFactor = pManager.AddNumberParameter("Dynamic Factor", "DF",
            "Dynamic amplification factor (optional).",
            GH_ParamAccess.item);
        _inGenerateStationaryLc = pManager.AddParameter(
            new Param_SgIntegerOption("Generate Stationary Load Case",
                ValueListHelper.GenerateStationaryLcOptions, defaultValue: -1),
            "Generate Stationary Load Case", "GSL",
            "For a stationary moving load (one with Speed = 0), controls where the stationary " +
            "load lands in the sequence of generated load cases. Starting Load Case Only: the " +
            "stationary load goes into the scenario's Starting Load Case and the moving-load " +
            "cases begin in the next case. All Load Cases: the stationary load is combined into " +
            "every generated case, starting from the Starting Load Case. Ignored when Speed is " +
            "non-zero.",
            GH_ParamAccess.item);

        pManager[_inVehicle].Optional = true;
        pManager[_inPressure].Optional = true;
        pManager[_inSpeed].Optional = true;
        pManager[_inStartPosition].Optional = true;
        pManager[_inDelay].Optional = true;
        pManager[_inLoadFactor].Optional = true;
        pManager[_inLaneFactor].Optional = true;
        pManager[_inDynamicFactor].Optional = true;
        pManager[_inGenerateStationaryLc].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outMovingLoad = pManager.AddParameter(new Param_SgMovingLoad(),
            "Moving Load", "ML",
            "The SpaceGass moving load — wire into a Moving Load Scenario's Loads input.",
            GH_ParamAccess.item);
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        document.ScheduleSolution(0, doc => ValueListHelper.AutoCreateOnPlacement(this, doc));
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        GH_SgMovingLoadVehicle vehicleGoo = null;
        GH_SgMovingLoadPressure pressureGoo = null;
        GH_SgMovingLoadTravelPath pathGoo = null;
        double speed = 0, startPosition = 0, delay = 0;
        double loadFactor = 0, laneFactor = 0, dynamicFactor = 0;
        var speedProvided = false;
        var startPositionProvided = false;
        var delayProvided = false;
        var loadFactorProvided = false;
        var laneFactorProvided = false;
        var dynamicFactorProvided = false;
        var genStatInt = -1;

        var vehicleProvided = da.GetData(_inVehicle, ref vehicleGoo) && vehicleGoo?.Value != null;
        var pressureProvided = da.GetData(_inPressure, ref pressureGoo) && pressureGoo?.Value != null;

        if (vehicleProvided && pressureProvided)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Connect either a Vehicle or a Pressure, not both.");
            return;
        }

        if (!vehicleProvided && !pressureProvided)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Connect a Vehicle or a Pressure.");
            return;
        }

        if (!da.GetData(_inTravelPath, ref pathGoo) || pathGoo?.Value == null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "A Travel Path is required.");
            return;
        }

        speedProvided = da.GetData(_inSpeed, ref speed);
        startPositionProvided = da.GetData(_inStartPosition, ref startPosition);
        delayProvided = da.GetData(_inDelay, ref delay);
        loadFactorProvided = da.GetData(_inLoadFactor, ref loadFactor);
        laneFactorProvided = da.GetData(_inLaneFactor, ref laneFactor);
        dynamicFactorProvided = da.GetData(_inDynamicFactor, ref dynamicFactor);
        da.GetData(_inGenerateStationaryLc, ref genStatInt);

        MovingLoadStationaryOption? generateStationaryLc = genStatInt switch
        {
            0 => MovingLoadStationaryOption.StartingLoadCase,
            1 => MovingLoadStationaryOption.AllLoadCases,
            _ => null
        };

        try
        {
            var movingLoad = new SgMovingLoadData(
                travelPath: pathGoo.Value,
                vehicle: vehicleProvided ? vehicleGoo!.Value : null,
                pressure: pressureProvided ? pressureGoo!.Value : null,
                speed: speedProvided ? speed : null,
                startPosition: startPositionProvided ? startPosition : null,
                delay: delayProvided ? delay : null,
                loadFactor: loadFactorProvided ? loadFactor : null,
                laneFactor: laneFactorProvided ? laneFactor : null,
                dynamicFactor: dynamicFactorProvided ? dynamicFactor : null,
                generateStationaryLc: generateStationaryLc);
            da.SetData(_outMovingLoad, new GH_SgMovingLoad(movingLoad));
        }
        catch (ArgumentException ex)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
        }
    }
}
