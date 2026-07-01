using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgLoadCase : GH_Goo<SgLoadCaseData>
{
    public GH_SgLoadCase()
    {
    }

    public GH_SgLoadCase(SgLoadCaseData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Load Case";
    public override string TypeDescription => "A SpaceGass load case definition.";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgLoadCase(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Load Case";
        return string.IsNullOrEmpty(Value.Notes)
            ? $"Load Case: {Value.Name}"
            : $"Load Case: {Value.Name} ({Value.Notes})";
    }
}