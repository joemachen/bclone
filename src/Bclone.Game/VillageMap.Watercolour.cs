using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐⭐ The valley in watercolour — the terrain shader drawn on the real ground (<c>specs/terrain-shader.md §5</c>, A22–A25),
/// behind Joe's tick <i>paint the valley in watercolour</i>, with a before/after line beside it (A23).
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>A material shades every draw of its canvas item</b> (HANDOFF art trap 19), and this map draws the whole village in
/// one <c>_Draw</c>. So the shaded valley is a <b>layer of its own, behind the map</b>: one rect over the map's whole size,
/// whose shader paints the Beyond outside the valley, today's bake left of the line, and the new look right of it. With
/// the tick on, <c>_Draw</c> leaves out the three things the layer now paints (the Beyond, the parchment, the bake) and
/// draws everything else exactly as before — so the paths, fields, washes and everything standing lie over the new ground
/// as they lay over the old, and the grain is on the ground only (A23, call 3).
/// </para>
/// <para>
/// ⭐ <b>With the tick off nothing here runs</b>: the layer does not exist until the tick first goes on, and <c>_Draw</c>
/// takes its old path to the letter. ⭐ <b>Lean (A23):</b> the shader, the grass masks, the four painted tiles and the
/// ground's mix are loaded and baked on that first switching on, never before; per frame the layer is a handful of
/// uniforms (D338: a uniform, never a re-bake).
/// </para>
/// </remarks>
public partial class VillageMap
{
    /// <summary>The valley's shader (A25).</summary>
    internal const string WatercolourShaderPath = "res://shaders/valley_terrain.gdshader";

    /// <summary>How near the before/after line a press must land to take hold of it, in screen pixels.</summary>
    private const float SplitGrabPixels = 10f;

    /// <summary>
    /// The share of a season that holds its own palette before it turns into the next (<c>seasonal-grass.md §6.4</c>, D628:
    /// 20 days of 30, then a 10-day turn) — and so when the snow comes and goes (A23).
    /// </summary>
    private const double SeasonHolds = 2.0 / 3.0;

    private bool _watercolour;
    private bool _beforeAfter;
    private bool _draggingTheSplit;

    /// <summary>Where the before/after line stands, as a share of the map's width. It opens in the middle.</summary>
    private float _splitShare = 0.5f;

    private Control? _watercolourLayer;
    private ShaderMaterial? _watercolourMaterial;

    /// <summary>
    /// The ink under each tree's crown, in tiles past its edge, and its colour (A28) — 0 with the tick off, so the canopies
    /// mesh exactly as they did. Read by <c>Canopies</c> when a chunk is meshed.
    /// </summary>
    private float _canopyInkTiles;
    private Color _canopyInk;

    /// <summary>
    /// ⭐ Joe's painted trees (<c>specs/tree-sprites.md §5</c>, piece 1, A35): with the tick on, each tree is a cell of
    /// <see cref="TreeSpritesPath"/> laid as one textured square, in place of the inked crowns — read by
    /// <c>BuildChunk</c> when a chunk is meshed. False with the tick off, so not one vertex of the canopies changes.
    /// </summary>
    private bool _paintedTrees;

    /// <summary>
    /// The Settings tick <i>draw the trees</i> (A36, Joe: *"give settings options to disable trees (so user can see land
    /// underneath)"*). On by default, so the map opens as it was; off, no tree is meshed — crowns or painted — and the
    /// ground under the woods shows. Boulders and berries stay.
    /// </summary>
    private bool _treesShown = true;

    /// <summary>
    /// ⭐ The river's clock (A45, <c>river-drawn.md §4.4</c>): real seconds times the game's speed (A46,
    /// <see cref="_flowPace"/>), summed while the village runs and the <i>moving water</i> tick is on, wrapped at
    /// <see cref="_flowPeriod"/>. A view clock, never the sim's; 0 at founding,
    /// so a render paused at tick 0 is the same picture in any build.
    /// </summary>
    private double _flowClock;

    /// <summary>The clock's period, in seconds: the ripples' grid drifts a whole <see cref="RippleRepeatCells"/> in it.</summary>
    private double _flowPeriod = 1.0;

    /// <summary>Whether the water moves — the Settings tick <i>moving water</i> (A45 call 4), on by default.</summary>
    private bool _waterMoves = true;

    /// <summary>The river's own coordinates (A45, §3), and the world they were built for: once a world.</summary>
    private RiverTable? _riverTable;
    private SimWorld? _riverOf;

    /// <summary>
    /// How many cells down the river the ripples' pattern runs before it repeats (A45, §4.4) — longer than any river
    /// (256 × 1.6 tiles), and what lets the clock wrap without a jump.
    /// </summary>
    internal const int RippleRepeatCells = 256;

    /// <summary>Whether the river's ripples move — the Settings tick's state.</summary>
    public bool MovingWaterShown => _waterMoves;

    /// <summary>The Settings tick <i>moving water</i> (A45 call 4): off, the ripples are drawn but hold still.</summary>
    public void ShowMovingWater(bool on) => _waterMoves = on;

    /// <summary>
    /// ⭐ The river keeps the game's speed (A46, Joe: <i>"Keep the game's speed"</i>, reversing A45's call 1 after he saw
    /// both): how many seconds of flow a real second gives — the game's speed multiplier, set by <c>Main</c> on every
    /// refresh. 1 at 1×, 20 at 20×; a pause stops the clock by <see cref="Paused"/> whatever this says.
    /// </summary>
    private double _flowPace = 1.0;

    /// <summary>How fast the river runs against real time: the game's speed (A46).</summary>
    internal double FlowPace => _flowPace;

    /// <summary>Sets the river's pace to the game's speed (A46) — <c>Main</c>'s refresh, every frame.</summary>
    public void SetFlowPace(double pace) => _flowPace = pace;

    /// <summary>The river clock's frame step: real seconds times the game's speed (A46) while the village runs and the water moves, nothing otherwise.</summary>
    private void TickTheFlowClock(double delta)
    {
        // Not before the layer is first built: until then there is nothing to move, and the clock starts at 0 with it.
        if (Paused || !_waterMoves || _watercolourMaterial is null)
        {
            return;
        }

        _flowClock = (_flowClock + (delta * _flowPace)) % _flowPeriod;
        if (_watercolour)
        {
            _watercolourMaterial?.SetShaderParameter("flow_seconds", (float)_flowClock);
        }
    }

    /// <summary>
    /// The raw water weight a straight bank has <paramref name="tilesIn"/> tiles inside it (A45, §4.1) — what the bake's
    /// kernel (<see cref="ValleyTexture"/>: a smooth falloff on the squared distance, <see cref="ValleyTexture.KernelTiles"/>
    /// across) gives there, averaged along the bank. The echo lines are that weight's contours. ⚠️ A copy of the kernel's
    /// shape, not a call into it: the probe names the spec's measured values so a change to either shows.
    /// </summary>
    internal static float BankWeightAt(float tilesIn)
    {
        const int along = 16;
        float reach = ValleyTexture.KernelTiles;
        double sum = 0;
        for (int s = 0; s < along; s++)
        {
            float cx = 0.5f + tilesIn;
            float cy = s / (float)along;
            double water = 0, all = 0;
            for (int gy = -3; gy <= 3; gy++)
            {
                for (int gx = -3; gx <= 4; gx++)
                {
                    float away = ((gx - cx) * (gx - cx)) + ((gy - cy) * (gy - cy));
                    if (away >= reach * reach)
                    {
                        continue;
                    }

                    float weight = 1f - Mathf.SmoothStep(0f, reach * reach, away);
                    all += weight;
                    if (gx >= 1)
                    {
                        water += weight;
                    }
                }
            }

            sum += water / all;
        }

        return (float)(sum / along);
    }

    /// <summary>Whether the trees are drawn — the Settings tick's state.</summary>
    public bool TreesShown => _treesShown;

    /// <summary>The <i>draw the trees</i> tick: every chunk re-meshed once, on the next refresh.</summary>
    public void ShowTrees(bool on)
    {
        if (on == _treesShown)
        {
            return;
        }

        _treesShown = on;
        _sceneryOf = null;
        QueueRedraw();
    }

    /// <summary>The atlas, wrapped so it is drawn mipmapped (the map's own filter is plain linear) — null until the tick first goes on.</summary>
    private CanvasTexture? _treeAtlas;

    /// <summary>The tree atlas (<c>tree-sprites.md §3</c>): 4 × 2 cells of 256 px, 128 px of painting a tile (A34).</summary>
    internal const string TreeSpritesPath = "res://textures/trees/tree_sprites.png";

    /// <summary>The autumn trees (A41, <c>tree-sprites.md §9</c>): the same cells, russet, orange and gold.</summary>
    internal const string TreeSpritesAutumnPath = "res://textures/trees/tree_sprites_autumn.png";

    /// <summary>The winter trees (A41): leafy, birch and willow bare with a little snow; the conifers snow-laden.</summary>
    internal const string TreeSpritesWinterPath = "res://textures/trees/tree_sprites_winter.png";

    /// <summary>Which set of the painted trees a season draws (A41): spring and summer share the summer set.</summary>
    internal enum TreeSet
    {
        Summer,
        Autumn,
        Winter,
    }

    /// <summary>The autumn and winter atlases, each loaded the first time its season comes (A23's leanness).</summary>
    private CanvasTexture? _treeAtlasAutumn;
    private CanvasTexture? _treeAtlasWinter;

    /// <summary>The painted deposits (A60, <c>painted-deposits.md</c>): the summer atlas, loaded on the tick's first switching on.</summary>
    internal const string NodeSpritesPath = "res://textures/nodes/node_sprites.png";

    /// <summary>The deposits in winter, cell for cell, snow on each rock's upper-left (A60, Joe's call 4).</summary>
    internal const string NodeSpritesWinterPath = "res://textures/nodes/node_sprites_winter.png";

    private CanvasTexture? _nodeAtlas;
    private CanvasTexture? _nodeAtlasWinter;

    /// <summary>Whether the deposits are painted: the tick on, the atlas loaded. Read by <c>BuildChunk</c>.</summary>
    private bool _paintedNodes;

    /// <summary>Each painted kind's band, from <see cref="NodeSpriteBands"/>; and the atlas's rows of 128 px.</summary>
    private readonly System.Collections.Generic.Dictionary<Terrain, int> _nodeBands = new();
    private int _nodeRows = 1;

    /// <summary>
    /// ⭐ What the trees draw at a point in the year (A41, Joe: *"Slow crossfade"*): the season's set, and while a season
    /// turns — <c>TheYearAt</c>'s last third, the grass's and the snow's clock — the next set over it at the turn's share.
    /// </summary>
    /// <remarks>
    /// Into winter the old crowns fade out as the bare branches come in (<c>FromAlpha</c> falls with the turn), so at its
    /// end no leaf is left over the branches; out of winter the leaves come in over the branches, which stay until covered.
    /// A <c>static</c> of the season: no tick, no clock of its own (D337).
    /// </remarks>
    internal static (TreeSet From, float FromAlpha, TreeSet To, float ToAlpha) TreeSeasonAt(float season)
    {
        double whole = Math.Floor(season);
        int now = (int)(((whole % 4) + 4) % 4);
        float turn = (float)(season - whole);
        TreeSet from = SetOf(now), to = SetOf((now + 1) % 4);
        if (from == to || turn <= 0f)
        {
            return (from, 1f, from, 0f);
        }

        return (from, to == TreeSet.Winter ? 1f - turn : 1f, to, turn);

        static TreeSet SetOf(int quarter) => quarter switch
        {
            2 => TreeSet.Autumn,
            3 => TreeSet.Winter,
            _ => TreeSet.Summer,
        };
    }

    /// <summary>A season's atlas, loaded on first need; a missing one is said loudly and the summer set stands in.</summary>
    private CanvasTexture? TheTreeAtlasFor(TreeSet set)
    {
        if (set == TreeSet.Summer)
        {
            return TheTreeAtlas();
        }

        ref CanvasTexture? held = ref set == TreeSet.Autumn ? ref _treeAtlasAutumn : ref _treeAtlasWinter;
        if (held is null)
        {
            string path = set == TreeSet.Autumn ? TreeSpritesAutumnPath : TreeSpritesWinterPath;
            Texture2D? atlas = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            if (atlas is null)
            {
                GD.PushError($"[watercolour] the {set} trees {path} did not load — the summer trees stand in");
                return TheTreeAtlas();
            }

            held = new CanvasTexture { DiffuseTexture = atlas, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        }

        return held;
    }

    /// <summary>
    /// The deposit atlas and its bands, loaded on the first switching on and kept (A60). ⛔ A missing atlas or band file is
    /// said with <c>GD.PushError</c>, and the discs stand in; a band file naming no terrain the sim knows is said too.
    /// </summary>
    private CanvasTexture? TheNodeAtlas()
    {
        if (_nodeAtlas is not null)
        {
            return _nodeAtlas;
        }

        Texture2D? atlas = ResourceLoader.Exists(NodeSpritesPath) ? GD.Load<Texture2D>(NodeSpritesPath) : null;
        NodeSpriteBands? bands = NodeSpriteBands.Current;
        if (atlas is null || bands is null)
        {
            GD.PushError($"[watercolour] the deposits' atlas {NodeSpritesPath} or bands {NodeSpriteBands.Path} did not load — the discs stand in");
            return null;
        }

        _nodeBands.Clear();
        foreach (Variant key in bands.Bands.Keys)
        {
            if (System.Enum.TryParse(key.AsString(), out Terrain terrain))
            {
                _nodeBands[terrain] = bands.Bands[key].AsInt32();
            }
            else
            {
                GD.PushError($"[watercolour] {NodeSpriteBands.Path} names a deposit \"{key.AsString()}\" the sim has no terrain for");
            }
        }

        _nodeRows = System.Math.Max(1, atlas.GetHeight() / 128);
        _nodeAtlas = new CanvasTexture { DiffuseTexture = atlas, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        return _nodeAtlas;
    }

    /// <summary>The deposits' atlas for a season: winter's from its own file (loaded on first need), the summer one otherwise.</summary>
    private CanvasTexture? TheNodeAtlasFor(TreeSet set)
    {
        if (set != TreeSet.Winter)
        {
            return TheNodeAtlas();
        }

        if (_nodeAtlasWinter is null)
        {
            Texture2D? atlas = ResourceLoader.Exists(NodeSpritesWinterPath) ? GD.Load<Texture2D>(NodeSpritesWinterPath) : null;
            if (atlas is null)
            {
                GD.PushError($"[watercolour] the winter deposits {NodeSpritesWinterPath} did not load — the summer ones stand in");
                return TheNodeAtlas();
            }

            _nodeAtlasWinter = new CanvasTexture { DiffuseTexture = atlas, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        }

        return _nodeAtlasWinter;
    }

    /// <summary>
    /// The chunks on screen's deposit meshes, textured (A60): the season's atlas, then, while a season turns into or out
    /// of winter, the other over it at the turn's share — the rocks keep their shapes and the snow comes and goes on them.
    /// </summary>
    private void DrawThePaintedNodes(int minX, int maxX, int minY, int maxY)
    {
        (float season, float _) = TheYearAt(_world!.Tick + _alpha, _world.Config.TicksPerSeason);
        (TreeSet from, float _, TreeSet to, float toAlpha) = TreeSeasonAt(season);
        CanvasTexture? under = TheNodeAtlasFor(from), over = TheNodeAtlasFor(to);
        if (under is null)
        {
            return;
        }

        DrawTheNodeChunks(minX, maxX, minY, maxY, under, 1f);
        if (toAlpha > 0f && over is not null && over != under)
        {
            DrawTheNodeChunks(minX, maxX, minY, maxY, over, toAlpha);
        }
    }

    private void DrawTheNodeChunks(int minX, int maxX, int minY, int maxY, CanvasTexture atlasOf, float alpha)
    {
        GeneratedMap map = _sceneryOf!;
        Transform2D toScreen = TileToScreen();
        Rid canvas = GetCanvasItem();
        Rid atlas = atlasOf.GetRid();
        Color modulate = Colors.White with { A = alpha };
        const int Reach = 1;

        int firstCx = Mathf.Clamp((minX - Reach - map.MinX) / ChunkTiles, 0, _chunksAcross - 1);
        int lastCx = Mathf.Clamp((maxX + Reach - map.MinX) / ChunkTiles, 0, _chunksAcross - 1);
        int firstCy = Mathf.Clamp((minY - Reach - map.MinY) / ChunkTiles, 0, _chunksDown - 1);
        int lastCy = Mathf.Clamp((maxY + Reach - map.MinY) / ChunkTiles, 0, _chunksDown - 1);

        for (int cy = firstCy; cy <= lastCy; cy++)
        {
            for (int cx = firstCx; cx <= lastCx; cx++)
            {
                ArrayMesh mesh = _scenery[(cy * _chunksAcross) + cx].Nodes;
                if (mesh.GetSurfaceCount() > 0)
                {
                    RenderingServer.CanvasItemAddMesh(canvas, mesh.GetRid(), toScreen, modulate, atlas);
                }
            }
        }
    }

    /// <summary>
    /// ⭐ The painted crops (A61, <c>specs/painted-crops.md</c>): on with the tick, if the shared ground paintings load. Read
    /// by <c>DrawWorkedGround</c>; with it false the fields are the flat fills, furrows, shoots and stalks, byte for byte.
    /// </summary>
    private bool _paintedCrops;

    /// <summary>Every crop painting asked for, by path, loaded once — a path with no file kept as null, so it is asked once.</summary>
    private readonly System.Collections.Generic.Dictionary<string, CanvasTexture?> _cropPaintings = new();

    /// <summary>How many tiles one painting covers before it repeats: 1024 px at 128 px a tile (§4).</summary>
    internal const float CropTilesPerRepeat = 8f;

    /// <summary>A point in tile space to its place in a crop painting — world-anchored, so every field's rows line up (call 1).</summary>
    internal static Vector2 CropUvOf(Vector2 tile) => tile / CropTilesPerRepeat;

    /// <summary>
    /// Which painting a field stage shows at <paramref name="season"/> (a season and how far it has turned, <c>TheYearAt</c>'s
    /// clock), and the next one over it at the turn's share (§3): bare ground ploughed in spring and summer and stubble from
    /// the autumn, crossfading as the season turns; sown ground young in spring and growing after; ripe ground ripe.
    /// </summary>
    internal static (string From, string To, float Share) CropStageAt(Terrain stage, float season)
    {
        double whole = Math.Floor(season);
        int now = (int)(((whole % 4) + 4) % 4);
        float turn = (float)(season - whole);
        string from = Name(now), to = Name((now + 1) % 4);
        return from == to || turn <= 0f ? (from, from, 0f) : (from, to, turn);

        string Name(int quarter) => stage switch
        {
            Terrain.Field => quarter >= 2 ? "stubble" : "ploughed",
            Terrain.Sown => quarter == 0 ? "young" : "growing",
            _ => "ripe",
        };
    }

    /// <summary>
    /// A crop's painting of a stage: <c>textures/crops/&lt;the crop row's name&gt;/&lt;stage&gt;.png</c>, else the shared
    /// <c>textures/crops/_ground/&lt;stage&gt;.png</c> (turned earth is turned earth); null where neither exists, and the flat
    /// fill stands in. Repeating, mipmapped.
    /// </summary>
    internal CanvasTexture? TheCropPainting(byte crop, string stage)
    {
        string? name = null;
        if (crop != 0 && _world is SimWorld world)
        {
            foreach (CropRow row in world.Config.Crops)
            {
                name = row.Id == crop ? row.Name : name;
            }
        }

        return (name is null ? null : Loaded($"res://textures/crops/{name}/{stage}.png")) ?? Loaded($"res://textures/crops/_ground/{stage}.png");

        CanvasTexture? Loaded(string path)
        {
            if (!_cropPaintings.TryGetValue(path, out CanvasTexture? held))
            {
                Texture2D? painting = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
                held = painting is null ? null : new CanvasTexture
                {
                    DiffuseTexture = painting,
                    TextureFilter = TextureFilterEnum.LinearWithMipmaps,
                    TextureRepeat = TextureRepeatEnum.Enabled,
                };
                _cropPaintings[path] = held;
            }

            return held;
        }
    }

    /// <summary>One field fill as its crop's painting, the next stage over it while the season turns. False: no painting.</summary>
    private bool LayThePaintedCrop(Terrain stage, byte crop, Vector2[] onScreen, Vector2[] uvs, int[] indices)
    {
        if (stage is not (Terrain.Field or Terrain.Sown or Terrain.Ripe))
        {
            return false;
        }

        (float season, float _) = TheYearAt(_world!.Tick + _alpha, _world.Config.TicksPerSeason);
        (string from, string to, float share) = CropStageAt(stage, season);
        CanvasTexture? under = TheCropPainting(crop, from);
        if (under is null)
        {
            return false;
        }

        LayTheCrop(onScreen, uvs, indices, under, 1f);
        if (share > 0f && to != from && TheCropPainting(crop, to) is CanvasTexture over)
        {
            LayTheCrop(onScreen, uvs, indices, over, share);
        }

        return true;
    }

    private void LayTheCrop(Vector2[] onScreen, Vector2[] uvs, int[] indices, CanvasTexture painting, float alpha) =>
        RenderingServer.CanvasItemAddTriangleArray(
            GetCanvasItem(), indices, onScreen, new[] { Colors.White with { A = alpha } }, uvs, texture: painting.GetRid());

    /// <summary>
    /// Over the painted fields (A61): a soft darker wash along each field's edge — pigment pooling, no ink line (Joe's
    /// call 4, after A28's <i>"the ground isn't outlined"</i>) — and, in winter, the fields' snow at the valley's own share
    /// (call 3), its painting's patchiness letting stubble show where it is thin.
    /// </summary>
    private void DrawTheFieldsEdgesAndSnow()
    {
        foreach (Vector2[] edge in _fieldEdges)
        {
            var onScreen = new Vector2[edge.Length + 1];
            for (int p = 0; p < edge.Length; p++)
            {
                onScreen[p] = ToScreen(edge[p]);
            }

            onScreen[edge.Length] = onScreen[0];
            DrawPolyline(onScreen, FieldPooling with { A = 0.10f }, Mathf.Max(2f, _pixelsPerTile * 0.16f), antialiased: true);
            DrawPolyline(onScreen, FieldPooling with { A = 0.14f }, Mathf.Max(1f, _pixelsPerTile * 0.07f), antialiased: true);
        }

        (float _, float snow) = TheYearAt(_world!.Tick + _alpha, _world.Config.TicksPerSeason);
        if (snow <= 0f || TheCropPainting(0, "snow") is not CanvasTexture white)
        {
            return;
        }

        foreach ((Terrain stage, byte _, Vector2[] triangles, Vector2[] uvs, int[] indices) in _fieldFills)
        {
            if (stage is Terrain.Field or Terrain.Sown or Terrain.Ripe)
            {
                var onScreen = new Vector2[triangles.Length];
                for (int p = 0; p < triangles.Length; p++)
                {
                    onScreen[p] = ToScreen(triangles[p]);
                }

                LayTheCrop(onScreen, uvs, indices, white, snow);
            }
        }
    }

    /// <summary>The fields' edge wash: a dark earth, pooled pigment rather than a pen (A61).</summary>
    private static readonly Color FieldPooling = new("#5b4a33");

    /// <summary>Whether the valley is drawn in watercolour — the Settings tick's state.</summary>
    public bool WatercolourShown => _watercolour;

    /// <summary>Whether the before/after line is shown — its tick's state. It shows only while the valley is in watercolour.</summary>
    public bool BeforeAfterShown => _beforeAfter;

    /// <summary>The layer's material, for the probe to ask (null until the tick first goes on).</summary>
    internal ShaderMaterial? WatercolourMaterial => _watercolourMaterial;

    /// <summary>The before/after line's place, as a share of the map's width — for the probe.</summary>
    internal float SplitShare => _splitShare;

    /// <summary>The Settings tick <i>paint the valley in watercolour</i> (A23, call 5), off until Joe approves the look.</summary>
    public void ShowWatercolour(bool on)
    {
        _watercolour = on;

        if (on)
        {
            BuildTheWatercolourLayer();
        }

        if (_watercolourLayer is not null)
        {
            _watercolourLayer.Visible = on;
        }

        // The trees' ink (A28): the canopies are meshed once and kept, so a change of ink re-meshes every chunk — once, on
        // the next refresh, by forgetting which map was meshed.
        ValleyWatercolourLook? look = on ? ValleyWatercolourLook.Current : null;
        _treeTint = look is { GreenOn: true } ? look.TreeTint : Colors.White;
        float inkTiles = look?.TreeInkTiles ?? 0f;
        Color ink = look?.InkColour ?? default;
        if (inkTiles != _canopyInkTiles || ink != _canopyInk)
        {
            _canopyInkTiles = inkTiles;
            _canopyInk = ink;
            _sceneryOf = null;
        }

        // The painted trees (A35): on with the tick, if the atlas loads; a change re-meshes every chunk, once, the same way.
        bool painted = on && TheTreeAtlas() is not null;
        if (painted != _paintedTrees)
        {
            _paintedTrees = painted;
            _sceneryOf = null;
        }

        // The painted deposits (A60): the same, with their own atlas and bands.
        bool nodes = on && TheNodeAtlas() is not null;
        if (nodes != _paintedNodes)
        {
            _paintedNodes = nodes;
            _sceneryOf = null;
        }

        // The painted crops (A61): on with the tick, if the shared ground paints. Fields are drawn each frame; nothing to re-mesh.
        _paintedCrops = on && TheCropPainting(0, "ploughed") is not null;

        QueueRedraw();
    }

    /// <summary>
    /// The atlas, loaded on the first switching on and kept (A23's leanness), wrapped in a <see cref="CanvasTexture"/> that
    /// samples its mipmaps: the trees are drawn at 0.38× of their painting at the closest zoom and 0.19× at the widest.
    /// ⛔ A missing atlas is said loudly and the inked crowns stand in, so the valley never draws without trees.
    /// </summary>
    private CanvasTexture? TheTreeAtlas()
    {
        if (_treeAtlas is not null)
        {
            return _treeAtlas;
        }

        Texture2D? atlas = ResourceLoader.Exists(TreeSpritesPath) ? GD.Load<Texture2D>(TreeSpritesPath) : null;
        if (atlas is null)
        {
            GD.PushError($"[watercolour] the tree atlas {TreeSpritesPath} did not load — the inked crowns (A28) stand in");
            return null;
        }

        _treeAtlas = new CanvasTexture { DiffuseTexture = atlas, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        return _treeAtlas;
    }

    /// <summary>
    /// ⭐ The probe's <c>painted trees:</c> line (A35, <c>tree-sprites.md §7</c>) — everything about the trees that can be
    /// checked headless: the atlas as imported, the tick reaching the meshes, every tree a whole cell of the right size,
    /// and off, the canopies to the vertex.
    /// </summary>
    /// <remarks>
    /// ⚠️ The draw itself (<c>DrawThePaintedTrees</c>) runs only inside <c>_Draw</c>, which never runs headless (D340); what
    /// it draws is these meshes, and a windowed render is its check.
    /// </remarks>
    internal string ThePaintedTreesHold()
    {
        if (_world is null)
        {
            return "[widths] painted trees: ⛔ no world to draw";
        }

        SimWorld world = _world;
        GeneratedMap map = world.Map;
        var faults = new System.Collections.Generic.List<string>();
        bool wasOn = _watercolour;

        // ⚠️ The probe's valley has no young wood, so every sixth wooded tile is made young for this line and put back after
        // it (the felling check's way, `TheSceneryIsMeshed`) — or the young trees would go unchecked. Every sixth, not one
        // (A51): the young trees have sixteen drawings and four kinds by place, and one tile would reach three of them.
        var planted = new System.Collections.Generic.List<GridPos>();
        int wooded = 0;
        for (int i = 0; i < map.Tiles.Count; i++)
        {
            if (map.Tiles[i] == Terrain.Forest && wooded++ % 6 == 0)
            {
                planted.Add(new GridPos(map.MinX + (i % map.Width), map.MinY + (i / map.Width)));
            }
        }

        foreach (GridPos young in planted)
        {
            map.SetTerrain(young, Terrain.Sapling);
        }

        _sceneryOf = null;

        // What the valley should hold: its crowns' discs with the tick off (36 vertices a disc), its painted trees' squares on.
        int discs = 0, squares = 0, woodTiles = 0, grownTiles = 0, youngTiles = 0, seamDiscs = 0;
        for (int i = 0; i < map.Tiles.Count; i++)
        {
            if (map.Tiles[i] is Terrain.Forest or Terrain.Sapling)
            {
                var tile = new GridPos(map.MinX + (i % map.Width), map.MinY + (i / map.Width));
                bool grown = map.Tiles[i] == Terrain.Forest;
                discs += TreesOn(tile, grown) * 36;
                squares += PaintedTreesOn(tile, grown) * 6;
                woodTiles++;
                grownTiles += grown ? 1 : 0;
                youngTiles += grown ? 0 : 1;
            }
            else if (map.Tiles[i] is Terrain.Rock or Terrain.IronDeposit && _nodeBands.ContainsKey(map.Tiles[i]))
            {
                // A seam's discs leave the standing mesh too while the deposits are painted (A60): one fewer, cut, on a face.
                var tile = new GridPos(map.MinX + (i % map.Width), map.MinY + (i / map.Width));
                bool face = _faceShadow.Length > i && _faceShadow[i];
                seamDiscs += (LumpsOn(tile, stone: map.Tiles[i] == Terrain.Rock) - (face ? 1 : 0)) * 36;
            }
        }

        // The atlas as imported (HANDOFF art trap 2): its size, lossless (RGBA8 — a VRAM import would be another format), mipmapped.
        Texture2D? atlas = ResourceLoader.Exists(TreeSpritesPath) ? GD.Load<Texture2D>(TreeSpritesPath) : null;
        using Image? image = atlas?.GetImage();
        // ⛔ Named from the spec, not the code (trap 17): 256 px cells, 4 across, 5 down — two rows leafy, then a row each of
        // conifer, birch and willow (A36) — and a sixth row of sixteen young trees in 128 px cells (A51, §10): 1024 × 1536.
        if (atlas is null || image is null || atlas.GetWidth() != 1024 || atlas.GetHeight() != 1536
            || image.GetFormat() != Image.Format.Rgba8 || !image.HasMipmaps())
        {
            faults.Add($"{TreeSpritesPath} is {atlas?.GetWidth()} × {atlas?.GetHeight()}, {image?.GetFormat()}, mipmaps {image?.HasMipmaps()} (want 1024 × 1536, Rgba8, mipmapped)");
        }

        // Off: no trees mesh, the crowns as ever.
        ShowWatercolour(false);
        RefreshTheScenery(world);
        (int offStanding, int offTrees) = Totals();
        if (offTrees != 0 || _paintedTrees)
        {
            faults.Add($"off, {offTrees} painted-tree vertices are meshed (want none)");
        }

        // On: every tree a square, and the crowns gone from the standing mesh.
        ShowWatercolour(true);
        RefreshTheScenery(world);
        (int onStanding, int onTrees) = Totals();
        if (!_paintedTrees || _treeAtlas?.TextureFilter != TextureFilterEnum.LinearWithMipmaps || _treeAtlas.DiffuseTexture?.ResourcePath != TreeSpritesPath)
        {
            faults.Add($"on, the trees are painted {_paintedTrees}, the atlas {_treeAtlas?.DiffuseTexture?.ResourcePath} drawn {_treeAtlas?.TextureFilter} (want true, {TreeSpritesPath}, LinearWithMipmaps)");
        }

        if (onTrees != squares || offStanding - onStanding != discs + seamDiscs)
        {
            faults.Add($"on, {onTrees} painted-tree vertices (want {squares}) and the standing mesh {offStanding - onStanding} lighter (want the crowns' {discs} and the painted seams' {seamDiscs})");
        }

        // ⛔ Named here, not read off the code's own count (HANDOFF art trap 17): 1–2 trees a grown tile (A30, call 4), both;
        // three a young tile (A51, call 1).
        int youngTrees = 3 * youngTiles;
        int grownTrees = (onTrees / 6) - youngTrees;
        if (grownTrees <= grownTiles || grownTrees >= 2 * grownTiles)
        {
            faults.Add($"{grownTrees} painted trees on {grownTiles} grown tiles (want 1–2 a tile, both: strictly between {grownTiles} and {2 * grownTiles})");
        }

        // Every tree a whole cell of the atlas, of a painted tree's size; and the valley uses every drawing, both faces.
        var cells = new bool[8 + 4 + 4 + 4 + 16]; // 8 leafy, 4 each conifer, birch, willow (A36); 16 young, 4 a kind (A51)
        int conifers = 0, birches = 0, willows = 0, misplaced = 0, youngCelled = 0, youngInTheirPlace = 0;
        string? firstMisplaced = null;
        int mirroredTrees = 0, trees = 0, wrong = 0, grownSized = 0, youngSized = 0;

        // ⛔ The sizes named here from the spec, not read off the code's constants (trap 17; the first build read
        // PaintedTreeHalf and a mutant making every tree 30 % too big scored zero): a grown cell is 256 px of painting at
        // 128 px a tile, so 2 tiles across, × 0.85–1.10; a young cell 128 px, so 1 tile, × 0.85–1.10 (`tree-sprites.md
        // §3`, §10).
        const float CellTiles = 256f / 128f, YoungCellTiles = 128f / 128f;
        float grownLow = CellTiles * 0.85f, grownHigh = CellTiles * 1.10f;
        float youngLow = YoungCellTiles * 0.85f, youngHigh = YoungCellTiles * 1.10f;
        string? firstWrong = null;
        // The grid, named: grown cells 256 px, 4 across, rows 0–4 of a 1536-px atlas; young cells 128 px, 8 across, from 1280 px down.
        const float YoungFrom = 1280f / 1536f;
        foreach (SceneryChunk chunk in _scenery)
        {
            if (chunk.Trees.GetSurfaceCount() == 0)
            {
                continue;
            }

            Godot.Collections.Array arrays = chunk.Trees.SurfaceGetArrays(0);
            Vector2[] at = arrays[(int)Mesh.ArrayType.Vertex].AsVector2Array();
            Vector2[] uv = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            for (int v = 0; v + 5 < at.Length; v += 6)
            {
                trees++;
                bool youngCell = uv[v].Y >= YoungFrom - 1e-4f;
                float du = youngCell ? 128f / 1024f : 256f / 1024f, dv = youngCell ? 128f / 1536f : 256f / 1536f;
                int across = youngCell ? 8 : 4;
                float top = youngCell ? YoungFrom : 0f;
                int k = (int)((uv[v].X / du) + 1e-3f) + ((int)(((uv[v].Y - top) / dv) + 1e-3f) * across);
                int cell = Mathf.Clamp((youngCell ? 20 : 0) + k, 0, cells.Length - 1);
                Vector2 corner = new(k % across * du, top + (k / across * dv));
                bool mirrored = uv[v + 1].IsEqualApprox(corner + new Vector2(0f, dv));
                bool plain = uv[v + 1].IsEqualApprox(corner + new Vector2(du, 0f));
                float side = at[v + 1].X - at[v].X;
                bool whole = uv[v].IsEqualApprox(corner) && uv[v + 2].IsEqualApprox(corner + new Vector2(du, dv)) && (mirrored || plain)
                    && (youngCell ? k < 16 : k < 20);
                bool grownSize = !youngCell && side >= grownLow - 1e-3f && side <= grownHigh + 1e-3f;
                bool youngSize = youngCell && side >= youngLow - 1e-3f && side <= youngHigh + 1e-3f;
                grownSized += grownSize ? 1 : 0;
                youngSized += youngSize ? 1 : 0;
                youngCelled += youngCell ? 1 : 0;
                bool sized = (grownSize || youngSize)
                    && Mathf.Abs(at[v + 2].Y - at[v].Y - side) < 1e-3f; // tile coordinates near 50 carry float noise past IsEqualApprox's 1e-6
                if (!whole || !sized)
                {
                    wrong++;
                    firstWrong ??= $"uv {uv[v]}–{uv[v + 2]}, {side:F2} tiles across";
                    continue;
                }

                cells[cell] = true;
                mirroredTrees += mirrored ? 1 : 0;

                // ⭐ Each kind where its place says (A36), read off the tree's own foot — not off KindOf, which is what is
                // being checked: a willow within reach of water, a birch within reach of open ground.
                Vector2 foot = (at[v] + at[v + 2]) / 2f;

                // ⭐ A young tree stands on a young tile (its foot within a sapling's reach of one), and a young tree is drawn
                // as the young of its place's kind (A51, call 2): the young cells go 20–23 leafy, 24–27 conifer, 28–31
                // birch, 32–35 willow, so its place is checked as the grown kind's is.
                youngInTheirPlace += youngCell && Near(foot, t => t == Terrain.Sapling) ? 1 : 0;
                int kindOf = youngCell ? (cell - 20) / 4 : cell < 8 ? 0 : (cell - 4) / 4; // 0 leafy, 1 conifer, 2 birch, 3 willow
                if (kindOf == 3)
                {
                    willows++;
                    if (!Near(foot, t => t == Terrain.Water))
                    {
                        misplaced++;
                        firstMisplaced ??= $"a willow at {foot} with no water near";
                    }
                }
                else if (kindOf == 2)
                {
                    birches++;
                    if (!Near(foot, t => t is not (Terrain.Forest or Terrain.Sapling or Terrain.Water)))
                    {
                        misplaced++;
                        firstMisplaced ??= $"a birch at {foot} inside the wood";
                    }
                }
                else if (kindOf == 1)
                {
                    conifers++;
                }
            }
        }

        if (grownSized != grownTrees || youngSized != youngTrees || youngCelled != youngTrees || youngInTheirPlace != youngTrees)
        {
            faults.Add($"{grownSized} trees are a grown cell at a grown tree's size ({grownLow:F2}–{grownHigh:F2} tiles; want {grownTrees}) and {youngSized} a young cell at a young one's ({youngLow:F2}–{youngHigh:F2}; want {youngTrees}); {youngCelled} young cells drawn, {youngInTheirPlace} on young tiles (want {youngTrees}, three on each of {youngTiles})");
        }

        // Willows and birches on the valley's water and edges, both; conifers a stand's share, not none and not most.
        float coniferShare = trees == 0 ? 0f : conifers / (float)trees;
        if (misplaced > 0 || willows == 0 || birches == 0 || coniferShare < 0.08f || coniferShare > 0.40f)
        {
            faults.Add($"{misplaced} trees out of their place (first: {firstMisplaced}); {willows} willows, {birches} birches, conifers {coniferShare:P0} of the trees (want 0, some, some, 8–40 %)");
        }

        // The trees tick (A36): off, no painted tree; back on, every one.
        bool wasTrees = _treesShown;
        ShowTrees(false);
        RefreshTheScenery(world);
        (int _, int hiddenTrees) = Totals();
        ShowTrees(true);
        RefreshTheScenery(world);
        (int _, int shownTrees) = Totals();
        if (hiddenTrees != 0 || shownTrees != onTrees)
        {
            faults.Add($"the trees tick off leaves {hiddenTrees} painted-tree vertices and on brings back {shownTrees} (want 0 and {onTrees})");
        }

        // ⭐ A fell at a chunk's edge re-meshes the chunk beside it (A36: the kinds read their neighbours). A wooded tile on
        // a chunk's right edge with a chunk beyond it is felled by hand and put back, as the scenery line's fell is.
        int edgeAt = -1;
        for (int i = 0; i < map.Tiles.Count && edgeAt < 0; i++)
        {
            int x = i % map.Width;
            edgeAt = map.Tiles[i] == Terrain.Forest && x % ChunkTiles == ChunkTiles - 1 && x + 1 < map.Width ? i : -1;
        }

        int rebuiltForAFell = 0;
        if (edgeAt >= 0)
        {
            var edge = new GridPos(map.MinX + (edgeAt % map.Width), map.MinY + (edgeAt / map.Width));
            map.SetTerrain(edge, Terrain.Grass);
            RefreshTheScenery(world, generation: _sceneryAtGeneration + 1);
            rebuiltForAFell = LastSceneryChunksBuilt;
            map.SetTerrain(edge, Terrain.Forest);
            RefreshTheScenery(world, generation: _sceneryAtGeneration + 1);
            _sceneryAtGeneration = world.TerrainGeneration;
        }

        if (rebuiltForAFell < 2)
        {
            faults.Add($"a fell on a chunk's edge re-meshed {rebuiltForAFell} chunks (want its own and the one beside it, 2 or more)");
        }

        int used = cells.Count(c => c);
        if (wrong > 0 || used != cells.Length || mirroredTrees == 0 || mirroredTrees == trees)
        {
            faults.Add($"{wrong} of {trees} trees are not a whole cell of a tree's size (first: {firstWrong}); {used} of {cells.Length} drawings used, {mirroredTrees} mirrored (want 0, all, some but not all)");
        }

        // Switched back off: the canopies to the vertex. ⚠️ Off explicitly, not "back as it was": since A58 the valley opens
        // in watercolour, and these two checks were gated on opening off, so they had quietly stopped running.
        ShowWatercolour(false);
        RefreshTheScenery(world);
        (int backStanding, int backTrees) = Totals();
        if (backTrees != 0 || backStanding != offStanding)
        {
            faults.Add($"switched back off, {backTrees} painted-tree vertices and {backStanding} standing (want 0 and {offStanding})");
        }

        // The trees tick with the watercolour off too: the crowns go, the boulders stay.
        ShowTrees(false);
        RefreshTheScenery(world);
        (int bareStanding, int _) = Totals();
        ShowTrees(true);
        RefreshTheScenery(world);
        (int againStanding, int _) = Totals();
        if (offStanding - bareStanding != discs || againStanding != offStanding)
        {
            faults.Add($"with the watercolour off, the trees tick off takes {offStanding - bareStanding} vertices (want the crowns' {discs}) and on gives back to {againStanding} (want {offStanding})");
        }

        ShowTrees(wasTrees);
        ShowWatercolour(wasOn);
        RefreshTheScenery(world);

        foreach (GridPos young in planted)
        {
            map.SetTerrain(young, Terrain.Forest);
        }

        _sceneryOf = null;
        RefreshTheScenery(world);

        if (youngTrees == 0)
        {
            faults.Add("no young tree was checked");
        }

        // ⭐ The trees through the year (A41). The two season atlases as imported — the summer one's grid, lossless,
        // mipmapped — and each drawn mipmapped from its own file.
        // ⛔ The paths named here, not read off the code's constants (trap 17).
        foreach ((string path, TreeSet set) in new[] { ("res://textures/trees/tree_sprites_autumn.png", TreeSet.Autumn), ("res://textures/trees/tree_sprites_winter.png", TreeSet.Winter) })
        {
            Texture2D? seasonal = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            using Image? pixels = seasonal?.GetImage();
            if (seasonal is null || pixels is null || seasonal.GetWidth() != 1024 || seasonal.GetHeight() != 1536
                || pixels.GetFormat() != Image.Format.Rgba8 || !pixels.HasMipmaps())
            {
                faults.Add($"{path} is {seasonal?.GetWidth()} × {seasonal?.GetHeight()}, {pixels?.GetFormat()}, mipmaps {pixels?.HasMipmaps()} (want 1024 × 1536, Rgba8, mipmapped)");
            }

            CanvasTexture? drawn = TheTreeAtlasFor(set);
            if (drawn?.DiffuseTexture?.ResourcePath != path || drawn.TextureFilter != TextureFilterEnum.LinearWithMipmaps)
            {
                faults.Add($"the {set} trees draw {drawn?.DiffuseTexture?.ResourcePath} with {drawn?.TextureFilter} (want {path}, LinearWithMipmaps)");
            }
        }

        // The year, at seven moments, named here (Joe, A41: a slow crossfade over each season's turn; into winter the
        // crowns fade out under the branches). A season value is the season plus how far it has turned.
        (float At, TreeSet From, float FromAlpha, TreeSet To, float ToAlpha, string When)[] year =
        {
            (0.0f, TreeSet.Summer, 1f, TreeSet.Summer, 0f, "spring"),
            (1.0f, TreeSet.Summer, 1f, TreeSet.Summer, 0f, "summer"),
            (1.5f, TreeSet.Summer, 1f, TreeSet.Autumn, 0.5f, "summer half turned"),
            (2.0f, TreeSet.Autumn, 1f, TreeSet.Autumn, 0f, "autumn"),
            (2.5f, TreeSet.Autumn, 0.5f, TreeSet.Winter, 0.5f, "autumn half turned to winter"),
            (3.0f, TreeSet.Winter, 1f, TreeSet.Winter, 0f, "winter"),
            (3.5f, TreeSet.Winter, 1f, TreeSet.Summer, 0.5f, "winter half turned to spring"),
        };
        foreach ((float at, TreeSet from, float fromAlpha, TreeSet to, float toAlpha, string when) in year)
        {
            (TreeSet f, float fa, TreeSet tt, float ta) = TreeSeasonAt(at);
            if (f != from || tt != to || Mathf.Abs(fa - fromAlpha) > 1e-4f || Mathf.Abs(ta - toAlpha) > 1e-4f)
            {
                faults.Add($"at {when} the trees draw {f} at {fa:F2} then {tt} at {ta:F2} (want {from} at {fromAlpha:F2} then {to} at {toAlpha:F2})");
            }
        }

        return faults.Count == 0
            ? $"[widths] painted trees: ✅ {TreeSpritesPath} 1024 × 1536, lossless, mipmapped, drawn with mipmaps; on, {trees} trees on {woodTiles} wooded tiles as {onTrees} vertices in their own mesh, the crowns' {discs} gone, every tree a whole cell of its size ({grownSized} grown; {youngSized} young, three a tile on the {youngTiles} made young for the check, each the young of its place's kind), all {cells.Length} drawings, {mirroredTrees} mirrored; {conifers} conifers in stands ({coniferShare:P0}), {willows} willows each by water, {birches} birches each at an edge; a fell on a chunk's edge re-meshes {rebuiltForAFell} chunks; the trees tick hides and brings back every tree in either look; through the year the summer, autumn and winter atlases (each 1024 × 1536, mipmapped) at seven moments as Joe chose them; off, no painted tree and the canopies to the vertex"
            : $"[widths] painted trees: ⛔ {string.Join("; ", faults)}";

        bool Near(Vector2 foot, System.Func<Terrain, bool> wanted)
        {
            // Within a tree's reach of its tile and one tile beyond: CanopySpread (0.55) plus a tile, rounded out to 2.
            int fx = Mathf.RoundToInt(foot.X), fy = Mathf.RoundToInt(foot.Y);
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    if (wanted(map.TerrainAt(new GridPos(fx + dx, fy + dy))))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        (int Standing, int Trees) Totals()
        {
            int standing = 0, painted = 0;
            foreach (SceneryChunk chunk in _scenery)
            {
                standing += chunk.StandingVertices;
                painted += chunk.TreeVertices;
            }

            return (standing, painted);
        }
    }

    /// <summary>The Settings tick <i>before/after slider</i> (A23): the line between today's valley and the watercolour.</summary>
    public void ShowBeforeAfter(bool on)
    {
        _beforeAfter = on;
        _draggingTheSplit = false;
        QueueRedraw();
    }

    /// <summary>
    /// The layer, the material and everything it reads — once, on the first switching on. ⚠️ The game's bake is swapped
    /// for one that keeps the ground's mix (A24); it bakes on the next refresh, a second or so, once.
    /// </summary>
    private void BuildTheWatercolourLayer()
    {
        if (_watercolourLayer is not null)
        {
            return;
        }

        GrassLook look = GrassLook.Approved;
        GrassLabTerrain terrain = GrassLabTerrain.StartingPoint;
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(WatercolourShaderPath) };

        // The grass, at the approved look (D632, A10).
        material.SetShaderParameter("grass_masks", GD.Load<Texture2D>($"res://textures/grass/grass_{look.Masks}_masks.png"));
        material.SetShaderParameter("grass_season_atlas", GD.Load<Texture2D>("res://textures/grass/grass_seasons.png"));
        material.SetShaderParameter("grass_jitter", look.Jitter);
        material.SetShaderParameter("grass_strength", look.Strength);
        material.SetShaderParameter("tiles_per_repeat", look.TilesPerRepeat);
        material.SetShaderParameter("macro_on", look.MacroOn);
        material.SetShaderParameter("macro_cycle_tiles", look.MacroCycleTiles);
        material.SetShaderParameter("macro_strength_amount", look.MacroStrengthAmount);
        material.SetShaderParameter("macro_season_amount", look.MacroSeasonAmount);
        material.SetShaderParameter("hex_on", look.HexOn);
        material.SetShaderParameter("hex_sharpness", look.HexSharpness);
        material.SetShaderParameter("height_blend", look.HeightBlend);
        material.SetShaderParameter("grain_amount", look.PaperGrain);

        // The terrain, at Joe's approved values (A7, A9; the coverage fade is the lab's, §5.3).
        material.SetShaderParameter("coverage_fade", terrain.CoverageFade);
        material.SetShaderParameter("splat_sharpness", terrain.SplatSharpness);
        material.SetShaderParameter("bleed_reach_tiles", terrain.BleedReachTiles);
        material.SetShaderParameter("bleed_scale_tiles", terrain.BleedScaleTiles);
        material.SetShaderParameter("grain_scale_tiles", terrain.GrainScaleTiles);
        material.SetShaderParameter("snow_srgb", Srgb(terrain.SnowColour));
        material.SetShaderParameter("snow_blend", terrain.SnowBlend);
        material.SetShaderParameter("snow_patchiness", terrain.SnowPatchiness);
        material.SetShaderParameter("snow_patch_tiles", terrain.SnowPatchTiles);
        material.SetShaderParameter("ground_tex_tiles", terrain.TextureTilesPerRepeat);
        material.SetShaderParameter("ground_tex_antitile", terrain.TextureAntiTiling);

        // The ink and the pooling (A27): the art direction's first piece, from its own look file — nothing approved yet.
        ValleyWatercolourLook watercolour = ValleyWatercolourLook.Current;
        material.SetShaderParameter("ink_srgb", Srgb(watercolour.InkColour));
        material.SetShaderParameter("ink_strength", watercolour.InkStrength);
        material.SetShaderParameter("ink_width_px", watercolour.InkWidthPx);
        material.SetShaderParameter("ink_pressure", watercolour.InkPressure);
        material.SetShaderParameter("ink_pressure_tiles", watercolour.InkPressureTiles);
        material.SetShaderParameter("ink_lifts", watercolour.InkLifts);
        material.SetShaderParameter("ink_feather_px", watercolour.InkFeatherPx);
        material.SetShaderParameter("pool_strength", watercolour.PoolStrength);
        material.SetShaderParameter("pool_width_px", watercolour.PoolWidthPx);
        material.SetShaderParameter("snow_peak", watercolour.SnowPeak);
        material.SetShaderParameter("snow_shadow_srgb", Srgb(watercolour.SnowShadowColour));
        material.SetShaderParameter("snow_shadow_strength", watercolour.SnowShadowStrength);
        material.SetShaderParameter("ice_srgb", Srgb(watercolour.IceColour));
        material.SetShaderParameter("ice_width_tiles", watercolour.IceWidthTiles);
        material.SetShaderParameter("ice_strength", watercolour.IceStrength);
        material.SetShaderParameter("river_winter_darken", watercolour.RiverWinterDarken);
        material.SetShaderParameter("channel_strength", watercolour.ChannelStrength);
        material.SetShaderParameter("echo_strength", watercolour.EchoStrength);
        material.SetShaderParameter("echo_width_share", watercolour.EchoWidthShare);
        material.SetShaderParameter("echo_tiles", new Vector2(watercolour.EchoNearTiles, watercolour.EchoFarTiles));
        material.SetShaderParameter("echo_levels", new Vector2(BankWeightAt(watercolour.EchoNearTiles), BankWeightAt(watercolour.EchoFarTiles)));
        material.SetShaderParameter("echo_lifts", watercolour.EchoLifts);
        material.SetShaderParameter("echo_dash_tiles", watercolour.EchoDashTiles);
        material.SetShaderParameter("echo_fade_px", watercolour.EchoFadePx);
        material.SetShaderParameter("ripple_strength", watercolour.RippleStrength);
        material.SetShaderParameter("ripple_width_px", watercolour.RippleWidthPx);
        material.SetShaderParameter("ripple_cell_tiles", watercolour.RippleCellTiles);
        material.SetShaderParameter("ripple_share", watercolour.RippleShare);
        material.SetShaderParameter("ripple_length_tiles", watercolour.RippleLengthTiles);
        material.SetShaderParameter("ripple_bow_tiles", watercolour.RippleBowTiles);
        material.SetShaderParameter("ripple_bank_tiles", watercolour.RippleBankTiles);
        material.SetShaderParameter("ripple_bank_level", BankWeightAt(watercolour.RippleBankTiles));
        material.SetShaderParameter("ripple_drift", watercolour.RippleDriftTilesPerSecond);
        material.SetShaderParameter("ripple_fade_px", watercolour.RippleFadePxPerTile);
        material.SetShaderParameter("hill_full_tilt_levels", watercolour.HillFullTiltLevels);
        material.SetShaderParameter("green_on", watercolour.GreenOn);
        material.SetShaderParameter("green_deep", Srgb(watercolour.GreenDeep));
        material.SetShaderParameter("green_low", Srgb(watercolour.GreenLow));
        material.SetShaderParameter("green_mid", Srgb(watercolour.GreenMid));
        material.SetShaderParameter("green_high", Srgb(watercolour.GreenHigh));
        material.SetShaderParameter("green_crest", Srgb(watercolour.GreenCrest));
        material.SetShaderParameter("green_warm", Srgb(watercolour.GreenWarm));
        material.SetShaderParameter("green_cool", Srgb(watercolour.GreenCool));
        material.SetShaderParameter("green_spring", Srgb(watercolour.GreenSpring));
        material.SetShaderParameter("green_summer", Srgb(watercolour.GreenSummer));
        material.SetShaderParameter("green_autumn", Srgb(watercolour.GreenAutumn));
        material.SetShaderParameter("green_winter", Srgb(watercolour.GreenWinter));
        material.SetShaderParameter("green_levels", watercolour.GreenLevels);
        material.SetShaderParameter("green_flat_level", watercolour.GreenFlatLevel);
        material.SetShaderParameter("green_wander", watercolour.GreenWander);
        material.SetShaderParameter("green_crest_turn", watercolour.GreenCrestTurn);
        material.SetShaderParameter("green_hollow", watercolour.GreenHollow);
        material.SetShaderParameter("green_warm_turn", watercolour.GreenWarmTurn);
        material.SetShaderParameter("green_cool_turn", watercolour.GreenCoolTurn);
        material.SetShaderParameter("green_steep", watercolour.GreenSteep);
        material.SetShaderParameter("green_kinds_share", watercolour.GreenKindsShare);
        material.SetShaderParameter("green_grass_share", watercolour.GreenGrassShare);
        material.SetShaderParameter("green_wood", watercolour.GreenWood);
        material.SetShaderParameter("brush_strength", watercolour.BrushStrength);
        material.SetShaderParameter("brush_tiles", watercolour.BrushTiles);
        material.SetShaderParameter("tuft_strength", watercolour.TuftStrength);
        material.SetShaderParameter("tuft_cell_tiles", watercolour.TuftCellTiles);
        material.SetShaderParameter("tuft_length_tiles", watercolour.TuftLengthTiles);
        material.SetShaderParameter("tuft_share", watercolour.TuftShare);
        material.SetShaderParameter("tuft_width_px", watercolour.TuftWidthPx);
        material.SetShaderParameter("tuft_fade_px", watercolour.TuftFadePxPerTile);
        material.SetShaderParameter("hachure_texels_per_tile", (float)HachureTexelsPerTile);
        material.SetShaderParameter("hachure_strength", watercolour.HachureStrength);
        material.SetShaderParameter("hachure_width_share", watercolour.HachureWidthShare);
        material.SetShaderParameter("hachure_fade_px", watercolour.HachureFadePxPerTile);
        material.SetShaderParameter("hill_lip_strength", watercolour.HillLipStrength);
        material.SetShaderParameter("hill_lip_width_share", watercolour.HillLipWidthShare);
        material.SetShaderParameter("ripple_repeat_cells", RippleRepeatCells);

        // The clock's period: the grid drifts RippleRepeatCells cells in it, and a whole number of lives fit it, so the wrap
        // is not seen (§4.4). The life the shader is given is the file's, rounded to fit.
        _flowPeriod = RippleRepeatCells * watercolour.RippleCellTiles.X / Math.Max(watercolour.RippleDriftTilesPerSecond, 1e-3f);
        double lives = Math.Max(1.0, Math.Round(_flowPeriod / Math.Max(watercolour.RippleLifeSeconds, 1e-3f)));
        material.SetShaderParameter("flow_period_s", (float)_flowPeriod);
        material.SetShaderParameter("ripple_life_s", (float)(_flowPeriod / lives));
        _flowClock %= _flowPeriod;
        material.SetShaderParameter("flow_seconds", (float)_flowClock);

        // The map's own colours, and Joe's four painted tiles (A13–A21).
        material.SetShaderParameter("beyond_srgb", Srgb(Beyond));
        material.SetShaderParameter("ground_srgb", Srgb(Ground));
        material.SetShaderParameter("kind_wood", Srgb(ColourOf(Terrain.Forest)));
        material.SetShaderParameter("kind_young", Srgb(ColourOf(Terrain.Sapling)));
        material.SetShaderParameter("kind_stone", Srgb(ColourOf(Terrain.Rock)));
        material.SetShaderParameter("kind_iron", Srgb(ColourOf(Terrain.IronDeposit)));
        material.SetShaderParameter("kind_water", Srgb(ColourOf(Terrain.Water)));
        material.SetShaderParameter("kind_shallows", Srgb(ShallowsColour));
        material.SetShaderParameter("ground_wood", GD.Load<Texture2D>(GrassLab.WoodsTilePath));
        material.SetShaderParameter("ground_stone", GD.Load<Texture2D>(GrassLab.StoneTilePath));
        material.SetShaderParameter("ground_iron", GD.Load<Texture2D>(GrassLab.IronTilePath));
        material.SetShaderParameter("ground_water", GD.Load<Texture2D>(GrassLab.WaterTilePath));

        _watercolourMaterial = material;
        _valley = new ValleyTexture(withWeights: true);

        var layer = new Control
        {
            Name = "Watercolour",
            ShowBehindParent = true,
            MouseFilter = MouseFilterEnum.Ignore,
            Material = material,
        };
        layer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layer.Draw += () => layer.DrawRect(new Rect2(Vector2.Zero, layer.Size), Colors.White);
        AddChild(layer);
        _watercolourLayer = layer;
    }

    /// <summary>
    /// ⭐ The valley's turn of <c>_Draw</c> with the tick on: the bake brought up to date as ever (it holds the mix now), and
    /// the layer told where the valley is, how near it is, the season, the snow and the line — uniforms only.
    /// </summary>
    private void PaintTheWatercolour(Rect2 valley)
    {
        _valley.Refresh(_world!);
        ShaderMaterial material = _watercolourMaterial!;
        SimConfig config = _world!.Config;

        // After a refresh all three exist: this bake keeps the mix (BuildTheWatercolourLayer).
        material.SetShaderParameter("valley_colour", _valley.Texture!);
        material.SetShaderParameter("ground_map", _valley.WeightsTexture!);
        material.SetShaderParameter("ground_map_shares", _valley.SharesTexture!);
        material.SetShaderParameter("ground_map_tiles", new Vector2(config.MapWidth, config.MapHeight));
        material.SetShaderParameter("valley_origin_px", valley.Position);
        material.SetShaderParameter("valley_size_px", valley.Size);
        material.SetShaderParameter("px_per_tile", _pixelsPerTile);
        HandTheRiverOver(material, config);
        HandTheHillsOver(material, config);

        (float season, float snow) = TheYearAt(_world.Tick + _alpha, config.TicksPerSeason);
        material.SetShaderParameter("grass_season", season);
        material.SetShaderParameter("snow_amount", snow);

        float split = _beforeAfter && valley.Size.X > 0f ? ((_splitShare * Size.X) - valley.Position.X) / valley.Size.X : -1f;
        material.SetShaderParameter("split_u", split);
    }

    /// <summary>
    /// ⭐ The river's own coordinates handed to the layer (A45, <c>river-drawn.md §3</c>) — <b>built once a world</b>, from
    /// its water tiles and the course it was cut down, because no <c>SetTerrain</c> makes or unmakes water. Never per frame
    /// (CLAUDE.md): a new world, or a load, is the only thing that rebuilds it.
    /// </summary>
    private void HandTheRiverOver(ShaderMaterial material, SimConfig config)
    {
        if (ReferenceEquals(_riverOf, _world))
        {
            return;
        }

        _riverOf = _world;
        _riverTable = RiverTable.Of(TheWaterTiles(), config.MapWidth, config.MapHeight, MapGenerator.CourseOf(config, _world!.Seed));
        material.SetShaderParameter("river_on", _riverTable is not null);
        if (_riverTable is null)
        {
            return;
        }

        material.SetShaderParameter("river_table", _riverTable.ToTexture());
        material.SetShaderParameter("river_rows", (float)_riverTable.Rows);
        material.SetShaderParameter("river_along", _riverTable.Along);
        material.SetShaderParameter("river_across", _riverTable.Across);
        material.SetShaderParameter("river_offset", _riverTable.Offset);
    }

    /// <summary>The heights handed to the shader, and the world they were built for: once a world (A66).</summary>
    private ImageTexture? _hillsTexture;
    private SimWorld? _hillsOf;

    /// <summary>The heights texture, for the probe: null on a flat valley.</summary>
    internal ImageTexture? HillsTexture => _hillsTexture;

    /// <summary>The hachures' bake (A68), built with the heights: null on a flat valley.</summary>
    private ImageTexture? _hachuresTexture;

    /// <summary>The painted crowns' tint (A68, <c>green-ground.md §3.8</c>): the look file's with the tick on.</summary>
    private Color _treeTint = Colors.White;

    /// <summary>
    /// ⭐ The valley's heights to the shader (A66, <c>hill-edges.md §4</c>), once a world: they never change in play. Not
    /// hashed, not saved (D335). The steep threshold is the sim's own, so the lip and the slow ground are the same ground.
    /// </summary>
    private void HandTheHillsOver(ShaderMaterial material, SimConfig config)
    {
        if (ReferenceEquals(_hillsOf, _world))
        {
            return;
        }

        _hillsOf = _world;
        GeneratedMap map = _world!.Map;
        bool[] water = TheWaterTiles();
        float[]? heights = HillHeights(map.Heights, water, config.MapWidth, config.MapHeight);
        _hillsTexture = null;
        if (heights is not null)
        {
            var steepness = new float[heights.Length];
            for (int i = 0; i < steepness.Length; i++)
            {
                steepness[i] = !water[i] && map.SteepnessAtIndex(i) >= config.SlopeSteepAt ? 1f : 0f;
            }

            float[] field = HillField(heights, steepness, config.MapWidth, config.MapHeight);
            var bytes = new byte[field.Length * 4];
            for (int i = 0; i < field.Length; i++)
            {
                BitConverter.TryWriteBytes(bytes.AsSpan(i * 4, 4), field[i]);
            }

            int w = config.MapWidth * HillTexelsPerTile, h = config.MapHeight * HillTexelsPerTile;
            using (Image hills = Image.CreateFromData(w, h, false, Image.Format.Rgbf, bytes)) // ⛔ `using` (A45)
            {
                _hillsTexture = ImageTexture.CreateFromImage(hills);
            }

            material.SetShaderParameter("heights_map", _hillsTexture);

            // The hachures (A68, `green-ground.md §4`): baked once a world beside the field they are traced on.
            byte[] strokes = HillHachures(field, water, config.MapWidth, config.MapHeight);
            using (Image hachures = Image.CreateFromData(
                config.MapWidth * HachureTexelsPerTile, config.MapHeight * HachureTexelsPerTile, false, Image.Format.Rgba8, strokes))
            {
                _hachuresTexture = ImageTexture.CreateFromImage(hachures);
            }

            material.SetShaderParameter("hachure_map", _hachuresTexture);
        }
        else
        {
            _hachuresTexture = null;
        }

        material.SetShaderParameter("hills_on", heights is not null);
    }

    /// <summary>
    /// The heights as the shader reads them (A66): each land tile's own level, and each water tile the level of the nearest
    /// land (filled outward from the banks), so the drop to the water is no slope — the sim counts steepness land to land
    /// (D737). Null where no land stands above level 0: a flat valley, every save from before heights.
    /// </summary>
    internal static float[]? HillHeights(IReadOnlyList<byte> heights, bool[] water, int width, int height)
    {
        var filled = new float[width * height];
        var known = new bool[width * height];
        var frontier = new System.Collections.Generic.Queue<int>();
        bool any = false;
        for (int i = 0; i < filled.Length; i++)
        {
            if (!water[i])
            {
                filled[i] = heights[i];
                known[i] = true;
                frontier.Enqueue(i);
                any |= heights[i] > 0;
            }
        }

        if (!any)
        {
            return null;
        }

        while (frontier.Count > 0)
        {
            int at = frontier.Dequeue();
            int x = at % width, y = at / width;
            foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                int next = (ny * width) + nx;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height || known[next])
                {
                    continue;
                }

                filled[next] = filled[at];
                known[next] = true;
                frontier.Enqueue(next);
            }
        }

        return filled;
    }

    /// <summary>Texels a tile in the hills' field: enough for a smooth lip at the closest zoom.</summary>
    internal const int HillTexelsPerTile = 4;

    /// <summary>Floats a texel in the hills' field: the height, the steep mask, the crest.</summary>
    internal const int HillChannels = 3;

    /// <summary>
    /// ⭐ The crest (A68, <c>green-ground.md §4</c>), a number a tile: above zero where the ground stands above its
    /// surroundings (a crest, a shoulder), below it in a hollow or at a bank's foot, zero on a plain. The heights blurred
    /// at 1.2 tiles less the heights blurred at 3.5, plus three tenths of the same at 3 and 8, in sixteenths of a level
    /// (a level is a sixteenth of a tile's rise, for drawing). No light in it: it is the land's own shape.
    /// </summary>
    internal static float[] HillCrest(float[] heights, int width, int height)
    {
        float[] near = Blurred(1.2f), wide = Blurred(3.5f), far = Blurred(3f), farther = Blurred(8f);
        var crest = new float[heights.Length];
        for (int i = 0; i < crest.Length; i++)
        {
            crest[i] = ((near[i] - wide[i]) + (0.3f * (far[i] - farther[i]))) / 16f;
        }

        return crest;

        float[] Blurred(float sigma)
        {
            int reach = (int)MathF.Ceiling(sigma * 3f);
            var kernel = new float[(2 * reach) + 1];
            float total = 0f;
            for (int k = -reach; k <= reach; k++)
            {
                kernel[k + reach] = MathF.Exp(-(k * k) / (2f * sigma * sigma));
                total += kernel[k + reach];
            }

            var across = new float[heights.Length];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;
                    for (int k = -reach; k <= reach; k++)
                    {
                        sum += kernel[k + reach] * heights[(y * width) + System.Math.Clamp(x + k, 0, width - 1)];
                    }

                    across[(y * width) + x] = sum / total;
                }
            }

            var down = new float[heights.Length];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;
                    for (int k = -reach; k <= reach; k++)
                    {
                        sum += kernel[k + reach] * across[(System.Math.Clamp(y + k, 0, height - 1) * width) + x];
                    }

                    down[(y * width) + x] = sum / total;
                }
            }

            return down;
        }
    }

    /// <summary>Texels a tile in the hachures' bake: strokes stand a third of a tile apart, so each has texels of its own.</summary>
    internal const int HachureTexelsPerTile = 8;

    /// <summary>
    /// ⭐ The hachures (A68, <c>green-ground.md §4</c>): pen strokes from a steep bank's lip down it, baked once a world
    /// because a stroke cannot be traced per pixel. For each texel, the one stroke nearest it (within two texels): RG where
    /// it starts, from the texel's own centre, in quarter-texels (128 is none); B its direction, 256 to the circle; A its
    /// length in 128ths of a tile, 0 where no stroke is near. The shader works the distance out from those, so the pen is
    /// as thin on screen at every zoom as the bank's own.
    /// </summary>
    /// <remarks>
    /// One stroke per third of a tile of lip, seeded where the steep mask crosses a half on its uphill side (the shader's
    /// own lip), walked downhill a twelfth of a tile at a time until its length is spent, the ground stops being steep, or
    /// water is met; long (1.25 tiles) and short (0.6) by a hash of its cell. ⛔ Every choice is a hash of the place: no
    /// random source, the same strokes every time (D632's rule for the lane).
    /// </remarks>
    internal static byte[] HillHachures(float[] field, bool[] water, int width, int height)
    {
        const int Cells = 3, Samples = 12;
        const float Reach = 0.25f, Edge = 2f;
        int n = HillTexelsPerTile, fw = width * n, fh = height * n;
        int m = HachureTexelsPerTile, bw = width * m, bh = height * m;

        float At(int channel, float x, float y)
        {
            float u = (x * n) - 0.5f, v = (y * n) - 0.5f;
            int x0 = System.Math.Clamp((int)MathF.Floor(u), 0, fw - 1), x1 = System.Math.Min(x0 + 1, fw - 1);
            int y0 = System.Math.Clamp((int)MathF.Floor(v), 0, fh - 1), y1 = System.Math.Min(y0 + 1, fh - 1);
            float fx = System.Math.Clamp(u - x0, 0f, 1f), fy = System.Math.Clamp(v - y0, 0f, 1f);
            float top = (field[(((y0 * fw) + x0) * HillChannels) + channel] * (1 - fx)) + (field[(((y0 * fw) + x1) * HillChannels) + channel] * fx);
            float bottom = (field[(((y1 * fw) + x0) * HillChannels) + channel] * (1 - fx)) + (field[(((y1 * fw) + x1) * HillChannels) + channel] * fx);
            return (top * (1 - fy)) + (bottom * fy);
        }

        (float X, float Y, float Length) Downhill(float x, float y)
        {
            float hx = At(0, x + Reach, y) - At(0, x - Reach, y), hy = At(0, x, y + Reach) - At(0, x, y - Reach);
            float length = MathF.Sqrt((hx * hx) + (hy * hy));
            return length < 1e-3f ? (0f, 0f, 0f) : (-hx / length, -hy / length, length);
        }

        // The seeds: in each third-of-a-tile cell, the sample nearest the mask's halfway line on its uphill side.
        int cellsAcross = width * Cells, cellsDown = height * Cells;
        var miss = new float[cellsAcross * cellsDown];
        var seedX = new float[miss.Length];
        var seedY = new float[miss.Length];
        System.Array.Fill(miss, float.MaxValue);
        for (int j = 0; j < height * Samples; j++)
        {
            for (int i = 0; i < width * Samples; i++)
            {
                float x = (i + 0.5f) / Samples, y = (j + 0.5f) / Samples;
                float off = MathF.Abs(At(1, x, y) - 0.5f);
                if (off >= 0.12f)
                {
                    continue;
                }

                (float dx, float dy, float slope) = Downhill(x, y);
                float rising = (((At(1, x + Reach, y) - At(1, x - Reach, y)) * dx) + ((At(1, x, y + Reach) - At(1, x, y - Reach)) * dy)) / (2f * Reach);
                int cell = ((j * Cells / Samples) * cellsAcross) + (i * Cells / Samples);
                if (slope > 0f && rising > 0.06f && off < miss[cell])
                {
                    miss[cell] = off;
                    seedX[cell] = x;
                    seedY[cell] = y;
                }
            }
        }

        var bytes = new byte[bw * bh * 4];
        var nearest = new float[bw * bh];
        System.Array.Fill(nearest, float.MaxValue);
        for (int cell = 0; cell < miss.Length; cell++)
        {
            if (miss[cell] == float.MaxValue)
            {
                continue;
            }

            int cx = cell % cellsAcross, cy = cell / cellsAcross;
            float wanted = (HachureRand(cx, cy, 1) < 0.5f ? 1.25f : 0.6f) * (0.75f + (0.45f * HachureRand(cx, cy, 2)));
            float x = seedX[cell], y = seedY[cell];
            int taken = 0;
            for (int steps = (int)(wanted * Samples); taken < steps; taken++)
            {
                (float dx, float dy, float slope) = Downhill(x, y);
                float nx = x + (dx / Samples), ny = y + (dy / Samples);
                if (slope == 0f || nx < 0f || ny < 0f || nx >= width || ny >= height
                    || water[((int)ny * width) + (int)nx] || At(1, nx, ny) < 0.15f)
                {
                    break;
                }

                x = nx;
                y = ny;
            }

            // The stroke as the shader will read it back: its start on the bake's quarter-texel lattice, its direction in
            // 256ths of a circle, its length in 128ths of a tile. Measured from those, so the stamp and the pen agree.
            float quarter = m * 4f;
            float startX = MathF.Round(seedX[cell] * quarter) / quarter, startY = MathF.Round(seedY[cell] * quarter) / quarter;
            float length = MathF.Sqrt(((x - startX) * (x - startX)) + ((y - startY) * (y - startY)));
            if (taken < 4 || length < 0.25f)
            {
                continue;
            }

            int turn = ((int)MathF.Round(MathF.Atan2(y - startY, x - startX) / MathF.Tau * 256f) + 256) % 256;
            int reach = System.Math.Clamp((int)MathF.Round(length * 128f), 1, 255);
            float ux = MathF.Cos(turn * MathF.Tau / 256f), uy = MathF.Sin(turn * MathF.Tau / 256f), run = reach / 128f;
            float endX = startX + (ux * run), endY = startY + (uy * run);
            int x0 = System.Math.Max(0, (int)MathF.Floor((System.Math.Min(startX, endX) * m) - Edge));
            int x1 = System.Math.Min(bw - 1, (int)MathF.Ceiling((System.Math.Max(startX, endX) * m) + Edge));
            int y0 = System.Math.Max(0, (int)MathF.Floor((System.Math.Min(startY, endY) * m) - Edge));
            int y1 = System.Math.Min(bh - 1, (int)MathF.Ceiling((System.Math.Max(startY, endY) * m) + Edge));
            for (int ty = y0; ty <= y1; ty++)
            {
                for (int tx = x0; tx <= x1; tx++)
                {
                    float px = ((tx + 0.5f) / m) - startX, py = ((ty + 0.5f) / m) - startY;
                    float along = System.Math.Clamp((px * ux) + (py * uy), 0f, run);
                    float ex = px - (ux * along), ey = py - (uy * along);
                    float away = MathF.Sqrt((ex * ex) + (ey * ey)) * m;
                    int at = (ty * bw) + tx;
                    if (away <= Edge && away < nearest[at])
                    {
                        nearest[at] = away;
                        bytes[at * 4] = (byte)(128 + (int)MathF.Round(((startX * m) - (tx + 0.5f)) * 4f));
                        bytes[(at * 4) + 1] = (byte)(128 + (int)MathF.Round(((startY * m) - (ty + 0.5f)) * 4f));
                        bytes[(at * 4) + 2] = (byte)turn;
                        bytes[(at * 4) + 3] = (byte)reach;
                    }
                }
            }
        }

        return bytes;
    }

    /// <summary>A number in [0, 1) for a cell and a salt: an integer hash, the same everywhere and every time.</summary>
    private static float HachureRand(int x, int y, uint salt)
    {
        unchecked
        {
            uint h = ((uint)x * 0x9E3779B1u) ^ ((uint)y * 0x85EBCA77u) ^ (salt * 0xC2B2AE3Du);
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return (h >> 8) / 16777216f;
        }
    }

    /// <summary>
    /// The hills' field (A66): the heights and the steep mask (1 where the sim calls a tile steep), each upsampled to <see cref="HillTexelsPerTile"/> a tile
    /// (bilinear between tile centres) and softened by two box passes half a tile across, interleaved R height, G steepness,
    /// and (A68) B the crest (<see cref="HillCrest"/>), brought up the same way.
    /// ⚠️ Softened, because the shader draws the lip where steepness crosses the steep line, and a per-tile field crosses it
    /// in tile-shaped steps: the first build drew blocks at the widest zoom.
    /// </summary>
    internal static float[] HillField(float[] heights, float[] steepness, int width, int height)
    {
        int n = HillTexelsPerTile, w = width * n, h = height * n;
        float[] r = Softened(Upsampled(heights)), g = Softened(Upsampled(steepness));
        float[] b = Softened(Upsampled(HillCrest(heights, width, height)));
        var field = new float[w * h * HillChannels];
        for (int i = 0; i < w * h; i++)
        {
            field[i * HillChannels] = r[i];
            field[(i * HillChannels) + 1] = g[i];
            field[(i * HillChannels) + 2] = b[i];
        }

        return field;

        float[] Upsampled(float[] tiles)
        {
            var up = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = ((y + 0.5f) / n) - 0.5f;
                int y0 = System.Math.Clamp((int)MathF.Floor(v), 0, height - 1), y1 = System.Math.Min(y0 + 1, height - 1);
                float fy = System.Math.Clamp(v - y0, 0f, 1f);
                for (int x = 0; x < w; x++)
                {
                    float u = ((x + 0.5f) / n) - 0.5f;
                    int x0 = System.Math.Clamp((int)MathF.Floor(u), 0, width - 1), x1 = System.Math.Min(x0 + 1, width - 1);
                    float fx = System.Math.Clamp(u - x0, 0f, 1f);
                    float top = (tiles[(y0 * width) + x0] * (1 - fx)) + (tiles[(y0 * width) + x1] * fx);
                    float bottom = (tiles[(y1 * width) + x0] * (1 - fx)) + (tiles[(y1 * width) + x1] * fx);
                    up[(y * w) + x] = (top * (1 - fy)) + (bottom * fy);
                }
            }

            return up;
        }

        float[] Softened(float[] f)
        {
            int reach = n / 2;
            for (int pass = 0; pass < 2; pass++)
            {
                var across = new float[f.Length];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float sum = 0f;
                        for (int k = -reach; k <= reach; k++)
                        {
                            sum += f[(y * w) + System.Math.Clamp(x + k, 0, w - 1)];
                        }

                        across[(y * w) + x] = sum / ((2 * reach) + 1);
                    }
                }

                var down = new float[f.Length];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float sum = 0f;
                        for (int k = -reach; k <= reach; k++)
                        {
                            sum += across[(System.Math.Clamp(y + k, 0, h - 1) * w) + x];
                        }

                        down[(y * w) + x] = sum / ((2 * reach) + 1);
                    }
                }

                f = down;
            }

            return f;
        }
    }

    /// <summary>The valley's water, row by row from its north-west corner, as <see cref="RiverTable.Of"/> reads it.</summary>
    private bool[] TheWaterTiles()
    {
        SimConfig config = _world!.Config;
        var water = new bool[config.MapWidth * config.MapHeight];
        for (int j = 0; j < config.MapHeight; j++)
        {
            for (int i = 0; i < config.MapWidth; i++)
            {
                water[(j * config.MapWidth) + i] = _world.Map.TerrainAt(new GridPos(config.MapMinX + i, config.MapMinY + j)) == Terrain.Water;
            }
        }

        return water;
    }

    /// <summary>
    /// ⭐ The year as the ground shows it, from the tick: the grass's season (0 spring … 3 winter, 4 spring again), each
    /// holding for <see cref="SeasonHolds"/> of its length and turning into the next over the rest (D628); and the snow,
    /// which lies as autumn turns, is whole through winter's hold, and melts as winter turns to spring (A23 — Joe: white at
    /// mid-winter). A pure function of the tick, so a place and a time always give the same picture; the sim untouched.
    /// </summary>
    internal static (float Season, float Snow) TheYearAt(double tick, int ticksPerSeason)
    {
        double perSeason = Math.Max(1, ticksPerSeason);
        double intoYear = tick % (perSeason * 4.0);
        int season = Math.Min(3, (int)(intoYear / perSeason));
        double within = (intoYear - (season * perSeason)) / perSeason;
        double turning = Math.Clamp((within - SeasonHolds) / (1.0 - SeasonHolds), 0.0, 1.0);

        double snow = season switch
        {
            2 => turning,
            3 => 1.0 - turning,
            _ => 0.0,
        };

        return ((float)(season + turning), (float)snow);
    }

    /// <summary>
    /// The before/after line, over everything: a hairline in the map's ink, a handle at the middle, and the two words —
    /// drawn last in <c>_Draw</c>, as the ghost is, because it is a tool in the hand.
    /// </summary>
    private void DrawTheBeforeAfterLine()
    {
        if (!_watercolour || !_beforeAfter)
        {
            return;
        }

        float x = _splitShare * Size.X;
        var ink = new Color(0.12f, 0.13f, 0.13f, 0.9f);
        var paper = new Color(0.97f, 0.95f, 0.90f, 0.95f);
        DrawLine(new Vector2(x, 0f), new Vector2(x, Size.Y), ink, 2f);

        var handle = new Rect2(x - 14f, (Size.Y / 2f) - 14f, 28f, 28f);
        DrawRect(handle, paper);
        DrawRect(handle, ink, filled: false, width: 1.5f);
        float mid = Size.Y / 2f;
        DrawLine(new Vector2(x - 3f, mid - 5f), new Vector2(x - 8f, mid), ink, 2f);
        DrawLine(new Vector2(x - 8f, mid), new Vector2(x - 3f, mid + 5f), ink, 2f);
        DrawLine(new Vector2(x + 3f, mid - 5f), new Vector2(x + 8f, mid), ink, 2f);
        DrawLine(new Vector2(x + 8f, mid), new Vector2(x + 3f, mid + 5f), ink, 2f);

        Font font = ThemeDB.FallbackFont;
        const int size = 13;
        DrawTheWord("Before", new Vector2(x - 12f - font.GetStringSize("Before", fontSize: size).X, 36f));
        DrawTheWord("After", new Vector2(x + 12f, 36f));

        void DrawTheWord(string word, Vector2 at)
        {
            Vector2 measured = font.GetStringSize(word, fontSize: size);
            DrawRect(new Rect2(at - new Vector2(6f, measured.Y - 2f), measured + new Vector2(12f, 4f)), paper);
            DrawString(font, at, word, fontSize: size, modulate: ink);
        }
    }

    /// <summary>
    /// ⭐ A press on the before/after line takes hold of it, and a drag moves it — <b>before anything else the map does with a
    /// press</b>, so the line moves and nothing under it is clicked. Anywhere else, the press is the map's as ever.
    /// </summary>
    private bool TheBeforeAfterLineTakes(InputEvent @event)
    {
        if (!_watercolour || !_beforeAfter)
        {
            return false;
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } press)
        {
            if (press.Pressed && Mathf.Abs(press.Position.X - (_splitShare * Size.X)) <= SplitGrabPixels)
            {
                _draggingTheSplit = true;
                AcceptEvent();
                return true;
            }

            if (!press.Pressed && _draggingTheSplit)
            {
                _draggingTheSplit = false;
                AcceptEvent();
                return true;
            }
        }

        if (_draggingTheSplit && @event is InputEventMouseMotion drag && Size.X > 0f)
        {
            _splitShare = Mathf.Clamp(drag.Position.X / Size.X, 0f, 1f);
            QueueRedraw();
            AcceptEvent();
            return true;
        }

        return false;
    }

    private static Vector3 Srgb(Color colour) => new(colour.R, colour.G, colour.B);

    /// <summary>
    /// Every uniform the map sets on the layer — what the compiled shader must declare (HANDOFF art trap 6: a shader that
    /// did not compile declares none, and every other check stays green over it).
    /// </summary>
    private static readonly string[] WatercolourUniforms =
    {
        "grass_masks", "grass_season_atlas", "grass_jitter", "grass_strength", "tiles_per_repeat", "macro_on",
        "macro_cycle_tiles", "macro_strength_amount", "macro_season_amount", "hex_on", "hex_sharpness", "height_blend",
        "grain_amount", "coverage_fade", "splat_sharpness", "bleed_reach_tiles", "bleed_scale_tiles", "grain_scale_tiles",
        "snow_srgb", "snow_blend", "snow_patchiness", "snow_patch_tiles", "ground_tex_tiles", "ground_tex_antitile",
        "beyond_srgb", "ground_srgb", "kind_wood", "kind_young", "kind_stone", "kind_iron", "kind_water", "kind_shallows",
        "ground_wood", "ground_stone", "ground_iron", "ground_water", "valley_colour", "ground_map", "ground_map_shares",
        "ground_map_tiles", "valley_origin_px", "valley_size_px", "px_per_tile", "grass_season", "snow_amount", "split_u",
        "ink_srgb", "ink_strength", "ink_width_px", "ink_pressure", "ink_pressure_tiles", "ink_lifts", "ink_feather_px",
        "pool_strength", "pool_width_px", "snow_peak", "snow_shadow_srgb", "snow_shadow_strength", "ice_srgb",
        "ice_width_tiles", "ice_strength", "river_winter_darken", "river_on", "river_table", "river_rows", "river_along",
        "river_across", "river_offset", "channel_strength", "echo_strength", "echo_width_share", "echo_levels", "echo_tiles",
        "echo_lifts", "echo_dash_tiles", "echo_fade_px", "ripple_strength", "ripple_width_px", "ripple_cell_tiles",
        "ripple_share", "ripple_length_tiles", "ripple_bow_tiles", "ripple_bank_tiles", "ripple_bank_level", "ripple_drift", "ripple_life_s",
        "ripple_fade_px", "ripple_repeat_cells", "flow_seconds", "flow_period_s", "hills_on", "heights_map",
        "hill_full_tilt_levels", "hill_lip_strength", "hill_lip_width_share",
        "green_on", "green_deep", "green_low", "green_mid", "green_high", "green_crest", "green_warm", "green_cool",
        "green_spring", "green_summer", "green_autumn", "green_winter", "green_levels", "green_flat_level", "green_wander",
        "green_crest_turn", "green_hollow", "green_warm_turn", "green_cool_turn", "green_steep", "green_kinds_share",
        "green_grass_share", "green_wood", "brush_strength", "brush_tiles", "tuft_strength", "tuft_cell_tiles", "tuft_length_tiles",
        "tuft_share", "tuft_width_px", "tuft_fade_px", "hachure_map", "hachure_texels_per_tile", "hachure_strength",
        "hachure_width_share", "hachure_fade_px",
    };

    /// <summary>
    /// ⭐ The probe's <c>watercolour:</c> line (A25) — <b>everything about the tick that can be checked headless</b>, which is
    /// not what it looks like (<c>_Draw</c> never runs there, D340; that is the windowed renders and Joe's eyes).
    /// </summary>
    /// <remarks>
    /// It opens on, the layer built and showing (A58: the default since Joe's *"make watercolour the default look"*; it opened
    /// off with nothing loaded until then, A23's leanness); switched on, the shader compiles and declares every uniform the map
    /// sets; every look value is the two look files' and every colour the palette's; the valley's turn of <c>_Draw</c>
    /// hands it a bake that keeps the mix, the valley's rect and the zoom; the line takes a press on it and a drag, and
    /// nothing else; the year's grass and snow land where A23 says on five days; and switched off, the map is as it was.
    /// ⚠️ It leaves the layer built but hidden — switching on is a one-time cost the probe has now paid.
    /// </remarks>
    internal string TheWatercolourHolds()
    {
        var faults = new System.Collections.Generic.List<string>();
        if (_world is null)
        {
            return "[widths] watercolour: ⛔ no world to draw";
        }

        bool wasOn = _watercolour;
        bool wasBeforeAfter = _beforeAfter;
        float wasSplit = _splitShare;

        // ⛔ Named from the decision, not read off the tick (A58): the valley opens in watercolour, the slider off.
        if (!wasOn || wasBeforeAfter || _watercolourLayer is not { Visible: true })
        {
            faults.Add($"opens with the tick {wasOn}, the slider {wasBeforeAfter}, a layer {_watercolourLayer is not null} showing {_watercolourLayer?.Visible} (want the tick on, the slider off, the layer built and showing)");
        }

        ShowWatercolour(true);
        ShaderMaterial? material = _watercolourMaterial;
        if (material is null || _watercolourLayer is not { Visible: true, ShowBehindParent: true } || !_watercolour)
        {
            faults.Add("switched on, there is no visible layer behind the map holding the material");

            // Put it back before saying so: the red-check found this return left the tick on for the lines after (A25).
            ShowWatercolour(wasOn);
            return Said();
        }

        var declared = new System.Collections.Generic.HashSet<string>();
        foreach (Godot.Collections.Dictionary uniform in material.Shader.GetShaderUniformList())
        {
            declared.Add(uniform["name"].AsString());
        }

        foreach (string uniform in WatercolourUniforms)
        {
            if (!declared.Contains(uniform))
            {
                faults.Add($"valley_terrain.gdshader declares no {uniform} ({declared.Count} in all; 0 is a shader that did not compile)");
            }
        }

        GrassLook look = GrassLook.Approved;
        GrassLabTerrain terrain = GrassLabTerrain.StartingPoint;
        Near("grass_strength", look.Strength);
        Near("grass_jitter", look.Jitter);
        Near("tiles_per_repeat", look.TilesPerRepeat);
        Near("macro_cycle_tiles", look.MacroCycleTiles);
        Near("macro_strength_amount", look.MacroStrengthAmount);
        Near("macro_season_amount", look.MacroSeasonAmount);
        Near("hex_sharpness", look.HexSharpness);
        Near("grain_amount", look.PaperGrain);
        Near("coverage_fade", terrain.CoverageFade);
        Near("splat_sharpness", terrain.SplatSharpness);
        Near("bleed_reach_tiles", terrain.BleedReachTiles);
        Near("bleed_scale_tiles", terrain.BleedScaleTiles);
        Near("grain_scale_tiles", terrain.GrainScaleTiles);
        Near("snow_patchiness", terrain.SnowPatchiness);
        Near("snow_patch_tiles", terrain.SnowPatchTiles);
        Near("ground_tex_tiles", terrain.TextureTilesPerRepeat);
        ValleyWatercolourLook watercolour = ValleyWatercolourLook.Current;
        Near("ink_strength", watercolour.InkStrength);
        Near("ink_width_px", watercolour.InkWidthPx);
        Near("ink_pressure", watercolour.InkPressure);
        Near("ink_pressure_tiles", watercolour.InkPressureTiles);
        Near("ink_lifts", watercolour.InkLifts);
        Near("ink_feather_px", watercolour.InkFeatherPx);
        Near("pool_strength", watercolour.PoolStrength);
        Near("pool_width_px", watercolour.PoolWidthPx);
        Near("snow_peak", watercolour.SnowPeak);
        Near("snow_shadow_strength", watercolour.SnowShadowStrength);
        Near("ice_width_tiles", watercolour.IceWidthTiles);
        Near("ice_strength", watercolour.IceStrength);
        Near("river_winter_darken", watercolour.RiverWinterDarken);
        Same("snow_shadow_srgb", material.GetShaderParameter("snow_shadow_srgb").AsVector3() == Srgb(watercolour.SnowShadowColour));
        Same("ice_srgb", material.GetShaderParameter("ice_srgb").AsVector3() == Srgb(watercolour.IceColour));

        // ⛔ Winter as A26 call 3 settled it, named here and not read off the file alone (trap 17): patchy — the peak under a
        // blanket, 0.7–0.8 — with its shadows and its ice, each on.
        if (watercolour.SnowPeak < 0.7f || watercolour.SnowPeak > 0.8f || watercolour.SnowShadowStrength <= 0f
            || watercolour.IceStrength <= 0f || watercolour.IceWidthTiles <= 0f || watercolour.RiverWinterDarken <= 0f)
        {
            faults.Add($"winter is not A26's: peak {watercolour.SnowPeak} (want 0.7–0.8), shadows {watercolour.SnowShadowStrength}, ice {watercolour.IceStrength} over {watercolour.IceWidthTiles} tiles, the river darker by {watercolour.RiverWinterDarken} (want each above 0)");
        }
        Same("ink_srgb", material.GetShaderParameter("ink_srgb").AsVector3() == Srgb(watercolour.InkColour));
        if (watercolour.InkStrength <= 0f || watercolour.InkWidthPx <= 0f)
        {
            faults.Add($"{ValleyWatercolourLook.Path} draws no ink (strength {watercolour.InkStrength}, width {watercolour.InkWidthPx})");
        }

        // The trees' ink (A28): the file's, in the canopies' mesh, while on.
        if (watercolour.TreeInkTiles <= 0f || _canopyInkTiles != watercolour.TreeInkTiles || _canopyInk != watercolour.InkColour)
        {
            faults.Add($"the trees' ink is {_canopyInkTiles} tiles of {_canopyInk} (want the file's {watercolour.TreeInkTiles} of {watercolour.InkColour}, above 0)");
        }
        Same("macro_on", material.GetShaderParameter("macro_on").AsBool() == look.MacroOn);
        Same("hex_on", material.GetShaderParameter("hex_on").AsBool() == look.HexOn);
        Same("height_blend", material.GetShaderParameter("height_blend").AsBool() == look.HeightBlend);
        Same("snow_blend", material.GetShaderParameter("snow_blend").AsInt32() == terrain.SnowBlend);
        Same("ground_tex_antitile", material.GetShaderParameter("ground_tex_antitile").AsInt32() == terrain.TextureAntiTiling);
        Same("snow_srgb", material.GetShaderParameter("snow_srgb").AsVector3() == Srgb(terrain.SnowColour));
        Same("ground_srgb", material.GetShaderParameter("ground_srgb").AsVector3() == Srgb(Ground));
        Same("beyond_srgb", material.GetShaderParameter("beyond_srgb").AsVector3() == Srgb(Beyond));
        Same("kind_wood", material.GetShaderParameter("kind_wood").AsVector3() == Srgb(ColourOf(Terrain.Forest)));
        Same("kind_young", material.GetShaderParameter("kind_young").AsVector3() == Srgb(ColourOf(Terrain.Sapling)));
        Same("kind_stone", material.GetShaderParameter("kind_stone").AsVector3() == Srgb(ColourOf(Terrain.Rock)));
        Same("kind_iron", material.GetShaderParameter("kind_iron").AsVector3() == Srgb(ColourOf(Terrain.IronDeposit)));
        Same("kind_water", material.GetShaderParameter("kind_water").AsVector3() == Srgb(ColourOf(Terrain.Water)));
        Same("kind_shallows", material.GetShaderParameter("kind_shallows").AsVector3() == Srgb(ShallowsColour));
        Same("grass_masks", material.GetShaderParameter("grass_masks").As<Texture2D>()?.ResourcePath == $"res://textures/grass/grass_{look.Masks}_masks.png");
        Same("ground_wood", material.GetShaderParameter("ground_wood").As<Texture2D>()?.ResourcePath == GrassLab.WoodsTilePath);
        Same("ground_stone", material.GetShaderParameter("ground_stone").As<Texture2D>()?.ResourcePath == GrassLab.StoneTilePath);
        Same("ground_iron", material.GetShaderParameter("ground_iron").As<Texture2D>()?.ResourcePath == GrassLab.IronTilePath);
        Same("ground_water", material.GetShaderParameter("ground_water").As<Texture2D>()?.ResourcePath == GrassLab.WaterTilePath);

        // The valley's turn of `_Draw`, asked directly: `_Draw` never runs headless (D340).
        SimConfig config = _world.Config;
        Vector2 topLeft = ToScreen(new Vector2(config.MapMinX - 0.5f, config.MapMinY - 0.5f));
        Vector2 bottomRight = ToScreen(new Vector2(config.MapMaxX + 0.5f, config.MapMaxY + 0.5f));
        var valley = new Rect2(topLeft, bottomRight - topLeft);
        PaintTheWatercolour(valley);

        Texture2D? mix = material.GetShaderParameter("ground_map").As<Texture2D>();
        Texture2D? colour = material.GetShaderParameter("valley_colour").As<Texture2D>();
        if (mix is null || colour is null || material.GetShaderParameter("ground_map_shares").As<Texture2D>() is null
            || mix.GetWidth() != config.MapWidth * ValleyTexture.WeightsPerTile
            || colour.GetWidth() != config.MapWidth * ValleyTexture.PixelsPerTile)
        {
            faults.Add($"the valley's turn hands the layer a mix of {mix?.GetWidth()} and a colour of {colour?.GetWidth()} px wide (want {config.MapWidth * ValleyTexture.WeightsPerTile} and {config.MapWidth * ValleyTexture.PixelsPerTile}, both, and the shares)");
        }

        Same("valley_origin_px", material.GetShaderParameter("valley_origin_px").AsVector2() == valley.Position);
        Same("valley_size_px", material.GetShaderParameter("valley_size_px").AsVector2() == valley.Size);
        Same("ground_map_tiles", material.GetShaderParameter("ground_map_tiles").AsVector2() == new Vector2(config.MapWidth, config.MapHeight));
        Near("px_per_tile", _pixelsPerTile);
        Near("split_u", -1f);

        // The line: shown, it stands mid-map; a press on it and a drag move it; a press away from it is the map's.
        ShowBeforeAfter(true);
        PaintTheWatercolour(valley);
        float expected = ((0.5f * Size.X) - valley.Position.X) / valley.Size.X;
        Near("split_u", expected);
        float x = _splitShare * Size.X;
        bool away = TheBeforeAfterLineTakes(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = new Vector2(x + 40f, Size.Y / 2f) });
        bool took = TheBeforeAfterLineTakes(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = new Vector2(x + 4f, Size.Y / 2f) });
        bool dragged = TheBeforeAfterLineTakes(new InputEventMouseMotion { Position = new Vector2(Size.X * 0.25f, Size.Y / 2f) });
        bool let = TheBeforeAfterLineTakes(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = new Vector2(Size.X * 0.25f, Size.Y / 2f) });
        bool after = TheBeforeAfterLineTakes(new InputEventMouseMotion { Position = new Vector2(Size.X * 0.75f, Size.Y / 2f) });
        if (away || !took || !dragged || !let || after || Mathf.Abs(_splitShare - 0.25f) > 1e-4f)
        {
            faults.Add($"the line: a press 40 px off taken {away}, on it {took}, the drag {dragged}, the release {let}, a move after {after}; it stands at {_splitShare:F3} (want false, true, true, true, false, 0.250)");
        }

        // The year (A23): five moments, as seasons into the year, each with what the grass and the snow should be there.
        // A season holds for its first two thirds and turns over the last third.
        Year(1.5, 1f, 0f, "mid-summer: summer, bare");
        Year(2.0 + (5.0 / 6.0), 2.5f, 0.5f, "five-sixths into autumn: half turned to winter, half snow");
        Year(3.25, 3f, 1f, "a quarter into winter: white");
        Year(3.9, 3.7f, 0.3f, "nine-tenths into winter: 70 % turned to spring, the snow 30 %");
        Year(0.1, 0f, 0f, "early spring: bare");

        // Off, the map is as it was before the watercolour: the layer hidden, no ink on the trees (A28).
        ShowBeforeAfter(wasBeforeAfter);
        ShowWatercolour(false);
        _splitShare = wasSplit;
        if (_watercolour || _watercolourLayer is { Visible: true })
        {
            faults.Add("switched off, the layer still shows");
        }

        // ⛔ Off, the trees mesh as they always did: no ink at all, so not one vertex differs (A28).
        if (_canopyInkTiles != 0f)
        {
            faults.Add($"switched off, the trees still carry {_canopyInkTiles} tiles of ink");
        }

        // And back as it opened.
        ShowWatercolour(wasOn);

        return Said();

        string Said() => faults.Count == 0
            ? $"[widths] watercolour: ✅ opens on, the layer showing (A58); on, valley_terrain.gdshader declares all {WatercolourUniforms.Length} uniforms the map sets, every value the look files' and the palette's, the mix and the colour handed over, the line taken and dragged and nothing else, the year's snow on five days, the trees inked; off, as it was"
            : $"[widths] watercolour: ⛔ {string.Join("; ", faults)}";

        void Near(string uniform, float want)
        {
            float got = material!.GetShaderParameter(uniform).AsSingle();
            if (Mathf.Abs(got - want) > 1e-5f)
            {
                faults.Add($"{uniform} is {got} (want {want})");
            }
        }

        void Same(string what, bool holds)
        {
            if (!holds)
            {
                faults.Add($"{what} is not the look files' or the palette's");
            }
        }

        void Year(double seasons, float season, float snow, string when)
        {
            int perSeason = config.TicksPerSeason;
            (float s, float w) = TheYearAt(seasons * perSeason, perSeason);
            if (Mathf.Abs(s - season) > 1e-4f || Mathf.Abs(w - snow) > 1e-4f)
            {
                faults.Add($"{when}: grass season {s:F3}, snow {w:F3} (want {season:F3}, {snow:F3})");
            }
        }
    }

    /// <summary>
    /// ⭐ The probe's <c>river drawn:</c> line (A45, <c>river-drawn.md §7</c>) — the river's table, its clock and its values,
    /// everything about the drawn river that can be checked headless. How it looks is the windowed renders and Joe's eyes.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Every expectation here is the probe's own</b> (HANDOFF art traps 17 and 25): each course's row and across,
    /// which way is downstream, the straight rivers' middles and widths, and the bank's weights at the echoes' depths are
    /// written out below, not read from <see cref="RiverTable"/> or the look file.
    /// </remarks>
    /// <summary>
    /// ⭐ The probe's <c>sites through the canopy:</c> line (A53, <c>tree-sprites.md §2</c>): a site marked in the woods
    /// comes through the painted canopy as its outline — its own turned footprint, in the clearing's orange while trees
    /// stand on its ground — with the watercolour and the trees on, and not otherwise. The site is taken back after.
    /// ⚠️ It checks what is drawn, not that <c>_Draw</c> draws it: that call is the render's to show (red-check zero).
    /// </summary>
    /// <summary>
    /// ⭐ The probe's <c>painted deposits:</c> line (A60, <c>painted-deposits.md §7</c>): the atlases as imported, the bands as
    /// the look file names them, every seam tile drawing its kind's cells — natural on a natural tile, worked on a worked
    /// one, two or three a tile — each a whole 128-px cell at its painted size; a kind with no band keeping its discs; winter
    /// its own atlas; off, nothing painted and the discs back.
    /// </summary>
    internal string ThePaintedDepositsHold()
    {
        if (_world is not SimWorld world)
        {
            return "[widths] painted deposits: ⛔ no world";
        }

        GeneratedMap map = world.Map;
        var faults = new System.Collections.Generic.List<string>();
        bool wasOn = _watercolour;

        // ⛔ Named from the spec, not read off the code (trap 17): 1024 × 512, a band of two 128-px rows a kind, stone 0, iron 1.
        foreach (string path in new[] { "res://textures/nodes/node_sprites.png", "res://textures/nodes/node_sprites_winter.png" })
        {
            Texture2D? texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            using Image? pixels = texture?.GetImage();
            if (texture is null || pixels is null || texture.GetWidth() != 1024 || texture.GetHeight() != 512
                || pixels.GetFormat() != Image.Format.Rgba8 || !pixels.HasMipmaps())
            {
                faults.Add($"{path} is {texture?.GetWidth()} × {texture?.GetHeight()}, {pixels?.GetFormat()}, mipmaps {pixels?.HasMipmaps()} (want 1024 × 512, Rgba8, mipmapped)");
            }
        }

        ShowWatercolour(true);
        if (!_nodeBands.TryGetValue(Terrain.Rock, out int stoneBand) || stoneBand != 0
            || !_nodeBands.TryGetValue(Terrain.IronDeposit, out int ironBand) || ironBand != 1 || _nodeRows != 4)
        {
            faults.Add($"the bands read {string.Join(", ", _nodeBands.Select(b => $"{b.Key} {b.Value}"))} over {_nodeRows} rows (want Rock 0, IronDeposit 1, four rows)");
        }

        // A stone tile made worked for the check, put back after (the faces are re-derived only when the village moves them).
        int workedAt = -1;
        for (int i = 0; i < map.Tiles.Count && workedAt < 0; i++)
        {
            workedAt = map.Tiles[i] == Terrain.Rock && (_faceShadow.Length <= i || !_faceShadow[i]) ? i : -1;
        }

        RefreshTheScenery(world);
        bool[] withAFace = (bool[])_faceShadow.Clone();
        if (workedAt >= 0 && workedAt < withAFace.Length)
        {
            withAFace[workedAt] = true;
        }

        // The face set handed in, as `faces:` does: the probe's village has no quarry (D449).
        _sceneryOf = null;
        RefreshTheScenery(world, facesForTheProbe: withAFace);

        int natural = 0, worked = 0;
        for (int i = 0; i < map.Tiles.Count; i++)
        {
            if (map.Tiles[i] is Terrain.Rock or Terrain.IronDeposit)
            {
                bool isWorked = _faceShadow.Length > i && _faceShadow[i];
                natural += isWorked ? 0 : 1;
                worked += isWorked ? 1 : 0;
            }
        }

        // Every rock a whole cell of its kind's band and the right row, at a deposit's painted size: a cell is 128 px of
        // painting at 128 px a tile, × 0.85–1.10.
        const float Du = 1f / 8f, Dv = 1f / 4f;
        int rocks = 0, wrong = 0, wrongKind = 0, workedRocks = 0;
        string? firstWrong = null;
        var usedCells = new bool[32];
        foreach (SceneryChunk chunk in _scenery)
        {
            if (chunk.Nodes.GetSurfaceCount() == 0)
            {
                continue;
            }

            Godot.Collections.Array arrays = chunk.Nodes.SurfaceGetArrays(0);
            Vector2[] at = arrays[(int)Mesh.ArrayType.Vertex].AsVector2Array();
            Vector2[] uv = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            for (int v = 0; v + 5 < at.Length; v += 6)
            {
                rocks++;
                int col = (int)((uv[v].X / Du) + 1e-3f), row = (int)((uv[v].Y / Dv) + 1e-3f);
                Vector2 corner = new(col * Du, row * Dv);
                float side = at[v + 1].X - at[v].X;
                bool whole = uv[v].IsEqualApprox(corner) && uv[v + 2].IsEqualApprox(corner + new Vector2(Du, Dv));
                if (!whole || side < 0.85f - 1e-3f || side > 1.10f + 1e-3f || row > 3)
                {
                    wrong++;
                    firstWrong ??= $"uv {uv[v]}–{uv[v + 2]}, {side:F2} tiles across";
                    continue;
                }

                usedCells[(row * 8) + col] = true;
                workedRocks += (row & 1) == 1 ? 1 : 0;

                // Its kind from the nearest seam tile to its foot: stone in rows 0–1, iron in 2–3; worked in the odd rows.
                Vector2 foot = (at[v] + at[v + 2]) / 2f;
                Terrain? kind = null;
                bool onWorked = false;
                float best = float.MaxValue;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var tile = new GridPos(Mathf.RoundToInt(foot.X) + dx, Mathf.RoundToInt(foot.Y) + dy);
                        Terrain there = map.TerrainAt(tile);
                        float d = new Vector2(tile.X, tile.Y).DistanceSquaredTo(foot);
                        if (there is Terrain.Rock or Terrain.IronDeposit && d < best)
                        {
                            best = d;
                            kind = there;
                            int idx = ((tile.Y - map.MinY) * map.Width) + (tile.X - map.MinX);
                            onWorked = idx >= 0 && idx < _faceShadow.Length && _faceShadow[idx];
                        }
                    }
                }

                int wantBand = kind == Terrain.IronDeposit ? 1 : 0;
                if (kind is null || row / 2 != wantBand || ((row & 1) == 1) != onWorked)
                {
                    wrongKind++;
                }
            }
        }

        int low = (2 * natural) + worked, high = (3 * natural) + (2 * worked);
        if (wrong > 0 || wrongKind > rocks / 50 || rocks < low || rocks > high || rocks == low || rocks == high || workedRocks < 1 || workedRocks > 2)
        {
            faults.Add($"{rocks} rocks on {natural} natural and {worked} worked seam tiles (want strictly between {low} and {high}: two or three a natural tile, one fewer worked); {wrong} not a whole cell at a deposit's size (first: {firstWrong}); {wrongKind} in the wrong kind's band or row; {workedRocks} worked rocks (want one or two)");
        }

        // Every natural drawing in the valley (rows 0 and 2), and the worked tile's in a worked row (1 or 3).
        int used = usedCells.Count(c => c);
        int naturalUsed = Enumerable.Range(0, 8).Count(c => usedCells[c]) + Enumerable.Range(16, 8).Count(c => usedCells[c]);
        if (naturalUsed != 16 || used - naturalUsed < 1)
        {
            faults.Add($"{naturalUsed} of the 16 natural drawings used and {used - naturalUsed} worked (want all 16, and one or more)");
        }

        // A kind with no band keeps its discs: iron's band taken away, iron paints nothing and its discs come back.
        (int standingWith, int rocksWith) = NodeTotals();
        _nodeBands.Remove(Terrain.IronDeposit);
        _sceneryOf = null;
        RefreshTheScenery(world, facesForTheProbe: withAFace);
        (int standingWithout, int rocksWithout) = NodeTotals();
        _nodeBands[Terrain.IronDeposit] = 1;
        if (rocksWithout >= rocksWith || standingWithout <= standingWith)
        {
            faults.Add($"with iron's band gone, {rocksWithout} painted vertices (want fewer than {rocksWith}) and {standingWithout} standing (want more than {standingWith})");
        }

        // Winter's atlas is its own; summer and autumn draw the summer one.
        if (TheNodeAtlasFor(TreeSet.Winter)?.DiffuseTexture?.ResourcePath != "res://textures/nodes/node_sprites_winter.png"
            || TheNodeAtlasFor(TreeSet.Autumn)?.DiffuseTexture?.ResourcePath != "res://textures/nodes/node_sprites.png")
        {
            faults.Add("the seasons' atlases: winter does not draw node_sprites_winter.png, or autumn not node_sprites.png");
        }

        // Off: nothing painted.
        ShowWatercolour(false);
        RefreshTheScenery(world);
        (int _, int rocksOff) = NodeTotals();
        if (rocksOff != 0 || _paintedNodes)
        {
            faults.Add($"off, {rocksOff} painted-deposit vertices (want none)");
        }

        ShowWatercolour(wasOn);
        _sceneryOf = null;
        RefreshTheScenery(world, facesForTheProbe: TheWorkedFaces(world));

        return faults.Count == 0
            ? $"[widths] painted deposits: ✅ node_sprites.png and its winter 1024 × 512, mipmapped; bands Rock 0, IronDeposit 1; {rocks} rocks on {natural} natural and {worked} worked seam tiles, two or three a tile, each a whole cell of its kind's band at its size, {workedRocks} worked on the tile made worked; {used} of 32 drawings; a kind with no band keeps its discs; winter its own atlas; off, nothing painted"
            : $"[widths] painted deposits: ⛔ {string.Join("; ", faults)}";

        (int Standing, int Nodes) NodeTotals()
        {
            int standing = 0, nodes = 0;
            foreach (SceneryChunk chunk in _scenery)
            {
                standing += chunk.StandingVertices;
                nodes += chunk.NodeVertices;
            }

            return (standing, nodes);
        }
    }

    /// <summary>
    /// ⭐ The probe's <c>painted crops:</c> line (A61, <c>painted-crops.md §7</c>): every painting as imported (1024², mipmapped)
    /// and drawn repeating; wheat's stages from its folder and the shared ground's from <c>_ground</c>; a crop with no folder
    /// finding no young painting, so its flat fill stands in; the year's stages at named moments; the world-anchored mapping;
    /// on with the tick and off without it.
    /// </summary>
    /// <remarks>⚠️ The fields themselves are drawn by <c>_Draw</c>, never headless (D340): the renders are the draw's evidence.</remarks>
    internal string ThePaintedCropsHold()
    {
        var faults = new System.Collections.Generic.List<string>();
        bool wasOn = _watercolour;
        ShowWatercolour(true);

        // ⛔ Named from the spec, not read off the code (trap 17): the five stages and the snow, 1024 × 1024, a folder a crop.
        (byte Crop, string Stage, string Path)[] paintings =
        {
            (0, "ploughed", "res://textures/crops/_ground/ploughed.png"),
            (0, "stubble", "res://textures/crops/_ground/stubble.png"),
            (0, "snow", "res://textures/crops/_ground/snow.png"),
            (1, "young", "res://textures/crops/wheat/young.png"),
            (1, "growing", "res://textures/crops/wheat/growing.png"),
            (1, "ripe", "res://textures/crops/wheat/ripe.png"),
            (1, "ploughed", "res://textures/crops/_ground/ploughed.png"),
        };
        foreach ((byte crop, string stage, string path) in paintings)
        {
            CanvasTexture? held = TheCropPainting(crop, stage);
            using Image? pixels = (held?.DiffuseTexture as Texture2D)?.GetImage();
            if (held?.DiffuseTexture?.ResourcePath != path || pixels is null || pixels.GetWidth() != 1024 || pixels.GetHeight() != 1024
                || !pixels.HasMipmaps() || held.TextureRepeat != TextureRepeatEnum.Enabled || held.TextureFilter != TextureFilterEnum.LinearWithMipmaps)
            {
                faults.Add($"crop {crop}'s {stage} is {held?.DiffuseTexture?.ResourcePath} {pixels?.GetWidth()} × {pixels?.GetHeight()} mipmaps {pixels?.HasMipmaps()} repeat {held?.TextureRepeat} (want {path}, 1024 × 1024, mipmapped, repeating)");
            }
        }

        // A crop the config has no row for has no painting of its own, and a growing stage has no shared one: flat fill.
        if (TheCropPainting(200, "young") is not null)
        {
            faults.Add("a crop with no row finds a young painting (want none, so its flat fill stands in)");
        }

        // The year (§3), at named moments: a season, then how far it has turned.
        (Terrain Stage, float At, string From, string To, float Share)[] year =
        {
            (Terrain.Field, 0.0f, "ploughed", "ploughed", 0f),
            (Terrain.Field, 1.5f, "ploughed", "stubble", 0.5f),
            (Terrain.Field, 2.0f, "stubble", "stubble", 0f),
            (Terrain.Field, 3.5f, "stubble", "ploughed", 0.5f),
            (Terrain.Sown, 0.0f, "young", "young", 0f),
            (Terrain.Sown, 0.5f, "young", "growing", 0.5f),
            (Terrain.Sown, 1.0f, "growing", "growing", 0f),
            (Terrain.Sown, 2.0f, "growing", "growing", 0f),
            (Terrain.Ripe, 2.0f, "ripe", "ripe", 0f),
        };
        foreach ((Terrain stage, float at, string from, string to, float share) in year)
        {
            (string f, string t, float s) = CropStageAt(stage, at);
            if (f != from || t != to || Mathf.Abs(s - share) > 1e-4f)
            {
                faults.Add($"{stage} at {at}: {f} → {t} at {s:F2} (want {from} → {to} at {share:F2})");
            }
        }

        // World-anchored, eight tiles a repeat (128 px a tile, 1024 px a painting).
        if (!CropUvOf(Vector2.Zero).IsEqualApprox(Vector2.Zero) || !CropUvOf(new Vector2(8f, 4f)).IsEqualApprox(new Vector2(1f, 0.5f)))
        {
            faults.Add($"the mapping puts (8, 4) at {CropUvOf(new Vector2(8f, 4f))} (want (1, 0.5): eight tiles a painting)");
        }

        bool onWithTheTick = _paintedCrops;
        ShowWatercolour(false);
        bool offWithout = !_paintedCrops;
        ShowWatercolour(wasOn);
        if (!onWithTheTick || !offWithout)
        {
            faults.Add($"painted with the tick on {onWithTheTick}, flat with it off {offWithout} (want both)");
        }

        return faults.Count == 0
            ? "[widths] painted crops: ✅ ploughed, stubble and snow shared, wheat's young, growing and ripe its own, each 1024 × 1024, mipmapped, repeating; a crop with no row keeps its flat fill; the year's stages at nine moments; eight tiles a painting, world-anchored; on with the tick, off without"
            : $"[widths] painted crops: ⛔ {string.Join("; ", faults)}";
    }

    /// <summary>
    /// ⭐ The probe's <c>hill edges:</c> line (A66, <c>hill-edges.md §6</c>): the heights the shader reads — each land tile
    /// its own level, each water tile its nearest land's, so a riverbank is no cliff — the look file's values and the
    /// sim's steep threshold handed over, and a flat valley drawing no hills. The draw itself is <c>_Draw</c>'s, never
    /// headless (D340): the renders are its evidence.
    /// </summary>
    internal string TheHillEdgesHold()
    {
        if (_world is not SimWorld world)
        {
            return "[widths] hill edges: ⛔ no world";
        }

        var faults = new System.Collections.Generic.List<string>();
        GeneratedMap map = world.Map;
        SimConfig config = world.Config;
        bool wasOn = _watercolour;
        ShowWatercolour(true);
        _hillsOf = null;
        ShaderMaterial? material = _watercolourMaterial;
        if (material is not null)
        {
            HandTheHillsOver(material, config);
        }

        int land = 0, raised = 0, steep = 0, highest = 0;
        for (int i = 0; i < map.Tiles.Count; i++)
        {
            if (map.Tiles[i] != Terrain.Water)
            {
                land++;
                raised += map.Heights[i] > 0 ? 1 : 0;
                steep += map.SteepnessAtIndex(i) >= config.SlopeSteepAt ? 1 : 0;
                highest = System.Math.Max(highest, map.Heights[i]);
            }
        }

        // The texture: four texels a tile, R the height and G the steepness. ⛔ Named from the spec (trap 17): 4 a tile.
        // ⛔ `using` (A45): a temporary Image left to the finalizer is the .NET crash at exit.
        using Image? image = _hillsTexture?.GetImage();
        if (raised > 0 && (image is null || image.GetWidth() != config.MapWidth * 4 || image.GetHeight() != config.MapHeight * 4
            || image.GetFormat() != Image.Format.Rgbf))
        {
            faults.Add($"the hills' field is {image?.GetWidth()} × {image?.GetHeight()} {image?.GetFormat()} (want four texels a tile, {config.MapWidth * 4} × {config.MapHeight * 4}, Rgbf)");
        }

        // The field on a made-up terrace: a 12 × 1 strip, flat at 0 then flat at 40 from tile 6 — softened, the height rises
        // through the step and is flat far from it, and the steepness peaks at the step and is zero far from it.
        var terraceHeights = new float[12];
        var terraceSteep = new float[12];
        for (int i = 0; i < 12; i++)
        {
            terraceHeights[i] = i < 6 ? 0f : 40f;
            terraceSteep[i] = i is 5 or 6 ? 1f : 0f;
        }

        float[] field = HillField(terraceHeights, terraceSteep, 12, 1);
        float HeightAtTexel(int x) => field[x * HillChannels];
        float SteepAtTexel(int x) => field[(x * HillChannels) + 1];
        if (HeightAtTexel(2) != 0f || HeightAtTexel(45) != 40f || !(HeightAtTexel(22) < HeightAtTexel(26))
            || SteepAtTexel(2) != 0f || SteepAtTexel(46) != 0f || !(SteepAtTexel(24) > 0.5f)
            // ⚠️ softened, not only upsampled: a tile's bilinear reaches no further than its neighbour's centre, so the mask is
            // still 0 at texel 15 (tile 3.75) unless the half-tile passes have carried it there
            || !(SteepAtTexel(15) > 0f))
        {
            faults.Add($"the field on a terrace: height {HeightAtTexel(2)} → {HeightAtTexel(22)},{HeightAtTexel(26)} → {HeightAtTexel(45)} (want 0, rising, 40), the steep mask {SteepAtTexel(2)}, {SteepAtTexel(24)}, {SteepAtTexel(46)} (want 0, over a half at the step, 0)");
        }

        // The mask from the sim's own class (`slope_steep_at`): a tile the sim calls steep in the middle of a steep patch (all
        // eight round it steep) reads over 0.9, and a tile with no steep ground within two reads 0.
        if (image is not null)
        {
            int inside = 0, insideWrong = 0, clear = 0, clearWrong = 0;
            for (int y = 2; y < config.MapHeight - 2; y++)
            {
                for (int x = 2; x < config.MapWidth - 2; x++)
                {
                    int steepNear = 0, steepWide = 0;
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int i = ((y + dy) * config.MapWidth) + x + dx;
                            bool s = map.Tiles[i] != Terrain.Water && map.SteepnessAtIndex(i) >= config.SlopeSteepAt;
                            steepWide += s ? 1 : 0;
                            steepNear += s && System.Math.Abs(dx) <= 1 && System.Math.Abs(dy) <= 1 ? 1 : 0;
                        }
                    }

                    float g = image.GetPixel((x * 4) + 2, (y * 4) + 2).G;
                    if (steepNear == 9)
                    {
                        inside++;
                        insideWrong += g > 0.9f ? 0 : 1;
                    }
                    else if (steepWide == 0)
                    {
                        clear++;
                        clearWrong += g == 0f ? 0 : 1;
                    }
                }
            }

            if (insideWrong > 0 || clearWrong > 0 || clear == 0)
            {
                faults.Add($"the steep mask: {insideWrong} of {inside} tiles inside a steep patch under 0.9, {clearWrong} of {clear} tiles two clear of any not 0 (want none, and some clear)");
            }
        }

        // The water filled from the land: a made-up strip, land at 5 and 9 either side of three water tiles, and a flat valley.
        float[]? strip = HillHeights(new byte[] { 5, 0, 0, 0, 9 }, new[] { false, true, true, true, false }, 5, 1);
        float[]? flat = HillHeights(new byte[] { 0, 0, 0, 0 }, new[] { false, true, false, false }, 4, 1);
        if (strip is null || strip[1] != 5f || strip[3] != 9f || (strip[2] != 5f && strip[2] != 9f) || flat is not null)
        {
            faults.Add($"the water filled {(strip is null ? "null" : string.Join(",", strip))} (want 5,5,5|9,9,9 from the land either side) and a flat valley {(flat is null ? "null" : "filled")} (want null)");
        }

        // The uniforms: on with heights, the sim's steep threshold, the look file's values.
        ValleyWatercolourLook look = ValleyWatercolourLook.Current;
        if (material is null
            || material.GetShaderParameter("hills_on").AsBool() != (raised > 0)
            || material.GetShaderParameter("hill_full_tilt_levels").AsSingle() != look.HillFullTiltLevels
            || material.GetShaderParameter("hill_lip_strength").AsSingle() != look.HillLipStrength
            || material.GetShaderParameter("hill_lip_width_share").AsSingle() != look.HillLipWidthShare)
        {
            faults.Add($"the uniforms: hills_on {material?.GetShaderParameter("hills_on")} (want {raised > 0}), or a look value not the file's");
        }

        ShowWatercolour(wasOn);
        return faults.Count == 0
            ? $"[widths] hill edges: ✅ {raised} of {land} land tiles raised (highest {highest} of 256), {steep} steep at the config's {config.SlopeSteepAt}; the field four texels a tile, the steep mask the sim's own class, water filled from the land; the look's values handed over; a flat valley draws none"
            : $"[widths] hill edges: ⛔ {string.Join("; ", faults)}";
    }

    /// <summary>
    /// ⭐ The probe's <c>green ground:</c> line (A68, <c>green-ground.md §6</c>): the field's crest on a made-up ridge, the
    /// hachures' bake on a made-up bank (strokes from the lip downhill, none on flat ground, the same bytes twice), the
    /// bake the valley was handed, the look's values on the layer, and the crowns' tint with the tick. The draw itself is
    /// the shader's, never headless (D340): the renders are its evidence.
    /// </summary>
    internal string TheGreenGroundHolds()
    {
        if (_world is not SimWorld world)
        {
            return "[widths] green ground: ⛔ no world";
        }

        var faults = new System.Collections.Generic.List<string>();
        try
        {
            // The crest on a made-up ridge: 61 tiles in a row, flat but for three tiles standing 48 levels up in the middle.
            var ridge = new float[61];
            ridge[29] = ridge[30] = ridge[31] = 48f;
            float[] crest = HillCrest(ridge, 61, 1);
            if (!(crest[30] > 0.5f) || !(crest[25] < 0f) || !(MathF.Abs(crest[2]) < 0.1f * crest[30]))
            {
                faults.Add($"the crest on a ridge: {crest[30]:0.###} on it, {crest[25]:0.###} at its foot, {crest[2]:0.###} far away (want over 0.5, under 0, near 0)");
            }

            // The hachures on a made-up bank: 12 × 6 tiles, level 0 west of tile 6 and 40 from it, the two tiles at the step
            // steep. The lip is on the uphill (east) side, so every stroke starts east of the step's middle and runs west.
            var bankHeights = new float[72];
            var bankSteep = new float[72];
            for (int i = 0; i < 72; i++)
            {
                bankHeights[i] = i % 12 < 6 ? 0f : 40f;
                bankSteep[i] = i % 12 is 5 or 6 ? 1f : 0f;
            }

            var dry = new bool[72];
            float[] bank = HillField(bankHeights, bankSteep, 12, 6);
            byte[] strokes = HillHachures(bank, dry, 12, 6);

            // The crest carried into the field's third channel: above zero on the bank's top side, below it at its foot.
            float CrestAtTile(float x) => bank[((((3 * 4) + 2) * 12 * 4) + (int)(x * 4)) * HillChannels + 2];
            if (!(CrestAtTile(7.5f) > 0f) || !(CrestAtTile(4.5f) < 0f))
            {
                faults.Add($"the field's crest on a bank: {CrestAtTile(7.5f):0.###} on its top side, {CrestAtTile(4.5f):0.###} at its foot (want above 0, below 0)");
            }
            int marked = 0, astray = 0, uphill = 0, starts = 0;
            float firstStart = float.MaxValue, lastStart = float.MinValue;
            for (int at = 0; at < strokes.Length / 4; at++)
            {
                if (strokes[(at * 4) + 3] == 0)
                {
                    continue;
                }

                // ⛔ Named from the spec (trap 17): 8 texels a tile, starts in quarter-texels from the texel's centre.
                int tx = at % (12 * 8);
                float texelTile = (tx + 0.5f) / 8f;
                float startTile = (tx + 0.5f + ((strokes[at * 4] - 128) * 0.25f)) / 8f;
                int turn = strokes[(at * 4) + 2];
                marked++;
                astray += texelTile is < 3.5f or > 9f ? 1 : 0;
                starts += startTile is < 6f or > 8.5f ? 1 : 0;
                firstStart = MathF.Min(firstStart, startTile);
                lastStart = MathF.Max(lastStart, startTile);
                uphill += turn is < 96 or > 160 ? 1 : 0;
            }

            // ⚠️ The lip is one straight line here, so the starts lie within the seeds' band of it (0.12 of the mask either
            // side, about half a tile in all): a wider spread is strokes seeded off the lip (red-check G12 scored zero
            // until this was asked).
            if (marked == 0 || astray > 0 || starts > 0 || uphill > 0 || lastStart - firstStart > 0.6f)
            {
                faults.Add($"the hachures on a bank: {marked} texels marked, {astray} off the bank, {starts} starting off the lip's side, {uphill} not running downhill, the starts spread over {lastStart - firstStart:0.##} tiles (want some, none of the next three, and under 0.6)");
            }

            if (!strokes.AsSpan().SequenceEqual(HillHachures(bank, dry, 12, 6)))
            {
                faults.Add("the hachures differ between two bakes of the same bank");
            }

            float[] plain = HillField(new float[72], new float[72], 12, 6);
            if (HillHachures(plain, dry, 12, 6).Any(b => b != 0))
            {
                faults.Add("hachures on flat ground");
            }

            // A bank that falls into water: the strokes stop at the water. ⚠️ The water reaches the step itself (tile 6),
            // where the mask is still steep: with it a tile further off no stroke came near it, and the guard scored zero
            // (red-check G5).
            var wet = new bool[72];
            for (int i = 0; i < 72; i++)
            {
                wet[i] = i % 12 < 6;
            }

            byte[] cut = HillHachures(bank, wet, 12, 6);
            int intoWater = 0;
            for (int at = 0; at < cut.Length / 4; at++)
            {
                if (cut[(at * 4) + 3] == 0)
                {
                    continue;
                }

                int tx = at % (12 * 8);
                float startTile = (tx + 0.5f + ((cut[at * 4] - 128) * 0.25f)) / 8f;
                float turn = cut[(at * 4) + 2] * MathF.Tau / 256f;
                float endTile = startTile + (MathF.Cos(turn) * cut[(at * 4) + 3] / 128f);
                intoWater += endTile < 5.85f ? 1 : 0;
            }

            if (intoWater > 0)
            {
                faults.Add($"{intoWater} texels name a stroke that runs into the water");
            }

            // The valley's own bake and the layer's values.
            SimConfig config = world.Config;
            bool wasOn = _watercolour;
            ShowWatercolour(true);
            _hillsOf = null;
            ShaderMaterial? material = _watercolourMaterial;
            if (material is not null)
            {
                HandTheHillsOver(material, config);
            }

            int steep = 0;
            for (int i = 0; i < world.Map.Tiles.Count; i++)
            {
                steep += world.Map.Tiles[i] != Terrain.Water && world.Map.SteepnessAtIndex(i) >= config.SlopeSteepAt ? 1 : 0;
            }

            using Image? image = _hachuresTexture?.GetImage(); // ⛔ `using` (A45)
            int drawn = 0;
            if (image is not null)
            {
                byte[] data = image.GetData();
                for (int at = 3; at < data.Length; at += 4)
                {
                    drawn += data[at] != 0 ? 1 : 0;
                }
            }

            if (_hillsTexture is not null && (image is null || image.GetWidth() != config.MapWidth * 8 || image.GetHeight() != config.MapHeight * 8
                || image.GetFormat() != Image.Format.Rgba8 || (steep > 20 && drawn == 0)))
            {
                faults.Add($"the valley's hachures are {image?.GetWidth()} × {image?.GetHeight()} {image?.GetFormat()} with {drawn} texels marked over {steep} steep tiles (want eight texels a tile, Rgba8, some marked)");
            }

            ValleyWatercolourLook look = ValleyWatercolourLook.Current;
            if (material is null
                || material.GetShaderParameter("green_on").AsBool() != look.GreenOn
                || material.GetShaderParameter("green_deep").AsVector3() != Srgb(look.GreenDeep)
                || material.GetShaderParameter("green_low").AsVector3() != Srgb(look.GreenLow)
                || material.GetShaderParameter("green_mid").AsVector3() != Srgb(look.GreenMid)
                || material.GetShaderParameter("green_high").AsVector3() != Srgb(look.GreenHigh)
                || material.GetShaderParameter("green_crest").AsVector3() != Srgb(look.GreenCrest)
                || material.GetShaderParameter("green_warm").AsVector3() != Srgb(look.GreenWarm)
                || material.GetShaderParameter("green_cool").AsVector3() != Srgb(look.GreenCool)
                || material.GetShaderParameter("green_spring").AsVector3() != Srgb(look.GreenSpring)
                || material.GetShaderParameter("green_summer").AsVector3() != Srgb(look.GreenSummer)
                || material.GetShaderParameter("green_autumn").AsVector3() != Srgb(look.GreenAutumn)
                || material.GetShaderParameter("green_winter").AsVector3() != Srgb(look.GreenWinter)
                || material.GetShaderParameter("green_levels").AsVector2() != look.GreenLevels
                || material.GetShaderParameter("green_flat_level").AsSingle() != look.GreenFlatLevel
                || material.GetShaderParameter("green_wander").AsSingle() != look.GreenWander
                || material.GetShaderParameter("green_crest_turn").AsVector2() != look.GreenCrestTurn
                || material.GetShaderParameter("green_hollow").AsSingle() != look.GreenHollow
                || material.GetShaderParameter("green_warm_turn").AsVector2() != look.GreenWarmTurn
                || material.GetShaderParameter("green_cool_turn").AsSingle() != look.GreenCoolTurn
                || material.GetShaderParameter("green_steep").AsSingle() != look.GreenSteep
                || material.GetShaderParameter("green_kinds_share").AsSingle() != look.GreenKindsShare
                || material.GetShaderParameter("green_grass_share").AsSingle() != look.GreenGrassShare
                || material.GetShaderParameter("green_wood").AsVector2() != look.GreenWood
                || material.GetShaderParameter("brush_strength").AsSingle() != look.BrushStrength
                || material.GetShaderParameter("brush_tiles").AsVector2() != look.BrushTiles
                || material.GetShaderParameter("tuft_strength").AsSingle() != look.TuftStrength
                || material.GetShaderParameter("tuft_cell_tiles").AsSingle() != look.TuftCellTiles
                || material.GetShaderParameter("tuft_length_tiles").AsVector2() != look.TuftLengthTiles
                || material.GetShaderParameter("tuft_share").AsSingle() != look.TuftShare
                || material.GetShaderParameter("tuft_width_px").AsSingle() != look.TuftWidthPx
                || material.GetShaderParameter("tuft_fade_px").AsSingle() != look.TuftFadePxPerTile
                || material.GetShaderParameter("hachure_texels_per_tile").AsSingle() != 8f
                || material.GetShaderParameter("hachure_strength").AsSingle() != look.HachureStrength
                || material.GetShaderParameter("hachure_width_share").AsSingle() != look.HachureWidthShare
                || material.GetShaderParameter("hachure_fade_px").AsSingle() != look.HachureFadePxPerTile)
            {
                faults.Add("a green ground value on the layer is not the look file's");
            }

            // The look draws something: the green on, a palette that climbs, the pen and the brush not at nothing; and no
            // green darker than the ground may go (`ground-textures.md §3`: L* 50, by the palette's own numbers).
            float Luma(Color c) => (0.2126f * c.R) + (0.7152f * c.G) + (0.0722f * c.B);
            if (!look.GreenOn || !(Luma(look.GreenDeep) < Luma(look.GreenMid)) || !(Luma(look.GreenMid) < Luma(look.GreenHigh))
                || !(Luma(look.GreenHigh) < Luma(look.GreenCrest)) || look.HachureStrength <= 0f || look.TuftStrength <= 0f
                || look.BrushStrength <= 0f || !(look.GreenDeep.G > look.GreenDeep.R && look.GreenCool.G > look.GreenCool.R)
                || Luma(look.GreenCool) < 0.45f)
            {
                faults.Add($"{ValleyWatercolourLook.Path} draws no green ground: on {look.GreenOn}, the palette not climbing from deep to crest, a pen or the brush at nothing, the deep or cool colour not a green, or the cool one too dark");
            }

            // The crowns' tint: the file's with the tick, none for the winter set, none without the tick.
            Color tinted = TreeTintFor(TreeSet.Summer), winter = TreeTintFor(TreeSet.Winter);
            ShowWatercolour(false);
            Color untinted = TreeTintFor(TreeSet.Summer);
            ShowWatercolour(wasOn);
            if (tinted != look.TreeTint || tinted == Colors.White || winter != Colors.White || untinted != Colors.White)
            {
                faults.Add($"the crowns' tint is {tinted} with the tick (want the file's {look.TreeTint}), {winter} for the winter set and {untinted} without the tick (want white twice)");
            }

            return faults.Count == 0
                ? $"[widths] green ground: ✅ the crest above zero on a ridge and below it at its foot; hachures from the lip downhill, none on flat ground or into water, the same twice; the valley's bake eight texels a tile, {drawn} texels marked over {steep} steep tiles; the look's values handed over; the crowns tinted with the tick, not in winter"
                : $"[widths] green ground: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            // Trap 33: a probe line that throws never reaches `done.` and leaves a headless Godot running.
            return $"[widths] green ground: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }

    internal string TheSitesShowThroughTheCanopy()
    {
        if (_world is not SimWorld world)
        {
            return "[widths] sites through the canopy: ⛔ no world";
        }

        var faults = new System.Collections.Generic.List<string>();
        bool wasOn = _watercolour, wasTrees = _treesShown;
        int sitesBefore = world.Workplaces.Count(w => w.Construction is not null);

        // A gatherer's hut marked on the first wooded tile out from the founding where one may go.
        GridPos founding = world.Map.FoundingSite;
        Workplace? marked = null;
        for (int r = 3; r < 30 && marked is null; r++)
        {
            for (int dx = -r; dx <= r && marked is null; dx++)
            {
                var tile = new GridPos(founding.X + dx, founding.Y + r);
                if (world.Map.TerrainAt(tile) == Terrain.Forest && world.CanBuildAt(BuildingKind.GathererHut, tile).Allowed
                    && world.Mark(BuildingKind.GathererHut, tile).Allowed)
                {
                    marked = world.Workplaces.LastOrDefault(w => w.Construction is not null);
                }
            }
        }

        if (marked is null)
        {
            return "[widths] sites through the canopy: ⛔ nowhere in the woods to mark a site";
        }

        ShowWatercolour(true);
        ShowTrees(true);
        var on = SitesThroughTheCanopy();
        // A free building waiting on its ground comes through as a site does (D726), so it is counted with them.
        int sites = world.Workplaces.Count(w => w.Construction is not null) + world.BuildingsWaiting.Count;

        // ⛔ Named from the spec, not read off SiteShapeOf (trap 17): the site's footprint 0.8 of its extent each way, centred
        // on its place, in the clearing's orange (D350) while its trees stand.
        Vector2 centre = ToScreen(marked.Position);
        float wantWide = marked.ExtentWidth * 0.8f * _pixelsPerTile, wantDeep = marked.ExtentHeight * 0.8f * _pixelsPerTile;
        bool found = false;
        foreach ((Vector2[] outline, Color edge) in on)
        {
            Vector2 middle = (outline[0] + outline[1] + outline[2] + outline[3]) / 4f;
            if (middle.DistanceTo(centre) > 0.5f)
            {
                continue;
            }

            found = true;
            float a = outline[0].DistanceTo(outline[1]), b = outline[1].DistanceTo(outline[2]);
            bool shaped = (Mathf.Abs(a - wantWide) < 0.5f && Mathf.Abs(b - wantDeep) < 0.5f) || (Mathf.Abs(a - wantDeep) < 0.5f && Mathf.Abs(b - wantWide) < 0.5f);
            if (!shaped || edge != HarvestEdge)
            {
                faults.Add($"the marked site comes through {a:F1} × {b:F1} px in {edge} (want {wantWide:F1} × {wantDeep:F1} in the clearing's orange {HarvestEdge})");
            }
        }

        if (!found || on.Count != sites)
        {
            faults.Add($"{on.Count} outlines through the canopy for {sites} sites, the marked one found {found} (want one a site, it among them)");
        }

        // No canopy, nothing to see through: the trees hidden, then the watercolour off.
        ShowTrees(false);
        int treesOff = SitesThroughTheCanopy().Count;
        ShowTrees(true);
        ShowWatercolour(false);
        int watercolourOff = SitesThroughTheCanopy().Count;
        if (treesOff != 0 || watercolourOff != 0)
        {
            faults.Add($"{treesOff} outlines with the trees hidden and {watercolourOff} with the watercolour off (want none)");
        }

        world.CancelConstruction(marked);
        ShowTrees(wasTrees);
        ShowWatercolour(wasOn);
        int sitesAfter = world.Workplaces.Count(w => w.Construction is not null);
        if (sitesAfter != sitesBefore)
        {
            faults.Add($"{sitesAfter} sites after taking the marked one back (want the {sitesBefore} there were)");
        }

        return faults.Count == 0
            ? $"[widths] sites through the canopy: ✅ a gatherer's hut marked in the woods comes through the canopy as its own {wantWide:F0} × {wantDeep:F0} px outline in the clearing's orange, one outline a site ({sites}); none with the trees hidden or the watercolour off; taken back after"
            : $"[widths] sites through the canopy: ⛔ {string.Join("; ", faults)}";
    }

    internal string TheRiverDrawnHolds()
    {
        var faults = new System.Collections.Generic.List<string>();
        if (_world is null)
        {
            return "[widths] river drawn: ⛔ no world to draw";
        }

        SimConfig config = _world.Config;
        int width = config.MapWidth;
        int height = config.MapHeight;
        float root2 = MathF.Sqrt(2f);
        string worst = "";

        // (a) The founding valley's table, against the tiles counted here.
        string course = MapGenerator.CourseOf(config, _world.Seed);
        bool[] water = TheWaterTiles();
        RiverTable? table = Built(water, width, height, course);
        if (table is null)
        {
            faults.Add($"the founding valley ({course}, {config.RiverWidthTiles} tiles wide) has no river table");
        }
        else
        {
            int rows = course is "we" ? width : course is "ns" ? height : width + height - 1;
            float spacing = course is "nwse" or "swne" ? root2 : 1f;
            var low = new float[rows];
            var high = new float[rows];
            var count = new int[rows];
            Array.Fill(low, float.MaxValue);
            Array.Fill(high, float.MinValue);
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    if (!water[(j * width) + i])
                    {
                        continue;
                    }

                    (int row, float across) = course switch
                    {
                        "we" => (i, j + 0.5f),
                        "ns" => (j, i + 0.5f),
                        "nwse" => (i + j, (i - j) / root2),
                        _ => (i - j + height - 1, (i + j + 1) / root2),
                    };
                    low[row] = Math.Min(low[row], across);
                    high[row] = Math.Max(high[row], across);
                    count[row]++;
                }
            }

            if (table.Rows != rows)
            {
                faults.Add($"the table has {table.Rows} rows for a {course} river on {width} × {height} (want {rows})");
            }

            float farthest = 0f;
            double rawHalves = 0, tableHalves = 0;
            int wet = 0;
            for (int r = 0; r < Math.Min(rows, table.Rows); r++)
            {
                if (count[r] == 0)
                {
                    continue;
                }

                wet++;

                // One run a row: the carve's promise, and what makes a table of rows enough.
                float run = high[r] - low[r] + spacing;
                if (MathF.Abs(run - (count[r] * spacing)) > 1e-3f)
                {
                    faults.Add($"row {r} holds {count[r]} water tiles over {run:F2} tiles: not one run");
                    break;
                }

                float stray = MathF.Max(low[r] - table.Centre[r], table.Centre[r] - high[r]);
                if (stray > farthest)
                {
                    farthest = stray;
                    worst = $"row {r}";
                }

                rawHalves += count[r] * spacing / 2f;
                tableHalves += table.HalfWidth[r];
            }

            // The smoothed middle may round a sharp bend (the founding valley's worst, measured A45: 1.11 tiles past its
            // row's water), but never by more than a tile and a half. It only steers the ripples: they keep off the bank by
            // the water's own weight.
            if (farthest > 1.5f)
            {
                faults.Add($"the smoothed middle leaves the water by {farthest:F2} tiles at {worst} (want within 1.5)");
            }

            if (wet == 0 || Math.Abs((tableHalves / wet) - (rawHalves / wet)) > 0.05 * (rawHalves / wet))
            {
                faults.Add($"the table's mean half-width {tableHalves / Math.Max(1, wet):F2} against the tiles' {rawHalves / Math.Max(1, wet):F2} (want within 5 %)");
            }

            for (int r = 1; r < table.Rows; r++)
            {
                if (table.Down[r] <= table.Down[r - 1] || table.Lean[r] <= 0.5f || table.Lean[r] > 1f)
                {
                    faults.Add($"row {r}: down {table.Down[r - 1]:F3} → {table.Down[r]:F3}, lean {table.Lean[r]:F3} (want growing, lean in (0.5, 1])");
                    break;
                }
            }

            worst = $"{course}, {wet} rows wet, the middle within {farthest:F2} tiles of its water";
        }

        // (b) Four made-up rivers, one a course: which way is downstream, named here — east, south, south-east, north-east.
        const int w = 40, h = 30;
        foreach ((string way, int di, int dj, float half) in new[] { ("we", 1, 0, 1.5f), ("ns", 0, 1, 1.5f), ("nwse", 1, 1, root2), ("swne", 1, -1, root2) })
        {
            var made = new bool[w * h];
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    made[(j * w) + i] = way switch
                    {
                        "we" => j is >= 14 and <= 16,
                        "ns" => i is >= 19 and <= 21,
                        "nwse" => i - j is >= 8 and < 12,
                        _ => i + j is >= 32 and < 36,
                    };
                }
            }

            RiverTable? straight = Built(made, w, h, way);
            if (straight is null)
            {
                faults.Add($"a made-up {way} river has no table");
                continue;
            }

            // A tile in the river near the middle, and the tile two steps downstream of it.
            (int ai, int aj) = way switch { "we" => (18, 15), "ns" => (20, 13), "nwse" => (24, 15), _ => (18, 15) };
            (int bi, int bj) = (ai + (2 * di), aj + (2 * dj));
            float rowA = new Vector2(ai + 0.5f, aj + 0.5f).Dot(straight.Along) + straight.Offset;
            float rowB = new Vector2(bi + 0.5f, bj + 0.5f).Dot(straight.Along) + straight.Offset;
            int ra = (int)MathF.Floor(rowA);
            int rb = (int)MathF.Floor(rowB);
            if (!made[(aj * w) + ai] || !made[(bj * w) + bi] || MathF.Abs(rowA - ra - 0.5f) > 1e-4f || ra < 0 || rb >= straight.Rows
                || straight.Down[rb] <= straight.Down[ra])
            {
                faults.Add($"a made-up {way} river: ({ai},{aj}) is row {rowA:F2}, two steps downstream ({bi},{bj}) row {rowB:F2} (want both in the river, a centre on a row's middle, down growing)");
                continue;
            }

            // A diagonal's rows hold alternate halves of its strip (x − y of one parity, then the other), so its raw middle
            // steps ±0.35 tiles row to row; smoothed over nine rows that leaves under 0.05.
            float expected = way switch { "we" => 15.5f, "ns" => 20.5f, "nwse" => 9.5f / root2, _ => 34.5f / root2 };
            float within = way is "we" or "ns" ? 1e-3f : 0.05f;
            int mid = straight.Rows / 2;
            if (MathF.Abs(straight.Centre[mid] - expected) > within || MathF.Abs(straight.HalfWidth[mid] - half) > 1e-3f
                || MathF.Abs(straight.Lean[mid] - 1f) > 1e-4f)
            {
                faults.Add($"a made-up {way} river's middle row: centre {straight.Centre[mid]:F3}, half {straight.HalfWidth[mid]:F3}, lean {straight.Lean[mid]:F4} (want {expected:F3}, {half:F3}, 1)");
            }
        }

        if (RiverTable.Of(new bool[w * h], w, h, "we") is not null)
        {
            faults.Add("a valley with no water has a river table");
        }

        // (c) The values reaching the layer, each part on, and the clock.
        bool wasOn = _watercolour;
        ShowWatercolour(true);
        ShaderMaterial material = _watercolourMaterial!;
        Vector2 topLeft = ToScreen(new Vector2(config.MapMinX - 0.5f, config.MapMinY - 0.5f));
        Vector2 bottomRight = ToScreen(new Vector2(config.MapMaxX + 0.5f, config.MapMaxY + 0.5f));
        PaintTheWatercolour(new Rect2(topLeft, bottomRight - topLeft));

        ValleyWatercolourLook look = ValleyWatercolourLook.Current;
        if (look.ChannelStrength <= 0f || look.EchoStrength <= 0f || look.RippleStrength <= 0f || look.RippleShare <= 0f
            || look.RippleDriftTilesPerSecond <= 0f)
        {
            faults.Add($"{ValleyWatercolourLook.Path} draws no channel ({look.ChannelStrength}), echoes ({look.EchoStrength}) or moving ripples ({look.RippleStrength}, {look.RippleShare} of cells, {look.RippleDriftTilesPerSecond} tiles/s)");
        }

        Near("channel_strength", look.ChannelStrength);
        Near("echo_strength", look.EchoStrength);
        Near("echo_width_share", look.EchoWidthShare);
        Near("echo_lifts", look.EchoLifts);
        Near("echo_dash_tiles", look.EchoDashTiles);
        Near("echo_fade_px", look.EchoFadePx);
        Near("ripple_strength", look.RippleStrength);
        Near("ripple_width_px", look.RippleWidthPx);
        Near("ripple_share", look.RippleShare);
        Near("ripple_bow_tiles", look.RippleBowTiles);
        Near("ripple_bank_tiles", look.RippleBankTiles);
        Near("ripple_bank_level", BankWeightAt(look.RippleBankTiles));
        Near("ripple_drift", look.RippleDriftTilesPerSecond);
        Near("ripple_fade_px", look.RippleFadePxPerTile);
        Same("ripple_cell_tiles", material.GetShaderParameter("ripple_cell_tiles").AsVector2() == look.RippleCellTiles);
        Same("ripple_length_tiles", material.GetShaderParameter("ripple_length_tiles").AsVector2() == look.RippleLengthTiles);
        Same("echo_tiles", material.GetShaderParameter("echo_tiles").AsVector2() == new Vector2(look.EchoNearTiles, look.EchoFarTiles));
        Same("river_on", material.GetShaderParameter("river_on").AsBool() == (table is not null));
        if (table is not null)
        {
            Near("river_rows", table.Rows);
        }

        // The echoes' contours: the weight a straight bank has at 0.15 and 0.32 tiles in, as measured for the spec (A45) —
        // 0.675 and 0.886, averaged along the bank. A change to the bake's kernel or to the copy of it moves these.
        Vector2 levels = material.GetShaderParameter("echo_levels").AsVector2();
        if (MathF.Abs(BankWeightAt(0.15f) - 0.675f) > 0.02f || MathF.Abs(BankWeightAt(0.32f) - 0.886f) > 0.02f
            || MathF.Abs(levels.X - BankWeightAt(look.EchoNearTiles)) > 1e-5f || MathF.Abs(levels.Y - BankWeightAt(look.EchoFarTiles)) > 1e-5f
            || levels.Y >= 0.99f)
        {
            faults.Add($"the echoes' weights: a bank at 0.15 and 0.32 tiles in weighs {BankWeightAt(0.15f):F3} and {BankWeightAt(0.32f):F3} (want 0.675 and 0.886), the layer has {levels} (want the file's depths' and under 0.99, where the weight still slopes)");
        }

        // The clock: the period a whole number of lives, the grid drifting RippleRepeatCells cells in it.
        float period = material.GetShaderParameter("flow_period_s").AsSingle();
        float life = material.GetShaderParameter("ripple_life_s").AsSingle();
        float drifted = period * look.RippleDriftTilesPerSecond / look.RippleCellTiles.X;
        float lives = period / life;
        if (MathF.Abs(drifted - 256f) > 1e-2f || MathF.Abs(lives - MathF.Round(lives)) > 1e-3f
            || MathF.Abs(life - look.RippleLifeSeconds) > 0.1f * look.RippleLifeSeconds)
        {
            faults.Add($"the clock's period {period:F2} s drifts {drifted:F3} cells (want 256) over {lives:F3} lives of {life:F3} s (want whole, near the file's {look.RippleLifeSeconds})");
        }

        // Paused it stands; the tick off it stands; running at 1× it takes the frame's real seconds; at 20× twenty times
        // them (A46: the river keeps the game's speed); at the period it wraps.
        double clockWas = _flowClock;
        bool pausedWas = Paused;
        bool movesWas = _waterMoves;
        double paceWas = _flowPace;
        _flowPace = 1.0;
        _flowClock = 10.0;
        Paused = true;
        TickTheFlowClock(1.0);
        bool stood = _flowClock == 10.0;
        Paused = false;
        _waterMoves = false;
        TickTheFlowClock(1.0);
        bool held = _flowClock == 10.0;
        _waterMoves = true;
        TickTheFlowClock(1.0);
        bool ran = Math.Abs(_flowClock - 11.0) < 1e-9 && Math.Abs(material.GetShaderParameter("flow_seconds").AsSingle() - 11f) < 1e-5f;
        _flowPace = 20.0;
        TickTheFlowClock(1.0);
        bool raced = Math.Abs(_flowClock - 31.0) < 1e-9;
        _flowPace = 1.0;
        _flowClock = _flowPeriod - 0.25;
        TickTheFlowClock(0.5);
        bool wrapped = Math.Abs(_flowClock - 0.25) < 1e-6;
        _flowClock = clockWas;
        Paused = pausedWas;
        _waterMoves = movesWas;
        _flowPace = paceWas;
        material.SetShaderParameter("flow_seconds", (float)_flowClock);
        if (!stood || !held || !ran || !raced || !wrapped)
        {
            faults.Add($"the clock: paused it stood {stood}, the tick off it held {held}, running it took a second {ran}, at 20× twenty {raced}, at its period it wrapped {wrapped} (want all true)");
        }

        ShowWatercolour(wasOn);

        return faults.Count == 0
            ? $"[widths] river drawn: ✅ the founding valley's table one run a row ({worst}); four made-up rivers run east, south, south-east and north-east, middles and widths as cut; no water, no table; the channel, the echoes at a bank's 0.675 and 0.886 and the ripples on, every value the file's; the clock still paused and with the tick off, the game's speed running (1× a second a second, 20× twenty), wrapping at {period:F0} s"
            : $"[widths] river drawn: ⛔ {string.Join("; ", faults)}";

        void Near(string uniform, float want)
        {
            float got = material.GetShaderParameter(uniform).AsSingle();
            if (Mathf.Abs(got - want) > 1e-5f)
            {
                faults.Add($"{uniform} is {got} (want {want})");
            }
        }

        void Same(string what, bool holds)
        {
            if (!holds)
            {
                faults.Add($"{what} is not the look file's");
            }
        }

        // ⚠️ A table that throws is a fault on this line, said with what threw — not an exception out of the probe, which
        // never reaches `done.` and leaves a headless Godot running (the red-check's first mutant hung it for 15 minutes).
        RiverTable? Built(bool[] tiles, int across, int down, string way)
        {
            try
            {
                return RiverTable.Of(tiles, across, down, way);
            }
            catch (Exception thrown) when (thrown is IndexOutOfRangeException or ArgumentException or InvalidOperationException)
            {
                faults.Add($"a {way} table on {across} × {down} threw {thrown.GetType().Name}: {thrown.Message}");
                return null;
            }
        }
    }
}
