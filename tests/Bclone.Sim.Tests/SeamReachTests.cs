using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// A quarry or a mine stands within reach of its seam, and paints only the seam within that reach —
/// <c>specs/seam-reach.md</c> (D539, D540, D541; Joe: *"same way fishing hut is for water"*, *"4 tiles"*, then
/// *"update from 4 tiles distance to 2 tiles. i want it even closer."*).
/// </summary>
/// <remarks>
/// <para>
/// Every "is this site otherwise fine?" is asked of the same valley with the rule switched off
/// (<c>face_reach_tiles</c> 0), so a refusal here can only be the rule's.
/// </para>
/// </remarks>
public sealed class SeamReachTests
{
    private readonly ITestOutputHelper _output;

    public SeamReachTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Shipped => ShippedConfig.Load();

    /// <summary>The shipped reach. Every pose reads it; only the pin below states the number.</summary>
    private static int Reach => Shipped.FaceReachTiles;

    /// <summary>
    /// ⚠️ THE RIVER POSES RUN AT A REACH OF 4 (D541). At the shipped 2 no site in this valley has dry ground across the
    /// water within reach — the river is wider than that — so "cut off" at 2 is a walled-in seam far more than a river.
    /// The rule does not read the number to decide reachability, so the river is posed where it can be.
    /// </summary>
    private const int RiverReach = 4;

    private static SimWorld AValley(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

    // ---------------------------------------------------------------
    //  The rows
    // ---------------------------------------------------------------

    [Fact]
    public void AQuarryAndAMineMustStandWithinTwoTilesOfTheirSeam()
    {
        SimConfig shipped = Shipped;
        Assert.Equal(2, shipped.FaceReachTiles);

        SimWorld world = AValley(shipped);
        foreach (BuildingKind kind in new[] { BuildingKind.Quarry, BuildingKind.Mine })
        {
            NearRule? rule = world.BuildingsCatalog[kind]!.MustBeNear;
            Assert.NotNull(rule);
            Assert.Equal(2, rule!.Tiles);

            // The row and the job cannot disagree about which seam it is.
            JobKind trade = world.BuildingsCatalog.EmployedBy(kind)!.Value;
            Assert.Equal(world.JobsCatalog.FaceOf(trade), rule.Terrain);
        }

        // Nothing else carries the rule: the fishery's touch and the lodge's woods decide theirs.
        foreach (BuildingKind kind in Enum.GetValues<BuildingKind>())
        {
            if (kind is not (BuildingKind.Quarry or BuildingKind.Mine) && world.BuildingsCatalog[kind] is BuildingRow row)
            {
                Assert.Null(row.MustBeNear);
            }
        }
    }

    // ---------------------------------------------------------------
    //  Placing
    // ---------------------------------------------------------------

    /// <summary>A quarry beside rock stands; the same quarry with no rock within reach is refused, in words.</summary>
    [Fact]
    public void AQuarryStandsNearRockAndNowhereElse()
    {
        TheBuildingStandsNearItsSeamAndNowhereElse(BuildingKind.Quarry, Terrain.Rock, "rock");
    }

    /// <summary>The same for a mine and iron — and stone beside it is not iron.</summary>
    [Fact]
    public void AMineStandsNearIronAndNowhereElse()
    {
        TheBuildingStandsNearItsSeamAndNowhereElse(BuildingKind.Mine, Terrain.IronDeposit, "iron");

        // Beside rock with no iron within reach: the face is the trade's, not any seam's.
        SimWorld off = AValley(Shipped with { FaceReachTiles = 0 });
        SimWorld on = AValley(Shipped);
        GridPos? site = FindASite(off, BuildingKind.Mine,
            at => SeamWithin(off, at, Terrain.Rock, Reach, reachable: true) && !SeamWithin(off, at, Terrain.IronDeposit, Reach, reachable: false));
        Assert.NotNull(site);
        PlacementVerdict verdict = on.CanBuildAt(BuildingKind.Mine, site!.Value);
        _output.WriteLine($"a mine beside rock with no iron near, at {site}: {verdict.Reason}");
        Assert.False(verdict.Allowed);
        Assert.Contains("iron", verdict.Reason, StringComparison.Ordinal);
    }

    /// <summary>⭐ Rock only across the river is not near — it is unreachable, and the sentence says so (D110).</summary>
    [Fact]
    public void RockAcrossTheRiverDoesNotCount()
    {
        SimWorld off = AValley(Shipped with { FaceReachTiles = 0 });
        SimWorld on = AValley(Shipped with { FaceReachTiles = RiverReach });

        // A site the rule-free valley allows, with no seam of any kind near it and dry ground across the water
        // within reach.
        GridPos? farBank;
        GridPos? site = FindASite(off, BuildingKind.Quarry, at =>
        {
            if (SeamWithin(off, at, Terrain.Rock, RiverReach, reachable: false))
            {
                return false;
            }

            return UnreachableDryGroundWithin(off, at, RiverReach) is not null;
        });
        Assert.NotNull(site);
        farBank = UnreachableDryGroundWithin(off, site!.Value, RiverReach);

        Assert.True(on.SetTerrain(farBank!.Value, Terrain.Rock), $"could not lay rock at {farBank}");
        Assert.False(on.TravelCost.CanReach(on.Map.FoundingSite, farBank.Value), "the posed rock is reachable after all");

        PlacementVerdict verdict = on.CanBuildAt(BuildingKind.Quarry, site!.Value);
        _output.WriteLine($"rock only across the water from {site} (at {farBank}): {verdict.Reason}");
        Assert.False(verdict.Allowed);
        Assert.Contains("across the water", verdict.Reason, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  Painting
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ A quarry paints rock within reach of it and no further — or the rule is a formality (Joe's Q2).
    /// </summary>
    [Fact]
    public void AQuarryPaintsOnlyTheRockWithinItsReach()
    {
        SimWorld world = AValley(Shipped);
        GridPos? site = FindASite(world, BuildingKind.Quarry, at => SeamWithin(world, at, Terrain.Rock, Reach, reachable: true));
        Assert.NotNull(site);
        Workplace quarry = RaiseAQuarryAt(world, site!.Value);
        GridPos at = quarry.Position.ToTile();

        GridPos near = TheNearestReachable(world, Terrain.Rock, at);
        Assert.True(Distance(at, near) <= Reach, $"the quarry at {at} stands {Distance(at, near)} tiles from its nearest rock");
        Assert.True(world.CanPaintWorkGround(quarry, near).Allowed, $"rock {Distance(at, near)} tiles away was refused");

        GridPos? far = null;
        for (int i = 0; i < world.Map.Tiles.Count && far is null; i++)
        {
            GridPos p = world.Zones.PositionOf(i);
            if (world.Map.Tiles[i] == Terrain.Rock && Distance(at, p) > Reach && world.TravelCost.CanReach(world.Map.FoundingSite, p))
            {
                far = p;
            }
        }

        Assert.NotNull(far);
        PlacementVerdict refused = world.CanPaintWorkGround(quarry, far!.Value);
        _output.WriteLine($"rock {Distance(at, far.Value)} tiles from the quarry: {refused.Reason}");
        Assert.False(refused.Allowed);
        Assert.Contains("Too far", refused.Reason, StringComparison.Ordinal);

        // One past the reach is not near. ⚠️ Asserted, not `if`-guarded (D540's trap): a valley with no rock at exactly
        // that distance would have skipped it silently.
        GridPos? onePast = RockAtExactly(world, at, Reach + 1);
        Assert.NotNull(onePast);
        Assert.False(world.CanPaintWorkGround(quarry, onePast!.Value).Allowed, $"rock {Reach + 1} tiles away was allowed");
    }

    /// <summary>Rock within reach but across the water is no face for this quarry.</summary>
    /// <remarks>
    /// Posed, never found: a site beside the river with dry ground across it within reach, a tile of rock laid on the
    /// near bank so a quarry may stand there, and a tile laid on the far bank to paint.
    /// </remarks>
    [Fact]
    public void AQuarryPaintsNoRockAcrossTheWater()
    {
        SimWorld off = AValley(Shipped with { FaceReachTiles = 0 });
        SimWorld world = AValley(Shipped with { FaceReachTiles = RiverReach });
        GridPos? farBank;
        GridPos? nearBank;
        GridPos? site = FindASite(off, BuildingKind.Quarry, at =>
        {
            return UnreachableDryGroundWithin(off, at, RiverReach) is not null && ReachableGrassWithin(off, at, RiverReach) is not null;
        });
        Assert.NotNull(site);
        farBank = UnreachableDryGroundWithin(off, site!.Value, RiverReach);
        nearBank = ReachableGrassWithin(off, site.Value, RiverReach);

        Assert.True(world.SetTerrain(nearBank!.Value, Terrain.Rock));
        Assert.True(world.SetTerrain(farBank!.Value, Terrain.Rock));
        Workplace quarry = RaiseAQuarryAt(world, site!.Value);

        PlacementVerdict near = world.CanPaintWorkGround(quarry, nearBank.Value);
        PlacementVerdict across = world.CanPaintWorkGround(quarry, farBank.Value);
        _output.WriteLine($"quarry at {site}: rock on its bank — {(near.Allowed ? "allowed" : near.Reason)}; across the water — {across.Reason}");
        Assert.True(near.Allowed, near.Reason);
        Assert.False(across.Allowed);
        Assert.Contains("across the water", across.Reason, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  Poses
    // ---------------------------------------------------------------

    private void TheBuildingStandsNearItsSeamAndNowhereElse(BuildingKind kind, Terrain face, string word)
    {
        SimWorld off = AValley(Shipped with { FaceReachTiles = 0 });
        SimWorld on = AValley(Shipped);

        GridPos? near = FindASite(off, kind, at => SeamWithin(off, at, face, Reach, reachable: true));
        Assert.NotNull(near);
        Assert.True(on.CanBuildAt(kind, near!.Value).Allowed, $"a {kind} with {face} within reach was refused");

        GridPos? far = FindASite(off, kind, at => !SeamWithin(off, at, face, Reach, reachable: false));
        Assert.NotNull(far);
        PlacementVerdict verdict = on.CanBuildAt(kind, far!.Value);
        _output.WriteLine($"a {kind} at {far} with no {face} within reach: {verdict.Reason}");
        Assert.False(verdict.Allowed);
        Assert.Contains(word, verdict.Reason, StringComparison.Ordinal);
        Assert.Contains($"{Reach} tiles", verdict.Reason, StringComparison.Ordinal);
    }

    /// <summary>The nearest tile to the founding, by travel, where the rule-free world lets this building stand and the test holds.</summary>
    private static GridPos? FindASite(SimWorld world, BuildingKind kind, Func<GridPos, bool> test)
    {
        GridPos from = world.Map.FoundingSite;
        GridPos? best = null;
        int cheapest = int.MaxValue;
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            GridPos at = world.Zones.PositionOf(i);
            int cost = world.TravelCost.Cost(at, from);
            if (cost == TravelCostField.Unreachable || cost >= cheapest || !test(at) || !world.CanBuildAt(kind, at).Allowed)
            {
                continue;
            }

            cheapest = cost;
            best = at;
        }

        return best;
    }

    private static bool SeamWithin(SimWorld world, GridPos centre, Terrain face, int reach, bool reachable)
    {
        for (int dy = -reach; dy <= reach; dy++)
        {
            int span = reach - Math.Abs(dy);
            for (int dx = -span; dx <= span; dx++)
            {
                var at = new GridPos(centre.X + dx, centre.Y + dy);
                if (world.Map.Contains(at) && world.Map.TerrainAt(at) == face
                    && (!reachable || world.TravelCost.CanReach(world.Map.FoundingSite, at)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static GridPos? UnreachableDryGroundWithin(SimWorld world, GridPos centre, int reach)
    {
        for (int dy = -reach; dy <= reach; dy++)
        {
            int span = reach - Math.Abs(dy);
            for (int dx = -span; dx <= span; dx++)
            {
                var at = new GridPos(centre.X + dx, centre.Y + dy);
                if (world.Map.Contains(at) && world.Map.TerrainAt(at) == Terrain.Grass
                    && !world.TravelCost.CanReach(world.Map.FoundingSite, at))
                {
                    return at;
                }
            }
        }

        return null;
    }

    private static GridPos? RockAtExactly(SimWorld world, GridPos centre, int distance)
    {
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            GridPos at = world.Zones.PositionOf(i);
            if (world.Map.Tiles[i] == Terrain.Rock && Distance(centre, at) == distance
                && world.TravelCost.CanReach(world.Map.FoundingSite, at))
            {
                return at;
            }
        }

        return null;
    }

    private static GridPos TheNearestReachable(SimWorld world, Terrain terrain, GridPos from)
    {
        GridPos? best = null;
        int nearest = int.MaxValue;
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            GridPos at = world.Zones.PositionOf(i);
            if (world.Map.Tiles[i] == terrain && world.TravelCost.CanReach(world.Map.FoundingSite, at) && Distance(from, at) < nearest)
            {
                nearest = Distance(from, at);
                best = at;
            }
        }

        Assert.NotNull(best);
        return best!.Value;
    }

    private static int Distance(GridPos a, GridPos b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static GridPos? ReachableGrassWithin(SimWorld world, GridPos centre, int reach)
    {
        for (int dy = -reach; dy <= reach; dy++)
        {
            int span = reach - Math.Abs(dy);
            for (int dx = -span; dx <= span; dx++)
            {
                var at = new GridPos(centre.X + dx, centre.Y + dy);
                if ((dx != 0 || dy != 0) && world.Map.Contains(at) && world.Map.TerrainAt(at) == Terrain.Grass
                    && world.TravelCost.CanReach(world.Map.FoundingSite, at))
                {
                    return at;
                }
            }
        }

        return null;
    }

    /// <summary>A finished quarry here — the village lent the stone it takes to learn.</summary>
    private static Workplace RaiseAQuarryAt(SimWorld world, GridPos site)
    {
        world.StoneEverDug = Math.Max(world.StoneEverDug, world.Config.QuarryUnlockStone);
        PlacementVerdict marked = world.Mark(BuildingKind.Quarry, site);
        Assert.True(marked.Allowed, $"no quarry at {site}: {marked.Reason}");
        Workplace plan = world.Workplaces.Single(w => w.Construction?.Kind == BuildingKind.Quarry);
        BuildFixtures.StockTheSite(plan);
        for (int i = 0; i <= plan.Construction!.Recipe.WorkTicks; i++)
        {
            plan.Construction.Work();
        }

        world.Complete(plan);
        world.StoneEverDug = 0;
        return world.Workplaces.Single(w => w.Kind == JobKind.Quarrier && !w.IsSite);
    }
}
