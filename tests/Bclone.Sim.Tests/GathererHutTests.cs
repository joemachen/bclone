using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The gatherer's hut, and food that depends on the trees around it —
/// <c>specs/forests-and-gathering.md</c>, slice 2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe:</b> <em>"The gatherer's hut should have a maximum gatherable area in a ring and
/// workers cannot gather outside that ring — the number of trees/forest in the circle has a
/// relation to the volume of food gathered. Less trees = less food available to gather."</em>
/// </para>
/// <para>
/// <b>⭐ It is the first building in this game whose yield depends on the ground around it</b>,
/// and that is what makes the harvest brush cost something: timber and food come out of the same
/// wood now, so felling beside your gatherers is spending food to get logs.
/// </para>
/// <para>
/// <b>Forage sites still exist alongside it</b>, deliberately — a new profession moves no golden
/// until somebody places its building (`professions.md §9.1`). They retire in slice 5.
/// </para>
/// </remarks>
public sealed class GathererHutTests
{
    private readonly ITestOutputHelper _output;

    public GathererHutTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    /// <summary>Raise a gatherer's hut outright, without waiting years for a builder.</summary>
    private static Workplace RaiseAHut(SimWorld world, GridPos at)
    {
        Assert.True(world.Mark(BuildingKind.GathererHut, at).Allowed, $"Could not mark at {at}.");

        Workplace site = Assert.Single(
            world.Workplaces, place => place.Construction?.Kind == BuildingKind.GathererHut);

        site.Construction!.Deliver(site.Construction.Recipe.Logs);
        for (int i = 0; i <= site.Construction.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        world.Complete(site);

        return Assert.Single(
            world.Workplaces, place => place.GatheringRadius > 0 && place.Position == at);
    }

    /// <summary>The most wooded buildable tile within a short walk — where a player would site it.</summary>
    /// <remarks>
    /// ⚠️ <b>Ranked on the cheap question first, and asked the expensive one last.</b>
    /// <c>CanBuildAt</c> calls <c>TravelCost.CanReach</c>, and every reachability query against
    /// a fresh destination is a full Dijkstra over the valley — so asking it of all 169 tiles
    /// in the box cost <b>22 seconds per test</b>. Counting trees is a few hundred array reads;
    /// buildability is asked only of the candidates actually worth having, best first.
    /// </remarks>
    private static GridPos WoodiestSpotNear(SimWorld world, int reach)
    {
        GridPos site = world.Map.FoundingSite;
        var ranked = new List<(int Wooded, GridPos At)>();

        for (int dy = -reach; dy <= reach; dy++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                var at = new GridPos(site.X + dx, site.Y + dy);
                if (!world.Map.Contains(at) || world.HasSomethingToHarvest(at))
                {
                    continue;
                }

                ranked.Add((CountForest(world, at, world.Config.GathererHutRingTiles), at));
            }
        }

        // Woodiest first; ties by position, so the choice is a fact about the valley rather
        // than about iteration order.
        ranked.Sort((a, b) =>
        {
            int byWooded = b.Wooded.CompareTo(a.Wooded);
            if (byWooded != 0)
            {
                return byWooded;
            }

            int byY = a.At.Y.CompareTo(b.At.Y);
            return byY != 0 ? byY : a.At.X.CompareTo(b.At.X);
        });

        foreach ((int wooded, GridPos at) in ranked)
        {
            if (wooded > 0 && world.CanBuildAt(BuildingKind.GathererHut, at).Allowed)
            {
                return at;
            }
        }

        throw new Xunit.Sdk.XunitException("Nowhere near the village is both wooded and buildable.");
    }

    private static int CountForest(SimWorld world, GridPos centre, int radius)
    {
        int wooded = 0;
        for (int dy = -radius; dy <= radius; dy++)
        {
            int span = radius - Math.Abs(dy);
            for (int dx = -span; dx <= span; dx++)
            {
                var at = new GridPos(centre.X + dx, centre.Y + dy);
                if (world.Map.Contains(at) && world.Map.TerrainAt(at) == Terrain.Forest)
                {
                    wooded++;
                }
            }
        }

        return wooded;
    }

    // ---------------------------------------------------------------
    //  The building
    // ---------------------------------------------------------------

    /// <summary>It costs timber and work, unlike the two buildings that cannot be charged.</summary>
    /// <remarks>
    /// The pile and the builder's hut are free because nothing can be built without them — a
    /// circle. A gatherer's hut has no such circle, so it is the first thing the player spends
    /// logs on because they chose to eat better.
    /// </remarks>
    [Fact]
    public void ItCostsTimberAndWork()
    {
        BuildingRecipe recipe = BuildingRecipe.For(BuildingKind.GathererHut, Config);

        Assert.True(recipe.Logs > 0, "A free gatherer's hut is a free food supply.");
        Assert.True(recipe.WorkTicks > 0, "A hut that owes no work is an instant hut.");
    }

    /// <summary>Its seats are the ring priced in workers — derived, not typed (D16, D50, D86).</summary>
    [Fact]
    public void ItsSeatsAreItsRingPricedInWorkers()
    {
        SimConfig config = Config;
        SimWorld world = Loop(config).World;

        Workplace hut = RaiseAHut(world, WoodiestSpotNear(world, 6));

        Assert.Equal(VillageEconomy.GathererHutCapacity(config), hut.Capacity);
        Assert.True(hut.Capacity >= 1, "A hut with no seat can never gather anything.");

        // Anti-vacuity (D7): a bigger ring is more ground and therefore more hands, or this
        // is asserting against a constant.
        int wider = VillageEconomy.GathererHutCapacity(
            config with { GathererHutRingTiles = config.GathererHutRingTiles * 2 });

        _output.WriteLine($"ring {config.GathererHutRingTiles} → {hut.Capacity} seats; "
            + $"ring {config.GathererHutRingTiles * 2} → {wider}");

        Assert.True(wider > hut.Capacity, "The seats do not follow the ground they price.");
    }

    /// <summary>A gatherer's hut is a forager's workplace — the job kind is reused, not added to.</summary>
    [Fact]
    public void ItIsAForagersWorkplaceWithARing()
    {
        SimWorld world = Loop(Config).World;
        Workplace hut = RaiseAHut(world, WoodiestSpotNear(world, 6));

        Assert.Equal(JobKind.Forager, hut.Kind);
        Assert.Equal(Config.GathererHutRingTiles, hut.GatheringRadius);

        // ⚠️ THIS USED TO CHECK THE OPPOSITE, and the change is the branch rather than a
        // slip. It asserted that every OTHER forager workplace has no ring at all — true when
        // the other foragers were berry patches, which yield from the spot you stand on and
        // so have nothing to a ring. Berry patches are retired. The only other forager
        // workplace in the valley is the founding gatherer's hut, and it failed at 8 against
        // an expected 0 by having exactly the ring this test exists to require.
        //
        // So it says the rule instead of the exception: a ring is what a gatherer's hut IS,
        // and every one of them has the same one, however it got onto the map.
        int huts = 0;
        foreach (Workplace other in world.Workplaces)
        {
            if (other.Kind == JobKind.Forager)
            {
                Assert.Equal(Config.GathererHutRingTiles, other.GatheringRadius);
                huts++;
            }
        }

        Assert.True(huts > 1, "Only the hut this test raised, so nothing was compared.");
    }

    // ---------------------------------------------------------------
    //  ⭐ Less trees, less food
    // ---------------------------------------------------------------

    /// <summary>⭐ Felling the ring costs the hut its yield, in proportion.</summary>
    /// <remarks>
    /// <b>Linear, and with no floor</b> — *"half the trees, half the food"* is a sentence a
    /// player can hold in their head while deciding whether to fell the wood beside their hut.
    /// </remarks>
    [Fact]
    public void FellingTheRingCostsTheHutItsYield()
    {
        SimWorld world = Loop(Config).World;
        Workplace hut = RaiseAHut(world, WoodiestSpotNear(world, 6));

        int woodedBefore = world.WoodedTilesAround(hut);
        int yieldBefore = world.GatherYieldAt(hut);

        Assert.True(woodedBefore > 0, "The hut was sited on bare ground, so nothing was tested.");
        Assert.True(yieldBefore > 0, "A wooded hut that yields nothing is already broken.");

        // Fell half of it, one tile at a time, through the one door terrain changes by.
        int felled = 0;
        int target = woodedBefore / 2;
        int radius = hut.GatheringRadius;

        for (int dy = -radius; dy <= radius && felled < target; dy++)
        {
            int span = radius - Math.Abs(dy);
            for (int dx = -span; dx <= span && felled < target; dx++)
            {
                var at = new GridPos(hut.Position.X + dx, hut.Position.Y + dy);
                if (world.Map.Contains(at) && world.Map.TerrainAt(at) == Terrain.Forest)
                {
                    world.SetTerrain(at, Terrain.Grass);
                    felled++;
                }
            }
        }

        int yieldAfter = world.GatherYieldAt(hut);

        _output.WriteLine($"{woodedBefore} wooded → {world.WoodedTilesAround(hut)} after felling "
            + $"{felled}; a trip was worth {yieldBefore}, now {yieldAfter}");

        Assert.Equal(woodedBefore - felled, world.WoodedTilesAround(hut));
        Assert.True(yieldAfter < yieldBefore, "Felling half the ring cost the hut nothing.");
    }

    /// <summary>⭐ No forest, no food — and the rule has no floor under it.</summary>
    /// <remarks>
    /// <b>The rule Joe asked for by name.</b> A floor would be kinder and would make it untrue.
    /// The safety is that the hut's panel says what its ring holds and what that is worth, not
    /// that the number is softened.
    /// </remarks>
    [Fact]
    public void ABaldRingFeedsNobody()
    {
        SimWorld world = Loop(Config).World;
        Workplace hut = RaiseAHut(world, WoodiestSpotNear(world, 6));

        Assert.True(world.GatherYieldAt(hut) > 0, "Nothing was tested — the ring was bare already.");

        int radius = hut.GatheringRadius;
        for (int dy = -radius; dy <= radius; dy++)
        {
            int span = radius - Math.Abs(dy);
            for (int dx = -span; dx <= span; dx++)
            {
                var at = new GridPos(hut.Position.X + dx, hut.Position.Y + dy);
                if (world.Map.Contains(at) && world.Map.TerrainAt(at) == Terrain.Forest)
                {
                    world.SetTerrain(at, Terrain.Grass);
                }
            }
        }

        Assert.Equal(0, world.WoodedTilesAround(hut));
        Assert.Equal(0, world.GatherYieldAt(hut));
    }

    /// <summary>Nothing outside the ring counts, however much woodland is out there.</summary>
    [Fact]
    public void TreesOutsideTheRingAreWorthNothing()
    {
        SimWorld world = Loop(Config).World;
        Workplace hut = RaiseAHut(world, WoodiestSpotNear(world, 6));

        int before = world.WoodedTilesAround(hut);
        int radius = hut.GatheringRadius;

        // Plant a wall of trees just beyond the ring.
        int planted = 0;
        for (int dy = -(radius + 3); dy <= radius + 3; dy++)
        {
            for (int dx = -(radius + 3); dx <= radius + 3; dx++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) <= radius)
                {
                    continue;
                }

                var at = new GridPos(hut.Position.X + dx, hut.Position.Y + dy);
                if (world.Map.Contains(at) && world.Map.TerrainAt(at) == Terrain.Grass)
                {
                    world.SetTerrain(at, Terrain.Forest);
                    planted++;
                }
            }
        }

        _output.WriteLine($"planted {planted} tiles outside the ring; "
            + $"the hut still counts {world.WoodedTilesAround(hut)} of {before}");

        Assert.True(planted > 0, "Nothing was planted, so nothing was tested.");
        Assert.Equal(before, world.WoodedTilesAround(hut));
    }

    // ---------------------------------------------------------------
    //  The cache
    // ---------------------------------------------------------------

    /// <summary>⚠️ The cached count is dropped by the one door terrain changes by (D85).</summary>
    /// <remarks>
    /// <b>The count exists because the alternative is a per-gather O(R²) scan</b>, and D87 is
    /// the recorded case of what a per-tick per-villager scan costs. A cache that is not
    /// invalidated is worse than no cache: it is a second source of truth about the ground.
    /// </remarks>
    [Fact]
    public void TheCountIsDroppedWhenTheGroundChanges()
    {
        SimWorld world = Loop(Config).World;
        Workplace hut = RaiseAHut(world, WoodiestSpotNear(world, 6));

        int before = world.WoodedTilesAround(hut);

        // Ask twice, so the second answer is definitely the cached one.
        Assert.Equal(before, world.WoodedTilesAround(hut));

        GridPos tree = FirstForestIn(world, hut);
        world.SetTerrain(tree, Terrain.Grass);

        Assert.Equal(before - 1, world.WoodedTilesAround(hut));
    }

    /// <summary>The cache never becomes a second source of truth — two runs agree.</summary>
    [Fact]
    public void TheCountIsNotPartOfTheHash()
    {
        SimWorld asked = Loop(Config).World;
        SimWorld untouched = Loop(Config).World;

        Workplace hut = RaiseAHut(asked, WoodiestSpotNear(asked, 6));
        RaiseAHut(untouched, WoodiestSpotNear(untouched, 6));

        // One of them has had its ring counted; the other has never been asked. If the count
        // were hashed — or if asking a question changed the world — these would differ.
        _output.WriteLine($"one hut counted {asked.WoodedTilesAround(hut)} wooded tiles; "
            + "the other was never asked");

        Assert.Equal(StateHash.Compute(untouched), StateHash.Compute(asked));
    }

    private static GridPos FirstForestIn(SimWorld world, Workplace hut)
    {
        int radius = hut.GatheringRadius;
        for (int dy = -radius; dy <= radius; dy++)
        {
            int span = radius - Math.Abs(dy);
            for (int dx = -span; dx <= span; dx++)
            {
                var at = new GridPos(hut.Position.X + dx, hut.Position.Y + dy);
                if (world.Map.Contains(at) && world.Map.TerrainAt(at) == Terrain.Forest)
                {
                    return at;
                }
            }
        }

        throw new Xunit.Sdk.XunitException("The hut's ring has no woodland in it.");
    }
}
