namespace GameSim;

/// <summary>Emitted once per unit, at tick 0, before any tick runs.</summary>
public sealed record UnitSpawnedEvent : BattleEvent
{
    /// <summary>Unique within the battle. See <see cref="BattleUnit.Id"/>.</summary>
    public required int UnitId { get; init; }

    public required BattleSide Side { get; init; }

    /// <summary>Look this up rather than embedding a copy of <see cref="UnitDefinition"/> in every event.</summary>
    public required string DefinitionId { get; init; }

    public required HexCoord Position { get; init; }
}
