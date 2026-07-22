using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgMovingLoadScenario : GH_Goo<SgMovingLoadScenarioData>
{
    public GH_SgMovingLoadScenario()
    {
    }

    public GH_SgMovingLoadScenario(SgMovingLoadScenarioData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Moving Load Scenario";
    public override string TypeDescription => "A SpaceGass moving load scenario definition.";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgMovingLoadScenario(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Moving Load Scenario";
        if (Value.StartingLoadCase != null)
            return $"Moving Load Scenario: {Value.Name} (LC: {Value.StartingLoadCase.Name})";
        return $"Moving Load Scenario: {Value.Name}";
    }
}
