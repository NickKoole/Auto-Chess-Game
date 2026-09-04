namespace Api.Battles.Dtos;

/// <summary>
/// One matchup a battle can be requested for, as exposed over the wire: enough
/// for a picker to list it and a client to request it back by id.
/// </summary>
public sealed record MatchupDto
{
    /// <summary>Stable identifier. Pass this back as <see cref="SimulateBattleRequest.MatchupId"/>.</summary>
    public required string Id { get; init; }

    public required string DisplayName { get; init; }
}
