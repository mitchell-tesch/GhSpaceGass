using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgMemberPrestressLoad : GH_Goo<SgMemberPrestressLoadData>
{
    public GH_SgMemberPrestressLoad()
    {
    }

    public GH_SgMemberPrestressLoad(SgMemberPrestressLoadData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Member Prestress Load";
    public override string TypeDescription => "A SpaceGass member prestress load (axial prestress force on a member).";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgMemberPrestressLoad(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Prestress Load";
        return $"Prestress: [{Value.LoadCase.Name}] P={Value.Prestress}";
    }
}

