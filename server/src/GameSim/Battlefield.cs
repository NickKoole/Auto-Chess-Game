using System.Diagnostics.CodeAnalysis;

namespace GameSim;

/// <summary>
/// The 8x8 pointy-top hex board a battle is fought on, and which unit stands on
/// each tile.
///
/// The fight runs vertically: the player deploys along the bottom (rows 5-7) and
/// advances north, the opponent deploys along the top (rows 0-2) and advances
/// south, and rows 3-4 are a neutral band nobody may deploy into but everybody
/// may walk through. With pointy-top hexes in offset rows, a row is a clean
/// horizontal band, so battle lines form along rows and close on each other.
///
/// Occupancy lives here rather than beside the board, because from story 1.3
/// onwards every tick moves units, and a mover that had to reach past the board
/// to find out what is in its way would end up owning this array anyway. That
/// mover will not be <see cref="TryPlace"/>: deployment and movement obey
/// different rules on the same tiles — crossing the neutral band is legal
/// movement and illegal deployment — so movement needs its own entry point
/// here, checking bounds and occupancy but not the bands.
/// </summary>
public sealed class Battlefield
{
    /// <summary>Number of rows, indexed 0 (north) to 7 (south).</summary>
    public const int Rows = 8;

    /// <summary>Number of columns, indexed 0 (west) to 7 (east).</summary>
    public const int Columns = 8;

    /// <summary>First row of the neutral band.</summary>
    public const int NeutralFirstRow = 3;

    /// <summary>Last row of the neutral band.</summary>
    public const int NeutralLastRow = 4;

    // One slot per tile, row-major. A flat array rather than a dictionary: the
    // board is a fixed 64 tiles, and nothing that decides an outcome may depend
    // on a dictionary's iteration order.
    private readonly BattleUnit?[] _tiles = new BattleUnit?[Rows * Columns];

    /// <summary>Whether a coordinate is on the board at all.</summary>
    public static bool IsInBounds(HexCoord coord) =>
        coord.Row >= 0 && coord.Row < Rows && coord.Col >= 0 && coord.Col < Columns;

    /// <summary>
    /// Whether a coordinate is in the neutral band, where nothing may deploy.
    /// False off the board, for the same reason as
    /// <see cref="IsDeploymentBandOf"/>: both are public row rules, and a
    /// passability check in story 1.3 is exactly the caller that would ask one
    /// of them directly rather than through <see cref="CheckTile"/>.
    /// </summary>
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
    /// How many rows a unit still has to cross to reach the far edge its side
    /// is advancing on — the enemy's back line, row 0 for the player and row
    /// <see cref="Rows"/> - 1 for the opponent.
    ///
    /// This is the front-to-back ordering key for a side's own units, and raw
    /// row will not do the job: the two sides advance in opposite directions,
    /// so ascending row is front-rank-first for the player and rear-rank-first
    /// for the opponent. Keyed this way, mirrored positions on the two sides
    /// also yield the same value, so a mirror match orders both armies
    /// identically instead of resolving their internal contention in opposite
    /// orders.
    ///
    /// Pure arithmetic on the row: unlike <see cref="IsDeploymentBandOf"/> it
    /// does not check bounds, because it answers a question about direction
    /// rather than about legality, and a caller sorting units it already holds
    /// has coordinates the board accepted.
    /// </summary>
    public static int RowsToEnemyBackLine(BattleSide side, HexCoord coord) => side switch
    {
        BattleSide.Player => coord.Row,
        BattleSide.Opponent => (Rows - 1) - coord.Row,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown side."),
    };

    /// <summary>
    /// Whether a side may deploy on a coordinate, ignoring what stands there.
    ///
    /// False for anything off the board. The band is a row rule, so it is
    /// tempting to answer with a row comparison alone — but this is public, and
    /// a caller reaching it directly rather than through <see cref="CheckTile"/>
    /// would then be told that (row 6, column 99) is a legal player tile.
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
    /// Checks everything about a tile that does not depend on what is standing
    /// on it, and returns null when the tile is legal for this side.
    ///
    /// The single home of the geography rules: <see cref="TryPlace"/> calls it
    /// before checking occupancy, and <see cref="BattleSetup"/> calls it while
    /// validating a whole army. Two copies of these three checks would drift.
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
    /// The unit standing on a tile, or null if it is empty.
    /// </summary>
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

        return _tiles[(coord.Row * Columns) + coord.Col];
    }

    /// <summary>Whether a unit is standing on a tile.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The coordinate is not on the board.</exception>
    public bool IsOccupied(HexCoord coord) => UnitAt(coord) is not null;

    /// <summary>
    /// Deploys a unit onto the tile it says it is standing on, or explains why
    /// it may not stand there. A tile holds at most one unit.
    ///
    /// Deployment only. This applies the band rules, so it rejects the neutral
    /// band and the opposing half outright — which makes it the wrong method to
    /// move a unit with, since a unit advancing from row 5 to row 4 is making a
    /// legal move onto a tile it could never have deployed on.
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

        int index = (coord.Row * Columns) + coord.Col;
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
