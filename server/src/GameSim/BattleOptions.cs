namespace GameSim;

public sealed record BattleOptions
{
    /// <summary>
    /// The slowest unit crosses a hex in 8 ticks and the longest attack
    /// interval is 24, so even two tanks trading at range resolve in low
    /// hundreds of ticks - 1000 is comfortably above any battle still making
    /// progress.
    /// </summary>
    public const int DefaultMaxTicks = 1000;

    public static BattleOptions Default { get; } = new();

    /// <summary>Must be positive - a battle that isn't allowed to run at all is a caller's mistake, not a zero-tick draw.</summary>
    public int MaxTicks
    {
        get => field;
        // The parameter name is spelled out rather than taken from
        // nameof(value): inside an init accessor that is the literal "value",
        // which tells a caller nothing about which property they got wrong.
        init => field = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(
                "MaxTicks", value, "MaxTicks must be positive.");
    } = DefaultMaxTicks;
}
