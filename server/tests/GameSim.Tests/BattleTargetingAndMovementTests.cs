using GameSim;

namespace GameSim.Tests;

/// <summary>
/// Targeting, movement, and the two-phase tick that resolves them.
///
/// One class rather than three, because the three are one behaviour: a unit
/// picks an enemy, decides a step against the board as it was at the start of
/// the tick, and the step lands only if nobody earlier claimed the tile. Split
/// up, each half's tests would need the other half's fixtures anyway.
/// </summary>
public sealed class BattleTargetingAndMovementTests
{
    private const ulong Seed = 0xC0FFEE;

    /// <summary>
    /// Crosses a hex every tick, so cadence never confuses a test about something
    /// else. HP is high enough that story 1.5's real combat cannot finish one of
    /// these off within any run this file makes — this class is about targeting
    /// and movement, and a unit that can now die mid-test would make combat, not
    /// the thing under test, decide the outcome.
    /// </summary>
    private static readonly UnitDefinition Runner = new(
        id: "runner",
        displayName: "Runner",
        maxHp: 1_000_000,
        attackDamage: 12,
        attackIntervalTicks: 12,
        movementTicksPerHex: 1,
        attackRangeHexes: 1);

    /// <summary>Crosses a hex every 6 ticks — the roster's ordinary infantry pace. See <see cref="Runner"/> for why HP is unrealistically high.</summary>
    private static readonly UnitDefinition Militia = new(
        id: "militia",
        displayName: "Militia",
        maxHp: 1_000_000,
        attackDamage: 12,
        attackIntervalTicks: 12,
        movementTicksPerHex: 6,
        attackRangeHexes: 1);

    /// <summary>Crosses a hex every 3 ticks: the cadence the acceptance criteria name.</summary>
    private static readonly UnitDefinition Strider = new(
        id: "strider",
        displayName: "Strider",
        maxHp: 100,
        attackDamage: 12,
        attackIntervalTicks: 12,
        movementTicksPerHex: 3,
        attackRangeHexes: 1);

    /// <summary>Reaches 3 hexes, so it has somewhere to stop short of contact. See <see cref="Runner"/> for why HP is unrealistically high.</summary>
    private static readonly UnitDefinition Archer = new(
        id: "archer",
        displayName: "Archer",
        maxHp: 1_000_000,
        attackDamage: 14,
        attackIntervalTicks: 14,
        movementTicksPerHex: 1,
        attackRangeHexes: 3);

    /// <summary>
    /// Reaches across the whole board — the longest distance between two tiles
    /// on an 8x8 hex grid is 11 — so it is always already in range and never has
    /// a reason to move.
    ///
    /// This is how a test in this story pins a unit in place. Since story 1.5,
    /// "always in range" also means a Sentinel is always willing to swing, so its
    /// HP is unrealistically high for the same reason as <see cref="Runner"/>'s:
    /// two Sentinels facing off, or a wall of them, must stay standing for the
    /// whole of a long test rather than trading itself down to nothing. Setting a
    /// movement cooldown by hand would be testing against the very mechanism
    /// these tests are about, which is why this exists instead.
    /// </summary>
    private static readonly UnitDefinition Sentinel = new(
        id: "sentinel",
        displayName: "Sentinel",
        maxHp: 1_000_000,
        attackDamage: 12,
        attackIntervalTicks: 12,
        movementTicksPerHex: 1,
        attackRangeHexes: 11);

    private static UnitPlacement At(UnitDefinition definition, int row, int col) =>
        new(definition, Horizontal(row, col));

    private static HexCoord Horizontal(int row, int col) => new(col, Battlefield.Rows - 1 - row);

    private static BattleSetup Setup(
        IReadOnlyList<UnitPlacement> player,
        IReadOnlyList<UnitPlacement> opponent)
    {
        Assert.True(BattleSetup.TryCreate(player, opponent, out BattleSetup? setup, out _));
        return setup;
    }

    /// <summary>
    /// The unit that started on a tile. Call it before running: units move, so
    /// this identifies one by where it deployed, and the reference stays good
    /// for the rest of the battle.
    /// </summary>
    private static BattleUnit From(BattleSetup setup, int row, int col) =>
        setup.Units.Single(u => u.Position == Horizontal(row, col));

    private static BattleResult Run(BattleSetup setup, int ticks) =>
        new BattleSimulation(setup, Seed, new BattleOptions { MaxTicks = ticks }).Run();

    // --- acquiring a target --------------------------------------------------

    [Fact]
    public void Acquires_the_nearest_enemy()
    {
        BattleSetup setup = Setup(
            [At(Sentinel, 7, 3)],
            [At(Sentinel, 2, 3), At(Sentinel, 0, 0)]);

        BattleUnit hunter = From(setup, 7, 3);
        BattleUnit near = From(setup, 2, 3);

        Run(setup, 1);

        // 5 hexes away against 7. Sentinels throughout, so nothing moves and the
        // distances the choice was made on are still on the board to read.
        Assert.Same(near, hunter.Target);
    }

    [Fact]
    public void Breaks_a_distance_tie_on_the_lowest_row()
    {
        // Both enemies are 4 hexes from (5, 0). The one in row 2 is in the
        // nearer rank and comes first in BattleSetup.Units order, so this fails
        // both for an implementation that keeps whichever it saw first and for
        // one that reaches for the nearest row instead of the nearest hex.
        BattleSetup setup = Setup(
            [At(Sentinel, 5, 1)],
            [At(Sentinel, 1, 2), At(Sentinel, 2, 3)]);

        BattleUnit hunter = From(setup, 5, 1);
        BattleUnit lowestRow = From(setup, 1, 2);

        Assert.Equal(4, new HexCoord(5, 0).DistanceTo(new HexCoord(1, 2)));
        Assert.Equal(4, new HexCoord(5, 0).DistanceTo(new HexCoord(2, 3)));

        Run(setup, 1);

        Assert.Same(lowestRow, hunter.Target);
    }

    [Fact]
    public void Breaks_a_row_tie_on_the_lowest_column()
    {
        // Both 3 hexes away and both in row 2, so only the column separates
        // them. Catches the mirror of the test above — an implementation that
        // kept the last equal candidate rather than the first.
        BattleSetup setup = Setup(
            [At(Sentinel, 5, 0)],
            [At(Sentinel, 2, 0), At(Sentinel, 2, 1)]);

        BattleUnit hunter = From(setup, 5, 0);
        BattleUnit lowestColumn = From(setup, 2, 0);

        Assert.Equal(3, Horizontal(5, 1).DistanceTo(Horizontal(2, 0)));
        Assert.Equal(3, Horizontal(5, 1).DistanceTo(Horizontal(2, 1)));

        Run(setup, 1);

        Assert.Same(lowestColumn, hunter.Target);
    }

    [Fact]
    public void Passes_over_a_dead_enemy_when_acquiring()
    {
        BattleSetup setup = Setup(
            [At(Sentinel, 5, 0)],
            [At(Sentinel, 2, 0), At(Sentinel, 2, 3)]);

        BattleUnit hunter = From(setup, 5, 0);
        BattleUnit nearestButDead = From(setup, 2, 0);
        BattleUnit alive = From(setup, 2, 3);

        nearestButDead.CurrentHp = 0;

        Run(setup, 1);

        Assert.Same(alive, hunter.Target);
    }

    [Fact]
    public void Re_acquires_when_the_target_it_holds_is_dead()
    {
        // The criterion is "a unit whose target is dead acquires the nearest
        // living enemy". Nothing can kill anything in this story and Run is
        // atomic, so the state is arranged before the battle rather than partway
        // through it — the check is stateless and runs every tick, so it is the
        // same branch either way.
        BattleSetup setup = Setup(
            [At(Sentinel, 5, 0)],
            [At(Sentinel, 2, 0), At(Sentinel, 2, 3)]);

        BattleUnit hunter = From(setup, 5, 0);
        BattleUnit corpse = From(setup, 2, 0);
        BattleUnit survivor = From(setup, 2, 3);

        hunter.Target = corpse;
        corpse.CurrentHp = 0;

        Run(setup, 1);

        Assert.Same(survivor, hunter.Target);
    }

    [Fact]
    public void Switches_to_an_enemy_that_is_strictly_nearer_than_the_target_it_holds()
    {
        // Targets are re-chosen every tick. Sentinels throughout, so nothing
        // moves and the distances the choice was made on stay on the board: the
        // hunter is handed a target 9 hexes away while a living enemy stands at
        // 3, and must come off it.
        BattleSetup setup = Setup(
            [At(Sentinel, 5, 0)],
            [At(Sentinel, 2, 0), At(Sentinel, 0, 7)]);

        BattleUnit hunter = From(setup, 5, 0);
        BattleUnit near = From(setup, 2, 0);
        BattleUnit far = From(setup, 0, 7);

        Assert.Equal(3, new HexCoord(5, 0).DistanceTo(new HexCoord(2, 0)));
        Assert.Equal(9, new HexCoord(5, 0).DistanceTo(new HexCoord(0, 7)));

        hunter.Target = far;

        Run(setup, 20);

        Assert.Same(near, hunter.Target);
    }

    [Fact]
    public void Keeps_its_target_when_another_enemy_is_exactly_as_close()
    {
        // The property that makes re-choosing every tick safe: only a strictly
        // nearer enemy displaces the incumbent. Both enemies sit 3 hexes out, and
        // (2, 0) is the one the acquisition tie break would pick on its own — so
        // handing the hunter the *other* one is what makes this fail if the
        // comparison is ever loosened to "no further than". Twenty ticks, so a
        // unit that alternated would be caught whichever parity it landed on.
        BattleSetup setup = Setup(
            [At(Sentinel, 5, 1)],
            [At(Sentinel, 2, 0), At(Sentinel, 2, 1)]);

        BattleUnit hunter = From(setup, 5, 1);
        BattleUnit wouldWinTheTieBreak = From(setup, 2, 0);
        BattleUnit held = From(setup, 2, 1);

        Assert.Equal(3, Horizontal(5, 1).DistanceTo(Horizontal(2, 0)));
        Assert.Equal(3, Horizontal(5, 1).DistanceTo(Horizontal(2, 1)));

        hunter.Target = held;

        Run(setup, 20);

        Assert.Same(held, hunter.Target);
        Assert.NotSame(wouldWinTheTieBreak, hunter.Target);
    }

    [Fact]
    public void Comes_off_a_distant_target_and_actually_attacks_the_nearer_enemy()
    {
        // The shape of the reported bug (#49): a unit fixated on something far
        // away while a much closer enemy is there to be fought. The two armies
        // deploy either side of the neutral band and so cannot start in contact,
        // which is why this closes the last hexes by walking rather than by
        // being placed adjacent.
        //
        // Asserting the swing and not just the target is the point: the bug was
        // never that a unit held the wrong reference, it was that the unit stood
        // there doing nothing. Both enemies are Sentinels, which never move, so
        // the distances the choice turns on stay put.
        BattleSetup setup = Setup(
            [At(Runner, 5, 0)],
            [At(Sentinel, 2, 0), At(Sentinel, 0, 7)]);

        BattleUnit hunter = From(setup, 5, 0);
        BattleUnit near = From(setup, 2, 0);
        BattleUnit far = From(setup, 0, 7);

        Assert.Equal(3, new HexCoord(5, 0).DistanceTo(new HexCoord(2, 0)));
        Assert.Equal(9, new HexCoord(5, 0).DistanceTo(new HexCoord(0, 7)));

        hunter.Target = far;

        // One attack interval, so "eventually" is not what is being claimed.
        var battle = new BattleSimulation(setup, Seed, new BattleOptions { MaxTicks = Runner.AttackIntervalTicks });
        battle.Run();

        Assert.Same(near, hunter.Target);
        Assert.Contains(
            battle.Events,
            e => e is UnitAttackedEvent a && a.AttackerId == hunter.Id && a.TargetId == near.Id);
    }

    // --- closing the distance ------------------------------------------------

    [Fact]
    public void Advances_one_hex_at_a_time_across_the_board()
    {
        // A Sentinel opponent, so the whole path is the mover's. The advance
        // zigzags between the two northern neighbours, and the tie break keeps
        // it centred on the target's column instead of drifting sideways.
        HexCoord[] path =
        [
            Horizontal(6, 3),
            Horizontal(5, 3),
            Horizontal(4, 3),
            Horizontal(3, 3),
            Horizontal(2, 3),
            Horizontal(1, 3),
        ];

        for (int tick = 1; tick <= path.Length; tick++)
        {
            BattleSetup setup = Setup([At(Runner, 7, 3)], [At(Sentinel, 0, 3)]);
            BattleUnit mover = From(setup, 7, 3);

            Run(setup, tick);

            Assert.Equal(path[tick - 1], mover.Position);
        }
    }

    [Fact]
    public void Moves_no_more_often_than_its_movement_speed_allows()
    {
        // Crosses a hex in 3 ticks, so it steps on ticks 1, 4, 7, 10 and stands
        // still in between. Run afresh for each tick count, because a battle
        // consumes its setup.
        HexCoord[] expected =
        [
            Horizontal(6, 3), Horizontal(6, 3), Horizontal(6, 3),
            Horizontal(5, 3), Horizontal(5, 3), Horizontal(5, 3),
            Horizontal(4, 3), Horizontal(4, 3), Horizontal(4, 3),
            Horizontal(3, 3),
        ];

        var actual = new List<HexCoord>();

        for (int tick = 1; tick <= expected.Length; tick++)
        {
            BattleSetup setup = Setup([At(Strider, 7, 3)], [At(Sentinel, 0, 3)]);
            BattleUnit mover = From(setup, 7, 3);

            Run(setup, tick);
            actual.Add(mover.Position);
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Holds_position_once_its_target_is_within_reach()
    {
        BattleSetup setup = Setup([At(Runner, 7, 3)], [At(Sentinel, 0, 3)]);
        BattleUnit mover = From(setup, 7, 3);

        Run(setup, 40);

        // Adjacent to its target and stopped, rather than pressed against it or
        // shuffling around it for the remaining 34 ticks.
        Assert.Equal(Horizontal(1, 3), mover.Position);
        Assert.Equal(1, mover.Position.DistanceTo(Horizontal(0, 3)));
    }

    [Fact]
    public void A_ranged_unit_stops_at_its_range_rather_than_closing()
    {
        BattleSetup setup = Setup([At(Archer, 7, 3)], [At(Sentinel, 0, 3)]);
        BattleUnit archer = From(setup, 7, 3);

        Run(setup, 40);

        // Range 3, so it halts three hexes out. A melee unit from the same tile
        // walks to (1, 3); this must not.
        Assert.Equal(Horizontal(3, 3), archer.Position);
        Assert.Equal(3, archer.Position.DistanceTo(Horizontal(0, 3)));
    }

    [Fact]
    public void Walks_through_the_neutral_band()
    {
        // Rows 3-4 are closed to deployment only. A mover that treated the band
        // as impassable would stall on row 5 and never reach its target.
        foreach ((int tick, HexCoord expected) in new[]
                 {
                     (3, Horizontal(4, 3)),
                     (4, Horizontal(3, 3)),
                 })
        {
            BattleSetup setup = Setup([At(Runner, 7, 3)], [At(Sentinel, 0, 3)]);
            BattleUnit mover = From(setup, 7, 3);

            Run(setup, tick);

            Assert.Equal(expected, mover.Position);
            Assert.True(Battlefield.IsNeutralBand(mover.Position));
        }
    }

    [Fact]
    public void Never_steps_off_the_board()
    {
        // Every unit starts on an edge or a corner, where two of the six
        // neighbours are off the board. Battlefield.Move throws on a coordinate
        // that is not on the board, so an unfiltered step fails loudly here
        // rather than silently.
        BattleSetup setup = Setup(
            [At(Runner, 7, 0), At(Runner, 7, 7), At(Runner, 5, 0), At(Runner, 5, 7)],
            [At(Runner, 0, 0), At(Runner, 0, 7), At(Runner, 2, 0), At(Runner, 2, 7)]);

        Run(setup, 200);

        Assert.All(setup.Units, u => Assert.True(Battlefield.IsInBounds(u.Position)));
    }

    [Fact]
    public void Settles_instead_of_oscillating()
    {
        // Only steps that strictly reduce the distance to the target are
        // candidates, so nothing here shuffles on the spot. That is a weaker
        // claim than it looks — targets move, so a unit's distance to its own
        // target can rise between ticks — which is exactly why this is an
        // empirical check on a real battle rather than an appeal to the rule.
        // Comparing an odd tick count against an even one is what would expose a
        // two-tile shuffle.
        static BattleSetup Build() => Setup(
            [At(Runner, 7, 1), At(Runner, 7, 2), At(Runner, 6, 4)],
            [At(Runner, 0, 1), At(Runner, 1, 5)]);

        BattleSetup odd = Build();
        BattleSetup even = Build();

        Assert.Equal(BattleEnding.TickCapDraw, Run(odd, 999).Ending);
        Assert.Equal(BattleEnding.TickCapDraw, Run(even, 1000).Ending);

        Assert.Equal(
            odd.Units.Select(u => (u.Side, u.Definition.Id, u.Position)),
            even.Units.Select(u => (u.Side, u.Definition.Id, u.Position)));
    }

    // --- blocked, contested, and the two-phase tick --------------------------

    [Fact]
    public void Two_units_reaching_for_the_same_tile_are_settled_by_the_setup_order()
    {
        // Both player units are drawn to (6, 4): it is one of two equally close
        // steps for each of them, and it is the one that closes the column gap
        // to the target. (7, 3) comes first in BattleSetup.Units order — same
        // row, lower column — so it takes the tile.
        BattleSetup setup = Setup(
            [At(Runner, 7, 3), At(Runner, 7, 4)],
            [At(Sentinel, 0, 4)]);

        BattleUnit first = From(setup, 7, 3);
        BattleUnit second = From(setup, 7, 4);

        Run(setup, 1);

        Assert.Equal(Horizontal(6, 4), first.Position);
        Assert.Equal(Horizontal(7, 4), second.Position);

        // Losing a contested tile is not an attempt it gets charged for.
        Assert.Equal(0, second.MovementCooldownTicks);
    }

    /// <summary>
    /// Two units sealed into a corner pocket with only each other's tiles
    /// between them and the rest of the board, plus a switch to unseal it.
    ///
    /// (7, 0) and (6, 0) each have three on-board neighbours; walling (5, 0),
    /// (6, 1) and (7, 1) leaves each of them with no free neighbour at all
    /// except the tile the other is standing on.
    /// </summary>
    private static BattleSetup Pocket(bool sealIt) => Setup(
        sealIt
            ? [At(Runner, 7, 0), At(Runner, 6, 0), At(Sentinel, 5, 0), At(Sentinel, 6, 1), At(Sentinel, 7, 1)]
            : [At(Runner, 7, 0), At(Runner, 6, 0), At(Sentinel, 5, 0), At(Sentinel, 7, 1)],
        [At(Sentinel, 0, 3)]);

    [Fact]
    public void Two_adjacent_units_cannot_swap_tiles()
    {
        // Each unit's only free neighbour is the tile the other stands on, and
        // an occupied tile is a wall — to the route search as much as to the
        // step itself. So neither unit has a route to the enemy and neither
        // moves, and in particular neither ends up where the other was.
        //
        // Since pathing landed this is structural rather than a rule applied at
        // the last moment: a unit cannot even propose an occupied tile, because
        // the search never traces a route through one. The old fixture here made
        // two units reach for each other's tiles and relied on the claim check to
        // refuse them; that situation can no longer be built, which is why this
        // now asserts the invariant instead of staging the collision.
        BattleSetup setup = Pocket(sealIt: true);
        BattleUnit north = From(setup, 6, 0);
        BattleUnit south = From(setup, 7, 0);

        Run(setup, 1);

        Assert.Equal(Horizontal(6, 0), north.Position);
        Assert.Equal(Horizontal(7, 0), south.Position);

        // Having nowhere to go is not an attempt either gets charged for.
        Assert.Equal(0, north.MovementCooldownTicks);
        Assert.Equal(0, south.MovementCooldownTicks);
    }

    [Fact]
    public void The_sealed_in_units_are_otherwise_willing_to_move()
    {
        // The control for the test above: the same pocket with (6, 1) left open.
        // If the northern unit walks out here, then staying put there was the
        // sealed board and not a frozen fixture.
        BattleSetup setup = Pocket(sealIt: false);
        BattleUnit north = From(setup, 6, 0);

        Run(setup, 1);

        Assert.NotEqual(Horizontal(6, 0), north.Position);
    }

    [Fact]
    public void A_blocked_unit_keeps_its_readiness_and_goes_the_tick_the_tile_frees()
    {
        // (7, 3) wants (6, 3), which is occupied. Sentinels hold every other
        // neighbour it has — (6, 4), (7, 2) and (7, 4), the rest being off the
        // board — so there is no way round and it is blocked outright. Walling
        // all three is what makes this a test about being blocked rather than
        // about pathing: leave one open and the unit simply routes around it.
        //
        // The unit on (6, 3) walks away this tick, and both are Militia, which
        // cross a hex in 6 ticks: a unit charged for its blocked attempt would
        // sit still until tick 7.
        static BattleSetup Build() => Setup(
            [
                At(Militia, 7, 3), At(Militia, 6, 3),
                At(Sentinel, 6, 4), At(Sentinel, 7, 2), At(Sentinel, 7, 4),
            ],
            [At(Sentinel, 0, 3)]);

        BattleSetup afterOne = Build();
        BattleUnit blocked = From(afterOne, 7, 3);
        BattleUnit blocker = From(afterOne, 6, 3);

        Run(afterOne, 1);

        Assert.Equal(new HexCoord(3, 2), blocker.Position);
        Assert.Equal(new HexCoord(2, 1), blocked.Position);
        Assert.Equal(Militia.MovementTicksPerHex, blocked.MovementCooldownTicks);

        BattleSetup afterTwo = Build();
        BattleUnit follower = From(afterTwo, 7, 3);

        Run(afterTwo, 2);

        Assert.Equal(new HexCoord(2, 1), follower.Position);
    }

    [Fact]
    public void A_unit_may_not_follow_another_into_a_tile_it_vacated_this_tick()
    {
        // The same walled board as above, one tick. Both units are ready and the
        // one in front does move — but the tile it leaves was occupied at the
        // start of the tick, so the one behind may not step into it until the
        // next one. Chasing the vacancy within a tick is sequential resolution
        // in disguise: it resolves as a chain, and the chain has an order.
        //
        // (6, 3) is the only neighbour the rear unit has that is not held by a
        // Sentinel, so following it is the one move available — which is what
        // makes staying put evidence of the rule rather than of a lack of
        // options.
        BattleSetup setup = Setup(
            [
                At(Runner, 7, 3), At(Runner, 6, 3),
                At(Sentinel, 6, 4), At(Sentinel, 7, 2), At(Sentinel, 7, 4),
            ],
            [At(Sentinel, 0, 3)]);

        BattleUnit front = From(setup, 6, 3);
        BattleUnit behind = From(setup, 7, 3);

        Run(setup, 1);

        Assert.Equal(Horizontal(5, 3), front.Position);
        Assert.NotEqual(Horizontal(6, 3), behind.Position);
    }

    [Fact]
    public void A_unit_with_no_free_step_stays_put_without_erroring()
    {
        // Sealed in: Sentinels hold all four of its on-board neighbours and never
        // move, so no route to the enemy exists at all and none ever opens. It
        // must sit there for a thousand ticks rather than throwing, looping, or
        // wandering — the pathfinder finding no route is the case this pins, and
        // it needs every neighbour held, not merely the closing ones.
        BattleSetup setup = Setup(
            [
                At(Militia, 7, 3),
                At(Sentinel, 6, 3), At(Sentinel, 6, 4), At(Sentinel, 7, 2), At(Sentinel, 7, 4),
            ],
            [At(Sentinel, 0, 3)]);

        BattleUnit stuck = From(setup, 7, 3);

        Assert.Equal(BattleEnding.TickCapDraw, Run(setup, BattleOptions.DefaultMaxTicks).Ending);
        Assert.True(Battlefield.IsInBounds(stuck.Position));
    }

    [Fact]
    public void Every_unit_decides_against_the_board_as_it_was_at_the_start_of_the_tick()
    {
        // A column of two in a walled lane, both ready every tick. Under
        // sequential resolution both move: the rear unit sees the vacancy the
        // front one just made and follows it within the tick. Two-phase, only
        // the front unit moves, so after one tick exactly one of them has.
        //
        // The Sentinels hold every neighbour of the rear unit except (6, 3), so
        // following the vacancy is its only possible move and the count is a
        // clean read on the two-phase rule.
        BattleSetup setup = Setup(
            [
                At(Runner, 7, 3), At(Runner, 6, 3),
                At(Sentinel, 6, 4), At(Sentinel, 7, 2), At(Sentinel, 7, 4),
            ],
            [At(Sentinel, 0, 3)]);

        HexCoord[] before = [.. setup.Units.Select(u => u.Position)];

        Run(setup, 1);

        HexCoord[] after = [.. setup.Units.Select(u => u.Position)];

        Assert.Equal(before.Length, after.Distinct().Count());
    }

    // --- routing around an obstruction ---------------------------------------

    [Fact]
    public void Walks_around_its_own_front_rank_instead_of_waiting_for_it_to_die()
    {
        // The reported stall (#51). The mover at (7, 3) has its direct approach
        // held by allied Sentinels on (6, 3) and (6, 4) — the two steps that
        // close the distance — but (7, 2) is open and leads north around them.
        // The Sentinels never move and never die, so a unit that only ever steps
        // strictly closer waits here forever.
        BattleSetup setup = Setup(
            [At(Runner, 7, 3), At(Sentinel, 6, 3), At(Sentinel, 6, 4)],
            [At(Sentinel, 0, 3)]);

        BattleUnit mover = From(setup, 7, 3);

        Assert.Equal(1, mover.Position.DistanceTo(Horizontal(6, 3)));

        Run(setup, 40);

        // Reached contact with the enemy without any ally dying to clear a lane.
        Assert.Equal(1, mover.Position.DistanceTo(Horizontal(0, 3)));
        Assert.All(setup.Units, u => Assert.True(u.IsAlive));
    }

    [Fact]
    public void A_tie_is_settled_by_the_coordinate_rule_and_not_by_neighbour_order()
    {
        // The tie that discriminates, and the only kind that can.
        //
        // HexCoord.Neighbours() returns a fixed array: (Row-1, Col) and
        // (Row-1, Col+lean) first, then (Row, Col-1) before (Row, Col+1). That
        // west-before-east order holds for every row, and the coordinate rule's
        // last resort is also the lowest column — so on any same-row tie the two
        // agree, and a test built on one cannot tell them apart.
        //
        // The northern diagonal pair can disagree. From (7, 3) the two north
        // steps are (6, 3), listed first, and (6, 4), listed second; against a
        // target on column 4 they are both 6 hexes out, so the route lengths tie
        // and the column gap decides — 0 for (6, 4) against 1 for (6, 3). An
        // implementation that took whichever tied neighbour the array happened to
        // list first would answer (6, 3), which is what makes this test worth
        // having.
        BattleSetup setup = Setup([At(Runner, 7, 3)], [At(Sentinel, 0, 4)]);

        BattleUnit mover = From(setup, 7, 3);

        Assert.Equal(6, new HexCoord(6, 3).DistanceTo(new HexCoord(0, 4)));
        Assert.Equal(6, new HexCoord(6, 4).DistanceTo(new HexCoord(0, 4)));
        Assert.Equal(new HexCoord(2, 0), mover.Position.Neighbours()[0]);
        Assert.Equal(new HexCoord(2, 1), mover.Position.Neighbours()[1]);

        Run(setup, 1);

        Assert.Equal(Horizontal(6, 4), mover.Position);
    }

    [Fact]
    public void Equal_length_detours_are_settled_by_the_same_rule()
    {
        // Both ways around the wall are the same length — west through (7, 2),
        // east through (7, 4) — so length alone does not pick one and the
        // coordinate rule decides. The gap ties at 1 and the rows tie at 7, so
        // the lowest column wins.
        //
        // This one cannot distinguish the rule from neighbour order, for the
        // reason the test above exists; it is here to pin the detour's first step
        // rather than to prove which mechanism chose it.
        BattleSetup setup = Setup(
            [At(Runner, 7, 3), At(Sentinel, 6, 3), At(Sentinel, 6, 4)],
            [At(Sentinel, 0, 3)]);

        BattleUnit mover = From(setup, 7, 3);

        Run(setup, 1);

        Assert.Equal(new HexCoord(2, 1), mover.Position);
    }

    [Fact]
    public void Does_not_backtrack_while_its_target_stays_put()
    {
        // Routing around means a unit sometimes steps without shortening the
        // straight-line distance, which is what the old greedy rule forbade
        // outright. What replaces that guarantee is the route length falling on
        // every step, so a unit still cannot revisit a tile while the board it
        // pathed against is unchanged. Sentinels never move or die, so it is.
        var visited = new List<HexCoord> { new(7, 3) };

        for (int tick = 1; tick <= 40; tick++)
        {
            BattleSetup step = Setup(
                [At(Runner, 7, 3), At(Sentinel, 6, 3), At(Sentinel, 6, 4)],
                [At(Sentinel, 0, 3)]);

            // Captured before the run: From identifies a unit by where it is
            // standing, and after a battle that is no longer where it deployed.
            BattleUnit mover = From(step, 7, 3);

            Run(step, tick);

            if (mover.Position != visited[^1])
            {
                Assert.DoesNotContain(mover.Position, visited);
                visited.Add(mover.Position);
            }
        }

        // It genuinely travelled, rather than passing by never moving at all.
        Assert.True(visited.Count > 3, $"Expected a real journey, got {visited.Count} distinct tiles.");
    }

    // --- determinism ---------------------------------------------------------

    [Fact]
    public void Two_runs_of_a_moving_battle_end_in_exactly_the_same_place()
    {
        static BattleSetup Build() => Setup(
            [At(Runner, 7, 1), At(Militia, 7, 2), At(Archer, 6, 4), At(Strider, 5, 5)],
            [At(Runner, 0, 1), At(Militia, 1, 5), At(Archer, 2, 2)]);

        BattleSetup a = Build();
        BattleSetup b = Build();

        Run(a, 300);
        Run(b, 300);

        Assert.Equal(
            a.Units.Select(u => (u.Definition.Id, u.Position, u.MovementCooldownTicks, u.Target?.Position)),
            b.Units.Select(u => (u.Definition.Id, u.Position, u.MovementCooldownTicks, u.Target?.Position)));
    }

    [Fact]
    public void Targeting_and_movement_spend_no_randomness()
    {
        // Every tie in this story is broken by a documented rule on the
        // coordinates, and since story 1.5 a landed hit is the only thing a
        // battle may ever draw for. Glacial() keeps each unit's cadence and
        // range shape but slows it far past anything that closes within this
        // run, so nobody ever gets in range and there is nothing here left to
        // draw for. The two corners are 8-10 hexes apart — every unit still
        // takes its single always-ready first step, so the gap can close by up
        // to 2, but even then it stays comfortably past the 3-hex reach of the
        // longest-ranged unit here. The day targeting or movement reaches for
        // the generator anyway, this fails — a tie break that consumes a draw
        // shifts the stream for every roll that comes after it.
        var battle = new BattleSimulation(
            Setup(
                [At(Glacial(Runner), 7, 0), At(Glacial(Militia), 7, 1), At(Glacial(Archer), 6, 0)],
                [At(Glacial(Runner), 0, 7), At(Glacial(Militia), 0, 6)]),
            Seed,
            new BattleOptions { MaxTicks = 300 });

        battle.Run();

        Assert.Equal(new Rng(Seed).Snapshot(), battle.Rng.Snapshot());
    }

    /// <summary>
    /// A copy of <paramref name="definition"/> that takes 10,000 ticks to cross
    /// one hex — far past any run this file makes — so it still takes its single
    /// always-ready first step but then sits for the rest of the test. Keeps
    /// every other stat, including range and interval, so a test using this is
    /// still exercising the real shape of that unit's decisions, just without
    /// ever letting movement actually close the distance.
    /// </summary>
    private static UnitDefinition Glacial(UnitDefinition definition) => new(
        id: definition.Id,
        displayName: definition.DisplayName,
        maxHp: definition.MaxHp,
        attackDamage: definition.AttackDamage,
        attackIntervalTicks: definition.AttackIntervalTicks,
        movementTicksPerHex: 10_000,
        attackRangeHexes: definition.AttackRangeHexes);
}
