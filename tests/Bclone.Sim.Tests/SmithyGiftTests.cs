using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The smithy is a gift after 50 iron dug by hand (Joe, D395; `tools-and-the-smith.md §9.1`, D444).
/// </summary>
public sealed class SmithyGiftTests
{
    private readonly ITestOutputHelper _output;

    public SmithyGiftTests(ITestOutputHelper output) => _output = output;

    private static SimWorld AVillage() =>
        SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink()).World;

    private static GridPos First(SimWorld world, Terrain terrain) =>
        Enumerable.Range(0, world.Map.Tiles.Count).Select(world.Zones.PositionOf)
            .First(p => world.Map.TerrainAt(p) == terrain);

    private static GridPos SomewhereASmithyFits(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 3; radius < 30; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var tile = new GridPos(site.X + dx, site.Y + dy);
                    if (world.CanBuildAt(BuildingKind.Smithy, tile).Allowed)
                    {
                        return tile;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Nowhere a smithy fits.");
    }

    /// <summary>⭐ No smithy until 50 iron has been dug by hand — and the refusal counts (§9.1).</summary>
    [Fact]
    public void NoSmithyBeforeFiftyIronIsDug()
    {
        SimWorld world = AVillage();
        GridPos site = SomewhereASmithyFits(world);

        PlacementVerdict early = world.Mark(BuildingKind.Smithy, site);
        _output.WriteLine(early.Reason);
        Assert.False(early.Allowed, "A smithy was marked before the village had dug any iron.");
        Assert.Contains("iron", early.Reason, StringComparison.Ordinal);

        world.IronEverDug = world.Config.SmithyUnlockIron - 1;
        Assert.False(world.IsUnlocked(BuildingKind.Smithy), "One iron short and it was known.");
        world.IronEverDug = world.Config.SmithyUnlockIron;
        Assert.True(world.IsUnlocked(BuildingKind.Smithy));
    }

    /// <summary>
    /// ⭐⭐ Digging the iron by hand brings the gift: a stopping moment, the first smithy free and the
    /// work still owed, and only the first (the library's three rules, D232).
    /// </summary>
    [Fact]
    public void TheFirstSmithyIsAGiftAndOnlyTheFirst()
    {
        SimWorld world = AVillage();
        world.Moments.Clear();
        world.IronEverDug = world.Config.SmithyUnlockIron - 1;

        world.Harvest(First(world, Terrain.IronDeposit));
        Moment gift = Assert.Single(world.Moments);
        _output.WriteLine($"{gift.Title}: {gift.Body}");
        Assert.True(gift.WaitsToBeDismissed);
        Assert.True(world.AFreeSmithyIsOwed);

        Assert.True(world.Mark(BuildingKind.Smithy, SomewhereASmithyFits(world)).Allowed);
        Workplace first = world.Workplaces.Single(w => w.Construction?.Kind == BuildingKind.Smithy);
        Assert.Equal(0, first.Construction!.Recipe.TotalMaterials);
        Assert.True(first.Construction.Recipe.WorkTicks > 0, "The gift raised itself — the work is owed.");
        Assert.False(world.AFreeSmithyIsOwed);

        Assert.True(world.Mark(BuildingKind.Smithy, SomewhereASmithyFits(world)).Allowed);
        Workplace second = world.Workplaces.Last(w => w.Construction?.Kind == BuildingKind.Smithy);
        Assert.True(second.Construction!.Recipe.TotalMaterials > 0, "The second smithy was free too.");
    }

    /// <summary>
    /// ⭐ Whichever the village learns by doing first introduces the tree — the smithy as well as the
    /// quarry (Joe, D440).
    /// </summary>
    [Fact]
    public void TheSmithyCanBeWhatIntroducesTheTree()
    {
        SimWorld world = AVillage();
        world.Moments.Clear();
        world.IronEverDug = world.Config.SmithyUnlockIron - 1;

        world.Harvest(First(world, Terrain.IronDeposit));

        Assert.True(world.ShownTheTechTree);
        Assert.Contains("the Tree shows", Assert.Single(world.Moments).Body, StringComparison.Ordinal);
    }

    /// <summary>The gift owed is in the fingerprint, sparsely — it changes what a building costs.</summary>
    [Fact]
    public void TheSmithysGiftIsInTheFingerprint()
    {
        SimWorld world = AVillage();
        ulong before = Bclone.Sim.Determinism.StateHash.Compute(world);
        world.AFreeSmithyIsOwed = true;
        Assert.NotEqual(before, Bclone.Sim.Determinism.StateHash.Compute(world));
    }
}
