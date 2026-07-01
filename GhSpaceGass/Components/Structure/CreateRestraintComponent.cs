using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace GhSpaceGass.Components.Structure;

public class CreateRestraintComponent : GH_Component
{
    private int _inFx, _inFy, _inFz, _inMx, _inMy, _inMz;
    private int _inPoint;
    private int _inStiffness, _inFriction;
    
    private int _outRestraint;

    public CreateRestraintComponent()
        : base("SG Restraint", "sgRestraint",
            "Create a SpaceGass node restraint (boundary condition) at a point.",
            "SpaceGass", "3 | Structure")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.tertiary;
    protected override Bitmap Icon => Icons.IconFactory.Restraint();
    public override Guid ComponentGuid => new("8DF373E1-2284-464B-8768-D5A4B13D52B6");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inPoint = pManager.AddPointParameter("Point", "P",
            "The location of the restraint.",
            GH_ParamAccess.item);
        _inFx = pManager.AddBooleanParameter("Fx", "Fx",
            "Fix X-translation (default: true).",
            GH_ParamAccess.item, true);
        _inFy = pManager.AddBooleanParameter("Fy", "Fy",
            "Fix Y-translation (default: true).",
            GH_ParamAccess.item, true);
        _inFz = pManager.AddBooleanParameter("Fz", "Fz",
            "Fix Z-translation (default: true).",
            GH_ParamAccess.item, true);
        _inMx = pManager.AddBooleanParameter("Mx", "Mx",
            "Fix X-rotation (default: false).",
            GH_ParamAccess.item, false);
        _inMy = pManager.AddBooleanParameter("My", "My",
            "Fix Y-rotation (default: false).",
            GH_ParamAccess.item, false);
        _inMz = pManager.AddBooleanParameter("Mz", "Mz",
            "Fix Z-rotation (default: false).",
            GH_ParamAccess.item, false);
        _inStiffness = pManager.AddParameter(new Param_SgRestraintStiffness(),
            "Stiffness", "K",
            "Spring stiffness parameters (optional - overrides DOFs to S code).",
            GH_ParamAccess.item);
        _inFriction = pManager.AddParameter(new Param_SgRestraintFriction(),
            "Friction", "Fr",
            "Friction parameters (optional - overrides translational DOFs to N code).",
            GH_ParamAccess.item);
        
        pManager[_inFx].Optional = true;
        pManager[_inFy].Optional = true;
        pManager[_inFz].Optional = true;
        pManager[_inMx].Optional = true;
        pManager[_inMy].Optional = true;
        pManager[_inMz].Optional = true;
        pManager[_inStiffness].Optional = true;
        pManager[_inFriction].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outRestraint = pManager.AddParameter(new Param_SgRestraint(),
            "Restraint", "R",
            "The SpaceGass restraint.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        var point = Point3d.Unset;
        bool fx = true, fy = true, fz = true;
        bool mx = false, my = false, mz = false;
        if (!da.GetData(_inPoint, ref point)) return;
        da.GetData(_inFx, ref fx);
        da.GetData(_inFy, ref fy);
        da.GetData(_inFz, ref fz);
        da.GetData(_inMx, ref mx);
        da.GetData(_inMy, ref my);
        da.GetData(_inMz, ref mz);
        // Read optional parameter Goo
        GH_SgRestraintStiffness stiffnessGoo = null;
        GH_SgRestraintFriction frictionGoo = null;
        da.GetData(_inStiffness, ref stiffnessGoo);
        da.GetData(_inFriction, ref frictionGoo);
        var stiffness = stiffnessGoo?.Value;
        var friction = frictionGoo?.Value;
        // Build base code from booleans: F = Fixed, R = Released
        var codes = new[]
        {
            fx ? 'F' : 'R',
            fy ? 'F' : 'R',
            fz ? 'F' : 'R',
            mx ? 'F' : 'R',
            my ? 'F' : 'R',
            mz ? 'F' : 'R'
        };
        // Override with stiffness (S code) where stiffness values are provided
        if (stiffness != null)
        {
            if (stiffness.KTx.HasValue) codes[0] = 'S';
            if (stiffness.KTy.HasValue) codes[1] = 'S';
            if (stiffness.KTz.HasValue) codes[2] = 'S';
            if (stiffness.KRx.HasValue) codes[3] = 'S';
            if (stiffness.KRy.HasValue) codes[4] = 'S';
            if (stiffness.KRz.HasValue) codes[5] = 'S';
        }

        // Override with friction (N code) where friction is defined - translational DOFs only
        if (friction != null)
        {
            if (friction.X != null)
            {
                if (codes[0] == 'S')
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Friction X overrides stiffness on TX - stiffness will be ignored for this DOF.");
                codes[0] = 'N';
            }

            if (friction.Y != null)
            {
                if (codes[1] == 'S')
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Friction Y overrides stiffness on TY - stiffness will be ignored for this DOF.");
                codes[1] = 'N';
            }

            if (friction.Z != null)
            {
                if (codes[2] == 'S')
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        "Friction Z overrides stiffness on TZ - stiffness will be ignored for this DOF.");
                codes[2] = 'N';
            }
        }

        var code = new string(codes);
        var sgPoint = new SgPoint3D(point.X, point.Y, point.Z);
        var restraint = new SgRestraintData(sgPoint, code, stiffness, friction);
        da.SetData(_outRestraint, new GH_SgRestraint(restraint));
    }
}