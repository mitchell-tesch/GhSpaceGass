namespace GhSpaceGass.Core.Models;

/// <summary>
///     Result of querying self-weight loads from SpaceGass.
/// </summary>
public class SgSelfWeightLoadsDataResult
{
    public List<SgSelfWeightLoadInfo> Loads { get; } = new();
    public List<string> Warnings { get; } = new();
}

/// <summary>A single self-weight load entry from the SpaceGass API.</summary>
public readonly record struct SgSelfWeightLoadInfo(
    int LoadCaseId,
    string LoadCaseName,
    int LoadCategoryId,
    double AccelerationX,
    double AccelerationY,
    double AccelerationZ);
