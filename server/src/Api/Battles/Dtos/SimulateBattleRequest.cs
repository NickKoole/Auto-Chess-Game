namespace Api.Battles.Dtos;

public sealed record SimulateBattleRequest
{
    /// <summary>Valid ids are listed in <see cref="MatchupCatalog"/>.</summary>
    public required string MatchupId { get; init; }

    /// <summary>
    /// The seed to run the battle with, as a base-10 string — see
    /// <see cref="SimulateBattleResponse.Seed"/> for why it is a string rather
    /// than a JSON number. Null to have the server generate one.
    /// </summary>
    public string? Seed { get; init; }
}
