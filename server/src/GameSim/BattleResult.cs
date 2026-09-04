namespace GameSim;

/// <summary>Value equality, so a determinism test can assert two runs are the same by comparing two results directly.</summary>
/// <param name="Ending">Exactly how the battle ended.</param>
/// <param name="EndTick">Zero means the battle was already decided at setup - every unit on one side was dead before the first tick.</param>
public readonly record struct BattleResult(BattleEnding Ending, int EndTick)
{
    /// <summary>Spares a caller who only cares who won from matching all four <see cref="BattleEnding"/> members.</summary>
    public BattleOutcome Outcome => Ending switch
    {
        BattleEnding.PlayerEliminatedOpponent => BattleOutcome.PlayerWin,
        BattleEnding.OpponentEliminatedPlayer => BattleOutcome.OpponentWin,
        BattleEnding.MutualElimination => BattleOutcome.Draw,
        BattleEnding.TickCapDraw => BattleOutcome.Draw,
        _ => throw new ArgumentOutOfRangeException(nameof(Ending), Ending, "Unknown battle ending."),
    };
}
