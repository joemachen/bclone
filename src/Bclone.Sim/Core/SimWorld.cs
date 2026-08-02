using Bclone.Sim.Config;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// The single root of all simulation state.
/// </summary>
/// <remarks>
/// <para>
/// If it affects the simulation, it lives here (or is reachable from here) and
/// is mixed into <see cref="StateHash"/>. One root makes "what is the state?" a
/// question with exactly one answer — which is what makes determinism testable,
/// saves possible, and desyncs findable.
/// </para>
/// <para>
/// The renderer may <em>read</em> this. It must never write to it (DESIGN.md §3).
/// </para>
/// </remarks>
public sealed class SimWorld
{
    /// <summary>
    /// Ticks elapsed. The sim's only notion of time — there is no wall clock in
    /// here, by design and by build-time enforcement (BannedSymbols.txt).
    /// </summary>
    public ulong Tick { get; internal set; }

    /// <summary>Seeded generator. Its state is part of world state.</summary>
    public DeterministicRandom Rng;

    /// <summary>Tunables for this run. Immutable once the run starts.</summary>
    public SimConfig Config { get; }

    /// <summary>Structured sink. Entries are stamped with the current tick.</summary>
    public ISimLogger Logger { get; }

    /// <summary>
    /// The calendar for the current tick.
    /// </summary>
    /// <remarks>
    /// Computed on demand rather than stored. Storing it meant the clock lagged
    /// <see cref="Tick"/> by one — the tick counter advanced at the end of
    /// <c>StepOnce</c> while the cached calendar still described the tick just
    /// finished — so the UI would have shown a date and a tick that disagreed.
    /// Deriving it makes that class of bug impossible, and keeps the calendar out
    /// of the state hash entirely, since it carries no information the tick does
    /// not already have.
    /// </remarks>
    public SimClock Clock => SimClock.FromTick(Tick, Config);

    /// <summary>
    /// Everyone in the village, ordered by id and never reordered.
    /// </summary>
    /// <remarks>
    /// A stable ordered list, deliberately not a dictionary. .NET guarantees no
    /// iteration order for <c>Dictionary</c>, and with N villagers competing for M
    /// jobs, iteration order decides who picks first — which would make the whole
    /// village non-deterministic (spec §4b).
    /// </remarks>
    public List<Villager> Villagers { get; } = new();

    /// <summary>Every household, ordered by id.</summary>
    public List<Household> Households { get; } = new();

    /// <summary>
    /// The shared travel-cost field. One instance, for everything that asks
    /// "how far is that" (DESIGN.md §2.6).
    /// </summary>
    public TravelCostField TravelCost { get; }

    /// <summary>
    /// The valley, generated from this run's seed (D18).
    /// </summary>
    /// <remarks>
    /// Immutable once generated, so it is world state without being world <em>change</em>
    /// — the hash covers it once and it can never drift. When terrain becomes mutable
    /// (a felled stand, a paved road) that stops being true and this needs revisiting.
    /// </remarks>
    public GeneratedMap Map { get; }

    /// <summary>Where the player has said the village may build (D42).</summary>
    public ZoneMap Zones { get; private set; } = null!;

    /// <summary>How much of each good the player wants kept (D62).</summary>
    /// <remarks>
    /// Player intent, so it lives beside <see cref="Zones"/> rather than in
    /// <see cref="Config"/>: a config value is what the world is made of, and this is
    /// something somebody decided during a run.
    /// </remarks>
    public StockLimits StockLimits { get; } = new();

    /// <summary>The berry patch.</summary>
    public FoodSource FoodSource { get; }

    /// <summary>The stand of trees.</summary>
    public TreeStand TreeStand { get; }

    /// <summary>Every workplace, ordered by id.</summary>
    public List<Workplace> Workplaces { get; } = new();

    /// <summary>
    /// Buildings that exist to hold things — the granary and the materials shed.
    /// </summary>
    /// <remarks>
    /// Ordered by id and never reordered, for the same reason the villagers are: this
    /// list decides which store a producer walks to when two are equally close, and a
    /// tie resolved by iteration order is a desync waiting to happen.
    /// </remarks>
    public List<StoreBuilding> StoreBuildings { get; } = new();

    // ---------------------------------------------------------------
    //  Stores, in the plural (D38)
    // ---------------------------------------------------------------
    //
    // There used to be a Granary, a StorageShed and a Market here, each returning
    // the FIRST building of its kind. Thirteen call sites read them, and every one
    // was correct only while the village had exactly one of each — so the moment
    // placement let a player build a second granary, it would have been silently
    // ignored by, among other things, the gate that decides whether anyone is born.
    //
    // They are deleted rather than kept alongside these, deliberately: each of those
    // call sites needed a DECISION, not a rename — is this question about the whole
    // village, or about the nearest building? Leaving a singular accessor in place
    // would have let the ones nobody re-read keep the old answer.

    /// <summary>Total food across every granary in the village.</summary>
    public int FoodInGranaries() => TotalIn(StoreKind.Granary, static store => store.Food);

    /// <summary>Total logs across every shed.</summary>
    public int LogsInSheds() => TotalIn(StoreKind.Shed, static store => store.Logs);

    /// <summary>Total firewood across every shed — what a household can actually fetch.</summary>
    public int FirewoodInSheds() => TotalIn(StoreKind.Shed, static store => store.Firewood);

    /// <summary>How much food the village's granaries can hold between them.</summary>
    public int GranaryCapacity() => TotalIn(StoreKind.Granary, static store => store.Capacity);

    private int TotalIn(StoreKind kind, Func<Stockpile, int> read)
    {
        int total = 0;
        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            if (StoreBuildings[i].Kind == kind)
            {
                total += read(StoreBuildings[i].Store);
            }
        }

        return total;
    }

    /// <summary>
    /// The nearest store of a kind that satisfies <paramref name="usable"/>, or null.
    /// </summary>
    /// <remarks>
    /// <b>Nearest by travel cost, not by list order</b>, so a village with two granaries
    /// sends people to the one they can actually get to — which is the whole point of
    /// being allowed to build a second. Unreachable stores are skipped: with water
    /// impassable (D40) a granary on the far bank is not a long walk, it is no walk at
    /// all. Ties go to the lower id so two runs never disagree.
    /// </remarks>
    public StoreBuilding? NearestStore(GridPos from, StoreKind kind, Func<StoreBuilding, bool> usable)
    {
        ArgumentNullException.ThrowIfNull(usable);

        StoreBuilding? best = null;
        int bestCost = int.MaxValue;

        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            StoreBuilding store = StoreBuildings[i];
            if (store.Kind != kind || !usable(store))
            {
                continue;
            }

            int cost = TravelCost.Cost(from, store.Position);
            if (cost != TravelCostField.Unreachable && cost < bestCost)
            {
                bestCost = cost;
                best = store;
            }
        }

        return best;
    }

    // ---------------------------------------------------------------
    //  Placement (D43) — the first thing the player actually does
    // ---------------------------------------------------------------

    /// <summary>Next id for a workplace, so construction sites never collide with one.</summary>
    private int _nextWorkplaceId = 1;

    // ---------------------------------------------------------------
    //  The residential brush (D42)
    // ---------------------------------------------------------------

    /// <summary>
    /// Paint one tile as somewhere the village may build homes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Painting is <b>permissive about where and firm about what</b>: water and ground
    /// outside the valley are simply never painted, so the shape the player gets is the
    /// shape they can actually use, and no home is ever promised on a tile that could
    /// not hold one.
    /// </para>
    /// <para>
    /// <b>The distance check happens here, once</b>, rather than on every house the
    /// village later builds inside it (D42). That is the whole reason zoning was the
    /// better answer than per-house placement: one considered warning about a
    /// neighbourhood, instead of a nag the player learns to click past.
    /// </para>
    /// </remarks>
    public PlacementVerdict PaintResidential(GridPos tile)
    {
        if (!Map.Contains(tile))
        {
            return PlacementVerdict.No("That is outside the valley.");
        }

        if (Map.TerrainAt(tile) == Terrain.Water)
        {
            return PlacementVerdict.No("Nobody can live on the water.");
        }

        Zones.SetResidential(tile, true);

        int toWork = NearestForageDistance(tile);
        int budget = VillageEconomy.MaxHomeToWorkTiles(Config);
        if (toWork > budget)
        {
            return PlacementVerdict.Yes(
                $"That corner is {toWork} tiles from the nearest food; the village budgets " +
                $"{budget}. Families there will go hungry.");
        }

        return PlacementVerdict.Fine;
    }

    /// <summary>
    /// Set or clear how much of a good the village should keep (D62).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Always obeyed, and warned about when it is below what the village needs to
    /// live.</b> That is the whole of D62's "derived floor, player ceiling": the economy goes
    /// on deriving the floor, the player sets the ceiling, and a ceiling set under the floor
    /// is a decision with a consequence rather than an error to reject. A game that refuses
    /// the player's number is arguing with them; a game that obeys it silently has killed
    /// them without saying so.
    /// </para>
    /// <para>
    /// <b>The warning fires here, once, when the limit is set</b> — not every tick the
    /// village is short. That is D42's rule about the distance warning happening per brush
    /// stroke rather than per house: one considered sentence, instead of a nag the player
    /// learns to click past.
    /// </para>
    /// </remarks>
    public PlacementVerdict SetStockLimit(Goods goods, int? limit)
    {
        if (!StockLimits.Set(goods, limit))
        {
            return PlacementVerdict.Fine;
        }

        if (limit is null)
        {
            return PlacementVerdict.Fine;
        }

        int floor = VillageEconomy.SurvivalFloorFor(Config, goods, Population, Households.Count);
        if (limit.Value >= floor)
        {
            return PlacementVerdict.Fine;
        }

        string name = goods switch
        {
            Goods.Food => "food",
            Goods.Logs => "logs",
            Goods.Firewood => "firewood",
            _ => goods.ToString().ToLowerInvariant(),
        };

        return PlacementVerdict.Yes(
            $"The village needs {floor} {name} to see the year out and you have asked it to "
            + $"stop at {limit.Value}. It will do as you say.");
    }

    /// <summary>Un-paint a tile. Homes already standing there stay where they are.</summary>
    /// <remarks>
    /// Erasing is about where the village may build <em>next</em>, not a demolition
    /// order. Pulling houses down because somebody adjusted a brush would be a cruel
    /// reading of an undo.
    /// </remarks>
    public void EraseResidential(GridPos tile) => Zones.SetResidential(tile, false);

    /// <summary>
    /// Whether the village has run out of room to build and needs the player.
    /// </summary>
    /// <remarks>
    /// The other half of the brush (D42): the game says when a decision is due rather
    /// than expecting the player to notice. Reduce babysitting, do not add it (§1.2).
    /// </remarks>
    public bool NeedsMoreResidentialLand { get; internal set; }

    /// <summary>Distance from a tile to the nearest place anyone forages.</summary>
    private int NearestForageDistance(GridPos from)
    {
        int nearest = int.MaxValue;
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Kind != JobKind.Forager)
            {
                continue;
            }

            int distance = from.ManhattanDistanceTo(Workplaces[i].Position);
            if (distance < nearest)
            {
                nearest = distance;
            }
        }

        return nearest == int.MaxValue ? 0 : nearest;
    }

    /// <summary>
    /// The land the exiles arrive having already chosen to live on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A village founded with nothing painted could never build a house at all, and
    /// the first thing the game asked of a player would be a decision they had no basis
    /// for (D43, spec §12.7). So the exiles turn up having decided where to live —
    /// plausible in fiction, and a gentle tutorial: you see what a zone looks like
    /// before being asked to paint one.
    /// </para>
    /// <para>
    /// Deliberately modest. Big enough that an unattended village behaves as it always
    /// has, small enough that a player who is actually growing the place will meet the
    /// brush rather than never needing it.
    /// </para>
    /// </remarks>
    private void PaintTheStarterZone(GridPos origin, SimConfig config)
    {
        int radius = config.StartingResidentialRadius;

        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) > radius)
                {
                    continue;
                }

                var tile = new GridPos(origin.X + dx, origin.Y + dy);
                if (Map.Contains(tile) && Map.TerrainAt(tile) != Terrain.Water)
                {
                    Zones.SetResidential(tile, true);
                }
            }
        }
    }

    /// <summary>
    /// Whether a building may be marked here, and what the player should be told.
    /// </summary>
    /// <remarks>
    /// Pure — asks nothing of the world but questions, changes nothing. That is what
    /// lets the view call it every frame under the cursor and show the answer before
    /// anybody commits to it, which is the whole of D43's "warn and allow".
    /// </remarks>
    public PlacementVerdict CanBuildAt(BuildingKind kind, GridPos position)
    {
        if (!Map.Contains(position))
        {
            return PlacementVerdict.No("That is outside the valley.");
        }

        if (Map.TerrainAt(position) == Terrain.Water)
        {
            return PlacementVerdict.No("The ground there is under water.");
        }

        if (SomethingStandsAt(position))
        {
            return PlacementVerdict.No("Something already stands there.");
        }

        // Reachable from where the people are. A building on the far bank is not a long
        // walk, it is no walk at all (D40), and it would look perfectly fine on the map.
        GridPos village = FirstHomeOrFoundingSite();
        if (!TravelCost.CanReach(village, position))
        {
            return PlacementVerdict.No("There is no route to there from the village.");
        }

        // Legal, but perhaps unwise — and that is the player's call to make (D43).
        int walk = TravelCost.Cost(village, position) / TravelCostField.BaseTileCost;
        int budget = VillageEconomy.MaxHomeToVillageTiles(Config);
        if (walk > budget)
        {
            return PlacementVerdict.Yes(
                $"That is {walk} tiles from the village; it budgets {budget}. " +
                "People will spend their days walking to it.");
        }

        _ = kind;
        return PlacementVerdict.Fine;
    }

    /// <summary>
    /// Mark out a building. It exists as a site, not a building, until somebody raises it.
    /// </summary>
    /// <returns>The verdict; the site is only created when it allows.</returns>
    public PlacementVerdict Mark(BuildingKind kind, GridPos position)
    {
        PlacementVerdict verdict = CanBuildAt(kind, position);
        if (!verdict.Allowed)
        {
            return verdict;
        }

        BuildingRecipe recipe = BuildingRecipe.For(kind, Config);
        string name = NameFor(kind);

        Workplaces.Add(new Workplace
        {
            Id = NextWorkplaceId(),
            Kind = JobKind.Builder,
            Name = $"{name} (building)",
            Position = position,
            Capacity = Config.ConstructionSiteCapacity,
            CatchmentRadius = TravelCostField.TilesToCost(Config.ForagerCatchmentTiles),
            Construction = new ConstructionSite { Kind = kind, Name = name, Recipe = recipe },
        });

        Log(Logging.LogLevel.Info, "placement",
            $"{name} was marked out — {recipe.Logs} logs and {recipe.WorkTicks} ticks of work. " +
            $"{Clock.SeasonAndYear()}.");

        return verdict;
    }

    /// <summary>
    /// Pull a building down. Some of its logs come back; whatever was inside does not.
    /// </summary>
    /// <remarks>
    /// <b>Contents are lost, and loudly.</b> That is the consequence D43 asked for — a
    /// demolished granary strands what was in it, the same lesson D34 taught about a
    /// dead family's larder. Losing it silently would be the worse sin: goods vanishing
    /// with no line in the log is exactly the untraceable outcome §1.1 forbids.
    /// </remarks>
    public void Demolish(StoreBuilding building)
    {
        ArgumentNullException.ThrowIfNull(building);

        BuildingKind kind = building.Kind switch
        {
            StoreKind.Granary => BuildingKind.Granary,
            StoreKind.Shed => BuildingKind.Shed,
            _ => BuildingKind.Market,
        };

        int held = building.Store.Held;
        int back = BuildingRecipe.For(kind, Config).Logs * Config.DemolitionReturnsPercent / 100;

        StoreBuildings.Remove(building);

        // Any market workplace standing on it goes too — the stall cannot outlive the
        // building it is part of.
        for (int i = Workplaces.Count - 1; i >= 0; i--)
        {
            if (Workplaces[i].Position == building.Position && Workplaces[i].Kind == JobKind.Marketer)
            {
                ReleaseWorkers(Workplaces[i]);
                Workplaces.RemoveAt(i);
            }
        }

        StoreBuilding? shed = NearestStore(
            building.Position, StoreKind.Shed, static store => !store.Store.IsFull);
        int recovered = shed?.Store.ReceiveLogs(back) ?? 0;

        Narrate(held > 0
            ? $"{building.Name} was pulled down — {recovered} logs recovered, and the {held} " +
              $"goods inside it were lost. {Clock.SeasonAndYear()}."
            : $"{building.Name} was pulled down — {recovered} logs recovered. {Clock.SeasonAndYear()}.");
    }

    /// <summary>Abandon a site that has not been finished; its delivered logs come back.</summary>
    public void CancelConstruction(Workplace site)
    {
        ArgumentNullException.ThrowIfNull(site);

        if (site.Construction is null)
        {
            throw new ArgumentException($"{site.Name} is not a construction site.", nameof(site));
        }

        int back = site.Construction.Abandon();
        ReleaseWorkers(site);
        Workplaces.Remove(site);

        StoreBuilding? shed = NearestStore(
            site.Position, StoreKind.Shed, static store => !store.Store.IsFull);
        shed?.Store.ReceiveLogs(back);

        Narrate($"{site.Construction.Name} was abandoned before it was built — " +
            $"{back} logs went back to store. {Clock.SeasonAndYear()}.");
    }

    /// <summary>Turn a finished site into the building it was always going to be.</summary>
    internal void Complete(Workplace site)
    {
        ConstructionSite plan = site.Construction!;

        switch (plan.Kind)
        {
            case BuildingKind.WoodcutterHut:
                Workplaces.Add(new Workplace
                {
                    Id = NextWorkplaceId(),
                    Kind = JobKind.Woodcutter,
                    Name = plan.Name,
                    Position = site.Position,
                    Capacity = Config.WoodcutterHutCapacity,
                    CatchmentRadius = site.CatchmentRadius,
                });
                break;

            default:
                StoreKind kind = plan.Kind switch
                {
                    BuildingKind.Granary => StoreKind.Granary,
                    BuildingKind.Shed => StoreKind.Shed,
                    _ => StoreKind.Market,
                };

                int capacity = plan.Kind switch
                {
                    BuildingKind.Granary => VillageEconomy.GranaryCapacity(Config),
                    BuildingKind.Shed => VillageEconomy.ShedCapacity(Config),
                    _ => VillageEconomy.MarketCapacity(Config),
                };

                StoreBuildings.Add(new StoreBuilding
                {
                    Id = NextStoreId(),
                    Kind = kind,
                    Name = plan.Name,
                    Position = site.Position,
                    Store = new Stockpile { Capacity = capacity },
                });

                // A market is a place to work as well as a place to keep things (D14).
                if (plan.Kind == BuildingKind.Market && Config.MarketCapacity > 0)
                {
                    Workplaces.Add(new Workplace
                    {
                        Id = NextWorkplaceId(),
                        Kind = JobKind.Marketer,
                        Name = plan.Name,
                        Position = site.Position,
                        Capacity = Config.MarketCapacity,
                        CatchmentRadius = site.CatchmentRadius,
                    });
                }

                break;
        }

        ReleaseWorkers(site);
        Workplaces.Remove(site);

        Narrate($"{plan.Name} was finished. {Clock.SeasonAndYear()}.");
    }

    private void ReleaseWorkers(Workplace workplace)
    {
        for (int i = 0; i < Villagers.Count; i++)
        {
            if (Villagers[i].WorkplaceId == workplace.Id)
            {
                Villagers[i].WorkplaceId = 0;
                Villagers[i].JobReason = $"{workplace.Name} is gone.";
            }
        }
    }

    private int NextWorkplaceId()
    {
        int max = _nextWorkplaceId;
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Id >= max)
            {
                max = Workplaces[i].Id + 1;
            }
        }

        _nextWorkplaceId = max + 1;
        return max;
    }

    private int NextStoreId()
    {
        int max = 1;
        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            if (StoreBuildings[i].Id >= max)
            {
                max = StoreBuildings[i].Id + 1;
            }
        }

        return max;
    }

    private static string NameFor(BuildingKind kind) => kind switch
    {
        BuildingKind.Granary => "a granary",
        BuildingKind.Shed => "a storage shed",
        BuildingKind.Market => "a market",
        _ => "a woodcutter's hut",
    };

    /// <summary>Any store of this kind, for naming things and for tests. Never for logic.</summary>
    /// <remarks>
    /// Deliberately awkward to call. If a piece of logic wants "the granary" it almost
    /// certainly wants either <see cref="FoodInGranaries"/> or
    /// <see cref="NearestStore"/>, and this is here so that the few places that really
    /// do just need a name have to say so.
    /// </remarks>
    public StoreBuilding AnyStoreOf(StoreKind kind) => FindStore(kind);

    /// <summary>Look up a household by id, or null if it has been dissolved.</summary>
    public Household? FindHousehold(int id)
    {
        for (int i = 0; i < Households.Count; i++)
        {
            if (Households[i].Id == id)
            {
                return Households[i];
            }
        }

        return null;
    }

    private StoreBuilding FindStore(StoreKind kind)
    {
        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            if (StoreBuildings[i].Kind == kind)
            {
                return StoreBuildings[i];
            }
        }

        throw new InvalidOperationException($"The village has no {kind}.");
    }

    /// <summary>
    /// Walk every store in the world — homes, workplaces and buildings.
    /// </summary>
    /// <remarks>
    /// Goods live in several kinds of place now (D30), so "how much does the village
    /// have?" stopped being a loop over households. One place to ask it means a store
    /// added to a new kind of building is counted by everything that already asks,
    /// rather than being quietly missed by half of them.
    /// </remarks>
    public IEnumerable<Stockpile> AllStores()
    {
        for (int i = 0; i < Households.Count; i++)
        {
            yield return Households[i].Stockpile;
        }

        for (int i = 0; i < Workplaces.Count; i++)
        {
            yield return Workplaces[i].Store;
        }

        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            yield return StoreBuildings[i].Store;
        }
    }

    /// <summary>Food held anywhere in the village.</summary>
    public int TotalFood()
    {
        int total = 0;
        foreach (Stockpile store in AllStores())
        {
            total += store.Food;
        }

        return total;
    }

    /// <summary>Logs held anywhere in the village, plus any in someone's arms.</summary>
    public int TotalLogs()
    {
        int total = 0;
        foreach (Stockpile store in AllStores())
        {
            total += store.Logs;
        }

        for (int i = 0; i < Villagers.Count; i++)
        {
            total += Villagers[i].CarriedLogs;
        }

        return total;
    }

    /// <summary>Firewood held anywhere in the village, plus any in someone's arms.</summary>
    public int TotalFirewood()
    {
        int total = 0;
        foreach (Stockpile store in AllStores())
        {
            total += store.Firewood;
        }

        for (int i = 0; i < Villagers.Count; i++)
        {
            total += Villagers[i].CarriedFirewood;
        }

        return total;
    }

    /// <summary>Logs ever felled, wherever they ended up.</summary>
    public int LifetimeLogsFelled()
    {
        int total = 0;
        foreach (Stockpile store in AllStores())
        {
            total += store.LifetimeLogsFelled;
        }

        return total;
    }

    /// <summary>Firewood ever split, wherever it ended up.</summary>
    public int LifetimeFirewoodCut()
    {
        int total = 0;
        foreach (Stockpile store in AllStores())
        {
            total += store.LifetimeFirewoodCut;
        }

        return total;
    }

    /// <summary>Look up a workplace by id, or null.</summary>
    /// <summary>
    /// Tell the village how many hands to put on a workplace, or null to let it decide
    /// again (D51).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This sets a number, never a person</b>, and that distinction is the whole
    /// reason it is allowed to exist. D15 removed the API that named a villager and put
    /// them in a job, and that stays removed: proximity, household and catchment still
    /// choose who goes where, so every "why is Elias at the stand?" sentence remains
    /// true. The player shapes the field; they do not command anybody — §2.2's
    /// philosophy, and the same trade D42 made for housing.
    /// </para>
    /// <para>
    /// <b>Zero is meaningful and different from null.</b> Zero is "nobody works here,
    /// I mean it"; null is "I have no opinion, do what you think best". The market has
    /// shipped a switched-off setting since D36, so a workplace nobody staffs is a
    /// supported state rather than a broken one.
    /// </para>
    /// <para>
    /// The village does not re-plan on the spot: the change lands at the next labour
    /// pass, which is at worst a season away and immediately if a job has just fallen
    /// vacant (D47). Re-running the allocator from a UI click would make staffing the
    /// one decision in this game that stops the world.
    /// </para>
    /// </remarks>
    public void SetStaffing(Workplace workplace, int? places)
    {
        ArgumentNullException.ThrowIfNull(workplace);

        if (places is int wanted && wanted < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(places), places, "A workplace cannot be staffed by fewer than nobody.");
        }

        workplace.StaffingOverride = places;

        Narrate(places is int n
            ? $"{workplace.Name} is to be worked by {n} {(n == 1 ? "person" : "people")} " +
              $"from now on. {Clock.SeasonAndYear()}."
            : $"{workplace.Name} is left to the village to staff as it sees fit. " +
              $"{Clock.SeasonAndYear()}.");
    }

    public Workplace? FindWorkplace(int id)
    {
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Id == id)
            {
                return Workplaces[i];
            }
        }

        return null;
    }

    /// <summary>Seed this run was created with — shown in the UI so a run that
    /// produced an interesting life can be reproduced exactly.</summary>
    public ulong Seed { get; }

    private SimWorld(SimConfig config, ISimLogger logger, ulong seed)
    {
        Config = config;
        Logger = logger;
        Seed = seed;
        Rng = new DeterministicRandom(seed);
        Tick = 0UL;

        // THE WORLD IS GENERATED FIRST, and from the run's own seeded stream (D18).
        //
        // Before anything else draws, because draw order is the seed contract: the
        // map, then the villagers' names and lifespans. Generating later — or from a
        // second RNG — would mean the seed no longer reproduced the world, which is
        // the entire point of tying worldgen to the sim's seed rather than its own.
        Map = MapGenerator.Generate(config, Rng);

        // The cost field needs the terrain, so it is built after the valley — a route
        // now goes ROUND the river rather than over it (D40), and catchment, market
        // errands and the economy's budget all get that for free because they have
        // always shared this one field (§2.6).
        TravelCost = new TravelCostField(config.TravelTicksPerUnit, Map);
        Zones = new ZoneMap(Map);

        // Everything the village builds hangs off the founding site the generator
        // chose. The config keys that used to hold absolute coordinates are now
        // OFFSETS from it, so a valley can be generated anywhere inside the bounds and
        // the settlement still assembles itself correctly around the spot.
        GridPos origin = Map.FoundingSite;

        FoodSource = new FoodSource
        {
            Position = Map.ForageSites[0],
            YieldPerGather = config.GatherYield,
        };

        TreeStand = new TreeStand { YieldPerCut = config.CutYield };

        int catchment = TravelCostField.TilesToCost(config.ForagerCatchmentTiles);
        int nextWorkplaceId = 1;

        // Several sites, spread around the valley, so an outlying household has
        // somewhere near enough to work. This is what a binding catchment needs in
        // order to be survivable rather than merely cruel (D19) — and the generator
        // guarantees the spread by construction rather than hoping for it (D24).
        List<string> forageNames = NamePlaces(Map.ForageSites, origin, "the berry patch", "thicket");

        for (int i = 0; i < Map.ForageSites.Count; i++)
        {
            GridPos position = Map.ForageSites[i];

            Workplaces.Add(new Workplace
            {
                Id = nextWorkplaceId++,
                Kind = JobKind.Forager,
                Name = forageNames[i],
                Position = position,
                Capacity = config.ForageSiteCapacity,
                CatchmentRadius = catchment,
            });
        }

        // Last ids, so that where ids break a tie the food comes first. The real
        // "feed yourself before you build" rule is the quota, not the ordering -
        // see LabourQuota - but there is no reason for the two to disagree.
        List<string> standNames = NamePlaces(Map.TreeStands, origin, "the tree stand", "wood");

        for (int i = 0; i < Map.TreeStands.Count; i++)
        {
            Workplaces.Add(new Workplace
            {
                Id = nextWorkplaceId++,
                Kind = JobKind.Logger,
                Name = standNames[i],
                Position = Map.TreeStands[i],
                Capacity = config.TreeStandCapacity,
                CatchmentRadius = catchment,
            });
        }

        // EVERYTHING FROM HERE IS A BUILDING, AND THE COLD START HAS NONE (D70).
        //
        // The workplaces above are not buildings and stay either way: a berry patch and a
        // stand of trees are features of the valley and were always there. What follows —
        // the hut, the granary, the shed, the market — is what somebody had to raise, and
        // in the cold start that somebody is the player. The founders get their cart.
        // The founding happens here rather than at the end of the constructor, and only
        // because there is nothing left to wait for: the reason it normally goes last is
        // that ChooseSite wants the stores to exist, and in a cold start there are none.
        if (!config.FoundingBuildings)
        {
            RaiseTheCart(config, origin);
            FoundVillage(config, origin);
            return;
        }

        // The first workplace that consumes an input rather than only producing one
        // (D29). Logs in, firewood out - and it can stand idle for want of logs,
        // which is a state no other workplace can be in.
        Workplaces.Add(new Workplace
        {
            Id = nextWorkplaceId++,
            Kind = JobKind.Woodcutter,
            Name = "the woodcutter's hut",
            Position = Offset(origin, config.WoodcutterHutX, config.WoodcutterHutY),
            Capacity = config.WoodcutterHutCapacity,
            CatchmentRadius = catchment,
        });

        // Somewhere to put things (D30). Two buildings rather than one, because food
        // and materials are different problems - see StoreBuilding and D32.
        //
        // These two exist from the founding, so a village always has somewhere to put
        // things. Every one after them is placed by the player (D43), and WHERE the
        // granary goes is the first decision storage makes interesting.
        //
        // Both hold a DERIVED amount, not a chosen one (D16). The granary's is the
        // village's real ceiling: births are gated on it holding a share of what
        // everyone alive would want, so capping it caps the village. See
        // VillageEconomy.PopulationCeiling, and the spec's §12 for why that is the
        // shape the population curve needed.
        StoreBuildings.Add(new StoreBuilding
        {
            Id = 1,
            Kind = StoreKind.Granary,
            Name = "the granary",
            Position = Offset(origin, config.GranaryX, config.GranaryY),
            Store = new Stockpile { Capacity = VillageEconomy.GranaryCapacity(config) },
        });

        StoreBuildings.Add(new StoreBuilding
        {
            Id = 2,
            Kind = StoreKind.Shed,
            Name = "the storage shed",
            Position = Offset(origin, config.StorageShedX, config.StorageShedY),
            Store = new Stockpile { Capacity = VillageEconomy.ShedCapacity(config) },
        });

        // The market (D14) — the one store that is also a workplace, because its
        // contents arrive by somebody's work rather than by producers dropping things
        // off. Two entries at one position: a store, and a place to work.
        //
        // They are separate types today. Merging them into the single Building the
        // spec's §4 describes is the right end state and is not this slice's job; the
        // seam is recorded there rather than left to be rediscovered.
        var market = Offset(origin, config.MarketX, config.MarketY);

        StoreBuildings.Add(new StoreBuilding
        {
            Id = 3,
            Kind = StoreKind.Market,
            Name = "the market",
            Position = market,
            Store = new Stockpile { Capacity = VillageEconomy.MarketCapacity(config) },
        });

        // Capacity zero means no market at all rather than a market nobody can work
        // at, so that "switch the market off" is a state the village genuinely runs in
        // (spec §14.4) rather than one that merely produces a permanently empty
        // building for the allocator to keep considering.
        if (config.MarketCapacity > 0)
        {
            Workplaces.Add(new Workplace
            {
                Id = nextWorkplaceId++,
                Kind = JobKind.Marketer,
                Name = "the market",
                Position = market,
                Capacity = config.MarketCapacity,
                CatchmentRadius = catchment,
            });
        }

        // THE VILLAGE IS FOUNDED LAST, after the work and the stores exist.
        //
        // It used to come first, which meant the founding homes were dropped on a
        // spiral before there was anything to be near — so they could land in the
        // river, and their families could not take a step, forage, or eat. With water
        // impassable (D40) that is fatal in year one and traceable to no decision
        // anybody made.
        //
        // Founding homes now go through exactly the same rule as every home built
        // afterwards (Household.ChooseSite): near the work, near the store, and never
        // in the water. One rule for the whole village rather than one for the
        // founders and another for their children.
        //
        // And the exiles arrive having already decided where to live (D42), because a
        // village with nothing painted could not build a house at all.
        //
        // UNLESS THEY ARRIVE TO AN EMPTY VALLEY (D70). Then deciding where to live is the
        // player's first act rather than a thing the world did for them, so no zone is
        // painted and no home is raised — the founders stand at their cart, and the first
        // winter is thirty days away.
        if (config.FoundingBuildings)
        {
            PaintTheStarterZone(origin, config);
        }

        FoundVillage(config, origin);
    }

    /// <summary>
    /// Create the founding households and their adults.
    /// </summary>
    /// <remarks>
    /// Draw order is part of the seed contract: for each villager, <b>name then
    /// lifespan, always</b>, and households in id order. Reordering any of it shifts
    /// every subsequent value in the stream, silently invalidating saved seeds and
    /// every golden test.
    /// </remarks>
    private void FoundVillage(SimConfig config, GridPos origin)
    {
        int nextVillagerId = 1;

        for (int h = 0; h < config.StartingHouseholds; h++)
        {
            // The same rule the village will use for every home it ever builds: near
            // the work, near the store, never in the water.
            //
            // Or no home at all, if the founders arrived to an empty valley (D70). The
            // household still exists — it is a family, not a building — and it goes on
            // holding a name, a larder and its members. What it does not have is anywhere
            // to put them, which is the whole of the cold start.
            GridPos? home = config.FoundingBuildings
                ? Household.ChooseSite(
                    this, new GridPos(origin.X + config.HomeX, origin.Y + config.HomeY))
                : null;

            var household = new Household
            {
                Id = h + 1,
                Name = config.HouseholdNames[h % config.HouseholdNames.Count],
                HomePosition = home,
            };

            // Added before its members are drawn, so the next founding household's
            // ChooseSite can see this one and does not build on top of it.
            Households.Add(household);

            for (int a = 0; a < config.AdultsPerHousehold; a++)
            {
                string name = DrawUnusedName();

                int lifespan = config.LifespanYearsBase;
                if (config.LifespanYearsVariance > 0)
                {
                    lifespan += Rng.NextInt(-config.LifespanYearsVariance, config.LifespanYearsVariance + 1);
                }

                var villager = new Villager
                {
                    Id = nextVillagerId++,
                    Name = name,
                    LifespanYears = lifespan,

                    // Standing at their house, or at the cart they arrived in (D70). Not
                    // RestingPlaceOf — that reads the household, and this villager is not
                    // in it yet.
                    Position = home ?? origin,
                    HouseholdId = household.Id,

                    // Year 1 is the first year, so someone aged N at founding was
                    // born in year 1-N. Deriving it this way means ClockSystem's
                    // per-tick recalculation reproduces the founding age rather
                    // than resetting every founder to zero on the first tick.
                    BirthYear = 1 - config.FounderAge,
                    AgeYears = config.FounderAge,
                    LifeStage = LifeStage.Adult,
                };

                household.AddMember(villager.Id);
                Villagers.Add(villager);
            }
        }

        PairFounders(config);
    }

    /// <summary>
    /// Founding adults arrive as couples, two to a household.
    /// </summary>
    /// <remarks>
    /// Otherwise the founders are all unpaired and immediately walk out of their own
    /// homes looking for partners, which is a strange way to found a settlement.
    /// </remarks>
    private void PairFounders(SimConfig config)
    {
        for (int h = 0; h < Households.Count; h++)
        {
            IReadOnlyList<int> members = Households[h].MemberIds;

            for (int i = 0; i + 1 < members.Count; i += 2)
            {
                Villager a = Villagers[members[i] - 1];
                Villager b = Villagers[members[i + 1] - 1];
                a.PartnerId = b.Id;
                b.PartnerId = a.Id;
            }
        }
    }

    /// <summary>
    /// Draw a name nobody in the village is already using.
    /// </summary>
    /// <remarks>
    /// Two villagers called Bess is a small thing that undercuts a large one: this
    /// game is defined against fungible labour units (§1.4), and you cannot tell a
    /// story about someone whose name is not theirs. Drawing with replacement from a
    /// short list produced twins-by-accident within two years.
    /// <para>
    /// Deterministic: pick an index from the seeded RNG, then walk forward to the
    /// first unused name. Walking is a fixed rule, so the same seed still yields the
    /// same village. If every name is taken, reuse is allowed rather than failing -
    /// a repeated name is a blemish, a crash is a bug.
    /// </para>
    /// </remarks>
    internal string DrawUnusedName()
    {
        IReadOnlyList<string> pool = Config.VillagerNames;
        int start = (int)Rng.NextUInt((uint)pool.Count);

        for (int offset = 0; offset < pool.Count; offset++)
        {
            string candidate = pool[(start + offset) % pool.Count];
            if (!IsNameInUse(candidate))
            {
                return candidate;
            }
        }

        return pool[start];
    }

    private bool IsNameInUse(string name)
    {
        for (int i = 0; i < Villagers.Count; i++)
        {
            // Only the living hold a name. The dead keep theirs in the log, but a
            // grandchild may carry it again - which is how families actually work.
            if (Villagers[i].Alive
                && string.Equals(Villagers[i].Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A place name from a direction, so the log reads as somewhere real.
    /// </summary>
    /// <remarks>
    /// "The western thicket" is a place; "Forage Site 3" is a row in a table. The
    /// same reason villagers are called Mabel (§1.4) applies to where they work — and
    /// it matters more here, because these names end up inside the sentence that
    /// explains why someone walks the way they do.
    /// </remarks>
    /// <summary>
    /// A building's spot, given as an offset from where the village was founded — moved
    /// to dry, reachable ground if the offset lands somewhere nobody can get to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The offsets are a village <em>layout</em>, drawn up without knowing what the
    /// valley looks like, so on a generated map any of them can land in the river.
    /// Measured on seed 1: the shed and the woodcutter's hut both came down in the
    /// water, so no logs could be stored and no firewood made, and all four founders
    /// froze in the first winter. Nothing in the log said "your shed is in the river" —
    /// it just said they were cold.
    /// </para>
    /// <para>
    /// <b>Reachable, not merely dry.</b> A building on the far bank is as useless as
    /// one under water, and the difference is invisible on the map. Reachability is
    /// asked against the founding site, and always in that direction, so the cost
    /// field builds one flow field and reuses it for every candidate rather than one
    /// per tile tried.
    /// </para>
    /// </remarks>
    private GridPos Offset(GridPos origin, int dx, int dy)
    {
        var wanted = new GridPos(origin.X + dx, origin.Y + dy);

        // Spiralling outward by Manhattan rings, scanned in a fixed order so two runs
        // never disagree about which equally-near spot the village chose.
        for (int radius = 0; radius < Config.MapWidth; radius++)
        {
            for (int ddy = -radius; ddy <= radius; ddy++)
            {
                for (int ddx = -radius; ddx <= radius; ddx++)
                {
                    if (Math.Abs(ddx) + Math.Abs(ddy) != radius)
                    {
                        continue;
                    }

                    var candidate = new GridPos(wanted.X + ddx, wanted.Y + ddy);
                    if (!Map.Contains(candidate)
                        || Map.TerrainAt(candidate) == Terrain.Water
                        || SomethingStandsAt(candidate))
                    {
                        continue;
                    }

                    if (TravelCost.Cost(candidate, origin) != TravelCostField.Unreachable)
                    {
                        return candidate;
                    }
                }
            }
        }

        throw new InvalidOperationException(
            $"No reachable ground anywhere near {wanted} to put a building on.");
    }

    /// <summary>
    /// Whether a building already occupies this tile.
    /// </summary>
    /// <remarks>
    /// Without this, two buildings whose offsets both landed in the river nudged to the
    /// same dry tile and stood on top of each other — and a granary drawn underneath a
    /// shed is a granary nobody can see, which makes "why is nobody fetching food?"
    /// unanswerable by looking.
    /// </remarks>
    internal bool SomethingStandsAt(GridPos position)
    {
        // HOMES COUNT, and leaving them out was a real bug rather than an omission:
        // CanBuildAt asked this question and got "no" for a tile with a house on it, so
        // the player could mark a granary on top of somebody's home. Meanwhile
        // Household.ChooseSite asked its OWN version of the same question, which did check
        // homes — two rules, one of them wrong, and the wrong one was the one facing the
        // player. There is one now, and ChooseSite calls it.
        for (int i = 0; i < Households.Count; i++)
        {
            if (Households[i].HomePosition == position)
            {
                return true;
            }
        }

        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Position == position)
            {
                return true;
            }
        }

        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            if (StoreBuildings[i].Position == position)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Place names for a set of sites — <b>distinct ones</b>, from where they are.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two sites that share a name are two the game cannot explain.</b> Joe read this
    /// off his own screen: <em>"Nobody is working the berry patch, the southern western
    /// thicket, the southern eastern thicket, the southern eastern thicket"</em> — the
    /// same phrase twice, because a bearing has eight values and this village has six
    /// forage sites. Every name here ends up inside a sentence about why somebody walks
    /// the way they do, and a name that points at two places answers nothing.
    /// </para>
    /// <para>
    /// <b>Worse than the repeat, and the reason this is a sim fix rather than a view
    /// one: the noun was wrong.</b> Every site past the first was called a *thicket*
    /// whatever it was, so a tree stand and a berry patch were named alike — and a
    /// player told nobody was working "the southern eastern thicket" could not tell
    /// whether the village was short of food or of timber. Thickets are for foraging;
    /// woods are for felling.
    /// </para>
    /// <para>
    /// Collisions are broken by <em>distance</em>, because that is what a person would
    /// reach for: the near one keeps the plain name and the ones behind it are the far,
    /// farther and farthest. Ordered by travel distance and then by tile, so it is a
    /// total order and no two runs can disagree (D15).
    /// </para>
    /// </remarks>
    private static List<string> NamePlaces(
        IReadOnlyList<GridPos> sites, GridPos origin, string firstName, string noun)
    {
        var names = new List<string>(sites.Count);
        for (int i = 0; i < sites.Count; i++)
        {
            // The first of each kind is the one the village started with, and it is
            // named as such — "the berry patch", not "the southern thicket".
            names.Add(i == 0 ? firstName : $"the {Bearing(sites[i], origin)} {noun}");
        }

        for (int i = 0; i < names.Count; i++)
        {
            var sharing = new List<int>();
            for (int j = i; j < names.Count; j++)
            {
                if (names[j] == names[i])
                {
                    sharing.Add(j);
                }
            }

            if (sharing.Count < 2)
            {
                continue;
            }

            sharing.Sort((a, b) =>
            {
                int byDistance = TilesApart(origin, sites[a]).CompareTo(TilesApart(origin, sites[b]));
                if (byDistance != 0)
                {
                    return byDistance;
                }

                int byX = sites[a].X.CompareTo(sites[b].X);
                return byX != 0 ? byX : sites[a].Y.CompareTo(sites[b].Y);
            });

            // The nearest keeps what it had; everyone behind it says how far behind.
            for (int k = 1; k < sharing.Count; k++)
            {
                names[sharing[k]] = Further(names[sharing[k]], k);
            }
        }

        return names;
    }

    /// <summary>Which way a site lies from the village, in words.</summary>
    /// <remarks>
    /// Relative to the village, not to the world origin — "the northern wood" has to mean
    /// north of the people saying it, and once the valley is generated the settlement is
    /// no longer at (0,0).
    /// </remarks>
    private static string Bearing(GridPos position, GridPos origin)
    {
        position = new GridPos(position.X - origin.X, position.Y - origin.Y);

        string northSouth = position.Y < 0 ? "north" : position.Y > 0 ? "south" : string.Empty;
        string eastWest = position.X < 0 ? "west" : position.X > 0 ? "east" : string.Empty;

        // Hyphenated when both apply. It used to read "the northern eastern thicket",
        // which is not a thing anybody says.
        if (northSouth.Length > 0 && eastWest.Length > 0)
        {
            return $"{northSouth}-{eastWest}ern";
        }

        if (northSouth.Length > 0)
        {
            return $"{northSouth}ern";
        }

        return eastWest.Length > 0 ? $"{eastWest}ern" : "near";
    }

    /// <summary>
    /// The same place name, said of somewhere further out.
    /// </summary>
    /// <remarks>
    /// Three qualifiers, which is four sites in one bearing before it runs out — and if
    /// it ever does run out the result is a duplicate, so there is a test that every
    /// workplace in the village has a name of its own. A guard rather than a hope: the
    /// alternative is this bug coming back silently the first time somebody raises
    /// <c>forage_site_count</c>.
    /// </remarks>
    private static string Further(string name, int rank)
    {
        string qualifier = rank switch
        {
            1 => "far",
            2 => "farther",
            _ => "farthest",
        };

        // "the southern wood" becomes "the far southern wood".
        return name.StartsWith("the ", StringComparison.Ordinal)
            ? $"the {qualifier} {name[4..]}"
            : $"{qualifier} {name}";
    }

    /// <summary>Straight-line-ish distance in tiles, integer only (D2).</summary>
    /// <remarks>
    /// Chebyshev rather than the travel-cost field, deliberately: names are settled while
    /// the world is being built, before the cost field exists, and "which is further out"
    /// is a question about the map rather than about the walk.
    /// </remarks>
    private static int TilesApart(GridPos from, GridPos to)
    {
        int dx = Math.Abs(to.X - from.X);
        int dy = Math.Abs(to.Y - from.Y);
        return dx > dy ? dx : dy;
    }

    /// <summary>
    /// What the tile at <paramref name="at"/> does for somebody standing on it in
    /// winter (D45).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A reader, not a flag</b> — the same discipline as D47's vacancy check. The
    /// answer is computed from the world every time it is asked, so there is nothing to
    /// hash, nothing that can be set and not cleared, and no way for it to drift out of
    /// step with where the buildings actually are. This project's recurring bug is code
    /// reading state from where it used to live.
    /// </para>
    /// <para>
    /// Homes are asked first and win, because a home is the only thing that can hold a
    /// fire. A hut, a market and a shed have roofs but no hearth; a berry patch and a
    /// tree stand have neither, which is what makes foraging and logging <em>outdoor
    /// work</em> — the asymmetry clothing later removes (D19/D39).
    /// </para>
    /// </remarks>
    public Shelter ShelterAt(GridPos at)
    {
        for (int i = 0; i < Households.Count; i++)
        {
            Household household = Households[i];

            // A family with no house shelters nobody, anywhere — including themselves. That
            // is the whole tension of the founding (D70): until somebody raises a roof there
            // is no Shelter.Roof and no Shelter.Fire in the valley, so open ground is the
            // only state there is and winter is counted in days.
            if (household.HomePosition is not GridPos home)
            {
                continue;
            }

            if (home.X != at.X || home.Y != at.Y)
            {
                continue;
            }

            // An empty house has nobody to keep the fire in, whatever is on its shelf.
            return LivingMembersOf(household) > 0 && household.Stockpile.Firewood > 0
                ? Shelter.Fire
                : Shelter.Roof;
        }

        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            StoreBuilding store = StoreBuildings[i];
            if (store.Position.X == at.X && store.Position.Y == at.Y)
            {
                return Shelter.Roof;
            }
        }

        for (int i = 0; i < Workplaces.Count; i++)
        {
            Workplace workplace = Workplaces[i];
            if (workplace.Position.X != at.X || workplace.Position.Y != at.Y)
            {
                continue;
            }

            if (IsUnderCover(workplace.Kind))
            {
                return Shelter.Roof;
            }
        }

        return Shelter.Outdoors;
    }

    /// <summary>Whether working at this kind of place puts a roof over you.</summary>
    /// <remarks>
    /// A woodcutter's hut and a market stall are buildings; a berry patch and a tree
    /// stand are weather. A building site is roofless by definition — it is the roof
    /// that is missing — which is a small cruelty that happens to be true, and it makes
    /// raising a granary in February a real cost rather than a free winter job.
    /// </remarks>
    private static bool IsUnderCover(JobKind kind) =>
        kind is JobKind.Woodcutter or JobKind.Marketer;

    /// <summary>Look up a villager by id, or null if there is no such person.</summary>
    public Villager? FindVillager(int id)
    {
        for (int i = 0; i < Villagers.Count; i++)
        {
            if (Villagers[i].Id == id)
            {
                return Villagers[i];
            }
        }

        return null;
    }

    /// <summary>The household a villager belongs to.</summary>
    public Household HouseholdOf(Villager villager)
    {
        ArgumentNullException.ThrowIfNull(villager);

        for (int i = 0; i < Households.Count; i++)
        {
            if (Households[i].Id == villager.HouseholdId)
            {
                return Households[i];
            }
        }

        throw new InvalidOperationException(
            $"Villager {villager.Id} ({villager.Name}) belongs to household {villager.HouseholdId}, which does not exist.");
    }

    /// <summary>Living members of a household.</summary>
    public int LivingMembersOf(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);

        int count = 0;
        for (int i = 0; i < Villagers.Count; i++)
        {
            Villager villager = Villagers[i];
            if (villager.Alive && villager.HouseholdId == household.Id)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Food a household aims to have stored before it stops foraging and rests.
    /// </summary>
    /// <remarks>
    /// Scales with the number of mouths. A fixed target was tuned for Phase 0's
    /// single villager, and left a four-person household permanently one bad season
    /// from empty — perpetually foraging, never building a surplus, and therefore
    /// never having children. A household stores enough for <em>everyone in it</em>.
    /// </remarks>
    public int TargetFoodFor(Household household) =>
        Config.StockpileTarget * Math.Max(1, LivingMembersOf(household));

    /// <summary>
    /// Food the village wants in the granary — a winter's store for everyone.
    /// </summary>
    /// <remarks>
    /// <b>This is what makes a village store mean anything.</b> A forager who stops
    /// the moment their own larder is full produces no surplus, so the granary stays
    /// empty, so a household with nobody foraging has nothing to fetch and starves
    /// beside neighbours who are resting. That is what happened the first time this
    /// ran: the founding woodcutters starved in year one while the other house sat on
    /// three hundred food and its foragers put their feet up.
    /// <para>
    /// So there are two reasons to keep working: my family is short, or the village
    /// is. The second one is the entire argument for having a granary.
    /// </para>
    /// <para>
    /// <b>Deliberately unbounded, and that is what makes it a ceiling.</b> It is the
    /// birth gate's question — <em>could this village feed another mouth through a
    /// winter?</em> — so it has to keep asking for a winter's store for everyone alive,
    /// even once that is more than the granary could physically hold. When it exceeds
    /// what the building holds, the gate shuts and the village stops growing at the
    /// size its buildings support (<see cref="VillageEconomy.PopulationCeiling"/>).
    /// </para>
    /// <para>
    /// <b>Do not use it to decide whether anyone should go out and work.</b> That is a
    /// different question and it lives in <see cref="FoodTheGranaryHasRoomFor"/>.
    /// Answering both with this one number killed the village outright: above the
    /// ceiling the target is unreachable by construction, so "does the village want
    /// more food?" was permanently yes, every hand stayed on the berry patches
    /// forever, nobody was ever spared for the woodcutter's hut, and a settlement of
    /// thirty froze to extinction in its twelfth decade with a full granary and a
    /// woodpile it never split. Two questions, one field — the same mistake D21 is a
    /// record of.
    /// </para>
    /// </remarks>
    public int TargetFoodForTheGranary() => Config.StockpileTarget * Population;

    /// <summary>
    /// Food worth gathering for the granary — the target, or what fits, whichever is
    /// less.
    /// </summary>
    /// <remarks>
    /// The work question, as against the birth question above. <b>A village cannot want
    /// more food than it has somewhere to put</b>, and a forager standing at a full
    /// granary should go and do something else — which is exactly the pressure capacity
    /// was added to create (spec §4: "a full shed means a producer has somewhere to
    /// stop").
    /// </remarks>
    public int FoodTheGranaryHasRoomFor()
    {
        // Across every granary the village has built (D38). Reading one building's
        // capacity here would have meant a second granary added room the foragers
        // never knew to fill.
        int wanted = TargetFoodForTheGranary();
        int capacity = GranaryCapacity();
        return wanted < capacity ? wanted : capacity;
    }

    /// <summary>Where a villager lives, or null if their family has no house yet (D70).</summary>
    public GridPos? HomeOf(Villager villager) => HouseholdOf(villager).HomePosition;

    /// <summary>
    /// Where a villager goes when there is nothing else to do — their house, or the cart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every villager must always have somewhere to be</b>, or the founding is a crash
    /// rather than a hardship (`specs/cold-start.md §8`). Until a family has a roof, that
    /// somewhere is the cart they arrived with: it is where the food is, which keeps D10's
    /// promise that a meal is takeable where you stand, and it puts the founders in one place
    /// so the player can see them.
    /// </para>
    /// <para>
    /// <b>It is emphatically not shelter.</b> <see cref="ShelterAt"/> knows nothing about the
    /// cart, so standing at it is standing outdoors, and the cold counts accordingly. The two
    /// questions — <em>where do I go?</em> and <em>what does this tile cost me?</em> — are
    /// kept apart on purpose; conflating them is how a cart quietly becomes a hearth.
    /// </para>
    /// </remarks>
    public GridPos RestingPlaceOf(Villager villager) =>
        HouseholdOf(villager).HomePosition ?? TheCart?.Position ?? Map.FoundingSite;

    /// <summary>
    /// Where "the village" is, for anything that needs one point to measure from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first household that has actually got a roof — <b>not merely the first
    /// household</b>. At the founding they all exist and none of them are anywhere yet
    /// (D70), and <em>"where is the village?"</em> then has exactly one honest answer: where
    /// they landed.
    /// </para>
    /// <para>
    /// <b>One method because two callers wanted it</b> — reachability in
    /// <see cref="CanBuildAt"/> and the placement ring the map draws. They had the same
    /// three lines each, which is the shape D57 spent a session deleting: a rule written
    /// twice is a rule that gets corrected once.
    /// </para>
    /// </remarks>
    public GridPos FirstHomeOrFoundingSite()
    {
        for (int i = 0; i < Households.Count; i++)
        {
            if (Households[i].HomePosition is GridPos standing)
            {
                return standing;
            }
        }

        return Map.FoundingSite;
    }

    /// <summary>
    /// Put down the wagon the founders arrived in — the only building of the cold start.
    /// </summary>
    /// <remarks>
    /// <b>It is what keeps D30 true through the founding:</b> goods live in buildings, and a
    /// valley with nothing raised would otherwise have nowhere for the supplies to be. What
    /// it holds is content and lives in config, because it is the difficulty dial for the
    /// opening (<c>specs/cold-start.md §7.2</c>) — the exposure rates are not, and must not
    /// be reached for instead (D53).
    /// </remarks>
    private void RaiseTheCart(SimConfig config, GridPos origin)
    {
        var cart = new StoreBuilding
        {
            Id = 1,
            Kind = StoreKind.Cart,
            Name = "the cart",
            Position = origin,
            Store = new Stockpile { Capacity = config.CartCapacity },
        };

        StoreBuildings.Add(cart);
        cart.Store.Receive(config.CartFood, config.CartLogs, 0);
    }


    /// <summary>The wagon the founders arrived in, while it still stands (D64).</summary>
    public StoreBuilding? TheCart
    {
        get
        {
            for (int i = 0; i < StoreBuildings.Count; i++)
            {
                if (StoreBuildings[i].Kind == StoreKind.Cart)
                {
                    return StoreBuildings[i];
                }
            }

            return null;
        }
    }

    /// <summary>Living villagers. Convenience for narration and the UI.</summary>
    public int Population
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Villagers.Count; i++)
            {
                if (Villagers[i].Alive)
                {
                    count++;
                }
            }

            return count;
        }
    }

    // ---------------------------------------------------------------
    //  Single-founder shorthand
    // ---------------------------------------------------------------
    // Phase 0 is not a special mode — it is the 1 household x 1 adult case. These
    // accessors let the Phase 0 tests keep saying what they mean without pretending
    // the world still holds one villager.

    /// <summary>The first villager. Meaningful when the village has exactly one.</summary>
    public Villager Villager => Villagers[0];

    /// <summary>The first household's store.</summary>
    public Stockpile Stockpile => Households[0].Stockpile;

    /// <summary>
    /// Create a world.
    /// </summary>
    /// <param name="config">Validated tunables.</param>
    /// <param name="logger">Where entries go. Defaults to discarding them.</param>
    /// <param name="seedOverride">
    /// Overrides <see cref="SimConfig.Seed"/>. Exists for tests and for
    /// "new game with a different seed" — the config file stays untouched.
    /// </param>
    public static SimWorld Create(SimConfig config, ISimLogger? logger = null, ulong? seedOverride = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();

        ulong seed = seedOverride ?? config.Seed;
        var world = new SimWorld(config, logger ?? NullSimLogger.Instance, seed);

        world.Log(LogLevel.Debug, "sim", $"World created (seed={seed}).");

        if (world.Villagers.Count == 1)
        {
            world.Narrate($"{world.Villager.Name} begins. {world.Clock.SeasonAndYear()}, no food stored.");
        }
        else
        {
            world.Narrate(
                $"{world.Villagers.Count} exiles arrive in {world.Households.Count} households. " +
                $"{world.Clock.SeasonAndYear()}, no food stored.");
        }

        return world;
    }

    /// <summary>
    /// Write a line of the villager's story.
    /// </summary>
    /// <remarks>
    /// The life log is not a separate system — it is the <c>INFO</c>-level view of
    /// sim events (spec §7). Same sink, same tick-stamping, same ordering, so the
    /// story a player reads and the log an engineer debugs from are the same
    /// artifact. Keep the wording plain and past-tense; this is the legibility
    /// deliverable, and it should read like a life rather than a changelog.
    /// </remarks>
    public void Narrate(string text) => Log(LogLevel.Info, "life", text);

    /// <summary>
    /// Log an entry stamped with the current tick.
    /// </summary>
    /// <remarks>
    /// Routing all sim logging through here is what guarantees METHODOLOGY.md §4's
    /// tick-stamping requirement — there is no way to emit an unstamped entry.
    /// </remarks>
    public void Log(LogLevel level, string subsystem, string message) =>
        Logger.Log(Tick, level, subsystem, message);

    /// <summary>
    /// Whether anything is listening at this level.
    /// </summary>
    /// <remarks>
    /// <b>Guard every DEBUG line with this.</b> The audit log is detailed enough that
    /// building its messages unconditionally would cost real time in the 300-year
    /// acceptance runs, where the sink discards them all — string interpolation happens
    /// before the sink ever gets a say. This is the check that makes rich logging free
    /// when nobody is reading it.
    /// </remarks>
    public bool Logs(LogLevel level) => level >= Logger.MinimumLevel;

    /// <summary>
    /// Record something a particular villager did, for the audit log.
    /// </summary>
    /// <remarks>
    /// Named and numbered, always in the same shape, so a run can be filtered down to
    /// one person's whole life with a text search. That is what turns a log into
    /// something you can answer questions with rather than something you scroll.
    /// </remarks>
    public void LogVillager(LogLevel level, Villager villager, string subsystem, string message)
    {
        if (!Logs(level))
        {
            return;
        }

        Log(level, subsystem, $"{villager.Name} #{villager.Id}: {message}");
    }
}
