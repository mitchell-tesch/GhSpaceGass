namespace GhSpaceGass.Core.Models;

/// <summary>
///     Result of querying all node-based loads from SpaceGass.
///     Grouped by unique node — each node entry contains its node loads,
///     lumped mass loads, and prescribed displacements.
/// </summary>
public class SgNodeLoadsDataResult
{
    /// <summary>One entry per unique node that has any load, ordered by node ID.</summary>
    public List<SgNodeLoadEntry> NodeEntries { get; } = new();

    public List<string> Warnings { get; } = new();
}

/// <summary>All loads applied at a single node.</summary>
public class SgNodeLoadEntry
{
    public SgNodeLoadEntry(int nodeId, SgPoint3D point)
    {
        NodeId = nodeId;
        Point = point;
    }

    public int NodeId { get; }
    public SgPoint3D Point { get; }
    public List<SgNodeLoadInfo> NodeLoads { get; } = new();
    public List<SgLumpedMassLoadInfo> LumpedMassLoads { get; } = new();
    public List<SgPrescribedDisplacementInfo> PrescribedDisplacements { get; } = new();
}

/// <summary>A single node load entry.</summary>
public readonly record struct SgNodeLoadInfo(
    int LoadCaseId,
    string LoadCaseName,
    int LoadCategoryId,
    double Fx, double Fy, double Fz,
    double Mx, double My, double Mz);

/// <summary>A single lumped mass load entry.</summary>
public readonly record struct SgLumpedMassLoadInfo(
    int LoadCaseId,
    string LoadCaseName,
    int LoadCategoryId,
    double Tmx, double Tmy, double Tmz,
    double Rmx, double Rmy, double Rmz);

/// <summary>A single prescribed displacement entry.</summary>
public readonly record struct SgPrescribedDisplacementInfo(
    int LoadCaseId,
    string LoadCaseName,
    int LoadCategoryId,
    double Tx, double Ty, double Tz,
    double Rx, double Ry, double Rz);
