using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgMaterial : GH_Goo<SgMaterialData>
{
    public GH_SgMaterial()
    {
    }

    public GH_SgMaterial(SgMaterialData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Material";
    public override string TypeDescription => "A SpaceGass material definition.";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgMaterial(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Material";
        return Value.IsLibrary
            ? $"Material: {Value.Library} / {Value.Name}"
            : $"Material: {Value.Name} (custom)";
    }

    public override bool CastFrom(object source)
    {
        if (source is SgMaterialData data)
        {
            Value = data;
            return true;
        }

        return false;
    }
}