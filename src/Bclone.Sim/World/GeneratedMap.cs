namespace Bclone.Sim.World;

/// <summary>What a tile is made of.</summary>
/// <remarks>
/// <para>
/// <b>Water is generated but nothing reads it yet</b>, and that is deliberate
/// sequencing rather than an oversight (`specs/seeded-map-generation.md §11`). Making
/// water impassable means real pathfinding in <see cref="TravelCostField"/>, which is
/// the field deciding labour catchment, market errands and the economy's distance
/// budget — the things that decide who eats. Landing worldgen and pathfinding together
/// would mean two hard changes failing at once with no way to tell which.
/// </para>
/// <para>
/// So this slice generates the river and proves the generation is deterministic and
/// survivable; the next slice makes the river mean something; bridges (D40) come after
/// the tech tree and placement.
/// </para>
/// </remarks>
public enum Terrain
{
    /// <summary>Open ground. Walkable, buildable.</summary>
    Grass = 0,

    /// <summary>The river. Impassable once pathfinding lands — see the remarks above.</summary>
    Water = 1,

    /// <summary>Trees. Where a tree stand can go.</summary>
    Forest = 2,
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
