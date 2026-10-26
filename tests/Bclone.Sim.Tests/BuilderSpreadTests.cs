using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ Builders spread across the queue (Joe, D602; `multi-material-construction.md §4.1`): each takes the first site in
/// queue order nobody else is serving — the head first, surplus only behind it.
/// </summary>
/// <remarks>
/// Joe, on his Year-70 village: <i>"despite having 3 builders, it takes a long time (months!) for a builder to start
/// building after a building blueprint/ghost is placed."</i> Every builder served the head, so his nine sites went up one
/// at a time with the timber for all of them in store.
/// </remarks>
public sealed class BuilderSpreadTests
{
    private readonly ITestOutputHelper _output;

    public BuilderSpreadTests(ITestOutputHelper output) => _output = output;

    /// <summary>A village with two sites marked, plenty of every material in store, and two villagers kept on building.</summary>
    private static (SimLoop Loop, Workplace First, Workplace Second, Villager A, Villager B) TwoSitesTwoBuilders()
    {
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;

        Assert.True(world.Mark(BuildingKind.Granary, ClearGroundNear(world, BuildingKind.Granary)).Allowed);
        Assert.True(world.Mark(BuildingKind.Warehouse, ClearGroundNear(world, BuildingKind.Warehouse)).Allowed);
        List<Workplace> queue = world.BuildQueue();
        Assert.Equal(2, queue.Count);

        StoreBuilding store = world.StoreBuildings.First(s => s.Gives(Goods.Logs) && s.Gives(Goods.Stone));
        store.Store.Receive(Goods.Logs, 400);
        store.Store.Receive(Goods.Stone, 400);

        Villager[] builders = world.Villagers.Where(v => v.Alive && v.CanWork).Take(2).ToArray();
        foreach (Villager builder in builders)
        {
            world.SetPinnedTrade(builder, JobKind.Builder);
        }

        // A day first: on tick 0 nobody is seated yet (the allocator seats a pin on its pass).
        loop.Step(world.Config.TicksPerDay);

        // ⚠️ The premise: both are seated at the builder's hut, not merely pinned.
        Assert.All(builders, b => Assert.Equal(JobKind.Builder, world.FindWorkplace(b.WorkplaceId)?.Kind));
        return (loop, queue[0], queue[1], builders[0], builders[1]);
    }

    /// <summary>
    /// ⭐⭐ The one Joe felt: two builders, two paid-for sites — <b>the second is begun before the first stands</b>. With
    /// the whole crew on the head it sat untouched until the head was raised.
    /// </summary>
    [Fact]
    public void TheSecondSiteIsBegunBeforeTheFirstStands()
    {
        (SimLoop loop, Workplace first, Workplace second, _, _) = TwoSitesTwoBuilders();
        SimWorld world = loop.World;
        bool secondBegun = false;

        for (int t = 0; t < world.Config.TicksPerYear && first.Construction is { IsFinished: false }
            && world.Workplaces.Contains(first); t++)
        {
            loop.StepOnce();
            if (second.Construction is { } plan && (plan.WorkDone > 0 || plan.Delivered(Goods.Logs) > 0))
            {
                secondBegun = true;
            }
        }

        _output.WriteLine($"tick {world.Tick}: first {(world.Workplaces.Contains(first) ? "standing as a site" : "raised")}, second begun {secondBegun}");
        Assert.False(world.Workplaces.Contains(first) && first.Construction is { IsFinished: false }, "The first site was not raised in a year.");
        Assert.True(secondBegun, "The second site was not touched before the first was raised — the whole crew worked the head.");
    }

    /// <summary>⭐ The head is taken first, and the next builder takes the next site.</summary>
    [Fact]
    public void TheHeadIsTakenFirstAndTheNextBuilderTakesTheNext()
    {
        (SimLoop loop, Workplace first, Workplace second, Villager a, Villager b) = TwoSitesTwoBuilders();
        SimWorld world = loop.World;
        a.ServingSiteId = 0;
        b.ServingSiteId = 0;

        Assert.Equal(first.Id, world.NextSiteToServe(a)?.Id);

        a.ServingSiteId = first.Id;
        Assert.Equal(second.Id, world.NextSiteToServe(b)?.Id);

        // And a builder further back moves up the moment the site ahead loses its builder.
        a.ServingSiteId = 0;
        b.ServingSiteId = second.Id;
        Assert.Equal(first.Id, world.NextSiteToServe(b)?.Id);

        // ⚠️ A builder's own site is not "somebody else's": alone on the head, they keep it (D602's B7 scored zero
        // without this — counting oneself as served sends every builder off their own site to the next).
        b.ServingSiteId = first.Id;
        Assert.Equal(first.Id, world.NextSiteToServe(b)?.Id);
    }

    /// <summary>
    /// ⛔ D102's line: <b>scarce timber still goes where the queue points.</b> With logs for the head and not one more, the
    /// second builder doubles up at the head instead of starting the site behind it.
    /// </summary>
    [Fact]
    public void ScarceTimberStaysWithTheHead()
    {
        (SimLoop loop, Workplace first, _, Villager a, Villager b) = TwoSitesTwoBuilders();
        SimWorld world = loop.World;
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TakeAll(Goods.Logs);
        }

        StoreBuilding holder = world.StoreBuildings.First(s => s.Gives(Goods.Logs));
        holder.Store.Receive(Goods.Logs, first.Construction!.StillNeeded(Goods.Logs));

        // ⚠️ The premise: both sites want logs next.
        Assert.Equal(Goods.Logs, first.Construction.NextMaterialWanted());
        a.ServingSiteId = first.Id;
        Assert.Equal(first.Id, world.NextSiteToServe(b)?.Id);

        // One log more than the head owes is not enough either: the site behind wants its whole recipe's worth spare.
        holder.Store.Receive(Goods.Logs, 1);
        Assert.Equal(first.Id, world.NextSiteToServe(b)?.Id);
    }

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
                    // ⚠️ THE WHOLE FOOTPRINT CLEAR, NOT ITS ANCHOR (D623): a 2×2 with a tree under one corner waits for the
                    // clearing (B5), and `NextSiteToServe` rightly skips a site still waiting. The anchor alone held while the
                    // first clear anchor happened to have clear neighbours; doors refuse a doorstep, and the search moved.
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (!world.FootprintOf(kind, at).CoveredTiles().Any(world.HasSomethingToHarvest) && world.CanBuildAt(kind, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No clear ground near the founding site.");
    }
}
