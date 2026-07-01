namespace GhSpaceGass.Core.Models;

/// <summary>
///     Result of a node displacements query, including mapped data and any warnings.
/// </summary>
public class SgNodeDisplacementsResult
{
    public List<SgNodeDisplacementData> Displacements { get; } = new();
    public List<string> Warnings { get; } = new();
}