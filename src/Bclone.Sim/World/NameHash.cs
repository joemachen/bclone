namespace Bclone.Sim.World;

/// <summary>
/// Names from a hash of the seed and an id — ⛔ never an <c>Rng</c> draw (D395, D465,
/// <c>specs/names-and-birthdays.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// A name is a function of (seed, id, attempt): the same valley names the same person the same
/// thing however long its lists grow, and naming takes nothing from the stream the economy draws
/// on. That is D392's lesson — <i>a list content can grow is not a list a draw may range over</i> —
/// and it is why <c>town_names</c> was already indexed arithmetically.
/// </para>
/// <para>
/// <b>Attempts</b> are how a collision is resolved: the caller asks for attempt 0, then 1, … until
/// the name is free. The function never looks at the village; who holds a name is the caller's
/// question.
/// </para>
/// </remarks>
public static class NameHash
{
    /// <summary>What a first name's first half is picked by.</summary>
    public const int PrefixSalt = 1;

    /// <summary>What a first name's second half is picked by.</summary>
    public const int SuffixSalt = 2;

    /// <summary>What a founding household's surname is picked by (keyed on the household's id).</summary>
    public const int SurnameSalt = 3;

    /// <summary>What a founder's birthday is picked by (D468).</summary>
    public const int BirthdaySalt = 4;

    /// <summary>What a household's day for a child is picked by (D469, keyed on the household's id).</summary>
    public const int ChildDaySalt = 5;

    /// <summary>
    /// splitmix64's finaliser folded over the seed, the id, the salt and the attempt in turn —
    /// <c>MapGenerator.HashJitter</c>'s shape, well spread even for adjacent ids.
    /// </summary>
    public static ulong Mix(ulong seed, int id, int salt, int attempt)
    {
        ulong z = Fold(seed, (ulong)(uint)id);
        z = Fold(z, (ulong)(uint)salt);
        return Fold(z, (ulong)(uint)attempt);
    }

    /// <summary>The <paramref name="attempt"/>th candidate first name for villager <paramref name="id"/>.</summary>
    public static string FirstName(
        ulong seed, int id, int attempt, IReadOnlyList<string> prefixes, IReadOnlyList<string> suffixes)
    {
        string prefix = prefixes[(int)(Mix(seed, id, PrefixSalt, attempt) % (ulong)prefixes.Count)];
        string suffix = suffixes[(int)(Mix(seed, id, SuffixSalt, attempt) % (ulong)suffixes.Count)];
        return prefix + suffix;
    }

    /// <summary>The <paramref name="attempt"/>th candidate surname for founding household <paramref name="householdId"/>.</summary>
    public static string Surname(ulong seed, int householdId, int attempt, IReadOnlyList<string> surnames) =>
        surnames[(int)(Mix(seed, householdId, SurnameSalt, attempt) % (ulong)surnames.Count)];

    /// <summary>
    /// How far into the year before the founding founder <paramref name="id"/> was born, in ticks —
    /// <c>[0, ticksPerYear)</c>. ⛔ A hash, never a draw (D468): a birthday is as cosmetic to the
    /// stream as a name, and the founding's draws stay where D466 left them.
    /// </summary>
    public static long BirthdayOffset(ulong seed, int id, int ticksPerYear) =>
        (long)(Mix(seed, id, BirthdaySalt, 0) % (ulong)ticksPerYear);

    /// <summary>
    /// The day of the year — <c>[0, daysPerYear)</c> — on which household <paramref name="householdId"/>
    /// tries for a child (D469). ⛔ A hash, never a draw, like a name and a birthday.
    /// </summary>
    public static int DayForAChild(ulong seed, int householdId, int daysPerYear) =>
        (int)(Mix(seed, householdId, ChildDaySalt, 0) % (ulong)daysPerYear);

    private static ulong Fold(ulong state, ulong value)
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
