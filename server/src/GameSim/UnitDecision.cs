namespace GameSim;

/// <summary>
/// One unit's choice for a tick, held as data until every unit has decided -
/// see <see cref="BattleSimulation"/>'s tick loop. <see cref="MoveTo"/> is
/// null to stay put, whichever of several reasons applies - in range,
/// cooldown, blocked, or nothing left to fight; distinguishing them is the
/// event log's job, not this type's. <see cref="Attack"/> is null if this
/// unit doesn't attack, and its damage is already rolled by the time this is
/// set.
/// </summary>
internal readonly record struct UnitDecision(BattleUnit Unit, HexCoord? MoveTo, AttackDecision? Attack);

/// <summary>
/// One landed hit, decided but not yet applied. <see cref="Damage"/> is the
/// raw roll, already floored to an integer and rolled exactly once - never
/// re-rolled when this is applied - but not yet reduced by the target's
/// resistance, which <see cref="BattleSimulation"/>'s apply phase computes
/// from both units' definitions at the point it lands.
/// </summary>
internal readonly record struct AttackDecision(BattleUnit Target, int Damage);
