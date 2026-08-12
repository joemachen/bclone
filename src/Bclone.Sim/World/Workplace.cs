namespace Bclone.Sim.World;

/// <summary>Kinds of work. Data-driven content lands here in a later pass.</summary>
/// <remarks>
/// <b>On the names:</b> a <see cref="Forester"/> fells trees and a
/// <see cref="Woodcutter"/> splits the logs into firewood (D29). That follows
/// <em>Banished</em>, and it cuts against everyday usage — colloquially a woodcutter
/// is the one with the axe in the forest. The split is deliberate: they are two
/// different jobs in two different places, and the chain between them is the first
/// secondary processing in the game.
/// <para>
/// The first of those was <c>Logger</c> until D86 made the timber chain something the
/// player sites and keeps rather than something the generator provides. A forester fells
/// <em>and</em> replants; a logger only ever took. Renaming was cheaper and more honest
/// than a second job kind — see the value's own remarks.
/// </para>
/// </remarks>
public enum JobKind
{
    /// <summary>Gather food from a wild source.</summary>
    Forager = 0,

    /// <summary>
    /// Work a wood — fell trees for logs, and in time plant them again (D86).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This was <c>Logger</c>, and it is a rename rather than a new job</b> — the same
    /// person, doing the same work, on ground somebody chose instead of at an
    /// inexhaustible stand the generator dropped. Adding <c>JobKind.Forester</c> beside
    /// <c>Logger</c> would have meant a second quota arm, a second slot in the allocator's
    /// scarcity order, a second plural, a second behaviour branch and a rule somewhere to
    /// stop the village staffing both — **machinery invented purely to undo a type that
    /// should not have existed**, which is the argument D66 made against
    /// <c>JobKind.Laborer</c> almost word for word.
    /// </para>
    /// <para>
    /// <b>The value stays 1 on purpose.</b> It is hashed by position, so renumbering would
    /// silently reinterpret every golden as being about a different job.
    /// </para>
    /// </remarks>
    Forester = 1,

    /// <summary>Split logs into firewood at a hut. Consumes an input (D29).</summary>
    Woodcutter = 2,

    /// <summary>
    /// Move goods between the stores and the homes — the market's trade (D14).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first job that <b>produces nothing</b>. A forager makes food appear and a
    /// woodcutter turns one good into another; a marketer only ever moves what already
    /// exists. That is the point — D14's claim is that distribution is work somebody
    /// does rather than a policy slider, and a job with no yield is what that claim
    /// looks like in code.
    /// </para>
    /// <para>
    /// It is also the only job that can be <em>switched off with no one dying</em>, and
    /// that is a deliberate property rather than an accident. Households still fetch
    /// for themselves (spec §3), so an unstaffed market means longer walks and stranded
    /// goods, never a household that cannot eat.
    /// </para>
    /// </remarks>
    Marketer = 3,

    /// <summary>
    /// Raise a building the player has marked out (D43).
    /// </summary>
    /// <remarks>
    /// Like the marketer, produces nothing — but unlike everything else, the workplace
    /// itself is temporary: a builder's job disappears the moment the thing they were
    /// building exists. That is why a construction site is a workplace rather than a
    /// system of its own; it gets catchment, allocation and refusal reasons for free,
    /// and then it stops.
    /// </remarks>
    Builder = 4,
}

/// <summary>
/// Somewhere work happens, with a demand for labour and a reach.
/// </summary>
/// <remarks>
/// <para>
/// The pillar this serves (<c>DESIGN.md §2.2</c>): the player never slots N workers
/// into a building. A workplace states <em>how much work it wants</em> and
/// <em>how far it is reasonable to come from</em>, and villagers take the work
/// themselves.
/// </para>
/// <para>
/// <b>It used to state how far it was reasonable to come from, and that fence is gone</b>
/// (`forests-and-gathering.md §3`). Distance still decides who works where — the allocator
/// sorts candidates by travel cost off the one shared <see cref="TravelCostField"/>, so the
/// nearest hands are claimed first — but it no longer <em>refuses</em>. What used to kill the
/// villager who walked across the map for one log is now the walk itself costing them their
/// working year, which is a consequence rather than a rule, and one they can see.
/// </para>
/// </remarks>
/// <summary>What a workplace is currently set to do, where it can do more than one thing.</summary>
/// <remarks>
/// <b>`professions.md §3`'s "optional sixth part", arriving for the first time.</b> Every
/// profession so far does exactly one thing; a forester's hut can take trees down or put them
/// back, and which one is the player's standing instruction rather than something the village
/// works out. <b>Appended, never renumbered</b> — it is hashed by position.
/// </remarks>
public enum WorkMode
{
    /// <summary>Take what is standing. The default, and what every other workplace does.</summary>
    Harvest = 0,

    /// <summary>Put trees back on bare ground this place owns (Joe, ungated).</summary>
    Plant = 1,
}

public sealed class Workplace
{
    public required int Id { get; init; }

    public required JobKind Kind { get; init; }

    /// <summary>A place name, so the log reads "the north stand" not "Workplace 3".</summary>
    public required string Name { get; init; }

    public required GridPos Position { get; init; }

    /// <summary>
    /// How many people can physically work here at once. <b>A local fact about the
    /// place</b>, fixed for its lifetime.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to be called <c>LabourDemand</c> and carried village-level meaning —
    /// it was recomputed every season from the population, so one berry patch's field
    /// had to express "how many foragers does the whole village need?". That is a
    /// global constraint written into a local variable, and
    /// <c>specs/labour-allocation.md §3</c> records the four different values of it
    /// that each broke the village in a different way.
    /// </para>
    /// <para>
    /// The village-level question now lives in <see cref="LabourQuota"/>, where it can
    /// actually be answered. This field went back to meaning the only thing a single
    /// site can honestly know: how many hands fit.
    /// </para>
    /// </remarks>
    public required int Capacity { get; init; }

    // ⭐ `CatchmentRadius` IS DELETED (`forests-and-gathering.md §3`, Joe's third call).
    // *"How far it is reasonable to travel here"* was a number the village enforced as a
    // refusal — past ten tiles you simply could not hold the job. It is a **cost** now, not
    // a fence: candidates are sorted cost-first, so a nearer workplace still wins, and a
    // ruinous commute is a mistake the player can make, see and pay for.
    //
    // **Deleted rather than zeroed** (D98): a radius of zero would mean *nobody may work
    // anywhere*, which is not what "no fence" means, and a number nothing reads is a lie
    // waiting to be found. The only thing that disqualifies a workplace now is
    // `TravelCostField.Unreachable`, which is a fact about the valley rather than a rule.

    /// <summary>
    /// How far this place reaches for what it harvests, in tiles. Zero for everything that
    /// does not (`specs/forests-and-gathering.md`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The gatherer's hut is the first building whose yield depends on the ground around
    /// it</b> (Joe): the trees inside this ring decide what a trip is worth, and nothing
    /// outside it counts for anything. Zero means *"this place has no ring"* — a berry patch,
    /// a tree stand, a market — and those yield what they have always yielded.
    /// </para>
    /// <para>
    /// <b>In tiles, not in travel cost.</b> A ring is about *area* — it is drawn on the map
    /// and the player counts it in squares — where every other distance in this game is a
    /// walk that has to go round the river.
    /// </para>
    /// </remarks>
    public int GatheringRadius { get; init; }

    /// <summary>
    /// Wooded tiles last counted in this place's ring, and the terrain it was counted against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠️ Derived state, and deliberately NOT hashed.</b> It is a count of terrain, and
    /// terrain is already hashed — hashing this too would mix the same fact twice and, worse,
    /// would let a stale cache become a determinism failure instead of the performance bug it
    /// actually is. <c>SimWorld.WoodedTilesAround</c> owns both fields; nothing else may
    /// write them.
    /// </para>
    /// <para>
    /// <b>It exists because the alternative is a per-gather O(R²) scan</b>, and D87 has already
    /// taught this suite what a per-tick per-villager scan costs — the run went from four
    /// minutes to over ten until a village that had painted nothing paid one integer compare.
    /// </para>
    /// </remarks>
    internal int CachedWoodedTiles;

    /// <summary>Which terrain generation <see cref="CachedWoodedTiles"/> was counted against.</summary>
    internal int CachedAtTerrainGeneration = -1;

    /// <summary>
    /// What this place is set to do — <see cref="WorkMode.Plant"/> unless the player says
    /// otherwise (Joe, D136: <i>"forester planting should be ON by default"</i>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Player intent, so it is sim state and it is hashed</b> — but <b>sparsely</b>, in the
    /// same shape and for the same reason as <see cref="QueueRank"/>: a village where nobody has
    /// ever touched a mode mixes nothing at all, so the control is a provable no-op until
    /// somebody uses it and no golden moves for its existing. <b>The sentinel in
    /// <c>StateHash</c> tracks this default and must keep tracking it</b> — it tests against
    /// whatever "untouched" means, not against <c>Harvest</c> by name.
    /// </para>
    /// <para>
    /// <b>⚠️ THE TWO MODES ARE EXCLUSIVE, and this default is worth understanding before
    /// relying on it.</b> <c>SimWorld</c> picks the tiles by mode — <c>wantsTrees = Mode ==
    /// Harvest</c> — so a hut set to <see cref="WorkMode.Plant"/> walks to <em>bare</em> ground
    /// and puts saplings in it, and never fells anything. A new hut on fully wooded ground has
    /// no bare tile to plant and so does nothing at all until the player switches it.
    /// </para>
    /// <para>
    /// <b>⛔ SO PLANTING-BY-DEFAULT WAS TRIED AND REVERTED, AND THE MEASUREMENT IS THE REASON.</b>
    /// Flipping this constant to <see cref="WorkMode.Plant"/> failed <b>eight tests</b> at once,
    /// and the list says exactly what it does: <c>ItFellsItsOwnGroundAndTheWoodRecedes</c>,
    /// <c>ItIsAForestersWorkplace</c>, and — the one that matters —
    /// <c>ALogLimitAboveWhatTheVillageSpendsIsAnAmbitionAndNotAceiling</c>, the stockpile tool
    /// Joe asked for in D130. **A village whose foresters plant by default has no timber
    /// industry at all**, which is the opposite of the complaint driving this branch.
    /// </para>
    /// <para>
    /// Held pending Joe's call, because the likely reading is the other one: <i>plant once
    /// there is nothing left to fell on their own ground</i>. That is a change at the
    /// tile-picking call site (<c>wantsTrees</c> in <c>SimWorld</c>) rather than a default here,
    /// and it gives a forester who replants without ever stopping felling.
    /// </para>
    /// </remarks>
    public WorkMode Mode { get; set; } = DefaultMode;

    /// <summary>
    /// What a place is set to before anybody touches it. <see cref="Determinism.StateHash"/>
    /// tests against this rather than against a named mode, so that changing the default cannot
    /// silently move every golden in the suite — which is a trap that was walked into and out
    /// of once already, and the constant is what stops the next attempt hitting it.
    /// </summary>
    public const WorkMode DefaultMode = WorkMode.Harvest;

    /// <summary>Villagers currently holding a job here, in id order.</summary>
    public List<int> WorkerIds { get; } = new();

    /// <summary>
    /// The building being raised here, when this is a construction site. Null otherwise.
    /// </summary>
    /// <remarks>
    /// Hung off the workplace rather than kept in a parallel list, so a site cannot
    /// exist without somewhere to work at it and a builder cannot hold a job at a site
    /// that has finished. Two lists that must agree is the shape of half the bugs in
    /// this project's history.
    /// </remarks>
    public ConstructionSite? Construction { get; init; }

    /// <summary>
    /// True when this is a construction site rather than a place anybody works at (D108).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A site stopped being staffed when the builder's hut arrived.</b> Builders hold a
    /// job at the hut and treat sites as errands (Joe), so a site has no seats, takes no
    /// workers, and must not be offered to the allocator or described to an idle villager
    /// as somewhere they might have worked.
    /// </para>
    /// <para>
    /// <b>Asked here rather than by comparing <see cref="Capacity"/> to zero</b>, which is
    /// the same thing today and stops being so the moment anything else has no seats. Every
    /// reader that has to skip sites asks this one question — the seam <c>StoreKind</c> ran
    /// to five instalments before anybody named (D76).
    /// </para>
    /// </remarks>
    public bool IsSite => Construction is not null;

    /// <summary>
    /// Goods held at this place.
    /// </summary>
    /// <remarks>
    /// A buffer at the point of production (D30): a few logs beside the stumps, a
    /// little firewood at the hut. Not the village's whole stock — that belongs in a
    /// granary or a shed, and carrying it there is the trip that makes distribution
    /// work somebody does rather than a rule the world enforces from nowhere.
    /// </remarks>
    public Stockpile Store { get; } = new();

    /// <summary>True when there is no room for anyone else.</summary>
    /// <summary>
    /// How many hands the player has insisted on here, or null to let the village
    /// decide (D51).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Null is the default and must stay the default.</b> The pillar this sits
    /// beside (§2.2) exists to delete Banished's slotting of N workers into a building,
    /// and a game that opens with a number on every workplace is that game whatever the
    /// numbers say. An override is opt-in: a player who never touches one plays the
    /// village the quota describes.
    /// </para>
    /// <para>
    /// <b>What is overridden is the count, never the person.</b> The player says how
    /// many; proximity, household and catchment still choose who, and every "why is
    /// Elias here?" sentence stays true. That is D42's zoning pattern applied to
    /// labour — you paint the neighbourhood, the sim picks the tile.
    /// </para>
    /// <para>
    /// Player intent, so it is sim state: hashed, deterministic, part of the seed
    /// contract, exactly as zones are.
    /// </para>
    /// </remarks>
    public int? StaffingOverride { get; set; }

    /// <summary>
    /// Where the player has asked this site to sit in the build queue, or null for
    /// "wherever it was marked" (D105).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Null and a number are different states, exactly as with
    /// <see cref="StaffingOverride"/></b> — "I have not touched this" is not the same fact as
    /// "I put it third", and the state hash keeps them apart. It also means the control is a
    /// provable no-op until somebody uses it: a village played without ever reordering
    /// anything hashes as though the control did not exist.
    /// </para>
    /// <para>
    /// <b>Sorted against <see cref="Id"/> when it is null</b>, so the default order is the
    /// order things were marked and a player who reorders one site does not have to reorder
    /// all of them.
    /// </para>
    /// </remarks>
    public int? QueueRank { get; set; }

    /// <summary>What this site sorts by in the build queue.</summary>
    public int EffectiveQueueRank => QueueRank ?? Id;

    /// <summary>
    /// Hands the village will actually staff here — the override if there is one,
    /// otherwise what physically fits.
    /// </summary>
    /// <remarks>
    /// The single place the two are reconciled. Everything that used to ask
    /// <see cref="Capacity"/> asks this instead, so an override cannot be honoured by
    /// half the code and ignored by the other half — which is this project's most
    /// repeated bug.
    /// </remarks>
    public int Places => StaffingOverride ?? Capacity;

    public bool IsFull => WorkerIds.Count >= Places;

    /// <summary>Room still going spare.</summary>
    public int OpenPositions => Places - WorkerIds.Count;
}
