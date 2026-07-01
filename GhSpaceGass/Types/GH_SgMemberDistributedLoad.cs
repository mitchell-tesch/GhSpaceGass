using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgMemberDistributedLoad : GH_Goo<SgMemberDistributedLoadData>
{
    public GH_SgMemberDistributedLoad()
    {
    }

    public GH_SgMemberDistributedLoad(SgMemberDistributedLoadData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Member Distributed Load";
    public override string TypeDescription => "A SpaceGass member distributed load.";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgMemberDistributedLoad(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Distributed Load";
        var parts = new System.Collections.Generic.List<string> { $"Dist. Load: [{Value.LoadCase.Name}]" };
        if (Value.HasForces)
            parts.Add($"F=({Value.FxStart:F2},{Value.FyStart:F2},{Value.FzStart:F2})→({Value.FxEnd:F2},{Value.FyEnd:F2},{Value.FzEnd:F2})");
        if (Value.HasMoments)
            parts.Add($"M=({Value.MxStart:F2},{Value.MyStart:F2},{Value.MzStart:F2})→({Value.MxEnd:F2},{Value.MyEnd:F2},{Value.MzEnd:F2})");
        if (!Value.HasForces && !Value.HasMoments)
            parts.Add("(zero)");
        return string.Join(" ", parts);
    }
}