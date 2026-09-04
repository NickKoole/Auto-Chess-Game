namespace Api.Battles.Dtos;

/// <summary>Mirrors <see cref="GameSim.HexCoord"/>.</summary>
public sealed record HexCoordDto
{
    public required int Row { get; init; }

    public required int Col { get; init; }
}
