namespace Api.Battles.Dtos;

/// <summary>A hex on the battlefield, as exposed over the wire. See <see cref="GameSim.HexCoord"/>.</summary>
public sealed record HexCoordDto
{
    public required int Row { get; init; }

    public required int Col { get; init; }
}
