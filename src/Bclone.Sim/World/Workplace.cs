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

    /// <summary>
    /// Sow a field in spring and reap it in autumn (`specs/crops-and-orchards.md`, D161).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE FIRST JOB THIS PROJECT HAS ADDED RATHER THAN RENAMED, and the bar it had to
    /// clear is on record twice.</b> <c>Logger → Forester</c> (D96) and the gatherer's hut
    /// (D112) both <em>reused</em> a kind, on the argument that a second value means a second
    /// quota arm, a second slot in the scarcity order, a second plural, a second behaviour
    /// branch and a rule to stop the village staffing both. **A farmer earns all five**, because
    /// none of them is the same as a forager's: the work is on ground the player painted, it
    /// happens in two seasons and not three, and it produces nothing at all in the other two.
    /// Folding it into <see cref="Forager"/> would mean one job whose demand is seasonal in two
    /// incompatible ways.
    /// </para>
    /// <para>
    /// <b>⚠️ AND THE ONE WITH TEETH IS THE DEMAND</b> (`crops-and-orchards.md §11b`).
    /// <c>SetStaffing</c> is a ceiling and not a summons (D146), so if
    /// <see cref="LabourQuota"/> does not actively <em>want</em> farmers when the fields are
    /// ripe, the harvest stands until winter takes it and every guard blames
    /// <c>CropSystem</c>, which will be working perfectly. That is D146's bug waiting one job
    /// over, and it is why the seasonal arm was built and proved before a single tile could be
    /// sown.
    /// </para>
    /// <para>
    /// <b>Appended, never renumbered</b> — hashed by position, like every other value here.
    /// </para>
    /// </remarks>
    Farmer = 5,

    /// <summary>
    /// Works a fishing hut on the riverbank — <b>food that does not run out</b> (Joe, 2026-09-02).
    /// </summary>
    /// <remarks>
    /// <b>⭐ A step up from foraging per worker, and the ranking is deliberate</b>: forager 2 seats,
    /// fisher 4. The seat count is the RANK of a food source now — *"foraging is bottom of the
    /// totem pole"* — and a fishery is the first thing a village should want to leave it for.
    /// </remarks>
    Fisher = 6,

    /// <summary>
    /// Take wild game from the woods — <b>slower than a cast, and worth more</b> (Joe).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, 2026-09-02:</b> *"Hunting ultimately yields more food, but it takes longer and
    /// isn't instantaneous. 3 hunters per hunting lodge."* So the totem pole he has stated, bottom
    /// to top, is <b>foraging → fishing → hunting</b>.
    /// </para>
    /// <para>
    /// ⛔ <b>THE VALUE IS 7 AND IT IS APPENDED, NEVER INSERTED.</b> Job kinds are hashed by
    /// position, so renumbering would silently reinterpret every golden as being about a different
    /// trade — the warning <see cref="Forester"/> already carries, for the same reason.
    /// </para>
    /// </remarks>
    Hunter = 7,
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
    /// <summary>
    /// Put trees back and take none — the wood is resting.
    /// </summary>
    /// <remarks>
    /// <b>⚠️ THIS SLOT MEANT THE OPPOSITE UNTIL D146, AND IT IS THE ONE PLACE THAT MATTERS.</b>
    /// It was <c>Harvest</c> — <em>fell and never plant</em> — and it is now <em>plant and
    /// never fell</em>. Renaming rather than appending keeps the numbering the remark below
    /// promises, and it moves no golden because <c>Mode</c> is hashed <b>sparsely against
    /// <see cref="Workplace.DefaultMode"/></b>: a village where nobody has touched a toggle
    /// mixes nothing at all, and no golden in the suite touches one.
    /// </remarks>
    PlantOnly = 0,

    /// <summary>
    /// Fell what is standing on this hut's ground and put back what is not. The default.
    /// </summary>
    FellAndPlant = 1,
}

public sealed class Workplace
{
    public required int Id { get; init; }

    public required JobKind Kind { get; init; }

    /// <summary>A place name, so the log reads "the north stand" not "Workplace 3".</summary>
    public required string Name { get; init; }

    private GridPos _position;

    /// <summary>Where it stands.</summary>
    /// <remarks>
    /// <b>⚠️ <c>init</c> for building it, <see cref="MoveTo"/> for moving it, and nothing else.</b>
    /// Relocation is a builder's job with a construction site behind it, and this is the one door
    /// it comes through.
    /// </remarks>
    public required GridPos Position { get => _position; init => _position = value; }

    /// <summary>Move it. Only a finished relocation may.</summary>
    internal void MoveTo(GridPos to) => _position = to;

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
    /// What this place is set to do — <see cref="WorkMode.FellAndPlant"/> unless the player
    /// says otherwise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Player intent, so it is sim state and it is hashed</b> — but <b>sparsely</b>, in the
    /// same shape and for the same reason as <see cref="QueueRank"/>: a village where nobody has
    /// ever touched a mode mixes nothing at all, so the control is a provable no-op until
    /// somebody uses it and no golden moves for its existing. <b>The sentinel in
    /// <c>StateHash</c> tracks this default and must keep tracking it</b> — it tests against
    /// whatever "untouched" means, never against a mode by name.
    /// </para>
    /// <para>
    /// <b>⭐ THE TOGGLE IS FELLING, NOT PLANTING (Joe, D146), AND THAT IS THE THIRD READING OF
    /// THIS CONTROL.</b> <i>"Planting should be on by default for a painted area, and felling
    /// should be the toggle on/off now that I think about it."</i> He is right, and the reason
    /// is that <b>painting ground for a hut is already the instruction to keep it wooded</b> —
    /// so planting was never the interesting question. What the player actually decides is
    /// whether they are <em>taking timber out of this wood</em> or letting it come back.
    /// </para>
    /// <para>
    /// <b>It also collapses two mechanisms into one</b>, which is why it is worth the rename: a
    /// met Logs limit is simply <em>felling off, temporarily</em>. `SimWorld.MayFell` is the one
    /// place that answers it, and the toggle and the cap now go through it together instead of
    /// being two rules that could disagree (D145's one-door finding, applied the day after).
    /// </para>
    /// <para>
    /// <b>The state this deletes is fell-only</b>, and it is not missed: clearing a wood for
    /// good is what the harvest brush does (D87), and the brush has been a standing instruction
    /// since D127. A forester's ground is a <em>managed</em> wood by definition.
    /// </para>
    /// <para>
    /// The two readings before this one, kept because each cost something. D137: the modes were
    /// exclusive — <c>wantsTrees = Mode == Harvest</c> — so a hut set to plant never felled, and
    /// flipping the default on that reading failed eight tests including D130's stockpile tool.
    /// D137 fixed the reading rather than the default, giving a forester who <em>tends</em>.
    /// D142 then found that the reading had reached two of its three call sites, and every fell
    /// in the village had been billed at <c>PlantTicks</c> ever since.
    /// </para>
    /// </remarks>
    public WorkMode Mode { get; set; } = DefaultMode;

    /// <summary>
    /// What a place is set to before anybody touches it. <see cref="Determinism.StateHash"/>
    /// tests against this rather than against a named mode, so that changing the default cannot
    /// silently move every golden in the suite — which is a trap that was walked into and out
    /// of once already, and the constant is what stops the next attempt hitting it.
    /// </summary>
    public const WorkMode DefaultMode = WorkMode.FellAndPlant;

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
    /// <para>
    /// A buffer at the point of production (D30): a few logs beside the stumps, a
    /// little firewood at the hut. Not the village's whole stock — that belongs in a
    /// granary or a warehouse, and carrying it there is the trip that makes distribution
    /// work somebody does rather than a rule the world enforces from nowhere.
    /// </para>
    /// <para>
    /// <b>⭐ IT WAS DEAD FROM D30 UNTIL THE FARM, and <c>init</c> is what woke it up.</b>
    /// `professions.md §4` lists <em>a local store with a stated cap</em> as the fifth part of
    /// every profession and records that this one *"exists and is dead"* — nothing in the sim
    /// had ever written to it, and the building panel carried a branch that could never be
    /// true. A farm holds up to <c>farm_store_cap</c> of its own harvest (Joe), so the store
    /// now needs a capacity it can be given at construction rather than one fixed at
    /// <see cref="int.MaxValue"/> for everything.
    /// </para>
    /// <para>
    /// <b>⚠️ Everything else still gets the uncapped default</b>, which is exactly as dead as
    /// it was — this is not the slice that gives the woodcutter a yard. `professions.md §11`
    /// planned to prove the local store on the forester and the woodcutter and it is proved
    /// here instead, on Joe's call; that spec's ordering is corrected rather than left to
    /// disagree (D159's lesson).
    /// </para>
    /// </remarks>
    public required Stockpile Store { get; init; }

    // -----------------------------------------------------------------
    //  ⭐⭐ WHAT THIS FARM HAS LEARNED IT CAN BRING IN
    //  (`specs/per-site-yield.md §4.2a`, D194)
    // -----------------------------------------------------------------
    //
    // ⛔ THE THING THESE FOUR FIELDS REPLACE WAS A PREDICTION, AND IT WAS WRONG IN THE
    // DIRECTION THAT MAKES ITSELF TRUE. `SimWorld.ReapableShareAt` scaled a farm's field by
    // `budgeted ÷ haul` — and those are not the same kind of quantity: `budgeted` is a ROUND
    // TRIP inside the field (4 ticks) and `haul` is a ONE-WAY walk to a store (10). Measured,
    // it left a farm ten ticks out committing five tiles when it could bring in six, and then
    // RESTING FOR 27% OF THE AUTUMN it was supposedly too busy for. At sixteen ticks 45%; at
    // twenty-two, 55%. **The cap cut the field and the idleness read back as proof the field
    // had been too big.**
    //
    // ⭐ AND NO FORMULA REPLACES IT, WHICH IS THE FINDING RATHER THAN A SHRUG. The true ceiling
    // depends on how fast the market drains the farm's buffer, on the shape of the painted
    // ground, on how full the granary is and on how many hands turned up. The measured curve
    // fits no closed form — `season ÷ (reap + walk)` wants a different constant at every
    // distance, and the constant moves the wrong way with distance. **A spring-time formula is
    // a guess by construction.** So the farm remembers instead.

    /// <summary>
    /// Tiles standing on this farm's ground when autumn began — <b>the year's lesson, and the
    /// gate on there being one</b>.
    /// </summary>
    /// <remarks>
    /// <b>⚠️ NOT OPTIONAL, AND THE TRAP IT CLOSES IS SUBTLE.</b> A farm held by a met stock
    /// limit sows nothing and ends autumn with nothing standing — <b>which is indistinguishable
    /// from having cleared its field</b>. Without this gate such a farm would read years of
    /// idleness as years of success, climb to the derived cap, and over-commit the moment the
    /// player raised the limit. <i>A year with no crop teaches nothing.</i>
    /// </remarks>
    public int FieldTilesSown { get; set; }

    /// <summary>Hands standing in this farm when autumn opened, for reading the lesson per hand.</summary>
    /// <remarks>
    /// <b>⚠️ Taken at the START of autumn, not at its end, and the difference is a bug.</b>
    /// D44 stands seasonal trades down at winter, so a farm can be <em>empty</em> on the very
    /// tick the lesson is taken — dividing that autumn's harvest by nought hands, or by the one
    /// straggler still there, would make a two-handed farm's record look twice what it was.
    /// </remarks>
    public int FieldHandsAtAutumn { get; set; }

    /// <summary>
    /// Tiles <b>one pair of hands</b> at this farm has learned it can actually bring in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Per hand, like <c>SimWorld.WorkGroundAllowanceFor</c></b>, so losing a farmhand in
    /// summer halves next spring's field without the farm forgetting what it knows.
    /// </para>
    /// <para>
    /// <b>Zero means it has never sown anything</b>, and that is the state a brand-new farm is
    /// in — <see cref="Core.SimWorld.HarvestOneFarmCanBringIn"/> falls back to the old
    /// prediction as an opening guess. The prediction is demoted from a ruling to a first
    /// guess rather than deleted, so <b>nobody is ever worse than today</b>.
    /// </para>
    /// </remarks>
    public int FieldTilesLearned { get; set; }

    /// <summary>
    /// The walk to the nearest food store that <see cref="FieldTilesLearned"/> was learned at.
    /// </summary>
    /// <remarks>
    /// <b>⭐ A memory that cannot be revised by the player is a scar.</b> The farm learned an
    /// answer about a walk; when the player builds a granary by the fields — or demolishes the
    /// near one — the old answer stopped being true, and the farm re-reckons from a fresh
    /// guess. <b>That is the lever that actually buys the thirteen tiles</b>, and it is the
    /// reason §4.3's placement warning ships in the same slice.
    /// </remarks>
    public int FieldWalkWhenLearned { get; set; } = -1;

    /// <summary>True when there is no room for anyone else.</summary>
    /// <summary>
    /// How many hands the player has insisted on here, or null to let the village
    /// decide (D51).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ NULL IS NO LONGER REACHABLE FROM THE GAME, AND THE IDEA IT NAMED IS GONE</b>
    /// (Joe, 2026-08-16: *"i want village decides gone entirely from all aspects of the game
    /// for now"*). It survives as one thing only: <em>nobody has turned this building down
    /// yet</em>, which is why <see cref="Places"/> falls back to <see cref="Capacity"/>. There
    /// is no control that returns a building to it, and no label that mentions it.
    /// </para>
    /// <para>
    /// <b>⚠️ It is kept rather than replaced with an explicit seed for one measured reason:</b>
    /// the state hash mixes it <em>sparsely</em>, so a village nobody has staffed by hand
    /// hashes exactly as it did before this control existed. Seeding every workplace at its
    /// capacity would be behaviourally identical (that is what the fallback already does) and
    /// would move every golden in the suite for no change in the game — a cost with no payoff.
    /// </para>
    /// <para>
    /// <b>Null was the default and stays the default.</b> The pillar this sits
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
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ A DEMOLITION LEADS THE QUEUE, AND WITHOUT THAT A BUSY VILLAGE WOULD NEVER PULL
    /// ANYTHING DOWN.</b> A demolition site takes a fresh workplace id, and the fallback rank is
    /// the id — so it sorted **behind every site already marked**, and a player who keeps building
    /// keeps pushing it back for ever. *Marking something for demolition and watching nothing
    /// happen is the shape Joe hit.*
    /// </para>
    /// <para>
    /// <b>⭐ It is also the right order on its own terms:</b> demolition is cheap (half the work,
    /// no materials) and is usually **clearing the way for the very thing behind it in the queue**.
    /// Taking it first is what a crew would do. ⚠️ It cannot starve construction for long — there
    /// is a bounded number of things standing to pull down, and each is quick.
    /// </para>
    /// <para>
    /// ⚠️ <b>Demolitions still sort among themselves by id</b>, so the order they were marked in
    /// survives — the same promise the ordinary fallback makes.
    /// </para>
    /// </remarks>
    public int EffectiveQueueRank =>
        Construction is { Demolishing: true } ? int.MinValue / 2 + Id : QueueRank ?? Id;

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
