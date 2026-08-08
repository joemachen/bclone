using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Ground is priced in workers — D86, and the first limit in this game that is not distance.
/// </summary>
/// <remarks>
/// <para>
/// Joe: <em>"more foresters in a hut, the bigger the painted area can be (to a limit). The
/// system should warn when an area is too big for the number of assigned workers."</em>
/// </para>
/// <para>
/// <b>Painting is unbounded and the warning is the whole mechanism</b> (Joe's choice of two):
/// the brush claims whatever ground you drag over, and the hands you have decide how much of
/// it is actually kept. Bounding the brush by a radius as well would be two constraints doing
/// similar work, where this way the staffing control is the only answer.
/// </para>
/// </remarks>
public sealed class WorkGroundAllowanceTests
{
    private readonly ITestOutputHelper _output;

    public WorkGroundAllowanceTests(ITestOutputHelper output) => _output = output;

    private static SimLoop Loop() =>
        ManagedVillage.Loop(VillageFixtures.Village, new InMemoryLogSink());

    /// <summary>A workplace with people actually assigned to it.</summary>
    private static Workplace AStaffedWorkplace(SimWorld world, out int hands)
    {
        foreach (Workplace place in world.Workplaces)
        {
            if (place.WorkerIds.Count > 0)
            {
                hands = place.WorkerIds.Count;
                return place;
            }
        }

        throw new Xunit.Sdk.XunitException("No workplace in the village has anybody at it.");
    }

    /// <summary>Tiles near the founding site that are not water, so painting takes.</summary>
    private static IEnumerable<GridPos> DryGround(SimWorld world, int count)
    {
        GridPos site = world.Map.FoundingSite;
        int found = 0;

        for (int radius = 1; radius < 40 && found < count; radius++)
        {
            for (int dy = -radius; dy <= radius && found < count; dy++)
            {
                for (int dx = -radius; dx <= radius && found < count; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (world.Map.Contains(at)
                        && world.Map.TerrainAt(at) != Terrain.Water
                        && world.Zones.WorkGroundOwner(at) == 0)
                    {
                        found++;
                        yield return at;
                    }
                }
            }
        }
    }

    // ---------------------------------------------------------------
    //  The allowance
    // ---------------------------------------------------------------

    /// <summary>The allowance is the hands assigned, not the seats available.</summary>
    /// <remarks>
    /// Joe's wording, and the livelier of the two: a hut whose forester dies is overstretched
    /// that moment, where a capacity-based allowance would go on claiming the land was fine
    /// while nobody worked it.
    /// </remarks>
    [Fact]
    public void GroundIsPricedInTheHandsYouActuallyHave()
    {
        SimLoop loop = Loop();
        loop.Step(VillageFixtures.Village.TicksPerYear);
        SimWorld world = loop.World;

        Workplace place = AStaffedWorkplace(world, out int hands);
        int perWorker = VillageFixtures.Village.WorkGroundTilesPerWorker;

        _output.WriteLine(
            $"{place.Name}: {hands} of {place.Capacity} seats filled, "
            + $"allowance {world.WorkGroundAllowanceFor(place)} tiles");

        Assert.Equal(hands * perWorker, world.WorkGroundAllowanceFor(place));
        Assert.False(world.IsOverstretched(place), "Ground nobody painted cannot be too much.");
    }

    /// <summary>
    /// ⭐ Paint past what the hands can keep and the village says so — and still paints it.
    /// </summary>
    /// <remarks>
    /// <b>A warning, never a refusal</b> (D86, and D43's rule for a site that is merely far).
    /// Painting big and hiring afterwards is an ordinary way to play; a brush that stopped at
    /// the current headcount would make the player fight the staffing control every stroke.
    /// </remarks>
    [Fact]
    public void PaintingPastTheHandsWarnsAndIsStillAllowed()
    {
        SimLoop loop = Loop();
        loop.Step(VillageFixtures.Village.TicksPerYear);
        SimWorld world = loop.World;

        Workplace place = AStaffedWorkplace(world, out int hands);
        int allowance = world.WorkGroundAllowanceFor(place);

        PlacementVerdict last = PlacementVerdict.Fine;
        int painted = 0;
        int warned = 0;

        foreach (GridPos tile in DryGround(world, allowance + 3))
        {
            PlacementVerdict verdict = world.PaintWorkGround(place, tile);
            Assert.True(verdict.Allowed, "Painting work ground must never be refused for size.");

            painted++;
            if (verdict.HasWarning)
            {
                warned++;
                last = verdict;
            }
        }

        _output.WriteLine(
            $"{hands} hands, allowance {allowance}; painted {painted}, warned on {warned}");
        _output.WriteLine(last.Warning);

        Assert.Equal(painted, world.Zones.WorkGroundTiles(place.Id));
        Assert.True(world.IsOverstretched(place));

        // The warning starts exactly one tile past the allowance, not before.
        Assert.Equal(painted - allowance, warned);

        // And it is a sentence a player can act on: it names the place, the ground and
        // the hands (§1.1).
        Assert.Contains(place.Name, last.Warning, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains(hands.ToString(), last.Warning, System.StringComparison.Ordinal);
    }

    /// <summary>Inside the allowance nothing is said at all.</summary>
    /// <remarks>
    /// The anti-vacuity half (D7): a warning that fires on the first tile would pass the
    /// guard above while being useless, because the player would learn to ignore it.
    /// </remarks>
    [Fact]
    public void PaintingWithinTheAllowanceIsSilent()
    {
        SimLoop loop = Loop();
        loop.Step(VillageFixtures.Village.TicksPerYear);
        SimWorld world = loop.World;

        Workplace place = AStaffedWorkplace(world, out _);
        int allowance = world.WorkGroundAllowanceFor(place);
        Assert.True(allowance > 1, "The fixture must staff somebody, or this proves nothing.");

        foreach (GridPos tile in DryGround(world, allowance))
        {
            Assert.False(
                world.PaintWorkGround(place, tile).HasWarning,
                "The village complained about ground it has the hands to keep.");
        }

        Assert.False(world.IsOverstretched(place));
    }

    /// <summary>Being overstretched outlives the painting that caused it.</summary>
    /// <remarks>
    /// The warning is a moment; the condition is a state. Somebody dies, or the staffing is
    /// turned down, and ground that was fine this morning is too much — the panel has to be
    /// able to ask, or the player is told once and never again.
    /// </remarks>
    [Fact]
    public void LosingAWorkerIsWhatMakesGroundTooMuch()
    {
        SimLoop loop = Loop();
        loop.Step(VillageFixtures.Village.TicksPerYear);
        SimWorld world = loop.World;

        Workplace place = AStaffedWorkplace(world, out int hands);
        Assert.True(hands >= 1);

        foreach (GridPos tile in DryGround(world, world.WorkGroundAllowanceFor(place)))
        {
            world.PaintWorkGround(place, tile);
        }

        Assert.False(world.IsOverstretched(place));

        // One hand fewer, and the same ground is now more than the hut can keep.
        place.WorkerIds.RemoveAt(place.WorkerIds.Count - 1);

        _output.WriteLine(
            $"{place.Name} kept {world.Zones.WorkGroundTiles(place.Id)} tiles with {hands} hands; "
            + $"with {place.WorkerIds.Count} its allowance is {world.WorkGroundAllowanceFor(place)}");

        Assert.True(world.IsOverstretched(place));
    }

    // ---------------------------------------------------------------
    //  What the brush refuses
    // ---------------------------------------------------------------

    /// <summary>Another building's ground is refused, and the refusal names them.</summary>
    /// <remarks>
    /// A careless drag across the valley must not quietly unstaff somebody else's hut, and
    /// <em>"that ground is taken"</em> without saying by whom is the kind of refusal §1.1
    /// exists to prevent.
    /// </remarks>
    [Fact]
    public void AnotherBuildingsGroundIsRefusedByName()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        Workplace first = world.Workplaces[0];
        Workplace second = world.Workplaces[1];
        GridPos tile = DryGround(world, 1).Single();

        Assert.True(world.PaintWorkGround(first, tile).Allowed);

        PlacementVerdict refused = world.PaintWorkGround(second, tile);
        _output.WriteLine(refused.Reason);

        // A refusal carries Reason; only a permitted-but-questionable answer carries
        // Warning. Two words for two outcomes, and the panel shows them differently.
        Assert.False(refused.Allowed);
        Assert.Contains(first.Name, refused.Reason, System.StringComparison.OrdinalIgnoreCase);
        Assert.Equal(first.Id, world.Zones.WorkGroundOwner(tile));
    }

    /// <summary>Water and the world's edge are never painted.</summary>
    [Fact]
    public void NobodyIsGivenWaterOrTheEdgeOfTheValley()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;
        Workplace place = world.Workplaces[0];

        var offMap = new GridPos(world.Map.MinX - 50, world.Map.MinY - 50);
        Assert.False(world.PaintWorkGround(place, offMap).Allowed);

        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (world.Map.TerrainAt(at) == Terrain.Water)
                {
                    Assert.False(world.PaintWorkGround(place, at).Allowed);
                    Assert.Equal(0, world.Zones.WorkGroundOwner(at));
                    return;
                }
            }
        }

        throw new Xunit.Sdk.XunitException("The generated valley has no water to refuse.");
    }

    /// <summary>Ground can be handed back one tile at a time.</summary>
    [Fact]
    public void GroundCanBeTakenBack()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        Workplace place = world.Workplaces[0];
        GridPos tile = DryGround(world, 1).Single();

        world.PaintWorkGround(place, tile);
        Assert.Equal(1, world.Zones.WorkGroundTiles(place.Id));

        Assert.True(world.EraseWorkGround(place, tile));
        Assert.Equal(0, world.Zones.WorkGroundTiles(place.Id));

        // Somebody else's ground is not yours to rub out.
        world.PaintWorkGround(place, tile);
        Assert.False(world.EraseWorkGround(world.Workplaces[1], tile));
        Assert.Equal(place.Id, world.Zones.WorkGroundOwner(tile));
    }

    /// <summary>The shipped config carries the number, so the game the player runs has it.</summary>
    /// <remarks>
    /// METHODOLOGY §3 — the fixture derives its numbers and <c>data/sim.config.json</c> is
    /// typed by hand, and the gap between them has produced D48, D49 and D50.
    /// </remarks>
    [Fact]
    public void TheShippedConfigPricesGroundToo()
    {
        SimConfig shipped = ShippedConfig.Load();

        Assert.True(
            shipped.WorkGroundTilesPerWorker > 0,
            "data/sim.config.json must say how much ground one worker keeps.");
        Assert.Equal(
            VillageFixtures.Village.WorkGroundTilesPerWorker,
            shipped.WorkGroundTilesPerWorker);
    }
}
