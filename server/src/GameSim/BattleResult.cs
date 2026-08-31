namespace GameSim;

/// <summary>
/// What a finished battle came to: how it ended and when.
///
/// Plain data with value equality, which is what lets a determinism test assert
/// two runs are the same by comparing two results directly.
/// </summary>
/// <param name="Ending">Exactly how the battle ended. See <see cref="BattleEnding"/>.</param>
/// <param name="EndTick">
/// The number of ticks that ran. Zero means the battle was already decided
/// before the first tick — every unit on one side was dead at the setup.
/// </param>
public readonly record struct BattleResult(BattleEnding Ending, int EndTick)
{
    /// <summary>
    /// Who won, from the player's point of view.
    ///
    /// A computed view over <see cref="Ending"/> for callers that only care who
    /// won and not how: "who won" is asked far more often than "how did it end",
    /// and this spares every one of those callers from matching all four
    /// <see cref="BattleEnding"/> members just to answer it.
    /// </summary>
    public BattleOutcome Outcome => Ending switch
    {
        BattleEnding.PlayerEliminatedOpponent => BattleOutcome.PlayerWin,
        BattleEnding.OpponentEliminatedPlayer => BattleOutcome.OpponentWin,
        BattleEnding.MutualElimination => BattleOutcome.Draw,
        BattleEnding.TickCapDraw => BattleOutcome.Draw,
        _ => throw new ArgumentOutOfRangeException(nameof(Ending), Ending, "Unknown battle ending."),
    };
}
