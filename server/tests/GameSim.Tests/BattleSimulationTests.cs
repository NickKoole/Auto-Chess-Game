using GameSim;

namespace GameSim.Tests;

public sealed class BattleSimulationTests
{
    private const ulong Seed = 0xC0FFEE;

    private static readonly UnitDefinition Militia = new(
        id: "militia",
        displayName: "Militia",
        maxHp: 100,
        attackDamage: 12,
        attackIntervalTicks: 12,
        movementTicksPerHex: 6,
        attackRangeHexes: 1);

    private static UnitPlacement At(int row, int col) => new(Militia, new HexCoord(row, col));

    /// <summary>
    /// A fresh two-a-side setup. Built on every call rather than shared, because
    /// a battle mutates the units its setup holds — two runs from one setup
    /// would be a resumption, not a replay.
    /// </summary>
    private static BattleSetup Setup()
    {
        Assert.True(BattleSetup.TryCreate(
            [At(7, 1), At(5, 3)],
            [At(0, 2), At(2, 6)],
            out BattleSetup? setup,
            out _));

        return setup;
    }

    private static void Kill(BattleSetup setup, BattleSide side)
    {
        foreach (BattleUnit unit in setup.Units.Where(u => u.Side == side))
        {
            unit.CurrentHp = 0;
        }
    }

    // --- the loop and its termination ---------------------------------------

    [Fact]
    public void Runs_to_the_default_tick_cap_and_draws_when_nobody_can_act()
    {
        var battle = new BattleSimulation(Setup(), Seed);

        BattleResult result = battle.Run();

        // The correct outcome for this story: units have no behaviour yet, so
        // neither army can ever be eliminated and the cap is the only exit.
        Assert.Equal(BattleOutcome.Draw, result.Outcome);
        Assert.Equal(BattleEndReason.TickCap, result.Reason);
        Assert.Equal(BattleOptions.DefaultMaxTicks, result.EndTick);
    }

    [Fact]
    public void Stops_at_the_configured_tick_cap()
    {
        var battle = new BattleSimulation(Setup(), Seed, new BattleOptions { MaxTicks = 7 });

        BattleResult result = battle.Run();

        Assert.Equal(7, result.EndTick);
        Assert.Equal(7, battle.Tick);
        Assert.Equal(BattleEndReason.TickCap, result.Reason);
    }

    [Fact]
    public void Runs_exactly_as_many_ticks_as_the_cap_allows()
    {
        // Distinguishes a correct loop from the off-by-one either side of it:
        // a cap of 1 must run one tick, not zero and not two.
        foreach (int cap in new[] { 1, 2, 3, 50 })
        {
            var battle = new BattleSimulation(Setup(), Seed, new BattleOptions { MaxTicks = cap });

            Assert.Equal(cap, battle.Run().EndTick);
        }
    }

    [Fact]
    public void Reports_a_player_win_when_the_opponent_has_no_living_units()
    {
        BattleSetup setup = Setup();
        Kill(setup, BattleSide.Opponent);

        BattleResult result = new BattleSimulation(setup, Seed).Run();

        Assert.Equal(BattleOutcome.PlayerWin, result.Outcome);
        Assert.Equal(BattleEndReason.Elimination, result.Reason);
        Assert.Equal(0, result.EndTick);
    }

    [Fact]
    public void Reports_an_opponent_win_when_the_player_has_no_living_units()
    {
        BattleSetup setup = Setup();
        Kill(setup, BattleSide.Player);

        BattleResult result = new BattleSimulation(setup, Seed).Run();

        Assert.Equal(BattleOutcome.OpponentWin, result.Outcome);
        Assert.Equal(BattleEndReason.Elimination, result.Reason);
    }

    [Fact]
    public void Reports_a_draw_by_elimination_when_both_sides_are_wiped_out()
    {
        BattleSetup setup = Setup();
        Kill(setup, BattleSide.Player);
        Kill(setup, BattleSide.Opponent);

        BattleResult result = new BattleSimulation(setup, Seed).Run();

        // Distinct from the tick-cap draw: both armies are gone, which is a
        // resolved battle rather than a stalled one.
        Assert.Equal(BattleOutcome.Draw, result.Outcome);
        Assert.Equal(BattleEndReason.Elimination, result.Reason);
    }

    [Fact]
    public void Treats_a_unit_on_negative_health_as_dead()
    {
        // Overkill drives HP below zero rather than clamping it, so a check
        // written as `CurrentHp != 0` would keep a corpse fighting.
        BattleSetup setup = Setup();
        foreach (BattleUnit unit in setup.Units.Where(u => u.Side == BattleSide.Opponent))
        {
            unit.CurrentHp = -40;
        }

        Assert.Equal(BattleOutcome.PlayerWin, new BattleSimulation(setup, Seed).Run().Outcome);
    }

    // --- determinism ---------------------------------------------------------

    [Fact]
    public void Same_setup_and_seed_produce_an_identical_result()
    {
        BattleResult first = new BattleSimulation(Setup(), Seed).Run();
        BattleResult second = new BattleSimulation(Setup(), Seed).Run();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Same_setup_and_seed_leave_identical_unit_state_and_rng_state()
    {
        // The result alone is a weak assertion while units have no behaviour:
        // every battle currently draws at the cap. Comparing the whole ending
        // state is what will actually catch divergence as behaviour lands.
        var a = new BattleSimulation(Setup(), Seed);
        var b = new BattleSimulation(Setup(), Seed);

        a.Run();
        b.Run();

        Assert.Equal(a.Rng.Snapshot(), b.Rng.Snapshot());
        Assert.Equal(
            a.Setup.Units.Select(u => (u.Side, u.Definition.Id, u.Position, u.CurrentHp, u.AttackCooldownTicks)),
            b.Setup.Units.Select(u => (u.Side, u.Definition.Id, u.Position, u.CurrentHp, u.AttackCooldownTicks)));
    }

    [Fact]
    public void Seeds_its_generator_from_the_seed_it_was_given()
    {
        var battle = new BattleSimulation(Setup(), Seed);

        Assert.Equal(new Rng(Seed).Snapshot(), battle.Rng.Snapshot());
    }

    [Fact]
    public void The_loop_itself_consumes_no_randomness()
    {
        // Guards the rule rather than today's behaviour: the day the loop
        // shuffles its turn order, or reaches for a second generator to break a
        // tie, this fails. A battle's randomness must be spent by units acting,
        // where the event log can account for it.
        var battle = new BattleSimulation(Setup(), Seed, new BattleOptions { MaxTicks = 200 });

        battle.Run();

        Assert.Equal(new Rng(Seed).Snapshot(), battle.Rng.Snapshot());
    }

    // --- action order --------------------------------------------------------

    [Fact]
    public void Units_act_in_the_order_the_setup_established()
    {
        // Listed deliberately out of order, so a pass-through of the caller's
        // list would not produce this expectation.
        Assert.True(BattleSetup.TryCreate(
            [At(7, 4), At(5, 0), At(7, 1)],
            [At(2, 3), At(0, 5)],
            out BattleSetup? setup,
            out _));

        var battle = new BattleSimulation(setup, Seed);

        Assert.Equal(
            [
                (BattleSide.Player, new HexCoord(5, 0)),
                (BattleSide.Player, new HexCoord(7, 1)),
                (BattleSide.Player, new HexCoord(7, 4)),
                (BattleSide.Opponent, new HexCoord(0, 5)),
                (BattleSide.Opponent, new HexCoord(2, 3)),
            ],
            battle.LivingUnits().Select(u => (u.Side, u.Position)));

        Assert.Equal(setup.Units, battle.LivingUnits());
    }

    [Fact]
    public void The_loop_gives_every_living_unit_one_turn_per_tick_in_that_order()
    {
        // Asserts on the loop, not on the helper that feeds it. Without this,
        // RunTick could iterate Setup.Units directly — acting on the dead — or
        // iterate backwards, and every other test would still pass.
        Assert.True(BattleSetup.TryCreate(
            [At(7, 4), At(5, 0), At(7, 1)],
            [At(2, 3), At(0, 5)],
            out BattleSetup? setup,
            out _));

        var battle = new BattleSimulation(setup, Seed, new BattleOptions { MaxTicks = 1 });
        battle.Run();

        Assert.Equal(setup.Units, battle.TurnsTakenLastTick);
    }

    [Fact]
    public void The_loop_gives_no_turn_to_a_dead_unit()
    {
        BattleSetup setup = Setup();
        setup.Units[1].CurrentHp = 0;

        var battle = new BattleSimulation(setup, Seed, new BattleOptions { MaxTicks = 1 });
        battle.Run();

        Assert.Equal(
            new[] { setup.Units[0], setup.Units[2], setup.Units[3] },
            battle.TurnsTakenLastTick);
    }

    [Fact]
    public void Takes_no_turns_at_all_before_it_runs()
    {
        Assert.Empty(new BattleSimulation(Setup(), Seed).TurnsTakenLastTick);
    }

    [Fact]
    public void Gives_each_unit_exactly_one_turn_per_tick_however_long_the_battle()
    {
        // The recorded turns are per tick, not cumulative, so a loop that acted
        // twice per tick — or leaked turns between ticks — shows up here.
        BattleSetup setup = Setup();
        var battle = new BattleSimulation(setup, Seed, new BattleOptions { MaxTicks = 40 });

        battle.Run();

        Assert.Equal(setup.Units, battle.TurnsTakenLastTick);
    }

    [Fact]
    public void Dead_units_drop_out_of_the_action_order_without_disturbing_it()
    {
        BattleSetup setup = Setup();
        var battle = new BattleSimulation(setup, Seed);

        BattleUnit[] expected = [.. setup.Units];
        setup.Units[1].CurrentHp = 0;

        Assert.Equal(
            new[] { expected[0], expected[2], expected[3] },
            battle.LivingUnits());
    }

    // --- guards --------------------------------------------------------------

    [Fact]
    public void Refuses_a_null_setup()
    {
        Assert.Throws<ArgumentNullException>(() => new BattleSimulation(null!, Seed));
    }

    [Fact]
    public void Refuses_to_run_twice()
    {
        var battle = new BattleSimulation(Setup(), Seed, new BattleOptions { MaxTicks = 3 });
        battle.Run();

        Assert.Throws<InvalidOperationException>(() => battle.Run());
    }

    [Fact]
    public void Exposes_no_result_until_it_has_run()
    {
        var battle = new BattleSimulation(Setup(), Seed, new BattleOptions { MaxTicks = 3 });

        Assert.Null(battle.Result);
        Assert.Equal(0, battle.Tick);

        BattleResult result = battle.Run();

        Assert.Equal(result, battle.Result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Refuses_a_non_positive_tick_cap(int cap)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleOptions { MaxTicks = cap });
    }

    [Fact]
    public void Defaults_to_the_default_tick_cap()
    {
        Assert.Equal(BattleOptions.DefaultMaxTicks, BattleOptions.Default.MaxTicks);
        Assert.Equal(BattleOptions.DefaultMaxTicks, new BattleOptions().MaxTicks);
        Assert.Equal(BattleOptions.Default.MaxTicks, new BattleSimulation(Setup(), Seed).Options.MaxTicks);
    }
}
