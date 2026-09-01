namespace Api.Battles;

/// <summary>
/// Everything a client needs to replay one battle: the seed it ran with, the
/// position it started from, the full log of what happened, the unit stats
/// the log refers to, and how it ended.
/// </summary>
public sealed record SimulateBattleResponse
{
    /// <summary>
    /// The seed this battle ran with, formatted as a base-10 string rather than
    /// a JSON number: it is a full-range <see langword="ulong"/>, and a JSON
    /// number silently loses precision above 2^53 in a JS/TS client — which
    /// would corrupt roughly half of all server-generated seeds and break exact
    /// re-requesting for them.
    /// </summary>
    public required string Seed { get; init; }

    /// <summary>Every unit's state at tick 0, in the order <see cref="GameSim.BattleSetup.Units"/> holds them.</summary>
    public required IReadOnlyList<StartingUnitDto> InitialSetup { get; init; }

    /// <summary>The stats for every unit kind referenced by <see cref="InitialSetup"/> or <see cref="Events"/>.</summary>
    public required IReadOnlyList<UnitDefinitionDto> UnitDefinitions { get; init; }

    /// <summary>Everything that happened, in order. See <see cref="GameSim.BattleSimulation.Events"/>.</summary>
    public required IReadOnlyList<BattleEventDto> Events { get; init; }

    public required BattleOutcomeDto Outcome { get; init; }
}
