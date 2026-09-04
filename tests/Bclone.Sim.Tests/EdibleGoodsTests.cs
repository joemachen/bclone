using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ Food is a <b>capability</b> now, not the name of one good.
/// </summary>
/// <remarks>
/// <para>
/// <b>`goods-catalog.md §2.2` predicted this day and named the shape of it.</b> That ruling kept
/// fish, meat, wheat, cheese and apples as one good — *"varieties are flavour and unlock, not new
/// goods"* — and wrote its own expiry: *"when that lands, **every reader of 'how much food has the
/// village got' has to ask a capability question instead of naming a good** — D76's seam, on the
/// one axis the whole economy is derived from."* Joe asked for fish and meat as real goods on
/// 2026-09-02.
/// </para>
/// <para>
/// ⛔ <b>The slice ships with food as the only edible good</b>, so the village behaves exactly as
/// it did and no golden moves — D82's pattern for Stone, *"do the indexed-goods refactor when the
/// first new good lands, not before and not after."* **These guards are what stop that being
/// indistinguishable from having done nothing.**
/// </para>
/// </remarks>
public sealed class EdibleGoodsTests
{
    private readonly ITestOutputHelper _output;

    public EdibleGoodsTests(ITestOutputHelper output) => _output = output;

    /// <summary>A second edible good is counted as food by the whole village.</summary>
    /// <remarks>
    /// <b>⛔ THE RED CHECK FOR THE ENTIRE SLICE.</b> Every conversion in it is a no-op while one
    /// good is edible, so *"the suite is green"* proves only that nothing broke — it cannot tell
    /// a finished refactor from an untouched one. This poses the second good and watches the
    /// totals move.
    /// </remarks>
    [Fact]
    public void ASecondEdibleGoodCountsAsFood()
    {
        SimConfig config = VillageFixtures.Village;
        var rows = new List<GoodRow>(config.GoodsCatalog);
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Id == (int)Goods.Logs)
            {
                // Logs are stored by the granary nowhere, so this also proves the count is not
                // quietly restricted to one kind of building.
                rows[i] = rows[i] with
                {
                    Nutrition = config.GoodsCatalog[(int)Goods.Food].Nutrition,
                    StoredBy = new[] { StoreKind.Granary, StoreKind.Shed, StoreKind.Cart, StoreKind.Pile },
                };
            }
        }

        SimConfig edibleLogs = config with { GoodsCatalog = rows };
        SimWorld world = SimFactory.CreatePhase0(edibleLogs, new InMemoryLogSink()).World;

        // Food, fish and meat ship edible; the posed logs make four.
        Assert.Equal(4, world.GoodsCatalog.EdibleGoods.Count);

        int before = world.FoodTheVillageHolds();
        StoreBuilding granary = world.StoreBuildings.First(s => s.Kind == StoreKind.Granary);
        granary.Store.Receive(Goods.Logs, 100);
        int after = world.FoodTheVillageHolds();

        _output.WriteLine($"the village held {before} food; 100 edible logs later it holds {after}");

        Assert.Equal(before + 100, after);
    }

    /// <summary>⛔ Two edible goods worth different amounts is refused at load.</summary>
    /// <remarks>
    /// <b>THE GUARD THAT KEEPS THE SURVIVAL FLOOR HONEST.</b> `VillageEconomy` solves the floor
    /// against `food_per_meal` — one number for one food — and `food-catalog.md` states the
    /// consequence: a catalogue of nutritional values means the floor must be solved against
    /// *"the worst food a village might be living on, or the derivation has to change shape."*
    /// `RequiredGatherYield` and `MouthsFedByOneAdult` have no valid form until somebody answers
    /// that, so **a second edible good is allowed and a second VALUE is not.**
    /// </remarks>
    [Fact]
    public void TwoFoodsWorthDifferentAmountsIsRefusedAtLoad()
    {
        SimConfig config = VillageFixtures.Village;
        var rows = new List<GoodRow>(config.GoodsCatalog);
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Id == (int)Goods.Logs)
            {
                rows[i] = rows[i] with { Nutrition = config.GoodsCatalog[(int)Goods.Food].Nutrition + 1 };
            }
        }

        SimConfigException blew =
            Assert.Throws<SimConfigException>(() => (config with { GoodsCatalog = rows }).Validate());

        _output.WriteLine(blew.Message);
        Assert.Contains("worth the same", blew.Message, System.StringComparison.Ordinal);
    }

    /// <summary>Exactly the foods the game ships are edible — no more, no fewer.</summary>
    /// <remarks>
    /// The anti-vacuity half (D7): the two guards above would both pass on a catalogue where
    /// everything was edible, which is not the game. ⚠️ **It is a whitelist rather than a count**,
    /// so a good that becomes edible by accident fails here by name.
    /// </remarks>
    [Fact]
    public void OnlyFoodIsEdibleToday()
    {
        SimWorld world = SimFactory.CreatePhase0(
            ShippedConfig.Load(), new InMemoryLogSink()).World;

        _output.WriteLine(string.Join(", ", world.GoodsCatalog.EdibleGoods));

        Assert.Equal(
            new[] { Goods.Food, Goods.Fish, Goods.Meat }, world.GoodsCatalog.EdibleGoods);
    }
}
