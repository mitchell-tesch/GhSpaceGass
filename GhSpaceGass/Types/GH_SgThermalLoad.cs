using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgThermalLoad : GH_Goo<SgThermalLoadData>
{
    public GH_SgThermalLoad()
    {
    }

    public GH_SgThermalLoad(SgThermalLoadData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Thermal Load";
    public override string TypeDescription => "A SpaceGass thermal load (temperature change + gradients on a member or plate).";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgThermalLoad(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Thermal Load";
        var type = Value.ElementType == SpaceGassApi.Models.ThermalElementType.Member ? "Member" : "Plate";
        if (Value.IsZero) return $"Thermal ({type}): [{Value.LoadCase.Name}] (zero)";
        return $"Thermal ({type}): [{Value.LoadCase.Name}] T={Value.ThermalLoad} Gy={Value.YGradient} Gz={Value.ZGradient}";
    }
}

