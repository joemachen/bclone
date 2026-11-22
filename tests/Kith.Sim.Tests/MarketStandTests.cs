using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Logging;
using Kith.Sim.Systems;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

/// <summary>
/// W14, the market stand — <c>specs/market-stand.md</c> (D851–D852).
/// </summary>
/// <remarks>
/// §5.2, call 1: <b>a marketer stocks the counter of the market they work at first.</b> Measured before it was written
/// (§4.3): a marketer's load went to the counter nearest the storehouse it came from, so a stall out among the far houses
/// stood with its seat filled and its shelf empty, and Oakhaven's far families walked <i>more</i> than with no stall.
/// The rule is the plan alone: the load already goes to the marketer's own counter (<c>StockingTheMarket</c> walks to their
/// workplace), which a real stall's keeper filling it over a season pins end to end.
/// </remarks>
public sealed class MarketStandTests
{
    private readonly ITestOutputHelper _output;

    public MarketStandTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => ShippedConfig.Established();

    /// <summary>The established village's market, a second market raised beside it, and a marketer seated at the second.</summary>
    private static (SimWorld world, StoreBuilding first, StoreBuilding second, Villager marketer) TwoMarkets()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        StoreBuilding first = world.StoreBuildings.Single(s => s.Kind == StoreKind.Market);
        Workplace secondWorks = FoodChainTests.RaiseA(world, BuildingKind.Market);
        StoreBuilding second = world.StoreAt(secondWorks.Tile)!;
        Assert.NotSame(first, second);
        Assert.Equal(StoreKind.Market, second.Kind);

        Villager marketer = world.Villagers.First(v => v.Alive && v.CanWork);
        marketer.WorkplaceId = secondWorks.Id;
        secondWorks.WorkerIds.Add(marketer.Id);
        return (world, first, second, marketer);
    }

    /// <summary>Every shelf of <paramref name="market"/> at a limit of nought but <paramref name="but"/>'s.</summary>
    private static void OnlyShortOf(SimWorld world, StoreBuilding market, Goods but, int limit, int holding)
    {
        for (int id = 0; id < world.GoodsCatalog.Count; id++)
        {
            var goods = (Goods)id;
            if (goods != but && world.GoodsCatalog.StoredBy(goods, StoreKind.Market))
            {
                world.SetMarketLimit(market, goods, 0);
            }
        }

        world.SetMarketLimit(market, but, limit);
        Assert.Equal(holding, market.Put(but, holding));
    }

    /// <summary>
    /// ⭐ The plan: the other market's firewood shelf is bare and this marketer's own meat shelf half full — today's
    /// emptiest-shelf rule (D601) took the bare one; their own counter comes first, so they go for meat.
    /// </summary>
    [Fact]
    public void AMarketerPlansForTheirOwnCounterFirst()
    {
        (SimWorld world, StoreBuilding first, StoreBuilding second, Villager marketer) = TwoMarkets();
        world.StoreBuildings.Single(s => s.Kind == StoreKind.Granary).Put(Goods.Meat, 400);
        world.StoreBuildings.Single(s => s.Kind == StoreKind.Warehouse).Put(Goods.Firewood, 400);
        OnlyShortOf(world, first, Goods.Firewood, 1000, 0);
        OnlyShortOf(world, second, Goods.Meat, 1000, 500);

        BehaviorSystem.MarketErrand? next = BehaviorSystem.PlanMarketErrand(world, marketer);
        _output.WriteLine($"{marketer.Name} plans {next?.Goods} from ({next?.Source.X},{next?.Source.Y})");

        Assert.Equal(Goods.Meat, next?.Goods);
    }

    /// <summary>With their own counter at its limits, a marketer stocks any market's emptiest shelf, as before (D601).</summary>
    [Fact]
    public void WithTheirOwnCounterFullAMarketerStocksAnyMarket()
    {
        (SimWorld world, StoreBuilding first, StoreBuilding second, Villager marketer) = TwoMarkets();
        world.StoreBuildings.Single(s => s.Kind == StoreKind.Granary).Put(Goods.Meat, 400);
        world.StoreBuildings.Single(s => s.Kind == StoreKind.Warehouse).Put(Goods.Firewood, 400);
        OnlyShortOf(world, first, Goods.Firewood, 1000, 0);
        OnlyShortOf(world, second, Goods.Meat, 500, 500);
        Assert.False(world.CounterWantsStocking(second));

        BehaviorSystem.MarketErrand? next = BehaviorSystem.PlanMarketErrand(world, marketer);
        _output.WriteLine($"{marketer.Name} plans {next?.Goods} from ({next?.Source.X},{next?.Source.Y})");

        Assert.Equal(Goods.Firewood, next?.Goods);
    }
}
