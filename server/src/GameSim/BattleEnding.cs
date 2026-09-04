namespace GameSim;

public enum BattleEnding
{
    PlayerEliminatedOpponent = 0,

    OpponentEliminatedPlayer = 1,

    /// <summary>Both sides wiped out on the same tick.</summary>
    MutualElimination = 2,

    /// <summary>
    /// Reached <see cref="BattleOptions.MaxTicks"/> with survivors on both
    /// sides. Never decided by unit count, HP, or damage - only elimination
    /// ends a battle in a win.
    /// </summary>
    TickCapDraw = 3,
}
