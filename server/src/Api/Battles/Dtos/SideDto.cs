namespace Api.Battles.Dtos;

/// <summary>
/// Which army a unit belongs to, as exposed over the wire - a deliberate copy
/// of <see cref="GameSim.BattleSide"/>, kept separate so the sim's type never
/// appears in the OpenAPI contract.
/// </summary>
public enum SideDto
{
    Player = 0,
    Opponent = 1,
}
