using System;
using System.Collections.Generic;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace GhSpaceGass.Components.Loads;

public class CreateMovingLoadTravelPathComponent : GH_Component
{
    private int _inName;
    private int _inPath;
    private int _inRadii;

    private int _outTravelPath;

    public CreateMovingLoadTravelPathComponent()
        : base("SG Moving Load Travel Path", "sgMovLoadPath",
            "Create a SpaceGass moving load travel path — the polyline the SpaceGass moving-load " +
            "engine drags a vehicle or pressure along. Each polyline vertex becomes one station " +
            "in path order.",
            "SpaceGass", "5 | Loads")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.senary;
    protected override Bitmap Icon => Icons.IconFactory.MovingLoadTravelPath();
    public override Guid ComponentGuid => new("F1E9C2A5-3D8B-4A6F-B7E1-5C8D2A9F1B4E");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inName = pManager.AddTextParameter("Name", "N",
            "Travel path title (e.g., \"Left Lane\").",
            GH_ParamAccess.item);
        _inPath = pManager.AddCurveParameter("Path", "P",
            "Travel path geometry. Accepts a Line or a Polyline — each vertex becomes one station " +
            "in polyline vertex order. **The polyline's direction is the travel direction**: the " +
            "vehicle moves from vertex 0 to vertex N. Use Grasshopper's Flip Curve upstream if " +
            "the polyline runs the wrong way. Stations are pushed at the absolute coordinates of " +
            "each vertex — the plug-in does not snap them to nearby model nodes. Arcs and " +
            "free-form curves are not supported; explode or discretise them into a polyline first.",
            GH_ParamAccess.item);
        _inRadii = pManager.AddNumberParameter("Radii", "R",
            "Optional per-station arc radius, in metres. When set on a station, the straight " +
            "chord between the previous station and this one is replaced by a circular arc of " +
            "that radius passing through both stations (the chord endpoints stay pinned; the arc " +
            "bulges to one side of the chord). A negative radius bulges the arc to the opposite " +
            "side. The first station's entry has no previous chord and is ignored — supply any " +
            "placeholder value there. When provided the list must match the number of path " +
            "vertices exactly; when omitted every segment is straight.",
            GH_ParamAccess.list);

        pManager[_inRadii].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outTravelPath = pManager.AddParameter(new Param_SgMovingLoadTravelPath(),
            "Moving Load Travel Path", "MLTP",
            "The SpaceGass moving load travel path.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        string name = null;
        Curve curve = null;
        var radii = new List<double>();

        if (!da.GetData(_inName, ref name) || string.IsNullOrWhiteSpace(name))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "Moving load travel path name cannot be empty.");
            return;
        }

        if (!da.GetData(_inPath, ref curve) || curve == null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "A Path curve is required.");
            return;
        }

        da.GetDataList(_inRadii, radii);

        var vertices = ExtractVertices(curve);
        if (vertices == null) return;

        if (vertices.Count < 2)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                "A travel path needs at least two stations.");
            return;
        }

        if (radii.Count > 0 && radii.Count != vertices.Count)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                $"Radii count ({radii.Count}) must match the number of path stations ({vertices.Count}).");
            return;
        }

        var stations = new List<SgMovingLoadStationData>(vertices.Count);
        for (var i = 0; i < vertices.Count; i++)
        {
            var pt = vertices[i];
            var pos = new SgPoint3D(pt.X, pt.Y, pt.Z);
            double? radius = null;
            if (i > 0 && i < radii.Count) radius = radii[i];
            stations.Add(new SgMovingLoadStationData(pos, radius));
        }

        try
        {
            var path = new SgMovingLoadTravelPathData(name, stations);
            da.SetData(_outTravelPath, new GH_SgMovingLoadTravelPath(path));
        }
        catch (ArgumentException ex)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
        }
    }

    private List<Point3d> ExtractVertices(Curve curve)
    {
        // Polyline → all vertices (check first to preserve intermediate vertices on
        // collinear polylines that would also pass IsLinear)
        if (curve.TryGetPolyline(out var polyline))
        {
            var pts = new List<Point3d>(polyline.Count);
            for (var i = 0; i < polyline.Count; i++)
                pts.Add(polyline[i]);
            return pts;
        }

        // Line or LineCurve → two vertices
        if (curve.IsLinear())
            return new List<Point3d> { curve.PointAtStart, curve.PointAtEnd };

        AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
            "Curve geometry is not supported. Use Line or Polyline, or explode/discretise curves first.");
        return null;
    }
}
