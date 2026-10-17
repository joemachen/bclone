using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ The fenced pasture (D598, `specs/livestock.md §0.2`; Joe's calls, D597): <i>Fence the pasture</i> locks a barn's
/// painted ground and queues a fence site priced by its edges; nothing is walled until it is finished; finished, every edge
/// but the gate is a wall; cancelled it unlocks and keeps the paint; taken down it unpaints and refunds half — and never
/// takes a neighbour's wall with it; the barn pulled down takes its fence.
/// </summary>
public sealed class PastureFenceTests
{
    private static SimLoop AVillage() => SimFactory.CreatePhase0(ShippedConfig.Established(), new InMemoryLogSink());

    /// <summary>
    /// A barn with a compact 8 × 5 pasture on open ground — every tile and a one-tile ring round it free, so the fence
    /// shuts nobody in. ⚠️ The herd's ring painting (<c>HerdTests.GivePasture</c>) wanders among the houses, and the
    /// wall-off refusal rightly refused to fence a family in (D598's first run).
    /// </summary>
    internal static (SimWorld World, Workplace Barn) ABarnWithPasture()
    {
        SimWorld world = AVillage().World;
        return (world, ABarnWithPastureIn(world));
    }

    /// <summary>The same barn and pasture, raised in a world the caller already has (the save pose's loop).</summary>
    internal static Workplace ABarnWithPastureIn(SimWorld world)
    {
        Workplace barn = HerdTests.RaiseABarn(world);
        GiveACompactPasture(world, barn, 8, 5);
        return barn;
    }

    /// <summary>Paint a <paramref name="wide"/> × <paramref name="deep"/> block of open ground, ringed by open ground, nearest the barn.</summary>
    internal static void GiveACompactPasture(SimWorld world, Workplace barn, int wide, int deep)
    {
        bool Open(GridPos t) =>
            world.Map.Contains(t) && world.Map.TerrainAt(t) is Terrain.Grass or Terrain.Forest
            && world.WhatStandsAt(t) is null && world.Zones.PlotOwner(t) == 0 && world.Zones.WorkGroundOwner(t) == 0;

        for (int reach = 2; reach < 40; reach++)
        {
            for (int dy = -reach; dy <= reach; dy++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != reach)
                    {
                        continue;
                    }

                    int x0 = barn.Tile.X + dx, y0 = barn.Tile.Y + dy;
                    bool fits = true;
                    for (int y = y0 - 1; y <= y0 + deep && fits; y++)
                    {
                        for (int x = x0 - 1; x <= x0 + wide && fits; x++)
                        {
                            var t = new GridPos(x, y);
                            bool inner = x >= x0 && x < x0 + wide && y >= y0 && y < y0 + deep;
                            fits = Open(t) && (!inner || world.CanPaintWorkGround(barn, t).Allowed);
                        }
                    }

                    if (!fits || !world.TravelCost.CanReach(world.Map.FoundingSite, new GridPos(x0, y0)))
                    {
                        continue;
                    }

                    for (int y = y0; y < y0 + deep; y++)
                    {
                        for (int x = x0; x < x0 + wide; x++)
                        {
                            Assert.True(world.PaintWorkGround(barn, new GridPos(x, y)).Allowed);
                        }
                    }

                    return;
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"No open {wide} × {deep} block near the barn.");
    }

    private static List<GridPos> Pasture(SimWorld world, Workplace barn) =>
        world.Zones.WorkGroundOf(barn.Id).Select(world.Zones.PositionOf).ToList();

    private static Workplace SiteOf(SimWorld world, Workplace barn) =>
        world.FenceSiteOf(barn) ?? throw new Xunit.Sdk.XunitException("Fencing raised no site.");

    /// <summary>Deliver everything the fence's site wants, work it through, and finish it.</summary>
    internal static void FinishTheFence(SimWorld world, Workplace barn)
    {
        Workplace site = SiteOf(world, barn);
        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        world.Complete(site);
    }

    private static bool WalledBetween(SimWorld world, GridPos from, GridPos to) =>
        (world.Zones.WallsOn(from) & ZoneMap.EdgeBit(to.X - from.X, to.Y - from.Y)) != 0;

    // ---------------------------------------------------------------
    //  Fence it
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ <i>Fence the pasture</i> freezes the barn's tiles, picks a gate outside them, and queues a site priced by the
    /// fence's edges — <b>a log and two ticks a length</b> — with nothing walled yet.
    /// </summary>
    [Fact]
    public void FencingLocksTheGroundAndQueuesASitePricedByItsEdges()
    {
        (SimWorld world, Workplace barn) = ABarnWithPasture();
        List<GridPos> pasture = Pasture(world, barn);

        PlacementVerdict fenced = world.FenceThePasture(barn);
        Assert.True(fenced.Allowed, fenced.Reason);

        Assert.Equal(PastureFenceState.Site, world.FenceStateOf(barn));
        Assert.Equal(world.Zones.WorkGroundOf(barn.Id).OrderBy(i => i), barn.FencedTiles);
        GridPos gate = barn.FenceGate ?? throw new Xunit.Sdk.XunitException("No gate.");
        Assert.DoesNotContain(gate, pasture);
        Assert.Contains(pasture, t => Math.Abs(t.X - gate.X) + Math.Abs(t.Y - gate.Y) == 1);

        int edges = ZoneMap.FenceEdges(pasture, new[] { gate }, Array.Empty<GridPos>(), out _).Count;
        Workplace site = SiteOf(world, barn);
        Assert.Equal(gate, site.Tile);
        Assert.Equal(edges * world.Config.PastureFenceLogsPerEdge, site.Construction!.StillNeeded(Goods.Logs));
        Assert.Equal(edges * world.Config.PastureFenceWorkTicksPerEdge, site.Construction.Recipe.WorkTicks);

        // ⛔ Nothing walled until it is finished — the builders must not fence themselves in.
        Assert.Empty(world.Zones.PastureFenceOf(barn.Id));
        Assert.DoesNotContain(pasture, t => world.Zones.WallsOn(t) != 0 && world.Zones.PlotOwner(t) == 0);
    }

    /// <summary>The refusals that can be posed: nothing painted, and a fence already up.</summary>
    [Fact]
    public void AFenceIsRefusedWithItsReason()
    {
        SimWorld world = AVillage().World;
        Workplace barn = HerdTests.RaiseABarn(world);
        Assert.Equal("There is no pasture to fence — give the barn ground first.", world.FenceThePasture(barn).Reason);

        GridPos one = FreeGrass(world, barn, 1, apart: 0)[0];
        Assert.True(world.PaintWorkGround(barn, one).Allowed);
        Assert.True(world.FenceThePasture(barn).Allowed);
        Assert.Equal("The fence is being built already.", world.FenceThePasture(barn).Reason);
    }

    /// <summary>
    /// ⛔ <b>A fence that would shut a family in is refused, naming them</b> — the herd's ring painting runs among the
    /// houses of the established valley, and fencing it would close the Brambles' door (D598's first run found it).
    /// </summary>
    [Fact]
    public void AFenceThatWouldShutAFamilyInIsRefused()
    {
        SimWorld world = AVillage().World;
        Workplace barn = HerdTests.RaiseABarn(world);
        HerdTests.GivePasture(world, barn, 40);

        PlacementVerdict refused = world.FenceThePasture(barn);

        Assert.False(refused.Allowed);
        Assert.StartsWith("That would fence in the ", refused.Reason, StringComparison.Ordinal);
        Assert.Equal(PastureFenceState.None, world.FenceStateOf(barn));
        Assert.Null(world.FenceSiteOf(barn));
        Assert.Null(world.SayWhatFencingWouldDo(barn));
    }

    /// <summary>
    /// ⭐ <b>A pasture in pieces is fenced round its main piece, and the strays are given back</b> — said before the
    /// button is pressed and in the log after (D598: Joe's own pasture was 100 tiles and 2 apart).
    /// </summary>
    [Fact]
    public void APastureInPiecesIsFencedRoundItsMainPiece()
    {
        (SimWorld world, Workplace barn) = ABarnWithPasture();
        int main = world.Zones.WorkGroundTiles(barn.Id);
        GridPos stray = FreeGrass(world, barn, 1, apart: 0, awayFrom: Pasture(world, barn))[0];
        Assert.True(world.PaintWorkGround(barn, stray).Allowed);
        Assert.EndsWith("— and the 1 tile apart from the pasture given back.", world.SayWhatFencingWouldDo(barn), StringComparison.Ordinal);

        Assert.True(world.FenceThePasture(barn).Allowed);

        Assert.Equal(0, world.Zones.WorkGroundOwner(stray));
        Assert.Equal(main, barn.FencedTiles.Count);
        Assert.Equal(main, world.Zones.WorkGroundTiles(barn.Id));
    }

    // ---------------------------------------------------------------
    //  The lock
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ From the button on, the ground is locked: no paint, no erase, no building inside the fence or on the lane at its
    /// gate, and the barn cannot be moved — each said in words.
    /// </summary>
    [Fact]
    public void AFencedPastureIsLocked()
    {
        (SimWorld world, Workplace barn) = ABarnWithPasture();
        GridPos outside = FreeGrass(world, barn, 1, apart: 0, awayFrom: Pasture(world, barn))[0];
        Assert.True(world.FenceThePasture(barn).Allowed);
        List<GridPos> pasture = Pasture(world, barn);
        const string Locked = "The pasture is fenced — take the fence down to change it.";

        Assert.Equal(Locked, world.CanPaintWorkGround(barn, outside).Reason);
        Assert.Equal(Locked, world.CanEraseWorkGround(barn, pasture[0]).Reason);
        Assert.False(world.EraseWorkGround(barn, pasture[0]));
        Assert.Equal(pasture.Count, world.Zones.WorkGroundTiles(barn.Id));

        Assert.StartsWith("That is inside", world.CanBuildAt(BuildingKind.Well, pasture[pasture.Count / 2]).Reason, StringComparison.Ordinal);
        Assert.Contains("take the fence down to move it", world.MarkRelocation(barn.Tile, outside).Reason, StringComparison.Ordinal);
        Assert.StartsWith("A pasture fence is put up from its barn's card", world.CanBuildAt(BuildingKind.PastureFence, outside).Reason, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  Finished
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ <b>Finished, every edge but the gate is a wall</b> — and the pasture is reached only through the gate: walled
    /// shut there too, its inside cannot be reached from the village.
    /// </summary>
    [Fact]
    public void AFinishedFenceWallsEveryEdgeButTheGate()
    {
        (SimWorld world, Workplace barn) = ABarnWithPasture();
        Assert.True(world.FenceThePasture(barn).Allowed);
        List<GridPos> pasture = Pasture(world, barn);
        GridPos gate = barn.FenceGate!.Value;
        FinishTheFence(world, barn);

        Assert.Equal(PastureFenceState.Standing, world.FenceStateOf(barn));
        Assert.Null(world.FenceSiteOf(barn));
        List<(GridPos From, GridPos To)> edges = ZoneMap.FenceEdges(pasture, new[] { gate }, Array.Empty<GridPos>(), out (GridPos Yard, GridPos Lane)? opening);
        Assert.NotEmpty(edges);
        Assert.All(edges, e => Assert.True(WalledBetween(world, e.From, e.To), $"No wall between {e.From} and {e.To}."));
        (GridPos inner, GridPos lane) = opening!.Value;
        Assert.Equal(gate, lane);
        Assert.False(WalledBetween(world, inner, lane), "The gate is walled.");

        // The lane at the gate stays open: its site has gone, and nothing may be built there.
        Assert.StartsWith("That is the way into", world.CanBuildAt(BuildingKind.Well, gate).Reason, StringComparison.Ordinal);

        GridPos home = world.Map.FoundingSite;
        GridPos deep = pasture.First(t => t != inner && world.Map.TerrainAt(t) != Terrain.Water);
        Assert.True(world.TravelCost.CanReach(home, deep), "The pasture cannot be reached even through its gate.");
        world.Zones.Wall(inner, lane, up: true);
        Assert.False(world.TravelCost.CanReach(home, deep), "The pasture is reached round its fence, not only through the gate.");
        world.Zones.Wall(inner, lane, up: false);
    }

    // ---------------------------------------------------------------
    //  Cancelled, taken down, pulled down with the barn
    // ---------------------------------------------------------------

    /// <summary>A fence cancelled before it is up gives back what was delivered, unlocks, and leaves the paint (rule 6).</summary>
    [Fact]
    public void ACancelledFenceUnlocksAndKeepsThePaint()
    {
        (SimWorld world, Workplace barn) = ABarnWithPasture();
        int tiles = world.Zones.WorkGroundTiles(barn.Id);
        Assert.True(world.FenceThePasture(barn).Allowed);
        Workplace site = SiteOf(world, barn);
        int owed = site.Construction!.StillNeeded(Goods.Logs);
        BuildFixtures.StockTheSite(site);
        int logs = world.InStores(Goods.Logs);

        Assert.True(world.TakeTheFenceDown(barn).Allowed);

        Assert.Equal(PastureFenceState.None, world.FenceStateOf(barn));
        Assert.Null(world.FenceSiteOf(barn));
        Assert.Null(barn.FenceGate);
        Assert.Equal(tiles, world.Zones.WorkGroundTiles(barn.Id));
        Assert.Equal(logs + owed, world.InStores(Goods.Logs));
        Assert.True(world.CanPaintWorkGround(barn, Pasture(world, barn)[0]).Allowed);
    }

    /// <summary>
    /// ⭐ A standing fence taken down: its walls go, half its logs come back, and the pasture is unpainted with it (Joe:
    /// <i>"completely unpainted"</i>) — ready to paint and fence again.
    /// </summary>
    [Fact]
    public void AStandingFenceTakenDownUnpaintsAndRefundsHalf()
    {
        (SimWorld world, Workplace barn) = ABarnWithPasture();
        Assert.True(world.FenceThePasture(barn).Allowed);
        List<GridPos> pasture = Pasture(world, barn);
        int edges = SiteOf(world, barn).Construction!.StillNeeded(Goods.Logs) / world.Config.PastureFenceLogsPerEdge;
        FinishTheFence(world, barn);
        int logs = world.InStores(Goods.Logs);

        Assert.True(world.TakeTheFenceDown(barn).Allowed);

        Assert.Equal(PastureFenceState.None, world.FenceStateOf(barn));
        Assert.Empty(world.Zones.PastureFenceOf(barn.Id));
        Assert.Equal(0, world.Zones.WorkGroundTiles(barn.Id));
        Assert.DoesNotContain(pasture, t => world.Zones.WallsOn(t) != 0 && world.Zones.PlotOwner(t) == 0);
        Assert.Equal(logs + (edges * world.Config.PastureFenceLogsPerEdge * world.Config.DemolitionReturnsPercent / 100), world.InStores(Goods.Logs));
        Assert.Equal("There is no fence to take down.", world.TakeTheFenceDown(barn).Reason);
    }

    /// <summary>
    /// ⛔ <b>A pasture fence taken down never takes a yard's wall with it</b> — the layer is a union, and an edge the two
    /// share must still be walled for the yard (`livestock.md §0.2.3` rule 7; `ZoneMap.PullTheFenceDown` restored only
    /// plots' walls).
    /// </summary>
    [Fact]
    public void TakingAPastureFenceDownLeavesAYardsWall()
    {
        SimWorld world = AVillage().World;
        ZoneMap zones = world.Zones;
        (GridPos yard, GridPos beyond)? shared = null;
        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height && shared is null; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width && shared is null; x++)
            {
                var t = new GridPos(x, y);
                if (zones.PlotOwner(t) == 0)
                {
                    continue;
                }

                foreach ((int dx, int dy) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
                {
                    var n = new GridPos(x + dx, y + dy);
                    if (zones.PlotOwner(n) == 0 && world.Map.Contains(n) && world.Map.TerrainAt(n) != Terrain.Water
                        && (zones.WallsOn(t) & ZoneMap.EdgeBit(dx, dy)) != 0)
                    {
                        shared = (t, n);
                        break;
                    }
                }
            }
        }

        (GridPos yardTile, GridPos outside) = shared ?? throw new Xunit.Sdk.XunitException("No yard wall in the established valley.");
        GridPos gate = new GridPos(outside.X + (outside.X - yardTile.X), outside.Y + (outside.Y - yardTile.Y));

        zones.RaiseAPastureFence(99_999, new[] { outside }, gate);
        Assert.True(WalledBetween(world, outside, yardTile));
        zones.PullAPastureFenceDown(99_999);

        Assert.True(WalledBetween(world, yardTile, outside), "The yard lost its wall when the pasture's fence came down.");

        // ⭐ And the other way: the yard released while the pasture's fence stands leaves the pasture its wall.
        zones.RaiseAPastureFence(99_999, new[] { outside }, gate);
        zones.ReleasePlot(zones.PlotOwner(yardTile));
        Assert.True(WalledBetween(world, outside, yardTile), "The pasture lost its wall when the yard was released.");
    }

    /// <summary>A barn pulled down takes its fence with it — the walls, or the site (rule 8).</summary>
    [Fact]
    public void ABarnPulledDownTakesItsFence()
    {
        (SimWorld world, Workplace barn) = ABarnWithPasture();
        Assert.True(world.FenceThePasture(barn).Allowed);
        FinishTheFence(world, barn);
        Assert.NotEmpty(world.Zones.PastureFenceOf(barn.Id));
        List<GridPos> pasture = Pasture(world, barn);

        world.Demolish(barn);

        Assert.Null(world.FindWorkplace(barn.Id)); // the barn's workplace goes with its building (D598 found it did not)
        Assert.Empty(world.Zones.PastureFenceOf(barn.Id));
        Assert.Null(world.FencedPastureAt(pasture[0]));
        Assert.DoesNotContain(pasture, t => world.Zones.WallsOn(t) != 0 && world.Zones.PlotOwner(t) == 0);
    }

    // ---------------------------------------------------------------
    //  Saved and hashed
    // ---------------------------------------------------------------

    /// <summary>
    /// A fence comes back from a save as it went — a site with its lock, a standing fence with its walls rebuilt — and the
    /// fact reaches the hash only when set.
    /// </summary>
    [Fact]
    public void AFenceComesBackFromASaveAndReachesTheHash()
    {
        (SimWorld world, Workplace barn) = ABarnWithPasture();
        ulong unfenced = StateHash.Compute(world);
        Assert.True(world.FenceThePasture(barn).Allowed);
        Assert.NotEqual(unfenced, StateHash.Compute(world));

        SimWorld site = Reloaded(world);
        Workplace siteBarn = site.FindWorkplace(barn.Id)!;
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(site));
        Assert.Equal(PastureFenceState.Site, site.FenceStateOf(siteBarn));
        Assert.False(site.CanPaintWorkGround(siteBarn, Pasture(site, siteBarn)[0]).Allowed);
        Assert.NotNull(site.FenceSiteOf(siteBarn));

        FinishTheFence(world, barn);
        SimWorld standing = Reloaded(world);
        Workplace standingBarn = standing.FindWorkplace(barn.Id)!;
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(standing));
        Assert.Equal(world.Zones.PastureFenceOf(barn.Id), standing.Zones.PastureFenceOf(standingBarn.Id));
        Assert.Equal(barn.FenceGate, standingBarn.FenceGate);

        // ⚠️ The fact alone moves the hash — fencing also raises a site, so "before and after the button" proved nothing
        // (D598's F18 scored zero against it). Each of the three, changed by itself:
        ulong fact = StateHash.Compute(world);
        barn.FenceStands = false;
        Assert.NotEqual(fact, StateHash.Compute(world));
        barn.FenceStands = true;
        GridPos gateWas = barn.FenceGate!.Value;
        barn.FenceGate = new GridPos(gateWas.X + 1, gateWas.Y);
        Assert.NotEqual(fact, StateHash.Compute(world));
        barn.FenceGate = gateWas;
        barn.FencedTiles.Add(barn.FencedTiles[^1] + 1);
        Assert.NotEqual(fact, StateHash.Compute(world));
        barn.FencedTiles.RemoveAt(barn.FencedTiles.Count - 1);
        Assert.Equal(fact, StateHash.Compute(world));
    }

    private static SimWorld Reloaded(SimWorld world)
    {
        var loop = new SimLoop(world, Array.Empty<ISimSystem>());
        System.Text.Json.Nodes.JsonObject document = Bclone.Sim.Persistence.SaveGame.Capture(loop, "test", "2026-10-17 12:00", "test");
        return Bclone.Sim.Persistence.SaveGame.Load(
            System.Text.Json.Nodes.JsonNode.Parse(Bclone.Sim.Persistence.SaveFile.TextOf(document))!.AsObject(), world.Config).World;
    }

    /// <summary>Free grass tiles near the barn the brush may give it, <paramref name="apart"/> tiles apart in a row.</summary>
    private static GridPos[] FreeGrass(SimWorld world, Workplace barn, int count, int apart, IReadOnlyList<GridPos>? awayFrom = null)
    {
        var found = new List<GridPos>();
        for (int reach = 3; reach < 30 && found.Count < count; reach++)
        {
            for (int dx = -reach; dx <= reach && found.Count < count; dx++)
            {
                var at = new GridPos(barn.Tile.X + dx, barn.Tile.Y + reach);
                bool clear = world.Map.Contains(at) && world.Map.TerrainAt(at) == Terrain.Grass
                    && world.Zones.WorkGroundOwner(at) == 0 && world.CanPaintWorkGround(barn, at).Allowed
                    && (awayFrom is null || awayFrom.All(t => Math.Abs(t.X - at.X) + Math.Abs(t.Y - at.Y) > 2))
                    && found.All(f => Math.Abs(f.X - at.X) + Math.Abs(f.Y - at.Y) >= apart);
                if (clear)
                {
                    found.Add(at);
                }
            }
        }

        Assert.Equal(count, found.Count);
        return found.ToArray();
    }
}
