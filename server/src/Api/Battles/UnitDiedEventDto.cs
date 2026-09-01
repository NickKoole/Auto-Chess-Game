namespace Api.Battles;

/// <summary>A unit was removed from the battle. See <see cref="GameSim.UnitDiedEvent"/>.</summary>
public sealed record UnitDiedEventDto : BattleEventDto
{
    public required int UnitId { get; init; }

    public required HexCoordDto Position { get; init; }
}
