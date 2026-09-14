using GameSim;

namespace GameSim.Tests;

public sealed class HexCoordTests
{
    /// <summary>Every tile on the 8x8 board, as test data.</summary>
    public static TheoryData<int, int> EveryTile()
    {
        var data = new TheoryData<int, int>();

        for (int row = 0; row < Battlefield.Rows; row++)
        {
            for (int col = 0; col < Battlefield.Columns; col++)
            {
                data.Add(row, col);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryTile))]
    public void Cube_conversion_round_trips_for_every_tile(int row, int col)
    {
        var coord = new HexCoord(row, col);

        Assert.Equal(coord, HexCoord.FromCube(coord.ToCube()));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(-3, 5)]
    [InlineData(0, -4)]
    [InlineData(-7, -7)]
    public void Cube_conversion_round_trips_outside_the_board(int row, int col)
    {
        // The shift is written to stay exact for negative rows, where a plain
        // Row / 2 would truncate toward zero and lose a half-hex. Nothing on the
        // board is negative today, but the overworld map shares this arithmetic.
        var coord = new HexCoord(row, col);

        Assert.Equal(coord, HexCoord.FromCube(coord.ToCube()));
    }

    [Theory]
    // Hand-computed from the flat-top odd-q formula x = col,
    // z = row - (col - (col & 1)) / 2, y = -x - z.
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(0, 7, 7, -4, -3)]
    [InlineData(3, 3, 3, -5, 2)]
    [InlineData(4, 3, 3, -6, 3)]
    [InlineData(7, 7, 7, -11, 4)]
    public void Converts_to_the_expected_cube_coordinate(int row, int col, int x, int y, int z)
    {
        Assert.Equal(new CubeCoord(x, y, z), new HexCoord(row, col).ToCube());
    }

    [Fact]
    public void Cube_rejects_axes_that_do_not_sum_to_zero()
    {
        // Not a hex. Constructing one would silently corrupt every distance
        // computed from it, so it throws rather than being carried around.
        Assert.Throws<ArgumentException>(() => new CubeCoord(1, 1, 1));
    }

    [Fact]
    public void Distance_to_itself_is_zero()
    {
        var coord = new HexCoord(5, 2);

        Assert.Equal(0, coord.DistanceTo(coord));
    }

    [Theory]
    // An odd column leans south.
    [InlineData(3, 3, 3, 2)]
    [InlineData(3, 3, 4, 2)]
    [InlineData(3, 3, 2, 3)]
    [InlineData(3, 3, 4, 3)]
    [InlineData(3, 3, 3, 4)]
    [InlineData(3, 3, 4, 4)]
    // An even column leans north.
    [InlineData(3, 4, 3, 3)]
    [InlineData(3, 4, 2, 3)]
    public void Adjacent_hexes_are_one_apart(int aRow, int aCol, int bRow, int bCol)
    {
        var a = new HexCoord(aRow, aCol);
        var b = new HexCoord(bRow, bCol);

        Assert.Equal(1, a.DistanceTo(b));
        Assert.Equal(1, b.DistanceTo(a));
    }

    [Theory]
    // The other corners around an odd-column tile are two steps away.
    [InlineData(3, 3, 2, 2)]
    [InlineData(3, 3, 2, 4)]
    [InlineData(3, 3, 5, 3)]
    public void Diagonally_offset_hexes_are_two_apart(int aRow, int aCol, int bRow, int bCol)
    {
        // Guards the odd-q choice specifically: reversing the column parity
        // turns these into neighbours.
        var a = new HexCoord(aRow, aCol);
        var b = new HexCoord(bRow, bCol);

        Assert.Equal(2, a.DistanceTo(b));
    }

    [Theory]
    // Opposite corners of the board.
    [InlineData(0, 0, 7, 11, 13)]
    [InlineData(0, 11, 7, 0, 12)]
    // Two more tiles that were the board's corners before it widened - still
    // valid known distances, just no longer the extremes.
    [InlineData(0, 0, 7, 7, 11)]
    [InlineData(0, 7, 7, 0, 10)]
    // Straight down a column: one hex per row, the zigzag costs nothing.
    [InlineData(7, 3, 0, 3, 7)]
    [InlineData(5, 0, 0, 0, 5)]
    // Straight along a row, the full new width.
    [InlineData(0, 0, 0, 11, 11)]
    // Across the neutral band, the shortest a battle line ever has to close.
    [InlineData(5, 4, 5, 7, 3)]
    public void Distance_matches_known_cases(int aRow, int aCol, int bRow, int bCol, int expected)
    {
        var a = new HexCoord(aRow, aCol);
        var b = new HexCoord(bRow, bCol);

        Assert.Equal(expected, a.DistanceTo(b));
        Assert.Equal(expected, b.DistanceTo(a));
    }

    [Fact]
    public void Neighbours_are_the_six_hexes_one_step_away()
    {
        // Checked well outside the board as well as on it, because the mover
        // asks for the neighbours of an edge tile and filters the answers — so
        // the ones that fall off still have to be the right ones.
        var wrong = new List<string>();

        for (int row = -4; row < Battlefield.Rows + 4; row++)
        {
            for (int col = -4; col < Battlefield.Columns + 4; col++)
            {
                var centre = new HexCoord(row, col);
                HexCoord[] neighbours = centre.Neighbours();

                if (neighbours.Length != 6 || neighbours.Distinct().Count() != 6)
                {
                    wrong.Add($"{centre} returned {neighbours.Length} neighbours");
                    continue;
                }

                foreach (HexCoord neighbour in neighbours)
                {
                    int distance = centre.DistanceTo(neighbour);
                    if (distance != 1)
                    {
                        wrong.Add($"{centre} -> {neighbour} was {distance}");
                    }
                }
            }
        }

        Assert.Empty(wrong);
    }

    [Theory]
    // An odd column leans south.
    [InlineData(3, 3, new[] { 2, 3, 3, 2, 3, 4, 4, 2, 4, 3, 4, 4 })]
    // An even column leans north.
    [InlineData(4, 4, new[] { 3, 3, 3, 4, 3, 5, 4, 3, 4, 5, 5, 4 })]
    public void Neighbours_follow_the_odd_q_lean(int row, int col, int[] expected)
    {
        // Stated as literal coordinates rather than recomputed from the lean, so
        // a flipped parity fails here instead of being confirmed by the same
        // mistake twice.
        HexCoord[] want =
        [
            .. Enumerable.Range(0, expected.Length / 2)
                .Select(i => new HexCoord(expected[i * 2], expected[(i * 2) + 1]))
        ];

        Assert.Equal(want.OrderBy(h => h.Row).ThenBy(h => h.Col),
                     new HexCoord(row, col).Neighbours().OrderBy(h => h.Row).ThenBy(h => h.Col));
    }

    [Fact]
    public void Every_tile_has_exactly_six_neighbours_one_hex_away()
    {
        // The odd-q neighbour rule stated independently of the conversion: odd
        // columns lean south, even columns lean north. Checking it on every tile of the
        // board catches a parity mistake that a single hand-picked example would
        // sail past.
        var wrong = new List<string>();

        for (int row = -4; row < Battlefield.Rows + 4; row++)
        {
            for (int col = -4; col < Battlefield.Columns + 4; col++)
            {
                var centre = new HexCoord(row, col);
                int lean = (col & 1) == 1 ? 1 : -1;

                HexCoord[] neighbours =
                [
                    new(row, col - 1),
                    new(row + lean, col - 1),
                    new(row - 1, col),
                    new(row + 1, col),
                    new(row, col + 1),
                    new(row + lean, col + 1),
                ];

                foreach (HexCoord neighbour in neighbours)
                {
                    int distance = centre.DistanceTo(neighbour);
                    if (distance != 1)
                    {
                        wrong.Add($"{centre} -> {neighbour} was {distance}");
                    }
                }
            }
        }

        Assert.Empty(wrong);
    }
}
