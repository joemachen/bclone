using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ <b>"Food" means every kind of food</b> — the totals, the mouth, and the dowry.
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe, 2026-09-05, reading his own overview:</b> it showed <b>Food 0</b> while the stock-limits
/// table two rows down showed <b>have 3,043</b>. ⛔ <b>Two panels on one screen disagreeing about
/// the same question</b> — and the overview was the wrong one, because it summed
/// <c>Goods.Food</c> alone.
/// </para>
/// <para>
/// ⚠️ <b>This is D283's family and it keeps coming back.</b> D277 made *"what counts as food?"* one
/// question with one answer; D283 taught the mouth; D285 taught the hands. Each time, the
/// stragglers were the places that read <c>Stockpile.Food</c> and looked perfectly reasonable.
/// </para>
/// </remarks>
public sealed class FoodIsAnUmbrellaTests
{
    private readonly ITestOutputHelper _output;

    public FoodIsAnUmbrellaTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    /// <summary>⭐ The village-wide total counts fish and meat, not just bread.</summary>
    /// <remarks>
    /// <c>SimWorld.TotalFood</c> summed <c>store.Food</c>, so it was blind to two of the three
    /// foods the game has. It feeds the overview panel, <c>ClockSystem</c>'s season summary and
    /// <c>LabourQuota</c>'s ration check — <b>all three were reading a poorer village than the one
    /// on screen.</b>
    /// </remarks>
    [Fact]
    public void TheVillageTotalCountsEveryKindOfFood()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;

        StoreBuilding granary = world.StoreBuildings.First(s => s.Accepts(Goods.Meat));
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TakeAll(Goods.Food);
        }

        int bare = world.TotalFood();
        granary.Store.Add(Goods.Meat, 400);
        granary.Store.Add(Goods.Fish, 100);
        int stocked = world.TotalFood();

        _output.WriteLine($"a granary holding 400 meat and 100 fish: {bare} -> {stocked}");

        Assert.Equal(0, bare);
        Assert.Equal(500, stocked);
    }

    /// <summary>
    /// ⭐⭐ A larder holding <b>only meat</b> still hands over a dowry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ THE DOWRY WAS BROKEN IN BOTH HALVES AND THEY HID EACH OTHER.</b> The share was
    /// <c>Stockpile.Food × dowry_percent</c> — zero for a family living on meat — and then
    /// <c>TryTake(Goods.Food, …)</c> would have failed anyway. **A couple starting out got
    /// nothing**, and the number looked like a rounding artefact rather than a family whose food
    /// the code could not see.
    /// </para>
    /// <para>
    /// ⭐ The fix put <c>MoveFood</c> on <see cref="SimWorld"/>: two callers wanted *"move up to N
    /// of anything edible"* and a private helper in <c>BehaviorSystem</c> could only serve one.
    /// D145 — a rule is safe while it is read at one chokepoint.
    /// </para>
    /// </remarks>
    [Fact]
    public void MeatLeavesTheLarderWhenThereIsNoBread()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;

        Household from = world.Households[0];
        var into = new Stockpile(world.GoodsCatalog.Count);

        from.Stockpile.TakeAll(Goods.Food);
        from.Stockpile.Receive(Goods.Meat, 300);

        int moved = world.MoveFood(from.Stockpile, into, 120);

        _output.WriteLine(
            $"a larder of 300 meat and no food gave up {moved}; "
            + $"{into[Goods.Meat]} meat arrived and {into[Goods.Food]} food");

        Assert.Equal(120, moved);
        Assert.Equal(120, into[Goods.Meat]);
        Assert.Equal(180, from.Stockpile[Goods.Meat]);
    }

    /// <summary>
    /// ⭐⭐ A village that only ever sees one food is <b>byte-identical</b> — which is why no
    /// golden moved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The plan for this change predicted the goldens WOULD move, and they did not.</b> That is
    /// worth a guard rather than a shrug: every fixture village builds no fishery and no lodge, so
    /// <c>FoodIn(store)</c> and <c>store.Food</c> are the same number in all of them, and swapping
    /// one for the other is inert.
    /// </para>
    /// <para>
    /// ⚠️ <b>It is also the claim that says the sweep was safe.</b> If this ever reddens, an
    /// umbrella reader has started to disagree with a single-good reader in a village holding one
    /// food — which would mean the two are no longer the same question.
    /// </para>
    /// </remarks>
    [Fact]
    public void OneFoodMakesTheUmbrellaInvisible()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        loop.Step(config.TicksPerYear * 8);

        int single = 0;
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            single += store.Store[Goods.Food];
        }

        foreach (Household household in world.Households)
        {
            single += household.Stockpile[Goods.Food];
        }

        _output.WriteLine(
            $"eight years in, holding {world.InStores(Goods.Fish)} fish and "
            + $"{world.InStores(Goods.Meat)} meat: single-good {single}, umbrella "
            + $"{world.TotalFood()}, hash {StateHash.Compute(world):X16}");

        Assert.Equal(0, world.InStores(Goods.Fish));
        Assert.Equal(0, world.InStores(Goods.Meat));
        Assert.Equal(single, world.TotalFood());
    }
}
