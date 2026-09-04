using System.Globalization;
using Api.Battles.Dtos;
using GameSim;

namespace Api.Battles;

/// <summary>
/// Builds a <see cref="SimulateBattleResponse"/> from a finished
/// <see cref="BattleSimulation"/> - the one place <c>GameSim</c> types are
/// translated into DTOs.
/// </summary>
public static class BattleResponseFactory
{
    public static SimulateBattleResponse Create(
        ulong seed, BattleSetup setup, IReadOnlyList<BattleEvent> events, BattleResult result)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(events);

        var initialSetup = new List<StartingUnitDto>(setup.Units.Count);
        var definitions = new Dictionary<string, UnitDefinitionDto>(StringComparer.Ordinal);

        // Read off the log, not setup.Units: BattleUnit.Position is mutable and
        // the simulation moves those instances in place, so by now a unit's
        // Position is where it finished, not where it started.
        foreach (BattleEvent battleEvent in events)
        {
            if (battleEvent is not UnitSpawnedEvent spawned)
            {
                continue;
            }

            initialSetup.Add(new StartingUnitDto
            {
                UnitId = spawned.UnitId,
                Side = MapSide(spawned.Side),
                DefinitionId = spawned.DefinitionId,
                Position = MapCoord(spawned.Position),
            });
        }

        // The simulation emits exactly one spawn per unit, so a mismatch means
        // this response was built before the battle ran.
        if (initialSetup.Count != setup.Units.Count)
        {
            throw new InvalidOperationException(
                $"Expected one spawn event per unit, got {initialSetup.Count} for {setup.Units.Count} units. " +
                "Build the response from a simulation that has been run.");
        }

        // Definitions come from the setup, not the log: BattleUnit.Definition
        // is the shared, immutable template, and nothing writes to it.
        foreach (BattleUnit unit in setup.Units)
        {
            definitions.TryAdd(unit.Definition.Id, MapUnitDefinition(unit.Definition));
        }

        return new SimulateBattleResponse
        {
            Seed = seed.ToString(CultureInfo.InvariantCulture),
            InitialSetup = initialSetup,
            UnitDefinitions = [.. definitions.Values],
            Events = [.. events.Select(MapEvent)],
            Outcome = new BattleOutcomeDto
            {
                Winner = MapWinner(result.Outcome),
                TerminatingTick = result.EndTick,
                TerminationReason = MapEnding(result.Ending),
            },
        };
    }

    private static BattleEventDto MapEvent(BattleEvent battleEvent) => battleEvent switch
    {
        UnitSpawnedEvent e => new UnitSpawnedEventDto
        {
            Tick = e.Tick,
            UnitId = e.UnitId,
            Side = MapSide(e.Side),
            DefinitionId = e.DefinitionId,
            Position = MapCoord(e.Position),
        },
        UnitMovedEvent e => new UnitMovedEventDto
        {
            Tick = e.Tick,
            UnitId = e.UnitId,
            From = MapCoord(e.From),
            To = MapCoord(e.To),
        },
        UnitAttackedEvent e => new UnitAttackedEventDto
        {
            Tick = e.Tick,
            AttackerId = e.AttackerId,
            TargetId = e.TargetId,
        },
        DamageDealtEvent e => new DamageDealtEventDto
        {
            Tick = e.Tick,
            AttackerId = e.AttackerId,
            TargetId = e.TargetId,
            Amount = e.Amount,
            ResultingHp = e.ResultingHp,
        },
        UnitDiedEvent e => new UnitDiedEventDto
        {
            Tick = e.Tick,
            UnitId = e.UnitId,
            Position = MapCoord(e.Position),
        },
        _ => throw new ArgumentOutOfRangeException(
            nameof(battleEvent), battleEvent, $"Unknown battle event type '{battleEvent.GetType()}'."),
    };

    private static UnitDefinitionDto MapUnitDefinition(UnitDefinition definition) => new()
    {
        Id = definition.Id,
        DisplayName = definition.DisplayName,
        MaxHp = definition.MaxHp,
        AttackDamage = definition.AttackDamage,
        AttackIntervalTicks = definition.AttackIntervalTicks,
        MovementTicksPerHex = definition.MovementTicksPerHex,
        AttackRangeHexes = definition.AttackRangeHexes,
        DamageVariance = definition.DamageVariance,
    };

    private static HexCoordDto MapCoord(HexCoord coord) => new() { Row = coord.Row, Col = coord.Col };

    private static SideDto MapSide(BattleSide side) => side switch
    {
        BattleSide.Player => SideDto.Player,
        BattleSide.Opponent => SideDto.Opponent,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown battle side."),
    };

    private static MatchWinnerDto MapWinner(BattleOutcome outcome) => outcome switch
    {
        BattleOutcome.Draw => MatchWinnerDto.Draw,
        BattleOutcome.PlayerWin => MatchWinnerDto.Player,
        BattleOutcome.OpponentWin => MatchWinnerDto.Opponent,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown battle outcome."),
    };

    private static BattleEndingDto MapEnding(BattleEnding ending) => ending switch
    {
        BattleEnding.PlayerEliminatedOpponent => BattleEndingDto.PlayerEliminatedOpponent,
        BattleEnding.OpponentEliminatedPlayer => BattleEndingDto.OpponentEliminatedPlayer,
        BattleEnding.MutualElimination => BattleEndingDto.MutualElimination,
        BattleEnding.TickCapDraw => BattleEndingDto.TickCapDraw,
        _ => throw new ArgumentOutOfRangeException(nameof(ending), ending, "Unknown battle ending."),
    };
}
