namespace GhSpaceGass.Core.Models;

/// <summary>
///     Result of a buckling results query, combining load factors and effective lengths.
/// </summary>
public class SgBucklingResultsResult
{
    public List<SgBucklingLoadFactorData> LoadFactors { get; } = new();
    public List<SgBucklingEffectiveLengthData> EffectiveLengths { get; } = new();
    public List<string> Warnings { get; } = new();
}