using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The armful and the meal — <c>specs/armful-and-meals.md</c> (D395, D474, D535; Joe's calls D536, D537).
/// </summary>
/// <remarks>
/// <para>
/// Joe: *"armful 40 → 80"* and *"they stop to eat 1x every 2 days — let's change it to 4."* Then, on the
/// spec's calls: a meal of <b>6</b>, <b>one armful for everybody</b>, and the founding race left as it is; then,
/// once building showed that 6 every four days was 5 % more food and starved a village with no market, a meal
/// every <b>17 ticks — 4¼ days</b> (D537): the same rare meals, 2 % less food a year than before.
/// </para>
/// <para>
/// Three numbers in <c>data/sim.config.json</c>, and the village fixture follows them (D223's seam). These
/// guards pin the numbers, pin the fixture to the game, and pose the two behaviours the numbers exist for: a
/// fetch brings a whole armful home, and a villager eats every 4¼ days.
/// </para>
/// </remarks>
public sealed class ArmfulAndMealTests
{
    private readonly ITestOutputHelper _output;

    public ArmfulAndMealTests(ITestOutputHelper output) => _output = output;

    private static SimLoop Build(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    // ---------------------------------------------------------------
    //  The numbers, and the fixture following them
    // ---------------------------------------------------------------

    [Fact]
    public void TheShippedArmfulIsEighty()
    {
        Assert.Equal(80, ShippedConfig.Load().CarryCapacity);
    }

    /// <summary>
    /// A meal every 17 ticks — 4¼ days — of 6, so an adult eats 168 a year against the 172 before (D537).
    /// Asked in ticks AND in days, so a change to <c>ticks_per_day</c> says so.
    /// </summary>
    [Fact]
    public void TheShippedVillagerEatsEveryFourAndAQuarterDaysAndAMealIsSix()
    {
        SimConfig shipped = ShippedConfig.Load();
        int interval = VillageEconomy.MealIntervalTicks(shipped);
        _output.WriteLine(
            $"a meal every {interval} ticks = {(double)interval / shipped.TicksPerDay:0.##} days; "
            + $"{VillageEconomy.MealsPerYear(shipped)} meals and {VillageEconomy.AdultFoodPerYear(shipped)} food a year");

        Assert.Equal(17, interval);
        Assert.Equal(17, (4 * shipped.TicksPerDay) + (shipped.TicksPerDay / 4));
        Assert.Equal(6, shipped.FoodPerMeal);

        // ⭐ What D537 bought: no more food a year than before the change (172), so the forage floor and the
        // market-off village stay where D363 left them.
        Assert.True(VillageEconomy.AdultFoodPerYear(shipped) <= 172,
            $"an adult eats {VillageEconomy.AdultFoodPerYear(shipped)} a year — more than the 172 before D536");
    }

    /// <summary>
    /// ⭐ The VILLAGE fixture eats, hungers and carries as the game does (D223) — or ~60 test files assert
    /// against a village that is not the one Joe plays, which is what D223 found once already.
    /// </summary>
    [Fact]
    public void TheVillageFixtureEatsAndCarriesAsTheGameDoes()
    {
        SimConfig shipped = ShippedConfig.Load();
        SimConfig village = VillageFixtures.Village;

        Assert.Equal(shipped.CarryCapacity, village.CarryCapacity);
        Assert.Equal(shipped.HungerPerTick, village.HungerPerTick);
        Assert.Equal(shipped.FoodPerMeal, village.FoodPerMeal);
        Assert.Equal(shipped.EatThreshold, village.EatThreshold);
        Assert.Equal(shipped.EatReducesHunger, village.EatReducesHunger);
    }

    /// <summary>
    /// At the shipped armful a quarry or mine stint is ended by its own count of digs, never by full arms —
    /// with no tool, a stone tool or an iron tool (`armful-and-meals.md §3`).
    /// </summary>
    /// <remarks>
    /// A dig is the face's yield with the tool's share on it, so at 40 a tool's 25 % made a stone dig 12 and
    /// the arms cut the stint at three digs of its four. ⚠️ This restates <c>OneDig</c>'s rounding (a floor
    /// of the percentage) rather than calling it, so a technique that raises a dig is not seen here.
    /// </remarks>
    [Fact]
    public void AStintIsEndedByItsDigsNotByTheArms()
    {
        SimConfig shipped = ShippedConfig.Load();
        int[] bonuses = { 0, shipped.ToolYieldBonusPercent, shipped.IronToolYieldBonusPercent };

        foreach (int bonus in bonuses)
        {
            int stoneDig = shipped.StonePerDig * (100 + bonus) / 100;
            int ironDig = shipped.IronPerDig * (100 + bonus) / 100;
            _output.WriteLine(
                $"tool +{bonus}%: a quarry stint is {shipped.DigsPerStint} x {stoneDig} = {shipped.DigsPerStint * stoneDig}, "
                + $"a mine stint {shipped.MineDigsPerStint} x {ironDig} = {shipped.MineDigsPerStint * ironDig}, "
                + $"against an armful of {shipped.CarryCapacity}");

            Assert.True(shipped.DigsPerStint * stoneDig <= shipped.CarryCapacity,
                $"with a +{bonus}% tool the quarrier's arms fill before the stint's {shipped.DigsPerStint} digs");
            Assert.True(shipped.MineDigsPerStint * ironDig <= shipped.CarryCapacity,
                $"with a +{bonus}% tool the miner's arms fill before the stint's {shipped.MineDigsPerStint} digs");
        }
    }

    // ---------------------------------------------------------------
    //  ⭐ A fetch brings a whole armful home
    // ---------------------------------------------------------------

    /// <summary>
    /// A household well short, beside a stocked granary, sends somebody who comes home with a whole armful —
    /// 80 — and not D473's 40.
    /// </summary>
    [Fact]
    public void AFetchBringsAWholeArmfulHome()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Build(config);
        SimWorld world = loop.World;
        PoseAnEmptyLarderBesideAFullGranary(world, out Household home);

        int target = world.TargetFoodFor(home);
        Assert.True(target > config.CarryCapacity,
            $"the larder wants {target}, which fits in one armful of {config.CarryCapacity} — the pose could not tell 40 from 80");

        int most = 0;
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            foreach (Villager member in world.Villagers)
            {
                if (member.Alive && member.HouseholdId == home.Id)
                {
                    most = Math.Max(most, world.FoodIn(member.Carried));
                }
            }
        }

        _output.WriteLine($"{home.Name} wants {target}; the fullest armful anybody carried home was {most}");
        Assert.Equal(80, config.CarryCapacity);
        Assert.Equal(config.CarryCapacity, most);
    }

    /// <summary>
    /// ⭐ D473's finding as a guard: the same larder refilled from the same granary takes far fewer trips at 80
    /// than at 40.
    /// </summary>
    [Fact]
    public void AnArmfulOfEightyTakesFewerTripsThanForty()
    {
        int at40 = FetchesInASeason(VillageFixtures.Village with { CarryCapacity = 40 });
        int at80 = FetchesInASeason(VillageFixtures.Village with { CarryCapacity = 80 });

        _output.WriteLine($"food fetches begun in a season with nobody gathering: {at40} at 40, {at80} at 80");
        Assert.True(at40 >= 3, $"only {at40} fetches at 40 — the pose barely fetched");
        Assert.True(at80 * 10 <= at40 * 7, $"{at80} fetches at 80 against {at40} at 40 — not the fewer trips the armful is for");
    }

    // ---------------------------------------------------------------
    //  ⭐ A meal every four days
    // ---------------------------------------------------------------

    /// <summary>
    /// A villager with food at home eats every seventeen ticks — 4¼ days — once their first meal is behind them.
    /// </summary>
    /// <remarks>
    /// Forage only, worth one, so no meal holds hunger off for longer (D522's <c>FullFor</c>). The first gap
    /// is skipped: D529 spreads the founders' first meals over one interval.
    /// </remarks>
    [Fact]
    public void AVillagerEatsEveryFourAndAQuarterDays()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Build(config);
        SimWorld world = loop.World;
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        granary.Store.Receive(Goods.Produce, 2000);
        foreach (Household household in world.Households)
        {
            household.Stockpile.Add(Goods.Produce, 150);
        }

        Villager villager = world.Villagers.First(v => v.Alive && v.CanWork);
        var meals = new List<ulong>();
        for (int tick = 0; tick < config.TicksPerSeason * 2; tick++)
        {
            loop.StepOnce();
            if (villager.JustAte)
            {
                meals.Add(world.Tick);
            }
        }

        var gaps = meals.Zip(meals.Skip(1), (a, b) => (int)(b - a)).Skip(1).ToList();
        _output.WriteLine($"{villager.Name} ate at {string.Join(", ", meals)}; gaps {string.Join(", ", gaps)}");

        Assert.True(gaps.Count >= 5, $"only {gaps.Count} gaps between meals in two seasons");
        Assert.All(gaps, gap => Assert.Equal(VillageEconomy.MealIntervalTicks(config), gap));
        Assert.Equal(17, VillageEconomy.MealIntervalTicks(config));
    }

    // ---------------------------------------------------------------
    //  Poses
    // ---------------------------------------------------------------

    private static void PoseAnEmptyLarderBesideAFullGranary(SimWorld world, out Household home)
    {
        Villager villager = world.Villagers.First(v => v.Alive && v.CanWork);
        home = world.HouseholdOf(villager);
        world.AnyStoreOf(StoreKind.Granary).Store.Receive(Goods.Produce, 2000);
        home.Stockpile.TryTake(Goods.Produce, home.Stockpile[Goods.Produce]);

        // Nobody gathers, so every armful that reaches the larder came from a fetch.
        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, 0);
        }
    }

    private static int FetchesInASeason(SimConfig config)
    {
        SimLoop loop = Build(config);
        SimWorld world = loop.World;
        PoseAnEmptyLarderBesideAFullGranary(world, out _);

        int fetches = 0;
        var fetching = new HashSet<int>();
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            foreach (Villager v in world.Villagers)
            {
                bool now = v.Alive && v.State == VillagerState.FetchingFromStore;
                if (now && fetching.Add(v.Id))
                {
                    fetches++;
                }
                else if (!now)
                {
                    fetching.Remove(v.Id);
                }
            }
        }

        return fetches;
    }
}
