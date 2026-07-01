using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;

namespace GhSpaceGass.Components.Properties;

public class CreateMaterialComponent : GH_Component
{
    private int _inE, _inPoissons, _inDensity, _inThermal, _inConcrete;
    private int _inLibrary, _inName;
    
    private int _outMaterial;

    public CreateMaterialComponent()
        : base("SG Material", "sgMaterial",
            "Create a SpaceGass material. Connect Library for library lookup, or omit for a custom user-defined material.",
            "SpaceGass", "2 | Properties")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.primary;
    protected override Bitmap Icon => Icons.IconFactory.Material();
    public override Guid ComponentGuid => new("A6267BFA-302F-470E-929D-707B1F6DF8AE");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inLibrary = pManager.AddTextParameter("Library", "L",
            "SpaceGass material library (e.g. \"Aust\"). Omit for custom material.",
            GH_ParamAccess.item);
        _inName = pManager.AddTextParameter("Name", "N",
            "Material name (library lookup or custom label).",
            GH_ParamAccess.item);
        _inE = pManager.AddNumberParameter("E", "E",
            "Young's modulus (custom mode).",
            GH_ParamAccess.item);
        _inPoissons = pManager.AddNumberParameter("Poissons Ratio", "v",
            "Poisson's ratio (custom mode).",
            GH_ParamAccess.item);
        _inDensity = pManager.AddNumberParameter("Density", "rho",
            "Mass density (custom mode).",
            GH_ParamAccess.item);
        _inThermal = pManager.AddNumberParameter("Thermal Coefficient", "a",
            "Thermal expansion coefficient (custom mode).",
            GH_ParamAccess.item);
        _inConcrete = pManager.AddNumberParameter("Concrete Strength", "fc",
            "Concrete compressive strength (custom mode, optional).",
            GH_ParamAccess.item);

        pManager[_inLibrary].Optional = true;
        for (var i = 2; i < pManager.ParamCount; i++)
            pManager[i].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outMaterial = pManager.AddParameter(new Param_SgMaterial(),
            "Material", "M",
            "The SpaceGass material.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        var name = string.Empty;
        if (!da.GetData(_inName, ref name) || string.IsNullOrWhiteSpace(name))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Material name is required.");
            return;
        }

        var library = string.Empty;
        var hasLibrary = da.GetData(_inLibrary, ref library) && !string.IsNullOrWhiteSpace(library);

        if (hasLibrary)
        {
            da.SetData(0, new GH_SgMaterial(new SgMaterialData(library, name)));
        }
        else
        {
            // Custom mode
            double v = 0;
            double? e = null, poissons = null, density = null, thermal = null, concrete = null;
            if (da.GetData(_inE, ref v)) e = v;
            if (da.GetData(_inPoissons, ref v)) poissons = v;
            if (da.GetData(_inDensity, ref v)) density = v;
            if (da.GetData(_inThermal, ref v)) thermal = v;
            if (da.GetData(_inConcrete, ref v)) concrete = v;

            da.SetData(_outMaterial, new GH_SgMaterial(
                new SgMaterialData(name, e, poissons, density, thermal, concrete)));
        }
    }
}