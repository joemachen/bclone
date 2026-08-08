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
    /// <para>
    /// <b>No longer immutable</b> (`specs/mutable-terrain.md`). It said the hash *"covers it
    /// once and it can never drift"*, which was true of the contents and never of the
    /// hashing: <c>StateHash.MixMap</c> has always walked the live tile array on every
    /// <c>Compute</c>, so a changed tile has always been covered. What genuinely had to be
    /// built was the cache-invalidation path D41 predicted.
    /// </para>
    /// <para>
    /// <b>Change it through <see cref="SetTerrain"/>, not through the map</b>, or nothing
    /// finds out.
    /// </para>
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

    /// <summary>
    /// How many people the player has put on a kind of work, anywhere in the village (D109).
    /// </summary>
    /// <remarks>
    /// <b>⭐ Derived, not stored, and that is the whole of "one number shown from two ends".</b>
    /// The buildings hold the numbers; this is their sum. So there is no global figure that can
    /// disagree with the buildings under it — the disagreement that <c>JobLimits</c> (D106) made
    /// possible and that D109 deleted the moment staffing became the only source of truth.
    /// Construction sites are skipped: nobody is ever posted to one (D108).
    /// </remarks>
    public int ProfessionTotal(JobKind kind)
    {
        int total = 0;
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Kind == kind && !Workplaces[i].IsSite)
            {
                total += Workplaces[i].Staffing;
            }
        }

        return total;
    }

    /// <summary>Room left across every building of a kind — what the total may still rise to.</summary>
    public int ProfessionCapacity(JobKind kind)
    {
        int total = 0;
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Kind == kind && !Workplaces[i].IsSite)
            {
                total += Workplaces[i].Capacity;
            }
        }

        return total;
    }

    /// <summary>
    /// Put <paramref name="wanted"/> people on a kind of work, spread across its buildings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ The global end of the one number</b> (Joe, D109): <em>"Staffing changes made in the
    /// global professions panel should also be made automatically in the related buildings."</em>
    /// Round-robin in id order, capped by each building's <see cref="Workplace.Capacity"/> — so
    /// two builders with two huts is one each, and a full hut means the next worker of that
    /// profession needs another hut.
    /// </para>
    /// <para>
    /// <b>Round-robin rather than fill-the-first</b>, because the buildings are in different
    /// places and the allocator chooses people by distance: piling everyone into hut 1 would
    /// quietly make hut 2 decorative, and the player would have to discover that by watching.
    /// </para>
    /// <para>
    /// <b>Obeyed and said out loud</b>, which is D43's pattern and D62's: asking for more than
    /// the village has room for is allowed and reported, never silently trimmed.
    /// </para>
    /// </remarks>
    public PlacementVerdict SetProfession(JobKind kind, int wanted)
    {
        if (wanted < 0)
        {
            wanted = 0;
        }

        int placed = DistributeStaffing(kind, wanted);

        Log(Logging.LogLevel.Info, "labour",
            $"You asked for {wanted} on {Describe(kind)}. {Clock.SeasonAndYear()}.");

        if (placed < wanted)
        {
            return PlacementVerdict.Yes(
                $"There is only room for {placed} on {Describe(kind)}, so {wanted} cannot all be "
                + "put to work. Build somewhere for them first.");
        }

        // The two that kill people if nobody does them (D45: hunger in six days, an unheated
        // house in twenty-five). Said plainly rather than refused.
        if (wanted == 0 && kind is JobKind.Forager or JobKind.Woodcutter)
        {
            return PlacementVerdict.Yes(
                $"Nobody will be put on {Describe(kind)} at all. The village will live on what "
                + "it has already put away.");
        }

        return PlacementVerdict.Fine;
    }

    private static string Describe(JobKind kind) => kind switch
    {
        JobKind.Forager => "gathering",
        JobKind.Forester => "felling timber",
        JobKind.Woodcutter => "splitting firewood",
        JobKind.Marketer => "the market",
        JobKind.Builder => "building",
        _ => kind.ToString().ToLowerInvariant(),
    };

    /// <summary>Able adults with no job — the hands everything spare is done by (D63).</summary>
    public int Laborers
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Villagers.Count; i++)
            {
                if (Villagers[i].IsLaborer)
                {
                    count++;
                }
            }

            return count;
        }
    }

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
    public int FoodInGranaries() => TotalAccepting(Goods.Food, static store => store.Food);

    /// <summary>Total logs anywhere a household or a builder could fetch them from.</summary>
    public int LogsInSheds() => TotalAccepting(Goods.Logs, static store => store.Logs);

    /// <summary>
    /// Total firewood the village can actually reach — the supply the fuel quota reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Any store that takes firewood, not only a shed (D79).</b> D29's rule is that a pile
    /// in <em>somebody's house</em> is not supply, because no errand reaches it — and that
    /// still holds, because household larders are not stores. But a cart and a storage pile
    /// are, and reading only sheds made them invisible.
    /// </para>
    /// <para>
    /// <b>Measured by Joe, twice, and it starved his village.</b> A cold start has no shed,
    /// so this returned zero however much fuel was standing about: `WoodcuttersWanted` saw a
    /// village with no firewood at all and put every spare hand on the fuel chain, forever.
    /// His cart held <b>541 firewood</b> and nobody was foraging — <em>"they were cold too,
    /// but it was hunger that killed them."</em>
    /// </para>
    /// <para>
    /// <b>This is the D76 seam on the quota side</b>, which `specs/storage-piles.md §4.1`
    /// named and then did not fix. The lesson is that widening the finders was half the job:
    /// anything that answers <em>how much has the village got?</em> had the same bug.
    /// </para>
    /// </remarks>
    public int FirewoodInSheds() => TotalAccepting(Goods.Firewood, static store => store.Firewood);

    /// <summary>Sum a good across every store that will hold it, reachable or not.</summary>
    private int TotalAccepting(Goods goods, Func<Stockpile, int> read)
    {
        int total = 0;
        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            if (StoreBuildings[i].Accepts(goods))
            {
                total += read(StoreBuildings[i].Store);
            }
        }

        return total;
    }

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
    /// <summary>
    /// The nearest store that will <em>take</em> this good, whatever kind of building it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ask by what a store holds, not by which building it is.</b> This is the fix for a
    /// bug that has now been patched four times: <c>TryTakeBuildingTimber</c>,
    /// <c>StoreForTheLoad</c>, the builder's material fetch and the woodcutter's log fetch
    /// each named <see cref="StoreKind.Shed"/> and each went blind the moment the goods were
    /// somewhere else. The founders' cart was the first store to expose it and the storage
    /// pile would have been the fifth.
    /// </para>
    /// <para>
    /// <em>"Where is the nearest shed?"</em> is a question about buildings.
    /// <em>"Where can I put these logs?"</em> is a question about goods, and only the second
    /// survives a new kind of store. <see cref="NearestStore"/> stays for the cases that
    /// genuinely mean a particular building — naming one, or refunding a demolition — but
    /// nothing that moves goods should be asking it.
    /// </para>
    /// <para>
    /// Unreachable stores are skipped and ties go to the lower id, exactly as
    /// <see cref="NearestStore"/> does: with water impassable (D40) a pile on the far bank
    /// is not a long walk, it is no walk at all.
    /// </para>
    /// </remarks>
    public StoreBuilding? NearestStoreAccepting(
        GridPos from, Goods goods, Func<StoreBuilding, bool> usable)
    {
        ArgumentNullException.ThrowIfNull(usable);

        StoreBuilding? best = null;
        int bestCost = int.MaxValue;

        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            StoreBuilding store = StoreBuildings[i];
            if (!store.Accepts(goods) || !usable(store))
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
    //  Goods on the ground (D96)
    // ---------------------------------------------------------------

    /// <summary>
    /// Loads people have set down because nothing would take them (D96).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A list of its own, beside <see cref="StoreBuildings"/> and never inside it.</b> That
    /// separation is D96's supply-invisible restraint, bought structurally: every reader that
    /// answers <em>what has the village got?</em> or <em>where can this go?</em> walks the
    /// store list, so a heap is invisible to all of them without one of them being told.
    /// See <see cref="GroundStack"/> for why a fifth <see cref="StoreKind"/> would have been
    /// the sixth instalment of D76.
    /// </para>
    /// <para>
    /// Appended in the order things were put down and never reordered, for the reason the
    /// villagers and the stores are: this list decides which heap somebody walks to when two
    /// are equally close, and a tie resolved by iteration order is a desync waiting to happen.
    /// </para>
    /// </remarks>
    public List<GroundStack> GroundStacks { get; } = new();

    /// <summary>
    /// Put a load down where somebody is standing. The one door in.
    /// </summary>
    /// <remarks>
    /// <b>Last resort, never a choice</b> (D96) — the callers are the ones that have just
    /// found nowhere at all to put a load, and there are only three of them. Merged into a
    /// heap of the same good already on the tile, so a clearing worked over a year is one
    /// pile of logs rather than fifty stacked in the same square.
    /// </remarks>
    public void SetDown(GridPos position, Goods goods, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SayIfThereIsNowhereAtAllForThis(goods);

        for (int i = 0; i < GroundStacks.Count; i++)
        {
            if (GroundStacks[i].Position == position && GroundStacks[i].Goods == goods)
            {
                GroundStacks[i].Amount += amount;
                return;
            }
        }

        GroundStacks.Add(new GroundStack { Position = position, Goods = goods, Amount = amount });
    }

    /// <summary>A free building the player marked, waiting for its ground to be cleared.</summary>
    /// <remarks>
    /// <b>The kind travels with the tile (D108).</b> This was a bare list of positions when the
    /// pile was the only free building; the builder's hut is the second, and a list that
    /// remembered only <em>where</em> would have raised a pile on ground somebody asked for a
    /// hut on.
    /// </remarks>
    private readonly record struct PendingBuilding(GridPos Position, BuildingKind Kind);

    private readonly List<PendingBuilding> _waitingOnTheGround = new();

    /// <summary>
    /// Free buildings the player has marked whose ground is still being cleared (D100, D108).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Player intent, so it is sim state and it is hashed</b> (D42's rule, D51's case): two
    /// runs of one seed given the same marks must produce the same village, and a building that
    /// is coming is a different world from one that is not.
    /// </para>
    /// <para>
    /// <b>Deliberately not a <see cref="ConstructionSite"/>.</b> A site needs a builder to tick
    /// it, and the whole point of a free building is that it needs nobody — a site would put
    /// the builder dependency back and re-open the window D95 died in. This is a waiting list,
    /// which is what it actually is.
    /// </para>
    /// </remarks>
    public IReadOnlyList<GridPos> BuildingsWaitingOnTheGround
    {
        get
        {
            var tiles = new List<GridPos>(_waitingOnTheGround.Count);
            for (int i = 0; i < _waitingOnTheGround.Count; i++)
            {
                tiles.Add(_waitingOnTheGround[i].Position);
            }

            return tiles;
        }
    }

    /// <summary>
    /// Raise any free building that was waiting for this tile to be cleared.
    /// </summary>
    /// <remarks>
    /// <b>Hung off <see cref="SetTerrain"/> — the one door terrain changes through (D85).</b>
    /// Hooking <c>Harvest</c> instead would have been the same thing today and wrong the day
    /// anything else clears ground; the rule that door exists for is that there is one place
    /// to ask. Guarded by an empty-list compare, so a village that has marked nothing free
    /// pays nothing for the check.
    /// </remarks>
    private void RaiseAnythingWaitingOn(GridPos tile)
    {
        if (_waitingOnTheGround.Count == 0
            || TerrainRules.Yields(Map.TerrainAt(tile)) is not null)
        {
            return;
        }

        for (int i = 0; i < _waitingOnTheGround.Count; i++)
        {
            if (_waitingOnTheGround[i].Position != tile)
            {
                continue;
            }

            BuildingKind kind = _waitingOnTheGround[i].Kind;
            _waitingOnTheGround.RemoveAt(i);
            string name = NameFor(kind);

            // Something may have gone up here while the trees were coming down. Said out
            // loud rather than silently dropped: a mark the player made and never sees the
            // result of is the untraceable outcome §1.1 forbids.
            if (SomethingStandsAt(tile))
            {
                Narrate($"The ground at {tile} was cleared, but something else stands there "
                    + $"now, so {name} was never laid out. {Clock.SeasonAndYear()}.");
                return;
            }

            RaiseFreeBuilding(kind, tile, name);
            Narrate($"{Capitalised(name)} was laid out on the ground the village just "
                + $"cleared. {Clock.SeasonAndYear()}.");
            return;
        }
    }

    /// <summary>Put up a building that costs nothing — a store, or a workplace (D108).</summary>
    /// <remarks>
    /// One place, so the two ways a free building can arrive — marked on clear ground, or
    /// raised later when its ground is cleared — cannot disagree about what it becomes. The
    /// same argument <see cref="RaiseStore"/> makes about the two ways a store arrives.
    /// </remarks>
    private void RaiseFreeBuilding(BuildingKind kind, GridPos position, string name)
    {
        if (kind == BuildingKind.Pile)
        {
            RaiseStore(kind, position, name);
            return;
        }

        // ⭐ THE BUILDER'S HUT, AND IT IS WHY THIS METHOD TAKES A KIND (D108). The one
        // workplace the player places that costs nothing — because it is the building every
        // other building waits on, and charging timber for it is the circle the pile exists
        // to avoid.
        if (kind == BuildingKind.BuilderHut)
        {
            RaiseBuilderHut(position, name);
            return;
        }

        throw new ArgumentOutOfRangeException(
            nameof(kind), kind, "That kind of building is not free.");
    }

    /// <summary>Put a builder's hut into the world, with the seats the economy derives.</summary>
    private void RaiseBuilderHut(GridPos position, string name) =>
        Workplaces.Add(new Workplace
        {
            Id = NextWorkplaceId(),
            Kind = JobKind.Builder,
            Name = name,
            Position = position,
            Capacity = VillageEconomy.BuilderHutCapacity(Config),
            CatchmentRadius = TravelCostField.TilesToCost(Config.ForagerCatchmentTiles),
        });

    /// <summary>Whether anyone in the village builds — that is, whether a hut stands.</summary>
    /// <remarks>
    /// <b>Asked of the world rather than remembered</b> (D66, D71): a flag is one more thing
    /// that can be set and not cleared, and a village whose only hut was pulled down while a
    /// flag still said otherwise would wait forever for a builder who is never coming.
    /// </remarks>
    public bool HasABuildersHut()
    {
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Kind == JobKind.Builder && !Workplaces[i].IsSite)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the village has already been told it has nowhere for a good.</summary>
    /// <remarks>
    /// <b>Gates narration and nothing else</b>, which is why it is not in the state hash: two
    /// runs of one seed say the same sentence at the same tick because everything that decides
    /// it is hashed. It exists for D42's rule about the distance warning — one considered
    /// sentence, rather than a nag the player learns to click past.
    /// </remarks>
    private readonly bool[] _saidThereIsNowhereFor = new bool[Stockpile.Kinds];

    /// <summary>
    /// Say so, once, when a good is being set down because the village has nowhere for it
    /// at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ This is the sentence that keeps D90 step 4 fair.</b> The cart stopped taking logs,
    /// so a founding that paints trees and places no store fells timber into a field where
    /// <c>LogsInSheds</c> cannot see it — goods on the ground are supply-invisible by design —
    /// and the hut then reports <em>"no logs here to split"</em> while four hundred logs lie
    /// about. <b>That is D89's silent strangling in a new costume</b>, and D89 is the decision
    /// that named it as a legibility failure before a balance one.
    /// </para>
    /// <para>
    /// <b>Nowhere AT ALL, not merely full.</b> A village whose stores are packed has a problem
    /// it can see in its own stores; a village with no store that will take a good has a
    /// problem it cannot see anywhere, and only the second is worth interrupting for. So this
    /// fires roughly once a game, on the first felled tree of a founding with no pile.
    /// </para>
    /// </remarks>
    private void SayIfThereIsNowhereAtAllForThis(Goods goods)
    {
        if (_saidThereIsNowhereFor[(int)goods])
        {
            return;
        }

        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            if (StoreBuildings[i].Accepts(goods))
            {
                return;
            }
        }

        _saidThereIsNowhereFor[(int)goods] = true;

        Narrate(
            $"There is nowhere in the village to keep {goods.ToString().ToLowerInvariant()}, "
            + "so it is being left "
            + "on the ground where it falls — and goods on the ground feed nobody and build "
            + $"nothing. A storage pile costs only the cleared ground it stands on. "
            + $"{Clock.SeasonAndYear()}.");
    }

    /// <summary>Take up to <paramref name="amount"/> off a heap; it goes when it is empty.</summary>
    public int TakeFromGround(GridPos position, Goods goods, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        for (int i = 0; i < GroundStacks.Count; i++)
        {
            GroundStack stack = GroundStacks[i];
            if (stack.Position != position || stack.Goods != goods)
            {
                continue;
            }

            int taken = amount < stack.Amount ? amount : stack.Amount;
            stack.Amount -= taken;
            if (stack.Amount == 0)
            {
                GroundStacks.RemoveAt(i);
            }

            return taken;
        }

        return 0;
    }

    /// <summary>How much of one good is lying on a tile.</summary>
    public int GroundStackAt(GridPos position, Goods goods)
    {
        for (int i = 0; i < GroundStacks.Count; i++)
        {
            if (GroundStacks[i].Position == position && GroundStacks[i].Goods == goods)
            {
                return GroundStacks[i].Amount;
            }
        }

        return 0;
    }

    /// <summary>
    /// The nearest heap worth walking to, or null if no trip would achieve anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ Only a heap a store would actually take.</b> Without that condition somebody
    /// picks up the load beside a full shed and carries it straight back to the same full
    /// shed, forever. With it, a village whose stores are all full simply leaves its heaps
    /// alone until there is room — which is the self-correcting behaviour D96 predicted, and
    /// it needs no rule telling anybody to.
    /// </para>
    /// <para>
    /// <b>The empty-list guard is not an optimisation.</b> This is asked by every able adult
    /// with nothing else to do, every tick — the same position that took the suite from four
    /// minutes to over ten when <see cref="NearestHarvest"/> shipped without one (D87). A
    /// village that has never dropped anything pays one integer compare.
    /// </para>
    /// </remarks>
    public GroundStack? NearestGroundStack(GridPos from)
    {
        if (GroundStacks.Count == 0)
        {
            return null;
        }

        GroundStack? best = null;
        int bestCost = int.MaxValue;

        for (int i = 0; i < GroundStacks.Count; i++)
        {
            GroundStack stack = GroundStacks[i];
            if (NearestStoreAccepting(
                    stack.Position, stack.Goods, static store => !store.Store.IsFull) is null)
            {
                continue;
            }

            int cost = TravelCost.Cost(from, stack.Position);
            if (cost != TravelCostField.Unreachable && cost < bestCost)
            {
                bestCost = cost;
                best = stack;
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

    // ---------------------------------------------------------------
    //  Work ground, and the first limit in this game that is not distance (D86)
    // ---------------------------------------------------------------

    /// <summary>
    /// How much ground a workplace can keep, given the hands currently assigned to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read off the workers actually assigned, not off the building's capacity</b> —
    /// Joe's wording, and the livelier of the two. A hut whose forester dies becomes
    /// overstretched that moment and can say so, where a capacity-based allowance would go
    /// on claiming the land was fine while nobody worked it.
    /// </para>
    /// <para>
    /// <b>The "to a limit" in D86 needs no number of its own</b>, and that is the whole
    /// reason to state the rule this way: a workplace cannot be staffed past its capacity,
    /// so the largest ground anyone can hold is capacity × per-worker, derived rather than
    /// typed (D16). A second cap would be a number to argue about that says nothing new.
    /// </para>
    /// </remarks>
    public int WorkGroundAllowanceFor(Workplace workplace)
    {
        ArgumentNullException.ThrowIfNull(workplace);
        return workplace.WorkerIds.Count * Config.WorkGroundTilesPerWorker;
    }

    /// <summary>Whether a workplace has been given more ground than it has hands for.</summary>
    /// <remarks>
    /// <b>A state, not just a moment.</b> The warning fires when land is painted, but the
    /// condition outlives the painting — somebody dies, the staffing is turned down, and the
    /// ground is suddenly too much. The panel needs to be able to ask.
    /// </remarks>
    public bool IsOverstretched(Workplace workplace) =>
        Zones.WorkGroundTiles(workplace.Id) > WorkGroundAllowanceFor(workplace);

    /// <summary>
    /// Give one tile of ground to a workplace, and say so if it is now more than its
    /// hands can keep.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A warning, never a refusal</b> (D86, and D43's rule for a site that is merely
    /// far). Painting big and hiring afterwards is an ordinary way to play, and a brush that
    /// stopped at the current headcount would make the player fight the staffing control
    /// with every stroke.
    /// </para>
    /// <para>
    /// <b>Ground already held by another building is left alone and reported</b>, so a
    /// careless drag across the valley cannot quietly unstaff somebody else's hut. The
    /// caller decides how loudly to say it: this returns a verdict per tile, and D42
    /// settled that a brush speaks <em>once per stroke</em>.
    /// </para>
    /// <para>
    /// <b>Water is never painted</b>, matching <see cref="PaintResidential"/> — the shape
    /// the player gets is the shape they can actually use.
    /// </para>
    /// </remarks>
    public PlacementVerdict PaintWorkGround(Workplace workplace, GridPos tile)
    {
        ArgumentNullException.ThrowIfNull(workplace);

        if (!Map.Contains(tile))
        {
            return PlacementVerdict.No("That is outside the valley.");
        }

        if (Map.TerrainAt(tile) == Terrain.Water)
        {
            return PlacementVerdict.No("Nobody can work the water.");
        }

        int held = Zones.WorkGroundOwner(tile);
        if (held != 0 && held != workplace.Id)
        {
            Workplace? other = FindWorkplace(held);
            return PlacementVerdict.No(
                other is null
                    ? "That ground is already spoken for."
                    : $"That ground belongs to {other.Name}.");
        }

        Zones.SetWorkGround(tile, workplace.Id);

        int tiles = Zones.WorkGroundTiles(workplace.Id);
        int allowance = WorkGroundAllowanceFor(workplace);
        if (tiles > allowance)
        {
            int hands = workplace.WorkerIds.Count;
            return PlacementVerdict.Yes(
                hands == 0
                    ? $"{Capitalised(workplace.Name)} has {tiles} tiles and nobody working it. "
                      + $"Every hand there can keep {Config.WorkGroundTilesPerWorker}."
                    : $"{Capitalised(workplace.Name)} has {tiles} tiles and {hands} "
                      + $"{(hands == 1 ? "pair of hands" : "pairs of hands")} to keep them — "
                      + $"enough for {allowance}. The rest will go untended.");
        }

        return PlacementVerdict.Fine;
    }

    /// <summary>Take one tile of ground back from a workplace.</summary>
    public bool EraseWorkGround(Workplace workplace, GridPos tile)
    {
        ArgumentNullException.ThrowIfNull(workplace);
        return Zones.WorkGroundOwner(tile) == workplace.Id && Zones.SetWorkGround(tile, 0);
    }

    // ---------------------------------------------------------------
    //  The harvest brush (D87)
    // ---------------------------------------------------------------

    /// <summary>
    /// Paint one tile as somewhere the village means to take what is standing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Painting harvest is <em>taking</em>; a forester's ground is <em>keeping</em></b>
    /// (D87). This is the brush the opening runs on: the founders mark the trees they mean
    /// to fell, and the hands no workplace wants go and fell them. The forester's hut is
    /// what answers running out, and it comes later on purpose — the pressure first, the
    /// sustainable answer second, which is the argument §12.7 already makes about planting.
    /// </para>
    /// <para>
    /// <b>Permissive about where and firm about what</b>, like
    /// <see cref="PaintResidential"/>: a tile with nothing on it to take is simply never
    /// painted, so the shape the player gets is work that actually exists rather than a
    /// promise the village cannot keep.
    /// </para>
    /// </remarks>
    public PlacementVerdict PaintHarvest(GridPos tile, HarvestBrush brush = HarvestBrush.Everything)
    {
        if (!Map.Contains(tile))
        {
            return PlacementVerdict.No("That is outside the valley.");
        }

        Goods? standing = TerrainRules.Yields(Map.TerrainAt(tile));
        if (standing is null)
        {
            return PlacementVerdict.No("There is nothing standing there to take.");
        }

        // ⭐ THE MODE IS A FILTER AND IS THEN FORGOTTEN (D90, Joe's call of two). A marked
        // tile is simply marked; what a laborer gets from it is whatever is standing there.
        // So "clear the stone and leave the wood" works by the wood never taking the paint,
        // rather than by storing three layers and letting a tile be marked for a good it
        // does not have.
        Goods? wanted = WhatTheBrushTakes(brush);
        if (wanted is not null && standing.Value != wanted.Value)
        {
            return PlacementVerdict.No(
                $"The brush is set to {Describe(brush)}, and that is {Describe(standing.Value)}.");
        }

        Zones.SetHarvest(tile, true);
        return PlacementVerdict.Fine;
    }

    /// <summary>The good a brush setting will accept, or null for "anything".</summary>
    private static Goods? WhatTheBrushTakes(HarvestBrush brush) => brush switch
    {
        HarvestBrush.Trees => Goods.Logs,
        HarvestBrush.Stone => Goods.Stone,
        HarvestBrush.Iron => Goods.Iron,
        _ => null,
    };

    private static string Describe(HarvestBrush brush) => brush switch
    {
        HarvestBrush.Trees => "fell trees",
        HarvestBrush.Stone => "clear stone",
        HarvestBrush.Iron => "dig iron",
        _ => "clear everything",
    };

    private static string Describe(Goods goods) => goods switch
    {
        Goods.Logs => "woodland",
        Goods.Stone => "a stone seam",
        Goods.Iron => "an iron seam",
        _ => goods.ToString().ToLowerInvariant(),
    };

    /// <summary>Un-paint a tile the village had meant to clear.</summary>
    public bool EraseHarvest(GridPos tile) => Zones.SetHarvest(tile, false);

    /// <summary>
    /// Whether a tile has been cleared of everything standing on it — so a site here may be
    /// worked (D101).
    /// </summary>
    /// <remarks>
    /// <b>The inverse of <see cref="HasSomethingToHarvest"/>, and named for the question its
    /// callers are actually asking.</b> A builder does not want to know whether there is a
    /// harvest here; they want to know whether they can start. Two readings of one fact, and
    /// the reason both exist is that <c>!HasSomethingToHarvest(x)</c> at a building call site
    /// is the sort of double negative somebody eventually gets backwards.
    /// </remarks>
    public bool GroundIsClearAt(GridPos tile) => !HasSomethingToHarvest(tile);

    /// <summary>Whether a tile holds anything a laborer could take.</summary>
    /// <remarks>
    /// <b>One question, so a new harvestable terrain is answered here and nowhere else.</b>
    /// Only forest today; stone and iron are D84's finite deposits and land next. This is
    /// deliberately the same shape as <c>TerrainRules.IsPassable</c> — the seam D76 spent
    /// five instalments learning to recognise.
    /// </remarks>
    public bool HasSomethingToHarvest(GridPos tile) =>
        TerrainRules.Yields(Map.TerrainAt(tile)) is not null;

    /// <summary>
    /// The nearest tile the village has asked to be cleared, or null if there are none.
    /// </summary>
    /// <remarks>
    /// <b>Measured from where the villager is standing, not from home.</b> A laborer holds
    /// no job, so this is an errand rather than a commute — there is no daily walk for it to
    /// flicker against, and taking the nearest tree to hand is what a person would do.
    /// Walked in a fixed order so two runs pick the same tree.
    /// </remarks>
    public GridPos? NearestHarvest(GridPos from)
    {
        // ⭐ NOTHING PAINTED, NOTHING TO SCAN — and this line is not an optimisation, it
        // is the difference between the feature being free and being ruinous. This is
        // asked by every able adult who has nothing else to do, every tick, and the scan
        // below walks the whole valley: 9,600 tiles × everybody idle × every tick. The
        // full suite went from four minutes to over ten the moment laborers could clear,
        // in a suite where almost nothing paints anything.
        //
        // A village that has never used the brush now pays one integer compare, which is
        // the same argument the sparse hashing makes one file over: a feature nobody has
        // switched on should cost nothing at all.
        if (Zones.HarvestTiles == 0)
        {
            return null;
        }

        GridPos? best = null;
        int bestCost = int.MaxValue;

        for (int y = Map.MinY; y < Map.MinY + Map.Height; y++)
        {
            for (int x = Map.MinX; x < Map.MinX + Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (!Zones.IsHarvest(at))
                {
                    continue;
                }

                // Painted ground whose tree has already gone stops being work. The
                // village un-paints it rather than sending somebody to an empty tile.
                if (!HasSomethingToHarvest(at))
                {
                    Zones.SetHarvest(at, false);
                    continue;
                }

                int cost = TravelCost.Cost(from, at);
                if (cost < bestCost)
                {
                    best = at;
                    bestCost = cost;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Take what is standing on a tile: the ground is cleared and the goods come out.
    /// </summary>
    /// <remarks>
    /// <b>The tile is spent</b> — this is D84's deposit rule, and the difference between
    /// the brush and the forester's hut in one method. Terrain goes through
    /// <see cref="SetTerrain"/> so the routing cache hears about it, and the paint comes off
    /// because the job is done.
    /// </remarks>
    public (Goods Goods, int Amount) Harvest(GridPos tile)
    {
        Goods? yields = TerrainRules.Yields(Map.TerrainAt(tile));
        if (yields is null)
        {
            return (Goods.Logs, 0);
        }

        SetTerrain(tile, Terrain.Grass);
        Zones.SetHarvest(tile, false);

        // One number per kind of ground, and the terrain is what says which — a new
        // harvestable kind is a row in TerrainRules.Yields and a key in config, not a
        // fifth place to remember.
        int amount = yields.Value switch
        {
            Goods.Logs => Config.LogsPerForestTile,
            Goods.Stone => Config.StonePerRockTile,
            Goods.Iron => Config.IronPerDepositTile,
            _ => 0,
        };

        return (yields.Value, amount);
    }

    /// <summary>Place names read "a forester's hut"; sometimes one has to start a sentence.</summary>
    private static string Capitalised(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];

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

    /// <summary>
    /// Change what a tile is made of, and tell everything that had cached an opinion
    /// about it. Returns whether anything changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The only way terrain should change during a run</b>
    /// (`specs/mutable-terrain.md §4.1`). A felled stand, a paved road, a bridge (D40) all
    /// come through here, so there is one place that knows the valley moved.
    /// </para>
    /// <para>
    /// <b>The cache is dropped on a change of passability, not on a change of terrain</b>,
    /// and that distinction is doing real work rather than being fussy. Every cached route is
    /// a full Dijkstra over the valley, one per destination, and a logger fells several times
    /// a year — dropping them all on every fell would rebuild the village's whole routing
    /// repeatedly for a change that moves no route. Grass and forest cost the same to cross,
    /// so felling genuinely changes nothing about travel; <b>that is now a rule with a test
    /// on it rather than the coincidence the cache had been relying on</b> (§12.3 of
    /// `building-placement.md` calls it luck, correctly).
    /// </para>
    /// <para>
    /// <b>Logged, because a valley changing shape is a thing an audit should be able to
    /// see</b> (METHODOLOGY §4) — and the route-affecting case says so at a level a player's
    /// bug report would carry.
    /// </para>
    /// </remarks>
    public bool SetTerrain(GridPos position, Terrain terrain)
    {
        Terrain before = Map.TerrainAt(position);
        if (!Map.SetTerrain(position, terrain))
        {
            return false;
        }

        bool routeAffecting = TerrainRules.IsPassable(before) != TerrainRules.IsPassable(terrain);
        if (routeAffecting)
        {
            TravelCost.Forget();
        }

        if (Logs(LogLevel.Debug))
        {
            Log(
                LogLevel.Debug,
                "map",
                $"{position} became {terrain} (was {before})."
                + (routeAffecting
                    ? " Passability changed, so every cached route was dropped."
                    : " Passability is unchanged, so routes stand."));
        }

        // Ground that has just been cleared may be ground a pile was waiting for (D100).
        RaiseAnythingWaitingOn(position);

        return true;
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

        // ⚠️ STANDING TREES ARE NOT A REFUSAL, and this is a correction rather than an
        // omission (Joe, D100). It refused a pile on wooded ground and told the player to
        // clear it first — which read the rule backwards. The village clears it: "I want
        // laborers to auto-remove the resources if a building is placed on a resource — the
        // user can if they choose to, but shouldn't have to."
        //
        // So `Mark` paints the ground for harvest instead, and a pile marked on a resource
        // waits for the clearing rather than being turned away. See `Mark`.

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

        // ⭐ THE VILLAGE CLEARS THE GROUND, THE PLAYER DOES NOT HAVE TO (Joe, D100).
        //
        // Marking anything on a tile that still has something standing paints that tile for
        // harvest, and the laborers who already do that work come and take it (D87). No new
        // machinery: the brush, the errand and the deposit rule all exist, and this simply
        // states the intent for them.
        //
        // "The user can if they choose to, but shouldn't have to" — so a player who clears it
        // by hand first sees exactly the same outcome, one step sooner.
        bool groundIsBusy = TerrainRules.Yields(Map.TerrainAt(position)) is not null;
        if (groundIsBusy)
        {
            Zones.SetHarvest(position, true);
        }

        // ⭐ A FREE BUILDING IS NOT A CONSTRUCTION SITE, AND THAT IS THE POINT (D96, D108). It
        // costs nothing and owes no work, so a site for one would be a builder walking over to
        // a footprint to do nothing — and worse than pointless: D95 built the cart's refusal of
        // logs on top of a pile that WAS a site, and the window between marking one and it
        // standing left a forester with nowhere on earth to put a load. Nothing was built at
        // all, 0 homes. Raising it here closes that window rather than narrowing it.
        //
        // ASKED OF THE RECIPE, NOT OF THE KIND (D108). It read `kind == BuildingKind.Pile`,
        // which was true while the pile was the only free building and would have been the
        // sixth silent special case the day a second one arrived. "Does it cost anything?" is
        // the question this branch is actually asking.
        if (recipe.Logs == 0 && recipe.WorkTicks == 0)
        {
            // Clear ground: it stands now. Wooded ground: it stands the moment the wood is
            // gone, and THE CLEARING IS WHAT IT COSTS (D96) — which is still true, and is
            // now a price the village pays rather than an errand the player is sent on.
            if (!groundIsBusy)
            {
                RaiseFreeBuilding(kind, position, name);
                Narrate($"{Capitalised(name)} was laid out on cleared ground. " +
                    $"{Clock.SeasonAndYear()}.");
                return verdict;
            }

            var pending = new PendingBuilding(position, kind);
            if (!_waitingOnTheGround.Contains(pending))
            {
                _waitingOnTheGround.Add(pending);
            }

            Narrate($"{Capitalised(name)} is marked out, and the ground is being cleared for "
                + $"it first. {Clock.SeasonAndYear()}.");
            return verdict;
        }

        RaiseSiteFor(kind, position, name, recipe, forHouseholdId: 0);
        return verdict;
    }

    /// <summary>
    /// Mark out a house for a household that has none (D102).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Internal, because a house is not player-placed and that is settled (D42).</b> The
    /// player paints the neighbourhood and <see cref="Household.ChooseSite"/> picks the tile,
    /// because <c>MaxHomeToWorkTiles</c> is the bound the food economy is derived against.
    /// What D102 changes is only that the house then has to be built.
    /// </para>
    /// <para>
    /// It does not go through <see cref="CanBuildAt"/>: <c>ChooseSite</c> has already found
    /// painted, reachable, buildable ground and thrown if there was none, so asking again
    /// would be a second opinion that can only disagree.
    /// </para>
    /// </remarks>
    internal void MarkHome(int householdId, GridPos position)
    {
        BuildingRecipe recipe = BuildingRecipe.For(BuildingKind.Home, Config);

        // ⚠️ AND WHEN THAT PROMISE IS BROKEN, SAY SO (D110). `ChooseSite` is supposed to have
        // found reachable ground, and in seed 11 of the twelve-seed arm it did not — a house
        // at (-1,-5), on the far bank, which no builder could ever walk to. It cost that
        // village its whole future and there was not one line in the log about it.
        //
        // Logged rather than refused, deliberately: refusing here would leave a roofless
        // family with no site and no explanation, which is the worse of the two silences. The
        // build queue skips what it cannot reach (see `NextToBuild`), so the damage is
        // contained; this is what makes the cause findable rather than the symptom.
        if (!TravelCost.CanReach(FirstHomeOrFoundingSite(), position))
        {
            Log(Logging.LogLevel.Warn, "placement",
                $"A house was sited at {position} for household {householdId}, and there is no "
                + $"route to it from the village — so nobody can ever build it. "
                + $"{Clock.SeasonAndYear()}.");
        }

        // The same rule every other site gets (D100): if something is standing here, the
        // village clears it, and nothing is built until it has.
        if (TerrainRules.Yields(Map.TerrainAt(position)) is not null)
        {
            Zones.SetHarvest(position, true);
        }

        RaiseSiteFor(BuildingKind.Home, position, "a house", recipe, householdId);
    }

    /// <summary>
    /// Every building waiting to be raised, in the order the village will get to them (D104).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE ORDER IS THE ORDER THINGS WERE MARKED. Nothing else.</b> Workplace ids only
    /// ever increase, so "first marked, first built" needs nothing stored and no rule anybody
    /// has to learn — which is the whole of what a player expects from a queue.
    /// </para>
    /// <para>
    /// <b>It was briefly "everything the player marked, then the houses" (D102), and Joe's
    /// village froze to death because of it.</b> He marked a granary in his first spring; it
    /// went in front of two houses that had been waiting since tick 4, took every builder for
    /// forty logs and sixty ticks of work, and <b>nobody ever got a roof</b>. Measured, exactly
    /// as he played it: <b>0 alive, 4 frozen, 0 houses</b>, the granary stuck at 46 of 60 —
    /// against 4 alive and 2 houses in the same opening without it.
    /// </para>
    /// <para>
    /// <b>Marking order solves what D102's rule was for, without that.</b> The founding case
    /// it existed to fix — two houses marked at tick 4 starving the woodcutter's hut — comes
    /// out right anyway, because the player marks the hut at tick 0 and `HouseTheRoofless`
    /// does not run until tick 4. <b>The hut is simply earlier.</b>
    /// </para>
    /// <para>
    /// ⚠️ <b>What it does not solve, and the honest limit of it:</b> a hut marked in year
    /// twenty queues behind every house already waiting, and there is no way to say
    /// <em>"that one first"</em>. That is what an editable priority list is for (Joe), and
    /// this is the order it should default to.
    /// </para>
    /// <para>
    /// The list is rebuilt on each call rather than maintained, for the reason
    /// <see cref="HomeSiteFor"/> gives: a cached order is one more thing that can disagree
    /// with the world.
    /// </para>
    /// </remarks>
    public List<Workplace> BuildQueue()
    {
        var queue = new List<Workplace>();

        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Construction is { IsFinished: false })
            {
                queue.Add(Workplaces[i]);
            }
        }

        // Rank first, then id — so a site nobody has touched sits where it was marked, and a
        // site the player moved sits where they put it (D105).
        queue.Sort(static (a, b) =>
        {
            int byRank = a.EffectiveQueueRank.CompareTo(b.EffectiveQueueRank);
            return byRank != 0 ? byRank : a.Id.CompareTo(b.Id);
        });

        return queue;
    }

    /// <summary>
    /// The site at the head of the build queue — what a builder walks out to (D108).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ The queue is what decides which site a builder goes to, and that is the whole
    /// of the rule.</b> Sites stopped being staffed when the hut arrived, so something had
    /// to say which one the crew works; making it the head of the queue means the player's
    /// ▲ Sooner / ▼ Later (D105) still moves real hands, and D102's guarantee — what the
    /// player marked before a house the village marked for itself — survives as marking
    /// order rather than as a rank the allocator applied. <b>The whole crew works the front
    /// of the queue</b>, which is what a queue means and what makes <em>"3rd of 5"</em> a
    /// sentence a player can plan against.
    /// </para>
    /// <para>
    /// <b>⚠️ A SITE NOBODY CAN WALK TO IS SKIPPED, NOT QUEUED BEHIND.</b> Measured, and it
    /// killed seed 11 of twelve: <c>ChooseSite</c> put a house at (-1,-5) on the far bank,
    /// and because the whole crew works the head of the queue, <b>every builder spent a
    /// century walking toward a place they could never arrive at</b> — eight sites behind it
    /// never raised, four households of eleven ever roofed, thirteen hundred logs in the shed,
    /// nobody starved, nobody frozen, the village simply aged out. That is the silent
    /// unrecoverable death §0.1 rules out.
    /// </para>
    /// <para>
    /// <b>It is the same question <see cref="CanBuildAt"/> refuses on</b>, asked from the same
    /// anchor — so a site the player could never have marked cannot arrive by another door and
    /// stop the village building. <b>The site that produced it should never have been marked</b>
    /// (see <see cref="MarkHome"/>); skipping it here is the belt to that braces, because one
    /// impossible footprint must not be able to cost a village its whole future.
    /// </para>
    /// <para>
    /// <b>One pass and no allocation</b>, unlike <see cref="BuildQueue"/>, which sorts. This
    /// is asked by every builder on every tick, and the suite has already been taught once
    /// what a per-tick per-villager scan costs (D87 — four minutes to over ten).
    /// </para>
    /// </remarks>
    public Workplace? NextToBuild()
    {
        Workplace? head = null;
        GridPos village = FirstHomeOrFoundingSite();

        for (int i = 0; i < Workplaces.Count; i++)
        {
            Workplace candidate = Workplaces[i];
            if (candidate.Construction is not { IsFinished: false }
                || !TravelCost.CanReach(village, candidate.Position))
            {
                continue;
            }

            // The same ordering BuildQueue sorts by, asked as a comparison: rank, then id,
            // so a site nobody has touched sits where it was marked. Two orderings that must
            // agree is the shape of half the bugs in this project's history, so this one is
            // guarded by a test that walks the queue and asks for its head.
            if (head is null
                || candidate.EffectiveQueueRank < head.EffectiveQueueRank
                || (candidate.EffectiveQueueRank == head.EffectiveQueueRank
                    && candidate.Id < head.Id))
            {
                head = candidate;
            }
        }

        return head;
    }

    /// <summary>An unfinished construction site standing on this tile, or null.</summary>
    /// <remarks>
    /// <b>The site is read from where the villager is standing, not from a field on them</b>
    /// (D108). A builder walks to a site as an errand now, so the two legs of the job —
    /// loading materials, then raising the building — can no longer re-derive it from
    /// <c>WorkplaceOf(villager).Construction</c>, which is the hut and has none. A
    /// <c>Villager.SiteId</c> would be exactly the set-and-not-cleared flag D66 and D71 argue
    /// against, and would have to be hashed; asking the position is D87's rule, and the
    /// position is already remembered as the errand they set off for.
    /// </remarks>
    public Workplace? SiteAt(GridPos position)
    {
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Position == position
                && Workplaces[i].Construction is { IsFinished: false })
            {
                return Workplaces[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Move a site one place up or down the build queue (D105, Joe).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe's answer to his own village freezing:</b> <em>"I think this is solved by letting
    /// the user increase/decrease the priority level of a building under construction."</em>
    /// It is, and it is the better answer than any rule about which KINDS of building matter
    /// most — the village cannot know whether this winter needs a granary or a roof, and the
    /// player can.
    /// </para>
    /// <para>
    /// <b>A swap with the neighbour, not a number the player nudges.</b> One press moves it
    /// exactly one place, always — where incrementing a priority value would sometimes move it
    /// past two things and sometimes past none, depending on what its neighbours happened to
    /// hold. The panel says <em>"3rd of 5"</em>, so pressing up had better make it 2nd.
    /// </para>
    /// <para>
    /// Returns false when there is nowhere to move — already first, already last, or not a
    /// site at all — so the view can say nothing happened rather than pretending.
    /// </para>
    /// </remarks>
    public bool MoveInBuildQueue(Workplace site, int places)
    {
        ArgumentNullException.ThrowIfNull(site);

        List<Workplace> queue = BuildQueue();
        int at = queue.FindIndex(candidate => candidate.Id == site.Id);
        int to = at + places;
        if (at < 0 || to < 0 || to >= queue.Count || places == 0)
        {
            return false;
        }

        // Both ends are pinned explicitly, because either may still be sitting on its id.
        // Leaving one null would let it drift the moment anything else was reordered.
        Workplace other = queue[to];
        int mine = site.EffectiveQueueRank;
        int theirs = other.EffectiveQueueRank;

        site.QueueRank = theirs;
        other.QueueRank = mine;

        // Identical ranks fall back to id order, which would undo the swap. Nudge the one
        // that is meant to be earlier so the order is unambiguous.
        if (mine == theirs)
        {
            site.QueueRank = places < 0 ? theirs - 1 : theirs + 1;
        }

        Log(Logging.LogLevel.Info, "placement",
            $"{site.Construction!.Name} moved {(places < 0 ? "up" : "down")} the build queue — "
            + $"now {QueuePositionOf(site)} of {queue.Count}. {Clock.SeasonAndYear()}.");

        return true;
    }

    /// <summary>Where a site stands in <see cref="BuildQueue"/>, counting from one, or 0.</summary>
    public int QueuePositionOf(Workplace site)
    {
        ArgumentNullException.ThrowIfNull(site);

        List<Workplace> queue = BuildQueue();
        for (int i = 0; i < queue.Count; i++)
        {
            if (queue[i].Id == site.Id)
            {
                return i + 1;
            }
        }

        return 0;
    }

    /// <summary>The house being built for a household, or null if none is marked.</summary>
    /// <remarks>
    /// <b>Asked rather than recorded on the household</b> (D66, D71's rule): a flag is one more
    /// thing that can be set and not cleared, and a household whose site was cancelled while
    /// it still believed one was coming would wait for a house forever.
    /// </remarks>
    internal Workplace? HomeSiteFor(int householdId)
    {
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Construction is { Kind: BuildingKind.Home } plan
                && plan.ForHouseholdId == householdId)
            {
                return Workplaces[i];
            }
        }

        return null;
    }

    /// <summary>Put a construction site into the world. One place, for the same reason
    /// <see cref="RaiseStore"/> is one place.</summary>
    /// <remarks>
    /// <b>⭐ NO SEATS (D108).</b> A site used to be a workplace people were assigned to, with
    /// <c>construction_site_capacity</c> hands at it. Joe: <em>"a construction site is a place
    /// that builders should treat as errands"</em> — so builders hold their job at the hut and
    /// walk out to whatever is at the head of the build queue, and a site is a job of work
    /// rather than a livelihood. It stays a <see cref="Workplace"/> because that is what
    /// carries its position, its name and its place in the queue; what it no longer carries is
    /// anybody's employment. <c>construction_site_capacity</c> is deleted rather than zeroed,
    /// on D98's rule that a number which is always zero is a lie waiting to be found.
    /// </remarks>
    private void RaiseSiteFor(
        BuildingKind kind, GridPos position, string name, BuildingRecipe recipe, int forHouseholdId)
    {
        Workplaces.Add(new Workplace
        {
            Id = NextWorkplaceId(),
            Kind = JobKind.Builder,
            Name = $"{name} (building)",
            Position = position,
            Capacity = 0,
            CatchmentRadius = TravelCostField.TilesToCost(Config.ForagerCatchmentTiles),
            Construction = new ConstructionSite
            {
                Kind = kind,
                Name = name,
                Recipe = recipe,
                ForHouseholdId = forHouseholdId,
            },
        });

        Log(Logging.LogLevel.Info, "placement",
            $"{name} was marked out — {recipe.Logs} logs and {recipe.WorkTicks} ticks of work. " +
            $"{Clock.SeasonAndYear()}.");

        // ⭐ AND IT SAYS SO WHEN NOBODY WILL COME (D108). A hut is the only path to a
        // building now, so a village without one can mark out as much as it likes and watch
        // none of it rise. **A silent stall is the one thing that would make this unfair
        // rather than hard** — D93's ruling, and §1.1: a footprint that never moves and never
        // explains itself is the untraceable outcome the whole design refuses. Narrated
        // rather than logged, because it is a sentence about what the player should do next.
        if (!HasABuildersHut())
        {
            Narrate($"{Capitalised(name)} is marked out, but nobody in the village builds — "
                + $"a builder's hut costs nothing but the ground it stands on, and until one "
                + $"is up and staffed, nothing will be raised. {Clock.SeasonAndYear()}.");
        }
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

        // ⚠️ THE PILE AND THE CART REFUNDED HALF A MARKET, and that was a free-timber press:
        // both fell into a `_ => Market` arm, so pulling down a heap of cleared ground — or
        // the wagon the founders turned up in — paid back seventeen logs out of a building
        // that cost nothing. Found while reading this to make the pile instant (D96).
        //
        // The two buildings the player did not pay for return nothing, which is the honest
        // answer and the one the recipes already give: a pile's is (0, 0).
        // Named rather than defaulted (D108): the cart is the other building nobody paid for,
        // and a pile's recipe of (0, 0) is the right refund for both. An unknown store kind is
        // a bug rather than a pile.
        BuildingKind kind = building.Kind switch
        {
            StoreKind.Granary => BuildingKind.Granary,
            StoreKind.Shed => BuildingKind.Shed,
            StoreKind.Market => BuildingKind.Market,
            StoreKind.Pile => BuildingKind.Pile,
            StoreKind.Cart => BuildingKind.Pile,
            _ => throw new ArgumentOutOfRangeException(
                nameof(building), building.Kind, "That kind of store has no refund."),
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
                RetireWorkplace(Workplaces[i]);
            }
        }

        StoreBuilding? shed = NearestStore(
            building.Position, StoreKind.Shed, static store => !store.Store.IsFull);
        int recovered = shed?.Store.Receive(Goods.Logs, back) ?? 0;

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
        RetireWorkplace(site);

        StoreBuilding? shed = NearestStore(
            site.Position, StoreKind.Shed, static store => !store.Store.IsFull);
        shed?.Store.Receive(Goods.Logs, back);

        Narrate($"{site.Construction.Name} was abandoned before it was built — " +
            $"{back} logs went back to store. {Clock.SeasonAndYear()}.");
    }

    /// <summary>Turn a finished site into the building it was always going to be.</summary>
    internal void Complete(Workplace site)
    {
        ConstructionSite plan = site.Construction!;

        switch (plan.Kind)
        {
            // ⭐ A HOUSE IS FINISHED, AND THE FAMILY IT WAS BUILT FOR MOVES IN (D102).
            //
            // The household is named on the site rather than looked up now, so a family who
            // waited two years gets THAT house — not whichever roofless family happens to be
            // first in the list on the day it is done.
            case BuildingKind.Home:
                Household? family = FindHousehold(plan.ForHouseholdId);
                if (family is null)
                {
                    // Nobody left to move in. Not an error — a family can die out while its
                    // house is being raised, and an empty house is a house (HouseholdSystem
                    // hands it to the next family that needs one).
                    Narrate($"The house at {site.Position} was finished with nobody left to "
                        + $"live in it. {Clock.SeasonAndYear()}.");
                    break;
                }

                family.HomePosition = site.Position;
                NeedsMoreResidentialLand = false;

                // Standing outside their new door, rather than wherever the errand that
                // filled the last tick left them.
                for (int i = 0; i < Villagers.Count; i++)
                {
                    if (Villagers[i].Alive && Villagers[i].HouseholdId == family.Id)
                    {
                        Villagers[i].Position = site.Position;
                    }
                }

                Narrate($"The {family.Name} household moved into the house they had raised at "
                    + $"{site.Position} — {Clock.SeasonAndYear()}.");
                break;

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

            // ⭐ THE STORES, NAMED (D108). This was a `default:` arm, and it was two silent
            // defaults deep: an unrecognised kind fell through to `RaiseStore`, whose own two
            // switches then made it a market with a market's capacity. A building kind nobody
            // taught this method about would have quietly become a market.
            case BuildingKind.Granary:
            case BuildingKind.Shed:
            case BuildingKind.Market:
                RaiseStore(plan.Kind, site.Position, plan.Name);

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

            // A pile and a builder's hut are free and instant, so neither is ever a site and
            // neither can reach this method. Said out loud rather than swallowed.
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(site), plan.Kind, "That kind of building is never raised from a site.");
        }

        RetireWorkplace(site);

        Narrate($"{plan.Name} was finished. {Clock.SeasonAndYear()}.");
    }

    /// <summary>Put a store building into the world, with the capacity its kind derives.</summary>
    /// <remarks>
    /// <b>One place, because there are two ways a store can arrive now</b> — finished by a
    /// builder, or laid out instantly because it is a pile (D96). Two copies of the
    /// kind-to-capacity mapping is exactly how a store kind comes to have the wrong size in
    /// one of them and nobody notices for a phase; <c>StoreKind</c> has already taught this
    /// lesson five times (D76).
    /// </remarks>
    private StoreBuilding RaiseStore(BuildingKind kind, GridPos position, string name)
    {
        // Both switches name the market rather than defaulting to it (D108). They were the
        // second and third silent defaults on the path from `Complete`, and between them they
        // would have turned any building kind nobody had taught this method about into a
        // market with a market's capacity.
        StoreKind storeKind = kind switch
        {
            BuildingKind.Granary => StoreKind.Granary,
            BuildingKind.Shed => StoreKind.Shed,
            BuildingKind.Pile => StoreKind.Pile,
            BuildingKind.Market => StoreKind.Market,
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "That kind of building is not a store."),
        };

        int capacity = kind switch
        {
            BuildingKind.Granary => VillageEconomy.GranaryCapacity(Config),
            BuildingKind.Shed => VillageEconomy.ShedCapacity(Config),
            BuildingKind.Pile => VillageEconomy.PileCapacity(Config),
            BuildingKind.Market => VillageEconomy.MarketCapacity(Config),
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "That kind of building has no store capacity."),
        };

        var building = new StoreBuilding
        {
            Id = NextStoreId(),
            Kind = storeKind,
            Name = name,
            Position = position,
            Store = new Stockpile { Capacity = capacity },
        };

        StoreBuildings.Add(building);
        return building;
    }

    /// <summary>
    /// Take a workplace out of the world: let its workers go, and give up any ground it
    /// had been painted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One door, because there are three ways a workplace can end</b> — demolished with
    /// its building, cancelled mid-construction, and replaced when a site is finished — and
    /// each one used to write <c>ReleaseWorkers</c> then <c>Workplaces.Remove</c> by hand.
    /// D86 gives a workplace a second thing to release, and adding that line to three call
    /// sites is exactly how <c>StoreKind</c> reached five instalments (D76). <b>The next
    /// thing a workplace owns gets released here and nowhere else.</b>
    /// </para>
    /// <para>
    /// <b>Ground that is not given up is haunted:</b> land no other hut may be given,
    /// refused on behalf of a building that no longer exists, and the refusal cannot even
    /// name who holds it.
    /// </para>
    /// <para>
    /// ⚠️ <b>A finished construction site gets a NEW workplace id</b> (see
    /// <c>FinishConstruction</c>), so ground painted for the site does not follow the
    /// building it becomes. That is harmless today, because ground is only ever given to a
    /// standing building — and it is the first thing to check when the forester's hut lands.
    /// </para>
    /// </remarks>
    private void RetireWorkplace(Workplace workplace)
    {
        ReleaseWorkers(workplace);

        int freed = Zones.ReleaseWorkGround(workplace.Id);
        if (freed > 0)
        {
            Log(
                Logging.LogLevel.Info,
                "placement",
                $"{workplace.Name} is gone, and the {freed} tiles it kept are free again. "
                + $"{Clock.SeasonAndYear()}.");
        }

        Workplaces.Remove(workplace);
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
        BuildingKind.Pile => "a storage pile",
        BuildingKind.Home => "a house",
        BuildingKind.BuilderHut => "a builder's hut",

        // Named, because the default arm called every unrecognised building a woodcutter's
        // hut — in the log, in the panel, and in every placement sentence (D108).
        BuildingKind.WoodcutterHut => "a woodcutter's hut",
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind), kind, "That kind of building has no name."),
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
    /// <b>⭐ Taking somebody off a building does not lose them to the profession</b> (Joe,
    /// D109). Remove the worker from hut 2 and they move to hut 1 if hut 1 has room —
    /// <b>the global holds</b>, and it drops only when no building of that kind can take them.
    /// That is what makes the panel and the building two views of one number rather than two
    /// numbers that argue, and it is the behaviour a player expects when they are shuffling
    /// crews between two huts rather than laying anybody off.
    /// </para>
    /// <para>
    /// <b>Bounded by <see cref="Workplace.Capacity"/>, because a hut has a size.</b> Asking for
    /// more than fits is trimmed and reported rather than silently obeyed.
    /// </para>
    /// <para>
    /// The village does not re-plan on the spot: the change lands at the next labour
    /// pass, which is at worst a season away and immediately if a job has just fallen
    /// vacant (D47). Re-running the allocator from a UI click would make staffing the
    /// one decision in this game that stops the world.
    /// </para>
    /// </remarks>
    public PlacementVerdict SetStaffing(Workplace workplace, int places)
    {
        ArgumentNullException.ThrowIfNull(workplace);

        if (places < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(places), places, "A workplace cannot be staffed by fewer than nobody.");
        }

        if (workplace.IsSite)
        {
            throw new ArgumentException(
                $"{workplace.Name} is a construction site, and nobody is posted to one (D108).",
                nameof(workplace));
        }

        int wanted = places > workplace.Capacity ? workplace.Capacity : places;
        int freed = workplace.Staffing - wanted;
        workplace.Staffing = wanted;

        int rehoused = freed > 0 ? SpreadAcrossTheRest(workplace, freed) : 0;

        Narrate($"{workplace.Name} is to be worked by {wanted} "
            + $"{(wanted == 1 ? "person" : "people")} from now on. {Clock.SeasonAndYear()}.");

        if (rehoused > 0)
        {
            Narrate($"{rehoused} of them moved to other work of the same kind rather than "
                + $"leaving it — the village still has "
                + $"{ProfessionTotal(workplace.Kind)} on {Describe(workplace.Kind)}.");
        }

        return places > workplace.Capacity
            ? PlacementVerdict.Yes(
                $"{workplace.Name} only holds {workplace.Capacity}, so {places} cannot all work "
                + "there. Another building of the same kind would take the rest.")
            : PlacementVerdict.Fine;
    }

    /// <summary>
    /// Spread a profession's hands across its buildings, round-robin in id order. Says nothing.
    /// </summary>
    /// <returns>How many were actually placed, which is fewer than asked if there is no room.</returns>
    /// <remarks>
    /// <b>The rule on its own, with no sentence attached</b>, so that the two things which need
    /// it cannot drift apart: <see cref="SetProfession"/>, which is the player and therefore
    /// speaks, and the test harness's stand-in for a player, which must not fill the village
    /// log with a hundred years of its own clicking. Internal, because there is still no public
    /// way to put a named villager anywhere (D15).
    /// </remarks>
    internal int DistributeStaffing(JobKind kind, int wanted)
    {
        var of = new List<Workplace>();
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Kind == kind && !Workplaces[i].IsSite)
            {
                of.Add(Workplaces[i]);
            }
        }

        of.Sort(static (a, b) => a.Id.CompareTo(b.Id));

        for (int i = 0; i < of.Count; i++)
        {
            of[i].Staffing = 0;
        }

        int placed = 0;
        bool roomLeft = true;
        while (placed < wanted && roomLeft)
        {
            roomLeft = false;
            for (int i = 0; i < of.Count && placed < wanted; i++)
            {
                if (of[i].RoomToStaff > 0)
                {
                    of[i].Staffing++;
                    placed++;
                    roomLeft = true;
                }
            }
        }

        return placed;
    }

    /// <summary>Find room elsewhere in a profession for hands taken off one building.</summary>
    /// <returns>How many were re-housed; the rest leave the profession.</returns>
    private int SpreadAcrossTheRest(Workplace from, int hands)
    {
        var others = new List<Workplace>();
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Kind == from.Kind && !Workplaces[i].IsSite && Workplaces[i].Id != from.Id)
            {
                others.Add(Workplaces[i]);
            }
        }

        others.Sort(static (a, b) => a.Id.CompareTo(b.Id));

        int placed = 0;
        bool roomLeft = true;
        while (placed < hands && roomLeft)
        {
            roomLeft = false;
            for (int i = 0; i < others.Count && placed < hands; i++)
            {
                if (others[i].RoomToStaff > 0)
                {
                    others[i].Staffing++;
                    placed++;
                    roomLeft = true;
                }
            }
        }

        return placed;
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
                Kind = JobKind.Forester,
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

        // ⭐ THE BUILDER'S HUT, AND A WARM START HAS ONE (D108). Nothing is raised without
        // it, so a founding that ships with a granary, a shed and a market must ship with
        // the building that could have raised them — otherwise the fixture villages the
        // whole suite is written against could never build a house again.
        //
        // Its seats are DERIVED (VillageEconomy.BuilderHutCapacity), not typed;
        // `woodcutter_hut_capacity` is the recorded case where capacities were typed while
        // the yields around them moved, and thirty-six people froze (D50).
        Workplaces.Add(new Workplace
        {
            Id = nextWorkplaceId++,
            Kind = JobKind.Builder,
            Name = "the builder's hut",
            Position = Offset(origin, config.BuilderHutX, config.BuilderHutY),
            Capacity = VillageEconomy.BuilderHutCapacity(config),
            CatchmentRadius = catchment,
        });

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
    /// different question and it lives in <see cref="FoodTheVillageHasRoomFor"/>.
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
    /// Food worth gathering for the village store — the target, or what will fit,
    /// whichever is less.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The work question, as against the birth question above. <b>A village cannot want
    /// more food than it has somewhere to put</b>, and a forager standing at a full
    /// store should go and do something else — which is exactly the pressure capacity
    /// was added to create (spec §4: "a full shed means a producer has somewhere to
    /// stop").
    /// </para>
    /// <para>
    /// <b>Asked by capability, not by kind, and this was the fifth site of D76's bug.</b>
    /// It used to read <see cref="GranaryCapacity"/> — granaries only — while the amount
    /// it was compared against, <see cref="FoodInGranaries"/>, counts every store that
    /// takes food. <b>One comparison, two different questions</b>, and the cold start is
    /// the world where they part company: a village with a pile and a cart and no granary
    /// scored zero room, so <em>"does the village want more food?"</em> answered <b>no,
    /// forever</b>. Measured on Joe's opening before the fix: the village-wide reason to
    /// work fired on <b>0.0%</b> of gatherable ticks, against 99.9% once asked this way,
    /// while a pile stood beside them with room for eleven hundred.
    /// </para>
    /// <para>
    /// <b>What that cost is the whole of D81.</b> With no village-wide reason to work,
    /// the only reason left is a household's own larder — so the family whose larder
    /// filled first rested for a century while their neighbours did the work, which is
    /// precisely the failure <see cref="TargetFoodForTheGranary"/>'s own remarks predict
    /// two methods above: <em>"the founding woodcutters starved in year one while the
    /// other house sat on three hundred food and its foragers put their feet up."</em>
    /// </para>
    /// <para>
    /// <b>Room, not raw capacity.</b> A pile packed with logs genuinely has nowhere to
    /// stack berries — <see cref="Stockpile.Capacity"/> is total across every kind of
    /// goods — so this asks what the village could <em>end up</em> holding: the food
    /// already stored, plus the space left beside it.
    /// </para>
    /// <para>
    /// <b>The birth gate is deliberately untouched.</b> <see cref="GranaryCapacity"/>
    /// still answers by kind, because <em>how big may this village get?</em> is answered
    /// per granary on purpose (D33, D39) — "build another one" is the intended reply, and
    /// widening it here would have moved the population ceiling while fixing a labour bug.
    /// Two questions, two readers.
    /// </para>
    /// </remarks>
    public int FoodTheVillageHasRoomFor()
    {
        // Across every store the village can actually put food in (D76, D79) — the
        // granaries it has built, the pile the player dropped on day one, and the cart
        // they arrived in.
        int wanted = TargetFoodForTheGranary();
        int capacity = FoodInGranaries() + RoomLeftForFood();
        return wanted < capacity ? wanted : capacity;
    }

    /// <summary>Free space across every store that would take food.</summary>
    private int RoomLeftForFood()
    {
        int room = 0;
        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            if (StoreBuildings[i].Accepts(Goods.Food))
            {
                room += StoreBuildings[i].Store.FreeSpace;
            }
        }

        return room;
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
        // Food first, then the tools they carried — the order capacity binds in, stated
        // rather than implied by an argument list (Stockpile.Receive). Received rather than
        // added: the founders did not make any of it here.
        //
        // NO TIMBER, and it is a wagon that will not take any (D90 step 4). What the cart
        // holds is what you arrived in: your food and your tools.
        cart.Store.Receive(Goods.Food, config.CartFood);
        cart.Store.Receive(Goods.Tools, config.CartTools);
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
