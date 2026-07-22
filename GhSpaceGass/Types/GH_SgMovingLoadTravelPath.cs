using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgMovingLoadTravelPath : GH_Goo<SgMovingLoadTravelPathData>
{
    public GH_SgMovingLoadTravelPath()
    {
    }

    public GH_SgMovingLoadTravelPath(SgMovingLoadTravelPathData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Moving Load Travel Path";
    public override string TypeDescription => "A SpaceGass moving load travel path definition.";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgMovingLoadTravelPath(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Moving Load Travel Path";
        return $"Moving Load Travel Path: {Value.Name} ({Value.Stations.Count} stations)";
    }
}
