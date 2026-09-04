namespace GameSim;

/// <summary>
/// A hex in cube coordinates, where the three axes sum to zero. Offset
/// coordinates are the storage format - <see cref="HexCoord"/> indexes a
/// rectangular array and reads like a grid. Cube is the arithmetic format:
/// distance and neighbours are trivial here and fiddly in offset space.
/// </summary>
public readonly record struct CubeCoord
{
    /// <summary>Throws when the axes don't sum to zero - not a hex, so a programmer error rather than a value to carry around.</summary>
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

    /// <summary>Increases to the east.</summary>
    public int X { get; }

    /// <summary>Implied by the other two - all three sum to zero.</summary>
    public int Y { get; }

    /// <summary>Increases to the south; this is the offset row.</summary>
    public int Z { get; }

    /// <summary>
    /// Half the sum of the axis deltas. Always exact: the axes sum to zero, so
    /// the deltas do too, and the sum is always even.
    /// </summary>
    public int DistanceTo(CubeCoord other) =>
        (Math.Abs(X - other.X) + Math.Abs(Y - other.Y) + Math.Abs(Z - other.Z)) / 2;

    public override string ToString() => $"({X}, {Y}, {Z})";
}
