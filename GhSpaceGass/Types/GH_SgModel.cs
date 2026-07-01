using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgModel : GH_Goo<SgModelData>
{
    public GH_SgModel()
    {
    }

    public GH_SgModel(SgModelData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Model";
    public override string TypeDescription => "A compiled SpaceGass structural model with ID mappings.";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgModel(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Model";
        return $"Model: {Value.NodeMap.Count} nodes, {Value.MemberMap.Count} members, " +
               $"{Value.SectionMap.Count} sections, {Value.MaterialMap.Count} materials";
    }
}