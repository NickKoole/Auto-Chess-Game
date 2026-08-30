namespace Api.Persistence;

/// <summary>
/// A player's persisted run.
///
/// Placeholder shape — the first real gameplay ticket will flesh this out. What
/// matters architecturally is already here: the save is an opaque serialized
/// blob owned by the simulation, plus a <see cref="LastTickProcessed"/> cursor.
/// Offline progress is "advance from this tick to the current server tick",
/// never "trust how much time the client says has passed".
/// </summary>
public sealed class PlayerSave
{
    public Guid Id { get; set; }

    public required string PlayerName { get; set; }

    /// <summary>Server clock, at creation. Never client-supplied.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Server clock, when the save was last advanced.</summary>
    public DateTimeOffset LastAdvancedAtUtc { get; set; }

    /// <summary>Simulation tick the save has been advanced to.</summary>
    public long LastTickProcessed { get; set; }

    /// <summary>Serialized <c>GameSim</c> state. Opaque to the API layer.</summary>
    public required string StateJson { get; set; }
}
