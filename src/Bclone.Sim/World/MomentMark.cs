namespace Bclone.Sim.World;

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
}
