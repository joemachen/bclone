using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ Farmhands live out at the steading through the working year, tend the field in summer, and
/// come home for winter (`specs/work-from-the-steading.md`, D511, D512).
/// </summary>
/// <remarks>
/// <para>
/// <b>The look only</b> (D355): where a farmhand stops changes, and nothing they produce, learn or
/// eat may. Every guard here is §6 of the spec, and each was red-checked: the mutant and its count
/// are written in the spec.
/// </para>
/// <para>
/// One fixture throughout, D194's: a farm about ten ticks from the granary, three tiles of ground
/// each way, one pinned farmhand. Far enough that the steading and home are different places, which
/// is the only case where any of this says anything.
/// </para>
/// </remarks>
public sealed class SteadingTests
{
    private const int FarmWalk = 10;

    private readonly ITestOutputHelper _output;

    public SteadingTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => ShippedConfig.EstablishedWithNoLimitsSet();

    private static (SimLoop Loop, Workplace Farm, Villager Hand) AFarmTenTicksOut(SimConfig config)
    {
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace farm = FarmTestGround.SiteAFarm(world, FarmWalk, out _);
        FarmFixtures.GiveItGround(world, farm, reach: 3);
        Villager hand = FarmFixtures.PinAFarmhand(world, farm);

        return (loop, farm, hand);
    }

    private static HashSet<GridPos> GroundOf(SimWorld world, Workplace farm)
    {
        var owned = new HashSet<GridPos>();
        IReadOnlyList<int> tiles = world.Zones.WorkGroundOf(farm.Id);
        for (int i = 0; i < tiles.Count; i++)
        {
            owned.Add(world.Zones.PositionOf(tiles[i]));
        }

        return owned;
    }

    private static bool IsTendingState(VillagerState state) =>
        state is VillagerState.WalkingOutToTend or VillagerState.Tending or VillagerState.WalkingBackToTheSteading;

    /// <summary>
    /// ⛔ <c>RestingPoint</c> and <c>RestingPlaceOf</c> answer the same place, for everybody, always
    /// (§2 finding 1).
    /// </summary>
    /// <remarks>
    /// <c>GoHome</c> walks to the Point and checks arrival against the tile. If they ever disagree, a
    /// farmhand walks to one place and is told they have not arrived at the other: D385's flicker,
    /// or a villager who never gets in. ⚠️ The claim only bites where the resting place is not home,
    /// so the run counts those ticks and refuses to pass on zero.
    /// </remarks>
    [Fact]
    public void TheTwoRestingMethodsAlwaysAgree()
    {
        SimConfig config = Config;
        (SimLoop loop, _, _) = AFarmTenTicksOut(config);
        SimWorld world = loop.World;

        int asked = 0;
        int awayFromHome = 0;

        for (int i = 0; i < config.TicksPerYear * 2; i++)
        {
            loop.StepOnce();

            foreach (Villager villager in world.Villagers)
            {
                if (!villager.Alive)
                {
                    continue;
                }

                asked++;
                GridPos stop = world.RestingPlaceOf(villager);
                Assert.True(
                    world.RestingPoint(villager).ToTile() == stop,
                    $"{villager.Name} at tick {world.Tick} ({world.Clock}): RestingPoint is "
                    + $"{world.RestingPoint(villager).ToTile()} but RestingPlaceOf is {stop}.");

                if (stop != world.HomePlaceOf(villager))
                {
                    awayFromHome++;
                }
            }
        }

        _output.WriteLine($"{asked} villager-ticks asked; {awayFromHome} of them resting somewhere other than home");
        Assert.True(awayFromHome > 0, "Nobody ever rested away from home, so the agreement was never tested where it matters.");
    }

    /// <summary>
    /// ⭐ A farmhand rests at the steading in spring, summer and autumn, and at home in winter
    /// (§3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Winter is what keeps it safe: the steading has no hearth, and the cold only counts in
    /// winter (`HearthSystem.IsHeatingSeason`). A farmhand resting at the farm in winter is
    /// standing outdoors in the cold D45 built to kill people.
    /// </para>
    /// <para>
    /// ⚠️ <b>Counted by the spell begun, in the season it was decided in.</b> A farmhand who carried a
    /// load home rests a spell-less tick at their door before walking back out (a load goes home,
    /// never to the steading), and a spell begun on autumn's last tick is still running on winter's
    /// first. Both were measured and both are right; neither is a spell begun in the wrong place.
    /// <c>world.Clock</c> after a step is the NEXT tick's — the decision ran at <c>Tick − 1</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public void AFarmhandRestsAtTheSteadingThroughTheWorkingYearAndAtHomeInWinter()
    {
        SimConfig config = Config;
        (SimLoop loop, Workplace farm, Villager hand) = AFarmTenTicksOut(config);
        SimWorld world = loop.World;

        int workingAtSteading = 0;
        int workingElsewhere = 0;
        int winterAtHome = 0;
        int winterElsewhere = 0;

        for (int i = 0; i < config.TicksPerYear * 3; i++)
        {
            loop.StepOnce();

            // A spell begins with `rest_ticks` on the clock. The founding stagger rests people
            // wherever they stand (`Rhythm`), and so does nobody but them.
            if (!hand.Alive
                || hand.State != VillagerState.Resting
                || hand.ActionTicksRemaining != config.RestTicks
                || hand.Rhythm > 0)
            {
                continue;
            }

            if (SimClock.FromTick(world.Tick - 1, config).IsWinter)
            {
                if (hand.Tile == world.HomePlaceOf(hand))
                {
                    winterAtHome++;
                }
                else
                {
                    winterElsewhere++;
                }
            }
            else if (hand.Tile == farm.Tile)
            {
                workingAtSteading++;
            }
            else
            {
                workingElsewhere++;
            }
        }

        _output.WriteLine(
            $"spring–autumn: {workingAtSteading} rest spells begun at the steading, {workingElsewhere} elsewhere; "
            + $"winter: {winterAtHome} at home, {winterElsewhere} elsewhere");

        Assert.True(workingAtSteading > 0, "The farmhand never rested at the steading in the working year.");
        Assert.Equal(0, workingElsewhere);
        Assert.True(winterAtHome > 0, "The farmhand never rested at home in winter.");
        Assert.Equal(0, winterElsewhere);
    }

    /// <summary>
    /// ⛔ The allocator costs a farmhand's job from where they LIVE (D148, D15).
    /// </summary>
    /// <remarks>
    /// If it read where they stop, a farmhand resting at their farm would cost zero to it, the
    /// allocator would find them unbeatable for that seat, and no reshuffle would ever move them.
    /// </remarks>
    [Fact]
    public void TheAllocatorCostsAFarmhandFromWhereTheyLive()
    {
        SimConfig config = Config;
        (SimLoop loop, Workplace farm, Villager hand) = AFarmTenTicksOut(config);
        SimWorld world = loop.World;

        int checkedAway = 0;

        for (int i = 0; i < config.TicksPerYear * 2; i++)
        {
            loop.StepOnce();
            if (!hand.Alive || world.RestingPlaceOf(hand) == world.HomePlaceOf(hand))
            {
                continue;
            }

            checkedAway++;
            int cost = LabourAllocator.CostBetween(world, hand, farm);
            Assert.Equal(world.TravelCost.Cost(world.HomePlaceOf(hand), farm.Tile), cost);
            Assert.True(cost > 0, $"At tick {world.Tick} the farmhand's cost to their own farm was {cost}.");
        }

        _output.WriteLine($"{checkedAway} ticks with the farmhand resting away from home");
        Assert.True(checkedAway > 0, "The farmhand never rested away from home, so this tested nothing.");
    }

    /// <summary>
    /// ⭐ Tending is summer only, and only on this farm's own sown tiles (§5).
    /// </summary>
    [Fact]
    public void TendingIsSummerOnlyOnTheFarmsOwnSownTiles()
    {
        SimConfig config = Config;
        (SimLoop loop, Workplace farm, Villager hand) = AFarmTenTicksOut(config);
        SimWorld world = loop.World;
        HashSet<GridPos> ground = GroundOf(world, farm);

        int tending = 0;
        int tiles = 0;
        var tended = new HashSet<GridPos>();

        for (int i = 0; i < config.TicksPerYear * 3; i++)
        {
            // ⚠️ THE SEASON THE STEP RAN IN, NOT THE ONE AFTER IT (D523). A step runs every system at
            // `World.Tick` and then advances it, so after the last step of summer the clock already reads
            // autumn — and a tend begun three ticks before the turn, still a tick from done, read as
            // "tending in autumn". It never lined up until the varied diet moved the village's timings.
            Season ran = world.Clock.Season;
            loop.StepOnce();
            if (!hand.Alive || hand.State != VillagerState.Tending)
            {
                Assert.False(
                    IsTendingState(hand.State) && ran != Season.Summer,
                    $"{hand.Name} is {hand.State} in {world.Clock}.");
                continue;
            }

            tending++;
            Assert.Equal(Season.Summer, ran);
            Assert.Contains(hand.Tile, ground);
            Assert.Equal(Terrain.Sown, world.Map.TerrainAt(hand.Tile));
            if (tended.Add(hand.Tile))
            {
                tiles++;
            }
        }

        _output.WriteLine($"{tending} ticks tending over three summers, on {tiles} different tiles of {ground.Count}");
        Assert.True(tending > 0, "Nobody ever tended the field.");
        Assert.True(tiles > 1, $"Every tend was on one tile ({tiles}); the hash is not spreading them.");
    }

    /// <summary>
    /// ⛔ Tending takes nothing, grows nothing and draws nothing: the look only (§5).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two villages from one seed in lockstep, <c>tend_ticks</c> 4 and 0 (off). They are the same
    /// village until the first summer. At the end of that summer:
    /// </para>
    /// <list type="bullet">
    /// <item>the farmhand's skill is the same, so tending is not work (<c>SkillSystem.OutOnTheWork</c>)
    /// and nor is the walk back from it (that is why it is not <c>TravelingHome</c>);</item>
    /// <item>every tile of the field holds the same crop; and</item>
    /// <item>the <c>Rng</c> has been drawn the same number of times: a tile is picked by a hash, never
    /// by a draw that would reshuffle every seed's history (D384).</item>
    /// </list>
    /// </remarks>
    [Fact]
    public void TendingTakesNothingGrowsNothingAndDrawsNothing()
    {
        (SimLoop tendLoop, Workplace tendFarm, Villager tendHand) = AFarmTenTicksOut(Config);
        (SimLoop restLoop, Workplace restFarm, Villager restHand) = AFarmTenTicksOut(Config with { TendTicks = 0 });
        SimWorld tendWorld = tendLoop.World;
        SimWorld restWorld = restLoop.World;

        // To the eve of summer: the next tick to run is its first (the clock reads the tick about to
        // run). The two villages are one village until then.
        while (tendWorld.Clock.Season != Season.Summer)
        {
            tendLoop.StepOnce();
            restLoop.StepOnce();
        }

        Assert.Equal(StateHash.Compute(restWorld), StateHash.Compute(tendWorld));

        int tended = 0;
        while (tendWorld.Clock.Season == Season.Summer)
        {
            tendLoop.StepOnce();
            restLoop.StepOnce();
            if (tendHand.State == VillagerState.Tending)
            {
                tended++;
            }

            Assert.False(IsTendingState(restHand.State), "Somebody tended with tend_ticks 0.");
        }

        _output.WriteLine($"{tended} ticks tended in the first summer");
        Assert.True(tended > 0, "Nobody tended, so this compared two identical villages.");

        Assert.Equal(restWorld.Rng.State, tendWorld.Rng.State);

        foreach (GridPos tile in GroundOf(tendWorld, tendFarm))
        {
            Assert.Equal(restWorld.Map.TerrainAt(tile), tendWorld.Map.TerrainAt(tile));
            Assert.Equal(restWorld.Map.CropAt(tile), tendWorld.Map.CropAt(tile));
        }

        Assert.Equal(restHand.Skills.Count, tendHand.Skills.Count);
        for (int s = 0; s < tendHand.Skills.Count; s++)
        {
            Assert.Equal(restHand.Skills[s].SkillId, tendHand.Skills[s].SkillId);
            Assert.Equal(restHand.Skills[s].Work, tendHand.Skills[s].Work);
            Assert.Equal(restHand.Skills[s].Ticks, tendHand.Skills[s].Ticks);
        }

        Assert.Equal(restFarm.Id, tendFarm.Id);
    }

    /// <summary>
    /// ⛔ A load goes HOME, and the steading is not the family's larder (§4).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Arriving to rest unloads into the household's larder wherever the villager stands
    /// (<c>UnloadAtHome</c>). Two halves, each closing one road to the teleport D30 and D45 closed
    /// once already:
    /// </para>
    /// <list type="bullet">
    /// <item>a farmhand arriving to rest at the farm with something in their arms does not post it to
    /// a cupboard across the valley; and</item>
    /// <item>a farmhand walking "home" with a load — the family's supper from the granary — walks it
    /// to the house, not to the farm.</item>
    /// </list>
    /// </remarks>
    [Fact]
    public void ALoadGoesHomeAndTheSteadingIsNotTheLarder()
    {
        SimConfig config = Config;
        (SimLoop loop, Workplace farm, Villager hand) = AFarmTenTicksOut(config);
        SimWorld world = loop.World;

        FarmFixtures.StepToTheStartOf(loop, Season.Summer);
        for (int i = 0; i < config.TicksPerYear && !(hand.State == VillagerState.Resting && hand.Tile == farm.Tile); i++)
        {
            loop.StepOnce();
        }

        Assert.Equal(farm.Tile, hand.Tile);
        Assert.NotEqual(world.HomePlaceOf(hand), farm.Tile);
        Household household = world.HouseholdOf(hand);

        // Half one: arriving at the steading with an armful.
        hand.Carried.Receive(Goods.Produce, 10);
        int before = world.FoodIn(household.Stockpile);
        BehaviorSystem.ArriveHomeForTest(world, hand);
        _output.WriteLine($"arrived at the steading with 10 produce: larder {before} → {world.FoodIn(household.Stockpile)}, now {hand.State}");
        Assert.Equal(before, world.FoodIn(household.Stockpile));
        Assert.Equal(10, hand.Carried[Goods.Produce]);

        // Half two: told to walk home with it.
        hand.State = VillagerState.TravelingHome;
        hand.ActionTicksRemaining = 0;
        for (int i = 0; i < config.TicksPerYear && hand.State == VillagerState.TravelingHome; i++)
        {
            hand.Hunger = 0;
            loop.StepOnce();
        }

        _output.WriteLine(
            $"walked home with it: stood at {hand.Tile} (home {world.HomePlaceOf(hand)}, farm {farm.Tile}), "
            + $"larder {world.FoodIn(household.Stockpile)}, carrying {hand.Carried[Goods.Produce]}");
        Assert.Equal(world.HomePlaceOf(hand), hand.Tile);
        Assert.Equal(0, hand.Carried[Goods.Produce]);
    }

    /// <summary>
    /// ⛔ Tending never takes a farmhand away from a chore they would do today (§5).
    /// </summary>
    /// <remarks>
    /// Posed: a summer farmhand at the steading whose rest spell is ending, with a load lying on
    /// the ground. They go and fetch it. Tending is offered only where a rest would begin, below
    /// every trade and chore, and that is what makes it the look only.
    /// </remarks>
    [Fact]
    public void TendingNeverTakesAChoreFromAFarmhand()
    {
        SimConfig config = Config;
        (SimLoop loop, Workplace farm, Villager hand) = AFarmTenTicksOut(config);
        SimWorld world = loop.World;

        FarmFixtures.StepToTheStartOf(loop, Season.Summer);
        for (int i = 0; i < config.TicksPerYear && !(hand.State == VillagerState.Resting && hand.Tile == farm.Tile); i++)
        {
            loop.StepOnce();
        }

        Assert.Equal(Season.Summer, world.Clock.Season);
        Assert.Equal(VillagerState.Resting, hand.State);
        Assert.Equal(farm.Tile, hand.Tile);

        GridPos beside = new(farm.Tile.X, farm.Tile.Y + 4);
        world.SetDown(beside, Goods.Logs, 5);
        hand.Hunger = 0;
        hand.ActionTicksRemaining = 1;

        loop.StepOnce();

        _output.WriteLine($"{hand.Name}, with a load on the ground beside the steading: {hand.State}");
        Assert.Equal(VillagerState.TidyingGround, hand.State);
    }
}
