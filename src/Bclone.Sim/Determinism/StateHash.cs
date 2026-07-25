using Bclone.Sim.Core;

namespace Bclone.Sim.Determinism;

/// <summary>
/// FNV-1a (64-bit) over a canonical view of sim state — a cheap witness that two
/// worlds are identical.
/// </summary>
/// <remarks>
/// <para>
/// This is a <em>fingerprint</em>, not a serializer. It exists so the determinism
/// test can compare two 10,000-tick runs with one integer comparison, and so a
/// desync (later, in co-op or replay) can be caught at the exact tick it starts
/// rather than whenever someone notices the towns diverged.
/// </para>
/// <para>
/// <b>Keep this current.</b> Every field added to <see cref="SimWorld"/> that is
/// genuinely part of the simulation must be mixed in here, in a fixed order.
/// A field that is hashed but not simulated is harmless; a field that is
/// simulated but not hashed makes the determinism test quietly weaker. When
/// save/load arrives, this should be reconciled with canonical serialization.
/// </para>
/// </remarks>
public static class StateHash
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>Fingerprint the whole world.</summary>
    public static ulong Compute(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        ulong hash = FnvOffsetBasis;

        // Order matters and must never change casually — it is part of the
        // value. Append new fields at the end.
        hash = MixUInt64(hash, world.Tick);
        hash = MixUInt64(hash, world.Rng.State);
        hash = MixUInt64(hash, world.Rng.Inc);

        // Phase 0 appends villager, clock, and stockpile state here.

        return hash;
    }

    /// <summary>Mix eight bytes, low byte first, into the running hash.</summary>
    public static ulong MixUInt64(ulong hash, ulong value)
    {
        for (int i = 0; i < 8; i++)
        {
            hash ^= (value >> (i * 8)) & 0xFF;
            hash = unchecked(hash * FnvPrime);
        }

        return hash;
    }

    /// <summary>Mix four bytes into the running hash.</summary>
    public static ulong MixUInt32(ulong hash, uint value)
    {
        for (int i = 0; i < 4; i++)
        {
            hash ^= (value >> (i * 8)) & 0xFF;
            hash = unchecked(hash * FnvPrime);
        }

        return hash;
    }

    /// <summary>Mix a single byte into the running hash.</summary>
    public static ulong MixByte(ulong hash, byte value)
    {
        hash ^= value;
        return unchecked(hash * FnvPrime);
    }
}
