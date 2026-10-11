using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The wooden bridge slows, then closes — <c>specs/building-condition.md §4.4</c> (Joe, D546 call 6; D555): a failing
/// deck's leg takes longer and the route does not change; a ruined deck is not walkable, never closed with anybody on
/// it and never when it is the only way somewhere; stone never wears.
/// </summary>
public sealed class BridgeConditionTests
{
    private readonly ITestOutputHelper _output;

    public BridgeConditionTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Open => ShippedConfig.Load() with { BridgeUnlockPopulation = 0, StoneBridgeUnlockCrossings = 0 };

    /// <summary>A valley thirteen years on, so a bridge can be stamped twelve or twenty years old.</summary>
    private static (SimLoop Loop, Bridge Bridge) AnOldBridge(int yearsOld, BuildingKind kind = BuildingKind.Bridge)
    {
        SimLoop loop = SimFactory.CreatePhase0(Open, new InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        loop.Step((world.Config.TicksPerYear * (yearsOld + 1)) + 3);
        Bridge bridge = BridgeTests.RaiseABridge(world, BridgeTests.ACrossing(world, kind: kind), kind);
        bridge.RaisedAtTick = world.Tick - ((ulong)yearsOld * (ulong)world.Config.TicksPerYear);
        world.StandingChanged();
        return (loop, bridge);
    }

    /// <summary>Step through the next season's first tick — the one the walk runs on — and no further.</summary>
    private static void StepToTheSeasonTurn(SimLoop loop)
    {
        int season = loop.World.Config.TicksPerSeason;
        int toBoundary = (season - (int)(loop.World.Tick % (ulong)season)) % season;
        loop.Step(toBoundary + 1);
    }

    [Fact]
    public void AFailingDeckSlowsTheWalkAndNotTheRoute()
    {
        (SimLoop loop, Bridge bridge) = AnOldBridge(Open.BuildingFailsInYears);
        SimWorld world = loop.World;
        bridge.RaisedAtTick = world.Tick;
        world.StandingChanged();
        IReadOnlyList<GridPos> tiles = bridge.Tiles();
        int deckTiles = BridgeTests.DeckOf(world, bridge).Count;
        GridPos near = bridge.Near.ToTile(), far = bridge.Far.ToTile();
        int routeBefore = world.TravelCost.Cost(near, far);
        (int walkedBefore, int grassBefore) = BehaviorSystem.WalkedOver(world, tiles, tiles.Count - 1);
        Assert.Equal(Condition.Sound, world.ConditionOf(bridge));
        Assert.All(BridgeTests.DeckOf(world, bridge), t => Assert.False(world.DeckIsSlow(t)));

        bridge.RaisedAtTick = world.Tick - ((ulong)world.Config.BuildingFailsInYears * (ulong)world.Config.TicksPerYear);
        world.StandingChanged();
        Assert.Equal(Condition.Failing, world.ConditionOf(bridge));
        Assert.All(BridgeTests.DeckOf(world, bridge), t => Assert.True(world.DeckIsSlow(t)));
        (int walkedAfter, int grassAfter) = BehaviorSystem.WalkedOver(world, tiles, tiles.Count - 1);

        int deckCost = world.BuildingsCatalog[BuildingKind.Bridge]!.DeckTileCost;
        int extra = deckTiles * ((deckCost * world.Config.FailingDeckSlowPercent / 100) - deckCost);
        Assert.Equal(walkedBefore + extra, walkedAfter);
        Assert.Equal(grassBefore, grassAfter);
        Assert.Equal(routeBefore, world.TravelCost.Cost(near, far));
        Assert.True(world.IsWalkable(BridgeTests.DeckOf(world, bridge)[0]));
        _output.WriteLine($"{deckTiles} deck tiles: leg {walkedBefore} -> {walkedAfter}, route {routeBefore} both times");
    }

    [Fact]
    public void ARuinedDeckClosesAtTheSeasonsTurnAndNobodyStandsOnIt()
    {
        (SimLoop loop, Bridge bridge) = AnOldBridge(Open.BuildingRuinedInYears);
        SimWorld world = loop.World;
        List<GridPos> deck = BridgeTests.DeckOf(world, bridge);
        Assert.Equal(Condition.Ruin, world.ConditionOf(bridge));
        Assert.False(bridge.Closed);
        Assert.True(world.IsWalkable(deck[0]), "a ruin stays open until the season's turn says so");

        StepToTheSeasonTurn(loop);
        Assert.True(bridge.Closed, world.WhyNotClose(bridge) ?? "not closed, and no reason given");
        Assert.All(deck, t => Assert.False(world.IsWalkable(t)));
        Assert.NotNull(world.RepairSiteAt(bridge.Near.ToTile()));

        for (int t = 0; t < world.Config.TicksPerSeason; t++)
        {
            loop.StepOnce();
            foreach (Villager v in world.Villagers)
            {
                if (v.Alive) { Assert.DoesNotContain(v.Position.ToTile(), deck); }
            }
        }
    }

    [Fact]
    public void ARuinedDeckIsNotClosedWithSomebodyOnIt()
    {
        (SimLoop loop, Bridge bridge) = AnOldBridge(Open.BuildingRuinedInYears);
        SimWorld world = loop.World;
        List<GridPos> deck = BridgeTests.DeckOf(world, bridge);
        Villager somebody = world.Villagers.First(v => v.Alive);

        // Up to the tick before the turn, then onto the deck: the clock walks first in the turn's tick, before anybody moves.
        int season = world.Config.TicksPerSeason;
        loop.Step((season - (int)(world.Tick % (ulong)season)) % season);
        somebody.Position = Point.CentreOf(deck[0]);
        Assert.Contains("is on the bridge", world.WhyNotClose(bridge)!, StringComparison.Ordinal);
        loop.Step(1);

        Assert.False(bridge.Closed);
        Assert.True(world.IsWalkable(deck[0]));
        _output.WriteLine($"{somebody.Name} stood on the deck at the turn; the bridge stayed open");

        // Off the deck, and the next turn closes it — unless the builders mended it in the meantime, which they may: the
        // first turn raised its repair site, and a shipped opening has the logs.
        somebody.Position = Point.CentreOf(world.Map.FoundingSite);
        StepToTheSeasonTurn(loop);
        Assert.True(bridge.Closed || bridge.RepairedAtTick > 0 || world.WhyNotClose(bridge) is not null);
        _output.WriteLine(bridge.Closed ? "closed at the next turn" : $"mended at {bridge.RepairedAtTick} before the next turn");
    }

    [Fact]
    public void ARuinedDeckIsNotClosedWhenItIsTheOnlyWaySomewhere()
    {
        (SimLoop loop, Bridge bridge) = AnOldBridge(Open.BuildingRuinedInYears);
        SimWorld world = loop.World;
        HashSet<GridPos> withBridge = BridgeTests.Reached(world);
        bridge.Closed = true;
        world.StandingChanged();
        HashSet<GridPos> without = BridgeTests.Reached(world);
        bridge.Closed = false;
        world.StandingChanged();
        // Open grass first: asking `CanBuildAt` of every reached tile across the river took this guard 46 s.
        GridPos across = BridgeTests.Reached(world).Where(t => !without.Contains(t) && world.Map.TerrainAt(t) == Terrain.Grass && !world.StandsOn(t))
            .OrderBy(t => t.ManhattanDistanceTo(bridge.Far.ToTile()))
            .First(t => world.CanBuildAt(BuildingKind.Well, t).Allowed);
        Assert.True(world.Mark(BuildingKind.Well, across).Allowed);
        Workplace site = world.Workplaces.Single(w => w.Construction?.Kind == BuildingKind.Well);
        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++) { site.Construction.Work(); }
        world.Complete(site);
        Assert.True(withBridge.Count > without.Count);

        string? why = world.WhyNotClose(bridge);
        Assert.NotNull(why);
        Assert.Contains("cut off", why, StringComparison.Ordinal);
        StepToTheSeasonTurn(loop);
        Assert.False(bridge.Closed);
        Assert.True(world.IsWalkable(BridgeTests.DeckOf(world, bridge)[0]));
        _output.WriteLine(why);
    }

    [Fact]
    public void AMendedBridgeOpensAgain()
    {
        (SimLoop loop, Bridge bridge) = AnOldBridge(Open.BuildingRuinedInYears);
        SimWorld world = loop.World;
        StepToTheSeasonTurn(loop);
        Assert.True(bridge.Closed);
        Workplace site = world.RepairSiteAt(bridge.Near.ToTile())!;
        Assert.Equal(0, site.Construction!.Recipe.Of(Goods.Stone));
        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++) { site.Construction.Work(); }
        world.Complete(site);

        Assert.False(bridge.Closed);
        Assert.Equal(world.Tick, bridge.RepairedAtTick);
        Assert.Equal(Condition.Sound, world.ConditionOf(bridge));
        Assert.True(world.IsWalkable(BridgeTests.DeckOf(world, bridge)[0]));
        Assert.Null(world.RepairSiteAt(bridge.Near.ToTile()));
    }

    [Fact]
    public void StoneNeverWears()
    {
        (SimLoop loop, Bridge bridge) = AnOldBridge(Open.BuildingRuinedInYears, BuildingKind.StoneBridge);
        SimWorld world = loop.World;
        bridge.RaisedAtTick = 0;
        Assert.Equal(Condition.Sound, world.ConditionOf(bridge));
        StepToTheSeasonTurn(loop);
        Assert.False(bridge.Closed);
        Assert.Null(world.RepairSiteAt(bridge.Near.ToTile()));
    }
}
