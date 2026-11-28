namespace Kith.Sim.World;

/// <summary>
/// What a paragraph of a <see cref="Moment"/> is about — named by the sim, drawn by the view (D577).
/// </summary>
/// <remarks>
/// <b>The sim says what, never how it looks</b> — <see cref="BuildingKind"/>'s split: the view owns the shapes and
/// draws nothing for a mark it does not know. Saved by number; never renumber, only append.
/// </remarks>
public enum MomentMark
{
    /// <summary>No mark: the paragraph is drawn without one.</summary>
    None = 0,

    /// <summary>Nomads — the chevron that rides over their heads as they walk in.</summary>
    Nomads = 1,

    /// <summary>A bridge learned.</summary>
    Bridge = 2,

    /// <summary>The stone cottage learned (Joe's play note 7, D894) — the view wears its building's own icon.</summary>
    Cottage = 3,

    /// <summary>The insulated manor learned (D894).</summary>
    Manor = 4,

    /// <summary>W6's first warning, a year on one food (`health.md §5.4`) — the art lane's fed-up face (A314).</summary>
    PlainDiet = 5,

    /// <summary>W6's second, three years on one food — the queasy face (A314).</summary>
    RunDown = 6,
}
