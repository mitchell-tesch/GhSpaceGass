namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a member end release. No API call — pure data.
///     Holds a 6-character release code for the 6 DOFs and optional per-DOF stiffness.
///     Created by the Create Release builder component and assigned to members (ADR-0013).
/// </summary>
public class SgReleaseData
{
    public SgReleaseData(
        string releaseCode,
        double? kTx = null,
        double? kTy = null,
        double? kTz = null,
        double? kRx = null,
        double? kRy = null,
        double? kRz = null)
    {
        if (string.IsNullOrEmpty(releaseCode) || releaseCode.Length != 6)
            throw new ArgumentException(
                "Release code must be exactly 6 characters (one per DOF: Fx,Fy,Fz,Mx,My,Mz).",
                nameof(releaseCode));

        var upper = releaseCode.ToUpperInvariant();
        foreach (var c in upper)
            if (c != 'F' && c != 'R' && c != 'S')
                throw new ArgumentException(
                    $"Invalid character '{c}' in release code. Each character must be 'F' (Fixed), 'R' (Released), or 'S' (Spring).",
                    nameof(releaseCode));

        ReleaseCode = upper;
        KTx = kTx;
        KTy = kTy;
        KTz = kTz;
        KRx = kRx;
        KRy = kRy;
        KRz = kRz;
    }

    /// <summary>
    ///     6-character release code for (Fx, Fy, Fz, Mx, My, Mz).
    ///     Each character is 'F' (Fixed), 'R' (Released), or 'S' (Spring).
    ///     Example: "FFFFFR" = release Mz only, "FFFFFF" = fully rigid, "FFFFS" = spring on My.
    /// </summary>
    public string ReleaseCode { get; }

    /// <summary>Spring stiffness for Fx (translation X). Null = no spring.</summary>
    public double? KTx { get; }

    /// <summary>Spring stiffness for Fy (translation Y). Null = no spring.</summary>
    public double? KTy { get; }

    /// <summary>Spring stiffness for Fz (translation Z). Null = no spring.</summary>
    public double? KTz { get; }

    /// <summary>Spring stiffness for Mx (rotation X). Null = no spring.</summary>
    public double? KRx { get; }

    /// <summary>Spring stiffness for My (rotation Y). Null = no spring.</summary>
    public double? KRy { get; }

    /// <summary>Spring stiffness for Mz (rotation Z). Null = no spring.</summary>
    public double? KRz { get; }

    /// <summary>
    ///     True when the release has no structural effect — all DOFs are Fixed
    ///     and no stiffness values are set. Used to skip unnecessary API payload.
    /// </summary>
    public bool IsFullyFixed =>
        ReleaseCode == "FFFFFF" &&
        !KTx.HasValue && !KTy.HasValue && !KTz.HasValue &&
        !KRx.HasValue && !KRy.HasValue && !KRz.HasValue;
}