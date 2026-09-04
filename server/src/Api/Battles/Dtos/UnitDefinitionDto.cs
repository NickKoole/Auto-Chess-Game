namespace Api.Battles.Dtos;

/// <summary>Mirrors <see cref="GameSim.UnitDefinition"/>.</summary>
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
