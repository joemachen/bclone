namespace Bclone.Sim.Determinism;

/// <summary>
/// PCG32 (XSH-RR variant) — a small, fast, statistically solid PRNG with
/// explicit, serializable state.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not <c>System.Random</c>:</b> its underlying algorithm changed between
/// .NET Framework and .NET Core, and nothing in its contract promises stability
/// across future runtimes. A seeded run that silently produces a different
/// sequence after a runtime upgrade would invalidate every golden test in the
/// project and break saves. So the sim owns its generator outright.
/// </para>
/// <para>
/// The state is public because it <em>is</em> sim state: it feeds the state hash
/// and will be written into save files. A generator whose position you cannot
/// observe is not deterministic in any useful sense.
/// </para>
/// <para>
/// Reference: O'Neill, "PCG: A Family of Simple Fast Space-Efficient
/// Statistically Good Algorithms for Random Number Generation" (2014).
/// </para>
/// </remarks>
public struct DeterministicRandom : IEquatable<DeterministicRandom>
{
    private const ulong Multiplier = 6364136223846793005UL;
    private const ulong DefaultStream = 1442695040888963407UL;

    /// <summary>Current position in the stream.</summary>
    public ulong State { get; private set; }

    /// <summary>Stream selector. Always odd; two generators with the same
    /// <see cref="State"/> but different <see cref="Inc"/> produce distinct
    /// sequences.</summary>
    public ulong Inc { get; private set; }

    /// <summary>Seed a generator. Same (seed, stream) always yields the same
    /// sequence, on every machine and every runtime.</summary>
    public DeterministicRandom(ulong seed, ulong stream = DefaultStream)
    {
        State = 0UL;
        Inc = (stream << 1) | 1UL;   // force odd
        NextUInt();
        State = unchecked(State + seed);
        NextUInt();
    }

    /// <summary>Restore a generator from previously captured state (save/load,
    /// or a test fixture).</summary>
    public static DeterministicRandom FromState(ulong state, ulong inc)
    {
        var rng = default(DeterministicRandom);
        rng.State = state;
        rng.Inc = inc | 1UL;         // an even Inc would degrade the period
        return rng;
    }

    /// <summary>Next 32 bits.</summary>
    public uint NextUInt()
    {
        ulong oldState = State;
        State = unchecked(oldState * Multiplier + Inc);

        uint xorshifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        int rot = (int)(oldState >> 59);

        // C# masks shift counts to 5 bits for uint, so the rot == 0 case
        // degenerates correctly to (x >> 0) | (x << 0).
        return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
    }

    /// <summary>Next 64 bits, assembled from two draws (high word first, so the
    /// byte order is fixed and does not depend on machine endianness).</summary>
    public ulong NextULong()
    {
        ulong high = NextUInt();
        ulong low = NextUInt();
        return (high << 32) | low;
    }

    /// <summary>
    /// Uniform value in <c>[0, exclusiveBound)</c>, free of modulo bias.
    /// </summary>
    /// <remarks>
    /// Uses rejection sampling: values below the threshold would map unevenly
    /// onto the range, so they are drawn again. The loop is unbounded in
    /// principle but terminates with probability 1 and, in practice, almost
    /// always on the first draw.
    /// </remarks>
    public uint NextUInt(uint exclusiveBound)
    {
        if (exclusiveBound == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exclusiveBound), "Bound must be greater than zero.");
        }

        uint threshold = (uint)((0x1_0000_0000UL - exclusiveBound) % exclusiveBound);

        while (true)
        {
            uint r = NextUInt();
            if (r >= threshold)
            {
                return r % exclusiveBound;
            }
        }
    }

    /// <summary>Uniform value in <c>[minInclusive, maxExclusive)</c>.</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                $"maxExclusive ({maxExclusive}) must be greater than minInclusive ({minInclusive}).");
        }

        uint range = (uint)((long)maxExclusive - minInclusive);
        return (int)(minInclusive + NextUInt(range));
    }

    public bool Equals(DeterministicRandom other) => State == other.State && Inc == other.Inc;

    public override bool Equals(object? obj) => obj is DeterministicRandom other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(State, Inc);

    public override string ToString() => $"DeterministicRandom(State=0x{State:X16}, Inc=0x{Inc:X16})";

    public static bool operator ==(DeterministicRandom left, DeterministicRandom right) => left.Equals(right);

    public static bool operator !=(DeterministicRandom left, DeterministicRandom right) => !left.Equals(right);
}
