namespace Api.Battles.Dtos;

/// <summary>Mirrors <see cref="GameSim.UnitDiedEvent"/>.</summary>
public sealed record UnitDiedEventDto : BattleEventDto
{
    public required int UnitId { get; init; }

    public required HexCoordDto Position { get; init; }
}
