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
            if (!TryTakeBuildingTimber(world, config, out int timber))
            {
                continue;
            }

            try
            {
                Pair(world, seeker, partner, config, timber);

                // Somebody got their home, so the village is no longer asking. Cleared
                // here rather than by the view noticing free land: the request means "a
                // couple is waiting", and the honest answer to "are they still waiting?"
                // is whether one of them has since moved out.
                world.NeedsMoreResidentialLand = false;
            }
            catch (Household.NoRoomToBuildException noRoom)
            {
                // The painted land is full. The timber is already out of the shed, so it
                // goes back to the nearest one with room rather than evaporating —
                // conservation is not negotiable just because the code took an unusual
                // branch.
                StoreBuilding? shed = world.NearestStore(
                    seeker.Position, StoreKind.Shed, static store => !store.Store.IsFull);
                shed?.Store.ReceiveLogs(timber);

                // AND THE VILLAGE ASKS FOR MORE LAND. This is the other half of the
                // brush (D42): the game says when a decision is due rather than
                // expecting the player to notice a couple quietly not moving out.
                // Narrated once, so it reads as people waiting rather than as an error
                // repeating every year until somebody dies.
                if (!world.NeedsMoreResidentialLand)
                {
                    world.NeedsMoreResidentialLand = true;
                    world.Narrate(
                        $"{seeker.Name} and {partner.Name} want a home of their own and there is " +
                        $"nowhere to put one — {noRoom.Message}. The village needs somewhere new " +
                        $"to build. {world.Clock.SeasonAndYear()}.");
                }

                continue;
            }
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
    /// Draw the timber for a new house out of the village's shed, or take nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All-or-nothing on purpose: half-taken timber would leave the shed poorer with
    /// no house to show for it, and the couple would try again next year and pay
    /// twice.
    /// </para>
    /// <para>
    /// <b>This used to be a sweep across every household's private pile</b>, added
    /// because timber is cut by whoever lives nearest the stand — very often nobody's
    /// parent — so drawing only from the two parent households meant the village cut
    /// wood year after year, piled it where it could not be spent, and never built
    /// anything (D25). The sweep was a shed in all but name: a village-wide store that
    /// could not be seen, sited, or reasoned about. Now it is a building, and this is
    /// one line.
    /// </para>
    /// </remarks>
    private static bool TryTakeBuildingTimber(SimWorld world, SimConfig config, out int taken)
    {
        taken = 0;
        if (config.LogsPerHouse == 0)
        {
            return true;
        }

        // Drawn from EVERY shed, a little from each if need be. A house is paid for by
        // the whole village (D25), so which building the logs happen to sit in should
        // not decide whether it can be built — and with several sheds, insisting on
        // one would have stalled a village that had the timber twice over.
        if (world.LogsInSheds() < config.LogsPerHouse)
        {
            return false;
        }

        int remaining = config.LogsPerHouse;
        for (int i = 0; i < world.StoreBuildings.Count && remaining > 0; i++)
        {
            StoreBuilding store = world.StoreBuildings[i];
            if (store.Kind != StoreKind.Shed)
            {
                continue;
            }

            int fromHere = Math.Min(remaining, store.Store.Logs);
            if (fromHere > 0 && store.Store.TryTakeLogs(fromHere))
            {
                remaining -= fromHere;
            }
        }

        taken = config.LogsPerHouse - remaining;
        return remaining == 0;
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
        // Chosen with regard to where the work is, rather than the next spot on a
        // spiral (see Household.ChooseSite). This is what makes a generated valley
        // habitable by construction instead of by a lucky ring radius.
        GridPos home = Household.ChooseSite(world, world.Map.FoundingSite);

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

        // A couple only ever moves into a house that exists — either one standing empty or
        // one just raised — so this is not the founding case and the position is real. Said
        // out loud rather than swallowed, per METHODOLOGY §4: a null here would mean somebody
        // had been married into thin air.
        GridPos home = household.HomePosition
            ?? throw new InvalidOperationException(
                $"The {household.Name} household was given a pair of residents with no house " +
                "to put them in.");

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

            // Born at home, and there is always one: IsReadyForAChild refuses a household
            // with no roof (D71), so a child cannot be born into the open.
            Position = household.HomePosition
                ?? throw new InvalidOperationException(
                    $"A child was born to the {household.Name} household, which has no house."),
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
        // ROOM IN THE HOUSE MEANS LIVING PEOPLE UNDER THE ROOF.
        //
        // This read MemberIds.Count, which is everyone who has EVER belonged to this
        // household: RemoveMember is called when somebody moves out and never when
        // somebody dies, so the dead stay on the list forever. A household that had
        // seen seven people pass through it was therefore permanently barred from
        // having another child — even with a young couple in it and every other
        // condition met, and even once all seven were in the ground.
        //
        // That is what was killing every village. Households ratchet one way into
        // sterility as their dead accumulate, so a settlement always dies out about a
        // century in, whatever its food or fuel is doing — measured extinct by year
        // 180 in every configuration, including with storage capacity switched off.
        // It reads as a slow demographic decline, which is why it survived so long:
        // it looks exactly like the population wave it happens to coincide with.
        //
        // Every other occupancy question in the sim already asks LivingMembersOf. This
        // was the one place that did not.
        if (world.LivingMembersOf(household) >= config.MaxHouseholdSize)
        {
            return false;
        }

        // NO ROOF, NO CHILDREN (Joe's call, D71). A homeless family has neither a larder
        // nor a hearth, so on the gates below they could never qualify anyway — but saying
        // it here rather than letting it fall out of two arithmetic checks is the
        // difference between a rule and an accident, and this one is load-bearing: it is
        // what makes the first house urgent at the founding rather than optional.
        if (!household.HasHome)
        {
            return false;
        }

        if (household.LastBirthYear != 0 && year - household.LastBirthYear < config.BirthIntervalYears)
        {
            return false;
        }

        // A share of THIS household's own target, which scales with how many mouths
        // it already has. A flat number let a family of seven breed on a tenth of a
        // full larder; scaling it is what stops the village outrunning its food.
        if (household.Stockpile.Food < world.TargetFoodFor(household) * config.BirthFoodPercent / 100)
        {
            return false;
        }

        // AND the village as a whole has to be in surplus, not just this family.
        //
        // The granary broke the household gate without anyone noticing. Before it, a
        // larder reflected what that family could actually produce, so gating births
        // on it made growth self-limiting: households stopped having children before
        // they starved. With a granary, every larder is topped up from the village
        // store, so the gate reads "comfortable" right up until the moment the store
        // runs dry. Measured: the village bred to ninety-two, outran what its forage
        // sites could produce, and thirty-three people starved on the way back down.
        //
        // So the question moved to where the answer now lives. A village with a shared
        // store can only afford a child if the STORE can afford one.
        // Every granary the village has (D38) — and this is THE call site that made the
        // singleton seam worth fixing before placement shipped. A second granary the
        // birth gate could not see would have been a building the player paid for that
        // did nothing at all.
        if (world.FoodInGranaries() < world.TargetFoodForTheGranary() * config.BirthFoodPercent / 100)
        {
            return false;
        }

        // AND a winter's fuel, on the same terms.
        //
        // Every constraint in this sim has, in turn, been the one the village grew
        // until it hit and then died on: the forage sites, the log pile, the food
        // economy. Each was fixed and the settlement simply grew to the next one.
        // Food is the only one that ever SELF-LIMITED, because births are gated on it
        // — so the village stopped having children before it starved rather than
        // after.
        //
        // Fuel had no such brake. The village grew to forty-eight people, outran what
        // the woodcutter's hut was derived to heat, and sixty-seven of them froze.
        // Gating births on firewood too gives cold the same brake hunger already has,
        // and it is the more diegetic rule of the two: you do not have a child you
        // cannot keep warm.
        int firewoodWanted =
            VillageEconomy.FirewoodStoreWantedPerHousehold(config) * config.BirthFoodPercent / 100;
        if (config.FirewoodPerWinterDay > 0 && household.Stockpile.Firewood < firewoodWanted)
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
