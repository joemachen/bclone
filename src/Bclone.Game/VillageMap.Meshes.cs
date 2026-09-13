using System.Diagnostics;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐⭐ The half of the map that is <b>meshes built on a counter</b>, not commands issued a frame
/// (D366): the scenery that stands still, the trails, and the instrument that says what a frame
/// costs.
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe: *"not sure whats happening with the FPS. its really dropping"* — 22 fps at a near zoom
/// over a dense path network, 55 zoomed out.</b> It was not the sim. It was the drawing, and it
/// was the D338 mistake twice over: above <c>TreeZoomFloor</c> every canopy, sapling, boulder and
/// berry was a live <c>DrawCircle</c> every frame — D338 measured that as ~5,740 unbatchable
/// polygons and fixed it for the far view only — and D359–D362 laid the trails on top as a disc
/// per worn tile plus a polyline per neighbour pair, with the L-corner and joining rules recomputed
/// per tile per frame.
/// </para>
/// <para>
/// ⭐ <b>Now:</b> the scenery is <see cref="MeshBuilder"/> fans in tile space, chunked
/// <see cref="ChunkTiles"/> square, each chunk one <see cref="ArrayMesh"/> rebuilt only when a tile
/// in it changed (a shadow copy of the terrain is diffed on <c>SimWorld.TerrainGeneration</c>, the
/// <see cref="ValleyTexture"/> shape — *it cannot miss a change the way a dirty list can*). The
/// trails are one mesh with a surface per grade, built inside <c>CollectTheTrailsIfTheyMoved</c>
/// on <c>PathWear.Generation</c>, where the D359/D360 rules already ran once a season. Each is
/// drawn with one tile→screen <see cref="Transform2D"/>, so zoom and pan cost nothing.
/// </para>
/// <para>
/// ⚠️ <b>The instrument is the other half of the fix.</b> <see cref="LastFrame"/> times each pass
/// of <c>_Draw</c> and the debug line shows it beside the fps, so the next regression is a number
/// on screen and not a feeling. *The fps line was the only frame-time instrument the game had.*
/// </para>
/// </remarks>
public partial class VillageMap
{
    // ---------------------------------------------------------------
    //  The instrument
    // ---------------------------------------------------------------

    /// <summary>What the last frame's <c>_Draw</c> spent, in milliseconds, per pass — smoothed.</summary>
    /// <remarks>
    /// CPU time to <em>issue</em> the commands, which is where an unbatched circle costs; the GPU's
    /// half is not measured here. Smoothed a tenth a frame so the readout can be read.
    /// </remarks>
    public readonly struct FrameCost
    {
        public FrameCost(double trees, double trails, double fields, double zones, double rest)
        {
            Trees = trees;
            Trails = trails;
            Fields = fields;
            Zones = zones;
            Rest = rest;
        }

        public double Trees { get; }

        public double Trails { get; }

        public double Fields { get; }

        public double Zones { get; }

        public double Rest { get; }

        public double Total => Trees + Trails + Fields + Zones + Rest;

        public override string ToString() =>
            $"draw {Total:F1}ms · trees {Trees:F1} · trails {Trails:F1} · fields {Fields:F1} · zones {Zones:F1} · rest {Rest:F1}";
    }

    /// <summary>The smoothed cost of the last frames' drawing, for the debug readout.</summary>
    public FrameCost LastFrame => new(_treesMs, _trailsMs, _fieldsMs, _zonesMs, _restMs);

    private double _treesMs;
    private double _trailsMs;
    private double _fieldsMs;
    private double _zonesMs;
    private double _restMs;

    /// <summary>One pass's raw milliseconds this frame, before smoothing.</summary>
    private double _treesNow;
    private double _trailsNow;
    private double _fieldsNow;
    private double _zonesNow;

    private static double Since(long started) => Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    /// <summary>Fold this frame's raw pass times into the smoothed readout. Called at the end of <c>_Draw</c>.</summary>
    private void RecordTheFrame(long frameStarted)
    {
        double total = Since(frameStarted);
        double rest = total - _treesNow - _trailsNow - _fieldsNow - _zonesNow;

        _treesMs = Smooth(_treesMs, _treesNow);
        _trailsMs = Smooth(_trailsMs, _trailsNow);
        _fieldsMs = Smooth(_fieldsMs, _fieldsNow);
        _zonesMs = Smooth(_zonesMs, _zonesNow);
        _restMs = Smooth(_restMs, rest);

        _treesNow = _trailsNow = _fieldsNow = _zonesNow = 0d;

        static double Smooth(double was, double now) => was == 0d ? now : (was * 0.9) + (now * 0.1);
    }

    // ---------------------------------------------------------------
    //  Tile space → screen, as a matrix
    // ---------------------------------------------------------------

    /// <summary>
    /// <see cref="ToScreen(Vector2)"/> as a transform — the same numbers, so a mesh built in tile
    /// space lands exactly where a point drawn through <c>ToScreen</c> would.
    /// </summary>
    private Transform2D TileToScreen() =>
        new(
            _pixelsPerTile, 0f, 0f, _pixelsPerTile,
            (Size.X / 2f) - (_centreTile.X * _pixelsPerTile),
            (Size.Y / 2f) - (_centreTile.Y * _pixelsPerTile));

    // ---------------------------------------------------------------
    //  The scenery: canopies, saplings, boulders, ore, berries
    // ---------------------------------------------------------------

    /// <summary>Tiles along one side of a scenery chunk.</summary>
    /// <remarks>
    /// Eight, measured by the probe: the shipped valley is 150 chunks and 344k vertices, built in
    /// ~26ms once at first sight, and a felled tile rebuilds its chunk in ~0.25ms (the busiest,
    /// 7.8k vertices) — which is what matters, because every fell bumps <c>TerrainGeneration</c>
    /// and at 20× that is up to fifteen times a second. Sixteen-tile chunks were 40 chunks, 22ms
    /// once and about four times the cost a fell; the whole valley at once would be the 22ms a
    /// fell, which is a hitch.
    /// </remarks>
    private const int ChunkTiles = 8;

    /// <summary>One chunk's meshes. The instances are kept and their surfaces replaced, so nothing waits on the GC.</summary>
    private sealed class SceneryChunk
    {
        public readonly ArrayMesh Standing = new();
        public readonly ArrayMesh Berries = new();
        public int StandingVertices;
        public int BerryVertices;
    }

    private SceneryChunk[] _scenery = System.Array.Empty<SceneryChunk>();
    private Terrain[] _sceneryShadow = System.Array.Empty<Terrain>();
    private GeneratedMap? _sceneryOf;
    private int _sceneryAtGeneration = -1;
    private float _berryRadiusBuilt = -1f;
    private int _chunksAcross;
    private int _chunksDown;
    private readonly MeshBuilder _standing = new();
    private readonly MeshBuilder _berries = new();

    /// <summary>How long the last scenery build took and how many chunks it touched — for the probe.</summary>
    public double LastSceneryBuildMs { get; private set; }

    public int LastSceneryChunksBuilt { get; private set; }

    /// <summary>
    /// A berry's radius in tiles at this zoom: seven hundredths of a tile, but never under a
    /// pixel — the floor the live pass had. Quantised to the zoom, so the berry meshes rebuild
    /// only when the floor engages (below ~14 px a tile) and the zoom steps.
    /// </summary>
    private float BerryRadiusTiles() => Mathf.Max(0.07f, 1f / _pixelsPerTile);

    /// <summary>
    /// Bring the scenery meshes up to date — <b>incrementally, on the terrain counter</b>.
    /// </summary>
    /// <remarks>
    /// The whole valley on the first sight of a map; afterwards only the chunks whose tiles the
    /// shadow copy says changed. Berries alone are rebuilt when their zoom-floored radius moves.
    /// </remarks>
    private void RefreshTheScenery(SimWorld world, int generation = -1)
    {
        GeneratedMap map = world.Map;
        float berryRadius = BerryRadiusTiles();
        int now = generation < 0 ? world.TerrainGeneration : generation;

        bool fresh = !ReferenceEquals(map, _sceneryOf);
        if (!fresh && now == _sceneryAtGeneration && berryRadius == _berryRadiusBuilt)
        {
            return;
        }

        long started = Stopwatch.GetTimestamp();
        int built = 0;

        if (fresh)
        {
            _sceneryOf = map;
            _chunksAcross = (map.Width + ChunkTiles - 1) / ChunkTiles;
            _chunksDown = (map.Height + ChunkTiles - 1) / ChunkTiles;
            _scenery = new SceneryChunk[_chunksAcross * _chunksDown];
            _sceneryShadow = new Terrain[map.Width * map.Height];
            for (int i = 0; i < _scenery.Length; i++)
            {
                _scenery[i] = new SceneryChunk();
            }

            for (int i = 0; i < _sceneryShadow.Length; i++)
            {
                _sceneryShadow[i] = map.Tiles[i];
            }

            for (int i = 0; i < _scenery.Length; i++)
            {
                BuildChunk(map, i, standing: true, berries: true, berryRadius);
                built++;
            }
        }
        else
        {
            bool berriesToo = berryRadius != _berryRadiusBuilt;
            var dirty = new bool[_scenery.Length];

            if (now != _sceneryAtGeneration)
            {
                System.Collections.Generic.IReadOnlyList<Terrain> tiles = map.Tiles;
                for (int i = 0; i < _sceneryShadow.Length; i++)
                {
                    if (_sceneryShadow[i] == tiles[i])
                    {
                        continue;
                    }

                    _sceneryShadow[i] = tiles[i];
                    dirty[ChunkOf(i % map.Width, i / map.Width)] = true;
                }
            }

            for (int i = 0; i < _scenery.Length; i++)
            {
                if (dirty[i] || berriesToo)
                {
                    BuildChunk(map, i, standing: dirty[i], berries: dirty[i] || berriesToo, berryRadius);
                    built++;
                }
            }
        }

        _sceneryAtGeneration = now;
        _berryRadiusBuilt = berryRadius;
        LastSceneryBuildMs = Since(started);
        LastSceneryChunksBuilt = built;
    }

    private int ChunkOf(int localX, int localY) => ((localY / ChunkTiles) * _chunksAcross) + (localX / ChunkTiles);

    /// <summary>Rebuild one chunk's meshes from the terrain under it.</summary>
    /// <remarks>
    /// Canopies first and lumps after, so a boulder beside a wood sits over the leaves, as the two
    /// live passes ordered them. Berries are their own mesh because the player can switch them off.
    /// </remarks>
    private void BuildChunk(GeneratedMap map, int chunk, bool standing, bool berries, float berryRadius)
    {
        SceneryChunk into = _scenery[chunk];
        int fromX = map.MinX + ((chunk % _chunksAcross) * ChunkTiles);
        int fromY = map.MinY + ((chunk / _chunksAcross) * ChunkTiles);
        int toX = Mathf.Min(fromX + ChunkTiles, map.MinX + map.Width);
        int toY = Mathf.Min(fromY + ChunkTiles, map.MinY + map.Height);

        if (standing)
        {
            _standing.Clear();
            for (int y = fromY; y < toY; y++)
            {
                for (int x = fromX; x < toX; x++)
                {
                    var tile = new GridPos(x, y);
                    Terrain terrain = map.TerrainAt(tile);
                    if (terrain is Terrain.Forest or Terrain.Sapling)
                    {
                        Canopies(_standing, tile, grown: terrain == Terrain.Forest);
                    }
                }
            }

            for (int y = fromY; y < toY; y++)
            {
                for (int x = fromX; x < toX; x++)
                {
                    var tile = new GridPos(x, y);
                    Terrain terrain = map.TerrainAt(tile);
                    if (terrain is Terrain.Rock or Terrain.IronDeposit)
                    {
                        Lumps(_standing, tile, stone: terrain == Terrain.Rock);
                    }
                }
            }

            into.Standing.ClearSurfaces();
            _standing.AddSurfaceTo(into.Standing);
            into.StandingVertices = _standing.VertexCount;
        }

        if (berries)
        {
            _berries.Clear();
            Color colour = GoodsPalette.ColourOf(Goods.Produce);
            for (int y = fromY; y < toY; y++)
            {
                for (int x = fromX; x < toX; x++)
                {
                    var tile = new GridPos(x, y);
                    if (!IsWoodland(tile) || Scramble(x + ForageSalt, y - ForageSalt) % TilesPerBerryPatch != 0)
                    {
                        continue;
                    }

                    BerryPatch(_berries, new Vector2(x, y), Scramble(x, y), berryRadius, colour);
                }
            }

            into.Berries.ClearSurfaces();
            _berries.AddSurfaceTo(into.Berries);
            into.BerryVertices = _berries.VertexCount;
        }
    }

    /// <summary>
    /// Draw every chunk that touches the visible tiles — a mesh a chunk, one transform. With
    /// <paramref name="berries"/> set, the berry meshes rather than the standing ones.
    /// </summary>
    private void DrawTheSceneryChunks(int minX, int maxX, int minY, int maxY, bool berries)
    {
        GeneratedMap map = _sceneryOf!;
        Transform2D toScreen = TileToScreen();
        Rid canvas = GetCanvasItem();

        // A canopy overhangs its tile by up to CanopySpread + CanopyRadius, under a tile, so a
        // chunk one tile off the screen's edge can still have leaves on it.
        int firstCx = Mathf.Clamp((minX - 1 - map.MinX) / ChunkTiles, 0, _chunksAcross - 1);
        int lastCx = Mathf.Clamp((maxX + 1 - map.MinX) / ChunkTiles, 0, _chunksAcross - 1);
        int firstCy = Mathf.Clamp((minY - 1 - map.MinY) / ChunkTiles, 0, _chunksDown - 1);
        int lastCy = Mathf.Clamp((maxY + 1 - map.MinY) / ChunkTiles, 0, _chunksDown - 1);

        for (int cy = firstCy; cy <= lastCy; cy++)
        {
            for (int cx = firstCx; cx <= lastCx; cx++)
            {
                SceneryChunk chunk = _scenery[(cy * _chunksAcross) + cx];
                ArrayMesh mesh = berries ? chunk.Berries : chunk.Standing;
                if (mesh.GetSurfaceCount() > 0)
                {
                    RenderingServer.CanvasItemAddMesh(canvas, mesh.GetRid(), toScreen);
                }
            }
        }
    }

    /// <summary>The trees standing on one tile, and the ones leaning off it — into the mesh.</summary>
    private static void Canopies(MeshBuilder into, GridPos tile, bool grown)
    {
        float radius = grown ? CanopyRadius : 0.13f;
        Color trunk = grown ? TreeCanopy : SaplingCanopy;

        for (int i = 0; i < TreesOn(tile, grown); i++)
        {
            (Vector2 where, float shade) = CanopyOn(tile, i);
            into.Disc(where, radius * shade, trunk with { R = trunk.R * shade, G = trunk.G * shade, B = trunk.B * shade });
        }
    }

    /// <summary>The lumps on one seam tile, and the ones spilling off it — into the mesh.</summary>
    private static void Lumps(MeshBuilder into, GridPos tile, bool stone)
    {
        Color base_ = stone ? Boulder : OreLump;

        for (int i = 0; i < LumpsOn(tile, stone); i++)
        {
            (Vector2 where, float size, float shade) = LumpOn(tile, i);
            into.Disc(where, size, base_ with { R = base_.R * shade, G = base_.G * shade, B = base_.B * shade });
        }
    }

    /// <summary>
    /// A handful of berries under the trees — <b>still, where the animals move</b>.
    /// </summary>
    /// <remarks>
    /// Three dots rather than one, so a patch reads as growing rather than as a good somebody
    /// dropped — <c>DrawHeaps</c> already owns the single-square shape. Their arrangement
    /// comes off the tile's own seed, so no two patches are laid out alike and none of them move.
    /// </remarks>
    private static void BerryPatch(MeshBuilder into, Vector2 home, uint seed, float radius, Color colour)
    {
        for (int i = 0; i < 3; i++)
        {
            double angle = (((seed >> (i * 5)) % 628) / 100.0) + (i * 2.1);
            float spread = 0.16f + (((seed >> (i * 3)) % 10) / 100f);

            var at = new Vector2(
                home.X + ((float)System.Math.Cos(angle) * spread),
                home.Y + ((float)System.Math.Sin(angle) * spread));

            into.Disc(at, radius, colour);
        }
    }

    /// <summary>
    /// The scenery is meshed, chunked and sized — <b>a probe line</b> (D366).
    /// </summary>
    /// <remarks>
    /// Says how many chunks the valley is, how many vertices the standing things and the berries
    /// came to, how long the whole valley took to build once, and — the number the design rests on
    /// — how long ONE chunk takes to rebuild, which is what a fell costs at 20×.
    /// </remarks>
    public string TheSceneryIsMeshed()
    {
        SimWorld world = _world!;
        RefreshTheScenery(world);
        double whole = LastSceneryBuildMs;
        int chunksBuilt = LastSceneryChunksBuilt;

        int standing = 0;
        int berries = 0;
        int busiest = 0;
        int busiestChunk = 0;
        for (int i = 0; i < _scenery.Length; i++)
        {
            standing += _scenery[i].StandingVertices;
            berries += _scenery[i].BerryVertices;
            if (_scenery[i].StandingVertices > busiest)
            {
                busiest = _scenery[i].StandingVertices;
                busiestChunk = i;
            }
        }

        // One chunk, rebuilt alone — the busiest, so the number is the worst case. Twice, because
        // the first rebuild of a chunk that already has a surface pays a one-off (measured 1.9ms
        // against 0.25ms steady); the second is what a fell costs in play.
        long started = Stopwatch.GetTimestamp();
        BuildChunk(world.Map, busiestChunk, standing: true, berries: true, BerryRadiusTiles());
        double first = Since(started);
        started = Stopwatch.GetTimestamp();
        BuildChunk(world.Map, busiestChunk, standing: true, berries: true, BerryRadiusTiles());
        double one = Since(started);

        if (standing == 0)
        {
            return "[widths] scenery: ⛔ no canopy, sapling or lump was meshed — the valley draws bare";
        }

        // ⛔ AND A FELLED TILE STOPS HAVING TREES, IN ITS OWN CHUNK ONLY. The whole-valley build
        // above never runs the diff; this does. One woodland tile is felled by hand, the counter
        // the view watches is bumped by hand (a guard, not a village), and exactly one chunk must
        // rebuild, lighter by that tile's fans; put back, it must come back to the same count.
        GridPos felled = default;
        bool found = false;
        for (int i = 0; i < world.Map.Tiles.Count && !found; i++)
        {
            if (world.Map.Tiles[i] == Terrain.Forest)
            {
                felled = new GridPos(world.Map.MinX + (i % world.Map.Width), world.Map.MinY + (i / world.Map.Width));
                found = true;
            }
        }

        if (!found)
        {
            return "[widths] scenery: ⛔ no woodland tile to fell — the incremental rebuild has checked nothing";
        }

        int chunk = ChunkOf(felled.X - world.Map.MinX, felled.Y - world.Map.MinY);
        int before = _scenery[chunk].StandingVertices;
        int expectedDrop = TreesOn(felled, grown: true) * 36;
        world.Map.SetTerrain(felled, Terrain.Grass);
        RefreshTheScenery(world, generation: _sceneryAtGeneration + 1);
        int rebuilt = LastSceneryChunksBuilt;
        int after = _scenery[chunk].StandingVertices;
        world.Map.SetTerrain(felled, Terrain.Forest);
        RefreshTheScenery(world, generation: _sceneryAtGeneration + 1);
        int restored = _scenery[chunk].StandingVertices;
        _sceneryAtGeneration = world.TerrainGeneration;

        if (rebuilt != 1 || before - after != expectedDrop || restored != before)
        {
            return $"[widths] scenery: ⛔ felling one tile rebuilt {rebuilt} chunks and its chunk went "
                + $"{before} → {after} → {restored} vertices (wanted one chunk, a drop of {expectedDrop}, and back) "
                + "— the scenery is not following the terrain";
        }

        return $"[widths] scenery: ✅ {_scenery.Length} chunks of {ChunkTiles}x{ChunkTiles}, "
            + $"{standing} standing vertices and {berries} berry vertices; whole valley "
            + $"{whole:F1}ms once ({chunksBuilt} chunks), the busiest chunk alone {one:F2}ms "
            + $"({busiest} vertices; {first:F2}ms the first time, warming up); a felled tile "
            + $"rebuilds one chunk and takes {expectedDrop} vertices with it";
    }

    // ---------------------------------------------------------------
    //  The trails
    // ---------------------------------------------------------------

    private readonly ArrayMesh _trailMesh = new();
    private readonly MeshBuilder _trailBuilder = new();

    /// <summary>Vertices the last trail build laid down, worn then packed — for the probe.</summary>
    public int TrailVerticesWorn { get; private set; }

    /// <summary>The area, in tiles, the yards covered on each surface (D368) — for the probe.</summary>
    public float TrailBlockAreaWorn { get; private set; }

    public float TrailBlockAreaPacked { get; private set; }

    public int TrailVerticesPacked { get; private set; }

    public double LastTrailBuildMs { get; private set; }

    /// <summary>
    /// The trails as one mesh, a surface per grade — <b>the D359/D360 rules run once a
    /// season, not once a tile a frame</b> (D366).
    /// </summary>
    /// <remarks>
    /// Exactly what the live pass drew: a disc at every worn tile's point, a band from a lone
    /// neighbour's midway to the point, and a bent band through the point between every pair of
    /// joined neighbours — each on the surface of the least-worn of the tiles it touches, worn
    /// under packed. In tile space; the transform does the zoom.
    /// </remarks>
    private void BuildTheTrailMesh()
    {
        long started = Stopwatch.GetTimestamp();
        _trailMesh.ClearSurfaces();
        TrailVerticesWorn = 0;
        TrailVerticesPacked = 0;

        System.Span<GridPos> joined = stackalloc GridPos[8];
        System.Span<Vector2> bend = stackalloc Vector2[BendSegments + 1];

        for (byte pass = 1; pass <= 2; pass++)
        {
            Color colour = pass == 2 ? PackedPath : WornPath;
            _trailBuilder.Clear();

            // ⭐⭐ THE YARDS FIRST (D368): every block of worn tiles as one smoothed patch — the
            // paint's tracer at a cell a tile, ear-clipped — on this pass's surface for the tiles
            // of this grade (worn is the whole block; packed the packed part of it, over it).
            HashSet<Vector2I> yard = pass == 2 ? _packedBlockTiles : _blockTiles;
            float area = 0f;
            if (yard.Count > 0)
            {
                Vector2[] patch = ZoneOutline.Fill(ZoneOutline.Trace(yard, 1), yard);
                _trailBuilder.Triangles(patch, colour);
                for (int i = 0; i + 2 < patch.Length; i += 3)
                {
                    Vector2 a = patch[i + 1] - patch[i];
                    Vector2 b = patch[i + 2] - patch[i];
                    area += Mathf.Abs((a.X * b.Y) - (a.Y * b.X)) / 2f;
                }
            }

            if (pass == 1)
            {
                TrailBlockAreaWorn = area;
            }
            else
            {
                TrailBlockAreaPacked = area;
            }

            for (int i = 0; i < _trail.Count; i++)
            {
                (GridPos tile, byte grade) = _trail[i];
                Vector2 here = TrailPointOf(tile);
                int count = JoinedNeighbours(tile, joined);

                // A block tile is drawn by its yard: no disc, no bends — only a bridge from its
                // centre to the midway of each lane that meets it, so the lane's half-bend and
                // the smoothed patch never leave a gap between them.
                if (_blockTiles.Contains(new Vector2I(tile.X, tile.Y)))
                {
                    for (int k = 0; k < count; k++)
                    {
                        if (!_blockTiles.Contains(new Vector2I(joined[k].X, joined[k].Y))
                            && Lesser(grade, TrailGradeAt(joined[k])) == pass)
                        {
                            _trailBuilder.Band(here, MidwayInTiles(tile, joined[k]), TrailHalfWidth, colour);
                        }
                    }

                    continue;
                }

                if (grade == pass)
                {
                    _trailBuilder.Disc(here, TrailHalfWidth, colour);
                }

                if (count == 1)
                {
                    if (Lesser(grade, TrailGradeAt(joined[0])) == pass)
                    {
                        _trailBuilder.Band(MidwayInTiles(tile, joined[0]), here, TrailHalfWidth, colour);
                    }

                    continue;
                }

                for (int p = 0; p < count; p++)
                {
                    for (int q = p + 1; q < count; q++)
                    {
                        byte least = Lesser(grade, Lesser(TrailGradeAt(joined[p]), TrailGradeAt(joined[q])));
                        if (least != pass)
                        {
                            continue;
                        }

                        Bend(MidwayInTiles(tile, joined[p]), here, MidwayInTiles(tile, joined[q]), bend);
                        _trailBuilder.Strip(bend, TrailHalfWidth, colour);
                    }
                }
            }

            if (pass == 1)
            {
                TrailVerticesWorn = _trailBuilder.VertexCount;
            }
            else
            {
                TrailVerticesPacked = _trailBuilder.VertexCount;
            }

            _trailBuilder.AddSurfaceTo(_trailMesh);
        }

        LastTrailBuildMs = Since(started);
    }

    /// <summary>Segments a bend is sampled at. Eight: a quarter turn in twelve-degree steps.</summary>
    private const int BendSegments = 8;

    /// <summary>The tile point halfway between two tiles' trail points — where one tile's curve hands over to the next.</summary>
    private Vector2 MidwayInTiles(GridPos a, GridPos b) => (TrailPointOf(a) + TrailPointOf(b)) / 2f;

    /// <summary>A quadratic Bézier from <paramref name="from"/> to <paramref name="to"/> bent round <paramref name="control"/>, sampled into <paramref name="into"/>.</summary>
    private static void Bend(Vector2 from, Vector2 control, Vector2 to, System.Span<Vector2> into)
    {
        for (int i = 0; i <= BendSegments; i++)
        {
            float t = i / (float)BendSegments;
            float u = 1f - t;
            into[i] = (u * u * from) + (2f * u * t * control) + (t * t * to);
        }
    }
}
