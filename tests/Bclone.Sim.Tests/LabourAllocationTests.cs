using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The village-level labour allocator — <c>specs/labour-allocation.md</c>.
/// </summary>
/// <remarks>
/// One test per item in spec §7, plus the Definition of Done in §8. The spec exists
/// because three improvised attempts each looked correct and each broke the village,
/// and every one of them was caught by a test rather than by reading the code — so
/// these are the point of the exercise, not a formality.
/// </remarks>
public sealed class LabourAllocationTests
{
    private readonly ITestOutputHelper _output;

    public LabourAllocationTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Build(SimConfig config, ulong? seed = null) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed);

    /// <summary>
    /// Run whole years, then one more tick, so the last thing to have happened is a
    /// reshuffle.
    /// </summary>
    /// <remarks>
    /// Several of the invariants below only hold immediately after a pass — between
    /// passes a villager can die, or a household can go hungry, and the world drifts
    /// out of agreement with the allocation until the next one. That drift is the
    /// design (jobs are not reconsidered four times a day), so the tests have to be
    /// honest about when they look.
    /// </remarks>
    private static SimLoop RunToAReshuffle(SimConfig config, int years)
    {
        SimLoop loop = Build(config);
        loop.Step((config.TicksPerYear * years) + 1);
        return loop;
    }

    // ---------------------------------------------------------------
    //  §7 — Nobody is sent past a nearer opening
    // ---------------------------------------------------------------

    [Fact]
    public void NobodyIsSentPastANearerOpening()
    {
        // The failure mode of the even-split attempt (spec §3), asserted directly:
        // splitting demand evenly across sites FORCES villagers to distant sites
        // rather than letting proximity sort them, so the near patch takes one person
        // and the next villager is sent across the valley to starve beside a patch
        // they are not permitted to work.
        SimLoop loop = RunToAReshuffle(Config, 60);
        SimWorld world = loop.World;

        foreach (Villager villager in world.Villagers)
        {
            if (!villager.HasJob)
            {
                continue;
            }

            Workplace held = world.FindWorkplace(villager.WorkplaceId)!;
            int heldCost = LabourAllocator.CostBetween(world, villager, held);

            foreach (Workplace other in world.Workplaces)
            {
                if (other.Id == held.Id || other.Kind != held.Kind || other.IsFull)
                {
                    continue;
                }

                int otherCost = LabourAllocator.CostBetween(world, villager, other);
                Assert.False(
                    otherCost < heldCost && otherCost != TravelCostField.Unreachable,
                    $"{villager.Name} walks to {held.Name} ({heldCost / 10} tiles) past " +
                    $"{other.Name} ({otherCost / 10} tiles), which had room.");
            }
        }
    }

    // ---------------------------------------------------------------
    //  §7 — Quotas are respected
    // ---------------------------------------------------------------

    [Fact]
    public void TheVillageNeverSparesMoreHandsForTimberThanItCan()
    {
        // The quota's real bite is on the timber side: nobody is spared for building
        // until everybody is fed. Checked immediately after each year's reshuffle,
        // for a century and a half.
        SimConfig config = Config;
        SimLoop loop = Build(config);

        for (int year = 1; year <= 150; year++)
        {
            loop.Step(config.TicksPerYear);
            loop.StepOnce();

            LabourQuota quota = LabourQuota.For(loop.World);
            int cutting = CountWorking(loop.World, JobKind.Forester);
            int sparable = System.Math.Max(0, quota.Hands - quota.ForagersToFeedEveryone);

            Assert.True(cutting <= sparable,
                $"Year {year}: {cutting} cutting timber, but the village could only spare " +
                $"{sparable} — {quota}");
        }
    }

    [Fact]
    public void AVillageShortOfHandsPutsAllOfThemOnFood()
    {
        // §4a's one-sentence policy: a village short of hands feeds itself before it
        // builds. Stated as a property of the quota rather than of a run, so it
        // cannot pass by accident of tuning.
        SimLoop loop = Build(Config);
        loop.StepOnce();

        LabourQuota quota = LabourQuota.For(loop.World);
        _output.WriteLine(quota.ToString());

        // A village founded with an empty larder has no spare hands by definition.
        Assert.True(LabourQuota.VillageIsShortOfFood(loop.World));
        Assert.Equal(0, quota.Foresters);
        Assert.Equal(quota.Hands, quota.Foragers);
    }

    [Fact]
    public void AFedVillageWithSomeoneWaitingForAHouseCutsTimber()
    {
        // The other half of the same rule — a policy that only ever says "no" is not
        // a policy, it is a wall. Both conditions have to hold: food in the store,
        // and an actual use for the wood. Nobody cuts timber for its own sake.
        SimConfig config = Config;
        SimLoop loop = Build(config);

        // Sampled per season: a year boundary is the one instant where last year's
        // cutting is finished and this year's has not been decided.
        LabourQuota quota = default;
        for (int season = 0; season < 40 * 4; season++)
        {
            loop.Step(config.TicksPerSeason);

            foreach (Household household in loop.World.Households)
            {
                household.Stockpile.Add(Goods.Food, loop.World.TargetFoodFor(household));
            }

            quota = LabourQuota.For(loop.World);
            if (quota.Foresters > 0)
            {
                _output.WriteLine($"{loop.World.Clock.SeasonAndYear()}: {quota}");
                break;
            }
        }

        Assert.False(LabourQuota.VillageIsShortOfFood(loop.World));
        Assert.True(quota.Foresters > 0, "A fed village with couples waiting should build.");
        Assert.Equal(quota.Hands, quota.Foragers + quota.Foresters + quota.Woodcutters);
    }

    [Fact]
    public void NobodyCutsTimberTheVillageHasNoUseFor()
    {
        // The timber quota is derived the same way the forager quota is: from what
        // the work is FOR. Right now wood buys houses, so a village whose woodpile
        // already covers the next home wants nobody at the stand — however much food
        // is in the store, and however many hands are going spare. Sparing every hand
        // food did not need put HALF a founding village on the tree stand, and it
        // oscillated for a century and died.
        SimLoop loop = Build(Config);
        loop.StepOnce();

        foreach (Household household in loop.World.Households)
        {
            household.Stockpile.Add(Goods.Food, loop.World.TargetFoodFor(household) * 10);
            household.Stockpile.Add(Goods.Logs, Config.LogsPerHouse * 10);
        }

        Assert.False(LabourQuota.VillageIsShortOfFood(loop.World));
        Assert.Equal(0, LabourQuota.ForestersWanted(loop.World));
    }

    // ---------------------------------------------------------------
    //  §7 — Local capacity is respected
    // ---------------------------------------------------------------

    [Fact]
    public void NoSiteEverExceedsItsOwnCapacity()
    {
        // Posed tight so the rule has something to bind on. `forage_site_capacity` was one
        // of these levers and is retired — a gatherer's hut prices its own seats from its
        // ring — so the two typed capacities left do the same job.
        SimLoop loop = Build(Config with { ForesterHutCapacity = 1, WoodcutterHutCapacity = 1 });
        loop.Step(Config.TicksPerYear * 60);

        foreach (Workplace workplace in loop.World.Workplaces)
        {
            Assert.True(workplace.WorkerIds.Count <= workplace.Capacity,
                $"{workplace.Name} has {workplace.WorkerIds.Count} people in room for " +
                $"{workplace.Capacity}.");
        }
    }

    // ---------------------------------------------------------------
    //  §7 — Nobody is ever given work they cannot walk to
    // ---------------------------------------------------------------

    /// <summary>
    /// The fence is gone, and <b>this is what replaced it</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This was <c>NoAssignmentIsEverOutsideAWorkplacesCatchment</c>, run at a deliberately
    /// tight five-tile catchment. Catchment is deleted (`forests-and-gathering.md §3`), so
    /// the old claim is not merely unenforced — <b>it is no longer something the design
    /// wants to be true</b>. A long walk is a mistake the player is now allowed to make.
    /// </para>
    /// <para>
    /// <b>The claim that survives is the one about water</b> (D40): however far anybody is
    /// sent, there must be a walk that gets them there. Losing this is D110's seed 11, where
    /// a village spent a century walking toward a place it could never arrive at.
    /// </para>
    /// </remarks>
    [Fact]
    public void NobodyIsEverGivenWorkTheyCannotWalkTo()
    {
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerYear * 60);

        foreach (Villager villager in loop.World.Villagers)
        {
            if (!villager.HasJob)
            {
                continue;
            }

            Workplace workplace = loop.World.FindWorkplace(villager.WorkplaceId)!;
            Assert.True(LabourAllocator.CanReach(loop.World, villager, workplace),
                $"{villager.Name} holds a job at {workplace.Name} with no way to walk there.");
        }
    }

    // ⛔ A GUARD I WROTE AND THEN DELETED, BECAUSE IT PASSED BY LUCK.
    //
    // It asserted that nobody ever ends up walking further than the food budget, having
    // measured the furthest commute at year 60 as **three tiles against a budget of seven**.
    // That measurement is real and is why deleting the fence was safe — cost-first matching
    // plus homes sited with regard to work (D18) keeps people close without anything
    // forbidding anything.
    //
    // **But it is not an invariant, and the test below proves it in the same run:** at a
    // reshuffle boundary, six villagers are beyond the budget and are told so. Both are true;
    // they are different moments. A guard that holds at one tick of one seed is a guard that
    // will go red for a reason nobody can explain, which is worse than no guard —
    // `TheCommuteNoteAppearsExactlyWhenTheWalkIsBeyondTheBudget` asserts the thing the design
    // actually promises, at every tick it is run at.

    // ---------------------------------------------------------------
    //  §7 — Shedding takes the furthest first
    // ---------------------------------------------------------------

    [Fact]
    public void SheddingReleasesTheLongestWalkNotTheHighestId()
    {
        // "Highest id" is the tempting shortcut, and is what the previous
        // implementation did. The longest commute is the weakest claim, and — unlike
        // an id — it is a reason that can be said out loud.
        SimLoop loop = RunToAReshuffle(Config, 40);
        SimWorld world = loop.World;

        int foraging = CountWorking(world, JobKind.Forager);
        Assert.True(foraging >= 2, "Need at least two foragers to have a furthest one.");

        Villager furthest = FurthestWorker(world, JobKind.Forager)!;
        int highestId = HighestIdWorker(world, JobKind.Forager)!.Id;
        _output.WriteLine($"furthest: {furthest.Name} (#{furthest.Id}); highest id: #{highestId}");

        // Ask for exactly one fewer forager than the village currently has.
        var quota = new LabourQuota(
            hands: foraging,
            mouths: world.Population,
            foragersToFeedEveryone: 1,
            foragers: foraging - 1,
            foresters: CountWorking(world, JobKind.Forester),
            woodcutters: CountWorking(world, JobKind.Woodcutter),

            // Every other kind is asked for exactly what the village already has, so
            // the forager is the only surplus and this test stays about the one thing
            // it is named for. Omitting the marketers made the quota ask for none and
            // shed the lot alongside the forager.
            marketers: CountWorking(world, JobKind.Marketer),

            // AND THE BUILDERS, for exactly the same reason and one decision later
            // (D102). A forty-year village used to hold no builders at all, because
            // nothing was ever marked; houses are construction sites now, so it holds
            // some almost always — and leaving this at its default of zero shed all four
            // of them alongside the forager.
            builders: CountWorking(world, JobKind.Builder));

        System.Collections.Generic.List<int> shed = LabourAllocator.ShedSurplus(world, quota);

        Assert.Single(shed);
        Assert.Equal(furthest.Id, shed[0]);
        Assert.Contains("longest walk", furthest.JobReason, System.StringComparison.Ordinal);
        _output.WriteLine(furthest.JobReason);
    }

    // ---------------------------------------------------------------
    //  §7 — Everyone can name the constraint that excluded them
    // ---------------------------------------------------------------

    [Fact]
    public void EveryIdleVillagerCanNameTheConstraintThatExcludedThem()
    {
        // "No work available" would collapse three genuinely different situations —
        // build somewhere nearer, you need another site, you have more hands than
        // mouths — into a shrug. Each has a different next move for the player.
        //
        // It used to squeeze catchment to four tiles to guarantee some idleness; catchment
        // is gone (`forests-and-gathering.md §3`) and a full village produces idle hands
        // anyway, from quota and from capacity, which are the reasons that remain.
        SimLoop loop = RunToAReshuffle(Config, 60);

        foreach (Villager villager in loop.World.Villagers)
        {
            if (!villager.CanWork || villager.HasJob)
            {
                continue;
            }

            Assert.StartsWith("No work:", villager.JobReason, System.StringComparison.Ordinal);
            Assert.True(
                villager.JobReason.Contains("within reach", System.StringComparison.Ordinal)
                || villager.JobReason.Contains("is full", System.StringComparison.Ordinal)
                || villager.JobReason.Contains("hands it needs", System.StringComparison.Ordinal)
                || villager.JobReason.Contains("back to food", System.StringComparison.Ordinal)
                || villager.JobReason.Contains("longest walk", System.StringComparison.Ordinal),
                $"{villager.Name} is idle for an unnamed reason: \"{villager.JobReason}\"");
        }
    }

    // ⛔ `CatchmentRefusalNamesTheDistanceAndTheReach` IS DELETED, not re-pointed. It squeezed
    // catchment to one tile and asserted the refusal named the distance and the reach —
    // *"outside its catchment of 1 tiles"*. **There is no such refusal any more** and there
    // is deliberately no replacement for it: being too far from work is not a thing the
    // village says no to. What replaced it is the sentence below, which is about a walk
    // somebody IS making rather than one they were forbidden.

    /// <summary>
    /// ⭐ The commute note says something exactly when there is something to say — <b>the
    /// condition that let catchment be deleted</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// D112's third call carried this as a condition rather than a caveat: removing the fence
    /// makes a long walk silent, and a village thinning out with nothing on screen saying why
    /// is §1.1 failing.
    /// </para>
    /// <para>
    /// <b>Asserted as an invariant rather than posed as a scenario</b>, because posing it is
    /// how the first version of the threshold got through: it looked for *a* villager with a
    /// note, found one, and never noticed that <em>everybody</em> had one. The rule is that a
    /// note appears if and only if the walk is beyond the food budget, and both halves are
    /// checked on every working villager.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheCommuteNoteAppearsExactlyWhenTheWalkIsBeyondTheBudget()
    {
        SimLoop loop = RunToAReshuffle(Config, 60);
        int budget = VillageEconomy.MaxHomeToWorkTiles(Config);

        int silent = 0;
        int noted = 0;

        foreach (Villager villager in loop.World.Villagers)
        {
            if (!villager.HasJob)
            {
                continue;
            }

            Workplace workplace = loop.World.FindWorkplace(villager.WorkplaceId)!;
            int tiles = LabourAllocator.CostBetween(loop.World, villager, workplace)
                / TravelCostField.BaseTileCost;

            if (tiles > budget)
            {
                noted++;
                Assert.True(
                    villager.CommuteNote.Length > 0,
                    $"{villager.Name} walks {tiles} tiles against a budget of {budget} and "
                    + "nothing says so.");
            }
            else
            {
                silent++;
                Assert.True(
                    villager.CommuteNote.Length == 0,
                    $"{villager.Name} walks {tiles} tiles, inside the {budget} the village "
                    + $"budgets for, and is complaining about it: \"{villager.CommuteNote}\"");
            }
        }

        // Anti-vacuity (D7): if nobody is working, both halves above pass by never running.
        _output.WriteLine($"{silent} affordable commutes, {noted} beyond the budget.");
        Assert.True(silent > 0, "Nobody is working, so this guard checked nothing.");
    }

    [Fact]
    public void CapacityRefusalNamesTheFullWorkplace()
    {
        // One seat per site and a village that outgrows them: the refusal has to say
        // "you need another site", not "no".
        //
        // Posed directly rather than hoped for: one seat everywhere, four founders.
        // Three of them have nowhere to fit from the very first tick, so the message
        // cannot be missed by a village that happened not to grow.
        //
        // The gathering seat is squeezed through the RING now rather than through a
        // capacity key: a hut prices its own seats from the ground it can reach
        // (`GathererHutCapacity`), so a ring of one tile is a hut with one pair of hands.
        // That is the same posing done through the number that still exists.
        SimConfig config = Config with
        {
            GathererHutCapacity = 1,
            ForesterHutCapacity = 1,
            WoodcutterHutCapacity = 1,

            // No market either, so the only work anyone can reach is the one full
            // patch. Otherwise a villager's refusal explains the market instead, which
            // is a true sentence about the wrong building.
            MarketCapacity = 0,
        };
        SimLoop loop = Build(config);
        loop.StepOnce();

        string? found = null;
        foreach (Villager villager in loop.World.Villagers)
        {
            if (villager.CanWork && !villager.HasJob
                && villager.JobReason.Contains("is full", System.StringComparison.Ordinal))
            {
                found = $"{villager.Name}: {villager.JobReason}";
                break;
            }
        }

        foreach (Villager villager in loop.World.Villagers)
        {
            _output.WriteLine($"  {villager.Name}: job {villager.WorkplaceId} — {villager.JobReason}");
        }

        _output.WriteLine(found ?? "(nobody was turned away for want of room)");
        Assert.NotNull(found);
    }

    // ---------------------------------------------------------------
    //  §7 — Determinism
    // ---------------------------------------------------------------

    [Fact]
    public void TheSameSeedGivesIdenticalAssignmentsAndIdenticalReasons()
    {
        // N villagers x M workplaces is the largest ordering surface in the sim so
        // far. The reason strings are hashed here as well as the state, because a
        // desync in a runner-up's name would not move a single integer.
        SimConfig config = Config;
        SimLoop a = Build(config);
        SimLoop b = Build(config);

        a.Step(config.TicksPerYear * 150);
        b.Step(config.TicksPerYear * 150);

        Assert.Equal(StateHash.Compute(a.World), StateHash.Compute(b.World));
        Assert.Equal(a.World.Villagers.Count, b.World.Villagers.Count);

        for (int i = 0; i < a.World.Villagers.Count; i++)
        {
            Assert.Equal(a.World.Villagers[i].WorkplaceId, b.World.Villagers[i].WorkplaceId);
            Assert.Equal(a.World.Villagers[i].JobReason, b.World.Villagers[i].JobReason);
        }
    }

    [Fact]
    public void ReshufflingTwiceInARowChangesNothing()
    {
        // D20 requires the allocator be re-runnable FROM SCRATCH rather than
        // incremental. If a from-scratch run did not reproduce itself, the annual
        // reshuffle would churn jobs for no reason at all.
        SimLoop loop = RunToAReshuffle(Config, 40);

        int[] first = Assignments(loop.World);
        LabourAllocator.Reshuffle(loop.World);
        int[] second = Assignments(loop.World);

        Assert.Equal(first, second);
    }

    // ---------------------------------------------------------------
    //  D20 — the reshuffle has to be able to explain itself
    // ---------------------------------------------------------------

    [Fact]
    public void AJobChangeSaysWhatItChangedFrom()
    {
        // "A reshuffle that cannot explain itself is worse than no reshuffle" (D20).
        // Over a century somebody's work must move, and when it does the sentence on
        // that villager has to name the place they left.
        SimConfig config = Config;
        SimLoop loop = Build(config);

        string? moved = null;
        for (int year = 1; year <= 150 && moved is null; year++)
        {
            loop.Step(config.TicksPerYear);
            loop.StepOnce();

            foreach (Villager villager in loop.World.Villagers)
            {
                if (villager.HasJob && villager.JobReason.StartsWith("Moved to", System.StringComparison.Ordinal))
                {
                    moved = $"Year {year} — {villager.Name}: {villager.JobReason}";
                    break;
                }
            }
        }

        _output.WriteLine(moved ?? "(nobody ever changed work)");
        Assert.NotNull(moved);
    }

    [Fact]
    public void TheVillageNarratesTheReshuffleItJustDid()
    {
        // The per-villager reason answers "why is she doing that?" on click. This is
        // the other half: the player should be able to see that a reshuffle HAPPENED
        // without clicking anyone.
        var log = new InMemoryLogSink();
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, log);

        loop.Step(config.TicksPerYear * 60);

        bool narrated = false;
        foreach (LogEntry entry in log.Entries)
        {
            if (entry.Level == LogLevel.Info
                && entry.Message.Contains("Work was shared out again", System.StringComparison.Ordinal))
            {
                _output.WriteLine(entry.Message);
                narrated = true;
                break;
            }
        }

        Assert.True(narrated, "The village reshuffled its work and never said so.");
    }

    // ---------------------------------------------------------------
    //  §8 — Definition of Done
    // ---------------------------------------------------------------

    /// <summary>
    /// THE acceptance test (spec §8) — <b>and it now runs with no fence at all.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// It was <c>TheVillageSurvivesAndGrowsWithCatchmentGenuinelyBinding</c>, and the binding
    /// half of that claim is gone: catchment is deleted
    /// (`forests-and-gathering.md §3`, Joe). What survives is the half that always mattered —
    /// <b>the village lives and grows over 150 years</b> — and it is a stronger statement
    /// without the fence than it was with it, because nothing is stopping a villager taking a
    /// ruinous job any more except the cost-first sort.
    /// </para>
    /// <para>
    /// <b>Its anti-vacuity moved rather than being dropped.</b> The old guard asserted that
    /// some villager/workplace pairs were out of reach, which is what made the fence real;
    /// <c>SomebodyWorksFurtherThanTheOldFenceWouldHaveAllowed</c> is the same idea from the
    /// other side, and the pair-counting below now measures <em>reachability</em>, which is
    /// the only thing left that can exclude anybody.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// <b>⚠️ MEASURED AT THE PEAK RATHER THAN AT YEAR 150 (D143).</b> Joe: <i>"an unattended
    /// village should die out. The user needs to play the game at some point."</i> Nobody sites
    /// a building or paints a tile in this run, so the headcount at year 150 says how long a
    /// village coasts, not whether the allocator works. <b>Growth is the claim</b> — and the
    /// pair-counting is read on the year the village was largest, where it means something,
    /// rather than off the two survivors at the end.
    /// </remarks>
    [Fact]
    public void TheVillageSurvivesAndGrowsWithNoDistanceFenceAtAll()
    {
        SimConfig config = Config;
        SimLoop loop = Build(config);

        int peak = 0;
        int peakYear = 0;
        int reachable = 0;
        int unreachable = 0;

        for (int year = 1; year <= 150; year++)
        {
            loop.Step(config.TicksPerYear);
            if (loop.World.Population <= peak)
            {
                continue;
            }

            peak = loop.World.Population;
            peakYear = year;
            reachable = 0;
            unreachable = 0;

            foreach (Villager villager in loop.World.Villagers)
            {
                if (!villager.Alive || !villager.CanWork)
                {
                    continue;
                }

                foreach (Workplace workplace in loop.World.Workplaces)
                {
                    if (LabourAllocator.CanReach(loop.World, villager, workplace))
                    {
                        reachable++;
                    }
                    else
                    {
                        unreachable++;
                    }
                }
            }
        }

        SimWorld world = loop.World;
        _output.WriteLine(
            $"Peaked at {peak} in year {peakYear}; year {world.Clock.Year}: {world.Population} " +
            $"alive in {world.Households.Count} houses. Villager/workplace pairs at the peak — " +
            $"{reachable} walkable, {unreachable} cut off.");

        // ⚠️ NO CLAIM ABOUT `unreachable` ANY MORE, in either direction. It used to have to be
        // positive, because a fence that reaches everything constrains nothing; with the fence
        // gone the only thing that can cut a workplace off is water, and whether a given seed's
        // river cuts anybody off is a property of that valley rather than of this design.
        // Asserting either way would be asserting something about seed 12345's geography.
        Assert.True(peak >= 25,
            $"The village never grew without a distance fence: it peaked at {peak} from "
            + $"{config.StartingPopulation} founders.");
        Assert.True(reachable > 0, "Nobody could walk to any work at the village's largest.");
    }

    /// <summary>
    /// Every household can <b>walk</b> to food. The fence is gone; the water is not.
    /// </summary>
    /// <remarks>
    /// It was <c>ABindingCatchmentStillLetsEveryHouseholdReachSomewhere</c> and asked whether
    /// a site lay inside the fence. The question that survives is D40's: a granary on the far
    /// bank is not a long walk, it is no walk at all — and a household with no walk to any
    /// food is the silent unrecoverable state §0.1 rules out.
    /// </remarks>
    [Fact]
    public void EveryHouseholdCanWalkToFood()
    {
        SimConfig config = Config;
        SimLoop loop = Build(config);
        loop.Step(config.TicksPerYear * 150);

        int checked_ = 0;
        foreach (Household household in loop.World.Households)
        {
            if (loop.World.LivingMembersOf(household) == 0)
            {
                continue;
            }

            checked_++;
            bool anywhere = false;
            foreach (Workplace workplace in loop.World.Workplaces)
            {
                if (workplace.Kind == JobKind.Forager
                    && loop.World.TravelCost.CanReach(household.Home(), workplace.Position))
                {
                    anywhere = true;
                    break;
                }
            }

            Assert.True(anywhere,
                $"The {household.Name} household at {household.Home()} cannot walk to any "
                + "food at all.");
        }

        // Anti-vacuity: a village that died leaves no occupied households, and the
        // loop above would pass by never running.
        Assert.True(checked_ > 0, "No occupied households left to check — the village died.");
    }

    [Fact]
    public void TheValleyContainsEveryWorkplaceAndEveryHomeTheVillageWillBuild()
    {
        // A site or a home outside the valley would simply be invisible, and a villager
        // would walk off the drawn map to reach it.
        //
        // ASSERTED AGAINST HOMES THE VILLAGE ACTUALLY BUILT, which it was not. This used
        // to walk `Household.PlacementFor` — a square spiral — two hundred times and check
        // where that put things, on the strength of a comment saying "homes are placed on
        // an unbounded spiral" and "clamping placement to the valley belongs with seeded
        // map generation (D18)". D18 shipped, `ChooseSite` replaced the spiral, and the
        // spiral became a function nothing called except this test. It was asserting a
        // property of dead code.
        SimConfig config = Config;
        SimLoop loop = Build(config);
        loop.Step(config.TicksPerYear * 200);

        foreach (Workplace workplace in loop.World.Workplaces)
        {
            AssertInsideTheValley(config, workplace.Position, workplace.Name);
        }

        int homes = 0;
        foreach (Household household in loop.World.Households)
        {
            // A house being built is not a home yet (D102), and this asks about where the
            // village PUT its homes. The site it is being raised on is a workplace, and the
            // loop above already checked every one of those.
            if (!household.HasHome)
            {
                continue;
            }

            AssertInsideTheValley(config, household.Home(), $"the {household.Name} home");
            homes++;
        }

        _output.WriteLine(
            $"valley {config.MapWidth}x{config.MapHeight}: " +
            $"x {config.MapMinX}..{config.MapMaxX}, y {config.MapMinY}..{config.MapMaxY} — " +
            $"{loop.World.Workplaces.Count} workplaces and {homes} homes, all inside it.");

        // Anti-vacuity (D7): a village that never built a second house proves nothing
        // about where the village puts houses.
        Assert.True(homes > config.StartingHouseholds,
            $"Only {homes} homes were ever built, so this guard never left the founding site.");
    }

    private static void AssertInsideTheValley(SimConfig config, GridPos position, string what)
    {
        Assert.True(
            position.X >= config.MapMinX && position.X <= config.MapMaxX
            && position.Y >= config.MapMinY && position.Y <= config.MapMaxY,
            $"{what} at {position} is outside the valley " +
            $"({config.MapMinX}..{config.MapMaxX}, {config.MapMinY}..{config.MapMaxY}).");
    }

    // ---------------------------------------------------------------

    private static int CountWorking(SimWorld world, JobKind kind)
    {
        int count = 0;
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.Workplaces[i].Kind == kind)
            {
                count += world.Workplaces[i].WorkerIds.Count;
            }
        }

        return count;
    }

    private static Villager? FurthestWorker(SimWorld world, JobKind kind)
    {
        Villager? furthest = null;
        int worst = -1;

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            Workplace? job = villager.HasJob ? world.FindWorkplace(villager.WorkplaceId) : null;
            if (job is null || job.Kind != kind)
            {
                continue;
            }

            int cost = LabourAllocator.CostBetween(world, villager, job);
            if (cost >= worst)
            {
                worst = cost;
                furthest = villager;
            }
        }

        return furthest;
    }

    private static Villager? HighestIdWorker(SimWorld world, JobKind kind)
    {
        Villager? highest = null;

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (villager.HasJob && world.FindWorkplace(villager.WorkplaceId)?.Kind == kind)
            {
                highest = villager;
            }
        }

        return highest;
    }

    private static int[] Assignments(SimWorld world)
    {
        int[] assignments = new int[world.Villagers.Count];
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            assignments[i] = world.Villagers[i].WorkplaceId;
        }

        return assignments;
    }
}
