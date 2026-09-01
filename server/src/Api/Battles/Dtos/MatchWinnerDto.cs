namespace Api.Battles.Dtos;

/// <summary>Who won a battle, from the player's point of view. See <see cref="GameSim.BattleOutcome"/>.</summary>
public enum MatchWinnerDto
{
    Draw = 0,
    Player = 1,
    Opponent = 2,
}
