using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// ⭐ The herd lives (D592) and is kept (D593) — on a barn, once a day; `specs/livestock.md §0` and §0.1.
/// </summary>
/// <remarks>
/// <para>
/// <b>The woodpile's shape a second time</b> (`livestock.md §3`, D53): grazing is free spring to autumn; winter is not,
/// and the honest answer to <i>"why did the cattle die?"</i> is <b>the hay chain failed</b> — one beast for each winter
/// day there is too little hay <i>put out on the pasture</i> (Joe, D60: <i>the herd starves</i>). Since D593 the herd
/// eats only what a herdsman has carried out (<see cref="Workplace.HayPutOut"/>), so <i>nobody fed it out</i> is as
/// true a failure as <i>the barn ran dry</i>.
/// </para>
/// <para>
/// ⭐ <b>Once a day, never per tick</b>, on the day's first tick (the hearth's beat). The pasture is read from
/// <c>ZoneMap.WorkGroundTiles</c>, a count kept where the paint changes — never a walk of the ground (CLAUDE.md).
/// </para>
/// </remarks>
public sealed partial class SimWorld
{
    /// <summary>How many head a barn's pasture holds: its painted tiles over the tiles a head grazes.</summary>
    public int PastureFor(Workplace barn)
    {
        ArgumentNullException.ThrowIfNull(barn);
        return Config.Animals.Count == 0 ? 0 : Zones.WorkGroundTiles(barn.Id) / Config.Animals[0].PastureTilesPerHead;
    }

    /// <summary>The store a barn keeps its hay in — the barn's own building, on its own tile.</summary>
    public StoreBuilding? BarnStoreOf(Workplace barn)
    {
        ArgumentNullException.ThrowIfNull(barn);
        return StoreAt(barn.Tile) is { Kind: StoreKind.Barn } store ? store : null;
    }

    /// <summary>The hay a barn should hold by winter: its pasture's full herd's winter, with the larder's margin.</summary>
    public int WinterHayWanted(Workplace barn) =>
        PastureFor(barn) * VillageEconomy.HayOneHeadEatsAWinter(Config);

    /// <summary>The hay a barn holds — in its store and already put out on the pasture.</summary>
    public int HayHeldFor(Workplace barn)
    {
        ArgumentNullException.ThrowIfNull(barn);
        return (BarnStoreOf(barn)?.Store[Goods.Hay] ?? 0) + barn.HayPutOut;
    }

    /// <summary>
    /// Whether the barn's winter hay is short and pasture is left to go over this year — the quota's question, with no
    /// walk of the ground (it is asked every tick; the tile itself is found when the herdsman decides).
    /// </summary>
    public bool HayIsShort(Workplace barn)
    {
        ArgumentNullException.ThrowIfNull(barn);
        return !Clock.IsWinter && barn.HerdHead > 0 && HayHeldFor(barn) < WinterHayWanted(barn) && !MayNotStore(barn)
            && barn.HayCutThisYear < Zones.WorkGroundTiles(barn.Id);
    }

    /// <summary>
    /// The next pasture tile to cut this year, or null when the herd's winter is in or the pasture has been gone over —
    /// the pasture's own order and a cursor (<see cref="Workplace.HayCutThisYear"/>), never a per-tile memory (D593).
    /// </summary>
    /// <remarks>
    /// ⚠️ A tile painted once may since stand under a house, or lie where nobody can walk: it is passed over, never
    /// walked at for ever (a herdsman stalled a whole autumn on one such tile before this, D593).
    /// </remarks>
    public GridPos? NextPastureToCut(Workplace barn)
    {
        if (!HayIsShort(barn))
        {
            return null;
        }

        IReadOnlyList<int> owned = Zones.WorkGroundOf(barn.Id);
        for (int i = barn.HayCutThisYear; i < owned.Count; i++)
        {
            GridPos tile = Zones.PositionOf(owned[i]);
            if (IsWorkablePasture(tile))
            {
                return tile;
            }
        }

        return null;
    }

    /// <summary>Move the cursor past the tile just cut — whatever unworkable tiles lay between (D593).</summary>
    internal void PassTheCutTile(Workplace barn, GridPos cut)
    {
        IReadOnlyList<int> owned = Zones.WorkGroundOf(barn.Id);
        for (int i = barn.HayCutThisYear; i < owned.Count; i++)
        {
            if (Zones.PositionOf(owned[i]) == cut)
            {
                barn.HayCutThisYear = i + 1;
                return;
            }
        }

        barn.HayCutThisYear++;
    }

    /// <summary>Grass or wood, nothing standing on it, and the village can walk there.</summary>
    private bool IsWorkablePasture(GridPos tile) =>
        Map.TerrainAt(tile) is Terrain.Grass or Terrain.Forest or Terrain.Sapling
        && !StandsOn(tile)
        && TravelCost.CanReach(FirstHomeOrFoundingSite(), tile);

    /// <summary>Whether the barn's store has no room left for another armful (or no store at all).</summary>
    private bool MayNotStore(Workplace barn) =>
        BarnStoreOf(barn) is not StoreBuilding store || store.Store.Held + Config.CarryCapacity > store.Store.Capacity;

    /// <summary>Where a herdsman puts the hay out: the pasture's first workable tile, in its own order.</summary>
    public GridPos? WhereTheHayGoesOut(Workplace barn)
    {
        ArgumentNullException.ThrowIfNull(barn);
        IReadOnlyList<int> owned = Zones.WorkGroundOf(barn.Id);
        for (int i = 0; i < owned.Count; i++)
        {
            GridPos tile = Zones.PositionOf(owned[i]);
            if (IsWorkablePasture(tile))
            {
                return tile;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether the herd needs hay carried out: winter — or its last <c>hay_feed_ahead_days</c> of autumn, so the herd does
    /// not meet the first frost with nothing out — a herd, less than that many days of it on the pasture, and hay in the
    /// barn to carry.
    /// </summary>
    public bool HerdWantsFeeding(Workplace barn)
    {
        ArgumentNullException.ThrowIfNull(barn);
        bool feedingTime = Clock.IsWinter
            || (Clock.Season == Season.Fall && Clock.DayOfSeason > Config.DaysPerSeason - Config.HayFeedAheadDays);
        if (!feedingTime || barn.HerdHead <= 0 || Config.Animals.Count == 0 || WhereTheHayGoesOut(barn) is null)
        {
            return false;
        }

        int ahead = barn.HerdHead * Config.Animals[0].HayPerHeadPerWinterDay * Config.HayFeedAheadDays;
        return barn.HayPutOut < ahead && (BarnStoreOf(barn)?.Store[Goods.Hay] ?? 0) > 0;
    }

    /// <summary>Whether a beast born to a full pasture is waiting to be culled.</summary>
    public bool CullIsOwed(Workplace barn)
    {
        ArgumentNullException.ThrowIfNull(barn);
        return barn.HerdHead > PastureFor(barn);
    }

    /// <summary>A day's grazing, feeding, growth and starvation for every herd — on the day's first tick only.</summary>
    internal void TendTheHerds()
    {
        if (Config.Animals.Count == 0 || Tick % (ulong)Config.TicksPerDay != 0UL)
        {
            return;
        }

        AnimalRow animal = Config.Animals[0];
        bool winter = Clock.IsWinter;
        bool newYear = Clock.Season == Season.Spring && Clock.DayOfSeason == 1;
        for (int i = 0; i < Workplaces.Count; i++)
        {
            Workplace barn = Workplaces[i];
            if (barn.Kind != JobKind.Herdsman || barn.IsSite)
            {
                continue;
            }

            // The grass comes back with the year: every tile may be cut again.
            if (newYear)
            {
                barn.HayCutThisYear = 0;
            }

            if (barn.HerdHead <= 0)
            {
                continue;
            }

            if (winter)
            {
                FeedTheHerd(barn, animal);
            }
            else
            {
                GrazeAndGrow(barn, animal);
            }
        }
    }

    /// <summary>
    /// A winter day: the herd eats the hay put out on its pasture, and if there is too little a beast is lost.
    /// </summary>
    private void FeedTheHerd(Workplace barn, AnimalRow animal)
    {
        int wanted = barn.HerdHead * animal.HayPerHeadPerWinterDay;
        if (barn.HayPutOut >= wanted)
        {
            barn.HayPutOut -= wanted;
            return;
        }

        bool inTheBarn = (BarnStoreOf(barn)?.Store[Goods.Hay] ?? 0) > 0;
        barn.HayPutOut = 0;
        barn.HerdHead--;
        Narrate(
            inTheBarn
                ? $"One of the {animal.Plural} at {barn.Name} starved with hay still in the barn — nobody carried it out. "
                    + $"{barn.HerdHead} left. {Clock.SeasonAndYear()}."
                : $"One of the {animal.Plural} at {barn.Name} starved — the barn ran out of hay. "
                    + $"{barn.HerdHead} left. {Clock.SeasonAndYear()}.",
            LogCategory.Warning);
    }

    /// <summary>
    /// A grazing day: the herd grows by its head count, a beast for every <c>head_days_per_birth</c>, while the pasture
    /// has room — and one more beyond it, which waits for a herdsman to cull it (D593). With nobody seated at the barn,
    /// a herd larger than its pasture loses its extra to the overgrazed ground, a beast a day and no yield.
    /// </summary>
    private void GrazeAndGrow(Workplace barn, AnimalRow animal)
    {
        int pasture = PastureFor(barn);
        if (barn.HerdHead > pasture && barn.WorkerIds.Count == 0)
        {
            barn.HerdHead--;
            Narrate(
                $"One of the {animal.Plural} at {barn.Name} starved on overgrazed ground — nobody is keeping the herd. "
                + $"{barn.HerdHead} left. {Clock.SeasonAndYear()}.",
                LogCategory.Warning);
            return;
        }

        barn.HerdGrowth += barn.HerdHead;
        while (barn.HerdGrowth >= animal.HeadDaysPerBirth)
        {
            if (barn.HerdHead > pasture)
            {
                // A cull is already owed: the next calf waits on it.
                barn.HerdGrowth = animal.HeadDaysPerBirth;
                return;
            }

            barn.HerdGrowth -= animal.HeadDaysPerBirth;
            barn.HerdHead++;
        }
    }

    /// <summary>
    /// The herdsman's cull at the barn (D593): one beast above the pasture into meat and leather in the barn's buffer —
    /// the laborers carry it away as any buffer's. False when no cull is owed.
    /// </summary>
    internal bool CullABeast(Workplace barn)
    {
        if (!CullIsOwed(barn) || Config.Animals.Count == 0)
        {
            return false;
        }

        AnimalRow animal = Config.Animals[0];
        barn.HerdHead--;
        barn.Store.Add(Goods.Meat, animal.MeatPerHead);
        barn.Store.Add(Goods.Leather, animal.LeatherPerHead);
        RecordFoodProduced(Goods.Meat, animal.MeatPerHead);
        return true;
    }
}
