using System.Text.Json.Serialization;

namespace Api.Battles.Dtos;

/// <summary>
/// One thing that happened in a battle, as exposed over the wire. Mirrors
/// <see cref="GameSim.BattleEvent"/>'s shape and discriminator so a client gets
/// the same replay information, without the sim's own type crossing the API
/// boundary.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UnitSpawnedEventDto), "unitSpawned")]
[JsonDerivedType(typeof(UnitMovedEventDto), "unitMoved")]
[JsonDerivedType(typeof(UnitAttackedEventDto), "unitAttacked")]
[JsonDerivedType(typeof(DamageDealtEventDto), "damageDealt")]
[JsonDerivedType(typeof(UnitDiedEventDto), "unitDied")]
public abstract record BattleEventDto
{
    public required int Tick { get; init; }
}
