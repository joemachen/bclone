using System.Text.Json.Nodes;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Persistence;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ Claimed, not painted (D613, `seam-reach.md §8`): a finished quarry or mine takes the whole seam it stands by — the
/// nearest face in its placement diamond the village can walk to and nobody holds — through the brush's own door, and the
/// ghost's preview (<see cref="SimWorld.SeamToClaim"/>) is what completion claims. Joe: <i>"when the player places a mine or
/// quarry near its ore node, the building 'smart draws' a type of mining/fence structure around the nearest node"</i>.
/// </summary>
public sealed class SeamClaimTests
{
    private readonly ITestOutputHelper _output;

    public SeamClaimTests(ITestOutputHelper output) => _output = output;

    private static SimLoop AValley() => SimFactory.CreatePhase0(ShippedConfig.Load(), new InMemoryLogSink());

    /// <summary>A grass tile the village can walk to with open, dry, unbuilt land for <paramref name="clear"/> tiles round it.</summary>
    private static GridPos AnOpenSite(SimWorld world, int clear)
    {
        GridPos from = world.Map.FoundingSite;
        for (int radius = 8; radius < 60; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                foreach (int dx in new[] { -radius, radius })
                {
                    var at = new GridPos(from.X + dx, from.Y + dy);
                    if (OpenAround(world, at, clear))
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No open ground for the pose.");
    }

    private static bool OpenAround(SimWorld world, GridPos at, int clear)
    {
        for (int dy = -clear - 2; dy <= clear + 2; dy++)
        {
            for (int dx = -clear - 2; dx <= clear + 2; dx++)
            {
                var tile = new GridPos(at.X + dx, at.Y + dy);
                if (!world.Map.Contains(tile) || world.Map.TerrainAt(tile) == Terrain.Water || world.StandsOn(tile)
                    || world.Zones.WorkGroundOwner(tile) != 0)
                {
                    return false;
                }
            }
        }

        return world.TravelCost.CanReach(world.Map.FoundingSite, at);
    }

    /// <summary>The ground within <paramref name="clear"/> of <paramref name="site"/> made grass — no seam of the valley's own reaches the pose.</summary>
    private static void ClearAround(SimWorld world, GridPos site, int clear)
    {
        for (int dy = -clear; dy <= clear; dy++)
        {
            for (int dx = -clear; dx <= clear; dx++)
            {
                world.SetTerrain(new GridPos(site.X + dx, site.Y + dy), Terrain.Grass);
            }
        }
    }

    private static void Lay(SimWorld world, Terrain face, params GridPos[] tiles)
    {
        foreach (GridPos tile in tiles)
        {
            world.SetTerrain(tile, face);
        }
    }

    /// <summary>Mark, stock, work and complete a building at <paramref name="tile"/> — and return what stands.</summary>
    private static Workplace Raise(SimWorld world, BuildingKind kind, GridPos tile)
    {
        world.StoneEverDug = Math.Max(world.StoneEverDug, world.Config.QuarryUnlockStone);
        PlacementVerdict verdict = world.Mark(kind, tile);
        Assert.True(verdict.Allowed, verdict.Reason);
        Workplace plan = world.Workplaces.Last(w => w.Construction?.Kind == kind);
        BuildFixtures.StockTheSite(plan);
        for (int i = 0; i <= plan.Construction!.Recipe.WorkTicks; i++)
        {
            plan.Construction.Work();
        }

        world.Complete(plan);
        return world.Workplaces.Last(w => !w.IsSite && world.BuildingsCatalog.EmployedBy(kind) == w.Kind);
    }

    private static HashSet<GridPos> Held(SimWorld world, Workplace workplace) =>
        world.Zones.WorkGroundOf(workplace.Id).Select(world.Zones.PositionOf).ToHashSet();

    // ---- §8.6 guards 1, 2, 6 ----------------------------------------------------------------

    /// <summary>
    /// A quarry raised by a seam holds every tile of it — the nearer seam over a bigger one further off, and exactly what the
    /// ghost's preview said, tile for tile.
    /// </summary>
    [Fact]
    public void AQuarryClaimsTheNearestSeamWholeAndOnlyIt()
    {
        SimWorld world = AValley().World;
        GridPos site = AnOpenSite(world, 5);
        ClearAround(world, site, 5);

        // Near: three tiles at distance 1 to the SOUTH, joined. Far: seven at distance 2 to the NORTH — first in map order, so
        // only the distance can choose the near one (a claim that walked the diamond in map order would take the far).
        var near = new[] { new GridPos(site.X, site.Y + 1), new GridPos(site.X, site.Y + 2), new GridPos(site.X - 1, site.Y + 3) };
        var far = new[]
        {
            new GridPos(site.X, site.Y - 2), new GridPos(site.X + 1, site.Y - 3), new GridPos(site.X - 1, site.Y - 3),
            new GridPos(site.X, site.Y - 3), new GridPos(site.X + 2, site.Y - 4), new GridPos(site.X - 2, site.Y - 4),
            new GridPos(site.X, site.Y - 4),
        };
        Lay(world, Terrain.Rock, near);
        Lay(world, Terrain.Rock, far);

        IReadOnlyList<GridPos> preview = world.SeamToClaim(BuildingKind.Quarry, site);
        Workplace quarry = Raise(world, BuildingKind.Quarry, site);
        HashSet<GridPos> held = Held(world, quarry);
        _output.WriteLine($"quarry at {quarry.Position.ToTile()} holds {string.Join(" ", held)}; the ghost said {string.Join(" ", preview)}");

        Assert.Equal(near.ToHashSet(), held);
        Assert.Equal(held, preview.ToHashSet());
    }

    /// <summary>Two seams at the same distance: the first in map order is claimed — the north one, whatever its size.</summary>
    [Fact]
    public void ATieGoesToMapOrder()
    {
        SimWorld world = AValley().World;
        GridPos site = AnOpenSite(world, 5);
        ClearAround(world, site, 5);
        var north = new GridPos(site.X, site.Y - 1);
        var south = new[] { new GridPos(site.X, site.Y + 1), new GridPos(site.X, site.Y + 2), new GridPos(site.X + 1, site.Y + 2) };
        Lay(world, Terrain.Rock, north);
        Lay(world, Terrain.Rock, south);
        if (world.StandsOn(south[0]) || !world.CanBuildAt(BuildingKind.Quarry, site).Allowed)
        {
            // The footprint reaches south over the pose: put the second seam east instead, same distance, later in map order.
            Lay(world, Terrain.Grass, south);
        }

        Assert.Equal(new[] { north }, world.SeamToClaim(BuildingKind.Quarry, site).ToArray());
    }

    // ---- §8.6 guard 4 -----------------------------------------------------------------------

    /// <summary>
    /// A seam is never shared: a second quarry by one quarry 1 holds is refused, in words; by that seam and another, it
    /// claims the other. And a mine by the same outcrop takes its iron, not the quarry's rock.
    /// </summary>
    [Fact]
    public void ASeamIsNeverShared()
    {
        SimWorld world = AValley().World;
        GridPos site = AnOpenSite(world, 7);
        ClearAround(world, site, 7);
        var rockA = new[] { new GridPos(site.X, site.Y - 1), new GridPos(site.X + 1, site.Y - 1), new GridPos(site.X + 2, site.Y - 1),
            new GridPos(site.X + 3, site.Y - 1), new GridPos(site.X + 4, site.Y - 1) };
        Lay(world, Terrain.Rock, rockA);
        Workplace first = Raise(world, BuildingKind.Quarry, site);
        Assert.Equal(rockA.ToHashSet(), Held(world, first));

        // By rock A alone (its far end): refused.
        var second = new GridPos(site.X + 4, site.Y + 1);
        PlacementVerdict refused = world.CanBuildAt(BuildingKind.Quarry, second);
        _output.WriteLine(refused.Reason);
        Assert.False(refused.Allowed);
        Assert.Contains("a seam of its own", refused.Reason, StringComparison.Ordinal);
        Assert.Contains(first.Name, refused.Reason, StringComparison.Ordinal);

        // A rock of its own beside it: allowed, and that is what it claims.
        var rockB = new GridPos(second.X + 1, second.Y + 1);
        Lay(world, Terrain.Rock, rockB);
        Assert.True(world.CanBuildAt(BuildingKind.Quarry, second).Allowed);
        Assert.Equal(new[] { rockB }, world.SeamToClaim(BuildingKind.Quarry, second).ToArray());

        // Iron joined to rock A is its own seam: a mine by it is not refused for the quarry's rock.
        var iron = new GridPos(site.X + 1, site.Y - 2);
        Lay(world, Terrain.IronDeposit, iron);
        Assert.Equal(new[] { iron }, world.SeamToClaim(BuildingKind.Mine, new GridPos(site.X + 1, site.Y)).ToArray());
    }

    // ---- §8.6 guard 5 -----------------------------------------------------------------------

    /// <summary>A quarry pulled down lets its seam go; a quarry moved lets the old seam go and claims the one where it lands.</summary>
    [Fact]
    public void ASeamGoesWithTheBuilding()
    {
        SimWorld world = AValley().World;
        GridPos site = AnOpenSite(world, 9);
        ClearAround(world, site, 9);
        var here = new[] { new GridPos(site.X, site.Y - 1), new GridPos(site.X + 1, site.Y - 1) };
        var there = new GridPos(site.X + 6, site.Y + 5);
        var thereRock = new[] { new GridPos(there.X, there.Y - 1), new GridPos(there.X - 1, there.Y - 2) };
        Lay(world, Terrain.Rock, here);
        Lay(world, Terrain.Rock, thereRock);

        Workplace quarry = Raise(world, BuildingKind.Quarry, site);
        Assert.Equal(here.ToHashSet(), Held(world, quarry));

        PlacementVerdict moving = world.MarkRelocation(quarry.Position.ToTile(), there);
        Assert.True(moving.Allowed, moving.Reason);
        Workplace move = world.Workplaces.Last(w => w.Construction?.MovingFrom is not null);
        BuildFixtures.StockTheSite(move);
        for (int i = 0; i <= move.Construction!.Recipe.WorkTicks; i++)
        {
            move.Construction.Work();
        }

        world.Complete(move);
        Assert.Equal(thereRock.ToHashSet(), Held(world, quarry));
        Assert.All(here, t => Assert.Equal(0, world.Zones.WorkGroundOwner(t)));

        world.Demolish(quarry);
        Assert.All(thereRock, t => Assert.Equal(0, world.Zones.WorkGroundOwner(t)));
    }

    // ---- §8.6 guard 8 -----------------------------------------------------------------------

    /// <summary>A claimed seam is bigger than the hands for it, and nothing says "paint less" — there is nothing to paint.</summary>
    [Fact]
    public void AClaimedSeamIsNeverOverstretched()
    {
        SimWorld world = AValley().World;
        GridPos site = AnOpenSite(world, 6);
        ClearAround(world, site, 6);
        var rock = Enumerable.Range(-3, 9).Select(dx => new GridPos(site.X + dx, site.Y - 1)).ToArray();
        Lay(world, Terrain.Rock, rock);

        Workplace quarry = Raise(world, BuildingKind.Quarry, site);
        Assert.Equal(rock.Length, world.Zones.WorkGroundTiles(quarry.Id));
        Assert.True(world.Zones.WorkGroundTiles(quarry.Id) > world.WorkGroundAllowanceFor(quarry), "the premise: more tiles than hands");
        Assert.Null(world.OverstretchedNote(quarry));
    }

    // ---- §8.6 guard 9 -----------------------------------------------------------------------

    /// <summary>A claimed seam survives a save and a load — the hash, and the ground tile for tile.</summary>
    [Fact]
    public void AClaimedSeamIsSavedAndLoaded()
    {
        SimLoop loop = AValley();
        SimWorld world = loop.World;
        GridPos site = AnOpenSite(world, 5);
        ClearAround(world, site, 5);
        Lay(world, Terrain.Rock, new GridPos(site.X, site.Y - 1), new GridPos(site.X + 1, site.Y - 2));
        Workplace quarry = Raise(world, BuildingKind.Quarry, site);
        HashSet<GridPos> held = Held(world, quarry);
        Assert.Equal(2, held.Count);

        JsonObject document = SaveGame.Capture(loop, "test", "2026-10-19 12:00", "test");
        SimLoop again = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), world.Config, null);
        Workplace loaded = again.World.Workplaces.Single(w => w.Id == quarry.Id);

        Assert.Equal(held, Held(again.World, loaded));
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(again.World));
    }
}
