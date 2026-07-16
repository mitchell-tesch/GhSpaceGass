namespace GhSpaceGass.Core.Models;

/// <summary>
///     A single steel-design check summary record for one steel design group — the result of
///     SpaceGass's steel design check aggregated to the critical load case and the governing
///     member within the group.
///     <para>
///     Note (API limitation): the SpaceGass Steel Member Check Summary endpoint reports one
///     record per design group, not one per physical member. The <see cref="DesignGroupId"/>
///     value is the group identifier as returned by the API. Resolving a design group to its
///     individual members is deferred to a future slice.
///     </para>
/// </summary>
public class SgSteelMemberCheckData
{
    public SgSteelMemberCheckData(
        int designGroupId,
        string section,
        string flag,
        double loadFactor,
        int? criticalCaseId,
        string failureMode,
        double segmentLength,
        double totalLength,
        double yield)
    {
        DesignGroupId = designGroupId;
        Section = section;
        Flag = flag;
        LoadFactor = loadFactor;
        CriticalCaseId = criticalCaseId;
        FailureMode = failureMode;
        SegmentLength = segmentLength;
        TotalLength = totalLength;
        Yield = yield;
    }

    /// <summary>
    ///     SpaceGass steel-design group identifier — the value returned in the API's `Member` field
    ///     on `SteelCheckSummary`. This aggregates a group of one or more physical members that
    ///     share the same steel-design definition.
    /// </summary>
    public int DesignGroupId { get; }

    /// <summary>Section name assigned to the design group (as reported by the design engine).</summary>
    public string Section { get; }

    /// <summary>Status flag — typically "PASS" or "FAIL" from the design engine.</summary>
    public string Flag { get; }

    /// <summary>
    ///     Design capacity ratio for the critical load case: <c>Capacity / Action</c>.
    ///     Values ≥ 1.0 indicate an adequate group (member has more capacity than the applied action);
    ///     values &lt; 1.0 indicate an overloaded group (action exceeds capacity — a design failure).
    /// </summary>
    public double LoadFactor { get; }

    /// <summary>SpaceGass load case ID that governs the design (null if not reported).</summary>
    public int? CriticalCaseId { get; }

    /// <summary>Description of the governing failure mode (e.g. "Combined bending &amp; axial").</summary>
    public string FailureMode { get; }

    /// <summary>Length of the critical segment along the governing member.</summary>
    public double SegmentLength { get; }

    /// <summary>Total length of the governing member.</summary>
    public double TotalLength { get; }

    /// <summary>Yield stress of the steel section.</summary>
    public double Yield { get; }
}

