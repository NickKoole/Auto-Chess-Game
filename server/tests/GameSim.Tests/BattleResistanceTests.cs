using System.Text.Json;

namespace GameSim.Tests;

/// <summary>
/// The seven damage types and their matching resistance stats: the
/// mitigation formula, its 75% cap, its type-specificity, and the proof that
/// every resist sitting at zero - today's state for every real unit - leaves
/// a battle byte-identical to what it was before this mechanism existed.
/// </summary>
public sealed class BattleResistanceTests
{
    private static UnitPlacement At(UnitDefinition definition, int row, int col) =>
        new(definition, new HexCoord(row, col));

    /// <summary>
    /// Captured from the real catalog and this exact seed before the resist
    /// mechanism existed - see the ticket's regression criterion. Every
    /// resist on every built-in unit is zero, so the formula's
    /// <c>raw * (100 - 0) / 100</c> is an exact identity and this log must
    /// never change while that stays true.
    /// </summary>
    private const string GoldenLog =
        """[{"type":"unitSpawned","UnitId":0,"Side":0,"DefinitionId":"conscript","Position":{"Row":3,"Col":4},"Tick":0},{"type":"unitSpawned","UnitId":1,"Side":1,"DefinitionId":"knight","Position":{"Row":3,"Col":7},"Tick":0},{"type":"unitMoved","UnitId":0,"From":{"Row":3,"Col":4},"To":{"Row":3,"Col":5},"Tick":1},{"type":"unitMoved","UnitId":1,"From":{"Row":3,"Col":7},"To":{"Row":3,"Col":6},"Tick":1},{"type":"unitAttacked","AttackerId":0,"TargetId":1,"Tick":2},{"type":"damageDealt","AttackerId":0,"TargetId":1,"Amount":12,"ResultingHp":168,"Tick":2},{"type":"unitAttacked","AttackerId":1,"TargetId":0,"Tick":2},{"type":"damageDealt","AttackerId":1,"TargetId":0,"Amount":16,"ResultingHp":84,"Tick":2},{"type":"unitAttacked","AttackerId":0,"TargetId":1,"Tick":14},{"type":"damageDealt","AttackerId":0,"TargetId":1,"Amount":12,"ResultingHp":156,"Tick":14},{"type":"unitAttacked","AttackerId":1,"TargetId":0,"Tick":18},{"type":"damageDealt","AttackerId":1,"TargetId":0,"Amount":18,"ResultingHp":66,"Tick":18},{"type":"unitAttacked","AttackerId":0,"TargetId":1,"Tick":26},{"type":"damageDealt","AttackerId":0,"TargetId":1,"Amount":11,"ResultingHp":145,"Tick":26},{"type":"unitAttacked","AttackerId":1,"TargetId":0,"Tick":34},{"type":"damageDealt","AttackerId":1,"TargetId":0,"Amount":14,"ResultingHp":52,"Tick":34},{"type":"unitAttacked","AttackerId":0,"TargetId":1,"Tick":38},{"type":"damageDealt","AttackerId":0,"TargetId":1,"Amount":12,"ResultingHp":133,"Tick":38},{"type":"unitAttacked","AttackerId":0,"TargetId":1,"Tick":50},{"type":"damageDealt","AttackerId":0,"TargetId":1,"Amount":13,"ResultingHp":120,"Tick":50},{"type":"unitAttacked","AttackerId":1,"TargetId":0,"Tick":50},{"type":"damageDealt","AttackerId":1,"TargetId":0,"Amount":16,"ResultingHp":36,"Tick":50},{"type":"unitAttacked","AttackerId":0,"TargetId":1,"Tick":62},{"type":"damageDealt","AttackerId":0,"TargetId":1,"Amount":10,"ResultingHp":110,"Tick":62},{"type":"unitAttacked","AttackerId":1,"TargetId":0,"Tick":66},{"type":"damageDealt","AttackerId":1,"TargetId":0,"Amount":17,"ResultingHp":19,"Tick":66},{"type":"unitAttacked","AttackerId":0,"TargetId":1,"Tick":74},{"type":"damageDealt","AttackerId":0,"TargetId":1,"Amount":10,"ResultingHp":100,"Tick":74},{"type":"unitAttacked","AttackerId":1,"TargetId":0,"Tick":82},{"type":"damageDealt","AttackerId":1,"TargetId":0,"Amount":14,"ResultingHp":5,"Tick":82},{"type":"unitAttacked","AttackerId":0,"TargetId":1,"Tick":86},{"type":"damageDealt","AttackerId":0,"TargetId":1,"Amount":13,"ResultingHp":87,"Tick":86},{"type":"unitAttacked","AttackerId":0,"TargetId":1,"Tick":98},{"type":"damageDealt","AttackerId":0,"TargetId":1,"Amount":11,"ResultingHp":76,"Tick":98},{"type":"unitAttacked","AttackerId":1,"TargetId":0,"Tick":98},{"type":"damageDealt","AttackerId":1,"TargetId":0,"Amount":17,"ResultingHp":-12,"Tick":98},{"type":"unitDied","UnitId":0,"Position":{"Row":3,"Col":5},"Tick":98}]""";

    [Fact]
    public void Zero_resist_everywhere_reproduces_the_pre_resist_battle_log_exactly()
    {
        var catalog = new StaticUnitCatalog();
        UnitDefinition conscript = catalog.Get("conscript");
        UnitDefinition knight = catalog.Get("knight");

        Assert.True(BattleSetup.TryCreate(
            [At(conscript, 3, 4)],
            [At(knight, 3, 7)],
            out BattleSetup? setup,
            out IReadOnlyList<PlacementError> errors));
        Assert.Empty(errors);

        var simulation = new BattleSimulation(setup!, seed: 20260916);
        BattleResult result = simulation.Run();

        Assert.Equal(BattleEnding.OpponentEliminatedPlayer, result.Ending);
        Assert.Equal(98, result.EndTick);
        Assert.Equal(GoldenLog, JsonSerializer.Serialize(simulation.Events));
    }

    private static UnitDefinition Attacker(DamageType damageType) => new(
        id: "attacker",
        displayName: "Attacker",
        maxHp: 500,
        attackDamage: 20,
        attackIntervalTicks: 5,
        movementTicksPerHex: 1,
        attackRangeHexes: 13,
        playerSelectable: true,
        damageVariance: 0.20,
        damageType: damageType);

    private static UnitDefinition Defender(int slashingResist = 0, int fireResist = 0) => new(
        id: "defender",
        displayName: "Defender",
        maxHp: 100_000,
        attackDamage: 1,
        attackIntervalTicks: 1000,
        movementTicksPerHex: 1,
        attackRangeHexes: 13,
        playerSelectable: true,
        damageVariance: 0.0,
        slashingResist: slashingResist,
        fireResist: fireResist);

    private static int TotalDamageTaken(UnitDefinition attacker, UnitDefinition defender, ulong seed)
    {
        Assert.True(BattleSetup.TryCreate(
            [At(attacker, 3, 4)],
            [At(defender, 3, 7)],
            out BattleSetup? setup,
            out IReadOnlyList<PlacementError> errors));
        Assert.Empty(errors);

        var simulation = new BattleSimulation(setup!, seed, new BattleOptions { MaxTicks = 200 });
        simulation.Run();

        return simulation.Events
            .OfType<DamageDealtEvent>()
            .Where(e => e.TargetId == 1)
            .Sum(e => e.Amount);
    }

    [Fact]
    public void A_defender_with_matching_resistance_takes_markedly_less_total_damage_across_many_seeds()
    {
        UnitDefinition attacker = Attacker(DamageType.Slashing);
        UnitDefinition unresisted = Defender();
        UnitDefinition resisted = Defender(slashingResist: 50);

        long unresistedTotal = 0;
        long resistedTotal = 0;

        for (ulong seed = 1; seed <= 60; seed++)
        {
            unresistedTotal += TotalDamageTaken(attacker, unresisted, seed);
            resistedTotal += TotalDamageTaken(attacker, resisted, seed);
        }

        Assert.True(
            resistedTotal < unresistedTotal * 0.65,
            $"Expected a 50% Slashing resist to hold total damage well under 65% of unresisted "
            + $"({resistedTotal} vs {unresistedTotal}).");
    }

    [Fact]
    public void Resistance_above_seventy_five_percent_still_caps_at_seventy_five_percent_blocked()
    {
        UnitDefinition attacker = new(
            id: "attacker",
            displayName: "Attacker",
            maxHp: 500,
            attackDamage: 20,
            attackIntervalTicks: 5,
            movementTicksPerHex: 1,
            attackRangeHexes: 13,
            playerSelectable: true,
            damageVariance: 0.0,
            damageType: DamageType.Fire);

        UnitDefinition defender = new(
            id: "defender",
            displayName: "Defender",
            maxHp: 500,
            attackDamage: 1,
            attackIntervalTicks: 1000,
            movementTicksPerHex: 1,
            attackRangeHexes: 13,
            playerSelectable: true,
            damageVariance: 0.0,
            fireResist: 100);

        Assert.True(BattleSetup.TryCreate(
            [At(attacker, 3, 4)],
            [At(defender, 3, 7)],
            out BattleSetup? setup,
            out IReadOnlyList<PlacementError> errors));
        Assert.Empty(errors);

        var simulation = new BattleSimulation(setup!, seed: 1, new BattleOptions { MaxTicks = 5 });
        simulation.Run();

        DamageDealtEvent hit = simulation.Events.OfType<DamageDealtEvent>().First(e => e.TargetId == 1);

        Assert.Equal(5, hit.Amount);
        Assert.NotEqual(0, hit.Amount);
    }

    [Fact]
    public void Resistance_only_mitigates_its_own_matching_damage_type()
    {
        UnitDefinition attacker = new(
            id: "attacker",
            displayName: "Attacker",
            maxHp: 500,
            attackDamage: 20,
            attackIntervalTicks: 5,
            movementTicksPerHex: 1,
            attackRangeHexes: 13,
            playerSelectable: true,
            damageVariance: 0.0,
            damageType: DamageType.Slashing);

        UnitDefinition defender = new(
            id: "defender",
            displayName: "Defender",
            maxHp: 500,
            attackDamage: 1,
            attackIntervalTicks: 1000,
            movementTicksPerHex: 1,
            attackRangeHexes: 13,
            playerSelectable: true,
            damageVariance: 0.0,
            fireResist: 90);

        Assert.True(BattleSetup.TryCreate(
            [At(attacker, 3, 4)],
            [At(defender, 3, 7)],
            out BattleSetup? setup,
            out IReadOnlyList<PlacementError> errors));
        Assert.Empty(errors);

        var simulation = new BattleSimulation(setup!, seed: 1, new BattleOptions { MaxTicks = 5 });
        simulation.Run();

        DamageDealtEvent hit = simulation.Events.OfType<DamageDealtEvent>().First(e => e.TargetId == 1);

        Assert.Equal(20, hit.Amount);
    }
}
