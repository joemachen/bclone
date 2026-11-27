using Kith.Sim.Logging;
using Kith.Sim.World;

namespace Kith.Sim.Core;

// ---------------------------------------------------------------
//  W4, the cemetery and the groundskeeper (`specs/cemetery.md`, D905) — step 1: who lies unburied
// ---------------------------------------------------------------

public sealed partial class SimWorld
{
    /// <summary>
    /// ⭐ <b>The dead who died before this tick are gone</b> — never buried, never waited for, held by nobody (Joe, D905, call 6 b:
    /// <i>"the dead from before do not get buried too. they are gone."</i>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 0 in a new village, so every death there is buried. <b>Written in every save from W4 on, 0 included</b>; a save without it
    /// is older, and on load it is set to that save's tick — the systems run on <see cref="Tick"/> and then it moves on
    /// (<c>SimLoop.StepOnce</c>), so every death already in the save is before it and every later one at it or after.
    /// </para>
    /// <para>Hashed only when it is not 0, so a new village hashes as it did before W4.</para>
    /// </remarks>
    public ulong BurialsFromTick { get; internal set; }

    /// <summary>The ids of the dead waiting for a grave, by the tick they died and then id. Derived: never saved, never hashed.</summary>
    /// <remarks>
    /// CLAUDE.md's rule — kept where the state changes (<see cref="ADeath"/> from <c>MortalitySystem.Kill</c>,
    /// <see cref="LayToRest"/> at a burial), rebuilt once on load (<see cref="RebuildTheUnburied"/>), never scanned per tick.
    /// </remarks>
    private readonly List<int> _unburied = new();

    /// <inheritdoc cref="_unburied"/>
    public IReadOnlyList<int> Unburied => _unburied;

    /// <summary>Dead since <see cref="BurialsFromTick"/> and not yet laid to rest. Somebody who left the valley never is.</summary>
    public bool AwaitsBurial(Villager villager) =>
        !villager.Alive && villager.DiedAtTick is ulong at && at >= BurialsFromTick && villager.BuriedAtTick is null;

    /// <summary>
    /// How many of the village have ever died — a leaver is not one. Derived: kept in <see cref="ADeath"/>, rebuilt on load,
    /// never saved or hashed. <b>Above nought, the cemetery is learned</b> (`cemetery.md §5.1`; D395, Joe's: the first death).
    /// </summary>
    public int DeathsEver { get; private set; }

    /// <summary>
    /// The small cemetery is the first death's gift (Joe, D906: <i>"first free"</i>): its materials fall away, the work stands,
    /// once. Saved, and hashed sparsely, because it changes what a building costs — the smithy's reason.
    /// </summary>
    public bool AFreeCemeteryIsOwed { get; internal set; }

    internal const string NobodyHasDiedYet = "Nobody has died here yet — the village has no need of a cemetery.";

    /// <summary>
    /// A death, from <c>MortalitySystem.Kill</c>: counted; the first raises the cemetery's one moment and owes the gift; and
    /// they join the end of the waiting list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ No <i>said it</i> flag for the moment (D313's shape): the count goes 0 → 1 once in a village's life and never again,
    /// and an older save with dead in it loads with the count above nought, so it never says it (call 10, D906).
    /// </para>
    /// <para>
    /// The end of the list is its place, by the tick and then the id, with nothing to sort: a death is always on the tick being
    /// stepped, and within a tick the mortality system walks <see cref="Villagers"/> in id order (D907 — a sorted insert scored
    /// zero against every guard, because it never moved anyone). <c>CemeteryTests.TheListEqualsAScanOverDecadesOfDeathsAndASaveMidway</c>
    /// is what fails if that ever stops being true.
    /// </para>
    /// </remarks>
    internal void ADeath(Villager villager)
    {
        // A groundskeeper who dies with one of the dead in their arms sets them down where they fell (§6.2).
        if (villager.CarryingTheDead != 0)
        {
            SetDownTheDead(villager);
        }

        DeathsEver++;
        if (DeathsEver == 1)
        {
            AFreeCemeteryIsOwed = true;
            RaiseMoment(
                "The village's first death",
                $"{villager.FullName} is dead at {villager.AgeYears} — the first this village has lost. They will need somewhere "
                + "to lie: a small cemetery is yours to place, and it will cost you nothing. "
                + $"{Clock.SeasonAndYear()}.");
        }

        if (AwaitsBurial(villager))
        {
            _unburied.Add(villager.Id);
        }
    }

    /// <summary>The cemetery row with the fewest graves (ties: the lower id) — the gift's. Null if the data has none.</summary>
    internal BuildingKind? TheFreeCemetery()
    {
        BuildingKind? free = null;
        int fewest = int.MaxValue;
        for (int id = 0; id < BuildingsCatalog.Count; id++)
        {
            if (BuildingsCatalog[id] is { Graves: > 0 } row && row.Graves < fewest)
            {
                (free, fewest) = ((BuildingKind)id, row.Graves);
            }
        }

        return free;
    }

    /// <summary>On load, after the villagers: the count of the dead, from what the save holds.</summary>
    private void RebuildTheDeathsEver() => DeathsEver = Villagers.Count(v => v.DiedAtTick is not null);

    /// <summary>A burial: the tick is set and they leave the list.</summary>
    internal void LayToRest(Villager villager, ulong tick)
    {
        villager.BuriedAtTick = tick;
        _unburied.Remove(villager.Id);
    }

    /// <summary>On load, after the villagers: the list again, from what the save holds.</summary>
    internal void RebuildTheUnburied()
    {
        _unburied.Clear();
        foreach (Villager villager in Villagers
            .Where(AwaitsBurial)
            .OrderBy(v => v.DiedAtTick)
            .ThenBy(v => v.Id))
        {
            _unburied.Add(villager.Id);
        }
    }

    // ---------------------------------------------------------------
    //  Step 4 (`cemetery.md §5.3, §6`, D909): the graves, the carried, a burial
    // ---------------------------------------------------------------

    /// <summary>Each cemetery's places — the id of who lies in each, 0 for free. Derived from the villagers, never saved or hashed.</summary>
    private readonly Dictionary<int, int[]> _graves = new();

    /// <summary>The ids of the dead a groundskeeper has taken up. Derived from <see cref="Villager.CarryingTheDead"/>.</summary>
    private readonly HashSet<int> _carried = new();

    /// <summary>How many lie in a cemetery of this row: its graves, one a tile; 0 for any workplace that is not one.</summary>
    public int GravesOf(Workplace cemetery) => RowOf(cemetery)?.Graves ?? 0;

    /// <summary>How many of a cemetery's places nobody lies in.</summary>
    public int FreeGraves(Workplace cemetery)
    {
        ArgumentNullException.ThrowIfNull(cemetery);
        if (!_graves.TryGetValue(cemetery.Id, out int[]? places))
        {
            return GravesOf(cemetery);
        }

        int free = 0;
        foreach (int id in places)
        {
            free += id == 0 ? 1 : 0;
        }

        return free;
    }

    /// <summary>
    /// ⭐ Bumped at every burial — the cheapest honest answer to <i>"has any grave changed since I last looked?"</i> (CLAUDE.md), read by
    /// the view's grave patterns. Derived: the count of the buried, rebuilt on load; never saved or hashed.
    /// </summary>
    public int GraveGeneration { get; private set; }

    /// <summary>A cemetery's places — the id of who lies in each, 0 for free — in the sim's own order (row by row from the gate).</summary>
    public IReadOnlyList<int> GravesLaidIn(Workplace cemetery)
    {
        ArgumentNullException.ThrowIfNull(cemetery);
        return PlacesOf(cemetery);
    }

    /// <summary>Whether a groundskeeper has this one of the dead in their arms.</summary>
    public bool IsBeingCarried(Villager dead) => _carried.Contains(dead.Id);

    /// <summary>
    /// The one this cemetery's groundskeeper should fetch next: <b>the oldest of the waiting</b> nobody is carrying whom the cemetery
    /// can be walked to from — or nobody, if it has no free place (`cemetery.md §6.2, §6.4`). An unreachable one is passed over.
    /// </summary>
    public Villager? NextToBury(Workplace cemetery)
    {
        ArgumentNullException.ThrowIfNull(cemetery);
        if (FreeGraves(cemetery) == 0)
        {
            return null;
        }

        foreach (int id in _unburied)
        {
            if (_carried.Contains(id) || FindVillager(id) is not Villager dead)
            {
                continue;
            }

            if (TravelCost.TicksBetween(cemetery.Tile, dead.Tile) != TravelCostField.Unreachable)
            {
                return dead;
            }
        }

        return null;
    }

    /// <summary>The waiting dead lying on a tile, nobody carrying them — the oldest if more than one.</summary>
    internal Villager? WaitingAt(GridPos tile)
    {
        foreach (int id in _unburied)
        {
            if (!_carried.Contains(id) && FindVillager(id) is Villager dead && dead.Tile == tile)
            {
                return dead;
            }
        }

        return null;
    }

    /// <summary>Taken up: in the groundskeeper's arms until they are laid to rest or set down.</summary>
    internal void TakeUp(Villager keeper, Villager dead)
    {
        keeper.CarryingTheDead = dead.Id;
        _carried.Add(dead.Id);
    }

    /// <summary>Set down where the carrier stands — they lie there and wait again (§6.2 step 5).</summary>
    internal void SetDownTheDead(Villager keeper)
    {
        if (keeper.CarryingTheDead == 0)
        {
            return;
        }

        if (FindVillager(keeper.CarryingTheDead) is Villager dead)
        {
            dead.Position = keeper.Position;
        }

        _carried.Remove(keeper.CarryingTheDead);
        keeper.CarryingTheDead = 0;
    }

    /// <summary>
    /// ⭐ THE GRAVE FILLED (§6.2 step 4): whoever the groundskeeper carries is laid in the place <see cref="PlaceFor"/> gives, the
    /// line is said — or, the cemetery filled meanwhile, they are set down at the gate to wait.
    /// </summary>
    internal void Bury(Villager keeper, Workplace cemetery)
    {
        if (FindVillager(keeper.CarryingTheDead) is not Villager dead)
        {
            keeper.CarryingTheDead = 0;
            return;
        }

        if (PlaceFor(cemetery, dead) is not int place)
        {
            SetDownTheDead(keeper);
            return;
        }

        _carried.Remove(dead.Id);
        keeper.CarryingTheDead = 0;
        dead.Position = keeper.Position;
        LayToRest(dead, Tick);
        _recentBurials.Add(dead.Id);
        dead.GraveAt = cemetery.Id;
        dead.GravePlace = place;
        PlacesOf(cemetery)[place] = dead.Id;
        GraveGeneration++;

        string beside = BesideWhom(cemetery, dead, place) is Villager kin ? $" beside {kin.Name}" : string.Empty;
        Narrate($"{dead.FullName} was buried{beside}. {Clock.SeasonAndYear()}.", LogCategory.Death);
    }

    /// <summary>
    /// Where somebody lies (§5.3): a free place next to their partner's grave, else next to one of their household's, else the lowest
    /// free place — so families lie together without anybody planning it. <i>Next to</i> is the same row, one either side, then the
    /// rows in front and behind. Null when the cemetery is full.
    /// </summary>
    internal int? PlaceFor(Workplace cemetery, Villager dead)
    {
        int[] places = PlacesOf(cemetery);
        int width = Math.Max(1, cemetery.ExtentWidth);

        int? NextTo(Villager kin)
        {
            if (kin.GraveAt != cemetery.Id || kin.BuriedAtTick is null)
            {
                return null;
            }

            int p = kin.GravePlace;
            foreach (int q in new[] { p + 1, p - 1, p + width, p - width })
            {
                bool sameRowOrColumn = Math.Abs(q - p) == width || q / width == p / width;
                if (q >= 0 && q < places.Length && sameRowOrColumn && places[q] == 0)
                {
                    return q;
                }
            }

            return null;
        }

        if (dead.PartnerId != 0 && FindVillager(dead.PartnerId) is Villager partner && NextTo(partner) is int byPartner)
        {
            return byPartner;
        }

        for (int i = 0; i < Villagers.Count; i++)
        {
            Villager kin = Villagers[i];
            if (kin.Id != dead.Id && kin.HouseholdId == dead.HouseholdId && NextTo(kin) is int byKin)
            {
                return byKin;
            }
        }

        for (int q = 0; q < places.Length; q++)
        {
            if (places[q] == 0)
            {
                return q;
            }
        }

        return null;
    }

    /// <summary>The partner, else the one of the household, lying next to a place — for the log line.</summary>
    private Villager? BesideWhom(Workplace cemetery, Villager dead, int place)
    {
        int[] places = PlacesOf(cemetery);
        int width = Math.Max(1, cemetery.ExtentWidth);
        Villager? household = null;
        foreach (int q in new[] { place + 1, place - 1, place + width, place - width })
        {
            if (q < 0 || q >= places.Length || places[q] == 0 || FindVillager(places[q]) is not Villager next)
            {
                continue;
            }

            if (next.Id == dead.PartnerId)
            {
                return next;
            }

            household ??= next.HouseholdId == dead.HouseholdId ? next : null;
        }

        return household;
    }

    /// <summary>A cemetery's places, made when first asked — as many as its row's graves.</summary>
    private int[] PlacesOf(Workplace cemetery)
    {
        if (!_graves.TryGetValue(cemetery.Id, out int[]? places))
        {
            places = new int[GravesOf(cemetery)];
            _graves[cemetery.Id] = places;
        }

        return places;
    }

    /// <summary>The sentence a cemetery with anybody in it gives a pull-down or a move (§5.2) — null for an empty one.</summary>
    internal string? WhyItStays(Workplace cemetery)
    {
        int lie = GravesOf(cemetery) - FreeGraves(cemetery);
        return lie == 0
            ? null
            : $"{Capitalised(cemetery.Name)} has {lie} {(lie == 1 ? "grave" : "graves")} in it, and nobody who lies under a stone is "
              + "ever moved — it stays where it is.";
    }

    /// <summary>On load, after the villagers and the workplaces: who lies where, who is carried, and who was buried lately.</summary>
    private void RebuildTheGraves()
    {
        GraveGeneration = Villagers.Count(v => v.BuriedAtTick is not null);
        _graves.Clear();
        _carried.Clear();
        _recentBurials.Clear();
        foreach (Villager buried in Villagers.Where(v => v.BuriedAtTick is not null).OrderBy(v => v.BuriedAtTick).ThenBy(v => v.Id))
        {
            _recentBurials.Add(buried.Id);
        }

        PruneTheRecentBurials();
        foreach (Villager villager in Villagers)
        {
            if (villager.GraveAt != 0 && FindWorkplace(villager.GraveAt) is Workplace cemetery)
            {
                int[] places = PlacesOf(cemetery);
                if (villager.GravePlace >= 0 && villager.GravePlace < places.Length)
                {
                    places[villager.GravePlace] = villager.Id;
                }
            }

            if (villager.CarryingTheDead != 0)
            {
                _carried.Add(villager.CarryingTheDead);
            }
        }
    }

    // ---------------------------------------------------------------
    //  Step 5 (`cemetery.md §6.3, §7`, D910): the place, and the family at the grave
    // ---------------------------------------------------------------

    /// <summary>
    /// The buried of the last <c>grave_visit_days</c>, in the order they were buried — the family's visits read this, never a scan of
    /// the village. Derived: kept in <see cref="Bury"/>, pruned as it is read, rebuilt on load; never saved or hashed.
    /// </summary>
    private readonly List<int> _recentBurials = new();

    /// <summary>
    /// Whether the village's dead have a place (§7): a cemetery standing that is not set to no groundskeepers (call 7 a), and whether
    /// one of those has a free grave. Asked once a season, by the contentment read, only when somebody waits.
    /// </summary>
    public (bool APlace, bool Room) TheDeadHaveAPlace()
    {
        if (JobLimits.For(JobKind.Groundskeeper) == 0)
        {
            return (false, false);
        }

        bool aPlace = false;
        for (int i = 0; i < Workplaces.Count; i++)
        {
            Workplace cemetery = Workplaces[i];
            if (cemetery.Kind != JobKind.Groundskeeper || cemetery.IsSite)
            {
                continue;
            }

            aPlace = true;
            if (FreeGraves(cemetery) > 0)
            {
                return (true, true);
            }
        }

        return (aPlace, false);
    }

    /// <summary>The oldest of theirs still waiting — the household's, or the partner — for the card's sentence. Null for nobody.</summary>
    internal Villager? WhoWaitsFor(Villager person)
    {
        foreach (int id in _unburied)
        {
            if (FindVillager(id) is Villager waiting && (waiting.HouseholdId == person.HouseholdId || waiting.PartnerId == person.Id))
            {
                return waiting;
            }
        }

        return null;
    }

    /// <summary>
    /// The cemetery holding one of this person's own (§6.3) buried in the last <c>grave_visit_days</c> whose grave they have not stood
    /// at since — or null. Of the household, or the partner.
    /// </summary>
    internal Workplace? GraveToVisitFor(Villager person)
    {
        PruneTheRecentBurials();
        for (int i = 0; i < _recentBurials.Count; i++)
        {
            if (FindVillager(_recentBurials[i]) is not Villager buried || buried.Id == person.Id)
            {
                continue;
            }

            bool theirs = buried.HouseholdId == person.HouseholdId || buried.PartnerId == person.Id;
            bool stood = person.LastGraveVisitTick is ulong at && at >= buried.BuriedAtTick;
            if (theirs && !stood && FindWorkplace(buried.GraveAt) is { IsSite: false } cemetery)
            {
                return cemetery;
            }
        }

        return null;
    }

    /// <summary>Off the front of the list, the buried longer ago than the visits run.</summary>
    private void PruneTheRecentBurials()
    {
        ulong window = (ulong)Config.GraveVisitDays * (ulong)Config.TicksPerDay;
        int old = 0;
        while (old < _recentBurials.Count && FindVillager(_recentBurials[old]) is { BuriedAtTick: ulong at } && Tick - at > window)
        {
            old++;
        }

        _recentBurials.RemoveRange(0, old);
    }

    // ---------------------------------------------------------------
    //  Step 6 (`cemetery.md §5.2`, D911): it wears, and its groundskeeper keeps it (Joe's call 9)
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ Whether a cemetery wants its groundskeeper's keeping today (Joe's play note, D915: <i>"Laborer between"</i>) —
    /// <c>cemetery_tend_after_seasons</c> seasons' wear since it was last kept, and fewer than <c>cemetery_tended_days_a_season</c>
    /// days kept this season. Otherwise, with nobody to bury, the keeper helps out as a laborer does. Derived from the stamps.
    /// </summary>
    public bool TheCemeteryWantsKeeping(Workplace cemetery)
    {
        ArgumentNullException.ThrowIfNull(cemetery);
        return cemetery.TendedDaysThisSeason < Config.CemeteryTendedDaysASeason
            && WearTicksOf(cemetery.RaisedAtTick, cemetery.RepairedAtTick) >= (ulong)Config.CemeteryTendAfterSeasons * (ulong)Config.TicksPerSeason;
    }

    /// <summary>
    /// A cemetery at the season's turn: kept <c>cemetery_tended_days_a_season</c> days, it is sound again; otherwise what it has
    /// become is said, in a cemetery's words. Never closed, never a repair site. The count is cleared for the next season.
    /// </summary>
    private void KeepACemeteryAtTheTurn(Workplace cemetery, Condition before, Condition now)
    {
        bool kept = cemetery.TendedDaysThisSeason >= Config.CemeteryTendedDaysASeason;
        cemetery.TendedDaysThisSeason = 0;
        if (kept)
        {
            cemetery.RepairedAtTick = Tick;
            return;
        }

        if (before >= now)
        {
            return;
        }

        string line = now switch
        {
            Condition.Worn => $"{Capitalised(cemetery.Name)} is growing untidy — nobody has kept it lately.",
            Condition.Failing => $"{Capitalised(cemetery.Name)} is overgrown.",
            _ => $"{Capitalised(cemetery.Name)} has gone wild. Its dead are still there, under the grass.",
        };

        Narrate($"{line} {Clock.SeasonAndYear()}.", LogCategory.Building);
    }

    // ---------------------------------------------------------------
    //  Step 7 (`cemetery.md §5.3`, D912): what the cemetery and its stones say
    // ---------------------------------------------------------------

    /// <summary>
    /// The cemetery's card sentence (§5.3, §6.4): who keeps it and who waits — or why nobody does, or that it is full. The bool is
    /// whether it is being kept.
    /// </summary>
    public (bool Kept, string Sentence) SayTheCemetery(Workplace cemetery)
    {
        ArgumentNullException.ThrowIfNull(cemetery);
        int waiting = _unburied.Count;
        string waits = waiting switch { 0 => string.Empty, 1 => " One lies waiting for a grave.", _ => $" {waiting} lie waiting for a grave." };
        if (FreeGraves(cemetery) == 0)
        {
            return (false, "Full — every grave is taken. Another cemetery will be wanted." + waits);
        }

        if (cemetery.WorkerIds.Count == 0)
        {
            string why = JobLimits.For(JobKind.Groundskeeper) == 0
                ? "the number of groundskeepers is set to none"
                : "nobody has been asked to keep it — raise Groundskeepers in Professions";
            return (false, $"Nobody keeps it — {why}." + waits);
        }

        string keeper = FindVillager(cemetery.WorkerIds[0]) is Villager kept ? kept.Name : "somebody";
        return (true, $"Kept by {keeper}." + waits);
    }

    /// <summary>
    /// ⭐ What a stone says (§5.3) — derived from the person's own record each time, so it reads the same in a hundred years:
    /// <i>"Otto Ashford · 64. Died in Winter of Year 67, of old age. Beside Mira."</i>
    /// </summary>
    public string TheStoneSays(Villager dead)
    {
        ArgumentNullException.ThrowIfNull(dead);
        string when = dead.DiedAtTick is ulong at ? $" Died in {SimClock.FromTick(at, Config).SeasonAndYear()}" : string.Empty;
        string how = dead.CauseOfDeath switch
        {
            CauseOfDeath.OldAge => ", of old age",
            CauseOfDeath.Starvation => ", in a hungry year",
            CauseOfDeath.Cold => ", in the cold",
            _ => string.Empty,
        };
        string beside = FindWorkplace(dead.GraveAt) is Workplace cemetery && BesideWhom(cemetery, dead, dead.GravePlace) is Villager kin
            ? $" Beside {kin.Name}."
            : string.Empty;
        return $"{dead.FullName} · {dead.AgeYears}.{when}{how}.{beside}";
    }

    /// <summary>
    /// The household card's line (§6.3) while one of theirs waits for a grave — <i>"Mabel lies at home, waiting for a grave."</i> —
    /// the oldest of them; null when nobody does.
    /// </summary>
    public string? WhoOfTheirsWaits(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);
        foreach (int id in _unburied)
        {
            if (FindVillager(id) is Villager dead && dead.HouseholdId == household.Id)
            {
                string where = household.HasHome && dead.Tile == HomePlaceOf(dead) ? "at home" : "where they fell";
                return IsBeingCarried(dead)
                    ? $"{dead.Name} is being carried to the cemetery."
                    : $"{dead.Name} lies {where}, waiting for a grave.";
            }
        }

        return null;
    }
}
