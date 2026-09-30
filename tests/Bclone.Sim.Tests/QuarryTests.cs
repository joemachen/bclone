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

    // ---------------------------------------------------------------
    //  § The quarry itself (part 3, `quarry.md §3.3–§3.7`)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ No quarry until the village has dug its stone by hand — and the refusal says so (§3.3).
    /// </summary>
    [Fact]
    public void NoQuarryBeforeTheVillageHasDugTheStone()
    {
        SimWorld world = AVillage();
        GridPos site = SomewhereAQuarryFits(world, TheNearestReachableRock(world));

        PlacementVerdict early = world.Mark(BuildingKind.Quarry, site);
        _output.WriteLine($"before: {early.Reason}");
        Assert.False(early.Allowed, "A quarry was marked before the village had dug any stone.");
        Assert.Contains("stone", early.Reason, StringComparison.Ordinal);
        Assert.False(world.IsUnlocked(BuildingKind.Quarry));

        world.StoneEverDug = world.Config.QuarryUnlockStone - 1;
        Assert.False(world.Mark(BuildingKind.Quarry, site).Allowed, "One stone short and it was allowed.");

        world.StoneEverDug = world.Config.QuarryUnlockStone;
        Assert.True(world.IsUnlocked(BuildingKind.Quarry));
        Assert.True(world.Mark(BuildingKind.Quarry, site).Allowed);
    }

    /// <summary>⭐ Only rock takes a quarry's paint, and the refusal is in words (§3.4).</summary>
    [Fact]
    public void AQuarryPaintsRockAndRefusesOtherGroundInWords()
    {
        SimWorld world = AVillage();
        GridPos rock = TheNearestReachableRock(world);
        Workplace quarry = RaiseAQuarry(world, rock);

        Assert.True(world.CanPaintWorkGround(quarry, rock).Allowed, "Rock refused a quarry's paint.");

        GridPos grass = FirstOf(world, Terrain.Grass);
        PlacementVerdict refused = world.CanPaintWorkGround(quarry, grass);
        _output.WriteLine(refused.Reason);
        Assert.False(refused.Allowed, "A quarry took paint on grass.");
        Assert.Contains("rock", refused.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⭐⭐ A face never runs out, and the stone reaches a store (§3.5) — the rig counts what was
    /// stored against the ticks a quarrier spent at it (trap 136: count events, not ticks ÷ ticks).
    /// </summary>
    [Fact]
    public void AQuarryCutsStoneAndItsFacesStayRock()
    {
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace quarry = RaiseAQuarry(world, TheNearestReachableRock(world));
        List<GridPos> faces = GiveItRock(world, quarry, 6);
        OnlyAQuarrierWorks(world);
        int storedBefore = world.InStores(Goods.Stone);

        int trips = 0;
        int workTicks = 0;
        for (int tick = 0; tick < world.Config.TicksPerSeason * 2; tick++)
        {
            bool wasCutting = world.Villagers.Any(v => v.State == VillagerState.Quarrying);
            loop.StepOnce();
            foreach (Villager v in world.Villagers)
            {
                if (v.WorkplaceId == quarry.Id
                    && v.State is VillagerState.TravelingToQuarry or VillagerState.Quarrying
                        or VillagerState.HaulingToStore)
                {
                    workTicks++;
                }
            }

            if (wasCutting && !world.Villagers.Any(v => v.State == VillagerState.Quarrying))
            {
                trips++;
            }
        }

        int stored = world.InStores(Goods.Stone) - storedBefore;
        _output.WriteLine(
            $"{trips} stints, {stored} stone stored, {workTicks} quarrier-ticks — "
            + $"{(workTicks > 0 ? stored * 100 / workTicks : 0)} stone per 100 ticks worked; "
            + $"stone ever dug by hand {world.StoneEverDug}");

        Assert.True(stored > 0, "A staffed quarry on painted rock put no stone in a store.");
        Assert.All(faces, f => Assert.Equal(Terrain.Rock, world.Map.TerrainAt(f)));
        Assert.Equal(0, world.StoneEverDug);
    }

    /// <summary>⛔ A laborer never clears a quarry's face, nor may the brush mark one (§3.4).</summary>
    [Fact]
    public void ALaborerNeverClearsAQuarrysFace()
    {
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;
        GridPos rock = TheNearestReachableRock(world);

        // Marked for clearing FIRST, then claimed by the quarry — the order a player might paint in.
        Assert.True(world.PaintHarvest(rock).Allowed);
        Workplace quarry = RaiseAQuarry(world, rock);
        Assert.True(world.PaintWorkGround(quarry, rock).Allowed);

        PlacementVerdict again = world.CanPaintHarvest(rock);
        _output.WriteLine(again.Reason);
        Assert.False(again.Allowed, "The harvest brush may mark a quarry's face.");

        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, 0);
        }

        for (int tick = 0; tick < world.Config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
        }

        Assert.Equal(Terrain.Rock, world.Map.TerrainAt(rock));
    }

    /// <summary>⛔ A met stone limit stops the cutting, and the note says why (D139, §3.6).</summary>
    [Fact]
    public void AMetStoneLimitStopsTheQuarriers()
    {
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace quarry = RaiseAQuarry(world, TheNearestReachableRock(world));
        GiveItRock(world, quarry, 6);
        OnlyAQuarrierWorks(world);
        Assert.True(world.SetStockLimit(Goods.Stone, 1).Allowed);
        world.StoreBuildings.First(s => s.Accepts(Goods.Stone)).Store.Add(Goods.Stone, 5);

        int cutting = 0;
        for (int tick = 0; tick < world.Config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            cutting += world.Villagers.Count(v => v.State == VillagerState.Quarrying);
        }

        string? why = world.WhyTheQuarryIsIdle(quarry);
        _output.WriteLine(why ?? "(no reason)");
        Assert.Equal(0, cutting);
        Assert.NotNull(why);
        Assert.Contains("stone", why!, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⭐ The village wants its quarry's seats while it wants stone and the quarry has rock — and
    /// not otherwise (§3.6).
    /// </summary>
    /// <remarks>
    /// ⚠️ Posed on the want itself because the behaviour guards above cannot see it: they set a
    /// job limit, and the player's number staffs a trade whatever the village would have asked
    /// for (D51). Red-checked: a want of nought scored zero against every guard above (D437).
    /// </remarks>
    [Fact]
    public void QuarriersAreWantedWhileTheVillageWantsStone()
    {
        SimWorld world = AVillage();
        Workplace quarry = RaiseAQuarry(world, TheNearestReachableRock(world));

        Assert.Equal(0, LabourQuota.QuarriersWanted(world));

        GiveItRock(world, quarry, 6);
        Assert.Equal(quarry.Capacity, LabourQuota.QuarriersWanted(world));

        Assert.True(world.SetStockLimit(Goods.Stone, 1).Allowed);
        world.StoreBuildings.First(s => s.Accepts(Goods.Stone)).Store.Add(Goods.Stone, 5);
        Assert.Equal(0, LabourQuota.QuarriersWanted(world));
    }

    private static GridPos TheNearestReachableRock(SimWorld world)
    {
        GridPos from = world.Map.FoundingSite;
        GridPos? best = null;
        int cheapest = int.MaxValue;
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            if (world.Map.Tiles[i] != Terrain.Rock)
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

    private static GridPos SomewhereAQuarryFits(SimWorld world, GridPos near)
    {
        for (int radius = 1; radius < 20; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var tile = new GridPos(near.X + dx, near.Y + dy);
                    if (world.CanBuildAt(BuildingKind.Quarry, tile).Allowed)
                    {
                        return tile;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"Nowhere a quarry fits near {near}.");
    }

    /// <summary>A finished quarry beside this rock — the village is lent the stone it takes to learn.</summary>
    private static Workplace RaiseAQuarry(SimWorld world, GridPos rock)
    {
        world.StoneEverDug = Math.Max(world.StoneEverDug, world.Config.QuarryUnlockStone);
        Assert.True(world.Mark(BuildingKind.Quarry, SomewhereAQuarryFits(world, rock)).Allowed);
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

    /// <summary>Paint the quarry the rock tiles nearest it that the village can reach.</summary>
    private static List<GridPos> GiveItRock(SimWorld world, Workplace quarry, int howMany)
    {
        var rock = new List<(int Cost, GridPos At)>();
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            GridPos at = world.Zones.PositionOf(i);
            if (world.Map.Tiles[i] == Terrain.Rock)
            {
                int cost = world.TravelCost.Cost(at, quarry.Tile);
                if (cost != TravelCostField.Unreachable)
                {
                    rock.Add((cost, at));
                }
            }
        }

        var given = rock.OrderBy(r => r.Cost).ThenBy(r => r.At.Y).ThenBy(r => r.At.X)
            .Take(howMany).Select(r => r.At).ToList();
        foreach (GridPos at in given)
        {
            Assert.True(world.PaintWorkGround(quarry, at).Allowed);
        }

        return given;
    }

    /// <summary>One quarrier and nobody else, fed and warm, so the only trade at work is the one watched.</summary>
    private static void OnlyAQuarrierWorks(SimWorld world)
    {
        foreach (Household household in world.Households)
        {
            household.Stockpile.Add(Goods.Produce, world.TargetFoodFor(household));
            household.Stockpile.Add(Goods.Firewood, VillageEconomy.FirewoodStoreWantedPerHousehold(world.Config));
        }

        Assert.True(world.SetStockLimit(Goods.Produce, 1).Allowed);
        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, kind == JobKind.Quarrier ? 1 : 0);
        }
    }
}
