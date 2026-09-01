using System.Text.Json.Serialization;

namespace GameSim;

/// <summary>
/// One thing that happened in a battle, stamped with the tick it happened on.
///
/// This is the public account of a battle: a client reconstructs everything it
/// needs to animate a fight from a list of these plus a static unit catalog,
/// without ever running <see cref="BattleSimulation"/> itself. That is the
/// entire reason it exists as its own type rather than as a side effect a
/// caller has to infer from <see cref="BattleUnit"/> state — state is mutated
/// in place and overwrites its own history, so nothing about "what happened"
/// survives a tick unless something writes it down as it happens.
///
/// Plain serialisable data, deliberately: every derived type is a
/// <see langword="record"/> with <see langword="init"/>-only properties, and
/// this base type is discriminated with the built-in
/// <see cref="JsonPolymorphicAttribute"/> support rather than a hand-written
/// converter. <c>System.Text.Json</c> ships in the shared framework, so this
/// costs no <c>PackageReference</c> and nothing here can drift from
/// <c>ArchitectureTests.GameSim_project_has_no_dependencies_at_all</c>.
///
/// Each derived event names the fields the acceptance criteria for story 1.6
/// actually ask for, rather than forcing a uniform "which unit caused this" field
/// onto every case: <see cref="DamageDealtEvent"/>, for instance, is about the
/// unit taking the hit, not the one landing it. Traceability back to an attacker
/// still holds, because a <see cref="UnitAttackedEvent"/> is always emitted
/// immediately before the <see cref="DamageDealtEvent"/> it caused.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UnitSpawnedEvent), "unitSpawned")]
[JsonDerivedType(typeof(UnitMovedEvent), "unitMoved")]
[JsonDerivedType(typeof(UnitAttackedEvent), "unitAttacked")]
[JsonDerivedType(typeof(DamageDealtEvent), "damageDealt")]
[JsonDerivedType(typeof(UnitDiedEvent), "unitDied")]
public abstract record BattleEvent
{
    /// <summary>
    /// The tick this happened on. Spawn events use 0, since they describe the
    /// setup a battle starts from rather than anything a tick decided.
    /// </summary>
    public required int Tick { get; init; }
}
