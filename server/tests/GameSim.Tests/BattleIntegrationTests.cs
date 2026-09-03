namespace GameSim.Tests;

/// <summary>
/// Story 1.7: the one test that wires the catalog, a realistic setup and the
/// simulation together and checks that a full battle behaves like a battle —
/// not merely that each piece it is built from compiles.
///
/// The roster here is deliberately lopsided rather than a mirror match. A
/// mirrored full roster was tried first and empirically resolves cleanly most
/// of the time, but mutual-elimination draws still turned up in roughly 3% of
/// 200 sampled seeds — exactly the kind of flakiness this test cannot afford,
/// since "the losing side has no survivors, the winning side has at least one"
/// is a hard acceptance criterion, not a usual case. A one-sided army built
/// from the same catalog gives a decisive winner with no observed exceptions
/// and stays well inside CLAUDE.md's "balance tuning is out of scope" line:
/// nothing here asserts anything about which composition is stronger, only
/// that combat converges and the log matches what happened.
/// </summary>
public sealed class BattleIntegrationTests
{
    // Enough seeds to make "this always resolves decisively" a real claim
    // rather than one lucky roll, while staying fast: a full 6v6 to
    // completion runs in low single-digit milliseconds.
    private static readonly ulong[] Seeds = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 42, 1337, 90210, 271828, 314159];

    private static UnitPlacement At(UnitDefinition definition, int row, int col) =>
        new(definition, new HexCoord(row, col));

    /// <summary>
    /// A fresh 6v6, legally deployed: player in rows 5-7, opponent in rows 0-2.
    /// Built anew per call, since a battle consumes the setup it runs on.
    /// </summary>
    private static BattleSetup Setup()
    {
        var catalog = new StaticUnitCatalog();
        UnitDefinition militia = catalog.Get("militia");
        UnitDefinition knight = catalog.Get("knight");
        UnitDefinition berserker = catalog.Get("berserker");
        UnitDefinition archer = catalog.Get("archer");
        UnitDefinition mage = catalog.Get("mage");

        Assert.True(BattleSetup.TryCreate(
            [
                At(knight, 5, 1), At(knight, 5, 3),
                At(archer, 6, 1), At(archer, 6, 3),
                At(mage, 6, 5), At(berserker, 5, 5),
            ],
            [
                At(militia, 2, 1), At(militia, 2, 3), At(militia, 2, 5),
                At(militia, 1, 1), At(militia, 1, 3), At(militia, 1, 5),
            ],
            out BattleSetup? setup,
            out IReadOnlyList<PlacementError> errors));

        Assert.Empty(errors);
        return setup!;
    }

    /// <summary>
    /// Rebuilds each side's final survivor count from the event log alone —
    /// every unit's side from its <see cref="UnitSpawnedEvent"/>, then removing
    /// whichever ones have a <see cref="UnitDiedEvent"/> — with no access to
    /// <see cref="BattleSetup"/> or <see cref="BattleUnit"/> state.
    /// </summary>
    private static (int PlayerAlive, int OpponentAlive) ReconstructSurvivors(IReadOnlyList<BattleEvent> events)
    {
        var side = new Dictionary<int, BattleSide>();
        var dead = new HashSet<int>();

        foreach (BattleEvent e in events)
        {
            switch (e)
            {
                case UnitSpawnedEvent spawned:
                    side[spawned.UnitId] = spawned.Side;
                    break;
                case UnitDiedEvent died:
                    dead.Add(died.UnitId);
                    break;
            }
        }

        int playerAlive = side.Count(kv => kv.Value == BattleSide.Player && !dead.Contains(kv.Key));
        int opponentAlive = side.Count(kv => kv.Value == BattleSide.Opponent && !dead.Contains(kv.Key));
        return (playerAlive, opponentAlive);
    }

    [Fact]
    public void A_full_battle_resolves_decisively_with_plausible_casualties_and_a_log_that_matches()
    {
        foreach (ulong seed in Seeds)
        {
            var battle = new BattleSimulation(Setup(), seed);
            BattleResult result = battle.Run();

            // --- terminates with a winner, not the tick cap -----------------
            Assert.NotEqual(BattleEnding.TickCapDraw, result.Ending);
            Assert.NotEqual(BattleOutcome.Draw, result.Outcome);
            Assert.True(
                result.EndTick < BattleOptions.DefaultMaxTicks,
                $"Seed {seed}: expected the battle to resolve well before the tick cap, " +
                $"but it ran the full {result.EndTick} ticks.");

            // --- casualties are plausible ------------------------------------
            int playerAlive = battle.Setup.Units.Count(u => u.Side == BattleSide.Player && u.IsAlive);
            int opponentAlive = battle.Setup.Units.Count(u => u.Side == BattleSide.Opponent && u.IsAlive);

            (int winnerAlive, int loserAlive) = result.Outcome == BattleOutcome.PlayerWin
                ? (playerAlive, opponentAlive)
                : (opponentAlive, playerAlive);

            Assert.True(winnerAlive >= 1, $"Seed {seed}: the winning side has no survivors.");
            Assert.Equal(0, loserAlive);

            // --- the event log is non-empty and reconstructs the result -----
            Assert.NotEmpty(battle.Events);

            (int reconstructedPlayerAlive, int reconstructedOpponentAlive) = ReconstructSurvivors(battle.Events);
            Assert.Equal(playerAlive, reconstructedPlayerAlive);
            Assert.Equal(opponentAlive, reconstructedOpponentAlive);
        }
    }

    /// <summary>
    /// The API's <c>melee-mirror</c> matchup: three knights a side, player on
    /// row 5 and opponent on row 2, columns 2-4. Rebuilt from the same roster
    /// rather than referenced, because <c>GameSim</c> cannot see <c>Api</c> —
    /// the reference direction only runs the other way.
    /// </summary>
    private static BattleSetup MeleeMirror()
    {
        UnitDefinition knight = new StaticUnitCatalog().Get("knight");

        Assert.True(BattleSetup.TryCreate(
            [At(knight, 5, 2), At(knight, 5, 3), At(knight, 5, 4)],
            [At(knight, 2, 2), At(knight, 2, 3), At(knight, 2, 4)],
            out BattleSetup? setup,
            out _));

        return setup!;
    }

    [Fact]
    public void No_unit_stands_in_reach_of_a_living_enemy_without_swinging()
    {
        // Reported as #49: a knight stood adjacent to a live enemy for 211
        // consecutive ticks without attacking, because its target was fixed on
        // someone two hexes away and walled off, and attack range is tested
        // against the target alone.
        //
        // A unit continuously in reach swings every AttackIntervalTicks, so the
        // longest legitimate run of in-reach-but-idle ticks is one interval less
        // one. Allowing a full interval leaves a tick of slack and still fails by
        // an order of magnitude on the reported behaviour.
        UnitDefinition knight = new StaticUnitCatalog().Get("knight");

        foreach (ulong seed in new ulong[] { 12345, 1, 777 })
        {
            var battle = new BattleSimulation(MeleeMirror(), seed);
            BattleResult result = battle.Run();

            Dictionary<int, List<BattleEvent>> byTick = battle.Events
                .GroupBy(e => e.Tick)
                .ToDictionary(g => g.Key, g => g.ToList());

            var position = new Dictionary<int, HexCoord>();
            var side = new Dictionary<int, BattleSide>();
            var alive = new HashSet<int>();

            foreach (UnitSpawnedEvent spawned in battle.Events.OfType<UnitSpawnedEvent>())
            {
                position[spawned.UnitId] = spawned.Position;
                side[spawned.UnitId] = spawned.Side;
                alive.Add(spawned.UnitId);
            }

            // A sorted list, never the dictionary's own enumeration order: which
            // unit a failure names would otherwise be a runtime detail.
            int[] ids = [.. position.Keys.Order()];
            var idleRun = ids.ToDictionary(id => id, _ => 0);

            for (int tick = 1; tick <= result.EndTick; tick++)
            {
                List<BattleEvent> events = byTick.TryGetValue(tick, out List<BattleEvent>? found) ? found : [];

                foreach (BattleEvent e in events)
                {
                    if (e is UnitMovedEvent moved)
                    {
                        position[moved.UnitId] = moved.To;
                    }
                }

                var swung = events.OfType<UnitAttackedEvent>().Select(a => a.AttackerId).ToHashSet();

                foreach (BattleEvent e in events)
                {
                    if (e is UnitDiedEvent died)
                    {
                        alive.Remove(died.UnitId);
                    }
                }

                foreach (int id in ids)
                {
                    bool inReach = alive.Contains(id) && alive.Any(other =>
                        side[other] != side[id]
                        && position[id].DistanceTo(position[other]) <= knight.AttackRangeHexes);

                    idleRun[id] = inReach && !swung.Contains(id) ? idleRun[id] + 1 : 0;

                    Assert.True(
                        idleRun[id] <= knight.AttackIntervalTicks,
                        $"Seed {seed}: unit {id} spent {idleRun[id]} consecutive ticks within reach of a "
                        + $"living enemy without attacking, ending at tick {tick} on {position[id]}. "
                        + $"A knight swings every {knight.AttackIntervalTicks} ticks.");
                }
            }
        }
    }

    [Fact]
    public void No_unit_is_stranded_out_of_reach_while_a_route_to_an_enemy_exists()
    {
        // Reported as #51: a unit whose direct approach was held by its own front
        // rank stood still until an ally died and freed the lane. In melee-mirror
        // two of the six spent 81 and 95 consecutive ticks out of reach that way.
        //
        // A knight crosses a hex in 8 ticks, so crossing the board's 8 rows takes
        // well under 100; 40 consecutive ticks out of reach with a route available
        // is far beyond ordinary travel and far below the reported stall.
        const int Stranded = 40;

        UnitDefinition knight = new StaticUnitCatalog().Get("knight");

        foreach (ulong seed in new ulong[] { 12345, 1, 777 })
        {
            var battle = new BattleSimulation(MeleeMirror(), seed);
            BattleResult result = battle.Run();

            Dictionary<int, List<BattleEvent>> byTick = battle.Events
                .GroupBy(e => e.Tick)
                .ToDictionary(g => g.Key, g => g.ToList());

            var position = new Dictionary<int, HexCoord>();
            var side = new Dictionary<int, BattleSide>();
            var alive = new HashSet<int>();

            foreach (UnitSpawnedEvent spawned in battle.Events.OfType<UnitSpawnedEvent>())
            {
                position[spawned.UnitId] = spawned.Position;
                side[spawned.UnitId] = spawned.Side;
                alive.Add(spawned.UnitId);
            }

            int[] ids = [.. position.Keys.Order()];
            var strandedFor = ids.ToDictionary(id => id, _ => 0);

            for (int tick = 1; tick <= result.EndTick; tick++)
            {
                List<BattleEvent> events = byTick.TryGetValue(tick, out List<BattleEvent>? found) ? found : [];

                foreach (BattleEvent e in events)
                {
                    if (e is UnitMovedEvent moved)
                    {
                        position[moved.UnitId] = moved.To;
                    }
                }

                foreach (BattleEvent e in events)
                {
                    if (e is UnitDiedEvent died)
                    {
                        alive.Remove(died.UnitId);
                    }
                }

                foreach (int id in ids)
                {
                    bool anyEnemyLeft = alive.Any(other => side[other] != side[id]);
                    bool inReach = alive.Any(other =>
                        side[other] != side[id]
                        && position[id].DistanceTo(position[other]) <= knight.AttackRangeHexes);

                    bool stranded = alive.Contains(id) && anyEnemyLeft && !inReach;
                    strandedFor[id] = stranded ? strandedFor[id] + 1 : 0;

                    Assert.True(
                        strandedFor[id] <= Stranded,
                        $"Seed {seed}: unit {id} spent {strandedFor[id]} consecutive ticks out of reach of "
                        + $"every enemy, ending at tick {tick} on {position[id]}. The board is 8 rows deep "
                        + "and a knight crosses a hex in 8 ticks, so it is not travelling — it is stuck.");
                }
            }
        }
    }

    [Fact]
    public void No_unit_ever_steps_onto_a_tile_that_was_occupied_when_the_tick_began()
    {
        // Occupied tiles are walls, to the route search as much as to the step
        // itself — so a unit can neither trade places with a neighbour nor follow
        // one into the tile it is vacating this very tick. Chasing a vacancy
        // within a tick resolves as a chain, and a chain has an order, which is
        // what the two-phase tick exists to keep out of the outcome.
        //
        // Checked over whole battles rather than a staged collision: pathing means
        // a unit never proposes an occupied tile in the first place, so the
        // collision can no longer be built by hand, and the property is what is
        // left to verify.
        foreach (ulong seed in Seeds)
        {
            var battle = new BattleSimulation(Setup(), seed);
            BattleResult result = battle.Run();

            var position = new Dictionary<int, HexCoord>();
            foreach (UnitSpawnedEvent spawned in battle.Events.OfType<UnitSpawnedEvent>())
            {
                position[spawned.UnitId] = spawned.Position;
            }

            foreach (IGrouping<int, BattleEvent> tick in battle.Events.Where(e => e.Tick > 0).GroupBy(e => e.Tick))
            {
                var occupiedAtTickStart = position
                    .Where(kv => kv.Key != -1)
                    .ToDictionary(kv => kv.Value, kv => kv.Key);

                foreach (UnitMovedEvent moved in tick.OfType<UnitMovedEvent>())
                {
                    Assert.False(
                        occupiedAtTickStart.TryGetValue(moved.To, out int occupant) && occupant != moved.UnitId,
                        $"Seed {seed}: unit {moved.UnitId} moved onto {moved.To} at tick {tick.Key}, "
                        + $"which unit {occupant} occupied when the tick began.");

                    Assert.Equal(position[moved.UnitId], moved.From);
                    position[moved.UnitId] = moved.To;
                }

                foreach (UnitDiedEvent died in tick.OfType<UnitDiedEvent>())
                {
                    position.Remove(died.UnitId);
                }
            }

            Assert.NotEqual(BattleEnding.TickCapDraw, result.Ending);
        }
    }

    [Fact]
    public void The_same_seed_produces_an_identical_result_and_log_on_a_second_run()
    {
        foreach (ulong seed in Seeds)
        {
            var a = new BattleSimulation(Setup(), seed);
            var b = new BattleSimulation(Setup(), seed);

            BattleResult resultA = a.Run();
            BattleResult resultB = b.Run();

            Assert.Equal(resultA, resultB);
            Assert.Equal(a.Events, b.Events);
        }
    }
}
