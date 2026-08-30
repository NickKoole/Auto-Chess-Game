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
    /// Hex distance to another coordinate: 1 for an adjacent hex, 0 for itself.
    /// </summary>
    public int DistanceTo(HexCoord other) => ToCube().DistanceTo(other.ToCube());

    public override string ToString() => $"(row {Row}, col {Col})";
}
