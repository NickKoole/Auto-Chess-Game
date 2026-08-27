using GameSim;

namespace GameSim.Tests;

public sealed class RngTests
{
    [Fact]
    public void Same_seed_produces_an_identical_sequence()
    {
        var a = new Rng(12345);
        var b = new Rng(12345);

        ulong[] first = [.. Enumerable.Range(0, 500).Select(_ => a.NextUInt64())];
        ulong[] second = [.. Enumerable.Range(0, 500).Select(_ => b.NextUInt64())];

        Assert.Equal(first, second);
    }

    [Fact]
    public void Different_seeds_diverge_immediately()
    {
        var a = new Rng(1);
        var b = new Rng(2);

        // splitmix64 seeding exists precisely so that adjacent seeds do not
        // produce correlated openings.
        Assert.NotEqual(a.NextUInt64(), b.NextUInt64());
    }

    [Fact]
    public void State_can_be_snapshotted_and_resumed()
    {
        var original = new Rng(999);
        for (int i = 0; i < 10; i++)
        {
            original.NextUInt64();
        }

        RngState saved = original.Snapshot();
        ulong[] expected = [.. Enumerable.Range(0, 20).Select(_ => original.NextUInt64())];

        var resumed = new Rng(saved);
        ulong[] actual = [.. Enumerable.Range(0, 20).Select(_ => resumed.NextUInt64())];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NextInt_stays_within_bounds()
    {
        var rng = new Rng(7);

        for (int i = 0; i < 10_000; i++)
        {
            int value = rng.NextInt(3, 9);
            Assert.InRange(value, 3, 8);
        }
    }

    [Fact]
    public void NextInt_covers_every_value_in_range()
    {
        var rng = new Rng(42);
        var seen = new HashSet<int>();

        for (int i = 0; i < 1_000; i++)
        {
            seen.Add(rng.NextInt(0, 6));
        }

        Assert.Equal(6, seen.Count);
    }

    [Fact]
    public void NextInt_is_not_biased_towards_low_values()
    {
        // A naive `NextUInt64() % range` skews low. With ~120k samples across 6
        // buckets, an unbiased generator lands each within a few percent of 1/6.
        var rng = new Rng(2024);
        int[] buckets = new int[6];
        const int samples = 120_000;

        for (int i = 0; i < samples; i++)
        {
            buckets[rng.NextInt(0, 6)]++;
        }

        foreach (int count in buckets)
        {
            Assert.InRange(count, samples / 6 * 0.94, samples / 6 * 1.06);
        }
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(5, 4)]
    public void NextInt_rejects_an_empty_or_inverted_range(int min, int max)
    {
        var rng = new Rng(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(min, max));
    }

    [Fact]
    public void Shuffle_is_deterministic_for_a_given_seed()
    {
        List<int> a = [.. Enumerable.Range(0, 50)];
        List<int> b = [.. Enumerable.Range(0, 50)];

        new Rng(555).Shuffle(a);
        new Rng(555).Shuffle(b);

        Assert.Equal(a, b);
        Assert.NotEqual([.. Enumerable.Range(0, 50)], a);
    }

    [Fact]
    public void Pick_rejects_an_empty_list()
    {
        var rng = new Rng(1);
        Assert.Throws<ArgumentException>(() => rng.Pick(Array.Empty<string>()));
    }
}
