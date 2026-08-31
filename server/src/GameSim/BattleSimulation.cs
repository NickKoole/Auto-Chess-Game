namespace GameSim;

/// <summary>
/// Runs one battle from one setup and one seed, and reports how it ended.
///
/// The clock is an integer tick counter and nothing else. There is no
/// wall-clock time anywhere below this line: a tick is not a duration, it is an
/// index, and the caller decides how many of them to run and how fast to draw
/// them. That is what lets the same battle be simulated live in a request, in a
/// test, and in an offline catch-up pass, and come out identical every time.
///
/// A simulation is stateful and single-use. It mutates the
/// <see cref="BattleUnit"/> instances its <see cref="BattleSetup"/> holds, so
/// the setup is consumed by the battle: to run the same armies twice — as a
/// determinism test must — build the setup twice rather than sharing one.
/// <see cref="Run"/> refuses to run a second time on the same instance, which
/// catches the near miss but cannot catch that one.
/// </summary>
public sealed class BattleSimulation
{
    // The units that decided in the tick currently running, cleared at the top
    // of each one. Only the latest tick is kept: this exists so the loop's
    // decide order is observable, and a battle-long history would be a
    // thousand-entry event log built for a test — which is story 1.6's job,
    // done properly.
    private readonly List<BattleUnit> _turnsThisTick = [];

    /// <summary>
    /// Prepares a battle. Nothing runs until <see cref="Run"/> is called.
    /// </summary>
    /// <param name="setup">The starting position. Already validated by construction.</param>
    /// <param name="seed">
    /// Seeds this battle's single <see cref="GameSim.Rng"/>. The same seed and
    /// the same setup must always produce the same result.
    /// </param>
    /// <param name="options">Match rules; <see cref="BattleOptions.Default"/> when null.</param>
    public BattleSimulation(BattleSetup setup, ulong seed, BattleOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(setup);

        Setup = setup;
        Options = options ?? BattleOptions.Default;
        Rng = new Rng(seed);
    }

    /// <summary>The starting position, and the board the battle is fought on.</summary>
    public BattleSetup Setup { get; }

    /// <summary>The rules this battle runs under.</summary>
    public BattleOptions Options { get; }

    /// <summary>
    /// The one source of randomness in this battle, seeded from the
    /// constructor's seed.
    ///
    /// Everything that needs a roll is handed this instance explicitly. Nothing
    /// in the simulation may construct its own generator or reach for a shared
    /// one — a second stream, however well seeded, makes the battle
    /// unreplayable, because the order the two are drawn from stops being part
    /// of the recorded state.
    /// </summary>
    public Rng Rng { get; }

    /// <summary>
    /// Ticks completed so far. Zero before <see cref="Run"/>, and the battle's
    /// terminating tick once it has finished.
    /// </summary>
    public int Tick { get; private set; }

    /// <summary>How the battle ended, or null while it has not been run.</summary>
    public BattleResult? Result { get; private set; }

    /// <summary>
    /// The units that took a turn in the most recently completed tick, in the
    /// order they decided in. Empty until the first tick has run.
    ///
    /// This is what makes "each tick, units decide in the setup's order" a
    /// property a test can fail on. It is the decide order specifically, and
    /// that is the one that matters: it fixes the sequence of random draws, and
    /// it settles which of two units reaching for the same tile gets it.
    /// Asserting on <see cref="LivingUnits"/> alone tests the helper that
    /// produces the order, not the loop that consumes it.
    ///
    /// <para>
    /// A copy, because the backing list is cleared and refilled every tick: a
    /// caller handed the live list would watch it empty underneath them the
    /// moment the next tick began. Not reachable while <see cref="Run"/> is
    /// atomic, but it is exactly the trap a per-tick stepping entry point would
    /// spring, and a copy of at most a board's worth of units is not worth
    /// economising on.
    /// </para>
    /// </summary>
    public IReadOnlyList<BattleUnit> TurnsTakenLastTick => [.. _turnsThisTick];

    /// <summary>
    /// Every unit still alive, in the order they act.
    ///
    /// The order is <see cref="BattleSetup.Units"/>' order — side, then
    /// front-to-back within the army, then column, fixed when the setup was
    /// built — filtered, never re-sorted.
    /// Two properties follow from that, and both are load-bearing: it is a
    /// total order, so no two units can tie and fall back on something
    /// incidental; and it does not change as units move, so a battle cannot be
    /// altered by a unit walking into a different sort position mid-fight.
    ///
    /// Built from a <see cref="List{T}"/> and never from a
    /// <see cref="HashSet{T}"/> or a <see cref="Dictionary{TKey, TValue}"/>,
    /// whose enumeration order .NET does not guarantee and has changed between
    /// runtimes.
    /// </summary>
    public IReadOnlyList<BattleUnit> LivingUnits()
    {
        var living = new List<BattleUnit>(Setup.Units.Count);

        foreach (BattleUnit unit in Setup.Units)
        {
            if (unit.IsAlive)
            {
                living.Add(unit);
            }
        }

        return living;
    }

    /// <summary>
    /// Runs the battle to completion and returns how it ended.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This simulation has already been run. Re-running would resume a finished
    /// battle from its own corpses rather than replay it, so it is a programmer
    /// error rather than a supported operation.
    /// </exception>
    public BattleResult Run()
    {
        if (Result is BattleResult finished)
        {
            throw new InvalidOperationException(
                $"This battle has already been run: it ended at tick {finished.EndTick}. " +
                "Build a fresh BattleSetup and BattleSimulation to run it again.");
        }

        while (true)
        {
            // Checked before the cap, so a battle decided on its very last tick
            // is reported as an elimination rather than as a draw.
            if (Decided() is BattleResult decided)
            {
                Result = decided;
                return decided;
            }

            if (Tick >= Options.MaxTicks)
            {
                var drawn = new BattleResult(BattleOutcome.Draw, BattleEndReason.TickCap, Tick);
                Result = drawn;
                return drawn;
            }

            Tick++;
            RunTick();
        }
    }

    /// <summary>
    /// One tick, in two phases: every living unit decides what to do against
    /// the board as it stood at the start of the tick, and only then is every
    /// decision applied.
    ///
    /// <para>
    /// The two phases are the whole point, and they are binding for stories 1.5
    /// and 1.6. Resolving unit by unit would let the second unit see the first
    /// one's move, which makes turn order decide contested tiles and who lands a
    /// killing blow — and turn order starts with <c>BattleSide.Player = 0</c>,
    /// so that would be a permanent advantage to one army hidden in an enum's
    /// numbering. Split in two, order survives only as a tie break for two units
    /// reaching for the same tile, never as "who strikes first".
    /// </para>
    ///
    /// <para>
    /// Nothing here may be parallelised, however independent two units look. The
    /// decide phase runs in <see cref="BattleSetup.Units"/> order because that
    /// order is what fixes the sequence of random draws; the apply phase runs in
    /// it because that is what settles contested tiles.
    /// </para>
    ///
    /// <para>
    /// The acting set is fixed at the top of the tick, not re-checked as it
    /// runs: <see cref="LivingUnits"/> is materialised once, so from story 1.5 a
    /// unit killed in this same tick still gets to decide and to act. A unit is
    /// alive for the whole of the tick it dies in, and trading blows stays
    /// symmetrical.
    /// </para>
    /// </summary>
    private void RunTick()
    {
        _turnsThisTick.Clear();

        IReadOnlyList<BattleUnit> acting = LivingUnits();

        // The clock advances before anyone decides, so readiness is a property
        // of the tick a unit is deciding in rather than of the one before it.
        // Only an actual step resets a cooldown, down in the apply phase — which
        // is what lets a blocked unit keep the readiness it has accumulated.
        foreach (BattleUnit unit in acting)
        {
            if (unit.MovementCooldownTicks > 0)
            {
                unit.MovementCooldownTicks--;
            }
        }

        var decisions = new List<UnitDecision>(acting.Count);

        foreach (BattleUnit unit in acting)
        {
            _turnsThisTick.Add(unit);
            decisions.Add(Decide(unit, Rng));
        }

        Apply(Setup.Battlefield, decisions);
    }

    /// <summary>
    /// One unit's choice for this tick, read off the board as it stood before
    /// anything in this tick moved.
    ///
    /// This is the only phase that may draw from <paramref name="rng"/>, and the
    /// explicit parameter is where those draws are meant to come from. Because it
    /// runs in <see cref="BattleSetup.Units"/> order and <see cref="Apply"/>
    /// genuinely cannot draw — it is static, so there is no <c>this.Rng</c> for it
    /// to reach — the sequence of draws in a battle is a function of that order
    /// alone, and not of which units happened to end up moving. Story 1.4 spends
    /// nothing: targeting and stepping are both settled by documented tie breaks
    /// rather than by rolls.
    ///
    /// <para>
    /// The parameter is a signpost rather than a fence: this method is an
    /// instance method, so <see cref="Rng"/> is in scope whatever its signature
    /// says. What actually holds the rule is
    /// <c>Targeting_and_movement_spend_no_randomness</c> and
    /// <c>The_loop_itself_consumes_no_randomness</c>, which compare the
    /// generator's state after a battle against a freshly seeded one.
    /// </para>
    ///
    /// <para>
    /// It does write one thing — <see cref="BattleUnit.Target"/>, on the unit
    /// deciding. That is safe precisely because no unit ever reads another
    /// unit's target, so it cannot leak one unit's decision into another's.
    /// Position is the state that must not move mid-phase, and it does not.
    /// </para>
    /// </summary>
    private UnitDecision Decide(BattleUnit unit, Rng rng)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(rng);

        // Sticky: re-acquired only when there is no target or it is dead, never
        // because something nearer wandered past.
        if (unit.Target is null || !unit.Target.IsAlive)
        {
            unit.Target = NearestEnemy(unit);
        }

        if (unit.Target is not BattleUnit target)
        {
            // Nothing left to fight. The battle is already decided; this is just
            // the tick that has not noticed yet.
            return new UnitDecision(unit, null);
        }

        // In range: hold position. Story 1.5 hangs the swing here.
        if (unit.Position.DistanceTo(target.Position) <= unit.Definition.AttackRangeHexes)
        {
            return new UnitDecision(unit, null);
        }

        if (unit.MovementCooldownTicks > 0)
        {
            return new UnitDecision(unit, null);
        }

        return new UnitDecision(unit, StepToward(unit, target));
    }

    /// <summary>
    /// Applies every decision, in the order they were decided.
    ///
    /// Static, and that is the point rather than a tidiness choice: a static
    /// method has no <c>this</c>, so it cannot reach <see cref="Rng"/> however
    /// much a future author would like it to. Applying a decision must never
    /// draw — a draw here would make the sequence of random values depend on
    /// which units happened to end up moving, rather than on
    /// <see cref="BattleSetup.Units"/> order alone, and two replays of one seed
    /// would diverge the first time a tile was contested. Hence the battlefield
    /// arriving as a parameter: it is the only state applying needs.
    ///
    /// It is also the only place a movement cooldown is reset, so a unit that
    /// decided to move and then lost the tile keeps its readiness and goes as
    /// soon as the tile frees.
    /// </summary>
    private static void Apply(Battlefield field, IReadOnlyList<UnitDecision> decisions)
    {
        // The tiles taken this tick. A set for lookup only — it is never
        // enumerated, so its order cannot reach an outcome.
        var claimed = new HashSet<HexCoord>();

        foreach (UnitDecision decision in decisions)
        {
            if (decision.MoveTo is not HexCoord destination)
            {
                continue;
            }

            // Two units wanted the same empty tile. The one earlier in
            // BattleSetup.Units order has already taken it, and this one stays
            // put for the tick rather than re-pathing — re-pathing can cascade
            // into a second contested tile and turns a tick into constraint
            // solving.
            if (!claimed.Add(destination))
            {
                continue;
            }

            // Cannot throw: the destination was empty when this unit decided,
            // nothing may move into a tile vacated during the tick, and the
            // claim above rules out two units taking the same one.
            field.Move(decision.Unit, destination);
            decision.Unit.MovementCooldownTicks = decision.Unit.Definition.MovementTicksPerHex;
        }
    }

    /// <summary>
    /// The closest living enemy, or null once that side has been wiped out.
    ///
    /// Ties break on the lowest row, then the lowest column — a documented total
    /// order over distinct tiles, never a roll. Two units cannot share a tile, so
    /// it always resolves, and it resolves the same way on every machine.
    /// </summary>
    private BattleUnit? NearestEnemy(BattleUnit unit)
    {
        BattleUnit? best = null;
        int bestDistance = 0;

        foreach (BattleUnit candidate in Setup.Units)
        {
            if (candidate.Side == unit.Side || !candidate.IsAlive)
            {
                continue;
            }

            int distance = unit.Position.DistanceTo(candidate.Position);

            if (best is null || Beats(distance, candidate.Position, bestDistance, best.Position))
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;

        static bool Beats(int distance, HexCoord at, int bestDistance, HexCoord bestAt)
        {
            if (distance != bestDistance)
            {
                return distance < bestDistance;
            }

            if (at.Row != bestAt.Row)
            {
                return at.Row < bestAt.Row;
            }

            return at.Col < bestAt.Col;
        }
    }

    /// <summary>
    /// The hex this unit should step onto to close on its target, or null if
    /// every step that would help is blocked or off the board.
    ///
    /// Greedy and deliberately not a pathfinder: only neighbours strictly closer
    /// to the target are considered at all. On an open 8x8 field with no terrain
    /// that is sufficient, it is far easier to keep deterministic than A*, and it
    /// removes the shuffling-on-the-spot failure mode without needing a guard for
    /// it: a step is only ever taken when it closes the distance, so a unit can
    /// never trade a tile for one no better than it. The cost is that a unit
    /// stuck behind a wall of its own army simply waits; if that shows up in
    /// playtesting it is a follow-up story, not a reason to build a pathfinder
    /// now.
    ///
    /// <para>
    /// Note the limit of that: the step is closer to where the target stood
    /// <em>this tick</em>. Targets move, so a unit's distance to its own target
    /// can still rise between ticks, and it can revisit a tile it has left —
    /// two units converging from different directions is enough. Story 1.5 makes
    /// that sharper, because death re-points a unit at a new enemy
    /// discontinuously. So this is not an unconditional no-oscillation proof, and
    /// nobody should read it as one when deciding whether 1.5 needs a guard.
    /// </para>
    ///
    /// <para>
    /// A pointy-top grid has no due-north neighbour, so a unit closing vertical
    /// distance always has two equally good steps and always needs a tie break.
    /// It is: the smaller column gap to the target, then the lowest row, then the
    /// lowest column — a total order over distinct tiles, and no RNG. The column
    /// gap leads because without it the lowest-column rule alone makes a unit
    /// drift steadily west as it zigzags, sidestepping away from a target to its
    /// east for the whole advance. Led by the gap, the zigzag stays centred on
    /// the target's column, which is both what an advance should look like and
    /// what the replay viewer will have to draw.
    /// </para>
    /// </summary>
    private HexCoord? StepToward(BattleUnit unit, BattleUnit target)
    {
        HexCoord goal = target.Position;
        int currentDistance = unit.Position.DistanceTo(goal);

        HexCoord? best = null;
        int bestDistance = 0;

        foreach (HexCoord step in unit.Position.Neighbours())
        {
            // Passability covers bounds and occupancy together, and it is read
            // here in the decide phase — so "empty" means empty at the start of
            // the tick, not empty by the time anything moves. A unit may never
            // follow another into a tile vacated during the same tick: allowing
            // it means resolving chains of moves, which is sequential resolution
            // wearing a disguise.
            if (!Setup.Battlefield.IsPassable(step))
            {
                continue;
            }

            int distance = step.DistanceTo(goal);
            if (distance >= currentDistance)
            {
                continue;
            }

            if (best is null || Beats(step, distance, best.Value, bestDistance, goal))
            {
                best = step;
                bestDistance = distance;
            }
        }

        return best;

        static bool Beats(HexCoord step, int distance, HexCoord best, int bestDistance, HexCoord goal)
        {
            if (distance != bestDistance)
            {
                return distance < bestDistance;
            }

            int gap = Math.Abs(step.Col - goal.Col);
            int bestGap = Math.Abs(best.Col - goal.Col);
            if (gap != bestGap)
            {
                return gap < bestGap;
            }

            if (step.Row != best.Row)
            {
                return step.Row < best.Row;
            }

            return step.Col < best.Col;
        }
    }

    /// <summary>
    /// The result if one side has been wiped out, or null while both are still
    /// standing.
    /// </summary>
    private BattleResult? Decided()
    {
        bool playerAlive = HasLivingUnits(BattleSide.Player);
        bool opponentAlive = HasLivingUnits(BattleSide.Opponent);

        if (playerAlive && opponentAlive)
        {
            return null;
        }

        // Both sides empty is a genuine draw, not a bug: a mutual kill on the
        // same tick has to resolve somehow, and awarding it to whichever side
        // the loop happened to reach first would make the outcome depend on
        // iteration order.
        BattleOutcome outcome = (playerAlive, opponentAlive) switch
        {
            (true, false) => BattleOutcome.PlayerWin,
            (false, true) => BattleOutcome.OpponentWin,
            _ => BattleOutcome.Draw,
        };

        return new BattleResult(outcome, BattleEndReason.Elimination, Tick);
    }

    private bool HasLivingUnits(BattleSide side)
    {
        foreach (BattleUnit unit in Setup.Units)
        {
            if (unit.Side == side && unit.IsAlive)
            {
                return true;
            }
        }

        return false;
    }
}
