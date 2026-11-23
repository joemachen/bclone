using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Logging;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

/// <summary>
/// ⭐ A deck is boarded and left only at its two ends (D858). Joe, playing Millbrook: <i>"villagers "Stairstep" across the
/// diagonal wooden bridge."</i> Diagnosed by the art lane and confirmed on a copy of his save (44 crossings, 38 staircased, all
/// on wooden bridge 1): its bank runs alongside the deck, the routing let a walker step onto any deck tile from any land tile
/// beside it, so walkers boarded and left mid-span — and D705's end-to-end aim (<c>AimAlongTheDeck</c>) fires only for a route
/// that passes an end, so every leg went tile centre to tile centre, up to 0.7 of a tile off the deck.
/// </summary>
/// <remarks>
/// The rule is WALLS (<c>SimWorld.DeckWallsOn</c>, derived with the deck index): every edge between a water deck tile and a tile
/// its bridge does not hold is walled (a bridge's two end tiles are dry land and its own, so the bank meets it only there), on both sides — so the routing, the leg (<c>LineOfSight</c>) and
/// every step's wall guard read it with no new reader, the way doors (D623) are walls on a building's other edges.
/// </remarks>
public sealed class BridgeBoardingTests
{
    private readonly ITestOutputHelper _output;

    public BridgeBoardingTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Open => ShippedConfig.Load() with
    {
        BridgeUnlockPopulation = 0,
        StoneBridgeUnlockCrossings = 0,
    };

    private static readonly (int X, int Y)[] Steps = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    /// <summary>
    /// ⭐ The rule itself: every side of a mid-span deck tile that faces ground its bridge does not hold is walled, from both
    /// sides; an end tile's sides are open.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void AMidSpanDeckIsWalledOnItsSides(int slant)
    {
        SimWorld world = SimFactory.CreatePhase0(Open, new InMemoryLogSink()).World;
        Bridge bridge = BridgeTests.RaiseABridge(world, BridgeTests.ACrossing(world, slant));
        GridPos near = bridge.Near.ToTile();
        GridPos far = bridge.Far.ToTile();

        int walled = 0;
        foreach (GridPos deck in BridgeTests.DeckOf(world, bridge))
        {
            foreach ((int dx, int dy) in Steps)
            {
                var beside = new GridPos(deck.X + dx, deck.Y + dy);
                if (bridge.Holds(beside))
                {
                    Assert.Equal(0, world.WallsOn(deck) & ZoneMap.EdgeBit(dx, dy));
                    continue;
                }

                bool end = deck == near || deck == far;
                Assert.Equal(!end, (world.WallsOn(deck) & ZoneMap.EdgeBit(dx, dy)) != 0);
                Assert.Equal(!end, (world.WallsOn(beside) & ZoneMap.EdgeBit(-dx, -dy)) != 0);
                walled += end ? 0 : 1;
            }
        }

        _output.WriteLine($"slant {slant}: {BridgeTests.DeckOf(world, bridge).Count} deck tiles, {walled} sides walled");
        Assert.True(walled > 0, "no side of the deck faced unheld ground, so the rule asked nothing");
    }

    /// <summary>
    /// ⭐ The walk: from every land tile beside a mid-span deck tile — the bank running alongside, Joe's bridge 1 — a route
    /// across steps onto the deck only at an end. Posed by turning the water along one side of the deck into bank, which is
    /// what a slanted bridge's river does where it bends.
    /// </summary>
    [Fact]
    public void ABankAlongsideTheDeckBoardsItAtAnEnd()
    {
        SimWorld world = SimFactory.CreatePhase0(Open, new InMemoryLogSink()).World;
        Bridge bridge = BridgeTests.RaiseABridge(world, BridgeTests.ACrossing(world, slant: 2));
        GridPos near = bridge.Near.ToTile();
        GridPos far = bridge.Far.ToTile();

        // Every walkable tile, not the bridge's, beside a mid-span deck tile — and the far bank's walkable tile beyond the far end.
        var alongside = new List<GridPos>();
        foreach (GridPos deck in BridgeTests.DeckOf(world, bridge).Where(t => t != near && t != far))
        {
            foreach ((int dx, int dy) in Steps)
            {
                var beside = new GridPos(deck.X + dx, deck.Y + dy);
                if (!bridge.Holds(beside) && world.Map.Contains(beside) && world.IsWalkable(beside) && !alongside.Contains(beside))
                {
                    alongside.Add(beside);
                }
            }
        }

        // ⚠️ A square bridge over a straight river has no bank alongside its deck; if the posed one has none either, make one:
        // the water beside the deck's mid tiles, on the near side, becomes bank — Joe's bridge 1 exactly.
        if (alongside.Count == 0)
        {
            foreach (GridPos deck in BridgeTests.DeckOf(world, bridge).Where(t => t != near && t != far))
            {
                var beside = new GridPos(deck.X + 1, deck.Y);
                if (!bridge.Holds(beside) && world.Map.TerrainAt(beside) == Terrain.Water)
                {
                    world.SetTerrain(beside, Terrain.Grass);
                    alongside.Add(beside);
                }
            }

            world.StandingChanged();
        }

        Assert.NotEmpty(alongside);
        GridPos across = Steps.Select(s => new GridPos(far.X + s.X, far.Y + s.Y))
            .First(t => !bridge.Holds(t) && world.IsWalkable(t));

        int boardedMidSpan = 0;
        foreach (GridPos start in alongside)
        {
            if (!world.TravelCost.CanReach(start, across))
            {
                continue;
            }

            GridPos at = start;
            for (int step = 0; step < 200 && at != across; step++)
            {
                GridPos next = world.TravelCost.StepToward(at, across);
                if (!bridge.Holds(at) && bridge.Holds(next) && world.DeckOn(next) != 0 && next != near && next != far)
                {
                    boardedMidSpan++;
                    _output.WriteLine($"from ({start.X},{start.Y}): stepped onto the deck at ({next.X},{next.Y}), mid-span");
                }

                at = next;
            }
        }

        _output.WriteLine($"{alongside.Count} tiles alongside the deck; boarded mid-span {boardedMidSpan} times");
        Assert.Equal(0, boardedMidSpan);
    }
}
