using GameSim;

namespace GameSim.Tests;

public sealed class BattleSetupTests
{
    private static readonly UnitDefinition Militia = new(
        id: "militia",
        displayName: "Militia",
        maxHp: 100,
        attackDamage: 12,
        attackIntervalTicks: 12,
        movementTicksPerHex: 6,
        attackRangeHexes: 1);

    private static UnitPlacement At(int row, int col) => new(Militia, Horizontal(row, col));

    private static HexCoord Horizontal(int row, int col) => new(col, Battlefield.Rows - 1 - row);

    /// <summary>One legal unit per side, for tests about something else.</summary>
    private static UnitPlacement[] Player => [At(7, 0)];

    private static UnitPlacement[] Opponent => [At(0, 0)];

    private static PlacementError SingleError(
        IReadOnlyList<UnitPlacement> player,
        IReadOnlyList<UnitPlacement> opponent)
    {
        Assert.False(BattleSetup.TryCreate(player, opponent, out BattleSetup? setup, out var errors));
        Assert.Null(setup);
        return Assert.Single(errors);
    }

    [Fact]
    public void Builds_a_setup_from_two_legal_armies()
    {
        Assert.True(BattleSetup.TryCreate(
            [At(5, 0), At(7, 7)],
            [At(0, 0), At(2, 7)],
            out BattleSetup? setup,
            out var errors));

        Assert.Empty(errors);
        Assert.Equal(4, setup.Units.Count);
    }

    [Fact]
    public void Produces_units_ready_to_simulate()
    {
        Assert.True(BattleSetup.TryCreate(Player, Opponent, out BattleSetup? setup, out _));

        foreach (BattleUnit unit in setup.Units)
        {
            Assert.Same(Militia, unit.Definition);
            Assert.Equal(Militia.MaxHp, unit.CurrentHp);
            Assert.Equal(0, unit.AttackCooldownTicks);
        }
    }

    [Fact]
    public void Stands_every_unit_on_the_battlefield()
    {
        Assert.True(BattleSetup.TryCreate([At(5, 0), At(7, 7)], [At(0, 3)], out BattleSetup? setup, out _));

        foreach (BattleUnit unit in setup.Units)
        {
            Assert.Same(unit, setup.Battlefield.UnitAt(unit.Position));
        }
    }

    [Fact]
    public void Orders_units_by_owner_then_front_to_back_then_column()
    {
        // Deliberately listed out of order, and the opponent listed first, so a
        // setup that simply concatenated the caller's lists would fail here.
        Assert.True(BattleSetup.TryCreate(
            [At(7, 3), At(5, 4), At(5, 1)],
            [At(2, 6), At(0, 0), At(0, 5)],
            out BattleSetup? setup,
            out _));

        // Front-to-back is ascending row for the player and descending row for
        // the opponent, because they advance in opposite directions: row 5 is
        // the player's front line and row 2 is the opponent's.
        (BattleSide, HexCoord)[] expected =
        [
            (BattleSide.Player, Horizontal(5, 1)),
            (BattleSide.Player, Horizontal(5, 4)),
            (BattleSide.Player, Horizontal(7, 3)),
            (BattleSide.Opponent, Horizontal(2, 6)),
            (BattleSide.Opponent, Horizontal(0, 0)),
            (BattleSide.Opponent, Horizontal(0, 5)),
        ];

        Assert.Equal(expected, setup.Units.Select(u => (u.Side, u.Position)));
    }

    [Fact]
    public void Orders_a_mirror_match_symmetrically()
    {
        // The same army twice, reflected across the halfway line: the player's
        // row r faces the opponent's row 7 - r. Ordering both sides by raw
        // ascending row pulls this pairing apart — the player acts front rank
        // first and the opponent rear rank first — so a mirror match resolves
        // its two sides' internal contention in opposite orders.
        HexCoord[] army = [Horizontal(5, 1), Horizontal(6, 4), Horizontal(7, 0), Horizontal(5, 6)];

        Assert.True(BattleSetup.TryCreate(
            [.. army.Select(c => new UnitPlacement(Militia, c))],
            [.. army.Select(c => new UnitPlacement(Militia, Mirror(c)))],
            out BattleSetup? setup,
            out _));

        HexCoord[] player = [.. PositionsOf(setup, BattleSide.Player)];
        HexCoord[] opponent = [.. PositionsOf(setup, BattleSide.Opponent)];

        Assert.Equal(player.Select(Mirror), opponent);

        // Symmetry alone would also hold if both armies were ordered
        // back-to-front, so pin down which end of each army comes first.
        Assert.Equal(
            [Horizontal(5, 1), Horizontal(5, 6), Horizontal(6, 4), Horizontal(7, 0)],
            player);
    }

    [Fact]
    public void Orders_an_army_the_same_way_however_the_caller_lists_it()
    {
        // The order is a total order, so it cannot fall back on the incoming
        // list to break a tie: two callers describing the same two armies get
        // the same sequence out.
        UnitPlacement[] player = [At(5, 1), At(7, 3), At(5, 4), At(6, 0)];
        UnitPlacement[] opponent = [At(0, 2), At(2, 6), At(1, 1)];

        Assert.True(BattleSetup.TryCreate(player, opponent, out BattleSetup? asListed, out _));
        Assert.True(BattleSetup.TryCreate(
            [.. player.Reverse()],
            [.. opponent.Reverse()],
            out BattleSetup? reversed,
            out _));

        Assert.Equal(
            asListed.Units.Select(u => (u.Side, u.Position)),
            reversed.Units.Select(u => (u.Side, u.Position)));
    }

    /// <summary>The tile a coordinate reflects onto across the halfway line.</summary>
    private static HexCoord Mirror(HexCoord coord) =>
        new(coord.Row, Battlefield.Columns - 1 - coord.Col);

    private static IEnumerable<HexCoord> PositionsOf(BattleSetup setup, BattleSide side) =>
        setup.Units.Where(u => u.Side == side).Select(u => u.Position);

    [Fact]
    public void Rejects_a_placement_off_the_board()
    {
        PlacementError error = SingleError([At(7, 8)], Opponent);

        Assert.Equal(PlacementErrorKind.OutOfBounds, error.Kind);
        Assert.Equal(BattleSide.Player, error.Side);
        Assert.Equal(Horizontal(7, 8), error.Coord);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Rejects_a_placement_in_the_neutral_band(int row)
    {
        PlacementError error = SingleError([At(row, 2)], Opponent);

        Assert.Equal(PlacementErrorKind.NeutralBand, error.Kind);
        Assert.Equal(Horizontal(row, 2), error.Coord);
    }

    [Fact]
    public void Rejects_a_player_placement_in_the_opponents_band()
    {
        PlacementError error = SingleError([At(1, 2)], Opponent);

        Assert.Equal(PlacementErrorKind.OpposingDeploymentBand, error.Kind);
        Assert.Equal(BattleSide.Player, error.Side);
    }

    [Fact]
    public void Rejects_an_opponent_placement_in_the_players_band()
    {
        PlacementError error = SingleError(Player, [At(6, 2)]);

        Assert.Equal(PlacementErrorKind.OpposingDeploymentBand, error.Kind);
        Assert.Equal(BattleSide.Opponent, error.Side);
    }

    [Fact]
    public void Rejects_two_units_of_one_side_on_the_same_tile()
    {
        PlacementError error = SingleError([At(6, 2), At(6, 2)], Opponent);

        Assert.Equal(PlacementErrorKind.DuplicateCoordinate, error.Kind);
        Assert.Equal(BattleSide.Player, error.Side);
        Assert.Equal(Horizontal(6, 2), error.Coord);
    }

    [Fact]
    public void Accepts_the_same_column_in_each_sides_own_band()
    {
        // The duplicate check keys on the whole coordinate, not on the column:
        // these are two different tiles and must both stand. ("The same tile
        // used by both sides" is not testable — the bands are disjoint, so the
        // second side is rejected for the band before any duplicate check.)
        Assert.True(BattleSetup.TryCreate([At(5, 4)], [At(2, 4)], out BattleSetup? setup, out _));

        Assert.Equal(2, setup.Units.Count);
    }

    [Fact]
    public void Rejects_a_player_with_no_units()
    {
        PlacementError error = SingleError([], Opponent);

        Assert.Equal(PlacementErrorKind.NoUnits, error.Kind);
        Assert.Equal(BattleSide.Player, error.Side);
        Assert.Null(error.Coord);
    }

    [Fact]
    public void Rejects_an_opponent_with_no_units()
    {
        PlacementError error = SingleError(Player, []);

        Assert.Equal(PlacementErrorKind.NoUnits, error.Kind);
        Assert.Equal(BattleSide.Opponent, error.Side);
    }

    [Fact]
    public void Reports_every_problem_at_once_rather_than_the_first()
    {
        // Four different mistakes across both armies. Someone fixing a layout
        // should see all of them, not discover them one submission at a time.
        Assert.False(BattleSetup.TryCreate(
            [At(7, 0), At(4, 1), At(7, 0), At(0, 0)],
            [At(2, 9)],
            out BattleSetup? setup,
            out var errors));

        Assert.Null(setup);
        Assert.Equal(
            [
                PlacementErrorKind.NeutralBand,
                PlacementErrorKind.DuplicateCoordinate,
                PlacementErrorKind.OpposingDeploymentBand,
                PlacementErrorKind.OutOfBounds,
            ],
            errors.Select(e => e.Kind));
    }

    [Fact]
    public void Every_error_explains_itself()
    {
        Assert.False(BattleSetup.TryCreate([At(4, 1)], [], out _, out var errors));

        foreach (PlacementError error in errors)
        {
            Assert.False(string.IsNullOrWhiteSpace(error.Message));
        }
    }

    [Fact]
    public void Rejects_missing_placement_lists()
    {
        Assert.Throws<ArgumentNullException>(
            () => BattleSetup.TryCreate(null!, Opponent, out _, out _));
        Assert.Throws<ArgumentNullException>(
            () => BattleSetup.TryCreate(Player, null!, out _, out _));
    }

    [Fact]
    public void Rejects_a_placement_with_no_definition()
    {
        // A default-constructed UnitPlacement carries a null definition, which
        // would otherwise become a BattleUnit with no stats.
        Assert.Throws<ArgumentNullException>(
            () => BattleSetup.TryCreate([default], Opponent, out _, out _));
    }
}
