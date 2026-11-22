using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐⭐ STOCK PILES (A159, `specs/stock-piles.md`; Joe's play note (d): <i>"stockpiles show piles of each good they hold (the
/// bars stay on other buildings for now), and goods on the ground show as piles, not the coloured block"</i>, then <i>"A,
/// all as recommended"</i>). With the watercolour on, a stockpile's goods are piles in bays over its yard, and a heap on the
/// ground is its good's pile on a wash of the good's colour — each good drawn as itself (§4), six sizes by amount (§5).
/// ⛔ Nothing is drawn that the sim does not hold (`building-yards.md §2`). Tick off, the bars and the chips as before.
/// </remarks>
public partial class VillageMap
{
    /// <summary>An amount as a pile's size (§5): 0 none; 1–9, 10–29, 30–69, 70–149, 150–399, 400 + → 1–6.</summary>
    internal static byte PileLevel(int amount) => amount switch
    {
        <= 0 => 0,
        < 10 => 1,
        < 30 => 2,
        < 70 => 3,
        < 150 => 4,
        < 400 => 5,
        _ => 6,
    };

    /// <summary>
    /// A store's held goods as their sizes, in catalogue order — <c>"id.size,…"</c>, or null when it holds nothing. In the
    /// stockpile's <see cref="YardState"/>, so it is laid again only when a good arrives, runs out or changes size (§5).
    /// A string rather than packed bits, so a good a mod adds past any fixed width still has its pile.
    /// </summary>
    internal static string? PileSizesOf(Stockpile store, int goodsCount)
    {
        System.Text.StringBuilder? sizes = null;
        for (int id = 0; id < goodsCount; id++)
        {
            byte level = PileLevel(store[(Goods)id]);
            if (level > 0)
            {
                sizes ??= new System.Text.StringBuilder();
                if (sizes.Length > 0)
                {
                    sizes.Append(',');
                }

                sizes.Append(id).Append('.').Append(level);
            }
        }

        return sizes?.ToString();
    }

    /// <summary>The goods and sizes <see cref="PileSizesOf"/> wrote, in its order.</summary>
    internal static List<(Goods Goods, byte Level)> PileSizesRead(string? sizes)
    {
        var held = new List<(Goods, byte)>();
        if (string.IsNullOrEmpty(sizes))
        {
            return held;
        }

        foreach (string one in sizes.Split(','))
        {
            int dot = one.IndexOf('.');
            held.Add(((Goods)int.Parse(one[..dot], System.Globalization.CultureInfo.InvariantCulture), byte.Parse(one[(dot + 1)..], System.Globalization.CultureInfo.InvariantCulture)));
        }

        return held;
    }

    /// <summary>The most goods a stockpile draws (§6): a 3 × 3 of bays; the card lists the rest.</summary>
    internal const int MostBays = 9;

    /// <summary>
    /// ⭐ The stockpile's bays over its yard (§6), in its frame: one good the whole yard; two or three side by side in strips;
    /// four a 2 × 2; five or six 3 × 2; seven to nine 3 × 3. Each is (centre, half width, half depth). Pure — the probe asks it.
    /// </summary>
    internal static List<(Vector2 At, float Hx, float Hy)> PileBays(int goods, float hw, float hd)
    {
        var bays = new List<(Vector2, float, float)>();
        int n = Mathf.Min(goods, MostBays);
        if (n <= 0)
        {
            return bays;
        }

        int columns = n switch { <= 3 => n, 4 => 2, _ => 3 };
        int rows = (n + columns - 1) / columns;
        float bw = hw * 2f * 0.9f / columns;
        float bh = hd * 2f * 0.9f / rows;
        for (int i = 0; i < n; i++)
        {
            int c = i % columns;
            int r = i / columns;
            bays.Add((new Vector2((-hw * 0.9f) + (bw * (c + 0.5f)), (-hd * 0.9f) + (bh * (r + 0.5f))), bw / 2f, bh / 2f));
        }

        return bays;
    }

    /// <summary>The stockpile's goods in their bays (§6), laid square to its posts.</summary>
    private static void PilesOnTheYard(YardKit k, float hw, float hd, string? sizes, int seed)
    {
        List<(Goods Goods, byte Level)> held = PileSizesRead(sizes);
        List<(Vector2 At, float Hx, float Hy)> bays = PileBays(held.Count, hw, hd);
        for (int i = 0; i < bays.Count; i++)
        {
            PileOf(k, held[i].Goods, held[i].Level, bays[i].At, bays[i].Hx, bays[i].Hy, seed + (i * 101), tidy: true);
        }
    }

    /// <summary>
    /// ⭐ One good's pile in a bay (half-sizes in tiles), its size 1–6 (§4, §5): each good drawn as itself, laid along the
    /// bay's long side; tumbled a little when <paramref name="tidy"/> is false (a heap set down, §7). A good no painter names
    /// is sacks in its colour, so a new good needs no art to appear.
    /// </summary>
    private static void PileOf(YardKit k, Goods goods, int level, Vector2 at, float hx, float hy, int seed, bool tidy)
    {
        float grow = level / 6f;
        float turn = tidy ? 0f : (Hash01(seed, 1, 301) - 0.5f) * 0.8f;

        // Laid along the bay's long side: the long half is hx from here on.
        if (hy > hx * 1.15f)
        {
            turn += Mathf.Pi / 2f;
            (hx, hy) = (hy, hx);
        }

        float h = Mathf.Min(hx, hy);
        Color own = Washed(GoodsPalette.ColourOf(goods));
        int count = PileCount(level);
        switch (goods)
        {
            case Goods.Logs:
            {
                // A stack: a bottom row and a row on it, the ends showing.
                float r = Mathf.Clamp(hy * 0.95f / 5f, 0.03f, 0.07f);
                float length = Mathf.Min(hx * 1.85f, 0.66f) * (0.7f + (0.3f * grow));
                int bottom = (count + 1) / 2;
                int top = count - bottom;
                k.Shade("logs shadow", Box(at, length, bottom * 2.05f * r, turn));
                for (int i = 0; i < bottom; i++)
                {
                    Log(k, "logs", Tn(at, 0, (i - ((bottom - 1) / 2f)) * 2.05f * r, turn), turn, length, r);
                }

                for (int i = 0; i < top; i++)
                {
                    Log(k, "logs", Tn(at, 0.02f, (i - ((top - 1) / 2f)) * 2.05f * r, turn), turn, length * 0.92f, r * 0.95f);
                }

                break;
            }

            case Goods.Firewood:
            {
                // Split wood stacked in rows — a cord: longer, then deeper, with the size.
                int rowsOf = level <= 1 ? 1 : level <= 3 ? 2 : level <= 4 ? 3 : 4;
                float rowLength = Mathf.Min(hx * 1.85f, 0.66f) * Mathf.Min(1f, (level + 2) / 5f);
                float depth = Mathf.Min(0.16f, hy * 1.85f / 4f);
                k.Shade("firewood shadow", Box(at, rowLength, depth * rowsOf, turn));
                for (int r = 0; r < rowsOf; r++)
                {
                    Firewood(k, "firewood", Tn(at, 0, (r - ((rowsOf - 1) / 2f)) * depth, turn), turn, rowLength, 4);
                }

                break;
            }

            case Goods.Stone:
            {
                // A mound: the stones packed as wide as the size, the outer ones first so the middle sits on top, lighter.
                float sx = hx * (0.3f + (0.62f * grow));
                float sy = hy * (0.3f + (0.58f * grow));
                k.Shade("stone shadow", Oval(at, sx * 1.05f, sy * 1.05f, turn, 16), 0.35f);
                int stones = 3 + (count * 3);
                var placed = new List<(float D, Vector2 P, int I)>();
                for (int i = 0; i < stones; i++)
                {
                    float a = Hash01(seed, i, 51) * Mathf.Tau;
                    float d = Mathf.Sqrt(Hash01(seed, i, 52));
                    placed.Add((d, Tn(at, Mathf.Cos(a) * d * sx, Mathf.Sin(a) * d * sy, turn), i));
                }

                placed.Sort((x, y) => y.D.CompareTo(x.D));
                foreach ((float d, Vector2 p, int i) in placed)
                {
                    float s = Mathf.Clamp(h * 0.5f, 0.06f, 0.11f) * (0.75f + (0.5f * Hash01(seed, i, 53)));
                    Color grey = Washed(new Color("#a9a497")).Lightened(0.3f * (1f - d));
                    k.Thing("stone", Blob(p, s * 0.55f, s * 0.45f, Hash01(seed, i, 54) * 3f, seed + i, 0.18f, 9), grey, 0.45f);
                }

                break;
            }

            case Goods.Iron:
                Heap(k, "ore", at, h * (0.35f + (0.6f * grow)), Ore, new Color("#4f2c1e"), seed);
                break;

            case Goods.Hay:
                HayRick(k, at, h * (0.4f + (0.55f * grow)));
                break;

            case Goods.Produce or Goods.Bread:
                Grid(at, hx, hy, count, 0.17f, seed, (p, s) => Basket(k, p, own, s));
                break;

            case Goods.Fish:
                // Creels of fish, laid head to tail.
                Grid(at, hx, hy, count, 0.2f, seed, (p, s) =>
                {
                    k.Thing("creel", Box(p, 0.18f, 0.13f, turn), Wicker, 0.8f);
                    for (int f = 0; f < 3; f++)
                    {
                        Vector2 c = Tn(p, 0f, (f - 1) * 0.038f, turn);
                        float flip = (f & 1) == 1 ? Mathf.Pi : 0f;
                        k.Thing("fish", Oval(c, 0.07f, 0.018f, turn + flip, 10), Washed(FishSilver.Lerp(GoodsPalette.ColourOf(goods), 0.35f)), 0.6f);
                        k.Fill("fish tail", new[] { Tn(c, 0.06f, 0f, turn + flip), Tn(c, 0.085f, -0.018f, turn + flip), Tn(c, 0.085f, 0.018f, turn + flip) }, TimberDark with { A = 0.6f });
                    }
                });
                break;

            case Goods.Meat:
                // Joints: a ham each, the bone's knuckle pale at the narrow end.
                Grid(at, hx, hy, count, 0.17f, seed, (p, s) =>
                {
                    float a = turn + ((Hash01(s, 3, 305) - 0.5f) * 1.2f);
                    k.Thing("joint", Oval(p, 0.07f, 0.05f, a, 12), Washed(MeatRed), 0.8f);
                    k.Fill("joint fat", Oval(Tn(p, -0.015f, -0.012f, a), 0.04f, 0.025f, a, 10), new Color("#e8c9b0") with { A = 0.45f });
                    k.Thing("bone", Oval(Tn(p, 0.075f, 0f, a), 0.02f, 0.016f, a, 8), new Color("#efe3cc"), 0.6f);
                });
                break;

            case Goods.Wheat or Goods.Flour:
                Grid(at, hx, hy, count, 0.16f, seed, (p, s) => Sack(k, goods.ToString(), p, turn + ((Hash01(s, 2, 302) - 0.5f) * 0.6f), own));
                break;

            case Goods.Leather:
            {
                // Hides laid over one another, the stack spreading with the size.
                float rx = Mathf.Min(hx * 0.55f, 0.12f);
                float ry = Mathf.Min(hy * 0.5f, 0.09f);
                for (int i = 0; i < count; i++)
                {
                    Vector2 p = Tn(at, ((i % 3) - 1) * rx * 0.6f * Mathf.Min(1, count - 1), ((i / 3) - ((count - 1) / 6f)) * ry * 0.7f, turn);
                    k.Thing("hides", Blob(p, rx, ry, turn + ((Hash01(seed, i, 306) - 0.5f) * 0.6f), seed + i, 0.22f, 12), own.Lightened(0.06f * (i % 2)), 0.75f);
                }

                break;
            }

            case Goods.Clothing:
                Grid(at, hx, hy, count, 0.19f, seed, (p, s) =>
                {
                    // A bundle of cloth folded and tied: the folds in pen, a cord across.
                    k.Thing("cloth", Box(p, 0.16f, 0.12f, turn), own, 0.8f);
                    k.Strokes("cloth", new List<Vector2> { Tn(p, -0.08f, -0.02f, turn), Tn(p, 0.08f, -0.02f, turn), Tn(p, -0.08f, 0.025f, turn), Tn(p, 0.08f, 0.025f, turn) }, Ink with { A = 0.35f });
                    k.Line("cloth cord", Tn(p, 0.02f, -0.06f, turn), Tn(p, 0.02f, 0.06f, turn), TimberDark with { A = 0.8f }, 1.5f);
                });
                break;

            case Goods.Tools or Goods.IronTools:
                Grid(at, hx, hy, count, 0.19f, seed, (p, s) =>
                {
                    // An open crate, the handles standing out of it; the iron tools' heads dark metal, the stone tools' grey.
                    k.Thing("tools", Box(p, 0.16f, 0.16f, turn), TimberLight, 0.85f);
                    k.Fill("tools", Box(p, 0.12f, 0.12f, turn), TimberDark with { A = 0.55f });
                    k.Line("tool", Tn(p, -0.05f, 0.04f, turn), Tn(p, 0.05f, -0.06f, turn), Timber, 2f);
                    k.Line("tool", Tn(p, -0.04f, -0.05f, turn), Tn(p, 0.06f, 0.03f, turn), Timber, 2f);
                    Color head = goods == Goods.IronTools ? new Color("#5a5f66") : Washed(new Color("#b9b4a8"));
                    k.Thing("tool head", Box(Tn(p, 0.05f, -0.06f, turn), 0.05f, 0.03f, turn + 0.8f), head, 0.7f);
                    k.Thing("tool head", Box(Tn(p, 0.06f, 0.03f, turn), 0.03f, 0.05f, turn + 0.8f), head, 0.7f);
                });
                break;

            case Goods.Handcart:
                // The carts themselves, two at most.
                for (int i = 0; i < Mathf.Min(count, 2); i++)
                {
                    Handcart(k, "cart", Tn(at, 0, (i - 0.5f) * 0.25f * (count > 1 ? 1 : 0), turn), turn, Mathf.Min(1f, hx * 2.2f));
                }

                break;

            default:
                Grid(at, hx, hy, count, 0.16f, seed, (p, s) => Sack(k, goods.ToString(), p, turn, own));
                break;
        }
    }

    /// <summary>How many things a pile of a size is (§5): 1, 2, 3, 5, 7, 9.</summary>
    internal static int PileCount(int level) => level switch { <= 0 => 0, 1 => 1, 2 => 2, 3 => 3, 4 => 5, 5 => 7, _ => 9 };

    /// <summary>Up to <paramref name="count"/> things set out in a little grid inside a bay, centred.</summary>
    private static void Grid(Vector2 at, float hx, float hy, int count, float pitch, int seed, Action<Vector2, int> thing)
    {
        int across = Mathf.Max(1, Mathf.FloorToInt(hx * 2f / pitch));
        int down = Mathf.Max(1, Mathf.FloorToInt(hy * 2f / pitch));
        int n = Mathf.Min(count, across * down);
        int cols = Mathf.Min(across, Mathf.CeilToInt(Mathf.Sqrt(n)));
        int rows = (n + cols - 1) / cols;
        float s = Mathf.Min(1f, Mathf.Min(hx * 2f / (cols * pitch), hy * 2f / (rows * pitch)));
        for (int i = 0; i < n; i++)
        {
            var p = at + new Vector2(((i % cols) - ((cols - 1) / 2f)) * pitch * s, ((i / cols) - ((rows - 1) / 2f)) * pitch * s);
            p += new Vector2((Hash01(seed, i, 303) - 0.5f) * 0.02f, (Hash01(seed, i, 304) - 0.5f) * 0.02f);
            thing(p, seed + i);
        }
    }

    // ------------------------------------------------------------------ goods on the ground (§7)

    /// <summary>A ground heap's drawn width in tiles at a size (§7): about a third of a tile at the first, two thirds at the sixth.</summary>
    internal static float HeapWidthOf(int level) => 2.1f * (0.12f + (0.035f * level));

    /// <summary>Narrower than this on screen a heap is today's chip (§7): a pile of a few pixels is a smear.</summary>
    private const float SmallestPilePixels = 7f;

    /// <summary>A ground heap's pieces, centred on <paramref name="centre"/> — pure, so the probe reads what the draw lays.</summary>
    internal static List<RoofPiece> HeapDrawing(Goods goods, int level, int seed, Vector2 centre)
    {
        var pieces = new List<RoofPiece>();
        var kit = new YardKit(pieces, new RoofFrame(centre, 0, 1f));
        float h = HeapWidthOf(level) / 2.1f;

        // The spill under the things, in the good's own colour — the chip's colour, so a heap still reads at a glance (call 2).
        kit.Thing("heap spill", Blob(Vector2.Zero, h * 1.05f, h * 0.9f, 0f, seed, 0.14f), Washed(GoodsPalette.ColourOf(goods)) with { A = 0.6f }, 0.7f);
        PileOf(kit, goods, level, Vector2.Zero, h * 0.9f, h * 0.78f, seed, tidy: false);
        return pieces;
    }

    /// <summary>A heap's laid runs and when it was last drawn.</summary>
    private sealed class HeapCache
    {
        public required List<RoofRun> Runs { get; init; }

        public ulong LastSeen { get; set; }
    }

    /// <summary>The ground heaps' meshes, one entry a heap, keyed by where it lies, its good and its size (CLAUDE.md: laid once).</summary>
    private readonly Dictionary<(Vector2 At, Goods Goods, byte Level), HeapCache> _heapCache = new();

    /// <summary>How many times a heap's pieces were laid (the probe asks; never hashed).</summary>
    internal int HeapLayings { get; private set; }

    /// <summary>
    /// ⭐ The ground heaps as piles (§7): at each chip's place, on the wash of its colour, tumbled, as big as its size; today's
    /// chip where it would be narrower than <see cref="SmallestPilePixels"/>. Laid once per heap and size, never per frame.
    /// </summary>
    private void DrawPiledHeaps()
    {
        ulong frame = Engine.GetProcessFrames();
        Transform2D toScreen = TileToScreen();
        Rid canvas = GetCanvasItem();
        foreach ((GroundStack heap, Rect2 box) in EveryHeapChip())
        {
            if (box.Size.X < SmallestPilePixels)
            {
                DrawRect(box, GoodsPalette.ColourOf(heap.Goods));
                DrawRect(box, HeapEdge, filled: false, width: 2f);
                continue;
            }

            HeapCache held = CachedHeap(heap, box);
            held.LastSeen = frame;
            for (int i = 0; i < held.Runs.Count; i++)
            {
                RoofRun run = held.Runs[i];
                if (run.Lines is { } lines && run.LinePpt != _pixelsPerTile)
                {
                    run = held.Runs[i] = RelaidForTheZoom(run, lines, _pixelsPerTile);
                }

                if (run.Mesh.GetSurfaceCount() > 0)
                {
                    RenderingServer.CanvasItemAddMesh(canvas, run.Mesh.GetRid(), toScreen, Colors.White);
                }
            }
        }

        // A heap hauled away, or grown a size, is forgotten a few frames later.
        if (_heapCache.Count > 0 && frame % 30 == 0)
        {
            foreach ((Vector2, Goods, byte) gone in _heapCache.Where(e => e.Value.LastSeen + 2 < frame).Select(e => e.Key).ToList())
            {
                _heapCache.Remove(gone);
            }
        }
    }

    /// <summary>A heap's runs — the kept ones while it lies where it lay at the size it was, laid (and counted) otherwise.</summary>
    private HeapCache CachedHeap(GroundStack heap, Rect2 box)
    {
        byte level = PileLevel(heap.Amount);
        Vector2 at = RoofRounded(((box.GetCenter() - (Size / 2f)) / _pixelsPerTile) + _centreTile);
        (Vector2, Goods, byte) key = (at, heap.Goods, level);
        if (!_heapCache.TryGetValue(key, out HeapCache? held))
        {
            HeapLayings++;
            int seed = (heap.Position.X * 7919) + (heap.Position.Y * 104729) + (int)heap.Goods;
            held = new HeapCache { Runs = RoofRuns(HeapDrawing(heap.Goods, level, seed, at), _pixelsPerTile) };
            _heapCache[key] = held;
        }

        return held;
    }
}
