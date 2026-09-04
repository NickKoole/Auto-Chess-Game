namespace GameSim;

/// <summary>Names the side and, for every kind but <see cref="PlacementErrorKind.NoUnits"/>, the tile at fault.</summary>
public sealed record PlacementError(
    PlacementErrorKind Kind,
    BattleSide Side,
    HexCoord? Coord,
    string Message);
