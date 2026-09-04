namespace GameSim;

/// <summary>
/// One unit in one battle: a shared <see cref="UnitDefinition"/> plus
/// everything that changes as it fights. Aliased and mutated in place
/// throughout the sim - by board tiles, <see cref="BattleSetup.Units"/>, and
/// other units' <see cref="Target"/> references - never copied. Nothing here
/// writes to <see cref="Definition"/>: it is shared by every unit of its
/// kind, in this battle and every other.
/// </summary>
public sealed class BattleUnit
{
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
    /// Unique within this battle only, not across battles or the same as
    /// <see cref="Definition"/>'s id. Assigned by <see cref="BattleSetup"/> as
    /// the index into its final, deterministically sorted
    /// <see cref="BattleSetup.Units"/>.
    /// </summary>
    public int Id { get; internal set; }

    public UnitDefinition Definition { get; }

    public BattleSide Side { get; }

    public HexCoord Position { get; set; }

    /// <summary>Starts at <see cref="UnitDefinition.MaxHp"/>.</summary>
    public int CurrentHp { get; set; }

    /// <summary>False once <see cref="CurrentHp"/> reaches zero or below - not clamped there, so overkill drives it negative.</summary>
    public bool IsAlive => CurrentHp > 0;

    /// <summary>
    /// The enemy this unit is currently pursuing, or null. Set by
    /// <see cref="BattleSimulation"/>'s decide phase, never written here. A
    /// reference rather than a coordinate: the enemy moves, and a coordinate
    /// would go stale within a tick.
    /// </summary>
    public BattleUnit? Target { get; set; }

    /// <summary>
    /// Zero means ready. Reset to <see cref="UnitDefinition.MovementTicksPerHex"/>
    /// after a step - a unit crossing a hex in 3 ticks steps on ticks 1, 4, 7.
    /// </summary>
    public int MovementCooldownTicks { get; set; }

    /// <summary>Zero means ready. Reset to <see cref="UnitDefinition.AttackIntervalTicks"/> after a swing.</summary>
    public int AttackCooldownTicks { get; set; }

    public override string ToString() =>
        $"{Side} {Definition.Id} at {Position} ({CurrentHp}/{Definition.MaxHp} hp)";
}
