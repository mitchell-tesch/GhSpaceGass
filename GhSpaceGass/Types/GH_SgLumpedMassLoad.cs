using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgLumpedMassLoad : GH_Goo<SgLumpedMassLoadData>
{
    public GH_SgLumpedMassLoad()
    {
    }

    public GH_SgLumpedMassLoad(SgLumpedMassLoadData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Lumped Mass Load";
    public override string TypeDescription => "A SpaceGass lumped mass load (mass at a node).";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgLumpedMassLoad(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Lumped Mass Load";
        return $"Lumped Mass: ({Value.Point.X:F2},{Value.Point.Y:F2},{Value.Point.Z:F2}) " +
               $"[{Value.LoadCase.Name}] T=({Value.Tmx},{Value.Tmy},{Value.Tmz}) R=({Value.Rmx},{Value.Rmy},{Value.Rmz})";
    }
}

