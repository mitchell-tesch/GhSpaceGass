using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgMemberConcentratedLoad : GH_Goo<SgMemberConcentratedLoadData>
{
    public GH_SgMemberConcentratedLoad()
    {
    }

    public GH_SgMemberConcentratedLoad(SgMemberConcentratedLoadData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Member Concentrated Load";
    public override string TypeDescription => "A SpaceGass member concentrated load (point force/moment along a member).";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgMemberConcentratedLoad(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Concentrated Load";
        return $"Conc. Load: [{Value.LoadCase.Name}] " +
               $"F=({Value.Fx},{Value.Fy},{Value.Fz}) M=({Value.Mx},{Value.My},{Value.Mz}) " +
               $"@ {Value.Position}{(Value.PositionUnits == SpaceGassApi.Models.LoadPositionUnits.Percent ? "%" : "")}";
    }
}

