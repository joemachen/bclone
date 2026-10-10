using System.Text.Json.Nodes;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Persistence;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ Bridges — a deck over the water, one tile wide, dragged from one bank to the other in a straight line at
/// any angle (D40, D546, D549, `specs/bridges.md §10`).
/// </summary>
/// <remarks>
/// Posed on the shipped valley, whose river runs west to east between the founding and a far bank that
/// holds about two-fifths of the land (D547). The unlocks are set to nothing unless a guard is about
/// them, so a bridge can be marked on the first tick.
/// </remarks>
public sealed class BridgeTests
{
    private readonly ITestOutputHelper _output;

    public BridgeTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Open => ShippedConfig.Load() with
    {
        BridgeUnlockPopulation = 0,
        StoneBridgeUnlockCrossings = 0,
    };

    private static SimLoop AValley(SimConfig config) => SimFactory.CreatePhase0(config, new InMemoryLogSink());

    /// <summary>A drag a player could make: from a bank tile's centre to another's, over the river.</summary>
    internal readonly record struct Crossing(Point From, Point To);

    /// <summary>
    /// The nearest crossing to the founding the sim would allow: the bank north of a water tile to the bank south
    /// of the water <paramref name="slant"/> tiles along — 0 is square across the west–east river, 2 an angle.
    /// </summary>
    internal static Crossing ACrossing(SimWorld world, int slant = 0, BuildingKind kind = BuildingKind.Bridge)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 1; radius < 80; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                    {
                        continue;
                    }

                    var water = new GridPos(site.X + dx, site.Y + dy);
                    if (!world.Map.Contains(water) || world.Map.TerrainAt(water) != Terrain.Water)
                    {
                        continue;
                    }

                    GridPos north = water;
                    while (world.Map.Contains(north) && world.Map.TerrainAt(north) == Terrain.Water)
                    {
                        north = new GridPos(north.X, north.Y - 1);
                    }

                    GridPos south = new(water.X + slant, water.Y);
                    while (world.Map.Contains(south) && world.Map.TerrainAt(south) == Terrain.Water)
                    {
                        south = new GridPos(south.X, south.Y + 1);
                    }

                    if (!world.Map.Contains(north) || !world.Map.Contains(south) || north == south)
                    {
                        continue;
                    }

                    var crossing = new Crossing(Point.CentreOf(north), Point.CentreOf(south));
                    if (world.BridgeLineFrom(kind, crossing.From, crossing.To).Allowed)
                    {
                        return crossing;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"No crossing at slant {slant} anywhere near the founding.");
    }

    /// <summary>Drag a bridge, stock its site, work it and finish it.</summary>
    internal static Bridge RaiseABridge(SimWorld world, Crossing crossing, BuildingKind kind = BuildingKind.Bridge)
    {
        PlacementVerdict verdict = world.MarkBridge(kind, crossing.From, crossing.To);
        Assert.True(verdict.Allowed, verdict.Reason);
        Workplace site = world.Workplaces.Single(w => w.Construction is { BridgeTo: not null });
        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        world.Complete(site);
        return world.BridgeAt(crossing.From.ToTile()) ?? throw new Xunit.Sdk.XunitException("The bridge did not stand.");
    }

    /// <summary>The water under a bridge.</summary>
    private static List<GridPos> DeckOf(SimWorld world, Bridge bridge) =>
        bridge.Tiles().Where(t => world.Map.TerrainAt(t) == Terrain.Water).ToList();

    /// <summary>Every tile the village can walk to today, from where it lives.</summary>
    private static HashSet<GridPos> Reached(SimWorld world)
    {
        GridPos village = world.FirstHomeOrFoundingSite();
        var reached = new HashSet<GridPos>();
        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (world.IsWalkable(at) && !world.StandsOn(at) && world.TravelCost.CanReach(village, at))
                {
                    reached.Add(at);
                }
            }
        }

        return reached;
    }

    /// <summary>A buildable tile across the river from the village, nearest the bridge's far end.</summary>
    private static GridPos AcrossTheRiver(SimWorld world, HashSet<GridPos> before, Bridge bridge) =>
        Reached(world).Where(t => !before.Contains(t))
            .OrderBy(t => t.ManhattanDistanceTo(bridge.Far.ToTile()))
            .First(t => world.CanBuildAt(BuildingKind.Well, t).Allowed);

    /// <summary>Whether a set of tiles is one piece when tiles join only along an edge — what a villager can walk.</summary>
    private static bool EdgeJoined(IReadOnlyList<GridPos> tiles)
    {
        var all = new HashSet<GridPos>(tiles);
        var seen = new HashSet<GridPos> { tiles[0] };
        var queue = new Queue<GridPos>(seen);
        while (queue.Count > 0)
        {
            GridPos t = queue.Dequeue();
            foreach (GridPos n in new[] { new GridPos(t.X + 1, t.Y), new GridPos(t.X - 1, t.Y), new GridPos(t.X, t.Y + 1), new GridPos(t.X, t.Y - 1) })
            {
                if (all.Contains(n) && seen.Add(n))
                {
                    queue.Enqueue(n);
                }
            }
        }

        return seen.Count == all.Count;
    }

    // ---------------------------------------------------------------
    //  §10.1–2 — the deck opens the far bank, and nothing before it does
    // ---------------------------------------------------------------

    /// <summary>⭐ A finished wooden bridge makes the far bank a walk away — and until it stands, nothing does.</summary>
    [Fact]
    public void AFinishedBridgeOpensTheFarBank()
    {
        SimWorld world = AValley(Open).World;
        GridPos village = world.FirstHomeOrFoundingSite();
        Crossing crossing = ACrossing(world);
        SimWorld.BridgeLine line = world.BridgeLineFrom(BuildingKind.Bridge, crossing.From, crossing.To);

        Assert.True(world.TravelCost.CanReach(village, line.Near.ToTile()));
        Assert.False(world.TravelCost.CanReach(village, line.Far.ToTile()));
        int reachedBefore = Reached(world).Count;
        ulong hashBefore = StateHash.Compute(world);

        Bridge bridge = RaiseABridge(world, crossing);
        Assert.NotEqual(hashBefore, StateHash.Compute(world));

        Assert.Equal(line.Near, bridge.Near);
        Assert.Equal(line.Far, bridge.Far);
        Assert.True(world.TravelCost.CanReach(village, bridge.Far.ToTile()));
        Assert.All(DeckOf(world, bridge), t => Assert.Equal(world.Config.BridgeWoodTileCost, world.DeckOn(t)));
        int reachedAfter = Reached(world).Count;
        _output.WriteLine($"length {bridge.Length} at {bridge.Near}→{bridge.Far}; reached {reachedBefore} → {reachedAfter} tiles");
        Assert.True(reachedAfter > reachedBefore + 1000, $"reached {reachedBefore} → {reachedAfter}");

        // Square across, the cost is the walk end to end at grass's price.
        Assert.Equal(bridge.Length * TravelCostField.BaseTileCost, world.TravelCost.Cost(bridge.Near.ToTile(), bridge.Far.ToTile()));
    }

    /// <summary>A building on the far bank is refused for want of a route before the bridge, and allowed after.</summary>
    [Fact]
    public void TheFarBankIsBuildableOnlyOnceTheBridgeStands()
    {
        SimWorld world = AValley(Open).World;
        HashSet<GridPos> before = Reached(world);
        Bridge bridge = RaiseABridge(world, ACrossing(world));
        GridPos across = AcrossTheRiver(world, before, bridge);

        Assert.True(world.Demolish(bridge).Allowed);
        PlacementVerdict refused = world.CanBuildAt(BuildingKind.Well, across);
        Assert.False(refused.Allowed);
        Assert.Equal("There is no route to there from the village.", refused.Reason);
    }

    // ---------------------------------------------------------------
    //  §10.3 — nobody on undecked water, over a bridge at an angle
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ Nobody ever stands on water a deck does not cover — every tick of two years in which a bridge laid at
    /// an angle is crossed to build on the far bank. The regression D41 exists to prevent, on D549's new ground.
    /// </summary>
    [Fact]
    public void NobodyStandsOnUndeckedWater()
    {
        SimLoop loop = AValley(Open);
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        HashSet<GridPos> before = Reached(world);
        Bridge bridge = RaiseABridge(world, ACrossing(world, slant: 2));
        Assert.NotEqual(bridge.Near.X, bridge.Far.X);
        Assert.True(world.Mark(BuildingKind.Well, AcrossTheRiver(world, before, bridge)).Allowed);

        int onTheDeck = 0;
        for (int t = 0; t < world.Config.TicksPerYear * 2; t++)
        {
            loop.StepOnce();
            foreach (Villager v in world.Villagers.Where(v => v.Alive))
            {
                GridPos under = v.Position.ToTile();
                Assert.True(world.IsWalkable(under), $"{v.Name} stood on water at {under}, tick {world.Tick}.");
                if (world.DeckOn(under) != 0)
                {
                    onTheDeck++;
                }
            }

            if (t == world.Config.TicksPerYear && world.Wells.Count == 1)
            {
                // The well across needs it, so pulling it down is refused.
                Assert.False(world.Demolish(bridge).Allowed);
            }
        }

        _output.WriteLine($"crossings {world.BridgeCrossings}, villager-ticks on the deck {onTheDeck}, wells {world.Wells.Count}");
        Assert.True(world.BridgeCrossings > 0, "Nobody ever crossed — the guard would pass on a world with no bridge.");
        Assert.True(onTheDeck > 0);
    }

    // ---------------------------------------------------------------
    //  §10.4 — the drag's rule
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ A line at any angle lies on tiles joined edge to edge — so the 4-connected cost field can walk it — and
    /// that holds through an exact grid corner, D412's trap.
    /// </summary>
    [Fact]
    public void ALineAtAnyAngleIsOnePieceToWalk()
    {
        Assert.True(EdgeJoined(Bridge.TilesUnder(Point.CentreOf(new GridPos(0, 0)), Point.CentreOf(new GridPos(4, 4)))));
        Assert.True(EdgeJoined(Bridge.TilesUnder(Point.CentreOf(new GridPos(0, 0)), Point.CentreOf(new GridPos(3, 7)))));
        Assert.True(EdgeJoined(Bridge.TilesUnder(new Point(Fixed.FromRatio(1, 3), Fixed.Zero), new Point(Fixed.FromInt(5), Fixed.FromRatio(17, 4)))));

        SimWorld world = AValley(Open).World;
        Bridge bridge = RaiseABridge(world, ACrossing(world, slant: 2));
        Assert.True(EdgeJoined(bridge.Tiles()));

        // Lengths are rounded up, exactly: 3–4–5 is 5; a diagonal of two is 2.83 → 3.
        Assert.Equal(5, Bridge.LengthOf(Point.CentreOf(new GridPos(0, 0)), Point.CentreOf(new GridPos(3, 4))));
        Assert.Equal(3, Bridge.LengthOf(Point.CentreOf(new GridPos(0, 0)), Point.CentreOf(new GridPos(2, 2))));
    }

    /// <summary>Every refusal says what is wrong — and the near end is the village's whichever way it was dragged.</summary>
    [Fact]
    public void TheDragSaysWhyNot()
    {
        SimWorld world = AValley(Open).World;
        Crossing crossing = ACrossing(world);
        SimWorld.BridgeLine line = world.BridgeLineFrom(BuildingKind.Bridge, crossing.From, crossing.To);
        GridPos water = line.Near.ToTile().Y < line.Far.ToTile().Y
            ? new GridPos(line.Near.ToTile().X, line.Near.ToTile().Y + 1)
            : new GridPos(line.Near.ToTile().X, line.Near.ToTile().Y - 1);
        Assert.Equal(Terrain.Water, world.Map.TerrainAt(water));

        Assert.Equal("A bridge begins and ends on dry land.",
            world.CanBridge(BuildingKind.Bridge, line.Near, Point.CentreOf(water)).Reason);
        Assert.Equal("Press on one bank and drag to the other.",
            world.CanBridge(BuildingKind.Bridge, line.Near, line.Near).Reason);

        GridPos land = line.Near.ToTile();
        var alongTheBank = new GridPos(land.X + 1, land.Y);
        Assert.NotEqual(Terrain.Water, world.Map.TerrainAt(alongTheBank));
        Assert.Equal("A bridge has to cross water — drag from one bank to the other.",
            world.CanBridge(BuildingKind.Bridge, line.Near, Point.CentreOf(alongTheBank)).Reason);

        SimWorld shorter = AValley(Open with { BridgeMaxLengthTiles = line.Length - 1 }).World;
        Assert.Equal($"That is {line.Length} tiles end to end — a bridge can be {line.Length - 1} at most.",
            shorter.CanBridge(BuildingKind.Bridge, crossing.From, crossing.To).Reason);

        // Dragged the other way, the builders still stand on the village's side.
        SimWorld.BridgeLine back = world.BridgeLineFrom(BuildingKind.Bridge, crossing.To, crossing.From);
        Assert.True(back.Allowed);
        Assert.Equal(line.Near, back.Near);
        Assert.Equal(line.Far, back.Far);
    }

    /// <summary>
    /// Nothing may stand under the line, and a bridge may not cross another.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>A line with neither end reachable is not posed</b>: the shipped valley has one river, so every
    /// crossing has the village on one side. The rule is <c>BridgeLineFrom</c>'s sentence, shared with every
    /// building (*"There is no route to there from the village."*), and stays unguarded here — written down.
    /// </remarks>
    [Fact]
    public void NothingStandsUnderTheLineAndOneBridgeCrossesNoOther()
    {
        SimWorld world = AValley(Open).World;
        Crossing crossing = ACrossing(world);
        SimWorld.BridgeLine line = world.BridgeLineFrom(BuildingKind.Bridge, crossing.From, crossing.To);

        Assert.True(world.Mark(BuildingKind.Well, line.Near.ToTile()).Allowed);
        PlacementVerdict onTheWell = world.CanBridge(BuildingKind.Bridge, crossing.From, crossing.To);
        Assert.False(onTheWell.Allowed);
        Assert.StartsWith("It would run into", onTheWell.Reason, StringComparison.Ordinal);

        SimWorld other = AValley(Open).World;
        Crossing first = ACrossing(other);
        RaiseABridge(other, first);
        Assert.Equal("It would cross another bridge.", other.CanBridge(BuildingKind.Bridge, first.From, first.To).Reason);
    }

    // ---------------------------------------------------------------
    //  §10.5–6 — a site routes nobody; nothing stands on its ground
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ A site is not a crossing: the far bank stays cut off until it is finished, its builders stand on the
    /// near bank, that bank stays walkable while they do — and it is priced by its length end to end.
    /// </summary>
    [Fact]
    public void ASiteRoutesNobodyAcrossAndIsPricedByItsLength()
    {
        SimWorld world = AValley(Open).World;
        Crossing crossing = ACrossing(world, slant: 2);
        Assert.True(world.MarkBridge(BuildingKind.Bridge, crossing.From, crossing.To).Allowed);
        Workplace site = world.Workplaces.Single(w => w.Construction is { BridgeTo: not null });
        Point far = site.Construction!.BridgeTo!.Value;

        Assert.False(world.TravelCost.CanReach(world.FirstHomeOrFoundingSite(), far.ToTile()));
        Assert.All(Bridge.TilesUnder(site.Position, far), t => Assert.Equal(0, world.DeckOn(t)));
        Assert.False(world.StandsOn(site.Tile), "The site closed the bank it is built from.");
        Assert.True(world.TravelCost.CanReach(world.FirstHomeOrFoundingSite(), site.Tile));
        Assert.Same(site, world.SiteAt(site.Tile));

        int length = Bridge.LengthOf(site.Position, far);
        Assert.Equal(world.Config.BridgeLogsPerTile * length, site.Construction.Recipe.Of(Goods.Logs));
        Assert.Equal(world.Config.BridgeWorkTicksPerTile * length, site.Construction.Recipe.WorkTicks);
        _output.WriteLine($"an angled bridge {length} tiles end to end, {site.Construction.Recipe.Of(Goods.Logs)} logs");
    }

    /// <summary>A bridge on a wooded bank is built all the same — its ground is walkable, so it never waits on clearing.</summary>
    [Fact]
    public void ABridgeOnAWoodedBankIsNotHeldForClearing()
    {
        SimWorld world = AValley(Open).World;
        Crossing crossing = ACrossing(world);
        Assert.True(world.MarkBridge(BuildingKind.Bridge, crossing.From, crossing.To).Allowed);
        Workplace site = world.Workplaces.Single(w => w.Construction is { BridgeTo: not null });
        world.SetTerrain(site.Tile, Terrain.Forest);

        Assert.True(world.FootprintIsClear(site), "a bridge's builders waited on a tree nobody will fell");
        Assert.True(world.IsWalkable(site.Tile));
    }

    /// <summary>Nothing may be built on a bridge's ground, planned or standing — it would close the crossing.</summary>
    [Fact]
    public void NothingStandsOnABridgesEnd()
    {
        SimWorld world = AValley(Open).World;
        Crossing crossing = ACrossing(world);
        Assert.True(world.MarkBridge(BuildingKind.Bridge, crossing.From, crossing.To).Allowed);
        Workplace site = world.Workplaces.Single(w => w.Construction is { BridgeTo: not null });

        Assert.True(world.SomethingStandsAt(site.Tile));
        Assert.False(world.CanBuildAt(BuildingKind.Well, site.Tile).Allowed);

        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        world.Complete(site);
        Bridge bridge = world.Bridges.Single();
        Assert.True(world.SomethingStandsAt(bridge.Near.ToTile()));
        Assert.True(world.SomethingStandsAt(bridge.Far.ToTile()));
        Assert.False(world.CanBuildAt(BuildingKind.Well, bridge.Far.ToTile()).Allowed);
        Assert.False(world.StandsOn(bridge.Near.ToTile()), "A bridge's end must stay walkable.");
    }

    // ---------------------------------------------------------------
    //  §10.7–8 — the two unlocks
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ The village learns to bridge when it has held enough people, says so, and does not forget when it
    /// shrinks — a save and a load re-derive it from the villagers' own history.
    /// </summary>
    [Fact]
    public void TheVillageLearnsToBridgeByItsSizeAndKeepsIt()
    {
        SimLoop loop = AValley(ShippedConfig.Load() with { BridgeUnlockPopulation = 5 });
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);

        Assert.False(world.IsUnlocked(BuildingKind.Bridge));
        Assert.Contains("has held 4 people", world.WhyNotYet(BuildingKind.Bridge), StringComparison.Ordinal);
        bool shownBefore = world.ShownTheTechTree;

        for (int t = 0; t < world.Config.TicksPerYear * 15 && world.MostEverAlive < 5; t++)
        {
            loop.StepOnce();
            if (!world.ShownTheTechTree)
            {
                shownBefore = false;
            }
        }

        Assert.Equal(5, world.MostEverAlive);
        Assert.True(world.IsUnlocked(BuildingKind.Bridge));
        Assert.Contains(world.Moments, m => m.Title == "The village learned to bridge water");

        // ⚠️ Learned by size, not by hands: it does not introduce the tech-tree map (D440's rule).
        Assert.Equal(shownBefore, world.ShownTheTechTree);

        // The village shrinks; it still knows how.
        foreach (Villager v in world.Villagers.Where(v => v.Alive).Take(3).ToList())
        {
            v.Alive = false;
            v.DiedAtTick = world.Tick;
        }

        Assert.True(world.Population < 5);
        SimWorld loaded = SaveGame.Load(
            JsonNode.Parse(SaveFile.TextOf(SaveGame.Capture(loop, "test", "now", "code")))!.AsObject(), world.Config).World;
        Assert.Equal(5, loaded.MostEverAlive);
        Assert.True(loaded.IsUnlocked(BuildingKind.Bridge));
    }

    /// <summary>⭐ Crossings are counted on the bridge and the village, and teach the stone bridge at their number.</summary>
    [Fact]
    public void CrossingsTeachTheStoneBridge()
    {
        SimLoop loop = AValley(Open with { StoneBridgeUnlockCrossings = 3 });
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        Assert.False(world.IsUnlocked(BuildingKind.StoneBridge));
        Assert.Contains("0 of the 3", world.WhyNotYet(BuildingKind.StoneBridge), StringComparison.Ordinal);

        HashSet<GridPos> before = Reached(world);
        Bridge bridge = RaiseABridge(world, ACrossing(world));
        Assert.True(world.Mark(BuildingKind.Well, AcrossTheRiver(world, before, bridge)).Allowed);

        for (int t = 0; t < world.Config.TicksPerYear * 2 && world.BridgeCrossings < 3; t++)
        {
            loop.StepOnce();
        }

        Assert.True(world.BridgeCrossings >= 3, $"only {world.BridgeCrossings} crossings in two years");
        Assert.Equal(world.BridgeCrossings, bridge.Crossings);
        Assert.True(world.IsUnlocked(BuildingKind.StoneBridge));
        Assert.Contains(world.Moments, m => m.Title == "The village learned to set a span in stone");
        Assert.True(world.ShownTheTechTree, "Learned with the feet — it is learned by doing, and shows the tree.");
    }

    /// <summary>
    /// ⭐ One walk across a bridge at an angle is one crossing, walked straight along it — counted where a step
    /// goes from land onto the deck, not per deck tile — and the deck takes no wear while the banks do.
    /// </summary>
    [Fact]
    public void OneWalkAcrossIsOneStraightCrossingAndTheDeckTakesNoWear()
    {
        SimWorld world = AValley(Open).World;
        Bridge bridge = RaiseABridge(world, ACrossing(world, slant: 2));
        Assert.True(DeckOf(world, bridge).Count >= 2, "a one-tile deck cannot tell a crossing from a step");

        Villager walker = world.Villagers.First(v => v.Alive);
        walker.StandAt(bridge.Near);
        GridPos far = bridge.Far.ToTile();
        double ax = bridge.Near.X.RawBits, ay = bridge.Near.Y.RawBits;
        double bx = bridge.Far.X.RawBits - ax, by = bridge.Far.Y.RawBits - ay;
        double furthest = 0;
        int steps = 0;
        while (steps < 100 && !(walker.Tile == far && walker.LegTicks == Fixed.Zero))
        {
            BehaviorSystem.TravelForTest(world, walker, far);
            double px = walker.Position.X.RawBits - ax, py = walker.Position.Y.RawBits - ay;
            furthest = Math.Max(furthest, Math.Abs((px * by) - (py * bx)) / Math.Sqrt((bx * bx) + (by * by)) / (1L << 32));
            steps++;
        }

        _output.WriteLine($"{steps} steps; furthest from the bridge's line {furthest:F3} tiles");
        Assert.Equal(far, walker.Tile);
        Assert.Equal(1, world.BridgeCrossings);
        Assert.Equal(1, bridge.Crossings);
        Assert.True(furthest < 0.5, $"the walk strayed {furthest:F2} tiles off the line — it did not go straight across");
        Assert.All(DeckOf(world, bridge), t => Assert.Equal(0, world.Paths.At(t)));
        Assert.True(world.Paths.At(far) > 0, "the far bank was stepped on and wore");
    }

    // ---------------------------------------------------------------
    //  §10.9 — stone: a click on a wooden bridge, open throughout
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ A click on a wooden bridge with the stone one in hand rebuilds it in stone (Joe, D549) — it carries the
    /// village across the whole time (D546), and the stone crossing is quicker. A drag elsewhere is a new one.
    /// </summary>
    [Fact]
    public void AClickRebuildsInStoneOpenThroughoutAndQuicker()
    {
        SimWorld world = AValley(Open).World;
        Bridge bridge = RaiseABridge(world, ACrossing(world));
        GridPos village = world.FirstHomeOrFoundingSite();
        int woodenCost = world.TravelCost.Cost(bridge.Near.ToTile(), bridge.Far.ToTile());
        Point onTheDeck = Point.CentreOf(DeckOf(world, bridge)[0]);

        Assert.True(world.MarkBridge(BuildingKind.StoneBridge, onTheDeck, onTheDeck).Allowed);
        Workplace site = world.Workplaces.Single(w => w.Construction is { BridgeTo: not null });
        Assert.True(site.Construction!.RebuildsBridge);
        Assert.Equal(world.Config.StoneBridgeStonePerTile * bridge.Length, site.Construction.Recipe.Of(Goods.Stone));

        // Open throughout: the deck is wood, the far bank a walk, the near bank walkable.
        Assert.All(DeckOf(world, bridge), t => Assert.Equal(world.Config.BridgeWoodTileCost, world.DeckOn(t)));
        Assert.True(world.TravelCost.CanReach(village, bridge.Far.ToTile()));
        Assert.False(world.StandsOn(bridge.Near.ToTile()));
        Assert.Equal("That bridge is being rebuilt already.",
            world.CanBuildAt(BuildingKind.StoneBridge, onTheDeck).Reason);
        Assert.Equal("It is being rebuilt — pull the site down first.", world.WhyNotPullDown(bridge));

        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        world.Complete(site);

        Assert.Same(bridge, world.Bridges.Single());
        Assert.Equal(BuildingKind.StoneBridge, bridge.Kind);
        Assert.All(DeckOf(world, bridge), t => Assert.Equal(world.Config.BridgeStoneTileCost, world.DeckOn(t)));
        int stoneCost = world.TravelCost.Cost(bridge.Near.ToTile(), bridge.Far.ToTile());
        _output.WriteLine($"length {bridge.Length}: wood {woodenCost} → stone {stoneCost}");
        Assert.True(stoneCost < woodenCost, $"stone {stoneCost} is not quicker than wood {woodenCost}");

        // A stone drag somewhere else is a new stone bridge, not a rebuild.
        Crossing elsewhere = ACrossing(world, slant: 2, kind: BuildingKind.StoneBridge);
        Assert.True(world.MarkBridge(BuildingKind.StoneBridge, elsewhere.From, elsewhere.To).Allowed);
        Assert.False(world.Workplaces.Single(w => w.Construction is { BridgeTo: not null }).Construction!.RebuildsBridge);
    }

    // ---------------------------------------------------------------
    //  §10.10 — pulling one down
    // ---------------------------------------------------------------

    /// <summary>Refused with somebody on the deck; allowed when nothing across needs it, and the far bank is cut off again.</summary>
    [Fact]
    public void PullingABridgeDownIsRefusedWhileItIsNeeded()
    {
        SimWorld world = AValley(Open).World;
        Bridge bridge = RaiseABridge(world, ACrossing(world));
        GridPos deck = DeckOf(world, bridge)[0];

        Villager walker = world.Villagers.First(v => v.Alive);
        Point was = walker.Position;
        walker.WalkTo(Point.CentreOf(deck));
        Assert.Equal($"{walker.Name} is on the bridge.", world.WhyNotPullDown(bridge));
        walker.WalkTo(was);

        Assert.True(world.Demolish(bridge).Allowed);
        Assert.Empty(world.Bridges);
        Assert.Equal(0, world.DeckOn(deck));
        Assert.False(world.TravelCost.CanReach(world.FirstHomeOrFoundingSite(), bridge.Far.ToTile()));
    }

    // ---------------------------------------------------------------
    //  §10.11 — saved, and the year after
    // ---------------------------------------------------------------

    /// <summary>A village with a bridge at an angle and a stone rebuild under way saves and loads to the same hash, and a year on is still the same village.</summary>
    [Fact]
    public void ABridgeIsSavedAndTheVillageRunsOnTheSame()
    {
        SimLoop loop = AValley(Open);
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        HashSet<GridPos> before = Reached(world);
        Bridge bridge = RaiseABridge(world, ACrossing(world, slant: 2));
        Assert.True(world.Mark(BuildingKind.Well, AcrossTheRiver(world, before, bridge)).Allowed);
        Point onTheDeck = Point.CentreOf(DeckOf(world, bridge)[0]);
        Assert.True(world.MarkBridge(BuildingKind.StoneBridge, onTheDeck, onTheDeck).Allowed);
        loop.Step(world.Config.TicksPerYear / 2);

        SimLoop loaded = SaveGame.Load(
            JsonNode.Parse(SaveFile.TextOf(SaveGame.Capture(loop, "test", "now", "code")))!.AsObject(), world.Config);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded.World));

        loop.Step(world.Config.TicksPerYear);
        loaded.Step(world.Config.TicksPerYear);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded.World));
        Assert.Equal(world.BridgeCrossings, loaded.World.BridgeCrossings);
        _output.WriteLine($"crossings {world.BridgeCrossings}, bridge {world.Bridges.Single().Kind}");
    }

    /// <summary>What a deck is made of is in the hash — it decides what a crossing costs (D546).</summary>
    [Fact]
    public void ABridgesKindIsInTheHash()
    {
        SimWorld world = AValley(Open).World;
        Bridge bridge = RaiseABridge(world, ACrossing(world));
        ulong wooden = StateHash.Compute(world);
        bridge.Kind = BuildingKind.StoneBridge;
        Assert.NotEqual(wooden, StateHash.Compute(world));
    }
}
