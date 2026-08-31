namespace GameSim;

/// <summary>
/// Who won a battle, from the player's point of view.
///
/// Stated from the player's point of view rather than as a winning
/// <see cref="BattleSide"/>, because a draw is not a side and modelling it as a
/// nullable side would push that special case into every caller. Computed from
/// the more precise <see cref="BattleEnding"/> via <see cref="BattleResult.Outcome"/>.
/// </summary>
public enum BattleOutcome
{
    /// <summary>Neither side won: both survived to the tick cap, or both were wiped out together.</summary>
    Draw = 0,

    /// <summary>The player's army was the one left standing.</summary>
    PlayerWin = 1,

    /// <summary>The opposing army was the one left standing.</summary>
    OpponentWin = 2,
}
