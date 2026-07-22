namespace GhSpaceGass.Core.Models;

/// <summary>
///     Result of the Generate Moving Loads workflow. Carries the load case IDs and group
///     labels SpaceGass produced for each subgroup (input branch path → result branch),
///     plus overall success / elapsed time / warnings.
/// </summary>
public class SgMovingLoadGenerationResult
{
    public bool Success { get; set; } = true;

    /// <summary>Total wall-clock time spent inside the Generate workflow.</summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    ///     Per-branch results, keyed by the input branch path (e.g. <c>"{0}"</c>,
    ///     <c>"{1}"</c>). Each branch carries the generated moving-load load case IDs and
    ///     group labels SpaceGass returned for that branch's Generate call.
    /// </summary>
    public Dictionary<string, SgMovingLoadGenerationBranch> Branches { get; } = new();

    /// <summary>Human-readable warnings surfaced during generation.</summary>
    public List<string> Warnings { get; } = new();
}

/// <summary>Per-branch result of a Generate Moving Loads call.</summary>
public class SgMovingLoadGenerationBranch
{
    public SgMovingLoadGenerationBranch(string path)
    {
        Path = path;
    }

    /// <summary>The input branch path this result corresponds to (e.g. <c>"{0}"</c>).</summary>
    public string Path { get; }

    /// <summary>Load case IDs SpaceGass generated for this branch.</summary>
    public List<int> LoadCaseIds { get; } = new();

    /// <summary>Group labels SpaceGass tagged the generated cases with.</summary>
    public List<string> Groups { get; } = new();
}
