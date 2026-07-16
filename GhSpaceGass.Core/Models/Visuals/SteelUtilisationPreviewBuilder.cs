namespace GhSpaceGass.Core.Models.Visuals;

/// <summary>A single steel-design group entry ready for viewport preview.</summary>
public class SteelUtilisationMember
{
    public SteelUtilisationMember(int designGroupId, SgPoint3D start, SgPoint3D end,
        double loadFactor, (int R, int G, int B) rgb)
    {
        DesignGroupId = designGroupId;
        Start = start;
        End = end;
        LoadFactor = loadFactor;
        Rgb = rgb;
    }

    /// <summary>SpaceGass steel-design group identifier for this preview entry.</summary>
    public int DesignGroupId { get; }
    public SgPoint3D Start { get; }
    public SgPoint3D End { get; }

    /// <summary>
    ///     Raw load factor value from the API — capacity / action.
    ///     Values ≥ 1.0 are adequate; values &lt; 1.0 are overloaded.
    /// </summary>
    public double LoadFactor { get; }

    /// <summary>Colour components mapped from the load factor via the traffic-light ramp.</summary>
    public (int R, int G, int B) Rgb { get; }
}

/// <summary>Result of building steel-utilisation preview members.</summary>
public class SteelUtilisationResult
{
    public SteelUtilisationResult(List<SteelUtilisationMember> members)
    {
        Members = members;
    }

    public List<SteelUtilisationMember> Members { get; }
}

/// <summary>
///     Maps steel-design check summaries to coloured member-line preview data.
///     LoadFactor is interpreted per the SpaceGass convention <c>Capacity / Action</c>:
///     values &lt; 1.0 are overloaded (red end of the ramp), values ≥ 1.0 are safe.
///     Colour ramp is a piecewise linear traffic-light:
///     Deep Red (0.5, severe overload) → Red (1.0, at capacity) → Orange (1.11, ~10% margin)
///     → Yellow (1.33, ~33% margin) → Green (2.0, ≥ 100% margin).
///     Values below 0.5 clamp to Deep Red; values above 2.0 clamp to Green;
///     values ≤ 0 are skipped as malformed (no reported capacity).
///     Pure data transformation — no Rhino dependency.
/// </summary>
public static class SteelUtilisationPreviewBuilder
{
    // Anchor stops: (loadFactor, R, G, B). Sorted ascending by loadFactor.
    private static readonly (double LoadFactor, int R, int G, int B)[] Anchors =
    {
        (0.50, 183, 28, 28),   // Deep Red — severely overloaded
        (1.00, 244, 67, 54),   // Red — at capacity
        (1.11, 255, 152, 0),   // Orange — ~10% margin
        (1.33, 255, 235, 59),  // Yellow — ~33% margin
        (2.00, 76, 175, 80)    // Green — plenty of margin
    };

    public static SteelUtilisationResult Build(
        IReadOnlyList<SgSteelMemberCheckData> checks,
        IReadOnlyDictionary<int, (SgPoint3D Start, SgPoint3D End)> memberMap)
    {
        var members = new List<SteelUtilisationMember>();
        if (checks.Count == 0) return new SteelUtilisationResult(members);

        foreach (var c in checks)
        {
            if (c.LoadFactor <= 0) continue;
            if (!memberMap.TryGetValue(c.DesignGroupId, out var geom)) continue;

            var rgb = MapColor(c.LoadFactor);
            members.Add(new SteelUtilisationMember(c.DesignGroupId, geom.Start, geom.End, c.LoadFactor, rgb));
        }

        return new SteelUtilisationResult(members);
    }

    /// <summary>
    ///     Maps a load factor (capacity/action) to a colour on the traffic-light ramp.
    ///     Values &lt; 0.5 clamp to Deep Red; values &gt; 2.0 clamp to Green.
    /// </summary>
    public static (int R, int G, int B) MapColor(double loadFactor)
    {
        var first = Anchors[0];
        if (loadFactor <= first.LoadFactor)
            return (first.R, first.G, first.B);

        var last = Anchors[Anchors.Length - 1];
        if (loadFactor >= last.LoadFactor)
            return (last.R, last.G, last.B);

        for (var i = 1; i < Anchors.Length; i++)
        {
            var hi = Anchors[i];
            if (loadFactor <= hi.LoadFactor)
            {
                var lo = Anchors[i - 1];
                var t = (loadFactor - lo.LoadFactor) / (hi.LoadFactor - lo.LoadFactor);
                var r = (int)Math.Round(lo.R + t * (hi.R - lo.R));
                var g = (int)Math.Round(lo.G + t * (hi.G - lo.G));
                var b = (int)Math.Round(lo.B + t * (hi.B - lo.B));
                return (r, g, b);
            }
        }

        return (last.R, last.G, last.B);
    }
}

