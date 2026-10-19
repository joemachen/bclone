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
    /// ⭐ The barn keeps painted ground (its card's brush), priced by the herd one herdsman's hay feeds — eight head at
    /// five tiles, forty (D597; D596 priced it as a farm's thirteen).
    /// </summary>
    [Fact]
    public void TheBarnKeepsGroundPricedByTheHeadItHolds()
    {
        SimWorld world = AVillage().World;
        Assert.True(SimWorld.KeepsWorkGround(JobKind.Herdsman));
        Assert.Equal(
            VillageEconomy.HeadOneHerdsmanKeeps(world.Config) * world.Config.Animals[0].PastureTilesPerHead,
            world.TilesOneWorkerKeeps(JobKind.Herdsman));
        Assert.Equal(40, world.TilesOneWorkerKeeps(JobKind.Herdsman));
    }

    /// <summary>
    /// ⭐ An overstretched pasture says so in head and in the herdsman's trade (D597 — Joe's barn read <i>"put another
    /// forester on"</i>).
    /// </summary>
    [Fact]
    public void AnOverstretchedPastureIsSaidInHead()
    {
        SimWorld world = AVillage().World;
        Workplace barn = HerdTests.RaiseABarn(world);
        HerdTests.GivePasture(world, barn, 60);
        Assert.Equal(
            "Barn 1's pasture has room for 12 head and no herdsman, so no hay will be cut for them. Put a herdsman on, or paint less.",
            world.OverstretchedNote(barn));
        Assert.DoesNotContain("forester", world.OverstretchedNote(barn), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  A firewood limit the village has outgrown (D600)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ <b>A firewood limit below what the winter needs is named</b> — on the limit's sentence and on a forge it keeps
    /// cold — and never while the village has enough (D600: Joe's 600 against 1,631, the smithy idle all year).
    /// </summary>
    [Fact]
    public void AFirewoodLimitTheVillageOutgrewIsNamed()
    {
        SimWorld world = AVillage().World;
        world.SetStockLimit(Goods.Firewood, null);
        Assert.Null(world.TheWinterOutgrewTheLimit());

        // A limit met far below what the homes will burn: the woodcutters stop short of the winter.
        world.SetStockLimit(Goods.Firewood, 0);
        int shortfall = LabourQuota.FirewoodShortfall(world);
        Assert.True(shortfall > 0, "The pose's premise: the village is short of its winter's firewood.");
        string said = world.TheWinterOutgrewTheLimit() ?? throw new Xunit.Sdk.XunitException("Nothing named the limit.");
        Assert.Contains("the limit of 0 stops the woodcutters", said, StringComparison.Ordinal);
        Assert.EndsWith(said, world.WhyTheLimitIsMet(Goods.Firewood), StringComparison.Ordinal);

        Workplace smithy = SmithyForging(world, Goods.IronTools);
        Assert.StartsWith("Nothing to forge — the firewood limit is too low.", world.WhyTheForgeIsCold(smithy), StringComparison.Ordinal);

        // Enough firewood for the winter: nothing to name, whatever the limit.
        world.SetStockLimit(Goods.Firewood, null);
        Assert.DoesNotContain("firewood limit", world.WhyTheForgeIsCold(smithy) ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>A standing smithy set to forge <paramref name="tool"/> — the suite's own (`ToolsTests.RaiseASmithy`).</summary>
    private static Workplace SmithyForging(SimWorld world, Goods tool)
    {
        Workplace smithy = ToolsTests.RaiseASmithy(world);
        smithy.ForgeGood = tool;
        return smithy;
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

    /// <summary>
    /// ⭐ The region panel's words (commit 7, `trade.md §4.2`): nothing while the region sleeps; woken, a paragraph a
    /// neighbour — which way it lies, plenty of what it makes and short of what it wants — and with the village's orders
    /// set, the rate for each in words with its reason; the overland neighbour waits for carts.
    /// </summary>
    [Fact]
    public void TheRegionSaysEachNeighbourAndItsRates()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Assert.Empty(world.SayTheRegion());
        Assert.StartsWith("The region wakes", Assert.Single(world.SayTheRegionInBrief()), StringComparison.Ordinal);

        TradeTests.RaiseAPost(world);
        for (int t = 0; t <= world.Config.TicksPerSeason && world.RegionAwakeSince == 0UL; t++)
        {
            loop.StepOnce();
        }

        Assert.NotEqual(0UL, world.RegionAwakeSince);
        IReadOnlyList<(string Name, string Words)> region = world.SayTheRegion();
        Assert.Equal(world.Config.Neighbours.Count, region.Count);

        int up = Enumerable.Range(0, world.Config.Neighbours.Count).First(n => world.Config.Neighbours[n].Direction == NeighbourDirection.Upriver);
        NeighbourRow row = world.Config.Neighbours[up];
        (string name, string words) = region[up];
        Assert.Equal(world.NeighbourName(up), name);
        Assert.StartsWith("Up the river.", words, StringComparison.Ordinal);
        Assert.Contains($"Has plenty of {world.GoodsCatalog.NameOf(row.Makes[0].Goods)}", words, StringComparison.Ordinal);
        Assert.Contains("Short of", words, StringComparison.Ordinal);
        Assert.EndsWith("its rates are said here.", words, StringComparison.Ordinal);

        // And in brief, a line a neighbour, for the top of the post's window (D607).
        IReadOnlyList<string> brief = world.SayTheRegionInBrief();
        Assert.Equal(world.Config.Neighbours.Count, brief.Count);
        Assert.StartsWith($"{name}, up the river — plenty of {world.GoodsCatalog.NameOf(row.Makes[0].Goods)}", brief[up], StringComparison.Ordinal);
        Assert.Contains("short of", brief[up], StringComparison.Ordinal);

        Goods sell = row.Wants[0].Goods, buy = row.Makes[0].Goods;
        Assert.True(world.SetTradeKeep(sell, 0).Allowed);
        Assert.True(world.SetTradeBuy(buy, 100).Allowed);
        Assert.Contains(world.SayTheRate(up, sell, buy)!, world.SayTheRegion()[up].Words, StringComparison.Ordinal);

        int overland = Enumerable.Range(0, world.Config.Neighbours.Count).FirstOrDefault(n => world.Config.Neighbours[n].Direction == NeighbourDirection.Overland, -1);
        if (overland >= 0)
        {
            Assert.EndsWith("its trade waits for carts.", world.SayTheRegion()[overland].Words, StringComparison.Ordinal);
        }
    }
}
