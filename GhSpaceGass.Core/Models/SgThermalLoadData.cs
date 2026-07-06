using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a thermal load (temperature change + gradients).
///     Applies to either a member or a plate element. No API call — pure data.
/// </summary>
public class SgThermalLoadData
{
    private SgThermalLoadData(
        ThermalElementType elementType,
        SgLoadCaseData loadCase,
        double thermalLoad,
        double yGradient,
        double zGradient,
        SgLoadCategoryData? loadCategory,
        SgPoint3D? memberStart,
        SgPoint3D? memberEnd,
        SgPoint3D[]? plateNodes)
    {
        ElementType = elementType;
        LoadCase = loadCase ?? throw new ArgumentNullException(nameof(loadCase));
        ThermalLoad = thermalLoad;
        YGradient = yGradient;
        ZGradient = zGradient;
        LoadCategory = loadCategory;
        MemberStart = memberStart;
        MemberEnd = memberEnd;
        PlateNodes = (SgPoint3D[]?)plateNodes?.Clone();
    }

    /// <summary>Creates a thermal load for a member.</summary>
    public static SgThermalLoadData ForMember(
        SgPoint3D memberStart, SgPoint3D memberEnd, SgLoadCaseData loadCase,
        double thermalLoad = 0, double yGradient = 0, double zGradient = 0,
        SgLoadCategoryData? loadCategory = null)
    {
        return new SgThermalLoadData(ThermalElementType.Member, loadCase,
            thermalLoad, yGradient, zGradient, loadCategory,
            memberStart, memberEnd, null);
    }

    /// <summary>Creates a thermal load for a plate element.</summary>
    public static SgThermalLoadData ForPlate(
        SgPoint3D[] plateNodes, SgLoadCaseData loadCase,
        double thermalLoad = 0, double yGradient = 0, double zGradient = 0,
        SgLoadCategoryData? loadCategory = null)
    {
        return new SgThermalLoadData(ThermalElementType.Plate, loadCase,
            thermalLoad, yGradient, zGradient, loadCategory,
            null, null, plateNodes);
    }

    /// <summary>Whether this thermal load applies to a Member or Plate.</summary>
    public ThermalElementType ElementType { get; }

    /// <summary>Start point of the member (Member type only).</summary>
    public SgPoint3D? MemberStart { get; }

    /// <summary>End point of the member (Member type only).</summary>
    public SgPoint3D? MemberEnd { get; }

    /// <summary>Corner nodes of the plate (Plate type only).</summary>
    public SgPoint3D[]? PlateNodes { get; }

    /// <summary>The load case this thermal load belongs to.</summary>
    public SgLoadCaseData LoadCase { get; }

    /// <summary>Optional load category.</summary>
    public SgLoadCategoryData? LoadCategory { get; }

    /// <summary>Uniform temperature change.</summary>
    public double ThermalLoad { get; }

    /// <summary>Thermal gradient in Y direction.</summary>
    public double YGradient { get; }

    /// <summary>Thermal gradient in Z direction.</summary>
    public double ZGradient { get; }

    /// <summary>Returns true if all thermal values are zero.</summary>
    public bool IsZero => ThermalLoad == 0 && YGradient == 0 && ZGradient == 0;
}

