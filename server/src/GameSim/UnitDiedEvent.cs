namespace GameSim;

/// <summary>
/// A unit was removed from the battle.
///
/// Emitted after every decision in the tick has applied, never inside the
/// same pass that deals damage — two attackers can jointly kill a unit that
/// either hit alone would not, so whether a unit is dead can only be known
/// once the whole tick's damage has landed.
/// </summary>
public sealed record UnitDiedEvent : BattleEvent
{
    /// <summary>The unit that died.</summary>
    public required int UnitId { get; init; }

    /// <summary>Where it fell.</summary>
    public required HexCoord Position { get; init; }
}
