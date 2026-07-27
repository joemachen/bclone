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
            int heldCost = LabourSystem.CostToWork(world, villager, held);

            foreach (Workplace other in world.Workplaces)
            {
                if (other.Id == held.Id || other.Kind != held.Kind || other.IsFull)
                {
                    continue;
                }

                int otherCost = LabourSystem.CostToWork(world, villager, other);
                Assert.False(
                    otherCost < heldCost && otherCost <= other.CatchmentRadius,
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
            int cutting = CountWorking(loop.World, JobKind.Logger);
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
        Assert.Equal(0, quota.Loggers);
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
                household.Stockpile.Add(loop.World.TargetFoodFor(household));
            }

            quota = LabourQuota.For(loop.World);
            if (quota.Loggers > 0)
            {
                _output.WriteLine($"{loop.World.Clock.SeasonAndYear()}: {quota}");
                break;
            }
        }

        Assert.False(LabourQuota.VillageIsShortOfFood(loop.World));
        Assert.True(quota.Loggers > 0, "A fed village with couples waiting should build.");
        Assert.Equal(quota.Hands, quota.Foragers + quota.Loggers + quota.Woodcutters);
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
            household.Stockpile.Add(loop.World.TargetFoodFor(household) * 10);
            household.Stockpile.AddLogs(Config.LogsPerHouse * 10);
        }

        Assert.False(LabourQuota.VillageIsShortOfFood(loop.World));
        Assert.Equal(0, LabourQuota.LoggersWanted(loop.World));
    }

    // ---------------------------------------------------------------
    //  §7 — Local capacity is respected
    // ---------------------------------------------------------------

    [Fact]
    public void NoSiteEverExceedsItsOwnCapacity()
    {
        SimLoop loop = Build(Config with { ForageSiteCapacity = 2, TreeStandCapacity = 1 });
        loop.Step(Config.TicksPerYear * 60);

        foreach (Workplace workplace in loop.World.Workplaces)
        {
            Assert.True(workplace.WorkerIds.Count <= workplace.Capacity,
                $"{workplace.Name} has {workplace.WorkerIds.Count} people in room for " +
                $"{workplace.Capacity}.");
        }
    }

    // ---------------------------------------------------------------
    //  §7 — Catchment still binds
    // ---------------------------------------------------------------

    [Fact]
    public void NoAssignmentIsEverOutsideAWorkplacesCatchment()
    {
        SimLoop loop = Build(Config with { ForagerCatchmentTiles = 5 });
        loop.Step(Config.TicksPerYear * 60);

        foreach (Villager villager in loop.World.Villagers)
        {
            if (!villager.HasJob)
            {
                continue;
            }

            Workplace workplace = loop.World.FindWorkplace(villager.WorkplaceId)!;
            Assert.True(LabourSystem.InCatchment(loop.World, villager, workplace),
                $"{villager.Name} works at {workplace.Name} from outside its catchment.");
        }
    }

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
            loggers: CountWorking(world, JobKind.Logger),
            woodcutters: CountWorking(world, JobKind.Woodcutter),

            // Every other kind is asked for exactly what the village already has, so
            // the forager is the only surplus and this test stays about the one thing
            // it is named for. Omitting the marketers made the quota ask for none and
            // shed the lot alongside the forager.
            marketers: CountWorking(world, JobKind.Marketer));

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
        SimLoop loop = RunToAReshuffle(Config with { ForagerCatchmentTiles = 4 }, 60);

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

    [Fact]
    public void CatchmentRefusalNamesTheDistanceAndTheReach()
    {
        // A catchment of one tile reaches nobody, so everyone is refused for exactly
        // one reason and it had better be the right one.
        SimLoop loop = RunToAReshuffle(Config with { ForagerCatchmentTiles = 1 }, 1);

        Villager villager = loop.World.Villagers[0];
        _output.WriteLine($"{villager.Name}: {villager.JobReason}");

        Assert.Contains("nothing within reach of home", villager.JobReason, System.StringComparison.Ordinal);
        Assert.Contains("outside its catchment", villager.JobReason, System.StringComparison.Ordinal);
    }

    [Fact]
    public void CapacityRefusalNamesTheFullWorkplace()
    {
        // One seat per site and a village that outgrows them: the refusal has to say
        // "you need another site", not "no".
        //
        // Posed directly rather than hoped for: one seat at the single patch, four
        // founders. Three of them have nowhere to fit from the very first tick, so
        // the message cannot be missed by a village that happened not to grow.
        SimConfig config = Config with
        {
            ExtraForageSites = System.Array.Empty<SitePosition>(),
            ForageSiteCapacity = 1,
            TreeStandCapacity = 1,
            WoodcutterHutCapacity = 1,
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

    [Fact]
    public void TheVillageSurvivesAndGrowsWithCatchmentGenuinelyBinding()
    {
        // THE acceptance test (spec §8), and the thing none of the three previous
        // attempts achieved. A catchment that binds means outlying households really
        // are restricted to the sites near them — not a generous radius that reaches
        // everything and therefore constrains nothing.
        //
        // Ten tiles is the shipped value, down from twelve. Below ten the village
        // still dies, and the cause is not food: it is that the one tree stand
        // becomes unreachable for most homes, so no timber is cut, no houses are
        // built, and the settlement ages out. D19's argument about food sources
        // applies to timber too, and that is the next piece of work.
        SimConfig config = Config;
        SimLoop loop = Build(config);

        loop.Step(config.TicksPerYear * 150);
        SimWorld world = loop.World;

        int reachable = 0;
        int unreachable = 0;
        foreach (Villager villager in world.Villagers)
        {
            if (!villager.Alive || !villager.CanWork)
            {
                continue;
            }

            foreach (Workplace workplace in world.Workplaces)
            {
                if (LabourSystem.InCatchment(world, villager, workplace))
                {
                    reachable++;
                }
                else
                {
                    unreachable++;
                }
            }
        }

        _output.WriteLine(
            $"Year {world.Clock.Year}: {world.Population} alive in {world.Households.Count} houses. " +
            $"Villager/workplace pairs — {reachable} within reach, {unreachable} out of reach.");

        Assert.True(unreachable > 0,
            "The catchment reaches every workplace from every home, so it constrains nothing.");
        Assert.True(world.Population > config.StartingPopulation,
            $"The village did not survive a binding catchment: {world.Population} alive.");
    }

    [Fact]
    public void ABindingCatchmentStillLetsEveryHouseholdReachSomewhere()
    {
        // Why several forage sites had to land first (D19): with one food source, a
        // catchment tight enough to bind is a catchment that starves the outskirts.
        // This is the property that makes the acceptance test above survivable rather
        // than merely lucky.
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
                    && loop.World.TravelCost.IsWithinCatchment(
                        household.HomePosition, workplace.Position, workplace.CatchmentRadius))
                {
                    anywhere = true;
                    break;
                }
            }

            Assert.True(anywhere,
                $"The {household.Name} household at {household.HomePosition} has no forage site in reach.");
        }

        // Anti-vacuity: a village that died leaves no occupied households, and the
        // loop above would pass by never running.
        Assert.True(checked_ > 0, "No occupied households left to check — the village died.");
    }

    [Fact]
    public void EveryHomeTheVillageWillBuildHasAForageSiteWithinReach()
    {
        // A layout guard, asserted against the map rather than against a run, so that
        // moving a site or tightening catchment fails the build instead of failing
        // the village a hundred years later.
        //
        // This is the constraint the first site layout broke: three sites out at the
        // edges left the middle of the village dependent on the one original berry
        // patch, and a home with nothing in reach is a household that cannot feed
        // itself however well the allocator works. Homes are placed on a fixed spiral
        // for now; when the map is generated from the seed (D18) this becomes a
        // property the generator has to guarantee.
        SimConfig config = Config;
        int radius = TravelCostField.TilesToCost(config.ForagerCatchmentTiles);
        var field = new TravelCostField(config.TravelTicksPerUnit);

        for (int i = 0; i <= config.EconomyHorizonHouseholds; i++)
        {
            GridPos home = Household.PlacementFor(i, config.HomeX, config.HomeY, config.HouseholdSpacing);
            int nearest = VillageEconomy.NearestForageDistance(config, home);

            Assert.True(field.Cost(home, home with { X = home.X + nearest }) <= radius,
                $"Home #{i} at {home} is {nearest} tiles from its nearest forage site, " +
                $"outside a catchment of {config.ForagerCatchmentTiles}.");
        }
    }

    [Fact]
    public void TheValleyContainsEveryWorkplaceAndEveryHomeTheVillageWillBuild()
    {
        // The valley bounds the camera and gives the drawn ground an edge. Nothing in
        // the sim reads them yet, so nothing in the sim would notice a site or a home
        // placed outside — it would simply be invisible, and a villager would walk off
        // the map to work at it.
        //
        // Homes are placed on an unbounded spiral. At 150 years the village reaches
        // about nine tiles out, well inside; clamping placement to the valley belongs
        // with seeded map generation (D18), and this is what will catch it if the
        // village outgrows the map first.
        SimConfig config = Config;
        SimLoop loop = Build(config);

        foreach (Workplace workplace in loop.World.Workplaces)
        {
            AssertInsideTheValley(config, workplace.Position, workplace.Name);
        }

        for (int i = 0; i < 200; i++)
        {
            GridPos home = Household.PlacementFor(i, config.HomeX, config.HomeY, config.HouseholdSpacing);
            AssertInsideTheValley(config, home, $"home #{i}");
        }

        _output.WriteLine(
            $"valley {config.MapWidth}x{config.MapHeight}: " +
            $"x {config.MapMinX}..{config.MapMaxX}, y {config.MapMinY}..{config.MapMaxY}");
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

            int cost = LabourSystem.CostToWork(world, villager, job);
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
