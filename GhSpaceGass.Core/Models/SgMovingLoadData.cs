using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Models;

/// <summary>
///     A single moving-load entry inside a moving load scenario — one vehicle or one pressure
///     travelling along one travel path, with optional speed / start / delay / factor overrides.
///     Exactly one of <see cref="Vehicle"/> or <see cref="Pressure"/> must be set; the
///     <see cref="LoadType"/> is derived from whichever is populated.
/// </summary>
public class SgMovingLoadData
{
    public SgMovingLoadData(
        SgMovingLoadTravelPathData travelPath,
        SgMovingLoadVehicleData? vehicle = null,
        SgMovingLoadPressureData? pressure = null,
        double? speed = null,
        double? startPosition = null,
        double? delay = null,
        double? loadFactor = null,
        double? laneFactor = null,
        double? dynamicFactor = null,
        MovingLoadStationaryOption? generateStationaryLc = null)
    {
        TravelPath = travelPath ?? throw new ArgumentNullException(nameof(travelPath));

        if (vehicle == null && pressure == null)
            throw new ArgumentException(
                "A moving load must reference either a vehicle or a pressure.");
        if (vehicle != null && pressure != null)
            throw new ArgumentException(
                "A moving load must reference either a vehicle or a pressure, not both.");
        if (startPosition is < 0)
            throw new ArgumentException(
                "Moving load start position cannot be negative.", nameof(startPosition));

        Vehicle = vehicle;
        Pressure = pressure;
        Speed = speed;
        StartPosition = startPosition;
        Delay = delay;
        LoadFactor = loadFactor;
        LaneFactor = laneFactor;
        DynamicFactor = dynamicFactor;
        GenerateStationaryLc = generateStationaryLc;
    }

    /// <summary>The travel path this moving load runs along.</summary>
    public SgMovingLoadTravelPathData TravelPath { get; }

    /// <summary>The vehicle running along the travel path. Null when <see cref="Pressure"/> is set.</summary>
    public SgMovingLoadVehicleData? Vehicle { get; }

    /// <summary>The pressure patch running along the travel path. Null when <see cref="Vehicle"/> is set.</summary>
    public SgMovingLoadPressureData? Pressure { get; }

    public double? Speed { get; }
    public double? StartPosition { get; }
    public double? Delay { get; }
    public double? LoadFactor { get; }
    public double? LaneFactor { get; }
    public double? DynamicFactor { get; }
    public MovingLoadStationaryOption? GenerateStationaryLc { get; }

    /// <summary>Whether this entry is a vehicle load or a pressure load.</summary>
    public MovingLoadType LoadType => Vehicle != null ? MovingLoadType.Vehicle : MovingLoadType.Pressure;
}
