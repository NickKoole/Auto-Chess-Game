namespace GameSim;

/// <summary>
/// A hex in cube coordinates, where the three axes sum to zero.
///
/// Offset coordinates are the storage format — they index a rectangular array
/// and read like a grid. Cube is the arithmetic format: distance, neighbours and
/// line-drawing are all trivial here and all fiddly in offset space. Convert,
/// compute, convert back.
/// </summary>
public readonly record struct CubeCoord
{
    /// <summary>
    /// Creates a cube coordinate, rejecting any triple that is not on the hex
    /// plane. A cube coordinate whose axes do not sum to zero is not a hex, so
    /// this is a programmer error rather than a value to carry around.
    /// </summary>
    public CubeCoord(int x, int y, int z)
    {
        if (x + y + z != 0)
        {
            throw new ArgumentException(
                $"Cube coordinates must sum to zero, but ({x}, {y}, {z}) sums to {x + y + z}.",
                nameof(x));
        }

        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>The x axis. Increases to the east.</summary>
    public int X { get; }

    /// <summary>The y axis. Implied by the other two, since all three sum to zero.</summary>
    public int Y { get; }

    /// <summary>The z axis. Increases to the south, so it is the offset row.</summary>
    public int Z { get; }

    /// <summary>
    /// Hex distance: the number of steps between two hexes.
    ///
    /// Half the sum of the axis deltas, in pure integer maths. The sum is always
    /// even — the axes sum to zero, so the deltas do too — which means the
    /// division is exact and never truncates.
    /// </summary>
    public int DistanceTo(CubeCoord other) =>
        (Math.Abs(X - other.X) + Math.Abs(Y - other.Y) + Math.Abs(Z - other.Z)) / 2;

    public override string ToString() => $"({X}, {Y}, {Z})";
}
