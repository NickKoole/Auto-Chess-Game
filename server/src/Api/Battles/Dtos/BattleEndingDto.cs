namespace Api.Battles.Dtos;

/// <summary>Exactly how a finished battle ended. See <see cref="GameSim.BattleEnding"/>.</summary>
public enum BattleEndingDto
{
    PlayerEliminatedOpponent = 0,
    OpponentEliminatedPlayer = 1,
    MutualElimination = 2,
    TickCapDraw = 3,
}
