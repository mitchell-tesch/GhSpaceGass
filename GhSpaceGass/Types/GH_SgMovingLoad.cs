using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgMovingLoad : GH_Goo<SgMovingLoadData>
{
    public GH_SgMovingLoad()
    {
    }

    public GH_SgMovingLoad(SgMovingLoadData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Moving Load";
    public override string TypeDescription =>
        "A single moving load — one vehicle or one pressure travelling along one travel path — " +
        "wired into a moving load scenario.";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgMovingLoad(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Moving Load";
        var subject = Value.Vehicle != null
            ? $"vehicle {Value.Vehicle.Name}"
            : $"pressure {Value.Pressure!.Name}";
        return $"Moving Load: {subject} on path {Value.TravelPath.Name}";
    }
}
