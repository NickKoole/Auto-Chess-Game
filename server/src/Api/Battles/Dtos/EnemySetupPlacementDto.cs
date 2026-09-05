namespace Api.Battles.Dtos;

public sealed record EnemySetupPlacementDto
{
    /// <summary>An id from <c>GET /battles/units</c>.</summary>
    public required string DefinitionId { get; init; }

    public required HexCoordDto Position { get; init; }
}
