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
internal readonly record struct UnitDecision(BattleUnit Unit, HexCoord? MoveTo);
