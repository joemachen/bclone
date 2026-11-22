using System.Reflection;
using System.Text.Json.Nodes;
using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Determinism;
using Kith.Sim.Logging;
using Kith.Sim.Persistence;
using Kith.Sim.Systems;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

/// <summary>
/// `specs/batch-trade.md` — W1, the batch trade as a row. <b>Step 1 (§6): today pinned, before anything moves.</b> Every
/// switch §1 counts — the recipe, the two states and their words, the skill clock, the D281 recall, the stop sentences,
/// the quota's hands and take order, the per-year sums, who knows of the goods — read for the four batch trades as they
/// shipped, so steps 2–6 can turn the switches into a row and prove they changed nothing anybody sees.
/// </summary>
/// <remarks>
/// ⚠️ The states are reached through <see cref="PutAt"/> and nothing else, so step 5 (Joe's call A, D792) changes one
/// helper here and none of the words, the recall or the clock the tests pin.
/// </remarks>
public sealed class BatchTradeTests
{
    private readonly ITestOutputHelper _output;

    public BatchTradeTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    /// <summary>The four batch trades, in hiring order (`LabourAllocator.KindsInOrder`).</summary>
    public static TheoryData<JobKind> Trades => new() { JobKind.Tailor, JobKind.Carpenter, JobKind.Miller, JobKind.Baker };

    /// <summary>A batch trade's building, the one the test raises.</summary>
    private static BuildingKind HutOf(JobKind trade) => trade switch
    {
        JobKind.Miller => BuildingKind.Mill,
        JobKind.Baker => BuildingKind.Bakery,
        JobKind.Tailor => BuildingKind.TailorShop,
        JobKind.Carpenter => BuildingKind.Workshop,
        _ => throw new ArgumentOutOfRangeException(nameof(trade), trade, "not a batch trade"),
    };

    /// <summary>Raise a trade's hut, its unlock lent first.</summary>
    private static Workplace RaiseTheHut(SimWorld world, JobKind trade) => trade switch
    {
        JobKind.Tailor => TailorTests.RaiseAShop(world),
        JobKind.Carpenter => WorkshopTests.RaiseAWorkshop(world),
        _ => FoodChainTests.RaiseA(world, HutOf(trade)),
    };

    /// <summary>
    /// Put a villager on a batch trade's walk or at its work — ⭐ the only place in this file that names a batch state.
    /// Step 1 (D792) named the eight states of the four trades here; step 5 (Joe's call A) made it the shared pair and the
    /// trade beside it, and nothing else in the file changed but <see cref="BehaviorSystem.Describe(VillagerState, Villager)"/>
    /// taking the villager.
    /// </summary>
    private static void PutAt(SimWorld world, Villager villager, JobKind trade, bool working)
    {
        world.SetBatchOf(villager, trade);
        villager.State = working ? VillagerState.WorkingABatch : VillagerState.TravelingToABatch;
    }

    /// <summary>Whether a villager is on this trade's walk or at its work.</summary>
    private static bool IsAt(Villager villager, JobKind trade, bool working) =>
        villager.State == (working ? VillagerState.WorkingABatch : VillagerState.TravelingToABatch)
        && villager.BatchTrade == trade;

    // ---------------------------------------------------------------
    //  The recipe — `SimWorld.BatchFor`
    // ---------------------------------------------------------------

    /// <summary>§6 step 1: each trade's <c>Batch</c>, field by field, off today's config keys.</summary>
    [Theory]
    [MemberData(nameof(Trades))]
    public void EachBatchTradesRecipeIsAsShipped(JobKind trade)
    {
        SimConfig c = Config;
        SimWorld world = SimFactory.CreatePhase0(c, new InMemoryLogSink()).World;

        Batch expected = trade switch
        {
            JobKind.Miller => new Batch(JobKind.Miller, Goods.Wheat, c.WheatPerGrind, Goods.Flour, c.FlourPerGrind,
                0, c.GrindTicks, c.GrindsPerStint, "grind", "a grind"),
            JobKind.Baker => new Batch(JobKind.Baker, Goods.Flour, c.FlourPerBake, Goods.Bread, c.BreadPerBake,
                c.FirewoodPerFiring, c.BakeTicks, c.BakesPerStint, "bake", "a bake"),
            JobKind.Tailor => new Batch(JobKind.Tailor, Goods.Leather, c.LeatherPerCoat, Goods.Clothing, 1,
                0, c.SewTicks, c.SewsPerStint, "sew", "a coat"),
            _ => new Batch(JobKind.Carpenter, Goods.Logs, c.LogsPerHandcart, Goods.Handcart, 1,
                0, c.MakeTicks, c.MakesPerStint, "make", "a handcart"),
        };

        Assert.Equal(expected, world.BatchFor(trade));

        // And with a standing hut — the carpenter's reads the first standing workshop's product.
        RaiseTheHut(world, trade);
        Assert.Equal(expected, world.BatchFor(trade));
    }

    /// <summary>
    /// §6 step 2: the built-in rows are priced from the config's own keys — ⛔ a row built once as a property default would
    /// keep the shipped numbers through every <c>Config with { … }</c>, and the suite's posed villages would stop meaning
    /// anything (`BuildingRow`'s reason, `SimConfig.Buildings`).
    /// </summary>
    [Fact]
    public void ABatchRowReadsTheConfigsOwnKeys()
    {
        SimConfig c = Config with { GrindTicks = 9, BreadPerBake = 13, SewsPerStint = 3, MakeTicks = 7, FirewoodPerFiring = 2 };
        SimWorld world = SimFactory.CreatePhase0(c, new InMemoryLogSink()).World;

        Assert.Equal(9, world.BatchFor(JobKind.Miller)!.Value.Ticks);
        Assert.Equal(13, world.BatchFor(JobKind.Baker)!.Value.OutputPerBatch);
        Assert.Equal(2, world.BatchFor(JobKind.Baker)!.Value.Firing);
        Assert.Equal(3, world.BatchFor(JobKind.Tailor)!.Value.PerStint);
        Assert.Equal(7, world.BatchFor(JobKind.Carpenter)!.Value.Ticks);
    }

    /// <summary>§6 step 2 (§3's validation): a modder's bad batch row is refused at load, saying which and why.</summary>
    [Fact]
    public void ABadBatchRowIsRefused()
    {
        IReadOnlyList<BatchRow> shipped = Config.BatchRows;
        BatchRow tailor = shipped[0];
        var bad = new (string Why, BatchRow[] Rows, string Says)[]
        {
            ("two rows, one trade", shipped.Append(tailor).ToArray(), "repeats a trade"),
            ("no such trade", new[] { tailor with { Trade = (JobKind)99 } }, "no job row"),
            ("a batch of no time", new[] { tailor with { Ticks = 0 } }, "ticks"),
            ("nothing a stint", new[] { tailor with { PerStint = 0 } }, "per_stint"),
            ("no such good", new[] { tailor with { Input = (Goods)99 } }, "input"),
            ("no input", new[] { tailor with { InputPerBatch = 0 } }, "input_per_batch"),
            ("no words", new[] { tailor with { Verb = " " } }, "words"),
        };

        foreach ((string why, BatchRow[] rows, string says) in bad)
        {
            SimConfigException refused = Assert.Throws<SimConfigException>(() => (Config with { BatchTrades = rows }).Validate());
            _output.WriteLine($"{why}: {refused.Message}");
            Assert.Contains("batch_trades", refused.Message, StringComparison.Ordinal);
            Assert.Contains(says, refused.Message, StringComparison.Ordinal);
        }

        // And the shipped four, given back as a modder's own list, are accepted.
        (Config with { BatchTrades = shipped.ToArray() }).Validate();
    }

    /// <summary>§6 step 1: no other trade has a batch — the smith keeps his own path (D446).</summary>
    [Fact]
    public void NoOtherTradeHasABatch()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        var batched = new List<JobKind>();
        foreach (JobKind kind in JobLimits.Kinds)
        {
            if (world.BatchFor(kind) is not null)
            {
                batched.Add(kind);
            }
        }

        Assert.Equal(new[] { JobKind.Tailor, JobKind.Carpenter, JobKind.Miller, JobKind.Baker, JobKind.Brewer }.Order(), batched.Order());
    }

    // ---------------------------------------------------------------
    //  The states and their words
    // ---------------------------------------------------------------

    /// <summary>§6 step 1: the words a card and the log read for each trade's walk and work, by the stint's count.</summary>
    [Theory]
    [MemberData(nameof(Trades))]
    public void EachBatchTradesWordsAreAsShipped(JobKind trade)
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Villager hand = world.Villagers.First(v => v.Alive && v.CanWork);

        (string logWalk, string logWork, string idle, string second, string third) = trade switch
        {
            JobKind.Miller => ("walking to the mill", "grinding wheat", "grinding wheat at the stones",
                "grinding wheat, the 2nd grind of the day", "grinding wheat, the 3rd grind of the day"),
            JobKind.Baker => ("walking to the bakery", "baking bread", "lighting the oven",
                "baking bread, the 2nd batch of the day", "baking bread, the 3rd batch of the day"),
            JobKind.Tailor => ("walking to the tailor's shop", "sewing coats", "cutting a hide for a coat",
                "sewing coats, the 2nd of the day", "sewing coats, the 3rd of the day"),
            _ => ("walking to the workshop", "making handcarts", "truing a wheel for a handcart",
                "making handcarts, the 2nd of the day", "making handcarts, the 3rd of the day"),
        };

        PutAt(world, hand, trade, working: false);
        Assert.Equal(logWalk, BehaviorSystem.Describe(hand.State, hand));
        Assert.Equal("walking to Somewhere", hand.DescribeState("Somewhere"));

        PutAt(world, hand, trade, working: true);
        Assert.Equal(logWork, BehaviorSystem.Describe(hand.State, hand));
        hand.BatchesThisStint = 0;
        Assert.Equal(idle, hand.DescribeState("Somewhere"));
        hand.BatchesThisStint = 1;
        Assert.Equal(second, hand.DescribeState("Somewhere"));
        hand.BatchesThisStint = 2;
        Assert.Equal(third, hand.DescribeState("Somewhere"));
    }

    /// <summary>§6 step 1: the walk and the work are both the trade's clock (D522, D659, D678).</summary>
    [Theory]
    [MemberData(nameof(Trades))]
    public void EachBatchTradesWalkAndWorkAreOutOnTheTrade(JobKind trade)
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Villager hand = world.Villagers.First(v => v.Alive && v.CanWork);

        PutAt(world, hand, trade, working: false);
        Assert.True(SkillSystem.OutOnTheWork(hand.State), $"{trade}'s walk is not out on the trade.");
        PutAt(world, hand, trade, working: true);
        Assert.True(SkillSystem.OutOnTheWork(hand.State), $"{trade}'s work is not out on the trade.");

        // And the skill it grows is the trade's row (one row a trade).
        Assert.Single(Config.Skills, s => s.GrownBy == trade);
    }

    // ---------------------------------------------------------------
    //  Step 5 — the shared pair, Joe's call A (D792)
    // ---------------------------------------------------------------

    /// <summary>
    /// §6 step 5: whose batch it is is in the fingerprint while at a batch, and only then — so a village with no batch hut
    /// hashes as it did, and two villages differing only in whose batch a hand is at are two villages. ⚠️ No shipped golden
    /// reads this: the one village that raises a mill and an oven is hashed once, at year 20, with nobody at a batch.
    /// </summary>
    [Fact]
    public void WhoseBatchIsInTheFingerprintOnlyAtABatch()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Villager hand = world.Villagers.First(v => v.Alive && v.CanWork);

        PutAt(world, hand, JobKind.Miller, working: true);
        ulong miller = StateHash.Compute(world);
        PutAt(world, hand, JobKind.Baker, working: true);
        ulong baker = StateHash.Compute(world);
        Assert.NotEqual(miller, baker);

        // Off the batch, the trade left behind is not a fact about the village.
        hand.State = VillagerState.Idle;
        ulong idleAfterBaking = StateHash.Compute(world);
        world.SetBatchOf(hand, JobKind.Tailor);
        Assert.Equal(idleAfterBaking, StateHash.Compute(world));
    }

    /// <summary>
    /// §6 step 5: the state is hashed as its number, so the retired numbers are never reused and every state after them
    /// keeps the number it shipped with (43–44 and 53–56 retired; the shared pair took the miller's 41–42).
    /// </summary>
    [Fact]
    public void TheRetiredStateNumbersAreNeverReused()
    {
        Assert.Equal(41, (int)VillagerState.TravelingToABatch);
        Assert.Equal(42, (int)VillagerState.WorkingABatch);
        Assert.Equal(45, (int)VillagerState.CuttingHay);
        Assert.Equal(52, (int)VillagerState.FetchingACoat);
        Assert.Equal(57, (int)VillagerState.FetchingAHandcart);
        Assert.Equal(64, (int)VillagerState.DrawingAtTheRiver);
        foreach (int retired in new[] { 43, 44, 53, 54, 55, 56 })
        {
            Assert.False(Enum.IsDefined((VillagerState)retired), $"State {retired} is retired and was reused.");
        }
    }

    /// <summary>
    /// §6 step 5, §7: a save from before W1 loads — its four trades' old state names read as the shared pair and the trade —
    /// with no format bump, and the card's words are put back; a save since round-trips the trade itself.
    /// </summary>
    [Theory]
    [InlineData("TravelingToMill", false, JobKind.Miller)]
    [InlineData("Grinding", true, JobKind.Miller)]
    [InlineData("TravelingToBakery", false, JobKind.Baker)]
    [InlineData("Baking", true, JobKind.Baker)]
    [InlineData("TravelingToTailor", false, JobKind.Tailor)]
    [InlineData("Sewing", true, JobKind.Tailor)]
    [InlineData("TravelingToWorkshop", false, JobKind.Carpenter)]
    [InlineData("Making", true, JobKind.Carpenter)]
    public void AnOldSavesBatchStatesLoadAsTheSharedPair(string oldName, bool working, JobKind trade)
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        Villager hand = loop.World.Villagers.First(v => v.Alive && v.CanWork);
        PutAt(loop.World, hand, trade, working);
        JsonObject document = SaveGame.Capture(loop, "test", "then", "code");

        // A save since W1 round-trips the trade.
        SimWorld since = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), Config, new InMemoryLogSink()).World;
        Assert.True(IsAt(since.Villagers.Single(v => v.Id == hand.Id), trade, working));

        // A save from before: the old name, and no `batch_trade`.
        JsonObject saved = VillagerIn(document, hand.Id);
        saved["state"] = oldName;
        saved.Remove("batch_trade");
        SimWorld before = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), Config, new InMemoryLogSink()).World;
        Villager loaded = before.Villagers.Single(v => v.Id == hand.Id);
        Assert.True(IsAt(loaded, trade, working), $"\"{oldName}\" loaded as {loaded.State} for {loaded.BatchTrade}.");
        Assert.Equal(hand.DescribeState("Somewhere"), loaded.DescribeState("Somewhere"));
    }

    /// <summary>The saved object of one villager, wherever the document keeps them.</summary>
    private static JsonObject VillagerIn(JsonNode node, int id)
    {
        if (node is JsonObject obj)
        {
            if (obj.ContainsKey("batches_this_stint") && obj["id"]?.GetValue<int>() == id)
            {
                return obj;
            }

            foreach (KeyValuePair<string, JsonNode?> child in obj)
            {
                if (child.Value is not null && TryVillagerIn(child.Value, id) is JsonObject found)
                {
                    return found;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                if (child is not null && TryVillagerIn(child, id) is JsonObject found)
                {
                    return found;
                }
            }
        }

        throw new InvalidOperationException($"Villager {id} is not in the save.");
    }

    private static JsonObject? TryVillagerIn(JsonNode node, int id)
    {
        try
        {
            return VillagerIn(node, id);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    // ---------------------------------------------------------------
    //  The D281 recall — the errand is the trade's, and only the trade's
    // ---------------------------------------------------------------

    /// <summary>
    /// §6 step 1, ⛔ the one W1 must not lose: a villager on a batch trade's walk, or at its work, keeps at it while the
    /// seat they hold is that trade's — and is sent home the tick it is another batch trade's (the yearly share-out moves
    /// people, D20). Without the second half a miller re-seated at the bakery bakes standing at the mill.
    /// </summary>
    [Theory]
    [MemberData(nameof(Trades))]
    public void ABatchErrandIsRecalledWhenTheSeatIsAnotherTrades(JobKind trade)
    {
        foreach (bool working in new[] { false, true })
        {
            foreach (JobKind seatedAt in new[] { JobKind.Tailor, JobKind.Carpenter, JobKind.Miller, JobKind.Baker })
            {
                SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
                ToolsTests.KeepTheVillageWarmAndFed(world);
                Workplace seat = RaiseTheHut(world, seatedAt);
                Villager hand = world.Villagers.First(v => v.Alive && v.CanWork);
                foreach (Workplace other in world.Workplaces)
                {
                    other.WorkerIds.Remove(hand.Id);
                }

                seat.WorkerIds.Add(hand.Id);
                hand.WorkplaceId = seat.Id;
                hand.Hunger = 0;
                hand.Rhythm = 0;
                PutAt(world, hand, trade, working);
                hand.ActionTicksRemaining = working ? 3 : 0;

                // The behaviour alone, one tick: the loop's first tick shares out the work and would re-seat them.
                new BehaviorSystem().Execute(world);

                bool kept = IsAt(hand, trade, working);
                _output.WriteLine($"{trade} {(working ? "at work" : "walking")}, seated at a {seatedAt}'s: {(kept ? "kept at it" : $"recalled ({hand.State})")}");
                Assert.Equal(seatedAt == trade, kept);
            }
        }
    }

    // ---------------------------------------------------------------
    //  The stops — `WhyTheBatchWaits`
    // ---------------------------------------------------------------

    /// <summary>§6 step 1: the two stops every trade shares — the player's limit, and no store with a batch's input.</summary>
    [Theory]
    [MemberData(nameof(Trades))]
    public void EachBatchTradesSharedStopsReadAsShipped(JobKind trade)
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Workplace hut = RaiseTheHut(world, trade);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        Batch batch = world.BatchFor(trade)!.Value;
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TakeAll(batch.Input);
        }

        string noStore = trade switch
        {
            JobKind.Miller => $"Nothing to grind — no store within reach of {hut.Name} has the {Config.WheatPerGrind} wheat a grind takes.",
            JobKind.Baker => $"Nothing to bake — no store within reach of {hut.Name} has the {Config.FlourPerBake} flour and the "
                + $"{Config.FirewoodPerFiring} firewood to light the oven a bake takes.",
            JobKind.Tailor => $"Nothing to sew — no store within reach of {hut.Name} has the {Config.LeatherPerCoat} leather a coat takes.",
            _ => $"Nothing to make — no store within reach of {hut.Name} has the {Config.LogsPerHandcart} logs a handcart takes.",
        };
        Assert.Equal(noStore, world.WhyTheBatchWaits(hut));

        StoreTaking(world, batch.Input).Store.Add(batch.Input, batch.InputPerBatch * 4);
        Assert.Null(world.WhyTheBatchWaits(hut));

        string goods = world.GoodsCatalog.NameOf(batch.Output);
        Assert.True(world.SetStockLimit(batch.Output, 0).Allowed);
        Assert.Equal(
            $"Nothing to {batch.Verb} — you asked the village to keep 0 {goods} and it has 0 stored.",
            world.WhyTheBatchWaits(hut));
    }

    /// <summary>
    /// §6 step 1, filled at step 4 (D793): each trade's <b>own</b> stop, with no limit set — the mill while the ovens have
    /// flour enough, the tailor while everyone is clothed, the workshop while every household has a cart, the oven while
    /// the homes want their firewood. ⚠️ Found missing by step 4's red-check: no test anywhere read the mill's ovens rule.
    /// </summary>
    [Theory]
    [MemberData(nameof(Trades))]
    public void EachBatchTradesOwnStopReadsAsShipped(JobKind trade)
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Workplace hut = RaiseTheHut(world, trade);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        Batch batch = world.BatchFor(trade)!.Value;
        StoreTaking(world, batch.Input).Store.Add(batch.Input, batch.InputPerBatch * 4);
        Assert.Null(world.WhyTheBatchWaits(hut));

        string expected;
        switch (trade)
        {
            case JobKind.Miller:
                int enough = LabourQuota.FlourWantedForTheOvens(world);
                StoreTaking(world, Goods.Flour).Store.Add(Goods.Flour, enough);
                expected = $"Nothing to grind — the ovens have flour enough ({enough} stored).";
                break;
            case JobKind.Tailor:
                int coats = LabourQuota.ClothingWanted(world);
                StoreTaking(world, Goods.Clothing).Store.Add(Goods.Clothing, coats);
                expected = $"Nothing to sew — every adult has a coat, and the shelves hold a year's wear ({coats} stored).";
                break;
            case JobKind.Carpenter:
                int carts = LabourQuota.HandcartsWanted(world) + LabourQuota.HandcartSpare;
                StoreTaking(world, Goods.Handcart).Store.Add(Goods.Handcart, carts);
                expected = $"Nothing to make — every household has a handcart, and the shelves hold one for the next ({carts} stored).";
                break;
            default:
                foreach (Household household in world.Households)
                {
                    household.Stockpile.TakeAll(Goods.Firewood);
                }

                foreach (StoreBuilding store in world.StoreBuildings)
                {
                    store.Store.TakeAll(Goods.Firewood);
                }

                expected = "Nothing to bake — the village needs its firewood for the winter.";
                break;
        }

        Assert.Equal(expected, world.WhyTheBatchWaits(hut));
    }

    /// <summary>
    /// §6 step 4 (D793): the shipped rows' rules, column by column — ⭐ the guard for what a four-hand village cannot show
    /// (step 4's red-check: a band moved, or the famine rule flipped, changed no number in the sweep below).
    /// </summary>
    [Fact]
    public void TheShippedBatchRowsRulesAreAsSpecced()
    {
        string Rule(BatchRow r) => $"{r.Trade}: {r.Stop}, {r.Hands}, {r.QuotaBand}, famine {(r.StandsDownInFamine ? "stands down" : "works")}"
            + (r.FromTheProduct ? ", the product's recipe" : string.Empty);

        Assert.Equal(
            new[]
            {
                "Tailor: EveryoneClothed, ShortfallOverAYear, AfterTools, famine stands down",
                "Carpenter: EveryHouseholdCarted, ShortfallOverAYear, AfterTools, famine works, the product's recipe",
                "Miller: FedFirst, OvensSupplied, EverySeatWithWork, AfterStone, famine works",
                "Baker: None, EverySeatWithWork, AfterStone, famine works",
                "Brewer: FedFirst, EverySeatWithWork, AfterStone, famine stands down",
            },
            Config.BatchRows.Select(Rule));
    }

    /// <summary>
    /// §6 step 4 (D793): a shortfall-hired trade is hired by the shortfall over one hand's year, capped by the seats — posed
    /// with a tailor so slow a year is one coat, so the divisor shows (in the shipped village one tailor is wanted either way).
    /// </summary>
    [Fact]
    public void AShortfallTradeIsHiredByTheYearsWorkUpToTheSeats()
    {
        SimConfig c = Config with { SewTicks = 1_000_000, TailorShopCapacity = 6 };
        SimWorld world = SimFactory.CreatePhase0(c, new InMemoryLogSink()).World;
        RaiseTheHut(world, JobKind.Tailor);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        StoreTaking(world, Goods.Leather).Store.Add(Goods.Leather, 200);

        BatchRow tailor = world.Batches.For(JobKind.Tailor)!;
        Assert.Equal(1, VillageEconomy.BatchesPerYearAtWorst(c, tailor));
        int shortfall = LabourQuota.ClothingShortfall(world);
        _output.WriteLine($"shortfall {shortfall}, seats 6: {LabourQuota.TailorsWanted(world)} wanted");
        Assert.True(shortfall >= 2, "The posed village is not short enough of coats to tell one tailor from several.");
        Assert.Equal(Math.Min(shortfall, 6), LabourQuota.TailorsWanted(world));
    }

    // ---------------------------------------------------------------
    //  Hands — the quota
    // ---------------------------------------------------------------

    /// <summary>
    /// §6 step 1, §5 risk 1: the whole quota, every trade, with all four huts standing and working, at every count of
    /// hands from the village's own down to one — so a take moved one place in the order shows wherever the hands run out.
    /// </summary>
    [Fact]
    public void TheQuotaWithEveryBatchHutStandingIsAsShipped()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        foreach (JobKind trade in new[] { JobKind.Miller, JobKind.Baker, JobKind.Tailor, JobKind.Carpenter })
        {
            RaiseTheHut(world, trade);
        }

        ToolsTests.KeepTheVillageWarmAndFed(world);
        StoreTaking(world, Goods.Wheat).Store.Add(Goods.Wheat, 200);
        StoreTaking(world, Goods.Flour).Store.Add(Goods.Flour, 40);
        StoreTaking(world, Goods.Leather).Store.Add(Goods.Leather, 40);
        StoreTaking(world, Goods.Logs).Store.Add(Goods.Logs, 200);

        var seen = new List<string>();
        Sweep(world, "all four", seen);

        // And the mill and the oven with the hands to themselves — the leather and the logs gone, so the tailor and the
        // carpenter want nobody and the miller and the baker show their own order.
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TakeAll(Goods.Leather);
            store.Store.TakeAll(Goods.Logs);
        }

        Sweep(world, "mill and oven", seen);
        Assert.Equal(QuotaAsShipped, seen);
    }

    /// <summary>The quota at every count of hands from the village's own down to one.</summary>
    private void Sweep(SimWorld world, string what, List<string> seen)
    {
        List<Villager> adults = world.Villagers.Where(v => v.Alive && v.LifeStage == LifeStage.Adult && !v.IsPupil).ToList();
        for (int benched = 0; benched < adults.Count; benched++)
        {
            for (int i = 0; i < adults.Count; i++)
            {
                adults[i].LifeStage = i < benched ? LifeStage.Child : LifeStage.Adult;
            }

            LabourQuota quota = LabourQuota.For(world);
            string line = $"{what}, {quota.Hands} hands: " + string.Join(" ", JobLimits.Kinds
                .Where(k => quota.For(k) > 0 || quota.Needed(k) > 0)
                .Select(k => $"{k}={quota.For(k)}/{quota.Needed(k)}"));
            _output.WriteLine($"\"{line}\",");
            seen.Add(line);
        }

        foreach (Villager adult in adults)
        {
            adult.LifeStage = LifeStage.Adult;
        }
    }

    /// <summary>A store that takes the good — the granary for grain, the warehouse for the rest — never the market.</summary>
    private static StoreBuilding StoreTaking(SimWorld world, Goods goods) =>
        world.StoreBuildings.First(s => s.Kind != StoreKind.Market && s.Accepts(goods));

    /// <summary>
    /// What <see cref="TheQuotaWithEveryBatchHutStandingIsAsShipped"/> read on the day W1 began (D792). ⭐ Read it as the
    /// take order: with four hands the tailor and the carpenter are seated and the miller and the baker, wanting two each,
    /// are not — the coat's band before the mill's; with three the tailor before the carpenter; and with the mill and the
    /// oven alone, the forester before the miller and the miller before the baker.
    /// </summary>
    private static readonly string[] QuotaAsShipped =
    {
        "all four, 4 hands: Forager=2/0 Marketer=0/1 Miller=0/2 Baker=0/2 Tailor=1/1 Carpenter=1/1",
        "all four, 3 hands: Forager=2/0 Marketer=0/1 Miller=0/2 Baker=0/2 Tailor=1/1 Carpenter=0/1",
        "all four, 2 hands: Forager=2/0 Marketer=0/1 Miller=0/2 Baker=0/2 Tailor=0/1 Carpenter=0/1",
        "all four, 1 hands: Forager=1/0 Marketer=0/1 Miller=0/2 Baker=0/2 Tailor=0/1 Carpenter=0/1",
        "mill and oven, 4 hands: Forager=2/0 Forester=1/1 Marketer=0/1 Miller=1/2 Baker=0/2",
        "mill and oven, 3 hands: Forager=2/0 Forester=1/1 Marketer=0/1 Miller=0/2 Baker=0/2",
        "mill and oven, 2 hands: Forager=2/0 Forester=0/1 Marketer=0/1 Miller=0/2 Baker=0/2",
        "mill and oven, 1 hands: Forager=1/0 Forester=0/1 Marketer=0/1 Miller=0/2 Baker=0/2",
    };

    /// <summary>§6 step 1: the allocator claims seats in the quota's order, the batch trades in their two bands.</summary>
    [Fact]
    public void TheAllocatorsOrderHoldsTheBatchTradesInTheirBands()
    {
        FieldInfo order = typeof(LabourAllocator).GetField("KindsInOrder", BindingFlags.NonPublic | BindingFlags.Static)!;
        var kinds = (JobKind[])order.GetValue(null)!;
        int At(JobKind k) => Array.IndexOf(kinds, k);

        Assert.Equal(At(JobKind.Smith) + 1, At(JobKind.Tailor));
        Assert.Equal(At(JobKind.Tailor) + 1, At(JobKind.Carpenter));
        Assert.Equal(At(JobKind.Carpenter) + 1, At(JobKind.Quarrier));
        Assert.Equal(At(JobKind.Miner) + 1, At(JobKind.Miller));
        Assert.Equal(At(JobKind.Miller) + 1, At(JobKind.Baker));
        Assert.Equal(At(JobKind.Baker) + 1, At(JobKind.Brewer));
        Assert.Equal(At(JobKind.Brewer) + 1, At(JobKind.Herdsman));
    }

    /// <summary>
    /// §6 step 1, and step 4's proof: what one tailor sews and one carpenter makes in a year. Pinned on the two sums
    /// (<c>CoatsSewnPerYearAtWorst</c>, <c>HandcartsMadePerYearAtWorst</c>) before W1, and read since step 4 off the one
    /// <see cref="VillageEconomy.BatchesPerYearAtWorst"/> that replaced them — the same two integers.
    /// </summary>
    [Fact]
    public void ABatchTradesYearAtWorstIsAsShipped()
    {
        SimConfig c = Config;
        int coats = VillageEconomy.BatchesPerYearAtWorst(c, c.BatchRows.Single(r => r.Trade == JobKind.Tailor));
        int carts = VillageEconomy.BatchesPerYearAtWorst(c, c.BatchRows.Single(r => r.Trade == JobKind.Carpenter));
        _output.WriteLine($"coats {coats}, handcarts {carts}");
        Assert.Equal(CoatsAYear, coats);
        Assert.Equal(HandcartsAYear, carts);
    }

    /// <summary>Read off the two sums on the day W1 began (D792).</summary>
    private const int CoatsAYear = 20;

    /// <summary>Read off the two sums on the day W1 began (D792).</summary>
    private const int HandcartsAYear = 10;

    // ---------------------------------------------------------------
    //  Who knows of it — `KnowsOf`
    // ---------------------------------------------------------------

    /// <summary>§6 step 1: a batch trade's good is unknown until its hut is, and known the moment it stands.</summary>
    [Theory]
    [MemberData(nameof(Trades))]
    public void ABatchTradesGoodWaitsOnItsHut(JobKind trade)
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Goods made = world.BatchFor(trade)!.Value.Output;
        BuildingKind hut = HutOf(trade);

        Assert.False(world.KnowsOf(made, kind => kind != hut), $"{made} was known without its {hut}.");
        Assert.True(world.KnowsOf(made, kind => kind == hut), $"{made} was unknown with its {hut}.");
    }
}
