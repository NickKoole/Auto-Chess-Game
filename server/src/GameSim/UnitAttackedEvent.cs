namespace GameSim;

/// <summary>
/// A unit landed a hit on another. Always followed immediately by the
/// <see cref="DamageDealtEvent"/> it caused — the two are separate events so a
/// client can play a strike animation and a damage animation independently,
/// but they are never emitted apart.
/// </summary>
public sealed record UnitAttackedEvent : BattleEvent
{
    /// <summary>The unit that struck.</summary>
    public required int AttackerId { get; init; }

    /// <summary>The unit it hit.</summary>
    public required int TargetId { get; init; }
}
