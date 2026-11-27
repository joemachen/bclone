using Kith.Sim.Config;
using Kith.Sim.World;
using Xunit;

namespace Kith.Sim.Tests;

/// <summary>
/// ⭐ W6, health — diet first (`specs/health.md`). Piece 1: food groups, what a person ate this season, the plain seasons that
/// wear a body down, and falling ill — never fatally.
/// </summary>
public sealed class HealthTests
{
    // ---------------------------------------------------------------
    //  §5.1 — food groups, a column on the good (step 1)
    // ---------------------------------------------------------------

    /// <summary>
    /// Every shipped food sits in its group (D357, D935): wheat, barley and bread are grain; the hunter's and the herd's meat is
    /// meat; forage is forage; fish is fish. Nothing is poultry yet. A good nobody eats has no group.
    /// </summary>
    [Fact]
    public void EveryShippedFoodSitsInItsGroup()
    {
        foreach (SimConfig config in new[] { VillageFixtures.Village, ShippedConfig.Load() })
        {
            GoodsCatalog goods = new(config.GoodsCatalog);
            Assert.Equal(FoodGroup.Forage, goods.FoodGroupOf(Goods.Produce));
            Assert.Equal(FoodGroup.Fish, goods.FoodGroupOf(Goods.Fish));
            Assert.Equal(FoodGroup.Meat, goods.FoodGroupOf(Goods.Meat));
            Assert.Equal(FoodGroup.Grain, goods.FoodGroupOf(Goods.Wheat));
            Assert.Equal(FoodGroup.Grain, goods.FoodGroupOf(Goods.Barley));
            Assert.Equal(FoodGroup.Grain, goods.FoodGroupOf(Goods.Bread));
            Assert.DoesNotContain(goods.EdibleGoods, g => goods.FoodGroupOf(g) == FoodGroup.Poultry);
            Assert.All(goods.EdibleGoods, g => Assert.NotEqual(FoodGroup.None, goods.FoodGroupOf(g)));
            Assert.Equal(FoodGroup.None, goods.FoodGroupOf(Goods.Logs));
            Assert.Equal(FoodGroup.None, goods.FoodGroupOf(Goods.Flour));
            Assert.Equal(FoodGroup.None, goods.FoodGroupOf(Goods.Ale));
        }
    }

    /// <summary>Five groups (D935, Joe: <i>"poultry is its own group"</i>) — and nothing else counts as one.</summary>
    [Fact]
    public void ThereAreFiveFoodGroups()
    {
        Assert.Equal(5, FoodGroups.All.Count);
        Assert.Equal(
            new[] { FoodGroup.Grain, FoodGroup.Meat, FoodGroup.Poultry, FoodGroup.Fish, FoodGroup.Forage },
            FoodGroups.All);
    }

    /// <summary>
    /// ⛔ An edible good with no group is refused at load — a modder's new food lands in a group or not at all (the D277
    /// shape) — and a group on a good nobody eats is refused too, because it would be a fact that means nothing.
    /// </summary>
    [Fact]
    public void AFoodWithNoGroupAndAGroupOnNoFoodAreRefused()
    {
        SimConfig config = VillageFixtures.Village;

        var noGroup = config.GoodsCatalog.Select(r => r.Id == (int)Goods.Fish ? r with { FoodGroup = FoodGroup.None } : r).ToList();
        SimConfigException a = Assert.Throws<SimConfigException>(() => (config with { GoodsList = noGroup }).Validate());
        Assert.Contains("'fish'", a.Message, StringComparison.Ordinal);
        Assert.Contains("food_group", a.Message, StringComparison.Ordinal);

        var stray = config.GoodsCatalog.Select(r => r.Id == (int)Goods.Logs ? r with { FoodGroup = FoodGroup.Meat } : r).ToList();
        SimConfigException b = Assert.Throws<SimConfigException>(() => (config with { GoodsList = stray }).Validate());
        Assert.Contains("'logs'", b.Message, StringComparison.Ordinal);
    }
}
