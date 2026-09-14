using System.Text.Json;

namespace GameSim.Tests;

/// <summary>
/// The battle event log — story 1.6. Every fixture keeps <c>damageVariance</c>
/// at 0 so damage numbers are exact rather than a range, which is what lets
/// these tests assert on precise event contents instead of just event counts.
/// </summary>
public sealed class BattleEventLogTests
{
    private const ulong Seed = 0xC0FFEE;

    /// <summary>Always in range (13 covers the whole board) and always ready: attacks every tick it can see an enemy.</summary>
    private static readonly UnitDefinition GlassCannon = new(
        id: "glass-cannon",
        displayName: "Glass Cannon",
        maxHp: 10,
        attackDamage: 100,
        attackIntervalTicks: 1,
        movementTicksPerHex: 1,
        attackRangeHexes: 13,
        damageVariance: 0.0);

    /// <summary>Crosses a hex every tick, melee range, and tough enough to survive being hit while it closes.</summary>
    private static readonly UnitDefinition Runner = new(
        id: "runner",
        displayName: "Runner",
        maxHp: 1_000_000,
        attackDamage: 12,
        attackIntervalTicks: 12,
        movementTicksPerHex: 1,
        attackRangeHexes: 1,
        damageVariance: 0.0);

    /// <summary>Never moves (already in range everywhere) and never dies to anything this file does.</summary>
    private static readonly UnitDefinition Sentinel = new(
        id: "sentinel",
        displayName: "Sentinel",
        maxHp: 1_000_000,
        attackDamage: 1,
        attackIntervalTicks: 1000,
        movementTicksPerHex: 1,
        attackRangeHexes: 13,
        damageVariance: 0.0);

    private static UnitPlacement At(UnitDefinition definition, int row, int col) =>
        new(definition, new HexCoord(row, col));


    private static BattleSetup Setup(
        IReadOnlyList<UnitPlacement> player,
        IReadOnlyList<UnitPlacement> opponent)
    {
        Assert.True(BattleSetup.TryCreate(player, opponent, out BattleSetup? setup, out _));
        return setup;
    }

    // --- unit ids --------------------------------------------------------------

    [Fact]
    public void Assigns_every_unit_a_unique_id()
    {
        BattleSetup setup = Setup(
            [At(GlassCannon, 0, 4), At(GlassCannon, 1, 4)],
            [At(GlassCannon, 0, 9)]);

        Assert.Equal(setup.Units.Count, setup.Units.Select(u => u.Id).Distinct().Count());
    }

    // --- spawn events ------------------------------------------------------------

    [Fact]
    public void Emits_a_spawn_event_for_every_unit_at_tick_zero_before_anything_else()
    {
        BattleSetup setup = Setup(
            [At(GlassCannon, 0, 4)],
            [At(GlassCannon, 0, 9), At(GlassCannon, 6, 7)]);

        var battle = new BattleSimulation(setup, Seed, new BattleOptions { MaxTicks = 1 });
        battle.Run();

        List<UnitSpawnedEvent> spawns = [.. battle.Events.Take(setup.Units.Count).Cast<UnitSpawnedEvent>()];

        Assert.All(spawns, s => Assert.Equal(0, s.Tick));
        Assert.Equal(
            setup.Units.Select(u => (u.Id, u.Side, u.Definition.Id, u.Position)),
            spawns.Select(s => (s.UnitId, s.Side, s.DefinitionId, s.Position)));
    }

    // --- move events -------------------------------------------------------------

    [Fact]
    public void Emits_a_move_event_only_for_a_step_that_actually_happened()
    {
        BattleSetup setup = Setup([At(Runner, 3, 2)], [At(Sentinel, 3, 9)]);
        BattleUnit mover = setup.Units.Single(u => u.Definition == Runner);

        var battle = new BattleSimulation(setup, Seed, new BattleOptions { MaxTicks = 6 });
        battle.Run();

        List<UnitMovedEvent> moves = [.. battle.Events.OfType<UnitMovedEvent>()];

        // Same path BattleTargetingAndMovementTests.Advances_one_hex_at_a_time_up_the_board
        // pins down for this exact fixture: one hex per tick, straight up the board.
        Assert.Equal(
            new (int Tick, HexCoord From, HexCoord To)[]
            {
                (1, new HexCoord(3, 2), new HexCoord(3, 3)),
                (2, new HexCoord(3, 3), new HexCoord(3, 4)),
                (3, new HexCoord(3, 4), new HexCoord(3, 5)),
                (4, new HexCoord(3, 5), new HexCoord(3, 6)),
                (5, new HexCoord(3, 6), new HexCoord(3, 7)),
                (6, new HexCoord(3, 7), new HexCoord(3, 8)),
            },
            moves.Select(m => (m.Tick, m.From, m.To)));

        Assert.All(moves, m => Assert.Equal(mover.Id, m.UnitId));
    }

    // --- attack, damage and death -------------------------------------------------

    [Fact]
    public void Emits_attack_damage_and_death_as_separate_events_after_a_mutual_kill()
    {
        // Both always in range, both always ready, both one-shot the other:
        // a mutual kill on tick 1, deterministically regardless of seed since
        // damageVariance is 0 for both.
        BattleSetup setup = Setup([At(GlassCannon, 0, 4)], [At(GlassCannon, 0, 9)]);
        BattleUnit player = setup.Units.Single(u => u.Side == BattleSide.Player);
        BattleUnit opponent = setup.Units.Single(u => u.Side == BattleSide.Opponent);

        var battle = new BattleSimulation(setup, Seed);
        BattleResult result = battle.Run();

        Assert.Equal(BattleEnding.MutualElimination, result.Ending);
        Assert.Equal(1, result.EndTick);

        List<BattleEvent> tick1 = [.. battle.Events.Where(e => e.Tick == 1)];

        // Acting order is Player then Opponent, so the player's attack and its
        // damage come first, then the opponent's — and only once both hits have
        // landed do the two deaths appear, in that same acting order.
        Assert.Equal(
            [
                typeof(UnitAttackedEvent), typeof(DamageDealtEvent),
                typeof(UnitAttackedEvent), typeof(DamageDealtEvent),
                typeof(UnitDiedEvent), typeof(UnitDiedEvent),
            ],
            tick1.Select(e => e.GetType()));

        var playerAttacks = (UnitAttackedEvent)tick1[0];
        Assert.Equal(player.Id, playerAttacks.AttackerId);
        Assert.Equal(opponent.Id, playerAttacks.TargetId);

        var opponentTakesDamage = (DamageDealtEvent)tick1[1];
        Assert.Equal(player.Id, opponentTakesDamage.AttackerId);
        Assert.Equal(opponent.Id, opponentTakesDamage.TargetId);
        Assert.Equal(GlassCannon.AttackDamage, opponentTakesDamage.Amount);
        Assert.Equal(GlassCannon.MaxHp - GlassCannon.AttackDamage, opponentTakesDamage.ResultingHp);

        var opponentAttacks = (UnitAttackedEvent)tick1[2];
        Assert.Equal(opponent.Id, opponentAttacks.AttackerId);
        Assert.Equal(player.Id, opponentAttacks.TargetId);

        var playerTakesDamage = (DamageDealtEvent)tick1[3];
        Assert.Equal(opponent.Id, playerTakesDamage.AttackerId);
        Assert.Equal(player.Id, playerTakesDamage.TargetId);

        Assert.Equal(
            [player.Id, opponent.Id],
            tick1.OfType<UnitDiedEvent>().Select(d => d.UnitId));
        Assert.Equal(
            [player.Position, opponent.Position],
            tick1.OfType<UnitDiedEvent>().Select(d => d.Position));
    }

    [Fact]
    public void Orders_events_within_a_tick_by_the_order_units_acted()
    {
        var striker1 = new UnitDefinition(
            id: "striker1", displayName: "Striker 1", maxHp: 50, attackDamage: 5,
            attackIntervalTicks: 1, movementTicksPerHex: 1, attackRangeHexes: 11, damageVariance: 0.0);
        var striker2 = new UnitDefinition(
            id: "striker2", displayName: "Striker 2", maxHp: 50, attackDamage: 7,
            attackIntervalTicks: 1, movementTicksPerHex: 1, attackRangeHexes: 11, damageVariance: 0.0);
        var target = new UnitDefinition(
            id: "target", displayName: "Target", maxHp: 500, attackDamage: 1,
            attackIntervalTicks: 1000, movementTicksPerHex: 1, attackRangeHexes: 11, damageVariance: 0.0);

        // (5,0) before (5,1): same front-to-back rank, lower column first — the
        // acting order BattleSetupTests already pins down for this shape.
        BattleSetup setup = Setup(
            [At(striker1, 0, 4), At(striker2, 1, 4)],
            [At(target, 0, 9)]);

        BattleUnit unit1 = setup.Units.Single(u => u.Definition == striker1);
        BattleUnit unit2 = setup.Units.Single(u => u.Definition == striker2);
        BattleUnit defender = setup.Units.Single(u => u.Definition == target);

        var battle = new BattleSimulation(setup, Seed, new BattleOptions { MaxTicks = 1 });
        battle.Run();

        List<UnitAttackedEvent> attacks = [.. battle.Events.OfType<UnitAttackedEvent>()];

        Assert.Equal([unit1.Id, unit2.Id, defender.Id], attacks.Select(a => a.AttackerId));
    }

    // --- determinism and round-tripping --------------------------------------

    [Fact]
    public void Produces_an_identical_log_for_the_same_setup_and_seed()
    {
        static BattleSetup Build() => Setup([At(Runner, 3, 2), At(GlassCannon, 0, 4)], [At(Sentinel, 3, 9), At(GlassCannon, 0, 9)]);

        var a = new BattleSimulation(Build(), Seed, new BattleOptions { MaxTicks = 10 });
        var b = new BattleSimulation(Build(), Seed, new BattleOptions { MaxTicks = 10 });

        a.Run();
        b.Run();

        Assert.Equal(a.Events, b.Events);
    }

    [Fact]
    public void Serialises_to_json_and_deserialises_back_to_an_equivalent_log()
    {
        BattleSetup setup = Setup([At(Runner, 3, 2), At(GlassCannon, 0, 4)], [At(Sentinel, 3, 9), At(GlassCannon, 0, 9)]);
        var battle = new BattleSimulation(setup, Seed, new BattleOptions { MaxTicks = 10 });
        battle.Run();

        string json = JsonSerializer.Serialize(battle.Events);
        List<BattleEvent>? roundTripped = JsonSerializer.Deserialize<List<BattleEvent>>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(battle.Events, roundTripped);
    }

    // --- self-sufficiency: reconstructing state from the log alone -------------

    private sealed record ReplayedUnit(bool Alive, int Hp, HexCoord Position);

    /// <summary>
    /// Rebuilds every unit's final alive/dead state, hit points and position
    /// from the log alone, plus a catalog to turn a spawn event's
    /// <see cref="UnitSpawnedEvent.DefinitionId"/> into starting HP — exactly
    /// what a client has, and nothing <see cref="BattleSimulation"/> or
    /// <see cref="BattleSetup"/> itself would additionally know.
    /// </summary>
    private static Dictionary<int, ReplayedUnit> Replay(
        IReadOnlyList<BattleEvent> events, IReadOnlyDictionary<string, UnitDefinition> catalog)
    {
        var state = new Dictionary<int, ReplayedUnit>();

        foreach (BattleEvent e in events)
        {
            switch (e)
            {
                case UnitSpawnedEvent spawned:
                    state[spawned.UnitId] = new ReplayedUnit(true, catalog[spawned.DefinitionId].MaxHp, spawned.Position);
                    break;
                case UnitMovedEvent moved:
                    state[moved.UnitId] = state[moved.UnitId] with { Position = moved.To };
                    break;
                case DamageDealtEvent damage:
                    state[damage.TargetId] = state[damage.TargetId] with { Hp = damage.ResultingHp };
                    break;
                case UnitDiedEvent died:
                    state[died.UnitId] = state[died.UnitId] with { Alive = false };
                    break;
            }
        }

        return state;
    }

    [Fact]
    public void Reconstructs_final_survivors_hp_and_position_from_the_log_alone()
    {
        // A mix of movement, repeated exchanges and a death, so the replay has
        // to get all four event kinds right to match.
        BattleSetup setup = Setup(
            [At(Runner, 3, 2), At(GlassCannon, 0, 4)],
            [At(Sentinel, 3, 9), At(GlassCannon, 0, 9)]);

        var battle = new BattleSimulation(setup, Seed, new BattleOptions { MaxTicks = 12 });
        battle.Run();

        var catalog = new Dictionary<string, UnitDefinition>
        {
            [Runner.Id] = Runner,
            [Sentinel.Id] = Sentinel,
            [GlassCannon.Id] = GlassCannon,
        };

        Dictionary<int, ReplayedUnit> replayed = Replay(battle.Events, catalog);

        // Sanity check that this fixture actually exercises a death — otherwise
        // the assertions below would pass just as well for a no-op replay.
        Assert.Contains(battle.Events, e => e is UnitDiedEvent);
        Assert.Contains(battle.Events, e => e is UnitMovedEvent);

        foreach (BattleUnit unit in setup.Units)
        {
            ReplayedUnit state = replayed[unit.Id];

            Assert.Equal(unit.IsAlive, state.Alive);
            Assert.Equal(unit.CurrentHp, state.Hp);
            Assert.Equal(unit.Position, state.Position);
        }

        Assert.Equal(
            setup.Units.Where(u => u.IsAlive).Select(u => u.Id).ToHashSet(),
            replayed.Where(kv => kv.Value.Alive).Select(kv => kv.Key).ToHashSet());
    }
}
