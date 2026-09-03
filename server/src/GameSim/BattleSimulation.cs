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
    // No route to anywhere this unit could attack from. Distinct from a large
    // step count rather than a sentinel that arithmetic could stumble into: the
    // board is 64 tiles, so a real count never approaches it.
    private const int Unreachable = int.MaxValue;

    // The units that decided in the tick currently running, cleared at the top
    // of each one. Only the latest tick is kept: this exists so the loop's
    // decide order is observable, and a battle-long history would be a
    // thousand-entry event log built for a test — which is story 1.6's job,
    // done properly.
    private readonly List<BattleUnit> _turnsThisTick = [];

    // Append-only for the life of the battle, unlike _turnsThisTick above: an
    // event once written describes something that already happened and is
    // never revised, so — unlike TurnsTakenLastTick — Events hands out this
    // same list rather than a defensive copy.
    private readonly List<BattleEvent> _events = [];

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
    /// Everything that has happened in this battle so far, in the order it
    /// happened: every unit's starting state at tick 0, then each tick's moves,
    /// attacks, damage and deaths. Empty until <see cref="Run"/> is called.
    ///
    /// This is the log story 1.6 exists to build: a client reconstructs a whole
    /// battle from this list alone — plus a static catalog to turn a
    /// <see cref="UnitSpawnedEvent.DefinitionId"/> back into stats — without ever
    /// running the simulation itself.
    /// </summary>
    public IReadOnlyList<BattleEvent> Events => _events;

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

        // Tick 0: the setup itself, before anything has decided or moved. Every
        // unit that starts the battle gets one of these, in Setup.Units order —
        // the same total order everything else in this class is keyed to.
        foreach (BattleUnit unit in Setup.Units)
        {
            _events.Add(new UnitSpawnedEvent
            {
                Tick = 0,
                UnitId = unit.Id,
                Side = unit.Side,
                DefinitionId = unit.Definition.Id,
                Position = unit.Position,
            });
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
                var drawn = new BattleResult(BattleEnding.TickCapDraw, Tick);
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
    ///
    /// <para>
    /// Story 1.5 adds a third step after apply: any unit in the acting set that
    /// is no longer alive is removed from the board. It is exactly the acting
    /// set — the units alive at the top of this tick — because nothing outside
    /// this tick's apply phase can kill a unit, so a unit found dead here died in
    /// this tick and has not been removed before. That is what makes the removal
    /// safe to do unconditionally, without a "was this already handled" check.
    /// </para>
    /// </summary>
    private void RunTick()
    {
        _turnsThisTick.Clear();

        IReadOnlyList<BattleUnit> acting = LivingUnits();

        // The clock advances before anyone decides, so readiness is a property
        // of the tick a unit is deciding in rather than of the one before it.
        // Only an actual step, or an actual swing, resets a cooldown, down in the
        // apply phase — which is what lets a blocked or waiting unit keep the
        // readiness it has accumulated.
        foreach (BattleUnit unit in acting)
        {
            if (unit.MovementCooldownTicks > 0)
            {
                unit.MovementCooldownTicks--;
            }

            if (unit.AttackCooldownTicks > 0)
            {
                unit.AttackCooldownTicks--;
            }
        }

        var decisions = new List<UnitDecision>(acting.Count);

        foreach (BattleUnit unit in acting)
        {
            _turnsThisTick.Add(unit);
            decisions.Add(Decide(unit, Rng));
        }

        _events.AddRange(Apply(Setup.Battlefield, decisions, Tick));

        // A separate pass, deliberately after every decision has applied: two
        // attackers can jointly kill a target that either hit alone would not,
        // so whether a unit died this tick is only knowable once all of the
        // tick's damage has landed — not while Apply is still working through
        // decisions one at a time.
        foreach (BattleUnit unit in acting)
        {
            if (!unit.IsAlive)
            {
                HexCoord diedAt = unit.Position;
                Setup.Battlefield.Remove(unit);
                _events.Add(new UnitDiedEvent { Tick = Tick, UnitId = unit.Id, Position = diedAt });
            }
        }
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

        unit.Target = ChooseTarget(unit);

        if (unit.Target is not BattleUnit target)
        {
            // Nothing left to fight. The battle is already decided; this is just
            // the tick that has not noticed yet.
            return new UnitDecision(unit, null, null);
        }

        // In range: hold position and swing if ready. Ranged and melee are not
        // different cases here — there is no projectile travel time, so a hit
        // this tick lands this tick regardless of AttackRangeHexes.
        if (unit.Position.DistanceTo(target.Position) <= unit.Definition.AttackRangeHexes)
        {
            if (unit.AttackCooldownTicks > 0)
            {
                return new UnitDecision(unit, null, null);
            }

            int damage = RollDamage(unit.Definition, rng);
            return new UnitDecision(unit, null, new AttackDecision(target, damage));
        }

        if (unit.MovementCooldownTicks > 0)
        {
            return new UnitDecision(unit, null, null);
        }

        return new UnitDecision(unit, StepToward(unit, target), null);
    }

    /// <summary>
    /// One landed hit's damage: <see cref="UnitDefinition.AttackDamage"/> scaled
    /// by a variance rolled uniformly from <paramref name="rng"/>, floored to an
    /// integer.
    ///
    /// The only place in the simulation that rolls damage, and it is called from
    /// exactly one branch of <see cref="Decide"/> — when a hit is actually about
    /// to land. There is no early return between that call and the roll, so the
    /// draw happens on every landed hit and only on a landed hit; a second call
    /// site, or a branch that could skip the roll after deciding to attack, would
    /// let two runs of the same seed draw a different number of times and
    /// diverge from that point on.
    ///
    /// <para>
    /// The variance is rolled as an integer basis-point offset — never as a
    /// <see langword="double"/> multiplier — and the damage is derived with
    /// integer division, which truncates toward zero and so floors a
    /// non-negative result. <c>CLAUDE.md</c> requires this: a
    /// <see langword="double"/> multiply-then-floor is not guaranteed
    /// bit-identical across runtimes, and this number decides an outcome.
    /// </para>
    /// </summary>
    private static int RollDamage(UnitDefinition definition, Rng rng)
    {
        const int BasisPointScale = 10_000;

        // A fixed conversion of a catalog constant, done the same way on every
        // call — not a source of cross-run drift, unlike the roll itself.
        int varianceBasisPoints = (int)Math.Round(
            definition.DamageVariance * BasisPointScale,
            MidpointRounding.AwayFromZero);

        // Uniform over [-varianceBasisPoints, +varianceBasisPoints]. Valid even
        // at zero variance: NextInt(0, 1) always returns 0.
        int rolledOffset = rng.NextInt(-varianceBasisPoints, varianceBasisPoints + 1);

        // varianceBasisPoints never exceeds BasisPointScale (DamageVariance is
        // capped at 1.0), so this is never negative.
        long multiplier = BasisPointScale + rolledOffset;

        return (int)(definition.AttackDamage * multiplier / BasisPointScale);
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
    /// arriving as a parameter, and hence every attack's damage arriving already
    /// rolled on <see cref="UnitDecision.Attack"/>: this method only spends what
    /// <see cref="Decide"/> already drew.
    ///
    /// It is also the only place a movement or attack cooldown is reset, so a
    /// unit that decided to move and then lost the tile — or one that held its
    /// swing because its target died to someone else this same tick — keeps its
    /// readiness rather than being charged for an attempt it did not complete.
    ///
    /// <para>
    /// Damage is summed here with no regard for what order the decisions arrive
    /// in and no check that the target is still alive: two attackers each
    /// decided against the same tick-start snapshot, where their shared target
    /// was alive, so both hits are real and adding them in either order gives
    /// the same total. That is what lets two units kill a shared target — or
    /// kill each other — on the same tick.
    /// </para>
    ///
    /// <para>
    /// Returns the events this tick's moves and attacks produced, in the order
    /// <paramref name="decisions"/> is walked — <see cref="BattleSetup.Units"/>
    /// order, the same order the events within a tick are contracted to follow.
    /// A return value rather than a field this method appends to: <see cref="Apply"/>
    /// stays static and stateless, which is what keeps it provably free of any
    /// access to <see cref="Rng"/>.
    /// </para>
    /// </summary>
    private static List<BattleEvent> Apply(Battlefield field, IReadOnlyList<UnitDecision> decisions, int tick)
    {
        var events = new List<BattleEvent>();

        // The tiles taken this tick. A set for lookup only — it is never
        // enumerated, so its order cannot reach an outcome.
        var claimed = new HashSet<HexCoord>();

        foreach (UnitDecision decision in decisions)
        {
            if (decision.MoveTo is HexCoord destination)
            {
                // Two units wanted the same empty tile. The one earlier in
                // BattleSetup.Units order has already taken it, and this one
                // stays put for the tick rather than re-pathing — re-pathing can
                // cascade into a second contested tile and turns a tick into
                // constraint solving.
                if (claimed.Add(destination))
                {
                    HexCoord origin = decision.Unit.Position;

                    // Cannot throw: the destination was empty when this unit
                    // decided, nothing may move into a tile vacated during the
                    // tick, and the claim above rules out two units taking the
                    // same one.
                    field.Move(decision.Unit, destination);
                    decision.Unit.MovementCooldownTicks = decision.Unit.Definition.MovementTicksPerHex;

                    events.Add(new UnitMovedEvent
                    {
                        Tick = tick,
                        UnitId = decision.Unit.Id,
                        From = origin,
                        To = destination,
                    });
                }
            }

            if (decision.Attack is AttackDecision attack)
            {
                events.Add(new UnitAttackedEvent
                {
                    Tick = tick,
                    AttackerId = decision.Unit.Id,
                    TargetId = attack.Target.Id,
                });

                attack.Target.CurrentHp -= attack.Damage;
                decision.Unit.AttackCooldownTicks = decision.Unit.Definition.AttackIntervalTicks;

                events.Add(new DamageDealtEvent
                {
                    Tick = tick,
                    AttackerId = decision.Unit.Id,
                    TargetId = attack.Target.Id,
                    Amount = attack.Damage,
                    ResultingHp = attack.Target.CurrentHp,
                });
            }
        }

        return events;
    }

    /// <summary>
    /// The enemy this unit fights this tick: the nearest living one, unless the
    /// target it already holds is just as close.
    ///
    /// Re-evaluated every tick rather than held until it dies. A target held
    /// until death lets a unit stand beside a living enemy indefinitely, waiting
    /// on someone it cannot reach — the unit is in range of something it refuses
    /// to swing at, because range is tested against its target alone.
    ///
    /// <para>
    /// The comparison is strictly closer, never merely as close, and that is the
    /// whole of what keeps re-evaluation safe. Two equidistant enemies leave the
    /// incumbent in place, so a unit cannot alternate between them, and it goes
    /// on concentrating its damage on one rather than spreading it along a line.
    /// A unit only ever switches when the board has genuinely put something
    /// nearer in front of it.
    /// </para>
    /// </summary>
    private BattleUnit? ChooseTarget(BattleUnit unit)
    {
        BattleUnit? nearest = NearestEnemy(unit);

        if (unit.Target is not BattleUnit current || !current.IsAlive)
        {
            return nearest;
        }

        if (nearest is null)
        {
            return current;
        }

        return unit.Position.DistanceTo(nearest.Position) < unit.Position.DistanceTo(current.Position)
            ? nearest
            : current;
    }

    /// <summary>
    /// The closest living enemy, or null once that side has been wiped out.
    ///
    /// Ties break on the lowest row, then the lowest column — a documented total
    /// order over distinct tiles, never a roll. Two units cannot share a tile, so
    /// it always resolves, and it resolves the same way on every machine. That
    /// makes it a nearest-first search: walking outwards ring by ring would pick
    /// the same unit, and this needs no bound on how far to look.
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
    /// The hex this unit should step onto to close on its target, or null when
    /// nothing it can reach shortens the route.
    ///
    /// The unit walks the shortest route to somewhere it could stand and attack
    /// from, going around whatever is in the way. Occupied tiles are walls —
    /// allies as much as enemies — so a unit whose direct approach is blocked by
    /// its own front rank goes round the flank instead of waiting for the rank to
    /// die, which is what it did while this was greedy.
    ///
    /// <para>
    /// The rule is unchanged in shape: step to the best neighbour, and only to
    /// one strictly better than where you stand. Only the measure of "better" has
    /// moved, from straight-line distance to remaining steps along a real route.
    /// That matters twice over. On an open lane the two agree — remaining steps
    /// is just distance less the unit's reach — so an unobstructed advance looks
    /// exactly as it did. And "strictly fewer steps remaining" carries the same
    /// weight the old "strictly closer" did: within a tick the board is fixed and
    /// the count is a non-negative integer that must fall on every step, so a unit
    /// cannot trade tiles in a circle.
    /// </para>
    ///
    /// <para>
    /// Note the limit of that, unchanged from when this was greedy: the route is
    /// to where the target stood <em>this tick</em>. Targets move and units die,
    /// so a unit's remaining count can rise between ticks and it can revisit a
    /// tile it left. This is not an unconditional no-oscillation proof and nobody
    /// should read it as one.
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
        int[] remaining = StepsToAttackPosition(unit, target);

        int here = remaining[Tile(unit.Position)];
        if (here == Unreachable)
        {
            // Walled in with no route at all. Standing still is the answer, and
            // the unit keeps its readiness for the tick a route opens.
            return null;
        }

        HexCoord goal = target.Position;

        HexCoord? best = null;
        int bestSteps = 0;

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

            int steps = remaining[Tile(step)];
            if (steps == Unreachable || steps >= here)
            {
                continue;
            }

            if (best is null || Beats(step, steps, best.Value, bestSteps, goal))
            {
                best = step;
                bestSteps = steps;
            }
        }

        return best;

        static bool Beats(HexCoord step, int steps, HexCoord best, int bestSteps, HexCoord goal)
        {
            if (steps != bestSteps)
            {
                return steps < bestSteps;
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
    /// For every tile on the board, how many steps this unit needs to reach
    /// somewhere it could stand and attack <paramref name="target"/> from, or
    /// <see cref="Unreachable"/> where no route exists.
    ///
    /// A breadth-first sweep outwards from the tiles that are already in range,
    /// rather than a search forwards from the unit. Backwards costs one sweep and
    /// answers for the whole board at once, so the caller compares neighbours by
    /// reading an array instead of pathing from each of them — and there is no
    /// route to reconstruct, which is the part of a forward search that would
    /// need a tie break of its own.
    ///
    /// <para>
    /// Nothing here may depend on the order <see cref="HexCoord.Neighbours"/>
    /// returns, and nothing does: breadth-first distances are the same whatever
    /// order a tile's neighbours are visited in, and where two neighbours tie the
    /// caller settles it on the coordinates. That is deliberate —
    /// <see cref="HexCoord.Neighbours"/> documents that its order is arbitrary and
    /// that no caller may let it decide anything.
    /// </para>
    ///
    /// <para>
    /// Occupied tiles are walls, and the seeds are only tiles a unit could
    /// actually stand on — so a route is never traced through an ally, and the
    /// target's own tile seeds nothing. The one exception is the unit's own tile,
    /// which is occupied by the unit itself and which it is plainly able to leave.
    /// </para>
    /// </summary>
    private int[] StepsToAttackPosition(BattleUnit unit, BattleUnit target)
    {
        var remaining = new int[Battlefield.Rows * Battlefield.Columns];
        Array.Fill(remaining, Unreachable);

        var frontier = new Queue<HexCoord>();

        for (int row = 0; row < Battlefield.Rows; row++)
        {
            for (int col = 0; col < Battlefield.Columns; col++)
            {
                var hex = new HexCoord(row, col);

                if (hex.DistanceTo(target.Position) > unit.Definition.AttackRangeHexes
                    || !CanStandOn(hex, unit))
                {
                    continue;
                }

                remaining[Tile(hex)] = 0;
                frontier.Enqueue(hex);
            }
        }

        while (frontier.Count > 0)
        {
            HexCoord from = frontier.Dequeue();
            int next = remaining[Tile(from)] + 1;

            foreach (HexCoord neighbour in from.Neighbours())
            {
                if (!CanStandOn(neighbour, unit) || remaining[Tile(neighbour)] <= next)
                {
                    continue;
                }

                remaining[Tile(neighbour)] = next;
                frontier.Enqueue(neighbour);
            }
        }

        return remaining;
    }

    /// <summary>
    /// Whether this unit could occupy a tile: empty, or the one it is already
    /// standing on.
    /// </summary>
    private bool CanStandOn(HexCoord hex, BattleUnit unit) =>
        Setup.Battlefield.IsPassable(hex) || hex == unit.Position;

    private static int Tile(HexCoord hex) => (hex.Row * Battlefield.Columns) + hex.Col;

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
        BattleEnding ending = (playerAlive, opponentAlive) switch
        {
            (true, false) => BattleEnding.PlayerEliminatedOpponent,
            (false, true) => BattleEnding.OpponentEliminatedPlayer,
            _ => BattleEnding.MutualElimination,
        };

        return new BattleResult(ending, Tick);
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
