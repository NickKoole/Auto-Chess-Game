namespace GameSim;

public enum PlacementErrorKind
{
    OutOfBounds,

    TileOccupied,

    NeutralBand,

    OpposingDeploymentBand,

    DuplicateCoordinate,

    NoUnits,
}
