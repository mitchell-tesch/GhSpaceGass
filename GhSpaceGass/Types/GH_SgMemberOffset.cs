using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgMemberOffset : GH_Goo<SgMemberOffsetData>
{
    public GH_SgMemberOffset()
    {
    }

    public GH_SgMemberOffset(SgMemberOffsetData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Member Offset";
    public override string TypeDescription => "A SpaceGass member offset (rigid offset at each end of a member).";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgMemberOffset(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Member Offset";
        if (Value.IsZero) return "Member Offset: (zero)";
        return $"Member Offset: A=({Value.XOffsetAtA},{Value.YOffsetAtA},{Value.ZOffsetAtA}) " +
               $"B=({Value.XOffsetAtB},{Value.YOffsetAtB},{Value.ZOffsetAtB})";
    }
}

