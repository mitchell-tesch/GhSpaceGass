using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace GhSpaceGass.Components.Loads;

public class CreateThermalLoadComponent : GH_Component
{
    private int _inElement;
    private int _inLoadCase;
    private int _inLoadCategory;
    private int _inTemperature;
    private int _inYGradient;
    private int _inZGradient;

    private int _outThermalLoad;

    public CreateThermalLoadComponent()
        : base("SG Thermal Load", "sgThermal",
            "Create a SpaceGass thermal load (temperature change + gradients). " +
            "Accepts a Line (member) or Plate Goo — auto-detects element type.",
            "SpaceGass", "5 | Loads")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.quinary;
    protected override Bitmap Icon => Icons.IconFactory.ThermalLoad();
    public override Guid ComponentGuid => new("A3E70C09-B8A3-4D22-9F1D-7E6A5C4B3D29");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inElement = pManager.AddGenericParameter("Element", "E",
            "The element to apply the thermal load to — Line (member) or Plate Goo.",
            GH_ParamAccess.item);
        _inLoadCase = pManager.AddParameter(new Param_SgLoadCase(),
            "Load Case", "LC",
            "The load case this load belongs to.",
            GH_ParamAccess.item);
        _inLoadCategory = pManager.AddParameter(new Param_SgLoadCategory(),
            "Load Category", "Cat",
            "Optional load category.",
            GH_ParamAccess.item);
        _inTemperature = pManager.AddNumberParameter("Temperature", "T",
            "Uniform temperature change (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inYGradient = pManager.AddNumberParameter("Y Gradient", "Gy",
            "Thermal gradient in Y direction (default: 0).",
            GH_ParamAccess.item, 0.0);
        _inZGradient = pManager.AddNumberParameter("Z Gradient", "Gz",
            "Thermal gradient in Z direction (default: 0).",
            GH_ParamAccess.item, 0.0);

        pManager[_inLoadCategory].Optional = true;
        pManager[_inTemperature].Optional = true;
        pManager[_inYGradient].Optional = true;
        pManager[_inZGradient].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outThermalLoad = pManager.AddParameter(new Param_SgThermalLoad(),
            "Thermal Load", "TL",
            "The SpaceGass thermal load.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        IGH_Goo elementGoo = null;
        GH_SgLoadCase loadCaseGoo = null;
        GH_SgLoadCategory loadCategoryGoo = null;
        double temperature = 0, yGradient = 0, zGradient = 0;

        if (!da.GetData(_inElement, ref elementGoo) || elementGoo == null) return;
        if (!da.GetData(_inLoadCase, ref loadCaseGoo)) return;
        da.GetData(_inLoadCategory, ref loadCategoryGoo);
        da.GetData(_inTemperature, ref temperature);
        da.GetData(_inYGradient, ref yGradient);
        da.GetData(_inZGradient, ref zGradient);

        if (loadCaseGoo?.Value == null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid load case input.");
            return;
        }

        var category = loadCategoryGoo?.Value;
        SgThermalLoadData thermalLoad;

        // Auto-detect element type
        if (elementGoo is GH_SgPlate plateGoo && plateGoo.Value != null)
        {
            // Plate thermal load
            thermalLoad = SgThermalLoadData.ForPlate(
                plateGoo.Value.Nodes, loadCaseGoo.Value,
                temperature, yGradient, zGradient, category);
        }
        else
        {
            // Try to extract a Line for member thermal load
            var line = Line.Unset;
            if (GH_Convert.ToLine(elementGoo, ref line, GH_Conversion.Both))
            {
                var start = new SgPoint3D(line.From.X, line.From.Y, line.From.Z);
                var end = new SgPoint3D(line.To.X, line.To.Y, line.To.Z);
                thermalLoad = SgThermalLoadData.ForMember(
                    start, end, loadCaseGoo.Value,
                    temperature, yGradient, zGradient, category);
            }
            else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    $"Element input must be a Line (member) or Plate Goo. Got: {elementGoo.TypeName}");
                return;
            }
        }

        if (thermalLoad.IsZero)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "All thermal values are zero — this load will have no effect.");

        da.SetData(_outThermalLoad, new GH_SgThermalLoad(thermalLoad));
    }
}

