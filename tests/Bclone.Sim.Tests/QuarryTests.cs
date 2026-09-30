using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The quarry — painted rock that never runs out (`specs/quarry.md`, D434).
/// </summary>
public sealed class QuarryTests
{
    private readonly ITestOutputHelper _output;

    public QuarryTests(ITestOutputHelper output) => _output = output;

    private static SimWorld AVillage(SimConfig? config = null) =>
        SimFactory.CreatePhase0(config ?? VillageFixtures.Village, new InMemoryLogSink()).World;

    private static GridPos FirstOf(SimWorld world, Terrain terrain)
    {
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            if (world.Map.Tiles[i] == terrain)
            {
                return world.Zones.PositionOf(i);
            }
        }

        throw new Xunit.Sdk.XunitException($"No {terrain} in this valley.");
    }

    /// <summary>
    /// ⭐ Stone and iron dug by hand are counted, and the count is the whole tile (§3.2).
    /// </summary>
    /// <remarks>
    /// The whole tile's yield, carried or not: what a villager cannot carry is left on the ground,
    /// and it is still stone the village dug. Logs are not counted here — they have their own.
    /// </remarks>
    [Fact]
    public void StoneAndIronDugByHandAreCounted()
    {
        SimWorld world = AVillage();
        Assert.Equal(0, world.StoneEverDug);
        Assert.Equal(0, world.IronEverDug);

        (Goods stone, int rock) = world.Harvest(FirstOf(world, Terrain.Rock));
        (Goods iron, int ore) = world.Harvest(FirstOf(world, Terrain.IronDeposit));
        world.Harvest(FirstOf(world, Terrain.Forest));

        Assert.Equal(Goods.Stone, stone);
        Assert.Equal(Goods.Iron, iron);
        Assert.Equal(rock, world.StoneEverDug);
        Assert.Equal(ore, world.IronEverDug);
        Assert.True(rock > 0 && ore > 0, "Nothing was dug, so nothing was tested.");
    }

    /// <summary>
    /// ⭐ What a village has dug is in its fingerprint — the unlocks read it (§3.2).
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>No golden can say this</b>: neither fifty-year village paints a seam, and the farm
    /// golden's "seam" is the crops × brush one. So two villages identical but for what they dug
    /// must hash apart, and stone must not read as iron — and a village that dug nothing must hash
    /// as it did before the counters existed (sparse).
    /// </remarks>
    [Fact]
    public void WhatTheVillageDugIsInItsFingerprint()
    {
        SimWorld none = AVillage();
        SimWorld stone = AVillage();
        SimWorld iron = AVillage();
        ulong before = Bclone.Sim.Determinism.StateHash.Compute(none);

        stone.StoneEverDug = 12;
        iron.IronEverDug = 12;

        ulong dugStone = Bclone.Sim.Determinism.StateHash.Compute(stone);
        ulong dugIron = Bclone.Sim.Determinism.StateHash.Compute(iron);

        Assert.NotEqual(before, dugStone);
        Assert.NotEqual(before, dugIron);
        Assert.NotEqual(dugStone, dugIron);

        // And the amount, not only the fact: twelve iron is not thirteen (red-checked, D436).
        iron.IronEverDug = 13;
        stone.StoneEverDug = 13;
        Assert.NotEqual(dugIron, Bclone.Sim.Determinism.StateHash.Compute(iron));
        Assert.NotEqual(dugStone, Bclone.Sim.Determinism.StateHash.Compute(stone));
    }

    /// <summary>
    /// ⭐ The brush warns when it would mark the last rock the village can walk to (§3.8).
    /// </summary>
    /// <remarks>
    /// A quarry is cut only into rock, and a laborer clears rock for good, so the stroke that
    /// marks the last of it is a decision the player should see. A warning, never a refusal —
    /// D86's shape: a player who clears it anyway has decided.
    /// </remarks>
    [Fact]
    public void TheLastRockTheVillageCanReachIsWarnedAbout()
    {
        SimWorld world = AVillage();
        var reachable = new List<GridPos>();
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            GridPos at = world.Zones.PositionOf(i);
            if (world.Map.Tiles[i] == Terrain.Rock
                && world.TravelCost.Cost(at, world.Map.FoundingSite) != TravelCostField.Unreachable)
            {
                reachable.Add(at);
            }
        }

        Assert.True(reachable.Count > 2, "This valley has almost no rock to test with.");

        PlacementVerdict first = world.PaintHarvest(reachable[0]);
        Assert.True(first.Allowed);
        Assert.False(first.HasWarning, $"Marking one rock of {reachable.Count} warned: {first.Warning}");

        for (int i = 1; i < reachable.Count - 1; i++)
        {
            world.PaintHarvest(reachable[i]);
        }

        PlacementVerdict last = world.PaintHarvest(reachable[^1]);
        _output.WriteLine($"{reachable.Count} reachable rock tiles; the last says: {last.Warning}");

        Assert.True(last.Allowed, "The last rock was refused — it should only be warned about.");
        Assert.True(last.HasWarning, "Marking the last rock the village can reach said nothing.");
        Assert.Contains("quarry", last.Warning, StringComparison.Ordinal);
    }
}
