using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ Commit 5 of `specs/trade.md §11` (D595): <b>the boat</b> — `trade.md §4.4–4.5`. Its clock by hash (every 2–3
/// years, spring to autumn, from either end of the river); it appears, docks and leaves on three ticks; it settles the
/// standing orders the day it docks at the day's rate — integers conserved, never more than its hold, never a good the
/// village sells, animals only to a barn with room; <i>Send the boat on</i> moves its leaving tick; and it is told.
/// </summary>
public sealed class TradeBoatTests
{
    private readonly ITestOutputHelper _output;

    public TradeBoatTests(ITestOutputHelper output) => _output = output;

    private static SimLoop AVillage(ulong seed = 12345, InMemoryLogSink? log = null) =>
        SimFactory.CreatePhase0(ShippedConfig.Established() with { Seed = seed }, log ?? new InMemoryLogSink());

    /// <summary>A post raised and the region woken at the next season turn — the first boat scheduled.</summary>
    private static (SimLoop Loop, Workplace Post, StoreBuilding Store) APostAwake(ulong seed = 12345, InMemoryLogSink? log = null)
    {
        SimLoop loop = AVillage(seed, log);
        Workplace post = TradeTests.RaiseAPost(loop.World);
        while (loop.World.RegionAwakeSince == 0)
        {
            loop.StepOnce();
        }

        return (loop, post, loop.World.PostStoreOf(post)!);
    }

    /// <summary>The boat from the hills (neighbour 0, upriver) tying up on the very next tick.</summary>
    private static void ABoatDocksNext(SimWorld world, int neighbour = 0) =>
        world.Boat = new TradeBoat
        {
            NeighbourId = neighbour,
            AppearsAtTick = world.Tick,
            DocksAtTick = world.Tick,
            LeavesAtTick = world.Tick + world.BoatDockTicks,
        };

    private static int YearOf(SimWorld world, ulong tick) => (int)(tick / (ulong)world.Config.TicksPerYear);

    /// <summary>
    /// The save pose (`SaveLoadTests`): a post and a barn with room, orders for firewood out and stone and beasts in, and
    /// a boat that docks at tick 20 and leaves twenty ticks into the run's last stretch — so a save at 60 is mid-visit.
    /// </summary>
    internal static void PoseABoatInDock(SimLoop loop)
    {
        SimWorld world = loop.World;
        Workplace post = TradeTests.RaiseAPost(world);
        Workplace barn = HerdTests.RaiseABarn(world);
        HerdTests.GivePasture(world, barn, 20);
        world.RegionAwakeSince = 1;
        foreach (MaterialCost made in world.Config.Neighbours[0].Makes)
        {
            world.NeighbourStocks[0].Held[(int)made.Goods] = made.Amount;
        }

        world.NeighbourStocks[0].Animals = 6;
        world.TradeKeep.Set(Goods.Firewood, 0);
        world.TradeBuy.Set(Goods.Stone, 30);
        Assert.True(world.SetTradeBuyHead(2).Allowed);
        world.PostStoreOf(post)!.Put(Goods.Firewood, 500);
        world.Boat = new TradeBoat { NeighbourId = 0, AppearsAtTick = 12, DocksAtTick = 20, LeavesAtTick = 80 };
    }

    // ---------------------------------------------------------------
    //  The clock — by hash, every 2–3 years, spring to autumn, either end
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ <b>Every two or three years, never in winter, from both ends of the river</b> (Joe, D587 calls 3 and 6): two
    /// hundred visits in turn, each scheduled from the last one's going — the gap in years is 2 or 3, the whole visit
    /// (sailing in, docked, sailing on) falls in spring to autumn of one year, both river neighbours send boats, and the
    /// overland one never does (its caravan waits for carts).
    /// </summary>
    [Fact]
    public void BoatsComeEveryTwoOrThreeYearsFromBothEndsAndNeverInWinter()
    {
        SimWorld world = AVillage().World;
        var config = world.Config;
        ulong winterStarts = (ulong)(config.TicksPerSeason * 3);
        var from = new HashSet<int>();
        var gaps = new HashSet<int>();
        ulong after = 0;
        for (int visit = 0; visit < 200; visit++)
        {
            TradeBoat boat = world.NextBoat(after, visit) ?? throw new Xunit.Sdk.XunitException("No boat scheduled.");
            int gap = YearOf(world, boat.AppearsAtTick) - YearOf(world, after);
            gaps.Add(gap);
            Assert.InRange(gap, config.TradeBoatGapYearsFloor, config.TradeBoatGapYearsFloor + config.TradeBoatGapYearsSpread);

            ulong gone = boat.LeavesAtTick + world.BoatSailTicks;
            ulong yearStart = (ulong)YearOf(world, boat.AppearsAtTick) * (ulong)config.TicksPerYear;
            Assert.True(gone < yearStart + winterStarts, $"Visit {visit} is still on the river in winter (gone at {gone}).");
            Assert.Equal(boat.AppearsAtTick + world.BoatSailTicks, boat.DocksAtTick);
            Assert.Equal(boat.DocksAtTick + world.BoatDockTicks, boat.LeavesAtTick);
            Assert.NotEqual(NeighbourDirection.Overland, config.Neighbours[boat.NeighbourId].Direction);
            from.Add(boat.NeighbourId);
            after = gone;
        }

        Assert.Equal(new[] { 0, 1 }, from.OrderBy(n => n));
        Assert.Equal(new[] { 2, 3 }, gaps.OrderBy(g => g));
    }

    /// <summary>⭐ Same seed, same boats — and another seed, other boats. A hash, never a draw (D344).</summary>
    [Fact]
    public void TheSameSeedBringsTheSameBoats()
    {
        SimWorld one = AVillage(7).World, again = AVillage(7).World, other = AVillage(8).World;
        bool differs = false;
        for (int visit = 0; visit < 20; visit++)
        {
            ulong after = (ulong)visit * 1000UL;
            Assert.Equal(one.NextBoat(after, visit), again.NextBoat(after, visit));
            differs |= one.NextBoat(after, visit) != other.NextBoat(after, visit);
        }

        Assert.True(differs, "Two seeds brought the same twenty boats.");
    }

    /// <summary>
    /// No boat before a post stands; the first comes two or three years after the region wakes with it, and says when.
    /// </summary>
    [Fact]
    public void TheFirstBoatIsScheduledWhenThePostWakesTheRegion()
    {
        SimLoop asleep = AVillage();
        asleep.Step(asleep.World.Config.TicksPerYear);
        Assert.Null(asleep.World.Boat);
        Assert.Equal("No boat will come until a trading post stands.", asleep.World.SayWhenTheBoatIsDue());

        (SimLoop loop, _, _) = APostAwake();
        SimWorld world = loop.World;
        TradeBoat first = world.Boat ?? throw new Xunit.Sdk.XunitException("Waking the region scheduled no boat.");
        Assert.Equal(world.NextBoat(world.RegionAwakeSince, 0), first);
        Assert.InRange(YearOf(world, first.AppearsAtTick) - YearOf(world, world.RegionAwakeSince), 2, 3);
        _output.WriteLine(world.SayWhenTheBoatIsDue());
        Assert.StartsWith("No boat is expected before", world.SayWhenTheBoatIsDue(), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  The visit — appear, dock and settle, leave, and the next scheduled
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ A whole visit run through the loop: it is told coming, it settles the day it docks, it is told leaving, and when
    /// it has sailed off the map the next boat is scheduled from the visit count.
    /// </summary>
    [Fact]
    public void AVisitIsToldAndTheNextBoatIsScheduledWhenItHasGone()
    {
        var log = new InMemoryLogSink();
        (SimLoop loop, _, _) = APostAwake(log: log);
        SimWorld world = loop.World;
        TradeBoat boat = world.Boat!;
        string name = world.NeighbourName(boat.NeighbourId);
        while (world.Tick <= boat.DocksAtTick)
        {
            loop.StepOnce();
        }

        Assert.True(world.BoatIsDocked);
        Assert.Equal(1, world.BoatsEverCame);
        Assert.Contains(log.Entries, e => e.Message.StartsWith($"A boat from {name} is coming ", StringComparison.Ordinal));
        Assert.Contains(log.Entries, e => e.Message.StartsWith($"The boat from {name} ", StringComparison.Ordinal) && e.Tick == boat.DocksAtTick);

        while (world.Tick < boat.LeavesAtTick + world.BoatSailTicks + 1)
        {
            loop.StepOnce();
        }

        Assert.False(world.BoatIsDocked);
        Assert.Contains(log.Entries, e => e.Message.StartsWith($"The boat from {name} is leaving", StringComparison.Ordinal));
        Assert.Equal(world.NextBoat(boat.LeavesAtTick + world.BoatSailTicks, 1), world.Boat);
    }

    // ---------------------------------------------------------------
    //  Settling the orders
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ <b>Settled at the day's rate, integers conserved</b>: what left the post is what the neighbour gained, what came
    /// is what it lost, the village paid no less than what it got and no more than one unit over — and what was not
    /// needed to pay stays at the post. The buy order is met and never exceeded.
    /// </summary>
    [Fact]
    public void TheOrdersSettleAtTheDaysRateWithEveryUnitAccountedFor()
    {
        var log = new InMemoryLogSink();
        (SimLoop loop, _, StoreBuilding post) = APostAwake(log: log);
        SimWorld world = loop.World;
        world.TradeKeep.Set(Goods.Firewood, 0);
        world.TradeBuy.Set(Goods.Stone, 30);
        Assert.Equal(200, post.Put(Goods.Firewood, 200));

        // ⚠️ Ten stone already held, so the order wants twenty — and the hills a quarter stocked with firewood, so it is
        // offered at 175, not a round 200: a rate of 1:1 hides both a wrong "wanted" and a payment rounded the wrong way.
        StoreBuilding warehouse = world.StoreBuildings.First(store => store.Accepts(Goods.Stone));
        Assert.Equal(10, warehouse.Put(Goods.Stone, 10));
        world.NeighbourStocks[0].Held[(int)Goods.Firewood] = 50;
        int stoneBefore = world.InStores(Goods.Stone);
        Assert.Equal(10, stoneBefore);
        NeighbourStock hills = world.NeighbourStocks[0];
        int hillsFirewood = hills.Held[(int)Goods.Firewood], hillsStone = hills.Held[(int)Goods.Stone];
        int offer = world.OfferOf(0, Goods.Firewood)!.Value, ask = world.AskOf(0, Goods.Stone)!.Value;
        Assert.NotEqual(0, (20 * ask) % offer); // the pose's premise: the payment does not come out even
        ABoatDocksNext(world);

        loop.StepOnce();

        int took = 200 - post.Store[Goods.Firewood];
        int left = post.Store[Goods.Stone];
        _output.WriteLine($"took {took} firewood at {offer}, left {left} stone at {ask}");
        Assert.Equal(30 - stoneBefore, left);
        Assert.Equal(hillsFirewood + took, hills.Held[(int)Goods.Firewood]);
        Assert.Equal(hillsStone - left, hills.Held[(int)Goods.Stone]);
        Assert.True((long)took * offer >= (long)left * ask, "The village paid less than it got.");
        Assert.True((long)(took - 1) * offer < (long)left * ask, "The village paid a unit more than it needed to.");
        Assert.True(post.Store[Goods.Firewood] > 0, "Nothing was left at the post — the boat took more than it paid for.");
        Assert.Contains(log.Entries, e => e.Message.Contains($"took {took} firewood and left {left} stone", StringComparison.Ordinal));
    }

    /// <summary>
    /// The boat takes only what its neighbour wants, and only goods with a keep order (for sale) — never a good the
    /// village means to bring home — and <b>no more than its hold</b>: with firewood cheap (the hills have plenty) and
    /// stone wanted without end, the hold is what stops it.
    /// </summary>
    [Fact]
    public void TheBoatTakesOnlyWhatIsForSaleAndWantedUpToItsHold()
    {
        (SimLoop loop, _, StoreBuilding post) = APostAwake();
        SimWorld world = loop.World;
        world.TradeKeep.Set(Goods.Fish, 0);
        world.TradeKeep.Set(Goods.Logs, 0);                 // for sale, but the hills make logs — they do not want them
        world.TradeBuy.Set(Goods.Leather, 10_000);          // as much leather as it will part with
        world.TradeBuy.Set(Goods.Logs, 10_000);
        post.Put(Goods.Fish, 450);
        post.Put(Goods.Logs, 50);

        // ⚠️ The hills want firewood — but it has no keep order: going home. Posed BEFORE the fish in id order, because
        // the boat pays good by good in id order: a wrongly sellable good after the fish is never reached (D595's B7
        // scored zero with the two the other way round).
        post.Put(Goods.Firewood, 30);
        Assert.True(Goods.Firewood < Goods.Fish);
        world.NeighbourStocks[0].Held[(int)Goods.Fish] = 125;      // at its size: offered at its worth, 100
        world.NeighbourStocks[0].Held[(int)Goods.Leather] = 5_000; // a cargo bigger than any hold, at half its worth: 150
        int offer = world.OfferOf(0, Goods.Fish)!.Value, ask = world.AskOf(0, Goods.Leather)!.Value;

        // ⚠️ The premise: all 450 fish would buy more leather (300) than the post has room for (270), and the hold's
        // 400 buys less (266) — so the hold, and nothing else, is what decides the trade.
        Assert.True(450L * offer / ask > post.Store.FreeSpace && 400L * offer / ask < post.Store.FreeSpace);
        ABoatDocksNext(world);

        loop.StepOnce();

        int took = 450 - post.Store[Goods.Fish];
        Assert.Equal(400 * offer / ask, post.Store[Goods.Leather]);
        Assert.InRange(took, 1, world.Config.TradeBoatHold);
        Assert.Equal(50, post.Store[Goods.Logs]);           // not wanted, so not taken — and it has a keep order, so none bought
        Assert.Equal(30, post.Store[Goods.Firewood]);
    }

    /// <summary>
    /// <b>No more than its hold, the other way</b>: three hundred bread to a neighbour short of it pays for six hundred
    /// stone, the post has room for five hundred — and the boat leaves its hold, four hundred.
    /// </summary>
    [Fact]
    public void TheBoatLeavesNoMoreThanItsHold()
    {
        (SimLoop loop, _, StoreBuilding post) = APostAwake();
        SimWorld world = loop.World;
        world.TradeKeep.Set(Goods.Bread, 0);
        world.TradeBuy.Set(Goods.Stone, 10_000);
        post.Put(Goods.Bread, 300);
        world.NeighbourStocks[0].Held[(int)Goods.Stone] = 5_000;
        Assert.True(300L * world.OfferOf(0, Goods.Bread)!.Value > 500L * world.AskOf(0, Goods.Stone)!.Value);
        ABoatDocksNext(world);

        loop.StepOnce();

        Assert.Equal(world.Config.TradeBoatHold, post.Store[Goods.Stone]);
    }

    /// <summary>⛔ <b>The boat never leaves a good the village sells</b> — it would sit at the post for sale for ever.</summary>
    [Fact]
    public void TheBoatNeverLeavesAGoodTheVillageSells()
    {
        (SimLoop loop, _, StoreBuilding post) = APostAwake();
        SimWorld world = loop.World;
        world.TradeKeep.Set(Goods.Firewood, 0);
        world.TradeKeep.Set(Goods.Stone, 100);
        world.TradeBuy.Set(Goods.Stone, 500);
        world.TradeBuy.Set(Goods.Leather, 20);              // and one it does not sell, so the boat is seen to trade
        post.Put(Goods.Firewood, 200);
        ABoatDocksNext(world);

        loop.StepOnce();

        Assert.Equal(0, post.Store[Goods.Stone]);
        Assert.Equal(20, post.Store[Goods.Leather]);
        Assert.True(post.Store[Goods.Firewood] < 200, "The boat traded nothing — the guard proves nothing.");
    }

    /// <summary>
    /// With nothing to trade the boat says it called and found nothing — and nothing moves.
    /// </summary>
    [Fact]
    public void AnEmptyPostIsToldAndNothingMoves()
    {
        var log = new InMemoryLogSink();
        (SimLoop loop, _, StoreBuilding post) = APostAwake(log: log);
        SimWorld world = loop.World;
        int[] before = (int[])world.NeighbourStocks[0].Held.Clone();
        ABoatDocksNext(world);

        loop.StepOnce();

        Assert.Equal(before, world.NeighbourStocks[0].Held);
        Assert.Equal(0, post.Store.Held);
        Assert.Contains(log.Entries, e => e.Message.Contains("found nothing to trade", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------
    //  Animals — only to a barn with room
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ <b>Animals only to a barn with room</b> (`trade.md §4.6`): a buy order for ten head lands no more than the
    /// pasture holds; with no pasture painted the order cannot be set, and the refusal says why.
    /// </summary>
    [Fact]
    public void AnimalsLandOnlyInABarnWithRoom()
    {
        (SimLoop loop, _, StoreBuilding post) = APostAwake();
        SimWorld world = loop.World;
        PlacementVerdict refused = world.SetTradeBuyHead(4);
        Assert.False(refused.Allowed);
        Assert.Contains("pasture", refused.Reason, StringComparison.Ordinal);
        Assert.Null(world.TradeBuyHead);

        Workplace barn = HerdTests.RaiseABarn(world);
        HerdTests.GivePasture(world, barn, 40);             // room for eight head
        barn.HerdHead = 6;
        Assert.True(world.SetTradeBuyHead(10).Allowed);
        world.TradeKeep.Set(Goods.Firewood, 0);
        post.Put(Goods.Firewood, 800);
        world.NeighbourStocks[0].Animals = 40;              // a boat with beasts to spare
        ABoatDocksNext(world);

        loop.StepOnce();

        Assert.Equal(world.PastureFor(barn), barn.HerdHead);
        Assert.Equal(40 - 2, world.NeighbourStocks[0].Animals);
    }

    // ---------------------------------------------------------------
    //  Send the boat on
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ <b>Send the boat on</b> (Joe, D587 call 6): a player input, so it is in the sim and the hash — the leaving tick
    /// moves to now, the boat is told leaving on the next tick, and with no boat docked it is refused with a reason.
    /// </summary>
    [Fact]
    public void SendingTheBoatOnMovesItsLeavingTick()
    {
        var log = new InMemoryLogSink();
        (SimLoop loop, _, _) = APostAwake(log: log);
        SimWorld world = loop.World;
        Assert.False(world.SendTheBoatOn().Allowed);

        ABoatDocksNext(world);
        loop.StepOnce();
        Assert.True(world.BoatIsDocked);
        ulong docked = StateHash.Compute(world);

        Assert.True(world.SendTheBoatOn().Allowed);
        Assert.Equal(world.Tick, world.Boat!.LeavesAtTick);
        Assert.NotEqual(docked, StateHash.Compute(world));

        loop.StepOnce();
        Assert.False(world.BoatIsDocked);
        Assert.Contains(log.Entries, e => e.Message.Contains("is leaving", StringComparison.Ordinal) && e.Tick == world.Tick - 1);
    }

    /// <summary>The buy order for animals reaches the save and the hash — sparsely, so a village without one mixes nothing.</summary>
    [Fact]
    public void TheAnimalOrderIsHashedOnlyWhenSet()
    {
        SimWorld world = AVillage().World;
        ulong none = StateHash.Compute(world);
        Workplace barn = HerdTests.RaiseABarn(world);
        HerdTests.GivePasture(world, barn, 10);
        ulong withABarn = StateHash.Compute(world);
        Assert.True(world.SetTradeBuyHead(0).Allowed);
        ulong zero = StateHash.Compute(world);
        Assert.True(world.SetTradeBuyHead(3).Allowed);
        Assert.NotEqual(withABarn, zero);
        Assert.NotEqual(zero, StateHash.Compute(world));
        Assert.True(world.SetTradeBuyHead(null).Allowed);
        Assert.Equal(withABarn, StateHash.Compute(world));
        Assert.NotEqual(none, withABarn);
    }
}
