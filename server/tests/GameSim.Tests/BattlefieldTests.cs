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
    [InlineData(BattleSide.Player, 5, 7)]
    [InlineData(BattleSide.Opponent, 0, 2)]
    public void Deployment_bands_leave_rows_three_and_four_neutral(BattleSide side, int firstRow, int lastRow)
    {
        Assert.Equal((firstRow, lastRow), Battlefield.DeploymentBand(side));
    }

    [Theory]
    // The player advances north toward row 0, the opponent south toward row 7,
    // so the same row means opposite things to the two of them.
    [InlineData(BattleSide.Player, 7, 7)]
    [InlineData(BattleSide.Player, 5, 5)]
    [InlineData(BattleSide.Player, 0, 0)]
    [InlineData(BattleSide.Opponent, 0, 7)]
    [InlineData(BattleSide.Opponent, 2, 5)]
    [InlineData(BattleSide.Opponent, 7, 0)]
    public void Counts_the_rows_a_side_still_has_to_cross(BattleSide side, int row, int expected)
    {
        Assert.Equal(expected, Battlefield.RowsToEnemyBackLine(side, new HexCoord(row, 3)));
    }

    [Fact]
    public void Gives_mirrored_rows_the_same_number_of_rows_to_cross()
    {
        // The property the unit order relies on: reflect a tile across the
        // halfway line, hand it to the other side, and it is the same distance
        // along. Without it a mirror match could not order its two armies alike.
        for (int row = 0; row < Battlefield.Rows; row++)
        {
            Assert.Equal(
                Battlefield.RowsToEnemyBackLine(BattleSide.Player, new HexCoord(row, 3)),
                Battlefield.RowsToEnemyBackLine(
                    BattleSide.Opponent, new HexCoord(Battlefield.Rows - 1 - row, 3)));
        }
    }

    [Fact]
    public void Refuses_to_count_rows_for_a_side_that_does_not_exist()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Battlefield.RowsToEnemyBackLine((BattleSide)7, new HexCoord(5, 3)));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Neutral_band_spans_rows_three_and_four(int row)
    {
        Assert.True(Battlefield.IsNeutralBand(new HexCoord(row, 0)));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void Rows_outside_three_and_four_are_not_neutral(int row)
    {
        Assert.False(Battlefield.IsNeutralBand(new HexCoord(row, 0)));
    }

    [Theory]
    [InlineData(BattleSide.Player, 5)]
    [InlineData(BattleSide.Player, 6)]
    [InlineData(BattleSide.Player, 7)]
    [InlineData(BattleSide.Opponent, 0)]
    [InlineData(BattleSide.Opponent, 1)]
    [InlineData(BattleSide.Opponent, 2)]
    public void A_side_may_deploy_in_its_own_band(BattleSide side, int row)
    {
        Assert.Null(Battlefield.CheckTile(side, new HexCoord(row, 4)));
    }

    [Theory]
    [InlineData(BattleSide.Player, 3)]
    [InlineData(BattleSide.Player, 4)]
    [InlineData(BattleSide.Opponent, 3)]
    [InlineData(BattleSide.Opponent, 4)]
    public void Nobody_may_deploy_in_the_neutral_band(BattleSide side, int row)
    {
        PlacementError? error = Battlefield.CheckTile(side, new HexCoord(row, 2));

        Assert.NotNull(error);
        Assert.Equal(PlacementErrorKind.NeutralBand, error.Kind);
        Assert.Equal(side, error.Side);
        Assert.Equal(new HexCoord(row, 2), error.Coord);
    }

    [Theory]
    [InlineData(BattleSide.Player, 0)]
    [InlineData(BattleSide.Player, 2)]
    [InlineData(BattleSide.Opponent, 5)]
    [InlineData(BattleSide.Opponent, 7)]
    public void A_side_may_not_deploy_in_the_opposing_band(BattleSide side, int row)
    {
        PlacementError? error = Battlefield.CheckTile(side, new HexCoord(row, 1));

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
        BattleUnit unit = Unit(BattleSide.Player, 6, 3);

        Assert.True(field.TryPlace(unit, out PlacementError? error));

        Assert.Null(error);
        Assert.Same(unit, field.UnitAt(new HexCoord(6, 3)));
        Assert.True(field.IsOccupied(new HexCoord(6, 3)));
    }

    [Fact]
    public void Placing_a_unit_leaves_every_other_tile_empty()
    {
        var field = new Battlefield();
        Assert.True(field.TryPlace(Unit(BattleSide.Player, 6, 3), out _));

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
        Assert.Equal([new HexCoord(6, 3)], occupied);
    }

    [Fact]
    public void A_tile_holds_at_most_one_unit()
    {
        var field = new Battlefield();
        BattleUnit first = Unit(BattleSide.Player, 5, 5);
        Assert.True(field.TryPlace(first, out _));

        BattleUnit second = Unit(BattleSide.Player, 5, 5);
        Assert.False(field.TryPlace(second, out PlacementError? error));

        Assert.Equal(PlacementErrorKind.TileOccupied, error.Kind);
        Assert.Equal(new HexCoord(5, 5), error.Coord);
        Assert.Same(first, field.UnitAt(new HexCoord(5, 5)));
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
}
