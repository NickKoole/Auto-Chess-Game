namespace GameSim;

/// <summary>
/// A unit stepped from one hex to an adjacent one.
///
/// Emitted only for a step that actually happened — a unit that decided to
/// move but lost the tile to a faster claim, or had nowhere to go, produces
/// nothing here. See <see cref="BattleSimulation"/>'s apply phase for why a
/// decision to move is not the same thing as a move.
/// </summary>
public sealed record UnitMovedEvent : BattleEvent
{
    /// <summary>The unit that moved.</summary>
    public required int UnitId { get; init; }

    /// <summary>Where it stood before this step.</summary>
    public required HexCoord From { get; init; }

    /// <summary>Where it stands after this step.</summary>
    public required HexCoord To { get; init; }
}
