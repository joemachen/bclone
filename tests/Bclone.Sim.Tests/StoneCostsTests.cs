using System.Collections.Generic;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// A store costs stone as well as timber — <b>and a village that has none is not killed by it</b>.
/// </summary>
/// <remarks>
/// <para>
/// Joe, 2026-08-25: <em>"as a basic, stone should be used for construction in addition to logs."</em>
/// `TECH-EXAMPLE.md` has been assuming it since D206 — even the first root cellar is
/// <em>"20 Wood, 10 Cut Stone"</em> — and `buildings-plan.md §4.3` puts stone behind the civic
/// tier.
/// </para>
/// <para>
/// <b>⭐⭐ WHICH BUILDINGS PAY WAS MEASURED, NOT CHOSEN (D213).</b> Fifty years of the shipped
/// opening, three ways:
/// </para>
/// <list type="table">
/// <item><description>stone on the <b>stores</b>, no seam painted — 24 alive, 0 sites unfinished
/// (identical to charging nothing)</description></item>
/// <item><description>stone on the <b>huts</b>, no seam painted — <b>7 alive</b>, 6 sites
/// unfinished</description></item>
/// <item><description>stone on the huts, a seam painted — 24 alive, 0 sites unfinished</description></item>
/// </list>
/// <para>
/// So the stores pay and the survival chain does not. A granary is something <em>the player
/// marks</em>; a gatherer's hut is what the founding eats out of, and a founding that cannot pay
/// for one starves before it learns why. `DESIGN.md §0.1`: the challenge is in the planning,
/// never in the punishment, and a mistake must never be unrecoverable before it was understood.
/// </para>
/// </remarks>
public sealed class StoneCostsTests
{
    private readonly ITestOutputHelper _output;

    public StoneCostsTests(ITestOutputHelper output) => _output = output;

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    /// <summary>⭐ The recipe asks for both, and says so in one sentence.</summary>
    [Fact]
    public void AGranaryCostsTimberAndStone()
    {
        SimConfig config = VillageFixtures.Village;
        BuildingRecipe recipe = BuildingRecipe.For(BuildingKind.Granary, config);
        SimWorld world = Loop(config).World;

        _output.WriteLine($"a granary costs {recipe.Describe(world.GoodsCatalog)}");

        Assert.Equal(config.GranaryLogs, recipe.Of(Goods.Logs));
        Assert.Equal(config.GranaryStone, recipe.Of(Goods.Stone));
        Assert.True(recipe.Of(Goods.Stone) > 0, "A granary is meant to cost stone now.");

        // In good order and with no empty slots, which is what makes iteration deterministic.
        Assert.Equal(2, recipe.Materials.Count);
        Assert.Equal(Goods.Logs, recipe.Materials[0].Goods);
        Assert.Equal(Goods.Stone, recipe.Materials[1].Goods);
    }

    /// <summary>⭐ A hut costs a nominal amount of stone (Joe, D214).</summary>
    /// <remarks>
    /// <b>Nominal against 25 logs, and one seam tile is 12 stone</b> — so clearing a single rock
    /// buys four huts. Measured over fifty years of the shipped opening: the village holds
    /// <b>24 either way</b>, and a founding that never paints a seam simply builds <em>fewer</em>
    /// huts (1 gatherer and 1 woodcutter against 2 and 2). A pressure you can read and recover
    /// from, which is `DESIGN.md §0.1`.
    /// </remarks>
    [Theory]
    [InlineData(BuildingKind.GathererHut)]
    [InlineData(BuildingKind.WoodcutterHut)]
    [InlineData(BuildingKind.ForesterHut)]
    [InlineData(BuildingKind.Farmhouse)]
    public void AHutCostsANominalAmountOfStone(BuildingKind kind)
    {
        BuildingRecipe recipe = BuildingRecipe.For(kind, VillageFixtures.Village);

        Assert.True(recipe.Of(Goods.Stone) > 0, $"A {kind} is meant to cost some stone.");
        Assert.True(
            recipe.Of(Goods.Stone) * 4 <= recipe.Of(Goods.Logs),
            $"A {kind} costs {recipe.Of(Goods.Stone)} stone against {recipe.Of(Goods.Logs)} logs, "
            + "which is not nominal any more.");
    }

    /// <summary>
    /// ⛔ The bootstrap is free and a house is timber-only, and neither is a balance number.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The pile and the builder's hut are the circle</b> (D96, D108): nothing can be built
    /// without them, so charging for them is charging a village for the means of paying.
    /// </para>
    /// <para>
    /// <b>⛔⛔ And a house pays nothing for a reason no measurement changes.</b> It is the one
    /// building the <em>village</em> decides to raise (D42) — the player never places one — so a
    /// stone price there is a growth gate on a resource an unattended valley never gathers, and
    /// `VillageEconomy` derives the timber budget with no stone term at all.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(BuildingKind.Home)]
    [InlineData(BuildingKind.Pile)]
    [InlineData(BuildingKind.BuilderHut)]
    public void TheBootstrapAndTheHousePayNoStone(BuildingKind kind)
    {
        BuildingRecipe recipe = BuildingRecipe.For(kind, VillageFixtures.Village);
        Assert.Equal(0, recipe.Of(Goods.Stone));
    }

    /// <summary>
    /// ⭐⭐ A founding that never paints a seam still fills the valley — it just builds less.
    /// </summary>
    /// <remarks>
    /// <b>The safety property for D214, and it is the number that mattered.</b> An earlier probe
    /// said pricing the huts took the founding from 24 alive to 7 — that probe ran before
    /// <c>SimWorld.NextSiteToServe</c> existed, so what it measured was D135's starved-head stall
    /// rather than the price. With the stall fixed there is no collapse: the village holds its
    /// full population and leaves a couple of huts unbuilt until somebody clears a rock.
    /// </remarks>
    [Fact]
    public void AFoundingThatPaintsNoSeamStillLives()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        ColdStartTests.PlayTheOpening(world);
        loop.Step(config.TicksPerYear * 50);

        int alive = CountAlive(world);
        _output.WriteLine(
            $"no seam ever painted, huts priced at {config.GathererHutStone} stone: "
            + $"{alive} alive after 50 years, {world.TotalFood()} food");

        Assert.True(
            alive >= 15,
            $"Pricing the huts in stone cost the founding its village — {alive} alive.");
    }

    /// <summary>
    /// ⭐⭐ A granary the village cannot pay for waits, and the village goes on living.
    /// </summary>
    /// <remarks>
    /// <b>The whole safety claim, as a test.</b> Not "the granary is built" and not "the village
    /// dies" — the site stands unfinished, the settlement carries on out of its pile, and the
    /// player is told what it is short of.
    /// </remarks>
    [Fact]
    public void AStoreWithNoStoneWaitsRatherThanKillingTheVillage()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        // ⛔ DELIBERATELY NO SEAM PAINTED. A played opening sends laborers to a rock now (D215),
        // so withholding that is the whole premise of this guard: a village that has marked a
        // store it cannot pay for.
        ColdStartTests.PlayTheOpening(world, paintASeam: false);
        loop.Step(config.TicksPerYear * 5);

        GridPos? where = MarkAGranary(world);
        Assert.NotNull(where);

        loop.Step(config.TicksPerYear * 10);

        int alive = CountAlive(world);

        // ⚠️ ASKED OF THE MAP, NOT OF A REFERENCE TAKEN FIFTEEN YEARS AGO. `Complete` retires
        // the site's workplace rather than emptying it, so a `Workplace` held across the run
        // reports a finished building as an unfinished site for ever — which is exactly what
        // this test said the first time it was written.
        ConstructionSite? site = world.SiteAt(where!.Value)?.Construction;

        _output.WriteLine(
            $"no stone anywhere: {alive} alive, granary "
            + (site is null ? "built" : $"still wants {site.DescribeWhatIsMissing(world.GoodsCatalog)}"));

        Assert.Equal(0, world.InStores(Goods.Stone));
        Assert.NotNull(site);
        Assert.True(site!.StillNeeded(Goods.Stone) > 0, "The site is not short of stone.");
        Assert.True(alive > 0, "Charging a granary in stone killed the village.");

        // And the refusal is a sentence, not a silence (METHODOLOGY §4).
        Assert.Contains("stone", site.DescribeWhatIsMissing(world.GoodsCatalog));
    }

    /// <summary>⭐ Paint a seam and the same granary goes up.</summary>
    /// <remarks>
    /// The anti-vacuity half (D7). A guard that watches a granary wait means nothing unless the
    /// same granary is built once the stone arrives — <b>which is also the first time in this
    /// project that a stone seam has paid for anything.</b>
    /// </remarks>
    [Fact]
    public void PaintASeamAndTheGranaryGoesUp()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        ColdStartTests.PlayTheOpening(world);
        int painted = PaintNearestSeams(world, 4);
        Assert.True(painted > 0, "The valley has no reachable stone to paint.");

        loop.Step(config.TicksPerYear * 5);

        GridPos? where = MarkAGranary(world);
        Assert.NotNull(where);

        loop.Step(config.TicksPerYear * 10);

        ConstructionSite? plan = world.SiteAt(where!.Value)?.Construction;
        _output.WriteLine(
            $"seam painted: {world.InStores(Goods.Stone)} stone reached a store; granary "
            + (plan is null
                ? "built"
                : $"unfinished — wants {plan.DescribeWhatIsMissing(world.GoodsCatalog)}"));

        Assert.Null(plan);
    }

    private static int CountAlive(SimWorld world)
    {
        int alive = 0;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            if (world.Villagers[i].Alive)
            {
                alive++;
            }
        }

        return alive;
    }

    /// <summary>Mark exactly one granary near the founding site, and say where.</summary>
    private static GridPos? MarkAGranary(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;

        for (int radius = 1; radius <= 8; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (world.Mark(BuildingKind.Granary, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        return null;
    }

    private static int PaintNearestSeams(SimWorld world, int howMany)
    {
        GridPos site = world.Map.FoundingSite;
        var found = new List<(int Cost, GridPos At)>();

        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (world.Map.TerrainAt(at) != Terrain.Rock)
                {
                    continue;
                }

                int cost = world.TravelCost.Cost(site, at);
                if (cost != TerrainCostField.Unreachable)
                {
                    found.Add((cost, at));
                }
            }
        }

        found.Sort(static (a, b) =>
            a.Cost != b.Cost ? a.Cost.CompareTo(b.Cost)
            : a.At.Y != b.At.Y ? a.At.Y.CompareTo(b.At.Y)
            : a.At.X.CompareTo(b.At.X));

        int painted = 0;
        for (int i = 0; i < found.Count && painted < howMany; i++)
        {
            if (world.PaintHarvest(found[i].At).Allowed)
            {
                painted++;
            }
        }

        return painted;
    }
}
