using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The hills, shaded (`specs/heights.md §6`, D735) — a plain light-and-shade wash from the sim's heights, so a slope that
/// slows the walk can be seen at all. Without it the change would show only as slower walking, which would read as a bug.
/// </summary>
/// <remarks>
/// <para>
/// <b>The systems lane's stand-in, not the look.</b> The art lane's relief (`terrain-shader.md §7` piece 5, A57) — contour
/// lines and hachures — replaces it, drawn from the same <see cref="GeneratedMap.HeightAt"/>.
/// </para>
/// <para>
/// <b>Baked once per valley</b> (CLAUDE.md: never rebuilt per frame): the heights never change in play, so the texture is
/// made the first frame a world is drawn and kept until another world is. A flat valley — every save from before heights —
/// bakes nothing and draws nothing.
/// </para>
/// <para>
/// A hillshade lit from the upper left: ground rising towards the lower right faces the light and is washed lighter,
/// ground rising towards the upper left faces away and is washed darker, the flat is untouched. Heights are sampled
/// between tile centres so a slope reads as a slope, not as steps.
/// </para>
/// <para>
/// ⭐ <b>Softened across a tile first</b> (D736; Joe, playing Hilly: <i>"things look a little weird"</i>). The sim's heights
/// are whole levels, so even a smooth hill is a staircase of one-level steps, and a hillshade of a staircase is hatching.
/// Two box passes a tile each way, in the view only — the sim's steepness is the steps, as the walk feels them.
/// </para>
/// </remarks>
public partial class VillageMap
{
    /// <summary>Texels per tile — fine enough that the shading has no visible steps at the usual zoom.</summary>
    internal const int ReliefPerTile = 8;

    /// <summary>How much wash a slope of one level per tile, facing the light squarely, puts down — a level being one of 256 (D736).</summary>
    private const float ReliefGain = 0.06f;

    /// <summary>How far the whole-level steps are softened before shading, in tiles (two passes).</summary>
    private const int ReliefSoftenTiles = 1;

    /// <summary>The most wash any slope gets, light or dark — a wash, not a paint.</summary>
    private const float ReliefMostAlpha = 0.34f;

    private static readonly Color ReliefLight = new("#fffaf0");
    private static readonly Color ReliefDark = new("#2a2014");

    private ImageTexture? _relief;
    private SimWorld? _reliefFor;

    /// <summary>The wash, for the probe: null on a flat valley.</summary>
    internal ImageTexture? Relief => _relief;

    /// <summary>Draw the wash over the ground — baking it the first time this world is drawn.</summary>
    private void DrawTheRelief(Rect2 valley)
    {
        if (!ReferenceEquals(_reliefFor, _world))
        {
            _reliefFor = _world;
            _relief = _world is null ? null : BakeTheRelief(_world.Map);
        }

        if (_relief is not null)
        {
            DrawTextureRect(_relief, valley, tile: false);
        }
    }

    /// <summary>The wash for a valley, or null when it is flat. Its texel rows run as the tiles' do (no flip — `ToScreen`).</summary>
    internal static ImageTexture? BakeTheRelief(GeneratedMap map)
    {
        if (map.Heights.All(h => h == 0))
        {
            return null;
        }

        float[] soft = Softened(map);
        int width = map.Width * ReliefPerTile;
        int height = map.Height * ReliefPerTile;
        var pixels = new byte[width * height * 4];
        for (int py = 0; py < height; py++)
        {
            for (int px = 0; px < width; px++)
            {
                // The texel's centre in tile space, where tile (0, 0)'s centre is (0, 0).
                float u = ((px + 0.5f) / ReliefPerTile) - 0.5f;
                float v = ((py + 0.5f) / ReliefPerTile) - 0.5f;
                float across = HeightBetween(soft, map.Width, map.Height, u + 0.5f, v) - HeightBetween(soft, map.Width, map.Height, u - 0.5f, v);
                float down = HeightBetween(soft, map.Width, map.Height, u, v + 0.5f) - HeightBetween(soft, map.Width, map.Height, u, v - 0.5f);
                float lit = (across + down) * ReliefGain;
                float alpha = Mathf.Min(Mathf.Abs(lit), ReliefMostAlpha);
                Color colour = lit >= 0f ? ReliefLight : ReliefDark;
                int at = ((py * width) + px) * 4;
                pixels[at] = (byte)(colour.R * 255f);
                pixels[at + 1] = (byte)(colour.G * 255f);
                pixels[at + 2] = (byte)(colour.B * 255f);
                pixels[at + 3] = (byte)(alpha * 255f);
            }
        }

        return ImageTexture.CreateFromImage(Image.CreateFromData(width, height, false, Image.Format.Rgba8, pixels));
    }

    /// <summary>The valley's heights softened across <see cref="ReliefSoftenTiles"/>, twice each way (the window cut at the edge).</summary>
    private static float[] Softened(GeneratedMap map)
    {
        int w = map.Width;
        int h = map.Height;
        var values = new float[w * h];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = map.Heights[i];
        }

        // ⭐ Water is drawn level with its banks (D736): the sim's water is 0, and the drop from a bank to it shaded as a dark
        // seam all along the river. The mean of a water tile's land neighbours stands in for it, here only.
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = (y * w) + x;
                if (map.Tiles[i] != Terrain.Water)
                {
                    continue;
                }

                float sum = 0f;
                int count = 0;
                foreach ((int nx, int ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                {
                    if (nx >= 0 && nx < w && ny >= 0 && ny < h && map.Tiles[(ny * w) + nx] != Terrain.Water)
                    {
                        sum += map.Heights[(ny * w) + nx];
                        count++;
                    }
                }

                if (count > 0)
                {
                    values[i] = sum / count;
                }
            }
        }

        for (int pass = 0; pass < 2; pass++)
        {
            values = Along(values, across: true);
            values = Along(values, across: false);
        }

        return values;

        float[] Along(float[] from, bool across)
        {
            var to = new float[from.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    int count = 0;
                    for (int k = -ReliefSoftenTiles; k <= ReliefSoftenTiles; k++)
                    {
                        int sx = across ? x + k : x;
                        int sy = across ? y : y + k;
                        if (sx >= 0 && sx < w && sy >= 0 && sy < h)
                        {
                            sum += from[(sy * w) + sx];
                            count++;
                        }
                    }

                    to[(y * w) + x] = sum / count;
                }
            }

            return to;
        }
    }

    /// <summary>The height between tile centres, by the four round the point (clamped at the valley's edge).</summary>
    private static float HeightBetween(float[] heights, int width, int height, float u, float v)
    {
        u = Mathf.Clamp(u, 0f, width - 1);
        v = Mathf.Clamp(v, 0f, height - 1);
        int x0 = Mathf.FloorToInt(u);
        int y0 = Mathf.FloorToInt(v);
        int x1 = Mathf.Min(x0 + 1, width - 1);
        int y1 = Mathf.Min(y0 + 1, height - 1);
        float fx = u - x0;
        float fy = v - y0;
        float top = Mathf.Lerp(At(x0, y0), At(x1, y0), fx);
        float bottom = Mathf.Lerp(At(x0, y1), At(x1, y1), fx);
        return Mathf.Lerp(top, bottom, fy);

        float At(int x, int y) => heights[(y * width) + x];
    }

    /// <summary>
    /// <c>relief:</c> (D735, `heights.md §10` guard 11) — the shipped valley has hills, so the wash is baked at its size; a
    /// steep tile is washed and the founding ring, kept flat, is not; and a flat valley bakes nothing.
    /// </summary>
    internal string TheHillsAreShaded()
    {
        var faults = new List<string>();
        if (_world is null)
        {
            return "[widths] relief: ⛔ no world";
        }

        GeneratedMap map = _world.Map;
        ImageTexture? baked = BakeTheRelief(map);
        if (baked is null)
        {
            return "[widths] relief: ⛔ the shipped valley baked no wash — it has no hills";
        }

        Image image = baked.GetImage();
        if (image.GetWidth() != map.Width * ReliefPerTile || image.GetHeight() != map.Height * ReliefPerTile)
        {
            faults.Add($"the wash is {image.GetWidth()} × {image.GetHeight()}, not the valley's {map.Width * ReliefPerTile} × {map.Height * ReliefPerTile}");
        }

        float AlphaOver(GridPos tile)
        {
            int x = ((tile.X - map.MinX) * ReliefPerTile) + (ReliefPerTile / 2);
            int y = ((tile.Y - map.MinY) * ReliefPerTile) + (ReliefPerTile / 2);
            return image.GetPixel(x, y).A;
        }

        // The steepest tile in the valley, and the founding site, which the ring keeps flat.
        GridPos steepest = map.FoundingSite;
        for (int i = 0; i < map.Heights.Count; i++)
        {
            if (map.SteepnessAtIndex(i) > map.SteepnessAt(steepest))
            {
                steepest = new GridPos((i % map.Width) + map.MinX, (i / map.Width) + map.MinY);
            }
        }

        float onSteep = AlphaOver(steepest);
        float onFounding = AlphaOver(map.FoundingSite);
        if (onSteep <= 0.02f)
        {
            faults.Add($"the steepest tile ({steepest}, steepness {map.SteepnessAt(steepest)}) is washed {onSteep:F2}");
        }

        // ⚠️ Not "unwashed" (D737): the smoothing lets a nearby bank tilt the ring a little, and a wash strong enough to see
        // shows it. The claim is that the ground the village starts on does not read as a hill.
        if (onFounding > onSteep / 4f)
        {
            faults.Add($"the founding site is washed {onFounding:F2}, more than a quarter of the steepest tile's {onSteep:F2}");
        }

        var flat = new GeneratedMap(map.Width, map.Height, map.MinX, map.MinY, map.Tiles.ToArray(), map.FoundingSite);
        if (BakeTheRelief(flat) is not null)
        {
            faults.Add("a flat valley baked a wash");
        }

        // What's here says the slope on the steep tile (the sim's sentence, `SimWorld.SayTheSlope`).
        string? said = _world.SayTheSlope(steepest);
        if (said is null || !said.Contains("Steep", StringComparison.Ordinal))
        {
            faults.Add($"the steepest tile says \"{said ?? "nothing"}\"");
        }

        return faults.Count == 0
            ? $"[widths] relief: ✅ the hills washed {image.GetWidth()} × {image.GetHeight()}, the steepest tile at {onSteep:F2}, the flat founding at {onFounding:F2}; a flat valley bakes nothing; the steep tile says \"{said}\""
            : $"[widths] relief: ⛔ {string.Join("; ", faults)}";
    }
}
