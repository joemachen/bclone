using System.Text.Json.Nodes;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Persistence;
using Bclone.Sim.World;
using Xunit;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ Commit 1 of `specs/trade.md §11` (D590): the rows and the state for trade and the herd, and <b>nothing that
/// behaves</b>. A village with no barn and no post must be today's village — the 362 goldens are the guard for that;
/// these are the guards for what the commit adds.
/// </summary>
public sealed class TradeRowsTests
{
    private static SimWorld Shipped() => SimFactory.CreatePhase0(ShippedConfig.Load(), new InMemoryLogSink()).World;

    // ---------------------------------------------------------------
    //  The rows
    // ---------------------------------------------------------------

    [Fact]
    public void TheBarnAndThePostAreRowsWithTheirTrades()
    {
        SimWorld world = Shipped();

        BuildingRow barn = world.BuildingsCatalog[BuildingKind.Barn];
        Assert.Equal("barn", barn.Name);
        Assert.Equal(StoreKind.Barn, barn.Stores);
        Assert.True(barn.Seats > 0, "the barn employs nobody — the herdsman would have nowhere to work");
        Assert.Null(barn.MustTouch);

        BuildingRow post = world.BuildingsCatalog[BuildingKind.TradingPost];
        Assert.Equal("trading post", post.Name);
        Assert.Equal(StoreKind.TradingPost, post.Stores);
        Assert.Equal(Terrain.Water, post.MustTouch);
        Assert.True(post.Seats > 0, "the post employs nobody — the trader would have nowhere to work");

        Assert.Equal(BuildingKind.Barn, world.JobsCatalog.WorksAt(JobKind.Herdsman));
        Assert.Equal("herdsman", world.JobsCatalog[JobKind.Herdsman].Name);
        Assert.Equal(BuildingKind.TradingPost, world.JobsCatalog.WorksAt(JobKind.Trader));
        Assert.Equal("trader", world.JobsCatalog[JobKind.Trader].Name);
    }

    /// <summary>Hay is kept only in a barn (D52's lesson: a good that shares a room is a shortage nobody can read).</summary>
    [Fact]
    public void HayIsKeptOnlyInABarnAndShipsWithNoLimit()
    {
        SimWorld world = Shipped();
        Assert.Equal("hay", world.GoodsCatalog.NameOf(Goods.Hay));
        Assert.Equal(new[] { StoreKind.Barn }, world.GoodsCatalog[Goods.Hay].StoredBy);
        Assert.False(world.GoodsCatalog.Edible(Goods.Hay), "hay is for the herd, never a villager's meal");

        // ⛔ No starting limit: D522 moved the shipped golden by adding two (`trade.md §11.1`).
        Assert.False(ShippedConfig.Load().StartingStockLimits.ContainsKey("hay"));
        Assert.Null(world.StockLimits.For(Goods.Hay));

        // And nothing else is stored by a barn or a post, so no hauler carries anything there by accident.
        for (int id = 0; id < world.GoodsCatalog.Count; id++)
        {
            IReadOnlyList<StoreKind> by = world.GoodsCatalog[(Goods)id].StoredBy;
            Assert.DoesNotContain(StoreKind.TradingPost, by);
            if ((Goods)id != Goods.Hay)
            {
                Assert.DoesNotContain(StoreKind.Barn, by);
            }
        }
    }

    /// <summary>Joe (D588): one kind of animal first. Joe (D587): three neighbours, and between them every good.</summary>
    [Fact]
    public void OneKindOfAnimalAndThreeNeighboursWhoMakeEveryGoodBetweenThem()
    {
        SimConfig config = ShippedConfig.Load();
        AnimalRow animal = Assert.Single(config.Animals);
        Assert.Equal("cattle", animal.Name);

        Assert.Equal(3, config.Neighbours.Count);
        Assert.Equal(
            new[] { NeighbourDirection.Upriver, NeighbourDirection.Downriver, NeighbourDirection.Overland },
            config.Neighbours.Select(n => n.Direction).ToArray());

        SimWorld world = Shipped();
        var made = config.Neighbours.SelectMany(n => n.Makes).Select(m => m.Goods).ToHashSet();
        for (int id = 0; id < world.GoodsCatalog.Count; id++)
        {
            Assert.True(made.Contains((Goods)id), $"no neighbour makes {world.GoodsCatalog.NameOf((Goods)id)}");
        }

        Assert.Contains(config.Neighbours, n => n.MakesAnimals > 0);
        Assert.All(config.Neighbours, n => Assert.NotEmpty(n.Wants));
    }

    // ---------------------------------------------------------------
    //  The gate — the town hall is the permission (D587 call 1)
    // ---------------------------------------------------------------

    [Fact]
    public void TheBarnAndThePostWaitForTheTownHall()
    {
        SimWorld world = Shipped();
        foreach (BuildingKind kind in new[] { BuildingKind.Barn, BuildingKind.TradingPost })
        {
            Assert.False(world.IsUnlocked(kind));
            Assert.Contains("town hall", world.WhyNotYet(kind), StringComparison.Ordinal);
        }

        // ⭐ And hay is not offered anywhere — the stock limits, the bar — before the barn that keeps it is known (D502's
        // rule, which the probe's `spoilers:` line showed hay slipping past).
        Assert.False(world.KnowsOf(Goods.Hay));

        world.TownHall = new TownHall { Position = Point.CentreOf(world.Map.FoundingSite), Name = "the hall", RaisedAtTick = 1 };
        Assert.True(world.IsUnlocked(BuildingKind.Barn));
        Assert.True(world.IsUnlocked(BuildingKind.TradingPost));
        Assert.True(world.KnowsOf(Goods.Hay));
    }

    // ---------------------------------------------------------------
    //  Nothing behaves
    // ---------------------------------------------------------------

    [Fact]
    public void NobodyIsAskedToHerdOrTradeYet()
    {
        SimLoop loop = SimFactory.CreatePhase0(ShippedConfig.Established(), new InMemoryLogSink());
        SimWorld world = loop.World;
        world.TownHall = new TownHall { Position = Point.CentreOf(world.Map.FoundingSite), Name = "the hall", RaisedAtTick = 1 };
        loop.Step(world.Config.TicksPerSeason);

        LabourQuota quota = LabourQuota.For(world);
        Assert.Equal(0, quota.For(JobKind.Herdsman));
        Assert.Equal(0, quota.For(JobKind.Trader));
        Assert.DoesNotContain(world.Workplaces, w => w.Kind is JobKind.Herdsman or JobKind.Trader);
        Assert.Null(world.Boat);
        Assert.Equal(0, world.BoatsEverCame);
        Assert.All(world.NeighbourStocks, n => Assert.True(n.IsEmpty));
    }

    // ---------------------------------------------------------------
    //  The state reaches the hash — and only when it is set
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ Sparse (`trade.md §11.1`): every new fact moves the hash when set, and an untouched village mixes nothing —
    /// which is what holds the goldens.
    /// </summary>
    [Fact]
    public void EveryNewFactReachesTheHashOnlyWhenItIsSet()
    {
        // Established, so there is a workplace to pose a herd on at tick 0.
        SimWorld world = SimFactory.CreatePhase0(ShippedConfig.Established(), new InMemoryLogSink()).World;
        ulong untouched = StateHash.Compute(world);

        void Differs(string what, Action set, Action clear)
        {
            set();
            Assert.True(StateHash.Compute(world) != untouched, $"{what} does not reach the hash");
            clear();
            Assert.Equal(untouched, StateHash.Compute(world));
        }

        Differs("a keep order", () => world.TradeKeep.Set(Goods.Firewood, 300), () => world.TradeKeep.Set(Goods.Firewood, null));
        Differs("a buy order", () => world.TradeBuy.Set(Goods.Stone, 50), () => world.TradeBuy.Set(Goods.Stone, null));
        Differs("a zero keep order", () => world.TradeKeep.Set(Goods.Logs, 0), () => world.TradeKeep.Set(Goods.Logs, null));
        Differs(
            "a boat",
            () => world.Boat = new TradeBoat { NeighbourId = 1, AppearsAtTick = 10, DocksAtTick = 40, LeavesAtTick = 80 },
            () => world.Boat = null);
        Differs("the boats that came", () => world.BoatsEverCame = 2, () => world.BoatsEverCame = 0);
        Differs(
            "a neighbour's stock",
            () => world.NeighbourStocks[2].Held[(int)Goods.Logs] = 40,
            () => world.NeighbourStocks[2].Held[(int)Goods.Logs] = 0);
        Differs("a neighbour's animals", () => world.NeighbourStocks[0].Animals = 3, () => world.NeighbourStocks[0].Animals = 0);

        Workplace any = world.Workplaces[0];
        Differs("a herd", () => any.HerdHead = 5, () => any.HerdHead = 0);
        Differs("a herd's growth", () => any.HerdGrowth = 7, () => any.HerdGrowth = 0);

        // The boat's three ticks each count on their own.
        world.Boat = new TradeBoat { NeighbourId = 1, AppearsAtTick = 10, DocksAtTick = 40, LeavesAtTick = 80 };
        ulong boat = StateHash.Compute(world);
        world.Boat = world.Boat with { LeavesAtTick = 81 };
        Assert.NotEqual(boat, StateHash.Compute(world));
        world.Boat = world.Boat with { LeavesAtTick = 80, DocksAtTick = 41 };
        Assert.NotEqual(boat, StateHash.Compute(world));
        world.Boat = world.Boat with { DocksAtTick = 40, NeighbourId = 0 };
        Assert.NotEqual(boat, StateHash.Compute(world));
    }

    // ---------------------------------------------------------------
    //  Saved — format 8
    // ---------------------------------------------------------------

    [Fact]
    public void TheFormatIsEight() => Assert.Equal(8, SaveGame.Format);

    [Fact]
    public void TradeAndAHerdComeBackFromASave()
    {
        SimLoop loop = SimFactory.CreatePhase0(ShippedConfig.Established(), new InMemoryLogSink());
        SimWorld world = loop.World;
        PoseTradeAndAHerd(world);

        JsonObject document = SaveGame.Capture(loop, "test", "2026-10-16 12:00", "test");
        SimWorld loaded = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), world.Config).World;

        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded));
        Assert.Equal(300, loaded.TradeKeep.For(Goods.Firewood));
        Assert.Equal(0, loaded.TradeKeep.For(Goods.Logs));
        Assert.Equal(50, loaded.TradeBuy.For(Goods.Stone));
        Assert.Equal(world.Boat, loaded.Boat);
        Assert.Equal(2, loaded.BoatsEverCame);
        Assert.Equal(40, loaded.NeighbourStocks[2].Held[(int)Goods.Logs]);
        Assert.Equal(3, loaded.NeighbourStocks[0].Animals);
        Assert.Equal(5, loaded.Workplaces[0].HerdHead);
        Assert.Equal(7, loaded.Workplaces[0].HerdGrowth);
    }

    /// <summary>Every new fact set at once — the pose `SaveLoadTests` saves and loads as well.</summary>
    internal static void PoseTradeAndAHerd(SimWorld world)
    {
        world.TradeKeep.Set(Goods.Firewood, 300);
        world.TradeKeep.Set(Goods.Logs, 0);
        world.TradeBuy.Set(Goods.Stone, 50);
        world.Boat = new TradeBoat { NeighbourId = 1, AppearsAtTick = world.Tick + 10, DocksAtTick = world.Tick + 40, LeavesAtTick = world.Tick + 80 };
        world.BoatsEverCame = 2;
        world.NeighbourStocks[2].Held[(int)Goods.Logs] = 40;
        world.NeighbourStocks[0].Animals = 3;
        world.Workplaces[0].HerdHead = 5;
        world.Workplaces[0].HerdGrowth = 7;
    }
}
