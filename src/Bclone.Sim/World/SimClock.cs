using Bclone.Sim.Config;

namespace Bclone.Sim.World;

/// <summary>
/// The calendar: tick → day → season → year.
/// </summary>
/// <remarks>
/// <para>
/// This is <b>derived, not stored</b> — a pure function of the tick and the config.
/// That is deliberate. A mutable calendar with its own rollover counters is one more
/// thing that can drift out of sync with the tick, and one more thing to get wrong in
/// a save file. Deriving it means the calendar is correct by construction, and the
/// determinism surface stays as small as possible.
/// </para>
/// <para>
/// Days and years are 1-based for display ("Day 1, Spring, Year 1"), because that is
/// how a person reads a calendar. Ticks stay 0-based, because that is how a machine
/// counts.
/// </para>
/// </remarks>
public readonly record struct SimClock(
    ulong Tick,
    int DayOfSeason,
    int DayOfYear,
    Season Season,
    int Year)
{
    /// <summary>Compute the calendar for a given tick.</summary>
    public static SimClock FromTick(ulong tick, SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        ulong ticksPerDay = (ulong)config.TicksPerDay;
        ulong daysPerSeason = (ulong)config.DaysPerSeason;
        ulong daysPerYear = daysPerSeason * 4UL;

        ulong totalDays = tick / ticksPerDay;
        ulong dayOfYear = totalDays % daysPerYear;

        return new SimClock(
            Tick: tick,
            DayOfSeason: (int)(dayOfYear % daysPerSeason) + 1,
            DayOfYear: (int)dayOfYear + 1,
            Season: (Season)(dayOfYear / daysPerSeason),
            Year: (int)(totalDays / daysPerYear) + 1);
    }

    /// <summary>True in winter, when nothing can be foraged.</summary>
    public bool IsWinter => Season == Season.Winter;

    /// <summary>"Day 4, Spring, Year 12"</summary>
    public override string ToString() => $"Day {DayOfSeason}, {Season}, Year {Year}";

    /// <summary>"Spring, Year 12" — for season-level narration.</summary>
    public string SeasonAndYear() => $"{Season}, Year {Year}";
}
