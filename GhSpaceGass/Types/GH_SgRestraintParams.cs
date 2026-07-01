using System.Collections.Generic;
using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgRestraintStiffness : GH_Goo<SgRestraintStiffnessData>
{
    public GH_SgRestraintStiffness()
    {
    }

    public GH_SgRestraintStiffness(SgRestraintStiffnessData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Restraint Stiffness";
    public override string TypeDescription => "Spring stiffness parameters for a SpaceGass node restraint.";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgRestraintStiffness(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Restraint Stiffness";
        var parts = new List<string>();
        if (Value.KTx.HasValue) parts.Add($"Kx={Value.KTx:F1}");
        if (Value.KTy.HasValue) parts.Add($"Ky={Value.KTy:F1}");
        if (Value.KTz.HasValue) parts.Add($"Kz={Value.KTz:F1}");
        if (Value.KRx.HasValue) parts.Add($"Kmx={Value.KRx:F1}");
        if (Value.KRy.HasValue) parts.Add($"Kmy={Value.KRy:F1}");
        if (Value.KRz.HasValue) parts.Add($"Kmz={Value.KRz:F1}");
        return parts.Count > 0
            ? $"Restraint Stiffness: {string.Join(", ", parts)}"
            : "Restraint Stiffness: (none)";
    }
}

public sealed class GH_SgRestraintFriction : GH_Goo<SgRestraintFrictionData>
{
    public GH_SgRestraintFriction()
    {
    }

    public GH_SgRestraintFriction(SgRestraintFrictionData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Restraint Friction";
    public override string TypeDescription => "Friction parameters for a SpaceGass node restraint.";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgRestraintFriction(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Restraint Friction";
        var parts = new List<string>();
        if (Value.X != null) parts.Add($"X={Value.X.Factor:F2}");
        if (Value.Y != null) parts.Add($"Y={Value.Y.Factor:F2}");
        if (Value.Z != null) parts.Add($"Z={Value.Z.Factor:F2}");
        return parts.Count > 0
            ? $"Restraint Friction: {string.Join(", ", parts)}"
            : "Restraint Friction: (none)";
    }
}