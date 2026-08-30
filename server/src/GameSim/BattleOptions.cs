namespace GameSim;

/// <summary>
/// The knobs a battle runs under. Separate from the seed and the setup because
/// these are rules of the match, not of the armies in it.
/// </summary>
public sealed record BattleOptions
{
    /// <summary>
    /// The tick cap used unless a caller sets one.
    ///
    /// Read against the built-in roster: the slowest unit crosses a hex in 8
    /// ticks and the longest attack interval is 24, so on an 8-row board even a
    /// pair of tanks trading at maximum range resolves in low hundreds of ticks.
    /// A thousand is therefore comfortably above any battle that is making
    /// progress, and is only reached by one that has genuinely stalled.
    /// </summary>
    public const int DefaultMaxTicks = 1000;

    /// <summary>The options a battle runs under when a caller supplies none.</summary>
    public static BattleOptions Default { get; } = new();

    /// <summary>
    /// Ticks the battle may run for before it is called a draw. Must be
    /// positive: a battle that is not allowed to run at all is a caller's
    /// mistake, not a zero-tick draw.
    /// </summary>
    public int MaxTicks
    {
        get => field;
        init => field = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(value), value, "MaxTicks must be positive.");
    } = DefaultMaxTicks;
}
