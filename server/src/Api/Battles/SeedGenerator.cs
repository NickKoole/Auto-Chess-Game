namespace Api.Battles;

/// <summary>
/// Generates a seed when a caller does not supply one.
///
/// Deliberately outside <c>GameSim</c>: the sim takes a seed and never invents
/// one, so this lives at the API boundary, the one place a battle's identity is
/// actually decided.
/// </summary>
public static class SeedGenerator
{
    /// <summary>A seed drawn from the full range of <see langword="ulong"/>.</summary>
    public static ulong NewSeed() => unchecked((ulong)Random.Shared.NextInt64(long.MinValue, long.MaxValue));
}
