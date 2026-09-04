namespace Api.Battles.Dtos;

/// <summary>
/// The player's side comes from exactly one of two shapes: <see cref="MatchupId"/>
/// alone (both sides hardcoded), or <see cref="EnemySetupId"/> paired with
/// <see cref="PlayerPlacements"/> (a hardcoded opponent, a player-built army).
/// </summary>
public sealed record SimulateBattleRequest
{
    /// <summary>Valid ids are listed in <see cref="MatchupCatalog"/>.</summary>
    public string? MatchupId { get; init; }

    /// <summary>Valid ids are listed in <see cref="EnemySetupCatalog"/>.</summary>
    public string? EnemySetupId { get; init; }

    /// <summary>Required alongside <see cref="EnemySetupId"/>; ignored alongside <see cref="MatchupId"/>.</summary>
    public IReadOnlyList<PlayerPlacementDto>? PlayerPlacements { get; init; }

    /// <summary>
    /// The seed to run the battle with, as a base-10 string — see
    /// <see cref="SimulateBattleResponse.Seed"/> for why it is a string rather
    /// than a JSON number. Null to have the server generate one.
    /// </summary>
    public string? Seed { get; init; }
}
