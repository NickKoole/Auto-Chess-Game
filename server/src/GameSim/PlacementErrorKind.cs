namespace GameSim;

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
