namespace GhSpaceGass.Core.Models.Visuals;

/// <summary>A single mode-shape polyline for one (load case, mode, member) tuple.</summary>
public class ModeShapePolyline
{
    public ModeShapePolyline(
        List<SgPoint3D> points,
        int loadCaseId,
        int mode,
        double? frequency,
        int paletteIndex,
        double maxResultant,
        SgPoint3D peakPoint)
    {
        Points = points;
        LoadCaseId = loadCaseId;
        Mode = mode;
        Frequency = frequency;
        PaletteIndex = paletteIndex;
        MaxResultant = maxResultant;
        PeakPoint = peakPoint;
    }

    /// <summary>Deformed member endpoints (start, end).</summary>
    public List<SgPoint3D> Points { get; }

    public int LoadCaseId { get; }
    public int Mode { get; }

    /// <summary>Natural frequency in Hz for the (load case, mode) — null if not available.</summary>
    public double? Frequency { get; }

    /// <summary>Palette colour slot for this mode. Cycles every 8 modes.</summary>
    public int PaletteIndex { get; }

    /// <summary>Maximum resultant mode-shape translation across the two endpoints of this member.</summary>
    public double MaxResultant { get; }

    /// <summary>
    ///     The deformed endpoint (start or end) that has the higher resultant translation on this polyline.
    ///     Used as the label anchor when identifying the mode in the viewport.
    /// </summary>
    public SgPoint3D PeakPoint { get; }
}

/// <summary>Result of building mode-shape polylines.</summary>
public class ModeShapeResult
{
    public ModeShapeResult(List<ModeShapePolyline> polylines, double computedScale)
    {
        Polylines = polylines;
        ComputedScale = computedScale;
    }

    /// <summary>One polyline per (load case, mode, member) triple with a non-zero deformation.</summary>
    public List<ModeShapePolyline> Polylines { get; }

    public double ComputedScale { get; }
}

/// <summary>
///     Builds deformed-shape polylines from dynamic-frequency mode-shape data.
///     One line per (load case, mode, member): from start + (Tx,Ty,Tz)×scale to end + (Tx,Ty,Tz)×scale.
///     Pure data transformation — no Rhino dependency.
/// </summary>
public static class ModeShapeBuilder
{
    private const int PaletteSize = 8;

    /// <summary>
    ///     Build deformed member polylines from mode-shape node data.
    /// </summary>
    /// <param name="modeShapes">Mode-shape node results to visualise.</param>
    /// <param name="naturalFrequencies">Natural frequencies used to look up Hz values for the label.</param>
    /// <param name="memberMap">SpaceGass member ID → (start, end) point geometry.</param>
    /// <param name="nodeIdToPoint">SpaceGass node ID → point location.</param>
    /// <param name="bboxDiagonal">Model bounding box diagonal for auto-scale.</param>
    /// <param name="userScale">Optional user-provided scale override. Null = auto-scale. Zero/negative = preview disabled.</param>
    public static ModeShapeResult Build(
        IReadOnlyList<SgModeShapeNodeData> modeShapes,
        IReadOnlyList<SgNaturalFrequencyData> naturalFrequencies,
        IReadOnlyDictionary<int, (SgPoint3D Start, SgPoint3D End)> memberMap,
        IReadOnlyDictionary<int, SgPoint3D> nodeIdToPoint,
        double bboxDiagonal,
        double? userScale)
    {
        if (modeShapes.Count == 0 || (userScale.HasValue && userScale.Value <= 0))
            return new ModeShapeResult(new List<ModeShapePolyline>(), 1.0);

        var maxMag = 0.0;
        foreach (var m in modeShapes)
        {
            var mag = Math.Sqrt(m.Tx * m.Tx + m.Ty * m.Ty + m.Tz * m.Tz);
            if (mag > maxMag) maxMag = mag;
        }

        var scale = userScale ?? PreviewScaleHelper.ComputeAutoScale(bboxDiagonal, maxMag);

        var frequencyLookup = new Dictionary<(int LoadCaseId, int Mode), double>();
        foreach (var f in naturalFrequencies)
            frequencyLookup[(f.LoadCaseId, f.Mode)] = f.Frequency;

        var byGroup = new Dictionary<(int LoadCaseId, int Mode), Dictionary<int, SgModeShapeNodeData>>();
        foreach (var ms in modeShapes)
        {
            var key = (ms.LoadCaseId, ms.Mode);
            if (!byGroup.TryGetValue(key, out var nodeLookup))
            {
                nodeLookup = new Dictionary<int, SgModeShapeNodeData>();
                byGroup[key] = nodeLookup;
            }
            nodeLookup[ms.NodeId] = ms;
        }

        var pointToNodeId = new Dictionary<SgPoint3D, int>();
        foreach (var kvp in nodeIdToPoint)
            pointToNodeId[kvp.Value] = kvp.Key;

        var polylines = new List<ModeShapePolyline>();

        var orderedGroups = byGroup
            .OrderBy(g => g.Key.LoadCaseId)
            .ThenBy(g => g.Key.Mode);

        foreach (var group in orderedGroups)
        {
            var (loadCaseId, mode) = group.Key;
            var paletteIndex = ComputePaletteIndex(mode);
            double? frequency = frequencyLookup.TryGetValue(group.Key, out var f) ? f : null;
            var nodeLookup = group.Value;

            var orderedMembers = memberMap
                .OrderBy(m => m.Key);

            foreach (var member in orderedMembers)
            {
                var start = member.Value.Start;
                var end = member.Value.End;

                if (!pointToNodeId.TryGetValue(start, out var startNodeId) ||
                    !pointToNodeId.TryGetValue(end, out var endNodeId))
                    continue;

                nodeLookup.TryGetValue(startNodeId, out var startShape);
                nodeLookup.TryGetValue(endNodeId, out var endShape);

                var startMag = ResultantMagnitude(startShape);
                var endMag = ResultantMagnitude(endShape);
                if (startMag < 1e-15 && endMag < 1e-15)
                    continue;

                var startPt = ApplyDisplacement(start, startShape, scale);
                var endPt = ApplyDisplacement(end, endShape, scale);
                var maxResultant = Math.Max(startMag, endMag);
                var peakPoint = endMag >= startMag ? endPt : startPt;

                polylines.Add(new ModeShapePolyline(
                    new List<SgPoint3D> { startPt, endPt },
                    loadCaseId, mode, frequency, paletteIndex, maxResultant, peakPoint));
            }
        }

        return new ModeShapeResult(polylines, scale);
    }

    private static double ResultantMagnitude(SgModeShapeNodeData? shape)
    {
        if (shape == null) return 0.0;
        return Math.Sqrt(shape.Tx * shape.Tx + shape.Ty * shape.Ty + shape.Tz * shape.Tz);
    }

    private static SgPoint3D ApplyDisplacement(SgPoint3D origin, SgModeShapeNodeData? shape, double scale)
    {
        if (shape == null) return origin;
        return new SgPoint3D(
            origin.X + shape.Tx * scale,
            origin.Y + shape.Ty * scale,
            origin.Z + shape.Tz * scale);
    }

    private static int ComputePaletteIndex(int mode)
    {
        if (mode < 1) return 0;
        return (mode - 1) % PaletteSize;
    }
}
