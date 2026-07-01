namespace GhSpaceGass.Core.Models;

/// <summary>
///     A single intermediate displacement result — global and local translations at one station
///     along a member for a specific load case. The API returns one record per member/load-case
///     with lists of station values; this model represents one flattened station.
/// </summary>
public class SgMemberDisplacementData
{
    public SgMemberDisplacementData(
        int memberId, int loadCaseId, int station, double location,
        double txGlobal, double tyGlobal, double tzGlobal,
        double txLocal, double tyLocal, double tzLocal)
    {
        MemberId = memberId;
        LoadCaseId = loadCaseId;
        Station = station;
        Location = location;
        TxGlobal = txGlobal;
        TyGlobal = tyGlobal;
        TzGlobal = tzGlobal;
        TxLocal = txLocal;
        TyLocal = tyLocal;
        TzLocal = tzLocal;
    }

    public int MemberId { get; }
    public int LoadCaseId { get; }
    public int Station { get; }

    /// <summary>Position along the member length.</summary>
    public double Location { get; }

    /// <summary>Global translation in X at this station.</summary>
    public double TxGlobal { get; }

    /// <summary>Global translation in Y at this station.</summary>
    public double TyGlobal { get; }

    /// <summary>Global translation in Z at this station.</summary>
    public double TzGlobal { get; }

    /// <summary>Local translation in X at this station.</summary>
    public double TxLocal { get; }

    /// <summary>Local translation in Y at this station.</summary>
    public double TyLocal { get; }

    /// <summary>Local translation in Z at this station.</summary>
    public double TzLocal { get; }
}