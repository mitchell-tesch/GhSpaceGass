namespace GhSpaceGass.Core.Models;

/// <summary>
///     Result container for a steel member check summary query — flat list of per-member checks
///     plus any warnings emitted during the query (unmatched filter IDs, API notes, etc.).
///     Steel design results are aggregated per-member (critical load case as a value), so no
///     load-case data-tree dimension is used (ADR-0016).
/// </summary>
public class SgSteelMemberCheckSummaryResult
{
    public List<SgSteelMemberCheckData> Checks { get; } = new();
    public List<string> Warnings { get; } = new();
}
