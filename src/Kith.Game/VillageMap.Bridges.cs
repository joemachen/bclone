using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <summary>
/// Everything a painted bridge's look depends on (A175, `painted-bridges.md §5`) — its pieces are laid again only when one of
/// these changes. The ends are in the view's tiles; <paramref name="Drawn"/> is the condition it is drawn in
/// (<see cref="VillageMap.DrawnCondition"/>), not the sim's.
/// </summary>
internal readonly record struct BridgeSignature(Vector2 Near, Vector2 Far, bool Stone, Condition Drawn, int Seed);

/// <remarks>
/// ⭐⭐ THE BRIDGES PAINTED (A175, `specs/painted-bridges.md`; Joe's pick, *"B, all as recommended"*, of three mocks on his own
/// villages). A wooden bridge is planks on trestles with a post-and-rail each side; a stone one is dressed setts between two
/// parapets on pointed piers. Each drawn in its own frame — along the span, across it — so it is the same picture at any angle,
/// and lit from the upper left whatever its turn, with its shadow on the water to the lower right: the buildings' treatment
/// (`building-roofs.md`). Only with the watercolour tick on; off, today's deck, to the byte.
/// </remarks>
public partial class VillageMap
{
    /// <summary>The deck's half-width in tiles: wood, and stone (a little wider, for its parapets).</summary>
    internal const float BridgeHalfWidth = 0.35f;

    internal const float StoneBridgeHalfWidth = 0.42f;

    /// <summary>A bent every this many tiles across the water (wood); a pier every this many (stone).</summary>
    internal const float BridgeBentSpacing = 1.5f;

    internal const float BridgePierSpacing = 2.6f;

    /// <summary>A board's width along the span, ± a quarter of it by hash; a stone deck is laid in slices a tile long.</summary>
    internal const float BridgeBoardWidth = 0.16f;

    /// <summary>The share of boards over the water a failing deck has lost (the mock's quarter read as windows, §3).</summary>
    internal const float BridgeBoardsLostWhenFailing = 1f / 6f;

    /// <summary>A rail's post every this many tiles, and one at each end.</summary>
    internal const float BridgePostSpacing = 0.75f;

    /// <summary>How high the deck stands, as the roofs' lift: its shadow moves (0.13, 0.17) × this.</summary>
    internal const float BridgeLift = 0.75f;

    internal const float StoneBridgeLift = 1f;

    /// <summary>The decks' paintings: 128 px a tile along the span, the longest span (12 tiles) across the width.</summary>
    internal const int BridgeDeckTextureWidth = 1536;

    internal const int BridgeDeckTextureHeight = 96;

    internal const string BridgePlanksPath = "res://textures/buildings/deck_planks.png";
    internal const string BridgeSettsPath = "res://textures/buildings/deck_setts.png";

    private static readonly Color BridgeShadowWash = new(0.12f, 0.16f, 0.24f, 0.16f);
    private static readonly Color BridgeSmallShadow = new(0.16f, 0.2f, 0.27f, 0.3f);
    private static readonly Color BridgePile = new("#4a3524");
    private static readonly Color BridgeBeam = new("#5a4330");
    private static readonly Color BridgeRail = new("#7a5a3c");
    private static readonly Color BridgeFoam = new(0.93f, 0.96f, 0.96f, 0.55f);
    private static readonly Color BridgeFooting = new("#9d968a");
    private static readonly Color BridgeParapet = new("#bdb8ae");
    private static readonly Color BridgePaper = new("#efe6d0");

    /// <summary>The two decks' paintings, loaded once and held (art trap 69: a scripted resource is never dropped).</summary>
    private CanvasTexture? _bridgePlanks;

    private CanvasTexture? _bridgeSetts;

    /// <summary>The painted bridges' meshes, one entry a bridge, keyed by its near end.</summary>
    private readonly Dictionary<Vector2, BridgeCache> _bridgeCache = new();

    private int _bridgeCacheGeneration = -1;

    /// <summary>How many times a bridge's pieces were laid (the probe asks; never hashed).</summary>
    internal int BridgeLayings { get; private set; }

    /// <summary>A bridge's pieces, laid in tile space once, as runs in their drawing order.</summary>
    internal sealed class BridgeCache
    {
        public required BridgeSignature Signature { get; init; }

        public required List<RoofRun> Runs { get; init; }

        public ulong LastSeen { get; set; }
    }

    /// <summary>
    /// What a bridge is drawn as: stone never wears; a closed bridge has fallen in; a ruin kept open — the only way somewhere, or
    /// somebody on it — is drawn failing, because people are still crossing it (§3).
    /// </summary>
    internal static Condition DrawnCondition(bool stone, Condition worn, bool closed) =>
        stone ? Condition.Sound
        : closed ? Condition.Ruin
        : worn == Condition.Ruin ? Condition.Failing
        : worn;

    internal static BridgeSignature BridgeSignatureOf(Bridge bridge, Condition worn)
    {
        bool stone = bridge.Kind == BuildingKind.StoneBridge;
        Vector2 near = RoofRounded(InViewTiles(bridge.Near));
        Vector2 far = RoofRounded(InViewTiles(bridge.Far));
        return new BridgeSignature(near, far, stone, DrawnCondition(stone, worn, bridge.Closed), BridgeSeedOf(near));
    }

    /// <summary>A bridge's own hash seed, from where it stands — the same boards lost every frame and every load.</summary>
    internal static int BridgeSeedOf(Vector2 near) =>
        unchecked((Mathf.RoundToInt(near.X * 16f) * 73856093) ^ (Mathf.RoundToInt(near.Y * 16f) * 19349663));

    internal static float BridgeHash(int a, int b, int seed)
    {
        uint h = unchecked((uint)((a * 374761393) + (b * 668265263) + (seed * 224682251)));
        h = unchecked((h ^ (h >> 13)) * 1274126177u);
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }

    // ------------------------------------------------------------------ the drawing

    /// <summary>
    /// ⭐ Draws a finished bridge painted, if the tick is on — <b>false means "draw it as before"</b>, and the caller does.
    /// </summary>
    private bool DrawPaintedBridge(SimWorld world, Bridge bridge, Condition worn)
    {
        if (!PaintsBuildings)
        {
            return false;
        }

        ulong frame = Engine.GetProcessFrames();
        ForgetGoneBridges(world, frame);
        BridgeCache held = CachedBridge(BridgeSignatureOf(bridge, worn), at => IsWaterInView(world, at));
        held.LastSeen = frame;
        DrawBridgeRuns(held, held.Signature.Stone ? BridgeDeckOf(true) : BridgeDeckOf(false));
        return true;
    }

    /// <summary>The tile under a point in the view's tiles is water (a deck's tiles stay water, `bridges.md §3`).</summary>
    private static bool IsWaterInView(SimWorld world, Vector2 at) =>
        world.Map.TerrainAt(new GridPos(Mathf.FloorToInt(at.X + 0.5f), Mathf.FloorToInt(at.Y + 0.5f))) == Terrain.Water;

    /// <summary>A bridge's runs — the cached ones while nothing it depends on has changed, laid again (and counted) when anything has.</summary>
    /// <remarks>The water under a span is read only when it is laid: the ground under a deck is never edited (`bridges.md §6`).</remarks>
    internal BridgeCache CachedBridge(BridgeSignature signature, Func<Vector2, bool> isWater)
    {
        if (_bridgeCache.TryGetValue(signature.Near, out BridgeCache? held) && held.Signature == signature)
        {
            return held;
        }

        BridgeLayings++;
        var laid = new BridgeCache { Signature = signature, Runs = RoofRuns(BridgeDrawing(signature, isWater), _pixelsPerTile) };
        _bridgeCache[signature.Near] = laid;
        return laid;
    }

    /// <summary>Forget the bridges not drawn lately, when what stands has changed (raised, set in stone, pulled down — a counter, CLAUDE.md).</summary>
    private void ForgetGoneBridges(SimWorld world, ulong frame)
    {
        if (world.StandingGeneration == _bridgeCacheGeneration)
        {
            return;
        }

        _bridgeCacheGeneration = world.StandingGeneration;
        foreach (Vector2 at in _bridgeCache.Where(e => e.Value.LastSeen + 2 < frame).Select(e => e.Key).ToList())
        {
            _bridgeCache.Remove(at);
        }
    }

    private CanvasTexture? BridgeDeckOf(bool stone)
    {
        if (stone)
        {
            return _bridgeSetts ??= LoadBridgeDeck(BridgeSettsPath);
        }

        return _bridgePlanks ??= LoadBridgeDeck(BridgePlanksPath);
    }

    private static CanvasTexture? LoadBridgeDeck(string path)
    {
        Texture2D? painting = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        if (painting is null)
        {
            GD.PushError($"[Kith] bridge deck missing at {path}: that deck draws flat");
            return null;
        }

        return new CanvasTexture { DiffuseTexture = painting, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
    }

    /// <summary>Draw a bridge's runs through the frame's one transform, its deck in its painting; its lines laid again if the zoom has moved.</summary>
    private void DrawBridgeRuns(BridgeCache bridge, CanvasTexture? deck)
    {
        Transform2D toScreen = TileToScreen();
        Rid canvas = GetCanvasItem();
        for (int i = 0; i < bridge.Runs.Count; i++)
        {
            RoofRun run = bridge.Runs[i];
            if (run.Lines is { } lines && run.LinePpt != _pixelsPerTile)
            {
                run = bridge.Runs[i] = RelaidForTheZoom(run, lines, _pixelsPerTile);
            }

            if (run.Mesh.GetSurfaceCount() == 0)
            {
                continue;
            }

            if (run.Material is not null)
            {
                float lift = 1f / RoofTextureMean;
                RenderingServer.CanvasItemAddMesh(canvas, run.Mesh.GetRid(), toScreen, new Color(lift, lift, lift, 1f), deck?.GetRid() ?? default);
            }
            else
            {
                RenderingServer.CanvasItemAddMesh(canvas, run.Mesh.GetRid(), toScreen, Colors.White);
            }
        }
    }

    // ------------------------------------------------------------------ the pieces (pure: the map and the probe read the same list)

    /// <summary>
    /// ⭐⭐ The pieces one bridge is drawn with, in the view's tiles, in order (`painted-bridges.md §3`). Pure, so the probe reads
    /// what the map draws. <paramref name="isWater"/> answers for a point in the view's tiles.
    /// </summary>
    internal static List<RoofPiece> BridgeDrawing(BridgeSignature s, Func<Vector2, bool> isWater)
    {
        var pieces = new List<RoofPiece>();
        Vector2 n = s.Near;
        float len = n.DistanceTo(s.Far);
        if (len < 0.05f)
        {
            return pieces;
        }

        Vector2 u = (s.Far - n) / len;
        var v = new Vector2(-u.Y, u.X);
        bool stone = s.Stone;
        Condition worn = s.Drawn;
        float hw = stone ? StoneBridgeHalfWidth : BridgeHalfWidth;
        int seed = s.Seed;

        Vector2 P(float along, float across) => n + (u * along) + (v * across);
        Vector2[] Quad(float a0, float a1, float c0, float c1) => new[] { P(a0, c0), P(a1, c0), P(a1, c1), P(a0, c1) };
        Vector2[] Circle(Vector2 at, float r, int sides = 12)
        {
            var points = new Vector2[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = Mathf.Tau * k / sides;
                points[k] = at + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }

            return points;
        }

        void Fill(string part, Vector2[] points, Color colour) => pieces.Add(new RoofPiece(RoofPieceKind.Fill, part, points, colour));
        void Pen(string part, Vector2[] points, Color colour, float width, bool closed = false) =>
            pieces.Add(new RoofPiece(RoofPieceKind.Line, part, points, colour, Width: width, Closed: closed));
        bool Water(float along) => isWater(P(along, 0f));

        // Where the water is under the span.
        float w0 = -1f;
        float w1 = -1f;
        for (float a = 0f; a <= len; a += 0.05f)
        {
            if (Water(a))
            {
                w0 = w0 < 0f ? a : w0;
                w1 = a;
            }
        }

        // The supports: bents (wood) every ~1.5 tiles across the water, both banks' included; piers (stone) between its arches.
        var supports = new List<float>();
        if (w0 >= 0f)
        {
            float span = w1 - w0;
            int gaps = Mathf.Max(1, Mathf.RoundToInt(span / (stone ? BridgePierSpacing : BridgeBentSpacing)));
            for (int k = 0; k <= gaps; k++)
            {
                if (stone && (k == 0 || k == gaps))
                {
                    continue;
                }

                supports.Add(span < 0.6f ? (w0 + w1) / 2f : Mathf.Lerp(w0 + 0.25f, w1 - 0.25f, k / (float)gaps));
                if (span < 0.6f)
                {
                    break;
                }
            }
        }

        // A fallen-in span: between the two supports nearest the middle; a deck too short for two, its water's middle third.
        float g0 = -1f;
        float g1 = -1f;
        if (worn == Condition.Ruin)
        {
            if (supports.Count >= 2)
            {
                int mid = Mathf.Clamp(supports.Count / 2, 1, supports.Count - 1);
                g0 = supports[mid - 1] + 0.15f;
                g1 = supports[mid] - 0.15f;
            }
            else if (w0 >= 0f)
            {
                g0 = Mathf.Lerp(w0, w1, 1f / 3f);
                g1 = Mathf.Lerp(w0, w1, 2f / 3f);
            }
        }

        bool InGap(float a) => g0 >= 0f && a > g0 && a < g1;

        // The colours: the kind's, 12 % toward the paper as the roofs (`building-roofs.md §3`), weathered (D560).
        Color timber = Weathered((stone ? StoneBridgeColour : BridgeColour).Lerp(BridgePaper, 0.12f), worn);
        Color beam = Weathered(BridgeBeam, worn);
        Color pile = Weathered(BridgePile, worn);
        Color rail = Weathered(BridgeRail, worn);

        // 1. Its shadow on the water, toward the lower right: three nested washes, in two where the middle has fallen in.
        Vector2 off = new Vector2(0.13f, 0.17f) * (stone ? StoneBridgeLift : BridgeLift);
        foreach (float grow in new[] { 0.05f, 0f, -0.05f })
        {
            float sw = hw + 0.06f + grow;
            foreach ((float a0, float a1) in g0 >= 0f ? new[] { (0f, g0), (g1, len) } : new[] { (0f, len) })
            {
                Vector2[] q = Quad(a0, a1, -sw, sw);
                Fill("shadow", q.Select(p => p + off).ToArray(), BridgeShadowWash);
            }
        }

        // 2. A footing at each bank: stones showing either side of the deck's end.
        foreach (float end in new[] { 0f, len })
        {
            float dir = end == 0f ? 1f : -1f;
            for (int k = 0; k < 6; k++)
            {
                float side = k % 2 == 0 ? -1f : 1f;
                float a = end + (dir * (0.12f + (0.18f * (k / 2)) + (0.05f * BridgeHash(k, 3, seed))));
                float c = side * (hw + 0.05f + (0.06f * BridgeHash(k, 5, seed)));
                float r = 0.07f + (0.04f * BridgeHash(k, 7, seed));
                Vector2 at = P(a, c);
                Fill("footing", Circle(at + new Vector2(0.03f, 0.04f), r, 10), BridgeSmallShadow with { A = 0.25f });
                Fill("footing", Circle(at, r, 10), stone ? BridgeParapet.Darkened(0.15f) : BridgeFooting.Lerp(BridgePaper, 0.1f * BridgeHash(k, 9, seed)));
                Pen("footing pen", Circle(at, r, 10), Ink with { A = 0.8f }, 1f, closed: true);
            }
        }

        // 3. The supports, under the deck — their ends stand out past it.
        foreach (float a in supports)
        {
            if (stone)
            {
                // A pier: a pointed cutwater each side, its top lit by the way it faces, foam along its point.
                const float half = 0.22f;
                float tip = hw + 0.32f;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2[] nose =
                    {
                        P(a - half, side * (hw - 0.05f)), P(a + half, side * (hw - 0.05f)), P(a + half, side * (hw + 0.1f)), P(a, side * tip),
                        P(a - half, side * (hw + 0.1f)),
                    };
                    Fill("pier shadow", nose.Select(p => p + new Vector2(0.06f, 0.08f)).ToArray(), BridgeSmallShadow);
                    Color lit = timber.Darkened(0.05f) * FaceLight(v * side);
                    Fill("pier", nose, lit with { A = 1f });
                    Pen("pier pen", new[] { nose[0], nose[4], nose[3], nose[2], nose[1] }, Ink, 1.5f);
                    Vector2 root = P(a, side * hw);
                    Pen("foam", new[] { nose[4], nose[3], nose[2] }.Select(p => root + ((p - root) * 1.18f)).ToArray(), BridgeFoam, 1.5f);
                }

                continue;
            }

            // A bent: a pile under each end of a cap beam, foam round each, the beam's ends past the deck.
            float ce = hw + 0.13f;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 at = P(a, side * (hw + 0.04f));
                Pen("foam", Circle(at, 0.12f), BridgeFoam, 1.5f, closed: true);
                Fill("pile shadow", Circle(at + new Vector2(0.04f, 0.05f), 0.075f, 10), BridgeSmallShadow);
                Fill("pile", Circle(at, 0.075f, 10), pile);
                Pen("pile pen", Circle(at, 0.075f, 10), Ink, 1.2f, closed: true);
            }

            Fill("cap", Quad(a - 0.06f, a + 0.06f, -ce, ce), beam);
            Pen("cap pen", Quad(a - 0.06f, a + 0.06f, -ce, ce), Ink, 1.2f, closed: true);
        }

        // 4. Under the boards, seen only where boards are gone: two stringers, broken where the middle fell.
        if (!stone)
        {
            foreach (float c in new[] { -hw * 0.55f, hw * 0.55f })
            {
                foreach ((float a0, float a1) in g0 >= 0f ? new[] { (0f, g0 + 0.25f), (g1 - 0.2f, len) } : new[] { (0f, len) })
                {
                    Fill("stringer", Quad(a0, a1, c - 0.035f, c + 0.035f), beam.Darkened(0.2f));
                }
            }
        }

        // 5. The deck, board by board (stone: a slice a tile long), each its own slice of the painting.
        float slack = Mathf.Max(0f, (BridgeDeckTextureWidth / 128f) - len);
        float u0Tiles = slack * BridgeHash(1, 1, seed);
        var joints = new List<(float At, float C0, float C1)>();
        var broken = new List<(float A0, float A1, float C1)>();
        float pos = 0f;
        int i = 0;
        while (pos < len - 1e-4f)
        {
            float step = stone ? 1f : BridgeBoardWidth * (1f + (0.25f * ((2f * BridgeHash(i, 11, seed)) - 1f)));
            float next = Mathf.Min(len, pos + step);
            if (len - next < 0.04f)
            {
                next = len;
            }

            bool lost = !stone && worn == Condition.Failing && BridgeHash(i, 13, seed) < BridgeBoardsLostWhenFailing && Water(pos) && Water(next);
            bool fallen = InGap((pos + next) / 2f);
            bool snapped = g0 >= 0f && !fallen && (Mathf.Abs(next - g0) < 0.35f || Mathf.Abs(pos - g1) < 0.35f);
            if (!lost && !fallen)
            {
                float c1 = snapped ? (BridgeHash(i, 19, seed) - 0.3f) * hw : hw;
                float ua = (u0Tiles + pos) * 128f / BridgeDeckTextureWidth;
                float ub = (u0Tiles + next) * 128f / BridgeDeckTextureWidth;
                float vb = (c1 + hw) / (2f * hw);
                pieces.Add(new RoofPiece(
                    RoofPieceKind.Textured, "board", Quad(pos, next, -hw, c1), timber, Uvs: new[] { new Vector2(ua, 0f), new Vector2(ub, 0f), new Vector2(ub, vb), new Vector2(ua, vb) }));
                if (!stone && pos > 0f)
                {
                    joints.Add((pos, -hw, c1));
                }

                if (snapped)
                {
                    broken.Add((pos, next, c1));
                }
            }

            pos = next;
            i++;
        }

        // 6. The pen between the boards, the snapped ends, a kerb and the pen along both edges, broken where the deck is gone.
        if (joints.Count > 0)
        {
            pieces.Add(new RoofPiece(
                RoofPieceKind.Lines, "board pen", joints.SelectMany(j => new[] { P(j.At, j.C0), P(j.At, j.C1) }).ToArray(), Ink with { A = 0.5f }, Width: 1f));
        }

        foreach ((float a0, float a1, float c1) in broken)
        {
            Pen("broken", new[] { P(a0, c1), P(a1, c1) }, Ink, 1.2f);
        }

        (float, float)[] runsAlong = g0 >= 0f ? new[] { (0f, g0), (g1, len) } : new[] { (0f, len) };
        foreach (float side in new[] { -1f, 1f })
        {
            if (stone)
            {
                // A parapet: a dressed wall each side, its top lit by the way it faces, a joint a block apart, inked.
                Color top = BridgeParapet * FaceLight(v * side);
                Fill("parapet", Quad(-0.05f, len + 0.05f, side * (hw - 0.11f), side * (hw + 0.02f)), top with { A = 1f });
                var blocks = new List<Vector2>();
                for (float a = 0.3f; a < len; a += 0.34f)
                {
                    blocks.Add(P(a, side * (hw - 0.11f)));
                    blocks.Add(P(a, side * (hw + 0.02f)));
                }

                pieces.Add(new RoofPiece(RoofPieceKind.Lines, "parapet joint", blocks.ToArray(), Ink with { A = 0.5f }, Width: 1f));
                Pen("parapet pen", new[] { P(-0.05f, side * (hw - 0.11f)), P(len + 0.05f, side * (hw - 0.11f)) }, Ink with { A = 0.7f }, 1f);
                Pen("edge pen", new[] { P(-0.05f, side * (hw + 0.02f)), P(len + 0.05f, side * (hw + 0.02f)) }, Ink, 2f);
                continue;
            }

            foreach ((float a0, float a1) in runsAlong)
            {
                Fill("kerb", Quad(a0, a1, side * (hw - 0.06f), side * hw), beam);
                Pen("edge pen", new[] { P(a0, side * hw), P(a1, side * hw) }, Ink, 1.6f);
            }
        }

        // 7. The rails: a post every 0.75 tile and at both ends, a rail along their tops — lit on the side toward the light —
        // one run of it lost when failing (by hash, the same every frame), none over a fallen span.
        if (!stone)
        {
            var posts = new List<float>();
            for (float a = 0.08f; a < len - 0.12f; a += BridgePostSpacing)
            {
                posts.Add(a);
            }

            posts.Add(len - 0.08f);
            int lostRun = worn == Condition.Failing && posts.Count > 1 ? (int)(BridgeHash(2, 23, seed) * 997f) % (posts.Count - 1) : -1;
            foreach (float side in new[] { -1f, 1f })
            {
                float c = side * (hw - 0.03f);
                Color lit = rail * FaceLight(v * side);
                for (int p = 0; p + 1 < posts.Count; p++)
                {
                    if ((p == lostRun && side > 0f) || InGap(posts[p]) || InGap(posts[p + 1]))
                    {
                        continue;
                    }

                    Fill("rail shadow", Quad(posts[p], posts[p + 1], c - 0.03f, c + 0.03f).Select(q => q + new Vector2(0.03f, 0.04f)).ToArray(), BridgeSmallShadow);
                    Fill("rail ink", Quad(posts[p], posts[p + 1], c - 0.043f, c + 0.043f), Ink);
                    Fill("rail", Quad(posts[p], posts[p + 1], c - 0.0275f, c + 0.0275f), lit with { A = 1f });
                }

                foreach (float a in posts)
                {
                    if (InGap(a))
                    {
                        continue;
                    }

                    Fill("post", Quad(a - 0.045f, a + 0.045f, c - 0.045f, c + 0.045f), beam);
                    Pen("post pen", Quad(a - 0.045f, a + 0.045f, c - 0.045f, c + 0.045f), Ink, 1.2f, closed: true);
                }
            }

            // 8. A sill across each end, where the deck meets the bank.
            foreach (float end in new[] { 0.05f, len - 0.05f })
            {
                Fill("sill ink", Quad(end - 0.055f, end + 0.055f, -hw - 0.05f, hw + 0.05f), Ink);
                Fill("sill", Quad(end - 0.04f, end + 0.04f, -hw - 0.04f, hw + 0.04f), beam);
            }
        }

        return pieces;
    }
}
