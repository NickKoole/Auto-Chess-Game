namespace GameSim;

/// <summary>
/// Why a placement or a whole army setup was rejected.
///
/// A kind rather than a message alone, so a caller can react to the reason —
/// the API turns these into a typed failure, and the client can highlight the
/// offending tile. The message beside it is for a human reading a log.
/// </summary>
public enum PlacementErrorKind
{
    /// <summary>The coordinate is not on the board.</summary>
    OutOfBounds,

    /// <summary>Another unit already stands there.</summary>
    TileOccupied,

    /// <summary>The tile is in the neutral band, where nothing may be deployed.</summary>
    NeutralBand,

    /// <summary>The tile is in the other side's deployment band.</summary>
    OpposingDeploymentBand,

    /// <summary>The same side listed this coordinate twice.</summary>
    DuplicateCoordinate,

    /// <summary>A side brought no units at all.</summary>
    NoUnits,
}
