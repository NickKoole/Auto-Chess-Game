using System.Diagnostics.CodeAnalysis;

namespace GameSim;

/// <summary>
/// A validated pair of armies on a board: the starting position a battle runs
/// from.
///
/// There is no public constructor, so a <see cref="BattleSetup"/> cannot exist
/// in an illegal state — if you are holding one, every placement was legal, both
/// sides brought units, and no two units share a tile. That is what lets the
/// simulation itself skip re-checking any of it.
/// </summary>
public sealed class BattleSetup
{
    private BattleSetup(Battlefield battlefield, IReadOnlyList<BattleUnit> units)
    {
        Battlefield = battlefield;
        Units = units;
    }

    /// <summary>The board, with every unit already standing on it.</summary>
    public Battlefield Battlefield { get; }

    /// <summary>
    /// Every unit in both armies, ordered by owner, then row, then column.
    ///
    /// The order is part of the contract, not an implementation detail. Tick
    /// order decides who wins a mutual kill, so a battle replayed from the same
    /// seed and the same setup can only be byte-identical if the units start in
    /// the same order — however the caller happened to list them.
    /// </summary>
    public IReadOnlyList<BattleUnit> Units { get; }

    /// <summary>
    /// Validates two armies and, if both are legal, builds the setup.
    ///
    /// Every placement is checked before anything is built, and
    /// <paramref name="errors"/> comes back holding all of the reasons
    /// rather than only the first: an illegal army is something a player fixes,
    /// and fixing it one tile per attempt is miserable.
    /// </summary>
    /// <exception cref="ArgumentNullException">Either list, or a definition inside one, is null.</exception>
    public static bool TryCreate(
        IReadOnlyList<UnitPlacement> playerPlacements,
        IReadOnlyList<UnitPlacement> opponentPlacements,
        [NotNullWhen(true)] out BattleSetup? setup,
        out IReadOnlyList<PlacementError> errors)
    {
        ArgumentNullException.ThrowIfNull(playerPlacements);
        ArgumentNullException.ThrowIfNull(opponentPlacements);

        var found = new List<PlacementError>();

        // Every tile claimed so far, across both armies. A set for lookup only —
        // it is never enumerated, so its order cannot reach an outcome.
        var claimed = new HashSet<HexCoord>();

        Validate(BattleSide.Player, playerPlacements, claimed, found);
        Validate(BattleSide.Opponent, opponentPlacements, claimed, found);

        if (found.Count > 0)
        {
            setup = null;
            errors = found;
            return false;
        }

        setup = Build(playerPlacements, opponentPlacements);
        errors = [];
        return true;
    }

    private static void Validate(
        BattleSide side,
        IReadOnlyList<UnitPlacement> placements,
        HashSet<HexCoord> claimed,
        List<PlacementError> found)
    {
        if (placements.Count == 0)
        {
            found.Add(new PlacementError(
                PlacementErrorKind.NoUnits,
                side,
                null,
                $"{side} brought no units: each side needs at least one."));
            return;
        }

        foreach (UnitPlacement placement in placements)
        {
            ArgumentNullException.ThrowIfNull(placement.Definition, nameof(placements));

            HexCoord coord = placement.Coord;

            PlacementError? tileError = Battlefield.CheckTile(side, coord);
            if (tileError is not null)
            {
                found.Add(tileError);
                continue;
            }

            if (!claimed.Add(coord))
            {
                // Necessarily the same side twice: the tile passed CheckTile, so
                // it is in this side's band, and the bands are disjoint — the
                // other army's unit on this tile would have been rejected as
                // OpposingDeploymentBand before ever reaching here. That is why
                // there is no cross-side TileOccupied case in this loop;
                // Battlefield.TryPlace still reports one, and would again here
                // if the bands were ever allowed to overlap.
                found.Add(new PlacementError(
                    PlacementErrorKind.DuplicateCoordinate,
                    side,
                    coord,
                    $"{side} listed {coord} more than once: a tile holds at most one unit."));
            }
        }
    }

    private static BattleSetup Build(
        IReadOnlyList<UnitPlacement> playerPlacements,
        IReadOnlyList<UnitPlacement> opponentPlacements)
    {
        List<BattleUnit> units =
        [
            .. playerPlacements.Select(p => new BattleUnit(p.Definition, BattleSide.Player, p.Coord)),
            .. opponentPlacements.Select(p => new BattleUnit(p.Definition, BattleSide.Opponent, p.Coord)),
        ];

        // A total order, not merely a stable one: duplicates are already
        // rejected above, so no two units share (side, row, column) and the sort
        // cannot fall back on the caller's list order to break a tie.
        BattleUnit[] ordered =
        [
            .. units
                .OrderBy(u => (int)u.Side)
                .ThenBy(u => u.Position.Row)
                .ThenBy(u => u.Position.Col)
        ];

        var battlefield = new Battlefield();

        foreach (BattleUnit unit in ordered)
        {
            if (!battlefield.TryPlace(unit, out PlacementError? error))
            {
                // Unreachable: validation above rejected every setup that could
                // land here. If it ever fires, the two are out of step, and
                // that is a bug to fix rather than a state to carry on from.
                throw new InvalidOperationException(
                    $"Validated setup failed to place a unit: {error.Message}");
            }
        }

        return new BattleSetup(battlefield, ordered);
    }
}
