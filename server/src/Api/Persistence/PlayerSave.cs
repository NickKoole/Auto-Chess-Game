namespace Api.Persistence;

/// <summary>
/// A player's persisted run: an opaque serialized blob owned by the
/// simulation, plus a <see cref="LastTickProcessed"/> cursor. Offline progress
/// is "advance from this tick to the current server tick", never "trust how
/// much time the client says has passed".
/// </summary>
public sealed class PlayerSave
{
    public Guid Id { get; set; }

    public required string PlayerName { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset LastAdvancedAtUtc { get; set; }

    public long LastTickProcessed { get; set; }

    /// <summary>Opaque to the API layer - only <c>GameSim</c> deserializes it.</summary>
    public required string StateJson { get; set; }
}
