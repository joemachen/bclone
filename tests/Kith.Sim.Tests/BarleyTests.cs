using System.Text.Json.Nodes;
using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Determinism;
using Kith.Sim.Logging;
using Kith.Sim.Persistence;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

/// <summary>
/// ⭐ W7, the second crop — <c>specs/barley.md</c> (D822–D825). Barley as a good, a farm told what to grow, each farm's
/// limit its own crop's, wheat's chain kept wheat's, and barley learned when a forager finds it wild.
/// </summary>
/// <remarks>
/// <b>D283's lesson is the first guard here</b>: D277 converted every reader of the food total and missed the mouth, and
/// fish was food nobody could eat. So barley is proved eaten, not only counted.
/// </remarks>
public sealed class BarleyTests
{
    private const int Wheat = 1;
    private const int Barley = 2;

    private readonly ITestOutputHelper _output;

    public BarleyTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Loop(SimConfig config) => SimFactory.CreatePhase0(config, new InMemoryLogSink());

    // ---------------------------------------------------------------
    //  §3 — barley, the good
    // ---------------------------------------------------------------

    /// <summary>§3: barley is wheat's row to the letter — food at nutrition 1, stored where wheat is — and crop 2 yields it.</summary>
    [Fact]
    public void BarleyIsAGrainStoredWhereWheatIsAndCropTwoYieldsIt()
    {
        SimWorld world = Loop(ShippedConfig.Load()).World;
        GoodsCatalog goods = world.GoodsCatalog;

        Assert.Equal(16, (int)Goods.Barley);
        Assert.Equal("barley", goods.NameOf(Goods.Barley));
        Assert.True(goods.Edible(Goods.Barley));
        Assert.Equal(GoodCategory.Food, goods.CategoryOf(Goods.Barley));
        Assert.Equal(goods.NutritionOf(Goods.Wheat), goods.NutritionOf(Goods.Barley));
        foreach (StoreKind kind in Enum.GetValues<StoreKind>())
        {
            Assert.Equal(goods.StoredBy(Goods.Wheat, kind), goods.StoredBy(Goods.Barley, kind));
        }

        Assert.Equal(Goods.Wheat, world.Crops.GoodOf(Wheat));
        Assert.Equal(Goods.Barley, world.Crops.GoodOf(Barley));
        Assert.Equal("barley", world.Crops[Barley]!.Name);
    }

    /// <summary>⛔ §3, D283: a larder holding nothing but barley feeds the household — the mouth, not only the count.</summary>
    [Fact]
    public void AHouseholdWithOnlyBarleyEatsIt()
    {
        SimWorld world = Loop(Config).World;
        Stockpile larder = world.Households[0].Stockpile;
        for (int g = 0; g < world.GoodsCatalog.Count; g++)
        {
            larder.TakeAll((Goods)g);
        }

        larder.Receive(Goods.Barley, 10);
        Assert.Equal(10, world.FoodIn(larder));

        Assert.True(world.TakeAMealFrom(larder, 3, out int worth, out Goods best), "A larder of barley fed nobody.");
        Assert.Equal(Goods.Barley, best);
        Assert.Equal(3, worth);
        Assert.Equal(7, larder[Goods.Barley]);
    }

    /// <summary>§3: barley in a granary is the village's food — the birth gate's and the limit's total.</summary>
    [Fact]
    public void BarleyInAGranaryIsTheVillagesFood()
    {
        SimWorld world = Loop(Config).World;
        int before = world.FoodTheVillageHolds();
        StoreBuilding granary = world.StoreBuildings.First(s => s.Kind == StoreKind.Granary);
        granary.Store.Receive(Goods.Barley, 100);
        Assert.Equal(before + 100, world.FoodTheVillageHolds());
        Assert.Equal(100, world.HeldAgainstItsLimit(Goods.Barley));
    }

    // ---------------------------------------------------------------
    //  §4 — a farm told what to grow
    // ---------------------------------------------------------------

    /// <summary>§4: an untouched farm grows wheat, and every tile it sows carries wheat.</summary>
    [Fact]
    public void AnUntouchedFarmSowsWheat()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        Assert.Equal(Wheat, farm.Grows);
        FarmFixtures.GiveItGround(world, farm, reach: 2);

        FarmFixtures.StepToTheStartOf(loop, Season.Spring);
        loop.Step(Config.TicksPerSeason);

        (int wheat, int barley) = SownBy(world, farm);
        _output.WriteLine($"{world.Clock.SeasonAndYear()}: {wheat} wheat, {barley} barley sown");
        Assert.True(wheat > 0, "A spring passed and nothing was sown.");
        Assert.Equal(0, barley);
    }

    /// <summary>⭐ §4: a farm set to barley sows barley in spring and reaps barley in autumn — and no wheat at all.</summary>
    [Fact]
    public void AFarmSetToBarleySowsAndReapsBarley()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        world.BarleyLearned = true;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 2);
        Assert.True(world.SetGrows(farm, Barley).Allowed);

        FarmFixtures.StepToTheStartOf(loop, Season.Spring);
        loop.Step(Config.TicksPerSeason);
        (int wheat, int barley) = SownBy(world, farm);
        _output.WriteLine($"{world.Clock.SeasonAndYear()}: {wheat} wheat, {barley} barley sown");
        Assert.True(barley > 0, "A farm set to barley sowed no barley.");
        Assert.Equal(0, wheat);

        FarmFixtures.StepToTheStartOf(loop, Season.Fall);
        loop.Step(Config.TicksPerSeason);
        _output.WriteLine($"{world.Clock.SeasonAndYear()}: the farm took in {farm.Store.Produced(Goods.Barley)} barley, "
            + $"{farm.Store.Produced(Goods.Wheat)} wheat");
        Assert.True(farm.Store.Produced(Goods.Barley) > 0, "The barley was sown and none was reaped.");
        Assert.Equal(0, farm.Store.Produced(Goods.Wheat));
    }

    /// <summary>
    /// ⭐ §4: a change in summer leaves this year's wheat wheat — the tile carries what was sown (D161) — and the next
    /// spring sows barley.
    /// </summary>
    [Fact]
    public void AChangeInSummerLeavesTheStandingWheatWheat()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        world.BarleyLearned = true;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 2);
        FarmFixtures.SowEveryTileOf(world, farm);

        FarmFixtures.StepToTheStartOf(loop, Season.Summer);
        Assert.True(world.SetGrows(farm, Barley).Allowed);

        FarmFixtures.StepToTheStartOf(loop, Season.Fall);
        loop.Step(Config.TicksPerSeason);
        Assert.True(farm.Store.Produced(Goods.Wheat) > 0, "The wheat standing when the card changed was not reaped as wheat.");
        Assert.Equal(0, farm.Store.Produced(Goods.Barley));

        FarmFixtures.StepToTheStartOf(loop, Season.Spring);
        loop.Step(Config.TicksPerSeason);
        (int wheat, int barley) = SownBy(world, farm);
        _output.WriteLine($"{world.Clock.SeasonAndYear()}: {wheat} wheat, {barley} barley sown");
        Assert.True(barley > 0, "The next spring sowed no barley.");
        Assert.Equal(0, wheat);
    }

    /// <summary>§4: the setter refuses a crop no row defines, barley before it is learned, and a building that is no farm.</summary>
    [Fact]
    public void SetGrowsRefusesWhatCannotBeSown()
    {
        SimConfig config = Config with { BarleyUnlockForage = 7 };
        SimWorld world = Loop(config).World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);

        PlacementVerdict unknown = world.SetGrows(farm, 9);
        Assert.False(unknown.Allowed);
        _output.WriteLine(unknown.Reason);

        PlacementVerdict notYet = world.SetGrows(farm, Barley);
        _output.WriteLine(notYet.Reason);
        Assert.False(notYet.Allowed);
        Assert.Contains("gathered 7 from the wild", notYet.Reason, StringComparison.Ordinal);
        Assert.Equal(notYet.Reason, world.WhyCannotGrow(Barley));
        Assert.Null(world.WhyCannotGrow(Wheat));

        Workplace notAFarm = world.Workplaces.First(w => w.Kind != JobKind.Farmer);
        Assert.False(world.SetGrows(notAFarm, Wheat).Allowed);
        Assert.Equal(Wheat, farm.Grows);

        world.BarleyLearned = true;
        Assert.True(world.SetGrows(farm, Barley).Allowed);
        Assert.Equal(Barley, farm.Grows);
        Assert.Equal(Goods.Barley, world.GoodGrownAt(farm));
    }

    /// <summary>§8: a farm left on wheat mixes nothing; set to barley, it does; set back, it is as it was.</summary>
    [Fact]
    public void AFarmsCropIsInTheFingerprintOnlyOffItsDefault()
    {
        SimWorld world = Loop(Config).World;
        world.BarleyLearned = true;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        ulong before = StateHash.Compute(world);

        Assert.True(world.SetGrows(farm, Barley).Allowed);
        Assert.NotEqual(before, StateHash.Compute(world));

        Assert.True(world.SetGrows(farm, Wheat).Allowed);
        Assert.Equal(before, StateHash.Compute(world));
    }

    /// <summary>§8: a farm's crop and the barley latch survive the round trip to the same hash and the same village a season on.</summary>
    [Fact]
    public void AFarmsCropAndTheLatchSurviveASave()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        world.BarleyLearned = true;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 2);
        Assert.True(world.SetGrows(farm, Barley).Allowed);

        SimLoop loaded = Reload(loop);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded.World));
        Assert.Equal(Barley, loaded.World.Workplaces.Single(w => w.Id == farm.Id).Grows);
        Assert.True(loaded.World.BarleyLearned);

        loop.Step(Config.TicksPerSeason);
        loaded.Step(Config.TicksPerSeason);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded.World));
    }

    /// <summary>§8: a save from before W7 — no <c>grows</c>, no <c>barley_learned</c> — reads its farms as wheat and barley unlearned.</summary>
    [Fact]
    public void ASaveFromBeforeBarleyReadsWheatAndUnlearned()
    {
        SimLoop loop = Loop(Config);
        Workplace farm = FarmFixtures.RaiseAFarm(loop.World);
        JsonObject document = SaveGame.Capture(loop, "test", "2026-11-21 12:00", "test");

        // A save from before W7 has neither key: take them out wherever they were written.
        int removed = StripKeys(document, "grows", "barley_learned");
        Assert.True(removed > 0, "The save wrote neither key, so this test strips nothing.");

        SimLoop loaded = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), loop.World.Config, null);
        Assert.Equal(Wheat, loaded.World.Workplaces.Single(w => w.Id == farm.Id).Grows);
        Assert.False(loaded.World.BarleyLearned);
    }

    /// <summary>
    /// ⭐ D827 (Joe: <i>"Yes, make them open"</i>): a save from before barley — every list a good long, one short — opens,
    /// and plays on: barley held nowhere, its limit the starting 2,000, a filtered granary letting it in (the player could
    /// not have kept out a good that did not exist), the lowlands holding what a fresh neighbour does.
    /// </summary>
    /// <remarks>
    /// Posed by cutting today's save back to sixteen goods, the shape every one of Joe's saves has — and proved on them
    /// by `tools/harness/ZzBarley.cs` (252 of 252 readable saves open, as before barley).
    /// </remarks>
    [Fact]
    public void ASaveFromBeforeBarleyOpensAndPlaysOn()
    {
        SimLoop loop = Loop(ShippedConfig.Load());
        SimWorld world = loop.World;
        StoreBuilding granary = world.StoreBuildings.First(s => s.Kind == StoreKind.Granary || s.Kind == StoreKind.Cart);
        Assert.True(world.SetStoreAccepts(granary, Goods.Barley, false).Allowed);
        Assert.False(granary.PlayerAllows(Goods.Barley), "the premise: the filter is spoken and keeps barley out");
        int goods = world.GoodsCatalog.Count;

        JsonObject document = SaveGame.Capture(loop, "test", "2026-11-21 12:00", "test");
        int cut = CutBackAGood(document["world"], goods);
        _output.WriteLine($"{cut} lists cut back from {goods} goods to {goods - 1}");
        Assert.True(cut > 10, $"Only {cut} lists were cut: the pose is not a save from before barley.");

        SimLoop loaded = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), world.Config, null);
        SimWorld old = loaded.World;
        Assert.Equal(0, old.InStores(Goods.Barley));
        Assert.Equal(2000, old.StockLimits.For(Goods.Barley));
        Assert.True(old.StoreBuildings.Single(s => s.Id == granary.Id).PlayerAllows(Goods.Barley),
            "A filter saved before barley existed kept barley out.");
        Assert.Equal(world.NeighbourStocks[1].Held[(int)Goods.Barley], old.NeighbourStocks[1].Held[(int)Goods.Barley]);

        // And it plays on: a year stepped without a fault (an untended founding may not live it out; the step is the claim).
        ulong opened = old.Tick;
        loaded.Step(world.Config.TicksPerYear);
        Assert.Equal(opened + (ulong)world.Config.TicksPerYear, old.Tick);
    }

    /// <summary>D827: a save holding a good this game no longer has is still refused, in the words it always was.</summary>
    [Fact]
    public void ASaveWithAGoodTheDataLacksIsStillRefused()
    {
        SimLoop loop = Loop(Config);
        JsonObject document = SaveGame.Capture(loop, "test", "2026-11-21 12:00", "test");
        JsonObject store = document["world"]!["stores"]!.AsArray()[0]!["store"]!.AsObject();
        store["held"]!.AsArray().Add(0);
        store["produced"]!.AsArray().Add(0);

        SaveDataException refused = Assert.Throws<SaveDataException>(
            () => SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), loop.World.Config, null));
        _output.WriteLine(refused.Message);
        Assert.Contains("goods where this game's data has", refused.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  §4.1 — a farm's limit is its own crop's
    // ---------------------------------------------------------------

    /// <summary>⭐ §4.1: a met barley limit stops the barley farm's reap and not the wheat farm's beside it — and the reverse.</summary>
    [Fact]
    public void ABarleyFarmStopsAtTheBarleyLimitAndAWheatFarmReapsOn()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        world.BarleyLearned = true;
        Workplace wheat = FarmFixtures.RaiseAFarm(world);
        Workplace barley = FarmFixtures.RaiseAFarm(world, AwayFrom(world, wheat));
        FarmFixtures.GiveItGround(world, wheat, reach: 1);
        FarmFixtures.GiveItGround(world, barley, reach: 1);
        Assert.True(world.SetGrows(barley, Barley).Allowed);
        Ripen(world, wheat, Wheat);
        Ripen(world, barley, Barley);
        FarmFixtures.StepToTheStartOf(loop, Season.Fall);
        Ripen(world, wheat, Wheat);
        Ripen(world, barley, Barley);

        StoreBuilding granary = world.StoreBuildings.First(s => s.Kind == StoreKind.Granary);
        granary.Store.Receive(Goods.Barley, 50);
        Assert.True(world.SetStockLimit(Goods.Barley, 10).Allowed);
        Assert.True(world.SetStockLimit(Goods.Wheat, null).Allowed);

        Assert.Null(world.NextFieldToWork(barley, barley.Tile));
        Assert.NotNull(world.NextFieldToWork(wheat, wheat.Tile));
        Assert.Equal(Goods.Barley, world.GrainHeldBackAt(barley));
        Assert.Null(world.GrainHeldBackAt(wheat));

        // The card's sentence names the crop — with a hand seated, or the card says nobody works there first.
        barley.WorkerIds.Add(world.Villagers.First(v => v.Alive && v.CanWork).Id);
        string? note = world.IdleNote(barley);
        _output.WriteLine(note);
        Assert.NotNull(note);
        Assert.Contains("10 barley", note, StringComparison.Ordinal);

        // And the reverse: wheat full, barley open.
        Assert.True(world.SetStockLimit(Goods.Barley, null).Allowed);
        granary.Store.Receive(Goods.Wheat, 50);
        Assert.True(world.SetStockLimit(Goods.Wheat, 10).Allowed);
        Assert.NotNull(world.NextFieldToWork(barley, barley.Tile));
        Assert.Null(world.NextFieldToWork(wheat, wheat.Tile));
        Assert.Equal(Goods.Wheat, world.GrainHeldBackAt(wheat));
    }

    /// <summary>§4.1: the farmers' quota stops on every farm's own crop — a met wheat limit does not stand a barley farm down.</summary>
    [Fact]
    public void AMetWheatLimitDoesNotStandABarleyFarmDown()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        world.BarleyLearned = true;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 1);
        Assert.True(world.SetGrows(farm, Barley).Allowed);
        FarmFixtures.StepToTheStartOf(loop, Season.Fall);
        Ripen(world, farm, Barley);

        StoreBuilding granary = world.StoreBuildings.First(s => s.Kind == StoreKind.Granary);
        granary.Store.Receive(Goods.Wheat, 50);
        Assert.True(world.SetStockLimit(Goods.Wheat, 10).Allowed);

        Assert.True(world.FarmerSeatsWithGroundToWork() > 0, "A met wheat limit closed a barley farm's seats.");
        Assert.Equal(Goods.Barley, LabourQuota.GoodTheTradeMakes(world, JobKind.Farmer));
    }

    /// <summary>
    /// §4.1: the farmers' "why none" sentence reads each farm's own crop — a met wheat limit says nothing to a village whose
    /// only farm grows barley; a met barley limit does. Posed in winter, when no farm has ground to work, so the limit is
    /// the only reason left to give.
    /// </summary>
    [Fact]
    public void TheFarmersStopSentenceNamesTheFarmsOwnCrop()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        world.BarleyLearned = true;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        Assert.True(world.SetGrows(farm, Barley).Allowed);
        FarmFixtures.StepToTheStartOf(loop, Season.Winter);
        Assert.Equal(0, world.FarmerSeatsWithGroundToWork());

        StoreBuilding granary = world.StoreBuildings.First(s => s.Kind == StoreKind.Granary);
        granary.Store.Receive(Goods.Wheat, 50);
        Assert.True(world.SetStockLimit(Goods.Wheat, 10).Allowed);
        Assert.True(world.SetStockLimit(Goods.Barley, null).Allowed);
        string? wheatMet = LabourQuota.WhyTheVillageWantsNone(world, JobKind.Farmer);
        _output.WriteLine($"wheat met: {wheatMet}");
        Assert.True(wheatMet is null || !wheatMet.Contains("barley", StringComparison.Ordinal),
            $"A met wheat limit was blamed on a barley farm: \"{wheatMet}\"");

        granary.Store.Receive(Goods.Barley, 50);
        Assert.True(world.SetStockLimit(Goods.Barley, 10).Allowed);
        string? barleyMet = LabourQuota.WhyTheVillageWantsNone(world, JobKind.Farmer);
        _output.WriteLine($"barley met: {barleyMet}");
        Assert.Equal("the barley limit of 10 is met", barleyMet);
    }

    // ---------------------------------------------------------------
    //  §5 — wheat's chain stays wheat's (call 5)
    // ---------------------------------------------------------------

    /// <summary>§5, call 5: reaped barley does not count toward the mill; reaped wheat does.</summary>
    [Fact]
    public void ReapedBarleyDoesNotTeachTheMill()
    {
        SimWorld world = Loop(Config).World;
        world.Reaped(Goods.Barley, 10_000);
        Assert.Equal(0, world.WheatEverReaped);
        Assert.False(world.IsUnlocked(BuildingKind.Mill));
        world.Reaped(Goods.Wheat, 3);
        Assert.Equal(3, world.WheatEverReaped);
    }

    // ---------------------------------------------------------------
    //  §9.1 — barley is a forager's find
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ §9.1: barley is learned at the number of forage gathered — one short is not, the next is, once; a moment that
    /// stops the village; the Lore node reads the counter and the knob the gate reads.
    /// </summary>
    [Fact]
    public void BarleyIsLearnedAtTheNumberOfForageGathered()
    {
        // ⚠️ A knob that is not the default (7, not 5,000), so a mutant reading a constant goes red.
        SimConfig config = Config with { BarleyUnlockForage = 7 };
        SimWorld world = Loop(config).World;
        Assert.False(world.BarleyLearned);

        world.Foraged(6);
        Assert.Equal(6, world.FoodEverProducedOf(Goods.Produce));
        Assert.False(world.BarleyLearned, "One short and barley was known.");
        Assert.Contains("(6 so far)", world.WhyCannotGrow(Barley), StringComparison.Ordinal);

        world.Moments.Clear();
        world.Foraged(1);
        Assert.True(world.BarleyLearned);
        Assert.Null(world.WhyCannotGrow(Barley));
        Moment learned = Assert.Single(world.Moments);
        _output.WriteLine($"{learned.Title}: {learned.Body}");
        Assert.Contains("wild barley", learned.Body, StringComparison.Ordinal);
        Assert.Contains("gathered 7", learned.Body, StringComparison.Ordinal);
        Assert.True(learned.WaitsToBeDismissed);
        Assert.True(world.ShownTheTechTree, "A hands-on learning introduces Lore (D440).");

        world.Foraged(10);
        Assert.Single(world.Moments);

        TechNodeRow node = Assert.Single(config.TechNodeRows, n => n.Id == "barley");
        Assert.Null(node.Unlocks);
        Assert.Equal(Barley, node.UnlocksCrop);
        Assert.Equal(TechCondition.ForageGathered, node.Condition);
        Assert.Equal((17, 7), TechTree.ProgressOf(world, TechCondition.ForageGathered));
        Assert.Equal(TechState.Known, TechTree.StateOf(world, node).State);
        Assert.True(TechTree.IntroducesTheMap(TechCondition.ForageGathered));
    }

    /// <summary>
    /// §11.4 (Joe, 2026-11-21): the find's sentence follows what the village has — no farm, and it says to build one; a
    /// farm, and it names it; and it promises no beer before the brewery exists.
    /// </summary>
    [Fact]
    public void TheFindSaysBuildAFarmOnlyWhereThereIsNone()
    {
        SimConfig config = Config with { BarleyUnlockForage = 5 };

        SimWorld bare = Loop(config).World;
        Assert.DoesNotContain(bare.Workplaces, w => w.Kind == JobKind.Farmer);
        bare.Moments.Clear();
        bare.Foraged(5);
        Moment none = Assert.Single(bare.Moments);
        _output.WriteLine(none.Body);
        Assert.Contains("build a farm", none.Body, StringComparison.Ordinal);
        Assert.Contains("excited", none.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("beer", none.Body, StringComparison.OrdinalIgnoreCase);

        SimWorld farmed = Loop(config).World;
        Workplace farm = FarmFixtures.RaiseAFarm(farmed);
        farmed.Moments.Clear();
        farmed.Foraged(5);
        Moment one = Assert.Single(farmed.Moments);
        _output.WriteLine(one.Body);
        Assert.DoesNotContain("build a farm", one.Body, StringComparison.Ordinal);
        Assert.Contains($"sown at {farm.Name}", one.Body, StringComparison.Ordinal);
        Assert.Contains("excited", one.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("beer", one.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// §11.1 (D825): barley is unknown until it is found — no store, limit or market sets room aside for it first (a
    /// market's starting limit of 1,000 would have taken an eighth of its room from the founding) — and known after.
    /// </summary>
    [Fact]
    public void BarleyIsUnknownUntilItIsFound()
    {
        SimWorld world = Loop(Config).World;
        Assert.True(world.KnowsOf(Goods.Wheat));
        Assert.False(world.KnowsOf(Goods.Barley), "Barley was known before any forager found it.");

        world.BarleyLearned = true;
        Assert.True(world.KnowsOf(Goods.Barley));
    }

    /// <summary>§9.1: a forager's gathering counts toward barley — the real gather, not the door alone.</summary>
    [Fact]
    public void AForagersGatherCountsTowardBarley()
    {
        SimConfig config = Config with { BarleyUnlockForage = 1 };
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;
        for (int tick = 0; tick < config.TicksPerYear && !world.BarleyLearned; tick++)
        {
            loop.StepOnce();
        }

        _output.WriteLine($"{world.FoodEverProducedOf(Goods.Produce)} forage gathered when barley was learned, at {world.Clock}.");
        Assert.True(world.BarleyLearned, "A year of foraging and barley never came, at a knob of one.");
    }

    /// <summary>
    /// §8, §9.1: the latch is in the fingerprint; the forage count beneath it is not — ⚠️ the gap, asserted on purpose
    /// (the workshop's D676 shape). The year's food total IS mixed (D397's ledger), so the instrument is two villages that
    /// produced the same food this year, one all forage and one all fish: their forage counts differ by 4,000 and they
    /// hash alike.
    /// </summary>
    [Fact]
    public void TheBarleyLatchIsInTheFingerprintAndTheCountIsNot()
    {
        SimWorld foraged = Loop(Config).World;
        SimWorld fished = Loop(Config).World;
        foraged.RecordFoodProduced(Goods.Produce, 4_000);
        fished.RecordFoodProduced(Goods.Fish, 4_000);
        Assert.Equal(4_000, foraged.FoodEverProducedOf(Goods.Produce) - fished.FoodEverProducedOf(Goods.Produce));
        Assert.Equal(StateHash.Compute(fished), StateHash.Compute(foraged));

        foraged.BarleyLearned = true;
        Assert.NotEqual(StateHash.Compute(fished), StateHash.Compute(foraged));
    }

    /// <summary>
    /// §10 step 6, call 4 (D823, "wheat's numbers"): the gate, barley's limit, and every neighbour trading barley exactly as
    /// it trades wheat — the lowlands grow 500, the hills and the grazing country want 500 each (D825's correction: the
    /// spec said "the three grow 500"; only downriver grows wheat).
    /// </summary>
    [Fact]
    public void TheShippedNumbersAreAsCalled()
    {
        SimConfig shipped = ShippedConfig.Load();
        Assert.Equal(5000, shipped.BarleyUnlockForage);
        Assert.Equal(2000, shipped.StartingStockLimits["barley"]);
        Assert.Equal(3, shipped.Neighbours.Count);
        foreach (NeighbourRow neighbour in shipped.Neighbours)
        {
            Assert.Equal(Of(neighbour.Makes, Goods.Wheat), Of(neighbour.Makes, Goods.Barley));
            Assert.Equal(Of(neighbour.Wants, Goods.Wheat), Of(neighbour.Wants, Goods.Barley));
        }

        NeighbourRow lowlands = Assert.Single(shipped.Neighbours, n => n.Direction == NeighbourDirection.Downriver);
        Assert.Equal(500, Of(lowlands.Makes, Goods.Barley));
        Assert.Equal(1000, shipped.Neighbours.Sum(n => Of(n.Wants, Goods.Barley)));

        static int Of(IReadOnlyList<MaterialCost> list, Goods good) => list.Where(m => m.Goods == good).Sum(m => m.Amount);
    }

    // ---------------------------------------------------------------

    private static (int Wheat, int Barley) SownBy(SimWorld world, Workplace farm)
    {
        IReadOnlyList<int> owned = world.Zones.WorkGroundOf(farm.Id);
        int wheat = 0;
        int barley = 0;
        for (int i = 0; i < owned.Count; i++)
        {
            GridPos at = world.Zones.PositionOf(owned[i]);
            if (!SimWorld.IsStandingCrop(world.Map.TerrainAt(at)))
            {
                continue;
            }

            if (world.Map.CropAt(at) == Wheat)
            {
                wheat++;
            }
            else if (world.Map.CropAt(at) == Barley)
            {
                barley++;
            }
        }

        return (wheat, barley);
    }

    private static void Ripen(SimWorld world, Workplace farm, int crop)
    {
        IReadOnlyList<int> owned = world.Zones.WorkGroundOf(farm.Id);
        for (int i = 0; i < owned.Count; i++)
        {
            GridPos at = world.Zones.PositionOf(owned[i]);
            world.SetTerrain(at, Terrain.Ripe);
            world.Map.SetCrop(at, (byte)crop);
        }
    }

    private static GridPos AwayFrom(SimWorld world, Workplace first)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 6; radius < 20; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (Math.Abs(at.X - first.Tile.X) + Math.Abs(at.Y - first.Tile.Y) >= 6
                        && !world.HasSomethingToHarvest(at)
                        && world.CanBuildAt(BuildingKind.Farmhouse, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Nowhere for a second farm.");
    }

    /// <summary>
    /// Cut every per-good list in a saved world back by its last good — a save from the data before it was added: the
    /// stockpiles' held and produced, the limit lists, and the packed said-there-is-nowhere bits.
    /// </summary>
    private static int CutBackAGood(JsonNode? node, int goods)
    {
        int cut = 0;
        if (node is JsonObject obj)
        {
            foreach (KeyValuePair<string, JsonNode?> pair in obj.ToList())
            {
                if (pair.Value is JsonArray list && list.Count == goods
                    && pair.Key is "held" or "produced" or "stock_limits" or "limits" or "trade_keep" or "trade_buy")
                {
                    list.RemoveAt(goods - 1);
                    cut++;
                }
                else if (pair.Key == "said_there_is_nowhere_for" && pair.Value is JsonValue packed)
                {
                    byte[] bytes = Convert.FromBase64String(packed.GetValue<string>());
                    obj[pair.Key] = Convert.ToBase64String(bytes.AsSpan(0, (goods - 1 + 7) / 8).ToArray());
                    cut++;
                }
                else
                {
                    cut += CutBackAGood(pair.Value, goods);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? item in array)
            {
                cut += CutBackAGood(item, goods);
            }
        }

        return cut;
    }

    private static int StripKeys(JsonNode? node, params string[] keys)
    {
        int removed = 0;
        if (node is JsonObject obj)
        {
            foreach (string key in keys)
            {
                if (obj.Remove(key))
                {
                    removed++;
                }
            }

            foreach (KeyValuePair<string, JsonNode?> pair in obj.ToList())
            {
                removed += StripKeys(pair.Value, keys);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? item in array)
            {
                removed += StripKeys(item, keys);
            }
        }

        return removed;
    }

    private static SimLoop Reload(SimLoop loop)
    {
        JsonObject document = SaveGame.Capture(loop, "test", "2026-11-21 12:00", "test");
        return SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), loop.World.Config, null);
    }
}
