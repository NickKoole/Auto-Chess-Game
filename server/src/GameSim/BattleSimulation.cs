namespace GameSim;

/// <summary>
/// Runs one battle from one setup and one seed, and reports how it ended.
/// A simulation is single-use: it mutates the <see cref="BattleUnit"/>
/// instances its <see cref="BattleSetup"/> holds, so build a fresh
/// <see cref="BattleSetup"/> for each run rather than sharing one.
/// </summary>
public sealed class BattleSimulation
{
    // Real step counts never approach the board's 96 tiles, so this can't
    // collide with one.
    private const int Unreachable = int.MaxValue;

    // Cleared and refilled every tick; only the most recently completed
    // tick's decide order is kept.
    private readonly List<BattleUnit> _turnsThisTick = [];

    // Append-only, so unlike _turnsThisTick, Events can hand out this list
    // directly instead of a defensive copy.
    private readonly List<BattleEvent> _events = [];

    /// <summary>
    /// Prepares a battle. Nothing runs until <see cref="Run"/> is called.
    /// </summary>
    /// <param name="setup">The starting position. Already validated by construction.</param>
    /// <param name="seed">
    /// Seeds this battle's <see cref="GameSim.Rng"/>; the same seed and setup
    /// must always produce the same result.
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

    public BattleOptions Options { get; }

    /// <summary>
    /// The only randomness source in this battle. Never construct a second
    /// generator, however well seeded - a second stream makes the battle
    /// unreplayable.
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
    /// order they decided in. Empty until the first tick has run. A copy: the
    /// backing list is cleared and refilled every tick, so a live reference
    /// would go empty underneath the caller.
    /// </summary>
    public IReadOnlyList<BattleUnit> TurnsTakenLastTick => [.. _turnsThisTick];

    /// <summary>
    /// Everything that has happened in this battle so far, in order: every
    /// unit's starting state at tick 0, then each tick's moves, attacks,
    /// damage and deaths. Empty until <see cref="Run"/> is called.
    /// </summary>
    public IReadOnlyList<BattleEvent> Events => _events;

    /// <summary>
    /// Every unit still alive, filtered from <see cref="BattleSetup.Units"/>
    /// in that order and never re-sorted - a fixed total order, so it can't
    /// tie and doesn't change as units move.
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
    /// This simulation has already been run.
    /// </exception>
    public BattleResult Run()
    {
        if (Result is BattleResult finished)
        {
            throw new InvalidOperationException(
                $"This battle has already been run: it ended at tick {finished.EndTick}. " +
                "Build a fresh BattleSetup and BattleSimulation to run it again.");
        }

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
            // Checked before the cap, so a battle decided on its very last
            // tick is reported as an elimination rather than a draw.
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
    /// One tick: every living unit decides against the board as it stood at
    /// the tick's start, then every decision applies - never unit by unit, or
    /// turn order would decide contested tiles and who lands a killing blow.
    /// The acting set is fixed at the start of the tick, so a unit killed
    /// this tick still gets to act.
    /// </summary>
    private void RunTick()
    {
        _turnsThisTick.Clear();

        IReadOnlyList<BattleUnit> acting = LivingUnits();

        // Only an actual step or swing resets a cooldown, in the apply phase
        // below - a blocked or waiting unit keeps the readiness it accrued.
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

        // A separate pass after every decision has applied: two attackers can
        // jointly kill a target that either hit alone would not, so a death
        // is only knowable once the tick's damage has fully landed.
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
    /// One unit's choice for this tick, decided against the board as it stood
    /// before this tick moved anything. The only phase that may draw from
    /// <paramref name="rng"/>. Writes <see cref="BattleUnit.Target"/> on the
    /// deciding unit only - safe because no unit ever reads another's target.
    /// </summary>
    private UnitDecision Decide(BattleUnit unit, Rng rng)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(rng);

        unit.Target = ChooseTarget(unit);

        if (unit.Target is not BattleUnit target)
        {
            return new UnitDecision(unit, null, null);
        }

        // Ranged and melee are not different cases here - there is no
        // projectile travel time, so a hit this tick lands this tick
        // regardless of AttackRangeHexes.
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
    /// One landed hit's damage: <see cref="UnitDefinition.AttackDamage"/>
    /// scaled by a variance rolled uniformly from <paramref name="rng"/>,
    /// floored to an integer. The only place damage is rolled, called from
    /// exactly one branch of <see cref="Decide"/> with no early return
    /// between the call and the roll - every landed hit draws exactly once.
    /// Variance is an integer basis-point offset, never a
    /// <see langword="double"/> multiplier: a double multiply-then-floor is
    /// not guaranteed bit-identical across runtimes.
    /// </summary>
    private static int RollDamage(UnitDefinition definition, Rng rng)
    {
        const int BasisPointScale = 10_000;

        int varianceBasisPoints = (int)Math.Round(
            definition.DamageVariance * BasisPointScale,
            MidpointRounding.AwayFromZero);

        // Uniform over [-varianceBasisPoints, +varianceBasisPoints]. Valid
        // even at zero variance: NextInt(0, 1) always returns 0.
        int rolledOffset = rng.NextInt(-varianceBasisPoints, varianceBasisPoints + 1);

        // DamageVariance is capped at 1.0, so varianceBasisPoints never
        // exceeds BasisPointScale and this is never negative.
        long multiplier = BasisPointScale + rolledOffset;

        return (int)(definition.AttackDamage * multiplier / BasisPointScale);
    }

    /// <summary>
    /// Applies every decision, in the order they were decided. Static, so it
    /// can never draw from <see cref="Rng"/> - a decision's damage arrives
    /// pre-rolled on <see cref="UnitDecision.Attack"/>. The only place a
    /// movement or attack cooldown resets, so a blocked move or a swing whose
    /// target died first keeps its readiness. Damage is summed with no
    /// ordering or alive check, so two attackers can jointly kill - or trade
    /// kills with - a shared target on the same tick. Also the only place the
    /// defender's resistance mitigates a hit, via <see cref="ApplyResistance"/>
    /// - <see cref="UnitDecision.Attack"/> still carries the raw, pre-mitigation
    /// roll.
    /// </summary>
    private static List<BattleEvent> Apply(Battlefield field, IReadOnlyList<UnitDecision> decisions, int tick)
    {
        var events = new List<BattleEvent>();

        // Lookup only - never enumerated, so this set's order can't reach an
        // outcome.
        var claimed = new HashSet<HexCoord>();

        foreach (UnitDecision decision in decisions)
        {
            if (decision.MoveTo is HexCoord destination)
            {
                // Two units wanted the same tile: the one earlier in
                // BattleSetup.Units order takes it, the other stays put
                // rather than re-pathing, which could cascade into a second
                // contested tile.
                if (claimed.Add(destination))
                {
                    HexCoord origin = decision.Unit.Position;

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

                int appliedDamage = ApplyResistance(attack.Damage, decision.Unit.Definition, attack.Target.Definition);

                attack.Target.CurrentHp -= appliedDamage;
                decision.Unit.AttackCooldownTicks = decision.Unit.Definition.AttackIntervalTicks;

                events.Add(new DamageDealtEvent
                {
                    Tick = tick,
                    AttackerId = decision.Unit.Id,
                    TargetId = attack.Target.Id,
                    Amount = appliedDamage,
                    ResultingHp = attack.Target.CurrentHp,
                });
            }
        }

        return events;
    }

    /// <summary>
    /// Mitigates a raw hit by the defender's resistance matching the
    /// attacker's <see cref="DamageType"/>: plain integer division, no
    /// floats, deterministic. A resist stat is capped at 75% here, at
    /// formula-evaluation time rather than by restricting the stored value -
    /// a field can hold more than 75 for a future "nearly immune" unit, this
    /// is just the one place that never lets it block more than 75%.
    /// </summary>
    private static int ApplyResistance(int rawDamage, UnitDefinition attacker, UnitDefinition defender)
    {
        const int ResistCap = 75;

        int resist = Math.Min(defender.ResistFor(attacker.DamageType), ResistCap);
        return rawDamage * (100 - resist) / 100;
    }

    /// <summary>
    /// The enemy this unit fights this tick: the nearest living one, unless
    /// its current target is exactly as close. Re-evaluated every tick rather
    /// than held until death, since range is tested against the target alone
    /// and a stale target could leave a unit beside a living enemy it won't
    /// swing at. Ties favour the incumbent, so a unit can't oscillate between
    /// two equidistant enemies.
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
    /// Ties break on the lowest row, then the lowest column - a total order,
    /// never a roll.
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
    /// The hex this unit should step onto to close on <paramref name="target"/>,
    /// or null if nothing reachable shortens the route. Routes around
    /// occupied tiles, allies included, rather than waiting on a blocked
    /// front rank. Ties break on the smallest row gap to the target, then
    /// the lowest row, then the lowest column - row gap first, so the
    /// offset-grid diagonal routes stay centred on the target's row
    /// instead of drifting. The route targets where the enemy stood this
    /// tick only, so a unit can revisit a tile it left as the target moves.
    /// </summary>
    private HexCoord? StepToward(BattleUnit unit, BattleUnit target)
    {
        int[] remaining = StepsToAttackPosition(unit, target);

        int here = remaining[Tile(unit.Position)];
        if (here == Unreachable)
        {
            return null;
        }

        HexCoord goal = target.Position;

        HexCoord? best = null;
        int bestSteps = 0;

        foreach (HexCoord step in unit.Position.Neighbours())
        {
            // Passability is read here, in the decide phase, so "empty"
            // means empty at the start of the tick - a unit may never follow
            // another into a tile vacated during the same tick.
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

            int gap = Math.Abs(step.Row - goal.Row);
            int bestGap = Math.Abs(best.Row - goal.Row);
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
    /// For every tile, how many steps this unit needs to reach somewhere it
    /// could stand and attack <paramref name="target"/> from, or
    /// <see cref="Unreachable"/> if no route exists. A breadth-first sweep
    /// backwards from the tiles already in range, so one sweep answers for
    /// the whole board. Occupied tiles are walls except the unit's own tile,
    /// so no route passes through an ally.
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
    /// The result if one side has been wiped out, or null while both are
    /// still standing.
    /// </summary>
    private BattleResult? Decided()
    {
        bool playerAlive = HasLivingUnits(BattleSide.Player);
        bool opponentAlive = HasLivingUnits(BattleSide.Opponent);

        if (playerAlive && opponentAlive)
        {
            return null;
        }

        // Both sides empty is a genuine draw, not a bug: awarding a mutual
        // kill to whichever side the loop reached first would make the
        // outcome depend on iteration order.
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
