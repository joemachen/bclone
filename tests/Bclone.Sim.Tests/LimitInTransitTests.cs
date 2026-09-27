using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ A stock limit counts a load on its way to storage, and the hide goes into the lodge
/// (D420, `stock-limits-and-laborers.md §4.5`).
/// </summary>
/// <remarks>
/// <para>
/// Joe, 2026-09-27: *"loads in transit should count and the market's shelf should count toward the
/// limit too."* The market already did. A load in someone's arms did not, so a limit overshot by an
/// armful per producer on the road; and a hunter's hide rode in their arms from hunt to hunt, where
/// no limit could see it at all (his call (b): put it in the lodge).
/// </para>
/// <para>
/// ⛔ <b>The truth is counted from the source, the claim from the thing under test</b> (D419's
/// trap): each count here is <c>HeldAgainstItsLimit</c> before and after one posed change, never
/// the function's own parts added back up.
/// </para>
/// </remarks>
public sealed class LimitInTransitTests
{
    private readonly ITestOutputHelper _output;

    public LimitInTransitTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimWorld AVillage() =>
        SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;

    /// <summary>What <c>HeldAgainstItsLimit</c> moves by when one living villager carries a load in one state.</summary>
    private static int CountedWhile(VillagerState state, bool alive = true, Goods goods = Goods.Logs)
    {
        SimWorld world = AVillage();
        Villager carrier = world.Villagers.First(v => v.Alive);
        int before = world.HeldAgainstItsLimit(goods);

        carrier.State = state;
        carrier.Alive = alive;
        carrier.Carried.Receive(goods, 30);

        return world.HeldAgainstItsLimit(goods) - before;
    }

    /// <summary>⭐ A load on its way to a store counts — the producer's haul, a cleared buffer, a tidied heap.</summary>
    [Fact]
    public void ALoadOnItsWayToAStoreCountsAgainstItsLimit()
    {
        int counted = CountedWhile(VillagerState.HaulingToStore);
        _output.WriteLine($"30 logs carried to a store moved the count by {counted}");
        Assert.Equal(30, counted);
    }

    /// <summary>⭐ A catch on its way to the hut's own buffer counts — the buffer does, and the load is the buffer's next.</summary>
    [Fact]
    public void ACatchOnItsWayToTheHutCountsAgainstItsLimit()
    {
        int counted = CountedWhile(VillagerState.HaulingToFarm, goods: Goods.Meat);
        _output.WriteLine($"30 meat carried to the lodge moved the count by {counted}");
        Assert.Equal(30, counted);
    }

    /// <summary>
    /// ⛔ A household's fetch, a marketer's restock and a builder's materials are NOT on their way
    /// to storage — they left it — and do not count (Joe's rule: never a larder).
    /// </summary>
    [Theory]
    [InlineData(VillagerState.FetchingFromStore)]
    [InlineData(VillagerState.TravelingHome)]
    [InlineData(VillagerState.StockingTheMarket)]
    [InlineData(VillagerState.FetchingMaterials)]
    [InlineData(VillagerState.Idle)]
    public void ALoadThatLeftStorageDoesNotCount(VillagerState state)
    {
        int counted = CountedWhile(state, goods: Goods.Produce);
        _output.WriteLine($"30 forage carried while {state} moved the count by {counted}");
        Assert.Equal(0, counted);
    }

    /// <summary>⛔ The dead keep their arms (<c>MortalitySystem</c> clears nothing), and a corpse's load is nobody's haul.</summary>
    [Fact]
    public void TheDeadDoNotCount()
    {
        int counted = CountedWhile(VillagerState.HaulingToStore, alive: false);
        _output.WriteLine($"30 logs in a dead hauler's arms moved the count by {counted}");
        Assert.Equal(0, counted);
    }

    /// <summary>⭐ A hide in the lodge counts against the leather limit — every good's buffer counts now, not food's alone.</summary>
    [Fact]
    public void AHideInTheLodgeCountsAgainstTheLeatherLimit()
    {
        SimWorld world = AVillage();
        Workplace lodge = HuntingTests.RaiseALodgeFor(world);
        int before = world.HeldAgainstItsLimit(Goods.Leather);

        lodge.Store.Add(Goods.Leather, 25);

        int counted = world.HeldAgainstItsLimit(Goods.Leather) - before;
        _output.WriteLine($"25 leather in the lodge moved the count by {counted}");
        Assert.Equal(25, counted);
    }

    /// <summary>
    /// ⭐⭐ The hide goes into the lodge with the meat — <b>a hunter never walks out to the next hunt
    /// carrying the last one's hide</b> (D420, Joe's call (b)).
    /// </summary>
    /// <remarks>
    /// Until D420 the hide stayed in the hunter's arms through <c>Idle</c>, <c>TravelingToGame</c> and
    /// <c>Hunting</c> until the lodge was full or they went home. ⚠️ Scored against the lodge having
    /// taken a catch at all, or it proves nothing (D7).
    /// </remarks>
    [Fact]
    public void TheHideGoesIntoTheLodgeWithTheMeat()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace lodge = HuntingTests.RaiseALodgeFor(world);

        int outWithAHide = 0;
        int hidesInTheLodge = 0;
        for (int tick = 0; tick < config.TicksPerYear * 3 && hidesInTheLodge == 0; tick++)
        {
            loop.StepOnce();
            hidesInTheLodge = lodge.Store[Goods.Leather];
            foreach (Villager villager in world.Villagers)
            {
                if (villager.Alive
                    && villager.WorkplaceId == lodge.Id
                    && villager.State is VillagerState.TravelingToGame or VillagerState.Hunting
                    && villager.Carried[Goods.Leather] > 0)
                {
                    outWithAHide++;
                }
            }
        }

        // And on, a season past the first hide, for the walks out that follow it.
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                if (villager.Alive
                    && villager.WorkplaceId == lodge.Id
                    && villager.State is VillagerState.TravelingToGame or VillagerState.Hunting
                    && villager.Carried[Goods.Leather] > 0)
                {
                    outWithAHide++;
                }
            }
        }

        _output.WriteLine($"{hidesInTheLodge} leather in the lodge; {outWithAHide} hunter-ticks out hunting with a hide in their arms");
        Assert.True(hidesInTheLodge > 0, "no hide ever went into the lodge");
        Assert.Equal(0, outWithAHide);
    }

    /// <summary>
    /// ⭐ A lodge full of hides is cleared by its hunter — <b>every good in a buffer is somebody's
    /// errand</b>, or the hides would fill the room the meat needs (the room is shared).
    /// </summary>
    /// <remarks>
    /// Posed full, because that is when the buffer is the producer's own errand while the village
    /// still wants meat (D370); short of full, hides wait for a marketer with nothing more pressing.
    /// </remarks>
    [Fact]
    public void ALodgeOfHidesIsClearedToAStore()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace lodge = HuntingTests.RaiseALodgeFor(world);

        // The hunter is the only one who may clear it.
        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, kind == JobKind.Hunter ? 1 : 0);
        }

        int posed = lodge.Store.FreeSpace;
        lodge.Store.Add(Goods.Leather, posed);
        Assert.True(world.HutCannotTakeAnotherLoad(lodge));
        int storedBefore = world.InStores(Goods.Leather);

        loop.Step(config.TicksPerSeason);

        int left = lodge.Store[Goods.Leather];
        int stored = world.InStores(Goods.Leather) - storedBefore;
        _output.WriteLine($"{posed} leather posed in the lodge; {left} left there, {stored} more in the stores");
        Assert.True(left < posed, "nobody ever carried a hide out of the lodge");
        Assert.True(stored > 0, "hides left the lodge and never reached a store");
    }
}
