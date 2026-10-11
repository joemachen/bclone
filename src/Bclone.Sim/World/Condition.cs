using Bclone.Sim.Config;

namespace Bclone.Sim.World;

/// <summary>
/// How a standing thing is holding up (`specs/building-condition.md §4.2`, D552–D553): four named states,
/// read off its row in years. The only effects are the ones a sentence can say — a ruin is closed, a
/// failing wooden deck slows — never a multiplier.
/// </summary>
public enum Condition
{
    Sound = 0,
    Worn = 1,
    Failing = 2,
    Ruin = 3,
}

/// <summary>
/// Condition is <b>derived</b> from two stamps and the calendar — never a counter anybody decrements
/// (`building-condition.md §4.1`; CLAUDE.md: nothing derivable is rebuilt per tick).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>WHY DERIVED.</b> D65 refused decay as *a tax the player answers with babysitting*, and a counter
/// ticking down on every building every tick is exactly the shape that objection pictures. Two stamps
/// (<c>RaisedAtTick</c>, <c>RepairedAtTick</c>) are facts of the village and are hashed; everything here
/// restates them and is not (D335). The sim reads this at the season turn, the view and the cards when
/// they draw, the tests whenever they like — and all of them get the same answer.
/// </para>
/// <para>
/// <b>Winter wears more</b> (D206's axis): every winter <em>begun</em> since the later stamp adds
/// <c>winter_wear_extra_percent</c> of a season's ticks. Counted in closed form from the calendar — the
/// winters are the ticks <c>k × TicksPerYear + 3 × TicksPerSeason</c>, so the count in a half-open range
/// is a subtraction, not a loop.
/// </para>
/// </remarks>
public static class ConditionRules
{
    /// <summary>The wear a thing has taken by <paramref name="now"/>, in ticks of mild weather.</summary>
    public static ulong WearTicks(ulong raisedAt, ulong repairedAt, ulong now, SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        ulong since = Math.Max(raisedAt, repairedAt);
        if (now <= since)
        {
            return 0;
        }

        ulong winters = WintersBegunBy(now, config) - WintersBegunBy(since, config);
        ulong extra = (ulong)config.TicksPerSeason * (ulong)config.WinterWearExtraPercent / 100UL;
        return (now - since) + (winters * extra);
    }

    /// <summary>How many winters have begun at or before <paramref name="tick"/> — the calendar's, not a count anybody keeps.</summary>
    internal static ulong WintersBegunBy(ulong tick, SimConfig config)
    {
        ulong firstWinter = (ulong)config.TicksPerSeason * 3UL;
        return tick < firstWinter ? 0UL : ((tick - firstWinter) / (ulong)config.TicksPerYear) + 1UL;
    }

    /// <summary>The state a row's thing is in at this much wear. A row with <c>wears_in_years</c> 0 never wears.</summary>
    public static Condition Of(ulong wearTicks, BuildingRow row, SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(config);

        if (row.WearsInYears <= 0)
        {
            return Condition.Sound;
        }

        ulong year = (ulong)config.TicksPerYear;
        if (wearTicks >= (ulong)row.RuinedInYears * year)
        {
            return Condition.Ruin;
        }

        if (wearTicks >= (ulong)row.FailsInYears * year)
        {
            return Condition.Failing;
        }

        return wearTicks >= (ulong)row.WearsInYears * year ? Condition.Worn : Condition.Sound;
    }

    /// <summary>The state of a thing with these stamps, now.</summary>
    public static Condition Of(ulong raisedAt, ulong repairedAt, ulong now, BuildingRow row, SimConfig config) =>
        Of(WearTicks(raisedAt, repairedAt, now, config), row, config);

    /// <summary>
    /// About how many years until a thing at this much wear reaches <paramref name="target"/> — the card's
    /// "fails in about N years" — or null for a row that never gets there. Rounded up; a winter's extra is
    /// averaged in, so the number is a year's worth of ordinary weather, not a promise.
    /// </summary>
    public static int? YearsUntil(ulong wearTicks, Condition target, BuildingRow row, SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(config);

        if (row.WearsInYears <= 0 || target == Condition.Sound)
        {
            return null;
        }

        int years = target switch
        {
            Condition.Worn => row.WearsInYears,
            Condition.Failing => row.FailsInYears,
            _ => row.RuinedInYears,
        };

        ulong threshold = (ulong)years * (ulong)config.TicksPerYear;
        if (wearTicks >= threshold)
        {
            return 0;
        }

        ulong aYearsWear = (ulong)config.TicksPerYear
            + ((ulong)config.TicksPerSeason * (ulong)config.WinterWearExtraPercent / 100UL);
        return (int)((threshold - wearTicks + aYearsWear - 1UL) / aYearsWear);
    }
}
