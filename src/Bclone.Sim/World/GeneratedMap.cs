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

    /// <summary>
    /// Stone lying on the ground — a seam you can see and clear (D84, D90).
    /// </summary>
    /// <remarks>
    /// <b>A deposit, so it is finite</b>: a laborer clears it and the ground is grass
    /// underneath. That is the opposite of a quarry, which is a building the player sites
    /// and which never runs out — the same good from two sources, and the difference is
    /// found-versus-placed rather than surface-versus-subsurface.
    /// </remarks>
    Rock = 3,

    /// <summary>Iron in the ground, visible and finite — the far half of the pair.</summary>
    /// <remarks>
    /// <b>Placed further out than stone on purpose.</b> Reaching it is a decision rather
    /// than a stage you pass: a valley whose iron is in the far woods plays differently
    /// from one where it is on the doorstep, which is the argument D67 makes for visible
    /// seams over a percentage roll — <em>"you can see the seam, so going after it is a
    /// decision"</em>.
    /// </remarks>
    IronDeposit = 4,

    /// <summary>
    /// Young trees — <b>a wood on its way back, and worth nothing yet</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The counterweight the design was missing</b> (D125, Joe: <em>"trees should grow
    /// over time… sapling for the first six months, mature tree after a year"</em>). Until
    /// now the only thing that put trees back was a forester in planting mode, which is a
    /// decision the <em>player</em> makes — so a village nobody was managing felled its own
    /// gatherer's ring and starved beside a valley still 97% wooded.
    /// </para>
    /// <para>
    /// <b>⭐ It is its own terrain rather than a timer on a grass tile, and that is the whole
    /// reason it works.</b> A sapling is <em>visible</em>: the player can see their wood
    /// coming back, see where it has not, and see that the ring they cleared last spring is
    /// half-grown rather than gone. A hidden countdown would restore the food supply without
    /// ever explaining itself, which is §1.1 failing in the player's favour — still failing.
    /// </para>
    /// <para>
    /// <b>Worth nothing to anybody until it matures.</b> A gatherer's ring counts
    /// <see cref="Forest"/>, so half a ring of saplings is half a ring of food — the same
    /// "less trees, less food" rule, with the clock now running the other way as well.
    /// </para>
    /// <para>
    /// <b>Appended, never renumbered</b> — terrain is hashed by value, so a new kind of
    /// ground goes on the end or every seed ever written down changes meaning.
    /// </para>
    /// </remarks>
    Sapling = 5,
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

    /// <summary>What clearing one tile of this ground yields, or null if nothing.</summary>
    /// <remarks>
    /// <b>One question, answered in one place</b>, so a new harvestable kind is a row here
    /// rather than a fifth site to remember — which is the seam D76 spent five instalments
    /// learning to recognise. It is deliberately the terrain that knows, not the harvester.
    /// </remarks>
    public static Goods? Yields(Terrain terrain) => terrain switch
    {
        Terrain.Forest => Goods.Logs,
        Terrain.Rock => Goods.Stone,
        Terrain.IronDeposit => Goods.Iron,
        _ => null,
    };
}

/// <summary>
/// Which standing thing the harvest brush is set to take (D67, D90).
/// </summary>
/// <remarks>
/// <para>
/// <b>Modes of one tool, not a layer per material</b> (Joe: *"you pick trees or stone or all
/// and drag"*). The mode filters <em>which tiles take the paint</em> and is then forgotten —
/// a marked tile is simply marked, and what a laborer gets from it is whatever is standing
/// there.
/// </para>
/// <para>
/// <b>That is what makes it cheap and what makes it right.</b> Nothing new is stored, nothing
/// new is hashed, and <see cref="Everything"/> falls out for free as the absence of a filter.
/// It still answers what D67 asked for — <em>clear the stone and leave the wood</em> — because
/// the wood in the dragged area never takes the paint in the first place. A layer per material
/// would store the same fact three times and let a tile be marked for a good it does not have.
/// </para>
/// </remarks>
public enum HarvestBrush
{
    /// <summary>Everything standing: trees, stone and iron alike.</summary>
    Everything = 0,

    /// <summary>Trees only.</summary>
    Trees = 1,

    /// <summary>Stone seams only.</summary>
    Stone = 2,

    /// <summary>Iron seams only.</summary>
    Iron = 3,
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
        FoundingSite = foundingSite;

        // The valley's natural woodland, recorded once. Everything the generator painted as
        // forest is ground a wood may return to; everything else is meadow until somebody
        // plants on it.
        _everWooded = new bool[terrain.Length];
        for (int i = 0; i < terrain.Length; i++)
        {
            _everWooded[i] = terrain[i] == Terrain.Forest;
        }
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Left edge of the valley in world coordinates.</summary>
    public int MinX { get; }

    /// <summary>Bottom edge of the valley in world coordinates.</summary>
    public int MinY { get; }

    // ⭐ `ForageSites` AND `TreeStands` ARE DELETED (`forests-and-gathering.md` slice 5).
    // The map no longer says where food is gathered or where timber is felled, because
    // **the map is no longer what decides that** — the player sites a gatherer's hut and a
    // forester's hut, and the woodland the generator paints across the whole valley is what
    // makes one spot better than another. A list of six berry patches was the last thing in
    // this game that handed the player an economy instead of asking them to build one.

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

        // Ground that has ever held trees remembers it — see `HasEverBeenWooded`.
        if (terrain is Terrain.Forest or Terrain.Sapling)
        {
            _everWooded[index] = true;
        }

        return true;
    }

    /// <summary>
    /// Whether trees have ever stood here — <b>the bound on where a wood may come back</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ MEASURED INTO EXISTENCE (D126).</b> Regrowth that spread wherever it touched wood
    /// took the valley from 2,600 forest tiles to <b>9,257 of 9,600 in fifteen years</b> —
    /// one solid wood, no meadow left, and <c>forest_coverage_percent</c> meaningless a few
    /// years in. Requiring two wooded neighbours instead of one slowed it and did not stop
    /// it: 85% of the valley, because clumps merge and every merge makes new concavities.
    /// </para>
    /// <para>
    /// <b>The honest bound is not a rate, it is a place.</b> A wood grows back where a wood
    /// was; it does not march across open meadow for ever. So regrowth reclaims only ground
    /// that has held trees at some point — which heals a clearing completely, however big,
    /// and leaves the grass that was always grass alone.
    /// </para>
    /// <para>
    /// <b>Planting extends it deliberately</b>, because a planted tile passes through
    /// <see cref="Terrain.Sapling"/> and is recorded here. That is Joe's *"foresters can plant
    /// trees in a painted area — this will allow the user to sculpt their forests to their own
    /// desires"*: the player may put a wood where there never was one, and once they have, it
    /// comes back like any other.
    /// </para>
    /// <para>
    /// <b>Not hashed, and it does not need to be.</b> It is a pure function of the terrain
    /// history — the initial map plus every <c>SetTerrain</c> — and all of that is hashed
    /// already. Two runs that agree on the terrain agree on this.
    /// </para>
    /// </remarks>
    public bool HasEverBeenWooded(GridPos position)
    {
        int index = IndexOf(position);
        return index >= 0 && _everWooded[index];
    }

    private readonly bool[] _everWooded;

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
