namespace GhSpaceGass.Core.Models;

/// <summary>
///     A constituent entry within a combination load case: a primary load case or another
///     combination load case + scale factor.
/// </summary>
public class SgCombinationConstituent
{
    /// <summary>Create a constituent referencing a primary load case.</summary>
    public SgCombinationConstituent(SgLoadCaseData loadCase, double factor)
    {
        LoadCase = loadCase ?? throw new ArgumentNullException(nameof(loadCase));
        Factor = factor;
    }

    /// <summary>Create a constituent referencing another combination load case.</summary>
    public SgCombinationConstituent(SgCombinationLoadCaseData combinationLoadCase, double factor)
    {
        CombinationLoadCase = combinationLoadCase ?? throw new ArgumentNullException(nameof(combinationLoadCase));
        Factor = factor;
    }

    /// <summary>The primary load case being combined (null when referencing a combination).</summary>
    public SgLoadCaseData? LoadCase { get; }

    /// <summary>The combination load case being combined (null when referencing a primary).</summary>
    public SgCombinationLoadCaseData? CombinationLoadCase { get; }

    /// <summary>The scale factor applied to this constituent (e.g., 1.2, 1.5).</summary>
    public double Factor { get; }

    /// <summary>Whether this constituent references another combination load case.</summary>
    public bool IsCombinationReference => CombinationLoadCase != null;

    /// <summary>The display name of the referenced load case (primary or combination).</summary>
    public string Name => LoadCase?.Name ?? CombinationLoadCase!.Name;

    /// <summary>The deduplication key of the referenced load case (primary or combination).</summary>
    public string Key => LoadCase?.Key ?? CombinationLoadCase!.Key;
}

/// <summary>
///     In-memory representation of a combination load case. No API call — pure data.
///     Combines primary load cases and/or other combination load cases with scale factors
///     (e.g., 1.2×Dead + 1.5×Live, or 1.0×ULS + 0.7×Wind_Combo).
/// </summary>
public class SgCombinationLoadCaseData
{
    public SgCombinationLoadCaseData(
        string name,
        IReadOnlyList<SgCombinationConstituent> constituents,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Combination load case name cannot be null or empty.", nameof(name));

        if (constituents == null || constituents.Count == 0)
            throw new ArgumentException(
                "Combination load case must have at least one constituent.", nameof(constituents));

        Name = name;
        Constituents = constituents;
        Notes = notes;
    }

    /// <summary>The combination load case title.</summary>
    public string Name { get; }

    /// <summary>Optional descriptive notes.</summary>
    public string? Notes { get; }

    /// <summary>The constituent load cases and their scale factors.</summary>
    public IReadOnlyList<SgCombinationConstituent> Constituents { get; }

    /// <summary>Unique key for deduplication (case-insensitivity enforced by consuming dictionaries).</summary>
    public string Key => Name;
}