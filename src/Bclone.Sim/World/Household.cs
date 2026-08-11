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

    /// <summary>A family name, so a household reads as people rather than "Household 3".</summary>
    public required string Name { get; init; }

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
    /// </remarks>
    public GridPos? HomePosition { get; set; }

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

        Config.SimConfig config = world.Config;

        // How far out to LOOK, which is the one bound that has to stay. Every tile in the
        // valley would be correct and would scan nine thousand of them per house; this is
        // the distance the economy budgets for, so it is the right place to stop looking
        // even though it is no longer the place to stop building.
        int search = VillageEconomy.MaxHomeToVillageTiles(config);

        GridPos best = default;
        int bestScore = int.MaxValue;
        int bestFromVillage = int.MaxValue;
        bool found = false;

        // A fixed scan order, so an exact tie always resolves the same way. An
        // unordered tie between two equally good sites is a desync waiting to happen.
        for (int dy = -search; dy <= search; dy++)
        {
            for (int dx = -search; dx <= search; dx++)
            {
                var candidate = new GridPos(villageCentre.X + dx, villageCentre.Y + dy);

                // INSIDE WHAT THE PLAYER PAINTED (D42). The sim still picks the tile —
                // it knows the walk to work and the walk to the store, and a cursor
                // does not — but it only looks where it has been told it may.
                if (!world.Zones.IsResidential(candidate)
                    || !world.Map.Contains(candidate)
                    || world.Map.TerrainAt(candidate) == Terrain.Water
                    || world.SomethingStandsAt(candidate))
                {
                    continue;
                }

                // ⭐ THE BOUND IS A BUDGET NOW, NOT A REFUSAL (`forests-and-gathering.md
                // §3.2`). This used to be `if (toWork > reach) continue;` — ground beyond the
                // economy's budget was simply not offered, which is the same fence catchment
                // was, applied to where you may live rather than where you may work.
                //
                // It still SCORES, which is what actually shapes a village: the sum below
                // picks the nearest workable spot every time. What has gone is the cliff at
                // the edge of the budget. **A home beyond it is allowed, and it costs food** —
                // the villager really does walk further and really does make fewer trips, and
                // `MarkResidential` already warns the player in exactly those terms (D43).
                //
                // ⚠️ Unreachable is still refused, and that is not the same thing: no walk at
                // all is a fact about the valley, not a long walk (D111).
                int toWork = NearestWorkDistance(world, candidate);
                if (toWork == int.MaxValue)
                {
                    continue;
                }

                // Nearest granary, not "the" granary — a home wants to be near a place
                // it can fetch food from, and with several the right one is whichever
                // is closest to this spot.
                int toStore = NearestStoreDistance(world, candidate, StoreKind.Granary);
                if (toStore == int.MaxValue)
                {
                    continue;
                }

                int score = toWork + toStore;

                // TIES GO TO THE TILE NEAREST THE VILLAGE.
                //
                // The score is a sum of two distances, so every tile on a shortest
                // path between the work and the granary scores identically — which is
                // most of the plausible sites. Left to "whichever the scan reached
                // first" the winner is real but arbitrary, and it pushed homes out to
                // whichever end of that path the loop happened to start from.
                //
                // Breaking toward the centre keeps the settlement compact, which is
                // what a village actually does, and it is what makes the market and
                // the granary worth standing where they stand.
                int fromVillage = candidate.ManhattanDistanceTo(villageCentre);

                if (score < bestScore || (score == bestScore && fromVillage < bestFromVillage))
                {
                    bestScore = score;
                    bestFromVillage = fromVillage;
                    best = candidate;
                    found = true;
                }
            }
        }

        if (found)
        {
            return best;
        }

        // Nowhere left in the painted land. A real and legible constraint, and the one
        // the brush exists to create: the village has filled the neighbourhood it was
        // given and needs the player to say where the next one goes (D42).
        //
        // ⚠️ IT MEANS SOMETHING NARROWER THAN IT USED TO, so it says something narrower.
        // With the distance bound demoted to a budget, this is no longer "every spot within
        // N tiles of work is taken" — distance cannot exclude a tile any more. What is left
        // is genuinely full or genuinely unreachable, and telling the player to look at a
        // distance would send them to fix the wrong thing.
        throw new NoRoomToBuildException(
            $"no room left in the residential land — {world.Zones.ResidentialTiles} tiles painted, "
            + "and every one of them is already built on or cut off from the village");
    }

    /// <summary>Distance to the nearest store of a kind, or <c>int.MaxValue</c>.</summary>
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
            int distance = WalkingTiles(world, from, store.Position);
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
            GridPos fallback = world.TheCart?.Position ?? world.Map.FoundingSite;
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
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace workplace = world.Workplaces[i];
            if (workplace.Kind != JobKind.Forager)
            {
                continue;
            }

            int distance = WalkingTiles(world, from, workplace.Position);
            if (distance < nearest)
            {
                nearest = distance;
            }
        }

        return nearest;
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

    /// <summary>This household's food. Not the village's.</summary>
    public Stockpile Stockpile { get; } = new();

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
