using System.Linq;
using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgCombinationLoadCase : GH_Goo<SgCombinationLoadCaseData>
{
    public GH_SgCombinationLoadCase()
    {
    }

    public GH_SgCombinationLoadCase(SgCombinationLoadCaseData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Combination Load Case";

    public override string TypeDescription =>
        "A SpaceGass combination load case (linear combination of primary and/or combination load cases).";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgCombinationLoadCase(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Combination Load Case";
        var items = string.Join(" + ",
            Value.Constituents.Select(c => $"{c.Factor}×{c.Name}"));
        return $"Combination: {Value.Name} = {items}";
    }
}