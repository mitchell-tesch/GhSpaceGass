namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a load case. No API call — pure data.
/// </summary>
public class SgLoadCaseData
{
    public SgLoadCaseData(string name, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Load case name cannot be null or empty.", nameof(name));

        Name = name;
        Notes = notes;
    }

    /// <summary>The load case title (e.g., "Dead Load", "Live Load").</summary>
    public string Name { get; }

    /// <summary>Optional descriptive notes for the load case.</summary>
    public string? Notes { get; }

    /// <summary>Unique key for deduplication (case-insensitivity enforced by consuming dictionaries).</summary>
    public string Key => Name;
}