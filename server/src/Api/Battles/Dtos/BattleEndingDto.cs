namespace Api.Battles.Dtos;

/// <summary>Mirrors <see cref="GameSim.BattleEnding"/>.</summary>
public enum BattleEndingDto
{
    PlayerEliminatedOpponent = 0,
    OpponentEliminatedPlayer = 1,
    MutualElimination = 2,
    TickCapDraw = 3,
}
