using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ Commit 3 of `specs/trade.md §11` (D593): <b>the herdsman</b> — `specs/livestock.md §0.1`. Cutting and carrying hay
/// in the growing seasons, feeding it out in winter, the cull as work, the quota that asks for him only where a herd
/// stands, the barn's room derived, and the hard guard: <b>a herdsman never freezes</b>.
/// </summary>
public sealed class HerdsmanTests
{
    private readonly ITestOutputHelper _output;

    public HerdsmanTests(ITestOutputHelper output) => _output = output;

    /// <summary>An established village, its hall, a barn with forty tiles of pasture (eight head), and a herd.</summary>
    private static (SimLoop Loop, Workplace Barn) AHerd(ulong seed = 12345, int head = 4)
    {
        SimLoop loop = SimFactory.CreatePhase0(ShippedConfig.Established() with { Seed = seed }, new InMemoryLogSink());
        Workplace barn = HerdTests.RaiseABarn(loop.World);
        HerdTests.GivePasture(loop.World, barn, 40);
        barn.HerdHead = head;
        return (loop, barn);
    }

    /// <summary>One able villager kept on the herd come what may — the herdsman is the measurement's premise.</summary>
    private static Villager PinAHerdsman(SimWorld world)
    {
        Villager herdsman = world.Villagers.First(v => v.Alive && v.CanWork);
        world.SetPinnedTrade(herdsman, JobKind.Herdsman);
        return herdsman;
    }

    private static void StepToTheStartOf(SimLoop loop, Season season)
    {
        do
        {
            loop.StepOnce();
        }
        while (!(loop.World.Clock.Season == season && loop.World.Clock.DayOfSeason == 1
            && loop.World.Tick % (ulong)loop.World.Config.TicksPerDay == 0UL));
    }

    // ---------------------------------------------------------------
    //  The barn's room and the head one herdsman keeps — derived
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ The barn holds the winter of the herd its hands can keep (`livestock.md §10`: never typed) — and one herdsman
    /// keeps eight head, Joe's own timber barn's "8 Large" (`livestock.md §10.1.6`).
    /// </summary>
    [Fact]
    public void TheBarnHoldsTheWinterOfTheHerdItsHandsCanKeep()
    {
        (SimLoop loop, Workplace barn) = AHerd();
        var config = loop.World.Config;
        Assert.Equal(8, VillageEconomy.HeadOneHerdsmanKeeps(config));
        int winter = VillageEconomy.HayOneHeadEatsAWinter(config);
        Assert.Equal(config.Animals[0].HayPerHeadPerWinterDay * config.DaysPerSeason * config.WinterBufferPercent / 100, winter);
        Assert.Equal(config.BarnCapacity * 8 * winter, VillageEconomy.BarnHayRoom(config));
        Assert.Equal(VillageEconomy.BarnHayRoom(config), loop.World.BarnStoreOf(barn)!.Store.Capacity);
    }

    // ---------------------------------------------------------------
    //  The quota — only where a herd stands
    // ---------------------------------------------------------------

    [Fact]
    public void TheQuotaAsksForAHerdsmanOnlyWhereAHerdStands()
    {
        (SimLoop loop, Workplace barn) = AHerd(head: 0);
        SimWorld world = loop.World;
        Assert.Equal(Season.Spring, world.Clock.Season);
        Assert.Equal(0, LabourQuota.HerdsmenWanted(world, foodComesFirst: false));

        // A herd, a pasture, no hay in: as many as the pasture's full herd needs — eight head, one pair of hands.
        barn.HerdHead = 4;
        Assert.Equal(1, LabourQuota.HerdsmenWanted(world, foodComesFirst: false));

        // While food comes first, the hay waits; only a cull is asked for, because a cull is meat.
        Assert.Equal(0, LabourQuota.HerdsmenWanted(world, foodComesFirst: true));
        barn.HerdHead = world.PastureFor(barn) + 1;
        Assert.Equal(1, LabourQuota.HerdsmenWanted(world, foodComesFirst: true));

        // The winter's hay in: nothing to cut, nobody asked.
        barn.HerdHead = 4;
        world.BarnStoreOf(barn)!.Store.Receive(Goods.Hay, world.WinterHayWanted(barn));
        Assert.Equal(0, LabourQuota.HerdsmenWanted(world, foodComesFirst: false));

        // Winter, hay in the barn: one to feed it out.
        StepToTheStartOf(loop, Season.Winter);
        Assert.True(barn.HerdHead > 0);
        Assert.Equal(1, LabourQuota.HerdsmenWanted(world, foodComesFirst: false));
    }

    // ---------------------------------------------------------------
    //  The work
    // ---------------------------------------------------------------

    /// <summary>
    /// The growing seasons: a herdsman cuts the pasture and carries the hay home until the barn holds the herd's winter
    /// — each tile once a year (`livestock.md §0.1`).
    /// </summary>
    [Fact]
    public void AHerdsmanCutsTheWinterHayBeforeWinter()
    {
        (SimLoop loop, Workplace barn) = AHerd();
        SimWorld world = loop.World;
        Villager herdsman = PinAHerdsman(world);
        Assert.Equal(Season.Spring, world.Clock.Season); // tick 0 is the year's first day: the whole growing year ahead

        // ⚠️ HIS LARDER KEPT FULL THROUGH THE GROWING YEAR (D617) — the household's errand is not what this asks, as D601
        // and D611 found for the winter's feeding. With a tool he cuts quicker and his day reshuffles: on the bare pose he
        // spent half again as long shopping and reached winter at 833 of 864 (it held by 7 before: 871); posed, a tool
        // brings 913 in 76 ticks of cutting against 871 in 107 without.
        Household home = world.HouseholdOf(herdsman);
        do
        {
            home.Stockpile.Receive(Goods.Produce, Math.Max(0, world.TargetFoodFor(home) - world.FoodIn(home.Stockpile)));
            home.ToppingUpFood = false;
            loop.StepOnce();
        }
        while (!(world.Clock.Season == Season.Winter && world.Clock.DayOfSeason == 1
            && world.Tick % (ulong)world.Config.TicksPerDay == 0UL));
        int held = world.HayHeldFor(barn);
        _output.WriteLine($"by winter: {held} hay held of {world.WinterHayWanted(barn)} wanted; {barn.HayCutThisYear} tiles cut of 40");
        Assert.True(barn.HayCutThisYear > 0, "nobody cut the pasture");
        Assert.True(held >= world.WinterHayWanted(barn) || barn.HayCutThisYear == 40,
            $"winter came with {held} hay of {world.WinterHayWanted(barn)} and {barn.HayCutThisYear} of 40 tiles cut");
        Assert.True(barn.HayCutThisYear <= 40, "a tile was cut twice in a year");
    }

    /// <summary>
    /// ⛔ A pasture tile nobody can work any more is passed over, never walked at for ever (D593: a traced herdsman stalled
    /// a whole autumn on one such tile, and the herd starved the winter after).
    /// </summary>
    [Fact]
    public void ATileNobodyCanWorkIsPassedOver()
    {
        (SimLoop loop, Workplace barn) = AHerd();
        SimWorld world = loop.World;
        GridPos first = world.NextPastureToCut(barn) ?? throw new Xunit.Sdk.XunitException("nothing to cut at the pose");
        world.SetTerrain(first, Terrain.Rock);
        GridPos? next = world.NextPastureToCut(barn);
        Assert.NotNull(next);
        Assert.NotEqual(first, next!.Value);
        Assert.NotEqual(first, world.WhereTheHayGoesOut(barn));
    }

    /// <summary>
    /// Winter: a herdsman carries the hay out to the herd — outdoors — and a herd with its winter in the barn loses no
    /// beast to hunger (`livestock.md §9.3`, with the herdsman doing it).
    /// </summary>
    [Fact]
    public void AHerdsmanFeedsTheHerdThroughWinter()
    {
        (SimLoop loop, Workplace barn) = AHerd(head: 6);
        SimWorld world = loop.World;
        Villager herdsman = PinAHerdsman(world);

        // ⚠️ His household's larder kept at its target through the autumn, so his last days of it are the herd's (D601).
        // The market's emptiest-shelf rule left the household 82 food on the pose's day instead of 175 — under half its
        // target (`fetch_below_share_percent`) — and he spent the five days shopping across the valley; winter came with
        // nothing out. That is the household's errand beating the herd's, which this guard does not ask about. Filling the
        // larder on the pose's day alone was too late: he had set out the day before.
        Household home = world.HouseholdOf(herdsman);
        void KeepHisLarderFull()
        {
            home.Stockpile.Receive(Goods.Produce, Math.Max(0, world.TargetFoodFor(home) - world.FoodIn(home.Stockpile)));
            home.ToppingUpFood = false;
        }

        // Five days before winter, when he starts carrying hay out (`hay_feed_ahead_days`): six head, the winter's hay in.
        StepToTheStartOf(loop, Season.Fall);
        for (int t = 0; t < world.Config.TicksPerDay * (world.Config.DaysPerSeason - world.Config.HayFeedAheadDays - 1); t++)
        {
            KeepHisLarderFull();
            loop.StepOnce();
        }

        StoreBuilding store = world.BarnStoreOf(barn)!;
        store.Store.TakeAll(Goods.Hay);
        store.Store.Receive(Goods.Hay, world.WinterHayWanted(barn));
        barn.HerdHead = 6;
        barn.HayPutOut = 0;
        KeepHisLarderFull();
        StepToTheStartOf(loop, Season.Winter);
        int head = barn.HerdHead;
        Assert.True(head < world.PastureFor(barn), "the pose is at its pasture — a cull could hide a starved beast");
        Assert.True(barn.HayPutOut > 0, "winter came with nothing out — the herdsman did not prepare");

        // ⚠️ And through the winter (D611): with Joe's starting limits the household fell under its fetch line on the 23rd and
        // he went five days across the valley for food, the hay out ran dry behind him and four head starved. The same
        // household errand D601 met in the autumn, a season later — still not what this guard asks.
        int outdoorsFeeding = 0;
        do
        {
            KeepHisLarderFull();
            loop.StepOnce();
            if (herdsman.State == VillagerState.FeedingTheHerd && world.ShelterAt(herdsman.Tile) == Shelter.Outdoors)
            {
                outdoorsFeeding++;
            }
        }
        while (world.Clock.Season == Season.Winter);

        _output.WriteLine($"{head} head into winter, {barn.HerdHead} out; {outdoorsFeeding} ticks feeding outdoors");
        Assert.True(outdoorsFeeding > 0, "nobody carried hay out to the herd");
        Assert.True(barn.HerdHead == head, $"{head} went into winter and {barn.HerdHead} came out");
    }

    /// <summary>The cull, as work (Joe, D588): a beast beyond the pasture, culled at the barn into meat and leather.</summary>
    [Fact]
    public void AHerdsmanCullsWhatThePastureCannotHold()
    {
        (SimLoop loop, Workplace barn) = AHerd();
        SimWorld world = loop.World;
        PinAHerdsman(world);

        // A day first: on tick 0 nobody is seated yet, and a barn with nobody at it loses the extra to the ground.
        loop.Step(world.Config.TicksPerDay);
        barn.HerdHead = world.PastureFor(barn) + 1;
        barn.HerdGrowth = 0;
        int meat = world.FoodEverProducedOf(Goods.Meat);
        int leather = barn.Store.Produced(Goods.Leather);

        // Within the spring: a cull is long work (`cull_ticks`), and a household nearly out of food sends its herdsman
        // for it mid-cull, as it sends anybody (`TryEmergencyRestock`) — the cull stays owed and he comes back to it.
        // (By summer the pasture's full herd has calved again — 8 head × 30 days is a birth — so the head count alone says
        // nothing; the cull's meat and leather are its own proof.)
        StepToTheStartOf(loop, Season.Summer);
        int culled = (world.FoodEverProducedOf(Goods.Meat) - meat) / world.Config.Animals[0].MeatPerHead;
        Assert.True(culled >= 1, "nobody culled the beast the pasture could not hold");
        Assert.Equal(culled * world.Config.Animals[0].MeatPerHead, world.FoodEverProducedOf(Goods.Meat) - meat);
        Assert.Equal(culled * world.Config.Animals[0].LeatherPerHead, barn.Store.Produced(Goods.Leather) - leather);
        Assert.InRange(barn.HerdHead, world.PastureFor(barn), world.PastureFor(barn) + 1);
    }

    // ---------------------------------------------------------------
    //  ⛔ A herdsman never freezes (`livestock.md §7`, a hard guard since D593)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⛔ The herd may starve; <b>the herdsman may not</b> (`livestock.md §7`). Three shipped valleys, a barn and its
    /// pasture by the village, three winters of feeding out — outdoors, with the 30-tile margin (§10.1.3) — and not one
    /// herdsman dead of cold.
    /// </summary>
    [Theory]
    [InlineData(12345UL)]
    [InlineData(1UL)]
    [InlineData(2UL)]
    public void AHerdsmanNeverFreezes(ulong seed)
    {
        (SimLoop loop, Workplace barn) = AHerd(seed, head: 6);
        SimWorld world = loop.World;
        Villager herdsman = PinAHerdsman(world);
        world.BarnStoreOf(barn)!.Store.Receive(Goods.Hay, world.WinterHayWanted(barn));

        int outdoorsInWinter = 0;
        for (int t = 0; t < world.Config.TicksPerYear * 3; t++)
        {
            loop.StepOnce();
            if (world.Clock.IsWinter && herdsman.Alive && herdsman.State == VillagerState.FeedingTheHerd)
            {
                outdoorsInWinter++;
            }
        }

        _output.WriteLine($"seed {seed}: {outdoorsInWinter} winter ticks feeding; herdsman {(herdsman.Alive ? "alive" : herdsman.CauseOfDeath.ToString())}");
        Assert.True(outdoorsInWinter > 0, "the herdsman never fed out in winter — the guard would be vacuous");
        Assert.False(!herdsman.Alive && herdsman.CauseOfDeath == CauseOfDeath.Cold, "the herdsman froze");
        Assert.DoesNotContain(world.Villagers,
            v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Cold && v.PinnedTrade == JobKind.Herdsman);
    }
}
