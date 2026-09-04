namespace GameSim;

/// <summary>
/// A hex on a pointy-top grid, in offset coordinates: zero-indexed row and
/// column, rows running top to bottom. Layout is odd-r - odd rows sit half a
/// hex to the right - which every formula here assumes. Board size and
/// occupancy are <see cref="Battlefield"/>'s business, not this type's.
///
/// No direct north or south neighbour, so closing vertical distance means
/// alternating NE/NW - that still costs one hex per row, not two.
/// </summary>
/// <param name="Row">Row index, increasing southwards.</param>
/// <param name="Col">Column index, increasing eastwards.</param>
public readonly record struct HexCoord(int Row, int Col)
{
    /// <summary>
    /// <c>(Row - (Row &amp; 1)) / 2</c> rather than <c>Row / 2</c>, so it stays
    /// exact for negative rows - the numerator is always even, so nothing is
    /// lost on either side of the origin.
    /// </summary>
    public CubeCoord ToCube()
    {
        int x = Col - ((Row - (Row & 1)) / 2);
        int z = Row;
        return new CubeCoord(x, -x - z, z);
    }

    /// <summary>Exactly inverts <see cref="ToCube"/>.</summary>
    public static HexCoord FromCube(CubeCoord cube) =>
        new(cube.Z, cube.X + ((cube.Z - (cube.Z & 1)) / 2));

    /// <summary>
    /// The six adjacent hexes. Odd rows lean east, even rows lean west, so the
    /// two northern neighbours are <c>(Row - 1, Col)</c> and
    /// <c>(Row - 1, Col + lean)</c>, mirrored to the south; east and west are
    /// always <c>Col - 1</c> or <c>Col + 1</c>.
    ///
    /// <para>
    /// Order is fixed but arbitrary - no caller may let it decide anything. A
    /// mover choosing between two equally good steps must break the tie on the
    /// coordinates themselves, not on this array's order.
    /// </para>
    ///
    /// <para>
    /// Not bounds-checked: an edge hex still returns six neighbours, some off
    /// the board. Filter with <see cref="Battlefield.IsInBounds"/>.
    /// </para>
    /// </summary>
    public HexCoord[] Neighbours()
    {
        int lean = (Row & 1) == 1 ? 1 : -1;

        return
        [
            new(Row - 1, Col),
            new(Row - 1, Col + lean),
            new(Row, Col - 1),
            new(Row, Col + 1),
            new(Row + 1, Col),
            new(Row + 1, Col + lean),
        ];
    }

    /// <summary>
    /// Hex distance to another coordinate: 1 for an adjacent hex, 0 for itself.
    /// </summary>
    public int DistanceTo(HexCoord other) => ToCube().DistanceTo(other.ToCube());

    public override string ToString() => $"(row {Row}, col {Col})";
}
