namespace GameSim;

/// <summary>
/// A draw isn't a <see cref="BattleSide"/>, so this isn't a nullable side -
/// that would push the null case into every caller. Computed from
/// <see cref="BattleEnding"/> via <see cref="BattleResult.Outcome"/>.
/// </summary>
public enum BattleOutcome
{
    Draw = 0,

    PlayerWin = 1,

    OpponentWin = 2,
}
