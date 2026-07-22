using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgMovingLoadVehicle : GH_Goo<SgMovingLoadVehicleData>
{
    public GH_SgMovingLoadVehicle()
    {
    }

    public GH_SgMovingLoadVehicle(SgMovingLoadVehicleData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Moving Load Vehicle";
    public override string TypeDescription => "A SpaceGass moving load vehicle definition.";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgMovingLoadVehicle(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Moving Load Vehicle";
        if (Value.IsLibrary) return $"Moving Load Vehicle: {Value.Library} :: {Value.Name}";
        return $"Moving Load Vehicle: {Value.Name} ({Value.WheelLoads.Count} wheels)";
    }
}

public sealed class GH_SgMovingLoadPressure : GH_Goo<SgMovingLoadPressureData>
{
    public GH_SgMovingLoadPressure()
    {
    }

    public GH_SgMovingLoadPressure(SgMovingLoadPressureData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Moving Load Pressure";
    public override string TypeDescription => "A SpaceGass moving load pressure definition.";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgMovingLoadPressure(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Moving Load Pressure";
        return $"Moving Load Pressure: {Value.Name} ({Value.Width}\u00d7{Value.Length})";
    }
}
