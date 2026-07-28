namespace Bclone.Sim.World;

/// <summary>Kinds of work. Data-driven content lands here in a later pass.</summary>
/// <remarks>
/// <b>On the names:</b> a <see cref="Logger"/> fells trees and a
/// <see cref="Woodcutter"/> splits the logs into firewood (D29). That follows
/// <em>Banished</em>, and it cuts against everyday usage — colloquially a woodcutter
/// is the one with the axe in the forest. The split is deliberate: they are two
/// different jobs in two different places, and the chain between them is the first
/// secondary processing in the game.
/// </remarks>
public enum JobKind
{
    /// <summary>Gather food from a wild source.</summary>
    Forager = 0,

    /// <summary>Fell trees at a stand, producing logs.</summary>
    Logger = 1,

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
/// <see cref="CatchmentRadius"/> is measured in <b>travel cost</b>, not tiles, and
/// read from the one shared <see cref="TravelCostField"/>. That is what kills the
/// villager who walks across the map for one log, and it is what will let a worn path
/// widen a workplace's reach in Phase 3 without either system knowing about the other.
/// </para>
/// </remarks>
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

    /// <summary>How far it is reasonable to travel here, in travel-cost units.</summary>
    public required int CatchmentRadius { get; init; }

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
    public bool IsFull => WorkerIds.Count >= Capacity;

    /// <summary>Room still going spare.</summary>
    public int OpenPositions => Capacity - WorkerIds.Count;
}
