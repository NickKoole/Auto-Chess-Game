namespace Api.Battles;

/// <summary>A unit's hit points changed from a landed hit. See <see cref="GameSim.DamageDealtEvent"/>.</summary>
public sealed record DamageDealtEventDto : BattleEventDto
{
    public required int AttackerId { get; init; }

    public required int TargetId { get; init; }

    public required int Amount { get; init; }

    public required int ResultingHp { get; init; }
}
