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
/// The sharing policy that softens this is a <b>placeholder</b>. The intended form is
/// a manned market or food stall that redistributes within its catchment — a building
/// someone works at, not a menu setting. See DESIGN.md §2.2 and D14.
/// </para>
/// </remarks>
public sealed class Household
{
    public required int Id { get; init; }

    /// <summary>A family name, so a household reads as people rather than "Household 3".</summary>
    public required string Name { get; init; }

    public required GridPos HomePosition { get; init; }

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
    /// <b>The distance to work is a hard bound, not part of the score.</b> The economy
    /// is derived from it (<see cref="VillageEconomy.MaxHomeToWorkTiles"/>), so a home
    /// built beyond it is a family the village cannot feed — and letting the scorer
    /// trade that away for a shorter walk to the granary is exactly how a settlement
    /// ends up with an outlying house that quietly starves.
    /// </para>
    /// </remarks>
    public static GridPos ChooseSite(Core.SimWorld world, GridPos villageCentre)
    {
        ArgumentNullException.ThrowIfNull(world);

        Config.SimConfig config = world.Config;
        int reach = VillageEconomy.MaxHomeToWorkTiles(config);
        int search = VillageEconomy.MaxHomeToVillageTiles(config);

        GridPos best = default;
        int bestScore = int.MaxValue;
        bool found = false;

        // A fixed scan order, so an exact tie always resolves the same way. An
        // unordered tie between two equally good sites is a desync waiting to happen.
        for (int dy = -search; dy <= search; dy++)
        {
            for (int dx = -search; dx <= search; dx++)
            {
                var candidate = new GridPos(villageCentre.X + dx, villageCentre.Y + dy);

                if (!world.Map.Contains(candidate)
                    || world.Map.TerrainAt(candidate) == Terrain.Water
                    || IsTaken(world, candidate))
                {
                    continue;
                }

                int toWork = NearestWorkDistance(world, candidate);
                if (toWork > reach)
                {
                    continue;
                }

                int score = toWork + candidate.ManhattanDistanceTo(world.Granary.Position);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    found = true;
                }
            }
        }

        if (found)
        {
            return best;
        }

        // Nowhere left within reach of work. A real and legible constraint — the
        // valley is full — and the caller decides what to do about it rather than a
        // home being dropped somewhere unsurvivable to keep the code tidy.
        throw new NoRoomToBuildException(
            $"No free ground within {reach} tiles of work and {search} of the village centre.");
    }

    /// <summary>Distance to the nearest place a household could work.</summary>
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

            int distance = from.ManhattanDistanceTo(workplace.Position);
            if (distance < nearest)
            {
                nearest = distance;
            }
        }

        return nearest;
    }

    /// <summary>Whether something already stands here.</summary>
    private static bool IsTaken(Core.SimWorld world, GridPos position)
    {
        for (int i = 0; i < world.Households.Count; i++)
        {
            if (world.Households[i].HomePosition == position)
            {
                return true;
            }
        }

        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.Workplaces[i].Position == position)
            {
                return true;
            }
        }

        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            if (world.StoreBuildings[i].Position == position)
            {
                return true;
            }
        }

        return false;
    }

    public static GridPos PlacementFor(int index, int originX, int originY, int spacing)
    {
        // Walk a square spiral outward so homes stay clustered around the origin.
        int ring = 0;
        while ((2 * ring + 1) * (2 * ring + 1) <= index)
        {
            ring++;
        }

        int withinRing = index - ((2 * ring - 1) * (2 * ring - 1));
        int side = ring == 0 ? 1 : 2 * ring;

        int dx, dy;
        if (ring == 0)
        {
            dx = 0;
            dy = 0;
        }
        else if (withinRing < side)
        {
            dx = ring;
            dy = -ring + 1 + withinRing;
        }
        else if (withinRing < 2 * side)
        {
            dx = ring - 1 - (withinRing - side);
            dy = ring;
        }
        else if (withinRing < 3 * side)
        {
            dx = -ring;
            dy = ring - 1 - (withinRing - 2 * side);
        }
        else
        {
            dx = -ring + 1 + (withinRing - 3 * side);
            dy = -ring;
        }

        return new GridPos(originX + (dx * spacing), originY + (dy * spacing));
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

    /// <summary>True when nobody lives here any more — a house that outlived its family.</summary>
    public bool IsEmpty => _memberIds.Count == 0;
}
