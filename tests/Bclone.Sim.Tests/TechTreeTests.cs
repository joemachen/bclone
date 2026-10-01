using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The tech-tree map — what the village may yet learn, and what it takes (`tech-tree-map.md`, D440).
/// </summary>
public sealed class TechTreeTests
{
    private readonly ITestOutputHelper _output;

    public TechTreeTests(ITestOutputHelper output) => _output = output;

    private static SimWorld AVillage() =>
        SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink()).World;

    private static TechNodeRow Node(SimWorld world, string id) =>
        world.Config.TechNodeRows.Single(n => n.Id == id);

    /// <summary>
    /// ⭐ The map and the gate cannot disagree: the quarry's node is known exactly when `Mark` would
    /// take a quarry, and its progress is the gate's own number (§3.3, §5).
    /// </summary>
    [Fact]
    public void TheQuarrysNodeReadsTheQuarrysGate()
    {
        SimWorld world = AVillage();
        TechNodeRow quarry = Node(world, "quarry");

        foreach (int dug in new[] { 0, 1, world.Config.QuarryUnlockStone - 1, world.Config.QuarryUnlockStone, 400 })
        {
            world.StoneEverDug = dug;
            (TechState state, int progress, int of) = TechTree.StateOf(world, quarry);
            _output.WriteLine($"{dug} dug: {state} {progress}/{of}");

            Assert.Equal(world.IsUnlocked(BuildingKind.Quarry), state == TechState.Known);
            Assert.Equal(dug, progress);
            Assert.Equal(world.Config.QuarryUnlockStone, of);
        }
    }

    /// <summary>⭐ Fog, sight, known — the three states (§3.2).</summary>
    [Fact]
    public void FogLiftsANodeAtATime()
    {
        SimWorld world = AVillage();
        TechNodeRow quarry = Node(world, "quarry");
        TechNodeRow mason = Node(world, "mason");
        TechNodeRow cottage = Node(world, "cottage");

        Assert.Equal(TechState.Fogged, TechTree.StateOf(world, quarry).State);
        Assert.Equal(TechState.Fogged, TechTree.StateOf(world, mason).State);

        world.StoneEverDug = 12;
        Assert.Equal(TechState.InSight, TechTree.StateOf(world, quarry).State);
        Assert.Equal(TechState.Fogged, TechTree.StateOf(world, mason).State);

        world.StoneEverDug = world.Config.QuarryUnlockStone;
        Assert.Equal(TechState.Known, TechTree.StateOf(world, quarry).State);
        Assert.Equal(TechState.InSight, TechTree.StateOf(world, mason).State);

        // The horizon is seen, never reached — and what hangs from it stays in the fog.
        Assert.Equal(TechState.Fogged, TechTree.StateOf(world, cottage).State);
    }

    /// <summary>
    /// ⭐ The first building learned by doing introduces the map, once — and the library and the
    /// town hall never do (Joe, D440).
    /// </summary>
    [Fact]
    public void TheFirstThingLearnedByDoingIntroducesTheTree()
    {
        SimWorld world = AVillage();
        world.Moments.Clear();
        Assert.False(world.ShownTheTechTree);

        world.StoneEverDug = world.Config.QuarryUnlockStone - 1;
        GridPos rock = Enumerable.Range(0, world.Map.Tiles.Count)
            .Select(world.Zones.PositionOf).First(p => world.Map.TerrainAt(p) == Terrain.Rock);
        world.Harvest(rock);

        Moment learned = Assert.Single(world.Moments);
        _output.WriteLine(learned.Body);
        Assert.True(world.ShownTheTechTree);
        Assert.Contains("the Tree shows", learned.Body, StringComparison.Ordinal);

        Assert.True(TechTree.IntroducesTheMap(TechCondition.StoneDug));
        Assert.True(TechTree.IntroducesTheMap(TechCondition.IronDug));
        Assert.False(TechTree.IntroducesTheMap(TechCondition.KeptGranaryYears));
        Assert.False(TechTree.IntroducesTheMap(TechCondition.FoundersGone));
    }

    /// <summary>Having been shown the tree is in the fingerprint, sparsely.</summary>
    [Fact]
    public void BeingShownTheTreeIsInTheFingerprint()
    {
        SimWorld world = AVillage();
        ulong before = StateHash.Compute(world);
        world.ShownTheTechTree = true;
        Assert.NotEqual(before, StateHash.Compute(world));
    }

    /// <summary>A tree that cannot be drawn honestly is refused at load (§4).</summary>
    [Fact]
    public void ABrokenTreeIsRefusedAtLoad()
    {
        TechNodeRow a = new() { Id = "a", Name = "A", Condition = TechCondition.NotYet, Requires = new[] { "b" } };
        TechNodeRow b = new() { Id = "b", Name = "B", Condition = TechCondition.NotYet, Requires = new[] { "a" } };
        Assert.Throws<SimConfigException>(() => TechTree.Validate(new[] { a, b }));

        TechNodeRow orphan = new() { Id = "c", Name = "C", Condition = TechCondition.NotYet, Requires = new[] { "nobody" } };
        Assert.Throws<SimConfigException>(() => TechTree.Validate(new[] { orphan }));

        TechNodeRow horizonThatBuilds = new() { Id = "d", Name = "D", Unlocks = BuildingKind.Well, Condition = TechCondition.NotYet };
        Assert.Throws<SimConfigException>(() => TechTree.Validate(new[] { horizonThatBuilds }));

        TechTree.Validate(TechTree.DefaultNodes());
    }
}
