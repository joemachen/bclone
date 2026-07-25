using Bclone.Sim.Determinism;
using Xunit;

namespace Bclone.Sim.Tests;

public sealed class DeterministicRandomTests
{
    /// <summary>
    /// Known-answer vector from the reference PCG implementation's demo program
    /// (<c>pcg32-demo.c</c>, <c>pcg32_srandom_r(&amp;rng, 42u, 54u)</c>).
    /// </summary>
    /// <remarks>
    /// This is the most valuable test in the file, and it is deliberately an
    /// <em>external</em> vector rather than output captured from our own code.
    /// Self-captured values would only prove we still do whatever we did first,
    /// including a bug. These prove we implement real PCG32 — and if this ever
    /// goes red, every golden test and save file in the project is invalidated
    /// at the same moment.
    /// </remarks>
    private static readonly uint[] ReferenceStream =
    {
        0xa15c02b7, 0x7b47f409, 0xba1d3330,
        0x83d2f293, 0xbfa4784b, 0xcbed606e,
    };

    [Fact]
    public void MatchesReferencePcg32Vector()
    {
        var rng = new DeterministicRandom(seed: 42UL, stream: 54UL);

        for (int i = 0; i < ReferenceStream.Length; i++)
        {
            Assert.Equal(ReferenceStream[i], rng.NextUInt());
        }
    }

    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var a = new DeterministicRandom(999UL);
        var b = new DeterministicRandom(999UL);

        for (int i = 0; i < 1_000; i++)
        {
            Assert.Equal(a.NextUInt(), b.NextUInt());
        }
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentSequence()
    {
        var a = new DeterministicRandom(1UL);
        var b = new DeterministicRandom(2UL);

        Assert.NotEqual(Draw(ref a, 16), Draw(ref b, 16));
    }

    [Fact]
    public void DifferentStream_ProducesDifferentSequence()
    {
        var a = new DeterministicRandom(42UL, stream: 1UL);
        var b = new DeterministicRandom(42UL, stream: 2UL);

        Assert.NotEqual(Draw(ref a, 16), Draw(ref b, 16));
    }

    [Fact]
    public void StateRoundTrips_ForSaveAndRestore()
    {
        var original = new DeterministicRandom(7UL);
        original.NextUInt();
        original.NextUInt();

        var restored = DeterministicRandom.FromState(original.State, original.Inc);

        Assert.Equal(original, restored);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(original.NextUInt(), restored.NextUInt());
        }
    }

    [Fact]
    public void IncIsAlwaysOdd()
    {
        // An even increment collapses the generator's period.
        Assert.Equal(1UL, new DeterministicRandom(1UL, stream: 0UL).Inc & 1UL);
        Assert.Equal(1UL, DeterministicRandom.FromState(0UL, 8UL).Inc & 1UL);
    }

    [Fact]
    public void NextULong_IsStableAcrossCalls()
    {
        var a = new DeterministicRandom(123UL);
        var b = new DeterministicRandom(123UL);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(a.NextULong(), b.NextULong());
        }
    }

    [Fact]
    public void BoundedDraw_StaysInRange()
    {
        var rng = new DeterministicRandom(5UL);

        for (int i = 0; i < 20_000; i++)
        {
            Assert.InRange(rng.NextUInt(10U), 0U, 9U);
        }
    }

    [Fact]
    public void BoundedDraw_CoversTheWholeRange()
    {
        var rng = new DeterministicRandom(5UL);
        var seen = new bool[6];

        for (int i = 0; i < 5_000; i++)
        {
            seen[rng.NextUInt(6U)] = true;
        }

        Assert.DoesNotContain(false, seen);
    }

    [Fact]
    public void BoundedDraw_IsNotObviouslyBiased()
    {
        // Not a statistical proof — just a tripwire for a badly broken bound,
        // like an off-by-one that starves the top or bottom bucket.
        const int Draws = 120_000;
        const uint Buckets = 6U;
        var rng = new DeterministicRandom(11UL);
        var counts = new int[Buckets];

        for (int i = 0; i < Draws; i++)
        {
            counts[rng.NextUInt(Buckets)]++;
        }

        int expected = Draws / (int)Buckets;
        foreach (int count in counts)
        {
            Assert.InRange(count, (int)(expected * 0.9), (int)(expected * 1.1));
        }
    }

    [Fact]
    public void ZeroBound_Throws()
    {
        var rng = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextUInt(0U));
    }

    [Fact]
    public void NextInt_RespectsInclusiveAndExclusiveBounds()
    {
        var rng = new DeterministicRandom(3UL);

        for (int i = 0; i < 20_000; i++)
        {
            Assert.InRange(rng.NextInt(-5, 5), -5, 4);
        }
    }

    [Fact]
    public void NextInt_WithInvertedRange_Throws()
    {
        var rng = new DeterministicRandom(1UL);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(5, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(5, 1));
    }

    private static List<uint> Draw(ref DeterministicRandom rng, int count)
    {
        var values = new List<uint>(count);
        for (int i = 0; i < count; i++)
        {
            values.Add(rng.NextUInt());
        }

        return values;
    }
}
