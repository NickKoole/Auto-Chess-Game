namespace GameSim;

/// <summary>Emitted only after all of a tick's damage has applied - see <see cref="BattleSimulation"/>'s tick loop.</summary>
public sealed record UnitDiedEvent : BattleEvent
{
    public required int UnitId { get; init; }

    public required HexCoord Position { get; init; }
}
