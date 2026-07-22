using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgMovingLoadSettings : GH_Goo<SgMovingLoadSettingsData>
{
    public GH_SgMovingLoadSettings()
    {
    }

    public GH_SgMovingLoadSettings(SgMovingLoadSettingsData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Moving Load Settings";
    public override string TypeDescription =>
        "Job-level settings for the SpaceGass moving-load engine.";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgMovingLoadSettings(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Moving Load Settings";
        return Value.HasAnyValue
            ? "Moving Load Settings"
            : "Moving Load Settings (no overrides)";
    }
}
