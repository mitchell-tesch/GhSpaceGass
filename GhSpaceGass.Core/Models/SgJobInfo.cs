namespace GhSpaceGass.Core.Models;

/// <summary>
///     Domain model encapsulating the full job status returned by GetJobStatusAsync.
///     Maps from the SpaceGass API's JobStatus response into a clean domain representation.
/// </summary>
public class SgJobInfo
{
    // ── Headings ────────────────────────────────────────────────────
    public string Heading { get; set; } = string.Empty;
    public string ProjectHeading { get; set; } = string.Empty;
    public string DesignerInitials { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    // ── Settings ────────────────────────────────────────────────────
    public string VerticalAxis { get; set; } = string.Empty;

    // ── Units ───────────────────────────────────────────────────────
    public string LengthUnit { get; set; } = string.Empty;
    public string ForceUnit { get; set; } = string.Empty;
    public string MomentUnit { get; set; } = string.Empty;
    public string StressUnit { get; set; } = string.Empty;
    public string TemperatureUnit { get; set; } = string.Empty;
    public string MassUnit { get; set; } = string.Empty;
    public string MassDensityUnit { get; set; } = string.Empty;
    public string TranslationUnit { get; set; } = string.Empty;
    public string AccelerationUnit { get; set; } = string.Empty;
    public string SectionPropertiesUnit { get; set; } = string.Empty;
    public string MaterialStrengthUnit { get; set; } = string.Empty;

    // ── State ───────────────────────────────────────────────────────
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public bool IsNew { get; set; }
    public bool IsModified { get; set; }

    // ── Structure Summary ───────────────────────────────────────────
    public int NodeCount { get; set; }
    public int MemberCount { get; set; }
    public int MaterialCount { get; set; }
    public int SectionCount { get; set; }
    public int RestraintCount { get; set; }
    public int PlateCount { get; set; }

    // ── Loads Summary ───────────────────────────────────────────────
    public int LoadCaseCount { get; set; }
    public int LoadCategoryCount { get; set; }
    public int NodeLoadCount { get; set; }
    public int MemberDistributedLoadCount { get; set; }
    public int SelfWeightLoadCount { get; set; }

    // ── Analysis Summary ────────────────────────────────────────────
    public bool HasStaticResults { get; set; }
    public bool HasBucklingResults { get; set; }
    public bool HasDynamicResults { get; set; }

    /// <summary>
    ///     Returns the vertical axis as a user-friendly display string ("Y" or "Z").
    /// </summary>
    public string DisplayVerticalAxis => FormatVerticalAxis(VerticalAxis);

    /// <summary>
    ///     Formats the units into a compact summary string using standard engineering abbreviations.
    /// </summary>
    public string FormatUnits()
    {
        return $"Length: {DisplayUnit(LengthUnit)} | Force: {DisplayUnit(ForceUnit)} | " +
               $"Moment: {DisplayUnit(MomentUnit)} | Stress: {DisplayUnit(StressUnit)} | " +
               $"Temperature: {DisplayUnit(TemperatureUnit)} | Mass: {DisplayUnit(MassUnit)}";
    }

    /// <summary>
    ///     Formats the full job status into a multi-line summary string.
    /// </summary>
    public string FormatStatus()
    {
        var lines = new List<string>();

        if (!string.IsNullOrEmpty(FilePath))
            lines.Add($"File: {FilePath}");
        if (IsNew) lines.Add("Status: New job");
        else if (IsModified) lines.Add("Status: Modified");
        else lines.Add("Status: Saved");

        lines.Add($"Vertical Axis: {DisplayVerticalAxis}");
        lines.Add($"Units: {FormatUnits()}");
        lines.Add(
            $"Structure: {NodeCount} nodes, {MemberCount} members, {SectionCount} sections, {MaterialCount} materials, {RestraintCount} restraints");
        lines.Add(
            $"Loads: {LoadCaseCount} cases, {NodeLoadCount} node loads, {MemberDistributedLoadCount} distributed loads, {SelfWeightLoadCount} self-weight loads");

        if (HasStaticResults || HasBucklingResults || HasDynamicResults)
        {
            var results = new List<string>();
            if (HasStaticResults) results.Add("Static");
            if (HasBucklingResults) results.Add("Buckling");
            if (HasDynamicResults) results.Add("Dynamic");
            lines.Add($"Analysis Results: {string.Join(", ", results)}");
        }
        else
        {
            lines.Add("Analysis Results: None");
        }

        return string.Join("\n", lines);
    }

    // ── Display formatting helpers ──────────────────────────────────

    /// <summary>
    ///     Maps a raw VerticalAxis enum name to a user-friendly display string.
    /// </summary>
    internal static string FormatVerticalAxis(string raw)
    {
        switch (raw)
        {
            case "YAxis": return "Y";
            case "ZAxis": return "Z";
            default: return raw;
        }
    }

    /// <summary>
    ///     Maps a raw unit enum name to a standard engineering abbreviation.
    /// </summary>
    internal static string DisplayUnit(string raw)
    {
        switch (raw)
        {
            // Length
            case "Ft": return "ft";
            case "In": return "in";
            case "M": return "m";
            case "Cm": return "cm";
            case "Mm": return "mm";
            // Force
            case "K": return "kip";
            case "Lb": return "lb";
            case "KN": return "kN";
            case "N": return "N";
            case "Kg": return "kg";
            // Moment
            case "Kft": return "kip·ft";
            case "Kin": return "kip·in";
            case "Lbft": return "lb·ft";
            case "Lbin": return "lb·in";
            case "KNm": return "kN·m";
            case "KNcm": return "kN·cm";
            case "KNmm": return "kN·mm";
            case "Nm": return "N·m";
            case "Ncm": return "N·cm";
            case "Nmm": return "N·mm";
            case "Kgm": return "kg·m";
            case "Kgcm": return "kg·cm";
            case "Kgmm": return "kg·mm";
            // Stress / Material Strength
            case "Ksf": return "ksf";
            case "Psf": return "psf";
            case "Ksi": return "ksi";
            case "Psi": return "psi";
            case "MPa": return "MPa";
            case "KPa": return "kPa";
            case "Pa": return "Pa";
            case "Kgperm2": return "kg/m²";
            case "Kgpercm2": return "kg/cm²";
            case "Kgpermm2": return "kg/mm²";
            case "KNperm2": return "kN/m²";
            case "Npermm2": return "N/mm²";
            // Temperature
            case "DegF": return "°F";
            case "DegC": return "°C";
            // Mass
            case "T": return "t";
            // Mass Density
            case "Kperft3": return "kip/ft³";
            case "Kperin3": return "kip/in³";
            case "Lbperft3": return "lb/ft³";
            case "Lbperin3": return "lb/in³";
            case "Tperm3": return "t/m³";
            case "Tpercm3": return "t/cm³";
            case "Tpermm3": return "t/mm³";
            case "Kgperm3": return "kg/m³";
            case "Kgpercm3": return "kg/cm³";
            case "Kgpermm3": return "kg/mm³";
            // Translation (same as Length)
            case "Inch": return "in";
            // Acceleration
            case "Gs": return "g";
            case "Ftpersec2": return "ft/s²";
            case "Inpersec2": return "in/s²";
            case "Mpersec2": return "m/s²";
            case "Cmpersec2": return "cm/s²";
            case "Mmpersec2": return "mm/s²";
            case "KNperkg": return "kN/kg";
            // Fallback — return raw if no mapping
            default: return raw;
        }
    }
}