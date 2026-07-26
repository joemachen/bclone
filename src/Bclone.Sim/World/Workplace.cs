namespace Bclone.Sim.World;

/// <summary>Kinds of work. Data-driven content lands here in a later pass.</summary>
public enum JobKind
{
    /// <summary>Gather food from a wild source.</summary>
    Forager = 0,

    /// <summary>Cut timber from a stand of trees.</summary>
    Woodcutter = 1,
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

    /// <summary>True when there is no room for anyone else.</summary>
    public bool IsFull => WorkerIds.Count >= Capacity;

    /// <summary>Room still going spare.</summary>
    public int OpenPositions => Capacity - WorkerIds.Count;
}
