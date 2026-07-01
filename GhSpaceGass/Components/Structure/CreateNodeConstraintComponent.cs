using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Helpers;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Rhino.Geometry;
using SpaceGassApi.Models;

namespace GhSpaceGass.Components.Structure;

public class CreateNodeConstraintComponent : GH_Component
{
    private int _inAxes;
    private int _inFx;
    private int _inFy;
    private int _inFz;
    private int _inMasterPoint;
    private int _inMx;
    private int _inMy;
    private int _inMz;
    private int _inSlavePoint;
    private int _inXVector;
    private int _inYVector;
    private int _inZVector;

    private int _outConstraint;

    public CreateNodeConstraintComponent()
        : base("SG Node Constraint", "sgConstraint",
            "Create a SpaceGass node constraint (master-slave rigid link between two nodes).",
            "SpaceGass", "3 | Structure")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.primary;
    protected override Bitmap Icon => Icons.IconFactory.NodeConstraint();
    public override Guid ComponentGuid => new("A3E70C05-B8A3-4D22-9F1D-7E6A5C4B3D25");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inSlavePoint = pManager.AddPointParameter("Slave Point", "SP",
            "The constrained (slave) node location.",
            GH_ParamAccess.item);
        _inMasterPoint = pManager.AddPointParameter("Master Point", "MP",
            "The master node location.",
            GH_ParamAccess.item);
        _inFx = pManager.AddBooleanParameter("Fx", "Fx",
            "Constrain translation in X (default: true = constrained).",
            GH_ParamAccess.item, true);
        _inFy = pManager.AddBooleanParameter("Fy", "Fy",
            "Constrain translation in Y (default: true = constrained).",
            GH_ParamAccess.item, true);
        _inFz = pManager.AddBooleanParameter("Fz", "Fz",
            "Constrain translation in Z (default: true = constrained).",
            GH_ParamAccess.item, true);
        _inMx = pManager.AddBooleanParameter("Mx", "Mx",
            "Constrain rotation about X (default: true = constrained).",
            GH_ParamAccess.item, true);
        _inMy = pManager.AddBooleanParameter("My", "My",
            "Constrain rotation about Y (default: true = constrained).",
            GH_ParamAccess.item, true);
        _inMz = pManager.AddBooleanParameter("Mz", "Mz",
            "Constrain rotation about Z (default: true = constrained).",
            GH_ParamAccess.item, true);
        _inAxes = pManager.AddParameter(
            new Param_SgIntegerOption("Constraint Axes", ValueListHelper.ConstraintAxesOptions,
                defaultValue: 0, autoCreate: true),
            "Axes", "Ax",
            "Constraint axis system (Global=0, Inclined=1). Default: Global.",
            GH_ParamAccess.item);
        _inXVector = pManager.AddNumberParameter("X Vector", "Xv",
            "X component of direction vector (for inclined axes).",
            GH_ParamAccess.item, 0.0);
        _inYVector = pManager.AddNumberParameter("Y Vector", "Yv",
            "Y component of direction vector (for inclined axes).",
            GH_ParamAccess.item, 0.0);
        _inZVector = pManager.AddNumberParameter("Z Vector", "Zv",
            "Z component of direction vector (for inclined axes).",
            GH_ParamAccess.item, 0.0);

        pManager[_inFx].Optional = true;
        pManager[_inFy].Optional = true;
        pManager[_inFz].Optional = true;
        pManager[_inMx].Optional = true;
        pManager[_inMy].Optional = true;
        pManager[_inMz].Optional = true;
        pManager[_inAxes].Optional = true;
        pManager[_inXVector].Optional = true;
        pManager[_inYVector].Optional = true;
        pManager[_inZVector].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outConstraint = pManager.AddParameter(new Param_SgNodeConstraint(),
            "Constraint", "C",
            "The SpaceGass node constraint.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        var slavePoint = Point3d.Unset;
        var masterPoint = Point3d.Unset;
        bool fx = true, fy = true, fz = true;
        bool mx = true, my = true, mz = true;
        int axes = 0;
        double xv = 0, yv = 0, zv = 0;

        if (!da.GetData(_inSlavePoint, ref slavePoint)) return;
        if (!da.GetData(_inMasterPoint, ref masterPoint)) return;
        da.GetData(_inFx, ref fx);
        da.GetData(_inFy, ref fy);
        da.GetData(_inFz, ref fz);
        da.GetData(_inMx, ref mx);
        da.GetData(_inMy, ref my);
        da.GetData(_inMz, ref mz);
        da.GetData(_inAxes, ref axes);
        da.GetData(_inXVector, ref xv);
        da.GetData(_inYVector, ref yv);
        da.GetData(_inZVector, ref zv);

        // Build 6-char constraint code: F=Constrained, R=Free
        var code = new string(new[]
        {
            fx ? 'F' : 'R', fy ? 'F' : 'R', fz ? 'F' : 'R',
            mx ? 'F' : 'R', my ? 'F' : 'R', mz ? 'F' : 'R'
        });

        var constraintAxes = axes == 0 ? ConstraintAxes.Global : ConstraintAxes.Inclined;

        // Direction vector only relevant for inclined axes
        double? xVector = null, yVector = null, zVector = null;
        if (constraintAxes == ConstraintAxes.Inclined)
        {
            xVector = xv;
            yVector = yv;
            zVector = zv;

            if (xv == 0 && yv == 0 && zv == 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                    "Inclined axes selected but direction vector is zero — constraint may not behave as expected.");
        }

        var sgSlave = new SgPoint3D(slavePoint.X, slavePoint.Y, slavePoint.Z);
        var sgMaster = new SgPoint3D(masterPoint.X, masterPoint.Y, masterPoint.Z);

        var constraint = new SgNodeConstraintData(
            sgSlave, sgMaster, code, constraintAxes, xVector, yVector, zVector);

        da.SetData(_outConstraint, new GH_SgNodeConstraint(constraint));
    }

    public override void AddedToDocument(GH_Document document)
    {
        base.AddedToDocument(document);
        document.ScheduleSolution(0, doc => ValueListHelper.AutoCreateOnPlacement(this, doc));
    }
}

