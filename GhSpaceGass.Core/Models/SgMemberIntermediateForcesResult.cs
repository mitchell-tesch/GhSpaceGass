namespace GhSpaceGass.Core.Models;

/// <summary>
///     Result of an intermediate member forces query, including mapped data and any warnings.
/// </summary>
public class SgMemberIntermediateForcesResult
{
    public List<SgMemberIntermediateForceData> Forces { get; } = new();
    public List<string> Warnings { get; } = new();
}