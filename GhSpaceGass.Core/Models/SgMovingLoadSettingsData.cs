namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of the SpaceGass moving-load engine's job-level settings.
///     All fields are optional — an unset field is sent as <c>null</c> on the PATCH payload so
///     SpaceGass keeps its current value for that field. No API call — pure data.
/// </summary>
public class SgMovingLoadSettingsData
{
    public SgMovingLoadSettingsData(
        bool? applyToClosestMember = null,
        bool? checkVerticalProximity = null,
        double? verticalProximity = null,
        bool? ignoreLoadsOnOneMember = null,
        bool? ignoreOutsideLoadedArea = null,
        bool? keepLoadsWithinTravelPath = null,
        bool? retainLoads = null)
    {
        ApplyToClosestMember = applyToClosestMember;
        CheckVerticalProximity = checkVerticalProximity;
        VerticalProximity = verticalProximity;
        IgnoreLoadsOnOneMember = ignoreLoadsOnOneMember;
        IgnoreOutsideLoadedArea = ignoreOutsideLoadedArea;
        KeepLoadsWithinTravelPath = keepLoadsWithinTravelPath;
        RetainLoads = retainLoads;
    }

    /// <summary>Apply each wheel load only to the single closest surrounding member.</summary>
    public bool? ApplyToClosestMember { get; }

    /// <summary>Filter which members / plates receive a load by their vertical distance.</summary>
    public bool? CheckVerticalProximity { get; }

    /// <summary>Maximum vertical distance (in metres) to a receiving member / plate.</summary>
    public double? VerticalProximity { get; }

    /// <summary>Ignore wheels / pressure parts that transfer their load to only one member.</summary>
    public bool? IgnoreLoadsOnOneMember { get; }

    /// <summary>Ignore wheels / pressure parts that fall outside the configured loading area polygon.</summary>
    public bool? IgnoreOutsideLoadedArea { get; }

    /// <summary>Keep each load entirely within the ends of its travel path (crane-rail behaviour).</summary>
    public bool? KeepLoadsWithinTravelPath { get; }

    /// <summary>Retain previously-generated loads for scenarios that are deselected on the next generate run.</summary>
    public bool? RetainLoads { get; }

    /// <summary>True when at least one setting is non-null (i.e. the PATCH would carry a value).</summary>
    public bool HasAnyValue =>
        ApplyToClosestMember.HasValue
        || CheckVerticalProximity.HasValue
        || VerticalProximity.HasValue
        || IgnoreLoadsOnOneMember.HasValue
        || IgnoreOutsideLoadedArea.HasValue
        || KeepLoadsWithinTravelPath.HasValue
        || RetainLoads.HasValue;
}
