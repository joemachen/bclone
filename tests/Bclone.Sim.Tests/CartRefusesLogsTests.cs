using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The cart is a food-and-tools box — D90 step 4, landed on D96's two preconditions.
/// </summary>
/// <remarks>
/// <para>
/// Joe, rewriting the opening: <em>"Founders arrive with no logs at all… they harvest logs,
/// take them to the building site."</em> A cart that cannot hold timber cannot be strangled by
/// it, which is what makes the constraint structural rather than a warning the player must
/// read — and it gives the storage pile its reason back: <b>you cannot take timber until you
/// have somewhere to put it.</b>
/// </para>
/// <para>
/// <b>This was built and reverted once (D95).</b> A pile was a construction site, so between
/// marking one and it standing a forester had nowhere on earth to put a load: 0 homes, nothing
/// built at all. D96's two steps are what make it safe — a load can be set down (D97), and the
/// pile goes up the tick it is marked (D98).
/// </para>
/// </remarks>
public sealed class CartRefusesLogsTests
{
    private readonly ITestOutputHelper _output;

    public CartRefusesLogsTests(ITestOutputHelper output) => _output = output;

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    private static void PaintHomes(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        for (int dy = -4; dy <= 4; dy++)
        {
            for (int dx = -4; dx <= 4; dx++)
            {
                world.PaintResidential(new GridPos(site.X + dx, site.Y + dy));
            }
        }
    }

    private static int PaintTrees(SimWorld world, int radius)
    {
        GridPos site = world.Map.FoundingSite;
        int painted = 0;
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (world.PaintHarvest(new GridPos(site.X + dx, site.Y + dy)).Allowed)
                {
                    painted++;
                }
            }
        }

        return painted;
    }

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

    // ---------------------------------------------------------------
    //  What the cart will and will not take
    // ---------------------------------------------------------------

    /// <summary>The cart takes food and tools, and refuses timber.</summary>
    [Fact]
    public void TheCartTakesEverythingExceptTimber()
    {
        SimWorld world = Loop(ShippedConfig.Load()).World;
        StoreBuilding cart = world.TheCart!;

        Assert.True(cart.Accepts(Goods.Food));
        Assert.True(cart.Accepts(Goods.Tools));
        Assert.True(cart.Accepts(Goods.Firewood));
        Assert.True(cart.Accepts(Goods.Stone));
        Assert.False(cart.Accepts(Goods.Logs));
    }

    /// <summary>And the founders bring none, because the key that loaded them is gone.</summary>
    [Fact]
    public void TheFoundersArriveWithNoTimberAtAll()
    {
        SimWorld world = Loop(ShippedConfig.Load()).World;

        Assert.Equal(0, world.TheCart!.Store.Logs);
        Assert.Equal(0, world.LogsInWarehouses());
        Assert.Equal(0, world.TotalLogs());
    }

    /// <summary>A forester with a full cart still gets rid of the load, one way or another.</summary>
    /// <remarks>
    /// <b>D95's failure in one assertion.</b> The change was reverted because a forester with
    /// nowhere to put timber had nowhere to put timber — full stop, forever. Now the load
    /// either reaches a store or reaches the ground, and either way it stops being carried.
    /// </remarks>
    [Fact]
    public void TimberAlwaysEndsUpSomewhere()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        PaintHomes(world);
        int painted = PaintTrees(world, 10);
        Assert.True(painted > 0);

        loop.Step(config.TicksPerYear / 2);

        int carried = 0;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            carried += world.Villagers[i].CarriedLogs;
        }

        int onGround = 0;
        for (int i = 0; i < world.GroundStacks.Count; i++)
        {
            if (world.GroundStacks[i].Goods == Goods.Logs)
            {
                onGround += world.GroundStacks[i].Amount;
            }
        }

        _output.WriteLine(
            $"half a year, no store placed: {onGround} logs on the ground, {carried} still "
            + $"carried, {world.LogsInWarehouses()} in stores, {world.Population} alive");

        Assert.True(onGround > 0, "Trees were painted and no timber ever reached the ground.");
        Assert.Equal(0, world.TheCart!.Store.Logs);
    }

    // ---------------------------------------------------------------
    //  D89's fatal arm
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ Painting a forest with only the cart no longer strangles the village in silence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>D89, measured over forty years on the shipped config.</b> Four arms, and one was a
    /// disaster: <em>no pile + harvest painted</em> ran <b>4 → 6 → 2</b> against 8 or 9 for
    /// every other arm. The cart filled with timber — 677 logs of its 1,200 by year five — and
    /// the food it crowded out never arrived: <b>164 food against 400+</b>. The village stopped
    /// having children and aged out with <b>zero starved and zero frozen</b>, which is a
    /// legibility failure before it is a balance one and exactly the death §0.1 rules out.
    /// </para>
    /// <para>
    /// <b>The fix is structural rather than a warning: a cart that cannot hold logs cannot be
    /// crowded by them.</b> What this asserts is the mechanism, not the survival — a founding
    /// with nowhere to keep timber still cannot build a woodcutter's hut, and that is D90's
    /// design working. What must not happen again is the food being squeezed out.
    /// </para>
    /// </remarks>
    [Fact]
    public void PaintingAForestNoLongerCrowdsTheFoodOutOfTheCart()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        PaintHomes(world);
        PaintTrees(world, 10);

        loop.Step(config.TicksPerYear * 5);

        StoreBuilding cart = world.TheCart!;
        _output.WriteLine(
            $"five years, trees painted and no store placed: the cart holds {cart.Store.Food} "
            + $"food and {cart.Store.Logs} logs of {cart.Store.Capacity}; "
            + $"{world.TotalFood()} food in the village");

        Assert.Equal(0, cart.Store.Logs);

        // D89's arm ran the village down to 164 food by year five while every other arm held
        // 400+. The bar is that figure, generously: what killed it was timber in the wagon.
        Assert.True(
            world.TotalFood() > 400,
            $"The village is down to {world.TotalFood()} food with trees painted — D89's "
            + "silent strangling, back.");
    }

    /// <summary>
    /// ⭐ And when there is nowhere to keep timber, the village says so.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is what keeps step 4 fair, and it ships in the same slice for D89's reason.</b>
    /// Goods on the ground are supply-invisible by design, so a founding that paints trees and
    /// places no store fells timber into a field where <c>LogsInWarehouses</c> cannot see it — and
    /// the hut reports <em>"no logs here to split"</em> while four hundred logs lie about.
    /// Silence there would be the untraceable failure §1.1 forbids and D88 rules out twice.
    /// </para>
    /// <para>
    /// <b>Once, not every tick</b> — D42's rule about the distance warning, and the reason it
    /// fires on <em>nowhere at all</em> rather than on <em>full</em>: a village whose stores
    /// are packed can see that in its stores.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheVillageSaysWhenItHasNowhereToKeepTimber()
    {
        SimConfig config = ShippedConfig.Load();
        var log = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(config, log);
        SimWorld world = loop.World;

        PaintHomes(world);
        PaintTrees(world, 10);
        loop.Step(config.TicksPerYear / 2);

        int said = 0;
        foreach (LogEntry entry in log.Entries)
        {
            if (entry.Subsystem == "life" && entry.Message.Contains(
                    "nowhere in the village to keep logs", System.StringComparison.Ordinal))
            {
                said++;
                _output.WriteLine($"t{entry.Tick}: {entry.Message}");
            }
        }

        Assert.Equal(1, said);
    }

    /// <summary>…and does not say it once the player has given the timber a home.</summary>
    [Fact]
    public void ItSaysNothingWhenThereIsAPile()
    {
        SimConfig config = ShippedConfig.Load();
        var log = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(config, log);
        SimWorld world = loop.World;

        PaintHomes(world);
        MarkSomewhereNear(world, BuildingKind.Pile, world.Map.FoundingSite, 2);
        PaintTrees(world, 10);
        loop.Step(config.TicksPerYear / 2);

        foreach (LogEntry entry in log.Entries)
        {
            Assert.DoesNotContain(
                "nowhere in the village to keep logs", entry.Message, System.StringComparison.Ordinal);
        }

        _output.WriteLine(
            $"with a pile: {world.LogsInWarehouses()} logs in reach after half a year");
        Assert.True(world.LogsInWarehouses() > 0);
    }
}
