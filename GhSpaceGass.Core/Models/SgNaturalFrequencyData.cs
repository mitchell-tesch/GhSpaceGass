namespace GhSpaceGass.Core.Models;

/// <summary>
///     A single natural frequency result for a specific mode.
/// </summary>
public class SgNaturalFrequencyData
{
    public SgNaturalFrequencyData(
        int loadCaseId, int mode, double frequency, double period,
        double massPartX, double massPartY, double massPartZ)
    {
        LoadCaseId = loadCaseId;
        Mode = mode;
        Frequency = frequency;
        Period = period;
        MassPartX = massPartX;
        MassPartY = massPartY;
        MassPartZ = massPartZ;
    }

    public int LoadCaseId { get; }
    public int Mode { get; }

    /// <summary>Natural frequency in Hz.</summary>
    public double Frequency { get; }

    /// <summary>Natural period in seconds.</summary>
    public double Period { get; }

    /// <summary>Mass participation ratio in X direction.</summary>
    public double MassPartX { get; }

    /// <summary>Mass participation ratio in Y direction.</summary>
    public double MassPartY { get; }

    /// <summary>Mass participation ratio in Z direction.</summary>
    public double MassPartZ { get; }
}

