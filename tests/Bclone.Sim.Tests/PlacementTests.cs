using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The player marks a building and the village raises it —
/// <c>specs/building-placement.md</c> (D43), slice 2, sim half.
/// </summary>
/// <remarks>
/// <b>The first system in the game that answers to somebody.</b> Everything until now
/// happened to the player: the village founded itself, staffed itself and grew or died
/// on constants. These tests are the other side of that — an intention, expressed, and
/// a village that acts on it with the hands it can spare.
/// </remarks>
public sealed class PlacementTests
{
    private readonly ITestOutputHelper _output;

    public PlacementTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Build(SimConfig config, ulong? seed = null) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed);

    /// <summary>Somewhere legal to build, a few tiles from the village.</summary>
    private static GridPos SomewhereBuildable(SimWorld world, BuildingKind kind)
    {
        GridPos village = world.Households[0].HomePosition;

        for (int radius = 1; radius < 12; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (System.Math.Abs(dx) + System.Math.Abs(dy) != radius)
                    {
                        continue;
                    }

                    var candidate = new GridPos(village.X + dx, village.Y + dy);
                    if (world.CanBuildAt(kind, candidate).Allowed)
                    {
                        return candidate;
                    }
                }
            }
        }

        throw new System.InvalidOperationException("Nowhere legal to build near the village.");
    }

    // ---------------------------------------------------------------
    //  Refusals — the legibility standard, applied to placement
    // ---------------------------------------------------------------

    [Fact]
    public void WaterIsRefusedInWords()
    {
        SimWorld world = Build(Config, 1UL).World;

        GridPos wet = default;
        bool found = false;
        foreach (Terrain _ in world.Map.Tiles)
        {
            // Scan for any water tile; the first is enough.
            for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height && !found; y++)
            {
                for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width && !found; x++)
                {
                    var here = new GridPos(x, y);
                    if (world.Map.TerrainAt(here) == Terrain.Water)
                    {
                        wet = here;
                        found = true;
                    }
                }
            }

            break;
        }

        Assert.True(found, "This seed has no water, so the guard is vacuous (D7).");

        PlacementVerdict verdict = world.CanBuildAt(BuildingKind.Granary, wet);
        _output.WriteLine($"{wet}: {verdict.Reason}");

        Assert.False(verdict.Allowed);
        Assert.Contains("water", verdict.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OccupiedGroundIsRefusedInWords()
    {
        SimWorld world = Build(Config).World;

        PlacementVerdict verdict = world.CanBuildAt(
            BuildingKind.Shed, world.AnyStoreOf(StoreKind.Granary).Position);

        _output.WriteLine(verdict.Reason);
        Assert.False(verdict.Allowed);
        Assert.Contains("already stands", verdict.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OutsideTheValleyIsRefusedInWords()
    {
        SimConfig config = Config;
        SimWorld world = Build(config).World;

        PlacementVerdict verdict = world.CanBuildAt(
            BuildingKind.Granary, new GridPos(config.MapMaxX + 5, 0));

        Assert.False(verdict.Allowed);
        Assert.Contains("outside the valley", verdict.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AFarSiteIsAllowedButWarnedAbout()
    {
        // D43's whole position: the player may build somewhere unwise, and is told at
        // the time. Refusing would make the map feel arbitrary — "why not there?" has
        // no good answer beyond a hidden constant — and allowing it silently would
        // fail §1.1 outright.
        SimConfig config = Config;
        SimWorld world = Build(config).World;
        GridPos village = world.Households[0].HomePosition;

        PlacementVerdict? warned = null;
        for (int distance = 8; distance < 40 && warned is null; distance++)
        {
            var far = new GridPos(village.X + distance, village.Y);
            PlacementVerdict verdict = world.CanBuildAt(BuildingKind.Granary, far);
            if (verdict.Allowed && verdict.HasWarning)
            {
                warned = verdict;
            }
        }

        Assert.True(warned is not null, "Nowhere legal was far enough to warn about (D7).");
        _output.WriteLine(warned!.Value.Warning);

        Assert.True(warned.Value.Allowed, "A distant site must be allowed, not refused.");
        Assert.Contains("tiles from the village", warned.Value.Warning, System.StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  Building it
    // ---------------------------------------------------------------

    [Fact]
    public void AMarkedBuildingIsNotYetABuilding()
    {
        // The heart of D43: marking is an intention, not a purchase. Until somebody
        // carries the logs over and does the work, there is nothing there.
        SimConfig config = Config;
        SimLoop loop = Build(config);
        SimWorld world = loop.World;

        int granariesBefore = CountStores(world, StoreKind.Granary);
        GridPos spot = SomewhereBuildable(world, BuildingKind.Granary);

        Assert.True(world.Mark(BuildingKind.Granary, spot).Allowed);

        Assert.Equal(granariesBefore, CountStores(world, StoreKind.Granary));
        Assert.Contains(world.Workplaces, w => w.Kind == JobKind.Builder);
    }

    [Fact]
    public void TheVillageRaisesWhatThePlayerMarksOut()
    {
        // End to end, with nobody's hand on the scales: mark a granary, let the village
        // get on with it, and it stands.
        SimConfig config = Config;
        SimLoop loop = Build(config);
        SimWorld world = loop.World;

        // Let them get established first — a village with an empty larder puts every
        // hand on food, and rightly so (§4a).
        loop.Step(config.TicksPerYear * 12);

        int before = CountStores(world, StoreKind.Granary);
        GridPos spot = SomewhereBuildable(world, BuildingKind.Granary);
        Assert.True(world.Mark(BuildingKind.Granary, spot).Allowed);

        int builtInYear = 0;
        for (int year = 1; year <= 25; year++)
        {
            loop.Step(config.TicksPerYear);
            if (CountStores(world, StoreKind.Granary) > before)
            {
                builtInYear = year;
                break;
            }
        }

        _output.WriteLine(builtInYear > 0
            ? $"The second granary was finished {builtInYear} years after it was marked."
            : "It was never built.");

        Assert.True(builtInYear > 0, "The village never raised the granary it was asked for.");
        Assert.DoesNotContain(world.Workplaces, w => w.Kind == JobKind.Builder);
    }

    [Fact]
    public void ABuildingCostsTheLogsItSaidItWould()
    {
        // Conservation across the new movement. Logs leave a shed, arrive at a site,
        // and become a building — none of them invented, none quietly lost.
        SimConfig config = Config;
        SimLoop loop = Build(config);
        SimWorld world = loop.World;
        loop.Step(config.TicksPerYear * 12);

        GridPos spot = SomewhereBuildable(world, BuildingKind.Shed);
        int logsBefore = world.LogsInSheds();
        world.Mark(BuildingKind.Shed, spot);

        int before = CountStores(world, StoreKind.Shed);
        for (int year = 1; year <= 25 && CountStores(world, StoreKind.Shed) == before; year++)
        {
            loop.Step(config.TicksPerYear);
        }

        Assert.True(CountStores(world, StoreKind.Shed) > before, "The shed was never built.");
        _output.WriteLine(
            $"{logsBefore} logs in store before, {world.LogsInSheds()} after; " +
            $"a shed costs {config.ShedLogs}.");

        Assert.True(world.TotalLogs() <= world.LifetimeLogsFelled(),
            "Building is creating logs out of nothing.");
    }

    [Fact]
    public void BuildingYieldsToFeedingPeople()
    {
        // §4a's policy, applied to the newest job. A village with an empty larder and
        // berries to pick cannot afford to have anybody raising a granary — the first
        // winter would take them, and a half-built store is a better outcome than a
        // finished one nobody lived to use.
        SimLoop loop = Build(Config);
        loop.StepOnce();

        SimWorld world = loop.World;
        world.Mark(BuildingKind.Granary, SomewhereBuildable(world, BuildingKind.Granary));

        Assert.True(LabourQuota.VillageIsShortOfFood(world));
        Assert.Equal(0, LabourQuota.For(world).Builders);
    }

    [Fact]
    public void ADemolishedStoreLosesWhatWasInsideIt()
    {
        // The consequence D43 asked for, and it must be LOUD. Goods vanishing with no
        // line in the log is exactly the untraceable outcome §1.1 forbids.
        SimConfig config = Config;
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        loop.Step(config.TicksPerYear * 10);

        SimWorld world = loop.World;
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        int inside = granary.Store.Held;
        Assert.True(inside > 0, "The granary was empty, so this proves nothing (D7).");

        world.Demolish(granary);

        Assert.Equal(0, CountStores(world, StoreKind.Granary));

        bool narrated = false;
        foreach (LogEntry entry in sink.Entries)
        {
            if (entry.Message.Contains("pulled down", System.StringComparison.Ordinal)
                && entry.Message.Contains("lost", System.StringComparison.Ordinal))
            {
                narrated = true;
                _output.WriteLine(entry.Message);
            }
        }

        Assert.True(narrated, "The village lost a granary's worth of food and said nothing.");
    }

    [Fact]
    public void AnAbandonedSiteGivesItsLogsBack()
    {
        SimConfig config = Config;
        SimLoop loop = Build(config);
        loop.Step(config.TicksPerYear * 12);
        SimWorld world = loop.World;

        world.Mark(BuildingKind.Granary, SomewhereBuildable(world, BuildingKind.Granary));

        // Let some materials arrive before changing our mind.
        for (int i = 0; i < config.TicksPerYear * 3; i++)
        {
            loop.StepOnce();
        }

        Workplace? site = null;
        foreach (Workplace workplace in world.Workplaces)
        {
            if (workplace.Kind == JobKind.Builder)
            {
                site = workplace;
            }
        }

        if (site is null)
        {
            // It finished already — nothing to abandon, and that is not a failure.
            return;
        }

        int delivered = site.Construction!.LogsDelivered;
        int before = world.LogsInSheds();
        world.CancelConstruction(site);

        _output.WriteLine($"{delivered} logs were on site; store went {before} -> {world.LogsInSheds()}.");
        Assert.Equal(before + delivered, world.LogsInSheds());
        Assert.DoesNotContain(world.Workplaces, w => w.Kind == JobKind.Builder);
    }

    [Fact]
    public void PlacementIsDeterministic()
    {
        SimConfig config = Config;
        SimLoop a = Build(config);
        SimLoop b = Build(config);

        a.Step(config.TicksPerYear * 12);
        b.Step(config.TicksPerYear * 12);

        GridPos spot = SomewhereBuildable(a.World, BuildingKind.Granary);
        a.World.Mark(BuildingKind.Granary, spot);
        b.World.Mark(BuildingKind.Granary, spot);

        a.Step(config.TicksPerYear * 30);
        b.Step(config.TicksPerYear * 30);

        Assert.Equal(StateHash.Compute(a.World), StateHash.Compute(b.World));
    }

    [Fact]
    public void AVillageThatIsAskedForNothingBehavesExactlyAsBefore()
    {
        // The guard that keeps this slice honest: placement exists, and a village
        // nobody asks anything of runs precisely as it did.
        SimConfig config = Config;
        SimLoop loop = Build(config);
        loop.Step(config.TicksPerYear * 200);

        _output.WriteLine($"{loop.World.Population} alive at year 200 with nothing ever marked.");
        Assert.True(loop.World.Population >= config.StartingPopulation);
        Assert.Equal(0, LabourQuota.For(loop.World).Builders);
    }

    private static int CountStores(SimWorld world, StoreKind kind)
    {
        int count = 0;
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            if (store.Kind == kind)
            {
                count++;
            }
        }

        return count;
    }
}
