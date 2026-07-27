using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Step 3 of the tick order: households grow.
/// </summary>
/// <remarks>
/// <para>
/// Birth is deliberately <b>conditional on a food surplus</b>. A village that breeds
/// into a famine is not telling a story, it is oscillating — and the resulting deaths
/// would not be traceable to any decision the player made, which fails the legibility
/// non-negotiable. Requiring a full-ish larder first makes population growth a
/// <em>consequence of a good decade</em>, which is exactly the kind of thing the
/// player can read off the log afterwards.
/// </para>
/// <para>
/// Determinism: households are visited in id order and the RNG is drawn from
/// <b>only when a birth actually happens</b>. A draw on every check would make the
/// stream depend on how many households merely considered it, which is the sort of
/// thing that changes when you add an unrelated feature.
/// </para>
/// </remarks>
public sealed class HouseholdSystem : ISimSystem
{
    public string Name => "household";

    public void Execute(SimWorld world)
    {
        SimConfig config = world.Config;

        if (world.Tick == 0UL)
        {
            return;
        }

        // Neighbours help every season. Annually was far too coarse - a household
        // can go from full to empty inside a single winter, so the yearly check
        // arrived months after the funerals.
        if (world.Tick % (ulong)config.TicksPerSeason == 0UL)
        {
            ShareFood(world, config);
        }

        // Births and household formation resolve only on a year boundary - a
        // household does not reconsider four times a day, and it keeps the log to
        // one line per event.
        if (world.Tick % (ulong)config.TicksPerYear != 0UL)
        {
            return;
        }

        int year = world.Clock.Year;

        // Formation next: a couple who move out this year get their own house
        // before anyone considers having children in it.
        FormNewHouseholds(world, config, year);

        // Snapshot the count: newborns are appended, and a baby must not be
        // considered for parenthood on the tick it is born.
        int householdCount = world.Households.Count;

        for (int i = 0; i < householdCount; i++)
        {
            TryBirth(world, world.Households[i], config, year);
        }
    }

    /// <summary>
    /// Households with a surplus give to households that are short.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the sharing policy decision D14 promised alongside per-household
    /// stores, and without it the village dies of a specific and repeatable cause: a
    /// parent dies, and the widowed survivor has to feed the children alone on
    /// declining vigour. One worker cannot support a house that two built. Thirteen
    /// of twenty-four villagers starved that way while their neighbours had food.
    /// </para>
    /// <para>
    /// <b>This is a placeholder for a building.</b> The intended form is a manned
    /// market or food stall that redistributes within its catchment — distribution
    /// as a job someone does, not a rule the world enforces from nowhere (D14,
    /// DESIGN.md §2.2). Keeping it deliberately simple here makes it easy to delete
    /// when the market arrives.
    /// </para>
    /// <para>
    /// Givers and receivers are both walked in household-id order, and each transfer
    /// is capped by what the giver can spare — so no household is ever pushed into
    /// need by its own generosity, and the order of transfers is fixed.
    /// </para>
    /// </remarks>
    private static void ShareFood(SimWorld world, SimConfig config)
    {
        for (int r = 0; r < world.Households.Count; r++)
        {
            Household needy = world.Households[r];
            int need = ShortfallOf(world, needy, config);
            if (need <= 0)
            {
                continue;
            }

            for (int g = 0; g < world.Households.Count && need > 0; g++)
            {
                Household giver = world.Households[g];
                if (giver.Id == needy.Id)
                {
                    continue;
                }

                int spare = SurplusOf(world, giver, config);
                if (spare <= 0)
                {
                    continue;
                }

                int gift = spare < need ? spare : need;
                if (!giver.Stockpile.TryTake(gift))
                {
                    continue;
                }

                needy.Stockpile.Receive(gift, 0, 0);
                need -= gift;

                world.Narrate(
                    $"The {giver.Name} household shared {gift} food with the {needy.Name} household " +
                    $"— {world.Clock.SeasonAndYear()}.");
            }
        }
    }

    /// <summary>How far below a survivable store a household is.</summary>
    private static int ShortfallOf(SimWorld world, Household household, SimConfig config)
    {
        if (world.LivingMembersOf(household) == 0)
        {
            return 0;
        }

        int floor = world.TargetFoodFor(household) * config.SharingNeedPercent / 100;
        return floor - household.Stockpile.Food;
    }

    /// <summary>What a household can give away without going short itself.</summary>
    private static int SurplusOf(SimWorld world, Household household, SimConfig config)
    {
        if (world.LivingMembersOf(household) == 0)
        {
            // A house whose family has died keeps nothing back.
            return household.Stockpile.Food;
        }

        int keep = world.TargetFoodFor(household) * config.SharingKeepPercent / 100;
        return household.Stockpile.Food - keep;
    }

    /// <summary>
    /// Grown, unpaired adults pair off across households and found new homes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without this the village suffocates: children never leave the house they were
    /// born in, every household fills to <c>max_household_size</c>, births stop
    /// permanently, and the settlement dies out with its last generation. That is
    /// not famine - it is a village with nowhere to put the next child.
    /// </para>
    /// <para>
    /// <b>Matching is fully ordered.</b> Candidates are visited in villager-id order,
    /// and each takes the lowest-id eligible partner. No scoring, no randomness, and
    /// no "whoever comes first" - a matching problem is exactly where a tie resolved
    /// by iteration order turns into a desync (spec 4b). It also means the answer to
    /// "why those two?" is one sentence, which the pairing is narrated with.
    /// </para>
    /// <para>
    /// Partners must come from <em>different</em> households, which is the closest
    /// thing to an incest rule this model can express: everyone in a house is either
    /// a parent or a sibling.
    /// </para>
    /// </remarks>
    private static void FormNewHouseholds(SimWorld world, SimConfig config, int year)
    {
        // Snapshot: a villager who pairs this year must not also be considered as
        // someone else's partner later in the same pass.
        int count = world.Villagers.Count;

        for (int i = 0; i < count; i++)
        {
            Villager seeker = world.Villagers[i];
            if (!IsSeekingAHome(seeker, config))
            {
                continue;
            }

            Villager? partner = FindPartner(world, seeker, config, count);
            if (partner is null)
            {
                continue;
            }

            // An empty house is a house. A couple moves into one that has outlived its
            // family rather than felling thirty logs to raise another beside it.
            //
            // Measured, and it was killing the village at about year 125: the
            // settlement held steady at eleven people but had built FIFTEEN houses,
            // and every one of them cost logs that firewood needed. The log pile grew
            // to a thousand, then drained to nothing, and once there were no logs
            // there was no firewood and people froze. A village standing among its own
            // empty houses while it fells more trees to build another is not a hard
            // decision, it is an absurdity.
            Household? standingEmpty = FindAnEmptyHome(world);
            if (standingEmpty is not null)
            {
                MoveInTogether(world, seeker, partner, standingEmpty, config, timber: 0);
                continue;
            }

            // Otherwise a home has to be BUILT. Until the two families between them
            // have the timber, the couple waits - which is what makes cutting wood
            // matter, and ties how fast the village spreads to how it spends its
            // labour.
            if (!TryTakeBuildingTimber(world, seeker, partner, config, out int timber))
            {
                continue;
            }

            Pair(world, seeker, partner, config, timber);
        }
    }

    /// <summary>
    /// A grown, unpaired villager who wants a partner and a home of their own.
    /// </summary>
    /// <remarks>
    /// Public because it is also what the village counts when deciding whether it
    /// needs anyone cutting timber (see <see cref="LabourQuota"/>). One definition of
    /// "waiting for a house", used both by the thing that builds houses and the thing
    /// that staffs the work of building them — two definitions would eventually
    /// disagree, and the village would cut wood for people who were not waiting.
    /// </remarks>
    public static bool IsSeekingAHome(Villager villager, SimConfig config) =>
        villager.Alive
        && !villager.IsPaired
        && villager.LifeStage != LifeStage.Child
        && villager.AgeYears >= config.LeaveHomeAge;

    /// <summary>Lowest-id eligible partner from a different household.</summary>
    private static Villager? FindPartner(SimWorld world, Villager seeker, SimConfig config, int count)
    {
        for (int j = 0; j < count; j++)
        {
            Villager candidate = world.Villagers[j];

            if (candidate.Id == seeker.Id || candidate.HouseholdId == seeker.HouseholdId)
            {
                continue;
            }

            if (IsSeekingAHome(candidate, config))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Draw the timber for a new house — the two parent households first, then the
    /// rest of the village — or take nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All-or-nothing on purpose: half-taken timber would leave the givers poorer with
    /// no house to show for it, and the couple would try again next year and pay
    /// twice.
    /// </para>
    /// <para>
    /// <b>The village makes up the difference, and it has to.</b> Drawing from the two
    /// parent households alone looked right — the families provide for their children
    /// — but timber is cut by whoever lives nearest the stand, who is very often
    /// nobody's parent. So the village would cut wood year after year, pile it in the
    /// woodcutter's own house where it could not be spent, and no home would ever get
    /// built. Every settlement stalled at a handful of houses and aged out without a
    /// single villager starving. Raising a house is communal work; the store it comes
    /// out of is the village's.
    /// </para>
    /// </remarks>
    private static bool TryTakeBuildingTimber(
        SimWorld world, Villager a, Villager b, SimConfig config, out int taken)
    {
        taken = 0;
        if (config.LogsPerHouse == 0)
        {
            return true;
        }

        Household homeA = world.HouseholdOf(a);
        Household homeB = world.HouseholdOf(b);

        if (TotalWood(world) < config.LogsPerHouse)
        {
            return false;
        }

        // Parents first, then everyone else in household-id order, so who paid for a
        // house is a fixed fact rather than an artifact of iteration.
        taken += TakeUpTo(homeA, config.LogsPerHouse - taken);
        taken += TakeUpTo(homeB, config.LogsPerHouse - taken);

        for (int i = 0; i < world.Households.Count && taken < config.LogsPerHouse; i++)
        {
            Household other = world.Households[i];
            if (other.Id == homeA.Id || other.Id == homeB.Id)
            {
                continue;
            }

            taken += TakeUpTo(other, config.LogsPerHouse - taken);
        }

        return taken >= config.LogsPerHouse;
    }

    private static int TakeUpTo(Household household, int wanted)
    {
        if (wanted <= 0)
        {
            return 0;
        }

        int available = household.Stockpile.Logs < wanted ? household.Stockpile.Logs : wanted;
        return household.Stockpile.TryTakeLogs(available) ? available : 0;
    }

    /// <summary>Every stick of timber the village has between it.</summary>
    private static int TotalWood(SimWorld world)
    {
        int total = 0;
        for (int i = 0; i < world.Households.Count; i++)
        {
            total += world.Households[i].Stockpile.Logs;
        }

        return total;
    }

    /// <summary>The lowest-id house nobody lives in any more, or null.</summary>
    /// <remarks>Lowest id, so which house a couple takes is a fact about the village
    /// rather than about iteration — and it means the oldest empty home fills first,
    /// which is also how a village would actually do it.</remarks>
    private static Household? FindAnEmptyHome(SimWorld world)
    {
        for (int i = 0; i < world.Households.Count; i++)
        {
            if (world.LivingMembersOf(world.Households[i]) == 0)
            {
                return world.Households[i];
            }
        }

        return null;
    }

    private static void Pair(SimWorld world, Villager a, Villager b, SimConfig config, int timber)
    {
        GridPos home = Household.PlacementFor(
            world.Households.Count, config.HomeX, config.HomeY, config.HouseholdSpacing);

        var household = new Household
        {
            Id = NextHouseholdId(world),
            Name = config.HouseholdNames[world.Households.Count % config.HouseholdNames.Count],
            HomePosition = home,
        };

        world.Households.Add(household);
        MoveInTogether(world, a, b, household, config, timber);
    }

    private static void MoveInTogether(
        SimWorld world, Villager a, Villager b, Household household, SimConfig config, int timber)
    {
        Household oldHome = world.HouseholdOf(a);
        Household partnerHome = world.HouseholdOf(b);
        GridPos home = household.HomePosition;

        // Each family sends their child off with a share of the larder. Without it a
        // new household starts on empty and can be wiped out by its first winter
        // before anyone has foraged a thing - a death with no decision behind it,
        // which is exactly what the legibility non-negotiable rules out.
        int dowry = TakeDowry(oldHome, config) + TakeDowry(partnerHome, config);

        MoveIn(world, a, household, home);
        MoveIn(world, b, household, home);

        a.PartnerId = b.Id;
        b.PartnerId = a.Id;

        // A dowry is goods changing hands, not goods produced.
        household.Stockpile.Receive(dowry, 0, 0);

        world.Narrate(timber > 0
            ? $"{a.Name} of the {oldHome.Name} household and {b.Name} of the {partnerHome.Name} " +
              $"built a home of their own - the {household.Name} household, " +
              $"{world.Clock.SeasonAndYear()}. {timber} timber and {dowry} food between them."
            : $"{a.Name} of the {oldHome.Name} household and {b.Name} of the {partnerHome.Name} " +
              $"took over the empty {household.Name} house - {world.Clock.SeasonAndYear()}. " +
              $"{dowry} food between them, and no trees felled for it.");
    }

    /// <summary>
    /// Food a parent household gives a departing child, capped so a generous family
    /// cannot starve itself sending someone away.
    /// </summary>
    private static int TakeDowry(Household from, SimConfig config)
    {
        int share = from.Stockpile.Food * config.DowryPercent / 100;
        if (share > config.StockpileTarget)
        {
            share = config.StockpileTarget;
        }

        return from.Stockpile.TryTake(share) ? share : 0;
    }

    private static void MoveIn(SimWorld world, Villager villager, Household household, GridPos home)
    {
        world.HouseholdOf(villager).RemoveMember(villager.Id);
        villager.HouseholdId = household.Id;
        villager.Position = home;
        household.AddMember(villager.Id);
    }

    private static int NextHouseholdId(SimWorld world)
    {
        int max = 0;
        for (int i = 0; i < world.Households.Count; i++)
        {
            if (world.Households[i].Id > max)
            {
                max = world.Households[i].Id;
            }
        }

        return max + 1;
    }

    private static void TryBirth(SimWorld world, Household household, SimConfig config, int year)
    {
        if (!IsReadyForAChild(world, household, config, year))
        {
            return;
        }

        // Draw order is part of the seed contract: name, then lifespan. Same order
        // as founding, so there is one rule rather than two.
        string name = world.DrawUnusedName();

        int lifespan = config.LifespanYearsBase;
        if (config.LifespanYearsVariance > 0)
        {
            lifespan += world.Rng.NextInt(-config.LifespanYearsVariance, config.LifespanYearsVariance + 1);
        }

        var child = new Villager
        {
            Id = NextVillagerId(world),
            Name = name,
            LifespanYears = lifespan,
            BirthYear = year,
            AgeYears = 0,
            LifeStage = LifeStage.Child,
            HouseholdId = household.Id,
            Position = household.HomePosition,
        };

        household.AddMember(child.Id);
        household.LastBirthYear = year;
        world.Villagers.Add(child);

        world.Narrate(
            $"{name} was born to the {household.Name} household — {world.Clock.SeasonAndYear()}. " +
            $"The village is now {world.Population}.");
    }

    /// <summary>
    /// Whether a household will have a child this year: two fertile adults, room in
    /// the house, food in the store, and enough time since the last birth.
    /// </summary>
    /// <remarks>
    /// A ranked list of plain conditions rather than a fertility score, for the same
    /// reason the behaviour system is an if-chain: the player has to be able to be
    /// told why, in one sentence.
    /// </remarks>
    public static bool IsReadyForAChild(SimWorld world, Household household, SimConfig config, int year)
    {
        if (household.MemberIds.Count >= config.MaxHouseholdSize)
        {
            return false;
        }

        if (household.LastBirthYear != 0 && year - household.LastBirthYear < config.BirthIntervalYears)
        {
            return false;
        }

        if (household.Stockpile.Food < config.BirthFoodThreshold)
        {
            return false;
        }

        return HasFertileCouple(world, household, config);
    }

    /// <summary>
    /// Whether the household contains a fertile <em>couple</em> - two partners, not
    /// merely two adults.
    /// </summary>
    /// <remarks>
    /// Counting any two fertile adults would let siblings breed in the parental home,
    /// and would let the village grow without ever forming a new household - which
    /// hides the very problem household formation exists to solve.
    /// </remarks>
    private static bool HasFertileCouple(SimWorld world, Household household, SimConfig config)
    {
        for (int i = 0; i < household.MemberIds.Count; i++)
        {
            Villager? member = world.FindVillager(household.MemberIds[i]);
            if (member is null || !IsFertile(member, config) || !member.IsPaired)
            {
                continue;
            }

            Villager? partner = world.FindVillager(member.PartnerId);
            if (partner is not null
                && partner.HouseholdId == household.Id
                && IsFertile(partner, config))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFertile(Villager villager, SimConfig config) =>
        villager.Alive
        && villager.AgeYears >= config.FertilityMinAge
        && villager.AgeYears <= config.FertilityMaxAge;

    /// <summary>Ids are never reused, so the dead keep theirs and the log stays honest.</summary>
    private static int NextVillagerId(SimWorld world)
    {
        int max = 0;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            if (world.Villagers[i].Id > max)
            {
                max = world.Villagers[i].Id;
            }
        }

        return max + 1;
    }
}
