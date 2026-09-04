namespace Api.Battles.Dtos;

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

    /// <summary>In <see cref="GameSim.BattleSetup.Units"/> order.</summary>
    public required IReadOnlyList<StartingUnitDto> InitialSetup { get; init; }

    /// <summary>Only the kinds referenced by <see cref="InitialSetup"/> or <see cref="Events"/>, not the full catalog.</summary>
    public required IReadOnlyList<UnitDefinitionDto> UnitDefinitions { get; init; }

    /// <summary>Mirrors <see cref="GameSim.BattleSimulation.Events"/>.</summary>
    public required IReadOnlyList<BattleEventDto> Events { get; init; }

    public required BattleOutcomeDto Outcome { get; init; }
}
