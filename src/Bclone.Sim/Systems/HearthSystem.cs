using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Homes burn firewood through winter, and the people in a cold one start to freeze.
/// </summary>
/// <remarks>
/// <para>
/// The second way winter can kill (D17, D29). Phase 0 refused this outright — *"winter's
/// danger is food scarcity only; do not add a second overlapping death system"* — and
/// reversing it is deliberate, now that there are households to heat and a labour
/// system for fuel to compete inside. The half of that reasoning which has <b>not</b>
/// expired is legibility: a death must never be ambiguous between cold and hunger.
/// <see cref="MortalitySystem"/> is where that promise is kept.
/// </para>
/// <para>
/// <b>Burning is per household and per day.</b> Per household because a house costs the
/// same to heat whether two live in it or five — which makes sprawl the thing that
/// costs, rather than population, and ties the pressure back to a decision rather than
/// to growth. Per day because fuel is measured in whole logs and a tick is a quarter of
/// a day; charging a fraction of a log per tick would need either floats (D2 forbids
/// them) or a rounding rule nobody could read off the screen.
/// </para>
/// </remarks>
public sealed class HearthSystem : ISimSystem
{
    public string Name => "hearth";

    public void Execute(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        SimConfig config = world.Config;

        // Fuel switched off entirely.
        //
        // Not a convenience: Phase 0 is the one-villager slice and its spec rules
        // warmth out by name — "winter's danger is food scarcity only; do not add a
        // second overlapping death system". A lone villager who must both feed
        // themselves and keep a fire going is precisely the double jeopardy that spec
        // refused, and switching fuel on under its fixtures killed them. The fixture
        // should encode the world its tests describe.
        if (config.FirewoodPerWinterDay <= 0)
        {
            ClearTheCold(world);
            return;
        }

        // Nothing to do outside winter — and, crucially, cold does not accumulate
        // either. Spring resets everyone, so a household that scraped through
        // February is not still dying of it in May.
        if (!IsHeatingSeason(world.Clock.Season))
        {
            ClearTheCold(world);
            return;
        }

        // Every few days rather than every day (Joe: *"make firewood consumption take
        // longer, like 4x longer"*). The interval carries the rate because sim state is
        // integer-only and a quarter of a log is not a number this game can hold (D2).
        // ⛔ COUNTED FROM THE FIRST DAY OF WINTER, NOT FROM TICK ZERO (D365). This was
        // `Tick % burnEvery == 0`, which is the same thing only while a winter's first tick happens
        // to fall on the beat: at three days a burn it always did (360 mod 12 = 0), and at four it
        // never did (360 mod 16 = 8), so a winter budgeted at eight burns got seven — the economy
        // said 24 and the hearth burned 21, and nothing would have said so but a colder village.
        // The economy (`FirewoodPerHouseholdPerWinter`) counts burns from the season's first day;
        // now the hearth does too, on the first day and every interval after it.
        int dayOfWinter = world.Clock.DayOfSeason - 1;
        bool onTheBeat = dayOfWinter % config.FirewoodBurnIntervalDays == 0
            && world.Tick % (ulong)config.TicksPerDay == 0UL;
        if (onTheBeat)
        {
            BurnADaysFirewood(world, config, dayOfWinter / config.FirewoodBurnIntervalDays);
        }

        // The coats wear on the day's first tick, before the chill — so the day a coat runs out is a bare day.
        if (world.Tick % (ulong)config.TicksPerDay == 0UL)
        {
            WearTheCoats(world);
        }

        ChillTheUnsheltered(world, config);
    }

    /// <summary>
    /// ⭐ A coat wears a day every winter day it is worn, whatever the day held (D659, `clothing.md §3.3`) — by the
    /// day and not by the tick outdoors, so the tailor's demand is a steady share of the adults and a coat in a
    /// drawer wears no faster than one on a back. At zero the DEBUG log says so; the villager goes on at the bare
    /// rate and fetches another when a store has one.
    /// </summary>
    private static void WearTheCoats(SimWorld world)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive || villager.CoatDays <= 0)
            {
                continue;
            }

            villager.CoatDays--;
            if (villager.CoatDays == 0 && world.Logs(LogLevel.Debug))
            {
                world.Log(LogLevel.Debug, "hearth", $"{villager.Name}'s coat is worn through — {world.Clock}.");
            }
        }
    }

    /// <summary>Winter only, for now. Shoulder seasons are a config change away.</summary>
    private static bool IsHeatingSeason(Season season) => season == Season.Winter;

    /// <summary>
    /// What a house of <paramref name="winterFirewood"/> a winter burns on beat <paramref name="beat"/> of
    /// <paramref name="beats"/> (`house-tiers.md §4.2`, D719): ⌊(k+1)·W/B⌋ − ⌊k·W/B⌋ — evenly spread, summing to W, and
    /// for the cabin (W = B × <c>firewood_per_winter_day</c>) exactly <c>firewood_per_winter_day</c> on every beat.
    /// </summary>
    internal static int BurnOnBeat(int winterFirewood, int beat, int beats)
    {
        if (beats <= 0 || winterFirewood <= 0 || beat < 0 || beat >= beats)
        {
            return 0;
        }

        long w = winterFirewood;
        return (int)(((beat + 1) * w / beats) - (beat * w / beats));
    }

    /// <summary>
    /// What a tick under a fireless roof at <paramref name="at"/> costs (D45; `house-tiers.md §4.8`, *keeps its warmth*):
    /// a house's own row says how many days it shelters, every other roof is <c>exposure_days_sheltered</c>.
    /// </summary>
    internal static int RoofChillAt(SimWorld world, GridPos at)
    {
        SimConfig config = world.Config;
        if (world.HouseholdAt(at) is Household household
            && world.HomeRowOf(household).ExposureDaysSheltered is int days && days > 0
            && days != config.ExposureDaysSheltered && config.ExposureThreshold > 0)
        {
            return config.ExposureThreshold / (days * config.TicksPerDay);
        }

        return config.ExposurePerTickSheltered;
    }

    private static void BurnADaysFirewood(SimWorld world, SimConfig config, int beat)
    {
        int beats = (config.DaysPerSeason + config.FirewoodBurnIntervalDays - 1) / config.FirewoodBurnIntervalDays;
        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            if (world.LivingMembersOf(household) == 0 || household.HomeClosed)
            {
                // Nobody home, or no roof to be home under (`building-condition.md §4.5`): the fire is not lit.
                continue;
            }

            // ⛔⛔ WHAT IS LEFT, NOT THREE OR NOTHING (D407). A burn that could only take a whole
            // day's three left a larder holding one or two logs untouched — and a home with any
            // firewood is a lit hearth (`SimWorld`'s fire test), so it stayed warm for ever at no
            // cost. Measured: an established village's stores ran dry in year six and both homes
            // wintered four more years on 1–2 logs, nobody cold. The last logs burn now, and the
            // house is cold on the next beat.
            // The house's own winter, spread over the beats (D719) — 3 a beat for the cabin, as it always was.
            int burn = Math.Min(household.Stockpile.Firewood, BurnOnBeat(world.WinterFirewoodOf(household), beat, beats));
            if (burn <= 0 || !household.Stockpile.TryTake(Goods.Firewood, burn))
            {
                continue;
            }

            // Warned, not surprised. The line lands when the last log goes on the
            // fire, not when somebody dies of its absence — the same principle §2.7
            // states about knowledge at risk: a foreseeable loss has to be visible
            // and actionable, or it reads as unfair rather than as a consequence.
            if (household.Stockpile.Firewood == 0)
            {
                world.Narrate(
                    $"The {household.Name} household put its last firewood on the fire " +
                    $"— {world.Clock.SeasonAndYear()}.", LogCategory.Warning);
            }
        }
    }

    /// <summary>
    /// Cold, counted from where each villager is standing (D45).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the half of the old system that D45 replaced.</b> It used to ask one
    /// question — does this villager's household have firewood? — of everyone, everywhere,
    /// identically, so a villager froze because of a number attached to their family
    /// rather than because of anything you could watch. Now the tile answers it: open
    /// ground costs the most, a roof with no hearth costs less, and a burning fire gives
    /// it back.
    /// </para>
    /// <para>
    /// One accumulator with three rates rather than a counter per state — see
    /// <see cref="SimConfig.ExposureThreshold"/> for why a counter per state leaves a
    /// villager who alternates immortal.
    /// </para>
    /// </remarks>
    private static void ChillTheUnsheltered(SimWorld world, SimConfig config)
    {
        int threshold = config.ExposureThreshold;
        if (threshold <= 0)
        {
            ClearTheCold(world);
            return;
        }

        int warnAt = threshold * config.SeekShelterPercent / 100;

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive)
            {
                continue;
            }

            int before = villager.Cold;

            // ⭐ A COAT IS A ROOF YOU WEAR (Joe, D659; `clothing.md §3.1`): open ground chills a clothed villager at
            // the roof's rate and no faster. The roof's and the fire's arms do not read the coat — a household out of
            // firewood still freezes, so fuel stays the winter's axis (D45, D53, D69).
            villager.Cold += world.ShelterAt(villager.Tile) switch
            {
                Shelter.Fire => -config.ThawPerTickAtAFire,
                Shelter.Roof => RoofChillAt(world, villager.Tile),
                _ => villager.CoatDays > 0 ? config.ExposurePerTickSheltered : config.ExposurePerTickOutdoors,
            };

            // Clamped both ends. Below zero would bank warmth against next winter, which
            // is not a thing a body does; above the threshold is death and is
            // MortalitySystem's to read, so there is nothing to gain by counting past it.
            if (villager.Cold < 0)
            {
                villager.Cold = 0;
            }
            else if (villager.Cold > threshold)
            {
                villager.Cold = threshold;
            }

            // Once per episode, on the way UP through the line where they break off work
            // to get warm — so the sentence a player reads and the decision the villager
            // makes are the same moment. Crossing it downward is somebody thawing out,
            // and saying it again then would train them to ignore it.
            if (before < warnAt && villager.Cold >= warnAt && warnAt > 0)
            {
                // ⭐ THE SENTENCE SAYS WHETHER THERE IS A COAT, AND HOW LONG THEY HAVE BEEN IN THE COLD (D691). Joe, on
                // a winter log of these with 23 coats in the warehouse: *"why are so many people cold when I have so
                // much clothing available?"* — every adult wore one. A coat is a roof you wear (D659): it slows the
                // chill, it does not stop it, so a coated outdoor worker crosses this line after 12.5 net days out.
                // The days are the villager's own cold divided by the rate their coat gives them.
                bool coated = villager.CoatDays > 0;
                int perDay = (coated ? config.ExposurePerTickSheltered : config.ExposurePerTickOutdoors) * config.TicksPerDay;
                int days = perDay > 0 ? (villager.Cold + (perDay / 2)) / perDay : 0;
                world.Narrate(
                    coated
                        ? $"{villager.Name} is dangerously cold after {days} days in the cold, coat and all, and is going in to get warm "
                            + $"— {world.Clock.SeasonAndYear()}."
                        : $"{villager.Name} is dangerously cold after {days} days in the cold with no coat and is going in to get warm "
                        + $"— {world.Clock.SeasonAndYear()}.",
                    LogCategory.Warning);
            }
        }
    }

    /// <summary>
    /// Spring thaws everyone.
    /// </summary>
    /// <remarks>
    /// Cold is a <em>winter</em> condition, so it cannot carry over. Without this a
    /// villager who ended winter half-frozen would still be counting toward death in
    /// midsummer, which is neither survivable nor explicable.
    /// </remarks>
    private static void ClearTheCold(SimWorld world)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            world.Villagers[i].Cold = 0;
        }
    }
}
