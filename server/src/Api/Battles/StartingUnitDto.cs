namespace Api.Battles;

/// <summary>One unit's state at tick 0, before anything in the battle has happened.</summary>
public sealed record StartingUnitDto
{
    /// <summary>This unit's id, unique within the battle. See <see cref="GameSim.BattleUnit.Id"/>.</summary>
    public required int UnitId { get; init; }

    public required SideDto Side { get; init; }

    /// <summary>The catalog id of the kind of unit this is. Look it up in the response's unit definitions.</summary>
    public required string DefinitionId { get; init; }

    public required HexCoordDto Position { get; init; }
}
