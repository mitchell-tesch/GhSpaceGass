using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgSection : GH_Goo<SgSectionData>
{
    public GH_SgSection()
    {
    }

    public GH_SgSection(SgSectionData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Section";
    public override string TypeDescription => "A SpaceGass cross-section profile.";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgSection(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Section";
        return Value.IsLibrary
            ? $"Section: {Value.Library} / {Value.Name}"
            : $"Section: {Value.Name} (custom)";
    }

    public override bool CastFrom(object source)
    {
        if (source is SgSectionData data)
        {
            Value = data;
            return true;
        }

        return false;
    }
}