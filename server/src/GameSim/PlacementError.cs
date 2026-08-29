namespace GameSim;

/// <summary>
/// One reason an army setup was rejected, naming the side and — for everything
/// except <see cref="PlacementErrorKind.NoUnits"/> — the tile at fault.
/// <para>
/// An illegal setup is player input arriving over the wire, not a programmer
/// error, so it comes back as a value rather than as an exception. Validation
/// collects every one of these in a single pass: telling someone about one bad
/// tile at a time, five round trips deep, is a worse experience than telling
/// them about all five at once.
/// </para>
/// </summary>
/// <param name="Kind">Why the placement was rejected.</param>
/// <param name="Side">The side whose placement was rejected.</param>
/// <param name="Coord">The offending tile, or null when the error is about the whole list.</param>
/// <param name="Message">A human-readable explanation naming the rule that was broken.</param>
public sealed record PlacementError(
    PlacementErrorKind Kind,
    BattleSide Side,
    HexCoord? Coord,
    string Message);
