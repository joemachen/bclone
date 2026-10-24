using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// Every roofed building's door (D619–D621, `specs/doors.md §4.1–§4.3`) — on its front, or, where the front cannot open,
/// on the first side that can.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>COMMIT 1 OF FIVE: THE DOOR IS WORKED OUT AND SAID, AND NOTHING WALKS THROUGH IT YET.</b> Walks and catchment
/// still enter by the cheapest side (D383) until commit 3, so nothing here moves a golden.
/// </para>
/// <para>
/// ⭐ <b>AN INDEX KEPT ON THE WORLD'S OWN COUNTERS</b> (CLAUDE.md: nothing derivable rebuilt per ask). A door restates the
/// footprint and what stands, walls and lies round it, so it is remembered per shape until
/// <see cref="StandingGeneration"/>, the walls' or the terrain's generation moves, and then forgotten whole. Never hashed
/// and never saved (D335): a loaded village works its doors out again.
/// </para>
/// </remarks>
public sealed partial class SimWorld
{
    /// <summary>The doors asked since the counters last moved, by footprint. Derived, never hashed.</summary>
    private Dictionary<Footprint, Door>? _doors;

    private int _doorsStanding = -1;
    private int _doorsWalls = -1;
    private int _doorsTerrain = -1;

    /// <summary>
    /// How many doors have been worked out rather than remembered — what <c>ADoorIsWorkedOutOncePerChangeNotPerAsk</c>
    /// reads; nothing in the sim does.
    /// </summary>
    internal int DoorsWorkedOut { get; private set; }

    /// <summary>The front first, then the back, then the right, then the left (§4.3, Joe's call 2) — quarter turns from the front.</summary>
    private static readonly int[] TheSidesInTurn = { 0, 2, 1, 3 };

    /// <summary>Whether a building of this kind has a door — every roofed building (§4.1, Joe's call 1).</summary>
    /// <remarks>A well is drawn from all round, a pile is a heap, a bridge is walked across, a fence is a fence.</remarks>
    internal static bool HasADoor(BuildingKind kind) =>
        kind is not (BuildingKind.Well or BuildingKind.Pile or BuildingKind.Bridge or BuildingKind.StoneBridge or BuildingKind.PastureFence);

    /// <summary>A workplace's door — or a site's, which is the door of the building it raises (§4.1). Null for a site that raises no roof.</summary>
    public Door? DoorOf(Workplace workplace)
    {
        ArgumentNullException.ThrowIfNull(workplace);
        return workplace.Construction is { } site && !HasADoor(site.Kind) ? null : DoorAt(workplace.Footprint);
    }

    /// <summary>A store's door. Null for a pile (§4.1).</summary>
    public Door? DoorOf(StoreBuilding store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.Kind == StoreKind.Pile ? null : DoorAt(store.Footprint);
    }

    /// <summary>A household's front door. Null while it has no house.</summary>
    public Door? DoorOf(Household household) =>
        HomeFootprintOf(household) is Footprint home ? DoorAt(home) : null;

    /// <summary>A library's door.</summary>
    public Door? DoorOf(Library library)
    {
        ArgumentNullException.ThrowIfNull(library);
        return DoorAt(library.Footprint);
    }

    /// <summary>The town hall's door.</summary>
    public Door? DoorOf(TownHall hall)
    {
        ArgumentNullException.ThrowIfNull(hall);
        return DoorAt(hall.Footprint);
    }

    /// <summary>
    /// Which of the four ways a facing's front looks (§4.2): the nearest quarter turn, and a facing exactly between two
    /// turns back — anticlockwise, to the lower angle. North at no turn, east at a quarter.
    /// </summary>
    internal static Compass FrontOf(Angle facing)
    {
        int quarter = facing.Raw >> 14;
        int within = facing.Raw & 0x3FFF;
        return (Compass)((within > 0x2000 ? quarter + 1 : quarter) & 3);
    }

    /// <summary>The door of a building with this footprint, standing here today — remembered until the world changes round it.</summary>
    internal Door DoorAt(Footprint shape)
    {
        if (_doors is null
            || _doorsStanding != StandingGeneration
            || _doorsWalls != Zones.WallGeneration
            || _doorsTerrain != _terrainGeneration)
        {
            _doors = new Dictionary<Footprint, Door>();
            _doorsStanding = StandingGeneration;
            _doorsWalls = Zones.WallGeneration;
            _doorsTerrain = _terrainGeneration;
        }

        if (!_doors.TryGetValue(shape, out Door door))
        {
            door = WorkOutTheDoor(shape);
            _doors[shape] = door;
            DoorsWorkedOut++;
        }

        return door;
    }

    /// <summary>
    /// The card's sentence for a door that is not on its front (§4.3): <i>"Its door is on its north side — water lies in
    /// front of it."</i> Null for a door on its front, and for a building with no door.
    /// </summary>
    /// <remarks>
    /// The side is said by the compass, not as "the back" or "the left": the player reads it off a map, not standing at
    /// the door. ⏸️ Shown on the cards in commit 5 (the view).
    /// </remarks>
    public string? SayWhereTheDoorIs(Door? door)
    {
        if (door is not Door d)
        {
            return null;
        }

        if (d.Shut)
        {
            return "No side of it opens onto ground the village can reach.";
        }

        if (d.Faces == d.Front)
        {
            return null;
        }

        string why = d.FrontBlockedBy switch
        {
            DoorBlock.Edge => "the valley ends in front of it",
            DoorBlock.Water => "water lies in front of it",
            DoorBlock.Built => $"{NameOfWhatStandsAt(d.FrontStep)} stands in front of it",
            DoorBlock.Fence => "a fence runs across its front",
            _ => "nobody can reach the ground in front of it",
        };
        return $"Its door is on its {CompassWord(d.Faces)} side — {why}.";
    }

    private static string CompassWord(Compass way) => way switch
    {
        Compass.North => "north",
        Compass.East => "east",
        Compass.South => "south",
        _ => "west",
    };

    private static GridPos StepFrom(GridPos tile, Compass way) => way switch
    {
        Compass.North => new GridPos(tile.X, tile.Y - 1),
        Compass.East => new GridPos(tile.X + 1, tile.Y),
        Compass.South => new GridPos(tile.X, tile.Y + 1),
        _ => new GridPos(tile.X - 1, tile.Y),
    };

    /// <summary>
    /// ⭐ The rule (§4.2–§4.3): each side in turn — front, back, right, left — and along each side its edge tiles from the
    /// one nearest the side's middle outward; the first whose outside tile is free, unwalled and reached is the door.
    /// None: the door is shut, on the front's middle.
    /// </summary>
    /// <remarks>
    /// A door slides along its side before it turns the corner, so <b>a building is shut exactly when no free tile the
    /// village reaches touches it across an unwalled edge</b> — D383's own test (<see cref="HasAReachedNeighbour"/>), so
    /// a building the village can reach today always has a door it can use.
    /// </remarks>
    private Door WorkOutTheDoor(Footprint shape)
    {
        Compass front = FrontOf(shape.Facing);
        List<GridPos> covered = shape.CoveredTiles();
        var own = new HashSet<GridPos>(covered);
        bool[] reached = FreeGroundToday();

        GridPos middle = AlongTheSide(shape, covered, own, 0, front)[0];
        GridPos frontStep = StepFrom(middle, front);
        DoorBlock frontBlock = WhatBlocks(middle, front, reached);

        foreach (int quarter in TheSidesInTurn)
        {
            var way = (Compass)(((int)front + quarter) & 3);
            foreach (GridPos tile in AlongTheSide(shape, covered, own, quarter, way))
            {
                if (WhatBlocks(tile, way, reached) == DoorBlock.None)
                {
                    return new Door(tile, StepFrom(tile, way), way, front, frontBlock, frontStep, Shut: false);
                }
            }
        }

        return new Door(middle, frontStep, front, front, frontBlock, frontStep, Shut: true);
    }

    /// <summary>
    /// The building's tiles on one side — those whose next tile that way is not the building's — nearest the middle of
    /// that side's edge first.
    /// </summary>
    /// <remarks>
    /// The middle is the turned edge's own (the front's is centre + (0, −H/2) turned, `PlotShape`'s "door on the −y
    /// edge"); the side's way is that edge's quarter turn. ⭐ A tie goes to the tile later in <see cref="Footprint.CoveredTiles"/>'
    /// order — the tile the middle point itself falls in, so a house at a quarter turn keeps the door its plot has always
    /// called its door (<see cref="PlotShape.Door"/>).
    /// </remarks>
    private static List<GridPos> AlongTheSide(Footprint shape, List<GridPos> covered, HashSet<GridPos> own, int quarter, Compass way)
    {
        Fixed halfWide = Fixed.FromRatio(shape.Width, 2);
        Fixed halfDeep = Fixed.FromRatio(shape.Height, 2);
        Point local = quarter switch
        {
            0 => new Point(Fixed.Zero, -halfDeep),
            1 => new Point(halfWide, Fixed.Zero),
            2 => new Point(Fixed.Zero, halfDeep),
            _ => new Point(-halfWide, Fixed.Zero),
        };
        Point mid = shape.Origin + local.RotatedBy(shape.Facing);

        var side = new List<(GridPos Tile, Fixed Distance, int Order)>();
        for (int i = 0; i < covered.Count; i++)
        {
            if (!own.Contains(StepFrom(covered[i], way)))
            {
                Point centre = Point.CentreOf(covered[i]);
                Fixed dx = centre.X - mid.X;
                Fixed dy = centre.Y - mid.Y;
                side.Add((covered[i], (dx * dx) + (dy * dy), i));
            }
        }

        side.Sort((a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : b.Order.CompareTo(a.Order));
        return side.ConvertAll(s => s.Tile);
    }

    /// <summary>What stops a door in this tile opening this way, or <see cref="DoorBlock.None"/>.</summary>
    private DoorBlock WhatBlocks(GridPos tile, Compass way, bool[] reached)
    {
        GridPos step = StepFrom(tile, way);
        if (!Map.Contains(step))
        {
            return DoorBlock.Edge;
        }

        if (!IsWalkable(step))
        {
            return DoorBlock.Water;
        }

        if (StandsOn(step))
        {
            return DoorBlock.Built;
        }

        if ((WallsOn(tile) & ZoneMap.EdgeBit(step.X - tile.X, step.Y - tile.Y)) != 0)
        {
            return DoorBlock.Fence;
        }

        int index = IndexOfTile(step);
        return index >= 0 && reached[index] ? DoorBlock.None : DoorBlock.Unreached;
    }
}
