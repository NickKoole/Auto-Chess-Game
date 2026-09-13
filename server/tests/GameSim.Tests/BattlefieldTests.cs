using GameSim;

namespace GameSim.Tests;

public sealed class BattlefieldTests
{
    private static readonly UnitDefinition Militia = new(
        id: "militia",
        displayName: "Militia",
        maxHp: 100,
        attackDamage: 12,
        attackIntervalTicks: 12,
        movementTicksPerHex: 6,
        attackRangeHexes: 1);

    private static BattleUnit Unit(BattleSide side, int row, int col) =>
        new(Militia, side, new HexCoord(row, col));

    [Fact]
    public void Is_eight_by_eight()
    {
        Assert.Equal(8, Battlefield.Rows);
        Assert.Equal(8, Battlefield.Columns);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 7)]
    [InlineData(3, 4)]
    public void Accepts_coordinates_on_the_board(int row, int col)
    {
        Assert.True(Battlefield.IsInBounds(new HexCoord(row, col)));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(8, 0)]
    [InlineData(0, 8)]
    [InlineData(8, 8)]
    [InlineData(int.MinValue, int.MinValue)]
    public void Rejects_coordinates_off_the_board(int row, int col)
    {
        Assert.False(Battlefield.IsInBounds(new HexCoord(row, col)));
    }

    [Theory]
    [InlineData(BattleSide.Player, 0, 2)]
    [InlineData(BattleSide.Opponent, 5, 7)]
    public void Deployment_bands_leave_columns_three_and_four_neutral(BattleSide side, int firstColumn, int lastColumn)
    {
        Assert.Equal((firstColumn, lastColumn), Battlefield.DeploymentBand(side));
    }

    [Theory]
    [InlineData(BattleSide.Player, 0, 7)]
    [InlineData(BattleSide.Player, 2, 5)]
    [InlineData(BattleSide.Player, 7, 0)]
    [InlineData(BattleSide.Opponent, 0, 0)]
    [InlineData(BattleSide.Opponent, 5, 5)]
    [InlineData(BattleSide.Opponent, 7, 7)]
    public void Counts_the_columns_a_side_still_has_to_cross(BattleSide side, int col, int expected)
    {
        Assert.Equal(expected, Battlefield.ColumnsToEnemyBackLine(side, new HexCoord(3, col)));
    }

    [Fact]
    public void Gives_mirrored_columns_the_same_number_of_columns_to_cross()
    {
        // The property the unit order relies on: reflect a tile across the
        // halfway line, hand it to the other side, and it is the same distance
        // along. Without it a mirror match could not order its two armies alike.
        for (int col = 0; col < Battlefield.Columns; col++)
        {
            Assert.Equal(
                Battlefield.ColumnsToEnemyBackLine(BattleSide.Player, new HexCoord(3, col)),
                Battlefield.ColumnsToEnemyBackLine(
                    BattleSide.Opponent, new HexCoord(3, Battlefield.Columns - 1 - col)));
        }
    }

    [Fact]
    public void Refuses_to_count_columns_for_a_side_that_does_not_exist()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Battlefield.ColumnsToEnemyBackLine((BattleSide)7, new HexCoord(3, 5)));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Neutral_band_spans_columns_three_and_four(int col)
    {
        Assert.True(Battlefield.IsNeutralBand(new HexCoord(0, col)));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void Columns_outside_three_and_four_are_not_neutral(int col)
    {
        Assert.False(Battlefield.IsNeutralBand(new HexCoord(0, col)));
    }

    [Theory]
    [InlineData(BattleSide.Player, 0)]
    [InlineData(BattleSide.Player, 1)]
    [InlineData(BattleSide.Player, 2)]
    [InlineData(BattleSide.Opponent, 5)]
    [InlineData(BattleSide.Opponent, 6)]
    [InlineData(BattleSide.Opponent, 7)]
    public void A_side_may_deploy_in_its_own_band(BattleSide side, int col)
    {
        Assert.Null(Battlefield.CheckTile(side, new HexCoord(4, col)));
    }

    [Theory]
    [InlineData(BattleSide.Player, 3)]
    [InlineData(BattleSide.Player, 4)]
    [InlineData(BattleSide.Opponent, 3)]
    [InlineData(BattleSide.Opponent, 4)]
    public void Nobody_may_deploy_in_the_neutral_band(BattleSide side, int col)
    {
        PlacementError? error = Battlefield.CheckTile(side, new HexCoord(2, col));

        Assert.NotNull(error);
        Assert.Equal(PlacementErrorKind.NeutralBand, error.Kind);
        Assert.Equal(side, error.Side);
        Assert.Equal(new HexCoord(2, col), error.Coord);
    }

    [Theory]
    [InlineData(BattleSide.Player, 5)]
    [InlineData(BattleSide.Player, 7)]
    [InlineData(BattleSide.Opponent, 0)]
    [InlineData(BattleSide.Opponent, 2)]
    public void A_side_may_not_deploy_in_the_opposing_band(BattleSide side, int col)
    {
        PlacementError? error = Battlefield.CheckTile(side, new HexCoord(1, col));

        Assert.NotNull(error);
        Assert.Equal(PlacementErrorKind.OpposingDeploymentBand, error.Kind);
    }

    [Theory]
    [InlineData(3, 99)]
    [InlineData(4, -1)]
    [InlineData(3, 8)]
    public void No_tile_off_the_board_is_neutral_however_right_the_row_looks(int row, int col)
    {
        // Mirrors the deployment-band guard below. Both are public row rules,
        // and hardening one but not the other is worse than hardening neither:
        // it reads as a considered decision that these differ.
        Assert.False(Battlefield.IsNeutralBand(new HexCoord(row, col)));
    }

    [Theory]
    [InlineData(BattleSide.Player, 6, 99)]
    [InlineData(BattleSide.Player, 6, -1)]
    [InlineData(BattleSide.Opponent, 1, 99)]
    public void No_side_may_deploy_off_the_board_however_right_the_row_looks(
        BattleSide side, int row, int col)
    {
        // The band is a row rule, so a row-only comparison would call these
        // legal. They are asked of the predicate directly, because a caller who
        // reaches past CheckTile is exactly who this guard is for.
        Assert.False(Battlefield.IsDeploymentBandOf(side, new HexCoord(row, col)));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(8, 8)]
    public void Deploying_off_the_board_is_rejected_before_any_band_rule(int row, int col)
    {
        // Out of bounds wins over the band checks: "row 9 is in the opposing
        // deployment band" would be a nonsense explanation of a nonsense tile.
        PlacementError? error = Battlefield.CheckTile(BattleSide.Player, new HexCoord(row, col));

        Assert.NotNull(error);
        Assert.Equal(PlacementErrorKind.OutOfBounds, error.Kind);
    }

    [Fact]
    public void An_empty_tile_holds_no_unit()
    {
        var field = new Battlefield();

        Assert.Null(field.UnitAt(new HexCoord(6, 3)));
        Assert.False(field.IsOccupied(new HexCoord(6, 3)));
    }

    [Fact]
    public void A_placed_unit_can_be_found_by_coordinate()
    {
        var field = new Battlefield();
        BattleUnit unit = Unit(BattleSide.Player, 6, 2);

        Assert.True(field.TryPlace(unit, out PlacementError? error));

        Assert.Null(error);
        Assert.Same(unit, field.UnitAt(new HexCoord(6, 2)));
        Assert.True(field.IsOccupied(new HexCoord(6, 2)));
    }

    [Fact]
    public void Placing_a_unit_leaves_every_other_tile_empty()
    {
        var field = new Battlefield();
        Assert.True(field.TryPlace(Unit(BattleSide.Player, 6, 2), out _));

        var occupied = new List<HexCoord>();

        for (int row = 0; row < Battlefield.Rows; row++)
        {
            for (int col = 0; col < Battlefield.Columns; col++)
            {
                if (field.IsOccupied(new HexCoord(row, col)))
                {
                    occupied.Add(new HexCoord(row, col));
                }
            }
        }

        // Exactly one tile, found by scanning all 64: an index formula that let
        // two coordinates collide would show up as a unit appearing somewhere it
        // was never placed.
        Assert.Equal([new HexCoord(6, 2)], occupied);
    }

    [Fact]
    public void A_tile_holds_at_most_one_unit()
    {
        var field = new Battlefield();
        BattleUnit first = Unit(BattleSide.Player, 5, 2);
        Assert.True(field.TryPlace(first, out _));

        BattleUnit second = Unit(BattleSide.Player, 5, 2);
        Assert.False(field.TryPlace(second, out PlacementError? error));

        Assert.Equal(PlacementErrorKind.TileOccupied, error.Kind);
        Assert.Equal(new HexCoord(5, 2), error.Coord);
        Assert.Same(first, field.UnitAt(new HexCoord(5, 2)));
    }

    [Fact]
    public void Placing_off_the_board_is_a_reason_not_an_exception()
    {
        // A player asking for an impossible tile is input, not a bug, so it
        // comes back as a value.
        var field = new Battlefield();

        Assert.False(field.TryPlace(Unit(BattleSide.Player, 9, 0), out PlacementError? error));

        Assert.Equal(PlacementErrorKind.OutOfBounds, error.Kind);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(8, 0)]
    [InlineData(0, 8)]
    public void Asking_about_a_tile_that_does_not_exist_throws(int row, int col)
    {
        // The other side of the line: a rejected placement is a player's
        // mistake, but reading a tile off the board is a programmer's.
        var field = new Battlefield();

        Assert.Throws<ArgumentOutOfRangeException>(() => field.UnitAt(new HexCoord(row, col)));
    }

    [Fact]
    public void Rejects_a_missing_unit()
    {
        var field = new Battlefield();

        Assert.Throws<ArgumentNullException>(() => field.TryPlace(null!, out _));
    }

    // --- movement, which is not deployment -----------------------------------

    [Fact]
    public void An_empty_tile_on_the_board_is_passable()
    {
        var field = new Battlefield();

        Assert.True(field.IsPassable(new HexCoord(3, 4)));
    }

    [Fact]
    public void An_occupied_tile_is_not_passable()
    {
        var field = new Battlefield();
        Assert.True(field.TryPlace(Unit(BattleSide.Player, 6, 2), out _));

        Assert.False(field.IsPassable(new HexCoord(6, 2)));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(8, 0)]
    [InlineData(0, 8)]
    public void A_tile_off_the_board_is_not_passable(int row, int col)
    {
        // Answers rather than throwing, unlike UnitAt: a mover asking about all
        // six neighbours of an edge tile is asking a reasonable question, and
        // making it pre-filter for bounds would put this check at every call
        // site.
        var field = new Battlefield();

        Assert.False(field.IsPassable(new HexCoord(row, col)));
    }

    [Fact]
    public void Moving_a_unit_takes_the_board_and_the_unit_with_it()
    {
        var field = new Battlefield();
        BattleUnit unit = Unit(BattleSide.Player, 5, 2);
        Assert.True(field.TryPlace(unit, out _));

        field.Move(unit, new HexCoord(4, 2));

        Assert.Equal(new HexCoord(4, 2), unit.Position);
        Assert.Same(unit, field.UnitAt(new HexCoord(4, 2)));
        Assert.Null(field.UnitAt(new HexCoord(5, 2)));
    }

    [Fact]
    public void Movement_ignores_the_bands_that_deployment_enforces()
    {
        // The whole reason movement has its own entry point: a unit advancing
        // from row 5 into the neutral band, and on into the opposing half, is
        // making legal moves onto tiles it could never have deployed on.
        var field = new Battlefield();
        BattleUnit unit = Unit(BattleSide.Player, 5, 2);
        Assert.True(field.TryPlace(unit, out _));

        field.Move(unit, new HexCoord(5, 3));
        field.Move(unit, new HexCoord(5, 4));
        field.Move(unit, new HexCoord(5, 6));

        Assert.Equal(new HexCoord(5, 6), unit.Position);
        Assert.NotNull(Battlefield.CheckTile(BattleSide.Player, new HexCoord(5, 6)));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(8, 0)]
    [InlineData(0, 8)]
    public void Moving_off_the_board_throws(int row, int col)
    {
        var field = new Battlefield();
        BattleUnit unit = Unit(BattleSide.Player, 7, 0);
        Assert.True(field.TryPlace(unit, out _));

        Assert.Throws<ArgumentOutOfRangeException>(() => field.Move(unit, new HexCoord(row, col)));
    }

    [Fact]
    public void Moving_onto_an_occupied_tile_throws()
    {
        // A blocked advance is not a failed move: it is a unit that decided to
        // stay put, and it decided before the board was ever asked. Anything
        // reaching Move with an occupied destination skipped that decision.
        var field = new Battlefield();
        BattleUnit mover = Unit(BattleSide.Player, 6, 2);
        BattleUnit blocker = Unit(BattleSide.Player, 5, 2);
        Assert.True(field.TryPlace(mover, out _));
        Assert.True(field.TryPlace(blocker, out _));

        Assert.Throws<InvalidOperationException>(() => field.Move(mover, new HexCoord(5, 2)));

        Assert.Equal(new HexCoord(6, 2), mover.Position);
    }

    [Fact]
    public void Moving_a_unit_the_board_does_not_hold_throws()
    {
        // Catches the board and its units drifting apart — a unit whose Position
        // was written directly rather than through Move would otherwise leave a
        // ghost on the tile it claims to have come from.
        var field = new Battlefield();
        BattleUnit unit = Unit(BattleSide.Player, 5, 2);

        Assert.Throws<InvalidOperationException>(() => field.Move(unit, new HexCoord(4, 2)));
    }

    [Fact]
    public void Moving_rejects_a_missing_unit()
    {
        var field = new Battlefield();

        Assert.Throws<ArgumentNullException>(() => field.Move(null!, new HexCoord(4, 2)));
    }

    // --- removal, for a unit that has died ------------------------------------

    [Fact]
    public void Removing_a_unit_frees_its_tile()
    {
        var field = new Battlefield();
        BattleUnit unit = Unit(BattleSide.Player, 6, 2);
        Assert.True(field.TryPlace(unit, out _));

        field.Remove(unit);

        Assert.Null(field.UnitAt(new HexCoord(6, 2)));
        Assert.False(field.IsOccupied(new HexCoord(6, 2)));
        Assert.True(field.IsPassable(new HexCoord(6, 2)));
    }

    [Fact]
    public void Removing_does_not_touch_the_units_own_position()
    {
        // The corpse still knows where it fell. Harmless once nothing may
        // target or path through it, and simpler than inventing a value to
        // reset Position to.
        var field = new Battlefield();
        BattleUnit unit = Unit(BattleSide.Player, 6, 2);
        Assert.True(field.TryPlace(unit, out _));

        field.Remove(unit);

        Assert.Equal(new HexCoord(6, 2), unit.Position);
    }

    [Fact]
    public void Removing_a_unit_the_board_does_not_hold_throws()
    {
        var field = new Battlefield();
        BattleUnit unit = Unit(BattleSide.Player, 5, 2);

        Assert.Throws<InvalidOperationException>(() => field.Remove(unit));
    }

    [Fact]
    public void Removing_a_unit_twice_throws_the_second_time()
    {
        var field = new Battlefield();
        BattleUnit unit = Unit(BattleSide.Player, 5, 2);
        Assert.True(field.TryPlace(unit, out _));

        field.Remove(unit);

        Assert.Throws<InvalidOperationException>(() => field.Remove(unit));
    }

    [Fact]
    public void Removing_rejects_a_missing_unit()
    {
        var field = new Battlefield();

        Assert.Throws<ArgumentNullException>(() => field.Remove(null!));
    }
}
