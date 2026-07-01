using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgAnalysisSettings : GH_Goo<SgAnalysisSettingsData>
{
    public GH_SgAnalysisSettings()
    {
    }

    public GH_SgAnalysisSettings(SgAnalysisSettingsData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Analysis Settings";
    public override string TypeDescription => "Analysis settings for a SpaceGass analysis run.";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgAnalysisSettings(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Analysis Settings";
        return Value.Type switch
        {
            SgAnalysisType.LinearStatic => "Settings: Linear Static",
            SgAnalysisType.NonlinearStatic => "Settings: Non-linear Static",
            SgAnalysisType.Buckling => $"Settings: Buckling ({Value.BucklingSettings?.Modes ?? 0} modes)",
            SgAnalysisType.DynamicFrequency =>
                $"Settings: Dynamic Frequency ({Value.DynamicSettings?.Modes ?? 0} modes)",
            _ => "Settings: Unknown"
        };
    }
}