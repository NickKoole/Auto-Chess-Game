namespace Api.Battles.Dtos;

/// <summary>Mirrors <see cref="GameSim.DamageDealtEvent"/>.</summary>
public sealed record DamageDealtEventDto : BattleEventDto
{
    public required int AttackerId { get; init; }

    public required int TargetId { get; init; }

    public required int Amount { get; init; }

    public required int ResultingHp { get; init; }
}
