namespace GameSim;

/// <summary>
/// A snapshot of an <see cref="Rng"/>'s state. Plain data, so it can live in
/// save state and be restored to resume a run mid-stream.
/// </summary>
public readonly record struct RngState(ulong S0, ulong S1, ulong S2, ulong S3);

/// <summary>
/// Deterministic pseudo-random number generator (xoshiro256**, seeded via
/// splitmix64).
///
/// This is the ONLY source of randomness permitted in the simulation. Thread an
/// instance through explicitly — never reach for <see cref="System.Random"/>,
/// <c>Random.Shared</c>, or <see cref="System.Guid.NewGuid"/>, all of which
/// make a battle unreplayable.
/// </summary>
public sealed class Rng
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    /// <summary>Creates a generator from a seed. The same seed always yields the same sequence.</summary>
    public Rng(ulong seed)
    {
        // splitmix64 expands a single seed into four well-distributed words.
        // Seeding xoshiro directly from a small integer produces a poor first
        // few outputs, which matters when a battle is short.
        _s0 = SplitMix64(ref seed);
        _s1 = SplitMix64(ref seed);
        _s2 = SplitMix64(ref seed);
        _s3 = SplitMix64(ref seed);
    }

    /// <summary>Restores a generator from a previously captured state.</summary>
    public Rng(RngState state)
    {
        _s0 = state.S0;
        _s1 = state.S1;
        _s2 = state.S2;
        _s3 = state.S3;
    }

    /// <summary>Captures the current state so the stream can be resumed later.</summary>
    public RngState Snapshot() => new(_s0, _s1, _s2, _s3);

    /// <summary>Next raw 64-bit value.</summary>
    public ulong NextUInt64()
    {
        ulong result = Rotl(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = Rotl(_s3, 45);

        return result;
    }

    /// <summary>
    /// Uniform integer in <paramref name="minInclusive"/>..<paramref name="maxExclusive"/>.
    /// Rejection sampling, so the result is unbiased — a modulo would skew low
    /// values, which quietly distorts drop tables and combat rolls.
    /// </summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                $"maxExclusive ({maxExclusive}) must be greater than minInclusive ({minInclusive}).");
        }

        ulong range = (ulong)((long)maxExclusive - minInclusive);
        ulong limit = ulong.MaxValue - (ulong.MaxValue % range) - 1;

        ulong value;
        do
        {
            value = NextUInt64();
        }
        while (value > limit);

        return (int)((long)minInclusive + (long)(value % range));
    }

    /// <summary>Uniform boolean.</summary>
    public bool NextBool() => (NextUInt64() & 1UL) != 0UL;

    /// <summary>Uniformly picks one element. Throws on an empty list.</summary>
    public T Pick<T>(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            throw new ArgumentException("Cannot pick from an empty list.", nameof(items));
        }

        return items[NextInt(0, items.Count)];
    }

    /// <summary>
    /// Shuffles in place (Fisher-Yates). Deterministic for a given state, unlike
    /// sorting by a random key.
    /// </summary>
    public void Shuffle<T>(IList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = NextInt(0, i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

    private static ulong SplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
