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

public class CreatePlateComponent : GH_Component
{
    private int _inBendingThickness;
    private int _inMaterial;
    private int _inMembraneThickness;
    private int _inMesh;
    private int _inOffset;
    private int _inShearThickness;
    private int _inTheory;
    private int _inThickness;

    private int _outPlate;

    public CreatePlateComponent()
        : base("SG Plate", "sgPlate",
            "Create SpaceGass plate elements from a mesh. Each mesh face becomes a plate (tri or quad).",
            "SpaceGass", "3 | Structure")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.primary;
    protected override Bitmap Icon => Icons.IconFactory.PlateElement();
    public override Guid ComponentGuid => new("A3E70C07-B8A3-4D22-9F1D-7E6A5C4B3D27");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inMesh = pManager.AddMeshParameter("Mesh", "M",
            "Mesh geometry. Each face becomes a plate element (tri or quad).",
            GH_ParamAccess.item);
        _inMaterial = pManager.AddParameter(new Param_SgMaterial(),
            "Material", "Mat",
            "The material for the plate elements.",
            GH_ParamAccess.item);
        _inThickness = pManager.AddNumberParameter("Thickness", "T",
            "Actual plate thickness.",
            GH_ParamAccess.item);
        _inBendingThickness = pManager.AddNumberParameter("Bending Thickness", "BT",
            "Bending thickness override (optional — defaults to actual thickness).",
            GH_ParamAccess.item);
        _inMembraneThickness = pManager.AddNumberParameter("Membrane Thickness", "MT",
            "Membrane thickness override (optional — defaults to actual thickness).",
            GH_ParamAccess.item);
        _inShearThickness = pManager.AddNumberParameter("Shear Thickness", "ST",
            "Shear thickness override (optional — defaults to actual thickness).",
            GH_ParamAccess.item);
        _inOffset = pManager.AddNumberParameter("Offset", "Off",
            "Offset from the nodal plane (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inTheory = pManager.AddParameter(
            new Param_SgIntegerOption("Theory", ValueListHelper.PlateTheoryOptions,
                defaultValue: 0, autoCreate: true),
            "Theory", "Th",
            "Plate theory (Kirchoff=0, Mindlin=1). Default: Kirchoff.",
            GH_ParamAccess.item);

        pManager[_inBendingThickness].Optional = true;
        pManager[_inMembraneThickness].Optional = true;
        pManager[_inShearThickness].Optional = true;
        pManager[_inOffset].Optional = true;
        pManager[_inTheory].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outPlate = pManager.AddParameter(new Param_SgPlate(),
            "Plates", "P",
            "The SpaceGass plate element(s). One per mesh face.",
            GH_ParamAccess.list);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        Mesh mesh = null;
        GH_SgMaterial materialGoo = null;
        double thickness = 0;

        if (!da.GetData(_inMesh, ref mesh) || mesh == null) return;
        if (!da.GetData(_inMaterial, ref materialGoo) || materialGoo?.Value == null) return;
        if (!da.GetData(_inThickness, ref thickness)) return;

        double bendingThickness = 0, membraneThickness = 0, shearThickness = 0;
        double offset = 0;
        int theory = 0;

        var hasBending = da.GetData(_inBendingThickness, ref bendingThickness);
        var hasMembrane = da.GetData(_inMembraneThickness, ref membraneThickness);
        var hasShear = da.GetData(_inShearThickness, ref shearThickness);
        da.GetData(_inOffset, ref offset);
        da.GetData(_inTheory, ref theory);

        var plateTheory = theory == 0 ? PlateTheory.Kirchoff : PlateTheory.Mindlin;

        var plates = new List<GH_SgPlate>();
        for (var i = 0; i < mesh.Faces.Count; i++)
        {
            var face = mesh.Faces[i];
            SgPoint3D[] nodes;

            if (face.IsQuad)
            {
                nodes = new[]
                {
                    ToSgPoint(mesh.Vertices[face.A]),
                    ToSgPoint(mesh.Vertices[face.B]),
                    ToSgPoint(mesh.Vertices[face.C]),
                    ToSgPoint(mesh.Vertices[face.D])
                };
            }
            else if (face.IsTriangle)
            {
                nodes = new[]
                {
                    ToSgPoint(mesh.Vertices[face.A]),
                    ToSgPoint(mesh.Vertices[face.B]),
                    ToSgPoint(mesh.Vertices[face.C])
                };
            }
            else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    $"Mesh face {i} has an unsupported vertex count — skipped.");
                continue;
            }

            var plate = new SgPlateData(
                nodes, materialGoo.Value, thickness,
                hasBending ? bendingThickness : null,
                hasMembrane ? membraneThickness : null,
                hasShear ? shearThickness : null,
                offset, plateTheory);

            plates.Add(new GH_SgPlate(plate));
        }

        if (plates.Count == 0)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "No valid mesh faces found — no plates created.");

        da.SetDataList(_outPlate, plates);
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        document.ScheduleSolution(0, doc => ValueListHelper.AutoCreateOnPlacement(this, doc));
    }

    private static SgPoint3D ToSgPoint(Point3f pt)
    {
        return new SgPoint3D(pt.X, pt.Y, pt.Z);
    }
}

