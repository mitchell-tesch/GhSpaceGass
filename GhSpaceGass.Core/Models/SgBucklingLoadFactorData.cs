namespace GhSpaceGass.Core.Models;

/// <summary>
///     A single buckling load factor result for a specific load case and mode.
/// </summary>
public class SgBucklingLoadFactorData
{
    public SgBucklingLoadFactorData(
        int loadCaseId, int mode, double loadFactor,
        SgPoint3D? nodeAtMaxTranslation, string translationAxis,
        SgPoint3D? nodeAtMaxRotation, string rotationAxis)
    {
        LoadCaseId = loadCaseId;
        Mode = mode;
        LoadFactor = loadFactor;
        NodeAtMaxTranslation = nodeAtMaxTranslation;
        TranslationAxis = translationAxis;
        NodeAtMaxRotation = nodeAtMaxRotation;
        RotationAxis = rotationAxis;
    }

    public int LoadCaseId { get; }
    public int Mode { get; }
    public double LoadFactor { get; }

    /// <summary>Point location of node at maximum translation for this mode (null if node not in model).</summary>
    public SgPoint3D? NodeAtMaxTranslation { get; }

    /// <summary>Axis of maximum translation (e.g. "X", "Y", "Z").</summary>
    public string TranslationAxis { get; }

    /// <summary>Point location of node at maximum rotation for this mode (null if node not in model).</summary>
    public SgPoint3D? NodeAtMaxRotation { get; }

    /// <summary>Axis of maximum rotation (e.g. "X", "Y", "Z").</summary>
    public string RotationAxis { get; }
}