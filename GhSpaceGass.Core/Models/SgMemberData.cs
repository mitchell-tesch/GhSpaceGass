using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a structural member. No API call — pure data.
///     References geometry (start/end points) and section/material by Goo reference.
/// </summary>
public class SgMemberData
{
    public SgMemberData(
        SgPoint3D start,
        SgPoint3D end,
        SgSectionData section,
        SgMaterialData material,
        MemberType? type = null,
        SgReleaseData? releaseA = null,
        SgReleaseData? releaseB = null,
        SgDirectionData? direction = null,
        SgMemberOffsetData? offset = null)
    {
        Start = start;
        End = end;
        Section = section ?? throw new ArgumentNullException(nameof(section));
        Material = material ?? throw new ArgumentNullException(nameof(material));
        Type = type;
        ReleaseA = releaseA;
        ReleaseB = releaseB;
        Direction = direction;
        Offset = offset;
    }

    public SgPoint3D Start { get; }
    public SgPoint3D End { get; }
    public SgSectionData Section { get; }
    public SgMaterialData Material { get; }
    public MemberType? Type { get; }
    public SgReleaseData? ReleaseA { get; }
    public SgReleaseData? ReleaseB { get; }
    public SgDirectionData? Direction { get; }
    public SgMemberOffsetData? Offset { get; }
}