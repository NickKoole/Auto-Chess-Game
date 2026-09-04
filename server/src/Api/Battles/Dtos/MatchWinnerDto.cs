namespace Api.Battles.Dtos;

/// <summary>Mirrors <see cref="GameSim.BattleOutcome"/>, from the player's point of view.</summary>
public enum MatchWinnerDto
{
    Draw = 0,
    Player = 1,
    Opponent = 2,
}
