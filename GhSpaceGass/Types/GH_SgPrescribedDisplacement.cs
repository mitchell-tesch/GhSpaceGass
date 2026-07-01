using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgPrescribedDisplacement : GH_Goo<SgPrescribedDisplacementData>
{
    public GH_SgPrescribedDisplacement()
    {
    }

    public GH_SgPrescribedDisplacement(SgPrescribedDisplacementData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Prescribed Displacement";
    public override string TypeDescription => "A SpaceGass prescribed node displacement (imposed displacement/rotation).";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgPrescribedDisplacement(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Prescribed Displacement";
        return $"Prescribed Disp: ({Value.Point.X:F2},{Value.Point.Y:F2},{Value.Point.Z:F2}) " +
               $"[{Value.LoadCase.Name}] T=({Value.Tx},{Value.Ty},{Value.Tz}) R=({Value.Rx},{Value.Ry},{Value.Rz})";
    }
}

