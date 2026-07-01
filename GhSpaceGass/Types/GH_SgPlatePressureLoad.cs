using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgPlatePressureLoad : GH_Goo<SgPlatePressureLoadData>
{
    public GH_SgPlatePressureLoad()
    {
    }

    public GH_SgPlatePressureLoad(SgPlatePressureLoadData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Plate Pressure Load";
    public override string TypeDescription => "A SpaceGass plate pressure load.";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgPlatePressureLoad(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Plate Pressure Load";
        if (Value.IsZero) return $"Plate Pressure: [{Value.LoadCase.Name}] (zero)";
        return $"Plate Pressure: [{Value.LoadCase.Name}] P=({Value.Px},{Value.Py},{Value.Pz})";
    }
}
