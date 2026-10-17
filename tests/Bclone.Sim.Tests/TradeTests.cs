using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ Commit 4 of `specs/trade.md §11` (D594): <b>the post and the region</b>. The neighbours sleep until a trading post
/// stands, wake with it, and step only at season turns; the rates are derived from their stocks, stay in the band, follow
/// scarcity and are said in words; the merchant carries what is above <i>keep N</i> to the post — <b>never below it</b> —
/// and brings home what nobody means to sell; the quota asks for him only where a post has work.
/// </summary>
public sealed class TradeTests
{
    private readonly ITestOutputHelper _output;

    public TradeTests(ITestOutputHelper output) => _output = output;

    private static SimLoop AVillage(ulong seed = 12345) =>
        SimFactory.CreatePhase0(ShippedConfig.Established() with { Seed = seed }, new InMemoryLogSink());

    /// <summary>A finished trading post on the river bank nearest the founding site, the hall raised first.</summary>
    internal static Workplace RaiseAPost(SimWorld world)
    {
        world.TownHall ??= new TownHall { Position = Point.CentreOf(world.Map.FoundingSite), Name = "the hall", RaisedAtTick = 1 };
        GridPos where = BankNear(world);
        Assert.True(world.Mark(BuildingKind.TradingPost, where).Allowed, $"Could not mark a trading post at {where}.");
        Workplace site = Assert.Single(world.Workplaces, w => w.Construction?.Kind == BuildingKind.TradingPost);
        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        GridPos stood = site.Tile;
        world.Complete(site);
        return Assert.Single(world.Workplaces, w => w.Kind == JobKind.Merchant && w.Tile == stood && !w.IsSite);
    }

    private static GridPos BankNear(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 2; radius < 60; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (world.Map.Contains(at) && !world.HasSomethingToHarvest(at)
                        && world.CanBuildAt(BuildingKind.TradingPost, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No river bank near the founding site to put a trading post on.");
    }

    private static StoreBuilding PostStore(SimWorld world, Workplace post) =>
        world.PostStoreOf(post) ?? throw new Xunit.Sdk.XunitException("The trading post raised no store.");

    private static bool AtASeasonTurn(SimWorld world) =>
        world.Clock.DayOfSeason == 1 && world.Tick % (ulong)world.Config.TicksPerDay == 0UL;

    /// <summary>Step until the tick after the next season turn has run (the turn's systems have acted).</summary>
    private static ulong StepPastTheNextSeasonTurn(SimLoop loop)
    {
        do
        {
            loop.StepOnce();
        }
        while (!AtASeasonTurn(loop.World));

        ulong turn = loop.World.Tick;
        loop.StepOnce();
        return turn;
    }

    // ---------------------------------------------------------------
    //  The region sleeps, wakes with the post, and steps a season at a time
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ <b>The region sleeps until a post stands</b> (`trade.md §11.1`): a year of a village without one leaves every
    /// neighbour empty and the region asleep — what keeps all 362 goldens where they are.
    /// </summary>
    [Fact]
    public void TheRegionSleepsUntilAPostStands()
    {
        SimLoop loop = AVillage();
        loop.Step(loop.World.Config.TicksPerDay * loop.World.Config.DaysPerSeason * 4);

        Assert.Equal(0UL, loop.World.RegionAwakeSince);
        Assert.False(loop.World.APostStands);
        Assert.All(loop.World.NeighbourStocks, stock => Assert.True(stock.IsEmpty));
    }

    /// <summary>
    /// ⭐ It wakes at the first season turn with a post standing — not the tick the post is raised — with everything a
    /// neighbour makes at its size, its animals, and nothing of what it wants.
    /// </summary>
    [Fact]
    public void TheRegionWakesAtTheSeasonTurnAfterThePostAndIsSeeded()
    {
        SimLoop loop = AVillage();
        loop.Step(30);
        RaiseAPost(loop.World);
        Assert.True(loop.World.APostStands);
        loop.StepOnce();
        Assert.Equal(0UL, loop.World.RegionAwakeSince); // raised mid-season: still asleep

        ulong turn = StepPastTheNextSeasonTurn(loop);
        Assert.Equal(turn, loop.World.RegionAwakeSince);
        for (int n = 0; n < loop.World.Config.Neighbours.Count; n++)
        {
            NeighbourRow row = loop.World.Config.Neighbours[n];
            NeighbourStock stock = loop.World.NeighbourStocks[n];
            Assert.All(row.Makes, made => Assert.Equal(made.Amount, stock.Held[(int)made.Goods]));
            Assert.All(row.Wants, wanted => Assert.Equal(0, stock.Held[(int)wanted.Goods]));
            Assert.Equal(row.MakesAnimals, stock.Animals);
        }
    }

    /// <summary>
    /// ⭐ <b>Stocks step only at season turns</b>, by the stated arithmetic: a made good <c>region_seasons_to_recover</c>'s
    /// share of the way back to its size, a wanted good used that share of its size, animals as made goods — and nothing
    /// between the turns.
    /// </summary>
    [Fact]
    public void StocksStepOnlyAtSeasonTurnsByTheirShare()
    {
        SimLoop loop = AVillage();
        RaiseAPost(loop.World);
        StepPastTheNextSeasonTurn(loop);
        SimWorld world = loop.World;

        NeighbourRow hills = world.Config.Neighbours[0];
        MaterialCost made = hills.Makes[0];
        MaterialCost wanted = hills.Wants[0];
        NeighbourStock stock = world.NeighbourStocks[0];
        stock.Held[(int)made.Goods] = 0;                 // a boat bought it all
        stock.Held[(int)wanted.Goods] = wanted.Amount;   // a boat sold it its fill
        stock.Animals = 0;
        int[] before = (int[])stock.Held.Clone();

        while (!AtASeasonTurn(world))
        {
            Assert.Equal(before, stock.Held);
            Assert.Equal(0, stock.Animals);
            loop.StepOnce();
        }

        loop.StepOnce();
        int share = world.Config.RegionSeasonsToRecover;
        Assert.Equal(8, share); // the shipped share (D594) — the arithmetic below reads it, this pins it
        Assert.Equal(Math.Max(1, made.Amount / share), stock.Held[(int)made.Goods]);
        Assert.Equal(wanted.Amount - Math.Max(1, wanted.Amount / share), stock.Held[(int)wanted.Goods]);
        Assert.Equal(Math.Max(1, hills.MakesAnimals / share), stock.Animals);
    }

    /// <summary>
    /// A made good piled past its size stays where it is, never above double; a wanted good runs out at nothing — the
    /// stocks stay inside the band the rates read.
    /// </summary>
    [Fact]
    public void StocksStayBetweenNothingAndDouble()
    {
        SimLoop loop = AVillage();
        RaiseAPost(loop.World);
        StepPastTheNextSeasonTurn(loop);
        SimWorld world = loop.World;
        NeighbourRow hills = world.Config.Neighbours[0];
        NeighbourStock stock = world.NeighbourStocks[0];
        stock.Held[(int)hills.Makes[0].Goods] = hills.Makes[0].Amount * 5;
        stock.Held[(int)hills.Wants[0].Goods] = 1;

        StepPastTheNextSeasonTurn(loop);
        Assert.Equal(hills.Makes[0].Amount * 2, stock.Held[(int)hills.Makes[0].Goods]);
        Assert.Equal(0, stock.Held[(int)hills.Wants[0].Goods]);
    }

    // ---------------------------------------------------------------
    //  Rates — derived, in the band, following scarcity, in words
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ <b>Every rate stays in the band</b> — within a quarter and four times the goods' worths — whatever the stocks,
    /// for every neighbour and every pair it trades, at nothing, at its size and far past it.
    /// </summary>
    [Fact]
    public void EveryRateStaysInTheBand()
    {
        SimWorld world = AVillage().World;
        int checkedPairs = 0;
        // ⚠️ Wants and makes filled independently: filled together, the two sides of a rate move as one and a wider band
        // never shows (its red check scored zero until they were apart — D594).
        int[] fills = { 0, 1, 2, 4, 8 };
        foreach ((int wantsFill, int makesFill) in fills.SelectMany(w => fills.Select(m => (w, m))))
        {
            for (int n = 0; n < world.Config.Neighbours.Count; n++)
            {
                NeighbourRow row = world.Config.Neighbours[n];
                foreach (MaterialCost stocked in row.Wants)
                {
                    world.NeighbourStocks[n].Held[(int)stocked.Goods] = stocked.Amount * wantsFill / 2;
                }

                foreach (MaterialCost stocked in row.Makes)
                {
                    world.NeighbourStocks[n].Held[(int)stocked.Goods] = stocked.Amount * makesFill / 2;
                }

                foreach (MaterialCost sell in row.Wants)
                {
                    foreach (MaterialCost buy in row.Makes)
                    {
                        (int give, int get) = world.RateFor(n, sell.Goods, buy.Goods)
                            ?? throw new Xunit.Sdk.XunitException($"No rate for {sell.Goods} → {buy.Goods} at neighbour {n}.");
                        int sellWorth = world.GoodsCatalog[sell.Goods].TradeWorth;
                        int buyWorth = world.GoodsCatalog[buy.Goods].TradeWorth;
                        Assert.True(give > 0 && get > 0);
                        Assert.True(4L * get * buyWorth >= (long)give * sellWorth, $"{give} {sell.Goods} for {get} {buy.Goods} is below the band.");
                        Assert.True((long)get * buyWorth <= 4L * give * sellWorth, $"{give} {sell.Goods} for {get} {buy.Goods} is above the band.");
                        checkedPairs++;
                    }
                }
            }
        }

        Assert.True(checkedPairs > 50, $"Only {checkedPairs} pairs checked.");
    }

    /// <summary>
    /// ⭐ <b>A rate follows the neighbour's need</b>: a good it is short of buys more than the same good once it has
    /// plenty, and a good it has plenty of comes cheaper than the same good when it is running low. A good it neither
    /// wants nor makes has no rate at all.
    /// </summary>
    [Fact]
    public void ARateFollowsScarcity()
    {
        SimWorld world = AVillage().World;
        NeighbourRow hills = world.Config.Neighbours[0];
        Goods sell = hills.Wants[0].Goods, buy = hills.Makes[0].Goods;
        int[] held = world.NeighbourStocks[0].Held;
        held[(int)buy] = hills.Makes[0].Amount;

        held[(int)sell] = 0;
        (int give, int get) shortOf = world.RateFor(0, sell, buy)!.Value;
        held[(int)sell] = hills.Wants[0].Amount * 2;
        (int give, int get) plenty = world.RateFor(0, sell, buy)!.Value;
        Assert.True((long)shortOf.get * plenty.give > (long)plenty.get * shortOf.give, $"short {shortOf}, plenty {plenty}");

        held[(int)sell] = hills.Wants[0].Amount;
        held[(int)buy] = 0;
        (int give, int get) runningLow = world.RateFor(0, sell, buy)!.Value;
        held[(int)buy] = hills.Makes[0].Amount * 2;
        (int give, int get) pileUp = world.RateFor(0, sell, buy)!.Value;
        Assert.True((long)pileUp.get * runningLow.give > (long)runningLow.get * pileUp.give, $"low {runningLow}, piled {pileUp}");

        Assert.Null(world.RateFor(0, hills.Makes[0].Goods, hills.Makes[1].Goods)); // it does not want what it makes
    }

    /// <summary>⭐ A rate is said in words with its reason (`trade.md §4.3`) — who, why, and how many of each.</summary>
    [Fact]
    public void ARateIsSaidInWordsWithItsReason()
    {
        SimWorld world = AVillage().World;
        NeighbourRow hills = world.Config.Neighbours[0];
        Goods sell = hills.Wants[0].Goods, buy = hills.Makes[0].Goods;
        world.NeighbourStocks[0].Held[(int)buy] = hills.Makes[0].Amount;
        world.NeighbourStocks[0].Held[(int)sell] = 0;

        string said = world.SayTheRate(0, sell, buy)!;
        _output.WriteLine(said);
        (int give, int get) = world.RateFor(0, sell, buy)!.Value;
        Assert.StartsWith(world.NeighbourName(0) + " is short of " + world.GoodsCatalog.NameOf(sell), said, StringComparison.Ordinal);
        Assert.Contains("has plenty of " + world.GoodsCatalog.NameOf(buy), said, StringComparison.Ordinal);
        Assert.EndsWith($"{give} {world.GoodsCatalog.NameOf(sell)} buys {get} {world.GoodsCatalog.NameOf(buy)}.", said, StringComparison.Ordinal);
        Assert.NotEqual(world.Name, world.NeighbourName(0));
        Assert.Equal(3, Enumerable.Range(0, 3).Select(world.NeighbourName).Distinct().Count());
    }

    // ---------------------------------------------------------------
    //  The merchant
    // ---------------------------------------------------------------

    /// <summary>Stone in the warehouse — the established village holds none, and nothing in its first season uses it.</summary>
    private static void GiveStone(SimWorld world, int stone)
    {
        StoreBuilding warehouse = world.StoreBuildings.First(store => store.Accepts(Goods.Stone));
        Assert.Equal(stone, warehouse.Put(Goods.Stone, stone));
    }

    private static Villager PinAMerchant(SimWorld world)
    {
        Villager merchant = world.Villagers.First(v => v.Alive && v.CanWork);
        world.SetPinnedTrade(merchant, JobKind.Merchant);
        return merchant;
    }

    /// <summary>
    /// ⛔ <b>The merchant never takes a good below <i>keep N</i></b>: with thirty stone above the order, the post ends with
    /// thirty — never more on any tick — and the village keeps what it said it would.
    /// </summary>
    [Fact]
    public void TheMerchantCarriesWhatIsAboveTheKeepAndNeverBelowIt()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace post = RaiseAPost(world);
        StoreBuilding store = PostStore(world, post);
        PinAMerchant(world);
        GiveStone(world, 200);
        int stone = world.InStores(Goods.Stone);
        world.TradeKeep.Set(Goods.Stone, stone - 30);

        bool sawWork = false;
        for (int t = 0; t < world.Config.TicksPerSeason; t++)
        {
            loop.StepOnce();
            Assert.True(store.Store[Goods.Stone] <= 30, $"Tick {world.Tick}: the post holds {store.Store[Goods.Stone]} stone.");
            sawWork |= world.Villagers.Any(v => v.State is VillagerState.CollectingForThePost or VillagerState.StockingThePost);
        }

        Assert.True(sawWork, "No merchant ever went to collect.");
        Assert.Equal(30, store.Store[Goods.Stone]);
        Assert.Equal(stone - 30, world.InStores(Goods.Stone));
        Assert.Null(world.GoodToSell(store));
    }

    /// <summary>
    /// ⭐ What is at the post with no keep order on it — what a boat left — goes home to the stores, all of it, and
    /// nothing is lost on the way.
    /// </summary>
    [Fact]
    public void WhatABoatLeftGoesHome()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace post = RaiseAPost(world);
        StoreBuilding store = PostStore(world, post);
        PinAMerchant(world);
        int stone = world.InStores(Goods.Stone);
        Assert.Equal(40, store.Put(Goods.Stone, 40));
        Assert.Equal(stone, world.InStores(Goods.Stone)); // at the post is not in the stores

        loop.Step(world.Config.TicksPerSeason);

        Assert.Equal(0, store.Store[Goods.Stone]);
        Assert.Equal(stone + 40, world.InStores(Goods.Stone));
    }

    // ---------------------------------------------------------------
    //  The quota
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ The quota asks for a merchant only where a post stands with work — goods above a keep order, or goods at the post
    /// to bring home. (While food comes first it is zeroed with every trade that is not food, in <c>Compute</c>.)
    /// </summary>
    [Fact]
    public void TheQuotaAsksForAMerchantOnlyWhereAPostHasWork()
    {
        SimWorld world = AVillage().World;
        GiveStone(world, 50);
        world.TradeKeep.Set(Goods.Stone, 0);
        Assert.Equal(0, LabourQuota.MerchantsWanted(world)); // no post

        Workplace post = RaiseAPost(world);
        Assert.Equal(1, LabourQuota.MerchantsWanted(world)); // stone above a keep of nothing

        world.TradeKeep.Set(Goods.Stone, null);
        Assert.Equal(0, LabourQuota.MerchantsWanted(world)); // a post, no orders, nothing at it

        PostStore(world, post).Put(Goods.Fish, 5);
        Assert.Equal(1, LabourQuota.MerchantsWanted(world)); // a boat's goods to bring home

        post.Closed = true;
        Assert.Equal(0, LabourQuota.MerchantsWanted(world)); // a closed post asks for nobody
    }

    /// <summary>A merchant is seated when the post has work — the allocator walks the trade (D593's trap: the herdsman was not).</summary>
    [Fact]
    public void AMerchantIsSeatedWhenThePostHasWork()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        RaiseAPost(world);
        GiveStone(world, 50);
        world.TradeKeep.Set(Goods.Stone, 0);
        loop.Step(world.Config.TicksPerSeason);

        Assert.Contains(world.Villagers, v => v.Alive && world.FindWorkplace(v.WorkplaceId)?.Kind == JobKind.Merchant);
    }
}
