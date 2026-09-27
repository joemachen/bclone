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
        // tiles walked, read from the lane tile in front of the door — plus the *apart* term, a
        // side WITH a neighbour's plot along it costs `plot_apart_tiles` (D404 flipped it: it
        // used to charge the open sides, which packed houses into rows) — plus a tile of walk for
        // every yard tile the paint, the water or a building clips off. ⚠️ Not "a full plot first": that was built and measured, and in a
        // cramped valley it sent the founders' second house twelve tiles from the hut past a
        // clipped plot six away — the walk is what feeds people, the yard is what a fence goes
        // round, and one tile of walk per missing tile is the exchange rate the sentence can say.
        //
        // ⭐⭐⭐ THE PATH TURNS THE HOUSE (D411, `organic-housing.md §9.5`, Joe: *"organic housing, and
        // NOT uniform rows of housing"* — with a Foundation screenshot). D388 let the lane pick one
        // of four facings, sorted first, and counted a lane-row tile that only TOUCHED a fronted one
        // as a lane: so a street extended itself plot by plot, and five houses in a column faced
        // one lane (§9.2). Now each tile faces the paths near it — worn ground, the village's daily
        // walks, a lane some house already fronts — at whatever angle they lie (to the 1/64 turn),
        // and the site is priced in the same tiles walked: how far the house stands off its path
        // against the household's hashed setback (P1), how short of its hashed gap to a neighbour's
        // yard (P2) less a little for company within reach of it, and a third house on one line
        // facing one way (P3). ⛔ Every variety is a hash of the household, never the `Rng`.
        int builtOn = 0;
        int cutOff = 0;
        int fencedIn = 0;
        int noRoom = 0;

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

        // The houses already standing or marked, once, for the row term (P3).
        List<(Point Centre, Angle Facing)> standing = HousesAndSites(world, householdId);
        int setback = PlotShape.ByHash(householdId, PlotShape.SetbackSalt, world.Config.HomeSetbackQuarters);
        int gap = PlotShape.ByHash(householdId, PlotShape.GapSalt, world.Config.HomeGapTiles);

        // ⭐⭐ FIRST THE CHEAP HALF OF EVERY TILE'S SCORE, THEN THE DEAR HALF OF THE BEST FEW (D411).
        // The walks and the setback are a few lookups; whether a plot fits, has a gate, walls nobody
        // in and bends no road are a plot, a fence and a sweep of the valley each. Every term left
        // for the second half only ever ADDS (a clipped yard, a crowd, a row, a detour) but one —
        // company, at most `home_company_tiles` off — so a tile whose cheap half, less that, is no
        // better than the best whole score found can never win, and nothing after it in cheap order
        // can either: the search stops there. Exactly the answer scoring every tile would give;
        // measured, the chooser tried every painted tile in full and ran at four times D405's 63 ms.
        var cheap = new List<Cheap>();
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
            (int toWork, GridPos? workAt) = NearestWork(world, front);
            int toStore = toWork == int.MaxValue ? int.MaxValue : NearestStoreDistance(world, front, StoreKind.Granary);

            // P1: which way the paths near this tile lie, and how far off the nearest is.
            (int dx, int dy, int offQuarters) = TowardThePath(world, walked, front);
            bool onAPath = offQuarters >= 0;
            if (!onAPath)
            {
                // No path in reach: the first houses face the village, and the paths start there.
                dx = villageCentre.X - front.X;
                dy = villageCentre.Y - front.Y;
            }

            // ⛔ Out of reach of any path costs what the reach's own edge does, not nothing — found
            // by `AHouseFacesThePathInFrontOfIt`: a site with no path near paid no setback at all and
            // beat every site beside one. The price rises with the distance and then holds.
            int reachQuarters = world.Config.HomePathSearchTiles * 4;
            int offTheLine = System.Math.Abs((onAPath ? offQuarters : reachQuarters) - setback) / 4;
            bool reachable = toWork != int.MaxValue && toStore != int.MaxValue;
            int floor = reachable ? toWork + toStore + offTheLine - world.Config.HomeCompanyTiles : int.MaxValue;
            cheap.Add(new Cheap(
                front, toWork, toStore, offTheLine, FacingToward(dx, dy), onAPath, floor,
                front.ManhattanDistanceTo(villageCentre), workAt));
        }

        cheap.Sort(static (a, b) =>
            a.Floor != b.Floor ? a.Floor.CompareTo(b.Floor)
            : a.FromVillage != b.FromVillage ? a.FromVillage.CompareTo(b.FromVillage)
            : a.Front.Y != b.Front.Y ? a.Front.Y.CompareTo(b.Front.Y)
            : a.Front.X.CompareTo(b.Front.X));

        Candidate? best = null;
        int bestTotal = int.MaxValue;
        int bestDetour = 0;
        Candidate? bestByItsOwnWalks = null;
        // ⭐ BEST FIRST, AND THE ROAD PRICED LAST (D411). A site that walls nobody in waits,
        // unpriced, beside the tiles not yet looked at; each turn resolves whichever is cheaper —
        // the waiting site with the lowest own score (its detour asked, as D383 always asked them,
        // lowest score first), or the next tile by its floor. Once neither can beat the best whole
        // total in hand, nothing can, and the search stops. ⚠️ Asking the detour of every site as it
        // was found, in tile order, was most of a warm-start village's time.
        var waiting = new List<Candidate>();
        int next = 0;
        while (true)
        {
            int w = CheapestWaiting(waiting);
            int waitingScore = w < 0 ? int.MaxValue : waiting[w].Score;
            int nextFloor = next < cheap.Count ? cheap[next].Floor : int.MaxValue;

            // Nothing left can beat what is in hand. ⚠️ Only once something is: with nothing found,
            // every tile is looked at, so the "none can take one" counts below are whole.
            if ((best is not null && System.Math.Min(waitingScore, nextFloor) >= bestTotal)
                || (w < 0 && next >= cheap.Count))
            {
                break;
            }

            if (w >= 0 && waitingScore <= nextFloor)
            {
                Candidate site = waiting[w];
                waiting.RemoveAt(w);

                // ⭐ NOT ON THE ROAD (D383): buildings are obstacles, and a house on the way to the
                // granary costs every haul, every day. The site is stood for a moment and the
                // village's daily walks priced again (`SimWorld.DetourOfAHouseAt`); what they
                // lengthen by is added to its score, tile for tile — a penalty, not a refusal, so a
                // village with nowhere else still gets a house and the road bends. Legible: *"the
                // house went there because the path to the granary runs here."*
                int detour = world.DetourOfAHouseAt(walks, site.Front, site.Facing, householdId);
                if (detour != int.MaxValue)
                {
                    int total = site.Score + detour;
                    if (total < bestTotal || (total == bestTotal && best is Candidate held && site.FromVillage < held.FromVillage))
                    {
                        bestTotal = total;
                        best = site;
                        bestDetour = detour;
                    }
                }

                continue;
            }

            Cheap tile = cheap[next++];
            GridPos front = tile.Front;
            bool anyFits = false;
            bool sited = false;
            bool shutsSomebodyIn = false;
            bool couldNotWin = false;

            // ⭐ The facings in the path's order, those whose yard keeps at least half its ground
            // first (D411: a turned rectangle rasterised on the grid can lose most of its yard, and
            // the first picture had houses standing with none — the path still turns the house, it
            // does not get to take the family's yard). Each is scored, and only one that could still
            // win is stood for the valley's sweep and the road's detour — the dear questions, asked
            // last and least (⚠️ asking them of every facing that fitted took a warm-start village
            // from 86 ms to 151 and the suite from 3m to 5½). The first that walls nobody in is the
            // tile's house.
            var losesMore = new List<PlotShape>();
            List<Angle> facings = FacingsToTry(tile.Toward);
            for (int pass = 0; pass < 2 && !sited; pass++)
            {
                int count = pass == 0 ? facings.Count : losesMore.Count;
                for (int f = 0; f < count && !sited; f++)
                {
                    PlotShape plot;
                    if (pass == 0)
                    {
                        plot = world.PlotFor(front, facings[f], householdId);
                        int lost = TilesClippedOff(world, plot);

                        // ⛔ A gate onto a building is no gate (D404): the yard behind it is shut to
                        // everybody, its own family included. ⛔ And a turned house is a rectangle,
                        // not two tiles (D331: collision is geometry) — turned toward its path it can
                        // reach into the ground beside it, and two turned houses on neighbouring tiles
                        // would stand in each other (D411).
                        if (lost < 0
                            || world.SomethingOverlaps(world.HomeFootprintAt(front, facings[f]))
                            || !world.GateOpensAt(front, facings[f], householdId))
                        {
                            continue;
                        }

                        anyFits = true;
                        if (lost * 2 > plot.Tiles.Count - plot.House.Count + plot.Unreached)
                        {
                            losesMore.Add(plot);
                            continue;
                        }
                    }
                    else
                    {
                        plot = losesMore[f];
                    }

                    if (tile.Floor == int.MaxValue)
                    {
                        // Cut off from the work or the store: counted below, never a house.
                        continue;
                    }

                    int clipped = TilesClippedOff(world, plot);
                    Point centre = world.HomeAnchorOn(front, plot.Facing);
                    (int clear, int neighbour) = AnyoneWithin(standing, centre, gap + 2 + Reach)
                        ? ClearGroundTo(world, plot, householdId, gap + 2)
                        : (int.MaxValue, 0);
                    int crowd = clear < gap ? (gap - clear) * world.Config.HomeCrowdTiles : 0;
                    int company = neighbour != 0 && clear >= gap ? world.Config.HomeCompanyTiles : 0;
                    int inARow = InARow(standing, centre, plot.Facing);
                    int row = inARow > 1 ? (inARow - 1) * world.Config.HomeRowTiles : 0;
                    int roundTheYard = RoundTheYard(world, plot, tile.WorkAt, householdId);
                    int score = tile.ToWork + tile.ToStore + clipped + tile.OffTheLine + crowd + row + roundTheYard - company;

                    // A detour is never negative: a facing no better than the best whole total so
                    // far cannot win, and is not stood for the sweep.
                    if (score >= bestTotal)
                    {
                        couldNotWin = true;
                        continue;
                    }

                    string? walls = world.WhatThisWouldWallOff(
                        world.HomeFootprintAt(front, plot.Facing),
                        world.TrialFence(front, plot.Facing, householdId));
                    if (walls is not null)
                    {
                        shutsSomebodyIn |= walls.StartsWith(Core.SimWorld.FencesSomebodyIn, StringComparison.Ordinal);
                        continue;
                    }

                    sited = true;
                    var site = new Candidate(
                        front, plot.Facing, score, tile.FromVillage, tile.ToWork, tile.ToStore, clipped,
                        company > 0 || crowd > 0 ? neighbour : 0, tile.OnAPath, row, crowd, roundTheYard);

                    if (bestByItsOwnWalks is not Candidate own || score < own.Score)
                    {
                        bestByItsOwnWalks = site;
                    }

                    waiting.Add(site);
                }
            }

            // Why a tile took no house — whole only when nothing was found, which is when it is read.
            if (!anyFits)
            {
                noRoom++;
            }
            else if (tile.Floor == int.MaxValue)
            {
                cutOff++;
            }
            else if (!sited && !couldNotWin)
            {
                if (shutsSomebodyIn)
                {
                    fencedIn++;
                }
                else
                {
                    cutOff++;
                }
            }
        }

        if (best is Candidate winner)
        {
            return new HomeSite(winner.Front, winner.Facing, TheReason(world, winner, bestDetour));
        }

        if (bestByItsOwnWalks is Candidate fallback)
        {
            // Every site would cut a daily walk altogether; the best by its own walks is still a
            // house.
            return new HomeSite(fallback.Front, fallback.Facing, TheReason(world, fallback, 0));
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
                + "(a house on whole-painted tiles, and a lane in front of it)");
        }

        if (cutOff > 0)
        {
            reasons.Add($"{cutOff} cut off from the village");
        }

        // ⭐ §3.3's refusal, in words the player can act on: the plot would fit, but its fence
        // would shut a neighbour's door (D404).
        if (fencedIn > 0)
        {
            reasons.Add($"{fencedIn} whose fence would shut a neighbour in");
        }

        if (painted > whole)
        {
            reasons.Add($"{painted - whole} painted only in part (a home needs a whole tile)");
        }

        throw new NoRoomToBuildException(
            $"{painted} tiles are painted for houses and none can take one: {string.Join(", ", reasons)}");
    }

    /// <summary>A painted tile's cheap half of the score (D411): its walks, its setback, which way its path lies.</summary>
    private readonly record struct Cheap(
        GridPos Front, int ToWork, int ToStore, int OffTheLine, Angle Toward, bool OnAPath, int Floor, int FromVillage,
        GridPos? WorkAt);

    /// <summary>One plot the chooser could take, and the terms that scored it.</summary>
    private readonly record struct Candidate(
        GridPos Front, Angle Facing, int Score, int FromVillage,
        int ToWork, int ToStore, int Clipped, int NeighbourId, bool FacesAPath, int Row, int Crowd, int RoundTheYard);

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

        return clipped + plot.Unreached;
    }

    /// <summary>
    /// ⭐ The walk round a house's own yard when its work lies behind it (§9.5 P5, D411): a tile of
    /// walk for every row of yard behind the house, there and back, and one across — 0 when the work
    /// is ahead of the door or to its side.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ Found by <c>EveryValleyMeetsTheEconomysDistanceBudget</c>: on seed 15 the Fletchers' door
    /// faced its path east, the gatherer's hut lay west, and the family walked 20 tiles round its
    /// own fence to a hut the chooser had scored at 12. D405 found the same walk under the four
    /// facings (founders at ~10 tiles against ~6) and Joe let it stand as the price of fences.
    /// </para>
    /// <para>
    /// ⭐ <b>It prices the site, never the facing</b> — the path still turns the house (P1), so
    /// this does not pull doors toward the granary the way fences §9.4 would have (the reason Joe
    /// declined it, D405). What it does is prefer the tiles where the path runs between the house
    /// and its work: houses face the road to their work, which is how a village round a hub looks.
    /// </para>
    /// </remarks>
    private static int RoundTheYard(Core.SimWorld world, PlotShape plot, GridPos? work, int householdId)
    {
        if (work is not GridPos at)
        {
            return 0;
        }

        Point forward = new Point(Fixed.Zero, -Fixed.FromInt(1)).RotatedBy(plot.Facing);
        Point toWork = Point.CentreOf(at) - Point.CentreOf(plot.Front);
        Fixed ahead = (toWork.X * forward.X) + (toWork.Y * forward.Y);
        if (ahead >= Fixed.Zero)
        {
            return 0;
        }

        int rowsBehind = PlotShape.ByHash(householdId, PlotShape.BackSalt, world.Config.HomeYardBackQuarters) / 4;
        return (2 * rowsBehind) + 1;
    }

    /// <summary>
    /// The waiting site to price next — lowest own score, then nearest the village, then row order
    /// (D383's order, stated, so a tie resolves the same way every run) — or −1.
    /// </summary>
    private static int CheapestWaiting(List<Candidate> waiting)
    {
        int best = -1;
        for (int i = 0; i < waiting.Count; i++)
        {
            if (best < 0 || Before(waiting[i], waiting[best]))
            {
                best = i;
            }
        }

        return best;

        static bool Before(Candidate a, Candidate b) =>
            a.Score != b.Score ? a.Score < b.Score
            : a.FromVillage != b.FromVillage ? a.FromVillage < b.FromVillage
            : a.Front.Y != b.Front.Y ? a.Front.Y < b.Front.Y
            : a.Front.X != b.Front.X ? a.Front.X < b.Front.X
            : a.Facing.Raw < b.Facing.Raw;
    }

    /// <summary>
    /// ⭐ Which way the paths near a tile lie, and how far off the nearest is, in quarter tiles
    /// (§9.5 P1) — or −1 for the distance when none is within <c>home_path_search_tiles</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A path is a lane some house already fronts, a tile the village's daily walks cross, or ground
    /// feet have worn (<c>Paths.At ≥ path_worn_at</c>) — D388's three, less the fourth: ⛔ a tile that
    /// only <em>touches</em> a lane is not a lane (§9.2 cause 1 — it is how one street became five
    /// houses in a column).
    /// </para>
    /// <para>
    /// ⭐ <b>Toward the path's middle, not its nearest tile.</b> A diagonal path is a staircase of
    /// tiles, and the nearest step alone turns a row of houses by a quarter at every other one. The
    /// direction is the sum over every path tile within a tile of the nearest — the bend's own
    /// average — and integer throughout.
    /// </para>
    /// </remarks>
    private static (int Dx, int Dy, int OffQuarters) TowardThePath(
        Core.SimWorld world, HashSet<GridPos> walked, GridPos from)
    {
        int reach = world.Config.HomePathSearchTiles;
        int nearest = int.MaxValue;

        // Outward ring by ring: every tile on ring r is at least r away, so once r² passes the
        // nearest found nothing farther out can be nearer, and the search stops (D411 — the whole
        // square round every painted tile was a third of the chooser's time).
        for (int r = 1; r <= reach && r * r <= nearest; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != r)
                    {
                        continue;
                    }

                    int d2 = (dx * dx) + (dy * dy);
                    if (d2 >= nearest || d2 > reach * reach)
                    {
                        continue;
                    }

                    if (IsAPath(world, walked, new GridPos(from.X + dx, from.Y + dy)))
                    {
                        nearest = d2;
                    }
                }
            }
        }

        if (nearest == int.MaxValue)
        {
            return (0, 0, -1);
        }

        int offQuarters = IntSqrt(nearest * 16);
        int within = offQuarters + 4;
        int box = System.Math.Min(reach, (within / 4) + 1);
        int sumX = 0;
        int sumY = 0;
        for (int dy = -box; dy <= box; dy++)
        {
            for (int dx = -box; dx <= box; dx++)
            {
                int d2 = (dx * dx) + (dy * dy);
                if (d2 == 0 || d2 * 16 > within * within)
                {
                    continue;
                }

                if (IsAPath(world, walked, new GridPos(from.X + dx, from.Y + dy)))
                {
                    sumX += dx;
                    sumY += dy;
                }
            }
        }

        // A path on both sides in balance leaves no direction: face the nearest step, in scan order.
        if (sumX == 0 && sumY == 0)
        {
            for (int dy = -reach; dy <= reach && sumX == 0 && sumY == 0; dy++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    if ((dx * dx) + (dy * dy) == nearest && IsAPath(world, walked, new GridPos(from.X + dx, from.Y + dy)))
                    {
                        sumX = dx;
                        sumY = dy;
                        break;
                    }
                }
            }
        }

        return (sumX, sumY, offQuarters);
    }

    private static bool IsAPath(Core.SimWorld world, HashSet<GridPos> walked, GridPos tile) =>
        world.Map.Contains(tile)
        && (world.Zones.IsLane(tile) || walked.Contains(tile) || world.Paths.At(tile) >= world.Config.PathWornAt);

    /// <summary>The floor of a square root, in integers — so no float is ever in a site's score.</summary>
    private static int IntSqrt(int value)
    {
        int root = 0;
        while ((root + 1) * (root + 1) <= value)
        {
            root++;
        }

        return root;
    }

    /// <summary>The 64 facings a house can take (§9.5 P1: to the 1/64 turn), and each one's forward step.</summary>
    private static readonly (Angle Angle, Point Forward)[] Turns = BuildTurns();

    private static (Angle, Point)[] BuildTurns()
    {
        var turns = new (Angle, Point)[64];
        for (int k = 0; k < 64; k++)
        {
            Angle angle = Angle.FromTurnFraction(k, 64);
            turns[k] = (angle, new Point(Fixed.Zero, -Fixed.FromInt(1)).RotatedBy(angle));
        }

        return turns;
    }

    /// <summary>The 1/64 turn whose forward step points most nearly along (dx, dy) — the lowest on a tie.</summary>
    internal static Angle FacingToward(int dx, int dy)
    {
        if (dx == 0 && dy == 0)
        {
            return Angle.Zero;
        }

        int best = 0;
        Fixed bestDot = Fixed.Zero;
        for (int k = 0; k < Turns.Length; k++)
        {
            Point forward = Turns[k].Forward;
            Fixed dot = (Fixed.FromInt(dx) * forward.X) + (Fixed.FromInt(dy) * forward.Y);
            if (k == 0 || dot > bestDot)
            {
                bestDot = dot;
                best = k;
            }
        }

        return Turns[best].Angle;
    }

    /// <summary>
    /// The facings a tile tries, in order: toward its path, a sixteenth either side, then the four
    /// quarters from the nearest round — a plot the paint or a neighbour clips at the path's own
    /// angle may still fit squarer, and a cramped founding needs every way D388 could turn it.
    /// </summary>
    /// <remarks>
    /// ⛔ Found on the first measurement: with only the nearest quarter as the last resort, fixture
    /// seed 7's founding found no plot for a founder in 161 painted tiles. D388 tried all four.
    /// </remarks>
    private static List<Angle> FacingsToTry(Angle toward)
    {
        Angle sixteenth = Angle.FromTurnFraction(1, 16);
        Angle quarter = Angle.FromRaw((ushort)((toward.Raw + 0x2000) & 0xC000));
        var tries = new List<Angle>(7);
        foreach (Angle a in new[]
        {
            toward, toward + sixteenth, toward - sixteenth,
            quarter, quarter + Angle.Right, quarter - Angle.Right, quarter + Angle.Right + Angle.Right,
        })
        {
            if (!tries.Contains(a))
            {
                tries.Add(a);
            }
        }

        return tries;
    }

    /// <summary>
    /// ⭐ The clear ground between a plot and the nearest other household's, in whole tiles, and
    /// whose it is (§9.5 P2) — looking no further than <paramref name="look"/> tiles; farther is
    /// "on its own" (<c>int.MaxValue</c>, 0).
    /// </summary>
    private static (int Clear, int NeighbourId) ClearGroundTo(
        Core.SimWorld world, PlotShape plot, int householdId, int look)
    {
        int clear = int.MaxValue;
        int neighbour = 0;
        for (int t = 0; t < plot.Tiles.Count; t++)
        {
            GridPos tile = plot.Tiles[t];
            for (int dy = -look - 1; dy <= look + 1; dy++)
            {
                for (int dx = -look - 1; dx <= look + 1; dx++)
                {
                    int between = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) - 1;
                    if (between < 0 || between > clear)
                    {
                        continue;
                    }

                    int owner = world.Zones.PlotOwner(new GridPos(tile.X + dx, tile.Y + dy));
                    if (owner == 0 || owner == householdId)
                    {
                        continue;
                    }

                    // The nearest, and the lowest id among the nearest — stated, so the sentence
                    // names the same neighbour every run.
                    if (between < clear || owner < neighbour)
                    {
                        clear = between;
                        neighbour = owner;
                    }
                }
            }
        }

        return neighbour == 0 ? (int.MaxValue, 0) : (clear, neighbour);
    }

    /// <summary>
    /// How far a plot's ground can lie from its house's centre, in whole tiles, rounded up — the
    /// farthest corner of the widest, deepest yard the config's ranges allow is well inside it.
    /// </summary>
    private const int Reach = 4;

    /// <summary>
    /// Whether any house stands within this many tiles of a centre, twice over (theirs and ours) —
    /// the cheap question before <see cref="ClearGroundTo"/>'s tile-by-tile one (D411: most sites
    /// have nobody near, and scanning thirteen by thirteen round every yard tile of every site to
    /// find nobody was most of the chooser's time).
    /// </summary>
    private static bool AnyoneWithin(List<(Point Centre, Angle Facing)> standing, Point centre, int tiles)
    {
        Fixed reach = Fixed.FromInt(tiles + Reach);
        Fixed square = reach * reach;
        for (int i = 0; i < standing.Count; i++)
        {
            Point apart = standing[i].Centre - centre;
            if ((apart.X * apart.X) + (apart.Y * apart.Y) <= square)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Every house standing or marked, but this household's own — its centre and facing (P3).</summary>
    private static List<(Point Centre, Angle Facing)> HousesAndSites(Core.SimWorld world, int householdId)
    {
        var list = new List<(Point, Angle)>();
        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            if (household.Id != householdId && household.HomePosition is Point home)
            {
                list.Add((home, household.HomeFacing));
            }
        }

        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace place = world.Workplaces[i];
            if (place.Construction is { Kind: BuildingKind.Home, Demolishing: false } site && site.ForHouseholdId != householdId)
            {
                list.Add((place.Position, site.Facing));
            }
        }

        return list;
    }

    /// <summary>
    /// ⭐ How many houses already stand on this house's front line, facing its way (§9.5 P3): within
    /// a sixteenth of a turn, their centres within ¾ of a tile of the line and six tiles along it.
    /// </summary>
    private static int InARow(List<(Point Centre, Angle Facing)> standing, Point centre, Angle facing)
    {
        Fixed across = Fixed.FromRatio(3, 4);
        Fixed along = Fixed.FromInt(6);
        int count = 0;
        for (int i = 0; i < standing.Count; i++)
        {
            int turn = (ushort)(standing[i].Facing.Raw - facing.Raw);
            if (System.Math.Min(turn, 65536 - turn) > 4096)
            {
                continue;
            }

            Point local = (standing[i].Centre - centre).RotatedBy(-facing);
            Fixed x = local.X < Fixed.Zero ? -local.X : local.X;
            Fixed y = local.Y < Fixed.Zero ? -local.Y : local.Y;
            if (y <= across && x <= along)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// The chooser's own sentence for the card (D386, D411): the terms that chose the plot, in the
    /// currency they were scored in.
    /// </summary>
    private static string TheReason(Core.SimWorld world, Candidate chosen, int detour)
    {
        string facing = Compass[((chosen.Facing.Raw + 0x1000) & 0xFFFF) >> 13];
        string toward = chosen.FacesAPath ? $"facing the path to the {facing}" : $"facing the village, to the {facing}";
        string beside = chosen.NeighbourId != 0 && world.FindHousehold(chosen.NeighbourId) is Household neighbour
            ? (chosen.Crowd > 0 ? $"close by the {neighbour.Name}s" : $"near the {neighbour.Name}s")
            : "on its own";

        string road = detour > 0 ? $"; the road bends {detour} for it" : "";
        string yard = chosen.Clipped > 0 ? $"; {chosen.Clipped} of the yard clipped off" : "";
        string row = chosen.Row > 0 ? "; not a third in a line along the path" : "";
        string crowd = chosen.Crowd > 0 ? ", closer than the family would like" : "";
        string behind = chosen.RoundTheYard > 0 ? $"; the work lies behind it, {chosen.RoundTheYard} round the yard" : "";
        return $"{chosen.ToWork} tiles to work and {chosen.ToStore} to the granary, "
            + $"{toward}, {beside}{crowd}{yard}{row}{behind}{road}.";
    }

    private static readonly string[] Compass =
    {
        "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west",
    };

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

            // By walking, not by ruler (D111) — the same correction as NearestWork,
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
    /// How far the nearest food is <b>by walking</b>, in tiles, or <c>int.MaxValue</c> — and where it is (D411), null with none anywhere.
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
    private static (int Tiles, GridPos? At) NearestWork(Core.SimWorld world, GridPos from)
    {
        int nearest = int.MaxValue;
        GridPos? at = null;
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
                at = workplace.Tile;
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
        return anyWorkAtAll ? (nearest, at) : (0, null);
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
