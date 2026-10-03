namespace Bclone.Sim.Determinism;

/// <summary>
/// splitmix64's finaliser — the one stateless mix the sim uses wherever a value must come from
/// a hash of the seed rather than a draw (D344, D434, D466, D473).
/// </summary>
/// <remarks>
/// <para>
/// <b>Well spread even for adjacent inputs</b>, which is the property D344 measured
/// <see cref="DeterministicRandom"/>'s <c>stream</c> parameter lacking: streams 1–5 gave six
/// dead valleys in 24 against one. A worldgen stage's seed, a hashed seam's jitter and a
/// villager's name all fold through here, so there is one copy of the constants, not three.
/// </para>
/// </remarks>
public static class SplitMix64
{
    /// <summary>
    /// Fold <paramref name="value"/> into <paramref name="state"/>: the finaliser over
    /// <c>state + 0x9E3779B97F4A7C15 × (value + 1)</c>.
    /// </summary>
    public static ulong Fold(ulong state, ulong value)
    {
        unchecked
        {
            ulong z = state + (0x9E3779B97F4A7C15UL * (value + 1));
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
