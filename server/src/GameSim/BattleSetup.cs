using System.Diagnostics.CodeAnalysis;

namespace GameSim;

/// <summary>
/// A validated pair of armies on a board. No public constructor: holding one
/// means every placement was legal, both sides brought units, and no two
/// units share a tile - the simulation never re-checks any of it.
/// </summary>
public sealed class BattleSetup
{
    private BattleSetup(Battlefield battlefield, IReadOnlyList<BattleUnit> units)
    {
        Battlefield = battlefield;
        Units = units;
    }

    public Battlefield Battlefield { get; }

    /// <summary>
    /// Every unit in both armies: by side, then front-to-back
    /// (<see cref="Battlefield.RowsToEnemyBackLine"/>, not raw row - the two
    /// sides advance in opposite directions), then by column. Part of the
    /// contract, not an implementation detail: tick order decides a mutual
    /// kill, so a replay is only byte-identical if units start in this order.
    /// </summary>
    public IReadOnlyList<BattleUnit> Units { get; }

    /// <summary>All errors are collected, not just the first.</summary>
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
                // Necessarily the same side: bands are disjoint, so the other
                // army's unit here would already be rejected as OpposingDeploymentBand.
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

        // A total order: duplicates are already rejected, so no two units of a
        // side can tie on (rows-to-cross, column).
        BattleUnit[] ordered =
        [
            .. units
                .OrderBy(u => (int)u.Side)
                .ThenBy(u => Battlefield.RowsToEnemyBackLine(u.Side, u.Position))
                .ThenBy(u => u.Position.Col)
        ];

        var battlefield = new Battlefield();

        // Index in the final sorted order - already unique, so no further
        // bookkeeping needed.
        for (int i = 0; i < ordered.Length; i++)
        {
            ordered[i].Id = i;
        }

        foreach (BattleUnit unit in ordered)
        {
            if (!battlefield.TryPlace(unit, out PlacementError? error))
            {
                // Unreachable: validation above already rejected any setup
                // that could land here.
                throw new InvalidOperationException(
                    $"Validated setup failed to place a unit: {error.Message}");
            }
        }

        return new BattleSetup(battlefield, ordered);
    }
}
