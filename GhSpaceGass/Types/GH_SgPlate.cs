using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgPlate : GH_Goo<SgPlateData>
{
    public GH_SgPlate()
    {
    }

    public GH_SgPlate(SgPlateData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Plate";
    public override string TypeDescription => "A SpaceGass plate element (3 or 4 node shell element).";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgPlate(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Plate";
        var type = Value.IsTriangle ? "Tri" : "Quad";
        return $"Plate ({type}): t={Value.ActualThickness} [{Value.Material.Name}]";
    }
}

