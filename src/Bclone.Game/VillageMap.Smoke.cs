using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <remarks>
/// ⭐⭐ CHIMNEY SMOKE WHILE FIREWOOD BURNS (A139, <c>specs/indoors-and-smoke.md §2, §4</c>; Joe: <i>"buildings burning
/// firewood should have smoke plums coming from their chimnies as long as they are burning firewood"</i> — look B, his
/// pick: <i>"Smoke B please"</i>). Watercolour puffs with a pooled edge, rising from each painted chimney, swelling and
/// thinning down the wind. <b>True to the sim</b> (§2): a home or the school in winter while its firewood lasts, the
/// smithy while a smith forges, an oven while it bakes — the tailor's, farmhouse's and workshop's chimneys stay cold.
/// View only; nothing here is hashed.
/// </remarks>
public partial class VillageMap
{
    /// <summary>Puffs in the air above one chimney at once.</summary>
    internal const int PuffsAChimney = 6;

    /// <summary>A puff's life from the flue to gone, in the people's seconds (game speed, capped, still when paused).</summary>
    internal const float PuffSeconds = 4f;

    /// <summary>A fire lit or gone cold shows over this long — no pop (§4).</summary>
    internal const float SmokeFadeSeconds = 1f;

    /// <summary>How high a puff rises in its life, how far the wind carries it, and how big it grows, in tiles.</summary>
    private const float PuffRise = 1.6f, PuffDrift = 0.65f, PuffFrom = 0.1f, PuffTo = 0.42f;

    /// <summary>
    /// The wash, warm and cool, and its pooled edge — ⭐ <b>darker than A139's</b> (<c>#b0a89e</c> / <c>#787068</c>; Joe, after
    /// play: <i>"smoke from chimnies needs a bit more watercolor touch and a bit darker"</i>, A145).
    /// </summary>
    internal static readonly Color PuffWarm = new("#8e857a"), PuffCool = new("#7f8189"), PuffEdge = new("#4f473f");

    /// <summary>How many puff paintings there are — each its own edge, grain and bloom, so no two puffs in the air match (A145).</summary>
    internal const int PuffPaintings = 4;

    /// <summary>A puff painting's size in pixels.</summary>
    internal const int PuffPixels = 96;

    private Texture2D[]? _puffs;

    /// <summary>The workplaces with a fire lit this tick (the smithy forging, an oven baking) — kept once a tick, where the states change.</summary>
    private readonly HashSet<int> _firesLit = new();

    /// <summary>
    /// ⭐ Pure: a puff of one chimney at this moment — where it is (tiles from the flue), its size (tiles), and how much of
    /// it shows (0..1). Each chimney starts at its own <paramref name="phase"/> so no two smoke in step (D28's rule).
    /// </summary>
    internal static (Vector2 Offset, float Radius, float Show) PuffAt(int puff, float phase, double clock)
    {
        float u = (float)(((clock / PuffSeconds) + ((float)puff / PuffsAChimney) + phase) % 1.0);
        float sway = 0.05f * u * Mathf.Sin((u * 6f) - ((float)clock * 1.6f) + (phase * Mathf.Tau));
        var offset = new Vector2((PuffDrift * u * u) + sway, -PuffRise * u);
        float show = Mathf.Min(1f, u / 0.1f) * Mathf.Pow(1f - u, 1.3f);
        return (offset, PuffFrom + ((PuffTo - PuffFrom) * u), show);
    }

    /// <summary>Pure: a fire's smoke level after a frame — toward 1 lit, 0 cold, over <see cref="SmokeFadeSeconds"/>; paused, held.</summary>
    internal static float SmokeOn(float level, bool burning, double frameSeconds) =>
        Mathf.MoveToward(level, burning ? 1f : 0f, (float)frameSeconds / SmokeFadeSeconds);

    /// <summary>⭐ Pure: whether a home's hearth burns (§2) — winter, somebody living there, and firewood in the larder.</summary>
    internal static bool HearthBurns(Season season, bool occupied, int firewood) =>
        season == Season.Winter && occupied && firewood > 0;

    /// <summary>Whether a workplace's fire is lit (§2): the school in winter, staffed, with wood; a forge or an oven at work.</summary>
    private bool FireLitAt(SimWorld world, Workplace workplace)
    {
        if (workplace.IsSite)
        {
            return false;
        }

        if (workplace.Kind == JobKind.Teacher)
        {
            return world.Clock.Season == Season.Winter && workplace.WorkerIds.Count > 0 && workplace.Store.Firewood > 0;
        }

        return _firesLit.Contains(workplace.Id);
    }

    /// <summary>
    /// Once a tick (<see cref="AdvanceInterpolation"/>): whose fire is lit — a smith <c>Forging</c> while a tool takes
    /// firewood, a batch trade at work whose batch is fired (the oven, `firewood_per_firing`).
    /// </summary>
    private void NoteTheFires(SimWorld world)
    {
        _firesLit.Clear();
        _millsWorked.Clear();
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive)
            {
                continue;
            }

            bool lit = villager.State switch
            {
                VillagerState.Forging => world.Config.FirewoodPerTool > 0,
                VillagerState.WorkingABatch => world.FindWorkplace(villager.WorkplaceId) is Workplace shop
                    && world.BatchFor(shop.Kind) is Batch { Firing: > 0 },
                _ => false,
            };
            if (lit)
            {
                _firesLit.Add(villager.WorkplaceId);
            }

            // A miller grinding: the mill is worked, and its sails turn (A145).
            if (villager.State == VillagerState.WorkingABatch && world.FindWorkplace(villager.WorkplaceId)?.Kind == JobKind.Miller)
            {
                _millsWorked.Add(villager.WorkplaceId);
            }
        }
    }

    /// <summary>The smoke over one painted building, its level carried on its roof's cache (laid and dropped with it).</summary>
    private void DrawTheSmoke(RoofCache held, bool burning, float alpha)
    {
        held.Smoke = SmokeOn(held.Smoke, burning, _peopleFrameSeconds);
        if (held.Smoke <= 0f || held.Flues.Length == 0)
        {
            return;
        }

        Texture2D[] puffs = ThePuffs();
        for (int f = 0; f < held.Flues.Length; f++)
        {
            float phase = RoofHash01(held.Signature.Seed, f);
            for (int i = 0; i < PuffsAChimney; i++)
            {
                (Vector2 offset, float radius, float show) = PuffAt(i, phase, _peopleClock);
                float a = show * held.Smoke * alpha;
                if (a <= 0.004f)
                {
                    continue;
                }

                Vector2 at = ToScreen(held.Flues[f] + offset);
                var size = new Vector2(2f * radius, 1.56f * radius) * _pixelsPerTile;
                DrawTextureRect(puffs[(i + f) % PuffPaintings], new Rect2(at - (size / 2f), size), tile: false, modulate: new Color(1f, 1f, 1f, a));
            }
        }
    }

    private static float RoofHash01(int seed, int flue) => Hash01(seed, flue, 7919);

    /// <summary>Pure: how far a puff painting's edge reaches at this angle (0..1 of its half-width) — three slow waves, its own.</summary>
    internal static float PuffEdgeAt(int variant, float angle)
    {
        float p1 = Hash01(variant, 1, 4441) * Mathf.Tau, p2 = Hash01(variant, 2, 4441) * Mathf.Tau, p3 = Hash01(variant, 3, 4441) * Mathf.Tau;
        return 0.8f + (0.07f * Mathf.Sin((3f * angle) + p1)) + (0.045f * Mathf.Sin((5f * angle) + p2)) + (0.025f * Mathf.Sin((9f * angle) + p3));
    }

    /// <summary>The puffs, each painted once (A145): <see cref="PuffPaintings"/> of them.</summary>
    private Texture2D[] ThePuffs()
    {
        if (_puffs is not null)
        {
            return _puffs;
        }

        _puffs = new Texture2D[PuffPaintings];
        for (int v = 0; v < PuffPaintings; v++)
        {
            Image image = Image.CreateFromData(PuffPixels, PuffPixels, false, Image.Format.Rgba8, PaintThePuff(v));
            image.GenerateMipmaps();
            _puffs[v] = ImageTexture.CreateFromImage(image);
        }

        return _puffs;
    }

    /// <summary>
    /// ⭐ Pure: one puff painting, RGBA8, <see cref="PuffPixels"/> square (A145, <c>indoors-and-smoke.md §7b</c>) — the watercolour's
    /// touch, as the game's other washes have it: <b>a ragged edge</b> (the outline wanders with three slow waves, its own for
    /// each painting), <b>a pooled rim</b> where the pigment dried darkest, <b>granulation</b> (the pigment settling in the paper's
    /// grain, a speckle at a few pixels' scale), <b>a bloom</b> (a paler patch where the water ran back), and <b>warm and cool
    /// greys</b> drifting across it. Darker than A139's one plain disc.
    /// </summary>
    internal static byte[] PaintThePuff(int variant)
    {
        const int n = PuffPixels;
        var rgba = new byte[n * n * 4];
        var bloom = new Vector2(Hash01(variant, 4, 4441) - 0.5f, Hash01(variant, 5, 4441) - 0.5f) * 0.6f;
        float drift = Hash01(variant, 6, 4441) * Mathf.Tau;

        // Granulation: value noise on a 3-pixel lattice, smoothed between its corners.
        float Grain(int x, int y)
        {
            int cx = x / 3, cy = y / 3;
            float fx = (x % 3) / 3f, fy = (y % 3) / 3f;
            float a = Hash01(cx, cy, 9001 + variant), b = Hash01(cx + 1, cy, 9001 + variant);
            float c = Hash01(cx, cy + 1, 9001 + variant), d = Hash01(cx + 1, cy + 1, 9001 + variant);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2(x + 0.5f - (n / 2f), y + 0.5f - (n / 2f)) / (n / 2f);
                float angle = Mathf.Atan2(p.Y, p.X);
                float edge = PuffEdgeAt(variant, angle);
                float d = p.Length() / edge;
                float fill = 1f - Mathf.SmoothStep(0.72f, 1f, d);
                float rim = Mathf.Exp(-Mathf.Pow((d - 0.86f) / 0.1f, 2f)) * (1f - Mathf.SmoothStep(0.94f, 1.02f, d));
                float pale = Mathf.Exp(-(p - bloom).LengthSquared() / 0.09f);
                float grain = 0.78f + (0.44f * Grain(x, y));
                float pigment = Mathf.Clamp(((0.74f * fill * (1f - (0.38f * pale))) + (0.42f * rim)) * grain, 0f, 1f);
                float tint = 0.5f + (0.5f * Mathf.Sin((p.X * 2.2f) + (p.Y * 1.4f) + drift));
                Color wash = PuffWarm.Lerp(PuffCool, tint).Lerp(PuffEdge, Mathf.Clamp(rim * 0.95f, 0f, 1f));
                int at = ((y * n) + x) * 4;
                rgba[at] = (byte)Mathf.RoundToInt(wash.R * 255f);
                rgba[at + 1] = (byte)Mathf.RoundToInt(wash.G * 255f);
                rgba[at + 2] = (byte)Mathf.RoundToInt(wash.B * 255f);
                rgba[at + 3] = (byte)Mathf.RoundToInt(pigment * 255f);
            }
        }

        return rgba;
    }
}
