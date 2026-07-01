using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Helpers;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using SpaceGassApi.Models;

namespace GhSpaceGass.Components.Structure;

public class CreateMemberOffsetComponent : GH_Component
{
    private int _inAxes;
    private int _inXA;
    private int _inXB;
    private int _inYA;
    private int _inYB;
    private int _inZA;
    private int _inZB;

    private int _outOffset;

    public CreateMemberOffsetComponent()
        : base("SG Member Offset", "sgOffset",
            "Create a SpaceGass member offset (rigid offset at each end of a member). Defaults to local member axes.",
            "SpaceGass", "3 | Structure")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.secondary;
    protected override Bitmap Icon => Icons.IconFactory.MemberOffset();
    public override Guid ComponentGuid => new("A3E70C06-B8A3-4D22-9F1D-7E6A5C4B3D26");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inXA = pManager.AddNumberParameter("X Offset A", "XA",
            "X offset at end A (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inYA = pManager.AddNumberParameter("Y Offset A", "YA",
            "Y offset at end A (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inZA = pManager.AddNumberParameter("Z Offset A", "ZA",
            "Z offset at end A (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inXB = pManager.AddNumberParameter("X Offset B", "XB",
            "X offset at end B (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inYB = pManager.AddNumberParameter("Y Offset B", "YB",
            "Y offset at end B (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inZB = pManager.AddNumberParameter("Z Offset B", "ZB",
            "Z offset at end B (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inAxes = pManager.AddParameter(
            new Param_SgIntegerOption("Offset Axes", ValueListHelper.OffsetAxesOptions,
                defaultValue: 0, autoCreate: true),
            "Axes", "Ax",
            "Offset axis system (Local=0, Global=1). Default: Local.",
            GH_ParamAccess.item);

        pManager[_inXA].Optional = true;
        pManager[_inYA].Optional = true;
        pManager[_inZA].Optional = true;
        pManager[_inXB].Optional = true;
        pManager[_inYB].Optional = true;
        pManager[_inZB].Optional = true;
        pManager[_inAxes].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outOffset = pManager.AddParameter(new Param_SgMemberOffset(),
            "Offset", "Off",
            "The SpaceGass member offset.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        double xa = 0, ya = 0, za = 0;
        double xb = 0, yb = 0, zb = 0;
        int axes = 0;

        da.GetData(_inXA, ref xa);
        da.GetData(_inYA, ref ya);
        da.GetData(_inZA, ref za);
        da.GetData(_inXB, ref xb);
        da.GetData(_inYB, ref yb);
        da.GetData(_inZB, ref zb);
        da.GetData(_inAxes, ref axes);

        var axesType = axes == 0 ? AxesType.Local : AxesType.Global;

        var offset = new SgMemberOffsetData(xa, ya, za, xb, yb, zb, axesType);

        if (offset.IsZero)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "All offset components are zero — this offset will have no effect.");

        da.SetData(_outOffset, new GH_SgMemberOffset(offset));
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        document.ScheduleSolution(0, doc => ValueListHelper.AutoCreateOnPlacement(this, doc));
    }
}

