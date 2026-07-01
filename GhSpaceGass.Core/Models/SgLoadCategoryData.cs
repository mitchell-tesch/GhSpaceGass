namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a load category. No API call — pure data.
///     Load categories group load cases (e.g., "Dead", "Live", "Wind").
/// </summary>
public class SgLoadCategoryData
{
    public SgLoadCategoryData(string name, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Load category name cannot be null or empty.", nameof(name));

        Name = name;
        Notes = notes;
    }

    /// <summary>The load category title (e.g., "Dead", "Live", "Wind").</summary>
    public string Name { get; }

    /// <summary>Optional descriptive notes for the load category.</summary>
    public string? Notes { get; }

    /// <summary>Unique key for deduplication: case-insensitive name.</summary>
    public string Key => Name;
}