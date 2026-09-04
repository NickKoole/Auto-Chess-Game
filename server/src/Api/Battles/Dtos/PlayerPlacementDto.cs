namespace Api.Battles.Dtos;

public sealed record PlayerPlacementDto
{
    /// <summary>An id from <c>GET /battles/units</c>.</summary>
    public required string UnitId { get; init; }

    public required HexCoordDto Position { get; init; }
}
