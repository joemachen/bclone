using System.Collections.Generic;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ The brush says what it will do before it does it (D198).
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe, playing:</b> *"when I'm painting I don't see an outline of the area I'm about to
/// paint. I just have to point and click and hope it's covering the right area."* A building has
/// had a ghost under the cursor since D43; **the brush — which lays down twelve tiles at a
/// time — had nothing.**
/// </para>
/// <para>
/// <b>⛔ THE REASON IT HAD NOTHING IS THE THING THESE GUARD.</b> Every paint method mixed the
/// test with the doing, so the only way to ask *"would this tile take?"* was to paint it. The
/// test is now its own method and the paint calls it — <b>one condition, two callers</b>, which
/// is D142's three call sites and D148's two meanings stated as a rule. <i>A preview computed
/// from a second copy of the condition is a preview that can lie.</i>
/// </para>
/// </remarks>
public sealed class BrushPreviewTests
{
    private readonly ITestOutputHelper _output;

    public BrushPreviewTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => ShippedConfig.Established();

    private static SimWorld World() =>
        SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;

    /// <summary>
    /// Every tile in the valley — <b>the whole map, and both reasons are findings</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>IT USED TO BE A 33-TILE BOX AROUND THE FOUNDING SITE, AND THE ANTI-VACUITY GUARD
    /// CAUGHT IT.</b> There is **no iron within sixteen tiles of the founding site** on the
    /// shipped seed, so the `Iron` arm compared 1,089 refusals against 1,089 refusals and called
    /// it agreement. *A sweep that only ever sees one answer proves nothing* (D7), and the whole
    /// map is the only region guaranteed to contain every material the brush can filter for.
    /// </para>
    /// <para>
    /// ⚠️ <b>AND IT USED TO BUILD A FRESH WORLD PER TILE, WHICH COST 28 SECONDS AN ARM.</b> One
    /// world is enough and is a truer test anyway: the preview and the paint are asked of **the
    /// same world in the same state**, which is exactly the claim. D179's rule — *measure the
    /// tooling too* — and the same nine thousand tiles now run in well under a second.
    /// </para>
    /// </remarks>
    private static IEnumerable<GridPos> TheWholeValley(SimWorld world)
    {
        for (int y = 0; y < world.Map.Height; y++)
        {
            for (int x = 0; x < world.Map.Width; x++)
            {
                yield return new GridPos(x, y);
            }
        }
    }

    // ---------------------------------------------------------------
    //  The preview and the paint are one answer
    // ---------------------------------------------------------------

    /// <summary>⭐⭐ What the residential preview promises is what the brush does.</summary>
    [Fact]
    public void TheResidentialPreviewAndTheBrushAgree()
    {
        int allowed = 0;
        int refused = 0;

        SimWorld world = World();

        foreach (GridPos tile in TheWholeValley(world))
        {
            PlacementVerdict preview = world.CanPaintResidential(tile);
            PlacementVerdict done = world.PaintResidential(tile);

            Assert.Equal(preview.Allowed, done.Allowed);
            Assert.Equal(preview.Reason, done.Reason);
            Assert.Equal(preview.Warning, done.Warning);

            if (preview.Allowed)
            {
                allowed++;
            }
            else
            {
                refused++;
            }
        }

        _output.WriteLine($"{allowed} tiles would take the paint, {refused} would refuse it");
        BothOutcomesWereSeen(allowed, refused, "residential");
    }

    /// <summary>
    /// ⭐⭐ And the harvest brush, which is the one where it matters most.
    /// </summary>
    /// <remarks>
    /// <b>The mode is a filter (D90)</b>, so half the tiles under a stroke routinely refuse it.
    /// *"The brush is set to fell trees, and that is a stone seam"* is a sentence the player
    /// should see coming rather than discover by clicking.
    /// </remarks>
    [Theory]
    [InlineData(HarvestBrush.Everything)]
    [InlineData(HarvestBrush.Trees)]
    [InlineData(HarvestBrush.Stone)]
    [InlineData(HarvestBrush.Iron)]
    public void TheHarvestPreviewAndTheBrushAgree(HarvestBrush mode)
    {
        int allowed = 0;
        int refused = 0;

        SimWorld world = World();

        foreach (GridPos tile in TheWholeValley(world))
        {
            PlacementVerdict preview = world.CanPaintHarvest(tile, mode);
            PlacementVerdict done = world.PaintHarvest(tile, mode);

            Assert.Equal(preview.Allowed, done.Allowed);
            Assert.Equal(preview.Reason, done.Reason);

            if (preview.Allowed)
            {
                allowed++;
            }
            else
            {
                refused++;
            }
        }

        _output.WriteLine($"{mode}: {allowed} tiles would take it, {refused} would refuse");
        BothOutcomesWereSeen(allowed, refused, mode.ToString());
    }

    /// <summary>⭐⭐ And work ground, including a tile another building already owns.</summary>
    [Fact]
    public void TheWorkGroundPreviewAndTheBrushAgree()
    {
        int allowed = 0;
        int refused = 0;

        SimWorld world = World();
        Workplace mine = world.Workplaces[0];

        foreach (GridPos tile in TheWholeValley(world))
        {
            PlacementVerdict preview = world.CanPaintWorkGround(mine, tile);
            PlacementVerdict done = world.PaintWorkGround(mine, tile);

            Assert.Equal(preview.Allowed, done.Allowed);
            Assert.Equal(preview.Reason, done.Reason);

            if (preview.Allowed)
            {
                allowed++;
            }
            else
            {
                refused++;
            }
        }

        _output.WriteLine($"work ground: {allowed} would take it, {refused} would refuse");
        BothOutcomesWereSeen(allowed, refused, "work ground");
    }

    /// <summary>
    /// ⛔ Ground another building owns is refused, and the preview says so first.
    /// </summary>
    /// <remarks>
    /// <b>The case the sweep above cannot reach on a fresh world</b>, because nothing owns any
    /// ground until somebody paints some — so it is posed. Without it the work-ground guard is
    /// green over a world where the interesting refusal is impossible, which is D157's blind
    /// fixture.
    /// </remarks>
    [Fact]
    public void GroundThatBelongsToSomebodyElseIsRefusedInTheGhostToo()
    {
        SimWorld world = World();
        Assert.True(world.Workplaces.Count > 1, "Need two workplaces to pose this.");

        Workplace theirs = world.Workplaces[0];
        Workplace mine = world.Workplaces[1];

        GridPos tile = FirstTileThatTakesGround(world, theirs);
        Assert.True(world.PaintWorkGround(theirs, tile).Allowed);

        PlacementVerdict preview = world.CanPaintWorkGround(mine, tile);
        PlacementVerdict done = world.PaintWorkGround(mine, tile);

        _output.WriteLine($"{mine.Name} over {theirs.Name}'s ground: \"{preview.Reason}\"");

        Assert.False(preview.Allowed);
        Assert.Equal(preview.Allowed, done.Allowed);
        Assert.Equal(preview.Reason, done.Reason);
        Assert.Contains(theirs.Name, preview.Reason, System.StringComparison.Ordinal);
    }

    /// <summary>⭐ Asking does not paint — the whole point of a preview.</summary>
    /// <remarks>
    /// <b>The guard that would catch the obvious mistake</b>: a predicate that quietly does the
    /// thing it is asked about would make the ghost paint the map as the cursor crossed it, which
    /// is worse than having no ghost at all.
    /// </remarks>
    [Fact]
    public void AskingTheGhostChangesNothing()
    {
        SimWorld world = World();
        Workplace mine = world.Workplaces[0];

        int residentialBefore = world.Zones.ResidentialTiles;
        int harvestBefore = world.Zones.HarvestTiles;
        int groundBefore = world.Zones.WorkGroundTiles(mine.Id);

        foreach (GridPos tile in TheWholeValley(world))
        {
            world.CanPaintResidential(tile);
            world.CanPaintHarvest(tile, HarvestBrush.Everything);
            world.CanPaintWorkGround(mine, tile);
        }

        Assert.Equal(residentialBefore, world.Zones.ResidentialTiles);
        Assert.Equal(harvestBefore, world.Zones.HarvestTiles);
        Assert.Equal(groundBefore, world.Zones.WorkGroundTiles(mine.Id));
        _output.WriteLine($"asked about {world.Map.Tiles.Count} tiles three ways; nothing was painted");
    }

    // ---------------------------------------------------------------

    private static GridPos FirstTileThatTakesGround(SimWorld world, Workplace workplace)
    {
        foreach (GridPos tile in TheWholeValley(world))
        {
            if (world.CanPaintWorkGround(workplace, tile).Allowed)
            {
                return tile;
            }
        }

        throw new Xunit.Sdk.XunitException("No tile near the village would take work ground.");
    }

    /// <summary>
    /// ⭐ Anti-vacuity (D7): a sweep that only ever saw one answer proves nothing.
    /// </summary>
    private static void BothOutcomesWereSeen(int allowed, int refused, string what)
    {
        Assert.True(
            allowed > 0,
            $"Not one tile near the village would take the {what} brush, so this guard compared "
            + "two refusals and called it agreement.");

        Assert.True(
            refused > 0,
            $"Every tile near the village would take the {what} brush, so the refusal path — "
            + "which is the whole reason a preview is worth drawing — was never exercised.");
    }
}
