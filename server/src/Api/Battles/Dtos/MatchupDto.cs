namespace Api.Battles.Dtos;

public sealed record MatchupDto
{
    /// <summary>Stable identifier. Pass this back as <see cref="SimulateBattleRequest.MatchupId"/>.</summary>
    public required string Id { get; init; }

    public required string DisplayName { get; init; }
}
