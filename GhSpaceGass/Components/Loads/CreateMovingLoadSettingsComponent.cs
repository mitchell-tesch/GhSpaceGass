using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;

namespace GhSpaceGass.Components.Loads;

public class CreateMovingLoadSettingsComponent : GH_Component
{
    private int _inApplyToClosestMember;
    private int _inCheckVerticalProximity;
    private int _inIgnoreLoadsOnOneMember;
    private int _inIgnoreOutsideLoadedArea;
    private int _inKeepLoadsWithinTravelPath;
    private int _inRetainLoads;
    private int _inVerticalProximity;

    private int _outSettings;

    public CreateMovingLoadSettingsComponent()
        : base("SG Moving Load Settings", "sgMovLoadSet",
            "Configure the job-level SpaceGass moving-load engine settings. Wire the output " +
            "into Assemble Model's Moving Load Settings input. Any input left disconnected " +
            "keeps its current SpaceGass value — only the fields you connect are updated.",
            "SpaceGass", "5 | Loads")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.senary;
    protected override Bitmap Icon => Icons.IconFactory.MovingLoadSettings();
    public override Guid ComponentGuid => new("2C4F8E1D-9A6B-4E3F-B5D2-7A8C1E5B4F9D");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inApplyToClosestMember = pManager.AddBooleanParameter(
            "Apply To Closest Member", "ACM",
            "When true, each wheel load is applied only to the single closest surrounding " +
            "member (instead of being distributed across the nearest members in proportion to " +
            "distance). Reduces the number of generated loads while keeping accuracy acceptable " +
            "for most cases.",
            GH_ParamAccess.item);
        _inCheckVerticalProximity = pManager.AddBooleanParameter(
            "Check Vertical Proximity", "CVP",
            "When true, wheel loads are only distributed to members / plates whose vertical " +
            "distance from the load is within the Vertical Proximity limit. Prevents loads on " +
            "an upper bridge deck being incorrectly applied to a lower deck below it.",
            GH_ParamAccess.item);
        _inVerticalProximity = pManager.AddNumberParameter(
            "Vertical Proximity", "VP",
            "Maximum vertical distance (in metres) between a load and a receiving member or " +
            "plate. Only used when Check Vertical Proximity is true.",
            GH_ParamAccess.item);
        _inIgnoreLoadsOnOneMember = pManager.AddBooleanParameter(
            "Ignore Loads On One Member", "ILOM",
            "When true, wheels or pressure parts that would transfer their load to only one " +
            "member are ignored — useful for deactivating wheels that have moved off the side " +
            "of a deck or off the end of a skew bridge, even while the vehicle is still within " +
            "the travel path extents.",
            GH_ParamAccess.item);
        _inIgnoreOutsideLoadedArea = pManager.AddBooleanParameter(
            "Ignore Outside Loaded Area", "IOA",
            "When true, wheels or pressure parts that fall outside the SpaceGass loading-area " +
            "polygon are treated as inactive. The loading-area polygon itself is defined " +
            "graphically inside SpaceGass — this plug-in does not push a loading area in the " +
            "current release. If no loading-area polygon has been defined in SpaceGass this " +
            "flag has no effect.",
            GH_ParamAccess.item);
        _inKeepLoadsWithinTravelPath = pManager.AddBooleanParameter(
            "Keep Loads Within Travel Path", "KLW",
            "When true, each load starts with its rear at the start of the travel path and " +
            "finishes when its front reaches the end — useful for overhead cranes on a fixed " +
            "rail. When false, loads approach the start of the path, move along it and " +
            "disappear off the end — useful for road traffic on a bridge or material on a " +
            "conveyor.",
            GH_ParamAccess.item);
        _inRetainLoads = pManager.AddBooleanParameter(
            "Retain Loads", "RL",
            "When true, previously-generated loads for scenarios that are deselected in the " +
            "SpaceGass scenario tree are kept. When false, loads for deselected scenarios are " +
            "cleared on the next generate run.",
            GH_ParamAccess.item);

        pManager[_inApplyToClosestMember].Optional = true;
        pManager[_inCheckVerticalProximity].Optional = true;
        pManager[_inVerticalProximity].Optional = true;
        pManager[_inIgnoreLoadsOnOneMember].Optional = true;
        pManager[_inIgnoreOutsideLoadedArea].Optional = true;
        pManager[_inKeepLoadsWithinTravelPath].Optional = true;
        pManager[_inRetainLoads].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outSettings = pManager.AddParameter(new Param_SgMovingLoadSettings(),
            "Moving Load Settings", "MLSet",
            "The SpaceGass moving-load engine settings — wire into Assemble Model.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        bool apply = false, cvp = false, ignoreOne = false, ignoreOutside = false,
             keepWithin = false, retain = false;
        double vp = 0;
        var applyProvided = da.GetData(_inApplyToClosestMember, ref apply);
        var cvpProvided = da.GetData(_inCheckVerticalProximity, ref cvp);
        var vpProvided = da.GetData(_inVerticalProximity, ref vp);
        var ignoreOneProvided = da.GetData(_inIgnoreLoadsOnOneMember, ref ignoreOne);
        var ignoreOutsideProvided = da.GetData(_inIgnoreOutsideLoadedArea, ref ignoreOutside);
        var keepWithinProvided = da.GetData(_inKeepLoadsWithinTravelPath, ref keepWithin);
        var retainProvided = da.GetData(_inRetainLoads, ref retain);

        var settings = new SgMovingLoadSettingsData(
            applyToClosestMember: applyProvided ? apply : null,
            checkVerticalProximity: cvpProvided ? cvp : null,
            verticalProximity: vpProvided ? vp : null,
            ignoreLoadsOnOneMember: ignoreOneProvided ? ignoreOne : null,
            ignoreOutsideLoadedArea: ignoreOutsideProvided ? ignoreOutside : null,
            keepLoadsWithinTravelPath: keepWithinProvided ? keepWithin : null,
            retainLoads: retainProvided ? retain : null);

        if (!settings.HasAnyValue)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "No fields are configured on this component — Assemble Model will skip the " +
                "settings update.");

        da.SetData(_outSettings, new GH_SgMovingLoadSettings(settings));
    }
}
