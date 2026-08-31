using GameSim;

namespace GameSim.Tests;

/// <summary>
/// Damage, death, and how a battle actually concludes — story 1.5.
///
/// Every fixture here uses a long attack range (11, which covers the whole
/// board per <see cref="HexCoord.DistanceTo"/>'s reach on an 8x8 grid) unless a
/// test is specifically about closing distance. That keeps combat timing exact
/// and free of movement noise: units start ready and already in range, so
/// "tick 1" is a known, reproducible moment to assert against rather than
/// something that depends on how many ticks a walk happened to take.
/// </summary>
public sealed class BattleCombatTests
{
    private const ulong Seed = 0xBADA55;

    /// <summary>
    /// A generic combatant with enough HP to survive many exchanges, so a test
    /// can run several ticks of real combat without a battle resolving out from
    /// under it.
    /// </summary>
    private static readonly UnitDefinition Brawler = new(
        id: "brawler",
        displayName: "Brawler",
        maxHp: 500,
        attackDamage: 20,
        attackIntervalTicks: 5,
        movementTicksPerHex: 1,
        attackRangeHexes: 11,
        damageVariance: 0.20);

    /// <summary>
    /// Zero variance and damage far past any plausible HP: a hit from this, or
    /// onto this, is always exactly lethal and never a matter of the roll. Used
    /// wherever a test needs a death to happen on a known tick rather than
    /// merely being likely to.
    /// </summary>
    private static readonly UnitDefinition GlassCannon = new(
        id: "glass-cannon",
        displayName: "Glass Cannon",
        maxHp: 10,
        attackDamage: 100,
        attackIntervalTicks: 5,
        movementTicksPerHex: 1,
        attackRangeHexes: 11,
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

    /// <summary>The unit that started on a tile. Call before running; see the equivalent helper in BattleTargetingAndMovementTests.</summary>
    private static BattleUnit From(BattleSetup setup, int row, int col) =>
        setup.Units.Single(u => u.Position == new HexCoord(row, col));

    // --- attacking, cooldown, and the roll ------------------------------------

    [Fact]
    public void Attacks_when_ready_and_in_range_then_resets_its_cooldown_to_the_attack_interval()
    {
        BattleSetup setup = Setup([At(Brawler, 5, 0)], [At(Brawler, 0, 0)]);
        BattleUnit attacker = From(setup, 5, 0);
        BattleUnit defender = From(setup, 0, 0);

        new BattleSimulation(setup, Seed, new BattleOptions { MaxTicks = 1 }).Run();

        // Both are always in range and both start ready (BattleUnitTests
        // confirms a fresh unit's AttackCooldownTicks is zero), so both swing on
        // the very first tick.
        Assert.True(defender.CurrentHp < Brawler.MaxHp);
        Assert.Equal(Brawler.AttackIntervalTicks, attacker.AttackCooldownTicks);
    }

    [Fact]
    public void Holds_its_swing_until_the_cooldown_has_actually_elapsed()
    {
        BattleSetup afterOneTick = Setup([At(Brawler, 5, 0)], [At(Brawler, 0, 0)]);
        BattleUnit hitOnce = From(afterOneTick, 0, 0);
        new BattleSimulation(afterOneTick, Seed, new BattleOptions { MaxTicks = 1 }).Run();

        BattleSetup rightBeforeReady = Setup([At(Brawler, 5, 0)], [At(Brawler, 0, 0)]);
        BattleUnit stillOnce = From(rightBeforeReady, 0, 0);
        new BattleSimulation(
            rightBeforeReady, Seed, new BattleOptions { MaxTicks = Brawler.AttackIntervalTicks - 1 }).Run();

        // Same seed, same first hit: if a second swing snuck in anywhere before
        // the interval elapsed, this diverges.
        Assert.Equal(hitOnce.CurrentHp, stillOnce.CurrentHp);
    }

    [Fact]
    public void Draws_from_rng_only_on_the_tick_a_hit_actually_lands()
    {
        BattleSetup afterOneTick = Setup([At(Brawler, 5, 0)], [At(Brawler, 0, 0)]);
        var oneTickBattle = new BattleSimulation(afterOneTick, Seed, new BattleOptions { MaxTicks = 1 });
        oneTickBattle.Run();

        BattleSetup rightBeforeReady = Setup([At(Brawler, 5, 0)], [At(Brawler, 0, 0)]);
        var almostReadyBattle = new BattleSimulation(
            rightBeforeReady, Seed, new BattleOptions { MaxTicks = Brawler.AttackIntervalTicks - 1 });
        almostReadyBattle.Run();

        // No further hit becomes possible until the interval elapses, so the
        // generator must be exactly where it was after the one hit that did.
        Assert.Equal(oneTickBattle.Rng.Snapshot(), almostReadyBattle.Rng.Snapshot());
    }

    [Fact]
    public void Does_not_attack_while_its_target_is_out_of_range()
    {
        var melee = new UnitDefinition(
            id: "melee",
            displayName: "Melee",
            maxHp: 100,
            attackDamage: 999,
            attackIntervalTicks: 1,
            movementTicksPerHex: 10_000, // one free step, then stuck for the whole run
            attackRangeHexes: 1,
            damageVariance: 0.0);

        BattleSetup setup = Setup([At(melee, 5, 0)], [At(melee, 0, 0)]);
        BattleUnit defender = From(setup, 0, 0);

        // 5 hexes apart; the one free step each side takes closes at most 2 of
        // them, leaving both still well outside melee range for the rest of the
        // run.
        new BattleSimulation(setup, Seed, new BattleOptions { MaxTicks = 10 }).Run();

        Assert.Equal(melee.MaxHp, defender.CurrentHp);
    }

    [Fact]
    public void Damage_lands_within_the_variance_band_floored_to_an_integer()
    {
        int minExpected = (int)(Brawler.AttackDamage * (1.0 - Brawler.DamageVariance));
        int maxExpected = (int)(Brawler.AttackDamage * (1.0 + Brawler.DamageVariance));

        var seenDamages = new HashSet<int>();

        for (ulong seed = 1; seed <= 60; seed++)
        {
            BattleSetup setup = Setup([At(Brawler, 5, 0)], [At(Brawler, 0, 0)]);
            BattleUnit defender = From(setup, 0, 0);

            new BattleSimulation(setup, seed, new BattleOptions { MaxTicks = 1 }).Run();

            int damage = Brawler.MaxHp - defender.CurrentHp;

            Assert.InRange(damage, minExpected, maxExpected);
            seenDamages.Add(damage);
        }

        // The band actually varies. A roll that always returned the same
        // number would pass the range check above while still being wrong.
        Assert.True(seenDamages.Count > 1, "Expected more than one distinct damage value across 60 seeds.");
    }

    [Fact]
    public void Same_seed_produces_identical_damage_rolls_across_two_runs()
    {
        BattleSetup a = Setup(
            [At(Brawler, 5, 0), At(Brawler, 5, 1)],
            [At(Brawler, 0, 0), At(Brawler, 0, 1)]);
        BattleSetup b = Setup(
            [At(Brawler, 5, 0), At(Brawler, 5, 1)],
            [At(Brawler, 0, 0), At(Brawler, 0, 1)]);

        new BattleSimulation(a, Seed, new BattleOptions { MaxTicks = 30 }).Run();
        new BattleSimulation(b, Seed, new BattleOptions { MaxTicks = 30 }).Run();

        Assert.Equal(a.Units.Select(u => u.CurrentHp), b.Units.Select(u => u.CurrentHp));
    }

    // --- death, removal, and the tile it frees --------------------------------

    [Fact]
    public void A_lethal_hit_kills_the_target_and_frees_its_tile_by_the_end_of_the_tick()
    {
        BattleSetup setup = Setup([At(GlassCannon, 5, 0)], [At(GlassCannon, 0, 0)]);
        BattleUnit defender = From(setup, 0, 0);
        HexCoord defenderTile = defender.Position;

        new BattleSimulation(setup, Seed, new BattleOptions { MaxTicks = 1 }).Run();

        Assert.False(defender.IsAlive);
        Assert.Null(setup.Battlefield.UnitAt(defenderTile));
        Assert.True(setup.Battlefield.IsPassable(defenderTile));
    }

    [Fact]
    public void Two_units_that_kill_each_other_both_land_their_hit_and_both_die_on_the_same_tick()
    {
        BattleSetup setup = Setup([At(GlassCannon, 5, 0)], [At(GlassCannon, 0, 0)]);
        BattleUnit player = From(setup, 5, 0);
        BattleUnit opponent = From(setup, 0, 0);

        BattleResult result = new BattleSimulation(setup, Seed).Run();

        // Neither is spared for having "died first": tick-start snapshot means
        // both connect before either death is resolved.
        Assert.False(player.IsAlive);
        Assert.False(opponent.IsAlive);
        Assert.Equal(BattleEnding.MutualElimination, result.Ending);
        Assert.Equal(1, result.EndTick);
    }

    [Fact]
    public void A_unit_whose_target_dies_to_someone_else_reacquires_on_the_next_tick()
    {
        // The killer and the victim are close together; the survivor sits far
        // across the board, so the bystander's initial nearest-enemy choice is
        // unambiguously the victim rather than the survivor.
        BattleSetup setup = Setup(
            [At(GlassCannon, 5, 0), At(Brawler, 5, 1)],
            [At(GlassCannon, 0, 0), At(Brawler, 0, 7)]);

        BattleUnit victim = From(setup, 0, 0);
        BattleUnit bystander = From(setup, 5, 1);
        BattleUnit survivor = From(setup, 0, 7);

        new BattleSimulation(setup, Seed, new BattleOptions { MaxTicks = 2 }).Run();

        Assert.False(victim.IsAlive);
        Assert.Same(survivor, bystander.Target);
    }

    // --- how a battle concludes ------------------------------------------------

    [Fact]
    public void A_one_versus_one_resolves_to_the_stronger_side()
    {
        var weakling = new UnitDefinition(
            id: "weakling",
            displayName: "Weakling",
            maxHp: 1,
            attackDamage: 1,
            attackIntervalTicks: 1000,
            movementTicksPerHex: 1,
            attackRangeHexes: 11,
            damageVariance: 0.0);

        BattleSetup setup = Setup([At(Brawler, 5, 0)], [At(weakling, 0, 0)]);

        BattleResult result = new BattleSimulation(setup, Seed).Run();

        Assert.Equal(BattleEnding.PlayerEliminatedOpponent, result.Ending);
        Assert.Equal(BattleOutcome.PlayerWin, result.Outcome);
    }

    [Fact]
    public void Reaches_the_tick_cap_as_a_draw_even_with_a_lopsided_board()
    {
        // Damage low enough, and its interval long enough, that nothing here
        // can be finished off within the cap tested — the point is the board
        // imbalance, not a race against combat.
        var tank = new UnitDefinition(
            id: "tank",
            displayName: "Tank",
            maxHp: 100_000,
            attackDamage: 1,
            attackIntervalTicks: 1000,
            movementTicksPerHex: 1,
            attackRangeHexes: 11,
            damageVariance: 0.0);

        BattleSetup setup = Setup([At(tank, 5, 0)], [At(tank, 0, 0), At(tank, 0, 1)]);
        BattleUnit lopsided = From(setup, 5, 0);

        // Deliberately lopsided survival: one side down to a single unit on low
        // HP, the other untouched. No outcome is ever decided by unit count,
        // remaining HP, or damage dealt — only elimination is, and nobody here
        // is eliminated.
        lopsided.CurrentHp = 5;

        BattleResult result = new BattleSimulation(setup, Seed, new BattleOptions { MaxTicks = 50 }).Run();

        Assert.Equal(BattleEnding.TickCapDraw, result.Ending);
        Assert.Equal(BattleOutcome.Draw, result.Outcome);
        Assert.True(lopsided.IsAlive);
    }

    [Fact]
    public void BattleEnding_has_exactly_the_four_legal_endings()
    {
        // Locks the enum's shape: BattleEnding exists specifically so no fifth,
        // impossible ending can ever be added back in by accident.
        Assert.Equal(
            [
                BattleEnding.PlayerEliminatedOpponent,
                BattleEnding.OpponentEliminatedPlayer,
                BattleEnding.MutualElimination,
                BattleEnding.TickCapDraw,
            ],
            Enum.GetValues<BattleEnding>());
    }

    [Theory]
    [InlineData(BattleEnding.PlayerEliminatedOpponent, BattleOutcome.PlayerWin)]
    [InlineData(BattleEnding.OpponentEliminatedPlayer, BattleOutcome.OpponentWin)]
    [InlineData(BattleEnding.MutualElimination, BattleOutcome.Draw)]
    [InlineData(BattleEnding.TickCapDraw, BattleOutcome.Draw)]
    public void BattleResult_computes_the_outcome_from_the_ending(BattleEnding ending, BattleOutcome expected)
    {
        var result = new BattleResult(ending, EndTick: 3);

        Assert.Equal(expected, result.Outcome);
    }
}
