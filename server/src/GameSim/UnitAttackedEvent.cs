namespace GameSim;

/// <summary>Always immediately followed by the <see cref="DamageDealtEvent"/> it caused - the two are never emitted apart.</summary>
public sealed record UnitAttackedEvent : BattleEvent
{
    public required int AttackerId { get; init; }

    public required int TargetId { get; init; }
}
