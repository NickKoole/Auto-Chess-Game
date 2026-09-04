using System.Text.Json.Serialization;

namespace Api.Battles.Dtos;

/// <summary>
/// One thing that happened in a battle, mirroring <see cref="GameSim.BattleEvent"/>'s
/// shape and discriminator.
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
