namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a member prestress load.
///     No API call — pure data. Applies an axial prestress force to a member.
/// </summary>
public class SgMemberPrestressLoadData
{
    public SgMemberPrestressLoadData(
        SgPoint3D memberStart,
        SgPoint3D memberEnd,
        SgLoadCaseData loadCase,
        double prestress = 0,
        SgLoadCategoryData? loadCategory = null)
    {
        MemberStart = memberStart;
        MemberEnd = memberEnd;
        LoadCase = loadCase ?? throw new ArgumentNullException(nameof(loadCase));
        Prestress = prestress;
        LoadCategory = loadCategory;
    }

    /// <summary>Start point of the member this load is applied to.</summary>
    public SgPoint3D MemberStart { get; }

    /// <summary>End point of the member this load is applied to.</summary>
    public SgPoint3D MemberEnd { get; }

    /// <summary>The load case this prestress load belongs to.</summary>
    public SgLoadCaseData LoadCase { get; }

    /// <summary>Optional load category for this prestress load.</summary>
    public SgLoadCategoryData? LoadCategory { get; }

    /// <summary>The prestress force applied to the member.</summary>
    public double Prestress { get; }

    /// <summary>Returns true if the prestress value is zero.</summary>
    public bool IsZero => Prestress == 0;
}

