using System.Linq;
using GhSpaceGass.Core.Models;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

public sealed class GH_SgRelease : GH_Goo<SgReleaseData>
{
    public GH_SgRelease()
    {
    }

    public GH_SgRelease(SgReleaseData value)
    {
        Value = value;
    }

    public override bool IsValid => Value != null;
    public override string TypeName => "SpaceGass Release";

    public override string TypeDescription =>
        "A SpaceGass member end release (per-DOF fixity with optional stiffness).";

    public override IGH_Goo Duplicate()
    {
        return new GH_SgRelease(Value);
    }

    public override string ToString()
    {
        if (Value == null) return "Null Release";

        var stiffnessParts = new[]
        {
            Value.KTx.HasValue ? $"Kx={Value.KTx:F2}" : null,
            Value.KTy.HasValue ? $"Ky={Value.KTy:F2}" : null,
            Value.KTz.HasValue ? $"Kz={Value.KTz:F2}" : null,
            Value.KRx.HasValue ? $"Kmx={Value.KRx:F2}" : null,
            Value.KRy.HasValue ? $"Kmy={Value.KRy:F2}" : null,
            Value.KRz.HasValue ? $"Kmz={Value.KRz:F2}" : null
        }.Where(s => s != null);

        var stiffness = string.Join(", ", stiffnessParts);
        return string.IsNullOrEmpty(stiffness)
            ? $"Release: {Value.ReleaseCode}"
            : $"Release: {Value.ReleaseCode} ({stiffness})";
    }
}