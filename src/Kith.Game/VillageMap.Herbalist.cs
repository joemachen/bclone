using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐⭐ THE HERBALIST'S HUT, DRAWN AHEAD OF ITS SYSTEMS (A326, <c>specs/herbalist-art.md</c>; Joe: <i>"All as recommended"</i> on the
/// six calls — the herb garden in front, A). W6 piece 2 (<c>health.md §6</c>) makes the herbalist, herbs and the remedy; this is
/// the yard its hut wears, built before them so the slice adds only its rows (<c>herbalist-art.md §3</c>): <c>RoofOf</c>'s
/// <c>JobKind.Herbalist</c> (thatch, a chimney, <see cref="YardKind.Herbalist"/>) and <c>StockReadOf</c>'s remedy read.
/// The hut at the back of its plot; in front a wattle-fenced garden, a path from the door to a gate, six raised beds each its own
/// herb, herbs drying on a rack by the wall, and <b>the remedies kept at the hut as jars on a shelf by the door, two a level</b> —
/// empty in spring, full by autumn, the way the tavern's casks show its ale (<c>health.md §6.2</c>: autumn's stock is the
/// winter's answer). Recommended footprint 2 × 2: at 2 × 1 the garden was too thin to read (A325).
/// </remarks>
public partial class VillageMap
{
    /// <summary>The six beds' herbs, in their order along the garden: sage, lavender, calendula, yarrow, mint, borage.</summary>
    internal static readonly Color[] HerbBedColours =
    {
        new("#9aa886"), new("#8e7bb5"), new("#e09a3a"), new("#e8e0c4"), new("#5e9a52"), new("#6f86c4"),
    };

    /// <summary>The garden's slots across its width: six beds and the path, which takes the slot nearest the door.</summary>
    private const int HerbSlots = 7;

    private static readonly Color HerbBedEarth = new("#6e5238");
    private static readonly Color HerbPath = new("#cdb88f");
    private static readonly Color BundleFresh = new("#7f9a5a");
    private static readonly Color BundleDried = new("#a89060");
    private static readonly Color BundleTwine = new("#c4983e");
    private static readonly Color RemedyJar = new("#7a4a6e");
    private static readonly Color RemedyCover = new("#e8dcc0");

    /// <summary>The most jars the shelf holds: two a level, to the third.</summary>
    internal const int RemedyJarsMost = 6;

    /// <summary>The hut at the back of its plot, the garden the front part, across the door's way.</summary>
    private static YardLayout HerbalistLayout(float fw, float fd, Vector2 doorOut, Vector2 doorLocal, float gap)
    {
        Vector2 way = DominantAxis(doorOut);
        Vector2 across = new(-way.Y, way.X);
        bool wayIsX = Mathf.Abs(way.X) > 0.5f;
        float depthHalf = (wayIsX ? fw : fd) * 0.92f;
        float widthHalf = (wayIsX ? fd : fw) * 0.92f;
        float roofDepthHalf = depthHalf * 0.44f;
        float roofWidthHalf = widthHalf * 0.74f;
        float roofCentre = -(depthHalf - roofDepthHalf);
        float inner = roofCentre + roofDepthHalf + gap;
        float yardDepth = (depthHalf - inner) / 2f;
        return new YardLayout(way * roofCentre, wayIsX ? roofDepthHalf : roofWidthHalf, wayIsX ? roofWidthHalf : roofDepthHalf,
            way * (inner + yardDepth), across, widthHalf, yardDepth, Mathf.Clamp(doorLocal.Dot(across) / widthHalf, -1f, 1f));
    }

    /// <summary>The slot (0..6) the path takes: the one nearest the door, along the garden.</summary>
    internal static int HerbPathSlot(float doorAt) =>
        Mathf.Clamp(Mathf.RoundToInt((doorAt - HerbSlotAlong(0)) / (HerbSlotAlong(1) - HerbSlotAlong(0))), 0, HerbSlots - 1);

    private static float HerbSlotAlong(int slot) => -0.84f + (slot * 1.68f / (HerbSlots - 1));

    private static void HerbalistYard(YardKit k, YardLayout l, YardState s, int seed)
    {
        float t = l.Turn;
        YardGround(k, l.YardAt, l.YardLong * 0.95f, l.YardShort, t, seed, null, 0.35f);

        // The path from the door to the gate, in the slot nearest the door; a bed in each of the other six.
        int path = HerbPathSlot(l.DoorAt);
        float pathAt = HerbSlotAlong(path);
        k.Fill("garden path", Box(l.P(pathAt, 0f), 0.13f, l.YardShort * 2f, t), HerbPath);
        int bed = 0;
        for (int slot = 0; slot < HerbSlots; slot++)
        {
            if (slot != path)
            {
                HerbBed(k, l.P(HerbSlotAlong(slot), -0.25f), t + (Mathf.Pi / 2f), l.YardShort * 1.1f, 0.13f, HerbBedColours[bed], seed + bed);
                bed++;
            }
        }

        // The wattle round the garden's three open sides, the gate where the path meets it.
        Vector2 a = l.P(-0.97f, -0.95f), b = l.P(0.97f, -0.95f), c = l.P(0.97f, 0.95f), d = l.P(-0.97f, 0.95f);
        k.Strokes("wattle", new List<Vector2> { a, l.P(pathAt - 0.09f, -0.95f), l.P(pathAt + 0.09f, -0.95f), b, b, c, d, a }, Wicker, 0f, 2f);

        // The jars on the roomier side of the path by the wall; the drying rack on the other.
        float roomy = pathAt > 0.05f ? -1f : pathAt < -0.05f ? 1f : (seed & 2) == 0 ? 1f : -1f;
        RemedyShelf(k, l.P(pathAt + (roomy * 0.3f), 0.55f), t, s.Stock);
        HerbRack(k, l.P(Mathf.Clamp(pathAt - (roomy * 0.45f), -0.7f, 0.7f), 0.78f), t, l.YardLong * 0.5f, 5, seed);
    }

    /// <summary>A raised bed of one herb: a timber-edged box of earth, clumps of the herb in it.</summary>
    private static void HerbBed(YardKit k, Vector2 at, float turn, float length, float depth, Color herb, int seed)
    {
        k.Thing("herb bed", Box(at, length, depth, turn), HerbBedEarth, 0.7f);
        int cols = Mathf.Max(2, Mathf.RoundToInt(length / 0.075f));
        int rows = Mathf.Max(1, Mathf.RoundToInt(depth / 0.075f));
        for (int j = 0; j < rows; j++)
        {
            for (int i = 0; i < cols; i++)
            {
                float x = (-length / 2) + ((i + 0.5f) * length / cols);
                float y = (-depth / 2) + ((j + 0.5f) * depth / rows);
                float r = 0.026f + (0.01f * Hash01(seed, (j * 31) + i, 97));
                Color c = herb.Lightened(0.12f * Hash01(seed, (j * 31) + i, 98)).Darkened(0.1f * Hash01(seed, (j * 31) + i, 99));
                k.Fill("herb", Blob(Tn(at, x, y, turn), r, r, 0f, seed + i + (j * 7), 0.25f, 8), c);
            }
        }
    }

    /// <summary>The remedies: jars on a shelf by the door, two a level, none at none.</summary>
    private static void RemedyShelf(YardKit k, Vector2 at, float turn, int level)
    {
        int n = Mathf.Min(level * 2, RemedyJarsMost);
        if (n == 0)
        {
            return;
        }

        int pairs = (n + 1) / 2;
        k.Thing("remedy shelf", Box(at, (0.1f * pairs) + 0.06f, 0.17f, turn), TimberLight, 0.8f);
        for (int i = 0; i < n; i++)
        {
            Vector2 c = Tn(at, (-0.05f * (pairs - 1)) + (0.1f * (i / 2)), (i % 2 == 0) ? -0.04f : 0.04f, turn);
            k.Thing("remedy", Oval(c, 0.038f, 0.038f, 0f, 12), RemedyJar, 0.85f);
            k.Fill("remedy cover", Oval(c, 0.018f, 0.018f, 0f, 8), RemedyCover);
        }
    }

    /// <summary>Herbs drying: a frame by the wall hung with bundles, green to straw.</summary>
    private static void HerbRack(YardKit k, Vector2 at, float turn, float length, int hung, int seed)
    {
        k.Strokes("drying rack", new List<Vector2>
        {
            Tn(at, -length / 2, 0, turn), Tn(at, length / 2, 0, turn),
            Tn(at, -length / 2, -0.1f, turn), Tn(at, -length / 2, 0.1f, turn),
            Tn(at, length / 2, -0.1f, turn), Tn(at, length / 2, 0.1f, turn),
        }, TimberDark, 0f, 1.5f);
        for (int i = 0; i < hung; i++)
        {
            float x = (-length / 2) + ((i + 0.5f) * length / hung);
            Color c = BundleFresh.Lerp(BundleDried, Hash01(seed, i, 61));
            k.Thing("herb bundle", new[] { Tn(at, x - 0.04f, 0f, turn), Tn(at, x + 0.04f, 0f, turn), Tn(at, x + 0.018f, 0.15f, turn), Tn(at, x - 0.018f, 0.15f, turn) }, c, 0.75f);
            k.Fill("herb bundle tie", Box(Tn(at, x, 0.025f, turn), 0.07f, 0.018f, turn), BundleTwine);
        }
    }
}
