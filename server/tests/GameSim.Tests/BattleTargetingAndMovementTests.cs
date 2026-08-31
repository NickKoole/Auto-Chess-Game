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

    /// <summary>Crosses a hex every tick, so cadence never confuses a test about something else.</summary>
    private static readonly UnitDefinition Runner = new(
        id: "runner",
        displayName: "Runner",
        maxHp: 100,
        attackDamage: 12,
        attackIntervalTicks: 12,
        movementTicksPerHex: 1,
        attackRangeHexes: 1);

    /// <summary>Crosses a hex every 6 ticks — the roster's ordinary infantry pace.</summary>
    private static readonly UnitDefinition Militia = new(
        id: "militia",
        displayName: "Militia",
        maxHp: 100,
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

    /// <summary>Reaches 3 hexes, so it has somewhere to stop short of contact.</summary>
    private static readonly UnitDefinition Archer = new(
        id: "archer",
        displayName: "Archer",
        maxHp: 70,
        attackDamage: 14,
        attackIntervalTicks: 14,
        movementTicksPerHex: 1,
        attackRangeHexes: 3);

    /// <summary>
    /// Reaches across the whole board — the longest distance between two tiles
    /// on an 8x8 hex grid is 11 — so it is always already in range and never has
    /// a reason to move.
    ///
    /// This is how a test in this story pins a unit in place. Nothing can die
    /// yet, so a stationary opponent cannot be arranged by killing it, and
    /// setting a movement cooldown by hand would be testing against the very
    /// counter half these tests are about.
    /// </summary>
    private static readonly UnitDefinition Sentinel = new(
        id: "sentinel",
        displayName: "Sentinel",
        maxHp: 100,
        attackDamage: 12,
        attackIntervalTicks: 12,
        movementTicksPerHex: 1,
        attackRangeHexes: 11);

    private static UnitPlacement At(UnitDefinition definition, int row, int col) =>
        new(definition, new HexCoord(row, col));

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
        setup.Units.Single(u => u.Position == new HexCoord(row, col));

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
            [At(Sentinel, 5, 0)],
            [At(Sentinel, 1, 2), At(Sentinel, 2, 3)]);

        BattleUnit hunter = From(setup, 5, 0);
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

        Assert.Equal(3, new HexCoord(5, 0).DistanceTo(new HexCoord(2, 0)));
        Assert.Equal(3, new HexCoord(5, 0).DistanceTo(new HexCoord(2, 1)));

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
    public void Keeps_a_living_target_even_when_a_nearer_enemy_is_available()
    {
        // Targets are sticky. A unit that re-chose every tick would jitter
        // between two equidistant enemies and never close on either.
        BattleSetup setup = Setup(
            [At(Sentinel, 5, 0)],
            [At(Sentinel, 2, 0), At(Sentinel, 0, 7)]);

        BattleUnit hunter = From(setup, 5, 0);
        BattleUnit near = From(setup, 2, 0);
        BattleUnit far = From(setup, 0, 7);

        hunter.Target = far;

        Run(setup, 20);

        Assert.Same(far, hunter.Target);
        Assert.NotSame(near, hunter.Target);
    }

    // --- closing the distance ------------------------------------------------

    [Fact]
    public void Advances_one_hex_at_a_time_up_the_board()
    {
        // A Sentinel opponent, so the whole path is the mover's. The advance
        // zigzags between the two northern neighbours, and the tie break keeps
        // it centred on the target's column instead of drifting sideways.
        HexCoord[] path =
        [
            new(6, 3),
            new(5, 3),
            new(4, 3),
            new(3, 3),
            new(2, 3),
            new(1, 3),
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
            new(6, 3), new(6, 3), new(6, 3),
            new(5, 3), new(5, 3), new(5, 3),
            new(4, 3), new(4, 3), new(4, 3),
            new(3, 3),
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
        Assert.Equal(new HexCoord(1, 3), mover.Position);
        Assert.Equal(1, mover.Position.DistanceTo(new HexCoord(0, 3)));
    }

    [Fact]
    public void A_ranged_unit_stops_at_its_range_rather_than_closing()
    {
        BattleSetup setup = Setup([At(Archer, 7, 3)], [At(Sentinel, 0, 3)]);
        BattleUnit archer = From(setup, 7, 3);

        Run(setup, 40);

        // Range 3, so it halts three hexes out. A melee unit from the same tile
        // walks to (1, 3); this must not.
        Assert.Equal(new HexCoord(3, 3), archer.Position);
        Assert.Equal(3, archer.Position.DistanceTo(new HexCoord(0, 3)));
    }

    [Fact]
    public void Walks_through_the_neutral_band()
    {
        // Rows 3-4 are closed to deployment only. A mover that treated the band
        // as impassable would stall on row 5 and never reach its target.
        foreach ((int tick, HexCoord expected) in new[]
                 {
                     (3, new HexCoord(4, 3)),
                     (4, new HexCoord(3, 3)),
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
        // Only steps that strictly reduce the distance are candidates, so a unit
        // can never return to a tile it has left. Comparing an odd tick count
        // against an even one is what would expose a two-tile shuffle.
        static BattleSetup Build() => Setup(
            [At(Runner, 7, 1), At(Runner, 7, 2), At(Runner, 6, 4)],
            [At(Runner, 0, 1), At(Runner, 1, 5)]);

        BattleSetup odd = Build();
        BattleSetup even = Build();

        Assert.Equal(BattleEndReason.TickCap, Run(odd, 999).Reason);
        Assert.Equal(BattleEndReason.TickCap, Run(even, 1000).Reason);

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

        Assert.Equal(new HexCoord(6, 4), first.Position);
        Assert.Equal(new HexCoord(7, 4), second.Position);

        // Losing a contested tile is not an attempt it gets charged for.
        Assert.Equal(0, second.MovementCooldownTicks);
    }

    /// <summary>
    /// Two units each wanting the tile the other is standing on, with every
    /// other useful step walled off by stationary Sentinels.
    ///
    /// A battle never produces this on its own: both armies advance in the same
    /// direction, and nearest-enemy targeting means two units that are next to
    /// each other are heading the same way. So the southern unit's target is set
    /// by hand to a friendly unit behind it, purely to make it want to walk
    /// backwards into its neighbour.
    /// </summary>
    private static BattleSetup FacingOff(bool turnItAround)
    {
        BattleSetup setup = Setup(
            [
                At(Runner, 5, 3),    // wants (6, 3) once turned around
                At(Runner, 6, 3),    // wants (5, 3)
                At(Sentinel, 6, 4),  // walls off the northern unit's other step
                At(Sentinel, 5, 2),  // walls off the southern unit's other step
                At(Runner, 7, 3),    // the lure
            ],
            [At(Sentinel, 0, 3)]);

        if (turnItAround)
        {
            From(setup, 5, 3).Target = From(setup, 7, 3);
        }

        return setup;
    }

    [Fact]
    public void Two_adjacent_units_cannot_swap_tiles()
    {
        BattleSetup setup = FacingOff(turnItAround: true);
        BattleUnit north = From(setup, 5, 3);
        BattleUnit south = From(setup, 6, 3);

        Run(setup, 1);

        // Both tiles were occupied at the start of the tick, so neither is a
        // legal destination — no special case needed, and none written.
        Assert.Equal(new HexCoord(5, 3), north.Position);
        Assert.Equal(new HexCoord(6, 3), south.Position);

        Assert.Equal(0, north.MovementCooldownTicks);
        Assert.Equal(0, south.MovementCooldownTicks);
    }

    [Fact]
    public void The_units_that_cannot_swap_are_otherwise_willing_to_move()
    {
        // The control for the test above: same board, same walls, but the
        // northern unit keeps its natural target to the north. If it moves here,
        // then staying put there was the swap rule and not a frozen fixture.
        BattleSetup setup = FacingOff(turnItAround: false);
        BattleUnit north = From(setup, 5, 3);

        Run(setup, 1);

        Assert.NotEqual(new HexCoord(5, 3), north.Position);
    }

    [Fact]
    public void A_blocked_unit_keeps_its_readiness_and_goes_the_tick_the_tile_frees()
    {
        // (7, 3) wants (6, 3), which is occupied. Its only other closing step,
        // (6, 4), is walled off by a Sentinel — so it is blocked outright. The
        // unit on (6, 3) walks away this tick, and both are Militia, which cross
        // a hex in 6 ticks: a unit charged for its blocked attempt would sit
        // still until tick 7.
        static BattleSetup Build() => Setup(
            [At(Militia, 7, 3), At(Militia, 6, 3), At(Sentinel, 6, 4)],
            [At(Sentinel, 0, 3)]);

        BattleSetup afterOne = Build();
        BattleUnit blocked = From(afterOne, 7, 3);
        BattleUnit blocker = From(afterOne, 6, 3);

        Run(afterOne, 1);

        Assert.Equal(new HexCoord(5, 3), blocker.Position);
        Assert.Equal(new HexCoord(7, 3), blocked.Position);
        Assert.Equal(0, blocked.MovementCooldownTicks);

        BattleSetup afterTwo = Build();
        BattleUnit follower = From(afterTwo, 7, 3);

        Run(afterTwo, 2);

        Assert.Equal(new HexCoord(6, 3), follower.Position);
    }

    [Fact]
    public void A_unit_may_not_follow_another_into_a_tile_it_vacated_this_tick()
    {
        // The same board as above, one tick. Both units are ready and the one in
        // front does move — but the tile it leaves was occupied at the start of
        // the tick, so the one behind may not step into it until the next one.
        // Chasing the vacancy within a tick is sequential resolution in
        // disguise: it resolves as a chain, and the chain has an order.
        BattleSetup setup = Setup(
            [At(Runner, 7, 3), At(Runner, 6, 3), At(Sentinel, 6, 4)],
            [At(Sentinel, 0, 3)]);

        BattleUnit front = From(setup, 6, 3);
        BattleUnit behind = From(setup, 7, 3);

        Run(setup, 1);

        Assert.Equal(new HexCoord(5, 3), front.Position);
        Assert.Equal(new HexCoord(7, 3), behind.Position);
    }

    [Fact]
    public void A_unit_with_no_free_step_stays_put_without_erroring()
    {
        // Walled in on both of its closing steps for the whole battle: the
        // Sentinels never move, so the block never lifts. It must sit there for
        // a thousand ticks rather than throwing, looping, or wandering.
        BattleSetup setup = Setup(
            [At(Militia, 7, 3), At(Sentinel, 6, 3), At(Sentinel, 6, 4)],
            [At(Sentinel, 0, 3)]);

        BattleUnit stuck = From(setup, 7, 3);

        Assert.Equal(BattleEndReason.TickCap, Run(setup, BattleOptions.DefaultMaxTicks).Reason);
        Assert.Equal(new HexCoord(7, 3), stuck.Position);
    }

    [Fact]
    public void Every_unit_decides_against_the_board_as_it_was_at_the_start_of_the_tick()
    {
        // A column of four, all ready every tick. Under sequential resolution
        // the whole column shuffles forward together each tick, because each
        // unit sees the vacancy the one ahead just made. Two-phase, only the
        // front unit moves on any given tick and the rest follow one tick apart,
        // so after one tick exactly one of them has moved.
        BattleSetup setup = Setup(
            [At(Runner, 7, 3), At(Runner, 6, 3), At(Sentinel, 6, 4), At(Sentinel, 7, 4)],
            [At(Sentinel, 0, 3)]);

        HexCoord[] before = [.. setup.Units.Select(u => u.Position)];

        Run(setup, 1);

        HexCoord[] after = [.. setup.Units.Select(u => u.Position)];

        Assert.Equal(1, before.Zip(after).Count(pair => pair.First != pair.Second));
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
        // coordinates. The day one of them reaches for the generator instead,
        // this fails — and it should, because a tie break that consumes a draw
        // shifts the stream for every roll that comes after it.
        var battle = new BattleSimulation(
            Setup(
                [At(Runner, 7, 1), At(Militia, 7, 2), At(Archer, 6, 4)],
                [At(Runner, 0, 1), At(Militia, 1, 5)]),
            Seed,
            new BattleOptions { MaxTicks = 300 });

        battle.Run();

        Assert.Equal(new Rng(Seed).Snapshot(), battle.Rng.Snapshot());
    }
}
