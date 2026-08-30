namespace GameSim;

/// <summary>
/// What a finished battle came to: who won, why it stopped, and when.
///
/// Plain data with value equality, which is what lets a determinism test assert
/// two runs are the same by comparing two results directly.
/// </summary>
/// <param name="Outcome">Who won, from the player's point of view.</param>
/// <param name="Reason">What stopped the loop.</param>
/// <param name="EndTick">
/// The number of ticks that ran. Zero means the battle was already decided
/// before the first tick — every unit on one side was dead at the setup.
/// </param>
public readonly record struct BattleResult(BattleOutcome Outcome, BattleEndReason Reason, int EndTick);
