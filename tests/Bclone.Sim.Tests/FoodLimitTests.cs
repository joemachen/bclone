using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The food limit reaches the person who would produce toward it — <b>it did not</b> (D216).
/// </summary>
/// <remarks>
/// <para>
/// Joe, playing: <em>"It seems like labor is taking priority over professions — if there are trees
/// marked for harvest, foragers will gather trees even though the food limit is not yet met [set to
/// 2000]. Assigned professions are first priority, labor work is secondary."</em>
/// </para>
/// <para>
/// <b>⭐ THE PRIORITY WAS NEVER WRONG, AND THAT IS WORTH SAYING.</b> The harvest branch sits below
/// every job in <c>Decide</c> (D87), so a forager who reaches it has already declined their own
/// work this tick. <b>What was wrong is why they declined it:</b> the work gate read the
/// <em>derived</em> target and never the player's limit, so <c>food_limit = 2000</c> and no limit
/// at all produced <b>byte-identical behaviour</b> — 959 forager ticks gathering and 871 clearing
/// in both arms. A control that changes nothing, on the good the whole economy is derived from.
/// </para>
/// <para>
/// <b>D62's *derived floor, player ceiling*, finally wired.</b> The floor half is untouched:
/// <c>TargetFoodForTheGranary</c> is what the <em>birth</em> gate reads and stays derived (D153).
/// The player's number governs work; the derived number governs children.
/// </para>
/// </remarks>
public sealed class FoodLimitTests
{
    private readonly ITestOutputHelper _output;

    public FoodLimitTests(ITestOutputHelper output) => _output = output;

    private sealed record Tally(int Gathering, int Clearing, int Food);

    private static Tally RunAForagingYear(int foodLimit, ITestOutputHelper output)
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        ColdStartTests.PlayTheOpening(world);
        loop.Step(config.TicksPerYear * 10);

        if (foodLimit > 0)
        {
            world.SetStockLimit(Goods.Food, foodLimit);
        }

        // Plenty of painted trees, so laborer work is always available to be chosen wrongly.
        ColdStartTests.PaintTheNearbyTrees(world, 10);

        int gathering = 0;
        int clearing = 0;

        for (int tick = 0; tick < config.TicksPerYear * 3; tick++)
        {
            loop.StepOnce();

            for (int i = 0; i < world.Villagers.Count; i++)
            {
                Villager v = world.Villagers[i];
                if (!v.Alive || KindOf(world, v) != JobKind.Forager)
                {
                    continue;
                }

                if (v.State is VillagerState.Gathering or VillagerState.TravelingToFood)
                {
                    gathering++;
                }
                else if (v.State == VillagerState.Clearing)
                {
                    clearing++;
                }
            }
        }

        output.WriteLine(
            $"limit {foodLimit}: forager ticks gathering {gathering}, clearing {clearing}; "
            + $"village holds {world.FoodTheVillageHolds()}, wants {world.FoodTheVillageHasRoomFor()}");

        return new Tally(gathering, clearing, world.FoodTheVillageHolds());
    }

    /// <summary>⭐⭐ A limit the village has not met keeps its foragers foraging.</summary>
    [Fact]
    public void AFoodLimitKeepsForagersOnFoodRatherThanOnPaintedTrees()
    {
        Tally unset = RunAForagingYear(0, _output);
        Tally asked = RunAForagingYear(2000, _output);

        Assert.True(
            asked.Clearing < unset.Clearing,
            $"A food limit of 2000 left foragers clearing {asked.Clearing} ticks against "
            + $"{unset.Clearing} with no limit at all — the control is not reaching them.");

        Assert.True(
            asked.Food > unset.Food,
            $"A food limit of 2000 brought in {asked.Food} against {unset.Food} unset.");
    }

    /// <summary>
    /// ⛔ The anti-vacuity half, and the licence for no golden moving: unset changes nothing.
    /// </summary>
    /// <remarks>
    /// <c>null</c> is the default and means <em>"the player has not said"</em>, so a village
    /// nobody has given a number to must behave exactly as it did before this was wired — which
    /// is the same argument D212 makes one control over.
    /// </remarks>
    [Fact]
    public void WithNoLimitTheVillageWantsWhatItAlwaysDid()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

        Assert.Null(world.StockLimits.For(Goods.Food));
        Assert.Equal(
            world.TargetFoodForTheGranary(),
            world.FoodTheVillageHasRoomFor());
    }

    /// <summary>
    /// ⭐ And a forager who stops says which of the two reasons it was.
    /// </summary>
    /// <remarks>
    /// <b>The two have opposite answers</b> — <em>raise the limit</em> against <em>build a
    /// granary</em> — and neither was on the screen. A forager who silently walks off to fell a
    /// tree is §1.1's failure whatever the sim is actually doing.
    /// </remarks>
    [Fact]
    public void AVillageThatWantsNoMoreFoodSaysWhichReasonItIs()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        ColdStartTests.PlayTheOpening(world);
        loop.Step(config.TicksPerYear * 10);

        // Nothing wanted at all: the limit is the reason, and it names the limit.
        world.SetStockLimit(Goods.Food, 0);
        string? byLimit = world.WhyTheVillageWantsNoMoreFood();
        _output.WriteLine($"limit 0: {byLimit}");

        Assert.NotNull(byLimit);
        Assert.Contains("asked the village to keep", byLimit);

        // Room to spare and a high limit: the village wants more, so there is nothing to say.
        world.SetStockLimit(Goods.Food, 100000);
        _output.WriteLine($"limit 100000: {world.WhyTheVillageWantsNoMoreFood() ?? "(still wants food)"}");
        Assert.Null(world.WhyTheVillageWantsNoMoreFood());
    }

    private static JobKind? KindOf(SimWorld world, Villager villager)
    {
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.Workplaces[i].Id == villager.WorkplaceId)
            {
                return world.Workplaces[i].Kind;
            }
        }

        return null;
    }
}
