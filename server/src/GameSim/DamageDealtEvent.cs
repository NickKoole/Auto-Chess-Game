namespace GameSim;

/// <summary>
/// Fires on every landed hit, lethal or not - a killing blow gets this AND a
/// separate <see cref="UnitDiedEvent"/>, not one instead of the other.
/// Carries <see cref="AttackerId"/> even though a
/// <see cref="UnitAttackedEvent"/> naming the same attacker always precedes
/// it: a consumer reading only this event type shouldn't need the whole
/// stream to answer "by whom".
/// </summary>
public sealed record DamageDealtEvent : BattleEvent
{
    public required int AttackerId { get; init; }

    public required int TargetId { get; init; }

    public required int Amount { get; init; }

    /// <summary>The exact value <see cref="BattleUnit.CurrentHp"/> holds immediately after this hit.</summary>
    public required int ResultingHp { get; init; }
}
