namespace Api.Battles.Dtos;

/// <summary>Mirrors <see cref="GameSim.UnitAttackedEvent"/>.</summary>
public sealed record UnitAttackedEventDto : BattleEventDto
{
    public required int AttackerId { get; init; }

    public required int TargetId { get; init; }
}
