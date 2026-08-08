using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Laborers clear the ground the village painted — D87, and the work D66 could not find.
/// </summary>
/// <remarks>
/// <para>
/// Joe's rule, and it is one line's worth of position in <c>Decide</c>: <em>"the harvest brush
/// is only for the jobless / laborers. Users with a specific job can harvest, but ONLY when
/// they are idle from their full-time job. They are just helping out on the side. Their
/// primary job comes first."</em>
/// </para>
/// <para>
/// <b>So the branch sits below every job and above resting.</b> Anybody reaching it has
/// already declined their own work this tick. That is the sentence in code, and it needs no
/// quota, no new job kind and no rule about who is allowed — which is what makes it the
/// answer rather than the workaround.
/// </para>
/// </remarks>
public sealed class LaborerHarvestTests
{
    private readonly ITestOutputHelper _output;

    public LaborerHarvestTests(ITestOutputHelper output) => _output = output;

    private static SimLoop Loop(SimConfig config) =>
        ManagedVillage.Loop(config, new InMemoryLogSink());

    /// <summary>Paint every forest tile within a short walk of the village.</summary>
    private static int PaintForestNear(SimWorld world, int radius)
    {
        GridPos site = world.Map.FoundingSite;
        int painted = 0;

        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (world.PaintHarvest(new GridPos(site.X + dx, site.Y + dy)).Allowed)
                {
                    painted++;
                }
            }
        }

        return painted;
    }

    // ---------------------------------------------------------------
    //  The work happens
    // ---------------------------------------------------------------

    /// <summary>⭐ Paint trees and the village fells them, and the forest recedes.</summary>
    /// <remarks>
    /// <b>§2.3's only real machinery.</b> Deforestation becomes visible on the map — the
    /// clearest possible case of <em>"every escalating problem should be back-traceable to
    /// something the player did"</em>. You can see the bald patch you made.
    /// </remarks>
    [Fact]
    public void PaintedTreesGetFelledAndTheForestRecedes()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        int painted = PaintForestNear(world, 8);
        Assert.True(painted > 0, "The valley must have forest near the village to paint.");

        int forestBefore = CountForest(world);
        loop.Step(config.TicksPerYear);

        int forestAfter = CountForest(world);
        _output.WriteLine(
            $"painted {painted} tiles; forest {forestBefore} -> {forestAfter}, "
            + $"{world.Zones.HarvestTiles} still to do");

        Assert.True(
            forestAfter < forestBefore,
            "A year passed with trees painted for harvest and not one was felled.");
    }

    /// <summary>And the timber reaches a store, so it is the village's to spend.</summary>
    [Fact]
    public void TheTimberEndsUpSomewhereTheVillageCanSpendIt()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        // Empty the stores first, so what arrives can only have come from the brush.
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TryTake(Goods.Logs, store.Store.Logs);
        }

        PaintForestNear(world, 8);
        int before = world.LogsInSheds();

        loop.Step(config.TicksPerYear);

        _output.WriteLine($"logs in reach: {before} -> {world.LogsInSheds()}");
        Assert.True(world.LogsInSheds() > before, "Cleared timber never reached a store.");
    }

    /// <summary>Nothing painted, nothing cleared — the brush is the only way to fell.</summary>
    /// <remarks>
    /// The anti-vacuity half (D7). A guard that watches the forest shrink means nothing
    /// unless a village left alone leaves it standing.
    /// </remarks>
    [Fact]
    public void UnpaintedForestIsLeftAlone()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);

        int before = CountForest(loop.World);
        loop.Step(config.TicksPerYear);

        Assert.Equal(before, CountForest(loop.World));
    }

    // ---------------------------------------------------------------
    //  Whose work it is
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ A job comes first: nobody abandons their trade to go and clear.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe's constraint, and the one that could quietly break.</b> If clearing outranked
    /// work, painting a forest would empty the berry patches — which is D77's stampede in a
    /// new costume, and it would show up as a village that starves beside a woodpile.
    /// </para>
    /// <para>
    /// Measured rather than asserted structurally: with trees painted all year, the village
    /// must still be doing its ordinary work and still be alive.
    /// </para>
    /// </remarks>
    [Fact]
    public void ClearingNeverOutranksSomebodysTrade()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        PaintForestNear(world, 10);

        long gathering = 0;
        long clearing = 0;
        long ticks = config.TicksPerYear * 3L;

        for (long t = 0; t < ticks; t++)
        {
            loop.StepOnce();

            foreach (Villager villager in world.Villagers)
            {
                if (!villager.Alive)
                {
                    continue;
                }

                if (villager.State == VillagerState.Gathering)
                {
                    gathering++;
                }
                else if (villager.State == VillagerState.Clearing)
                {
                    clearing++;
                }
            }
        }

        _output.WriteLine(
            $"over three years: {gathering} gathering ticks, {clearing} clearing ticks, "
            + $"{world.Population} alive");

        // The village goes on feeding itself with a forest painted over its head.
        Assert.True(gathering > 0, "Painting trees stopped the village foraging entirely.");
        Assert.True(world.Population > 0, "The village died with trees painted.");
    }

    /// <summary>Children do not fell trees. It is work, not carrying.</summary>
    /// <remarks>
    /// The line D77 drew when it let children fetch: carrying an armful home is not a job,
    /// and swinging an axe is.
    /// </remarks>
    [Fact]
    public void ChildrenDoNotClearGround()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        PaintForestNear(world, 10);

        bool sawAChild = false;
        for (long t = 0; t < config.TicksPerYear * 20L; t++)
        {
            loop.StepOnce();

            foreach (Villager villager in world.Villagers)
            {
                if (!villager.Alive || villager.LifeStage != LifeStage.Child)
                {
                    continue;
                }

                sawAChild = true;
                Assert.NotEqual(VillagerState.Clearing, villager.State);
            }
        }

        Assert.True(sawAChild, "No child was ever born, so this proves nothing.");
    }

    // ---------------------------------------------------------------
    //  The edges
    // ---------------------------------------------------------------

    /// <summary>Painting a whole forest and letting it finish leaves nobody stuck.</summary>
    /// <remarks>
    /// The failure worth guarding: a villager who walks to a tile somebody else already
    /// felled, and stands there. Runs long enough that the painted ground is exhausted and
    /// the village has to go back to ordinary life.
    /// </remarks>
    [Fact]
    public void WhenThePaintedGroundRunsOutEverybodyGoesBackToWork()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        PaintForestNear(world, 6);
        loop.Step(config.TicksPerYear * 10);

        _output.WriteLine(
            $"{world.Zones.HarvestTiles} tiles still painted, {world.Population} alive");

        Assert.Equal(0, world.Zones.HarvestTiles);
        Assert.DoesNotContain(
            world.Villagers,
            villager => villager.Alive && villager.State == VillagerState.Clearing);
        Assert.True(world.Population > 0);
    }

    /// <summary>A cleared valley is still a village — clearing does not kill it.</summary>
    [Fact]
    public void AVillageThatClearsItsValleyStillLives()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        PaintForestNear(world, 12);
        loop.Step(config.TicksPerYear * 30);

        _output.WriteLine(
            $"thirty years on: {world.Population} alive, {CountForest(world)} forest left");

        Assert.True(world.Population > 0);
    }

    private static int CountForest(SimWorld world)
    {
        int count = 0;
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            if (world.Map.Tiles[i] == Terrain.Forest)
            {
                count++;
            }
        }

        return count;
    }
}
