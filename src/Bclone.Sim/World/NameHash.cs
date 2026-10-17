using Bclone.Sim.Determinism;

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

    /// <summary>What a villager's meal phase is picked by (D529) — and, keyed on the first founder's id, the founding's.</summary>
    public const int MealPhaseSalt = 6;

    /// <summary>What a nomad's lifespan is picked by (`nomads.md §4.2`, D564) — a hash where a founder's is a draw, so a knock moves no stream.</summary>
    public const int NomadLifespanSalt = 7;

    /// <summary>What a nomad's personal rhythm is picked by.</summary>
    public const int NomadRhythmSalt = 8;

    /// <summary>What a nomad's age is picked by.</summary>
    public const int NomadAgeSalt = 9;

    /// <summary>What the year of a knock is picked by (keyed on the count of bands so far).</summary>
    public const int KnockSalt = 10;

    /// <summary>What a band's journeyman and their trade are picked by (keyed on the count of bands so far).</summary>
    public const int NomadTradeSalt = 11;

    /// <summary>What the wait before a dead valley is re-founded is picked by (`nomads.md §4.6`).</summary>
    public const int RefoundSalt = 12;

    /// <summary>
    /// splitmix64's finaliser folded over the seed, the id, the salt and the attempt in turn —
    /// <c>SplitMix64.Fold</c>, well spread even for adjacent ids.
    /// </summary>
    public static ulong Mix(ulong seed, int id, int salt, int attempt)
    {
        ulong z = SplitMix64.Fold(seed, (ulong)(uint)id);
        z = SplitMix64.Fold(z, (ulong)(uint)salt);
        return SplitMix64.Fold(z, (ulong)(uint)attempt);
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

    /// <summary>
    /// Where on the meal's cycle villager <paramref name="id"/> starts — a starting hunger in
    /// <c>[0, eatThreshold)</c> (`skills-catalog.md §3.5a`, D529). ⛔ A hash, never a draw: it decides only
    /// when a first meal falls, and the stream every other draw takes from stays where it was.
    /// </summary>
    public static int MealPhase(ulong seed, int id, int eatThreshold) =>
        (int)(Mix(seed, id, MealPhaseSalt, 0) % (ulong)eatThreshold);

    /// <summary>
    /// A number in <c>[0, spread]</c> for a knock — the hash's share of <i>"random with a floor"</i> (Joe), keyed on
    /// how many bands have knocked so far, so the first knock and every later one have a year of their own.
    /// </summary>
    public static int KnockOffset(ulong seed, int bandsSoFar, int spread) =>
        spread <= 0 ? 0 : (int)(Mix(seed, bandsSoFar, KnockSalt, 0) % (ulong)(spread + 1));

    /// <summary>A number in <c>[0, spread]</c> for the wait before a dead valley is re-founded, keyed on how many times it has been (`nomads.md §4.6`).</summary>
    public static int RefoundOffset(ulong seed, int refoundings, int spread) =>
        spread <= 0 ? 0 : (int)(Mix(seed, refoundings, RefoundSalt, 0) % (ulong)(spread + 1));

    /// <summary>A nomad's lifespan — the founders' base and spread, hashed rather than drawn (`nomads.md §4.2`).</summary>
    public static int NomadLifespan(ulong seed, int key, int baseYears, int variance) =>
        variance <= 0 ? baseYears : baseYears - variance + (int)(Mix(seed, key, NomadLifespanSalt, 0) % (ulong)((2 * variance) + 1));

    /// <summary>A nomad's rhythm in <c>[0, ticksPerDay)</c>.</summary>
    public static int NomadRhythm(ulong seed, int key, int ticksPerDay) =>
        ticksPerDay <= 1 ? 0 : (int)(Mix(seed, key, NomadRhythmSalt, 0) % (ulong)ticksPerDay);

    /// <summary>A nomad's age in <c>[from, to]</c>.</summary>
    public static int NomadAge(ulong seed, int key, int from, int to) =>
        to <= from ? from : from + (int)(Mix(seed, key, NomadAgeSalt, 0) % (ulong)(to - from + 1));
}
