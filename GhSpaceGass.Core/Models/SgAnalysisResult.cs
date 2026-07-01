namespace GhSpaceGass.Core.Models;

/// <summary>
///     Result of a static analysis run.
///     Maps from the SpaceGass API's AnalysisRun response to a domain type.
/// </summary>
public class SgAnalysisResult
{
    /// <summary>True if the analysis completed successfully.</summary>
    public bool Succeeded { get; init; }

    /// <summary>The unique run identifier assigned by SpaceGass.</summary>
    public Guid? RunId { get; init; }

    /// <summary>Elapsed wall-clock time reported by SpaceGass (e.g. "00:00:01.234").</summary>
    public string ElapsedTime { get; init; } = string.Empty;

    /// <summary>Error message from SpaceGass if the analysis failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Analysis warnings reported by SpaceGass.</summary>
    public List<string> Warnings { get; init; } = new();
}