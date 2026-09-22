using Bclone.Sim.Core;
namespace Bclone.Sim.World;

/// <summary>
/// Where a villager lives, and — crucially — where their food is.
/// </summary>
/// <remarks>
/// <para>
/// Food is stored <b>per household</b>, not in one village pile (decision D14). That
/// is what makes one family starving beside a thriving neighbour possible, and that
/// asymmetry is where the inequality stories come from. A single global stockpile
/// would quietly make the village one organism.
/// </para>
/// <para>
/// <b>What softens it is the market</b> (D14, D36) — a building somebody works at, not a
/// menu setting. A marketer carries food from the stores to households below target, and
/// is the only thing in the sim that can reach a dead family's larder. The two automatic
/// sharing policies that stood in for it were deleted by D30.
/// </para>
/// </remarks>
public sealed class Household
{
    public required int Id { get; init; }


    private string _name = string.Empty;

    /// <summary>
    /// What the village calls it. Born with a place name; the player may give it another
    /// (D376) — ⛔ only through <c>SimWorld.Rename</c>, which validates and logs.
    /// </summary>
    public required string Name
    {
        get => _name;
        init { _name = value; BornAs = value; }
    }

    /// <summary>The name it was founded with — what a blank rename hands back.</summary>
    public string BornAs { get; private set; } = string.Empty;

    /// <summary>The player's name for it, or null while it carries the one it was born with — hashed sparsely.</summary>
    public string? GivenName => _name == BornAs ? null : _name;

    internal void Rename(string? given) => _name = given ?? BornAs;

    /// <summary>
    /// Where this family's house stands, or <c>null</c> if they have not got one yet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Null is the founding (D70).</b> Until the cold start, a household <em>was</em> its
    /// home — this was <c>required</c> and non-nullable, there was no homeless state anywhere
    /// in the sim, and every villager belonged to a household from birth. The founders now
    /// arrive with a cart and no roof, so the type has to be able to say so.
    /// </para>
    /// <para>
    /// <b>A nullable rather than a sentinel position, deliberately.</b> Parking a homeless
    /// family at the cart would have changed no readers and lied to all of them:
    /// <c>ShelterAt</c> would have treated the cart as a hearth, and a larder held there is
    /// exactly the right-stuff-in-the-wrong-place shape that has cost this project four
    /// investigations (D25, D29, D48, D57). <see cref="GridPos"/> is a struct, so this is a
    /// genuinely different type and every reader has to say what it means by it — the
    /// compiler performs the audit rather than a person remembering to.
    /// </para>
    /// <para>
    /// <b>Set once a house is raised</b>, which is why it is no longer <c>init</c>.
    /// </para>
    /// <para>
    /// ⭐⭐ <b>A <see cref="Point"/> since gridless 2c (D329)</b>, like every other building anchor.
    /// ⚠️ In practice a home is always on a tile centre and will stay there: housing is painted
    /// with the land brush and the sim picks the tile (D42, D102, and Joe's call on this slice), so
    /// there is no moment at which a player could put one between two squares. **It is the type
    /// that changed, not where houses go** — which is exactly why no golden moved for it.
    /// </para>
    /// </remarks>
    public Point? HomePosition { get; set; }

    /// <summary>
    /// Which way the house faces — toward its lane (D386, `specs/organic-housing.md §3.4`).
    /// Meaningful only while <see cref="HomePosition"/> is set; hashed beside it.
    /// </summary>
    /// <remarks>
    /// ⭐ The plot is derived from this, the position and the household's id — never stored
    /// (D335: a derived index is never hashed). The facing is the one new fact: a 2×1 house
    /// turned a quarter covers different ground, and which row is the lane follows from it.
    /// </remarks>
    public Angle HomeFacing { get; set; }

    /// <summary>
    /// Why the house is where it is, in the chooser's words — *"near the granary, facing the
    /// lane, beside the Ashfords"* (D386). Set with the site; the card reads it. Not hashed: it
    /// restates the choice.
    /// </summary>
    public string WhyHere { get; set; } = "";

    /// <summary>
    /// The tiles the house's fence encloses — house tiles included — as they stood the day the
    /// house was marked out (D388, `specs/organic-housing.md §3.5`). Empty while roofless with no
    /// site. ⛔ Hashed: it is what was built, and the brush never moves it.
    /// </summary>
    /// <remarks>
    /// Joe: *"it feels too malleable."* D386 derived the plot from the house and drew the fence
    /// along the paint, so unpainting a yard moved a fence for free. The fence is timber somebody
    /// carried now: the plot the chooser proposed, less what the paint, the water or a building
    /// clipped that day, fixed at the marking; the recipe carries a log a yard tile; demolition
    /// refunds it with the house; a new couple takes it with the house (D381). Painting or
    /// unpainting beside a built plot changes nothing about it.
    /// </remarks>
    public List<GridPos> FencedTiles { get; set; } = new();

    /// <summary>Which tile the family's house is filed under — derived, never stored.</summary>
    public GridPos? HomeTile => HomePosition?.ToTile();

    /// <summary>True once this family has a roof of their own.</summary>
    /// <remarks>
    /// A reader over the nullable rather than a second flag — nothing to hash, nothing to set
    /// and fail to clear (D66's argument for <see cref="Villager.IsLaborer"/>, one type over).
    /// </remarks>
    public bool HasHome => HomePosition is not null;

    /// <summary>
    /// Where the nth household is built.
    /// </summary>
    /// <remarks>
    /// A compact grid, not a line. Placing each new home one spacing further out
    /// than the last meant the ninth household sat nineteen tiles from the food
    /// source against the first household's five — a round trip three times as long,
    /// on the same number of working hours. Those families simply could not feed
    /// themselves, and the village died of its own sprawl.
    /// <para>
    /// This is the catchment problem from DESIGN.md §2.2 showing up in the economy
    /// before the labour system exists to name it: distance to work is not flavour,
    /// it is whether you eat.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Where to build the next home — near the work, and near the store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This replaces a square spiral that knew nothing about where the work was</b>,
    /// and that ignorance is what made a generated valley uninhabitable. Hand-placed
    /// coordinates hid it for two phases, because the sites had been positioned around
    /// the spiral by hand until it worked; generate the sites instead and the spiral
    /// simply has a new set of them to ignore. Measured: the worst home landed exactly
    /// at the economy's budget with no margin, and the village starved out at year 200
    /// with a full granary. Tightening the ring made it <em>worse</em>, which is what
    /// finally said the problem was structural (`specs/seeded-map-generation.md §12`).
    /// </para>
    /// <para>
    /// <b>The rule is the two trips a household actually makes</b>: out to work, and
    /// over to the store. A site is scored on the sum of those, so a home sits between
    /// its livelihood and its larder rather than optimising one and paying for it
    /// daily with the other. That is one sentence a player can be told, which is the
    /// §2.2 test, and it is the same shape as every other decision here — a ranked
    /// list of plain conditions rather than a weighting nobody can explain (D15).
    /// </para>
    /// <para>
    /// <b>The distance to work used to be a hard bound and is now part of the score</b>
    /// (`forests-and-gathering.md §3.2`). The economy is still derived against
    /// <see cref="VillageEconomy.MaxHomeToWorkTiles"/>, but that is a <em>budget</em> — a
    /// home beyond it is a family the village feeds less well, not one it refuses to house.
    /// The old reasoning feared the scorer trading a long walk to work for a short one to the
    /// granary; the sum below cannot do that, because both walks are in it.
    /// </para>
    /// </remarks>
    public static HomeSite ChooseSite(Core.SimWorld world, GridPos villageCentre, int householdId)
    {
        ArgumentNullException.ThrowIfNull(world);

        // ⛔⛔ THE PAINT, NOT A BOX (D381). This scanned a square of ±`MaxHomeToVillageTiles`
        // round the founding site — described as "the one bound that has to stay", a search
        // bound rather than a refusal. It refused: Joe painted a neighbourhood twelve tiles from
        // the cart at tick 1, nothing in the valley ever looked at it, and the village told him
        // to "paint some land for houses" every day until the last founder froze in Winter Year 1.
        // D120 says distance no longer refuses a home; the box was that refusal under another
        // name. The zone map keeps the whole-painted tiles as an index now, so this walks the
        // paint wherever it is — a far home is scored, chosen if it is the best there is, and
        // costs the food it costs (D120, D43's warning at the brush).
        //
        // ⭐ A PLOT, NOT A TILE (D386, `specs/organic-housing.md §3.3`). Every whole-painted tile
        // is tried as the front tile of a plot at each of the four facings. The house's two tiles
        // must be whole-painted (D350) and free; the yard is the rest of the rectangle, in nobody's
        // plot and on nobody's lane, painted or not — the fence follows the paint, and a yard the
        // brush clipped is a smaller yard, not no house. The score is the chooser's old currency,
        // tiles walked, read from the lane tile in front of the door — plus the *apart* term,
        // which is what packs houses into rows: a side with no neighbour's plot along it costs
        // `plot_apart_tiles` — plus a tile of walk for every yard tile the paint, the water or a
        // building clips off. ⚠️ Not "a full plot first": that was built and measured, and in a
        // cramped valley it sent the founders' second house twelve tiles from the hut past a
        // clipped plot six away — the walk is what feeds people, the yard is what a fence goes
        // round, and one tile of walk per missing tile is the exchange rate the sentence can say.
        //
        // ⭐⭐ THE WALK PICKS THE PLOT, THE LANE PICKS THE DOOR (D388, Joe: *"the homes should have
        // less uniform orientation. this isn't supposed to be suburbs."*). D386 read the walks
        // from the DOOR tile, so every door landed on the granary's side and a street was a row of
        // houses all facing one way — and its tie-break was the facings' fixed order, north first.
        // The walks are read from the plot's own tile now, the same for all four facings; which
        // way the house faces is decided per tile, in order: a facing whose lane row is already a
        // lane (a tile some plot fronts, a worn path, a tile the village's daily walks cross) —
        // the most such tiles wins, so a second row faces the first across the street and a house
        // beside a path fronts it; then the facing whose yard the paint clips least and whose
        // sides have a neighbour; then a hash of the household (⛔ never the `Rng`) — the founders'
        // first houses and a plot with no lane nearby face by hash, which is where the variety
        // comes from.
        bool found = false;
        int builtOn = 0;
        int cutOff = 0;
        int noRoom = 0;

        // Every plot that can take a house, scored by its own walks; sorted best first below.
        var sites = new List<Candidate>();

        // The haul routes, once per search, so a house is not sited on the road (D383) — and the
        // tiles they cross, once, so a house can face the road (D388).
        List<Core.SimWorld.DailyWalk> walks = world.TheDailyWalks();
        var walked = new HashSet<GridPos>();
        for (int i = 0; i < walks.Count; i++)
        {
            List<GridPos> route = world.TravelCost.RouteFrom(walks[i].From, walks[i].To);
            for (int t = 0; t < route.Count; t++)
            {
                walked.Add(route[t]);
            }
        }

        // A house pair is the same for two of the four facings, so the wall-off sweep — a walk of
        // the valley — is asked once per pair rather than once per facing.
        var wallOff = new Dictionary<(GridPos Front, bool AlongY), bool>();

        // Row order (Y then X — the zone map's set is sorted so), which is the order the old box
        // walked, so an exact tie still resolves the same way. An unordered tie between two
        // equally good sites is a desync waiting to happen.
        foreach (GridPos front in world.Zones.WholeResidentialTiles)
        {
            // ⛔⛔ "INSIDE" MEANS THE WHOLE TILE, NOT HALF OF IT (D350). A tile is residential at
            // eight of sixteen quarters (D335) — the economy's threshold — and a house is drawn on
            // the whole tile, so a house on a half-painted edge tile stood 0.4 of a tile past the
            // line the player drew. Joe, with a screenshot of two sites straddling his border:
            // *"the houses are building outside of the painted area. that shouldn't happen."*
            // The brush's ragged rim is a margin nobody builds on — the index holds whole tiles only.
            if (!world.Map.Contains(front) || world.Map.TerrainAt(front) == Terrain.Water)
            {
                continue;
            }

            if (world.SomethingStandsAt(front) || world.Zones.PlotOwner(front) != 0 || world.Zones.IsLane(front))
            {
                builtOn++;
                continue;
            }

            // The walk, read from the plot's own tile: the same whichever way the house faces.
            int toWork = NearestWorkDistance(world, front);
            int toStore = toWork == int.MaxValue ? int.MaxValue : NearestStoreDistance(world, front, StoreKind.Granary);

            // Every facing the plot fits at, with what the lane, the paint and the neighbours say
            // about it; the best of them is this tile's candidate.
            var fits = new List<Facing>(PlotShape.Facings.Count);
            for (int f = 0; f < PlotShape.Facings.Count; f++)
            {
                Angle facing = PlotShape.Facings[f];
                PlotShape plot = world.PlotFor(front, facing, householdId);
                int clipped = TilesClippedOff(world, plot);
                if (clipped < 0)
                {
                    continue;
                }

                int openSides = 0;
                int neighbour = 0;
                for (int side = 0; side < plot.Beside.Count; side++)
                {
                    int along = NeighbourAlong(world, plot.Beside[side]);
                    if (along == 0)
                    {
                        openSides++;
                    }
                    else if (neighbour == 0)
                    {
                        neighbour = along;
                    }
                }

                // A lane row is "already a lane" tile by tile: some plot fronts it, or it touches
                // a tile some plot fronts (a street continues), or the village walks it, or feet
                // have worn it.
                int laneAlready = 0;
                for (int i = 0; i < plot.Lane.Count; i++)
                {
                    if (IsALaneAlready(world, walked, plot.Lane[i]))
                    {
                        laneAlready++;
                    }
                }

                // ⭐⭐ IT CHARGES FOR A NEIGHBOUR, NOT FOR ROOM (D401, Joe: *"could the fence problem
                // be that you're cramming the houses in too closely? they need some room to breathe
                // with yards and pathways and such"* — and he was right, and the old comment here
                // admitted it: a side with NO neighbour cost `plot_apart_tiles`, which is a packing
                // term wearing a spacing term's name). Packing is what makes a fence enclose a
                // door. **Measured, six shipped seeds × fifty years with fences up:** charging for
                // open sides built 14 houses and held 17 people; charging for neighbours built 26
                // and held 55. ⚠️ The magnitude stopped mattering once the sign flipped (−2, −4 and
                // −6 were identical), so the term is a tie-break and the number stays 2.
                int sidesWithANeighbour = plot.Beside.Count - openSides;
                fits.Add(new Facing(facing, f, laneAlready, sidesWithANeighbour * world.Config.PlotApartTiles, clipped, neighbour));
            }

            if (fits.Count == 0)
            {
                noRoom++;
                continue;
            }

            if (toWork == int.MaxValue || toStore == int.MaxValue)
            {
                cutOff++;
                continue;
            }

            // The lane first, then the yard and the neighbours, then the household's own hash —
            // and the first of them whose house would not wall a neighbour in (D383): the sweep
            // is a walk of the valley, asked once per house pair and only of facings that could win.
            int start = PlotShape.FacingByHash(householdId);
            fits.Sort((a, b) =>
                a.LaneAlready != b.LaneAlready ? b.LaneAlready.CompareTo(a.LaneAlready)
                : (a.Apart + a.Clipped) != (b.Apart + b.Clipped) ? (a.Apart + a.Clipped).CompareTo(b.Apart + b.Clipped)
                : ((a.Order - start + 4) % 4).CompareTo((b.Order - start + 4) % 4));

            Facing? chosen = null;
            for (int i = 0; i < fits.Count && chosen is null; i++)
            {
                var pair = (front, PlotShape.LaneDirection(fits[i].Angle).X != 0);
                if (!wallOff.TryGetValue(pair, out bool walls))
                {
                    walls = world.WhatThisWouldWallOff(world.HomeFootprintAt(front, fits[i].Angle)) is not null;
                    wallOff[pair] = walls;
                }

                if (!walls)
                {
                    chosen = fits[i];
                }
            }

            if (chosen is not Facing best)
            {
                cutOff++;
                continue;
            }

            int score = toWork + toStore + best.Apart + best.Clipped;
            int fromVillage = front.ManhattanDistanceTo(villageCentre);
            sites.Add(new Candidate(
                front, best.Angle, best.Order, score, fromVillage, toWork, toStore, best.Apart, best.Clipped,
                best.NeighbourId, best.LaneAlready));
            found = true;
        }

        if (found)
        {
            // ⭐ NOT ON THE ROAD (D383): buildings are obstacles, and a house on the way to the
            // granary costs every haul, every day. The sites are stood for a moment each, best
            // by their own walks first, and the village's daily walks priced again
            // (`SimWorld.DetourOfAHouseAt`); what they lengthen by is added to the site's score,
            // tile for tile — a penalty, not a refusal, so a village with nowhere else still
            // gets a house and the road bends. Legible: *"the house went there because the path
            // to the granary runs here."*
            //
            // ⚠️ EVERY SITE THAT COULD STILL WIN, NOT THE BEST FEW. A detour is never negative,
            // so once a site's own score is no better than the best total so far nothing after
            // it can beat that total and the trials stop — but the fixture's whole road scored
            // 8 and the first free tile off it 10, and a shortlist of six was six road tiles.
            sites.Sort(static (a, b) =>
                a.Score != b.Score ? a.Score.CompareTo(b.Score)
                : a.FromVillage != b.FromVillage ? a.FromVillage.CompareTo(b.FromVillage)
                : a.Front.Y != b.Front.Y ? a.Front.Y.CompareTo(b.Front.Y)
                : a.Front.X != b.Front.X ? a.Front.X.CompareTo(b.Front.X)
                : a.FacingOrder.CompareTo(b.FacingOrder));
            int bestScore = int.MaxValue;
            int bestFromVillage = int.MaxValue;
            Candidate best = sites[0];
            int bestDetour = 0;
            for (int i = 0; i < sites.Count && sites[i].Score < bestScore; i++)
            {
                int detour = world.DetourOfAHouseAt(walks, sites[i].Front, sites[i].Facing, householdId);
                if (detour == int.MaxValue)
                {
                    continue;
                }

                int score = sites[i].Score + detour;
                if (score < bestScore || (score == bestScore && sites[i].FromVillage < bestFromVillage))
                {
                    bestScore = score;
                    bestFromVillage = sites[i].FromVillage;
                    best = sites[i];
                    bestDetour = detour;
                }
            }

            if (bestScore == int.MaxValue)
            {
                // Every site would cut a daily walk altogether; the best by its own walks is
                // still a house.
                best = sites[0];
                bestDetour = 0;
            }

            return new HomeSite(best.Front, best.Facing, TheReason(world, best, bestDetour));
        }

        // Nowhere in the painted land — and SAY WHICH WAY nowhere (D381). The old sentence
        // ("every one of them is already built on, cut off from the village, or painted only in
        // part") listed every possible reason and named none; the one the player sees now is
        // the count of each, which is what they can act on. With the box gone these are the only
        // ways a painted tile can fail, so a village that says "none can take one" is telling
        // the truth.
        int painted = world.Zones.ResidentialTiles;
        int whole = world.Zones.WholeResidentialTiles.Count;
        if (painted == 0)
        {
            throw new NoRoomToBuildException("nothing is painted for houses yet");
        }

        var reasons = new List<string>();
        if (builtOn > 0)
        {
            reasons.Add($"{builtOn} built on or in somebody's plot already");
        }

        if (noRoom > 0)
        {
            reasons.Add($"{noRoom} without room for a plot round them "
                + $"({world.Config.PlotWidth} by {world.Config.PlotDepth} painted tiles and a lane)");
        }

        if (cutOff > 0)
        {
            reasons.Add($"{cutOff} cut off from the village");
        }

        if (painted > whole)
        {
            reasons.Add($"{painted - whole} painted only in part (a home needs a whole tile)");
        }

        throw new NoRoomToBuildException(
            $"{painted} tiles are painted for houses and none can take one: {string.Join(", ", reasons)}");
    }

    /// <summary>One plot the chooser could take, and the terms that scored it.</summary>
    private readonly record struct Candidate(
        GridPos Front, Angle Facing, int FacingOrder, int Score, int FromVillage,
        int ToWork, int ToStore, int Apart, int Clipped, int NeighbourId, int LaneAlready);

    /// <summary>One facing a plot fits at, and what the lane, the paint and the neighbours say about it (D388).</summary>
    private readonly record struct Facing(
        Angle Angle, int Order, int LaneAlready, int Apart, int Clipped, int NeighbourId);

    /// <summary>
    /// Whether a plot can be taken here (§3.1–3.2), and how much of its yard the paint, the water
    /// or a building clips off. <b>−1</b> unless the house's two tiles are whole-painted, on land,
    /// free, in nobody's plot and on nobody's lane, the lane is in nobody's plot, and the door's
    /// tile is land nobody stands on; the yard tiles must be in nobody's plot and on nobody's lane.
    /// Otherwise the count of yard tiles that are off the map, water, unpainted by the half rule,
    /// or built on — zero for a full plot.
    /// </summary>
    internal static int TilesClippedOff(Core.SimWorld world, PlotShape plot)
    {
        for (int i = 0; i < plot.House.Count; i++)
        {
            GridPos tile = plot.House[i];
            if (!world.Map.Contains(tile)
                || world.Map.TerrainAt(tile) == Terrain.Water
                || !world.Zones.WholeResidentialTiles.Contains(tile)
                || world.SomethingStandsAt(tile)
                || world.Zones.PlotOwner(tile) != 0
                || world.Zones.IsLane(tile))
            {
                return -1;
            }
        }

        for (int i = 0; i < plot.Lane.Count; i++)
        {
            if (world.Zones.PlotOwner(plot.Lane[i]) != 0)
            {
                return -1;
            }
        }

        if (!world.Map.Contains(plot.Door)
            || world.Map.TerrainAt(plot.Door) == Terrain.Water
            || world.SomethingStandsAt(plot.Door))
        {
            return -1;
        }

        int clipped = 0;
        for (int i = 0; i < plot.Tiles.Count; i++)
        {
            GridPos tile = plot.Tiles[i];
            if (world.Zones.PlotOwner(tile) != 0 || world.Zones.IsLane(tile))
            {
                return -1;
            }

            if (!world.Map.Contains(tile)
                || world.Map.TerrainAt(tile) == Terrain.Water
                || !world.Zones.IsResidential(tile)
                || world.SomethingStandsAt(tile))
            {
                clipped++;
            }
        }

        return clipped;
    }

    /// <summary>
    /// Whether a tile is already a lane (D388): some plot fronts it, or it touches a tile some plot
    /// fronts (a street continues along it), or a daily walk crosses it, or feet have worn it.
    /// </summary>
    internal static bool IsALaneAlready(Core.SimWorld world, HashSet<GridPos> walked, GridPos tile)
    {
        if (world.Zones.IsLane(tile) || walked.Contains(tile) || world.Paths.At(tile) >= world.Config.PathWornAt)
        {
            return true;
        }

        return world.Zones.IsLane(new GridPos(tile.X + 1, tile.Y))
            || world.Zones.IsLane(new GridPos(tile.X - 1, tile.Y))
            || world.Zones.IsLane(new GridPos(tile.X, tile.Y + 1))
            || world.Zones.IsLane(new GridPos(tile.X, tile.Y - 1));
    }

    /// <summary>The household whose plot lies along this side of a plot, or 0 for nobody.</summary>
    private static int NeighbourAlong(Core.SimWorld world, IReadOnlyList<GridPos> side)
    {
        for (int i = 0; i < side.Count; i++)
        {
            int owner = world.Zones.PlotOwner(side[i]);
            if (owner != 0)
            {
                return owner;
            }
        }

        return 0;
    }

    /// <summary>
    /// The chooser's own sentence for the card (D386): the three terms that chose the plot, in
    /// the currency they were scored in.
    /// </summary>
    private static string TheReason(Core.SimWorld world, Candidate chosen, int detour)
    {
        string facing = PlotShape.LaneDirection(chosen.Facing) switch
        {
            { Y: -1 } => "north",
            { X: 1 } => "east",
            { Y: 1 } => "south",
            _ => "west",
        };

        string beside = chosen.NeighbourId != 0 && world.FindHousehold(chosen.NeighbourId) is Household neighbour
            ? $"beside the {neighbour.Name}s"
            : "on its own";

        string road = detour > 0 ? $"; the road bends {detour} for it" : "";
        string yard = chosen.Clipped > 0 ? $"; {chosen.Clipped} of the yard clipped off" : "";
        string lane = chosen.LaneAlready > 0 ? "the lane" : "a lane of its own";
        return $"{chosen.ToWork} tiles to work and {chosen.ToStore} to the granary, "
            + $"facing {lane} to the {facing}, {beside}{yard}{road}.";
    }

    private static int NearestStoreDistance(Core.SimWorld world, GridPos from, StoreKind kind)
    {
        int nearest = int.MaxValue;
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            StoreBuilding store = world.StoreBuildings[i];
            if (store.Kind != kind)
            {
                continue;
            }

            // By walking, not by ruler (D111) — the same correction as NearestWorkDistance,
            // and it has to be the same or a home's two scores measure different worlds.
            int distance = WalkingTiles(world, from, store.Tile);
            if (distance < nearest)
            {
                nearest = distance;
            }
        }

        // THE CART, AND THEN THE GROUND THEY LANDED ON (D72).
        //
        // At the founding there is no granary, so every candidate tile scored
        // int.MaxValue, every score overflowed to the same value, and ChooseSite refused
        // the entire valley — the village asked for more land it already had, and four
        // founders froze beside 30 logs and a painted field. This is the fallback
        // `specs/cold-start.md §6` names and is the only one of the three that was
        // actually load-bearing.
        //
        // The cart is the honest answer while it is the only store: it IS where the food
        // is. After that, the founding site — because a village with no stores at all
        // still has a middle, and scoring every tile alike is what "no opinion" should
        // look like rather than what "reject everything" looks like.
        if (nearest == int.MaxValue)
        {
            GridPos fallback = world.TheCart?.Tile ?? world.Map.FoundingSite;
            nearest = WalkingTiles(world, from, fallback);
        }

        return nearest;
    }

    /// <summary>
    /// How far the nearest food is <b>by walking</b>, in tiles, or <c>int.MaxValue</c>.
    /// </summary>
    /// <remarks>
    /// <b>⭐ THIS MEASURED WITH A RULER AND EVERY OTHER SYSTEM WALKS (D111)</b>, which is the
    /// *"two competing travel-cost systems"* `CLAUDE.md` forbids by name. It scored candidate
    /// tiles on <c>ManhattanDistanceTo</c> — grid distance, as the crow flies — while since
    /// D40 water is impassable and every real journey goes round the river on the shared
    /// <see cref="TravelCostField"/>. It checked that a tile <em>is not water</em>; it never
    /// checked that a tile is not <em>cut off by</em> water.
    /// <para>
    /// <b>The player could never have made this mistake — only the village could.</b> `Mark`
    /// goes through `CanBuildAt`, which asks the cost field and refuses. `MarkHome`
    /// deliberately skips that check on the written grounds that ChooseSite has already found
    /// reachable ground — a sentence that was simply not true, and that nothing tested.
    /// </para>
    /// </remarks>
    private static int NearestWorkDistance(Core.SimWorld world, GridPos from)
    {
        int nearest = int.MaxValue;
        bool anyWorkAtAll = false;

        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace workplace = world.Workplaces[i];
            if (workplace.Kind != JobKind.Forager)
            {
                continue;
            }

            anyWorkAtAll = true;

            int distance = WalkingTiles(world, from, workplace.Tile);
            if (distance < nearest)
            {
                nearest = distance;
            }
        }

        // ⚠️ NO GATHERING ANYWHERE IS "NO OPINION", NOT "REFUSE EVERYWHERE" — and getting
        // this wrong would have stopped the village building a single house (slice 5). Until
        // the thickets retired there was always somewhere to forage from the first tick, so
        // this could not return "none"; now a cold start has no food source at all until the
        // player raises a gatherer's hut, and every candidate tile would have scored
        // `int.MaxValue`, been skipped, and thrown `NoRoomToBuildException` for ever.
        //
        // Zero, so the score falls back to the walk to the store alone — **exactly D72's
        // fallback for a village with no granary**, and for the same reason: a term with
        // nothing to measure should stop contributing, not veto.
        //
        // ⚠️ It is deliberately NOT the same as "work exists but this tile cannot reach it",
        // which stays `int.MaxValue` and is still refused. One is an empty valley; the other
        // is the far bank (D111).
        return anyWorkAtAll ? nearest : 0;
    }

    /// <summary>
    /// Tiles of actual walk between two points, or <c>int.MaxValue</c> if there is no walk.
    /// </summary>
    /// <remarks>
    /// One helper for both of <see cref="ChooseSite"/>'s distances, so the two halves of a
    /// home's score cannot come to disagree about what "far" means — which is the shape of
    /// bug D111 was.
    /// </remarks>
    private static int WalkingTiles(Core.SimWorld world, GridPos from, GridPos to)
    {
        int cost = world.TravelCost.Cost(from, to);
        return cost == TravelCostField.Unreachable
            ? int.MaxValue
            : cost / TravelCostField.BaseTileCost;
    }

    /// <summary>In-game year of the household's most recent birth. Zero if never.</summary>
    public int LastBirthYear { get; set; }

    /// <summary>
    /// The household is fetching food back up to target — set when the larder reaches
    /// <c>fetch_below_share_percent</c>, cleared when it is at target again (D372).
    /// </summary>
    /// <remarks>
    /// <b>The one bit of state Joe's rule costs.</b> A trigger alone would leave every larder
    /// hovering between a half and a half-plus-an-armful; this is what makes the trips come
    /// back to back instead. Hashed, because a village that forgot it would fetch differently.
    /// </remarks>
    public bool ToppingUpFood { get; set; }

    /// <summary>The same, for firewood.</summary>
    public bool ToppingUpFirewood { get; set; }

    /// <summary>This household's food. Not the village's.</summary>
    public required Stockpile Stockpile { get; init; }

    /// <summary>
    /// Member ids, kept sorted ascending.
    /// </summary>
    /// <remarks>
    /// Sorted because iteration order is part of the determinism contract — an
    /// unordered membership list would make "who eats first" depend on insertion
    /// history. See specs/phase-1-households-and-labour.md §4b.
    /// </remarks>
    private readonly List<int> _memberIds = new();

    public IReadOnlyList<int> MemberIds => _memberIds;

    public void AddMember(int villagerId)
    {
        if (_memberIds.Contains(villagerId))
        {
            return;
        }

        // Insert in sorted position rather than appending and re-sorting, so the
        // list is never briefly out of order.
        int index = _memberIds.BinarySearch(villagerId);
        _memberIds.Insert(index < 0 ? ~index : index, villagerId);
    }

    public bool RemoveMember(int villagerId) => _memberIds.Remove(villagerId);

    /// <summary>Thrown when the valley has no room left within reach of work.</summary>
    /// <remarks>
    /// A real constraint rather than an error: a village can genuinely fill its valley.
    /// It is an exception rather than a null so that a caller has to decide what it
    /// means — a couple that cannot build stays at home — instead of a bad site being
    /// returned quietly and a family starving on it (METHODOLOGY §4).
    /// </remarks>
    public sealed class NoRoomToBuildException : InvalidOperationException
    {
        public NoRoomToBuildException(string message)
            : base(message)
        {
        }
    }

    // THERE IS DELIBERATELY NO IsEmpty HERE.
    //
    // There was: `_memberIds.Count == 0`, summarised as "true when nobody lives here any
    // more". That is the rule HouseholdSystem.IsReadyForAChild spends seventeen lines
    // explaining "was killing every village" — RemoveMember is called when somebody moves
    // out and never when somebody dies, so the list only ever grows and a house full of
    // graves reads as full. Ask `SimWorld.LivingMembersOf` instead; it counts the living,
    // which is what every occupancy question in the sim actually means.
}
