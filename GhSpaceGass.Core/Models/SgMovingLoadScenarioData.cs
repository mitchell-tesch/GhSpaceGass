namespace GhSpaceGass.Core.Models;

/// <summary>
///     A combination entry attached to a moving load scenario. Describes how the generated
///     moving-load sub-cases combine with an existing load case (primary or combination).
/// </summary>
public class SgMovingLoadCombinationEntry
{
    /// <summary>Create an entry referencing a primary load case.</summary>
    public SgMovingLoadCombinationEntry(
        SgLoadCaseData loadCase, double loadCaseFactor, double scenarioFactor,
        int? startingCombinationCase = null)
    {
        LoadCase = loadCase ?? throw new ArgumentNullException(nameof(loadCase));
        LoadCaseFactor = loadCaseFactor;
        ScenarioFactor = scenarioFactor;
        StartingCombinationCase = startingCombinationCase;
    }

    /// <summary>Create an entry referencing another combination load case.</summary>
    public SgMovingLoadCombinationEntry(
        SgCombinationLoadCaseData combinationLoadCase, double loadCaseFactor, double scenarioFactor,
        int? startingCombinationCase = null)
    {
        CombinationLoadCase = combinationLoadCase
                              ?? throw new ArgumentNullException(nameof(combinationLoadCase));
        LoadCaseFactor = loadCaseFactor;
        ScenarioFactor = scenarioFactor;
        StartingCombinationCase = startingCombinationCase;
    }

    /// <summary>The primary load case being combined (null when referencing a combination).</summary>
    public SgLoadCaseData? LoadCase { get; }

    /// <summary>The combination load case being combined (null when referencing a primary).</summary>
    public SgCombinationLoadCaseData? CombinationLoadCase { get; }

    /// <summary>Factor applied to the combined load case.</summary>
    public double LoadCaseFactor { get; }

    /// <summary>Factor applied to the moving load scenario itself.</summary>
    public double ScenarioFactor { get; }

    /// <summary>
    ///     Optional starting ID for the combination cases that SpaceGass generates from this
    ///     combination entry. When null, SpaceGass assigns the ID. Useful when appending to an
    ///     existing model where combination-case IDs would otherwise collide.
    /// </summary>
    public int? StartingCombinationCase { get; }

    /// <summary>Whether this entry references another combination load case.</summary>
    public bool IsCombinationReference => CombinationLoadCase != null;

    /// <summary>The display name of the referenced load case (primary or combination).</summary>
    public string Name => LoadCase?.Name ?? CombinationLoadCase!.Name;

    /// <summary>The deduplication key of the referenced load case (primary or combination).</summary>
    public string Key => LoadCase?.Key ?? CombinationLoadCase!.Key;
}

/// <summary>
///     In-memory representation of a moving load scenario. No API call — pure data.
///     A moving load scenario is the container that references vehicles / pressures / travel
///     paths (populated in later slices) and generates a sequence of sub-load-cases via the
///     SpaceGass moving-load engine. This slice covers scenario metadata plus optional
///     combination entries.
/// </summary>
public class SgMovingLoadScenarioData
{
    public SgMovingLoadScenarioData(
        string name,
        SgLoadCaseData? startingLoadCase = null,
        double? timeInterval = null,
        bool include = true,
        IReadOnlyList<SgMovingLoadCombinationEntry>? combinations = null,
        IReadOnlyList<SgMovingLoadData>? loads = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Moving load scenario name cannot be null or empty.", nameof(name));

        Name = name;
        StartingLoadCase = startingLoadCase;
        TimeInterval = timeInterval;
        Include = include;
        Combinations = combinations ?? Array.Empty<SgMovingLoadCombinationEntry>();
        Loads = loads ?? Array.Empty<SgMovingLoadData>();
    }

    /// <summary>The scenario title (e.g., "Truck Left Lane").</summary>
    public string Name { get; }

    /// <summary>Optional load case anchoring the first generated moving-load load case.</summary>
    public SgLoadCaseData? StartingLoadCase { get; }

    /// <summary>Optional time step between generated moving-load load cases (must be &gt; 0 when set).</summary>
    public double? TimeInterval { get; }

    /// <summary>Whether analysis picks up this scenario.</summary>
    public bool Include { get; }

    /// <summary>
    ///     Optional combination entries describing how the scenario's generated moving-load load
    ///     cases combine with existing load cases. Empty when no combinations are attached.
    /// </summary>
    public IReadOnlyList<SgMovingLoadCombinationEntry> Combinations { get; }

    /// <summary>
    ///     The moving loads (vehicle-or-pressure + travel path + factors) that make up this
    ///     scenario. Empty when the scenario has no moving loads attached — Assemble Model
    ///     still creates the scenario but emits a warning.
    /// </summary>
    public IReadOnlyList<SgMovingLoadData> Loads { get; }

    /// <summary>Unique key for deduplication (case-insensitivity enforced by consuming dictionaries).</summary>
    public string Key => Name;
}
