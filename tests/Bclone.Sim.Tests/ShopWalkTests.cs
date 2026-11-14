using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ The shop walk said, never hidden — logistics' last piece (D668 outcome 2, D670; `phase-5-exit-gate.md §4`, D744): the
/// home card names the shop the household actually uses and how far it is, and the housing brush warns by the walk to the
/// nearest SHOP, not the nearest forage (D385: every load goes to a store, never home).
/// </summary>
public sealed class ShopWalkTests
{
    private readonly ITestOutputHelper _output;

    public ShopWalkTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Shipped => ShippedConfig.Established();

    private static SimWorld AVillageAFewYearsOn()
    {
        SimLoop loop = SimFactory.CreatePhase0(Shipped, new InMemoryLogSink());
        loop.Step(Shipped.TicksPerYear * 3);
        return loop.World;
    }

    [Fact]
    public void TheHomeCardNamesTheShopTheHouseholdWouldWalkTo()
    {
        SimWorld world = AVillageAFewYearsOn();
        int said = 0;
        foreach (Household home in world.Households)
        {
            if (home.HomeTile is not GridPos at || world.LivingMembersOf(home) == 0)
            {
                continue;
            }

            (StoreBuilding Shop, int Tiles)? walk = world.ShopOf(home);
            Assert.True(walk is not null, $"the {home.Name} household has no shop at all in a founded village");

            // ⛔ The same rule the shopper walks by, asked the same question — never a second rule that could disagree.
            int trip = BehaviorSystem.ATripsWorth(world.Config, world.TargetFoodFor(home), home.HandcartUses > 0 ? world.Config.CartArmfuls : 1);
            StoreBuilding? shopper = BehaviorSystem.FoodShopFrom(world, at, System.Math.Max(1, trip));
            if (shopper is not null)
            {
                Assert.Same(shopper, walk!.Value.Shop);
            }

            string sentence = world.SayTheShopWalk(home)!;
            _output.WriteLine($"{home.Name}: {sentence}");
            Assert.Contains(walk!.Value.Shop.Name, sentence, System.StringComparison.Ordinal);
            Assert.Contains($"{walk.Value.Tiles} tiles away", sentence, System.StringComparison.Ordinal);
            said++;
        }

        Assert.True(said > 0, "no household with a home to ask (D7)");
    }

    [Theory]
    [InlineData(6, 8, false)]
    [InlineData(8, 8, false)]
    [InlineData(9, 8, true)]
    public void AWalkPastTheVillagesBudgetIsCalledLong(int tiles, int budget, bool longWalk) =>
        Assert.Equal(longWalk, SimWorld.ShopWalkSentence("Market 1", tiles, budget).Contains("a long walk", System.StringComparison.Ordinal));

    [Fact]
    public void TheHousingBrushWarnsByTheWalkToTheNearestShop()
    {
        SimWorld world = AVillageAFewYearsOn();
        Assert.NotEmpty(world.StoreBuildings);
        int budget = VillageEconomy.MaxHomeToWorkTiles(world.Config);
        GridPos from = world.StoreBuildings[0].Tile;

        // Out along a row from a shop until the walk passes the budget: warned there, in the shop's words.
        PlacementVerdict? warned = null;
        GridPos? where = null;
        for (int d = 1; d < 60 && warned is null; d++)
        {
            var tile = new GridPos(from.X + d, from.Y);
            PlacementVerdict verdict = world.CanPaintResidential(tile);
            if (verdict.Allowed && verdict.HasWarning)
            {
                warned = verdict;
                where = tile;
            }
        }

        Assert.True(warned is not null, "nowhere along the row was far enough from a shop to warn (D7)");
        _output.WriteLine(warned!.Value.Warning);
        Assert.Contains("tiles from the nearest shop", warned.Value.Warning, System.StringComparison.Ordinal);
        Assert.Contains($"budgets {budget}", warned.Value.Warning, System.StringComparison.Ordinal);
        Assert.True(world.NearestShopWalk(where!.Value) > budget, "warned inside the budget");

        // And a tile right beside the shop is fine.
        Assert.False(world.CanPaintResidential(new GridPos(from.X + 1, from.Y)).HasWarning, "warned on the shop's doorstep");
    }

    [Fact]
    public void WithNoShopYetTheBrushStillWarnsByTheForage()
    {
        SimWorld world = SimFactory.CreatePhase0(Shipped, new InMemoryLogSink()).World;
        world.StoreBuildings.Clear();
        int budget = VillageEconomy.MaxHomeToWorkTiles(world.Config);
        GridPos village = world.Map.FoundingSite;
        PlacementVerdict? warned = null;
        for (int d = budget; d < budget + 40 && warned is null; d++)
        {
            PlacementVerdict verdict = world.CanPaintResidential(new GridPos(village.X + d, village.Y));
            if (verdict.Allowed && verdict.HasWarning)
            {
                warned = verdict;
            }
        }

        Assert.True(warned is not null, "no warning at all with no shop (D7)");
        Assert.Contains("tiles from the nearest food", warned!.Value.Warning, System.StringComparison.Ordinal);
    }
}
