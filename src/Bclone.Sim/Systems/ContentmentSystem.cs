using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Contentment (W2, `specs/morale.md`; the player's word since D797): the day's tallies a cause needs, and the read at
/// each season's turn of which causes every adult holds.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>It reads and remembers, and two things act on it:</b> the births gate (step 4, D804) — an unsettled couple waits
/// <c>contentment_birth_interval_percent</c> of the interval between children (<c>HouseholdSystem.BirthIntervalFor</c>) —
/// and leaving (step 5): a year unsettled and the log warns once, two and they go at the season's turn
/// (<see cref="SeeWhoLeaves"/>). The tallies are saved, and hashed sparsely since step 4 — until then nothing
/// read them, so the goldens moved once, with the first effect. <i>(Read "NOTHING ACTS ON IT YET … saved but not hashed"
/// for steps 2–3, D801–D803.)</i>
/// </para>
/// <para>
/// ⭐ <b>A cause is HELD, not counted in a row</b> (D799): a standing one (no roof, the walk) while it is true, an episode
/// (a cold winter, the fire out, a death) for its row's <c>held_seasons</c> from the season it happened in. A streak of
/// consecutive seasons forgot a cold winter by spring, and a rolling sum counted a standing cause four times; this is
/// what was measured and what Joe took (D800).
/// </para>
/// <para>
/// Last in the tick order: the season's read sees this tick's deaths, and the hearth's cold days are counted before it.
/// </para>
/// </remarks>
public sealed class ContentmentSystem : ISimSystem
{
    public string Name => "contentment";

    public void Execute(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        SimConfig config = world.Config;
        if (config.ContentmentCauses.Count == 0 || world.Tick % (ulong)config.TicksPerDay != 0UL)
        {
            return;
        }

        if (world.Tick > 0 && world.Tick % ((ulong)config.TicksPerDay * (ulong)config.DaysPerSeason) == 0UL)
        {
            ReadTheSeason(world, config);
        }

        CountTheDay(world, config);
    }

    /// <summary>
    /// The day's turn: a housed household with its jar dry counts a dry day, and in winter one with no firewood at home
    /// has had its fire out — held from this season.
    /// </summary>
    private static void CountTheDay(SimWorld world, SimConfig config)
    {
        ContentmentCauseRow? dry = RowFor(config, ContentmentCause.DryJar);
        ContentmentCauseRow? fire = RowFor(config, ContentmentCause.FireOut);
        bool winter = world.Clock.Season == Season.Winter;
        if (dry is null && (fire is null || !winter))
        {
            return;
        }

        HashSet<int> lived = LivedIn(world);
        int season = world.SeasonNumber;
        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            if (!household.HasHome || !lived.Contains(household.Id))
            {
                continue;
            }

            if (dry is not null && household.Water <= 0)
            {
                household.DryDaysThisSeason++;
            }

            if (fire is not null && winter && household.Stockpile.Firewood == 0)
            {
                household.FireOutHeldUntil = season + fire.HeldSeasons;
            }
        }
    }

    /// <summary>The season just ended, read for every adult; then its tallies cleared.</summary>
    private static void ReadTheSeason(SimWorld world, SimConfig config)
    {
        int ended = world.SeasonNumber - 1;
        bool endedWinter = SimClock.FromTick(world.Tick - 1UL, config).Season == Season.Winter;
        ulong seasonTicks = (ulong)config.TicksPerDay * (ulong)config.DaysPerSeason;

        ContentmentCauseRow? cold = RowFor(config, ContentmentCause.ColdWinter);
        ContentmentCauseRow? death = RowFor(config, ContentmentCause.DeathInTheFamily);

        // Who has lost somebody: one pass over the dead of the held seasons — their household, and their partner.
        var bereavedHouseholds = new HashSet<int>();
        var bereavedPartners = new HashSet<int>();
        if (death is not null)
        {
            ulong window = seasonTicks * (ulong)death.HeldSeasons;
            ulong since = world.Tick > window ? world.Tick - window : 0UL;
            for (int i = 0; i < world.Villagers.Count; i++)
            {
                Villager dead = world.Villagers[i];
                if (!dead.Alive && dead.DiedAtTick is ulong at && at >= since)
                {
                    bereavedHouseholds.Add(dead.HouseholdId);
                    bereavedPartners.Add(dead.PartnerId);
                }
            }
        }

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.CanWork)
            {
                continue;
            }

            if (endedWinter && cold is not null && villager.ColdDaysThisWinter >= cold.At)
            {
                villager.ColdWinterHeldUntil = ended + cold.HeldSeasons;
            }

            Household? household = world.FindHousehold(villager.HouseholdId);
            int weight = 0;
            int causes = 0;
            foreach (ContentmentCauseRow row in config.ContentmentCauses)
            {
                bool held = row.Cause switch
                {
                    ContentmentCause.NoRoof => household is not { HasHome: true },
                    ContentmentCause.FarWalk => villager.CommuteTiles > 0 && villager.CommuteTiles >= row.At,
                    ContentmentCause.ColdWinter => villager.ColdWinterHeldUntil > ended,
                    ContentmentCause.FireOut => household is not null && household.FireOutHeldUntil > ended,
                    ContentmentCause.DryJar => household is not null && household.DryDaysThisSeason >= row.At,
                    ContentmentCause.DeathInTheFamily =>
                        bereavedHouseholds.Contains(villager.HouseholdId) || bereavedPartners.Contains(villager.Id),
                    _ => false,
                };

                if (held)
                {
                    weight += row.Weight;
                    causes |= 1 << (int)row.Cause;
                }
            }

            villager.ContentmentCauses = causes;
            villager.SeasonsUnsettled = weight >= config.ContentmentUnsettledAt ? villager.SeasonsUnsettled + 1 : 0;

            // A year unsettled: the log says it once a spell (step 5) — a settled season puts the count back to 0.
            if (config.ContentmentLeavingSeasons > 0 && villager.SeasonsUnsettled == config.ContentmentThinkingOfLeavingSeasons)
            {
                world.SayTheyAreThinkingOfLeaving(villager);
            }
        }

        SeeWhoLeaves(world, config);

        // The season's tallies, spent.
        for (int i = 0; i < world.Households.Count; i++)
        {
            world.Households[i].DryDaysThisSeason = 0;
        }

        if (endedWinter)
        {
            for (int i = 0; i < world.Villagers.Count; i++)
            {
                world.Villagers[i].ColdDaysThisWinter = 0;
            }
        }
    }

    /// <summary>
    /// ⭐ WHO GOES (step 5, `morale.md §13.2` item 3; Joe's D798 call 2), household by household in id order (D15): a ready
    /// adult whose living partner is not ready stays (<i>"she stayed for him"</i>); a ready couple goes together; a ready adult
    /// with no living partner goes alone. The household's children go with a couple that leaves, or with the last adult to
    /// leave the house — while an adult stays, a lone leaver's children stay with them. A child never leaves alone.
    /// </summary>
    private static void SeeWhoLeaves(SimWorld world, SimConfig config)
    {
        int leaving = config.ContentmentLeavingSeasons;
        if (leaving <= 0)
        {
            return;
        }

        for (int h = 0; h < world.Households.Count; h++)
        {
            Household household = world.Households[h];
            List<Villager>? goers = null;
            var children = new List<Villager>();
            bool aCoupleGoes = false;
            bool anAdultStays = false;
            foreach (int id in household.MemberIds)
            {
                if (world.FindVillager(id) is not { Alive: true } member || member.HouseholdId != household.Id)
                {
                    continue;
                }

                if (!member.CanWork)
                {
                    children.Add(member);
                    continue;
                }

                Villager? partner = world.FindVillager(member.PartnerId) is { Alive: true } p ? p : null;
                if (member.SeasonsUnsettled >= leaving && (partner is null || partner.SeasonsUnsettled >= leaving))
                {
                    (goers ??= new List<Villager>()).Add(member);
                    aCoupleGoes |= partner is not null;
                }
                else
                {
                    anAdultStays = true;
                }
            }

            if (goers is not null)
            {
                world.LetThemGo(goers, aCoupleGoes || !anAdultStays ? children : Array.Empty<Villager>());
            }
        }
    }

    private static ContentmentCauseRow? RowFor(SimConfig config, ContentmentCause cause)
    {
        foreach (ContentmentCauseRow row in config.ContentmentCauses)
        {
            if (row.Cause == cause)
            {
                return row;
            }
        }

        return null;
    }

    /// <summary>The households with somebody alive in them — one pass over the villagers.</summary>
    private static HashSet<int> LivedIn(SimWorld world)
    {
        var lived = new HashSet<int>();
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            if (world.Villagers[i].Alive)
            {
                lived.Add(world.Villagers[i].HouseholdId);
            }
        }

        return lived;
    }
}
