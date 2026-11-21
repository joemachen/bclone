using Bclone.Sim.World;

namespace Bclone.Game;

/// <summary>Which way a painted person is seen: toward the viewer, walking away, or from the side.</summary>
internal enum FigurePose
{
    Front,
    Back,
    Side,
}

/// <summary>What a painted person's hands are doing.</summary>
internal enum FigureHold
{
    /// <summary>At the sides, swinging if walking.</summary>
    None,

    /// <summary>The trade's tool held out.</summary>
    Tool,

    /// <summary>Both hands in front, carrying.</summary>
    Front,

    /// <summary>Arms hugged in against the cold.</summary>
    Hug,

    /// <summary>One palm raised, speaking — the manuscripts' gesture.</summary>
    Talk,
}

/// <summary>
/// Everything a painted person's picture depends on — and nothing else, so a figure is painted once for each of these
/// and kept (CLAUDE.md: nothing derivable rebuilt per frame).
/// </summary>
/// <param name="Id">The villager: their skin, hair, coif and colours fall from it, so a person keeps their look.</param>
/// <param name="Stage">Child, adult or elder.</param>
/// <param name="Trade">Their trade, or null for a labourer.</param>
/// <param name="Pose">Which way they are seen.</param>
/// <param name="Flip">Seen from the side, walking left.</param>
/// <param name="Hold">What their hands do.</param>
/// <param name="Walking">Mid-stride.</param>
/// <param name="ToolOut">The tool (or an elder's stick) in hand.</param>
/// <param name="Good">The good they carry most of, if any.</param>
/// <param name="Coat">In a coat (the cloak).</param>
/// <param name="Cart">Pushing the household's handcart.</param>
/// <param name="Cold">Going in out of the cold.</param>
/// <param name="Beat">
/// Which pose of a motion (A130, <c>villager-animation.md</c>, look B): 0 is the still picture A93 painted (a stride,
/// the palm raised, the tool held) — except a walker toward or away, whose feet, hem and hands step both ways since
/// Joe's play; walking, 1 is the other step; talking, 1 is the palm lowered; at work with a tool
/// that swings, 1 is the tool raised and 2 the stroke.
/// </param>
/// <param name="Dye">The cloak's dye, the household's (A143, <see cref="PersonPainter.CloakDyes"/>).</param>
internal readonly record struct FigureKey(
    int Id, LifeStage Stage, JobKind? Trade, FigurePose Pose, bool Flip, FigureHold Hold, bool Walking, bool ToolOut,
    Goods? Good, bool Coat, bool Cart, bool Cold, int Beat = 0, int Dye = 0);

/// <summary>A painted person: straight RGBA bytes, and where on them the feet stand.</summary>
internal sealed record PaintedFigure(int Width, int Height, byte[] Rgba, float FeetX, float FeetY);

/// <summary>
/// ⭐⭐ THE VILLAGERS PAINTED AS PEOPLE (A93, <c>specs/villager-art.md §3b</c>; Joe's pick, <i>"Yes, build E"</i>). A person
/// standing up on the top-down map, in the manner of a medieval illuminated manuscript: a long A-line gown in the trade's
/// colour pushed toward its pigment, sleeves of a second pigment on half of everyone, inked outlines and fold lines, a
/// rolled curly bob or a linen coif or a tailed hood or a red cap, pink cheeks, hose and pointed shoes, the tool in hand
/// at work, the load on the shoulder or in the arms, the coat a cloak with a pale fur collar, a soft shadow to the lower
/// right.
/// </summary>
/// <remarks>
/// <para>
/// <b>A port of the mock's painter</b> (<c>D:\Projects\brushes\villagers\illum.py</c>, outside git): the same parts in the
/// same order, in the same units — <c>h = 1</c> is the figure's height, the feet at <c>y = 0</c>, the crown at
/// <c>y = -1</c>, <c>x</c> to the right. A change to how a person looks is made there first and mocked on Joe's saves.
/// </para>
/// <para>
/// <b>Godot-free on purpose</b>: it fills polygons into a supersampled buffer of its own and hands back bytes, so the map
/// only turns them into a texture. ⚠️ <b>No sex is drawn — the sim has none</b> (<c>SimConfig</c>: <i>"villagers have
/// names and no sex"</i>); coifs, curls, gown lengths and colours fall by the id for everyone.
/// </para>
/// </remarks>
internal static class PersonPainter
{
    /// <summary>An adult is painted this many pixels tall; the map scales the picture to the zoom (with mipmaps).</summary>
    internal const int BakeHeight = 64;

    /// <summary>The supersample inside a figure's canvas.</summary>
    internal const int Supersample = 4;

    /// <summary>The canvas, in figure units: this wide and this tall, the feet this far in from its top left.</summary>
    internal const float CanvasW = 1.7f, CanvasH = 1.55f, FeetFromLeft = 0.85f, FeetFromTop = 1.32f;

    // ------------------------------------------------------------------ colours (sRGB bytes)
    internal readonly record struct Rgb(byte R, byte G, byte B)
    {
        internal static Rgb Hex(string hex)
        {
            int v = Convert.ToInt32(hex.TrimStart('#'), 16);
            return new Rgb((byte)(v >> 16), (byte)((v >> 8) & 0xff), (byte)(v & 0xff));
        }

        internal static Rgb F(float r, float g, float b) => new(Byte(r * 255f), Byte(g * 255f), Byte(b * 255f));

        internal Rgb Shade(float k) => new(Byte(R * k), Byte(G * k), Byte(B * k));

        internal Rgb Mix(Rgb o, float t) => new(Byte((R * (1 - t)) + (o.R * t)), Byte((G * (1 - t)) + (o.G * t)), Byte((B * (1 - t)) + (o.B * t)));

        private static byte Byte(float v) => (byte)Math.Clamp((int)MathF.Round(v), 0, 255);
    }

    internal static readonly Rgb Ink = Rgb.Hex("#2e221a");
    private static readonly Rgb Vermilion = Rgb.Hex("#c4432c");
    private static readonly Rgb Linen = Rgb.Hex("#f0ebdf");
    /// <summary>
    /// ⭐ The cloak's dyes (A143, <c>specs/villager-art.md §9</c>; Joe: <i>"clothes should be available in different colors so
    /// villagers aren't all green"</i> → <i>"Yes, as recommended"</i>): seven a valley could make, muted to sit in the wash —
    /// madder, woad, weld, moss (A93's one cloak), a dull purple, walnut, undyed — in this order so that households founded one
    /// after another never share a dye (<see cref="VillageMap.CloakDyeOf"/>).
    /// </summary>
    internal static readonly Rgb[] CloakDyes = Hexes("#9a4a3c", "#4a6488", "#b8a050", "#56703f", "#6c5070", "#6e5238", "#9a948a");

    /// <summary>A dye's cloak colour; any number wraps round the seven.</summary>
    internal static Rgb CloakOf(int dye) => CloakDyes[((dye % CloakDyes.Length) + CloakDyes.Length) % CloakDyes.Length];
    private static readonly Rgb Fur = Rgb.Hex("#ece4d0");
    private static readonly Rgb ElderHood = Rgb.Hex("#7a5a40");
    private static readonly Rgb Shoe = Rgb.Hex("#26201c");
    private static readonly Rgb Belt = Rgb.Hex("#3e2a1e");
    private static readonly Rgb Wood = Rgb.Hex("#8a6038");
    private static readonly Rgb Iron = Rgb.Hex("#6e727a");
    private static readonly Rgb Cheek = Rgb.Hex("#d8706a");
    private static readonly Rgb Shiver = Rgb.Hex("#3a6ea8");
    private static readonly Rgb Basket = Rgb.Hex("#a8804a");

    private static readonly Rgb[] Pigments = Hexes("#c4432c", "#33529e", "#3f8458", "#c87a86", "#c99a3c", "#e6dcc4", "#8e2e3c", "#5f7aa8");
    private static readonly Rgb[] Skins = Hexes("#f2d6b8", "#ecc9a6", "#dcb28c", "#c69468", "#a06e48", "#7c5236");
    private static readonly Rgb[] Hairs = Hexes("#d2aa5c", "#b07a3a", "#a0522d", "#c06a30", "#5a3a22", "#3a2a1c", "#2a2220", "#e0c070");
    private static readonly Rgb[] Greys = Hexes("#d6d0c6", "#bcb6aa", "#ece8e0");
    private static readonly Rgb[] Hoses = Hexes("#8c8c92", "#70707a", "#5e6478", "#7a6a5a", "#9a4a3a", "#4a4a52");
    private static readonly Rgb[] ChildCloth = Hexes("#3f6aa8", "#c4562c", "#4f8a5a", "#c88a3c", "#b06a7a");

    private enum Head
    {
        Bare,
        Coif,
        CoifCap,
        Liripipe,
        Kerchief,
        RedCap,
        Straw,
        Bucket,
        Felt,
        Chaperon,
        Scholar,
    }

    private enum Tool
    {
        None,
        Fork,
        Spade,
        Axe,
        Hammer,
        Rod,
        Bow,
        Pick,
        Crook,
        Peel,
        Saw,
        Book,
        Basket,
        Stick,
    }

    private enum Length
    {
        Ankle,
        Calf,
        Knee,
    }

    private readonly record struct TradeLook(Rgb Colour, Head Head, Tool Tool, Length Length, Rgb? Apron = null, Rgb? Hood = null);

    /// <summary>
    /// The trades as the mocks drew them (<c>villager-art.md §3b</c>): <see cref="TradeGlyph.ColourOf"/>'s colour (the
    /// gown's, pushed toward its pigment), the head, the tool, the gown's length. ⚠️ The teacher's colour is held here
    /// (A90 call 4) until the systems lane gives the trade one of its own; the farmer's is lifted from the glyph's near-black
    /// so the gown has folds to show.
    /// </summary>
    private static TradeLook LookOf(JobKind? trade) => trade switch
    {
        JobKind.Forager => new(Rgb.F(0.82f, 0.35f, 0.38f), Head.Coif, Tool.Basket, Length.Calf),
        JobKind.Forester => new(Rgb.Hex("#2f6b3a"), Head.Liripipe, Tool.Spade, Length.Knee, Hood: Rgb.Hex("#3f7a44")),
        JobKind.Woodcutter => new(Rgb.F(0.88f, 0.55f, 0.24f), Head.Bare, Tool.Axe, Length.Knee),
        JobKind.Marketer => new(Rgb.Hex("#c98f4a"), Head.Kerchief, Tool.Basket, Length.Calf),
        JobKind.Builder => new(Rgb.F(0.72f, 0.76f, 0.82f), Head.RedCap, Tool.Hammer, Length.Knee),
        JobKind.Farmer => new(Rgb.Hex("#6a5038"), Head.Straw, Tool.Fork, Length.Knee),
        JobKind.Fisher => new(Rgb.Hex("#2f5f7a"), Head.Bucket, Tool.Rod, Length.Knee),
        JobKind.Hunter => new(Rgb.Hex("#6a7a3f"), Head.Liripipe, Tool.Bow, Length.Knee, Hood: Rgb.Hex("#5a7a3a")),
        JobKind.Smith => new(Rgb.F(0.42f, 0.44f, 0.50f), Head.Bare, Tool.Hammer, Length.Knee, Apron: Rgb.Hex("#6a4a34")),
        JobKind.Quarrier => new(Rgb.F(0.66f, 0.62f, 0.54f), Head.CoifCap, Tool.Pick, Length.Knee),
        JobKind.Miner => new(Rgb.F(0.64f, 0.40f, 0.28f), Head.CoifCap, Tool.Pick, Length.Knee),
        JobKind.Miller => new(Rgb.F(0.86f, 0.82f, 0.70f), Head.Coif, Tool.None, Length.Calf, Apron: Linen),
        JobKind.Baker => new(Rgb.F(0.72f, 0.48f, 0.24f), Head.Coif, Tool.Peel, Length.Calf, Apron: Linen),
        JobKind.Herdsman => new(Rgb.Hex("#8a4b32"), Head.Felt, Tool.Crook, Length.Calf),
        JobKind.Merchant => new(Rgb.Hex("#5f6f8c"), Head.Chaperon, Tool.None, Length.Ankle),
        JobKind.Tailor => new(Rgb.F(0.40f, 0.52f, 0.34f), Head.Bare, Tool.None, Length.Ankle),
        JobKind.Carpenter => new(Rgb.F(0.78f, 0.60f, 0.38f), Head.RedCap, Tool.Saw, Length.Knee),
        JobKind.Teacher => new(Rgb.Hex("#4a5ea8"), Head.Scholar, Tool.Book, Length.Ankle),
        _ => new(Rgb.Hex("#a89a80"), Head.Bare, Tool.None, Length.Knee),
    };

    /// <summary>A good's colour in a person's hands (<see cref="GoodsPalette"/>'s, as pigments).</summary>
    private static Rgb GoodColour(Goods good) => good switch
    {
        Goods.Produce => Rgb.Hex("#c8453e"),
        Goods.Logs => Rgb.Hex("#7a5a38"),
        Goods.Firewood => Rgb.Hex("#9a7448"),
        Goods.Stone => Rgb.Hex("#a8a8a6"),
        Goods.Tools => Rgb.F(0.72f, 0.76f, 0.82f),
        Goods.Iron => Rgb.Hex("#8a5a48"),
        Goods.Fish => Rgb.Hex("#8cb0c8"),
        Goods.Meat => Rgb.Hex("#a8343a"),
        Goods.Leather => Rgb.Hex("#9a7048"),
        Goods.Wheat => Rgb.Hex("#dcb84e"),
        Goods.IronTools => Rgb.F(0.50f, 0.52f, 0.58f),
        Goods.Flour => Linen,
        Goods.Bread => Rgb.Hex("#b87a3a"),
        Goods.Hay => Rgb.Hex("#ccbc6a"),
        Goods.Clothing => Rgb.Hex("#56703f"),
        _ => Rgb.Hex("#b4b4b4"),
    };

    /// <summary>Carried on the shoulder or the back, leaving the hands free.</summary>
    internal static bool OnTheShoulder(Goods good) => good is Goods.Logs or Goods.Firewood or Goods.Wheat or Goods.Hay
        or Goods.Flour or Goods.Meat or Goods.Tools or Goods.IronTools;

    /// <summary>Whether a trade has a tool to hold out at work.</summary>
    internal static bool HasATool(JobKind? trade, LifeStage stage) =>
        stage == LifeStage.Adult && LookOf(trade).Tool != Tool.None;

    // ------------------------------------------------------------------ who: the person, from their id
    private sealed class Who
    {
        internal Rgb Skin, Hair, Hose, Gown, Sleeve;
        internal Head Head;
        internal Tool Tool;
        internal Length Length;
        internal Rgb? Apron, Hood;
        internal float K = 1f, HeadK = 1f, Stoop, Sway;
        internal bool CoatOn, CartOn, Cold, Elder;

        /// <summary>The cloak's colour — the household's dye (A143).</summary>
        internal Rgb Cloak;
        internal Goods? Good;

        internal Who(FigureKey f)
        {
            int i = f.Id;
            TradeLook look = LookOf(f.Trade);
            Skin = Pick(i, "skin", Skins);
            Hair = Pick(i, "hair", Hairs);
            Hose = Pick(i, "hose", Hoses);
            Gown = Pigment(look.Colour);
            Sleeve = Hash01(i, "kirtle") < 0.5f ? Pick(i, "sleeve", Pigments) : Gown.Shade(0.86f);
            if (Sleeve == Gown)
            {
                Sleeve = Gown.Shade(0.86f);
            }

            Length = look.Length == Length.Knee && Hash01(i, "len") < 0.3f ? Length.Calf : look.Length;
            Head = look.Head;
            Tool = look.Tool;
            if (Head == Head.Bare)
            {
                float r = Hash01(i, "head");
                Head = r < 0.22f ? Head.Coif : r < 0.34f ? Head.RedCap : Head.Bare;
            }

            Apron = look.Apron;
            Hood = look.Hood;
            Sway = (Hash01(i, "sway") - 0.5f) * 0.04f;
            if (f.Stage == LifeStage.Child)
            {
                K = 0.62f;
                HeadK = 1.28f;
                Gown = Pick(i, "childcloth", ChildCloth);
                Sleeve = Gown.Shade(0.86f);
                Head = Head.Bare;
                Tool = Tool.None;
                Apron = null;
                Hood = null;
                Length = Length.Knee;
            }
            else if (f.Stage == LifeStage.Elder)
            {
                Elder = true;
                K = 0.94f;
                Stoop = 0.05f;
                Hair = Pick(i, "grey", Greys);
                Head = Head.Liripipe;
                Hood = ElderHood;
                Tool = Tool.Stick;
                Gown = Gown.Mix(Rgb.Hex("#6a5a4e"), 0.40f);
                Sleeve = Gown.Shade(0.86f);
                Length = Length.Ankle;
            }

            CoatOn = f.Coat;
            Cloak = CloakOf(f.Dye);
            CartOn = f.Cart;
            Cold = f.Cold;
            Good = f.Good;
        }
    }

    // ------------------------------------------------------------------ the canvas
    private sealed class Canvas
    {
        internal readonly int W, H;
        internal readonly float S;
        internal readonly uint[] Px; // 0xAABBGGRR, opaque or empty
        internal readonly float InkW;
        internal readonly bool Detail, Face;
        internal bool Flip;

        /// <summary>While set, every point is turned this many degrees (clockwise on screen) about a point first — a tool swung (A130).</summary>
        internal (float Cx, float Cy, float Cos, float Sin)? Turn;

        internal Canvas(float pxH)
        {
            S = pxH * Supersample;
            W = (int)(CanvasW * S) + 2;
            H = (int)(CanvasH * S) + 2;
            Px = new uint[W * H];
            InkW = MathF.Max(1f, MathF.Round(Supersample * Math.Clamp(pxH / 70f, 0.3f, 0.75f)));
            Detail = pxH >= 24f;
            Face = pxH >= 30f;
        }

        private (float X, float Y) P(float x, float y)
        {
            if (Turn is (float cx, float cy, float cos, float sin))
            {
                float dx = x - cx, dy = y - cy;
                x = cx + (dx * cos) - (dy * sin);
                y = cy + (dx * sin) + (dy * cos);
            }

            return ((FeetFromLeft + (Flip ? -x : x)) * S, (FeetFromTop + y) * S);
        }

        internal void TurnAbout((float X, float Y) about, float degrees)
        {
            float a = degrees * MathF.PI / 180f;
            Turn = (about.X, about.Y, MathF.Cos(a), MathF.Sin(a));
        }

        /// <summary>Fills a polygon given in figure units, even-odd, sampling at pixel centres.</summary>
        internal void Fill(ReadOnlySpan<(float X, float Y)> pts, Rgb c)
        {
            int n = pts.Length;
            if (n < 3)
            {
                return;
            }

            Span<float> xs = n <= 64 ? stackalloc float[n] : new float[n];
            Span<float> ys = n <= 64 ? stackalloc float[n] : new float[n];
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int k = 0; k < n; k++)
            {
                (xs[k], ys[k]) = P(pts[k].X, pts[k].Y);
                minY = MathF.Min(minY, ys[k]);
                maxY = MathF.Max(maxY, ys[k]);
            }

            uint colour = 0xff000000u | ((uint)c.B << 16) | ((uint)c.G << 8) | c.R;
            int y0 = Math.Max(0, (int)MathF.Floor(minY));
            int y1 = Math.Min(H - 1, (int)MathF.Ceiling(maxY));
            Span<float> cross = n <= 64 ? stackalloc float[n] : new float[n];
            for (int y = y0; y <= y1; y++)
            {
                float sy = y + 0.5f;
                int m = 0;
                for (int k = 0, j = n - 1; k < n; j = k++)
                {
                    float ya = ys[j], yb = ys[k];
                    if ((ya <= sy && yb > sy) || (yb <= sy && ya > sy))
                    {
                        cross[m++] = xs[j] + ((sy - ya) / (yb - ya) * (xs[k] - xs[j]));
                    }
                }

                if (m < 2)
                {
                    continue;
                }

                cross[..m].Sort();
                for (int a = 0; a + 1 < m; a += 2)
                {
                    int xa = Math.Max(0, (int)MathF.Ceiling(cross[a] - 0.5f));
                    int xb = Math.Min(W - 1, (int)MathF.Floor(cross[a + 1] - 0.5f));
                    int row = y * W;
                    for (int x = xa; x <= xb; x++)
                    {
                        Px[row + x] = colour;
                    }
                }
            }
        }

        /// <summary>A filled shape with its ink round the edge — every shape in a manuscript is outlined.</summary>
        internal void Poly(ReadOnlySpan<(float X, float Y)> pts, Rgb c, bool ink = true)
        {
            Fill(pts, c);
            if (!ink)
            {
                return;
            }

            // the pen round the edge: a quad along each side and one round joint at each corner
            float w = InkW / S;
            for (int k = 0; k < pts.Length; k++)
            {
                Quad(pts[k], pts[(k + 1) % pts.Length], w, Ink);
                Dot(pts[k].X, pts[k].Y, w / 2f, Ink);
            }
        }

        internal void Ell(float x, float y, float rx, float ry, Rgb c, int n = 32, bool ink = true)
        {
            Span<(float, float)> pts = stackalloc (float, float)[n];
            for (int k = 0; k < n; k++)
            {
                float t = MathF.Tau * k / n;
                pts[k] = (x + (rx * MathF.Cos(t)), y + (ry * MathF.Sin(t)));
            }

            Poly(pts, c, ink);
        }

        /// <summary>A straight piece of pen, as a quad and round ends; the width in figure units.</summary>
        internal void Segment((float X, float Y) a, (float X, float Y) b, float w, Rgb c)
        {
            Quad(a, b, w, c);
            Dot(a.X, a.Y, w / 2f, c);
            Dot(b.X, b.Y, w / 2f, c);
        }

        /// <summary>A straight band of width <paramref name="w"/> from a to b, square-ended.</summary>
        private void Quad((float X, float Y) a, (float X, float Y) b, float w, Rgb c)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float len = MathF.Sqrt((dx * dx) + (dy * dy));
            if (len < 1e-6f)
            {
                return;
            }

            float h = w / 2f;
            float nx = -dy / len * h, ny = dx / len * h;
            Span<(float, float)> q = stackalloc (float, float)[4];
            q[0] = (a.X + nx, a.Y + ny);
            q[1] = (b.X + nx, b.Y + ny);
            q[2] = (b.X - nx, b.Y - ny);
            q[3] = (a.X - nx, a.Y - ny);
            Fill(q, c);
        }

        private void Dot(float x, float y, float r, Rgb c)
        {
            if (r * S < 0.75f)
            {
                return;
            }

            Span<(float, float)> pts = stackalloc (float, float)[10];
            for (int k = 0; k < 10; k++)
            {
                float t = MathF.Tau * k / 10;
                pts[k] = (x + (r * MathF.Cos(t)), y + (r * MathF.Sin(t)));
            }

            Fill(pts, c);
        }

        /// <summary>A pen line through points, inked round unless told not to; round joints.</summary>
        internal void Line(ReadOnlySpan<(float X, float Y)> pts, Rgb c, float w, bool ink = true)
        {
            if (ink)
            {
                float wi = w + (2f * InkW / S);
                for (int k = 0; k + 1 < pts.Length; k++)
                {
                    Segment(pts[k], pts[k + 1], wi, Ink);
                }
            }

            for (int k = 0; k + 1 < pts.Length; k++)
            {
                Segment(pts[k], pts[k + 1], w, c);
            }
        }

        internal void Limb((float, float) a, (float, float) b, float w, Rgb c, bool ink = true) => Line(new[] { a, b }, c, w, ink);

        /// <summary>A bare pen stroke — fold lines, strings: no ink round it, no round ends.</summary>
        internal void Stroke((float X, float Y) a, (float X, float Y) b, Rgb c, float w)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float len = MathF.Sqrt((dx * dx) + (dy * dy));
            if (len < 1e-6f)
            {
                return;
            }

            Quad(a, b, MathF.Max(w, 0.9f / S), c);
        }

        internal void Arc(float cx, float cy, float r, float a0, float a1, Rgb c, float w, bool ink = true, int n = 16)
        {
            var pts = new (float, float)[n + 1];
            for (int k = 0; k <= n; k++)
            {
                float a = (a0 + ((a1 - a0) * k / n)) * MathF.PI / 180f;
                pts[k] = (cx + (r * MathF.Cos(a)), cy + (r * MathF.Sin(a)));
            }

            Line(pts, c, w, ink);
        }
    }

    // ------------------------------------------------------------------ the parts (illum.py, in order)
    private static readonly Dictionary<Length, float> Hem = new() { [Length.Ankle] = -0.05f, [Length.Calf] = -0.15f, [Length.Knee] = -0.29f };

    private static void Legs(Canvas c, Who w, FigurePose pose, bool walking, int beat)
    {
        const float hip = -0.42f, lw = 0.068f;
        if (pose == FigurePose.Side)
        {
            if (walking && beat == 1)
            {
                // the passing step (A130): the far leg straight under, the near knee lifted, its foot off the ground
                c.Limb((0f, hip), (0f, -0.03f), lw, w.Hose.Shade(0.85f));
                c.Poly(new[] { (-0.05f, -0.05f), (0.01f, -0.05f), (0.09f, -0.01f), (-0.05f, 0f) }, Shoe);
                c.Limb((0f, hip), (0.07f, -0.22f), lw, w.Hose);
                c.Limb((0.07f, -0.22f), (0.02f, -0.10f), lw, w.Hose);
                c.Poly(new[] { (-0.01f, -0.12f), (0.05f, -0.12f), (0.13f, -0.08f), (-0.01f, -0.07f) }, Shoe);
            }
            else if (walking)
            {
                c.Limb((0f, hip), (0.12f, -0.03f), lw, w.Hose.Shade(0.85f));
                c.Limb((0f, hip), (-0.10f, -0.03f), lw, w.Hose);
                c.Poly(new[] { (0.09f, -0.05f), (0.15f, -0.05f), (0.23f, -0.01f), (0.09f, 0f) }, Shoe);
                c.Poly(new[] { (-0.13f, -0.05f), (-0.07f, -0.05f), (0.01f, -0.01f), (-0.13f, 0f) }, Shoe);
            }
            else
            {
                c.Limb((0f, hip), (0f, -0.03f), lw, w.Hose);
                c.Poly(new[] { (-0.04f, -0.05f), (0.03f, -0.05f), (0.11f, -0.01f), (-0.04f, 0f) }, Shoe);
            }

            return;
        }

        // Toward or away: one foot lifted mid-step, the left (beat 0) or the right (beat 1). ⚠️ A93's lift was 0.03 and
        // the other step changed 19 screen pixels at the closest zoom (6 in a long gown, 4 for an elder) against 50–107 from
        // the side — Joe saw *"their legs look stationary"* (A130) — so the step is higher and a long gown's shoes alternate.
        if (w.Length == Length.Ankle)
        {
            float l = walking && beat == 0 ? 0.04f : 0f, r = walking && beat == 1 ? 0.04f : 0f;
            c.Ell(-0.055f, -0.015f - l, 0.05f, 0.026f, Shoe);
            c.Ell(0.055f, -0.015f - r, 0.05f, 0.026f, Shoe);
            return;
        }

        const float spread = 0.06f;
        float lift = walking && beat == 0 ? 0.05f : 0f, rift = walking && beat == 1 ? 0.05f : 0f;
        c.Limb((-0.05f, hip), (-spread, -0.04f - lift), lw, w.Hose);
        c.Limb((0.05f, hip), (spread, -0.04f - rift), lw, w.Hose.Shade(0.9f));
        c.Poly(new[] { (-spread - 0.045f, -0.05f - lift), (-spread + 0.04f, -0.05f - lift), (-spread + 0.02f, -lift), (-spread - 0.06f, -0.005f - lift) }, Shoe);
        c.Poly(new[] { (spread - 0.04f, -0.05f - rift), (spread + 0.045f, -0.05f - rift), (spread + 0.06f, -0.005f - rift), (spread - 0.02f, -rift) }, Shoe);
    }

    private static float Gown(Canvas c, Who w, FigurePose pose, float hemSwing = 0f)
    {
        float hem = Hem[w.Length];
        bool side = pose == FigurePose.Side;
        float top = -0.75f + (w.Stoop * 0.3f);
        float lean = w.Stoop + w.Sway;
        float sh = side ? 0.10f : 0.15f;
        float flare = (w.Length switch { Length.Ankle => 0.22f, Length.Calf => 0.20f, _ => 0.17f }) * (side ? 0.72f : 1f);
        float hs = (-w.Sway * 0.6f) + hemSwing;
        c.Poly(new[] { (-sh + lean, top), (sh + lean, top), (0.12f + (lean * 0.5f), -0.52f), (flare + hs, hem), (-flare + hs, hem), (-0.12f + (lean * 0.5f), -0.52f) }, w.Gown);
        if (w.Apron is Rgb apron && pose != FigurePose.Back)
        {
            c.Poly(new[] { (-0.085f, -0.63f), (0.085f, -0.63f), (0.12f + hs, hem + 0.02f), (-0.12f + hs, hem + 0.02f) }, apron);
        }

        if (c.Detail)
        {
            Rgb dark = w.Gown.Shade(0.68f);
            Rgb light = w.Gown.Mix(new Rgb(250, 244, 228), 0.45f);
            int n = w.Length != Length.Knee ? 3 : 2;
            for (int k = 0; k < n; k++)
            {
                float t = (k + 1f) / (n + 1f);
                c.Stroke((-0.08f + (0.16f * t), -0.47f), ((-flare * 0.8f) + (flare * 1.6f * t) + hs, hem + 0.015f), dark, 0.011f);
            }

            c.Stroke((-0.09f, -0.47f), ((-flare * 0.78f) + hs, hem + 0.02f), light, 0.012f);
        }

        float bl = lean * 0.5f;
        c.Poly(new[] { (-0.12f + bl, -0.525f), (0.12f + bl, -0.525f), (0.12f + bl, -0.49f), (-0.12f + bl, -0.49f) }, Belt);
        if (pose == FigurePose.Front)
        {
            c.Ell(lean, top + 0.012f, 0.045f, 0.025f, Linen);
        }

        return top;
    }

    private static void CoatBack(Canvas c, Who w, FigurePose pose)
    {
        if (!w.CoatOn)
        {
            return;
        }

        if (pose == FigurePose.Side)
        {
            c.Poly(new[] { (-0.02f, -0.76f), (0.06f, -0.76f), (0.02f, -0.26f), (-0.22f, -0.22f), (-0.17f, -0.58f) }, w.Cloak.Shade(0.85f));
        }
        else
        {
            c.Poly(new[] { (-0.18f, -0.76f), (0.18f, -0.76f), (0.25f, -0.22f), (-0.25f, -0.22f) }, pose == FigurePose.Front ? w.Cloak.Shade(0.85f) : w.Cloak);
        }
    }

    private static void CoatFront(Canvas c, Who w, FigurePose pose, float top)
    {
        if (!w.CoatOn)
        {
            return;
        }

        if (pose == FigurePose.Front)
        {
            c.Poly(new[] { (-0.19f, top - 0.01f), (-0.04f, top - 0.01f), (-0.08f, -0.23f), (-0.25f, -0.23f) }, w.Cloak);
            c.Poly(new[] { (0.04f, top - 0.01f), (0.19f, top - 0.01f), (0.25f, -0.23f), (0.08f, -0.23f) }, w.Cloak);
            c.Poly(new[] { (-0.18f, top - 0.04f), (0.18f, top - 0.04f), (0.14f, top + 0.07f), (-0.14f, top + 0.07f) }, Fur);
        }
        else if (pose == FigurePose.Back)
        {
            c.Poly(new[] { (-0.19f, top - 0.01f), (0.19f, top - 0.01f), (0.25f, -0.22f), (-0.25f, -0.22f) }, w.Cloak);
            c.Poly(new[] { (-0.17f, top - 0.04f), (0.17f, top - 0.04f), (0.15f, top + 0.04f), (-0.15f, top + 0.04f) }, Fur);
        }
        else
        {
            c.Poly(new[] { (-0.13f, top - 0.01f), (0.08f, top - 0.01f), (0.04f, -0.27f), (-0.22f, -0.23f) }, w.Cloak);
            c.Poly(new[] { (-0.13f, top - 0.04f), (0.09f, top - 0.04f), (0.07f, top + 0.05f), (-0.12f, top + 0.05f) }, Fur);
        }

        if (c.Detail)
        {
            Rgb dark = w.Cloak.Shade(0.65f);
            if (pose == FigurePose.Front)
            {
                c.Stroke((-0.17f, top + 0.1f), (-0.20f, -0.25f), dark, 0.010f);
                c.Stroke((0.17f, top + 0.1f), (0.20f, -0.25f), dark, 0.010f);
            }
            else
            {
                c.Stroke((-0.08f, top + 0.1f), (-0.12f, -0.25f), dark, 0.010f);
                c.Stroke((0.08f, top + 0.1f), (0.12f, -0.25f), dark, 0.010f);
            }
        }
    }

    private static void Eyes(Canvas c, FigurePose pose, float fx, float hy, float rx, float ry, float spread, float cheekOut)
    {
        if (!c.Face)
        {
            return;
        }

        ReadOnlySpan<float> eyes = pose == FigurePose.Side ? stackalloc float[] { 0.42f } : stackalloc float[] { -spread, spread };
        foreach (float e in eyes)
        {
            c.Ell(fx + (rx * e), hy + (ry * 0.08f), rx * 0.09f, ry * 0.07f, Ink, 8, false);
            c.Ell(fx + (rx * (e + (e > 0 ? cheekOut : -cheekOut))), hy + (ry * 0.43f), rx * 0.15f, ry * 0.1f, Cheek, 10, false);
        }
    }

    private static void HeadOf(Canvas c, Who w, FigurePose pose, float top)
    {
        float hk = w.HeadK;
        float hy = top - (0.105f * hk);
        float hx = (w.Stoop * 1.6f) + (w.Sway * 1.4f) + (pose == FigurePose.Side ? 0.02f : 0f);
        float rx = 0.074f * hk, ry = 0.088f * hk;
        c.Limb((hx * 0.6f, top + 0.02f), (hx, hy + (ry * 0.6f)), 0.045f * hk, w.Skin.Shade(0.9f));
        bool covered = w.Head is Head.Coif or Head.CoifCap or Head.Liripipe;
        if (!covered)
        {
            if (pose != FigurePose.Side)
            {
                c.Ell(hx, hy - (ry * 0.12f), rx * 1.18f, ry, w.Hair);
                c.Ell(hx - (rx * 0.95f), hy + (ry * 0.45f), rx * 0.36f, ry * 0.32f, w.Hair);
                c.Ell(hx + (rx * 0.95f), hy + (ry * 0.45f), rx * 0.36f, ry * 0.32f, w.Hair);
            }
            else
            {
                c.Ell(hx - (rx * 0.25f), hy - (ry * 0.12f), rx * 1.05f, ry, w.Hair);
                c.Ell(hx - (rx * 0.75f), hy + (ry * 0.48f), rx * 0.36f, ry * 0.32f, w.Hair);
            }
        }

        if (pose == FigurePose.Back)
        {
            if (!covered)
            {
                c.Ell(hx, hy + (ry * 0.05f), rx * 1.05f, ry * 0.95f, w.Hair);
                for (int s = -1; s <= 1; s++)
                {
                    c.Ell(hx + (s * rx * 0.7f), hy + (ry * 0.62f), rx * 0.34f, ry * 0.3f, w.Hair);
                }
            }
        }
        else
        {
            float fx = hx + (pose == FigurePose.Side ? rx * 0.25f : 0f);
            c.Ell(fx, hy + (ry * 0.08f), rx * (pose == FigurePose.Side ? 0.78f : 0.86f), ry * 0.86f, w.Skin);
            if (!covered)
            {
                c.Poly(new[]
                {
                    (fx - (rx * 0.82f), hy - (ry * 0.2f)), (fx - (rx * 0.4f), hy - (ry * 0.75f)), (fx + (rx * 0.4f), hy - (ry * 0.75f)),
                    (fx + (rx * 0.82f), hy - (ry * 0.2f)), (fx + (rx * 0.5f), hy - (ry * 0.32f)), (fx, hy - (ry * 0.42f)), (fx - (rx * 0.5f), hy - (ry * 0.32f)),
                }, w.Hair, ink: false);
            }

            Eyes(c, pose, fx, hy, rx, ry, 0.32f, 0.1f);
        }

        Hat(c, w, pose, hx, hy, rx, ry, top);
    }

    private static void Hat(Canvas c, Who w, FigurePose pose, float hx, float hy, float rx, float ry, float top)
    {
        float fx = hx + (pose == FigurePose.Side ? rx * 0.25f : 0f);
        switch (w.Head)
        {
            case Head.Bare:
                return;
            case Head.Coif:
            case Head.CoifCap:
                // White linen over the crown and the ears, tied under the chin: the face in its frame.
                if (pose == FigurePose.Back)
                {
                    c.Ell(hx, hy - (ry * 0.05f), rx * 1.12f, ry * 1.08f, Linen);
                    return;
                }

                c.Ell(hx, hy - (ry * 0.05f), rx * 1.15f, ry * 1.12f, Linen);
                c.Ell(fx, hy + (ry * 0.14f), rx * (pose == FigurePose.Side ? 0.66f : 0.72f), ry * 0.74f, w.Skin);
                c.Stroke((fx - (rx * 0.6f), hy + (ry * 0.75f)), (fx + (rx * 0.6f), hy + (ry * 0.75f)), Linen, 0.02f);
                Eyes(c, pose, fx, hy + (ry * 0.04f), rx, ry, 0.28f, 0.08f);
                if (w.Head == Head.CoifCap)
                {
                    c.Ell(hx, hy - (ry * 0.75f), rx * 0.95f, ry * 0.42f, Rgb.Hex("#7a5234"));
                }

                return;
            case Head.Liripipe:
            {
                Rgb hc = w.Hood ?? ElderHood;
                c.Poly(new[] { (hx - (rx * 0.2f), hy - (ry * 0.9f)), (hx - (rx * 1.8f), hy - (ry * 0.2f)), (hx - (rx * 2.0f), top + 0.20f), (hx - (rx * 1.5f), top + 0.22f), (hx - (rx * 1.2f), hy) }, hc.Shade(0.85f));
                c.Poly(new[] { (hx - (rx * 2.1f), top + 0.09f), (hx + (rx * 2.1f), top + 0.09f), (hx + (rx * 1.4f), hy), (hx - (rx * 1.4f), hy) }, hc.Shade(0.95f));
                c.Ell(hx, hy - (ry * 0.08f), rx * 1.3f, ry * 1.25f, hc);
                if (pose != FigurePose.Back)
                {
                    c.Ell(fx, hy + (ry * 0.12f), rx * (pose == FigurePose.Side ? 0.62f : 0.72f), ry * 0.76f, w.Skin);
                    if (w.Elder)
                    {
                        c.Poly(new[] { (fx - (rx * 0.55f), hy - (ry * 0.25f)), (fx + (rx * 0.55f), hy - (ry * 0.25f)), (fx + (rx * 0.3f), hy - (ry * 0.5f)), (fx - (rx * 0.3f), hy - (ry * 0.5f)) }, w.Hair, ink: false);
                    }

                    Eyes(c, pose, fx, hy + (ry * 0.04f), rx, ry, 0.28f, 0.08f);
                }

                return;
            }

            case Head.Straw:
            {
                Rgb col = Rgb.Hex("#dcbc62");
                c.Ell(hx, hy - (ry * 0.55f), rx * 2.15f, ry * 0.42f, col);
                c.Ell(hx, hy - (ry * 0.98f), rx, ry * 0.62f, col.Shade(0.93f));
                c.Poly(new[] { (hx - rx, hy - (ry * 0.78f)), (hx + rx, hy - (ry * 0.78f)), (hx + rx, hy - (ry * 0.58f)), (hx - rx, hy - (ry * 0.58f)) }, Vermilion);
                return;
            }

            case Head.Bucket:
            case Head.Felt:
            {
                Rgb col = w.Head == Head.Bucket ? Rgb.Hex("#8d979c") : Rgb.Hex("#4a3a2e");
                float brim = w.Head == Head.Bucket ? 1.65f : 1.8f;
                c.Ell(hx, hy - (ry * 0.62f), rx * brim, ry * 0.33f, col);
                c.Poly(new[] { (hx - (rx * 0.95f), hy - (ry * 0.62f)), (hx + (rx * 0.95f), hy - (ry * 0.62f)), (hx + (rx * 0.7f), hy - (ry * 1.45f)), (hx - (rx * 0.7f), hy - (ry * 1.45f)) }, col.Shade(1.08f));
                return;
            }

            case Head.Kerchief:
            {
                Rgb col = Rgb.Hex("#a8402e");
                c.Ell(hx, hy - (ry * 0.45f), rx * 1.18f, ry * 0.72f, col);
                c.Poly(new[] { (hx - (rx * 0.6f), hy - (ry * 0.3f)), (hx - (rx * 1.5f), hy + (ry * 0.6f)), (hx - (rx * 0.9f), hy + (ry * 0.5f)) }, col);
                return;
            }

            case Head.RedCap:
                c.Ell(hx, hy - (ry * 0.58f), rx * 1.1f, ry * 0.66f, Vermilion);
                c.Poly(new[] { (hx - (rx * 1.12f), hy - (ry * 0.55f)), (hx + (rx * 1.12f), hy - (ry * 0.55f)), (hx + (rx * 1.12f), hy - (ry * 0.36f)), (hx - (rx * 1.12f), hy - (ry * 0.36f)) }, Vermilion.Shade(0.82f));
                return;
            case Head.Chaperon:
            {
                Rgb col = Rgb.Hex("#8e2e3c");
                c.Poly(new[] { (hx + (rx * 0.6f), hy - (ry * 0.7f)), (hx + (rx * 1.6f), hy + (ry * 0.4f)), (hx + (rx * 1.5f), top + 0.12f), (hx + (rx * 1.1f), top + 0.10f), (hx + (rx * 0.9f), hy) }, col.Shade(0.85f));
                c.Ell(hx, hy - (ry * 0.72f), rx * 1.3f, ry * 0.5f, col);
                c.Ell(hx + (rx * 0.2f), hy - (ry * 1.05f), rx * 0.8f, ry * 0.42f, col.Shade(1.1f));
                return;
            }

            case Head.Scholar:
                c.Ell(hx, hy - (ry * 0.58f), rx * 1.08f, ry * 0.62f, Rgb.Hex("#262630"));
                if (pose != FigurePose.Back)
                {
                    c.Poly(new[] { (hx - (rx * 1.9f), top + 0.08f), (hx + (rx * 1.9f), top + 0.08f), (hx + (rx * 1.1f), top - 0.02f), (hx - (rx * 1.1f), top - 0.02f) }, Vermilion);
                }

                return;
        }
    }

    private static (float X, float Y) Arms(Canvas c, Who w, FigurePose pose, float top, FigureHold hold, bool walking, int beat,
        (float X, float Y) workHand)
    {
        const float aw = 0.058f, sx = 0.15f;
        Rgb sl = w.Sleeve;
        if (pose == FigurePose.Side)
        {
            if (hold is FigureHold.Front or FigureHold.Hug)
            {
                c.Limb((0f, top + 0.03f), (0.13f, -0.50f), aw, sl);
                c.Ell(0.14f, -0.50f, 0.032f, 0.032f, w.Skin);
                return (0.14f, -0.50f);
            }

            // the free arm swings forward on a stride (beat 0) and hangs on the passing step (beat 1)
            float swing = walking && beat == 0 ? 0.09f : 0f;
            c.Limb((0f, top + 0.03f), (swing, -0.45f), aw, sl);
            c.Ell(swing, -0.45f, 0.032f, 0.032f, w.Skin);
            return (swing, -0.45f);
        }

        switch (hold)
        {
            case FigureHold.Front:
                for (int s = -1; s <= 1; s += 2)
                {
                    c.Limb((s * sx, top + 0.03f), (s * 0.16f, -0.55f), aw, sl);
                    c.Limb((s * 0.16f, -0.55f), (s * 0.08f, -0.53f), aw, sl);
                    if (pose == FigurePose.Front)
                    {
                        c.Ell(s * 0.07f, -0.53f, 0.03f, 0.03f, w.Skin);
                    }
                }

                return (0.07f, -0.53f);
            case FigureHold.Hug:
                for (int s = -1; s <= 1; s += 2)
                {
                    c.Limb((s * sx, top + 0.03f), (s * 0.17f, -0.58f), aw, sl);
                    c.Limb((s * 0.17f, -0.58f), (-s * 0.06f, -0.62f), aw, sl);
                }

                return (0f, -0.6f);
            case FigureHold.Talk when pose == FigurePose.Front:
            {
                // the palm raised (beat 0, A93's) or lowered to the chest (beat 1): speaking, then a pause
                float k = beat == 1 ? 1f : 0f;
                (float X, float Y) elbow = (0.22f - (0.02f * k), -0.60f + (0.06f * k));
                (float X, float Y) palm = (0.20f - (0.07f * k), -0.75f + (0.15f * k));
                c.Limb((-sx, top + 0.03f), (-0.185f, -0.45f), aw, sl);
                c.Ell(-0.185f, -0.45f, 0.032f, 0.032f, w.Skin);
                c.Limb((sx, top + 0.03f), elbow, aw, sl);
                c.Limb(elbow, (palm.X, palm.Y + 0.03f), aw, sl);
                c.Ell(palm.X, palm.Y, 0.034f, 0.045f - (0.01f * k), w.Skin);
                return palm;
            }
        }

        // Walking toward or away, the hand opposite the stepping foot comes up a little (A130, after play).
        float rightUp = walking && hold == FigureHold.None && beat == 0 ? 0.045f : 0f;
        float leftUp = walking && hold == FigureHold.None && beat == 1 ? 0.045f : 0f;
        (float, float) right = hold == FigureHold.Tool ? workHand : (0.185f - (rightUp * 0.4f), -0.45f - rightUp);
        (float, float) left = (-0.185f + (leftUp * 0.4f), -0.45f - leftUp);
        c.Limb((-sx, top + 0.03f), left, aw, sl);
        c.Limb((sx, top + 0.03f), right, aw, sl);
        if (pose == FigurePose.Front)
        {
            c.Ell(left.Item1, left.Item2, 0.032f, 0.032f, w.Skin);
            c.Ell(right.Item1, right.Item2, 0.032f, 0.032f, w.Skin);
        }

        return right;
    }

    private static void ToolBehind(Canvas c, Who w, FigurePose pose, (float X, float Y) hand)
    {
        if (w.Tool == Tool.None || pose == FigurePose.Back)
        {
            return;
        }

        (float x, _) = hand;
        const float tw = 0.026f;
        switch (w.Tool)
        {
            case Tool.Fork:
                c.Limb((x + 0.01f, -0.02f), (x + 0.01f, -1.02f), tw, Wood);
                foreach (float o in new[] { -0.045f, 0f, 0.045f })
                {
                    c.Limb((x + 0.01f + o, -1.02f), (x + 0.01f + o, -1.16f), 0.014f, Iron);
                }

                c.Limb((x - 0.035f, -1.02f), (x + 0.055f, -1.02f), 0.016f, Iron);
                break;
            case Tool.Spade:
                c.Limb((x + 0.01f, -0.86f), (x + 0.01f, -0.14f), tw, Wood);
                c.Limb((x - 0.025f, -0.86f), (x + 0.045f, -0.86f), 0.022f, Wood);
                c.Poly(new[] { (x - 0.04f, -0.15f), (x + 0.06f, -0.15f), (x + 0.05f, 0f), (x - 0.03f, 0f) }, Iron);
                break;
            case Tool.Crook:
                c.Limb((x + 0.01f, -0.02f), (x + 0.01f, -0.98f), tw, Wood);
                c.Arc(x + 0.07f, -0.98f, 0.06f, 180f, 360f, Wood, tw);
                break;
            case Tool.Stick:
                c.Limb((x + 0.03f, -0.02f), (x, -0.62f), 0.028f, Wood);
                break;
            case Tool.Peel:
                c.Limb((x, -0.15f), (x + 0.08f, -0.95f), tw * 0.9f, Wood);
                c.Ell(x + 0.09f, -1.02f, 0.07f, 0.09f, Rgb.Hex("#b08a5a"));
                break;
            case Tool.Bow:
            {
                c.Arc(x - 0.12f, -0.55f, 0.42f, -62f, 62f, Wood, 0.026f);
                float a = -62f * MathF.PI / 180f, b = 62f * MathF.PI / 180f;
                c.Stroke((x - 0.12f + (0.42f * MathF.Cos(a)), -0.55f + (0.42f * MathF.Sin(a))), (x - 0.12f + (0.42f * MathF.Cos(b)), -0.55f + (0.42f * MathF.Sin(b))), Linen, 0.008f);
                break;
            }
        }
    }

    private static void ToolFront(Canvas c, Who w, FigurePose pose, (float X, float Y) hand)
    {
        if (w.Tool == Tool.None || pose == FigurePose.Back)
        {
            return;
        }

        (float x, float y) = hand;
        const float tw = 0.026f;
        switch (w.Tool)
        {
            case Tool.Axe:
                c.Limb((x, y + 0.04f), (x + 0.10f, -0.98f), tw, Wood);
                c.Poly(new[] { (x + 0.09f, -0.97f), (x + 0.20f, -1.03f), (x + 0.22f, -0.88f), (x + 0.10f, -0.89f) }, Iron);
                break;
            case Tool.Hammer:
                c.Limb((x, y + 0.02f), (x + 0.07f, -0.74f), tw, Wood);
                c.Poly(new[] { (x, -0.80f), (x + 0.15f, -0.77f), (x + 0.14f, -0.70f), (x - 0.01f, -0.73f) }, Iron);
                break;
            case Tool.Pick:
                c.Limb((x, y + 0.04f), (x + 0.10f, -0.95f), tw, Wood);
                c.Line(new[] { (x - 0.04f, -0.86f), (x + 0.10f, -0.98f), (x + 0.26f, -0.90f) }, Iron, 0.028f);
                break;
            case Tool.Rod:
                c.Limb((x, y + 0.03f), (x + 0.42f, -1.18f), 0.016f, Wood);
                c.Stroke((x + 0.42f, -1.18f), (x + 0.45f, -0.55f), Linen, 0.006f);
                break;
            case Tool.Saw:
                c.Poly(new[] { (x - 0.01f, y + 0.02f), (x + 0.04f, y - 0.01f), (x + 0.11f, -0.12f), (x + 0.03f, -0.10f) }, Rgb.Hex("#a8aeb6"));
                c.Limb((x - 0.02f, y + 0.03f), (x + 0.04f, y - 0.02f), 0.032f, Wood);
                break;
            case Tool.Book:
                c.Poly(new[] { (-0.04f, -0.66f), (0.13f, -0.68f), (0.14f, -0.53f), (-0.03f, -0.51f) }, Rgb.Hex("#8e2e2a"));
                c.Poly(new[] { (-0.02f, -0.65f), (0.12f, -0.67f), (0.125f, -0.64f), (-0.015f, -0.62f) }, Linen, ink: false);
                break;
            case Tool.Basket:
                c.Ell(x + 0.02f, y + 0.04f, 0.085f, 0.06f, Basket);
                c.Ell(x + 0.02f, y, 0.07f, 0.03f, Rgb.Hex("#5a9a3a"));
                c.Arc(x + 0.02f, y + 0.02f, 0.07f, 180f, 360f, Rgb.Hex("#7a5a32"), 0.012f);
                break;
        }
    }

    private static void LoadBehind(Canvas c, Who w, FigurePose pose)
    {
        if (w.Good is not (Goods.Firewood or Goods.Hay or Goods.Wheat))
        {
            return;
        }

        Rgb col = GoodColour(w.Good.Value);
        if (pose == FigurePose.Side)
        {
            c.Poly(new[] { (-0.22f, -0.92f), (-0.04f, -0.94f), (-0.02f, -0.50f), (-0.20f, -0.48f) }, col);
            foreach (float o in new[] { -0.18f, -0.12f, -0.06f })
            {
                c.Stroke((o, -0.92f), (o + 0.01f, -0.52f), col.Shade(0.7f), 0.010f);
            }

            return;
        }

        c.Poly(new[] { (-0.15f, -1.02f), (0.15f, -1.02f), (0.13f, -0.56f), (-0.13f, -0.56f) }, col);
        foreach (float o in new[] { -0.10f, -0.03f, 0.04f, 0.11f })
        {
            c.Stroke((o, -1.0f), (o * 0.9f, -0.6f), col.Shade(0.7f), 0.010f);
        }

        c.Limb((-0.15f, -0.70f), (0.15f, -0.70f), 0.018f, Vermilion);
    }

    private static void LoadFront(Canvas c, Who w, FigurePose pose, float top)
    {
        if (w.Good is not Goods g || g is Goods.Firewood or Goods.Hay or Goods.Wheat)
        {
            return;
        }

        Rgb col = GoodColour(g);
        Rgb end = Rgb.Hex("#d8b480");
        if (g == Goods.Logs)
        {
            float y = top - 0.03f;
            if (pose == FigurePose.Side)
            {
                foreach (float dy in new[] { 0f, -0.055f })
                {
                    c.Limb((-0.30f, y + dy), (0.30f, y + dy), 0.065f, col);
                    c.Ell(0.30f, y + dy, 0.032f, 0.032f, end);
                }
            }
            else
            {
                foreach (float dx in new[] { -0.04f, 0.04f })
                {
                    c.Limb((-0.34f + dx, y - 0.02f), (0.34f + dx, y), 0.065f, col);
                }

                if (pose == FigurePose.Front)
                {
                    c.Ell(0.38f, y, 0.032f, 0.032f, end);
                }
            }

            return;
        }

        switch (g)
        {
            case Goods.Flour:
                c.Ell(0.10f, top - 0.06f, 0.13f, 0.09f, col);
                c.Ell(0f, top - 0.10f, 0.025f, 0.03f, Vermilion);
                return;
            case Goods.Meat:
                c.Ell(0.19f, top, 0.085f, 0.06f, col);
                c.Limb((0.19f, top - 0.02f), (0.11f, top - 0.07f), 0.022f, Linen);
                return;
            case Goods.Tools:
            case Goods.IronTools:
                c.Limb((-0.05f, top + 0.05f), (0.25f, top - 0.20f), 0.026f, Wood);
                c.Limb((0f, top + 0.05f), (0.28f, top - 0.12f), 0.026f, Wood);
                c.Ell(0.26f, top - 0.20f, 0.05f, 0.03f, col);
                c.Ell(0.29f, top - 0.12f, 0.05f, 0.03f, col);
                return;
        }

        if (pose == FigurePose.Back)
        {
            return; // held in front: hidden by the body walking away
        }

        float x0 = pose == FigurePose.Side ? 0.07f : 0f;
        switch (g)
        {
            case Goods.Produce:
            case Goods.Bread:
                c.Ell(x0, -0.52f, 0.13f, 0.075f, Basket);
                c.Ell(x0, -0.575f, 0.11f, 0.04f, col);
                break;
            case Goods.Stone:
            case Goods.Iron:
                c.Poly(new[] { (x0 - 0.10f, -0.62f), (x0 + 0.10f, -0.62f), (x0 + 0.10f, -0.45f), (x0 - 0.10f, -0.45f) }, col);
                break;
            case Goods.Fish:
                c.Ell(x0 + 0.13f, -0.33f, 0.026f, 0.10f, col);
                c.Ell(x0 + 0.21f, -0.33f, 0.026f, 0.10f, col);
                break;
            case Goods.Clothing:
            case Goods.Leather:
                for (int k = 0; k < 3; k++)
                {
                    float dy = -0.035f * k;
                    c.Poly(new[] { (x0 - 0.11f, -0.50f + dy), (x0 + 0.11f, -0.50f + dy), (x0 + 0.11f, -0.465f + dy), (x0 - 0.11f, -0.465f + dy) }, col.Shade(1f - (0.07f * k)));
                }

                break;
            default:
                c.Ell(x0, -0.55f, 0.10f, 0.08f, col);
                break;
        }
    }

    private static void Cart(Canvas c, Who w, FigurePose pose)
    {
        if (!w.CartOn)
        {
            return;
        }

        Rgb bed = Rgb.Hex("#8a6238");
        if (pose == FigurePose.Side)
        {
            c.Poly(new[] { (-0.72f, -0.48f), (-0.28f, -0.48f), (-0.30f, -0.28f), (-0.70f, -0.28f) }, bed);
            c.Limb((-0.28f, -0.42f), (0.12f, -0.47f), 0.02f, Wood);
            c.Ell(-0.50f, -0.14f, 0.13f, 0.13f, Rgb.Hex("#5a4028"));
            c.Ell(-0.50f, -0.14f, 0.05f, 0.05f, Rgb.Hex("#a8885a"));
        }
        else
        {
            c.Poly(new[] { (-0.26f, -0.80f), (0.26f, -0.80f), (0.26f, -0.52f), (-0.26f, -0.52f) }, bed);
            c.Poly(new[] { (-0.26f, -0.80f), (0.26f, -0.80f), (0.22f, -0.86f), (-0.22f, -0.86f) }, bed.Shade(1.15f));
        }
    }

    private static void ShiverMarks(Canvas c, Who w, FigurePose pose, float top)
    {
        if (!w.Cold)
        {
            return;
        }

        float hy = top - (0.10f * w.HeadK);
        for (int s = -1; s <= 1; s += 2)
        {
            if (pose == FigurePose.Side && s < 0)
            {
                continue;
            }

            c.Arc(s * 0.12f, hy, 0.09f, s > 0 ? -40f : 140f, s > 0 ? 40f : 220f, Shiver, 0.016f, ink: false);
            c.Arc(s * 0.12f, hy, 0.15f, s > 0 ? -35f : 145f, s > 0 ? 35f : 215f, Shiver, 0.016f, ink: false);
        }
    }

    // ------------------------------------------------------------------ the work's two poses (A130, villager-animation.md §1)
    /// <summary>
    /// How a tool that swings is held in its two working poses: the hand moved from where A93 holds it (0.21, -0.46) and
    /// the tool turned about the hand, degrees clockwise on screen. Beat 1 is the tool raised, beat 2 the stroke —
    /// <c>anim.py</c>'s <c>WORK</c> frames 1 and 2, as mocked on Joe's Hollowmere.
    /// </summary>
    private static (float Dx, float Dy, float Degrees)? WorkPose(Tool tool, int beat) => (tool, beat) switch
    {
        (Tool.Axe or Tool.Pick, 1) => (-0.04f, -0.30f, 28f),
        (Tool.Axe or Tool.Pick, 2) => (-0.13f, 0.10f, -150f),
        (Tool.Hammer, 1) => (-0.02f, -0.24f, 22f),
        (Tool.Hammer, 2) => (-0.09f, 0.03f, -105f),
        (Tool.Fork, 1) => (0f, -0.12f, 14f),
        (Tool.Fork, 2) => (0f, 0.05f, -22f),
        (Tool.Spade, 1) => (0f, -0.10f, -18f),
        (Tool.Spade, 2) => (0f, 0.07f, 0f),
        (Tool.Rod, 1) => (0f, -0.02f, 3f),
        (Tool.Rod, 2) => (0f, 0.02f, -7f),
        (Tool.Basket, 1) => (-0.02f, 0.06f, 0f),
        (Tool.Basket, 2) => (-0.05f, 0.16f, 0f),
        (Tool.Saw, 1) => (0.05f, -0.02f, 0f),
        (Tool.Saw, 2) => (-0.04f, 0.02f, 0f),
        (Tool.Peel, 1) => (0.03f, -0.04f, 10f),
        (Tool.Peel, 2) => (0.06f, 0f, -12f),
        (Tool.Bow, 1) => (-0.03f, -0.18f, -60f),
        (Tool.Bow, 2) => (-0.03f, -0.20f, -80f),
        _ => null,
    };

    /// <summary>The trade's tool as a person of this age holds it: none for a child, the stick for an elder.</summary>
    private static Tool ToolOf(JobKind? trade, LifeStage stage) =>
        stage == LifeStage.Child ? Tool.None : stage == LifeStage.Elder ? Tool.Stick : LookOf(trade).Tool;

    /// <summary>
    /// Seconds a swing of this person's tool takes at the game's 1× (§1): the axe and the pick 1.6, the hammer 1.2, the saw
    /// 0.96, the rod's dip 4.8, the rest 2.4; 0 when their tool has no working poses (the crook, the book, a child, an
    /// elder's stick, a trade with no tool).
    /// </summary>
    internal static float SwingSeconds(JobKind? trade, LifeStage stage) => ToolOf(trade, stage) switch
    {
        Tool.Axe or Tool.Pick => 1.6f,
        Tool.Hammer => 1.2f,
        Tool.Saw => 0.96f,
        Tool.Rod => 4.8f,
        Tool.Fork or Tool.Spade or Tool.Basket or Tool.Peel or Tool.Bow => 2.4f,
        _ => 0f,
    };

    /// <summary>How far the body bends into the stroke, as a share of its height (picking, digging, pitching).</summary>
    internal static float StoopOf(JobKind? trade, LifeStage stage) => ToolOf(trade, stage) switch
    {
        Tool.Basket => 0.06f,
        Tool.Spade => 0.03f,
        Tool.Fork => 0.02f,
        _ => 0f,
    };

    // ------------------------------------------------------------------ one person
    /// <summary>Paints one person <see cref="BakeHeight"/> pixels tall (an adult; a child and an elder less).</summary>
    internal static PaintedFigure Paint(FigureKey f)
    {
        var w = new Who(f);
        float hk = BakeHeight * w.K;
        var c = new Canvas(hk) { Flip = f.Flip };
        FigurePose pose = f.Pose;

        // At work facing us with a tool that swings, beats 1 and 2 move the hand and turn the tool about it (A130).
        (float Dx, float Dy, float Degrees)? swung = f.Hold == FigureHold.Tool && !f.Walking && pose == FigurePose.Front
            ? WorkPose(w.Tool, f.Beat) : null;
        (float X, float Y) workHand = swung is (float dx, float dy, _) ? (0.21f + dx, -0.46f + dy) : (0.21f, -0.46f);

        if (pose != FigurePose.Side)
        {
            Cart(c, w, pose);
        }

        CoatBack(c, w, pose);
        LoadBehind(c, w, pose);
        if (f.ToolOut)
        {
            if (swung is (_, _, float turned))
            {
                c.TurnAbout(workHand, turned);
            }

            ToolBehind(c, w, pose, swung is not null ? workHand : pose != FigurePose.Side ? (w.Elder ? (0.185f, -0.45f) : (0.21f, -0.46f)) : (0.09f, -0.45f));
            c.Turn = null;
        }

        Legs(c, w, pose, f.Walking, f.Beat);
        // Walking toward or away, the hem swings toward the stepping leg (A130, after play: the legs alone barely moved there).
        float hemSwing = f.Walking && pose != FigurePose.Side ? (f.Beat == 0 ? -0.03f : 0.03f) : 0f;
        float top = Gown(c, w, pose, hemSwing);
        (float, float) hand = Arms(c, w, pose, top, f.Hold, f.Walking, f.Beat, workHand);
        CoatFront(c, w, pose, top);
        bool logsUnderHead = w.Good == Goods.Logs && pose != FigurePose.Side;
        if (logsUnderHead)
        {
            LoadFront(c, w, pose, top);
        }

        HeadOf(c, w, pose, top);
        if (f.ToolOut)
        {
            if (swung is (_, _, float turned))
            {
                c.TurnAbout(hand, turned);
            }

            ToolFront(c, w, pose, hand);
            c.Turn = null;
        }

        if (!logsUnderHead)
        {
            LoadFront(c, w, pose, top);
        }

        if (pose == FigurePose.Side)
        {
            Cart(c, w, pose);
        }

        ShiverMarks(c, w, pose, top);
        return Finish(c, hk, shadowOnly: false);
    }

    /// <summary>
    /// ⭐ The shadow on the ground, painted once on its own (A130): an adult's, on an adult's canvas, so it lies at the feet
    /// of any person's picture drawn the same way and scales with them. It is drawn under a person and not moved with them,
    /// so a step's rise and a lean read against the ground. Until A130 it was part of every person's painting.
    /// </summary>
    internal static PaintedFigure PaintShadow() => Finish(new Canvas(BakeHeight), BakeHeight, shadowOnly: true);

    /// <summary>
    /// The light from the upper left as a soft darkening to the right, the ink outline round the whole silhouette; or,
    /// alone, the shadow on the ground to the lower right; then the supersample averaged down.
    /// </summary>
    private static PaintedFigure Finish(Canvas c, float hk, bool shadowOnly)
    {
        int W = c.W, H = c.H;
        int r = Math.Max(1, (int)MathF.Round(Supersample * 0.5f));

        // the silhouette grown by r: a max filter, rows then columns
        var mask = new byte[W * H];
        for (int k = 0; k < mask.Length; k++)
        {
            mask[k] = (byte)((c.Px[k] >> 24) != 0 ? 1 : 0);
        }

        var rows = new byte[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                byte m = 0;
                for (int d = Math.Max(0, x - r); d <= Math.Min(W - 1, x + r) && m == 0; d++)
                {
                    m = mask[(y * W) + d];
                }

                rows[(y * W) + x] = m;
            }
        }

        var grown = new byte[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                byte m = 0;
                for (int d = Math.Max(0, y - r); d <= Math.Min(H - 1, y + r) && m == 0; d++)
                {
                    m = rows[(d * W) + x];
                }

                grown[(y * W) + x] = m;
            }
        }

        // the shadow: an ellipse at the feet, to the lower right, softened
        float S = c.S;
        float sx = (FeetFromLeft + 0.10f) * S, sy = (FeetFromTop + 0.02f) * S;
        float srx = 0.22f * S, sry = 0.065f * S;
        var shadow = new float[shadowOnly ? W * H : 0];
        if (shadowOnly)
        {
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f - sx) / srx, dy = (y + 0.5f - sy) / sry;
                    if ((dx * dx) + (dy * dy) <= 1f)
                    {
                        shadow[(y * W) + x] = 105f / 255f;
                    }
                }
            }

            BoxBlur(shadow, W, H, Math.Max(1, (int)(0.035f * S)));
            BoxBlur(shadow, W, H, Math.Max(1, (int)(0.035f * S)));
        }

        // compose at the supersample, then average each Supersample² block, premultiplied
        int ow = (W + Supersample - 1) / Supersample, oh = (H + Supersample - 1) / Supersample;
        var acc = new float[ow * oh * 4];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int k = (y * W) + x;
                float rr, gg, bb, aa;
                uint px = c.Px[k];
                if ((px >> 24) != 0)
                {
                    // the light: darker toward the right, up to 16% ink
                    float dark = 0.16f * x / W;
                    rr = Lerp(px & 0xff, Ink.R, dark);
                    gg = Lerp((px >> 8) & 0xff, Ink.G, dark);
                    bb = Lerp((px >> 16) & 0xff, Ink.B, dark);
                    aa = 1f;
                }
                else if (grown[k] != 0)
                {
                    rr = Ink.R;
                    gg = Ink.G;
                    bb = Ink.B;
                    aa = 0.92f;
                }
                else
                {
                    rr = Ink.R;
                    gg = Ink.G;
                    bb = Ink.B;
                    aa = shadowOnly ? shadow[k] : 0f;
                }

                int o = (((y / Supersample) * ow) + (x / Supersample)) * 4;
                acc[o] += rr * aa;
                acc[o + 1] += gg * aa;
                acc[o + 2] += bb * aa;
                acc[o + 3] += aa;
            }
        }

        var rgba = new byte[ow * oh * 4];
        const float n = Supersample * Supersample;
        for (int o = 0; o < ow * oh; o++)
        {
            float a = acc[(o * 4) + 3];
            if (a <= 0f)
            {
                continue;
            }

            rgba[o * 4] = (byte)Math.Clamp((int)MathF.Round(acc[o * 4] / a), 0, 255);
            rgba[(o * 4) + 1] = (byte)Math.Clamp((int)MathF.Round(acc[(o * 4) + 1] / a), 0, 255);
            rgba[(o * 4) + 2] = (byte)Math.Clamp((int)MathF.Round(acc[(o * 4) + 2] / a), 0, 255);
            rgba[(o * 4) + 3] = (byte)Math.Clamp((int)MathF.Round(a / n * 255f), 0, 255);
        }

        return new PaintedFigure(ow, oh, rgba, FeetFromLeft * hk, FeetFromTop * hk);
    }

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);

    private static void BoxBlur(float[] v, int w, int h, int r)
    {
        var tmp = new float[v.Length];
        for (int y = 0; y < h; y++)
        {
            float sum = 0f;
            for (int x = -r; x <= r; x++)
            {
                sum += v[(y * w) + Math.Clamp(x, 0, w - 1)];
            }

            for (int x = 0; x < w; x++)
            {
                tmp[(y * w) + x] = sum / ((2 * r) + 1);
                sum += v[(y * w) + Math.Min(w - 1, x + r + 1)] - v[(y * w) + Math.Max(0, x - r)];
            }
        }

        for (int x = 0; x < w; x++)
        {
            float sum = 0f;
            for (int y = -r; y <= r; y++)
            {
                sum += tmp[(Math.Clamp(y, 0, h - 1) * w) + x];
            }

            for (int y = 0; y < h; y++)
            {
                v[(y * w) + x] = sum / ((2 * r) + 1);
                sum += tmp[(Math.Min(h - 1, y + r + 1) * w) + x] - tmp[(Math.Max(0, y - r) * w) + x];
            }
        }
    }

    // ------------------------------------------------------------------ the person's own look, from their id
    /// <summary>A stable 0..1 from an id and a salt — FNV-1a, never <c>string.GetHashCode</c>, which changes every run.</summary>
    internal static float Hash01(int id, string salt)
    {
        uint h = 2166136261u;
        foreach (char ch in $"{id}:{salt}")
        {
            h = (h ^ ch) * 16777619u;
        }

        return (h & 0xFFFF) / 65535f;
    }

    private static Rgb Pick(int id, string salt, Rgb[] from) => from[(int)(Hash01(id, salt) * from.Length) % from.Length];

    private static Rgb[] Hexes(params string[] hexes) => Array.ConvertAll(hexes, Rgb.Hex);

    /// <summary>The trade's colour as a manuscript pigment: more saturated, a touch lighter, never muddy.</summary>
    internal static Rgb Pigment(Rgb c)
    {
        float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
        float max = MathF.Max(r, MathF.Max(g, b)), min = MathF.Min(r, MathF.Min(g, b));
        float v = max, d = max - min;
        float s = max <= 0f ? 0f : d / max;
        float hue = 0f;
        if (d > 0f)
        {
            hue = max == r ? ((g - b) / d) + (g < b ? 6f : 0f) : max == g ? ((b - r) / d) + 2f : ((r - g) / d) + 4f;
            hue /= 6f;
        }

        s = MathF.Min(0.82f, (s * 1.35f) + 0.06f);
        v = MathF.Min(0.92f, MathF.Max(0.50f, v * 1.08f));
        float hh = hue * 6f;
        int i = (int)MathF.Floor(hh) % 6;
        float f = hh - MathF.Floor(hh);
        float p = v * (1 - s), q = v * (1 - (s * f)), t = v * (1 - (s * (1 - f)));
        (float R, float G, float B) o = i switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
        return Rgb.F(o.R, o.G, o.B);
    }
}
