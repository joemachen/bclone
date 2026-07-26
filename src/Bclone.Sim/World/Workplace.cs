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
    /// How many workers this place wants. <b>Recomputed each season</b> for places
    /// whose demand depends on the village, like feeding it.
    /// </summary>
    public int LabourDemand { get; set; }

    /// <summary>How far it is reasonable to travel here, in travel-cost units.</summary>
    public required int CatchmentRadius { get; init; }

    /// <summary>Villagers currently holding a job here, in id order.</summary>
    public List<int> WorkerIds { get; } = new();

    /// <summary>True when nobody else is wanted.</summary>
    public bool IsFullyStaffed => WorkerIds.Count >= LabourDemand;

    /// <summary>Workers still wanted.</summary>
    public int OpenPositions => LabourDemand - WorkerIds.Count;
}
