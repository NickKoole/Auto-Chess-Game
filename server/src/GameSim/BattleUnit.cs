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
    /// Creates a unit ready to fight: at full health, with no target, and with
    /// neither cooldown pending — so it may attack and it may move on the very
    /// first tick of the battle.
    /// </summary>
    public BattleUnit(UnitDefinition definition, BattleSide side, HexCoord position)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Definition = definition;
        Side = side;
        Position = position;
        CurrentHp = definition.MaxHp;
        AttackCooldownTicks = 0;
        MovementCooldownTicks = 0;
    }

    /// <summary>
    /// This unit's id, unique within the battle it belongs to — never across
    /// battles, and never stable for "the same kind of unit" the way
    /// <see cref="Definition"/>'s id is. Ties an event in the battle log back to
    /// a specific instance rather than to a kind of unit.
    ///
    /// Assigned by <see cref="BattleSetup"/> once the final, sorted
    /// <see cref="BattleSetup.Units"/> order is known — as that order's index,
    /// which is already a deterministic total order with no ties. Internal
    /// rather than a constructor parameter: nothing about one unit's identity
    /// can be decided until every unit in the battle has been placed and sorted,
    /// so there is no meaningful value to hand the constructor.
    /// </summary>
    public int Id { get; internal set; }

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
    /// The enemy this unit is currently going after, or null before it has
    /// picked one.
    ///
    /// Re-chosen every tick, but only ever in favour of an enemy that is
    /// strictly closer than the one already held. Two equidistant foes therefore
    /// leave it alone: a unit cannot alternate between them, and it goes on
    /// concentrating its damage on one rather than spreading it along a line.
    ///
    /// <para>
    /// It is deliberately not held until death. A target kept through anything
    /// short of dying lets a unit stand beside a living enemy for the whole
    /// battle waiting on someone walled off behind it, because attack range is
    /// tested against the target alone — so the unit is in reach of something it
    /// will not swing at.
    /// </para>
    ///
    /// A reference to the other unit rather than a coordinate or an id: the
    /// target moves, and a coordinate would go stale within a tick. Nothing here
    /// writes to it.
    /// </summary>
    public BattleUnit? Target { get; set; }

    /// <summary>
    /// Ticks remaining before this unit may move again. Zero means ready, and
    /// it is reset to <see cref="UnitDefinition.MovementTicksPerHex"/> after a
    /// step — so a unit that crosses a hex in 3 ticks steps on ticks 1, 4, 7.
    ///
    /// Only an actual step resets it. A unit that wanted to move and found its
    /// way blocked keeps whatever readiness it had accumulated and goes on the
    /// first tick the tile frees; charging it for an attempt would let a
    /// traffic jam quietly halve an army's speed.
    /// </summary>
    public int MovementCooldownTicks { get; set; }

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
