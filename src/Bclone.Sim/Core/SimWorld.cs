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

    /// <summary>
    /// What the goods are — <b>the one place the sim asks</b> (`goods-catalog.md`, D210).
    /// </summary>
    /// <remarks>
    /// Built once from <see cref="Config"/> and never mutated, like the config itself. Everything
    /// that used to <c>switch</c> over <see cref="Goods"/> asks this instead.
    /// </remarks>
    public GoodsCatalog GoodsCatalog { get; }

    /// <summary>What the trades are — the one place the sim asks (`jobs-catalog.md`, D218).</summary>
    public JobsCatalog JobsCatalog { get; }

    /// <summary>What the techniques are — the one place the sim asks (`tech-tree.md`).</summary>
    public TechniquesCatalog TechniquesCatalog { get; }

    /// <summary>The libraries standing in the village, and what is written in them.</summary>
    /// <remarks>
    /// <b>Its own list, because a library is not a store, a workplace or a home</b> — see
    /// <see cref="Library"/>. Order is placement order and it is hashed, like every other list of
    /// buildings here.
    /// </remarks>
    public List<Library> Libraries { get; } = new();

    /// <summary>
    /// The tick the village's first granary began keeping count, or 0 if none ever has.
    /// </summary>
    /// <remarks>
    /// <b>Never cleared</b> — pulling the granary down does not unlearn writing, any more than
    /// burning a ledger un-teaches the person who kept it.
    /// </remarks>
    public ulong FirstGranaryTick { get; private set; }

    /// <summary>Whether the village has already been told it can write. An edge, said once.</summary>
    internal bool SaidTheyCanWrite { get; set; }

    /// <summary>
    /// Whether anybody here can write — <b>and it comes out of the granary</b> (D32, §7a).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ THE PLAYER DOES NOT SET OUT TO INVENT WRITING. THEY SET OUT NOT TO STARVE.</b>
    /// `tech-tree.md §7a`, Joe's own design: <em>"the granary is a staffed building whose job is
    /// counting. A keeper who has tallied stores for long enough begins marking the sacks with
    /// signs of her own devising. Tally marks → notation → letters."</em>
    /// </para>
    /// <para>
    /// <b>⛔ IT EXISTS BECAUSE THE LIBRARY WAS OFFERED FROM TICK ONE AND THAT READ AS WRONG</b>
    /// (Joe, from play): *"it feels too early for the library to be necessary… you just stabilised,
    /// now build a library? Maybe a smoother transition to writing."* **`buildings-plan.md §10`
    /// had said the same thing in a different vocabulary** — knowledge is step 8 of 11 — and the
    /// phase plan argued its way past that for the techniques and then shipped the *building*
    /// anyway. *He found it by playing in ten minutes.*
    /// </para>
    /// <para>
    /// <b>⭐ And it makes the storage branch feed the knowing branch</b>, which is §7a's structural
    /// point rather than its flavour: the tree stops being parallel columns and becomes a web.
    /// </para>
    /// </remarks>
    public bool HasLiteracy =>
        FirstGranaryTick > 0
        && Tick >= FirstGranaryTick + ((ulong)Config.LiteracyYears * (ulong)Config.TicksPerYear);

    /// <summary>Whether any standing library has a shelf free.</summary>
    public bool AnyShelfFree()
    {
        for (int i = 0; i < Libraries.Count; i++)
        {
            if (Libraries[i].HasRoom)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Write a technique down, if there is a shelf for it. Returns the library that took it.
    /// </summary>
    /// <remarks>
    /// <b>⭐ THE FIRST LIBRARY WITH ROOM, IN PLACEMENT ORDER</b> — stated so it cannot become an
    /// unordered tie (D15). It matters more than it looks: **which building holds a record is what
    /// a later slice's fire will take**, so *"whichever happened to be first in memory"* would make
    /// a fire's outcome unreproducible from the seed.
    /// </remarks>
    internal Library? WriteDown(int techniqueId)
    {
        for (int i = 0; i < Libraries.Count; i++)
        {
            if (Libraries[i].HasRoom)
            {
                Libraries[i].Records.Add(techniqueId);
                return Libraries[i];
            }
        }

        return null;
    }

    /// <summary>Pull down a library, and lose whatever was written in it.</summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THIS IS WHAT KEEPS `Established` FROM BEING A RATCHET IN THIS SLICE.</b> Fire is not in
    /// this phase (`phase-4-the-tech-tree.md §3`), so **demolition is the only way a record can be
    /// lost** — and without it, everything written down would be permanent and §11's *"the
    /// collections become a ratchet"* failure mode would arrive by the back door. A technique whose
    /// record is gone falls back to being worth exactly as many living knowers as it has.
    /// </para>
    /// <para>
    /// <b>⛔ It warns by saying what is being destroyed</b>, the way demolishing a full store does
    /// — the player is told what they are about to forget, not merely that a building is going.
    /// </para>
    /// </remarks>
    public void Demolish(Library library)
    {
        ArgumentNullException.ThrowIfNull(library);

        if (!Libraries.Remove(library))
        {
            throw new ArgumentException($"{library.Name} is not standing.", nameof(library));
        }

        if (library.Records.Count == 0)
        {
            Narrate($"{Capitalised(library.Name)} was pulled down. Nothing was written in it. "
                + $"{Clock.SeasonAndYear()}.");
            return;
        }

        var lost = new System.Text.StringBuilder();
        for (int i = 0; i < library.Records.Count; i++)
        {
            if (i > 0)
            {
                lost.Append(i == library.Records.Count - 1 ? " and " : ", ");
            }

            lost.Append(TechniquesCatalog[library.Records[i]].Name);
        }

        Narrate($"{Capitalised(library.Name)} was pulled down, and with it the village's record "
            + $"of {lost}. {Clock.SeasonAndYear()}.");
    }

    /// <summary>Whether a technique is written down anywhere that still stands.</summary>
    public bool IsWrittenDown(int techniqueId)
    {
        for (int i = 0; i < Libraries.Count; i++)
        {
            if (Libraries[i].Records.Contains(techniqueId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// What the village knows, one state per technique, indexed by technique id.
    /// </summary>
    /// <remarks>
    /// <b>⚠️ REDUNDANT BY CONSTRUCTION, AND STORED ANYWAY — FOR THE EDGE.</b>
    /// <see cref="Systems.KnowledgeSystem"/> recomputes it every tick from who is alive, so it can
    /// never disagree with the village's own people. **What it buys is the transition**: the log
    /// has to say *"the village learned this"* and *"this went with her"*, and a sentence on an
    /// edge needs to know what was true a tick ago.
    /// <b>It is hashed</b>, because <see cref="YieldWithTechnique"/> reads it — and state the sim
    /// reads that the hash cannot see is two runs that read identical and are not.
    /// </remarks>
    public KnowledgeState[] KnowledgeStates { get; }

    /// <summary>
    /// The villager id who last held each technique, so the village can name them when it is lost.
    /// </summary>
    /// <remarks>
    /// <b>An id rather than a name</b>, because a name is a string and this is hashed state. The
    /// dead stay in <see cref="Villagers"/> with <c>Alive</c> false, so the name is still there to
    /// be looked up on the tick it is needed.
    /// </remarks>
    public int[] LastKnowerIds { get; }

    /// <summary>What the buildings are — the one place the sim asks (`buildings-catalog.md`).</summary>
    /// <remarks>
    /// <b>The last of the four enums D168 named</b> (<c>Goods</c>, <c>JobKind</c>,
    /// <c>BuildingKind</c>, <c>Terrain</c>) to get a row behind it. Everything that used to
    /// <c>switch</c> over a building — its name, its cost, the store it becomes, the trade worked
    /// there, its ring, its buffer, how many live in it — asks this instead.
    /// </remarks>
    public BuildingsCatalog BuildingsCatalog { get; }

    /// <summary>
    /// A stockpile with a slot for every good <em>this run</em> has (D210, slice 1b).
    /// </summary>
    /// <remarks>
    /// <b>⭐ One place, so a new kind of store cannot be born the wrong size.</b> Every larder,
    /// buffer and cart in the village is sized from the same catalogue — which is what stops a
    /// village holding a good its own state hash never mixes, a determinism bug that would surface
    /// as an unreproducible run rather than as an error.
    /// </remarks>
    internal Stockpile NewStockpile() => new(GoodsCatalog.Count);

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
    public StockLimits StockLimits { get; }

    /// <summary>How many people the player wants on each kind of work (D106).</summary>
    /// <remarks>
    /// Banished's professions panel. <b>The player always has an opinion</b> — every
    /// profession carries an explicit number from the first frame (D136), and since
    /// 2026-08-16 there is no *"village decides"* anywhere in the game to hand one back to.
    /// See <see cref="JobLimits"/> for why this is allowed to ask for <em>more</em> than the
    /// village would choose, where a stock limit may only ask for less.
    /// </remarks>
    public JobLimits JobLimits { get; } = new();

    /// <summary>
    /// Set or clear how many people should be on a kind of work, and say what it will cost.
    /// </summary>
    /// <remarks>
    /// <b>Always obeyed, and warned about when it takes hands off something that keeps people
    /// alive</b> — D62's shape exactly. A game that refuses the player's number is arguing with
    /// them; one that obeys silently has killed them without saying so. The warning fires once,
    /// when the number is set, rather than every tick the village is short (D42's rule about
    /// the distance warning).
    /// </remarks>
    public PlacementVerdict SetJobLimit(JobKind kind, int? target)
    {
        if (!JobLimits.Set(kind, target))
        {
            return PlacementVerdict.Fine;
        }

        if (target is not int asked)
        {
            Log(Logging.LogLevel.Info, "labour",
                $"{Describe(kind)} is left to the village again. {Clock.SeasonAndYear()}.");
            return PlacementVerdict.Fine;
        }

        int seats = 0;
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Kind == kind)
            {
                seats += Workplaces[i].Capacity;
            }
        }

        Log(Logging.LogLevel.Info, "labour",
            $"You asked for {asked} on {Describe(kind)}. {Clock.SeasonAndYear()}.");

        if (asked > seats)
        {
            return PlacementVerdict.Yes(
                $"There is only room for {seats} on {Describe(kind)}, so {asked} cannot all be "
                + "put to work. Build somewhere for them first.");
        }

        // The two that kill people if nobody does them (D45: hunger in six days, an unheated
        // house in twenty-five). Said plainly rather than refused.
        if (asked == 0 && kind is JobKind.Forager or JobKind.Woodcutter)
        {
            return PlacementVerdict.Yes(
                $"Nobody will be put on {Describe(kind)} at all. The village will live on what "
                + "it has already put away.");
        }

        return PlacementVerdict.Fine;
    }

    // ⭐ Six arms naming six trades; the gerund is a column on the row now (D218). The old
    // default arm — `kind.ToString().ToLowerInvariant()` — is gone with them: a trade that
    // reaches here has a row, because the validator refuses a catalogue missing a built-in id.
    private string Describe(JobKind kind) => JobsCatalog.DoingOf(kind);

    /// <summary>
    /// Everyone old enough and well enough to hold a job — the number
    /// <see cref="Laborers"/> is the remainder of.
    /// </summary>
    /// <remarks>
    /// <b>Beside <see cref="Laborers"/> on purpose (D148).</b> The professions panel showed
    /// *"Laborer 1"* to a player with four villagers who had assigned all four, and there was
    /// no figure anywhere saying what the 1 was one *of*. Keeping the two definitions adjacent
    /// is what stops them drifting into disagreeing about who counts.
    /// </remarks>
    public int AbleAdults
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Villagers.Count; i++)
            {
                if (Villagers[i].Alive && Villagers[i].CanWork)
                {
                    count++;
                }
            }

            return count;
        }
    }

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

    // `world.FoodSource` and `world.TreeStand` went in `forests-and-gathering.md` slice 5.
    // They were Phase 0's single berry patch and single stand of trees, and they outlived the
    // plural lists that replaced them by five phases — a yield-per-gather and a
    // yield-per-cut, read from one place while the village worked eight others.
    //
    // ⚠️ THE FIELDS WENT THERE; THE CLASSES DID NOT, AND THIS COMMENT SAID OTHERWISE FOR A
    // MONTH (D159). `TreeStand` survived holding nothing at all and `FoodSource` survived
    // holding one static predicate, which is now `SeasonRules.IsGatherable`. Both are gone.

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
    /// <remarks>
    /// <b>⚠️ Literally what it says, and that is why it is no longer what decisions read.</b>
    /// It counts <see cref="StoreBuildings"/> and nothing else. Use it for the panel, where the
    /// player is being told what is in the buildings; use <see cref="FoodTheVillageHolds"/> for
    /// any question of the form *does the village have enough food?*
    /// </remarks>
    public int FoodInGranaries() => TotalAccepting(Goods.Food, static store => store.Food);

    /// <summary>
    /// ⭐ All the food the village is holding — its stores <em>and</em> its workplaces (D161).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This exists because <see cref="Workplace.Store"/> is about to be written to for the
    /// first time.</b> It has been on the type since D30 and nothing has ever put anything in
    /// it, so <see cref="FoodInGranaries"/> and <see cref="TotalFood"/> have never once
    /// disagreed. The farm fills a local store (`crops-and-orchards.md §3.1`), and on that day
    /// they diverge by up to a hundred food per farm — with the **birth gate**, the village-wide
    /// reason to gather, and the food stock limit all reading the blind one.
    /// </para>
    /// <para>
    /// <b>The failure it prevents is a village that quietly stops having children</b> because
    /// its food is in the wrong building — D155's symptom from a new direction, and structurally
    /// D81's bug: one comparison asking two different questions. D81 is recorded as *D76's seam
    /// for the fifth time*. **Found by writing the spec, before the farm existed.**
    /// </para>
    /// <para>
    /// <b>⚠️ Larders are deliberately excluded, and that boundary is load-bearing.</b> This sits
    /// between the two readers that already exist: wider than the granaries, narrower than
    /// <see cref="TotalFood"/>. A household's larder is food already *distributed* — counting it
    /// here would re-add the household term D153 deliberately removed from the birth gate.
    /// </para>
    /// </remarks>
    public int FoodTheVillageHolds()
    {
        int total = FoodInGranaries();

        for (int i = 0; i < Workplaces.Count; i++)
        {
            total += Workplaces[i].Store.Food;
        }

        return total;
    }

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

    /// <summary>
    /// How much of any good the village's stores hold between them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The general form of the three named totals above</b>, added for the overview panel,
    /// which lists every value of <see cref="Goods"/> rather than the three the economy
    /// happens to be derived against. Driving that panel off the enum is what makes a new
    /// good appear the day it is added instead of the day somebody remembers the panel.
    /// </para>
    /// <para>
    /// <b>The named three stay</b>, and are not thin wrappers over this one: each carries a
    /// hard-won argument about <em>which</em> stores count (D79's firewood, above), and
    /// collapsing them into a general reader is how those arguments get lost. This answers a
    /// different question — <em>what is standing in the stores?</em> — and callers that mean
    /// one of the other three should keep saying so.
    /// </para>
    /// </remarks>
    public int InStores(Goods goods) => TotalAccepting(goods, store => store[goods]);

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

    /// <summary>How much of one good is lying in heaps across the valley.</summary>
    /// <remarks>
    /// <b>D134: the player could not see this, and it was the whole answer to a bug they could
    /// see.</b> Joe: <i>"it still feels wrong — there's never enough wood but so many trees are
    /// harvested."</i> Measured on his shape of village: **320 logs in store and 5,977 lying on
    /// the ground**, because a valley has one timber store, it fills, and every load after that
    /// is set down outside it and then correctly refused for pickup — there is still nowhere to
    /// put it. The Overview read "Logs 320" and said nothing about the mountain in the yard.
    /// A village drowning in timber that reports a shortage is a §1.1 failure, not a balance
    /// one, so the number exists to be shown.
    /// </remarks>
    public int OnTheGround(Goods goods)
    {
        int total = 0;
        for (int i = 0; i < GroundStacks.Count; i++)
        {
            if (GroundStacks[i].Goods == goods)
            {
                total += GroundStacks[i].Amount;
            }
        }

        return total;
    }

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
        // ⭐ IT IS THE SAME METHOD AS THE FINISHED PATH NOW, WHICH IS WHAT THIS METHOD'S OWN
        // REMARKS HAVE ASKED FOR SINCE D108: *"one place, so the two ways a free building can
        // arrive cannot disagree about what it becomes."* It was two hand-written arms — a
        // stockpile became a store, a builder's hut became a workplace — and the row says which
        // without either being written down twice.
        //
        // ⚠️ Free-ness itself is not a column: `Mark` asks the recipe (D108), and a row that costs
        // nothing and owes no work is the whole of it.
        RaiseFinished(kind, position, name);
    }

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

    /// <summary>Bumped whenever any tile changes, so cached counts of terrain know they are stale.</summary>
    /// <remarks>
    /// <b>Not hashed, and it must not be.</b> It is bookkeeping about a cache, not a fact about
    /// the village — two runs of one seed that cleared the same tiles in the same order would
    /// agree on it anyway, and two that did not have already diverged somewhere that IS hashed.
    /// </remarks>
    private int _terrainGeneration;

    /// <summary>
    /// How many times the ground has changed. <b>For cache invalidation only.</b>
    /// </summary>
    /// <remarks>
    /// Exposed so the view can bake the valley into a texture once instead of redrawing
    /// nine thousand tiles a frame, and so it asks <em>the same</em> question the hut rings
    /// ask rather than inventing a second way to notice a felled tree. Never a source of
    /// truth about anything — see the field's own note for why it is not hashed.
    /// </remarks>
    public int TerrainGeneration => _terrainGeneration;

    /// <summary>
    /// Wooded tiles inside a workplace's ring — what its trips are worth
    /// (`specs/forests-and-gathering.md`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Counted once per terrain change, not once per gather.</b> A ring of radius eight is a
    /// hundred and forty-five tiles and this is asked by every gatherer on every trip; D87 is
    /// the recorded case of what that costs. The count is kept on the workplace and thrown away
    /// by <see cref="SetTerrain"/>.
    /// </para>
    /// <para>
    /// <b>Water and rock count against the hut, and that is the design.</b> The denominator is
    /// the whole ring, so a hut with the river through its ground genuinely feeds fewer people —
    /// a real, visible trade-off about where you site it, rather than a hidden exemption.
    /// </para>
    /// </remarks>
    public int WoodedTilesAround(Workplace workplace)
    {
        ArgumentNullException.ThrowIfNull(workplace);

        if (workplace.GatheringRadius <= 0)
        {
            return 0;
        }

        if (workplace.CachedAtTerrainGeneration == _terrainGeneration)
        {
            return workplace.CachedWoodedTiles;
        }

        int radius = workplace.GatheringRadius;
        int wooded = 0;

        for (int dy = -radius; dy <= radius; dy++)
        {
            int span = radius - Math.Abs(dy);
            for (int dx = -span; dx <= span; dx++)
            {
                var at = new GridPos(workplace.Position.X + dx, workplace.Position.Y + dy);
                if (Map.Contains(at) && Map.TerrainAt(at) == Terrain.Forest)
                {
                    wooded++;
                }
            }
        }

        workplace.CachedWoodedTiles = wooded;
        workplace.CachedAtTerrainGeneration = _terrainGeneration;
        return wooded;
    }

    /// <summary>
    /// What one gathering trip at this place is worth, before vigour.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ Linear in the wooded fraction of the ring, and with no floor</b> (Joe:
    /// *"less trees = less food available to gather"*). *"Half the trees, half the food"* is a
    /// sentence a player can hold in their head while deciding whether to fell the wood beside
    /// their hut, which is the whole point of the mechanic (§1.6). A floor would be kinder and
    /// would make **no forest, no food** untrue, which is the rule Joe asked for by name.
    /// </para>
    /// <para>
    /// <b>Zero trees yields zero food.</b> The safety is that it is <em>visible</em> — the hut's
    /// panel says what its ring holds and what that is worth — not that it is softened.
    /// </para>
    /// <para>
    /// <b>Every gathering place has a ring now</b> (slice 5). The arm for one that did not was
    /// the berry patch, which yielded a flat <c>gather_yield</c> wherever it stood; with the
    /// patches retired there is nothing left that gathers without a ring, so a radius of zero
    /// is a hut with no reach and correctly worth nothing.
    /// </para>
    /// </remarks>
    public int GatherYieldAt(Workplace workplace)
    {
        ArgumentNullException.ThrowIfNull(workplace);

        int ring = VillageEconomy.TilesInRing(workplace.GatheringRadius);
        int perTrip = ring <= 0 ? 0 : Config.GatherYield * WoodedTilesAround(workplace) / ring;

        // Tended patches, if anybody alive knows the woods that well (Phase 4). Applied here so
        // the panel and the sim quote the same number: the hut says what a trip is worth, and a
        // village that knows the technique is told the truth about it.
        return YieldWithTechnique(JobKind.Forager, perTrip);
    }

    /// <summary>
    /// What one tile of crop is worth <b>on this ground</b> — the farm's half of per-site
    /// yield (`specs/per-site-yield.md §4.1`, D178).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE SIBLING OF <see cref="GatherYieldAt"/>, AND DELIBERATELY THE SAME SHAPE.</b> A
    /// gatherer's hut has been worth what the trees around it are worth since D112; this is the
    /// farm finally getting the same treatment, on the axis a field actually varies by. Two
    /// buildings, one idea: **what a site produces depends on where it is.**
    /// </para>
    /// <para>
    /// <b>⛔ <c>crop_yield_per_tile</c> IS LOCKED AND THIS DOES NOT TOUCH IT.</b> Soil is a
    /// multiplier <em>around</em> <see cref="VillageEconomy.ReferenceSoil"/>, so a field on
    /// average ground yields exactly what it yielded before this existed. The locked number is
    /// unchanged and now means something precise: **the yield on average ground.**
    /// </para>
    /// <para>
    /// <b>Never zero.</b> Poor ground is poor, not barren — a farm the player sited badly should
    /// disappoint them, not fail silently and look broken. That is the same call `GatherYieldAt`
    /// deliberately made the *other* way, and the difference is the point: **a bald ring has no
    /// trees in it, while thin soil still grows something.**
    /// </para>
    /// </remarks>
    public int CropYieldAt(GridPos tile)
    {
        int reference = VillageEconomy.ReferenceSoil(Config);
        if (reference <= 0)
        {
            return Config.CropYieldPerTile;
        }

        int yield = Config.CropYieldPerTile * Map.SoilAt(tile) / reference;

        // Crop rotation, if anybody alive knows to rest a field (Phase 4). DESIGN.md 2.7 own
        // worked example, arriving as content at last. It is applied to the per-tile figure, so
        // the soil overlay and the harvest agree about what a field is worth.
        yield = YieldWithTechnique(JobKind.Farmer, yield);
        return yield < 1 ? 1 : yield;
    }

    /// <summary>
    /// What one tile of this ground is worth against ordinary ground, as a percentage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE NUMBER BEHIND THE SENTENCE THE PLAYER READS.</b> Joe, walking the shipped
    /// build: <em>"I can't really tell which areas are good or bad."</em> Until this existed
    /// the soil overlay's wash was the <b>only</b> channel the game had for saying so — no
    /// panel, no log line, no sentence anywhere stated a tile's soil in words — and a wash is
    /// a thing you compare, not a thing you read.
    /// </para>
    /// <para>
    /// <b>Derived from <see cref="CropYieldAt"/> rather than from <c>SoilAt</c> directly</b>,
    /// so the number the player is shown is the number the farm actually reaps — the never-zero
    /// floor included. A panel that quoted the raw soil byte could disagree with the harvest,
    /// and D147's rule is that the marker and the panel must not be able to.
    /// </para>
    /// <para>
    /// 100 is ordinary — <c>crop_yield_per_tile</c> is locked and means <em>the yield on
    /// average ground</em> (D178).
    /// </para>
    /// </remarks>
    public int SoilShareAt(GridPos tile) =>
        Config.CropYieldPerTile <= 0 ? 100 : CropYieldAt(tile) * 100 / Config.CropYieldPerTile;

    /// <summary>
    /// What a farm's own ground is worth against ordinary, averaged over the tiles it holds —
    /// or 0 if it has been given none.
    /// </summary>
    /// <remarks>
    /// <b>Averaged over what it actually works</b>, not sampled at the farmhouse: soil is
    /// regional at lattice 8 (`per-site-yield.md §3.1`) and a farm's ground can straddle two
    /// regions, so the doorstep tile is not the answer to <em>"is this a good farm?"</em>.
    /// Reads <see cref="ZoneMap.WorkGroundOf"/>, which is already indexed by owner, rather
    /// than walking the valley.
    /// </remarks>
    public int FarmGroundShare(Workplace workplace)
    {
        ArgumentNullException.ThrowIfNull(workplace);

        IReadOnlyList<int> ground = Zones.WorkGroundOf(workplace.Id);
        if (ground.Count == 0)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < ground.Count; i++)
        {
            total += SoilShareAt(Zones.PositionOf(ground[i]));
        }

        return total / ground.Count;
    }

    /// <summary>
    /// A tile of this workplace's own ground for it to work next, or null if there is none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One question, two answers, decided by <see cref="MayFell"/></b>
    /// (`forests-and-gathering.md`): a forester that may fell wants a wooded tile of its ground
    /// while any is standing, and one that may not wants a bare one to put a tree back on.
    /// Written as one method because they are the same walk to the same ground for the same
    /// person — two methods would be two places to teach about a third mode.
    /// </para>
    /// <para>
    /// <b>Nearest by travel cost, from where the worker stands</b> — the same rule every other
    /// errand in this game uses, off the one shared cost field (§2.6), so a forester works
    /// outward from where they are rather than criss-crossing their own wood.
    /// </para>
    /// <para>
    /// <b>Walks the owned tiles, never the valley.</b> `ZoneMap` keeps a per-owner list, so a
    /// hut with forty tiles looks at forty — not at 9,600, which is the mistake
    /// <see cref="NearestHarvest(GridPos)"/> had to be rescued from when the valley became wooded.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Whether this workplace is allowed to take a tree down at this moment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE ONE DOOR (D146), and it exists because D145 had just finished writing down why
    /// controls need one.</b> Two separate things can stop a forester felling — the player's
    /// per-hut toggle, and a met Logs limit — and they are the same instruction seen from two
    /// distances: <em>stop taking timber out</em>. Answering them in one place is what stops the
    /// tile-picker, the action's duration and its outcome from ever disagreeing about which job
    /// is being done, which is the exact bug D142 spent a session on.
    /// </para>
    /// <para>
    /// <b>Planting is not gated by either of them</b> (Joe): <i>"a capped hut can replant.
    /// Priority should be replant → extra-hands labour. It just shouldn't fell if it has met its
    /// cap."</i> So a hut that may not fell is not idle — it puts its ground back, and only
    /// falls through to spare work once there is nothing bare left to plant.
    /// </para>
    /// </remarks>
    public bool MayFell(Workplace workplace)
    {
        ArgumentNullException.ThrowIfNull(workplace);

        return workplace.Mode == WorkMode.FellAndPlant && MayTake(Goods.Logs);
    }

    /// <summary>
    /// Whether the village still wants more of a good — <b>the limit's one door</b> (D212).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ D146 cut this door for the forester and for nobody else.</b> <see cref="MayFell"/>
    /// asked the Logs limit by name, so the <em>hut</em> obeyed the player and the
    /// <em>harvest brush</em> did not — and the brush is the only source of stone and iron the
    /// game has. A player could type <em>"keep 100 stone"</em>, watch every seam in the valley
    /// come out of the ground, and get no sentence explaining it.
    /// </para>
    /// <para>
    /// <b>⚠️ It reads <see cref="InStores"/> for every good, and that is not a widening of what
    /// <see cref="MayFell"/> counted.</b> `stock-limits-and-laborers.md §4.1` is emphatic that a
    /// limit must read the same supply the good's demand function reads — D29 froze a village to
    /// extinction on the other reading. <c>InStores(Goods.Logs)</c> and <see cref="LogsInSheds"/>
    /// are the same sum over the same stores (<c>store.Logs</c> <em>is</em>
    /// <c>store[Goods.Logs]</c>), so the forester's question is unchanged to the byte. The spec's
    /// own answer for a good with no demand function yet — stone, iron, tools — is <em>"the limit
    /// reads village stores"</em>, which is this.
    /// </para>
    /// <para>
    /// <b>⛔ Nothing switches on a good by name here</b>, which is `goods-catalog.md §2.1`'s rule
    /// and the reason a mod-added good is limited the day it is added.
    /// </para>
    /// </remarks>
    public bool MayTake(Goods goods) => !StockLimits.IsMet(goods, InStores(goods));

    /// <summary>
    /// Why this workplace cannot do its job right now, or <c>null</c> if it can.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE MAP MARKER'S RULE, IN ONE PLACE (Joe, D147).</b> <i>"Idle huts should get an
    /// indicator like full storage buildings."</i> D140 put an amber ring on a store with no
    /// room; this is the same idea for a building that is not working — and it earns its place
    /// because <b>three separate times this session a building looked idle for a reason set on
    /// a different panel</b> (a log limit, a firewood limit, ground nobody ever painted). §1.1
    /// is the whole argument: the player cannot answer a problem they cannot see.
    /// </para>
    /// <para>
    /// <b>⚠️ "IDLE" IS NOT ONE FACT THE WAY "FULL" IS, AND THAT IS WHAT THIS METHOD IS FOR.</b>
    /// A store is full or it is not. A workplace has half a dozen reasons for having nothing to
    /// do and most of them are fine — so a marker that lit up for all of them would be
    /// wallpaper, which is exactly what D42 and D123 moved <em>out</em> of the Overview. The
    /// rule is therefore narrow: <b>this building cannot do its job, and the fix is the
    /// player's.</b>
    /// </para>
    /// <list type="bullet">
    /// <item><b>Not flagged: a hut the player emptied on purpose.</b> <c>StaffingOverride == 0</c>
    /// is an instruction, not a fault, and D42's rule is that an instruction is obeyed rather
    /// than argued with. This is why the null-versus-zero distinction D136 fought for matters
    /// here as well.</item>
    /// <item><b>Not flagged: a gatherer in winter.</b> Seasonal, expected, and nothing the
    /// player can do about it — the definition of a marker that teaches people to ignore
    /// markers.</item>
    /// <item><b>Not flagged: a forester that is replanting.</b> It is working (D146).</item>
    /// <item><b>Flagged: a met stock limit.</b> The one case where "the player already knows"
    /// is <em>not</em> a good enough answer, because the number lives on another panel
    /// entirely — which is the finding that made this feature worth building.</item>
    /// </list>
    /// <para>
    /// A sentence rather than a flag, so the panel and the marker cannot drift apart, and so
    /// every reason has to be sayable before it is allowed to light anything up.
    /// </para>
    /// </remarks>
    public string? IdleNote(Workplace workplace)
    {
        ArgumentNullException.ThrowIfNull(workplace);

        // A site is not a workplace yet; the build queue is where it explains itself.
        if (workplace.IsSite)
        {
            return null;
        }

        // "Nobody here" is an instruction when the player typed it (D136's null-versus-zero).
        if (workplace.StaffingOverride == 0)
        {
            return null;
        }

        if (workplace.WorkerIds.Count == 0)
        {
            return $"Nobody is working {workplace.Name}.";
        }

        return workplace.Kind switch
        {
            JobKind.Forester => ForesterIdleNote(workplace),
            JobKind.Woodcutter => WoodcutterIdleNote(workplace),
            JobKind.Forager => ForagerIdleNote(workplace),
            JobKind.Farmer => FarmIdleNote(workplace),

            // Builders and marketers are idle whenever the village has nothing to build or
            // move, which is the ordinary state of a settled village rather than a fault.
            _ => null,
        };
    }

    /// <summary>Why a farm has nothing to do, when that is something the player can fix.</summary>
    /// <remarks>
    /// <para>
    /// <b>D147's rule, and the design work is all in what does NOT light up.</b> A workplace
    /// has half a dozen reasons for having nothing to do and most of them are fine; a marker
    /// that fired for all of them would be the always-on alert D42 and D123 deleted. So:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Not flagged: summer and winter.</b> Seasonal, expected and unfixable — the
    /// definition of a marker that teaches people to ignore markers, exactly as a gatherer in
    /// winter is not flagged.</item>
    /// <item><b>Flagged: no ground.</b> A farmhouse with nothing painted for it is a building
    /// that cannot do its job, and the fix is one brush stroke away.</item>
    /// <item><b>⭐ Flagged: a standing harvest nobody is reaping.</b>
    /// `crops-and-orchards.md §5.1` makes this the load-bearing half of use-it-or-lose-it —
    /// the loss is only fair if it could be seen coming, and it has to be sayable
    /// <em>while it can still be acted on</em>.</item>
    /// <item><b>⭐ Flagged: a met food limit.</b> D147's own finding — the one case where
    /// *"the player already knows"* is not good enough, because the number lives two windows
    /// away from the building that stopped.</item>
    /// </list>
    /// </remarks>
    private string? FarmIdleNote(Workplace farm)
    {
        if (Zones.WorkGroundTiles(farm.Id) == 0)
        {
            return $"{farm.Name} has no ground to work — paint some for it.";
        }

        // Still a tile to sow or a tile to reap: it is working.
        if (NextFieldToWork(farm, farm.Position) is not null)
        {
            return null;
        }

        // ⭐ THE MET LIMIT, AND IT IS D147'S OWN FINDING: the one case where "the player
        // already knows" is not good enough, because the number lives two windows away from
        // the building that stopped. Said only in the season it actually stops anything —
        // sowing is spring's work, and a farm idle in July is idle because it is July.
        if (SeasonRules.IsSowing(Clock.Season) && !MaySow())
        {
            return $"{farm.Name} has stopped sowing — you asked the village to keep "
                + $"{StockLimits.For(Goods.Food)} food and it has {FoodTheVillageHolds()}.";
        }

        // ⚠️ A HARVEST STANDING IN AUTUMN WITH NOBODY TAKING IT. The one sentence in this
        // method that is about a loss rather than about idleness, and it is the reason the
        // method is worth having: winter takes what is left, and this is the last window in
        // which the player can do anything about it.
        if (SeasonRules.IsReaping(Clock.Season) && StandingCropTiles(farm) > 0)
        {
            return $"{farm.Name} has a crop standing and nobody reaping it — winter will "
                + "take what is left.";
        }

        // Summer and winter are not faults, and neither is a farm that has finished.
        return null;
    }

    /// <summary>
    /// Tiles of crop this farm can actually reap in one autumn — <b>what it may commit in
    /// spring</b>.
    /// </summary>
    /// <remarks>
    /// <b>The hands that are there, not the seats that exist</b>, exactly as
    /// <see cref="WorkGroundAllowanceFor"/> does it: a farm that loses somebody in summer
    /// should commit less ground the following spring, and a farm nobody works should commit
    /// none. <b>Floored at one where anybody is standing in it</b>, so a single farmer is never
    /// told their field is too big to start.
    /// <para>
    /// <b>⭐⭐ AND IT ASKS *THIS* FARM'S WALK, NOT THE AVERAGE FARM'S</b> (D178,
    /// `per-site-yield.md §4.2`). <see cref="VillageEconomy.FieldTilesOneFarmerKeeps"/> charges
    /// *"a round trip to the steading"* in its own words and is **one number for every farm in
    /// the valley** — so a farm ten ticks from the store it actually hauls to was committing
    /// ground a farm next door could reap, and rotting the difference. Measured over ten years
    /// (D171): **93–96% brought in next door, 46% at ten ticks, 25% at twenty-two.**
    /// </para>
    /// <para>
    /// <b>The derivation keeps its meaning — <em>what a well-sited farm manages</em> — and a
    /// badly-sited one now commits less</b> rather than committing the same and losing it to
    /// winter. ⭐ **That is what makes the rot line honest**, which is the whole of D167's
    /// argument: rot meant *you over-painted* or *you lost a farmer*, and **distance was a third
    /// cause the game could not say.** A rot line the player cannot act on is weather.
    /// </para>
    /// <para>
    /// ⛔ <b><c>farm_store_cap</c> is not the lever and is untouched</b> — measured at nought to
    /// seven points across one armful against thirteen (D171).
    /// </para>
    /// </remarks>
    public int HarvestOneFarmCanBringIn(Workplace farm)
    {
        ArgumentNullException.ThrowIfNull(farm);

        int hands = farm.WorkerIds.Count;
        int tiles = hands * FieldTilesThisFarmCommitsPerHand(farm);

        return hands > 0 && tiles < 1 ? 1 : tiles;
    }

    /// <summary>
    /// Tiles <b>one pair of hands</b> at this farm commits next spring — <b>what it has learned,
    /// not what anybody predicted</b> (`per-site-yield.md §4.2a`, D194).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ A farm tries one more tile every year until it cannot bring the crop in, then
    /// settles one back and stays there.</b> That sentence is the whole mechanism, and it is
    /// deliberately something a player could be told.
    /// </para>
    /// <para>
    /// <b>⛔ WHAT IT REPLACED WAS SELF-FULFILLING.</b> <see cref="ReapableShareAt"/> cut a
    /// distant farm's field, the farmer then had nothing left to do, and the idleness read back
    /// as proof the field had been too big — <b>27% of the autumn resting at ten ticks out, 45%
    /// at sixteen, 55% at twenty-two</b>, while the farm could in fact bring in one to two more
    /// tiles at every distance. See <see cref="Workplace.FieldTilesLearned"/> for why no formula
    /// replaces it.
    /// </para>
    /// <para>
    /// <b>⛔ NEVER PAST <see cref="VillageEconomy.FieldTilesOneFarmerKeeps"/>.</b> That is the
    /// survival floor the whole economy is solved against (D16, D189), and a well-sited farm's
    /// measured physical ceiling is <b>21</b> tiles against the derivation's <b>13</b>. Thirteen
    /// wins: a memory allowed to climb past it would inflate a derived, locked number from the
    /// far end, which is exactly the move D189 refused for <c>crop_yield_per_tile</c>.
    /// </para>
    /// <para>
    /// <b>The opening guess is the old prediction</b>, so a brand-new farm commits exactly what
    /// it commits today and then learns. <i>Nobody is ever worse than today.</i>
    /// </para>
    /// </remarks>
    public int FieldTilesThisFarmCommitsPerHand(Workplace farm)
    {
        ArgumentNullException.ThrowIfNull(farm);

        int derived = VillageEconomy.FieldTilesOneFarmerKeeps(Config);

        // ⭐ THE WALK CHANGED, SO THE ANSWER DID. A farm that learned its limit at ten ticks
        // out was answering a question about a walk; a granary built beside the fields makes
        // that answer stale, and the farm re-reckons from a fresh guess rather than insisting
        // on a number the valley no longer supports. Checked here rather than on a store being
        // built, because there are four ways the walk can move — a store raised, demolished,
        // filled, or told to stop taking food — and one door beats four notifications (D142).
        int walk = HaulWalkFor(farm);
        if (farm.FieldTilesLearned > 0 && farm.FieldWalkWhenLearned != walk)
        {
            // Take the better of what it knows and what the fresh walk suggests, and let it
            // climb again. A shorter walk raises the guess and the farm jumps to it rather
            // than crawling up a tile a year; a longer one keeps the record and lets the next
            // autumn settle it down honestly.
            farm.FieldTilesLearned =
                Math.Max(farm.FieldTilesLearned, OpeningGuessFor(farm, derived));
            farm.FieldWalkWhenLearned = walk;
        }

        if (farm.FieldTilesLearned <= 0)
        {
            return OpeningGuessFor(farm, derived);
        }

        return farm.FieldTilesLearned > derived ? derived : farm.FieldTilesLearned;
    }

    /// <summary>What a farm with no history commits — <b>the old prediction, demoted</b>.</summary>
    private int OpeningGuessFor(Workplace farm, int derived)
    {
        int guess = derived * ReapableShareAt(farm) / 100;
        return guess < 1 ? 1 : guess > derived ? derived : guess;
    }

    /// <summary>
    /// The walk this farm's harvest actually makes — <b>to the nearest store that takes
    /// food</b>, or -1 where there is none.
    /// </summary>
    /// <remarks>
    /// <b>One place asks it, and three read the answer</b> — the memory's staleness check, the
    /// opening guess, and the placement warning (§4.3). Two copies of one walk is how they come
    /// to disagree (D142's three call sites, D148's two meanings).
    /// </remarks>
    public int HaulWalkFor(Workplace farm)
    {
        ArgumentNullException.ThrowIfNull(farm);

        return HaulWalkFrom(farm.Position);
    }

    /// <summary>
    /// ⭐⭐ How many homes a market on this tile would be <b>the nearest food store for</b> — its
    /// service area, stated truthfully (D201, Joe).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe asked to see the market's service radius before placing it.</b> ⛔ <b>There is no
    /// radius, and drawing one would be drawing a lie</b> — a marketer picks the cheapest errand
    /// from wherever they are standing (§14.2) and households fetch from whatever store is
    /// nearest (§3), so nothing in the model refuses a distance. <b>Inventing a ring would also
    /// rebuild the catchment fence D120 deleted</b>, which is the one thing this project has
    /// already paid to take out.
    /// </para>
    /// <para>
    /// <b>⭐ So the honest answer is a count rather than a circle</b>, and it is exactly what
    /// makes a market worth having: *the homes for which this would be the closest place to get
    /// food.* Those are the households whose walk it shortens. **It is not circular** — it
    /// depends on where the granary and every other store already are, which is the point Joe
    /// made about positioning: a market beside the granary serves nobody, because the granary was
    /// already nearer.
    /// </para>
    /// <para>
    /// <b>Strictly nearer</b>, so a tie goes to the store that already exists — a market that
    /// merely matches the granary's walk has not shortened anybody's errand.
    /// </para>
    /// <para>
    /// <b>Occupied homes only</b>, since a market's job is feeding families rather than
    /// buildings — the same live-count rule <c>VillageEconomy.MarketStockWanted</c>'s caller uses.
    /// </para>
    /// </remarks>
    public int HomesAMarketHereWouldBeNearestFor(GridPos position)
    {
        int served = 0;

        for (int i = 0; i < Households.Count; i++)
        {
            Household household = Households[i];
            if (household.HomePosition is not GridPos home || LivingMembersOf(household) == 0)
            {
                continue;
            }

            int here = TravelCost.Cost(home, position);
            if (here == TravelCostField.Unreachable)
            {
                continue;
            }

            bool nearest = true;
            for (int s = 0; s < StoreBuildings.Count && nearest; s++)
            {
                StoreBuilding store = StoreBuildings[s];
                if (!store.CanEverHold(Goods.Food))
                {
                    continue;
                }

                int theirs = TravelCost.Cost(home, store.Position);
                nearest = theirs == TravelCostField.Unreachable || here < theirs;
            }

            if (nearest)
            {
                served++;
            }
        }

        return served;
    }

    /// <summary>The walk from a tile to the nearest store that takes food, or -1.</summary>
    public int HaulWalkFrom(GridPos position)
    {
        StoreBuilding? store = NearestStoreAccepting(
            position, Goods.Food, static place => place.CanEverHold(Goods.Food));

        return store is null ? -1 : TravelCost.TicksBetween(position, store.Position);
    }

    /// <summary>
    /// Take this autumn's lesson — <b>run once, on the turn of winter, before the rot sweep</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Before the rot, and that ordering is the whole reading.</b> What is still standing at
    /// the turn of winter <em>is</em> the answer to *did this farm bring in what it sowed?*, and
    /// <see cref="Systems.CropSystem"/> is about to clear it.
    /// </para>
    /// <para>
    /// <b>±1 with a latch, and the simplicity is deliberate.</b> The richer rule — *settle on
    /// exactly what you brought in* — has to divide a tile count by the hands that worked the
    /// autumn, and <b>the hand count at the turn of winter is not that number</b>: D44 stands
    /// seasonal trades down, so a farm can be empty on the very tick the lesson is taken. ±1
    /// needs no hand count at all and converges in three years.
    /// </para>
    /// </remarks>
    public void LearnFromTheAutumn(Workplace farm)
    {
        ArgumentNullException.ThrowIfNull(farm);

        if (farm.Kind != JobKind.Farmer || farm.IsSite)
        {
            return;
        }

        int sown = farm.FieldTilesSown;
        int hands = farm.FieldHandsAtAutumn;
        farm.FieldTilesSown = 0;
        farm.FieldHandsAtAutumn = 0;

        // A year with no crop teaches nothing — see `Workplace.FieldTilesSown`. An empty field
        // at the turn of winter is what a met stock limit looks like, and it is identical to
        // what success looks like.
        if (sown <= 0 || hands <= 0)
        {
            return;
        }

        int derived = VillageEconomy.FieldTilesOneFarmerKeeps(Config);
        int broughtIn = sown - StandingCropTiles(farm);
        int record = broughtIn / hands;

        // ⛔⛔ A HIGH-WATER MARK, AND NOTHING ELSE — no probe, no latch, no settling back.
        // Two drafts of this method had a `+1` that made the farm try one more tile a year and
        // a flag that stopped it once a tile rotted. **Deleting both changed nothing anywhere
        // it could be measured**: 6/5/4 tiles learned and 72/60/48 reaped at ten, sixteen and
        // twenty-two ticks, identical either way. The reason is that
        // <see cref="HarvestOneFarmCanBringIn"/> multiplies by the hands standing in the field
        // *at that moment*, so a farm with two hands in spring and one by autumn already
        // commits ground for two — **the village probes on its own, and a deliberate probe was
        // a fifth invisible no-op** (D56, D177, D187 are the other four).
        //
        // ⭐ And the failure modes point the same way. Without a probe the worst a farm can do
        // is sit on its opening guess, which is exactly today's behaviour — *nobody is ever
        // worse than today*. With one, the worst it can do is rot a tile every year, which is
        // the weather D167 spent a decision deleting.
        if (record > farm.FieldTilesLearned)
        {
            farm.FieldTilesLearned = record > derived ? derived : record;
        }

        // What a farm brought in once it can bring in again; a thin year is about the hands
        // that turned up, not about the ground. That is D183's *give, never take* one system
        // over, and it is what stops one short-staffed autumn becoming a permanent verdict.
        if (farm.FieldTilesLearned < 1)
        {
            farm.FieldTilesLearned = 1;
        }

        farm.FieldWalkWhenLearned = HaulWalkFor(farm);

        if (Logs(LogLevel.Debug))
        {
            Log(
                LogLevel.Debug,
                "crops",
                $"{farm.Name} had {sown} standing on {hands} pair(s) of hands and brought in "
                + $"{broughtIn}; it has learned it can bring in {farm.FieldTilesLearned} a hand "
                + $"({farm.FieldWalkWhenLearned} ticks from a store). {Clock.SeasonAndYear()}.");
        }
    }

    /// <summary>
    /// What share of a well-sited farm's autumn <em>this</em> farm can actually manage, as a
    /// percentage — <b>a brand-new farm's opening guess, and nothing more</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔⛔ DEMOTED FROM A RULING TO A FIRST GUESS (D194), AND THE REASON IS THAT IT IS
    /// DIMENSIONALLY WRONG.</b> It divides <c>budgeted</c> — a ROUND TRIP inside the field, 4
    /// ticks — by <c>haul</c>, a ONE-WAY walk to a store, 10. <b>The ratio is not a share of
    /// anything</b>, and that it lands near the right answer at ten ticks is arithmetic
    /// coincidence. Measured, it left a farm ten ticks out sowing five tiles when it could bring
    /// in six, and <b>resting for 27% of the autumn</b> it was supposedly too busy for — 45% at
    /// sixteen ticks, 55% at twenty-two. <b>The cap cut the field and the idleness read back as
    /// proof the field had been too big.</b>
    /// </para>
    /// <para>
    /// <b>It is kept, not deleted, because a brand-new farm has no history to read</b> and this
    /// is a safe place to start from: it is what the game commits today, so <i>nobody is ever
    /// worse than today</i>. One autumn later
    /// <see cref="FieldTilesThisFarmCommitsPerHand"/> is reading
    /// <see cref="Workplace.FieldTilesLearned"/> instead and this number never speaks again.
    /// </para>
    /// <para>
    /// <b>The derivation budgets a round trip to the steading</b>
    /// (<see cref="VillageEconomy.FieldTileTicks"/>), and that is true right up until the farm's
    /// own buffer is full — after which every armful walks to the granary instead
    /// (`crops-and-orchards.md §3.2a`). So the honest question is *how much longer is this
    /// farm's real haul than the one the economy paid for?*, and the answer scales the ground
    /// it may commit.
    /// </para>
    /// <para>
    /// <b>Measured against the nearest store that takes food</b>, because that is where the
    /// harvest actually ends up once the buffer fills. A farm with its granary next door is
    /// unaffected — which is why <see cref="VillageEconomy.FieldTilesOneFarmerKeeps"/> keeps its
    /// meaning as *what a well-sited farm manages* and needs no re-derivation.
    /// </para>
    /// <para>
    /// <b>Never below a tenth.</b> A farm at the far edge of the valley should be a poor idea,
    /// not an impossible one — D43 and D86's standing rule that the player is warned and never
    /// refused. And **never above 100**: being nearer than the budget is already worth
    /// something (fewer wasted ticks), and letting it *raise* the cap would quietly re-derive
    /// the economy upward, which is the trap `skills-catalog.md §3.2` names.
    /// </para>
    /// </remarks>
    public int ReapableShareAt(Workplace farm)
    {
        ArgumentNullException.ThrowIfNull(farm);

        int haul = HaulWalkFor(farm);
        int budgeted = VillageEconomy.FieldHaulTicksBudgeted(Config);

        if (haul < 0)
        {
            return 100;
        }

        if (haul <= budgeted || budgeted <= 0)
        {
            return 100;
        }

        int share = budgeted * 100 / haul;
        return share < 10 ? 10 : share;
    }

    /// <summary>Tiles of this farm's ground with a crop standing on them.</summary>
    public int StandingCropTiles(Workplace farm)
    {
        ArgumentNullException.ThrowIfNull(farm);

        IReadOnlyList<int> owned = Zones.WorkGroundOf(farm.Id);
        int standing = 0;

        for (int i = 0; i < owned.Count; i++)
        {
            if (IsStandingCrop(Map.TerrainAt(Zones.PositionOf(owned[i]))))
            {
                standing++;
            }
        }

        return standing;
    }

    /// <summary>
    /// Whether this workplace's own buffer is <b>worth a trader's trip</b> (D171, D185).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ ONE CONDITION, TWO CALLERS, AND THE BUG THIS METHOD EXISTS TO CLOSE WAS THE
    /// SECOND CALLER NOT HAVING IT.</b> `crops-and-orchards.md §3.2` has said since the farm
    /// shipped that the buffer is free and *"running it dry is the market's job"*, and D171
    /// built the leg in <c>BehaviorSystem.PlanMarketErrand</c> that does it. But
    /// <see cref="World.LabourQuota.MarketersWanted"/> counted errands by looping over
    /// households **and nothing else** — so **the village never staffed a marketer because a
    /// farm needed emptying.** A trader who happened to be working would clear it; if every
    /// household was content, nobody was working, and the farm sat full however long it stood.
    /// </para>
    /// <para>
    /// <b>The behaviour existed and the demand did not</b>, which is D36's own rule —
    /// *"bounded by errands and never by spare hands"* — applied to two of three leg types.
    /// **The fix is not a second copy of the comparison**: two copies of one sum is how they
    /// come to disagree (D142's three call sites, D148's two meanings), and this bug is that
    /// failure one level up. So both callers ask here.
    /// </para>
    /// <para>
    /// <b>⭐ THE CONDITION IS DERIVED, NOT TUNED</b> (D16, and D171's own standard). A buffer is
    /// worth clearing exactly when it can no longer take a whole armful — which is precisely
    /// when <c>HaulTheHarvest</c> stops choosing it and starts sending the farmer to the
    /// granary. No threshold, no new number, one comparison.
    /// </para>
    /// <para>
    /// <b>A workplace with no store of its own has <c>int.MaxValue</c> capacity</b>, so this is
    /// also what keeps every other building out without naming a kind.
    /// </para>
    /// </remarks>
    /// <summary>
    /// How long this villager takes over one action of <paramref name="trade"/> — <b>where
    /// mastery finally bites</b> (`skills-catalog.md §3.3`, Phase 3 landing 2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ THIS IS THE METHOD THAT STOPS THE SKILL PILLAR BEING DECORATIVE.</b> Landing 1
    /// shipped proficiency that accrues, is hashed and is visible and **changes nothing** —
    /// which is the exact shape of D56's clothing, measured as a no-op over 300 years and
    /// blocked for it. Everything before this call site was bookkeeping.
    /// </para>
    /// <para>
    /// <b>⛔ A NOVICE GETS EXACTLY TODAY'S NUMBER, TO THE TICK.</b> Zero progress scales nothing,
    /// so `VillageEconomy`'s survival floor — solved about the least skilled person in the
    /// valley — is untouched and every number derived from it still holds (§3.2). **Nobody is
    /// ever worse than today**; a master is simply better.
    /// </para>
    /// <para>
    /// <b>Linear in work up to mastery, then flat.</b> Traceable rather than clever (§1.6): a
    /// villager halfway to mastery is halfway to the bonus, and the player can be told that in
    /// one sentence. A curve would need a reason, and nothing in the design supplies one.
    /// </para>
    /// <para>
    /// <b>⚠️ IN PRACTICE IT IS A STEP, NOT A RAMP, AND THE DURATIONS ARE WHY.</b> A three-tick
    /// action can only become two, so the bonus buys nothing until it rounds to a whole tick —
    /// at 34% that is around 84% of the way to mastery. **That is the tier model arriving
    /// through arithmetic rather than through a design decision**, and it is worth knowing
    /// before §12's tier names get chosen: the sim already behaves as though there are two
    /// tiers, because it cannot express any others at these durations.
    /// </para>
    /// <para>
    /// <b>Never below one tick.</b> An action that costs nothing is an action that happens
    /// infinitely often, which is a hang rather than a fast farmer.
    /// </para>
    /// </remarks>
    public int WorkTicksFor(Villager villager, JobKind trade, int baseTicks)
    {
        ArgumentNullException.ThrowIfNull(villager);

        if (baseTicks <= 1 || Config.MasterySpeedBonusPercent <= 0)
        {
            return baseTicks;
        }

        SkillRow? skill = SkillGrownBy(trade);
        if (skill is null)
        {
            return baseTicks;
        }

        int mastery = Config.MasteryWorkFor(skill);
        if (mastery <= 0)
        {
            return baseTicks;
        }

        SkillProgress? progress = villager.FindProgressIn(skill.Id);
        if (progress is null || progress.Work <= 0)
        {
            return baseTicks;
        }

        // Share of the way to mastery, 0–100. Capped rather than allowed to run on: a master
        // who keeps working is a master, not somebody who eventually works in no time at all.
        long share = (long)progress.Work * 100 / mastery;
        if (share > 100)
        {
            share = 100;
        }

        // Integer throughout (D2). `long` for the product only — 4 × 34 × 100 is small, but the
        // shape of this expression is exactly where an int overflow would hide if the durations
        // or the bonus ever grew.
        long faster = (long)baseTicks * Config.MasterySpeedBonusPercent * share / 10000;
        int ticks = baseTicks - (int)faster;

        return ticks < 1 ? 1 : ticks;
    }
    /// <summary>Remember who holds a technique, so the village can name them when it is lost.</summary>
    internal void RememberKnowerOf(int techniqueId, Villager knower)
    {
        ArgumentNullException.ThrowIfNull(knower);
        LastKnowerIds[techniqueId] = knower.Id;
    }

    /// <summary>
    /// The name of the last soul who held a technique, for the sentence about losing it.
    /// </summary>
    /// <remarks>
    /// <b>⚠️ Looked up among the dead as well as the living, on purpose</b> — by the tick this is
    /// asked, the person it names has just died, which is the entire reason the sentence is being
    /// written. Falls back to <em>"somebody"</em> rather than throwing: a village that lost a
    /// technique still has to be able to say so, and a missing name is a worse sentence rather than
    /// a broken run.
    /// </remarks>
    public string LastKnowerOf(int techniqueId)
    {
        int id = LastKnowerIds[techniqueId];
        for (int i = 0; i < Villagers.Count; i++)
        {
            if (Villagers[i].Id == id)
            {
                return Villagers[i].Name;
            }
        }

        return "Somebody";
    }

    /// <summary>
    /// What a trade brings in, once the village's own techniques are counted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ ONE PLACE, DELIBERATELY — the sibling of <see cref="WorkTicksFor"/>.</b> That method is
    /// the single seam where mastery bites (D187); this is the single seam where a <em>technique</em>
    /// does. Four production sites call it, and a fifth trade added tomorrow gets the behaviour by
    /// calling it rather than by remembering a formula. **Two copies of a multiplier is how a bonus
    /// comes to be applied twice in one place and not at all in another**, which `StoreKind` has
    /// taught this project five times (D76).
    /// </para>
    /// <para>
    /// <b>⭐⭐ IT IS THE VILLAGE'S BONUS, NOT THE WORKER'S, AND IT TAKES NO VILLAGER.</b> That is the
    /// signature saying so. Once anybody has worked a technique out, <em>everyone</em> in that trade
    /// does the job the better way — which is what makes a technique different from proficiency, and
    /// what makes the whole village's output drop when the last knower dies. A per-villager bonus
    /// would be indistinguishable from mastery, which already bites.
    /// </para>
    /// <para>
    /// <b>⚠️ NOTHING HERE REACHES <see cref="VillageEconomy"/>, AND THAT IS THE POINT.</b> The
    /// survival floor is solved against the base config numbers, so **it assumes a village that
    /// knows nothing.** A technique is upside above that line and never a move in the line itself —
    /// so losing one can cost a village its surplus and can never cost it the run. *§0.1's "you lose
    /// villagers, not runs", applied to knowledge.*
    /// </para>
    /// </remarks>
    public int YieldWithTechnique(JobKind trade, int baseAmount)
    {
        if (baseAmount <= 0)
        {
            return baseAmount;
        }

        SkillRow? skill = SkillGrownBy(trade);
        if (skill is null)
        {
            return baseAmount;
        }

        int id = TechniquesCatalog.FromSkill(skill.Id);
        if (id < 0 || KnowledgeStates[id] == KnowledgeState.Unknown)
        {
            return baseAmount;
        }

        int bonus = TechniquesCatalog[id].YieldBonusPercent;

        // Integer only (D2). Rounded down, so a technique never invents a unit out of rounding —
        // the village's gain has to come from the percentage being real.
        return bonus <= 0 ? baseAmount : baseAmount + (baseAmount * bonus / 100);
    }


    /// <summary>How practised this villager is in one skill, in words (§3.2c, D190).</summary>
    /// <remarks>
    /// <para>
    /// <b>A reading of the one integer, computed every time and never stored</b> — see
    /// <see cref="SkillTier"/> for why a stored tier would be two sources of truth for one fact.
    /// </para>
    /// <para>
    /// <b>The bands are halves, which is the plainest thing that could be true:</b> a novice has
    /// done none of it, an apprentice is on the way, a journeyman is past halfway, and a master
    /// has arrived. **<see cref="SkillProgress.Mastered"/> is what makes the top one permanent**
    /// — it is §5.4's record of achievement, so somebody who mastered a trade and moved on is
    /// still a master of it.
    /// </para>
    /// <para>
    /// ⚠️ <b>The speed step falls at about 70% of mastery, inside the journeyman band</b> (D187).
    /// So the tier a player reads and the speed the sim runs at change at different moments, and
    /// that is a consequence of three-tick actions rather than a design.
    /// </para>
    /// </remarks>
    public SkillTier TierOf(Villager villager, SkillRow skill)
    {
        ArgumentNullException.ThrowIfNull(villager);
        ArgumentNullException.ThrowIfNull(skill);

        SkillProgress? progress = villager.FindProgressIn(skill.Id);
        if (progress is null || progress.Work <= 0)
        {
            return SkillTier.Novice;
        }

        if (progress.Mastered)
        {
            return SkillTier.Master;
        }

        int mastery = Config.MasteryWorkFor(skill);
        if (mastery <= 0)
        {
            return SkillTier.Novice;
        }

        return progress.Work * 2 >= mastery ? SkillTier.Journeyman : SkillTier.Apprentice;
    }

    /// <summary>The skill this kind of work grows, or null if no row claims it.</summary>
    /// <remarks>
    /// <b>⚠️ Not assumed to be one-to-one</b> (§4.3). The catalogue happens to be 1:1 today and
    /// **the model must not depend on it**, because a skill two jobs grow — a smith and a
    /// farrier — is obviously coming. The first row that claims the trade wins, in catalogue
    /// order, which is stated so it cannot become an unordered tie (D15).
    /// </remarks>
    public SkillRow? SkillGrownBy(JobKind trade)
    {
        for (int i = 0; i < Config.Skills.Count; i++)
        {
            if (Config.Skills[i].GrownBy == trade)
            {
                return Config.Skills[i];
            }
        }

        return null;
    }

    public bool BufferWorthClearing(Workplace workplace)
    {
        ArgumentNullException.ThrowIfNull(workplace);

        return !workplace.IsSite
            && workplace.Store.Food > 0
            && workplace.Store.FreeSpace < Config.CropYieldPerTile;
    }

    private string? ForesterIdleNote(Workplace hut)
    {
        if (Zones.WorkGroundTiles(hut.Id) == 0)
        {
            return $"{hut.Name} has no ground to work — paint some for it.";
        }

        // Still something to fell or something to plant: it is working.
        if (NextGroundToWork(hut, hut.Position) is not null)
        {
            return null;
        }

        if (hut.Mode != WorkMode.PlantOnly && StockLimits.IsMet(Goods.Logs, LogsInSheds()))
        {
            return $"{hut.Name} has stopped — you asked the village to keep "
                + $"{StockLimits.For(Goods.Logs)} logs and it has {LogsInSheds()}.";
        }

        return hut.Mode == WorkMode.PlantOnly
            ? $"{hut.Name} is resting its wood, and its ground is wooded again."
            : $"{hut.Name} has nothing left to fell or to plant on its ground.";
    }

    private string? WoodcutterIdleNote(Workplace hut)
    {
        if (StockLimits.IsMet(Goods.Firewood, FirewoodInSheds()))
        {
            return $"{hut.Name} has stopped — you asked the village to keep "
                + $"{StockLimits.For(Goods.Firewood)} firewood and it has {FirewoodInSheds()}.";
        }

        return NearestStoreAccepting(
                hut.Position, Goods.Logs, store => store.Store.Logs >= Config.LogsPerSplit)
            is null
            ? $"{hut.Name} has no logs to split — no store in reach holds the "
                + $"{Config.LogsPerSplit} a batch needs."
            : null;
    }

    private string? ForagerIdleNote(Workplace hut)
    {
        // Winter is not a fault, and marking it would teach the player to ignore the marker.
        if (!SeasonRules.IsGatherable(Clock.Season))
        {
            return null;
        }

        return GatherYieldAt(hut) == 0
            ? $"{hut.Name} has no trees left in its ring, so a trip brings back nothing."
            : null;
    }

    /// <summary>
    /// Seats at forester's huts that still own ground with no tree on it — <b>the village's
    /// demand for planting</b>, as opposed to for timber.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What a capped hut is wanted for (D146).</b> When a Logs limit is met the village
    /// stops wanting timber, and <c>LabourQuota</c> used to zero the foresters outright — which
    /// left <see cref="MayFell"/> sending nobody to bare ground, because the allocator had
    /// emptied the building. <b>A staffing number is a ceiling, not a summons</b>, so the
    /// demand has to say what the hands are <em>for</em>.
    /// </para>
    /// <para>
    /// <b>Derived, and it falls to zero by itself</b> (D16): once every painted tile is wooded
    /// again there is nothing to plant, this is zero, and the hands become laborers — which is
    /// Joe's ordering, <i>replant until the painted area is maxed out, then extra hands</i>,
    /// expressed as a quantity rather than as a rule.
    /// </para>
    /// <para>
    /// Counted in <see cref="Workplace.Places"/> rather than <c>Capacity</c>, so a hut the
    /// player has turned down does not ask for hands it would refuse.
    /// </para>
    /// </remarks>
    public int ForesterSeatsWithGroundToPlant()
    {
        int seats = 0;

        for (int i = 0; i < Workplaces.Count; i++)
        {
            Workplace workplace = Workplaces[i];
            if (workplace.Kind != JobKind.Forester || workplace.IsSite)
            {
                continue;
            }

            IReadOnlyList<int> owned = Zones.WorkGroundOf(workplace.Id);
            for (int t = 0; t < owned.Count; t++)
            {
                if (Map.TerrainAt(Zones.PositionOf(owned[t])) != Terrain.Forest)
                {
                    seats += workplace.Places;
                    break;
                }
            }
        }

        return seats;
    }

    public GridPos? NextGroundToWork(Workplace workplace, GridPos from)
    {
        ArgumentNullException.ThrowIfNull(workplace);

        IReadOnlyList<int> owned = Zones.WorkGroundOf(workplace.Id);
        if (owned.Count == 0)
        {
            return null;
        }

        // ⭐ PLANTING IS ALWAYS ON; FELLING IS THE TOGGLE (Joe, D146). Painting ground for a hut
        // is already the instruction to keep it wooded, so the question the player actually
        // answers is whether they are taking timber out of this wood or letting it come back.
        //
        // So: **fell while anything is standing, and plant the bare tiles whenever felling is
        // not allowed or nothing is left to fell.** `MayFell` is the single place that answers
        // "is felling allowed here, right now", and it folds the toggle together with a met
        // Logs limit — a cap is simply felling switched off for a while.
        bool mayFell = MayFell(workplace);
        bool anyStanding = false;

        if (mayFell)
        {
            for (int i = 0; i < owned.Count && !anyStanding; i++)
            {
                anyStanding = Map.TerrainAt(Zones.PositionOf(owned[i])) == Terrain.Forest;
            }
        }

        bool wantsTrees = mayFell && anyStanding;

        GridPos? best = null;
        int bestCost = int.MaxValue;

        for (int i = 0; i < owned.Count; i++)
        {
            GridPos at = Zones.PositionOf(owned[i]);
            bool wooded = Map.TerrainAt(at) == Terrain.Forest;

            if (wooded != wantsTrees)
            {
                continue;
            }

            // Planting needs bare ground, and rock or water is not bare ground — it is
            // ground nothing will ever grow on.
            if (!wantsTrees && Map.TerrainAt(at) != Terrain.Grass)
            {
                continue;
            }

            int cost = TravelCost.Cost(from, at);
            if (cost < bestCost)
            {
                best = at;
                bestCost = cost;
            }
        }

        return best;
    }

    // ---------------------------------------------------------------
    //  The farm (`specs/crops-and-orchards.md`, D161)
    // ---------------------------------------------------------------

    /// <summary>Whether a farm could put this ground under seed.</summary>
    /// <remarks>
    /// <b>Two terrains, and they are the same fact wearing two faces.</b>
    /// <see cref="Terrain.Field"/> is ground a farm has already broken;
    /// <see cref="Terrain.Grass"/> is ground it has not got to yet — a tile the laborers
    /// cleared after the brush went over it, or one the plough at painting time could not
    /// take. Both are bare ground a farm owns, and asking the question once is what stops
    /// the two answers drifting apart (D76's seam, named before it could open).
    /// </remarks>
    public static bool IsSowable(Terrain terrain) =>
        terrain is Terrain.Field or Terrain.Grass;

    /// <summary>Whether this ground has a crop standing on it — this year's food, unreaped.</summary>
    public static bool IsStandingCrop(Terrain terrain) =>
        terrain is Terrain.Sown or Terrain.Ripe;

    /// <summary>Break ground for a field. The farm's answer to <see cref="Plant"/>.</summary>
    /// <remarks>
    /// <para>
    /// <b>Through <see cref="SetTerrain"/> like every other change of ground</b> (D85), so
    /// the flow-field cache and every hut's tree count hear about it by the one door rather
    /// than by somebody remembering to tell them.
    /// </para>
    /// <para>
    /// <b>Grass only, on the same restraint <see cref="Plant"/> keeps.</b> A farm does not
    /// plough up a wood, a sapling, a rock or a river — it takes the ground that is already
    /// open. What happens to the rest is the player's business: paint it for harvest and the
    /// laborers will clear it, and it becomes ploughable then (D87, D100).
    /// </para>
    /// </remarks>
    public bool Plough(GridPos tile) =>
        Map.TerrainAt(tile) == Terrain.Grass && SetTerrain(tile, Terrain.Field);

    /// <summary>
    /// Whether the village may put more ground under seed right now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ ONE DOOR, THE WAY <see cref="MayFell"/> IS ONE DOOR (D145, D146).</b> A met food
    /// limit is simply <em>sowing switched off for a while</em>, and putting the cap and the
    /// season through one predicate is what stops them becoming two rules that can disagree —
    /// which is the shape of D128, D139, D142 and D144, four bugs and one form.
    /// </para>
    /// <para>
    /// <b>⚠️ AND IT STOPS THE SOWING ONLY, NEVER THE REAPING, which is the interesting half.</b>
    /// A limit is an instruction about how much to keep; leaving a standing harvest to rot
    /// because the granary is presently full would spend a year of the player's work on a
    /// number they set for a different reason — and `crops-and-orchards.md §5.1`'s
    /// use-it-or-lose-it is meant to punish inattention, not obedience. So a capped village
    /// still brings its crop in, and simply does not commit next year's ground until it wants
    /// the food. That is D146's *"a capped hut can replant"* one job over: the cap stops the
    /// work that <em>makes</em> the good, not the work that rescues what is already made.
    /// </para>
    /// <para>
    /// <b>What it reads is what the village HOLDS</b> (D161) — stores plus workplaces — because
    /// a farm's own buffer is food the village has, and comparing a limit against a total that
    /// cannot see it is D81's seam for the seventh time.
    /// </para>
    /// </remarks>
    public bool MaySow() => !StockLimits.IsMet(Goods.Food, FoodTheVillageHolds());

    /// <summary>
    /// Seats at farmhouses the year still has work for — <b>the village's demand for farmers</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔⛔ THIS IS THE ONE WITH TEETH, and it was built and proved before a single tile
    /// could be sown</b> (`crops-and-orchards.md §11b`). <c>SetStaffing</c> is a ceiling and
    /// not a summons (D146): if the village does not <em>actively want</em> farmers when the
    /// fields are ripe, the harvest stands until winter takes it, and every guard written for
    /// the crop blames <see cref="Systems.CropSystem"/> — which will be working perfectly.
    /// That is D146's bug waiting one job over.
    /// </para>
    /// <para>
    /// <b>⚠️ AND SUMMER IS WANTED TOO, WHICH IS A CORRECTION TO THE OBVIOUS READING OF THE
    /// CALENDAR.</b> `crops-and-orchards.md §5` says a farmer *tends* in summer and is *a
    /// spare hand* in winter — so the seasonal fact is what a farmer <em>does</em>, not
    /// whether the village wants one. Making the demand zero in summer looks right and is a
    /// trap: <see cref="Systems.LabourSystem"/> reshuffles the whole village every three years
    /// and <c>TakeUpSlack</c> only ever fills openings from villagers who are <em>idle</em>, so
    /// a reshuffle landing in July would empty the farm and autumn would find nobody free to
    /// put back in it. <b>The standing crop is why the hands are wanted</b>, which is a truer
    /// sentence anyway — somebody has to be there in September, and the village decides that
    /// in June.
    /// </para>
    /// <para>
    /// <b>Winter is the one season it really is zero</b>, and that costs nothing: the forager
    /// quota is zero in winter too (D44), so the village is full of idle hands when spring
    /// comes and the scarce kinds are matched first.
    /// </para>
    /// <para>
    /// Derived and falling to zero by itself (D16), counted in <see cref="Workplace.Places"/>
    /// rather than <c>Capacity</c> so a farm the player has turned down does not ask for hands
    /// it would refuse — both exactly as <see cref="ForesterSeatsWithGroundToPlant"/> does.
    /// </para>
    /// </remarks>
    public int FarmerSeatsWithGroundToWork()
    {
        bool sowing = SeasonRules.IsSowing(Clock.Season) && MaySow();
        int seats = 0;

        for (int i = 0; i < Workplaces.Count; i++)
        {
            Workplace farm = Workplaces[i];
            if (farm.Kind != JobKind.Farmer || farm.IsSite)
            {
                continue;
            }

            IReadOnlyList<int> owned = Zones.WorkGroundOf(farm.Id);
            for (int t = 0; t < owned.Count; t++)
            {
                Terrain here = Map.TerrainAt(Zones.PositionOf(owned[t]));

                // Bare ground in spring is a year waiting to be committed; a standing crop
                // in any season is this year's food, and somebody has to be here to take it.
                if ((sowing && IsSowable(here)) || IsStandingCrop(here))
                {
                    seats += farm.Places;
                    break;
                }
            }
        }

        return seats;
    }

    /// <summary>The tile a farmer should walk to next, or null if the farm has nothing to do.</summary>
    /// <remarks>
    /// <para>
    /// <b>The same shape as <see cref="NextGroundToWork"/>, and deliberately not the same
    /// method.</b> A forester asks *tree or bare?*; a farm asks *what season is it?* — and the
    /// two verbs must not share a door, which is the rule `crops-and-orchards.md §6` states
    /// about the harvest brush and is worth keeping one level up as well.
    /// </para>
    /// <para>
    /// <b>Nearest first, off the one shared cost field</b> (§2.6), so a farmer works outward
    /// from where they are standing rather than in the order the tiles were painted.
    /// </para>
    /// <para>
    /// <b>⚠️ Sowing is spring and only spring</b> (<see cref="SeasonRules.IsSowing"/>): a
    /// missed sowing is a missed year, and that is what makes spring a decision rather than a
    /// deadline that slides. Reaping is autumn and only autumn, because winter takes what is
    /// left standing (Joe — use it or lose it).
    /// </para>
    /// </remarks>
    public GridPos? NextFieldToWork(Workplace farm, GridPos from)
    {
        ArgumentNullException.ThrowIfNull(farm);

        IReadOnlyList<int> owned = Zones.WorkGroundOf(farm.Id);
        if (owned.Count == 0)
        {
            return null;
        }

        bool sowing = SeasonRules.IsSowing(Clock.Season) && MaySow();
        bool reaping = SeasonRules.IsReaping(Clock.Season);
        if (!sowing && !reaping)
        {
            return null;
        }

        // ⭐⭐ A FARM SOWS ONLY WHAT IT CAN BRING IN (Joe, 2026-08-16: *"2x farmers planted 20
        // fields in the spring, and harvested only 9 in the fall"*).
        //
        // **It was worse than he saw: measured over his own run, every year was ~17 sown and
        // ~5 reaped, with twelve to sixteen fields rotting — for ever.** Sowing is cheap (a
        // step between rows, carrying nothing) and reaping is dear (an armful to a store), so
        // a spring will always commit two or three times the ground an autumn can take. The
        // economy already knows the number — `FieldTilesOneFarmerKeeps` takes the SMALLER of
        // the two seasons for exactly this reason — but nothing was enforcing it on the
        // sowing, so the derivation described a farm nobody was running.
        //
        // ⚠️ **This is what makes use-it-or-lose-it mean anything** (`crops-and-orchards.md
        // §5.1`). A rot line every single year by construction is weather, not a consequence;
        // the player cannot act on it and learns to ignore it. Rot should say *you over-painted,
        // or you lost a farmer* — which it now does, because a farm that is within its hands
        // sows within its hands.
        //
        // Counted in `WorkerIds` rather than `Places`, like `WorkGroundAllowanceFor`: the crop
        // a farm can bring in depends on who is actually standing in it, so losing a farmer in
        // summer correctly means next spring commits less ground (D86's live-allowance rule).
        if (sowing && StandingCropTiles(farm) >= HarvestOneFarmCanBringIn(farm))
        {
            return null;
        }

        GridPos? best = null;
        int bestCost = int.MaxValue;

        for (int i = 0; i < owned.Count; i++)
        {
            GridPos at = Zones.PositionOf(owned[i]);
            Terrain here = Map.TerrainAt(at);

            bool wanted = sowing ? IsSowable(here) : here == Terrain.Ripe;
            if (!wanted)
            {
                continue;
            }

            int cost = TravelCost.Cost(from, at);
            if (cost < bestCost)
            {
                best = at;
                bestCost = cost;
            }
        }

        return best;
    }

    /// <summary>Put a tree back on a tile. The other half of <see cref="Harvest"/>.</summary>
    /// <remarks>
    /// <b>⭐ The first thing in this game that makes the valley richer than it found it.</b>
    /// Everything else consumes or converts. Through <see cref="SetTerrain"/> like every other
    /// change of ground (D85), so the flow-field cache and every hut's tree count learn about it
    /// by the same door rather than by anybody remembering to tell them.
    /// </remarks>
    public bool Plant(GridPos tile)
    {
        // ⭐ A SAPLING, NOT A TREE (Joe, D126). Planting used to produce full-grown woodland
        // the instant the forester finished, which made a planted wood indistinguishable
        // from an old one and gave the player no way to see the years they had bought.
        // A planted tile now grows up on the same clock as one that came back by itself —
        // *"sapling for the first six months, mature tree after a year"* — so the two kinds
        // of recovery cost the same time and only differ in who started them.
        if (Map.TerrainAt(tile) != Terrain.Grass || !SetTerrain(tile, Terrain.Sapling))
        {
            return false;
        }

        // ⭐ PLANTED, SO IT WAITS ITS FULL PERIOD (D220). Without this the sweep would mature
        // it on its next visit — which may be the very next tick — where a sapling the sweep
        // seeded itself is not seen again for a whole period. See `GeneratedMap.IsYoungSapling`.
        Map.SetYoungSapling(tile, true);
        return true;
    }

    /// <summary>Whether the village has already been told it has nowhere for a good.</summary>
    /// <remarks>
    /// <b>Gates narration and nothing else</b>, which is why it is not in the state hash: two
    /// runs of one seed say the same sentence at the same tick because everything that decides
    /// it is hashed. It exists for D42's rule about the distance warning — one considered
    /// sentence, rather than a nag the player learns to click past.
    /// </remarks>
    private readonly bool[] _saidThereIsNowhereFor;

    /// <summary>
    /// Which (villager, skill) pairs the village has already been warned about
    /// (`skills-catalog.md §7`).
    /// </summary>
    /// <remarks>
    /// <b>Gates narration and nothing else, so it is not in the state hash</b> — the same
    /// standing as <see cref="_saidThereIsNowhereFor"/> above and for the same reason: two runs
    /// of one seed say the same sentence on the same tick because <em>everything that decides
    /// it</em> is hashed. It starts empty and is driven entirely by hashed state.
    /// <para>
    /// <b>Entries are removed as well as added</b>, which is what makes this an *edge* detector
    /// rather than a one-shot. A trade that gains a second master and later loses them is at
    /// risk again, and the village should be told again — D123 and D147's rule is *narrate on
    /// the change*, and a flag that only ever sets would silently swallow the second warning.
    /// </para>
    /// </remarks>
    private readonly HashSet<(int Villager, int Skill)> _saidKnowledgeIsAtRisk = new();

    /// <summary>
    /// ⭐⭐ <b>The at-risk sentence, or null</b> — *"Mabel is 68 and the only soul who knows
    /// herbalism."* (`skills-catalog.md §7`, `DESIGN.md §2.1`/§2.7.)
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>§2.1's failure mode is what this exists to answer:</b> *"punishing the player for
    /// losses they couldn't foresee. Knowledge-at-risk must be **visible and actionable**."*
    /// Without it, a village loses its last master farmer and the only evidence is that
    /// everything quietly got slower — <b>which is a funeral surprise, and D103's rule is that a
    /// feature the player cannot reach does not exist.</b>
    /// </para>
    /// <para>
    /// <b>⭐ THE SENTENCE OR NOTHING, FROM ONE PLACE</b> — D147's shape for <c>IdleNote</c>,
    /// taken deliberately. The village log and the villager's own panel both read this, so
    /// <b>they cannot disagree about who is at risk</b>; two copies of one condition is how they
    /// come to (D142's three call sites, D148's two meanings).
    /// </para>
    /// <para>
    /// <b>⭐ BOTH HALVES ARE DERIVED, AND NEITHER IS A NEW NUMBER.</b> *Near the end* is
    /// <see cref="LifeStage.Elder"/>, which the game already derives from vigour and already
    /// calls by that name. *The only soul who knows* is <b>the only living master</b> — and
    /// mastery is the one threshold this design already has, already narrates and already keeps
    /// in <c>data/</c>. A fraction picked here would be a number with no derivation behind it,
    /// which is what D16 refuses.
    /// </para>
    /// <para>
    /// ⚠️ <b>It names the trade a second hand would learn</b>, because the action is the point:
    /// the player's remedy is to staff somebody beside them, and `skills-catalog.md §5.3` is
    /// explicit that this is the lever rather than a pairing screen. **A warning whose remedy is
    /// unstated is an alert, not information.**
    /// </para>
    /// </remarks>
    public string? KnowledgeAtRiskNote(Villager villager)
    {
        ArgumentNullException.ThrowIfNull(villager);

        SkillRow? skill = OnlyLivingMasterOf(villager);
        if (skill is null)
        {
            return null;
        }

        return $"{villager.Name} is {villager.AgeYears} and the only soul in the village who has "
            + $"mastered {skill.Name.ToLowerInvariant()}. Put somebody beside them to learn it, "
            + "or it goes with them.";
    }

    /// <summary>
    /// The skill this elder is the last living master of, or null.
    /// </summary>
    /// <remarks>
    /// <b>The first one in id order where it is true</b>, so a villager who is the last master of
    /// two things is warned about one of them and the sentence stays a sentence. The second is
    /// not lost — it is still true next year, and the village is told then.
    /// </remarks>
    private SkillRow? OnlyLivingMasterOf(Villager villager)
    {
        if (!villager.Alive || villager.LifeStage != LifeStage.Elder)
        {
            return null;
        }

        for (int i = 0; i < Config.Skills.Count; i++)
        {
            SkillRow skill = Config.Skills[i];
            if (villager.FindProgressIn(skill.Id) is not { Mastered: true })
            {
                continue;
            }

            bool anybodyElse = false;
            for (int v = 0; v < Villagers.Count && !anybodyElse; v++)
            {
                Villager other = Villagers[v];
                anybodyElse = other.Alive
                    && other.Id != villager.Id
                    && other.FindProgressIn(skill.Id) is { Mastered: true };
            }

            if (!anybodyElse)
            {
                return skill;
            }
        }

        return null;
    }

    /// <summary>
    /// Tell the village about knowledge that is about to be lost — <b>once, on the edge</b>.
    /// </summary>
    /// <remarks>
    /// <b>Swept rather than triggered, and that is not laziness.</b> The condition turns true for
    /// three different reasons — this villager ages into <see cref="LifeStage.Elder"/>, this
    /// villager masters something, or <em>somebody else dies</em> — and the third has nothing to
    /// do with the person being warned about. **One sweep beats three notifications** (D142), and
    /// a year is the right cadence for a warning about a lifetime.
    /// </remarks>
    public void SayWhatKnowledgeIsAtRisk()
    {
        for (int i = 0; i < Villagers.Count; i++)
        {
            Villager villager = Villagers[i];
            SkillRow? skill = OnlyLivingMasterOf(villager);

            if (skill is null)
            {
                // Not at risk any more — for any reason, including having died. Forgetting is
                // what lets the warning fire again if it becomes true again.
                _saidKnowledgeIsAtRisk.RemoveWhere(said => said.Villager == villager.Id);
                continue;
            }

            if (_saidKnowledgeIsAtRisk.Add((villager.Id, skill.Id)))
            {
                Narrate(KnowledgeAtRiskNote(villager)!);
            }
        }
    }

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
            + $"nothing. A stockpile costs only the cleared ground it stands on. "
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
    /// minutes to over ten when <see cref="NearestHarvest(GridPos)"/> shipped without one (D87). A
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
    /// <summary>
    /// Whether this tile would take residential paint, <b>and what the player should be told</b>
    /// — pure (D198).
    /// </summary>
    /// <remarks>
    /// <b>The <see cref="CanBuildAt"/> / <see cref="Mark"/> split, applied to the brush.</b> The
    /// view could show a ghost under the cursor for a *building* and could show nothing at all
    /// for a *brush*, because every paint method mixed the test with the doing — so the only way
    /// to ask *"would this tile take?"* was to paint it. Joe, playing: *"when I'm painting I
    /// don't see an outline of the area I'm about to paint. I just have to point and click and
    /// hope."*
    /// <para>
    /// <b>One condition, two callers</b> (D142's three call sites, D148's two meanings):
    /// <see cref="PaintResidential"/> asks this and then acts, so the preview and the paint can
    /// never disagree about which tiles are in.
    /// </para>
    /// </remarks>
    public PlacementVerdict CanPaintResidential(GridPos tile)
    {
        if (!Map.Contains(tile))
        {
            return PlacementVerdict.No("That is outside the valley.");
        }

        if (Map.TerrainAt(tile) == Terrain.Water)
        {
            return PlacementVerdict.No("Nobody can live on the water.");
        }

        // ⚠️ Asked BEFORE the tile is painted and it does not read the zone, so the answer is
        // the same either side of the stroke — which is what makes it safe to show in advance.
        int toWork = NearestForageDistance(tile);
        int budget = VillageEconomy.MaxHomeToWorkTiles(Config);

        return toWork > budget
            ? PlacementVerdict.Yes(
                $"That corner is {toWork} tiles from the nearest food; the village budgets " +
                $"{budget}. Families there will go hungry.")
            : PlacementVerdict.Fine;
    }

    public PlacementVerdict PaintResidential(GridPos tile)
    {
        PlacementVerdict verdict = CanPaintResidential(tile);
        if (!verdict.Allowed)
        {
            return verdict;
        }

        Zones.SetResidential(tile, true);
        return verdict;
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
        return workplace.WorkerIds.Count * TilesOneWorkerKeeps(workplace.Kind);
    }

    /// <summary>
    /// How much ground one pair of hands can look after, given what they are doing to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠️ IT USED TO BE ONE NUMBER FOR EVERYTHING, AND THE FARM IS WHERE THAT STOPPED BEING
    /// HONEST.</b> D86 priced work ground in workers — <c>work_ground_tiles_per_worker</c>,
    /// *"how much land one person can look after"* — and D112 reused it for the gatherer's
    /// ring on the stated grounds that <em>one rule serving two buildings beats two numbers
    /// that can drift apart</em>. That argument still holds and this does not break it, because
    /// the second figure is <b>derived rather than typed</b> and therefore cannot drift.
    /// </para>
    /// <para>
    /// <b>The difference is mechanical.</b> A forester visits each of their tiles once, at any
    /// time of year. A farmer must visit each tile <em>twice</em> — sowing it in spring and
    /// reaping it in autumn — and both visits are confined to one season. So the ground one
    /// farmer keeps is what <see cref="VillageEconomy.FieldTilesOneFarmerKeeps"/> says, and
    /// pretending otherwise would have the overstretched warning call a farm comfortable while
    /// most of its field lay fallow every year — a control saying the opposite of what is
    /// happening, which is D148's bug and D139's.
    /// </para>
    /// </remarks>
    public int TilesOneWorkerKeeps(JobKind kind) => kind == JobKind.Farmer
        ? VillageEconomy.FieldTilesOneFarmerKeeps(Config)
        : Config.WorkGroundTilesPerWorker;

    /// <summary>
    /// Whether this kind of work is done on ground the player paints for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ THE VIEW ASKED THIS BY NAMING A KIND, AND THE FARM SHIPPED WITH NO BRUSH BECAUSE OF
    /// IT.</b> `Main.cs` read <c>staffable is { Kind: JobKind.Forester }</c> under a comment
    /// promising the opposite — *"shown by whether the building CAN own ground rather than by
    /// what kind it is, so the next one needs no line here"*. The next one was the farmhouse,
    /// and it needed a line: Joe placed a farm, read <em>"give it some with the work-ground
    /// brush"</em> on its own panel, and **there was no brush**. A feature the player cannot
    /// reach does not exist (D103), and a comment describing code that does something else is
    /// D159's finding in miniature.
    /// </para>
    /// <para>
    /// <b>So the question lives here, once, where both the sim and the view can ask it</b> —
    /// and the promise the comment made is now true: the third building that owns ground adds
    /// a value to this line and nothing else anywhere.
    /// </para>
    /// </remarks>
    public static bool KeepsWorkGround(JobKind kind) =>
        kind is JobKind.Forester or JobKind.Farmer;

    /// <summary>Whether a workplace has been given more ground than it has hands for.</summary>
    /// <remarks>
    /// <b>A state, not just a moment.</b> The warning fires when land is painted, but the
    /// condition outlives the painting — somebody dies, the staffing is turned down, and the
    /// ground is suddenly too much. The panel needs to be able to ask.
    /// </remarks>
    public bool IsOverstretched(Workplace workplace) =>
        Zones.WorkGroundTiles(workplace.Id) > WorkGroundAllowanceFor(workplace);

    /// <summary>
    /// Why this workplace's ground is more than its hands can keep, in a sentence — or
    /// <c>null</c> when it is not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ ONE DOOR FOR THE SENTENCE (D147's shape, and D145's rule).</b> The brush says it
    /// once per stroke (D42) and the panel says it for as long as the state lasts, and those
    /// are two places, so the sentence is written here rather than in either of them. A
    /// warning composed twice is a warning that can disagree with itself — which is D148's
    /// bug, one panel over.
    /// </para>
    /// <para>
    /// <b>⭐ AND A FARM SAYS IT DIFFERENTLY, WHICH IS THE POINT</b> (Joe, 2026-08-22:
    /// *"at this size you dont have enough farmers to utilize the land — add more farmers or
    /// make your field smaller"*). The old sentence was written for a forester and a farm
    /// inherited it word for word: *"…and 1 pair of hands to keep them — enough for 24. The
    /// rest will go untended."* **Untended is the wrong word for a field**, and not merely a
    /// stylistic one: since D167 a farm sows only what its hands can bring in
    /// (<see cref="HarvestOneFarmCanBringIn"/>, the same headcount this allowance is read
    /// from), so the surplus is not tended badly — <b>it is never sown at all</b>. Saying
    /// *fallow* is the true word and it is also the one that makes the remedy obvious.
    /// </para>
    /// <para>
    /// <b>⛔ IT NAMES THE TWO REMEDIES AND REFUSES NOTHING</b> (D86, D43, and Joe saying so
    /// outright: *"which the user can choose to ignore and 'waste' land if they want"*).
    /// Painting big and hiring afterwards is an ordinary way to play; wasting land on purpose
    /// is a decision the player is allowed to make. This is a sentence, never a gate.
    /// </para>
    /// </remarks>
    public string? OverstretchedNote(Workplace workplace)
    {
        ArgumentNullException.ThrowIfNull(workplace);

        if (!IsOverstretched(workplace))
        {
            return null;
        }

        string name = Capitalised(workplace.Name);
        int tiles = Zones.WorkGroundTiles(workplace.Id);
        int allowance = WorkGroundAllowanceFor(workplace);
        int hands = workplace.WorkerIds.Count;

        if (workplace.Kind == JobKind.Farmer)
        {
            return hands == 0
                ? $"{name} is {tiles} tiles of field with nobody farming it, so none of it "
                  + "will be sown. Put a farmer on, or paint a smaller field."
                : $"{name} is {tiles} tiles of field and {Hands(hands)} can sow "
                  + $"{allowance} of them. The other {tiles - allowance} will lie fallow — "
                  + "put another farmer on, or paint a smaller field.";
        }

        return hands == 0
            ? $"{name} has {tiles} tiles and nobody working it, so none of it will be kept. "
              + $"Put a forester on, or paint less — one pair of hands keeps "
              + $"{TilesOneWorkerKeeps(workplace.Kind)}."
            : $"{name} has {tiles} tiles and {Hands(hands)} to keep them — enough for "
              + $"{allowance}. The other {tiles - allowance} will go untended — put another "
              + "forester on, or paint less.";
    }

    /// <summary>"1 pair of hands" / "2 pairs of hands", so the sentences read as English.</summary>
    private static string Hands(int hands) =>
        hands == 1 ? "1 pair of hands" : $"{hands} pairs of hands";

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
    /// <summary>Whether this workplace could be given this tile — pure (D198).</summary>
    /// <remarks>
    /// The preview's door and the paint's — see <see cref="CanPaintResidential"/>. <b>The
    /// overstretch warning is deliberately not here</b>: it is about the workplace's total
    /// ground rather than about this tile, so it belongs to the stroke rather than to the ghost,
    /// and <see cref="OverstretchedNote"/> stays its single author.
    /// </remarks>
    public PlacementVerdict CanPaintWorkGround(Workplace workplace, GridPos tile)
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

        int owner = Zones.WorkGroundOwner(tile);
        if (owner != 0 && owner != workplace.Id)
        {
            Workplace? other = FindWorkplace(owner);
            return PlacementVerdict.No(
                other is null
                    ? "That ground is already spoken for."
                    : $"That ground belongs to {other.Name}.");
        }

        return PlacementVerdict.Fine;
    }

    public PlacementVerdict PaintWorkGround(Workplace workplace, GridPos tile)
    {
        ArgumentNullException.ThrowIfNull(workplace);

        PlacementVerdict verdict = CanPaintWorkGround(workplace, tile);
        if (!verdict.Allowed)
        {
            return verdict;
        }

        Zones.SetWorkGround(tile, workplace.Id);

        // ⭐ A FARM BREAKS THE GROUND IT IS GIVEN, AND THE PLAYER SEES THE FIELD APPEAR.
        //
        // The one thing a farm's brush does that a forester's does not, and it is here rather
        // than in the farmer's work for a legibility reason (§1.1): paint a field in autumn and
        // nothing would happen until the following spring, so the player would have no way to
        // tell a farm they had given ground from one they had not.
        //
        // ⚠️ It is not load-bearing, deliberately. Sowing takes `Grass` as readily as `Field`
        // (`IsSowable`), so a tile this could not plough — one still under trees when the brush
        // went over it — joins the field the moment the laborers clear it, with no second rule
        // to remember. The plough is what the player can see; the sowing is what is true.
        if (workplace.Kind == JobKind.Farmer)
        {
            Plough(tile);
        }

        // The sentence is `OverstretchedNote`'s, not this method's, so the brush and the
        // building's own panel can never describe the same state two different ways.
        return OverstretchedNote(workplace) is string note
            ? PlacementVerdict.Yes(note)
            : PlacementVerdict.Fine;
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
    /// <summary>Whether this tile would take harvest paint — pure (D198).</summary>
    /// <remarks>
    /// <b>The preview's door, and the paint's</b> — see <see cref="CanPaintResidential"/> for why
    /// the split exists. <b>This is the brush where it matters most</b>: the mode is a filter
    /// (D90), so half the tiles under a stroke routinely refuse it, and *"the brush is set to
    /// fell trees and that is a stone seam"* is a sentence the player should be able to see
    /// coming rather than discover by clicking.
    /// </remarks>
    public PlacementVerdict CanPaintHarvest(
        GridPos tile, HarvestBrush brush = HarvestBrush.Everything)
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

        Goods? takes = WhatTheBrushTakes(brush);
        return takes is not null && standing.Value != takes.Value
            ? PlacementVerdict.No(
                $"The brush is set to {Describe(brush)}, and that is {Describe(standing.Value)}.")
            : PlacementVerdict.Fine;
    }

    /// <remarks>
    /// ⭐ <b>THE MODE IS A FILTER AND IS THEN FORGOTTEN</b> (D90, Joe's call of two). A marked
    /// tile is simply marked; what a laborer gets from it is whatever is standing there. So
    /// *"clear the stone and leave the wood"* works by the wood never taking the paint, rather
    /// than by storing three layers and letting a tile be marked for a good it does not have.
    /// <para>
    /// <b>The whole test lives in <see cref="CanPaintHarvest"/></b>, so the preview under the
    /// cursor and the paint under the click are the same answer (D198).
    /// </para>
    /// </remarks>
    public PlacementVerdict PaintHarvest(GridPos tile, HarvestBrush brush = HarvestBrush.Everything)
    {
        PlacementVerdict verdict = CanPaintHarvest(tile, brush);
        if (!verdict.Allowed)
        {
            return verdict;
        }

        Zones.SetHarvest(tile, true);
        return verdict;
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

    // ⭐ Was a switch naming three goods; the words live in the row now (D210). The fallback to
    // the good's own name is preserved inside `SourceNameOf`, so every sentence this writes is
    // unchanged.
    private string Describe(Goods goods) => GoodsCatalog.SourceNameOf(goods);

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
    /// <para>
    /// <b>One question, so a new harvestable terrain is answered here and nowhere else.</b>
    /// Forest, stone seams and iron seams — <c>TerrainRules.Yields</c> is the list. This is
    /// deliberately the same shape as <c>TerrainRules.IsPassable</c> — the seam D76 spent
    /// five instalments learning to recognise.
    /// </para>
    /// <para>
    /// ⚠️ <b>It said <em>"only forest today; stone and iron are D84's finite deposits and land
    /// next"</em> until D211</b>, and that stopped being true the day the seams shipped. It is
    /// recorded rather than quietly deleted because the stale sentence was read as evidence
    /// while `goods-catalog.md §4.0` was working out whether a villager could reach a seam at
    /// all — <b>a comment that lies is worse than no comment, because it is believed at exactly
    /// the moment somebody is orienting.</b>
    /// </para>
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
    public GridPos? NearestHarvest(GridPos from) => NearestHarvest(from, out _);

    /// <summary>
    /// The nearest tile to clear, and — when there is none — <b>the good a limit held back</b>.
    /// </summary>
    /// <remarks>
    /// <b>The out-parameter is for the sentence, not for the decision</b> (D212, METHODOLOGY §4:
    /// every refusal writes its own reason). A laborer who walks past a painted seam because the
    /// village already has enough stone must be able to say so, and working that out afterwards
    /// would mean a second scan of the painted tiles for every idle adult on every tick. This
    /// scan already knows.
    /// </remarks>
    public GridPos? NearestHarvest(GridPos from, out Goods? heldBackBy)
    {
        heldBackBy = null;
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

        // ⭐ A FOOTPRINT SOMEBODY IS WAITING ON COMES FIRST, BECAUSE NEAREST-FIRST NEVER GETS
        // TO IT. D100 paints a marked building's tile for harvest and promises *the village
        // clears the ground, the player does not have to*; D127 then made the paint a standing
        // instruction whose wood grows back, so every tile nearer than the footprint returns as
        // work before the footprint is ever reached. Measured on the shipped opening: a
        // gatherer's hut marked eight tiles out in real woodland is still standing on Forest
        // after forty years, while the panel says "the ground it stands on is still being
        // cleared" — a sentence that was never going to come true.
        //
        // ⚠️ WALKED FROM THE BUILDINGS, NOT FROM THE PAINT, and that is a cost decision rather
        // than a style one. Asking "is a building waiting on this tile?" inside the scan below
        // is the whole workplace list per painted tile per idle adult per tick, which is the
        // ruin this method's own comment already warns about. A village has a handful of
        // unraised buildings and hundreds of painted tiles, so the short list is the one to
        // walk.
        GridPos? blocked = NextFootprintToClear(from);
        if (blocked is not null)
        {
            return blocked;
        }

        GridPos? best = null;
        int bestCost = int.MaxValue;
        GridPos? needed = null;
        int neededCost = int.MaxValue;

        // ⭐ WHAT THE VILLAGE HAS ENOUGH OF, ASKED ONCE (D212). A limit is a ceiling on
        // production (D62), and clearing painted ground is production — it was simply the one
        // producer nobody had wired the control to.
        //
        // ⚠️ COMPUTED HERE RATHER THAN PER TILE, and that is not tidiness. `MayTake` sums a good
        // across every store that accepts it, and the loop below runs over every painted tile
        // for every idle adult on every tick — the exact ruin this method's own comments above
        // are about. One pass over the goods, then an array lookup per tile.
        //
        // ⛔ AND `AnySet` IS THE EARLY-OUT THAT KEEPS THE FEATURE FREE. A village that has never
        // opened the control pays one boolean, which is the argument the sparse hashing and the
        // `HarvestTiles == 0` line above both make: a feature nobody has switched on should cost
        // nothing at all. It is also why no golden moves for this — `null` is the default and
        // means *"the player has not said"*.
        bool[]? enoughOf = null;
        if (StockLimits.AnySet)
        {
            enoughOf = new bool[GoodsCatalog.Count];
            for (int g = 0; g < enoughOf.Length; g++)
            {
                enoughOf[g] = !MayTake((Goods)g);
            }
        }

        // ⭐⭐ WHAT A BUILDING IS WAITING ON COMES BEFORE WHAT IS MERELY NEAREST (D215).
        //
        // **This is `NextFootprintToClear`'s exception, one good over, and its comment already
        // wrote the reason: *"nearest-first never gets to it."*** Stone sits on a ring fourteen
        // tiles out by design (*"STONE NEAR, IRON FAR"*), while a village paints its trees on its
        // doorstep and the wood **grows back** (D126) — so there is always something cheaper to
        // walk to, for ever. Measured on the shipped opening with both painted: **one seam of
        // four cleared in five years and not one stone reaching a store**, while the huts that
        // needed three of it stayed sites and everybody froze.
        //
        // ⚠️ ONLY WHEN NOTHING IN STORE CAN SERVE IT. If a shed already holds the stone, a
        // builder fetches it and this is not clearing work at all — the rule is *"the village
        // cannot otherwise get this"*, not *"a site wants this"*, which would send laborers to
        // the rock every time a granary was marked.
        bool[]? waitedOn = null;
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Construction is not { IsFinished: false } plan
                || !GroundIsClearAt(Workplaces[i].Position))
            {
                continue;
            }

            for (int m = 0; m < plan.Recipe.Materials.Count; m++)
            {
                Goods wants = plan.Recipe.Materials[m].Goods;
                if (plan.StillNeeded(wants) <= 0 || AnyStoreHolding(wants))
                {
                    continue;
                }

                waitedOn ??= new bool[GoodsCatalog.Count];
                waitedOn[(int)wants] = true;
            }
        }

        // ⭐ THE PAINTED TILES THEMSELVES, NOT THE WHOLE VALLEY (`forests-and-gathering.md`).
        // This walked all 9,600 tiles, guarded by the early-out above — which was enough while
        // almost no village painted anything. A WOODED VALLEY BROKE THAT: every house the
        // village sites now lands on trees and paints itself for clearing (D100), so the guard
        // stopped firing for anybody and the full scan came back. Measured: the twelve-seed arm
        // went from about three minutes to six.
        //
        // The list is in map order, so ties break exactly as they did and no golden moves for
        // what is meant to be a pure speed-up. Copied first because un-painting a spent tile
        // edits the list underneath us.
        var painted = new List<int>(Zones.PaintedHarvest);

        for (int i = 0; i < painted.Count; i++)
        {
            GridPos at = Zones.PositionOf(painted[i]);

            // ⭐ SKIPPED, NOT UN-PAINTED (Joe, D127). Empty painted ground used to have its
            // paint taken off here, on the reasoning that a tree already gone stops being
            // work. **That was true only while nothing grew back.** With regrowth, a bare
            // painted tile is not finished work — it is work that is waiting, and the wood
            // will be standing on it again within the year.
            //
            // So the village passes it over this tick and comes back when there is
            // something there. A sapling answers no here too, which is exactly right: it is
            // left to grow up rather than cut down the season it appears.
            if (!HasSomethingToHarvest(at))
            {
                continue;
            }

            // ⭐ AND IT IS LEFT STANDING WHEN THE VILLAGE HAS ENOUGH (D212). Skipped, never
            // un-painted — the rule D127 wrote three paragraphs up: the paint is a standing
            // instruction, so a seam the village is currently full of is *work that is waiting*
            // and it comes back the moment the stores are spent down. The player raises the
            // limit or spends the stone; the village does not forget what they asked for.
            //
            // ⛔ THE FOOTPRINT BRANCH ABOVE RETURNS BEFORE THIS, AND MUST. A building's ground
            // is cleared whatever the limit says, or the village deadlocks on its own
            // instruction: the site waits on the ground, the ground waits on the limit, and the
            // limit waits on nothing at all. Guarded by
            // `HarvestLimitTests.GroundABuildingWaitsOnIsClearedEvenAtTheLimit`.
            if (enoughOf is not null)
            {
                Goods standing = TerrainRules.Yields(Map.TerrainAt(at))!.Value;
                if (enoughOf[(int)standing])
                {
                    heldBackBy ??= standing;
                    continue;
                }
            }

            int cost = TravelCost.Cost(from, at);

            // A tile something is waiting on outranks every merely-nearer tile, and is still
            // chosen by cost among its own kind — so the village goes to the closest rock, not
            // to a random one.
            if (waitedOn is not null
                && waitedOn[(int)TerrainRules.Yields(Map.TerrainAt(at))!.Value])
            {
                if (cost < neededCost)
                {
                    needed = at;
                    neededCost = cost;
                }

                continue;
            }

            if (cost < bestCost)
            {
                best = at;
                bestCost = cost;
            }
        }

        if (needed is not null)
        {
            heldBackBy = null;
            return needed;
        }

        // Only a refusal if it is the reason there is nothing to do. Somebody who walked past a
        // capped seam on the way to a tree is not being held back by anything.
        if (best is not null)
        {
            heldBackBy = null;
        }

        return best;
    }

    /// <summary>
    /// The tile the next building in the queue is waiting on, or null if none is blocked (D100).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both kinds of waiting building, because both stall the same way.</b> A free building
    /// sits in <see cref="_waitingOnTheGround"/> and a costed one is a
    /// <see cref="ConstructionSite"/> that <see cref="NextBuildableSite"/> refuses while
    /// <see cref="GroundIsClearAt"/> is false — different machinery, one symptom.
    /// </para>
    /// <para>
    /// <b>⭐ THE BUILD QUEUE'S ORDER, NOT THE NEAREST ONE (Joe, D157).</b> The first version of
    /// this took the nearest blocked footprint, which is a second ordering over the same list —
    /// and <see cref="NextToBuild"/>'s own comment names *two orderings that must agree* as the
    /// shape of half the bugs in this project. So clearing defers to building: the ground gets
    /// cleared in the order the village will actually raise things, rank then id, exactly as
    /// <see cref="BuildQueue"/> sorts. A player who moves a site up the list moves its clearing
    /// with it, which is the whole point of the list.
    /// </para>
    /// <para>
    /// <b>Free buildings come before the queue, because they are not in it and everything else
    /// waits on them.</b> A pile and a builder's hut cost nothing but the ground (D96, D108) —
    /// and until the pile stands there is nowhere to put a felled log, so clearing anything
    /// else first produces timber the village cannot see (D95). Marking order among them, which
    /// is the order they were added.
    /// </para>
    /// <para>
    /// <b>The paint is still required</b>, so this is a change of priority and not of scope:
    /// <see cref="Mark"/> puts it on, and a player who deliberately takes it off is telling
    /// the village something. What moves is only which painted tile is taken first.
    /// </para>
    /// </remarks>
    private GridPos? NextFootprintToClear(GridPos from)
    {
        for (int i = 0; i < _waitingOnTheGround.Count; i++)
        {
            GridPos at = _waitingOnTheGround[i].Position;
            if (NeedsClearing(at) && TravelCost.CanReach(from, at))
            {
                return at;
            }
        }

        Workplace? head = null;
        for (int i = 0; i < Workplaces.Count; i++)
        {
            Workplace candidate = Workplaces[i];

            // ⚠️ Reachable FROM THE VILLAGER, not from the village. The queue asks whether the
            // settlement can get there at all; this asks whether the person being sent can, and
            // a laborer who cannot walk to the head of the queue must fall through to work they
            // can reach rather than stand still.
            if (candidate.Construction is not { IsFinished: false }
                || !NeedsClearing(candidate.Position)
                || !TravelCost.CanReach(from, candidate.Position))
            {
                continue;
            }

            if (head is null
                || candidate.EffectiveQueueRank < head.EffectiveQueueRank
                || (candidate.EffectiveQueueRank == head.EffectiveQueueRank
                    && candidate.Id < head.Id))
            {
                head = candidate;
            }
        }

        return head?.Position;

        bool NeedsClearing(GridPos at) => Zones.IsHarvest(at) && HasSomethingToHarvest(at);
    }

    /// <summary>
    /// Take what is standing on a tile: the ground is cleared and the goods come out.
    /// </summary>
    /// <remarks>
    /// <b>The tile is spent</b> — this is D84's deposit rule, and the difference between
    /// the brush and the forester's hut in one method. Terrain goes through
    /// <see cref="SetTerrain"/> so the routing cache hears about it.
    /// <para>
    /// <b>⭐ THE PAINT STAYS ON (Joe, D127).</b> It used to come off here, *because the job
    /// is done* — and with regrowth the job is not done, it is due again. A painted patch is
    /// now **a standing instruction rather than a one-off order**: the wood grows back and
    /// the village fells it again, indefinitely, which turns the harvest brush from a queue
    /// of chores into something closer to a coppice the player has designated.
    /// </para>
    /// <para>
    /// <b>Only the player takes paint off</b>, with <em>Unmark</em>. That is the whole of
    /// Joe's rule — <em>"it's up to the user to clear the paint if the area is empty"</em> —
    /// and it is the same shape as every other zone in this game: the brush says what the
    /// player wants, and the village keeps doing it until told otherwise.
    /// </para>
    /// </remarks>
    public (Goods Goods, int Amount) Harvest(GridPos tile)
    {
        Goods? yields = TerrainRules.Yields(Map.TerrainAt(tile));
        if (yields is null)
        {
            return (Goods.Logs, 0);
        }

        SetTerrain(tile, Terrain.Grass);

        // ⭐ One number per kind of ground, and the terrain is what says which — a new harvestable
        // kind is a row in TerrainRules.Yields and a row here, not a fifth place to remember.
        // This comment asked for exactly the change D210 made: the three loose config keys it used
        // to read were used by this switch and by nothing else in the codebase.
        int amount = GoodsCatalog.YieldPerTileOf(yields.Value);

        // ⚠️ COPPICING IS A TECHNIQUE ABOUT WOOD, AND THIS METHOD IS NOT ABOUT WOOD — it hands out
        // whatever the ground yields, which is stone from a seam and iron from a deposit as
        // readily as logs from a stand. **Applying the forester's technique unconditionally here
        // would have made a master forester improve the village's QUARRY**, silently, with nothing
        // to see but a number that was slightly too good. Asked of the good rather than of the
        // caller, because the caller is the harvest brush and it does not know which trade it is
        // standing in.
        if (yields.Value == Goods.Logs)
        {
            amount = YieldWithTechnique(JobKind.Forester, amount);
        }

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
    /// <summary>
    /// Tell a store whether it will take a kind of goods. The one door in (D141).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Joe: <i>"user should be able to set which materials are stored in which buildings."</i>
    /// Routed through the world rather than set on the building, for the reason
    /// <see cref="SetTerrain"/> is (D85): this is hashed state, and state the view can poke
    /// directly is state nothing can guard.
    /// </para>
    /// <para>
    /// <b>⚠️ IT NARROWS ONLY.</b> Asking a granary to take logs is refused rather than obeyed —
    /// what a kind can hold is the model (D32), not a preference. So the verdict says no, and
    /// says why, instead of producing a granary full of timber.
    /// </para>
    /// </remarks>
    public PlacementVerdict SetStoreAccepts(StoreBuilding store, Goods goods, bool accepted)
    {
        ArgumentNullException.ThrowIfNull(store);

        // Start from "everything this kind can hold", so the first click narrows rather than
        // wiping. A mask of zero means no opinion, and turning one good off has to leave the
        // others on — otherwise the first click would empty the building.
        long mask = store.AllowedGoods;
        if (mask == 0)
        {
            for (int g = 0; g < GoodsCatalog.Count; g++)
            {
                if (store.CanEverHold((Goods)g))
                {
                    mask |= 1L << g;
                }
            }
        }

        // The player has now spoken, and that has to be recorded separately from WHAT they
        // said — otherwise switching off the last good lands back on zero, which means "no
        // opinion", and the store silently starts accepting everything again.
        mask |= StoreBuilding.Spoken;

        long bit = 1L << (int)goods;

        if (accepted)
        {
            // The kind is the authority on what is possible, and the player is not.
            if (!store.CanEverHold(goods))
            {
                return PlacementVerdict.No(
                    $"{store.Name} is a {store.Kind.ToString().ToLowerInvariant()}, and a "
                    + $"{goods.ToString().ToLowerInvariant()} is not something it can hold.");
            }

            mask |= bit;
        }
        else
        {
            mask &= ~bit;
        }

        store.AllowedGoods = mask;

        // Said once, when it is set, rather than nagged every tick — D42's rule.
        if (mask == StoreBuilding.Spoken)
        {
            return PlacementVerdict.Yes(
                $"{store.Name} will take nothing now. Anything carried to it will be set down "
                + "on the ground instead.");
        }

        return PlacementVerdict.Yes(string.Empty);
    }

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

        // ⭐ One of the two places that carried these three words; the other was `Stockpile.Name`,
        // with identical arms. D148 and D188's finding in code (D210).
        string name = GoodsCatalog.NameOf(goods);

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

        // ⚠️ A tile that stops being a sapling stops being a YOUNG one (D220) — felled, built
        // on, or matured. Cleared here, at the one door terrain changes by (D85), so the bit
        // cannot be left behind for whatever occupies the tile next.
        if (terrain != Terrain.Sapling)
        {
            Map.SetYoungSapling(position, false);
        }

        bool routeAffecting = TerrainRules.IsPassable(before) != TerrainRules.IsPassable(terrain);
        if (routeAffecting)
        {
            TravelCost.Forget();
        }

        // ⭐ AND EVERY RING'S TREE COUNT IS NOW STALE (`forests-and-gathering.md`). One
        // integer, bumped through the one door terrain changes by (D85), which is the same
        // hook `TravelCost.Forget()` hangs off and for the same reason: hooking `Harvest`
        // instead would be identical today and wrong the day anything else clears ground.
        //
        // A generation counter rather than a sweep over the workplaces, because felling
        // happens several times a year and there is no reason for one tile to cost a walk
        // over every hut in the village.
        _terrainGeneration++;
        // Every reader of terrain-derived cache is stale now, including the view's
        // (`Minimap` bakes the valley into a texture rather than redrawing 9,600 tiles a
        // frame). It reads the same counter the hut rings do, so there is one answer to
        // "has the ground changed?" and not two.

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

    /// <summary>
    /// Un-paint a tile, and pull down the house standing on it. Returns the household turned out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ THE BRUSH IS THE ONLY CONTROL HOUSING HAS, AND THAT IS NOW TRUE IN BOTH DIRECTIONS</b>
    /// (Joe, 2026-08-26): *"houses should not be relocatable. The way to demolish a house is to
    /// 'unpaint' the residential area underneath. And then the user paints another area to
    /// 'relocate' houses."* D42 settled that the player paints the neighbourhood and the sim picks
    /// the tile — so **the removal control has to be the brush too**, or housing would be the one
    /// thing you place with one tool and unplace with another. <b>A house is the one building the
    /// player never sited, so it is the one building they should not have to move by hand.</b>
    /// </para>
    /// <para>
    /// <b>⛔ THIS OVERTURNS WHAT THIS METHOD USED TO SAY, AND THE OBJECTION IT MADE WAS RIGHT:</b>
    /// <em>"Erasing is about where the village may build next, not a demolition order. Pulling
    /// houses down because somebody adjusted a brush would be a cruel reading of an undo."</em>
    /// **That is correct about an accident and wrong about an intent** — so the difference is made
    /// visible in the view rather than argued away here: erasing over occupied ground warns, names
    /// how many households it would turn out, and takes a second deliberate action (the arming
    /// pattern the demolish brush already uses).
    /// </para>
    /// <para>
    /// <b>⭐ "Relocating" a neighbourhood then needs no new mechanism at all</b> — unpaint here,
    /// paint there, and <c>HouseholdSystem.HouseTheRoofless</c> re-sites the family on whatever
    /// painted ground remains, exactly as it does for a new couple. **The timber is refunded at
    /// <c>demolition_returns_percent</c> like any other demolition**, so moving a neighbourhood
    /// costs half its houses — which is what keeps it a decision rather than a free redraw.
    /// </para>
    /// </remarks>
    public Household? EraseResidential(GridPos tile)
    {
        Zones.SetResidential(tile, false);

        Household? turnedOut = HouseholdAt(tile);
        if (turnedOut is null)
        {
            return null;
        }

        turnedOut.HomePosition = null;

        string recovered = ReturnToStore(
            tile, RefundFor(BuildingRecipe.For(BuildingKind.Home, Config)));

        Narrate($"The {turnedOut.Name} household's house at {tile} was pulled down when the "
            + $"ground was unpainted — {recovered} went back to store. "
            + $"{Clock.SeasonAndYear()}.");

        return turnedOut;
    }

    /// <summary>
    /// Move a building that already stands to another tile — <b>a builder's job, not a teleport</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ THE REMEDY JOE'S CONDITION REQUIRES</b> (2026-08-26): *"if we are going to allow the
    /// sim to place it, then we need to add a 'relocate' function for all buildings."* **The sim may
    /// only impose a placement if the player can undo it** — `§0.1`'s *recoverable by design*
    /// applied to layout, and the reason a gifted building is a gift rather than a trap.
    /// </para>
    /// <para>
    /// ⛔ <b>NOT HOUSES</b> (D228). Housing is the brush's business in both directions: unpaint the
    /// ground and the house comes down, paint elsewhere and the family rebuilds. <b>A house is the
    /// one building the player never sited, so it is the one they never have to move by hand.</b>
    /// </para>
    /// <para>
    /// <b>⭐ Validated by <see cref="CanBuildAt"/>, so every placement rule already written applies
    /// unchanged</b> — the water, the reachability, the *"something already stands there"*, the farm's
    /// distance warning, the library's literacy gate. *No second opinion about what a legal tile is,
    /// which is the mistake `Household.ChooseSite` made for a phase (D111).*
    /// </para>
    /// </remarks>
    public PlacementVerdict MarkRelocation(GridPos from, GridPos to)
    {
        // ⚠️ THE HOUSE IS ASKED ABOUT FIRST, AND THE ORDER IS THE WHOLE ANSWER. A house is not in
        // the stores, the libraries or the workplaces — it is a position on a household — so
        // `WhatStandsAt` returns null for one, and asking it first told the player *"there is
        // nothing there to move"* while they were pointing at somebody's home. **A wrong sentence,
        // not a wrong outcome**, which is this project's hardest class of bug to notice.
        if (HouseholdAt(from) is not null)
        {
            return PlacementVerdict.No(
                "A house is not moved by hand. Unpaint the ground under it and paint somewhere "
                + "else, and the family will rebuild there.");
        }

        BuildingKind? kind = WhatStandsAt(from);
        if (kind is null)
        {
            return PlacementVerdict.No("There is nothing there to move.");
        }

        // ⛔ A FULL STORE CANNOT BE CARRIED, AND THIS IS THE SENTENCE THAT SAYS SO RATHER THAN A
        // DISABLED CONTROL (D43). Joe named it as the second half of the feature: *"storage
        // buildings must be 'emptied' first."*
        if (StoreAt(from) is StoreBuilding store && store.Store.Held > 0)
        {
            return PlacementVerdict.No(
                $"There are {store.Store.Held} goods inside {store.Name}. Empty it first, and "
                + "the village will carry them to the other stores.");
        }

        PlacementVerdict verdict = CanBuildAt(kind.Value, to, alreadyStanding: true);
        if (!verdict.Allowed)
        {
            return verdict;
        }

        BuildingRecipe recipe = BuildingsCatalog.RecipeOf(kind.Value);

        // ⭐ WORK BUT NO MATERIALS — the timber and stone walk over with the crew. A relocation
        // that also charged for the building would be a demolition and a rebuild wearing one name.
        RaiseSiteFor(
            kind.Value,
            to,
            $"{NameOfWhatStandsAt(from)} (being moved)",
            new BuildingRecipe(recipe.WorkTicks),
            forHouseholdId: 0,
            movingFrom: from);

        Narrate($"{NameOfWhatStandsAt(from)} is being moved to {to}. {Clock.SeasonAndYear()}.");
        return verdict;
    }

    /// <summary>
    /// Move whatever stands on one tile to another. False if there was nothing left to move.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE SAME BUILDING ARRIVES, WHICH IS THE WHOLE POINT.</b> Nothing is recreated, so a
    /// moved store keeps its name and its filter, a moved workplace keeps its workers and its
    /// painted ground, and <b>a moved library keeps its records</b> — the shelves are the building.
    /// </para>
    /// <para>
    /// ⚠️ <b>Painted work ground needs no thought here</b>, and that is worth saying because it
    /// looks like it should: <c>ZoneMap.WorkGround</c> stores the owning building's <em>id</em>, not
    /// its position, so a farm that moves is still the owner of its fields (D86, D118).
    /// </para>
    /// <para>
    /// ⚠️ <b>The travel-cost cache is forgotten</b>, because a building that moved is every route in
    /// the village answering a different question. That is the same invalidation D85 built for
    /// terrain, used for the same reason.
    /// </para>
    /// </remarks>
    private bool MoveWhatStandsAt(GridPos from, GridPos to)
    {
        if (StoreAt(from) is StoreBuilding store)
        {
            store.MoveTo(to);
            TravelCost.Forget();
            return true;
        }

        for (int i = 0; i < Libraries.Count; i++)
        {
            if (Libraries[i].Position == from)
            {
                Libraries[i].MoveTo(to);
                TravelCost.Forget();
                return true;
            }
        }

        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Position == from && !Workplaces[i].IsSite)
            {
                Workplaces[i].MoveTo(to);
                TravelCost.Forget();
                return true;
            }
        }

        return false;
    }

    /// <summary>What kind of building stands on a tile, or null.</summary>
    public BuildingKind? WhatStandsAt(GridPos tile)
    {
        if (StoreAt(tile) is StoreBuilding store)
        {
            return BuildingsCatalog.ThatStores(store.Kind);
        }

        for (int i = 0; i < Libraries.Count; i++)
        {
            if (Libraries[i].Position == tile)
            {
                return BuildingKind.Library;
            }
        }

        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Position == tile && !Workplaces[i].IsSite)
            {
                return JobsCatalog.WorksAt(Workplaces[i].Kind);
            }
        }

        return null;
    }

    /// <summary>What the building on a tile is called, or "it".</summary>
    private string NameOfWhatStandsAt(GridPos tile)
    {
        if (StoreAt(tile) is StoreBuilding store)
        {
            return store.Name;
        }

        for (int i = 0; i < Libraries.Count; i++)
        {
            if (Libraries[i].Position == tile)
            {
                return Libraries[i].Name;
            }
        }

        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Position == tile && !Workplaces[i].IsSite)
            {
                return Workplaces[i].Name;
            }
        }

        return "it";
    }

    /// <summary>The store standing on a tile, or null.</summary>
    public StoreBuilding? StoreAt(GridPos tile)
    {
        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            if (StoreBuildings[i].Position == tile)
            {
                return StoreBuildings[i];
            }
        }

        return null;
    }

    /// <summary>The household whose house stands on a tile, or null.</summary>
    public Household? HouseholdAt(GridPos tile)
    {
        for (int i = 0; i < Households.Count; i++)
        {
            if (Households[i].HomePosition == tile)
            {
                return Households[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Whether the village has run out of room to build and needs the player.
    /// </summary>
    /// <remarks>
    /// The other half of the brush (D42): the game says when a decision is due rather
    /// than expecting the player to notice. Reduce babysitting, do not add it (§1.2).
    /// </remarks>
    public bool NeedsMoreResidentialLand { get; internal set; }

    /// <summary>
    /// Whether work the village wants done currently has nobody on it (D47).
    /// </summary>
    /// <remarks>
    /// <b>An edge-detector, not a fact anybody reads.</b> It exists so the log line fires
    /// once when the state begins and once when it ends, rather than every tick — the same
    /// shape and the same reason as <see cref="NeedsMoreResidentialLand"/> beside it, and
    /// like that one it is <b>not hashed</b>: it is bookkeeping about narration, derived
    /// from workplaces and quotas that are hashed already.
    /// </remarks>
    public bool WorkIsGoingUndone { get; internal set; }

    /// <summary>
    /// Whether work was already seen unmanned at the previous labour pass.
    /// </summary>
    /// <remarks>
    /// The patience half of the pair above: a shortage is only worth a line if it survives
    /// one pass, because a season boundary produces one that does not. Not hashed, for the
    /// same reason as its neighbours.
    /// </remarks>
    public bool WorkSeenUndoneOnce { get; internal set; }

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

                // ⚠️ NOT WATER IS NOT THE SAME AS NOT CUT OFF BY WATER (D111). This painted
                // a diamond around the founding site skipping only water tiles — so in a
                // valley where the river runs close it painted the **far bank**, and
                // `ChooseSite` would then find a beautifully-scoring, permanently
                // unreachable home site there. That is how seed 11 froze a whole village
                // (D110): one house nobody could walk to sat at the head of the build queue
                // and starved every site behind it for a century.
                //
                // Asked of the shared cost field, which is the one place in this game that
                // knows the river goes round (§2.6). The starter zone is the village's own
                // land, so *reachable from the founding site* is exactly the right question.
                var tile = new GridPos(origin.X + dx, origin.Y + dy);
                if (Map.Contains(tile)
                    && Map.TerrainAt(tile) != Terrain.Water
                    && TravelCost.CanReach(origin, tile))
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
    public PlacementVerdict CanBuildAt(
        BuildingKind kind, GridPos position, bool alreadyStanding = false)
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

        // ⛔⛔ AN UNLOCK IS NOT A PLACEMENT RULE, AND CONFLATING THEM REFUSED A LIBRARY THE VILLAGE
        // ALREADY OWNED. Relocation validates its destination through this method — so a library
        // being carried across the village was told *"nobody here can write yet"* in a valley
        // whose granary had taught them, simply because the check lives on the wrong side of the
        // question. **"May the village have one of these at all?" and "is this tile legal?" are
        // different questions**, and only the second one is about a tile.
        //
        // ⭐ So it is skipped for a building that already stands. *You do not re-earn a building by
        // moving it.*
        //
        // ⛔ A REFUSAL WITH A REASON, NOT A GREYED-OUT BUTTON (D43). The library waits on literacy,
        // and literacy comes out of the granary — so the sentence says **what to do**, not merely
        // that the answer is no. *"You cannot build this yet"* would be the untraceable outcome
        // §1.1 forbids; naming the granary makes it a plan.
        //
        // ⛔⛔ ASKED OF THE ROW, NOT OF THE KIND, AND THAT WAS A BUG BEFORE IT WAS A PRINCIPLE.
        // The first version compared `kind == BuildingKind.Library` — **a switch on a building by
        // name, which is the exact thing `buildings-catalog.md` exists to delete** — and it fired
        // on `ModdedBuildingTests`' boathouse within one test run, because that fixture's building
        // holds id 10 too. *A modder's building was refused for want of literacy it had no use
        // for.*
        //
        // ⭐ And the data-driven rule is the better sentence anyway: **you must be able to write
        // before you can build somewhere to write things down.** Any building with shelves waits
        // on literacy, including one this sim has never heard of.
        if (alreadyStanding is false && BuildingsCatalog[kind]?.Shelves > 0 && !HasLiteracy)
        {
            return PlacementVerdict.No(FirstGranaryTick == 0
                ? "Nobody here can write yet. Keeping a granary's count is what teaches it — "
                    + "build one, and give it years."
                : "Nobody here can write yet. The granary's count has not been kept long enough.");
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

        // Legal, but perhaps unwise — and that is the player's call to make (D43). Two
        // things can be unwise about one tile, so they are collected rather than returned
        // from the first `if`: losing the distance warning because the ground also happened
        // to be sown would be the quieter half of a two-part mistake.
        string? standingCrop = WarningForBuildingOverACrop(position);

        int walk = TravelCost.Cost(village, position) / TravelCostField.BaseTileCost;
        int budget = VillageEconomy.MaxHomeToVillageTiles(Config);
        string? tooFar = walk > budget
            ? $"That is {walk} tiles from the village; it budgets {budget}. "
                + "People will spend their days walking to it."
            : null;

        string? longHaul = WarningForAFarmFarFromAStore(kind, position);

        if (standingCrop is null && tooFar is null && longHaul is null)
        {
            return PlacementVerdict.Fine;
        }

        return PlacementVerdict.Yes(
            string.Join(
                ' ',
                new[] { standingCrop, longHaul, tooFar }.Where(
                    static line => !string.IsNullOrEmpty(line))));
    }

    /// <summary>
    /// ⭐ A farm far from a store halves its own harvest, and until D194 nothing said so.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The distance warning above measures the wrong walk for a farmhouse.</b> It asks how
    /// far the building is from the <em>village</em>, which is about people's days; a farm's
    /// binding walk is to <b>the nearest store that takes food</b>, because that is where every
    /// armful past the first one goes (`crops-and-orchards.md §3.2a`). Measured, a farm ten
    /// ticks out brings in six tiles a hand against a well-sited farm's thirteen — <b>the
    /// single largest legible consequence in the farm, and it happened silently.</b>
    /// </para>
    /// <para>
    /// <b>⭐ AND IT IS THE LEVER THE PLAYER ACTUALLY HAS.</b> Thirteen tiles ten ticks from a
    /// granary is physically impossible — autumn is 120 ticks and it needs about 230 — so
    /// *"build a store near the fields"* is not advice, it is the only thing that works.
    /// </para>
    /// <para>
    /// <b>Warned, never refused</b> (D43, D86), and <b>only past the haul the economy budgets
    /// for</b>, so a farm beside the granary is not nagged — D42's one considered sentence
    /// rather than an alert the player learns to click past.
    /// </para>
    /// </remarks>
    private string? WarningForAFarmFarFromAStore(BuildingKind kind, GridPos position)
    {
        if (kind != BuildingKind.Farmhouse)
        {
            return null;
        }

        int haul = HaulWalkFrom(position);
        int budgeted = VillageEconomy.FieldHaulTicksBudgeted(Config);

        if (haul < 0)
        {
            return "There is no store here that takes food, so every armful of the harvest "
                + "will be set down where it stands.";
        }

        if (haul <= budgeted)
        {
            return null;
        }

        int derived = VillageEconomy.FieldTilesOneFarmerKeeps(Config);
        int share = ReapableShareAt(new Workplace
        {
            Store = NewStockpile(),
            Id = -1,
            Kind = JobKind.Farmer,
            Name = "ghost",
            Position = position,
            Capacity = 1,
        });

        int tiles = derived * share / 100;
        return $"That is a {haul}-tick walk to the nearest store that takes food. A farmer "
            + $"there brings in about {(tiles < 1 ? 1 : tiles)} tiles of crop against "
            + $"{derived} beside a store — build a store near the fields.";
    }

    /// <summary>
    /// ⭐ Building over a standing crop is allowed, and it is said out loud (Joe, D161).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Warned, not refused, and Joe ruled it that way.</b> The alternative was tempting — a
    /// sown field is a year of food and a house on it destroys the lot — but refusing would let
    /// a field <em>permanently block the village from housing itself</em>, and D42 already
    /// settled that the player picks the neighbourhood. So this is D43's pattern exactly: a
    /// decision with a consequence, stated, rather than an error.
    /// </para>
    /// <para>
    /// <b>A bare <see cref="Terrain.Field"/> says nothing</b>, because nothing is lost — the
    /// ground is ploughed and empty. Only a crop actually standing is worth a sentence, which
    /// is the restraint D147 applies to the idle ring: a marker that fires for everything is
    /// the always-on alert D42 and D123 deleted.
    /// </para>
    /// </remarks>
    private string? WarningForBuildingOverACrop(GridPos position) =>
        Map.TerrainAt(position) switch
        {
            Terrain.Sown =>
                "That ground is sown — building here loses this year's crop from it.",
            Terrain.Ripe =>
                "That ground is standing ripe — building here loses the harvest on it.",
            _ => null,
        };

    /// <summary>
    /// Mark out a building. It exists as a site, not a building, until somebody raises it.
    /// </summary>
    /// <returns>The verdict; the site is only created when it allows.</returns>
    /// <remarks>
    /// ⭐ <b>Building over a standing crop is allowed and said out loud</b> — see
    /// <see cref="WarningForBuildingOverACrop"/>.
    /// </remarks>
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
        if (recipe.TotalMaterials == 0 && recipe.WorkTicks == 0)
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
    /// <para>
    /// <b>⭐ AND THAT SENTENCE IS TRUE AS OF D111, WHICH IT WAS NOT WHEN IT WAS WRITTEN.</b>
    /// <c>ChooseSite</c> scored candidates with a ruler and only checked that a tile was not
    /// water — never that it was not <em>cut off by</em> water — so "reachable" was a claim
    /// nothing backed. It walks the shared cost field now, and the starter zone does too.
    /// The warning below is kept as the guard on that promise rather than removed as
    /// redundant: <b>it should never fire again, and that is exactly why it stays.</b>
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
    /// <summary>
    /// The first site in the queue that a builder could actually put a day's work into.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ D135, and it is head-of-line blocking.</b> <see cref="NextToBuild"/> answers *what
    /// is first*, which is what the player's queue means and what materials should chase. Every
    /// builder asked only that — so when the head was short of timber and no store had any,
    /// **every builder in the village stood still**, however many sites behind it were stocked
    /// and ready. Measured: *"a house — 30 logs delivered, 0 still wanted"* sat untouched for
    /// three years while the builders shuttled for the site in front of it.
    /// </para>
    /// <para>
    /// Joe watched the same thing and described it exactly: <i>"the builder shouldn't just sit
    /// at the building waiting."</i> His woodcutter's hut was *"Queue: 1st of 3"* at 12 of 25
    /// logs with *"Work: 0 of 40 ticks done"*, and two sites queued behind it.
    /// </para>
    /// <para>
    /// <b>⚠️ THIS DOES NOT REORDER THE QUEUE, and that distinction is D102's.</b> Marking a
    /// granary in the first spring once jumped it ahead of two houses and killed the village,
    /// so the queue decides <em>where scarce timber goes</em> and still does — fetching always
    /// serves the head. What this changes is only what a builder does with time they would
    /// otherwise spend standing next to a site they cannot advance. Priority over materials,
    /// not over labour.
    /// </para>
    /// </remarks>
    /// <summary>Whether anywhere in the village is holding this good right now.</summary>
    public bool AnyStoreHolding(Goods goods)
    {
        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            if (StoreBuildings[i].Accepts(goods) && StoreBuildings[i].Store[goods] > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The site a builder should serve right now — <b>the first in queue order they can
    /// actually advance</b> (D213).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔⛔ D135'S BUG, ARRIVING THROUGH A SECOND MATERIAL.</b> That decision gave a builder
    /// somewhere to go when the head of the queue was starved — *"the builder shouldn't just sit
    /// at the building waiting"* — and it asked <see cref="NextBuildableSite"/>, which only ever
    /// answers with a site that <b>already has everything</b> and is merely short of work. While
    /// timber was the only material that was nearly always true: the village makes logs, so a
    /// starved head was rare and short-lived.
    /// </para>
    /// <para>
    /// <b>Stone is not like that.</b> Nothing produces it until the player paints a seam, so
    /// *"the head wants something the village has not got"* is the NORMAL state of a fresh
    /// village — and the head then blocked every site behind it for ever. Measured before this
    /// existed: a played founding went to <b>0 alive, 4 frozen, no house ever built</b>, and a
    /// century of the shipped village finished at <b>0 alive</b> with four sites queued and 116
    /// logs in store. The houses were affordable the whole time and nobody could reach them.
    /// </para>
    /// <para>
    /// <b>⚠️ THE QUEUE IS STILL NOT REORDERED, which is D102's line and it holds.</b> This walks
    /// the same order <see cref="NextToBuild"/> does and prefers the head whenever the head can
    /// be advanced at all — so scarce timber still goes where the player pointed. What moves is
    /// only which site a builder serves when the head is waiting on something that does not
    /// exist yet.
    /// </para>
    /// <para>
    /// <b>⭐ ONE ORDERING, ASKED IN ONE PLACE.</b> <c>WorkTheSite</c> and <c>LoadMaterials</c>
    /// both ask this, so the site somebody walks to a store for and the site they carry the load
    /// back to cannot disagree — which is D157's rule about two orderings over one list being
    /// the shape of half the bugs in this project.
    /// </para>
    /// </remarks>
    public Workplace? NextSiteToServe()
    {
        Workplace? best = null;
        GridPos village = FirstHomeOrFoundingSite();

        for (int i = 0; i < Workplaces.Count; i++)
        {
            Workplace candidate = Workplaces[i];
            if (candidate.Construction is not { IsFinished: false } plan
                || !GroundIsClearAt(candidate.Position)
                || !TravelCost.CanReach(village, candidate.Position))
            {
                continue;
            }

            // Either it is paid for and merely owes work, or the next thing it wants is
            // standing in a store somewhere. Anything else is a site nobody can move today.
            if (plan.NextMaterialWanted() is Goods wanted && !AnyStoreHolding(wanted))
            {
                continue;
            }

            if (best is null
                || candidate.EffectiveQueueRank < best.EffectiveQueueRank
                || (candidate.EffectiveQueueRank == best.EffectiveQueueRank
                    && candidate.Id < best.Id))
            {
                best = candidate;
            }
        }

        return best;
    }

    public Workplace? NextBuildableSite()
    {
        Workplace? best = null;
        GridPos village = FirstHomeOrFoundingSite();

        for (int i = 0; i < Workplaces.Count; i++)
        {
            Workplace candidate = Workplaces[i];
            if (candidate.Construction is not { IsFinished: false, HasMaterials: true }
                || !GroundIsClearAt(candidate.Position)
                || !TravelCost.CanReach(village, candidate.Position))
            {
                continue;
            }

            if (best is null
                || candidate.EffectiveQueueRank < best.EffectiveQueueRank
                || (candidate.EffectiveQueueRank == best.EffectiveQueueRank
                    && candidate.Id < best.Id))
            {
                best = candidate;
            }
        }

        return best;
    }

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
        BuildingKind kind,
        GridPos position,
        string name,
        BuildingRecipe recipe,
        int forHouseholdId,
        GridPos? movingFrom = null)
    {
        Workplaces.Add(new Workplace
        {
            Store = NewStockpile(),
            Id = NextWorkplaceId(),
            Kind = JobKind.Builder,
            Name = $"{name} (building)",
            Position = position,
            Capacity = 0,
            Construction = new ConstructionSite(recipe)
            {
                Kind = kind,
                Name = name,
                ForHouseholdId = forHouseholdId,
                MovingFrom = movingFrom,
            },
        });

        // ⭐ The cost is a sentence the recipe writes, not a format string naming one good
        // (D213). "40 logs and 10 stone" comes out of the same method the panel uses, so the
        // village log and the inspector can never describe one building two ways — D148 and
        // D188's finding, which is what put the words on the row in the first place.
        Log(Logging.LogLevel.Info, "placement",
            $"{name} was marked out — {recipe.Describe(GoodsCatalog)} and "
            + $"{recipe.WorkTicks} ticks of work. {Clock.SeasonAndYear()}.");

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
        // ⛔ THE CART STAYS NAMED, AND IT IS THE EXEMPTION `buildings-catalog.md §2.3` RECORDS.
        // It is not a building — it is the wagon the founders arrive in — so no row claims it and
        // none should. It borrows the stockpile's recipe because that is the right refund for both
        // buildings nobody paid for: nothing.
        BuildingKind kind = building.Kind == StoreKind.Cart
            ? BuildingKind.Pile
            : BuildingsCatalog.ThatStores(building.Kind)
                ?? throw new ArgumentOutOfRangeException(
                    nameof(building), building.Kind, "That kind of store has no refund.");

        int held = building.Store.Held;
        IReadOnlyList<MaterialCost> back = RefundFor(BuildingRecipe.For(kind, Config));

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

        // ANYWHERE THAT TAKES IT, not a shed by name (D132). Asking for the kind
        // meant a refund vanished in a village that has only a storage pile — silently,
        // because `shed` was simply null and the logs went nowhere.
        string recovered = ReturnToStore(building.Position, back);

        Narrate(held > 0
            ? $"{building.Name} was pulled down — {recovered} recovered, and the {held} " +
              $"goods inside it were lost. {Clock.SeasonAndYear()}."
            : $"{building.Name} was pulled down — {recovered} recovered. {Clock.SeasonAndYear()}.");
    }

    /// <summary>
    /// Pull down a standing workplace — a hut the player has finished with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The gap `professions.md §7` listed and nothing had filled</b>, found by Joe trying
    /// to use it: *"I can't cancel/demolish a building that is under construction. Demolish
    /// says 'nothing there to pull down'."* The demolish tool only ever searched
    /// <see cref="StoreBuildings"/>, so **no hut and no construction site in the game could
    /// be removed at all** — every workplace the player has ever placed was permanent.
    /// </para>
    /// <para>
    /// <b>Instant, like the marking that put it there.</b> A hut is not unbuilt by anybody;
    /// it simply stops being. Making its removal a job would put a demolition errand in front
    /// of a player whose reason for pulling it down is usually that they misplaced it.
    /// </para>
    /// <para>
    /// <b>Refunded on what it cost, which is nothing for the free ones.</b> Half the recipe,
    /// the same rule stores get — and a builder's hut and a pile have a recipe of zero logs,
    /// so pulling one down pays nothing back. That is D98's free-timber press staying shut
    /// without a special case for it.
    /// </para>
    /// </remarks>
    public void Demolish(Workplace workplace)
    {
        ArgumentNullException.ThrowIfNull(workplace);

        if (workplace.Construction is not null)
        {
            CancelConstruction(workplace);
            return;
        }

        // ⚠️ A MARKET IS ONE BUILDING IN TWO LISTS (D36's seam), so pulling down its stall
        // and leaving its store standing would be half a demolition — a granary-sized hole
        // in the village that still holds goods and still shows on the map. The store's own
        // demolition already takes the stall with it, so that is the one to run.
        StoreBuilding? sameBuilding = null;
        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            if (StoreBuildings[i].Position == workplace.Position)
            {
                sameBuilding = StoreBuildings[i];
                break;
            }
        }

        if (sameBuilding is not null)
        {
            Demolish(sameBuilding);
            return;
        }

        BuildingKind kind = KindOf(workplace);
        IReadOnlyList<MaterialCost> back = RefundFor(BuildingRecipe.For(kind, Config));

        string name = workplace.Name;
        RetireWorkplace(workplace);

        // ANYWHERE THAT TAKES IT, not a shed by name (D132). Asking for the kind
        // meant a refund vanished in a village that has only a storage pile — silently,
        // because `shed` was simply null and the logs went nowhere.
        string recovered = ReturnToStore(workplace.Position, back);

        Narrate(back.Count > 0
            ? $"{Capitalised(name)} was pulled down — {recovered} recovered. "
                + $"{Clock.SeasonAndYear()}."
            : $"{Capitalised(name)} was pulled down. {Clock.SeasonAndYear()}.");
    }

    /// <summary>What pulling a building down hands back — a share of every material (D213).</summary>
    /// <remarks>
    /// <b>Priced off the recipe rather than off what was delivered</b>, which is what demolition
    /// has always done: a standing building has no memory of its own site. The share is
    /// <c>demolition_returns_percent</c>, applied per material so that a building costing two
    /// things gives a little of each back rather than all of one.
    /// </remarks>
    private IReadOnlyList<MaterialCost> RefundFor(BuildingRecipe recipe)
    {
        var back = new List<MaterialCost>();
        for (int i = 0; i < recipe.Materials.Count; i++)
        {
            int share = recipe.Materials[i].Amount * Config.DemolitionReturnsPercent / 100;
            if (share > 0)
            {
                back.Add(new MaterialCost(recipe.Materials[i].Goods, share));
            }
        }

        return back;
    }

    /// <summary>
    /// Put a refund back wherever will take it, and say what actually landed.
    /// </summary>
    /// <remarks>
    /// <b>Each material asked about separately</b>, for D144's reason one path over: a store the
    /// player has filtered may take the timber and refuse the stone, and a refund that assumed
    /// one destination for a two-material building would quietly destroy half of it.
    /// </remarks>
    private string ReturnToStore(GridPos where, IReadOnlyList<MaterialCost> back)
    {
        var landed = new List<MaterialCost>();
        for (int i = 0; i < back.Count; i++)
        {
            StoreBuilding? store = NearestStoreAccepting(
                where, back[i].Goods, static place => !place.Store.IsFull);

            int took = store?.Store.Receive(back[i].Goods, back[i].Amount) ?? 0;
            if (took > 0)
            {
                landed.Add(new MaterialCost(back[i].Goods, took));
            }
        }

        return landed.Count == 0
            ? "nothing"
            : new BuildingRecipe(0, landed.ToArray()).Describe(GoodsCatalog);
    }

    /// <summary>Which kind of building a standing workplace is, for pricing its refund.</summary>
    /// <remarks>
    /// Named rather than defaulted (D108). A workplace kind nobody has taught this about is a
    /// bug, not a woodcutter's hut — the default arm is exactly how six buildings came to be
    /// mis-priced and mis-named before anybody noticed.
    /// </remarks>
    // ⭐ Which building a trade staffs is `works_at` on the row now (D218). The throw stays:
    // a row may legally have no workplace — a laborer is "the villagers no job currently
    // wants" (D66) — and asking such a trade for its building is still a caller error.
    private BuildingKind KindOf(Workplace workplace) =>
        JobsCatalog.WorksAt(workplace.Kind)
        ?? throw new ArgumentOutOfRangeException(
            nameof(workplace), workplace.Kind, "That kind of workplace has no building.");

    /// <summary>Abandon a site that has not been finished; its delivered logs come back.</summary>
    public void CancelConstruction(Workplace site)
    {
        ArgumentNullException.ThrowIfNull(site);

        if (site.Construction is null)
        {
            throw new ArgumentException($"{site.Name} is not a construction site.", nameof(site));
        }

        IReadOnlyList<MaterialCost> back = site.Construction.Abandon();
        RetireWorkplace(site);

        // ANYWHERE THAT TAKES IT, not a shed by name (D132). Asking for the kind
        // meant a refund vanished in a village that has only a storage pile — silently,
        // because `shed` was simply null and the logs went nowhere.
        string recovered = ReturnToStore(site.Position, back);

        Narrate($"{site.Construction.Name} was abandoned before it was built — " +
            $"{recovered} went back to store. {Clock.SeasonAndYear()}.");
    }

    /// <summary>Turn a finished site into the building it was always going to be.</summary>
    internal void Complete(Workplace site)
    {
        ConstructionSite plan = site.Construction!;

        // ⭐⭐ A RELOCATION MOVES WHAT ALREADY STANDS RATHER THAN RAISING ANYTHING NEW, which is
        // what keeps a moved building the SAME building — its name, its contents, its workers, its
        // records and its painted ground all travel with it because none of them are recreated.
        if (plan.MovingFrom is GridPos from)
        {
            RetireWorkplace(site);

            if (!MoveWhatStandsAt(from, site.Position))
            {
                // ⚠️ SAID OUT LOUD RATHER THAN SWALLOWED. The player can demolish the source while
                // the crew are still working, and a site that then raised a fresh building would
                // hand them a free one — a phantom nobody paid for.
                Narrate($"{plan.Name} was being moved, but there was nothing left to move. "
                    + $"{Clock.SeasonAndYear()}.");
                return;
            }

            Narrate($"{plan.Name} finished moving. {Clock.SeasonAndYear()}.");
            return;
        }

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

            // ⭐⭐ AND EVERYTHING ELSE IS THE ROW (`buildings-catalog.md §2`). This was six more
            // arms, each hand-writing the same four lines with a different trade and a different
            // capacity, and between them they held the second copy of the building↔trade relation:
            // `JobRow.WorksAt` said forager → gatherer's hut and this said gatherer's hut →
            // forager, with nothing checking that they agreed. **One relation, one direction.**
            //
            // ⭐ A GATHERER'S HUT IS A FORAGER'S WORKPLACE WITH A RING, and a farm is a forester's
            // hut with a different verb (Joe, `forests-and-gathering.md`, `crops-and-orchards.md
            // §3`). `JobKind` is REUSED rather than added beside — the same argument D96 made for
            // renaming `Logger` to `Forester`: a second kind means a second quota arm, a second
            // slot in the allocator's scarcity order, a second plural, a second behaviour branch
            // and a rule somewhere to stop the village staffing both. **That those three were the
            // same four lines all along is exactly why they collapse.**
            //
            // ⭐ THE STORES WERE NAMED RATHER THAN DEFAULTED (D108), and the reason survives the
            // collapse: this used to be a `default:` arm two silent defaults deep — an unrecognised
            // kind fell through to `RaiseStore`, whose own two switches made it a market with a
            // market's capacity. **A building with no row now throws, and says which.**
            default:
                RaiseFinished(plan.Kind, site.Position, plan.Name);
                break;
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
        // ⭐ THE STORE KIND IS A COLUMN NOW. Both of these were switches that named the market
        // rather than defaulting to it (D108) — they were the second and third silent defaults on
        // the path from `Complete`, and between them they would have turned any building kind
        // nobody had taught this method about into a market with a market's capacity.
        StoreKind storeKind = BuildingsCatalog.StoresAs(kind)
            ?? throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "That kind of building is not a store.");

        int capacity = CapacityOfTheStoreIn(kind);

        var building = new StoreBuilding
        {
            Catalog = GoodsCatalog,
            Id = NextStoreId(),
            Kind = storeKind,
            Name = name,
            Position = position,
            Store = new Stockpile(GoodsCatalog.Count) { Capacity = capacity },
        };

        StoreBuildings.Add(building);

        // ⭐ THE CLOCK ON WRITING STARTS HERE (D32, §7a). The first granary is the first thing in
        // this village whose job is *counting*, and literacy is what a well-run count eventually
        // produces. Recorded on the first one only — a second granary does not restart the years.
        if (storeKind == StoreKind.Granary && FirstGranaryTick == 0)
        {
            FirstGranaryTick = Tick == 0 ? 1UL : Tick;
        }

        return building;
    }

    /// <summary>
    /// Put a building into the world once it stands — <b>whatever its row says it is</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ ONE PLACE, BECAUSE THERE ARE TWO WAYS A BUILDING CAN ARRIVE</b> — finished by a
    /// builder, or laid out instantly because it costs nothing (D96, D108). Two copies of
    /// "what does this become?" is exactly how a kind comes to be born the wrong size in one of
    /// them and nobody notices for a phase; <c>StoreKind</c> has already taught this lesson five
    /// times (D76).
    /// </para>
    /// <para>
    /// <b>A row may be a store, a workplace, or both</b> — the market is deliberately both (D14,
    /// D36's seam). <b>A row that is neither is a building that does nothing</b>, refused at load
    /// by <c>SimConfig.ValidateBuildings</c> rather than silently raised here.
    /// </para>
    /// <para>
    /// ⚠️ <b>A home does not come through here.</b> Finishing one moves a family in — it is
    /// reasoning rather than a value, and `buildings-catalog.md §2.3` keeps it named in
    /// <see cref="Complete"/> for that reason.
    /// </para>
    /// </remarks>
    private void RaiseFinished(BuildingKind kind, GridPos position, string name)
    {
        BuildingRow row = BuildingsCatalog[kind]
            ?? throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "That kind of building has no row, so it cannot be raised.");

        if (row.Stores is not null)
        {
            RaiseStore(kind, position, name);
        }

        // ⭐ A library is a fourth thing a building can be (Phase 4 slice 2) — not a store, not a
        // workplace, not a home. It holds records, which is the only thing in this game that
        // outlives the person who made it.
        if (row.Shelves > 0)
        {
            Libraries.Add(new Library
            {
                Position = position,
                Name = name,
                Shelves = row.Shelves,
            });

            Narrate($"{Capitalised(name)} stands, with {row.Shelves} shelves waiting. "
                + $"{Clock.SeasonAndYear()}.");
        }

        if (BuildingsCatalog.EmployedBy(kind) is not JobKind trade)
        {
            return;
        }

        // A market with no seats is a store and nothing else — the gate was
        // `Config.MarketCapacity > 0` when this was a switch, and it means the same thing.
        int seats = SeatsIn(kind);
        if (seats <= 0)
        {
            return;
        }

        Workplaces.Add(new Workplace
        {
            // ⭐ The farmhouse is the only building with a buffer of its own today
            // (`crops-and-orchards.md §3.2a`), and `int.MaxValue` is what every other workplace
            // store has always had — `Stockpile`'s own default.
            Store = new Stockpile(GoodsCatalog.Count)
            {
                Capacity = row.LocalStoreCap > 0 ? row.LocalStoreCap : int.MaxValue,
            },
            Id = NextWorkplaceId(),
            Kind = trade,
            Name = name,
            Position = position,
            Capacity = seats,
            GatheringRadius = row.GatheringRadius,
        });
    }

    /// <summary>
    /// How much a store of this kind holds — <b>the row's number, or the economy's</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE EXEMPTION `buildings-catalog.md §2.2` NAMES ON THE RECORD, AND IT IS PRINCIPLED
    /// RATHER THAN A SHORTCUT: a STATED capacity is data; a DERIVED one is the survival floor, and
    /// the survival floor is <see cref="VillageEconomy"/>'s business (D16).</b> A granary is a box
    /// of a stated size (D219). A shed is <em>solved</em> — a horizon of households, the firewood
    /// they want, the logs to split it out of, a house's timber, floored at a granary — and typing
    /// that number into a row is exactly the move D16 exists to refuse.
    /// </para>
    /// <para>
    /// <b>⚠️ A modded building has no derivation to appeal to, so it must state a capacity</b>,
    /// which is the test of whether this exemption is honest: it covers what the game already
    /// solves for itself, never what a modder can reach. <c>SimConfig.ValidateBuildings</c> refuses
    /// a null capacity on any row but the three named here.
    /// </para>
    /// </remarks>
    private int CapacityOfTheStoreIn(BuildingKind kind)
    {
        if (BuildingsCatalog[kind]?.StoreCapacity is int stated)
        {
            return stated;
        }

        return kind switch
        {
            BuildingKind.Shed => VillageEconomy.ShedCapacity(Config),
            BuildingKind.Pile => VillageEconomy.PileCapacity(Config),
            BuildingKind.Market => VillageEconomy.MarketCapacity(Config),
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "That store states no capacity and the economy derives none."),
        };
    }

    /// <summary>
    /// How many work at a building of this kind — <b>the row's number, or the economy's</b>.
    /// </summary>
    /// <remarks>
    /// The other half of <see cref="CapacityOfTheStoreIn"/>'s exemption, and the same rule governs
    /// it: the woodcutter's hut, the farmhouse and the market <em>state</em> their seats; the
    /// gatherer's hut (its ring ÷ tiles per worker), the forester's hut (what the woodcutters can
    /// eat, plus a hand for building) and the builder's hut <em>solve</em> for them.
    /// </remarks>
    private int SeatsIn(BuildingKind kind)
    {
        if (BuildingsCatalog[kind]?.Seats is int stated)
        {
            return stated;
        }

        return kind switch
        {
            BuildingKind.GathererHut => VillageEconomy.GathererHutCapacity(Config),
            BuildingKind.ForesterHut => VillageEconomy.RequiredForesterSeats(Config),
            BuildingKind.BuilderHut => VillageEconomy.BuilderHutCapacity(Config),
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "That workplace states no seats and the economy derives none."),
        };
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

        // ⛔⛔ AND WHATEVER WAS IN ITS OWN STORE GOES ON THE GROUND (D162). This method did
        // nothing about `Workplace.Store` for five phases and was right not to, because
        // **nothing in the sim had ever written to one** — and the farm's 100-unit buffer is
        // the moment that stopped being true. Pulling down a full farmhouse would have
        // destroyed up to `farm_store_cap` of food silently, which is D96 exactly (17,451 food
        // into a full granary and out of the world) and D144 one path over. Both were
        // invisible, and both were found by Joe playing rather than by the suite.
        //
        // On the ground rather than into a store, deliberately: D96's rule is that goods
        // nothing will take go down where they are and somebody carries them in, and a village
        // that has just demolished a full farm is exactly a village that may have nowhere to
        // put a hundred food. It is on the map, it can be fetched, and it counts in no total
        // until it is (`GroundStack`).
        int spilled = 0;
        for (int g = 0; g < GoodsCatalog.Count; g++)
        {
            var goods = (Goods)g;
            int held = workplace.Store[goods];
            if (held > 0 && workplace.Store.TryTake(goods, held))
            {
                SetDown(workplace.Position, goods, held);
                spilled += held;
            }
        }

        if (spilled > 0)
        {
            Narrate($"{Capitalised(workplace.Name)} came down with {spilled} still in it — "
                + $"it is on the ground where it stood. {Clock.SeasonAndYear()}.");
        }

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

    /// <summary>
    /// What to call the next building of a kind — <b>numbered, and unique for the run</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ Joe's call (2026-08-10): <em>"name them numerically — gatherer's hut 1,
    /// gatherer's hut 2 — eventually we will let the user rename them."</em></b> Every
    /// building of a kind used to be called the same thing, so two gatherer's huts both
    /// reported as <em>"a gatherer's hut"</em> and no sentence in the game could tell them
    /// apart. That is D56's collision arriving from the other side: the generated places it
    /// fixed have retired, and the player-placed buildings that replaced them never had
    /// names of their own.
    /// </para>
    /// <para>
    /// <b>It overturns D56's "Forage Site 3 is a row in a table", and the difference is who
    /// chose the place.</b> A bearing gave identity to somewhere the generator dropped and
    /// the player had no relationship with. A building the player sited needs no help being
    /// remembered — and a bearing would collide again anyway, since there are eight of them,
    /// and would be wrong the moment renaming lands. <b>A number is the honest placeholder
    /// for a name somebody is going to type.</b>
    /// </para>
    /// <para>
    /// <b>⚠️ Counted per kind and never reused, which is the whole correctness of it.</b>
    /// Numbering from how many are currently standing looks simpler and is broken: pull down
    /// hut 2 of three, build another, and it would be christened <em>hut 3</em> alongside the
    /// hut 3 already there. A monotonic counter cannot do that, and the gap it leaves behind
    /// is the truth — hut 2 was pulled down, and the log still says so.
    /// </para>
    /// <para>
    /// Not hashed, like every other name: it is derived from the order the player marked
    /// things, which is hashed already through the buildings themselves.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Where a warm start puts its gatherer's hut — <b>in the wood, not in the clearing</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ MEASURED, AND THE FIXED OFFSET IT REPLACES BROKE 105 TESTS.</b> The obvious way
    /// to found a hut is at a configured offset from the village, the way every other founding
    /// building is placed. That puts it <b>inside the founding glade</b> — the clearing D112
    /// skips woodland over so the opening is not blocked by trees — and a gatherer's hut in a
    /// clearing is worth almost nothing: measured, a ring of 145 tiles yielding <b>3 food a
    /// trip against a full-woodland 51</b>. The village starved, and a hundred and five tests
    /// failed with things like <em>"nobody ever split a log"</em>, because nobody had eaten.
    /// </para>
    /// <para>
    /// <b>So it is sited the way a player would site it: where the trees are.</b> That is not
    /// a workaround, it is the mechanic — <em>less trees, less food</em> is the whole rule, and
    /// a warm start that ignored it would hand every test in the suite a village standing on a
    /// special case the game itself does not have.
    /// </para>
    /// <para>
    /// <b>Bounded by the economy's own budget</b>, so the hut cannot wander off to the finest
    /// wood in the valley and leave the homes beyond the walk the food economy is derived
    /// against. Deterministic and draw-free: a full scan with ties broken by distance and then
    /// by position, so two runs of one seed cannot disagree (D15).
    /// </para>
    /// </remarks>
    private GridPos WhereTheTreesAre(GridPos origin, SimConfig config)
    {
        int reach = VillageEconomy.MaxHomeToWorkTiles(config);
        int ring = config.GathererHutRingTiles;

        GridPos best = origin;
        int bestTrees = -1;
        int bestDistance = int.MaxValue;

        for (int dy = -reach; dy <= reach; dy++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                var at = new GridPos(origin.X + dx, origin.Y + dy);
                int distance = origin.ManhattanDistanceTo(at);

                // ⚠️ MANHATTAN, NOT PER-AXIS, and the difference is the whole point of the
                // bound. Bounding dx and dy separately let the hut sit fourteen tiles from
                // the village on a budget of eight — which is the economy's assumption about
                // the walk to work being false before the first tick.
                // ⚠️ THE REACHABILITY QUESTION IS ASKED THE OTHER WAY ROUND, AND IT IS THE
                // DIFFERENCE BETWEEN ONE DIJKSTRA AND THREE HUNDRED. `TravelCost` caches a
                // flow field per DESTINATION, so `CanReach(origin, at)` builds a fresh field
                // for every candidate tile — 289 of them, on every world this suite
                // constructs. Asked as `CanReach(at, origin)` it builds one field to the
                // founding site and answers every candidate off it.
                //
                // Reachability is symmetric — passability is a property of the ground, not of
                // the direction — so this is the same question, and it is the trap
                // `building-placement.md` records `CanBuildAt` falling into at 22 seconds a
                // test. **Rank on the cheap question first.**
                if (distance > reach
                    || !Map.Contains(at)
                    || Map.TerrainAt(at) == Terrain.Water
                    || SomethingStandsAt(at)
                    || !TravelCost.CanReach(at, origin))
                {
                    continue;
                }

                int trees = WoodedTilesWithin(at, ring);

                if (trees > bestTrees || (trees == bestTrees && distance < bestDistance))
                {
                    best = at;
                    bestTrees = trees;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Give a warm start's forester's hut the wood around it to work.
    /// </summary>
    /// <remarks>
    /// <b>As much as its hands can actually keep, and no more</b> — the allowance D86 derives
    /// (`WorkGroundAllowanceFor`), so a founded village is never born already overstretched
    /// and carrying the warning that says so. Nearest wood first, in a fixed scan order, so
    /// two runs of a seed give the hut the same ground.
    /// </remarks>
    private void GiveItTheWoodAroundIt(Workplace hut, SimConfig config)
    {
        // ⚠️ AGAINST THE SEATS, NOT AGAINST THE WORKERS. `WorkGroundAllowanceFor` prices
        // ground by the hands **actually assigned** (D86), and at the founding a hut has
        // none — so asking it here gave the forester **zero tiles**, and the measurement
        // said so: the valley kept all 2,662 of its trees while the village dwindled to
        // nothing for want of timber. It was not deforestation, it was a hut that had never
        // been given anything to fell.
        //
        // The ground a FULL hut could keep is what a player would hand it, and the
        // allowance check still applies from the next tick — so if the village never staffs
        // it, the overstretched warning fires and says exactly that.
        int allowance = hut.Capacity * config.WorkGroundTilesPerWorker;
        int given = 0;

        for (int radius = 1; radius <= config.GathererHutRingTiles && given < allowance; radius++)
        {
            for (int dy = -radius; dy <= radius && given < allowance; dy++)
            {
                for (int dx = -radius; dx <= radius && given < allowance; dx++)
                {
                    // Only the ring being added this pass, so nearer wood is always taken
                    // before further wood.
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                    {
                        continue;
                    }

                    var tile = new GridPos(hut.Position.X + dx, hut.Position.Y + dy);
                    if (Map.TerrainAt(tile) != Terrain.Forest
                        || Zones.WorkGroundOwner(tile) != 0)
                    {
                        continue;
                    }

                    Zones.SetWorkGround(tile, hut.Id);
                    given++;
                }
            }
        }
    }

    /// <summary>Wooded tiles inside a radius of a point. Counts the ground, not a cache.</summary>
    private int WoodedTilesWithin(GridPos centre, int radius)
    {
        int count = 0;
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if ((dx * dx) + (dy * dy) > radius * radius)
                {
                    continue;
                }

                var tile = new GridPos(centre.X + dx, centre.Y + dy);
                if (Map.Contains(tile) && Map.TerrainAt(tile) == Terrain.Forest)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private string NameFor(BuildingKind kind)
    {
        // A HOUSE IS NOT NUMBERED, and that is not an oversight. Homes are identified by the
        // family in them — "the Thatcher household" — which is a better name than any number
        // and the one every sentence about a home already uses. "House 47" would be the row
        // in a table D56 objected to, in the one place the objection still holds.
        if (kind == BuildingKind.Home)
        {
            return "a house";
        }

        int index = (int)kind;
        _buildingsNamed[index]++;

        // ⭐⭐ THE WORD COMES OFF THE ROW (`buildings-catalog.md §2`). It was a nine-arm switch, and
        // the arms are kept below as the record of what each one said and why — the labels are
        // `SimConfig.DefaultBuildings`' `Name` column now, word for word.
        //
        // ⚠️ "stockpile", not "storage pile" (Joe, D217), and it shares a word with the `Stockpile`
        // class without being the same thing: that is the goods container every store, larder,
        // workplace and pair of arms holds; this is the name of ONE kind of store building.
        //
        // ⚠️ And the arm that mattered most was the one that did not exist: the default called every
        // unrecognised building a woodcutter's hut — in the log, in the panel and in every placement
        // sentence (D108). A missing row is now a missing name and says so.
        string what = BuildingsCatalog[kind]?.Name
            ?? throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "That kind of building has no row, so it has no name.");

        return $"{what} {_buildingsNamed[index]}";
    }

    /// <summary>How many of each kind have ever been named. Never decremented.</summary>
    /// <remarks>
    /// <b>⛔ SIZED FROM THE CATALOGUE, NOT FROM THE ENUM, AND THAT WAS A LIVE CEILING.</b> It read
    /// <c>Enum.GetValues&lt;BuildingKind&gt;().Length</c>, so <b>building eleven would have walked
    /// off the end of it</b> — an <c>IndexOutOfRangeException</c> the first time a modded building
    /// was named, in the middle of a run rather than at load. That is the same class of thing
    /// `goods-catalog.md` found twice by counting rather than by reasoning (<c>Stockpile.Kinds</c>
    /// at six, <c>AllowedGoods</c> at thirty), and it is why the ceilings get counted.
    /// </remarks>
    private readonly int[] _buildingsNamed;

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
    public void SetStaffing(Workplace workplace, int places)
    {
        ArgumentNullException.ThrowIfNull(workplace);

        if (places < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(places), places, "A workplace cannot be staffed by fewer than nobody.");
        }

        workplace.StaffingOverride = places;

        Narrate($"{workplace.Name} is to be worked by {places} " +
            $"{(places == 1 ? "person" : "people")} from now on. {Clock.SeasonAndYear()}.");
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

    /// <summary>
    /// What this village is called — <b>derived from the seed, never drawn from it</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A run you can quote by number is one thing; a run you can talk about is another, and
    /// <em>"Ashbourne died at sixty"</em> is the sentence this game is for (§1.4). So the
    /// valley gets a name, and it is a fact about the seed rather than a fact about the map.
    /// </para>
    /// <para>
    /// <b>⚠️ It takes no draw, and that is the whole care in it.</b> Draw order is the seed
    /// contract (<see cref="MapGenerator"/> §1) — a call to <c>Rng.Next</c> inserted anywhere
    /// shifts every subsequent value for every seed anybody has ever written down. Arithmetic
    /// on the seed is the same run every time and costs the stream nothing, so no golden
    /// moves and no valley changes shape for a label.
    /// </para>
    /// <para>
    /// Not hashed, for the same reason: it is a pure function of the seed, and the seed is
    /// already what identifies a run. Two worlds that hash alike cannot be called differently.
    /// </para>
    /// </remarks>
    public string Name =>
        Config.TownNames[(int)(Seed % (ulong)Config.TownNames.Count)];

    private SimWorld(SimConfig config, ISimLogger logger, ulong seed)
    {
        Config = config;
        GoodsCatalog = new GoodsCatalog(config.GoodsCatalog);
        JobsCatalog = new JobsCatalog(config.JobsCatalog);

        // ⚠️ AFTER THE JOBS, AND IT MUST BE: the buildings catalogue indexes `JobRow.WorksAt`
        // backwards to answer "who works here?", because that relation is stated once
        // (`buildings-catalog.md §2.1`).
        BuildingsCatalog = new BuildingsCatalog(config.BuildingRows, JobsCatalog);
        _buildingsNamed = new int[BuildingsCatalog.Count];

        TechniquesCatalog = new TechniquesCatalog(config.Techniques, config.Skills);
        KnowledgeStates = new KnowledgeState[TechniquesCatalog.Count];
        LastKnowerIds = new int[TechniquesCatalog.Count];
        _saidThereIsNowhereFor = new bool[GoodsCatalog.Count];
        StockLimits = new StockLimits(GoodsCatalog.Count);
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

        int nextWorkplaceId = 1;

        // ⭐ THE BERRY PATCHES AND THE TREE STANDS ARE GONE, AND WITH THEM THE LAST WORK IN
        // THIS GAME THAT NOBODY CHOSE (`forests-and-gathering.md` slice 5, Joe). Six forage
        // sites and two stands used to be added here as workplaces before a single building
        // existed — a village woke up on its first tick already owning eight jobs.
        //
        // **Every workplace in the valley is now a building somebody placed.** Food comes
        // from a gatherer's hut sited in woodland the player can see, timber from a
        // forester's hut on ground the player painted, and *both compete for the same
        // trees.* That is §2.3's escalating pressure arriving out of the food system rather
        // than being bolted on.
        //
        // `world.FoodSource` and `world.TreeStand` went with them: Phase 0's single berry
        // patch and single stand, kept alive long after the plural lists replaced them. The
        // classes themselves outlived even that and went in D159.

        // EVERYTHING FROM HERE IS A BUILDING, AND THE COLD START HAS NONE (D70).
        //
        // That distinction used to matter because the berry patches and the stands were
        // features of the valley and stayed either way. **There is nothing above any more**,
        // so a cold start now begins with no work of any kind — which is the whole of Joe's
        // *"no forest, no food"*. What follows — the hut, the granary, the shed, the market —
        // is what somebody had to raise, and in the cold start that somebody is the player.
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
            Store = NewStockpile(),
            Id = nextWorkplaceId++,
            Kind = JobKind.Builder,
            Name = NameFor(BuildingKind.BuilderHut),
            Position = Offset(origin, config.BuilderHutX, config.BuilderHutY),
            Capacity = VillageEconomy.BuilderHutCapacity(config),
        });

        // ⭐ A GATHERER'S HUT, BECAUSE A WARM START NOW HAS NO OTHER FOOD (slice 5). The
        // berry patches used to be laid down by the generator before any building existed,
        // so a warm start woke up able to eat. With them retired, a village founded with
        // buildings and no hut is a village with **nothing to gather anywhere** — and every
        // test in the suite that says "a village lives here" would have been asserting that
        // four people can starve gracefully.
        //
        // Sited where the wood is, not at an offset — see `WhereTheTreesAre` for the 105
        // tests that taught me the difference. The COLD start still has no hut at all, and
        // that is Joe's *"no forest, no food"*: the player sites the first one, in woodland
        // they picked, and it is the first real decision of a run.
        Workplaces.Add(new Workplace
        {
            Store = NewStockpile(),
            Id = nextWorkplaceId++,
            Kind = JobKind.Forager,
            Name = NameFor(BuildingKind.GathererHut),
            Position = WhereTheTreesAre(origin, config),
            Capacity = VillageEconomy.GathererHutCapacity(config),
            GatheringRadius = config.GathererHutRingTiles,
        });

        // ⭐ AND A FORESTER'S HUT WITH GROUND, WHICH IS THE OTHER HALF OF THE SAME HOLE.
        // Foresters worked the two generated tree stands; with those retired a warm start
        // had timber-workers and nowhere to fell, so no logs reached the woodcutter and the
        // suite reported *"nobody ever split a log"* — the fuel chain starved at its source
        // rather than in the middle.
        //
        // ⚠️ **A hut alone would not have been enough, and that is the mechanic rather than
        // an oversight.** A forester's hut fells the ground the player gives it (D86, D112);
        // with no ground it is a building nobody can work. So the warm start gives it some,
        // exactly as a player would — which is what makes this a village that already works
        // rather than one holding a building it does not understand.
        var forester = new Workplace
        {
            Store = NewStockpile(),
            Id = nextWorkplaceId++,
            Kind = JobKind.Forester,
            Name = NameFor(BuildingKind.ForesterHut),
            Position = WhereTheTreesAre(origin, config),
            Capacity = VillageEconomy.RequiredForesterSeats(config),
        };

        Workplaces.Add(forester);
        GiveItTheWoodAroundIt(forester, config);

        // The first workplace that consumes an input rather than only producing one
        // (D29). Logs in, firewood out - and it can stand idle for want of logs,
        // which is a state no other workplace can be in.
        Workplaces.Add(new Workplace
        {
            Store = NewStockpile(),
            Id = nextWorkplaceId++,
            Kind = JobKind.Woodcutter,
            Name = NameFor(BuildingKind.WoodcutterHut),
            Position = Offset(origin, config.WoodcutterHutX, config.WoodcutterHutY),
            Capacity = config.WoodcutterHutCapacity,
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
            Catalog = GoodsCatalog,
            Id = 1,
            Kind = StoreKind.Granary,
            Name = NameFor(BuildingKind.Granary),
            Position = Offset(origin, config.GranaryX, config.GranaryY),
            Store = new Stockpile(GoodsCatalog.Count) { Capacity = VillageEconomy.GranaryCapacity(config) },
        });

        StoreBuildings.Add(new StoreBuilding
        {
            Catalog = GoodsCatalog,
            Id = 2,
            Kind = StoreKind.Shed,
            Name = NameFor(BuildingKind.Shed),
            Position = Offset(origin, config.StorageShedX, config.StorageShedY),
            Store = new Stockpile(GoodsCatalog.Count) { Capacity = VillageEconomy.ShedCapacity(config) },
        });

        // The market (D14) — the one store that is also a workplace, because its
        // contents arrive by somebody's work rather than by producers dropping things
        // off. Two entries at one position: a store, and a place to work.
        //
        // They are separate types today. Merging them into the single Building the
        // spec's §4 describes is the right end state and is not this slice's job; the
        // seam is recorded there rather than left to be rediscovered.
        var market = Offset(origin, config.MarketX, config.MarketY);

        // ⚠️ NAMED ONCE AND USED TWICE, because a market IS one building that happens to
        // live in two lists (D36's seam). Calling `NameFor` again below would christen the
        // store "market 1" and the workplace inside it "market 2", which is the seam telling
        // a lie about how many markets the village has.
        string marketName = NameFor(BuildingKind.Market);

        StoreBuildings.Add(new StoreBuilding
        {
            Catalog = GoodsCatalog,
            Id = 3,
            Kind = StoreKind.Market,
            Name = marketName,
            Position = market,
            Store = new Stockpile(GoodsCatalog.Count) { Capacity = VillageEconomy.MarketCapacity(config) },
        });

        // Capacity zero means no market at all rather than a market nobody can work
        // at, so that "switch the market off" is a state the village genuinely runs in
        // (spec §14.4) rather than one that merely produces a permanently empty
        // building for the allocator to keep considering.
        if (config.MarketCapacity > 0)
        {
            Workplaces.Add(new Workplace
            {
                Store = NewStockpile(),
                Id = nextWorkplaceId++,
                Kind = JobKind.Marketer,
                Name = marketName,
                Position = market,
                Capacity = config.MarketCapacity,
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
                Stockpile = NewStockpile(),
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

                // ⭐ AND THEIR RHYTHM, THIRD IN THE DRAW ORDER — name, lifespan, rhythm (§3.5,
                // D190). `HouseholdSystem.TryBirth` draws the same three in the same order, so
                // there is one rule rather than two; that comment has stood over the birth path
                // since D71 and this keeps it true.
                // ⭐ AND THEIR RHYTHM, THIRD IN THE DRAW ORDER — name, lifespan, rhythm (§3.5,
                // D190), ROTATED BY THEIR PLACE IN THE HOUSEHOLD.
                //
                // ⛔⛔ THE ROTATION IS NOT BELT AND BRACES — WITHOUT IT THE FIX DID NOTHING, AND
                // THE MEASUREMENT IS WORTH THE PARAGRAPH. The founding draws four small-range
                // numbers at a fixed stride at the very start of the stream, and at that stride
                // the first four come out **1, 1, 2, 2** — so both adults of household 1 got the
                // same rhythm, both of household 2 got the same rhythm, and two people who were
                // meant to stop moving in lockstep were handed identical staggers.
                //
                // ⚠️ THE RNG IS NOT AT FAULT AND THAT MATTERS. Forty raw `NextInt(0, 4)` draws
                // come out 9/11/8/12 — well distributed. **It is a short-range correlation at a
                // fixed stride, showing at the start of the stream**, and the founding is
                // exactly four such draws. *A generator can be sound and still be the wrong tool
                // for four draws that must differ from each other.*
                //
                // So the draw supplies the seeded part and the rotation supplies the guarantee:
                // no two adults of one household can share a rhythm while a household holds no
                // more people than a day holds ticks.
                int rhythm = config.SeededRhythm && config.TicksPerDay > 1
                    ? (Rng.NextInt(0, config.TicksPerDay) + a) % config.TicksPerDay
                    : 0;

                var villager = new Villager
                {
                    Id = nextVillagerId++,
                    Name = name,
                    LifespanYears = lifespan,
                    Rhythm = rhythm,

                    // Sized from the run's catalogue like every other stockpile (D210) — so a
                    // good a mod adds can be picked up rather than only stored.
                    Carried = NewStockpile(),

                    // ⭐⭐ AND THEIR HUNGER STARTS A LITTLE APART, WHICH IS THE HALF THE STAGGER
                    // ALONE COULD NOT REACH (§3.5, D190). Measured with only the action
                    // stagger: two adults of one household still had **identical hunger 100% of
                    // ticks** — because hunger is a pure function of ticks since the last meal,
                    // so two people who eat on the same tick stay in step for ever however
                    // differently they walk. **Identical hunger is one of the two numbers D28
                    // named**, and nothing that offsets only movement can touch it.
                    Hunger = rhythm,

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

        GiveTheFoundersTheirTrades(config);
        PairFounders(config);
    }

    /// <summary>
    /// ⭐⭐ The founders arrive as a <b>mix of tiers</b> — fixed shape, seeded trades
    /// (`skills-catalog.md §3.2c`, Joe's call 2026-08-23, D190).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, D175: *"Maybe the founders could be a mix of masters, mids — whatever that is —
    /// and novices? Could be a master woodcutter or gatherer or apprentice forester."*</b> It
    /// does three things at once: it makes the four founders **people at tick 0** rather than
    /// four identical units with different names; it gives the opening **a shape to read** — a
    /// party with a master woodcutter and nobody who can farm is a different opening from the
    /// reverse; and it **finishes the lockstep fix**, because founders at different tiers do
    /// different work in different numbers of ticks from the first day.
    /// </para>
    /// <para>
    /// <b>⛔ FIXED COMPOSITION, SEEDED TRADES, AND THE DISTINCTION IS THE WHOLE DESIGN.</b> Every
    /// seed gets the same *strength* of party and a different *speciality*. A fully seeded roll
    /// would make a seed handing you four novices and a seed handing you two masters **a bad run
    /// and a good one rather than two playthroughs** — and §0.1 is that the challenge is in the
    /// planning, never in the punishment. **You never lose before you press play.**
    /// </para>
    /// <para>
    /// <b>⚠️ MEASURED BEFORE IT WAS BUILT, because §11 named this as the unmeasured arm:</b>
    /// *does a master gatherer make the opening trivial?* **It does not — it changes nothing at
    /// all.** Food at the first winter is identical across three seeds with one master forager,
    /// two master foragers, or none, because **nobody forages during the opening**. A master
    /// forester moves it 13% and 9% on two seeds, and population at years 1 and 5 is unchanged
    /// in every arm.
    /// </para>
    /// <para>
    /// <b>Drawn after every founder exists</b>, so the trades are dealt from a settled roster in
    /// villager-id order — an unordered tie is a desync waiting to happen (D15).
    /// </para>
    /// </remarks>
    private void GiveTheFoundersTheirTrades(SimConfig config)
    {
        if (config.Skills.Count == 0 || Villagers.Count == 0)
        {
            return;
        }

        // The trades on offer, one draw each and never the same twice: a master and a
        // journeyman of the same trade is a narrower party than the shape asks for.
        var available = new List<SkillRow>(config.Skills);

        int masters = config.FoundingMasters;
        int journeymen = config.FoundingJourneymen;

        for (int i = 0; i < Villagers.Count && available.Count > 0; i++)
        {
            SkillTier tier = i < masters ? SkillTier.Master
                : i < masters + journeymen ? SkillTier.Journeyman
                : SkillTier.Novice;

            if (tier == SkillTier.Novice)
            {
                // ⛔ A NOVICE IS TODAY'S VILLAGER, TO THE TICK (§3.2) — no entry at all, so
                // they hash exactly as a villager did before any of this existed.
                continue;
            }

            SkillRow trade = available[Rng.NextInt(0, available.Count)];
            available.Remove(trade);

            SkillProgress progress = Villagers[i].ProgressIn(trade.Id);

            if (tier == SkillTier.Master)
            {
                progress.Work = config.MasteryWorkFor(trade);
                progress.Ticks = config.MasteryYearsFor(trade) * config.TicksPerYear;
                progress.Mastered = true;
                continue;
            }

            // ⭐ THREE QUARTERS OF THE WAY, NOT THE MIDDLE OF THE BAND, AND THE REASON IS
            // MEASURED. The journeyman band starts at half of mastery, but the sim's one speed
            // step falls at about 70% (D187) — so a journeyman seeded at the band's midpoint
            // would read as *"Otto knows his trade"* and work at exactly a novice's pace.
            // **A tier the player can see and the sim cannot feel is the invisible number this
            // project keeps refusing**, so they are seeded past the step.
            progress.Work = config.MasteryWorkFor(trade) * 3 / 4;
            progress.Ticks = config.MasteryYearsFor(trade) * config.TicksPerYear * 3 / 4;
        }
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

        // ⚠️ AND THE LIBRARIES, WHICH ARE THE FOURTH KIND OF THING TO STAND ON A TILE. The comment
        // above is about exactly this going wrong once already — two rules for *"is this tile
        // free?"*, one of them missing a kind of building, and the wrong one facing the player.
        // **A new kind of building is a new line here or it can be built on top of.**
        for (int i = 0; i < Libraries.Count; i++)
        {
            if (Libraries[i].Position == position)
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

    // ⭐ D56's PLACE-NAMING IS DELETED HERE, AND IT IS THE RIGHT KIND OF DELETION.
    // `NamePlaces`, `Bearing`, `Further` and `TilesApart` gave the generated forage sites and
    // tree stands distinct names — *"the south-western thicket"* — because a bearing has
    // eight values and the valley had eight places, so two of them shared a phrase and the
    // game could not say which it meant.
    //
    // **Both halves of that problem are gone.** The generated places retired in this slice,
    // and the player-placed buildings that replaced them are numbered (D124), which solves
    // the same collision in the one way that survives the player renaming them later.
    //
    // A hundred and fifty lines removed, and the argument they were written for is recorded
    // in D56 and D124 rather than in code nothing calls.

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
        // ⭐⭐ THE PLAYER'S NUMBER IF THEY HAVE GIVEN ONE, THE DERIVED TARGET IF NOT (D216).
        //
        // **This read the derived target ONLY, so a food limit was invisible to the one person
        // who would produce toward it.** Joe, playing: *"if there are trees marked for harvest,
        // foragers will gather trees even though the food limit is not yet met [set to 2000]."*
        // Measured on his shape of village: **a limit of 2000 and no limit at all produced
        // byte-identical behaviour** — 959 forager ticks gathering and 871 clearing in both arms.
        // A control that changes nothing is D212's stone box, on the good the whole economy is
        // derived from.
        //
        // **This is exactly D62's *derived floor, player ceiling*** — and the floor half is
        // unaffected, because `TargetFoodForTheGranary` is what the BIRTH gate reads
        // (`HouseholdSystem`) and that deliberately stays derived (D153). The player's number
        // governs *work*; the derived number governs *children*. Two questions, two readers.
        //
        // ⚠️ A limit BELOW the derived floor is obeyed rather than argued with, which is D62's
        // own rule — `SetStockLimit` already warns at the moment it is set, because *a game that
        // refuses the player's number is arguing with them, and one that obeys it silently has
        // killed them without saying so.*
        int wanted = StockLimits.For(Goods.Food) ?? TargetFoodForTheGranary();

        // Across every store the village can actually put food in (D76, D79) — the
        // granaries it has built, the pile the player dropped on day one, and the cart
        // they arrived in.
        //
        // ⛔ STILL CAPPED BY ROOM, and that is not a hedge: *a village cannot want more food
        // than it has somewhere to put* (D33, D76). Asking for 2000 with granaries for 900 is a
        // request for granaries, and the forager who stops now says so out loud rather than
        // wandering off to fell a tree — see `BehaviorSystem`'s note where this is read.
        int capacity = FoodInGranaries() + RoomLeftForFood();
        return wanted < capacity ? wanted : capacity;
    }

    /// <summary>
    /// Why the village has stopped wanting food, or null while it still does.
    /// </summary>
    /// <remarks>
    /// <b>For the sentence, not the decision</b> (METHODOLOGY §4, D216). A forager who falls
    /// through to clearing painted ground looks exactly like a forager who has decided timber
    /// matters more than food — which is what Joe read off the screen — and the two have
    /// completely different answers: *raise the limit* against *build a granary*.
    /// </remarks>
    public string? WhyTheVillageWantsNoMoreFood()
    {
        int holds = FoodTheVillageHolds();
        if (holds < FoodTheVillageHasRoomFor())
        {
            return null;
        }

        if (StockLimits.For(Goods.Food) is int limit && holds >= limit)
        {
            return $"you asked the village to keep {limit} food and it has {holds}";
        }

        return $"every store that takes food is full — {holds} held, and nowhere to put more";
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
            Catalog = GoodsCatalog,
            Id = 1,
            Kind = StoreKind.Cart,
            Name = "the cart",
            Position = origin,
            Store = new Stockpile(GoodsCatalog.Count) { Capacity = config.CartCapacity },
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

        // ⛔ AND NOTHING ELSE (Joe, D215). Stone was added here for one commit and taken out
        // again: *"there should already be stone on the map for the user to ask the laborers to
        // harvest."* There is — four seams on a fourteen-tile ring — see `SimConfig`'s note where
        // `cart_stone` used to be for the misread unit that put it here.
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
