using System.Text.Json.Nodes;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.Persistence;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ A market of 8,000 whose goods start at 1,000 each and whose limits may not add up past its room (D612, Joe's D607
/// notes; `storage-and-distribution.md §14.9.3`). At a market the limits ARE the split — they replace D361's half-for-food
/// rule — so every load anybody brings is held to its good's limit, by the one door (<see cref="StoreBuilding.RoomFor"/>).
/// </summary>
public sealed class MarketLimitsTests
{
    private readonly ITestOutputHelper _output;

    public MarketLimitsTests(ITestOutputHelper output) => _output = output;

    private static (SimLoop Loop, StoreBuilding Market) AMarket()
    {
        SimLoop loop = SimFactory.CreatePhase0(ShippedConfig.Established(), new InMemoryLogSink());
        return (loop, loop.World.AnyStoreOf(StoreKind.Market));
    }

    /// <summary>
    /// Untouched, a market keeps the starting limit of every good it holds that the village knows — and nothing of one it
    /// does not: bread waits for the oven, as its row on the card does, and takes none of the room meanwhile.
    /// </summary>
    [Fact]
    public void ANewMarketKeepsItsStartingLimitOfEveryGoodTheVillageKnows()
    {
        (SimLoop loop, StoreBuilding market) = AMarket();
        SimWorld world = loop.World;
        Assert.False(world.KnowsOf(Goods.Bread), "the premise: a new village does not know bread");

        int counted = 0;
        for (int g = 0; g < world.GoodsCatalog.Count; g++)
        {
            var goods = (Goods)g;
            int keeps = world.MarketStockLimit(market, goods);
            _output.WriteLine($"{world.GoodsCatalog.NameOf(goods)}: {keeps}");
            if (market.CanEverHold(goods) && world.KnowsOf(goods))
            {
                Assert.Equal(1000, keeps);
                counted++;
            }
            else
            {
                Assert.Equal(0, keeps);
            }
        }

        Assert.Equal(5, counted);
        Assert.Equal(5000, market.KeptInAll);
        Assert.Equal(8000, market.Store.Capacity);
    }

    /// <summary>
    /// ⛔ The half-for-food rule is gone at a market (Joe's D612 call): with the other goods' limits lowered, forage may be
    /// kept to 7,000 of 8,000 and the room for more of it is its limit's — where D361 would have said none past 4,000.
    /// </summary>
    [Fact]
    public void AMarketsRoomForAGoodIsItsLimitNotHalfTheBuilding()
    {
        (SimLoop loop, StoreBuilding market) = AMarket();
        SimWorld world = loop.World;
        foreach (Goods other in new[] { Goods.Firewood, Goods.Fish, Goods.Meat, Goods.Wheat })
        {
            Assert.True(world.SetMarketLimit(market, other, 0).Allowed);
        }

        Assert.True(world.SetMarketLimit(market, Goods.Produce, 7000).Allowed);
        market.Store.TakeAll(Goods.Produce);
        market.Store.Add(Goods.Produce, 6000);

        Assert.Equal(1000, market.RoomFor(Goods.Produce));
        Assert.Equal(0, market.RoomFor(Goods.Firewood));
        Assert.True(market.HasRoomFor(Goods.Produce));
    }

    /// <summary>
    /// ⭐ Every load is held to its good's limit — a forager's overflow from a full granary as well as the marketer's armful:
    /// <see cref="StoreBuilding.Put"/> clamps to <see cref="StoreBuilding.RoomFor"/>, so nothing past the limit goes in.
    /// </summary>
    [Fact]
    public void AnyLoadIsHeldToItsGoodsLimit()
    {
        (SimLoop loop, StoreBuilding market) = AMarket();
        market.Store.TakeAll(Goods.Produce);
        int keeps = loop.World.MarketStockLimit(market, Goods.Produce);

        int went = market.Put(Goods.Produce, keeps + 500);

        Assert.Equal(keeps, went);
        Assert.Equal(keeps, market.Store[Goods.Produce]);
        Assert.Equal(0, market.Put(Goods.Produce, 1));
    }

    /// <summary>
    /// A good learned late keeps what is left of the room, not its whole starting limit: the set limits come first, and
    /// the starting limit is shared out in the catalogue's order. The total never passes the building.
    /// </summary>
    [Fact]
    public void AGoodLearnedLateKeepsWhatIsLeft()
    {
        (SimLoop loop, StoreBuilding market) = AMarket();
        SimWorld world = loop.World;
        Assert.True(world.SetMarketLimit(market, Goods.Produce, market.MostItMayKeep(Goods.Produce) - 300).Allowed);
        Assert.Equal(7700, market.KeptInAll);
        Assert.Equal(0, world.MarketStockLimit(market, Goods.Bread));

        // The oven comes: the village knows bread (posed on the market's own hook — the arithmetic is under test).
        market.Knows = _ => true;

        Assert.Equal(300, world.MarketStockLimit(market, Goods.Bread));
        Assert.Equal(8000, market.KeptInAll);
        Assert.False(world.SetMarketLimit(market, Goods.Bread, 301).Allowed);
    }

    /// <summary>
    /// ⚠️ A save from before D612 may hold limits past the room (Joe's own market kept forage at 10,000 of 4,000). They are
    /// kept as they are — a load changes nothing — and only a lowering is accepted until they fit.
    /// </summary>
    [Fact]
    public void LimitsAlreadyPastTheRoomMayOnlyBeLowered()
    {
        (SimLoop loop, StoreBuilding market) = AMarket();
        SimWorld world = loop.World;
        market.Limits.Set(Goods.Produce, 10_000);
        Assert.Equal(10_000, world.MarketStockLimit(market, Goods.Produce));

        Assert.False(world.SetMarketLimit(market, Goods.Produce, 10_010).Allowed);
        Assert.True(world.SetMarketLimit(market, Goods.Produce, 9_000).Allowed);
        Assert.Equal(9_000, world.MarketStockLimit(market, Goods.Produce));
        Assert.False(world.SetMarketLimit(market, Goods.Fish, 1_010).Allowed);
    }

    /// <summary>
    /// A market the player raises keeps the starting limits too — the third road a store enters the world by, after the
    /// founding and a load: marked, stocked, worked and completed as any site is.
    /// </summary>
    [Fact]
    public void ARaisedMarketKeepsItsStartingLimitsToo()
    {
        (SimLoop loop, StoreBuilding founded) = AMarket();
        SimWorld world = loop.World;
        GridPos site = world.Map.FoundingSite;
        GridPos? at = null;
        for (int radius = 3; radius < 30 && at is null; radius++)
        {
            for (int dy = -radius; dy <= radius && at is null; dy++)
            {
                for (int dx = -radius; dx <= radius && at is null; dx++)
                {
                    var tile = new GridPos(site.X + dx, site.Y + dy);
                    if (world.CanBuildAt(BuildingKind.Market, tile).Allowed)
                    {
                        at = tile;
                    }
                }
            }
        }

        Assert.NotNull(at);
        Assert.True(world.Mark(BuildingKind.Market, at!.Value).Allowed);
        Workplace plan = world.Workplaces.Last(w => w.Construction?.Kind == BuildingKind.Market);
        BuildFixtures.StockTheSite(plan);
        for (int i = 0; i <= plan.Construction!.Recipe.WorkTicks; i++)
        {
            plan.Construction.Work();
        }

        world.Complete(plan);
        StoreBuilding raised = world.StoreBuildings.Last(s => s.Kind == StoreKind.Market);
        Assert.NotSame(founded, raised);
        Assert.Equal(1000, world.MarketStockLimit(raised, Goods.Firewood));
        Assert.Equal(founded.KeptInAll, raised.KeptInAll);
    }

    /// <summary>
    /// A loaded market keeps what the saved one kept, good for good — the starting limit and what the village knows are
    /// handed to it on load as on the raise, and are never in the file.
    /// </summary>
    [Fact]
    public void ALoadedMarketKeepsWhatTheSavedOneKept()
    {
        (SimLoop loop, StoreBuilding market) = AMarket();
        Assert.True(loop.World.SetMarketLimit(market, Goods.Firewood, 2500).Allowed);
        loop.Step(loop.World.Config.TicksPerDay);

        JsonObject document = SaveGame.Capture(loop, "test", "2026-10-19 12:00", "test");
        SimLoop again = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), loop.World.Config, null);
        StoreBuilding loaded = again.World.AnyStoreOf(StoreKind.Market);

        for (int g = 0; g < loop.World.GoodsCatalog.Count; g++)
        {
            Assert.Equal(market.KeepsUpTo((Goods)g), loaded.KeepsUpTo((Goods)g));
        }

        Assert.Equal(market.KeptInAll, loaded.KeptInAll);
    }
}
