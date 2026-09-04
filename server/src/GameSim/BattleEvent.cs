using System.Text.Json.Serialization;

namespace GameSim;

/// <summary>
/// One thing that happened in a battle, stamped with the tick it happened
/// on. A client reconstructs a whole battle from a list of these plus a
/// static unit catalog, without running <see cref="BattleSimulation"/>
/// itself - <see cref="BattleUnit"/> state is mutated in place and
/// overwrites its own history, so nothing survives a tick unless this
/// writes it down. Discriminated with
/// <see cref="JsonPolymorphicAttribute"/> rather than a hand-written
/// converter: <c>System.Text.Json</c> ships in the shared framework, so
/// this costs no <c>PackageReference</c> and can't violate GameSim's
/// zero-dependency rule.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UnitSpawnedEvent), "unitSpawned")]
[JsonDerivedType(typeof(UnitMovedEvent), "unitMoved")]
[JsonDerivedType(typeof(UnitAttackedEvent), "unitAttacked")]
[JsonDerivedType(typeof(DamageDealtEvent), "damageDealt")]
[JsonDerivedType(typeof(UnitDiedEvent), "unitDied")]
public abstract record BattleEvent
{
    /// <summary>Spawn events use 0, since they describe the starting setup rather than anything a tick decided.</summary>
    public required int Tick { get; init; }
}
