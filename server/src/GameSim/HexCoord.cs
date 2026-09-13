namespace GameSim;

/// <summary>
/// A flat-top hex in offset coordinates: zero-indexed row and column, with
/// odd columns shifted half a hex south. Board size and occupancy are
/// <see cref="Battlefield"/>'s business, not this type's.
/// </summary>
/// <param name="Row">Row index, increasing southwards.</param>
/// <param name="Col">Column index, increasing eastwards.</param>
public readonly record struct HexCoord(int Row, int Col)
{
    /// <summary>
    /// <c>(Col - (Col &amp; 1)) / 2</c> keeps the offset exact for negative
    /// columns, where a plain <c>Col / 2</c> would truncate toward zero.
    /// </summary>
    public CubeCoord ToCube()
    {
        int x = Col;
        int z = Row - ((Col - (Col & 1)) / 2);
        return new CubeCoord(x, -x - z, z);
    }

    /// <summary>Exactly inverts <see cref="ToCube"/>.</summary>
    public static HexCoord FromCube(CubeCoord cube) =>
        new(cube.Z + ((cube.X - (cube.X & 1)) / 2), cube.X);

    /// <summary>
    /// The six adjacent hexes. Odd columns lean south and even columns lean
    /// north. Order is fixed but arbitrary -
    /// no caller may let it decide anything; a mover choosing between two
    /// equally good steps must break the tie on the coordinates themselves.
    /// Not bounds-checked: an edge hex still returns six neighbours, some off
    /// the board - filter with <see cref="Battlefield.IsInBounds"/>.
    /// </summary>
    public HexCoord[] Neighbours()
    {
        int lean = (Col & 1) == 1 ? 1 : -1;

        return
        [
            new(Row, Col - 1),
            new(Row + lean, Col - 1),
            new(Row - 1, Col),
            new(Row + 1, Col),
            new(Row, Col + 1),
            new(Row + lean, Col + 1),
        ];
    }

    /// <summary>
    /// Hex distance to another coordinate: 1 for an adjacent hex, 0 for itself.
    /// </summary>
    public int DistanceTo(HexCoord other) => ToCube().DistanceTo(other.ToCube());

    public override string ToString() => $"(row {Row}, col {Col})";
}
