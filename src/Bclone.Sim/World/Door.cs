namespace Bclone.Sim.World;

/// <summary>The four ways a tile has neighbours — north is −Y, as everywhere in this game.</summary>
public enum Compass
{
    North = 0,
    East = 1,
    South = 2,
    West = 3,
}

/// <summary>What stands in the way of a building's front (`specs/doors.md §4.3`), or nothing.</summary>
public enum DoorBlock
{
    None,

    /// <summary>The map ends there.</summary>
    Edge,

    /// <summary>Ground nobody can walk — water with no deck over it.</summary>
    Water,

    /// <summary>Something stands on it.</summary>
    Built,

    /// <summary>A fence runs between the building and it.</summary>
    Fence,

    /// <summary>Free ground the village does not reach.</summary>
    Unreached,
}

/// <summary>
/// ⭐ A building's door (D619–D621, `specs/doors.md §4.2–§4.3`): the tile of the building it opens from, the free tile
/// outside it, and which way it faces — <b>derived from the footprint and what stands round it, never stored, never
/// hashed</b> (D335: it restates the world).
/// </summary>
/// <param name="Tile">The building's own tile the door is in.</param>
/// <param name="Doorstep">The tile outside it, one step <paramref name="Faces"/> of <paramref name="Tile"/>.</param>
/// <param name="Faces">Which way the door opens.</param>
/// <param name="Front">Which way the building's front faces — the door's way unless the front could not open.</param>
/// <param name="FrontBlockedBy">Why the front could not open, or <see cref="DoorBlock.None"/>.</param>
/// <param name="FrontStep">The tile in front of the front's middle — the one <paramref name="FrontBlockedBy"/> is about.</param>
/// <param name="Shut">No side opens onto ground the village reaches: the door stands on the front, and nobody can use it.</param>
public readonly record struct Door(
    GridPos Tile,
    GridPos Doorstep,
    Compass Faces,
    Compass Front,
    DoorBlock FrontBlockedBy,
    GridPos FrontStep,
    bool Shut);
