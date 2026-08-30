namespace GameSim;

/// <summary>
/// Why the loop stopped, which is a different question from who won.
///
/// The pair matters: a <see cref="BattleOutcome.Draw"/> by
/// <see cref="Elimination"/> is two armies destroying each other, while a draw
/// by <see cref="TickCap"/> is a fight that could not resolve itself. The first
/// is a result; the second is usually a balance problem worth surfacing.
/// </summary>
public enum BattleEndReason
{
    /// <summary>At least one side had no living units left.</summary>
    Elimination = 0,

    /// <summary>
    /// The battle reached <see cref="BattleOptions.MaxTicks"/> with both sides
    /// still standing, and was resolved as a draw.
    /// </summary>
    TickCap = 1,
}
