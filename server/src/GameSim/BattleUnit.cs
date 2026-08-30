namespace GameSim;

/// <summary>
/// One unit in one battle: a shared <see cref="UnitDefinition"/> plus everything
/// about it that changes as the fight runs.
///
/// A class rather than a record, because this is the one place in the
/// simulation where identity and mutation are both real. Two militia standing on
/// different tiles are different units even when every field matches, and a unit
/// that takes damage is still the same unit — so value equality would be wrong
/// and copying on every hit would be waste.
///
/// Nothing here writes to <see cref="Definition"/>: it is shared by every unit
/// of its kind, in this battle and every other.
/// </summary>
public sealed class BattleUnit
{
    /// <summary>
    /// Creates a unit ready to fight: at full health, with no attack pending.
    /// </summary>
    public BattleUnit(UnitDefinition definition, BattleSide side, HexCoord position)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Definition = definition;
        Side = side;
        Position = position;
        CurrentHp = definition.MaxHp;
        AttackCooldownTicks = 0;
    }

    /// <summary>The immutable template this unit was built from.</summary>
    public UnitDefinition Definition { get; }

    /// <summary>The army this unit fights for.</summary>
    public BattleSide Side { get; }

    /// <summary>Where it currently stands.</summary>
    public HexCoord Position { get; set; }

    /// <summary>
    /// Remaining hit points. Starts at <see cref="UnitDefinition.MaxHp"/>; a
    /// unit is dead at zero or below.
    /// </summary>
    public int CurrentHp { get; set; }

    /// <summary>
    /// Whether this unit is still in the fight.
    ///
    /// The one place the "dead at zero or below" rule is written down, so that
    /// the tick loop, the targeting pass and the outcome check cannot each
    /// arrive at a slightly different idea of dead — and so that overkill, which
    /// drives <see cref="CurrentHp"/> negative rather than clamping it, does not
    /// have to be remembered at every call site.
    /// </summary>
    public bool IsAlive => CurrentHp > 0;

    /// <summary>
    /// Ticks remaining before this unit may attack again. Zero means ready, and
    /// it is reset to <see cref="UnitDefinition.AttackIntervalTicks"/> after a
    /// swing. Counted down rather than compared against a timestamp, because
    /// the simulation has no clock.
    /// </summary>
    public int AttackCooldownTicks { get; set; }

    public override string ToString() =>
        $"{Side} {Definition.Id} at {Position} ({CurrentHp}/{Definition.MaxHp} hp)";
}
