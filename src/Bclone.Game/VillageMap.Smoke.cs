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

    /// <summary>The wash and its pooled edge (mock B, darkened to read on snow — measured in the A138 mock).</summary>
    private static readonly Color PuffWash = new("#b0a89e"), PuffEdge = new("#787068");

    private Texture2D? _puff;

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

        Texture2D puff = ThePuff();
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
                DrawTextureRect(puff, new Rect2(at - (size / 2f), size), tile: false, modulate: new Color(1f, 1f, 1f, a));
            }
        }
    }

    private static float RoofHash01(int seed, int flue) => Hash01(seed, flue, 7919);

    /// <summary>One puff, painted once: a soft wash with a darker pooled edge, as the game's washes pool.</summary>
    private Texture2D ThePuff()
    {
        if (_puff is not null)
        {
            return _puff;
        }

        const int n = 64;
        var image = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x + 0.5f - (n / 2f), y + 0.5f - (n / 2f)).Length() / (n / 2f);
                float fill = 1f - Mathf.SmoothStep(0.72f, 1f, d);
                float ring = Mathf.Exp(-Mathf.Pow((d - 0.8f) / 0.13f, 2f)) * (1f - Mathf.SmoothStep(0.9f, 1f, d));
                float a = Mathf.Clamp((0.72f * fill) + (0.3f * ring), 0f, 1f);
                Color c = PuffWash.Lerp(PuffEdge, Mathf.Clamp(ring * 0.9f, 0f, 1f));
                image.SetPixel(x, y, c with { A = a });
            }
        }

        image.GenerateMipmaps();
        _puff = ImageTexture.CreateFromImage(image);
        return _puff;
    }
}
