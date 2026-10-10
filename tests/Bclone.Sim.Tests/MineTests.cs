using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The iron mine — painted iron that never runs out (`specs/iron-mine.md`, D449). The quarry's twin
/// on an iron seam, unlocked by the smith's first iron tool.
/// </summary>
public sealed class MineTests
{
    private readonly ITestOutputHelper _output;

    public MineTests(ITestOutputHelper output) => _output = output;

    private static SimWorld AVillage() =>
        SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink()).World;

    // ---------------------------------------------------------------
    //  § The unlock — the smith's first iron tool (§3.4)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ No mine until the smith has worked iron — and the refusal says what to do (§3.4).
    /// </summary>
    [Fact]
    public void NoMineBeforeTheSmithHasWorkedIron()
    {
        SimWorld world = AVillage();
        GridPos site = SomewhereItFits(world, BuildingKind.Mine, TheNearestReachable(world, Terrain.IronDeposit));

        PlacementVerdict early = world.Mark(BuildingKind.Mine, site);
        _output.WriteLine($"before: {early.Reason}");
        Assert.False(early.Allowed, "A mine was marked before the smith had forged any iron.");
        Assert.Contains("iron tools", early.Reason, StringComparison.Ordinal);
        Assert.False(world.IsUnlocked(BuildingKind.Mine));

        // ⛔ Iron dug by hand is not iron worked: the smithy's own unlock does not open the mine.
        world.IronEverDug = 400;
        Assert.False(world.IsUnlocked(BuildingKind.Mine), "Digging iron by hand taught the village to mine.");

        world.IronToolsEverForged = world.Config.MineUnlockIronTools;
        Assert.True(world.IsUnlocked(BuildingKind.Mine));
        Assert.True(world.Mark(BuildingKind.Mine, site).Allowed);
    }

    /// <summary>
    /// ⭐⭐ The smith's first iron tool teaches the village to mine, once — a stop, not a log line
    /// (D442) — and stone tools teach it nothing (§3.4).
    /// </summary>
    [Fact]
    public void TheFirstIronToolTeachesTheVillageToMineOnce()
    {
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace smithy = ToolsTests.RaiseASmithy(world);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        ToolsTests.OnlyASmithWorks(world);
        StoreBuilding warehouse = world.StoreBuildings.First(s => s.Accepts(Goods.Iron) && s.Accepts(Goods.Stone));
        warehouse.Store.Add(Goods.Stone, 40);
        warehouse.Store.Add(Goods.Logs, 40);
        world.Moments.Clear();

        // Stone tools first — the smithy's default. Forged, and the mine stays unknown.
        for (int tick = 0; tick < world.Config.TicksPerSeason && world.ToolsEverForged < 2; tick++)
        {
            loop.StepOnce();
        }

        Assert.True(world.ToolsEverForged > 0, "The smith forged no stone tools, so the stone arm tested nothing.");
        Assert.Equal(0, world.IronToolsEverForged);
        Assert.False(world.IsUnlocked(BuildingKind.Mine), "Stone tools taught the village to mine.");
        Assert.Empty(world.Moments);

        // Then iron, set on the card.
        Assert.True(world.SetForgeGood(smithy, Goods.IronTools).Allowed);
        warehouse.Store.Add(Goods.Iron, 40);
        for (int tick = 0; tick < world.Config.TicksPerSeason * 2 && world.IronToolsEverForged < 3; tick++)
        {
            loop.StepOnce();
        }

        _output.WriteLine($"{world.ToolsEverForged} tools forged, {world.IronToolsEverForged} of them iron");
        Assert.True(world.IronToolsEverForged >= 2, "The smith forged fewer than two iron tools, so 'once' was not tested.");
        Assert.True(world.IsUnlocked(BuildingKind.Mine));

        Moment learned = Assert.Single(world.Moments);
        _output.WriteLine($"{learned.Title}: {learned.Body}");
        Assert.True(learned.WaitsToBeDismissed, "Learning to mine was a passing banner, not a stop.");
        Assert.Contains("mine", learned.Body, StringComparison.Ordinal);
    }

    /// <summary>⭐ The iron tools forged are in the fingerprint — the mine's unlock reads them (§3.4).</summary>
    [Fact]
    public void IronToolsForgedAreInTheFingerprint()
    {
        SimWorld none = AVillage();
        SimWorld forged = AVillage();
        ulong before = StateHash.Compute(none);

        forged.IronToolsEverForged = 1;
        ulong one = StateHash.Compute(forged);
        Assert.NotEqual(before, one);

        forged.IronToolsEverForged = 2;
        Assert.NotEqual(one, StateHash.Compute(forged));
    }

    /// <summary>⭐ The tree's mine node reads the mine's gate — the map and `Mark` cannot disagree.</summary>
    [Fact]
    public void TheTreesMineNodeReadsTheMinesGate()
    {
        SimWorld world = AVillage();
        TechNodeRow mine = world.Config.TechNodeRows.Single(n => n.Id == "mine");
        Assert.Equal(BuildingKind.Mine, mine.Unlocks);

        world.IronEverDug = world.Config.SmithyUnlockIron;
        foreach (int forged in new[] { 0, world.Config.MineUnlockIronTools, 9 })
        {
            world.IronToolsEverForged = forged;
            (TechState state, int progress, int of) = TechTree.StateOf(world, mine);
            _output.WriteLine($"{forged} forged: {state} {progress}/{of}");

            Assert.Equal(world.IsUnlocked(BuildingKind.Mine), state == TechState.Known);
            Assert.Equal(forged, progress);
            Assert.Equal(world.Config.MineUnlockIronTools, of);
        }
    }

    // ---------------------------------------------------------------
    //  § The mine itself (§3.1–§3.3, §3.5)
    // ---------------------------------------------------------------

    /// <summary>⭐ Only an iron seam takes a mine's paint, and the refusal is in words (§3.1).</summary>
    [Fact]
    public void AMinePaintsIronAndRefusesOtherGroundInWords()
    {
        SimWorld world = AVillage();
        GridPos iron = TheNearestReachable(world, Terrain.IronDeposit);
        Workplace mine = RaiseAMine(world, iron);

        Assert.True(world.CanPaintWorkGround(mine, iron).Allowed, "An iron seam refused a mine's paint.");

        foreach (Terrain other in new[] { Terrain.Rock, Terrain.Grass })
        {
            PlacementVerdict refused = world.CanPaintWorkGround(mine, FirstOf(world, other));
            _output.WriteLine(refused.Reason);
            Assert.False(refused.Allowed, $"A mine took paint on {other}.");
            Assert.Contains("iron seam", refused.Reason, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// ⭐⭐ A mine face never runs out, and the iron reaches a store — the §6 rig counts what was
    /// stored against the ticks a miner spent at it (trap 136: count events, not ticks ÷ ticks).
    /// </summary>
    [Fact]
    public void AMineDigsIronAndItsFacesStayIron()
    {
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace mine = RaiseAMine(world, TheNearestReachable(world, Terrain.IronDeposit));
        List<GridPos> faces = GiveItIron(world, mine, world.Config.MineTilesPerWorker);
        OnlyAMinerWorks(world);
        int storedBefore = world.InStores(Goods.Iron);

        int workTicks = 0;
        for (int tick = 0; tick < world.Config.TicksPerSeason * 2; tick++)
        {
            loop.StepOnce();
            foreach (Villager v in world.Villagers)
            {
                if (v.WorkplaceId == mine.Id
                    && v.State is VillagerState.TravelingToMine or VillagerState.Mining or VillagerState.HaulingToStore)
                {
                    workTicks++;
                }
            }
        }

        int stored = world.InStores(Goods.Iron) - storedBefore;
        _output.WriteLine(
            $"{stored} iron stored, {workTicks} miner-ticks — "
            + $"{(workTicks > 0 ? stored * 100 / workTicks : 0)} iron per 100 ticks worked; "
            + $"iron ever dug by hand {world.IronEverDug}");

        Assert.True(stored > 0, "A staffed mine on painted iron put no iron in a store.");
        Assert.All(faces, f => Assert.Equal(Terrain.IronDeposit, world.Map.TerrainAt(f)));
        Assert.Equal(0, world.IronEverDug);
    }

    /// <summary>⛔ A laborer never clears a mine's face, nor may the brush mark one (§3.1).</summary>
    [Fact]
    public void ALaborerNeverClearsAMinesFace()
    {
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;
        GridPos iron = TheNearestReachable(world, Terrain.IronDeposit);

        Assert.True(world.PaintHarvest(iron).Allowed);
        Workplace mine = RaiseAMine(world, iron);
        Assert.True(world.PaintWorkGround(mine, iron).Allowed);

        PlacementVerdict again = world.CanPaintHarvest(iron);
        _output.WriteLine(again.Reason);
        Assert.False(again.Allowed, "The harvest brush may mark a mine's face.");
        Assert.Contains("iron mine", again.Reason, StringComparison.Ordinal);

        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, 0);
        }

        for (int tick = 0; tick < world.Config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
        }

        Assert.Equal(Terrain.IronDeposit, world.Map.TerrainAt(iron));
    }

    /// <summary>⛔ A met iron limit stops the digging, and the note says why (D139, §3.3).</summary>
    [Fact]
    public void AMetIronLimitStopsTheMiners()
    {
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace mine = RaiseAMine(world, TheNearestReachable(world, Terrain.IronDeposit));
        GiveItIron(world, mine, world.Config.MineTilesPerWorker);
        OnlyAMinerWorks(world);
        Assert.True(world.SetStockLimit(Goods.Iron, 1).Allowed);
        world.StoreBuildings.First(s => s.Accepts(Goods.Iron)).Store.Add(Goods.Iron, 5);

        int digging = 0;
        for (int tick = 0; tick < world.Config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            digging += world.Villagers.Count(v => v.State == VillagerState.Mining);
        }

        string? why = world.WhyTheFaceIsIdle(mine);
        _output.WriteLine(why ?? "(no reason)");
        Assert.Equal(0, digging);
        Assert.NotNull(why);
        Assert.Contains("iron", why!, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⭐ The village wants its mine's seats while it wants iron and the mine has iron painted — and
    /// not otherwise (§3.3). Posed on the want itself, for the quarry's reason (D437).
    /// </summary>
    [Fact]
    public void MinersAreWantedWhileTheVillageWantsIron()
    {
        SimWorld world = AVillage();
        Workplace mine = RaiseAMine(world, TheNearestReachable(world, Terrain.IronDeposit));

        Assert.Equal(0, LabourQuota.MinersWanted(world));

        GiveItIron(world, mine, world.Config.MineTilesPerWorker);
        Assert.Equal(mine.Capacity, LabourQuota.MinersWanted(world));
        Assert.Equal(0, LabourQuota.QuarriersWanted(world));

        Assert.True(world.SetStockLimit(Goods.Iron, 1).Allowed);
        world.StoreBuildings.First(s => s.Accepts(Goods.Iron)).Store.Add(Goods.Iron, 5);
        Assert.Equal(0, LabourQuota.MinersWanted(world));
    }

    /// <summary>⭐ The brush warns when it would mark the last iron the village can walk to (§3.5).</summary>
    [Fact]
    public void TheLastIronTheVillageCanReachIsWarnedAbout()
    {
        SimWorld world = AVillage();
        var reachable = new List<GridPos>();
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            GridPos at = world.Zones.PositionOf(i);
            if (world.Map.Tiles[i] == Terrain.IronDeposit
                && world.TravelCost.Cost(at, world.Map.FoundingSite) != TravelCostField.Unreachable)
            {
                reachable.Add(at);
            }
        }

        Assert.True(reachable.Count > 2, "This valley has almost no iron to test with.");

        PlacementVerdict first = world.PaintHarvest(reachable[0]);
        Assert.False(first.HasWarning, $"Marking one iron of {reachable.Count} warned: {first.Warning}");

        for (int i = 1; i < reachable.Count - 1; i++)
        {
            world.PaintHarvest(reachable[i]);
        }

        PlacementVerdict last = world.PaintHarvest(reachable[^1]);
        _output.WriteLine($"{reachable.Count} reachable iron tiles; the last says: {last.Warning}");

        Assert.True(last.Allowed, "The last iron was refused — it should only be warned about.");
        Assert.True(last.HasWarning, "Marking the last iron the village can reach said nothing.");
        Assert.Contains("iron mine", last.Warning, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  Fixtures
    // ---------------------------------------------------------------

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

    private static GridPos TheNearestReachable(SimWorld world, Terrain terrain)
    {
        GridPos from = world.Map.FoundingSite;
        GridPos? best = null;
        int cheapest = int.MaxValue;
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            if (world.Map.Tiles[i] != terrain)
            {
                continue;
            }

            GridPos at = world.Zones.PositionOf(i);
            int cost = world.TravelCost.Cost(at, from);
            if (cost != TravelCostField.Unreachable && cost < cheapest)
            {
                cheapest = cost;
                best = at;
            }
        }

        Assert.NotNull(best);
        return best!.Value;
    }

    private static GridPos SomewhereItFits(SimWorld world, BuildingKind kind, GridPos near)
    {
        for (int radius = 1; radius < 20; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var tile = new GridPos(near.X + dx, near.Y + dy);
                    if (world.CanBuildAt(kind, tile).Allowed)
                    {
                        return tile;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"Nowhere a {kind} fits near {near}.");
    }

    /// <summary>A finished mine beside this iron — the village is lent the iron tool it takes to learn.</summary>
    private static Workplace RaiseAMine(SimWorld world, GridPos iron)
    {
        world.IronToolsEverForged = Math.Max(world.IronToolsEverForged, world.Config.MineUnlockIronTools);
        Assert.True(world.Mark(BuildingKind.Mine, SomewhereItFits(world, BuildingKind.Mine, iron)).Allowed);
        Workplace plan = world.Workplaces.Single(w => w.Construction?.Kind == BuildingKind.Mine);
        BuildFixtures.StockTheSite(plan);
        for (int i = 0; i <= plan.Construction!.Recipe.WorkTicks; i++)
        {
            plan.Construction.Work();
        }

        world.Complete(plan);
        world.IronToolsEverForged = 0;
        return world.Workplaces.Single(w => w.Kind == JobKind.Miner && !w.IsSite);
    }

    /// <summary>Paint the mine the iron tiles nearest it that the village can reach.</summary>
    private static List<GridPos> GiveItIron(SimWorld world, Workplace mine, int howMany)
    {
        var iron = new List<(int Cost, GridPos At)>();
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            GridPos at = world.Zones.PositionOf(i);
            if (world.Map.Tiles[i] == Terrain.IronDeposit)
            {
                // ⚠️ ONLY IRON THE MINE MAY PAINT (D540/D541): within `face_reach_tiles` of it.
                int cost = world.TravelCost.Cost(at, mine.Tile);
                if (cost != TravelCostField.Unreachable && world.CanPaintWorkGround(mine, at).Allowed)
                {
                    iron.Add((cost, at));
                }
            }
        }

        var given = iron.OrderBy(r => r.Cost).ThenBy(r => r.At.Y).ThenBy(r => r.At.X)
            .Take(howMany).Select(r => r.At).ToList();
        foreach (GridPos at in given)
        {
            Assert.True(world.PaintWorkGround(mine, at).Allowed);
        }

        return given;
    }

    /// <summary>One miner and nobody else, fed and warm, so the only trade at work is the one watched.</summary>
    private static void OnlyAMinerWorks(SimWorld world)
    {
        foreach (Household household in world.Households)
        {
            household.Stockpile.Add(Goods.Produce, world.TargetFoodFor(household));
            household.Stockpile.Add(Goods.Firewood, VillageEconomy.FirewoodStoreWantedPerHousehold(world.Config));
        }

        Assert.True(world.SetStockLimit(Goods.Produce, 1).Allowed);
        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, kind == JobKind.Miner ? 1 : 0);
        }
    }
}
