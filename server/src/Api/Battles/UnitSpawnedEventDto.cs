namespace Api.Battles;

/// <summary>A unit's starting state. See <see cref="GameSim.UnitSpawnedEvent"/>.</summary>
public sealed record UnitSpawnedEventDto : BattleEventDto
{
    public required int UnitId { get; init; }

    public required SideDto Side { get; init; }

    public required string DefinitionId { get; init; }

    public required HexCoordDto Position { get; init; }
}
