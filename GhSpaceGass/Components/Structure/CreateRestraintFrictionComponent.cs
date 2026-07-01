using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Helpers;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using SpaceGassApi.Models;

namespace GhSpaceGass.Components.Structure;

public class CreateRestraintFrictionComponent : GH_Component
{
    private int _inFxAxis, _inFyAxis, _inFzAxis;
    private int _inFxDir, _inFyDir, _inFzDir;
    private int _inFxFactor, _inFyFactor, _inFzFactor;
    
    private int _outFriction;

    public CreateRestraintFrictionComponent()
        : base("SG Restraint Friction", "sgRstFr",
            "Create friction parameters for a SpaceGass restraint. DOFs with friction become friction supports (code 'N').",
            "SpaceGass", "3 | Structure")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.tertiary;
    protected override Bitmap Icon => Icons.IconFactory.RestraintFriction();
    public override Guid ComponentGuid => new("D6037A7E-CD52-425A-A122-19021B11D9A7");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inFxFactor = pManager.AddNumberParameter("Factor X", "Fx",
            "Friction factor for X-translation.",
            GH_ParamAccess.item);
        _inFxAxis = pManager.AddParameter(
            new Param_SgIntegerOption("Normal Axis X", ValueListHelper.FrictionNormalAxisOptions, defaultValue: 0),
            "Normal Axis X", "NAx",
            "Normal axis for X friction (None=0, X-Axis=1, Y-Axis=2, Z-Axis=3) Default: 0.",
            GH_ParamAccess.item);
        _inFxDir = pManager.AddParameter(
            new Param_SgIntegerOption("Normal Direction X", ValueListHelper.FrictionNormalDirectionOptions, defaultValue: 0),
            "Normal Direction X", "NDx",
            "Normal direction for X friction (Either=0, Positive Only=1, Negative Only=2) Default: 0.",
            GH_ParamAccess.item);

        _inFyFactor = pManager.AddNumberParameter("Factor Y", "Fy",
            "Friction factor for Y-translation.",
            GH_ParamAccess.item);
        _inFyAxis = pManager.AddParameter(
            new Param_SgIntegerOption("Normal Axis Y", ValueListHelper.FrictionNormalAxisOptions, defaultValue: 0),
            "Normal Axis Y", "NAy",
            "Normal axis for Y friction (None=0, X-Axis=1, Y-Axis=2, Z-Axis=3) Default: 0.",
            GH_ParamAccess.item);
        _inFyDir = pManager.AddParameter(
            new Param_SgIntegerOption("Normal Direction Y", ValueListHelper.FrictionNormalDirectionOptions, defaultValue: 0),
            "Normal Direction Y", "NDy",
            "Normal direction for Y friction (Either=0, Positive Only=1, Negative Only=2) Default: 0.",
            GH_ParamAccess.item);

        _inFzFactor = pManager.AddNumberParameter("Factor Z", "Fz",
            "Friction factor for Z-translation.",
            GH_ParamAccess.item);
        _inFzAxis = pManager.AddParameter(
            new Param_SgIntegerOption("Normal Axis Z", ValueListHelper.FrictionNormalAxisOptions, defaultValue: 0),
            "Normal Axis Z", "NAz",
            "Normal axis for Z friction (None=0, X-Axis=1, Y-Axis=2, Z-Axis=3) Default: 0.",
            GH_ParamAccess.item);
        _inFzDir = pManager.AddParameter(
            new Param_SgIntegerOption("Normal Direction Z", ValueListHelper.FrictionNormalDirectionOptions, defaultValue: 0),
            "Normal Direction Z", "NDz",
            "Normal direction for Z friction (Either=0, Positive Only=1, Negative Only=2) Default: 0.",
            GH_ParamAccess.item);

        // All optional — user defines which axes have friction
        for (var i = 0; i < pManager.ParamCount; i++)
            pManager[i].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outFriction = pManager.AddParameter(new Param_SgRestraintFriction(),
            "Friction", "Fr",
            "Restraint friction parameters.",
            GH_ParamAccess.item);
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        document.ScheduleSolution(0, doc => ValueListHelper.AutoCreateOnPlacement(this, doc));
    }


    protected override void SolveInstance(IGH_DataAccess da)
    {
        var x = ReadFrictionAxis(da, _inFxFactor, _inFxAxis, _inFxDir);
        var y = ReadFrictionAxis(da, _inFyFactor, _inFyAxis, _inFyDir);
        var z = ReadFrictionAxis(da, _inFzFactor, _inFzAxis, _inFzDir);

        var friction = new SgRestraintFrictionData(x, y, z);

        if (!friction.HasAnyFriction)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "No friction factors provided — at least one axis is needed for a friction support.");

        da.SetData(_outFriction, new GH_SgRestraintFriction(friction));
    }

    private SgFrictionAxisData ReadFrictionAxis(IGH_DataAccess da, int factorIdx, int axisIdx, int dirIdx)
    {
        double factor = 0;
        if (!da.GetData(factorIdx, ref factor)) return null;

        int axisInt = 0, dirInt = 0;
        da.GetData(axisIdx, ref axisInt);
        da.GetData(dirIdx, ref dirInt);

        return new SgFrictionAxisData(factor,
            (FrictionNormalAxis)axisInt,
            (FrictionNormalDirection)dirInt);
    }
}