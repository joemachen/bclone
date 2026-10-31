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
/// <see cref="SettledStandingGeneration"/> (the standing counter less the site chooser's trial houses, D622) or the
/// walls' generation moves, and then forgotten whole. ⚠️ <b>Not the terrain's</b> (D622): only water stops a walker, water
/// never changes, and a bridge's deck moves the standing counter — while a crop turning or a tree felled moves the
/// terrain's many times a year, and keyed on it the doors were worked out again on nearly every ask. Never hashed
/// and never saved (D335): a loaded village works its doors out again.
/// </para>
/// </remarks>
public sealed partial class SimWorld
{
    /// <summary>The doors asked since the counters last moved, by footprint. Derived, never hashed.</summary>
    private Dictionary<Footprint, Door>? _doors;

    private int _doorsStanding = -1;
    private int _doorsWalls = -1;

    /// <summary>
    /// How many doors have been worked out rather than remembered — what <c>ADoorIsWorkedOutOncePerChangeNotPerAsk</c>
    /// reads; nothing in the sim does.
    /// </summary>
    internal int DoorsWorkedOut { get; private set; }

    /// <summary>The front first, then the back, then the right, then the left (§4.3, Joe's call 2) — quarter turns from the front.</summary>
    private static readonly int[] TheSidesInTurn = { 0, 2, 1, 3 };

    /// <summary>Whether a building of this kind has a door — every roofed building (§4.1, Joe's call 1) but the two that open onto their ground.</summary>
    /// <remarks>
    /// A well is drawn from all round, a pile is a heap, a bridge is walked across, a fence is a fence — and ⭐ <b>a farmhouse
    /// and a barn open onto their fields on every side</b> (Joe, D624). Priced honestly through one door, a farmer kept one
    /// ring of field, five tiles not thirteen, and a farmer's year was worth a gatherer's only if a tile gave more than an
    /// armful (`doors.md §5`); the herd the same, its hay cut the same way. Their work is the ground all round them.
    /// </remarks>
    internal static bool HasADoor(BuildingKind kind) =>
        kind is not (BuildingKind.Well or BuildingKind.Pile or BuildingKind.Bridge or BuildingKind.StoneBridge or BuildingKind.PastureFence
            or BuildingKind.Farmhouse or BuildingKind.Barn);

    /// <summary>
    /// A workplace's door — or a site's, which is the door of the building it raises (§4.1). Null for a building, or a
    /// site raising one, that has none: a farmhouse, a barn (D624), a bridge, a fence.
    /// </summary>
    public Door? DoorOf(Workplace workplace)
    {
        ArgumentNullException.ThrowIfNull(workplace);
        BuildingKind? kind = workplace.Construction?.Kind ?? JobsCatalog.WorksAt(workplace.Kind);
        return kind is BuildingKind building && !HasADoor(building) ? null : DoorAt(workplace.Footprint);
    }

    /// <summary>
    /// A store's door. Null for a pile (§4.1), a barn's store (D624) — and the cart (D625): a wagon is not a roofed building,
    /// and with a door it closed the best ground beside the founding site ("That is in front of the cart's door"), the played
    /// opening's woodcutter's hut went unraised, nobody split a log, and the founders froze.
    /// </summary>
    public Door? DoorOf(StoreBuilding store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.Kind is StoreKind.Pile or StoreKind.Barn or StoreKind.Cart ? null : DoorAt(store.Footprint);
    }

    /// <summary>
    /// ⭐ The door a building of this kind would have on this footprint, standing here today (`doors.md §12.1`) — what the
    /// ghost draws before the click. Null for a kind with no door (§4.1). Remembered per footprint like every door.
    /// </summary>
    public Door? DoorOfAProposal(BuildingKind kind, Footprint shape) => HasADoor(kind) ? DoorAt(shape) : null;

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
    /// The doors every standing roofed building uses today — what a proposal may not shut (§4.5) and what a walk goes
    /// through (§4.4). Rebuilt on the doors' own counters, never hashed. A shut door is not listed: nobody uses it.
    /// </summary>
    private DoorIndex? _doorsInUse;

    private int _doorsInUseStanding = -1;
    private int _doorsInUseWalls = -1;

    /// <summary>The doors in use, by building, by doorstep and by tile covered — one answer, built together.</summary>
    private sealed class DoorIndex
    {
        public required List<(Footprint Shape, string Name, Door Door)> Doors { get; init; }

        /// <summary>Which door's doorstep each tile is, by tile.</summary>
        public required Dictionary<GridPos, int> Doorstep { get; init; }

        /// <summary>Which door covers each tile by map-order index, −1 for none.</summary>
        public required int[] Covering { get; init; }
    }

    /// <summary>The name of the building whose door opens onto this tile today, or null (§4.5).</summary>
    internal string? WhoseDoorstep(GridPos tile)
    {
        DoorIndex doors = DoorsInUse();
        return doors.Doorstep.TryGetValue(tile, out int owner) ? doors.Doors[owner].Name : null;
    }

    /// <summary>
    /// A measurement's knob (D623, like <see cref="AlwaysSweepTheWholeValley"/>): every building entered and left by its
    /// cheapest side again, D383's rule, so a harness can price a door's in-and-out against the walk it replaced. The
    /// caller forgets the cost field's routes after setting it. Never set by the game.
    /// </summary>
    internal bool DoorsOnEverySide { get; set; }

    /// <summary>The door in use of the building standing on this tile, or null (`IObstacles`, §4.4).</summary>
    public Door? DoorCovering(GridPos tile)
    {
        if (DoorsOnEverySide)
        {
            return null;
        }

        int index = IndexOfTile(tile);
        if (index < 0)
        {
            return null;
        }

        DoorIndex doors = DoorsInUse();
        int owner = doors.Covering[index];
        return owner < 0 ? null : doors.Doors[owner].Door;
    }

    /// <summary>
    /// ⭐ The first door this proposal would shut (§4.5, commit 2): a door in use whose doorstep it covers, walls off from
    /// its door, or leaves unreached — even with another side of the building still open. Null if none.
    /// </summary>
    /// <remarks>
    /// Stricter than D383's any-side test on purpose: a door slides and turns (§4.3), so "it still has a door" is the old
    /// rule again, and the door a building has would move every time a neighbour went up in front of it.
    /// </remarks>
    private string? WhatDoorThisWouldShut(bool[] after, List<GridPos> proposedTiles, IReadOnlyDictionary<GridPos, byte>? fence)
    {
        List<(Footprint Shape, string Name, Door Door)> doors = DoorsInUse().Doors;
        for (int i = 0; i < doors.Count; i++)
        {
            Door door = doors[i].Door;
            int index = IndexOfTile(door.Doorstep);
            bool covered = proposedTiles.Contains(door.Doorstep);
            bool walled = (WallsForTheSweep(door.Tile, fence) & ZoneMap.EdgeBit(door.Doorstep.X - door.Tile.X, door.Doorstep.Y - door.Tile.Y)) != 0;
            if (covered || walled || index < 0 || !after[index])
            {
                return $"That would block {doors[i].Name}'s door.";
            }
        }

        return null;
    }

    /// <summary>
    /// The doorstep a building with this footprint opens onto when nothing is in the way — its front's, by §4.2's
    /// geometry alone. What the site chooser keeps clear for a house it has not raised yet (§4.5).
    /// </summary>
    internal static GridPos FrontDoorstepOf(Footprint shape)
    {
        Compass front = FrontOf(shape.Facing);
        List<GridPos> covered = shape.CoveredTiles();
        return StepFrom(AlongTheSide(shape, covered, new HashSet<GridPos>(covered), 0, front)[0], front);
    }

    /// <remarks>
    /// ⚠️ <b>While a trial house stands the settled index is used as it is</b> (D623): the trial's plot has already been
    /// refused if it shut a door, and nothing walks to a trial house. If there is no settled index yet, one is worked out
    /// for the trial and thrown away.
    /// </remarks>
    private DoorIndex DoorsInUse()
    {
        if (_doorsInUse is not null
            && _doorsInUseStanding == SettledStandingGeneration
            && _doorsInUseWalls == Zones.WallGeneration)
        {
            return _doorsInUse;
        }

        var doors = new List<(Footprint Shape, string Name, Door Door)>();
        var seen = new HashSet<Footprint>();
        void Add(Footprint shape, Door? door, string name)
        {
            // ⚠️ A market, a trading post and a barn are a workplace AND a store on one footprint: one door (D619's trap).
            if (door is Door d && !d.Shut && seen.Add(shape))
            {
                doors.Add((shape, name, d));
            }
        }

        for (int i = 0; i < Households.Count; i++)
        {
            if (HomeFootprintOf(Households[i]) is Footprint home)
            {
                Add(home, DoorOf(Households[i]), $"the {Households[i].Name} house");
            }
        }

        for (int i = 0; i < Workplaces.Count; i++)
        {
            // A repair's site stands on the building it mends, whose door is listed already; a bridge's has none.
            if (!IsABridgeSite(Workplaces[i]) && !IsARepairSite(Workplaces[i]))
            {
                // A site is named for what it raises ("granary 2", not "granary 2 (building)").
                Add(Workplaces[i].Footprint, DoorOf(Workplaces[i]), Workplaces[i].Construction?.Name ?? Workplaces[i].Name);
            }
        }

        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            Add(StoreBuildings[i].Footprint, DoorOf(StoreBuildings[i]), StoreBuildings[i].Name);
        }

        for (int i = 0; i < Libraries.Count; i++)
        {
            Add(Libraries[i].Footprint, DoorOf(Libraries[i]), Libraries[i].Name);
        }

        if (TownHall is not null)
        {
            Add(TownHall.Footprint, DoorOf(TownHall), TownHall.Name);
        }

        var owner = new Dictionary<GridPos, int>();
        int[] covering = new int[Map.Width * Map.Height];
        System.Array.Fill(covering, -1);
        for (int i = 0; i < doors.Count; i++)
        {
            owner.TryAdd(doors[i].Door.Doorstep, i);
            List<GridPos> tiles = doors[i].Shape.CoveredTiles();
            for (int j = 0; j < tiles.Count; j++)
            {
                int index = IndexOfTile(tiles[j]);
                if (index >= 0)
                {
                    covering[index] = i;
                }
            }
        }

        var built = new DoorIndex { Doors = doors, Doorstep = owner, Covering = covering };
        if (_trialHome is not null)
        {
            return built;
        }

        _doorsInUse = built;
        _doorsInUseStanding = SettledStandingGeneration;
        _doorsInUseWalls = Zones.WallGeneration;
        return built;
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
        // ⚠️ Never remembered while a trial house stands (D622): the trial's walls and footprint are not the village's.
        if (_trialHome is not null)
        {
            DoorsWorkedOut++;
            return WorkOutTheDoor(shape);
        }

        if (_doors is null
            || _doorsStanding != SettledStandingGeneration
            || _doorsWalls != Zones.WallGeneration)
        {
            _doors = new Dictionary<Footprint, Door>();
            _doorsStanding = SettledStandingGeneration;
            _doorsWalls = Zones.WallGeneration;
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
        bool[] reached = VillageGroundToday();

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

    /// <summary>The village's piece of today's free ground, and the sweep it was cut from (D623). Derived, never hashed.</summary>
    private bool[]? _villageGround;

    private bool[]? _villageGroundFrom;

    /// <summary>
    /// ⭐ <b>The free ground the village actually reaches today</b> (D623): today's sweep (<see cref="FreeGroundToday"/>)
    /// cut into pieces, keeping the piece the most standing shapes touch — D614's own rule for which piece is the
    /// village (<see cref="TheVillagesPiece"/>).
    /// </summary>
    /// <remarks>
    /// ⛔ <b>The sweep alone would not do.</b> It is SEEDED beside the founding site (D383, for seed 11's one-tile spit), so a
    /// tile beside the founding site always reads as reached — even sealed in a pocket between the founding buildings. A
    /// town hall stood on the site faced its door into exactly that pocket, and the whole village was cut off from it
    /// (found by `PastureFenceTests`: 1 tile of 493 reached from the site). Rebuilt only when the sweep is: kept against
    /// the very array it was cut from.
    /// </remarks>
    private bool[] VillageGroundToday()
    {
        bool[] today = FreeGroundToday();
        if (_villageGround is not null && ReferenceEquals(_villageGroundFrom, today))
        {
            return _villageGround;
        }

        _freeGroundTodayPieces ??= PiecesOf(today, null, out _);
        int[] pieces = _freeGroundTodayPieces;
        int count = 0;
        for (int i = 0; i < pieces.Length; i++)
        {
            count = System.Math.Max(count, pieces[i]);
        }

        bool[] village = today;
        if (count > 1)
        {
            var touching = new int[count + 1];
            var counted = new HashSet<int>();
            void Score(IReadOnlyList<GridPos> shape)
            {
                counted.Clear();
                for (int i = 0; i < shape.Count; i++)
                {
                    GridPos tile = shape[i];
                    byte walls = WallsOn(tile);
                    if ((walls & 1) == 0) { Touch(new GridPos(tile.X, tile.Y - 1)); }
                    if ((walls & 2) == 0) { Touch(new GridPos(tile.X + 1, tile.Y)); }
                    if ((walls & 4) == 0) { Touch(new GridPos(tile.X, tile.Y + 1)); }
                    if ((walls & 8) == 0) { Touch(new GridPos(tile.X - 1, tile.Y)); }
                }
            }

            void Touch(GridPos tile)
            {
                int index = IndexOfTile(tile);
                if (index >= 0 && pieces[index] != 0 && counted.Add(pieces[index]))
                {
                    touching[pieces[index]]++;
                }
            }

            RebuildTheStandingIndexIfStale();
            for (int i = 0; i < _standingFootprints.Count; i++)
            {
                Score(_standingFootprints[i]);
            }

            Score(new[] { Map.FoundingSite });

            // The piece most shapes touch; a tie to the lowest, which the sweep found first.
            int main = 1;
            for (int piece = 2; piece <= count; piece++)
            {
                if (touching[piece] > touching[main])
                {
                    main = piece;
                }
            }

            village = new bool[today.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                village[i] = pieces[i] == main;
            }
        }

        _villageGround = village;
        _villageGroundFrom = today;
        return village;
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
