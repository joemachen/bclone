namespace Bclone.Sim.World;

/// <summary>
/// What stands on the ground, for the cost field — <b>a tile a building stands on cannot be
/// walked through, only to</b> (D383, `specs/buildings-as-obstacles.md`).
/// </summary>
/// <remarks>
/// <para>
/// The shared cost field (§2.6, D41) asks these three questions and nothing else: has the set of
/// standing tiles changed since I last built a field (<see cref="Generation"/>, a monotonic
/// counter — CLAUDE.md's cheapest honest answer), does something stand on this tile, and which
/// tiles belong to the building standing on this one (so a field whose destination is that
/// building can open its whole footprint, and a villager standing on it can step off).
/// </para>
/// <para>
/// An interface rather than a reference to <c>SimWorld</c>, because <c>TravelCostField</c> is
/// built and tested without a world (`_map` may even be null) and must stay that way.
/// </para>
/// </remarks>
public interface IObstacles
{
    /// <summary>Bumped every time a footprint appears, moves or goes.</summary>
    int Generation { get; }

    /// <summary>Whether a building stands on this tile.</summary>
    bool StandsOn(GridPos tile);

    /// <summary>The tiles of the building standing on this one — empty when nothing does.</summary>
    IReadOnlyList<GridPos> FootprintCovering(GridPos tile);
}
