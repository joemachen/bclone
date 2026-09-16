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
    public static GridPos ChooseSite(Core.SimWorld world, GridPos villageCentre)
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
        bool found = false;
        int builtOn = 0;
        int cutOff = 0;

        // Every site that can take a house, scored by its own walks; sorted best first below.
        var sites = new List<(GridPos Site, int Score, int FromVillage)>();

        // The haul routes, once per search, so a house is not sited on the road (D383).
        List<Core.SimWorld.DailyWalk> walks = world.TheDailyWalks();

        // Row order (Y then X — the zone map's set is sorted so), which is the order the old box
        // walked, so an exact tie still resolves the same way. An unordered tie between two
        // equally good sites is a desync waiting to happen.
        foreach (GridPos candidate in world.Zones.WholeResidentialTiles)
        {
            // ⛔⛔ "INSIDE" MEANS THE WHOLE TILE, NOT HALF OF IT (D350). A tile is residential at
            // eight of sixteen quarters (D335) — the economy's threshold — and a house is drawn on
            // the whole tile, so a house on a half-painted edge tile stood 0.4 of a tile past the
            // line the player drew. Joe, with a screenshot of two sites straddling his border:
            // *"the houses are building outside of the painted area. that shouldn't happen."*
            // The brush's ragged rim is a margin nobody builds on — the index holds whole tiles only.
            if (!world.Map.Contains(candidate) || world.Map.TerrainAt(candidate) == Terrain.Water)
            {
                continue;
            }

            if (world.SomethingStandsAt(candidate))
            {
                builtOn++;
                continue;
            }

            // A house that would close the last free tile beside a neighbour is not a site
            // (D383) — one sweep of the valley per surviving candidate, once a day at most.
            if (world.WhatThisWouldWallOff(world.FootprintOf(BuildingKind.Home, candidate)) is not null)
            {
                cutOff++;
                continue;
            }

            // ⭐ THE BOUND IS A BUDGET NOW, NOT A REFUSAL (`forests-and-gathering.md §3.2`,
            // D120): distance SCORES, which is what actually shapes a village — the sum below
            // picks the nearest workable spot every time. **A home beyond the budget is allowed,
            // and it costs food** — the villager really does walk further and really does make
            // fewer trips, and `CanPaintResidential` already warns the player in exactly those
            // terms (D43).
            //
            // ⚠️ Unreachable is still refused, and that is not the same thing: no walk at all is
            // a fact about the valley, not a long walk (D111).
            int toWork = NearestWorkDistance(world, candidate);
            if (toWork == int.MaxValue)
            {
                cutOff++;
                continue;
            }

            // Nearest granary, not "the" granary — a home wants to be near a place it can fetch
            // food from, and with several the right one is whichever is closest to this spot.
            int toStore = NearestStoreDistance(world, candidate, StoreKind.Granary);
            if (toStore == int.MaxValue)
            {
                cutOff++;
                continue;
            }

            int score = toWork + toStore;

            // ⭐ NOT ON THE ROAD (D383): a tile the daily walks pass through scores as if it were
            // four tiles further out per walk through it — a penalty, not a refusal, so a village
            // with nowhere else still gets a house and the road bends. Legible: *"the house went
            // there because the path to the granary runs here."*
            // TIES GO TO THE TILE NEAREST THE VILLAGE. The score is a sum of two distances, so
            // every tile on a shortest path between the work and the granary scores identically
            // — which is most of the plausible sites. Breaking toward the centre keeps the
            // settlement compact, which is what a village actually does, and it is what makes
            // the market and the granary worth standing where they stand.
            int fromVillage = candidate.ManhattanDistanceTo(villageCentre);
            sites.Add((candidate, score, fromVillage));
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
                : a.Site.Y != b.Site.Y ? a.Site.Y.CompareTo(b.Site.Y)
                : a.Site.X.CompareTo(b.Site.X));
            int bestScore = int.MaxValue;
            int bestFromVillage = int.MaxValue;
            GridPos best = default;
            for (int i = 0; i < sites.Count && sites[i].Score < bestScore; i++)
            {
                int detour = world.DetourOfAHouseAt(walks, sites[i].Site);
                if (detour == int.MaxValue)
                {
                    continue;
                }

                int score = sites[i].Score + detour;
                if (score < bestScore || (score == bestScore && sites[i].FromVillage < bestFromVillage))
                {
                    bestScore = score;
                    bestFromVillage = sites[i].FromVillage;
                    best = sites[i].Site;
                }
            }

            if (bestScore != int.MaxValue)
            {
                return best;
            }

            // Every site would cut a daily walk altogether; the best by its own walks is still
            // a house.
            return sites[0].Site;
        }

        // Nowhere in the painted land — and SAY WHICH WAY nowhere (D381). The old sentence
        // ("every one of them is already built on, cut off from the village, or painted only in
        // part") listed every possible reason and named none; the one the player sees now is
        // the count of each, which is what they can act on. With the box gone these three are
        // the only ways a painted tile can fail, so a village that says "none can take one" is
        // telling the truth.
        int painted = world.Zones.ResidentialTiles;
        int whole = world.Zones.WholeResidentialTiles.Count;
        if (painted == 0)
        {
            throw new NoRoomToBuildException("nothing is painted for houses yet");
        }

        var reasons = new List<string>();
        if (builtOn > 0)
        {
            reasons.Add($"{builtOn} built on already");
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
