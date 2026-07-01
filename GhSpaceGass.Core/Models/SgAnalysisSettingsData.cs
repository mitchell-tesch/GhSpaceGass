using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Models;

/// <summary>
///     Analysis type enum for the Run Analysis component.
/// </summary>
public enum SgAnalysisType
{
    LinearStatic = 0,
    NonlinearStatic = 1,
    Buckling = 2,
    DynamicFrequency = 3
}

/// <summary>
///     Unified container for analysis settings. Wraps the API settings types directly
///     (no intermediate domain model — settings are pure pass-through to the POST body).
///     Created by the per-type settings builder components (ADR-0014).
/// </summary>
public class SgAnalysisSettingsData
{
    private SgAnalysisSettingsData(SgAnalysisType type,
        StaticSettingsUpdate? staticSettings,
        BucklingSettingsUpdate? bucklingSettings,
        DynamicFrequencySettingsUpdate? dynamicSettings)
    {
        Type = type;
        StaticSettings = staticSettings;
        BucklingSettings = bucklingSettings;
        DynamicSettings = dynamicSettings;
    }

    /// <summary>Which analysis type these settings are for.</summary>
    public SgAnalysisType Type { get; }

    /// <summary>Settings for Linear Static or Non-linear Static analysis.</summary>
    public StaticSettingsUpdate? StaticSettings { get; }

    /// <summary>Settings for Buckling analysis.</summary>
    public BucklingSettingsUpdate? BucklingSettings { get; }

    /// <summary>Settings for Dynamic Frequency analysis.</summary>
    public DynamicFrequencySettingsUpdate? DynamicSettings { get; }

    /// <summary>Create settings for Linear Static analysis.</summary>
    public static SgAnalysisSettingsData ForLinearStatic(StaticSettingsUpdate settings)
    {
        return new SgAnalysisSettingsData(SgAnalysisType.LinearStatic, settings, null, null);
    }

    /// <summary>Create settings for Non-linear Static analysis.</summary>
    public static SgAnalysisSettingsData ForNonlinearStatic(StaticSettingsUpdate settings)
    {
        return new SgAnalysisSettingsData(SgAnalysisType.NonlinearStatic, settings, null, null);
    }

    /// <summary>Create settings for Buckling analysis.</summary>
    public static SgAnalysisSettingsData ForBuckling(BucklingSettingsUpdate settings)
    {
        return new SgAnalysisSettingsData(SgAnalysisType.Buckling, null, settings, null);
    }

    /// <summary>Create settings for Dynamic Frequency analysis.</summary>
    public static SgAnalysisSettingsData ForDynamicFrequency(DynamicFrequencySettingsUpdate settings)
    {
        return new SgAnalysisSettingsData(SgAnalysisType.DynamicFrequency, null, null, settings);
    }
}