namespace Api.Battles.Dtos;

public sealed record StartingUnitDto
{
    /// <summary>Unique within this battle only, not globally. See <see cref="GameSim.BattleUnit.Id"/>.</summary>
    public required int UnitId { get; init; }

    public required SideDto Side { get; init; }

    /// <summary>Look this up in the response's <see cref="SimulateBattleResponse.UnitDefinitions"/>.</summary>
    public required string DefinitionId { get; init; }

    public required HexCoordDto Position { get; init; }
}
