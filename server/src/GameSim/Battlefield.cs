using System.Diagnostics.CodeAnalysis;

namespace GameSim;

/// <summary>
/// The 8x8 pointy-top hex board a battle is fought on. The player deploys
/// along rows 5-7 and advances north; the opponent deploys along rows 0-2 and
/// advances south; rows 3-4 are a neutral band nobody may deploy into but
/// everybody may walk through.
/// </summary>
public sealed class Battlefield
{
    /// <summary>Indexed 0 (north) to 7 (south).</summary>
    public const int Rows = 8;

    /// <summary>Indexed 0 (west) to 7 (east).</summary>
    public const int Columns = 8;

    public const int NeutralFirstRow = 3;

    public const int NeutralLastRow = 4;

    // One slot per tile, row-major. A flat array rather than a dictionary: the
    // board is a fixed 64 tiles, and nothing that decides an outcome may depend
    // on a dictionary's iteration order.
    private readonly BattleUnit?[] _tiles = new BattleUnit?[Rows * Columns];

    public static bool IsInBounds(HexCoord coord) =>
        coord.Row >= 0 && coord.Row < Rows && coord.Col >= 0 && coord.Col < Columns;

    /// <summary>False off the board.</summary>
    public static bool IsNeutralBand(HexCoord coord) =>
        IsInBounds(coord) && coord.Row >= NeutralFirstRow && coord.Row <= NeutralLastRow;

    /// <summary>
    /// The rows a side may deploy in, inclusive: the player holds 5-7, the
    /// opponent holds 0-2.
    /// </summary>
    public static (int FirstRow, int LastRow) DeploymentBand(BattleSide side) => side switch
    {
        BattleSide.Player => (NeutralLastRow + 1, Rows - 1),
        BattleSide.Opponent => (0, NeutralFirstRow - 1),
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown side."),
    };

    /// <summary>
    /// Row 0 for the player, row <see cref="Rows"/> - 1 for the opponent - the
    /// front-to-back sort key for a side's own units, since raw row order runs
    /// backward for one side. No bounds check: callers already hold accepted
    /// coordinates.
    /// </summary>
    public static int RowsToEnemyBackLine(BattleSide side, HexCoord coord) => side switch
    {
        BattleSide.Player => coord.Row,
        BattleSide.Opponent => (Rows - 1) - coord.Row,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown side."),
    };

    /// <summary>
    /// Ignores what stands on the tile. False off the board - a row-only
    /// check would call (row 6, column 99) a legal player tile.
    /// </summary>
    public static bool IsDeploymentBandOf(BattleSide side, HexCoord coord)
    {
        if (!IsInBounds(coord))
        {
            return false;
        }

        (int firstRow, int lastRow) = DeploymentBand(side);
        return coord.Row >= firstRow && coord.Row <= lastRow;
    }

    /// <summary>
    /// Null when the tile is legal for this side. The single home of the
    /// geography rules - <see cref="TryPlace"/> and <see cref="BattleSetup"/>
    /// both call it, so there is only one copy to drift.
    /// </summary>
    public static PlacementError? CheckTile(BattleSide side, HexCoord coord)
    {
        if (!IsInBounds(coord))
        {
            return new PlacementError(
                PlacementErrorKind.OutOfBounds,
                side,
                coord,
                $"{coord} is off the board: rows are 0-{Rows - 1} and columns are 0-{Columns - 1}.");
        }

        if (IsNeutralBand(coord))
        {
            return new PlacementError(
                PlacementErrorKind.NeutralBand,
                side,
                coord,
                $"{coord} is in the neutral band (rows {NeutralFirstRow}-{NeutralLastRow}), " +
                "where neither side may deploy.");
        }

        if (!IsDeploymentBandOf(side, coord))
        {
            (int firstRow, int lastRow) = DeploymentBand(side);
            return new PlacementError(
                PlacementErrorKind.OpposingDeploymentBand,
                side,
                coord,
                $"{coord} is in the opposing deployment band: {side} deploys in rows {firstRow}-{lastRow}.");
        }

        return null;
    }

    /// <summary>
    /// On the board and empty. Blind to deployment bands, unlike
    /// <see cref="CheckTile"/> - a unit may move through the neutral band even
    /// though it could never deploy there. Answers about the board right now:
    /// a caller wanting "empty at the start of the tick" must ask before
    /// anything moves. Returns false off the board rather than throwing,
    /// unlike <see cref="UnitAt"/>.
    /// </summary>
    public bool IsPassable(HexCoord coord) => IsInBounds(coord) && _tiles[Index(coord)] is null;

    /// <summary>
    /// Moves a unit to <paramref name="destination"/> and updates its
    /// <see cref="BattleUnit.Position"/>. Throws rather than returning a
    /// reason, unlike <see cref="TryPlace"/>: the mover already decided to
    /// move before calling this, so an off-board or occupied destination here
    /// is a programmer error. Distance is not checked - the one-hex-per-step
    /// rule belongs to the mover, not the board.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The destination is off the board.</exception>
    /// <exception cref="InvalidOperationException">
    /// The destination is occupied, or the unit is not standing where the board
    /// says it is.
    /// </exception>
    public void Move(BattleUnit unit, HexCoord destination)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (!IsInBounds(destination))
        {
            throw new ArgumentOutOfRangeException(
                nameof(destination),
                destination,
                $"{destination} is off the board: rows are 0-{Rows - 1} and columns are 0-{Columns - 1}.");
        }

        int to = Index(destination);
        if (_tiles[to] is BattleUnit occupant)
        {
            throw new InvalidOperationException(
                $"Cannot move {unit.Side} {unit.Definition.Id} to {destination}: " +
                $"it is occupied by {occupant.Side} {occupant.Definition.Id}.");
        }

        HexCoord origin = unit.Position;
        if (!IsInBounds(origin) || !ReferenceEquals(_tiles[Index(origin)], unit))
        {
            throw new InvalidOperationException(
                $"{unit.Side} {unit.Definition.Id} says it is at {origin}, but the board does not " +
                "have it there. The board and its units have been allowed to drift apart.");
        }

        _tiles[Index(origin)] = null;
        _tiles[to] = unit;
        unit.Position = destination;
    }

    /// <summary>
    /// Frees the unit's tile. Does not clear <see cref="BattleUnit.Position"/>
    /// - the corpse still knows where it fell, which is harmless once nothing
    /// targets or paths through it.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The unit is not standing where the board says it is. Removing a unit
    /// already removed, or one the board never placed, is a programmer error —
    /// the simulation removes a unit at most once, in the tick it died.
    /// </exception>
    public void Remove(BattleUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        HexCoord at = unit.Position;
        if (!IsInBounds(at) || !ReferenceEquals(_tiles[Index(at)], unit))
        {
            throw new InvalidOperationException(
                $"{unit.Side} {unit.Definition.Id} says it is at {at}, but the board does not " +
                "have it there. It may already have been removed.");
        }

        _tiles[Index(at)] = null;
    }

    private static int Index(HexCoord coord) => (coord.Row * Columns) + coord.Col;

    /// <exception cref="ArgumentOutOfRangeException">
    /// The coordinate is not on the board. Asking about a tile that does not
    /// exist is a programmer error, unlike trying to deploy onto one — which is
    /// a player's mistake and comes back from <see cref="TryPlace"/> as a
    /// reason.
    /// </exception>
    public BattleUnit? UnitAt(HexCoord coord)
    {
        if (!IsInBounds(coord))
        {
            throw new ArgumentOutOfRangeException(
                nameof(coord),
                coord,
                $"{coord} is off the board: rows are 0-{Rows - 1} and columns are 0-{Columns - 1}.");
        }

        return _tiles[Index(coord)];
    }

    /// <exception cref="ArgumentOutOfRangeException">The coordinate is not on the board.</exception>
    public bool IsOccupied(HexCoord coord) => UnitAt(coord) is not null;

    /// <summary>
    /// Deploys onto <see cref="BattleUnit.Position"/>, applying the band
    /// rules - so it rejects a legal mid-battle move like row 5 to row 4 as an
    /// illegal deployment. Use <see cref="Move"/> to move a unit.
    /// </summary>
    public bool TryPlace(BattleUnit unit, [NotNullWhen(false)] out PlacementError? error)
    {
        ArgumentNullException.ThrowIfNull(unit);

        HexCoord coord = unit.Position;

        error = CheckTile(unit.Side, coord);
        if (error is not null)
        {
            return false;
        }

        int index = Index(coord);
        if (_tiles[index] is BattleUnit occupant)
        {
            error = new PlacementError(
                PlacementErrorKind.TileOccupied,
                unit.Side,
                coord,
                $"{coord} is already occupied by {occupant.Side} {occupant.Definition.Id}.");
            return false;
        }

        _tiles[index] = unit;
        error = null;
        return true;
    }
}
