namespace GameSim;

/// <summary>
/// What one unit chose to do in one tick, decided against the board as it stood
/// at the start of that tick and carried unapplied until every other unit has
/// decided too.
///
/// This type exists so that deciding and acting cannot be the same statement.
/// A tick that resolved unit by unit would let the second unit see the first
/// one's move, which makes turn order — and so the enum value of
/// <see cref="BattleSide"/> — decide contested tiles and, from story 1.5, who
/// strikes first. Holding the choice as data for the length of a phase is what
/// keeps a tick simultaneous.
///
/// Internal on purpose. Story 1.6 owns the shape of the battle event log, which
/// is the public account of what happened in a tick; this is only the plumbing
/// between two phases, and publishing it now would freeze a shape that story
/// has every right to choose differently.
/// </summary>
/// <param name="Unit">The unit that decided. Never null.</param>
/// <param name="MoveTo">
/// The hex it wants to step onto, or null to stay put — because it is already in
/// range, because its movement cooldown has not elapsed, because every step that
/// would close the distance is blocked, or because there is nothing left to
/// fight. Those are one outcome as far as applying it goes, and telling them
/// apart is the event log's job, not this type's.
/// </param>
/// <param name="Attack">
/// The hit this unit lands this tick, or null if it does not attack — because it
/// is out of range, its attack cooldown has not elapsed, or it has nothing left
/// to fight. The damage is rolled here, in the decide phase, because that is the
/// only phase with access to the battle's <see cref="Rng"/>; carrying the rolled
/// value forward is what lets the static apply phase commit it without ever
/// touching the generator itself.
/// </param>
internal readonly record struct UnitDecision(BattleUnit Unit, HexCoord? MoveTo, AttackDecision? Attack);

/// <summary>
/// One landed hit, decided but not yet applied: who it lands on and how much it
/// deals, already rolled.
/// </summary>
/// <param name="Target">The unit being hit. Never null.</param>
/// <param name="Damage">
/// Hit points to remove, already floored to an integer. Rolled once, from the
/// battle's single <see cref="Rng"/>, at the moment the hit was decided to land —
/// never re-rolled or re-derived when this is applied.
/// </param>
internal readonly record struct AttackDecision(BattleUnit Target, int Damage);
