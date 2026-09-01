namespace Api.Battles.Dtos;

/// <summary>
/// A kind of unit's stats, as exposed over the wire — enough for a client to
/// display names and stats without its own copy of the catalog. See
/// <see cref="GameSim.UnitDefinition"/>.
/// </summary>
public sealed record UnitDefinitionDto
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required int MaxHp { get; init; }

    public required int AttackDamage { get; init; }

    public required int AttackIntervalTicks { get; init; }

    public required int MovementTicksPerHex { get; init; }

    public required int AttackRangeHexes { get; init; }

    public required double DamageVariance { get; init; }
}
