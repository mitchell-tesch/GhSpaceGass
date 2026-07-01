using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgRestraint : GH_Goo<SgRestraintData>
{
    public GH_SgRestraint()
    {
    }

    public GH_SgRestraint(SgRestraintData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Restraint";
    public override string TypeDescription => "A SpaceGass node restraint (boundary condition).";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgRestraint(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Restraint";
        return $"Restraint: ({Value.Point.X:F2},{Value.Point.Y:F2},{Value.Point.Z:F2}) [{Value.RestraintCode}]";
    }
}