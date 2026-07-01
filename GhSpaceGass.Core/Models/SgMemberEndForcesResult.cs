namespace GhSpaceGass.Core.Models;

/// <summary>
///     Result of a member end forces query, including mapped data and any warnings.
/// </summary>
public class SgMemberEndForcesResult
{
    public List<SgMemberEndForceData> EndForces { get; } = new();
    public List<string> Warnings { get; } = new();
}