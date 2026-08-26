using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ A planted tree takes as long as a seeded one (D220).
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe, playing:</b> *"it feels like the trees are planted by the forester and ready to fell
/// very quickly, but maybe I am misperceiving."* He was not.
/// </para>
/// <para>
/// <b>The asymmetry, and why nobody had checked it:</b> `RegrowthSystem` carried the sentence
/// *"a sapling seen by a sweep is a sapling that has stood for one period, because the sweep
/// visits every tile exactly once per period."* True of a sapling **the sweep seeded itself** —
/// it will not be seen again for a full period — and **never checked for the other path.** A
/// forester plants at an arbitrary tick, so the next visit might be the very next tick.
/// </para>
/// <para>
/// Seeded ground took <b>1–2 periods</b> to become wood; planted ground took <b>0–1</b>. Three
/// times faster on average, and near-instant at worst. *A long-standing comment is a hypothesis
/// nobody has tested.*
/// </para>
/// </remarks>
public sealed class PlantedSaplingTests
{
    private readonly ITestOutputHelper _output;

    public PlantedSaplingTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static int PeriodTicks(SimConfig config) =>
        config.RegrowthPeriodDays * config.TicksPerDay;

    /// <summary>A patch of bare ground the sweep has not seeded, well away from the village.</summary>
    private static GridPos BareGround(SimWorld world)
    {
        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var tile = new GridPos(x, y);
                if (world.Map.TerrainAt(tile) == Terrain.Grass)
                {
                    return tile;
                }
            }
        }

        throw new System.InvalidOperationException("No bare ground in this valley.");
    }

    // -----------------------------------------------------------------

    /// <summary>
    /// ⭐ The guard the whole slice exists for: one period is not enough.
    /// </summary>
    /// <remarks>
    /// <b>This is the one that reddens</b> if the young-sapling bit is removed — without it the
    /// sweep matures a planted sapling on its first visit, which lands somewhere inside the first
    /// period rather than after it.
    /// </remarks>
    [Fact]
    public void APlantedSaplingIsStillASaplingAfterOnePeriod()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink(), 42UL);
        SimWorld world = loop.World;

        GridPos tile = BareGround(world);
        Assert.True(world.Plant(tile), "the tile should have been plantable");
        Assert.Equal(Terrain.Sapling, world.Map.TerrainAt(tile));
        Assert.True(world.Map.IsYoungSapling(tile), "a planted sapling starts young");

        // One full period: the sweep has now passed over every tile exactly once, so it has
        // SEEN this sapling — and must have left it standing.
        loop.Step(PeriodTicks(config));

        _output.WriteLine(
            $"after one period ({PeriodTicks(config)} ticks): {world.Map.TerrainAt(tile)}, "
            + $"young {world.Map.IsYoungSapling(tile)}");

        Assert.Equal(Terrain.Sapling, world.Map.TerrainAt(tile));
        Assert.False(world.Map.IsYoungSapling(tile), "the sweep should have aged it, not matured it");
    }

    /// <summary>⭐ And the anti-vacuity half — it does become wood, on the period after.</summary>
    /// <remarks>
    /// A guard that only proves a tree never grows would pass against a broken regrowth sweep, so
    /// the pair is the claim: <b>not one period, and yes by two.</b>
    /// </remarks>
    [Fact]
    public void APlantedSaplingIsWoodAfterTwo()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink(), 42UL);
        SimWorld world = loop.World;

        GridPos tile = BareGround(world);
        Assert.True(world.Plant(tile));

        loop.Step(PeriodTicks(config) * 2);

        _output.WriteLine($"after two periods: {world.Map.TerrainAt(tile)}");
        Assert.Equal(Terrain.Forest, world.Map.TerrainAt(tile));
    }

    /// <summary>
    /// ⚠️ The bit does not outlive the sapling it describes.
    /// </summary>
    /// <remarks>
    /// Cleared through `SetTerrain`, the one door terrain changes by (D85) — so a planted sapling
    /// that is felled, built on or overwritten cannot leave the mark behind for whatever occupies
    /// the tile next. <b>A stale bit would make the NEXT sapling on that tile take two periods</b>,
    /// which is the kind of fault that shows up as "sometimes trees are slow" and never as an error.
    /// </remarks>
    [Fact]
    public void FellingAPlantedSaplingLeavesNoMarkBehind()
    {
        SimConfig config = Config;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink(), 42UL).World;

        GridPos tile = BareGround(world);
        Assert.True(world.Plant(tile));
        Assert.True(world.Map.IsYoungSapling(tile));

        world.SetTerrain(tile, Terrain.Grass);

        Assert.False(world.Map.IsYoungSapling(tile), "the mark should go with the sapling");
    }

    /// <summary>
    /// ⭐⭐ The state is hashed, because it decides when a tile becomes wood.
    /// </summary>
    /// <remarks>
    /// Sim state the hash cannot see is <b>two runs that read identical and are not</b> — the trap
    /// `MixStore` records and the one this project treats as P0. Two worlds identical but for one
    /// planted sapling must not agree.
    /// </remarks>
    [Fact]
    public void APlantedSaplingChangesTheStateHash()
    {
        SimConfig config = Config;
        SimWorld a = SimFactory.CreatePhase0(config, new InMemoryLogSink(), 42UL).World;
        SimWorld b = SimFactory.CreatePhase0(config, new InMemoryLogSink(), 42UL).World;

        Assert.Equal(StateHash.Compute(a), StateHash.Compute(b));

        Assert.True(a.Plant(BareGround(a)));

        Assert.NotEqual(StateHash.Compute(a), StateHash.Compute(b));
    }

    /// <summary>
    /// ⚠️ And a village that never plants hashes exactly as it did — the layer is sparse.
    /// </summary>
    /// <remarks>
    /// The same shape the crop layer uses, and for the same reason: a full pass would mix a fresh
    /// zero per tile into every village in the game and move both goldens for the feature merely
    /// existing.
    /// </remarks>
    [Fact]
    public void AValleyWithNoPlantingMixesNothingExtra()
    {
        SimConfig config = Config;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink(), 42UL).World;

        for (int i = 0; i < world.Map.YoungSaplings.Count; i++)
        {
            Assert.False(world.Map.YoungSaplings[i], $"tile {i} is marked young in a fresh valley");
        }
    }
}
