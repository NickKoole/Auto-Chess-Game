namespace GameSim;

/// <summary>
/// A hex on a pointy-top grid, in offset coordinates: zero-indexed row and
/// column, rows running top to bottom.
///
/// The layout is odd-r — odd-numbered rows sit half a hex to the right —
/// which is the arrangement every formula here assumes. This type knows nothing
/// about how large the board is or what may stand on it: those are a
/// <see cref="Battlefield"/>'s business, and keeping them apart is what lets the
/// overworld map reuse this arithmetic without inheriting the battlefield's
/// rules.
///
/// A pointy-top grid has no direct north or south neighbour, so closing vertical
/// distance means alternating NE/NW. That still costs one hex per row — the
/// zigzag is free.
/// </summary>
/// <param name="Row">Row index, increasing southwards.</param>
/// <param name="Col">Column index, increasing eastwards.</param>
public readonly record struct HexCoord(int Row, int Col)
{
    /// <summary>
    /// This hex in cube coordinates.
    ///
    /// The shift is <c>(Row - (Row &amp; 1)) / 2</c> rather than <c>Row / 2</c>
    /// so it stays exact for negative rows: C# integer division truncates toward
    /// zero, but the numerator here is always even, so nothing is lost either
    /// side of the origin.
    /// </summary>
    public CubeCoord ToCube()
    {
        int x = Col - ((Row - (Row & 1)) / 2);
        int z = Row;
        return new CubeCoord(x, -x - z, z);
    }

    /// <summary>The offset coordinate for a cube coordinate. Exactly inverts <see cref="ToCube"/>.</summary>
    public static HexCoord FromCube(CubeCoord cube) =>
        new(cube.Z, cube.X + ((cube.Z - (cube.Z & 1)) / 2));

    /// <summary>
    /// The six hexes adjacent to this one, under the odd-r layout: odd rows
    /// lean east, even rows lean west, so the two northern neighbours are
    /// <c>(Row - 1, Col)</c> and <c>(Row - 1, Col + lean)</c> and the two
    /// southern ones mirror them. East and west are always <c>Col ± 1</c>.
    ///
    /// <para>
    /// The order they come back in is fixed but arbitrary — north-west pair,
    /// west, east, south pair — and no caller may let it decide anything. A
    /// mover choosing between two equally good steps has to break the tie on
    /// the coordinates themselves, or the outcome would depend on the order
    /// this method happens to build its array in, which is precisely the class
    /// of hidden ordering dependency the determinism rules exist to prevent.
    /// </para>
    ///
    /// <para>
    /// Bounds are not this type's business: a hex on the edge of a board still
    /// has six neighbours, some of which are off that particular board. Filter
    /// with <see cref="Battlefield.IsInBounds"/>.
    /// </para>
    /// </summary>
    public HexCoord[] Neighbours()
    {
        // Odd rows sit half a hex to the right, so their diagonal neighbours are
        // the column they are in and the one east of it; even rows take the
        // column they are in and the one west of it.
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
