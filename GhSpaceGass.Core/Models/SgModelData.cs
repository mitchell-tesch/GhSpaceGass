namespace GhSpaceGass.Core.Models;

/// <summary>
///     The compiled model returned by Assemble Model.
///     Holds the ID ↔ geometry mappings created by SpaceGass.
///     Flows downstream to Analysis and Results components.
/// </summary>
public class SgModelData
{
    /// <summary>
    ///     Point → SpaceGass node ID.
    ///     Keys are canonical points from the spatial deduplication grid.
    ///     Use <see cref="TryGetNodeId"/> for tolerance-based lookup from external points.
    /// </summary>
    public Dictionary<SgPoint3D, int> NodeMap { get; } = new();

    /// <summary>SpaceGass member ID → (start, end) geometry.</summary>
    public Dictionary<int, (SgPoint3D Start, SgPoint3D End)> MemberMap { get; } = new();

    /// <summary>SpaceGass plate ID → corner points.</summary>
    public Dictionary<int, SgPoint3D[]> PlateMap { get; } = new();

    /// <summary>Section key → SpaceGass section ID.</summary>
    public Dictionary<string, int> SectionMap { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Material key → SpaceGass material ID.</summary>
    public Dictionary<string, int> MaterialMap { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Point → restraint code applied at that node.</summary>
    public Dictionary<SgPoint3D, string> RestraintMap { get; } = new();

    /// <summary>Total number of node constraints pushed to SpaceGass.</summary>
    public int ConstraintCount { get; set; }

    /// <summary>Load case name → SpaceGass load case ID.</summary>
    public Dictionary<string, int> LoadCaseMap { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Combination load case name → SpaceGass load case ID.</summary>
    public Dictionary<string, int> CombinationLoadCaseMap { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Load category name → SpaceGass load category ID.</summary>
    public Dictionary<string, int> LoadCategoryMap { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Total number of node loads pushed to SpaceGass.</summary>
    public int NodeLoadCount { get; set; }

    /// <summary>Total number of member distributed loads pushed to SpaceGass.</summary>
    public int MemberDistributedLoadCount { get; set; }

    /// <summary>Total number of member distributed moments pushed to SpaceGass.</summary>
    public int MemberDistributedMomentCount { get; set; }

    /// <summary>Total number of member concentrated loads pushed to SpaceGass.</summary>
    public int MemberConcentratedLoadCount { get; set; }

    /// <summary>Total number of member prestress loads pushed to SpaceGass.</summary>
    public int MemberPrestressLoadCount { get; set; }

    /// <summary>Total number of self-weight loads pushed to SpaceGass.</summary>
    public int SelfWeightLoadCount { get; set; }

    /// <summary>Total number of lumped mass loads pushed to SpaceGass.</summary>
    public int LumpedMassLoadCount { get; set; }

    /// <summary>Total number of prescribed displacements pushed to SpaceGass.</summary>
    public int PrescribedDisplacementCount { get; set; }

    /// <summary>Total number of plate pressure loads pushed to SpaceGass.</summary>
    public int PlatePressureLoadCount { get; set; }

    /// <summary>Total number of thermal loads pushed to SpaceGass.</summary>
    public int ThermalLoadCount { get; set; }

    /// <summary>Moving load scenario name → SpaceGass scenario ID.</summary>
    public Dictionary<string, int> MovingLoadScenarioMap { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Moving load vehicle key (Library::Name for library vehicles, Name for user vehicles) → SpaceGass vehicle ID.</summary>
    public Dictionary<string, int> MovingLoadVehicleMap { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Moving load pressure name → SpaceGass pressure ID.</summary>
    public Dictionary<string, int> MovingLoadPressureMap { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Moving load travel path name → SpaceGass travel path ID.</summary>
    public Dictionary<string, int> MovingLoadTravelPathMap { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<int, string> BuildLoadCaseIdToNameMap()
    {
        var map = new Dictionary<int, string>();
        foreach (var kvp in LoadCaseMap) map[kvp.Value] = kvp.Key;
        foreach (var kvp in CombinationLoadCaseMap) map.TryAdd(kvp.Value, kvp.Key);
        return map;
    }

    /// <summary>
    ///     Tolerance-based node lookup. Tries exact match first (O(1)), then falls back
    ///     to a linear scan with coincidence check. Returns false if no match within tolerance.
    /// </summary>
    public bool TryGetNodeId(SgPoint3D point, out int nodeId, double tolerance = 0)
    {
        // Fast path: exact match (works when point came from the same canonical source)
        if (NodeMap.TryGetValue(point, out nodeId))
            return true;

        // Slow path: tolerance-based scan for externally-sourced points
        if (tolerance > 0)
        {
            foreach (var kvp in NodeMap)
            {
                if (kvp.Key.IsCoincident(point, tolerance))
                {
                    nodeId = kvp.Value;
                    return true;
                }
            }
        }

        nodeId = default;
        return false;
    }
}

/// <summary>
///     Result of the assembly process, including the model and any warnings.
/// </summary>
public class AssemblyResult
{
    public AssemblyResult(SgModelData model)
    {
        Model = model;
    }

    public SgModelData Model { get; }
    public List<string> Warnings { get; } = new();
}