using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ A fence is a wall the one cost field routes around, with one gate on the lane (D404,
/// `specs/fences-as-walls.md`).
/// </summary>
/// <remarks>
/// Joe, from his D396 play: *"villagers are definitely walking through other villager's yards —
/// look at all of the packed trail within the yards."* D400 counted it before a line was written:
/// <b>16.2 % of every step in six shipped valleys was inside somebody else's yard.</b> These
/// guards read the wall layer, the field that honours it, the leg that must agree with the field,
/// and the wall-off sweep that must not walk through a fence to call a door reachable.
/// </remarks>
public sealed class FencesAsWallsTests
{
    private readonly ITestOutputHelper _output;

    public FencesAsWallsTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    /// <summary>The fixture village with the founders' houses standing and their fences up.</summary>
    private static SimWorld Founded() => SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;

    /// <summary>Whether the step from one tile to the next crosses a wall — the question every guard asks.</summary>
    private static bool CrossesAWall(ZoneMap zones, GridPos from, GridPos to)
    {
        byte bit = ZoneMap.EdgeBit(to.X - from.X, to.Y - from.Y);
        return bit != 0 && (zones.WallsOn(from) & bit) != 0;
    }

    /// <summary>Every tile of a household's fence that is yard rather than house.</summary>
    private static List<GridPos> YardOf(SimWorld world, Household family)
    {
        PlotShape plot = world.PlotFor(family.HomeTile!.Value, family.HomeFacing, family.Id);
        var yard = new List<GridPos>();
        foreach (GridPos tile in family.FencedTiles)
        {
            if (!plot.House.Contains(tile))
            {
                yard.Add(tile);
            }
        }

        return yard;
    }

    /// <summary>The four unit steps, north first — the wall bits' own order.</summary>
    private static readonly (int Dx, int Dy)[] Steps = { (0, -1), (1, 0), (0, 1), (-1, 0) };

    /// <summary>
    /// ⭐ A walk from just outside a yard to the tile just inside it goes round by the gate — it
    /// never takes the one step across the fence that separates them.
    /// </summary>
    /// <remarks>
    /// Red with <c>TerrainCostField.Scratch.Block</c> leaving the walls out: the route is the two
    /// tiles, straight across the fence.
    /// </remarks>
    [Fact]
    public void AFenceIsAWall()
    {
        SimWorld world = Founded();
        int checkedFences = 0;
        foreach (Household family in world.Households)
        {
            if (family.HomeTile is null)
            {
                continue;
            }

            foreach (GridPos yard in YardOf(world, family))
            {
                foreach ((int dx, int dy) in Steps)
                {
                    // ⚠️ Ground somebody could stand on: a walk that begins on a building steps off
                    // it first (D383), and since D411 a lane may have a building on it beside a
                    // fence (fences §9.3, Joe: allowed) — its route starts past the building.
                    var outside = new GridPos(yard.X + dx, yard.Y + dy);
                    if (!CrossesAWall(world.Zones, outside, yard)
                        || world.SomethingStandsAt(outside)
                        || !world.TravelCost.CanReach(world.Map.FoundingSite, outside))
                    {
                        continue;
                    }

                    List<GridPos> route = world.TravelCost.RouteFrom(outside, yard);
                    Assert.True(route.Count > 2, $"The walk from {outside} to {yard} crossed the {family.Name}s' fence.");
                    for (int i = 1; i < route.Count; i++)
                    {
                        Assert.False(
                            CrossesAWall(world.Zones, route[i - 1], route[i]),
                            $"The walk from {outside} to {yard} stepped across a fence at {route[i - 1]} → {route[i]}.");
                    }

                    checkedFences++;
                }
            }
        }

        _output.WriteLine($"{checkedFences} fence edges walked round, none crossed.");
        Assert.True(checkedFences > 0, "No fence edge had walkable ground outside it, so the guard proved nothing.");
    }

    /// <summary>
    /// ⭐ Every yard has exactly one way in — an open edge onto the lane the house faces — and
    /// the walk from the founding site into the deepest yard tile comes through it.
    /// </summary>
    /// <remarks>
    /// Red with the gate never left open (<c>ZoneMap.FenceEdges</c> walling the lane edge too):
    /// no open edge, and the yard cannot be reached at all.
    /// </remarks>
    [Fact]
    public void AGateIsTheOneWayIn()
    {
        SimWorld world = Founded();
        int yards = 0;
        foreach (Household family in world.Households)
        {
            if (family.HomeTile is null)
            {
                continue;
            }

            List<GridPos> yard = YardOf(world, family);
            var fenced = new HashSet<GridPos>(family.FencedTiles);
            var gates = new List<(GridPos Yard, GridPos Beyond)>();
            foreach (GridPos tile in yard)
            {
                foreach ((int dx, int dy) in Steps)
                {
                    var beyond = new GridPos(tile.X + dx, tile.Y + dy);
                    if (!fenced.Contains(beyond) && !CrossesAWall(world.Zones, tile, beyond))
                    {
                        gates.Add((tile, beyond));
                    }
                }
            }

            _output.WriteLine($"the {family.Name}s: {yard.Count} yard tiles, gates {string.Join(" ", gates)}");
            Assert.Single(gates);
            Assert.True(world.Zones.IsLane(gates[0].Beyond), $"The {family.Name}s' gate opens onto {gates[0].Beyond}, which is not their lane.");

            // The deepest yard tile — furthest from the gate — is reached, and through the gate.
            GridPos deepest = yard[0];
            foreach (GridPos tile in yard)
            {
                if (tile.ManhattanDistanceTo(gates[0].Yard) > deepest.ManhattanDistanceTo(gates[0].Yard))
                {
                    deepest = tile;
                }
            }

            List<GridPos> route = world.TravelCost.RouteFrom(world.Map.FoundingSite, deepest);
            Assert.NotEmpty(route);

            // The route does not list where it starts, and the founding site can be the lane tile
            // in front of the gate.
            route.Insert(0, world.Map.FoundingSite);
            int entries = 0;
            for (int i = 1; i < route.Count; i++)
            {
                if (!fenced.Contains(route[i - 1]) && fenced.Contains(route[i]))
                {
                    entries++;
                    Assert.True(
                        gates[0].Beyond == route[i - 1] && gates[0].Yard == route[i],
                        $"The walk into the {family.Name}s' yard came in {route[i - 1]} → {route[i]}, not through the gate {gates[0]}.");
                }
            }

            Assert.True(entries == 1, $"The walk into the {family.Name}s' yard came in {entries} times: {string.Join(" ", route)}.");
            yards++;
        }

        Assert.True(yards > 0, "No household had a yard, so the guard proved nothing.");
    }

    /// <summary>
    /// ⛔ Nothing is built on the lane in front of a gate — refused in words (Joe, D448; D456).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>§9.3 was <em>allowed</em> (2026-09-26) and Joe reversed it (D448): *refuse it*.</b> A
    /// building there shuts the yard behind it. The gate is found the way <see cref="AGateIsTheOneWayIn"/>
    /// finds it — by the walls, the open edge onto the lane — so the index the refusal reads is
    /// held to the fence itself rather than to its own record.
    /// </para>
    /// <para>
    /// And the index follows the plot: handed on with it to an heir, and gone when it is released.
    /// </para>
    /// </remarks>
    [Fact]
    public void NothingIsBuiltOnTheLaneInFrontOfAGate()
    {
        SimWorld world = Founded();
        Household? posed = null;
        GridPos front = default;

        foreach (Household family in world.Households)
        {
            if (family.HomeTile is null)
            {
                continue;
            }

            var fenced = new HashSet<GridPos>(family.FencedTiles);
            foreach (GridPos tile in YardOf(world, family))
            {
                foreach ((int dx, int dy) in Steps)
                {
                    var beyond = new GridPos(tile.X + dx, tile.Y + dy);
                    if (fenced.Contains(beyond) || CrossesAWall(world.Zones, tile, beyond))
                    {
                        continue;
                    }

                    Assert.Equal(family.Id, world.Zones.GateOwnerFacing(beyond));

                    PlacementVerdict verdict = world.CanBuildAt(BuildingKind.Pile, beyond);
                    _output.WriteLine($"the {family.Name}s' gate opens onto {beyond}: {verdict.Reason}");
                    Assert.False(verdict.Allowed, $"A pile was allowed in front of the {family.Name}s' gate.");
                    if (verdict.Reason.Contains("gate", System.StringComparison.Ordinal))
                    {
                        posed ??= family;
                        front = posed == family ? beyond : front;
                    }
                }
            }
        }

        Assert.True(posed is not null, "No gate's lane was refused for being a gate's, so nothing was posed.");
        Assert.Contains($"the {posed!.Name}s' gate", world.CanBuildAt(BuildingKind.Pile, front).Reason);

        world.Zones.HandPlotOn(posed.Id, 99_999);
        Assert.Equal(99_999, world.Zones.GateOwnerFacing(front));

        world.Zones.ReleasePlot(99_999);
        Assert.Equal(0, world.Zones.GateOwnerFacing(front));
    }

    /// <summary>
    /// ⭐ A straight leg may not cross a fence (§3.4), so the drawing agrees with the routing
    /// (D356's string-pulled leg) — and a leg beside it that crosses none is still clear.
    /// </summary>
    /// <remarks>Red with <c>LineOfSight.Clear</c>'s wall check taken out.</remarks>
    [Fact]
    public void ALegNeverCrossesAFence()
    {
        SimWorld world = Founded();
        foreach (Household family in world.Households)
        {
            if (family.HomeTile is null)
            {
                continue;
            }

            foreach (GridPos yard in YardOf(world, family))
            {
                foreach ((int dx, int dy) in Steps)
                {
                    var outside = new GridPos(yard.X + dx, yard.Y + dy);
                    if (!CrossesAWall(world.Zones, outside, yard) || world.SomethingStandsAt(outside)
                        || !TerrainRules.IsPassable(world.Map.TerrainAt(outside)))
                    {
                        continue;
                    }

                    Point from = Point.CentreOf(outside);
                    Point to = Point.CentreOf(yard);
                    _output.WriteLine($"the {family.Name}s: {outside} → {yard} across the fence");
                    Assert.False(
                        LineOfSight.Clear(world.Map, world, from, to, System.Array.Empty<GridPos>(), System.Array.Empty<GridPos>()),
                        $"A leg from {outside} to {yard} is clear straight across the {family.Name}s' fence.");
                    return;
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No fence with open ground on both sides, so the guard proved nothing.");
    }

    /// <summary>
    /// ⭐⭐ The wall-off sweep reads a standing wall (§3.3): a building on the only way into a
    /// walled square is refused by name, where the old sweep walked through the wall and said fine.
    /// </summary>
    /// <remarks>
    /// The walls are put up through <see cref="ZoneMap.Wall"/> directly — the layer is a union
    /// (§10), so this is also the shape a player-built fence will have. Red with
    /// <c>SimWorld.SweepTheFreeGround</c> ignoring the walls: the pile goes on the gap.
    /// </remarks>
    [Fact]
    public void ABuildingOnTheOnlyWayInIsRefusedByName()
    {
        SimWorld world = Founded();
        GridPos centre = OrganicHousingTests.ABareSquareAtLeast(world, world.Map.FoundingSite, 8, 4);

        // Something standing inside: a pile, one tile.
        Assert.True(world.Mark(BuildingKind.Pile, centre).Allowed, "The pile could not be marked on bare ground.");
        Assert.True(world.SomethingStandsAt(centre), "A marked pile is not standing, so there is nothing to wall off.");

        // A walled 3×3 round it, with one gap on the north side.
        var gapInside = new GridPos(centre.X, centre.Y - 1);
        var gapOutside = new GridPos(centre.X, centre.Y - 2);
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                var tile = new GridPos(centre.X + dx, centre.Y + dy);
                foreach ((int sx, int sy) in Steps)
                {
                    var beyond = new GridPos(tile.X + sx, tile.Y + sy);
                    bool inside = Math.Abs(beyond.X - centre.X) <= 1 && Math.Abs(beyond.Y - centre.Y) <= 1;
                    if (!inside && !(tile == gapInside && beyond == gapOutside))
                    {
                        world.Zones.Wall(tile, beyond, up: true);
                    }
                }
            }
        }

        // The control: a pile beside the square, not on the gap, is fine.
        var beside = new GridPos(centre.X + 3, centre.Y);
        Assert.True(world.CanBuildAt(BuildingKind.Pile, beside).Allowed, "A pile beside the square was refused, so the gap's refusal proves nothing.");

        PlacementVerdict onTheGap = world.CanBuildAt(BuildingKind.Pile, gapOutside);
        _output.WriteLine($"on the gap: {onTheGap.Allowed} — {onTheGap.Reason}");
        Assert.False(onTheGap.Allowed, "A pile on the only way into a walled square was allowed.");
        Assert.Contains("wall off", onTheGap.Reason);
    }

    /// <summary>
    /// ⭐⭐ A house whose fence would shut a neighbour in is refused, in those words (§3.3):
    /// <i>"That would fence in the Ashfords — nobody could reach their door."</i>
    /// </summary>
    /// <remarks>
    /// The fence is stood the way the chooser stands a candidate's (<c>SimWorld.TrialFence</c>'s
    /// shape): a ring round the Ashfords' house. Red with the sweep reading the standing walls
    /// only — the proposed fence is not in the question, and the house is "fine".
    /// </remarks>
    [Fact]
    public void AHouseThatWouldFenceSomebodyInIsRefusedByName()
    {
        SimWorld world = Founded();
        Household ashfords = world.Households[0];
        Assert.True(ashfords.HasHome, "The founders have no house to shut in.");
        List<GridPos> house = world.HomeFootprintAt(ashfords.HomeTile!.Value, ashfords.HomeFacing).CoveredTiles();

        // The box one tile round the house, walled on its outside.
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (GridPos tile in house)
        {
            minX = Math.Min(minX, tile.X - 1);
            minY = Math.Min(minY, tile.Y - 1);
            maxX = Math.Max(maxX, tile.X + 1);
            maxY = Math.Max(maxY, tile.Y + 1);
        }

        GridPos site = world.Map.FoundingSite;
        Assert.False(
            site.X >= minX && site.X <= maxX && site.Y >= minY && site.Y <= maxY,
            "The founding site is inside the ring, so the sweep starts inside it and nothing is shut in.");

        var fence = new Dictionary<GridPos, byte>();
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var tile = new GridPos(x, y);
                foreach ((int dx, int dy) in Steps)
                {
                    var beyond = new GridPos(x + dx, y + dy);
                    if (beyond.X < minX || beyond.X > maxX || beyond.Y < minY || beyond.Y > maxY)
                    {
                        fence[tile] = (byte)(fence.GetValueOrDefault(tile) | ZoneMap.EdgeBit(dx, dy));
                        fence[beyond] = (byte)(fence.GetValueOrDefault(beyond) | ZoneMap.EdgeBit(-dx, -dy));
                    }
                }
            }
        }

        // The house the fence would come with stands somewhere else entirely.
        GridPos far = OrganicHousingTests.ABareSquareAtLeast(world, site, 8, 2);
        Footprint proposed = world.HomeFootprintAt(far, PlotShape.Quarters[0]);
        Assert.Null(world.WhatThisWouldWallOff(proposed));

        string? refused = world.WhatThisWouldWallOff(proposed, fence);
        _output.WriteLine(refused ?? "(allowed)");
        Assert.Equal($"That would fence in the {ashfords.Name}s — nobody could reach their door.", refused);
    }

    /// <summary>
    /// ⭐ The site-chooser never proposes a plot that walls anything off (§3.3): after forty years
    /// of the fixture growing on its own, every house and every standing shape can still be
    /// reached from where the village landed.
    /// </summary>
    /// <remarks>
    /// ⚠️ On villages that GROW — the shipped valley and fixture seed 2 (five and six houses at
    /// year 50 in the D404 measurement). Fixture 12345 reads two houses at year 50 with the fences
    /// up and would prove nothing; the cost is written down in `fences-as-walls.md §6`.
    /// </remarks>
    [Theory]
    [InlineData(true, 12345UL)]
    // ⚠️ 4, NOT 2, SINCE PER-STAGE SEEDS (D473): seed 2's new valley built almost nothing; 4 is
    // the lowest whose opening-only village lives (8 alive at fifty, measured over seeds 1–24).
    [InlineData(false, 4UL)]
    public void TheSiteChooserNeverProposesAPlotThatWallsAnythingOff(bool shipped, ulong seed)
    {
        SimConfig config = (shipped ? ShippedConfig.Load() : Config) with { Seed = seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);

        int houses = 0;
        for (int year = 0; year < 40; year++)
        {
            loop.Step(config.TicksPerYear);
            foreach (Household family in world.Households)
            {
                if (family.HomeTile is not GridPos home)
                {
                    continue;
                }

                Assert.True(
                    world.TravelCost.Cost(world.Map.FoundingSite, home) != TravelCostField.Unreachable,
                    $"Year {year + 1}: nobody can reach the {family.Name}s' door at {home}.");
            }
        }

        foreach (Household family in world.Households)
        {
            if (family.HomeTile is not null)
            {
                houses++;
            }
        }

        _output.WriteLine($"{(shipped ? "shipped" : "fixture")} seed {seed}: {houses} houses at year 40, every door reachable every year.");
        Assert.True(houses > config.StartingPopulation / 2, "The village built almost nothing, so the guard proved nothing.");
    }

    /// <summary>
    /// ⛔ The wall layer is never hashed (D335): it restates the plots, it is not a second fact
    /// about the village. Take a wall down by hand and the fingerprint does not move.
    /// </summary>
    /// <remarks>Red with <c>StateHash</c> folding the wall layer in.</remarks>
    [Fact]
    public void TheWallMaskIsNotHashed()
    {
        SimWorld world = Founded();
        Household family = world.Households[0];
        GridPos yard = YardOf(world, family)[0];
        (int dx, int dy) = Array.Find(Steps, s => CrossesAWall(world.Zones, yard, new GridPos(yard.X + s.Dx, yard.Y + s.Dy)));
        Assert.True(dx != 0 || dy != 0, "The yard tile has no fence on it to take down.");

        ulong before = StateHash.Compute(world);
        world.Zones.Wall(yard, new GridPos(yard.X + dx, yard.Y + dy), up: false);
        ulong after = StateHash.Compute(world);
        Assert.Equal(before, after);
    }

    /// <summary>
    /// ⛔ The walls are maintained, never rebuilt (Joe's rule, CLAUDE.md): over a year of a
    /// growing village the wall generation moves only on a tick when the plots themselves changed.
    /// </summary>
    /// <remarks>
    /// Read against <see cref="ZoneMap.Edits"/>, which moves on every plot claim and release.
    /// Red with the generation bumped once a tick anywhere in the step.
    /// </remarks>
    [Fact]
    public void TheWallsAreMaintainedNotRebuilt()
    {
        // Fixture seed 4: a village that builds (see the chooser's guard above for why not 12345).
        // ⚠️ 4 SINCE PER-STAGE SEEDS (D473): seed 2's new valley builds nothing in thirty years;
        // 4 is the lowest whose opening-only village lives (8 alive at fifty, measured over 1–24).
        SimConfig config = Config with { Seed = 4 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);

        int moved = 0;
        for (int tick = 0; tick < config.TicksPerYear * 30; tick++)
        {
            int walls = world.Zones.WallGeneration;
            int edits = world.Zones.Edits;
            loop.StepOnce();
            if (world.Zones.WallGeneration != walls)
            {
                moved++;
                Assert.True(world.Zones.Edits != edits, $"At tick {world.Tick} the walls moved with no plot claimed or released.");
            }
        }

        _output.WriteLine($"the walls moved on {moved} of {config.TicksPerYear * 30} ticks, each with a plot claimed or released.");
        Assert.True(moved > 0, "No fence went up in thirty years, so the guard proved nothing.");
    }

    /// <summary>
    /// ⭐⭐ THE HONEST GUARD (the handoff's word): <b>no step anybody takes ever crosses a wall</b>,
    /// checked on every villager on every tick — not "steps in other people's yards", which the
    /// house's own two tiles make non-zero by design (they are never walled, §3.1).
    /// </summary>
    /// <remarks>
    /// A step is the segment a villager moved in one tick, walked tile by tile as a leg is
    /// (<see cref="LineOfSight.TilesCrossed"/>).
    /// <para>⚠️ SEED 11, NOT 7, SINCE D543. The seam draws reshuffled the fixture's seed-7 valley, and its warm-start forester was given 72 wooded tiles all across the river - `GiveItTheWoodAroundIt` never asks whether the village can walk to the wood (D110's mistake, latent until a valley put it there) - so no log is ever felled and the four founders freeze in Year 1. A fixture's fixed pose on a reshuffled valley is a coin (D475); the latent bug is on file, not fixed here, so these goldens move for the seams alone.</para>
    /// </remarks>
    [Theory]
    [InlineData(12345UL)]
    [InlineData(11UL)]
    public void NoStepEverCrossesAWall(ulong seed)
    {
        SimConfig config = Config with { Seed = seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);

        var was = new Dictionary<Villager, (Point Position, Point LegFrom)>();
        long steps = 0;
        for (int tick = 0; tick < config.TicksPerYear * 20; tick++)
        {
            was.Clear();
            foreach (Villager villager in world.Villagers)
            {
                if (villager.Alive)
                {
                    was[villager] = (villager.Position, villager.LegFrom);
                }
            }

            loop.StepOnce();
            foreach ((Villager villager, (Point from, Point legFrom)) in was)
            {
                if (!villager.Alive || from == villager.Position)
                {
                    continue;
                }

                // ⚠️ The path, not the chord: a tick that begins a new leg went to the leg's start
                // first (a step off a building's standing place) and then along it. A straight line
                // from where they were to where they are cuts a corner nobody walked.
                if (villager.LegFrom != legFrom)
                {
                    Check(villager, from, villager.LegFrom);
                    Check(villager, villager.LegFrom, villager.Position);
                }
                else
                {
                    Check(villager, from, villager.Position);
                }
            }
        }

        _output.WriteLine($"seed {seed}: {steps} tile steps in twenty years, none across a wall.");
        Assert.True(steps > 10_000, "The village barely walked, so the guard proved nothing.");

        void Check(Villager villager, Point a, Point b)
        {
            // The same walk a leg is judged by, so a corner counts all four of its edges (D404) —
            // and with the terrain and the buildings left out, so this asks about walls alone.
            steps += LineOfSight.TilesCrossed(a, b).Count - 1;
            Assert.True(
                LineOfSight.ClearOfWalls(world, a, b),
                $"Tick {world.Tick}: {villager.Name} ({villager.State}) crossed a fence moving {a} → {b}.");
        }
    }

    /// <summary>
    /// ⛔ Nothing is built on somebody's yard, and no field is painted in one (D404) — said in
    /// words: <i>"That is the Fletchers' yard."</i>
    /// </summary>
    /// <remarks>
    /// Found by the farm guard on the first full run: the fixture's farmhouse was set down with a
    /// corner on a yard — a fence through the middle of the building — and its field painted over
    /// five yard tiles its farmer could reach only through the family's gate. Red with the yard
    /// refusal taken out of <c>CanBuildAt</c> (the pile) and out of <c>CanPaintWorkGround</c> (the field).
    /// </remarks>
    [Fact]
    public void NothingIsBuiltOrPaintedInSomebodysYard()
    {
        SimWorld world = Founded();
        Household family = world.Households[0];

        // ⚠️ The yard tile farthest from the house (D411): a house turned toward its path is a
        // rectangle that can reach into the yard tile beside it, and a pile there is refused as
        // "something already stands there" before the yard is asked (D331: collision is geometry).
        GridPos yard = YardOf(world, family)[^1];

        PlacementVerdict pile = world.CanBuildAt(BuildingKind.Pile, yard);
        _output.WriteLine($"a pile in the {family.Name}s' yard: {pile.Reason}");
        Assert.False(pile.Allowed, "A pile was allowed in somebody's yard.");
        Assert.Equal($"That is the {family.Name}s' yard.", pile.Reason);

        Workplace any = world.Workplaces.First(w => !w.IsSite);
        PlacementVerdict field = world.CanPaintWorkGround(any, yard);
        Assert.False(field.Allowed, "Work ground was allowed in somebody's yard.");
        Assert.Equal($"That is the {family.Name}s' yard.", field.Reason);
    }

    /// <summary>
    /// ⛔ A fence handed on with a house comes down with the heir's release (D404) — the walls
    /// belong to whoever holds the plot, not to whoever raised it.
    /// </summary>
    /// <remarks>Red with <c>ZoneMap.HandPlotOn</c> leaving the fence under the old owner.</remarks>
    [Fact]
    public void AnInheritedFenceComesDownWithTheHeirsRelease()
    {
        SimWorld world = Founded();
        Household family = world.Households[0];
        var fenced = new List<GridPos>(family.FencedTiles);
        Assert.Contains(fenced, tile => world.Zones.WallsOn(tile) != 0);

        const int Heir = 900;
        world.Zones.HandPlotOn(family.Id, Heir);
        world.Zones.ReleasePlot(Heir);

        foreach (GridPos tile in fenced)
        {
            foreach ((int dx, int dy) in Steps)
            {
                var beyond = new GridPos(tile.X + dx, tile.Y + dy);
                if (world.Zones.PlotOwner(beyond) == 0)
                {
                    Assert.False(CrossesAWall(world.Zones, tile, beyond), $"A wall stood on at {tile} → {beyond} with nobody holding the plot.");
                }
            }
        }
    }

    /// <summary>
    /// ⭐ Two yards back to back share an edge and both raised it (§5): pull one family's fence
    /// down and the other's side still stands.
    /// </summary>
    /// <remarks>Red with <c>ZoneMap.PullTheFenceDown</c> not raising the neighbour's edges again.</remarks>
    [Fact]
    public void BackToBackFencesKeepTheirSharedWall()
    {
        SimWorld world = Founded();
        ZoneMap zones = world.Zones;

        // Two plots posed side by side on bare ground, far from the founding, sharing one column.
        GridPos at = OrganicHousingTests.ABareSquareAtLeast(world, world.Map.FoundingSite, 10, 4);
        var left = new List<GridPos>();
        var right = new List<GridPos>();
        for (int dy = 0; dy < 3; dy++)
        {
            for (int dx = 0; dx < 3; dx++)
            {
                left.Add(new GridPos(at.X - 3 + dx, at.Y + dy));
                right.Add(new GridPos(at.X + dx, at.Y + dy));
            }
        }

        var laneLeft = new List<GridPos> { new(at.X - 3, at.Y - 1), new(at.X - 2, at.Y - 1), new(at.X - 1, at.Y - 1) };
        var laneRight = new List<GridPos> { new(at.X, at.Y - 1), new(at.X + 1, at.Y - 1), new(at.X + 2, at.Y - 1) };
        zones.ClaimPlot(801, left, laneLeft, new List<GridPos> { left[0], left[1] });
        zones.ClaimPlot(802, right, laneRight, new List<GridPos> { right[0], right[1] });

        var shared = (From: new GridPos(at.X - 1, at.Y + 2), To: new GridPos(at.X, at.Y + 2));
        Assert.True(CrossesAWall(zones, shared.From, shared.To), "The two yards do not wall their shared edge, so there is nothing to keep.");

        zones.ReleasePlot(801);
        Assert.True(CrossesAWall(zones, shared.To, shared.From), "Pulling one fence down took the neighbour's side of the shared edge with it.");
        Assert.True(CrossesAWall(zones, shared.From, shared.To), "The shared edge is walled on one side only — the layer's two sides disagree.");
    }

    /// <summary>
    /// ⭐ A planned leg may not graze a fence post: a straight line through a grid corner is refused
    /// if a wall stands on ANY of the four edges that meet there — while the physical question
    /// (<see cref="LineOfSight.ClearOfWalls"/>) lets it by, because one way round is open (D404).
    /// </summary>
    /// <remarks>Red with the fourth edge (the one from the corner's <c>above</c> tile) not asked.</remarks>
    [Fact]
    public void ALegDoesNotGrazeAFencePost()
    {
        SimWorld world = Founded();
        GridPos corner = OrganicHousingTests.ABareSquareAtLeast(world, world.Map.FoundingSite, 10, 2);
        var above = new GridPos(corner.X, corner.Y + 1);
        var diagonal = new GridPos(corner.X + 1, corner.Y + 1);

        // One wall, on the edge between the corner's `above` tile and the diagonal.
        world.Zones.Wall(above, diagonal, up: true);

        Point from = Point.CentreOf(corner);
        Point to = Point.CentreOf(diagonal);
        Assert.True(LineOfSight.ClearOfWalls(world, from, to), "The physical question refused a move with one way round the post open.");
        Assert.False(
            LineOfSight.Clear(world.Map, world, from, to, System.Array.Empty<GridPos>(), System.Array.Empty<GridPos>()),
            "A planned leg grazed a fence post at a grid corner.");
    }

    /// <summary>
    /// ⛔ The wall-off question's quick local answer is the whole-valley sweep's answer, for every
    /// plot the chooser could ask about and every pile the player could drop (D404, the 237 ms tick).
    /// </summary>
    /// <remarks>
    /// A crowded neighbourhood: six houses sited in a painted square, then every whole-painted tile
    /// asked at all four facings with its fence, and every tile asked for a one-tile pile — once
    /// locally, once with <see cref="SimWorld.AlwaysSweepTheWholeValley"/>. Anti-vacuity: some of
    /// those questions must actually be refused, or two answers of "fine" agreed about nothing.
    /// Red with the local check trusting a boundary it never joined.
    /// </remarks>
    [Fact]
    public void TheQuickWallOffAnswerIsTheSweepsAnswer()
    {
        SimWorld world = Founded();
        GridPos site = world.Map.FoundingSite;
        for (int dy = -7; dy <= 7; dy++)
        {
            for (int dx = -7; dx <= 7; dx++)
            {
                world.PaintResidential(new GridPos(site.X + dx, site.Y + dy));
            }
        }

        for (int house = 0; house < 6; house++)
        {
            var family = new Household { Stockpile = world.NewStockpile(), Id = 950 + house, Surname = "Crowd" + house };
            world.Households.Add(family);
            world.MarkHome(family.Id, Household.ChooseSite(world, site, family.Id));
        }

        int asked = 0;
        int refused = 0;
        foreach (GridPos front in world.Zones.WholeResidentialTiles)
        {
            foreach (Angle facing in PlotShape.Quarters)
            {
                Footprint home = world.HomeFootprintAt(front, facing);
                Dictionary<GridPos, byte> fence = world.TrialFence(front, facing, 999);
                Compare(home, fence);
            }

            Compare(world.FootprintOf(BuildingKind.Pile, front), null);
        }

        _output.WriteLine($"{asked} questions, {refused} refused, every one answered alike.");
        Assert.True(refused > 0, "Nothing asked was refused, so the two answers agreed about nothing.");

        void Compare(Footprint proposed, Dictionary<GridPos, byte>? fence)
        {
            world.AlwaysSweepTheWholeValley = false;
            string? quick = world.WhatThisWouldWallOff(proposed, fence);
            world.AlwaysSweepTheWholeValley = true;
            string? swept = world.WhatThisWouldWallOff(proposed, fence);
            world.AlwaysSweepTheWholeValley = false;
            asked++;
            if (swept is not null)
            {
                refused++;
            }

            Assert.True(quick == swept, $"At {proposed.Origin}: the quick answer said \"{quick}\", the sweep said \"{swept}\".");
        }
    }
}
