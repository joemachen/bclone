using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The builder's hut, and construction sites becoming errands — D108,
/// <c>specs/professions.md §7.1</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe:</b> <em>"A construction site is a place that builders should treat as errands. If
/// there is an incomplete construction site on the map and an active/staffed builder's hut,
/// then the builders' priority should be completing the construction site."</em>
/// </para>
/// <para>
/// Two things change together and cannot be split. Builders get a <b>building</b> — the last
/// profession with none (<c>specs/professions.md §4</c>) — and a site stops being somewhere
/// anybody is posted. A hut on its own would add Builder seats while sites still supplied
/// demand, which is a state nobody designed.
/// </para>
/// <para>
/// <b>Free and instant, like the pile (Joe).</b> It is the one building that must exist before
/// any other can be raised, so charging timber for it is the circle the pile exists to avoid.
/// </para>
/// </remarks>
public sealed class BuildersHutTests
{
    private readonly ITestOutputHelper _output;

    public BuildersHutTests(ITestOutputHelper output) => _output = output;

    private static SimWorld World(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

    /// <summary>A buildable tile near the village with nothing standing on it.</summary>
    private static GridPos ClearGroundNear(SimWorld world, BuildingKind kind)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 1; radius < 12; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (!world.HasSomethingToHarvest(at) && world.CanBuildAt(kind, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No clear ground near the founding site.");
    }

    /// <summary>A forest tile near the village, or null if this valley has none in reach.</summary>
    private static GridPos? WoodedGroundNear(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 1; radius < 15; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (world.Map.Contains(at) && world.HasSomethingToHarvest(at))
                    {
                        return at;
                    }
                }
            }
        }

        return null;
    }

    private static Workplace? HutIn(SimWorld world)
    {
        foreach (Workplace workplace in world.Workplaces)
        {
            if (workplace.Kind == JobKind.Builder && !workplace.IsSite)
            {
                return workplace;
            }
        }

        return null;
    }

    // ---------------------------------------------------------------
    //  Free, and instant
    // ---------------------------------------------------------------

    /// <summary>⭐ The hut costs nothing and stands the tick it is marked.</summary>
    /// <remarks>
    /// The same shape as the pile (D96, D98), and asked of the recipe rather than of the kind
    /// (D108) — which is what made a second free building cost one branch instead of a sixth
    /// silent special case.
    /// </remarks>
    [Fact]
    public void TheHutIsFreeAndStandsTheTickItIsMarked()
    {
        BuildingRecipe recipe = BuildingRecipe.For(BuildingKind.BuilderHut, VillageFixtures.Village);
        Assert.Equal(0, recipe.Logs);
        Assert.Equal(0, recipe.WorkTicks);

        SimWorld world = World(ShippedConfig.Load());
        Assert.Null(HutIn(world));

        GridPos at = ClearGroundNear(world, BuildingKind.BuilderHut);
        Assert.True(world.Mark(BuildingKind.BuilderHut, at).Allowed);

        Workplace hut = Assert.Single(
            world.Workplaces, place => place.Kind == JobKind.Builder && !place.IsSite);

        Assert.Equal(at, hut.Position);

        // No site, no waiting list, and nothing owed. A construction site for a building
        // that costs nothing is a builder walking to a footprint to do nothing — which is
        // the window D95 died in.
        Assert.Empty(world.BuildingsWaitingOnTheGround);
        Assert.DoesNotContain(
            world.Workplaces, place => place.Construction?.Kind == BuildingKind.BuilderHut);
    }

    /// <summary>A hut marked on woodland waits, and goes up when the ground is bare.</summary>
    /// <remarks>
    /// <b>The clearing is what it costs</b> (D96), and the village does the clearing rather
    /// than the player being sent on an errand (D100). This is the pile's rule reaching a
    /// second free building without a second copy of the machinery — the waiting list carries
    /// a <em>kind</em> now, so it cannot raise a pile on ground somebody asked a hut for.
    /// </remarks>
    [Fact]
    public void AHutMarkedOnWoodedGroundStandsOnceTheGroundIsCleared()
    {
        SimWorld world = World(ShippedConfig.Load());

        GridPos? wooded = WoodedGroundNear(world);
        Assert.NotNull(wooded);
        GridPos at = wooded!.Value;

        Assert.True(world.Mark(BuildingKind.BuilderHut, at).Allowed);

        Assert.Null(HutIn(world));
        Assert.Contains(at, world.BuildingsWaitingOnTheGround);
        Assert.True(world.Zones.IsHarvest(at), "Marking on a resource asks for it to be cleared.");

        world.Harvest(at);

        Workplace? hut = HutIn(world);
        Assert.NotNull(hut);
        Assert.Equal(at, hut!.Position);
    }

    /// <summary>
    /// ⭐ Its seats are derived from the economy, not typed into a config file.
    /// </summary>
    /// <remarks>
    /// <b><c>woodcutter_hut_capacity</c> is the recorded case</b> (D50): the yields were
    /// re-derived when the economy horizon moved and the capacities were not, the village
    /// could not physically make enough firewood however many hands were free, and thirty-six
    /// people froze. Guarded non-vacuously — a bigger horizon has to move the number, or this
    /// would pass against a constant.
    /// </remarks>
    [Fact]
    public void TheHutsSeatsAreDerivedFromTheEconomy()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld world = World(config);

        Workplace? hut = HutIn(world);
        Assert.NotNull(hut);
        Assert.Equal(VillageEconomy.BuilderHutCapacity(config), hut!.Capacity);
        Assert.True(hut.Capacity >= 1, "A hut with no seat can never build anything.");

        int wider = VillageEconomy.BuilderHutCapacity(
            config with { EconomyHorizonHouseholds = config.EconomyHorizonHouseholds * 2 });

        _output.WriteLine($"seats at {config.EconomyHorizonHouseholds} households: {hut.Capacity}; "
            + $"at {config.EconomyHorizonHouseholds * 2}: {wider}");

        Assert.True(wider > hut.Capacity, "The seats do not follow the economy they are derived from.");
    }

    // ---------------------------------------------------------------
    //  A site is an errand, not a workplace
    // ---------------------------------------------------------------

    /// <summary>⭐ Nobody is ever posted to a construction site.</summary>
    [Fact]
    public void AConstructionSiteHasNoSeatsAndTakesNoWorkers()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        GridPos at = ClearGroundNear(world, BuildingKind.Granary);
        Assert.True(world.Mark(BuildingKind.Granary, at).Allowed);

        Workplace site = Assert.Single(
            world.Workplaces, place => place.Construction?.Kind == BuildingKind.Granary);

        Assert.True(site.IsSite);
        Assert.Equal(0, site.Capacity);

        // A whole year of labour passes, including a reshuffle and four seasonal passes.
        for (int tick = 0; tick < config.TicksPerYear; tick++)
        {
            loop.StepOnce();

            foreach (Workplace workplace in world.Workplaces)
            {
                if (workplace.IsSite)
                {
                    Assert.Empty(workplace.WorkerIds);
                }
            }
        }

        // And no villager's job is a site, whether or not this particular one is finished.
        foreach (Villager villager in world.Villagers)
        {
            Workplace? job = villager.HasJob ? world.FindWorkplace(villager.WorkplaceId) : null;
            Assert.False(job is { IsSite: true },
                $"{villager.Name} holds a job at a construction site.");
        }
    }

    /// <summary>The head of the build queue is what a builder walks out to.</summary>
    /// <remarks>
    /// <b>Two orderings that must agree is the shape of half the bugs in this project's
    /// history</b>, so the cheap single-pass reader and the sorted queue the panel shows are
    /// asserted against each other — including after the player has reordered it (D105),
    /// which is the whole reason the errand reads the queue rather than picking the nearest.
    /// </remarks>
    [Fact]
    public void TheHeadOfTheQueueIsWhatTheBuildersGoTo()
    {
        SimWorld world = World(VillageFixtures.Village);

        GridPos first = ClearGroundNear(world, BuildingKind.Granary);
        Assert.True(world.Mark(BuildingKind.Granary, first).Allowed);

        GridPos second = ClearGroundNear(world, BuildingKind.Shed);
        Assert.True(world.Mark(BuildingKind.Shed, second).Allowed);

        Assert.Equal(2, world.BuildQueue().Count);
        Assert.Equal(world.BuildQueue()[0].Id, world.NextToBuild()!.Id);
        Assert.Equal(BuildingKind.Granary, world.NextToBuild()!.Construction!.Kind);

        // ▼ Later on the granary, and the shed is what the crew goes to instead.
        Assert.True(world.MoveInBuildQueue(world.BuildQueue()[0], +1));

        Assert.Equal(world.BuildQueue()[0].Id, world.NextToBuild()!.Id);
        Assert.Equal(BuildingKind.Shed, world.NextToBuild()!.Construction!.Kind);
    }

    /// <summary>
    /// ⭐ A site nobody can walk to is skipped, not queued behind — it must never block the rest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured, and it killed seed 11 of twelve.</b> `ChooseSite` put a house at (-1,-5)
    /// on the far bank of the river, and because the whole crew works the head of the queue,
    /// <b>every builder spent a century walking toward a place they could never arrive at.</b>
    /// Eight sites behind it were never raised, four households of eleven were ever roofed,
    /// thirteen hundred logs sat in the shed, nobody starved and nobody froze — the village
    /// simply aged out. **That is the silent unrecoverable death §0.1 rules out**, and it is
    /// the cost of the queue being an order rather than a set of separate workplaces.
    /// </para>
    /// <para>
    /// <b>The site should never have been marked</b>, and `MarkHome` now says so out loud
    /// (§1.1 — the cause has to be findable, not just the symptom). This guard is the belt to
    /// that braces: one impossible footprint must not cost a village its whole future.
    /// </para>
    /// </remarks>
    [Fact]
    public void AnUnreachableSiteNeverBlocksTheOnesBehindIt()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld world = World(config);

        // A tile across the water, which is the only genuinely unreachable ground there is
        // (D40). Found rather than assumed — a valley whose river is out of reach of this
        // scan would make the guard vacuous.
        GridPos? marooned = MaroonedGroundNear(world);
        Assert.NotNull(marooned);

        GridPos reachable = ClearGroundNear(world, BuildingKind.Granary);
        Assert.True(world.Mark(BuildingKind.Granary, reachable).Allowed);

        // Straight into the world, ahead of the granary — `Mark` would refuse it, which is
        // the point: this is the state only `MarkHome` can produce.
        world.MarkHome(world.Households[0].Id, marooned!.Value);
        Workplace stranded = Assert.Single(
            world.Workplaces, place => place.Position == marooned.Value && place.IsSite);

        Assert.True(world.MoveInBuildQueue(stranded, -1) || world.QueuePositionOf(stranded) == 1);
        Assert.Equal(1, world.QueuePositionOf(stranded));

        _output.WriteLine($"stranded house at {marooned} is 1st of {world.BuildQueue().Count}; "
            + $"the crew goes to {world.NextToBuild()?.Construction?.Name ?? "nothing"}");

        // The queue still says the stranded house is first — that is honest, it IS first —
        // but the crew walks past it to something they can actually reach.
        Assert.NotNull(world.NextToBuild());
        Assert.NotEqual(stranded.Id, world.NextToBuild()!.Id);
        Assert.Equal(BuildingKind.Granary, world.NextToBuild()!.Construction!.Kind);
    }

    /// <summary>A tile the village has no route to, or null if this valley has none.</summary>
    private static GridPos? MaroonedGroundNear(SimWorld world)
    {
        GridPos village = world.FirstHomeOrFoundingSite();
        for (int y = 0; y < world.Map.Height; y++)
        {
            for (int x = 0; x < world.Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (world.Map.TerrainAt(at) != Terrain.Water
                    && !world.TravelCost.CanReach(village, at))
                {
                    return at;
                }
            }
        }

        return null;
    }

    /// <summary>Nothing marked out means nobody is wanted at the hut.</summary>
    /// <remarks>
    /// A hut is a livelihood somebody holds and there will be work at it again — but staffing
    /// it with nothing to raise takes a hand off the berries for no yield at all, which is
    /// the make-work D52 measured as costing the village a third of its population.
    /// </remarks>
    [Fact]
    public void AVillageWithNothingMarkedWantsNoBuilders()
    {
        SimWorld world = World(VillageFixtures.Village);

        Assert.NotNull(HutIn(world));
        Assert.Empty(world.BuildQueue());
        Assert.Equal(0, LabourQuota.BuildersWanted(world));

        GridPos at = ClearGroundNear(world, BuildingKind.Granary);
        Assert.True(world.Mark(BuildingKind.Granary, at).Allowed);

        // And with something marked, the demand is the HUT's seats — not the site's, which
        // are zero, and not a number per site, which is what it used to count.
        Assert.Equal(HutIn(world)!.Places, LabourQuota.BuildersWanted(world));
    }

    // ---------------------------------------------------------------
    //  ⭐ The hut is the only path to a building
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ With no hut, nothing is raised — and the village says so rather than standing mute.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>There is deliberately no "no hut, sites staff themselves" fallback</b> (Joe). That
    /// is what makes the hut a decision rather than a formality, and it is why it costs
    /// nothing: the price of building is the hands you put in it, not the timber.
    /// </para>
    /// <para>
    /// <b>The sentence is not polish, it is what makes the stall fair</b> (D93, §1.1). A
    /// footprint reading <em>"0 of 25 logs"</em> that never moves and never explains itself is
    /// the untraceable outcome the whole design refuses.
    /// </para>
    /// </remarks>
    [Fact]
    public void NothingIsRaisedWithoutAHutAndTheVillageSaysSo()
    {
        SimConfig config = ShippedConfig.Load();
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;

        GridPos site = world.Map.FoundingSite;
        for (int dy = -4; dy <= 4; dy++)
        {
            for (int dx = -4; dx <= 4; dx++)
            {
                world.PaintResidential(new GridPos(site.X + dx, site.Y + dy));
            }
        }

        MarkSomewhereNear(world, BuildingKind.Pile, site, 2);
        MarkSomewhereNear(world, BuildingKind.WoodcutterHut, site, 3);

        // Somewhere to gather and something to fell, in BOTH arms. A founding valley has held
        // no workplaces at all since the thickets retired, so without this neither village
        // builds anything and the comparison says nothing about builder's huts.
        ColdStartTests.FeedTheFounding(world);

        Assert.Null(HutIn(world));

        Assert.Contains(sink.Entries, entry => entry.Message.Contains(
            "nobody in the village builds", StringComparison.OrdinalIgnoreCase));

        loop.Step(config.TicksPerYear);

        _output.WriteLine($"a year with no builder's hut: {world.BuildQueue().Count} sites still "
            + $"waiting, {world.LogsInSheds()} logs in store");

        // Everything marked is still a footprint — including the houses the village marked
        // for itself, which is the harshest half of the rule and the whole of Joe's design.
        Assert.DoesNotContain(
            world.Workplaces, place => place.Kind == JobKind.Woodcutter);
        Assert.All(world.Households, household => Assert.False(household.HasHome));

        // ⭐ ANTI-VACUITY (D7): the same opening WITH a hut has to come out differently, or
        // this guard is only proving that the shipped opening is hard.
        //
        // ⚠️ The two arms must be placed IDENTICALLY, and getting that wrong cost a
        // diagnosis. Siting the three buildings on the nearest bare tiles instead — a tight
        // cluster at (-1,-2), (0,-2), (0,-1) — killed all four founders in year one WITH the
        // hut, which is D99's finding verbatim: "a probe that sited the pile at (-1,-2)
        // instead of (-3,-3) killed all four founders where the guard's own placement
        // survives comfortably." The founding is still exquisitely placement-sensitive, and
        // a guard about the hut must not accidentally be a guard about that.
        SimLoop withHut = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld warm = withHut.World;

        for (int dy = -4; dy <= 4; dy++)
        {
            for (int dx = -4; dx <= 4; dx++)
            {
                warm.PaintResidential(new GridPos(site.X + dx, site.Y + dy));
            }
        }

        MarkSomewhereNear(warm, BuildingKind.Pile, site, 2);
        MarkSomewhereNear(warm, BuildingKind.BuilderHut, site, 2);
        MarkSomewhereNear(warm, BuildingKind.WoodcutterHut, site, 3);
        ColdStartTests.FeedTheFounding(warm);

        withHut.Step(config.TicksPerYear);

        _output.WriteLine($"the same year WITH a builder's hut: {warm.Population} alive, "
            + $"{warm.Workplaces.Count(place => place.Kind == JobKind.Woodcutter)} woodcutter huts, "
            + $"{warm.TotalFirewood()} firewood");

        Assert.Contains(warm.Workplaces, place => place.Kind == JobKind.Woodcutter);
    }

    /// <summary>Mark on the first tile near the site that will take one — the opening's rule.</summary>
    private static void MarkSomewhereNear(
        SimWorld world, BuildingKind kind, GridPos site, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (world.Mark(kind, new GridPos(site.X + dx, site.Y + dy)).Allowed)
                {
                    return;
                }
            }
        }
    }
}
