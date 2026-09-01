namespace GameSim;

/// <summary>
/// A unit's hit points changed from a landed hit — separate from
/// <see cref="UnitAttackedEvent"/> so who struck and what it did to the
/// target are two independently animatable moments, and separate from
/// <see cref="UnitDiedEvent"/> so a killing blow and a non-lethal hit can look
/// different: this fires either way, and death is its own event on top of it
/// when the hit was lethal.
/// </summary>
public sealed record DamageDealtEvent : BattleEvent
{
    /// <summary>The unit that took the hit.</summary>
    public required int TargetId { get; init; }

    /// <summary>Hit points removed by this hit.</summary>
    public required int Amount { get; init; }

    /// <summary>
    /// The target's hit points immediately after this hit — the exact value
    /// <see cref="BattleUnit.CurrentHp"/> holds at that moment, so it can go
    /// negative on an overkill rather than clamping at zero.
    /// </summary>
    public required int ResultingHp { get; init; }
}
