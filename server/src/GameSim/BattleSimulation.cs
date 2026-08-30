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
    // The turns taken in the tick currently running, cleared at the top of each
    // one. Only the latest tick is kept: this exists so the loop's turn order is
    // observable while units have no behaviour to observe, and a battle-long
    // history would be a thousand-entry event log built for a test — which is
    // story 1.6's job, done properly.
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
    /// order they took it. Empty until the first tick has run.
    ///
    /// This is what makes "each tick, units act in the setup's order" a
    /// property a test can fail on. Until story 1.4 gives a turn a visible
    /// effect, a loop that iterated <see cref="BattleSetup.Units"/> directly —
    /// dead units included — or iterated backwards would be indistinguishable
    /// from a correct one, because nothing a turn does is recorded anywhere
    /// else. Asserting on <see cref="LivingUnits"/> alone tests the helper that
    /// produces the order, not the loop that consumes it.
    /// </summary>
    public IReadOnlyList<BattleUnit> TurnsTakenLastTick => _turnsThisTick;

    /// <summary>
    /// Every unit still alive, in the order they act.
    ///
    /// The order is <see cref="BattleSetup.Units"/>' order — side, then row,
    /// then column, fixed when the setup was built — filtered, never re-sorted.
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
    /// One tick: every living unit takes its turn, in order.
    ///
    /// Sequential by construction. Nothing here may be parallelised, however
    /// independent two units look — the moment two turns can interleave, which
    /// one claims a contested tile stops being a property of the seed.
    ///
    /// <para>
    /// The acting set is fixed at the top of the tick, not re-checked as it
    /// runs: <see cref="LivingUnits"/> is materialised once, so from story 1.5
    /// a unit killed earlier in this same tick still takes its own turn. That
    /// is a rule, not an oversight — it means a tick resolves as a
    /// simultaneous exchange, so who struck first within it cannot decide who
    /// gets to strike at all, and trading blows stays symmetrical. The
    /// alternative (a liveness check inside the loop, letting the dead drop out
    /// mid-tick) makes turn order far more decisive and is the kind of thing
    /// that shows up as an unexplained balance bias. Whichever 1.5 settles on
    /// needs a test that can tell the two apart; nothing can die yet, so no
    /// test here distinguishes them.
    /// </para>
    /// </summary>
    private void RunTick()
    {
        _turnsThisTick.Clear();

        foreach (BattleUnit unit in LivingUnits())
        {
            _turnsThisTick.Add(unit);
            Act(unit, Rng);
        }
    }

    /// <summary>
    /// One unit's turn, and deliberately empty: targeting, movement and
    /// attacking are later stories, and a battle that runs to the tick cap
    /// because nobody can do anything is the correct outcome for this one.
    ///
    /// The signature is the point. A turn sees exactly one unit and the
    /// battle's single generator, handed to it explicitly, so the behaviour
    /// that lands here cannot quietly acquire a second source of randomness.
    /// </summary>
    private static void Act(BattleUnit unit, Rng rng)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(rng);
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
