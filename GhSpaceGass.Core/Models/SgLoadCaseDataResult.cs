namespace GhSpaceGass.Core.Models;

/// <summary>
///     Result of querying load case data from SpaceGass.
/// </summary>
public class SgLoadCaseDataResult
{
    public List<SgLoadCaseInfo> LoadCases { get; } = new();
    public List<SgLoadCategoryInfo> Categories { get; } = new();
    public List<SgLoadCaseGroupInfo> Groups { get; } = new();
    public List<string> Warnings { get; } = new();
}

/// <summary>Load case info from the SpaceGass API.</summary>
public readonly record struct SgLoadCaseInfo(
    int Id,
    string Name,
    string Type,
    string Notes,
    List<string> CombinationItems);

/// <summary>Load category info from the SpaceGass API.</summary>
public readonly record struct SgLoadCategoryInfo(
    int Id,
    string Name,
    string Notes);

/// <summary>Load case group info from the SpaceGass API.</summary>
public readonly record struct SgLoadCaseGroupInfo(
    int Id,
    string Name,
    string LoadCaseList);
