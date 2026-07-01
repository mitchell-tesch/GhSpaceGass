namespace GhSpaceGass.Core.Models;

/// <summary>
///     Result of a member displacements query, including mapped data and any warnings.
/// </summary>
public class SgMemberDisplacementsResult
{
    public List<SgMemberDisplacementData> Displacements { get; } = new();
    public List<string> Warnings { get; } = new();
}