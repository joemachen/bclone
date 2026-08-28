using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
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
        // THE ROOFLESS ARE ANSWERED EVERY DAY, NOT EVERY NEW YEAR (D72).
        //
        // Everything else here is deliberately annual — a household does not reconsider
        // having children four times a day, and the annual cadence keeps the log to one
        // line per event. Housing a family that is standing in the open cannot wait that
        // long: the founders arrive in spring, the next year boundary is after winter, and
        // measured, they freeze to death before the pass that would have housed them ever
        // runs. A family in the open does not wait for New Year's Day to be given the
        // house the village can already afford.
        //
        // Cheap to run: it walks the households and leaves immediately once they all have
        // roofs, which is every tick of an established village.
        if (world.Tick % (ulong)config.TicksPerDay == 0UL)
        {
            HouseTheRoofless(world, config);
        }

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
    /// <summary>
    /// Raise a house for a family that has one already and lives in the open (D72).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The path the cold start needed and did not have.</b> Until D70 there was exactly
    /// one way for a house to get built — an unpaired adult finds a partner and the pair
    /// moves out — so <em>wanting a house</em> and <em>forming a couple</em> were the same
    /// event. The founders are already paired and already a household; they simply have no
    /// roof, which was a state the sim could not previously represent and therefore could
    /// not answer.
    /// </para>
    /// <para>
    /// <b>Found by playing it, not by reasoning about it.</b> Joe's first cold start: all
    /// four founders froze in winter 1 without a single log being cut, and the tree stand
    /// read <em>"the village wants 0 on this kind of work"</em> — because
    /// <c>ForestersWanted</c> counted only unpaired seekers, and because even with timber
    /// nothing would have spent it. Two halves of one gap.
    /// </para>
    /// <para>
    /// Ordered before <see cref="FormNewHouseholds"/>: a family in the open has a better
    /// claim on the village's timber than a couple wanting to move out of a house that
    /// already exists.
    /// </para>
    /// </remarks>
    private static void HouseTheRoofless(SimWorld world, SimConfig config)
    {
        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            if (household.HasHome || world.LivingMembersOf(household) == 0)
            {
                continue;
            }

            // An empty house is a house — the same rule new couples get, and for the same
            // reason: standing among your own empty homes while felling more trees is an
            // absurdity, not a hard decision.
            Household? standingEmpty = FindAnEmptyHome(world);
            if (standingEmpty is not null)
            {
                household.HomePosition = standingEmpty.HomePosition;
                standingEmpty.HomePosition = null;
                world.Narrate(
                    $"The {household.Name} household moved into the empty house at "
                    + $"{household.HomePosition} — {world.Clock.SeasonAndYear()}.", LogCategory.Life);
                continue;
            }

            // ⭐ A HOUSE IS MARKED OUT, NOT CONJURED (D102). It used to take the timber
            // straight out of the stores and set HomePosition in the same tick, which is the
            // inconsistency `specs/cold-start.md §7.1b` has been carrying since Joe watched
            // it: "immediate builds, not a visual timed thing like other buildings."
            //
            // No timber is taken here now. A builder hauls it, like every other site — which
            // is D43's rule about construction not being a purchase, finally applied to the
            // building the village raises most often.
            if (world.HomeSiteFor(household.Id) is not null)
            {
                // Already being built for them. Asked rather than flagged, so a cancelled
                // site cannot leave a family waiting for a house forever.
                continue;
            }

            try
            {
                world.MarkHome(household.Id, Household.ChooseSite(world, world.Map.FoundingSite));
                world.NeedsMoreResidentialLand = false;
            }
            catch (Household.NoRoomToBuildException)
            {
                if (!world.NeedsMoreResidentialLand)
                {
                    world.NeedsMoreResidentialLand = true;
                    world.Narrate(
                        $"The {household.Name} household has nowhere to build — "
                        + "paint some land for houses.", LogCategory.Warning);
                }
            }
        }
    }

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
                MoveInTogether(world, seeker, partner, standingEmpty, config);
                continue;
            }

            // Otherwise a home has to be BUILT — marked out and raised by somebody, like
            // every other building (D102). The couple pairs off now and waits for the roof,
            // which is what ties how fast the village spreads to how it spends its labour.
            //
            // No timber is drawn here any more: a builder hauls it to the site.
            try
            {
                Pair(world, seeker, partner, config);

                // Somebody got their home, so the village is no longer asking. Cleared
                // here rather than by the view noticing free land: the request means "a
                // couple is waiting", and the honest answer to "are they still waiting?"
                // is whether one of them has since moved out.
                world.NeedsMoreResidentialLand = false;
            }
            catch (Household.NoRoomToBuildException noRoom)
            {
                // The painted land is full. Nothing to hand back — no timber is drawn until
                // a builder carries it to the site (D102).

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
                        $"to build. {world.Clock.SeasonAndYear()}.", LogCategory.Warning);
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

    // `TryTakeBuildingTimber` WAS HERE AND IS GONE (D102). It drew a house's timber straight
    // out of the village's stores in one tick, which is what made a house instant — and it was
    // itself the fix for something worse (D25's sweep across every household's private pile).
    //
    // A builder hauls the logs to the site now, exactly as they do for a granary, so the
    // question "where does the timber come from?" has one answer for every building rather
    // than two. Deleted rather than left unused, per D57: a method nobody calls is a method
    // somebody re-reads and believes.
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

    private static void Pair(SimWorld world, Villager a, Villager b, SimConfig config)
    {
        // Chosen with regard to where the work is, rather than the next spot on a
        // spiral (see Household.ChooseSite). This is what makes a generated valley
        // habitable by construction instead of by a lucky ring radius. It throws if the
        // painted land is full, BEFORE the household exists — so a couple is never left
        // half-formed by a site that could not be found.
        GridPos site = Household.ChooseSite(world, world.Map.FoundingSite);

        var household = new Household
        {
            Stockpile = world.NewStockpile(),
            Id = NextHouseholdId(world),
            Name = config.HouseholdNames[world.Households.Count % config.HouseholdNames.Count],

            // ⭐ NO ROOF YET (D102). The house is marked out below and somebody has to build
            // it; until then this couple is homeless, which is a state the sim has had since
            // the cold start (`specs/cold-start.md §3`) and which D71 already attaches a rule
            // to: no roof, no children.
            HomePosition = null,
        };

        world.Households.Add(household);
        MoveInTogether(world, a, b, household, config);
        world.MarkHome(household.Id, site);
    }

    private static void MoveInTogether(
        SimWorld world, Villager a, Villager b, Household household, SimConfig config)
    {
        Household oldHome = world.HouseholdOf(a);
        Household partnerHome = world.HouseholdOf(b);

        // A house if there is one — a couple taking over one standing empty — and null if
        // theirs is still being built (D102). Homeless is a real state, not an error.
        GridPos? home = household.HomePosition;

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
        household.Stockpile.Receive(Goods.Food, dowry);

        world.Narrate(home is null
            ? $"{a.Name} of the {oldHome.Name} household and {b.Name} of the {partnerHome.Name} " +
              $"started the {household.Name} household - {world.Clock.SeasonAndYear()}. " +
              $"{dowry} food between them, and a house being raised for them."
            : $"{a.Name} of the {oldHome.Name} household and {b.Name} of the {partnerHome.Name} " +
              $"took over the empty {household.Name} house - {world.Clock.SeasonAndYear()}. " +
              $"{dowry} food between them, and no trees felled for it.", LogCategory.Life);
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

        return from.Stockpile.TryTake(Goods.Food, share) ? share : 0;
    }

    private static void MoveIn(
        SimWorld world, Villager villager, Household household, GridPos? home)
    {
        world.HouseholdOf(villager).RemoveMember(villager.Id);
        villager.HouseholdId = household.Id;

        // Only if there is a door to stand at. A couple whose house is still being built
        // stays where they are and rests wherever RestingPlaceOf sends them (D102).
        if (home is GridPos doorstep)
        {
            villager.Position = doorstep;
        }

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

        // Draw order is part of the seed contract: name, then lifespan, then rhythm. Same
        // order as founding, so there is one rule rather than two.
        string name = world.DrawUnusedName();

        int lifespan = config.LifespanYearsBase;
        if (config.LifespanYearsVariance > 0)
        {
            lifespan += world.Rng.NextInt(-config.LifespanYearsVariance, config.LifespanYearsVariance + 1);
        }

        // ⭐ Drawn at birth and spent when their working life begins (§3.5, D190) — a child who
        // burned it during infancy would be staggered against nobody.
        int rhythm = config.SeededRhythm && config.TicksPerDay > 1
            ? world.Rng.NextInt(0, config.TicksPerDay)
            : 0;

        var child = new Villager
        {
            Id = NextVillagerId(world),
            Name = name,
            LifespanYears = lifespan,
            Rhythm = rhythm,
            Carried = world.NewStockpile(),

            // ⭐ And their hunger a little apart — see the founding path for why the action
            // stagger alone leaves two siblings eating on the same tick for ever.
            Hunger = rhythm,
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
            $"The village is now {world.Population}.", LogCategory.Life);
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
        // ⭐ AND IT ASKS THE HOUSE, NOT A GLOBAL NUMBER (Joe, D153). *"Put a cap size on family
        // homes to limit the number of children a couple can have — eventually an unlock/tech
        // that allows for larger homes."* One house kind today, so this answers exactly what
        // the constant did; the point is that the question is now asked of the building, so a
        // larger dwelling is one arm in `HouseholdCapacity` rather than a hunt through callers.
        //
        // ⭐ IT ALSO MATTERS AGAIN, WHICH IT HAD STOPPED DOING. At seven this refused only
        // 1-3% of household-years while the granary gate refused 42-70% — the cap was not a
        // lever, it was scenery. At five it binds, and the shipped village stops dying out
        // (300 unattended years: final 0 at seven, final 20 at five, nobody starving either way).
        if (world.LivingMembersOf(household)
            >= VillageEconomy.HouseholdCapacity(BuildingKind.Home, config))
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

        // ⛔ THE FAMILY'S OWN LARDER WAS CHECKED HERE, AND IT IS DELETED (Joe, D153).
        //
        // The rule was: *a share of THIS household's own target, which scales with how many
        // mouths it already has* — a flat number let a family of seven breed on a tenth of a
        // full larder, so scaling it was what stopped the village outrunning its food.
        //
        // **It was already all but toothless, and the granary gate below is why.** Before
        // stores existed a larder reflected what that family could actually produce, so gating
        // births on it was self-limiting. With a granary every larder is topped up from the
        // village store, so this read "comfortable" until the store ran dry — which is exactly
        // what the gate below was added to catch.
        //
        // ⭐ MEASURED BEFORE REMOVING IT, over four runs of 150 and 300 years on both configs:
        // this term refused **6–10%** of household-years while the granary term refused
        // **42–70%**. Taking it out moved the fixture's peak from 38 to 37 and the shipped
        // village's from 31 to 27, and **nobody starved in any arm, before or after.**
        //
        // ⚠️ THE DISASTER IT WAS ADDED FOR IS NOW RECORDED IN `DESIGN.md` D153 AND NOWHERE
        // ELSE, so it is written here too: the village once *"bred to ninety-two, outran what
        // its forage sites could produce, and thirty-three people starved on the way back
        // down."* That was an economy with no granary gate. This comment is the only surviving
        // trace of it besides the decision entry — do not delete it with the code.

        // The village as a whole has to be in surplus. **Since D153 this is the only food
        // brake on births**, which also means `birth_food_percent` now has exactly one
        // meaning — it is read here and by `VillageEconomy.PopulationCeiling`, so the derived
        // ceiling is no longer an approximation of the gate: it *is* the gate.
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
        // ⭐ AND IT ASKS WHAT THE VILLAGE *HOLDS*, NOT WHAT IS IN THE GRANARIES (D161). Those
        // were the same number until the farm's local store landed; a village whose harvest
        // sat at the farm would have read zero here and stopped having children, which is
        // D155's symptom arriving from a new direction. See SimWorld.FoodTheVillageHolds.
        if (world.FoodTheVillageHolds() < world.TargetFoodForTheGranary() * config.BirthFoodPercent / 100)
        {
            return false;
        }

        // ⛔ AND A WINTER'S FUEL WAS CHECKED HERE, AND IT IS DELETED TOO (Joe, D153).
        //
        // The rule was *you do not have a child you cannot keep warm* — the more diegetic of
        // the two, and it gave cold the same brake hunger already had. **Its removal is the
        // sharper half of D153, because unlike food there is no village-wide backstop**: cold
        // can now kill a village that outgrows its fuel, and Joe took that deliberately.
        //
        // ⭐ MEASURED, AND IT WAS ALMOST NEVER THE THING STOPPING A BIRTH: this term refused
        // **0–1%** of household-years across 150- and 300-year runs on both configs, and
        // **nobody froze in any arm, before or after removing it.** The disaster it was added
        // for belongs to an older fuel economy.
        //
        // ⚠️ AND A VILLAGE-WIDE REPLACEMENT WAS CONSIDERED AND REFUSED, on the design's own
        // grounds rather than on taste. `VillageEconomy.ShedCapacity` already rules it out by
        // name: *"Food is what regulates the village… the shed binding as well would mean two
        // constraints fighting for the same job, and the player could not tell which one was
        // stopping them — which is non-negotiable 1 failing."* `SimWorld.FirewoodInSheds()` is
        // what such a check would read, if this is ever revisited.
        //
        // ⚠️ THE DISASTER IT WAS ADDED FOR IS RECORDED IN `DESIGN.md` D153 AND NOWHERE ELSE,
        // so it is written here too: the village once *"grew to forty-eight people, outran what
        // the woodcutter's hut was derived to heat, and sixty-seven of them froze."*

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
