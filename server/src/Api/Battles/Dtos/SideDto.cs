namespace Api.Battles.Dtos;

/// <summary>
/// Which army a unit belongs to, as exposed over the wire.
///
/// A copy of <see cref="GameSim.BattleSide"/> rather than the sim's own enum:
/// the API's response is composed entirely of DTOs it owns, so the sim's type
/// never appears in the OpenAPI contract or the JSON it describes.
/// </summary>
public enum SideDto
{
    Player = 0,
    Opponent = 1,
}
