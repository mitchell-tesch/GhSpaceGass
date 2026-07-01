using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgMember : GH_Goo<SgMemberData>
{
    public GH_SgMember()
    {
    }

    public GH_SgMember(SgMemberData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Member";
    public override string TypeDescription => "A SpaceGass structural member (beam, truss, etc.).";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgMember(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Member";
        return $"Member: ({Value.Start.X:F2},{Value.Start.Y:F2},{Value.Start.Z:F2}) → " +
               $"({Value.End.X:F2},{Value.End.Y:F2},{Value.End.Z:F2}) " +
               $"[{Value.Section.Name}]";
    }
}