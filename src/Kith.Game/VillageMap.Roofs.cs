using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <summary>The material a roof is painted in (`building-roofs.md §3`).</summary>
internal enum RoofMaterial
{
    Thatch,
    Shingle,
    Slate,
    Tile,
    Boards,
    Turf,
    StoneSlab,
}

/// <summary>What a building is drawn as from above: a roof, or a shape that says what it is (`building-roofs.md §2`).</summary>
internal enum RoofForm
{
    Roof,
    Market,
    Yard,
    Mill,
    Well,

    /// <summary>A parked handcart (A86, `building-yards.md §4.2`): drawn where its chip stood.</summary>
    Cart,

    /// <summary>The founders' cart (A87, `building-yards.md §3.4`): a wagon on its own ground, its load from its store.</summary>
    Wagon,

    /// <summary>A trade whose building is a store (the market, the barn, the trading post): the store paints it.</summary>
    ByItsStore,
}

/// <summary>A kind's look from above: its form, its material, its chimneys and extras, and how high it stands.</summary>
internal readonly record struct RoofLook(
    RoofForm Form, RoofMaterial Material, int Chimneys = 0, bool Turret = false, bool UpperStorey = false, float Lift = 1f, YardKind Yard = YardKind.None);

/// <summary>One thing a painted building is drawn with: a filled shape, a shape painted in a material, or a pen line.</summary>
internal enum RoofPieceKind
{
    Fill,
    Textured,
    Line,

    /// <summary>Separate segments, drawn as one call: the points in pairs.</summary>
    Lines,
}

/// <summary>A piece of a painted building, on screen, tagged with the part it is (the probe asks by part).</summary>
internal sealed record RoofPiece(
    RoofPieceKind Kind,
    string Part,
    Vector2[] Points,
    Color Colour,
    RoofMaterial Material = RoofMaterial.Shingle,
    Vector2[]? Uvs = null,
    float Width = 1f,
    bool Closed = false,
    bool Dashed = false,
    float Spacing = 0f);

/// <remarks>
/// ⭐⭐ THE BUILDINGS PAINTED, SEEN STRAIGHT DOWN (A85, `specs/building-roofs.md`; Joe's pick, *"A, roofs"*, of three mocks on
/// his own villages). From above a building is its roof: each face painted in its material and multiplied by the kind's
/// colour, lit by which way it faces — so a building turned to any angle is still lit from the upper left — a shadow to the
/// lower right, the eave inked, a step at the door. Only with the watercolour tick on; off, the rectangles, to the byte.
/// </remarks>
public partial class VillageMap
{
    /// <summary>How far the eaves stand out past the drawn walls, in tiles.</summary>
    internal const float RoofEaves = 0.04f;

    /// <summary>A face's light: 1 + this × (its outward direction on screen · toward the light).</summary>
    internal const float RoofLitStrength = 0.17f;

    /// <summary>The material textures' mean tone; the game multiplies by the kind's colour ÷ this.</summary>
    internal const float RoofTextureMean = 0.8f;

    /// <summary>A roof is a gable when its long side is at least this many times its short, a hip otherwise.</summary>
    internal const float RoofGableRatio = 1.45f;

    /// <summary>Toward the valley's light, on screen: the upper left.</summary>
    internal static readonly Vector2 RoofLightTo = new Vector2(-1f, -1f).Normalized();

    /// <summary>The shadows' wash, laid three times nested.</summary>
    private static readonly Color RoofShadow = new(0.16f, 0.2f, 0.27f, 0.11f);

    private static readonly Color RoofStepColour = new("#b5a68a");
    private static readonly Color ChimneyStone = new("#a39b8e");
    private static readonly Color ChimneyFlue = new("#3a3028");
    private static readonly Color TurretColour = new("#8a5a40");
    private static readonly Color YardEarth = new("#c9b48e");
    private static readonly Color MarketEarth = new("#d6bf96");
    private static readonly Color AwningCream = new("#f3e6c8");
    private static readonly Color YardPost = new("#5a4330");
    private static readonly Color SailCloth = new("#e8dcc0");
    private static readonly Color SailLattice = new("#6b5038");
    private static readonly Color SailSpar = new("#4a3524");
    private static readonly Color MillCap = new("#7a5a3c");
    private static readonly Color WellWater = new("#4f7488");

    /// <summary>The cottage's roof: weathered-oak shingle, its own colour (§3 — a slate roof sat ΔE 3.8 from the smithy).</summary>
    internal static readonly Color CottageRoofColour = new("#938d82");

    internal static readonly Dictionary<RoofMaterial, string> RoofTexturePaths = new()
    {
        [RoofMaterial.Thatch] = "res://textures/buildings/roof_thatch.png",
        [RoofMaterial.Shingle] = "res://textures/buildings/roof_shingle.png",
        [RoofMaterial.Slate] = "res://textures/buildings/roof_slate.png",
        [RoofMaterial.Tile] = "res://textures/buildings/roof_tile.png",
        [RoofMaterial.Boards] = "res://textures/buildings/roof_boards.png",
        [RoofMaterial.Turf] = "res://textures/buildings/roof_turf.png",
        [RoofMaterial.StoneSlab] = "res://textures/buildings/roof_stoneslab.png",
    };

    /// <summary>Loaded once, the first time a painted building is drawn; never rebuilt.</summary>
    private Dictionary<RoofMaterial, CanvasTexture>? _roofTextures;

    /// <summary>Whether the buildings are painted: the watercolour tick, nothing else.</summary>
    internal bool PaintsBuildings => _watercolour;

    // ------------------------------------------------------------------ the table (§3)

    /// <summary>A house, the library, the town hall or a well.</summary>
    internal static RoofLook RoofOf(BuildingKind kind) => kind switch
    {
        BuildingKind.Home => new(RoofForm.Roof, RoofMaterial.Thatch, Chimneys: 1, Yard: YardKind.Cabin),
        BuildingKind.Cottage => new(RoofForm.Roof, RoofMaterial.Shingle, Chimneys: 1, Yard: YardKind.Cottage),
        BuildingKind.Manor => new(RoofForm.Roof, RoofMaterial.Slate, Chimneys: 2, UpperStorey: true, Lift: 1.4f, Yard: YardKind.Manor),
        BuildingKind.Library => new(RoofForm.Roof, RoofMaterial.Slate, Yard: YardKind.Library),
        BuildingKind.TownHall => new(RoofForm.Roof, RoofMaterial.StoneSlab, Turret: true, Lift: 1.5f, Yard: YardKind.TownHall),
        BuildingKind.Well => new(RoofForm.Well, RoofMaterial.StoneSlab, Yard: YardKind.Well),
        _ => new(RoofForm.Roof, RoofMaterial.Shingle),
    };

    /// <summary>A store; the founders' cart is not a building and is not painted (null).</summary>
    internal static RoofLook? RoofOf(StoreKind kind) => kind switch
    {
        StoreKind.Granary => new(RoofForm.Roof, RoofMaterial.Thatch, Yard: YardKind.Granary),
        StoreKind.Warehouse => new(RoofForm.Roof, RoofMaterial.Shingle, Yard: YardKind.Warehouse),
        StoreKind.Market => new(RoofForm.Market, RoofMaterial.Shingle, Yard: YardKind.Market),
        StoreKind.Pile => new(RoofForm.Yard, RoofMaterial.Shingle),
        StoreKind.Barn => new(RoofForm.Roof, RoofMaterial.Tile, Lift: 1.3f, Yard: YardKind.Barn),
        StoreKind.TradingPost => new(RoofForm.Roof, RoofMaterial.Slate, Yard: YardKind.TradingPost),
        StoreKind.Cart => new(RoofForm.Wagon, RoofMaterial.Boards, Yard: YardKind.FoundersCart),
        _ => new(RoofForm.Roof, RoofMaterial.Shingle),
    };

    /// <summary>A workplace, by its trade.</summary>
    internal static RoofLook RoofOf(JobKind kind) => kind switch
    {
        JobKind.Forager => new(RoofForm.Roof, RoofMaterial.Turf, Yard: YardKind.Forager),
        JobKind.Forester => new(RoofForm.Roof, RoofMaterial.Turf, Yard: YardKind.Forester),
        JobKind.Fisher => new(RoofForm.Roof, RoofMaterial.Boards, Yard: YardKind.Fisher),
        JobKind.Smith => new(RoofForm.Roof, RoofMaterial.Slate, Chimneys: 1, Yard: YardKind.Smith),
        JobKind.Miner => new(RoofForm.Roof, RoofMaterial.Tile, Yard: YardKind.Mine),
        JobKind.Baker => new(RoofForm.Roof, RoofMaterial.Tile, Chimneys: 1, Yard: YardKind.Bakery),
        JobKind.Tailor => new(RoofForm.Roof, RoofMaterial.Tile, Chimneys: 1, Yard: YardKind.Tailor),
        JobKind.Farmer => new(RoofForm.Roof, RoofMaterial.Thatch, Chimneys: 1, Yard: YardKind.Farmhouse),
        JobKind.Carpenter => new(RoofForm.Roof, RoofMaterial.Shingle, Chimneys: 1, Yard: YardKind.Workshop),
        JobKind.Miller => new(RoofForm.Mill, RoofMaterial.Shingle, Yard: YardKind.Mill),

        // ⭐ The school's stove (D764) has a chimney now, so its winter fire shows (A139, Joe's call 2: "okay").
        JobKind.Teacher => new(RoofForm.Roof, RoofMaterial.Shingle, Chimneys: 1),
        JobKind.Woodcutter => new(RoofForm.Roof, RoofMaterial.Shingle, Yard: YardKind.Woodcutter),
        JobKind.Builder => new(RoofForm.Roof, RoofMaterial.Shingle, Yard: YardKind.Builder),
        JobKind.Hunter => new(RoofForm.Roof, RoofMaterial.Shingle, Yard: YardKind.Hunter),
        JobKind.Quarrier => new(RoofForm.Roof, RoofMaterial.Shingle, Yard: YardKind.Quarry),
        JobKind.Marketer or JobKind.Herdsman or JobKind.Merchant => new(RoofForm.ByItsStore, RoofMaterial.Shingle),
        _ => new(RoofForm.Roof, RoofMaterial.Shingle),
    };

    /// <summary>A house's roof colour: the cabin's is its own; the cottage's oak shingle and the manor's dark slate (§3).</summary>
    internal static Color DwellingRoofColourOf(BuildingKind kind) => kind switch
    {
        BuildingKind.Cottage => CottageRoofColour,
        BuildingKind.Manor => ManorRoofColour,
        _ => HomeColour,
    };

    // ------------------------------------------------------------------ the drawing

    /// <summary>
    /// ⭐ Draws a finished building painted, if the tick is on — <b>false means "draw it as before"</b>, and the caller does.
    /// <paramref name="wide"/> and <paramref name="deep"/> are the drawn size in tiles (already × 0.8, or the kind's share).
    /// </summary>
    private bool DrawPaintedBuilding(
        RoofLook? look, Vector2 centre, float wide, float deep, ushort facing, Color colour, Condition worn, Door? door, int seed, float alpha = 1f,
        YardState yard = default, Func<Vector2, Vector2[]>? homeYard = null, bool burning = false)
    {
        if (!PaintsBuildings || look is not RoofLook roof)
        {
            return false;
        }

        // In tile space, rounded so a pan's float noise never reads as a change: the cache keys on where it stands.
        Vector2 at = RoofRounded(((centre - (Size / 2f)) / _pixelsPerTile) + _centreTile);
        Vector2 doorAt = door is Door d ? RoofRounded(new Vector2(d.Tile.X, d.Tile.Y)) : at;
        ulong frame = Engine.GetProcessFrames();
        ForgetGoneRoofs(frame);
        RoofCache held = CachedRoof(new RoofSignature(roof, at, wide, deep, facing, colour, worn == Condition.Ruin, door, doorAt, seed, yard), homeYard);
        held.LastSeen = frame;
        if (roof.Form == RoofForm.Mill)
        {
            // ⭐ The sails turn while the mill is worked (A145): its seed is its workplace's id.
            TurnTheSails(held, _millsWorked.Contains(seed));
        }

        DrawRoofRuns(held, alpha);

        // ⭐ Smoke from its chimneys while its fire burns (A139, `indoors-and-smoke.md §2`).
        DrawTheSmoke(held, burning, alpha);
        return true;
    }

    // ------------------------------------------------------------------ kept between frames (CLAUDE.md: nothing derivable rebuilt per frame)

    /// <summary>Everything a painted building's look depends on — its pieces are laid again only when one of these changes.</summary>
    internal readonly record struct RoofSignature(
        RoofLook Look, Vector2 At, float Wide, float Deep, ushort Facing, Color Colour, bool FallenIn, Door? Door, Vector2 DoorAt, int Seed, YardState Yard = default);

    /// <summary>
    /// A run of pieces drawn as one mesh: filled, painted in one material, or pen lines — the lines laid for one zoom
    /// (<paramref name="LinePpt"/>), because their width is in pixels, and laid again only when the zoom changes.
    /// </summary>
    /// <remarks><paramref name="Turns"/>: a mill's sails (A145) — drawn turned about the hub, the rest of the roof still.</remarks>
    internal sealed record RoofRun(ArrayMesh Mesh, RoofMaterial? Material, List<RoofPiece>? Lines, int Triangles, float LinePpt = 0f, bool Turns = false);

    /// <summary>A building's pieces, laid in tile space once, as runs in their drawing order.</summary>
    internal sealed class RoofCache
    {
        public required RoofSignature Signature { get; init; }

        public required List<RoofRun> Runs { get; init; }

        public ulong LastSeen { get; set; }

        /// <summary>Its chimneys' flues, in the view's tiles (A139): where its smoke rises from.</summary>
        public Vector2[] Flues { get; init; } = Array.Empty<Vector2>();

        /// <summary>Its smoke, 0 cold to 1 lit, carried frame to frame (<see cref="SmokeOn"/>).</summary>
        public float Smoke { get; set; }

        /// <summary>A mill's hub, in the view's tiles (A145): what its sails turn about.</summary>
        public Vector2 Hub { get; init; }

        /// <summary>A mill's sails: how far round (radians) and how fast, 0 still to 1 at speed — carried frame to frame.</summary>
        public float SailsAt { get; set; }

        /// <summary><inheritdoc cref="SailsAt"/></summary>
        public float SailSpeed { get; set; }
    }

    /// <summary>The painted buildings' meshes, one entry a building, keyed by where it stands.</summary>
    private readonly Dictionary<Vector2, RoofCache> _roofCache = new();

    private int _roofCacheGeneration = -1;

    /// <summary>How many times a building's pieces were laid (the probe asks; never hashed).</summary>
    internal int RoofLayings { get; private set; }

    private static Vector2 RoofRounded(Vector2 v) => new(Mathf.Round(v.X * 1024f) / 1024f, Mathf.Round(v.Y * 1024f) / 1024f);

    /// <summary>
    /// ⭐ A building's runs — the cached ones while nothing it depends on has changed, laid again (and counted) when anything
    /// has. A pan or a zoom changes nothing here: the runs are in tile space and the frame's transform puts them on screen.
    /// </summary>
    internal RoofCache CachedRoof(RoofSignature signature, Func<Vector2, Vector2[]>? homeYard = null)
    {
        if (_roofCache.TryGetValue(signature.At, out RoofCache? held) && held.Signature == signature)
        {
            return held;
        }

        RoofLayings++;
        List<RoofPiece> pieces = RoofDrawing(
            signature.Look, signature.At, signature.Wide, signature.Deep, signature.Facing, signature.Colour, 1f,
            signature.Door, signature.DoorAt, signature.Seed, signature.FallenIn, signature.Yard, homeYard?.Invoke(signature.At));
        var laid = new RoofCache
        {
            Signature = signature, Runs = RoofRuns(pieces, _pixelsPerTile), Flues = FluesOf(pieces), Smoke = held?.Smoke ?? 0f,
            Hub = HubOf(pieces), SailsAt = held?.SailsAt ?? 0f, SailSpeed = held?.SailSpeed ?? 0f,
        };
        _roofCache[signature.At] = laid;
        KeepTheStep(signature, pieces);
        return laid;
    }

    /// <summary>
    /// The pieces as runs, in order: consecutive fills one mesh, consecutive faces of one material one mesh, consecutive pen
    /// lines one mesh laid for this zoom (<see cref="RoofLineRun"/>). Each polygon is fanned — every piece is convex. A face's
    /// vertex colour is its piece's × the textures' mean, and is drawn × 1 ÷ the mean, so nothing clamps.
    /// </summary>
    internal static List<RoofRun> RoofRuns(List<RoofPiece> pieces, float ppt)
    {
        var runs = new List<RoofRun>();
        int i = 0;
        while (i < pieces.Count)
        {
            RoofPiece first = pieces[i];
            if (first.Kind != RoofPieceKind.Textured)
            {
                // ⭐ A86: fills and pen lines in one mesh, in their order — a yard alternates them thing by thing, and a run
                // each was a draw call each (A85's cost). Laid for the zoom when it holds a line.
                // ⭐ A mill's sails a run of their own (A145), so they can turn while the body over them stays still.
                bool turns = TurnsWithTheSails(first);
                var mixed = new List<RoofPiece>();
                while (i < pieces.Count && pieces[i].Kind != RoofPieceKind.Textured && TurnsWithTheSails(pieces[i]) == turns)
                {
                    mixed.Add(pieces[i++]);
                }

                runs.Add(RoofLineRun(mixed, ppt) with { Turns = turns });
                continue;
            }

            bool textured = first.Kind == RoofPieceKind.Textured;
            var vertices = new List<Vector2>();
            var colours = new List<Color>();
            var uvs = new List<Vector2>();
            while (i < pieces.Count && pieces[i].Kind == first.Kind && (!textured || pieces[i].Material == first.Material))
            {
                RoofPiece piece = pieces[i++];
                Color colour = textured
                    ? new Color(piece.Colour.R * RoofTextureMean, piece.Colour.G * RoofTextureMean, piece.Colour.B * RoofTextureMean, piece.Colour.A)
                    : piece.Colour;
                for (int k = 1; k + 1 < piece.Points.Length; k++)
                {
                    foreach (int corner in new[] { 0, k, k + 1 })
                    {
                        vertices.Add(piece.Points[corner]);
                        colours.Add(colour);
                        if (textured)
                        {
                            uvs.Add(piece.Uvs![corner]);
                        }
                    }
                }
            }

            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = colours.ToArray();
            if (textured)
            {
                arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
            }

            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            runs.Add(new RoofRun(mesh, textured ? first.Material : null, null, vertices.Count / 3));
        }

        return runs;
    }

    /// <summary>
    /// ⭐ Pen lines as one mesh, in tile space, for one zoom (A85's cost: a draw call a line, every frame, was +2.7 ms of CPU at
    /// 4K). Each segment a core <c>width − 1</c> pixels wide with a pixel of feather each side fading to nothing — what an
    /// antialiased line is — its ends carried half the width on so corners close; a dashed line (a ruin's) cut into dashes of
    /// <c>max(3 px, 0.15 tile)</c>, as <c>DrawDashedLine</c> drew them.
    /// </summary>
    internal static RoofRun RoofLineRun(List<RoofPiece> lines, float ppt)
    {
        var vertices = new List<Vector2>();
        var colours = new List<Color>();

        void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ab, Color cd)
        {
            vertices.AddRange(new[] { a, b, c, a, c, d });
            colours.AddRange(new[] { ab, ab, cd, ab, cd, cd });
        }

        void Segment(Vector2 a, Vector2 b, float widthPx, Color colour)
        {
            Vector2 way = b - a;
            float length = way.Length();
            if (length < 1e-6f)
            {
                return;
            }

            Vector2 t = way / length;
            var n = new Vector2(-t.Y, t.X);
            float core = Mathf.Max(0f, (widthPx - 1f) / 2f) / ppt;
            float feather = 1f / ppt;
            a -= t * core;
            b += t * core;
            Color faded = colour with { A = 0f };
            if (core > 0f)
            {
                Quad(a + (n * core), b + (n * core), b - (n * core), a - (n * core), colour, colour);
            }

            Quad(a + (n * core), b + (n * core), b + (n * (core + feather)), a + (n * (core + feather)), colour, faded);
            Quad(a - (n * core), b - (n * core), b - (n * (core + feather)), a - (n * (core + feather)), colour, faded);
        }

        bool anyLine = false;
        foreach (RoofPiece line in lines)
        {
            Vector2[] p = line.Points;
            if (line.Kind == RoofPieceKind.Fill)
            {
                for (int k = 1; k + 1 < p.Length; k++)
                {
                    vertices.AddRange(new[] { p[0], p[k], p[k + 1] });
                    colours.AddRange(new[] { line.Colour, line.Colour, line.Colour });
                }

                continue;
            }

            anyLine = true;
            var segments = new List<(Vector2, Vector2)>();
            if (line.Kind == RoofPieceKind.Lines)
            {
                // ⭐ The pen's texture thins with the zoom (A86 §5): never closer than PenMinPixels on screen.
                int stride = line.Spacing > 0f && line.Spacing * ppt < PenMinPixels ? Mathf.CeilToInt(PenMinPixels / (line.Spacing * ppt)) : 1;
                for (int k = 0; k + 1 < p.Length; k += 2)
                {
                    if ((k / 2) % stride == 0)
                    {
                        segments.Add((p[k], p[k + 1]));
                    }
                }
            }
            else
            {
                for (int k = 0; k < (line.Closed ? p.Length : p.Length - 1); k++)
                {
                    segments.Add((p[k], p[(k + 1) % p.Length]));
                }
            }

            float dash = Mathf.Max(3f / ppt, 0.15f);
            foreach ((Vector2 a, Vector2 b) in segments)
            {
                if (!line.Dashed)
                {
                    Segment(a, b, line.Width, line.Colour);
                    continue;
                }

                float length = a.DistanceTo(b);
                for (float from = 0f; from < length; from += 2f * dash)
                {
                    Segment(a.Lerp(b, from / length), a.Lerp(b, Mathf.Min(length, from + dash) / length), line.Width, line.Colour);
                }
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colours.ToArray();
        var mesh = new ArrayMesh();
        if (vertices.Count > 0)
        {
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        }

        return new RoofRun(mesh, null, anyLine ? lines : null, vertices.Count / 3, ppt);
    }

    /// <summary>Pure: a run's pen lines laid again for a new zoom — still turning if it turned (A145: a mill's sails).</summary>
    internal static RoofRun RelaidForTheZoom(RoofRun run, List<RoofPiece> lines, float ppt) => RoofLineRun(lines, ppt) with { Turns = run.Turns };

    /// <summary>Draw a building's runs, each mesh through the frame's one transform; its lines laid again if the zoom has moved.</summary>
    private void DrawRoofRuns(RoofCache roof, float alpha)
    {
        Transform2D toScreen = TileToScreen();
        Rid canvas = GetCanvasItem();
        for (int i = 0; i < roof.Runs.Count; i++)
        {
            RoofRun run = roof.Runs[i];
            if (run.Lines is { } lines && run.LinePpt != _pixelsPerTile)
            {
                run = roof.Runs[i] = RelaidForTheZoom(run, lines, _pixelsPerTile);
            }

            if (run.Mesh.GetSurfaceCount() == 0)
            {
                continue;
            }

            Transform2D place = run.Turns ? toScreen * SailsTurned(roof.Hub, roof.SailsAt) : toScreen;

            if (run.Material is RoofMaterial material)
            {
                CanvasTexture? painting = RoofTextureOf(material);
                float lift = 1f / RoofTextureMean;
                RenderingServer.CanvasItemAddMesh(canvas, run.Mesh.GetRid(), place, new Color(lift, lift, lift, alpha), painting?.GetRid() ?? default);
            }
            else
            {
                RenderingServer.CanvasItemAddMesh(canvas, run.Mesh.GetRid(), place, new Color(1f, 1f, 1f, alpha));
            }
        }
    }

    /// <summary>Forget the buildings that were not drawn last frame, when the buildings have changed (a counter, CLAUDE.md).</summary>
    private void ForgetGoneRoofs(ulong frame)
    {
        if (_world is not SimWorld world || world.BuildingGeneration == _roofCacheGeneration)
        {
            return;
        }

        _roofCacheGeneration = world.BuildingGeneration;
        foreach (Vector2 at in _roofCache.Where(e => e.Value.LastSeen + 2 < frame).Select(e => e.Key).ToList())
        {
            // Its door's step goes with it (A139): a door the village no longer has is never asked for.
            if (_roofCache[at].Signature.Door is Door door)
            {
                _paintedSteps.Remove(door.Tile);
            }

            _roofCache.Remove(at);
        }
    }

    private CanvasTexture? RoofTextureOf(RoofMaterial material)
    {
        if (_roofTextures is null)
        {
            _roofTextures = new Dictionary<RoofMaterial, CanvasTexture>();
            foreach ((RoofMaterial m, string path) in RoofTexturePaths)
            {
                Texture2D? painting = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
                if (painting is null)
                {
                    GD.PushError($"[Kith] roof material {m} missing at {path}: that roof draws flat");
                    continue;
                }

                _roofTextures[m] = new CanvasTexture { DiffuseTexture = painting, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
            }
        }

        return _roofTextures.TryGetValue(material, out CanvasTexture? held) ? held : null;
    }

    /// <summary>
    /// ⭐⭐ The pieces one building is drawn with, on screen, in order — a pure function, so the map and the probe read the
    /// same list (`building-roofs.md §5`). <paramref name="colour"/> is the kind's colour, weathered.
    /// </summary>
    internal static List<RoofPiece> RoofDrawing(
        RoofLook look, Vector2 centre, float wide, float deep, ushort facing, Color colour, float ppt, Door? door, Vector2 doorAt, int seed, bool fallenIn,
        YardState yard = default, Vector2[]? homeTiles = null)
    {
        var pieces = new List<RoofPiece>();
        if (look.Form == RoofForm.ByItsStore)
        {
            return pieces;
        }

        var frame = new RoofFrame(centre, facing, ppt);
        if (look.Form == RoofForm.Cart)
        {
            var kit = new YardKit(pieces, frame);
            kit.Shade("cart shadow", Box(Vector2.Zero, 0.34f, 0.3f, 0f), 0.3f);
            Handcart(kit, "cart", Vector2.Zero, 0f);
            return pieces;
        }

        float hw = wide / 2f;
        float hd = deep / 2f;
        (Vector2 Out, Vector2 Middle) doorIn = door is Door dd ? DoorInFrame(frame, wide, deep, facing, dd, doorAt, ppt) : (new Vector2(0, 1), new Vector2(0, hd));
        YardLayout layout = LayoutOf(look.Yard, hw, hd, doorIn.Out, doorIn.Middle, seed);
        bool home = look.Yard is YardKind.Cabin or YardKind.Cottage or YardKind.Manor;
        bool yarded = look.Yard != YardKind.None && !home;
        switch (look.Form)
        {
            case RoofForm.Market:
                Market(pieces, frame, hw, hd, colour, fallenIn);
                YardUnder(pieces, frame, look.Yard, layout, yard, seed);
                break;
            case RoofForm.Yard:
                Yard(pieces, frame, hw, hd, colour, fallenIn);
                PilesOnTheYard(new YardKit(pieces, frame), hw, hd, yard.Piles, seed);
                break;
            case RoofForm.Mill:
                YardUnder(pieces, frame, look.Yard, layout, yard, seed);
                Mill(pieces, frame, hw, hd, colour, fallenIn);
                break;
            case RoofForm.Well:
                YardUnder(pieces, frame, look.Yard, layout, yard, seed);
                Well(pieces, frame, hw, hd, colour, fallenIn);
                break;
            case RoofForm.Wagon:
                Wagon(pieces, frame, yard, seed);
                break;
            default:
                // ⭐ A86: the yard first (`building-yards.md §3`), then the roof on its own part of the ground.
                if (yarded)
                {
                    YardUnder(pieces, frame, look.Yard, layout, yard, seed);
                }
                else if (home && homeTiles is not null)
                {
                    HomeYard(pieces, frame, TierOf(look.Yard), homeTiles, doorIn.Out, hw, hd, yard, seed);
                }

                Vector2 roofAt = yarded ? layout.RoofAt : Vector2.Zero;
                float ew = (yarded ? layout.RoofHw : hw) + RoofEaves;
                float ed = (yarded ? layout.RoofHd : hd) + RoofEaves;
                Shadow(pieces, frame, ew, ed, roofAt, look.Lift);
                if (door is Door d)
                {
                    Step(pieces, frame, wide, deep, facing, d, doorAt, ppt, roofAt, ew - RoofEaves, ed - RoofEaves);
                }

                bool mirrored = (seed & 1) == 1;
                Color wash = Washed(colour);
                RoofFaces(pieces, frame, ew, ed, roofAt, wash, look.Material, mirrored, fallenIn, "face", seed);
                if (look.UpperStorey)
                {
                    Shadow(pieces, frame, ew * 0.55f, ed * 0.62f, roofAt, 0.5f);
                    RoofFaces(pieces, frame, ew * 0.55f, ed * 0.62f, roofAt, wash.Lightened(0.12f), look.Material, !mirrored, fallenIn, "storey", seed + 1);
                }

                bool alongX = ew >= ed;
                float ridge = alongX ? ew : ed;
                if (look.Chimneys == 1)
                {
                    Chimney(pieces, frame, roofAt + (alongX ? new Vector2(0.3f * ridge, 0f) : new Vector2(0f, 0.3f * ridge)));
                }
                else if (look.Chimneys >= 2)
                {
                    foreach (float s in new[] { -0.62f, 0.62f })
                    {
                        Chimney(pieces, frame, roofAt + (alongX ? new Vector2(s * ridge, 0f) : new Vector2(0f, s * ridge)));
                    }
                }

                if (look.Turret)
                {
                    const float half = 0.23f;
                    Shadow(pieces, frame, half, half, roofAt, 1.2f);
                    RoofFaces(pieces, frame, half, half, roofAt, TurretColour, RoofMaterial.Tile, false, false, "turret", seed + 2);
                }

                if (yarded)
                {
                    YardOver(pieces, frame, look.Yard, layout);
                }

                break;
        }

        return pieces;
    }

    /// <summary>A building's own turned frame: local tiles (x along its width, y along its depth) to screen.</summary>
    private readonly struct RoofFrame
    {
        private readonly Vector2 _centre;
        private readonly float _cos;
        private readonly float _sin;
        private readonly float _ppt;

        public RoofFrame(Vector2 centre, ushort facing, float ppt)
        {
            float radians = facing * Mathf.Tau / 65536f;
            _centre = centre;
            _cos = Mathf.Cos(radians);
            _sin = Mathf.Sin(radians);
            _ppt = ppt;
        }

        public Vector2 At(Vector2 local) =>
            _centre + (new Vector2((local.X * _cos) - (local.Y * _sin), (local.X * _sin) + (local.Y * _cos)) * _ppt);

        public Vector2 Turn(Vector2 local) => new((local.X * _cos) - (local.Y * _sin), (local.X * _sin) + (local.Y * _cos));

        /// <summary>A screen offset in tiles, back into this frame.</summary>
        public Vector2 Local(Vector2 screenTiles) =>
            new((screenTiles.X * _cos) + (screenTiles.Y * _sin), (-screenTiles.X * _sin) + (screenTiles.Y * _cos));

        public Vector2[] Box(Vector2 at, float hw, float hd) =>
            new[] { At(at + new Vector2(-hw, -hd)), At(at + new Vector2(hw, -hd)), At(at + new Vector2(hw, hd)), At(at + new Vector2(-hw, hd)) };

        public Vector2[] Circle(Vector2 at, float r, int n = 24)
        {
            var points = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.Tau / n;
                points[i] = At(at + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r));
            }

            return points;
        }
    }

    /// <summary>A face's light: 1 + strength × (its outward direction, turned onto the screen, · toward the light).</summary>
    internal static float FaceLight(Vector2 screenOutward) => 1f + (RoofLitStrength * screenOutward.Normalized().Dot(RoofLightTo));

    private static void Shadow(List<RoofPiece> pieces, RoofFrame frame, float hw, float hd, Vector2 at, float lift)
    {
        Vector2 off = frame.Local(new Vector2(0.13f, 0.17f) * lift);
        foreach (float grow in new[] { 0.05f, 0f, -0.05f })
        {
            pieces.Add(new RoofPiece(RoofPieceKind.Fill, "shadow", frame.Box(at + off, hw + grow, hd + grow), RoofShadow));
        }

        // ⭐ A86 §5: the pen hatches the cast shadow, as the concept sheets do.
        Vector2 c = at + off;
        HatchInto(pieces, frame, new[] { c + new Vector2(-hw, -hd), c + new Vector2(hw, -hd), c + new Vector2(hw, hd), c + new Vector2(-hw, hd) }, "shadow", 1f);
    }

    private static void Step(
        List<RoofPiece> pieces, RoofFrame frame, float wide, float deep, ushort facing, Door door, Vector2 doorAt, float ppt, Vector2 roofAt, float roofHw, float roofHd)
    {
        // The notch the rectangles drew is on the door's own wall (`DoorQuadAt`); the step is it, pushed out past the eave —
        // of the roof's own wall, slid along it no further than the roof reaches (A86: the roof may stand on part of the ground).
        (Vector2 outward, Vector2 middle) = DoorInFrame(frame, wide, deep, facing, door, doorAt, ppt);
        var along = new Vector2(-outward.Y, outward.X);
        float alongHalf = Mathf.Abs(along.X) > 0.5f ? roofHw : roofHd;
        float outHalf = Mathf.Abs(outward.X) > 0.5f ? roofHw : roofHd;
        float slide = Mathf.Clamp((middle - roofAt).Dot(along), -Mathf.Max(0f, alongHalf - 0.15f), Mathf.Max(0f, alongHalf - 0.15f));
        Vector2 stepAt = roofAt + (along * slide) + (outward * (outHalf + RoofEaves + 0.06f));
        Vector2 half = outward.X == 0 ? new Vector2(0.15f, 0.06f) : new Vector2(0.06f, 0.15f);
        Vector2[] step = frame.Box(stepAt, half.X, half.Y);
        Color stone = door.Shut ? RoofStepColour with { A = 0.3f } : RoofStepColour;
        pieces.Add(new RoofPiece(RoofPieceKind.Fill, "step", step, stone));
        pieces.Add(new RoofPiece(RoofPieceKind.Line, "step", step, Ink with { A = door.Shut ? 0.3f : 0.8f }, Width: 1f, Closed: true));
    }

    /// <summary>
    /// The roof's faces over the eaves' rectangle — a gable along the long side, or a hip (the nearest-eave partition) —
    /// each painted in its material, lit by its facing; then the ridge, the hips and the inked eave.
    /// </summary>
    private static void RoofFaces(
        List<RoofPiece> pieces, RoofFrame frame, float hw, float hd, Vector2 at, Color colour, RoofMaterial material, bool mirrored, bool fallenIn, string part, int seed = 0)
    {
        var pen = new List<RoofPiece>();
        bool alongX = hw >= hd;
        float longHalf = alongX ? hw : hd;
        float shortHalf = alongX ? hd : hw;
        bool gable = longHalf >= RoofGableRatio * shortHalf;

        // Faces in a frame where the ridge runs along x; swap back at the end when it runs along y.
        Vector2 L(float x, float y) => alongX ? at + new Vector2(x, y) : at + new Vector2(y, x);
        Vector2 Out(Vector2 v) => alongX ? v : new Vector2(v.Y, v.X);
        float flip = mirrored ? -1f : 1f;

        void Face(Vector2[] corners, Vector2[] eaveAlong, Vector2 outward, float height)
        {
            // eaveAlong[i] = (t, e): along the eave in tiles, and up from it.
            var points = new Vector2[corners.Length];
            var uvs = new Vector2[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                points[i] = frame.At(corners[i]);
                uvs[i] = new Vector2(((eaveAlong[i].X * flip) + 1.5f) / 3f, eaveAlong[i].Y);
            }

            float light = FaceLight(frame.Turn(Out(outward)));
            Color lit = new(colour.R * light / RoofTextureMean, colour.G * light / RoofTextureMean, colour.B * light / RoofTextureMean, colour.A);
            pieces.Add(new RoofPiece(RoofPieceKind.Textured, part, points, lit, material, uvs));

            // ⭐ A86 §5: the pen draws the material up the slope from the eave (corners 0 → 1), and hatches a face in shade.
            PenTexture(pen, frame, part, material, corners, corners[0], corners[1], -Out(outward), height, seed + (pen.Count * 7));
            HatchInto(pen, frame, corners, part, (1f - light) / RoofLitStrength);
        }

        float r = gable ? longHalf : longHalf - shortHalf;
        if (gable)
        {
            Face(new[] { L(-longHalf, -shortHalf), L(longHalf, -shortHalf), L(longHalf, 0), L(-longHalf, 0) },
                new[] { new Vector2(-longHalf, 0), new Vector2(longHalf, 0), new Vector2(longHalf, shortHalf), new Vector2(-longHalf, shortHalf) },
                new Vector2(0, -1), shortHalf);
            Face(new[] { L(longHalf, shortHalf), L(-longHalf, shortHalf), L(-longHalf, 0), L(longHalf, 0) },
                new[] { new Vector2(-longHalf, 0), new Vector2(longHalf, 0), new Vector2(longHalf, shortHalf), new Vector2(-longHalf, shortHalf) },
                new Vector2(0, 1), shortHalf);
        }
        else
        {
            Face(new[] { L(-longHalf, -shortHalf), L(longHalf, -shortHalf), L(r, 0), L(-r, 0) },
                new[] { new Vector2(-longHalf, 0), new Vector2(longHalf, 0), new Vector2(r, shortHalf), new Vector2(-r, shortHalf) },
                new Vector2(0, -1), shortHalf);
            Face(new[] { L(longHalf, shortHalf), L(-longHalf, shortHalf), L(-r, 0), L(r, 0) },
                new[] { new Vector2(-longHalf, 0), new Vector2(longHalf, 0), new Vector2(r, shortHalf), new Vector2(-r, shortHalf) },
                new Vector2(0, 1), shortHalf);
            Face(new[] { L(longHalf, -shortHalf), L(longHalf, shortHalf), L(r, 0) },
                new[] { new Vector2(-shortHalf, 0), new Vector2(shortHalf, 0), new Vector2(0, shortHalf) },
                new Vector2(1, 0), longHalf - r);
            Face(new[] { L(-longHalf, shortHalf), L(-longHalf, -shortHalf), L(-r, 0) },
                new[] { new Vector2(-shortHalf, 0), new Vector2(shortHalf, 0), new Vector2(0, shortHalf) },
                new Vector2(-1, 0), longHalf - r);
        }

        // The ridge: a faint darker band and a fine pen; the hips a fine pen from each corner.
        float band = material == RoofMaterial.Thatch ? 0.05f : 0.03f;
        pieces.Add(new RoofPiece(RoofPieceKind.Fill, part + " ridge", new[] { frame.At(L(-r, -band)), frame.At(L(r, -band)), frame.At(L(r, band)), frame.At(L(-r, band)) },
            Ink with { A = material == RoofMaterial.Thatch ? 0.16f : 0.1f }));
        pieces.AddRange(pen);
        if (r > 0.001f)
        {
            pieces.Add(new RoofPiece(RoofPieceKind.Line, part + " ridge", new[] { frame.At(L(-r, 0)), frame.At(L(r, 0)) }, Ink with { A = 0.55f }, Width: 1f));
        }

        if (!gable)
        {
            var hips = new List<Vector2>();
            foreach ((float sx, float sy) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
            {
                hips.Add(frame.At(L(sx * longHalf, sy * shortHalf)));
                hips.Add(frame.At(L(sx * r, 0)));
            }

            pieces.Add(new RoofPiece(RoofPieceKind.Lines, part + " hip", hips.ToArray(), Ink with { A = 0.45f }, Width: 1f));
        }

        pieces.Add(new RoofPiece(RoofPieceKind.Line, part + " eave", frame.Box(at, hw, hd), Ink, Width: 2f, Closed: true, Dashed: fallenIn));
    }

    /// <summary>The flues among a building's pieces — the chimney's smallest square, its dark mouth (A139).</summary>
    internal static Vector2[] FluesOf(List<RoofPiece> pieces) =>
        pieces.Where(p => p.Kind == RoofPieceKind.Fill && p.Part == "chimney" && p.Colour == ChimneyFlue)
            .Select(p => CentreOf(p.Points)).ToArray();

    private static void Chimney(List<RoofPiece> pieces, RoofFrame frame, Vector2 at)
    {
        const float half = 0.09f;
        pieces.Add(new RoofPiece(RoofPieceKind.Fill, "chimney", frame.Box(at + frame.Local(new Vector2(0.1f, 0.12f)), half, half), RoofShadow with { A = 0.3f }));
        Vector2[] stack = frame.Box(at, half, half);
        pieces.Add(new RoofPiece(RoofPieceKind.Fill, "chimney", stack, ChimneyStone));
        pieces.Add(new RoofPiece(RoofPieceKind.Fill, "chimney", frame.Box(at, half * 0.45f, half * 0.45f), ChimneyFlue));
        pieces.Add(new RoofPiece(RoofPieceKind.Line, "chimney", stack, Ink, Width: 1f, Closed: true));
    }

    private static void Market(List<RoofPiece> pieces, RoofFrame frame, float hw, float hd, Color colour, bool fallenIn)
    {
        Vector2[] ground = frame.Box(Vector2.Zero, hw, hd);
        pieces.Add(new RoofPiece(RoofPieceKind.Fill, "ground", ground, MarketEarth.Lerp(colour, 0.4f) with { A = colour.A * 0.9f }));
        pieces.Add(new RoofPiece(RoofPieceKind.Line, "ground", ground, Ink with { A = 0.55f }, Width: 1.5f, Closed: true, Dashed: fallenIn));

        // Three stalls, placed as the mock placed them on a 2 × 2 and scaled to the ground.
        float sx = hw / 0.8f;
        float sy = hd / 0.8f;
        Color cream = colour.Lerp(AwningCream, 0.45f);
        foreach ((float cu, float cv, float w, float h) in new[] { (-0.33f, -0.33f, 0.31f, 0.24f), (0.36f, -0.28f, 0.27f, 0.27f), (-0.02f, 0.38f, 0.5f, 0.22f) })
        {
            var at = new Vector2(cu * sx, cv * sy);
            float aw = w * sx;
            float ah = h * sy;
            Shadow(pieces, frame, aw, ah, at, 0.7f);
            foreach (float side in new[] { -1f, 1f })
            {
                float light = FaceLight(frame.Turn(new Vector2(0, side)));
                Vector2 Corner(float x, float y) => frame.At(at + new Vector2(x, y));
                pieces.Add(new RoofPiece(RoofPieceKind.Fill, "stall", new[] { Corner(-aw, side * ah), Corner(aw, side * ah), Corner(aw, 0), Corner(-aw, 0) },
                    (cream * light) with { A = 1f }));

                // The stripes, the kind's own colour, every 0.13 tile along the awning.
                for (float x = -aw; x < aw; x += 0.13f)
                {
                    float to = Mathf.Min(aw, x + (0.13f * 0.62f));
                    pieces.Add(new RoofPiece(RoofPieceKind.Fill, "stall stripe", new[] { Corner(x, side * ah), Corner(to, side * ah), Corner(to, 0), Corner(x, 0) },
                        (colour * light) with { A = 1f }));
                }
            }

            pieces.Add(new RoofPiece(RoofPieceKind.Line, "stall", new[] { frame.At(at + new Vector2(-aw, 0)), frame.At(at + new Vector2(aw, 0)) }, Ink with { A = 0.5f }));
            pieces.Add(new RoofPiece(RoofPieceKind.Line, "stall", frame.Box(at, aw, ah), Ink, Width: 1.5f, Closed: true));
        }
    }

    private static void Yard(List<RoofPiece> pieces, RoofFrame frame, float hw, float hd, Color colour, bool fallenIn)
    {
        Vector2[] ground = frame.Box(Vector2.Zero, hw, hd);
        pieces.Add(new RoofPiece(RoofPieceKind.Fill, "ground", ground, YardEarth.Lerp(colour, 0.45f) with { A = 0.9f }));
        pieces.Add(new RoofPiece(RoofPieceKind.Line, "ground", ground, Ink with { A = 0.5f }, Width: 1.5f, Closed: true, Dashed: fallenIn));
        foreach ((float x, float y) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
        {
            pieces.Add(new RoofPiece(RoofPieceKind.Fill, "post", frame.Circle(new Vector2(x * hw, y * hd), 0.045f, 8), YardPost));
        }
    }

    private static void Mill(List<RoofPiece> pieces, RoofFrame frame, float hw, float hd, Color colour, bool fallenIn)
    {
        float s = Mathf.Min(hw, hd) / 0.8f;
        float body = 0.36f * s;
        pieces.Add(new RoofPiece(RoofPieceKind.Fill, "shadow", frame.Circle(frame.Local(new Vector2(0.1f, 0.13f) * 1.3f), body + 0.04f), RoofShadow with { A = 0.3f }));

        // Four sails, a quarter turn apart, set at a slant and turned with the building.
        for (int k = 0; k < 4; k++)
        {
            float a = 0.35f + (k * Mathf.Pi / 2f);
            var along = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var across = new Vector2(-along.Y, along.X);
            Vector2 P(float u, float v) => frame.At((along * u * s) + (across * v * s));
            Vector2[] sail = { P(0.12f, -0.02f), P(0.95f, -0.02f), P(0.95f, 0.16f), P(0.12f, 0.16f) };
            pieces.Add(new RoofPiece(RoofPieceKind.Fill, "sail", sail, SailCloth with { A = 0.92f }));
            var lattice = new List<Vector2> { P(0.12f, 0.07f), P(0.95f, 0.07f) };
            for (float u = 0.22f; u < 0.95f; u += 0.11f)
            {
                lattice.Add(P(u, -0.02f));
                lattice.Add(P(u, 0.16f));
            }

            pieces.Add(new RoofPiece(RoofPieceKind.Lines, "sail lattice", lattice.ToArray(), SailLattice));
            pieces.Add(new RoofPiece(RoofPieceKind.Line, "sail", sail, Ink with { A = 0.7f }, Closed: true));
            pieces.Add(new RoofPiece(RoofPieceKind.Line, "spar", new[] { P(0f, 0f), P(0.97f, 0f) }, SailSpar, Width: 2f));
        }

        Vector2[] round = frame.Circle(Vector2.Zero, body);
        pieces.Add(new RoofPiece(RoofPieceKind.Fill, "body", round, colour));
        pieces.Add(new RoofPiece(RoofPieceKind.Fill, "body", frame.Circle(frame.Local(new Vector2(-0.05f, -0.05f) * s), body * 0.8f), colour.Lightened(0.08f) with { A = 0.6f }));
        pieces.Add(new RoofPiece(RoofPieceKind.Fill, "cap", frame.Circle(Vector2.Zero, 0.16f * s, 16), MillCap));
        pieces.Add(new RoofPiece(RoofPieceKind.Line, "body", round, Ink, Width: 2f, Closed: true, Dashed: fallenIn));
        pieces.Add(new RoofPiece(RoofPieceKind.Line, "cap", frame.Circle(Vector2.Zero, 0.16f * s, 16), Ink with { A = 0.8f }, Closed: true));
        pieces.Add(new RoofPiece(RoofPieceKind.Fill, "hub", frame.Circle(Vector2.Zero, 0.04f * s, 8), Ink));
    }

    private static void Well(List<RoofPiece> pieces, RoofFrame frame, float hw, float hd, Color colour, bool fallenIn)
    {
        float ring = Mathf.Min(hw, hd) * 0.69f;
        float water = ring * 0.58f;
        pieces.Add(new RoofPiece(RoofPieceKind.Fill, "shadow", frame.Circle(frame.Local(new Vector2(0.06f, 0.08f)), ring + 0.03f), RoofShadow with { A = 0.3f }));
        Vector2[] stones = frame.Circle(Vector2.Zero, ring);
        pieces.Add(new RoofPiece(RoofPieceKind.Fill, "stones", stones, colour));
        var seams = new Vector2[28];
        for (int i = 0; i < 14; i++)
        {
            float a = i * Mathf.Tau / 14f;
            var way = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            seams[2 * i] = frame.At(way * water);
            seams[(2 * i) + 1] = frame.At(way * ring);
        }

        pieces.Add(new RoofPiece(RoofPieceKind.Lines, "stones", seams, Ink with { A = 0.45f }));

        Vector2[] pool = frame.Circle(Vector2.Zero, water);
        pieces.Add(new RoofPiece(RoofPieceKind.Fill, "water", pool, WellWater));
        pieces.Add(new RoofPiece(RoofPieceKind.Line, "stones", stones, Ink, Width: 2f, Closed: true, Dashed: fallenIn));
        pieces.Add(new RoofPiece(RoofPieceKind.Line, "water", pool, Ink with { A = 0.8f }, Closed: true));
    }

    // ------------------------------------------------------------------ the probe (§6)

    /// <summary>
    /// ⭐ The probe's <c>painted buildings:</c> line (A85, <c>building-roofs.md §6</c>). Every expectation is named here from
    /// the spec — the files, the table, the chimneys — never read back from the code's own lists (art traps 17, 25).
    /// </summary>
    /// <remarks>⚠️ Nothing is drawn headless (D340): the textures' look, the stripes and the shadows' softness are the renders'.</remarks>
    /// <remarks>⭐ The bridges' line rides on this one's return (A175, art trap 73: the list of lines is `Main.cs`'s), each in
    /// its own <c>try</c>, so a crash in one never loses the other.</remarks>
    internal string ThePaintedBuildingsHold() => ThePaintedRoofsHold() + "\n" + ThePaintedBridgesHold();

    private string ThePaintedRoofsHold()
    {
        try
        {
            var faults = new List<string>();

            // The seven materials, as the spec names them: 384 × 128, uncompressed, mipmapped, mean tone 0.8 ± 0.03.
            foreach ((RoofMaterial m, string file) in new[]
            {
                (RoofMaterial.Thatch, "roof_thatch"), (RoofMaterial.Shingle, "roof_shingle"), (RoofMaterial.Slate, "roof_slate"),
                (RoofMaterial.Tile, "roof_tile"), (RoofMaterial.Boards, "roof_boards"), (RoofMaterial.Turf, "roof_turf"),
                (RoofMaterial.StoneSlab, "roof_stoneslab"),
            })
            {
                string path = $"res://textures/buildings/{file}.png";
                CanvasTexture? held = RoofTextureOf(m);
                if (held?.DiffuseTexture?.ResourcePath != path || held.TextureFilter != TextureFilterEnum.LinearWithMipmaps)
                {
                    faults.Add($"{m} draws {held?.DiffuseTexture?.ResourcePath} with {held?.TextureFilter} (want {path}, LinearWithMipmaps)");
                    continue;
                }

                using Image image = held.DiffuseTexture.GetImage();
                if (image.GetWidth() != 384 || image.GetHeight() != 128 || !image.HasMipmaps() || image.IsCompressed())
                {
                    faults.Add($"{file} is {image.GetWidth()} × {image.GetHeight()}, mipmaps {image.HasMipmaps()}, compressed {image.IsCompressed()} (want 384 × 128, mipmapped, lossless)");
                    continue;
                }

                double sum = 0;
                for (int y = 0; y < 128; y++)
                {
                    for (int x = 0; x < 384; x++)
                    {
                        Color c = image.GetPixel(x, y);
                        sum += (c.R + c.G + c.B) / 3.0;
                    }
                }

                double mean = sum / (384 * 128);
                if (System.Math.Abs(mean - 0.8) > 0.03)
                {
                    faults.Add($"{file}'s mean tone is {mean:F3} (want 0.8 ± 0.03, or a roof is not its kind's colour)");
                }
            }

            // The table (§3), as the spec writes it.
            void Want(string what, RoofLook? got, RoofForm form, RoofMaterial material)
            {
                if (got is not RoofLook look || look.Form != form || (form == RoofForm.Roof && look.Material != material))
                {
                    faults.Add($"{what} draws as {got?.Form} {got?.Material} (want {form} {material})");
                }
            }

            Want("the cabin", RoofOf(BuildingKind.Home), RoofForm.Roof, RoofMaterial.Thatch);
            Want("the cottage", RoofOf(BuildingKind.Cottage), RoofForm.Roof, RoofMaterial.Shingle);
            Want("the manor", RoofOf(BuildingKind.Manor), RoofForm.Roof, RoofMaterial.Slate);
            Want("the library", RoofOf(BuildingKind.Library), RoofForm.Roof, RoofMaterial.Slate);
            Want("the town hall", RoofOf(BuildingKind.TownHall), RoofForm.Roof, RoofMaterial.StoneSlab);
            Want("the well", RoofOf(BuildingKind.Well), RoofForm.Well, default);
            Want("the granary", RoofOf(StoreKind.Granary), RoofForm.Roof, RoofMaterial.Thatch);
            Want("the warehouse", RoofOf(StoreKind.Warehouse), RoofForm.Roof, RoofMaterial.Shingle);
            Want("the market", RoofOf(StoreKind.Market), RoofForm.Market, default);
            Want("the stockpile", RoofOf(StoreKind.Pile), RoofForm.Yard, default);
            Want("the barn", RoofOf(StoreKind.Barn), RoofForm.Roof, RoofMaterial.Tile);
            Want("the trading post", RoofOf(StoreKind.TradingPost), RoofForm.Roof, RoofMaterial.Slate);
            // A87: the founders' cart is drawn now, as a wagon (`building-yards.md §3.4`).
            Want("the founders' cart", RoofOf(StoreKind.Cart), RoofForm.Wagon, default);

            foreach ((JobKind job, RoofForm form, RoofMaterial material) in new[]
            {
                (JobKind.Forager, RoofForm.Roof, RoofMaterial.Turf), (JobKind.Forester, RoofForm.Roof, RoofMaterial.Turf),
                (JobKind.Woodcutter, RoofForm.Roof, RoofMaterial.Shingle), (JobKind.Builder, RoofForm.Roof, RoofMaterial.Shingle),
                (JobKind.Fisher, RoofForm.Roof, RoofMaterial.Boards), (JobKind.Hunter, RoofForm.Roof, RoofMaterial.Shingle),
                (JobKind.Smith, RoofForm.Roof, RoofMaterial.Slate), (JobKind.Quarrier, RoofForm.Roof, RoofMaterial.Shingle),
                (JobKind.Miner, RoofForm.Roof, RoofMaterial.Tile), (JobKind.Miller, RoofForm.Mill, default),
                (JobKind.Baker, RoofForm.Roof, RoofMaterial.Tile), (JobKind.Tailor, RoofForm.Roof, RoofMaterial.Tile),
                (JobKind.Farmer, RoofForm.Roof, RoofMaterial.Thatch), (JobKind.Carpenter, RoofForm.Roof, RoofMaterial.Shingle),
                (JobKind.Marketer, RoofForm.ByItsStore, default), (JobKind.Herdsman, RoofForm.ByItsStore, default),
                (JobKind.Merchant, RoofForm.ByItsStore, default),
            })
            {
                Want($"the {job}", RoofOf(job), form, material);
            }

            // The light, as the spec names it — from the upper left — not read back from the code's own constant (trap 17).
            Vector2 upperLeft = new Vector2(-1f, -1f).Normalized();
            const float ppt = 40f;
            var centre = new Vector2(500f, 500f);
            Color kind = new("#b98a52");
            List<RoofPiece> Draw(RoofLook look, float wide, float deep, ushort facing, Door? door = null) =>
                RoofDrawing(look, centre, wide, deep, facing, kind, ppt, door, centre, 7, false);
            Vector2 Local(Vector2 at, ushort facing)
            {
                float a = facing * Mathf.Tau / 65536f;
                Vector2 d = (at - centre) / ppt;
                return new Vector2((d.X * Mathf.Cos(a)) + (d.Y * Mathf.Sin(a)), (-d.X * Mathf.Sin(a)) + (d.Y * Mathf.Cos(a)));
            }

            static float Area(Vector2[] p)
            {
                float s = 0f;
                for (int i = 0; i < p.Length; i++)
                {
                    s += (p[i].X * p[(i + 1) % p.Length].Y) - (p[(i + 1) % p.Length].X * p[i].Y);
                }

                return Mathf.Abs(s) / 2f;
            }

            // The chimneys (§2 item 4).
            foreach ((string what, RoofLook look, int want) in new[]
            {
                ("cabin", RoofOf(BuildingKind.Home), 1), ("cottage", RoofOf(BuildingKind.Cottage), 1), ("manor", RoofOf(BuildingKind.Manor), 2),
                ("smithy", RoofOf(JobKind.Smith), 1), ("bakery", RoofOf(JobKind.Baker), 1), ("farmhouse", RoofOf(JobKind.Farmer), 1),
                ("workshop", RoofOf(JobKind.Carpenter), 1), ("tailor", RoofOf(JobKind.Tailor), 1), ("library", RoofOf(BuildingKind.Library), 0),
                ("woodcutter", RoofOf(JobKind.Woodcutter), 0), ("granary", RoofOf(StoreKind.Granary)!.Value, 0),
            })
            {
                int got = Draw(look, 1.6f, 0.8f, 0).Count(p => p.Part == "chimney" && p.Kind == RoofPieceKind.Line);
                if (got != want)
                {
                    faults.Add($"the {what} has {got} chimneys (want {want})");
                }
            }

            // The faces: a gable on a 2 × 1 and a 3 × 2, a hip on a 2 × 2 and a 1 × 1, tiling the eaves' rectangle at sixteen turns.
            int turnsHeld = 0;
            foreach ((float wide, float deep, int faces) in new[] { (1.6f, 0.8f, 2), (1.6f, 1.6f, 4), (0.8f, 0.8f, 4), (2.4f, 1.6f, 2) })
            {
                for (int k = 0; k < 16; k++)
                {
                    ushort facing = (ushort)(k * 4096);
                    List<RoofPiece> pieces = Draw(new RoofLook(RoofForm.Roof, RoofMaterial.Slate), wide, deep, facing);
                    RoofPiece[] face = pieces.Where(p => p.Part == "face" && p.Kind == RoofPieceKind.Textured).ToArray();
                    float ew = (wide / 2f) + 0.04f;
                    float ed = (deep / 2f) + 0.04f;
                    float area = face.Sum(p => Area(p.Points));
                    bool inside = face.All(p => p.Points.All(q => Mathf.Abs(Local(q, facing).X) <= ew + 0.001f && Mathf.Abs(Local(q, facing).Y) <= ed + 0.001f));
                    if (face.Length != faces || Mathf.Abs(area - (4f * ew * ed * ppt * ppt)) > 0.005f * area || !inside)
                    {
                        faults.Add($"a {wide} × {deep} at turn {k}/16 has {face.Length} faces covering {area / (ppt * ppt):F3} tiles², inside {inside} (want {faces}, {4f * ew * ed:F3}, the eaves 0.04 out)");
                        continue;
                    }

                    // Lit by its facing: the face toward the upper left the lightest, its opposite the darkest, the mean 1 — of the
                    // kind's colour as a wash, 12 % of the paper #efe6d0 in it (A86, `building-yards.md §5`).
                    float Light(RoofPiece p) => p.Colour.R * RoofTextureMean / kind.Lerp(new Color("#efe6d0"), 0.12f).R;
                    Vector2 Toward(RoofPiece p) => (p.Points.Aggregate(Vector2.Zero, (s, q) => s + q) / p.Points.Length) - centre;
                    RoofPiece lightest = face.MaxBy(Light)!;
                    RoofPiece darkest = face.MinBy(Light)!;
                    RoofPiece most = face.MaxBy(p => Toward(p).Normalized().Dot(upperLeft))!;
                    RoofPiece least = face.MinBy(p => Toward(p).Normalized().Dot(upperLeft))!;
                    float mean = face.Sum(p => Light(p) * Area(p.Points)) / area;
                    bool tie = Mathf.Abs(Light(lightest) - Light(most)) < 0.001f && Mathf.Abs(Light(darkest) - Light(least)) < 0.001f;
                    if (!tie || Mathf.Abs(mean - 1f) > 0.002f || (faces == 4 && Light(lightest) - Light(darkest) < 0.1f))
                    {
                        faults.Add($"a {wide} × {deep} at turn {k}/16 is lit {Light(darkest):F3}–{Light(lightest):F3}, mean {mean:F3}, the lightest face toward the light {tie} (want the upper left lightest, the mean 1)");
                        continue;
                    }

                    // The ridge runs along the long side.
                    RoofPiece? ridge = pieces.FirstOrDefault(p => p.Part == "face ridge" && p.Kind == RoofPieceKind.Line);
                    if (wide > deep && (ridge is null || Mathf.Abs(Local(ridge.Points[0], facing).Y) > 0.001f || Mathf.Abs(Local(ridge.Points[1], facing).Y) > 0.001f))
                    {
                        faults.Add($"a {wide} × {deep} at turn {k}/16 has its ridge off its long middle");
                        continue;
                    }

                    turnsHeld++;
                }
            }

            // The step: outside the eave, on the door's own wall — the front, and a quarter round.
            foreach ((Compass faces, Vector2 way) in new[] { (Compass.North, new Vector2(0, -1)), (Compass.East, new Vector2(1, 0)) })
            {
                var door = new Door(default, default, faces, Compass.North, DoorBlock.None, default, false);
                RoofPiece? step = Draw(RoofOf(BuildingKind.Home), 1.6f, 0.8f, 8192, door).FirstOrDefault(p => p.Part == "step" && p.Kind == RoofPieceKind.Fill);
                Vector2 at = step is null ? Vector2.Zero : Local(step.Points.Aggregate(Vector2.Zero, (s, q) => s + q) / 4f, 8192);
                float beyond = way.X != 0 ? at.X * way.X : at.Y * way.Y;
                float eave = way.X != 0 ? 0.84f : 0.44f;
                if (step is null || beyond <= eave || Mathf.Abs(way.X != 0 ? at.Y : at.X) > 0.01f)
                {
                    faults.Add($"a door facing {faces} has its step at {at} (want past the eave at {eave} on that wall)");
                }
            }

            // The extras: the turret on the town hall alone; the mill's four sails; the well's water; the market's three stalls.
            int turrets = Draw(RoofOf(BuildingKind.TownHall), 2.85f, 1.9f, 0).Count(p => p.Part == "turret");
            int strays = Draw(RoofOf(BuildingKind.Library), 1.6f, 1.6f, 0).Count(p => p.Part == "turret");
            int sails = Draw(RoofOf(JobKind.Miller), 1.6f, 1.6f, 0).Count(p => p.Part == "sail" && p.Kind == RoofPieceKind.Fill);
            int water = Draw(RoofOf(BuildingKind.Well), 0.7f, 0.7f, 0).Count(p => p.Part == "water" && p.Kind == RoofPieceKind.Fill);
            List<RoofPiece> market = Draw(RoofOf(StoreKind.Market)!.Value, 1.6f, 1.6f, 0);
            int stalls = market.Count(p => p.Part == "stall" && p.Kind == RoofPieceKind.Line && p.Closed);
            int stripes = market.Count(p => p.Part == "stall stripe");
            int byStore = Draw(RoofOf(JobKind.Marketer), 1.6f, 1.6f, 0).Count;
            if (turrets == 0 || strays != 0 || sails != 4 || water != 1 || stalls != 3 || stripes == 0 || byStore != 0)
            {
                faults.Add($"turret pieces {turrets} (on the library {strays}), sails {sails}, well water {water}, stalls {stalls} with {stripes} stripes, a market's trade drawn as {byStore} pieces (want some, 0, 4, 1, 3, some, 0)");
            }

            // Kept between frames: the same building twice lays nothing; a change of colour lays it once; the runs carry every
            // piece in its order, each polygon fanned (n − 2 triangles), the pen lines kept as lines.
            var far = new Vector2(-9999f, -9999f);
            var asked = new RoofSignature(RoofOf(BuildingKind.Home), far, 1.6f, 0.8f, 8192, kind, false, null, far, 3);
            int layingsBefore = RoofLayings;
            RoofCache firstLaid = CachedRoof(asked);
            RoofCache again = CachedRoof(asked);
            RoofCache recoloured = CachedRoof(asked with { Colour = kind.Darkened(0.2f) });
            int laid = RoofLayings - layingsBefore;
            _roofCache.Remove(far);
            List<RoofPiece> cabin = RoofDrawing(RoofOf(BuildingKind.Home), far, 1.6f, 0.8f, 8192, kind, 1f, null, far, 3, false);
            List<RoofRun> runs = RoofRuns(cabin, 40f);
            // A86: a textured run per material; the fills and the pen lines between them one run, in order, laid for the zoom.
            int wantTriangles = cabin.Where(p => p.Kind is RoofPieceKind.Fill or RoofPieceKind.Textured).Sum(p => p.Points.Length - 2);
            bool linesInOrder = runs.Where(r => r.Lines is not null).SelectMany(r => r.Lines!).Where(p => p.Kind != RoofPieceKind.Fill)
                .SequenceEqual(cabin.Where(p => p.Kind is RoofPieceKind.Line or RoofPieceKind.Lines));
            // Each pen segment: a core and two feathers, six triangles (a 1-pixel line has no core: four); a texture's strokes
            // kept no closer than 3 px at this zoom (§5): every n-th, n = ⌈3 ÷ (spacing × 40)⌉.
            static int Kept(RoofPiece p) => p.Kind == RoofPieceKind.Lines
                ? (p.Spacing > 0f && p.Spacing * 40f < 3f ? ((p.Points.Length / 2) + Mathf.CeilToInt(3f / (p.Spacing * 40f)) - 1) / Mathf.CeilToInt(3f / (p.Spacing * 40f)) : p.Points.Length / 2)
                : p.Closed ? p.Points.Length : p.Points.Length - 1;
            int lineSegments = cabin.Where(p => p.Kind is RoofPieceKind.Line or RoofPieceKind.Lines).Sum(Kept);
            int wideSegments = cabin.Where(p => (p.Kind is RoofPieceKind.Line or RoofPieceKind.Lines) && p.Width > 1f).Sum(Kept);
            int meshTriangles = runs.Sum(r => r.Mesh.GetSurfaceCount() == 0 ? 0 : r.Mesh.SurfaceGetArrayLen(0) / 3);
            int gotTriangles = meshTriangles - (4 * lineSegments) - (2 * wideSegments);
            int lineTriangles = (4 * lineSegments) + (2 * wideSegments);
            bool linesLaidForTheZoom = runs.Where(r => r.Lines is not null).All(r => r.LinePpt == 40f);
            // A face's vertex colour is its piece's × the textures' mean (drawn × 1 ÷ the mean), read back from the mesh.
            RoofPiece firstFace = cabin.First(p => p.Kind == RoofPieceKind.Textured);
            var faceColours = runs.First(r => r.Material is not null).Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Color].AsColorArray();
            bool facesScaled = Mathf.Abs(faceColours[0].R - (firstFace.Colour.R * RoofTextureMean)) < 0.01f;
            if (!ReferenceEquals(firstLaid, again) || ReferenceEquals(again, recoloured) || laid != 2
                || gotTriangles != wantTriangles || meshTriangles != wantTriangles + lineTriangles || !linesInOrder
                || !linesLaidForTheZoom || !facesScaled)
            {
                faults.Add($"the cache: the same building {(ReferenceEquals(firstLaid, again) ? "kept" : "laid again")}, recoloured {(ReferenceEquals(again, recoloured) ? "kept" : "laid again")}, {laid} layings (want kept, laid again, 2); the runs {gotTriangles} triangles, the meshes {meshTriangles} (want {wantTriangles}), the lines in order {linesInOrder}, as {lineTriangles} triangles (want {(4 * lineSegments) + (2 * wideSegments)}) laid for the zoom {linesLaidForTheZoom}; a face's colour × the mean {facesScaled}");
            }

            // The tick: painted with the watercolour on, never with it off.
            bool was = _watercolour;
            _watercolour = false;
            bool off = PaintsBuildings;
            _watercolour = true;
            bool on = PaintsBuildings;
            _watercolour = was;
            if (off || !on)
            {
                faults.Add($"the buildings are painted with the tick off {off}, on {on} (want false, true)");
            }

            return faults.Count == 0
                ? $"[widths] painted buildings: ✅ the seven roof materials 384 × 128, lossless, mipmapped, drawn with mipmaps, each mean tone 0.8; every kind in its material (§3's table, 30 rows); the chimneys counted on eleven kinds; a 2 × 1 and a 3 × 2 a gable along the long side, a 2 × 2 and a 1 × 1 a hip, the faces tiling the eaves' rectangle 0.04 out, the face toward the upper left the lightest (a gable whose ridge points at the light lit evenly), its opposite the darkest and the mean 1, at sixteen turns ({turnsHeld} of 64 held); a door's step past the eave on its own wall; the turret on the town hall alone, the mill's four sails, the well's water, the market's three striped stalls, the shared trades left to their stores; laid once and kept until the building changes, the runs every piece in order, the pen lines a mesh laid for the zoom; painted only with the tick"
                : $"[widths] painted buildings: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            return $"[widths] painted buildings: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}
