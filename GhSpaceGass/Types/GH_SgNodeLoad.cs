using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgNodeLoad : GH_Goo<SgNodeLoadData>
{
    public GH_SgNodeLoad()
    {
    }

    public GH_SgNodeLoad(SgNodeLoadData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Node Load";
    public override string TypeDescription => "A SpaceGass node load (concentrated force/moment).";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgNodeLoad(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Node Load";
        return $"Node Load: ({Value.Point.X:F2},{Value.Point.Y:F2},{Value.Point.Z:F2}) " +
               $"[{Value.LoadCase.Name}] F=({Value.Fx},{Value.Fy},{Value.Fz}) M=({Value.Mx},{Value.My},{Value.Mz})";
    }
}