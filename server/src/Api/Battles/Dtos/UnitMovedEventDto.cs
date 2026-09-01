namespace Api.Battles.Dtos;

/// <summary>A unit stepped from one hex to an adjacent one. See <see cref="GameSim.UnitMovedEvent"/>.</summary>
public sealed record UnitMovedEventDto : BattleEventDto
{
    public required int UnitId { get; init; }

    public required HexCoordDto From { get; init; }

    public required HexCoordDto To { get; init; }
}
