namespace Api.Battles.Dtos;

public sealed record EnemySetupDto
{
    /// <summary>Stable identifier for this enemy setup, unique within the list.</summary>
    public required string Id { get; init; }

    public required string DisplayName { get; init; }
}
