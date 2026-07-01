namespace GhSpaceGass.Core.Models;

/// <summary>
///     Result of a node reactions query, including the mapped reaction data and any warnings.
/// </summary>
public class SgNodeReactionsResult
{
    /// <summary>The reaction records, mapped from API types to domain types.</summary>
    public List<SgNodeReactionData> Reactions { get; } = new();

    /// <summary>Warnings generated during the query (e.g., unmatched filter points).</summary>
    public List<string> Warnings { get; } = new();
}