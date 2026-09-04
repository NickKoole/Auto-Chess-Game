namespace GameSim;

/// <summary>Only for a step that actually happened - a decision to move that lost the tile or had nowhere to go produces nothing here.</summary>
public sealed record UnitMovedEvent : BattleEvent
{
    public required int UnitId { get; init; }

    public required HexCoord From { get; init; }

    public required HexCoord To { get; init; }
}
