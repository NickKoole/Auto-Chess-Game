namespace GameSim;

/// <summary>
/// A unit's starting state: emitted once per unit, at <see cref="BattleEvent.Tick"/>
/// 0, before any tick runs.
///
/// Carries <see cref="DefinitionId"/> rather than the full
/// <see cref="UnitDefinition"/>, so the log stays plain data instead of
/// embedding a copy of the catalog inside every battle. A consumer looks up
/// the definition once, the same way <see cref="BattleSetup"/> itself does.
/// </summary>
public sealed record UnitSpawnedEvent : BattleEvent
{
    /// <summary>This unit's id, unique within the battle. See <see cref="BattleUnit.Id"/>.</summary>
    public required int UnitId { get; init; }

    /// <summary>The army this unit fights for.</summary>
    public required BattleSide Side { get; init; }

    /// <summary>The catalog id of the kind of unit this is.</summary>
    public required string DefinitionId { get; init; }

    /// <summary>Where it starts the battle.</summary>
    public required HexCoord Position { get; init; }
}
