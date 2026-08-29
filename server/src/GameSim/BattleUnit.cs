namespace GameSim;

/// <summary>
/// One unit in one battle: a shared <see cref="UnitDefinition"/> plus everything
/// about it that changes as the fight runs.
/// <para>
/// A class rather than a record, because this is the one place in the
/// simulation where identity and mutation are both real. Two militia standing on
/// different tiles are different units even when every field matches, and a unit
/// that takes damage is still the same unit — so value equality would be wrong
/// and copying on every hit would be waste.
/// </para>
/// <para>
/// Nothing here writes to <see cref="Definition"/>: it is shared by every unit
/// of its kind, in this battle and every other.
/// </para>
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
    /// Ticks remaining before this unit may attack again. Zero means ready, and
    /// it is reset to <see cref="UnitDefinition.AttackIntervalTicks"/> after a
    /// swing. Counted down rather than compared against a timestamp, because
    /// the simulation has no clock.
    /// </summary>
    public int AttackCooldownTicks { get; set; }

    /// <summary>Whether this unit is still in the fight.</summary>
    public bool IsAlive => CurrentHp > 0;

    /// <inheritdoc />
    public override string ToString() =>
        $"{Side} {Definition.Id} at {Position} ({CurrentHp}/{Definition.MaxHp} hp)";
}
