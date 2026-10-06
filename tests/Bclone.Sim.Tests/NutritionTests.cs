using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ What a food is worth (`specs/food-chain.md §3`, D520–D522): the floor is solved at nutrition 1, a
/// better food holds hunger off for longer (<see cref="Villager.FullFor"/>), and the best food in a larder
/// is eaten first.
/// </summary>
public sealed class NutritionTests
{
    private readonly ITestOutputHelper _output;

    public NutritionTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The one villager of a Phase 0 world, about to eat: their arms and larder hold exactly what is
    /// given, and hunger is at the threshold, so the next tick is a meal.
    /// </summary>
    private static (SimLoop Loop, Villager Villager, Stockpile Larder) AboutToEat(
        SimConfig config, params (Goods Good, int Amount)[] larder)
    {
        (SimLoop loop, _) = Phase0Fixtures.Build(config);
        SimWorld world = loop.World;
        Villager villager = world.Villager;
        Stockpile home = world.HouseholdOf(villager).Stockpile;
        foreach (Goods good in world.GoodsCatalog.EdibleGoods)
        {
            home.TakeAll(good);
            villager.Carried.TakeAll(good);
        }

        foreach ((Goods good, int amount) in larder)
        {
            home.Add(good, amount);
        }

        villager.Hunger = config.EatThreshold;

        // ⚠️ A founder starts with a seeded rhythm (D190) and rests through it rather than eating; the pose
        // is about the meal, so it is spent.
        villager.Rhythm = 0;
        return (loop, villager, home);
    }

    private static int Worth(SimConfig config, Goods good) => config.GoodsCatalog[(int)good].Nutrition;

    /// <summary>
    /// ⭐ A meal of bread holds hunger off for the extra it is worth (§3.2): a meal costs what it always
    /// did, and its worth beyond a plain meal becomes ticks with no hunger rise. Hunger never goes below
    /// nought — the invariant in <c>NeedsSystem</c> stands.
    /// </summary>
    [Fact]
    public void AMealOfBreadHoldsHungerOff()
    {
        SimConfig config = Phase0Fixtures.Plenty;
        (SimLoop loop, Villager villager, Stockpile larder) = AboutToEat(config, (Goods.Bread, 100));
        int cost = config.FoodPerMeal;
        int interval = VillageEconomy.MealIntervalTicks(config);
        int expected = ((cost * Worth(config, Goods.Bread)) - cost) * interval / cost;

        loop.StepOnce();

        _output.WriteLine($"ate {100 - larder[Goods.Bread]} bread; full for {villager.FullFor} (expected {expected}); hunger {villager.Hunger}");
        Assert.Equal(cost, 100 - larder[Goods.Bread]);
        Assert.True(expected > 0, "The premise: bread is worth more than a plain meal.");
        Assert.Equal(expected, villager.FullFor);

        // Held off: no hunger rises while full, and none is lost below nought.
        int after = villager.Hunger;
        for (int t = 0; t < expected; t++)
        {
            loop.StepOnce();
            Assert.Equal(after, villager.Hunger);
            Assert.True(villager.Hunger >= 0);
        }

        Assert.Equal(0, villager.FullFor);
        loop.StepOnce();
        Assert.Equal(after + config.HungerPerTick, villager.Hunger);
    }

    /// <summary>
    /// A meal is worth its parts: two loaves and two handfuls of forage hold hunger off for what the two
    /// loaves add, and no more.
    /// </summary>
    [Fact]
    public void AMixedMealIsWorthItsParts()
    {
        SimConfig config = Phase0Fixtures.Plenty;
        int cost = config.FoodPerMeal;
        int loaves = cost / 2;
        (SimLoop loop, Villager villager, _) = AboutToEat(config, (Goods.Bread, loaves), (Goods.Produce, 100));
        int interval = VillageEconomy.MealIntervalTicks(config);
        int worth = (loaves * Worth(config, Goods.Bread)) + (cost - loaves);

        loop.StepOnce();

        int expected = (worth - cost) * interval / cost;
        _output.WriteLine($"a meal of {loaves} loaves and {cost - loaves} forage, worth {worth}: full for {villager.FullFor}");
        Assert.Equal(expected, villager.FullFor);
    }

    /// <summary>
    /// ⭐ The best food in the larder is eaten first (Joe's §9 call 4) — and, with every food worth the
    /// same, that is id order, so nothing that ate before bread existed eats differently.
    /// </summary>
    [Fact]
    public void TheBestFoodIsEatenFirst()
    {
        SimConfig config = Phase0Fixtures.Plenty;
        (SimLoop loop, _, Stockpile larder) = AboutToEat(config, (Goods.Produce, 100), (Goods.Bread, 100));

        loop.StepOnce();

        Assert.Equal(100, larder[Goods.Produce]);
        Assert.Equal(100 - config.FoodPerMeal, larder[Goods.Bread]);

        IReadOnlyList<Goods> order = loop.World.GoodsCatalog.EdibleGoods;
        Assert.Equal(Goods.Bread, order[0]);
        var plain = order.Where(g => Worth(config, g) == 1).ToList();
        Assert.Equal(plain.OrderBy(g => (int)g).ToList(), plain);
    }

    /// <summary>
    /// ⛔ With no bread, nobody is ever full — the rule lands byte-identical (§3.2). Red with a meal that
    /// always adds a tick of fullness.
    /// </summary>
    [Fact]
    public void WithNoBreadNobodyIsEverFull()
    {
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;
        int meals = 0;
        for (int t = 0; t < world.Config.TicksPerYear; t++)
        {
            loop.StepOnce();
            meals += world.Villagers.Count(v => v.Alive && v.JustAte);
            Assert.All(world.Villagers, v => Assert.Equal(0, v.FullFor));
        }

        _output.WriteLine($"{meals} meals in a year; nobody full after any of them");
        Assert.True(meals > 0, "The premise: people ate.");
    }

    /// <summary>
    /// ⭐ The floor reads no nutrition (§3.1): bread at 2 or at 9, every number the survival floor is
    /// derived from is the same.
    /// </summary>
    [Fact]
    public void TheFloorReadsNoNutrition()
    {
        SimConfig two = VillageFixtures.Village with { BreadNutrition = 2 };
        SimConfig nine = VillageFixtures.Village with { BreadNutrition = 9 };
        Assert.Equal(2, two.GoodsCatalog[(int)Goods.Bread].Nutrition);
        Assert.Equal(9, nine.GoodsCatalog[(int)Goods.Bread].Nutrition);

        Assert.Equal(VillageEconomy.AdultFoodPerYear(two), VillageEconomy.AdultFoodPerYear(nine));
        Assert.Equal(VillageEconomy.ChildFoodPerYear(two), VillageEconomy.ChildFoodPerYear(nine));
        Assert.Equal(VillageEconomy.MouthsFedByOneAdult(two), VillageEconomy.MouthsFedByOneAdult(nine));
        Assert.Equal(VillageEconomy.FoodFarmedPerYearAtWorst(two), VillageEconomy.FoodFarmedPerYearAtWorst(nine));
        Assert.Equal(VillageEconomy.FoodGatheredPerYearAtWorst(two), VillageEconomy.FoodGatheredPerYearAtWorst(nine));
    }

    /// <summary>
    /// ⛔ A full belly survives a save (D522): the field guard only checks the table names it, and the
    /// fixture villages the save tests play never eat bread, so without this pose a dropped
    /// <c>full_for</c> would load as nought and nothing would notice.
    /// </summary>
    [Fact]
    public void AFullBellyIsSaved()
    {
        SimConfig config = Phase0Fixtures.Plenty;
        (SimLoop loop, Villager villager, _) = AboutToEat(config, (Goods.Bread, 100));
        loop.StepOnce();
        Assert.True(villager.FullFor > 0, "The premise: a meal of bread filled them.");

        System.Text.Json.Nodes.JsonObject document =
            Bclone.Sim.Persistence.SaveGame.Capture(loop, "test", "2026-10-06 12:00", "test");
        SimLoop loaded = Bclone.Sim.Persistence.SaveGame.Load(
            System.Text.Json.Nodes.JsonNode.Parse(Bclone.Sim.Persistence.SaveFile.TextOf(document))!.AsObject(),
            config);

        Villager again = loaded.World.Villager;
        Assert.Equal(villager.FullFor, again.FullFor);
        Assert.Equal(villager.FullFrom, again.FullFrom);
        Assert.Equal(Bclone.Sim.Determinism.StateHash.Compute(loop.World), Bclone.Sim.Determinism.StateHash.Compute(loaded.World));
    }

    /// <summary>The card's sentence (Joe's §9 call 5): what filled them, and for how many days.</summary>
    [Fact]
    public void TheCardSaysWhatFilledThemAndForHowLong()
    {
        SimConfig config = Phase0Fixtures.Plenty;
        (SimLoop loop, Villager villager, _) = AboutToEat(config, (Goods.Bread, 100));
        SimWorld world = loop.World;
        Assert.Null(world.FullNote(villager));

        loop.StepOnce();

        string? note = world.FullNote(villager);
        _output.WriteLine(note);
        Assert.NotNull(note);
        Assert.Matches(@"^Full from a meal of bread — not hungry for another \d+ days?\.$", note);
    }
}
