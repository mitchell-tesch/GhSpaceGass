using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgSelfWeightLoad : GH_Goo<SgSelfWeightLoadData>
{
    public GH_SgSelfWeightLoad()
    {
    }

    public GH_SgSelfWeightLoad(SgSelfWeightLoadData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Self-Weight Load";
    public override string TypeDescription => "A SpaceGass self-weight load.";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgSelfWeightLoad(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Self-Weight Load";
        return $"Self-Weight: [{Value.LoadCase.Name}] " +
               $"Acc=({Value.AccelerationX},{Value.AccelerationY},{Value.AccelerationZ})";
    }
}