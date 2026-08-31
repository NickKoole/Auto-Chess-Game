namespace GameSim;

/// <summary>
/// Exactly how a finished battle ended.
///
/// Replaces the earlier <c>BattleOutcome</c> x <c>BattleEndReason</c> pair, which
/// made six combinations constructible when only four are real — a tick-cap win
/// was nonsense that still compiled. With the no-tiebreak rule settled (victory
/// requires elimination; a stalemate is a genuine draw), these four are the whole
/// space, so there is nothing left to represent that cannot happen.
/// </summary>
public enum BattleEnding
{
    /// <summary>The player wiped out the opponent, with player units still standing.</summary>
    PlayerEliminatedOpponent = 0,

    /// <summary>The opponent wiped out the player, with opponent units still standing.</summary>
    OpponentEliminatedPlayer = 1,

    /// <summary>Both sides were wiped out on the same tick. Neither wins.</summary>
    MutualElimination = 2,

    /// <summary>
    /// The battle reached <see cref="BattleOptions.MaxTicks"/> with at least one
    /// unit alive on both sides. However lopsided the survivors, this is never
    /// decided by unit count, remaining HP, or damage dealt — only elimination
    /// ever ends a battle in a win, so a battle neither side wins is a draw.
    /// </summary>
    TickCapDraw = 3,
}
