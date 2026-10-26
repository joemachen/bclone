using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// A door in use is kept clear (D622, `specs/doors.md §4.5`, commit 2): nothing stands on its doorstep, no fence runs
/// across it, no proposal leaves it in a pocket — and a house is sited where its own door will open.
/// </summary>
/// <remarks>
/// Still nobody walks through a door (commit 3). These are refusals: what a player or the site chooser may no longer do.
/// </remarks>
public sealed class DoorstepTests
{
    private readonly ITestOutputHelper _output;

    public DoorstepTests(ITestOutputHelper output) => _output = output;

    private static SimWorld World() =>
        SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink()).World;

    /// <summary>
    /// ⭐ Nothing is built on a doorstep (§4.5): a pile or a hut over a granary's doorstep is refused by name, and the tile
    /// beside it is not.
    /// </summary>
    [Fact]
    public void NothingIsBuiltOnADoorstep()
    {
        SimWorld world = World();
        (GridPos at, Door door, string name) = AGranary(world);

        PlacementVerdict pile = world.CanBuildAt(BuildingKind.Pile, door.Doorstep);
        _output.WriteLine($"a pile on {name}'s doorstep {door.Doorstep}: {pile.Reason}");
        Assert.False(pile.Allowed);
        Assert.Equal($"That is in front of {name}'s door.", pile.Reason);

        GridPos beside = new(door.Doorstep.X + 2, door.Doorstep.Y);
        Assert.True(world.CanBuildAt(BuildingKind.Pile, beside).Allowed, $"a pile two tiles along, at {beside}, was refused");
    }

    /// <summary>
    /// A fence across a door in use is refused (§4.5) — the wall between the door and its doorstep, read through the
    /// fence a proposal would stand for this question only.
    /// </summary>
    [Fact]
    public void AFenceAcrossADoorIsRefused()
    {
        SimWorld world = World();
        (GridPos at, Door door, string name) = AGranary(world);
        byte toTheStep = ZoneMap.EdgeBit(door.Doorstep.X - door.Tile.X, door.Doorstep.Y - door.Tile.Y);
        byte back = ZoneMap.EdgeBit(door.Tile.X - door.Doorstep.X, door.Tile.Y - door.Doorstep.Y);
        var fence = new Dictionary<GridPos, byte> { [door.Tile] = toTheStep, [door.Doorstep] = back };

        string? said = world.WhatAFenceWouldWallOff(fence, AFarTile(world, at));
        _output.WriteLine($"a fence across {name}'s door: {said}");
        Assert.Equal($"That would block {name}'s door.", said);
    }

    /// <summary>
    /// ⭐⭐ A doorstep shut in a pocket is refused though the building is still reached (§4.5) — the rule stricter than
    /// D383's any-side test: the granary's other front tile stays open, and before doors this fence was allowed.
    /// </summary>
    [Fact]
    public void APocketRoundADoorInUseIsRefusedThoughTheBuildingIsStillReached()
    {
        SimWorld world = World();
        (GridPos at, Door door, string name) = AGranary(world);
        GridPos d = door.Doorstep;
        var fence = new Dictionary<GridPos, byte>();
        void Wall(GridPos a, GridPos b)
        {
            fence[a] = (byte)((fence.TryGetValue(a, out byte x) ? x : 0) | ZoneMap.EdgeBit(b.X - a.X, b.Y - a.Y));
            fence[b] = (byte)((fence.TryGetValue(b, out byte y) ? y : 0) | ZoneMap.EdgeBit(a.X - b.X, a.Y - b.Y));
        }

        // Every edge of the doorstep but the door's own.
        foreach (Compass way in new[] { Compass.North, Compass.East, Compass.South, Compass.West })
        {
            GridPos next = way switch
            {
                Compass.North => new GridPos(d.X, d.Y - 1),
                Compass.East => new GridPos(d.X + 1, d.Y),
                Compass.South => new GridPos(d.X, d.Y + 1),
                _ => new GridPos(d.X - 1, d.Y),
            };
            if (next != door.Tile)
            {
                Wall(d, next);
            }
        }

        string? said = world.WhatAFenceWouldWallOff(fence, AFarTile(world, at));
        _output.WriteLine($"{name}'s doorstep {d} fenced into a pocket: {said}");
        Assert.Equal($"That would block {name}'s door.", said);
    }

    /// <summary>
    /// ⭐ Every house the site chooser raises opens on its front (§4.5): its doorstep was kept clear when it was sited, and
    /// nothing has been built on it since — asked of every house of a fixture village thirty years on.
    /// </summary>
    [Fact]
    public void EveryHouseTheChooserSitesOpensOnItsFront()
    {
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;
        loop.Step(30 * world.Config.TicksPerYear);

        int houses = 0, offTheQuarter = 0;
        foreach (Household household in world.Households)
        {
            if (world.DoorOf(household) is not Door door)
            {
                continue;
            }

            houses++;
            offTheQuarter += household.HomeFacing.Raw % 16384 == 0 ? 0 : 1;
            Assert.True(
                door.Faces == door.Front && door.FrontBlockedBy == DoorBlock.None,
                $"the {household.Name} house at {household.HomeTile} faces {household.HomeFacing.Raw}: its door is {door.Faces}, the front blocked by {door.FrontBlockedBy} at {door.FrontStep}");
        }

        _output.WriteLine($"{houses} houses, {offTheQuarter} turned off the quarter, every one opening on its front");
        Assert.True(houses > 4, $"only {houses} houses — the pose did not grow a village");
    }

    /// <summary>A granary marked on open ground near the founding site, with its door and its name.</summary>
    private static (GridPos At, Door Door, string Name) AGranary(SimWorld world)
    {
        GridPos at = OpenGround(world);
        PlacementVerdict raised = world.Mark(BuildingKind.Granary, at);
        Assert.True(raised.Allowed, raised.Reason);
        Workplace? site = world.Workplaces.Find(w => w.Footprint.Covers(at));
        StoreBuilding? store = world.StoreBuildings.Find(s => s.Footprint.Covers(at));
        Door door = (store is not null ? world.DoorOf(store) : world.DoorOf(site!))!.Value;
        string name = store?.Name ?? site!.Construction?.Name ?? site.Name;
        Assert.False(door.Shut);
        Assert.Equal(DoorBlock.None, door.FrontBlockedBy);
        return (at, door, name);
    }

    /// <summary>A free tile the village reaches, well away from the pose, for a fence's gate to keep reached.</summary>
    private static GridPos AFarTile(SimWorld world, GridPos at)
    {
        GridPos from = world.FirstHomeOrFoundingSite();
        for (int r = 8; r < 40; r++)
        {
            foreach (GridPos t in new[] { new GridPos(at.X + r, at.Y), new GridPos(at.X - r, at.Y), new GridPos(at.X, at.Y + r), new GridPos(at.X, at.Y - r) })
            {
                if (world.IsWalkable(t) && !world.StandsOn(t) && world.TravelCost.CanReach(from, t))
                {
                    return t;
                }
            }
        }

        throw new InvalidOperationException("no free tile far from the pose");
    }

    /// <summary>A tile with a 9 × 9 square of free, unwalled ground round it that the village reaches, nearest the founding site.</summary>
    private static GridPos OpenGround(SimWorld world)
    {
        GridPos from = world.FirstHomeOrFoundingSite();
        GridPos site = world.Map.FoundingSite;
        for (int r = 6; r < 40; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && Clear(at))
                    {
                        return at;
                    }
                }
            }
        }

        throw new InvalidOperationException("no open ground near the founding site");

        bool Clear(GridPos at)
        {
            for (int y = at.Y - 4; y <= at.Y + 4; y++)
            {
                for (int x = at.X - 4; x <= at.X + 4; x++)
                {
                    var tile = new GridPos(x, y);
                    if (!world.IsWalkable(tile) || world.StandsOn(tile) || world.WallsOn(tile) != 0
                        || !world.TravelCost.CanReach(from, tile))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
