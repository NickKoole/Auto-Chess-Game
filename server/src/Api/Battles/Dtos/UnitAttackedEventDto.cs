namespace Api.Battles.Dtos;

/// <summary>A unit landed a hit on another. See <see cref="GameSim.UnitAttackedEvent"/>.</summary>
public sealed record UnitAttackedEventDto : BattleEventDto
{
    public required int AttackerId { get; init; }

    public required int TargetId { get; init; }
}
