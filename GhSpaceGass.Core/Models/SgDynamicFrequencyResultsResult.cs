namespace GhSpaceGass.Core.Models;

/// <summary>
///     Result of a dynamic frequency results query, combining natural frequencies and mode shapes.
/// </summary>
public class SgDynamicFrequencyResultsResult
{
    public List<SgNaturalFrequencyData> NaturalFrequencies { get; } = new();
    public List<SgModeShapeNodeData> ModeShapes { get; } = new();
    public List<string> Warnings { get; } = new();
}

