namespace Bclone.Sim.World;

/// <summary>What a tile is made of.</summary>
/// <remarks>
/// <para>
/// <b>Water is impassable</b> (D40, D41). <see cref="TravelCostField"/> routes round it,
/// and because every system that asks "how far is that?" shares one cost field (§2.6),
/// labour catchment, market errands, household placement and the economy's distance
/// budget all inherited that for free.
/// </para>
/// <para>
/// It was generated-but-unread for one slice on purpose, because landing worldgen and
/// pathfinding together would have meant two hard changes failing at once with no way to
/// tell which. Bridges (D40) still wait on the tech tree.
/// </para>
/// </remarks>
public enum Terrain
{
    /// <summary>Open ground. Walkable, buildable.</summary>
    Grass = 0,

    /// <summary>The river. Impassable — nothing walks it and nothing is built on it.</summary>
    Water = 1,

    /// <summary>Trees. Where a tree stand can go.</summary>
    Forest = 2,
}

/// <summary>What a kind of ground does to somebody trying to cross it.</summary>
public static class TerrainRules
{
    /// <summary>Whether anything can walk over this kind of ground.</summary>
    /// <remarks>
    /// <para>
    /// <b>Asked of the terrain rather than listed at each call site.</b>
    /// <see cref="Terrain.Water"/> was named in two places in
    /// <see cref="TerrainCostField"/>, and mutable terrain wanted to name it a third — which
    /// is the <c>StoreKind</c> seam in a new costume, and that one ran to five instalments
    /// before the question was replaced instead of the call site (D76).
    /// </para>
    /// <para>
    /// <b>It is the question the cache turns on</b>, too: a terrain change matters to a route
    /// exactly when it changes this answer (`specs/mutable-terrain.md §4.2`).
    /// </para>
    /// </remarks>
    public static bool IsPassable(Terrain terrain) => terrain != Terrain.Water;
}

/// <summary>
/// A valley, generated from the run's seed.
/// </summary>
/// <remarks>
/// <para>
/// The world used to be a handful of literal coordinates in <c>sim.config.json</c>.
/// This is what replaced them (D18), and the reason it matters is not variety alone:
/// the sim is already fully seeded, so the world belongs to the <em>same</em> seed as
/// everything else. Quoting one number reproduces an entire run, world included, which
/// is what makes a shared seed and a bug report work.
/// </para>
/// <para>
/// The config keys that held those coordinates became generator <em>parameters</em> —
/// how many sites, how far out, how wide the river runs. That is the honest data-driven
/// form: a modder controls the rules rather than the outcomes.
/// </para>
/// </remarks>
public sealed class GeneratedMap
{
    private readonly Terrain[] _terrain;
    private readonly byte[] _soil;

    public GeneratedMap(
        int width,
        int height,
        int minX,
        int minY,
        Terrain[] terrain,
        byte[] soilQuality,
        IReadOnlyList<GridPos> forageSites,
        IReadOnlyList<GridPos> treeStands,
        GridPos foundingSite)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(soilQuality);

        Width = width;
        Height = height;
        MinX = minX;
        MinY = minY;
        _terrain = terrain;
        _soil = soilQuality;
        ForageSites = forageSites ?? throw new ArgumentNullException(nameof(forageSites));
        TreeStands = treeStands ?? throw new ArgumentNullException(nameof(treeStands));
        FoundingSite = foundingSite;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Left edge of the valley in world coordinates.</summary>
    public int MinX { get; }

    /// <summary>Bottom edge of the valley in world coordinates.</summary>
    public int MinY { get; }

    /// <summary>Where food is gathered. Spread, per D24 — never clustered in one place.</summary>
    public IReadOnlyList<GridPos> ForageSites { get; }

    /// <summary>Where timber is felled.</summary>
    public IReadOnlyList<GridPos> TreeStands { get; }

    /// <summary>Where the first homes and the village's buildings go.</summary>
    public GridPos FoundingSite { get; }

    /// <summary>What a tile is made of. Out-of-bounds reads as <see cref="Terrain.Grass"/>.</summary>
    public Terrain TerrainAt(GridPos position)
    {
        int index = IndexOf(position);
        return index < 0 ? Terrain.Grass : _terrain[index];
    }

    /// <summary>
    /// Change what a tile is made of. Returns whether it changed anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The one door terrain changes through</b> (`specs/mutable-terrain.md §4.1`), so
    /// <em>"who changed this tile?"</em> has a single answer. Everything else about the map
    /// stays read-only.
    /// </para>
    /// <para>
    /// <b>The valley stopped being immutable here</b>, which D41 predicted a phase early and
    /// which costs one thing: <see cref="TravelCostField"/> caches a flow field per
    /// destination and had no way to drop one. Callers go through
    /// <c>SimWorld.SetTerrain</c>, which owns that. Changing a tile on the map directly is
    /// legal and does not tell anybody — which is fine for the generator, building the world
    /// before anything has cached an opinion about it, and wrong for anything during a run.
    /// </para>
    /// <para>
    /// <b>Out of bounds is refused rather than thrown</b>, matching
    /// <see cref="TerrainAt"/> — a brush dragged off the edge of the valley is an ordinary
    /// thing for a player to do.
    /// </para>
    /// </remarks>
    public bool SetTerrain(GridPos position, Terrain terrain)
    {
        int index = IndexOf(position);
        if (index < 0 || _terrain[index] == terrain)
        {
            return false;
        }

        _terrain[index] = terrain;
        return true;
    }

    /// <summary>Whether a tile is inside the valley at all.</summary>
    public bool Contains(GridPos position) =>
        position.X >= MinX && position.X < MinX + Width
        && position.Y >= MinY && position.Y < MinY + Height;

    private int IndexOf(GridPos position) =>
        Contains(position) ? ((position.Y - MinY) * Width) + (position.X - MinX) : -1;

    /// <summary>Every tile, in a fixed order — for hashing and for drawing.</summary>
    public IReadOnlyList<Terrain> Tiles => _terrain;

    /// <summary>Soil, in the same order as <see cref="Tiles"/>.</summary>
    public IReadOnlyList<byte> Soil => _soil;
}
