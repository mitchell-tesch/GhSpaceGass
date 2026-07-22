namespace GhSpaceGass.Core.Models;

/// <summary>
///     A single station along a moving-load travel path. The <see cref="Position"/> is an
///     absolute point in the model's coordinate space (not a reference to any structural node);
///     <see cref="Radius"/>, when set, is the arc radius of the segment from the previous
///     station to this one. The first station in a path has no meaningful radius.
/// </summary>
public class SgMovingLoadStationData
{
    public SgMovingLoadStationData(SgPoint3D position, double? radius = null)
    {
        Position = position;
        Radius = radius;
    }

    /// <summary>Absolute station coordinates in the model's coordinate space.</summary>
    public SgPoint3D Position { get; }

    /// <summary>
    ///     Optional arc radius of the segment from the previous station to this one. Null means
    ///     a straight segment. Meaningless (and ignored by SpaceGass) on the first station of a
    ///     path.
    /// </summary>
    public double? Radius { get; }
}

/// <summary>
///     In-memory representation of a moving-load travel path — a named polyline the SpaceGass
///     moving-load engine drags a vehicle or pressure along. Stations are stored in path order
///     and every path needs at least two of them. No API call — pure data.
/// </summary>
public class SgMovingLoadTravelPathData
{
    public SgMovingLoadTravelPathData(string name, IReadOnlyList<SgMovingLoadStationData> stations)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Moving load travel path name cannot be null or empty.", nameof(name));
        if (stations == null || stations.Count < 2)
            throw new ArgumentException(
                "A moving load travel path needs at least two stations.", nameof(stations));

        Name = name;
        Stations = stations;
    }

    public string Name { get; }
    public IReadOnlyList<SgMovingLoadStationData> Stations { get; }

    /// <summary>Deduplication key. Case-insensitivity enforced by consuming dictionaries.</summary>
    public string Key => Name;
}
