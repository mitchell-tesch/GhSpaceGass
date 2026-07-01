using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgNodeConstraint : GH_Goo<SgNodeConstraintData>
{
    public GH_SgNodeConstraint()
    {
    }

    public GH_SgNodeConstraint(SgNodeConstraintData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Node Constraint";
    public override string TypeDescription => "A SpaceGass node constraint (master-slave link between two nodes).";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgNodeConstraint(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Node Constraint";
        return $"Constraint: ({Value.SlavePoint.X:F2},{Value.SlavePoint.Y:F2},{Value.SlavePoint.Z:F2}) → " +
               $"({Value.MasterPoint.X:F2},{Value.MasterPoint.Y:F2},{Value.MasterPoint.Z:F2}) [{Value.ConstraintCode}]";
    }
}

