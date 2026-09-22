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

    /// <summary>
    /// The walls on this tile's four edges — N 1, E 2, S 4, W 8 (D401,
    /// `specs/fences-as-walls.md`). <b>A step across a set bit is impossible.</b>
    /// </summary>
    /// <remarks>
    /// A fourth question on the same seam, and the field asks it the same way it asks the others:
    /// <em>is there a wall here?</em> — never <em>whose?</em> A yard's fence writes the bits today
    /// and a player-built fence will write the same ones (§10).
    /// </remarks>
    byte WallsOn(GridPos tile);

    /// <summary>Moves whenever a wall goes up or comes down, so a field knows to rebuild.</summary>
    int WallGeneration { get; }
}
