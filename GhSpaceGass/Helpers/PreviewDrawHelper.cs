using System;
using System.Drawing;
using GhSpaceGass.Core.Models.Visuals;
using Rhino.Display;
using Rhino.Geometry;

namespace GhSpaceGass.Helpers;

/// <summary>
///     Shared viewport drawing helpers for results preview components.
///     Arrowheads, force arrows, moment arcs, and value text.
/// </summary>
public static class PreviewDrawHelper
{
    private const int LineWeight = 2;

    /// <summary>Draw a straight arrow (line + arrowhead) from origin to tip.</summary>
    public static void DrawForceArrow(DisplayPipeline display, Point3d origin, Point3d tip, Color color)
    {
        display.DrawLine(new Line(origin, tip), color, LineWeight);
        DrawArrowHead(display, origin, tip, color);
    }

    /// <summary>Draw an arrowhead (two wing lines) at the tip of a line.</summary>
    public static void DrawArrowHead(DisplayPipeline display, Point3d from, Point3d tip, Color color)
    {
        var dir = tip - from;
        var length = dir.Length;
        if (length < 1e-10) return;

        var headLength = length * 0.15;
        dir.Unitize();

        var perp = Math.Abs(dir.Z) < 0.9
            ? Vector3d.CrossProduct(dir, Vector3d.ZAxis)
            : Vector3d.CrossProduct(dir, Vector3d.XAxis);
        perp.Unitize();

        var headWidth = headLength * 0.4;
        var basePoint = tip - dir * headLength;
        display.DrawLine(new Line(tip, basePoint + perp * headWidth), color, LineWeight);
        display.DrawLine(new Line(tip, basePoint - perp * headWidth), color, LineWeight);
    }

    /// <summary>Draw a ¾-circle moment arc with arrowhead around the given axis.</summary>
    public static void DrawMomentArc(DisplayPipeline display, Point3d origin, PreviewArrow arrow, Color color)
    {
        var radius = Math.Sqrt(arrow.Dx * arrow.Dx + arrow.Dy * arrow.Dy + arrow.Dz * arrow.Dz);
        if (radius < 1e-10) return;

        var normal = arrow.Axis switch
        {
            0 => Vector3d.XAxis,
            1 => Vector3d.YAxis,
            _ => Vector3d.ZAxis
        };

        var magnitude = arrow.Axis switch
        {
            0 => arrow.Dx,
            1 => arrow.Dy,
            _ => arrow.Dz
        };
        if (magnitude < 0) normal = -normal;

        var plane = new Plane(origin, normal);
        var arc = new Arc(plane, radius, Math.PI * 1.5);

        display.DrawArc(arc, color, LineWeight);

        // Arrowhead at arc endpoint
        var endPt = arc.EndPoint;
        var tangent = arc.TangentAt(arc.AngleDomain.T1);
        tangent.Unitize();
        var headLength = radius * 0.2;
        var headPerp = Vector3d.CrossProduct(tangent, normal);
        headPerp.Unitize();
        var headWidth = headLength * 0.4;
        var basePoint = endPt - tangent * headLength;
        display.DrawLine(new Line(endPt, basePoint + headPerp * headWidth), color, LineWeight);
        display.DrawLine(new Line(endPt, basePoint - headPerp * headWidth), color, LineWeight);
    }

    /// <summary>Get the endpoint of a moment arc (for value text placement).</summary>
    public static Point3d GetMomentArcEndPoint(Point3d origin, PreviewArrow arrow)
    {
        var radius = Math.Sqrt(arrow.Dx * arrow.Dx + arrow.Dy * arrow.Dy + arrow.Dz * arrow.Dz);
        if (radius < 1e-10) return origin;

        var normal = arrow.Axis switch
        {
            0 => Vector3d.XAxis,
            1 => Vector3d.YAxis,
            _ => Vector3d.ZAxis
        };
        var magnitude = arrow.Axis switch
        {
            0 => arrow.Dx,
            1 => arrow.Dy,
            _ => arrow.Dz
        };
        if (magnitude < 0) normal = -normal;
        var plane = new Plane(origin, normal);
        var arc = new Arc(plane, radius, Math.PI * 1.5);
        return arc.EndPoint;
    }
}
