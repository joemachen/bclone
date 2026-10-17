using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ Commit 6 of `specs/trade.md §11` (D596): the words the barn's card and the post's window read — the sim's, so the
/// view restates no rule — and the post's orders as player inputs, with their refusals. Plus the barn's paint.
/// </summary>
public sealed class TradeWordsTests
{
    private static SimLoop AVillage() => SimFactory.CreatePhase0(ShippedConfig.Established(), new InMemoryLogSink());

    // ---------------------------------------------------------------
    //  The barn's card
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ The barn says what it lacks, in order: no pasture, then no beasts (and where they come from), then the herd with
    /// its hay counted in winter days and why it is short.
    /// </summary>
    [Fact]
    public void TheBarnSaysWhatItLacksAndWhy()
    {
        SimWorld world = AVillage().World;
        Workplace barn = HerdTests.RaiseABarn(world);
        Assert.StartsWith("No pasture", world.SayTheHerd(barn), StringComparison.Ordinal);

        HerdTests.GivePasture(world, barn, 40);
        Assert.Equal(
            "No cattle yet — 40 tiles of pasture for 8; cattle come by trade, from a trading post on the river.",
            world.SayTheHerd(barn));
        TradeTests.RaiseAPost(world);
        Assert.Contains("set the trading post to buy some", world.SayTheHerd(barn), StringComparison.Ordinal);

        barn.HerdHead = 4;
        world.BarnStoreOf(barn)!.Store.Receive(Goods.Hay, 240);
        Assert.Equal(30, world.HayDaysFor(barn));            // 240 hay ÷ (4 head × 2 a day)
        Assert.Equal(
            "4 head grazing 40 tiles (room for 8) — hay for 30 winter days of 30; nobody is cutting hay — a barn needs a herdsman.",
            world.SayTheHerd(barn));
    }

    /// <summary>A barn with the winter's hay in says so, and in winter it counts the days against the days left.</summary>
    [Fact]
    public void TheBarnCountsItsHayAgainstTheWinter()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace barn = HerdTests.RaiseABarn(world);
        HerdTests.GivePasture(world, barn, 40);
        barn.HerdHead = 4;
        world.BarnStoreOf(barn)!.Store.Receive(Goods.Hay, world.WinterHayWanted(barn));
        Assert.EndsWith("the winter's hay is in.", world.SayTheHerd(barn), StringComparison.Ordinal);

        while (!world.Clock.IsWinter)
        {
            loop.StepOnce();
        }

        Assert.Contains("in for the winter — hay for", world.SayTheHerd(barn), StringComparison.Ordinal);
        Assert.Contains("of winter left", world.SayTheHerd(barn), StringComparison.Ordinal);
    }

    /// <summary>
    /// ⭐ The barn keeps painted ground (its card's brush), and its hands are priced as a farm's — the hay cut is a reap.
    /// </summary>
    [Fact]
    public void TheBarnKeepsGroundPricedAsAFarms()
    {
        SimWorld world = AVillage().World;
        Assert.True(SimWorld.KeepsWorkGround(JobKind.Herdsman));
        Assert.Equal(world.TilesOneWorkerKeeps(JobKind.Farmer), world.TilesOneWorkerKeeps(JobKind.Herdsman));
    }

    // ---------------------------------------------------------------
    //  The post's orders and what they will do
    // ---------------------------------------------------------------

    /// <summary>
    /// ⛔ <b>A good is bought or sold, not both</b> — each order refuses the other, with a reason, and clearing one lets
    /// the other through.
    /// </summary>
    [Fact]
    public void AGoodIsBoughtOrSoldNotBoth()
    {
        SimWorld world = AVillage().World;
        Assert.True(world.SetTradeKeep(Goods.Stone, 100).Allowed);
        PlacementVerdict buy = world.SetTradeBuy(Goods.Stone, 50);
        Assert.False(buy.Allowed);
        Assert.Contains("clear what it keeps first", buy.Reason, StringComparison.Ordinal);
        Assert.Null(world.TradeBuy.For(Goods.Stone));

        Assert.True(world.SetTradeKeep(Goods.Stone, null).Allowed);
        Assert.True(world.SetTradeBuy(Goods.Stone, 50).Allowed);
        PlacementVerdict keep = world.SetTradeKeep(Goods.Stone, 10);
        Assert.False(keep.Allowed);
        Assert.Contains("set to buy stone", keep.Reason, StringComparison.Ordinal);
        Assert.Null(world.TradeKeep.For(Goods.Stone));
    }

    /// <summary>A buy order no boat can fill is set, with a warning that says why (the overland caravan waits for carts).</summary>
    [Fact]
    public void AnOrderNoBoatCanFillIsWarned()
    {
        SimWorld world = AVillage().World;
        Assert.False(world.ABoatCanBring(Goods.Hay));
        PlacementVerdict hay = world.SetTradeBuy(Goods.Hay, 100);
        Assert.True(hay.Allowed);
        Assert.Contains("no boat will bring it", hay.Warning, StringComparison.Ordinal);
        Assert.False(world.SetTradeBuy(Goods.Stone, 100).HasWarning);
    }

    /// <summary>
    /// ⭐ The post says what its orders will do (Joe, D596) — above all that a keep order alone sells nothing, and to set
    /// what to buy.
    /// </summary>
    [Fact]
    public void ThePostSaysWhatItsOrdersWillDo()
    {
        SimWorld world = AVillage().World;
        Assert.StartsWith("Set what the village keeps and what it buys", world.SayWhatThePostWillDo(), StringComparison.Ordinal);
        world.SetTradeKeep(Goods.Firewood, 400);
        Assert.StartsWith("Nothing will be sold", world.SayWhatThePostWillDo(), StringComparison.Ordinal);
        Assert.Contains("Set what to buy", world.SayWhatThePostWillDo(), StringComparison.Ordinal);
        world.SetTradeBuy(Goods.Stone, 50);
        Assert.StartsWith("The merchant carries", world.SayWhatThePostWillDo(), StringComparison.Ordinal);
        world.SetTradeKeep(Goods.Firewood, null);
        Assert.StartsWith("Nothing is for sale to pay with", world.SayWhatThePostWillDo(), StringComparison.Ordinal);
    }
}
