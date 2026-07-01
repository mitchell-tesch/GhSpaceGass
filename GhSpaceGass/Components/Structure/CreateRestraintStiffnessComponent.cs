using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;

namespace GhSpaceGass.Components.Structure;

public class CreateRestraintStiffnessComponent : GH_Component
{
    private int _inKx, _inKy, _inKz, _inKmx, _inKmy, _inKmz;

    public CreateRestraintStiffnessComponent()
        : base("SG Restraint Stiffness", "sgRstK",
            "Create spring stiffness parameters for a SpaceGass restraint. DOFs with stiffness become spring supports (code 'S').",
            "SpaceGass", "3 | Structure")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.tertiary;
    protected override Bitmap Icon => Icons.IconFactory.RestraintStiffness();
    public override Guid ComponentGuid => new("940DC569-64BC-43F7-A7E3-DAB231C1137F");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inKx = pManager.AddNumberParameter("Kx", "Kx",
            "Spring stiffness for TX (translation X).",
            GH_ParamAccess.item);
        _inKy = pManager.AddNumberParameter("Ky", "Ky",
            "Spring stiffness for TY (translation Y).",
            GH_ParamAccess.item);
        _inKz = pManager.AddNumberParameter("Kz", "Kz",
            "Spring stiffness for TZ (translation Z).",
            GH_ParamAccess.item);
        _inKmx = pManager.AddNumberParameter("Kmx", "Kmx",
            "Spring stiffness for RX (rotation X).",
            GH_ParamAccess.item);
        _inKmy = pManager.AddNumberParameter("Kmy", "Kmy",
            "Spring stiffness for RY (rotation Y).",
            GH_ParamAccess.item);
        _inKmz = pManager.AddNumberParameter("Kmz", "Kmz",
            "Spring stiffness for RZ (rotation Z).",
            GH_ParamAccess.item);

        for (var i = 0; i < pManager.ParamCount; i++)
            pManager[i].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddParameter(new Param_SgRestraintStiffness(),
            "Stiffness", "K",
            "Restraint spring stiffness parameters.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        double v = 0;
        double? kTx = da.GetData(_inKx, ref v) ? v : null;
        double? kTy = da.GetData(_inKy, ref v) ? v : null;
        double? kTz = da.GetData(_inKz, ref v) ? v : null;
        double? kRx = da.GetData(_inKmx, ref v) ? v : null;
        double? kRy = da.GetData(_inKmy, ref v) ? v : null;
        double? kRz = da.GetData(_inKmz, ref v) ? v : null;

        var stiffness = new SgRestraintStiffnessData(kTx, kTy, kTz, kRx, kRy, kRz);

        if (!stiffness.HasAnyStiffness)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "No stiffness values provided — at least one is needed for a spring support.");

        da.SetData(0, new GH_SgRestraintStiffness(stiffness));
    }
}