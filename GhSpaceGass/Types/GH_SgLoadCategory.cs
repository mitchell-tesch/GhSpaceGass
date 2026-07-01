using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgLoadCategory : GH_Goo<SgLoadCategoryData>
{
    public GH_SgLoadCategory()
    {
    }

    public GH_SgLoadCategory(SgLoadCategoryData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Load Category";
    public override string TypeDescription => "A SpaceGass load category definition.";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgLoadCategory(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Load Category";
        return string.IsNullOrEmpty(Value.Notes)
            ? $"Load Category: {Value.Name}"
            : $"Load Category: {Value.Name} ({Value.Notes})";
    }
}