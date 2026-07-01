#pragma warning disable CS8632 // Nullable annotation in non-nullable context
using System;
using System.Collections.Generic;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Helpers;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Rhino.Geometry;
using SpaceGassApi.Models;

namespace GhSpaceGass.Components.Structure;

public class CreateMemberComponent : GH_Component
{
    private int _inCurve;
    private int _inDirAngle;
    private int _inDirAxis;
    private int _inDirNode;
    private int _inMaterial;
    private int _inOffset;
    private int _inReleaseA;
    private int _inReleaseB;
    private int _inSection;
    private int _inType;
    
    private int _outMember;

    public CreateMemberComponent()
        : base("SG Member", "sgMember",
            "Create SpaceGass structural members from a line or polyline, section, and material.",
            "SpaceGass", "3 | Structure")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.primary;
    protected override Bitmap Icon => Icons.IconFactory.Member();
    public override Guid ComponentGuid => new("032C0739-0E87-44DA-B33C-8224600C49D8");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inCurve = pManager.AddCurveParameter("Curve", "C",
            "Line or Polyline geometry defining the member(s). A Polyline produces one member per segment.",
            GH_ParamAccess.item);
        _inSection = pManager.AddParameter(new Param_SgSection(),
            "Section", "S",
            "The cross-section profile.",
            GH_ParamAccess.item);
        _inMaterial = pManager.AddParameter(new Param_SgMaterial(),
            "Material", "Mt",
            "The material.",
            GH_ParamAccess.item);
        _inType = pManager.AddParameter(
            new Param_SgIntegerOption("Type", ValueListHelper.MemberTypeOptions, defaultValue: 0),
            "Type", "T",
            "Member type (Beam=0, Truss=1, Cable=2, Compression Only=3, Tension Only=4)\n" +
            "Default: Beam.",
            GH_ParamAccess.item);
        _inReleaseA = pManager.AddParameter(new Param_SgRelease(),
            "Release A", "RA",
            "End release at Node A (optional).",
            GH_ParamAccess.item);
        _inReleaseB = pManager.AddParameter(new Param_SgRelease(),
            "Release B", "RB",
            "End release at Node B (optional).",
            GH_ParamAccess.item);
        _inDirAngle = pManager.AddNumberParameter("Direction Angle", "DA",
            "Member orientation angle in degrees about the longitudinal axis. Default: 0.",
            GH_ParamAccess.item, 0.0);
        _inDirAxis = pManager.AddParameter(
            new Param_SgIntegerOption("Direction Axis", ValueListHelper.DirectionAxisOptions, defaultValue: 0),
            "Direction Axis", "DX",
            "Global axis for member orientation (overrides Direction Angle)\n" +
            "(Not Applicable=0, X-Axis=1, Y-Axis=2, Z-Axis=3, Negative X-Axis=4, Negative Y-Axis=5, Negative Z-Axis=6).\n" +
            "Default: NotApplicable.",
            GH_ParamAccess.item);
        _inDirNode = pManager.AddPointParameter("Direction Node", "DN",
            "Point defining the member orientation direction (overrides Direction Axis and Angle). Must coincide with a model node.",
            GH_ParamAccess.item);
        _inOffset = pManager.AddParameter(new Param_SgMemberOffset(),
            "Offset", "Off",
            "Member offset — rigid offset at each end (optional).",
            GH_ParamAccess.item);

        pManager[_inType].Optional = true;
        pManager[_inReleaseA].Optional = true;
        pManager[_inReleaseB].Optional = true;
        pManager[_inDirAngle].Optional = true;
        pManager[_inDirAxis].Optional = true;
        pManager[_inDirNode].Optional = true;
        pManager[_inOffset].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outMember = pManager.AddParameter(new Param_SgMember(),
            "Member", "M",
            "The SpaceGass member(s). A Polyline produces multiple members.",
            GH_ParamAccess.list);
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        document.ScheduleSolution(0, doc => ValueListHelper.AutoCreateOnPlacement(this, doc));
    }


    protected override void SolveInstance(IGH_DataAccess da)
    {
        Curve curve = null;
        GH_SgSection sectionGoo = null;
        GH_SgMaterial materialGoo = null;
        var typeInt = 0;

        if (!da.GetData(_inCurve, ref curve) || curve == null) return;
        if (!da.GetData(_inSection, ref sectionGoo) || sectionGoo?.Value == null) return;
        if (!da.GetData(_inMaterial, ref materialGoo) || materialGoo?.Value == null) return;
        da.GetData(_inType, ref typeInt);

        // Read optional releases
        GH_SgRelease releaseAGoo = null;
        GH_SgRelease releaseBGoo = null;
        da.GetData(_inReleaseA, ref releaseAGoo);
        da.GetData(_inReleaseB, ref releaseBGoo);

        // Read direction inputs (ADR-0010)
        var dirAngle = 0.0;
        var dirAxisInt = 0;
        var dirNodePt = Point3d.Unset;
        da.GetData(_inDirAngle, ref dirAngle);
        da.GetData(_inDirAxis, ref dirAxisInt);
        var hasDirNode = da.GetData(_inDirNode, ref dirNodePt);
        var hasDirAxis = Params.Input[_inDirAxis].Sources.Count > 0;
        var hasDirAngle = Params.Input[_inDirAngle].Sources.Count > 0;

        // Read optional offset
        GH_SgMemberOffset offsetGoo = null;
        da.GetData(_inOffset, ref offsetGoo);

        // Resolve direction with priority: Node > Axis > Angle
        var direction = ResolveDirection(
            dirAngle, dirAxisInt, dirNodePt,
            hasDirNode, hasDirAxis, hasDirAngle);

        // Map integer to SpaceGass MemberType (null = default beam)
        MemberType? memberType = typeInt switch
        {
            0 => null, // beam (default)
            1 => MemberType.Truss,
            2 => MemberType.Cable,
            3 => MemberType.CompressionOnly,
            4 => MemberType.TensionOnly,
            _ => null
        };

        // Extract line segments from geometry (ADR-0003)
        var lines = ExtractLines(curve);
        if (lines == null) return; // error already added

        var members = new List<GH_SgMember>(lines.Count);
        foreach (var line in lines)
        {
            if (line.Length < 1e-10)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    $"Skipped zero-length segment at ({line.From.X:F3}, {line.From.Y:F3}, {line.From.Z:F3}).");
                continue;
            }

            var start = new SgPoint3D(line.From.X, line.From.Y, line.From.Z);
            var end = new SgPoint3D(line.To.X, line.To.Y, line.To.Z);

            var member = new SgMemberData(start, end, sectionGoo.Value, materialGoo.Value, memberType,
                releaseAGoo?.Value,
                releaseBGoo?.Value,
                direction,
                offsetGoo?.Value);
            members.Add(new GH_SgMember(member));
        }

        if (members.Count == 0)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No valid segments produced.");
            return;
        }

        da.SetDataList(_outMember, members);
    }

    /// <summary>
    ///     Resolves member direction from three optional inputs with priority: Node > Axis > Angle.
    ///     Warns when multiple inputs are provided simultaneously.
    /// </summary>
    private SgDirectionData? ResolveDirection(
        double angle, int axisInt, Point3d nodePt,
        bool hasDirNode, bool hasDirAxis, bool hasDirAngle)
    {
        var axis = (DirectionAxis)axisInt;

        // Priority 1: Direction Node (highest)
        if (hasDirNode)
        {
            if (hasDirAxis && axis != DirectionAxis.NotApplicable)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Direction Node overrides Direction Axis — Direction Axis input will be ignored.");
            if (hasDirAngle && Math.Abs(angle) > 1e-10)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Direction Node overrides Direction Angle — Direction Angle input will be ignored.");

            return SgDirectionData.FromNode(
                new SgPoint3D(nodePt.X, nodePt.Y, nodePt.Z));
        }

        // Priority 2: Direction Axis
        if (hasDirAxis && axis != DirectionAxis.NotApplicable)
        {
            if (hasDirAngle && Math.Abs(angle) > 1e-10)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Direction Axis overrides Direction Angle — Direction Angle input will be ignored.");

            return SgDirectionData.FromAxis(axis);
        }

        // Priority 3: Direction Angle (or default)
        if (hasDirAngle && Math.Abs(angle) > 1e-10)
            return SgDirectionData.FromAngle(angle);

        // No direction inputs connected or all defaults → null (SpaceGass defaults apply)
        return null;
    }

    /// <summary>
    ///     Extracts line segments from the input curve.
    ///     Returns null and adds an error if the geometry type is not supported.
    /// </summary>
    private List<Line> ExtractLines(Curve curve)
    {
        // Polyline → multiple segments (check first to preserve intermediate
        // nodes on collinear polylines that would pass IsLinear)
        if (curve.TryGetPolyline(out var polyline))
        {
            var lines = new List<Line>(polyline.SegmentCount);
            for (var i = 0; i < polyline.SegmentCount; i++)
                lines.Add(polyline.SegmentAt(i));
            return lines;
        }

        // Line or LineCurve → single segment
        if (curve.IsLinear()) return new List<Line> { new(curve.PointAtStart, curve.PointAtEnd) };

        // Reject arcs, NURBS, and other curve types
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
            "Curve geometry is not supported. Use Line or Polyline, or explode/discretise curves first.");
        return null;
    }
}