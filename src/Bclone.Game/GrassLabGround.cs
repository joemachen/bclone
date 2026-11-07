using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The lab's fake valley — <b>a coverage map standing in for the bake's</b>, so the terrain shader can be tuned
/// without touching <see cref="ValleyTexture"/> (`specs/terrain-shader.md §4`, A1).
/// </summary>
/// <remarks>
/// <para>
/// One RGBA8 image of weights: <b>R grass, G wood, B stone, A water</b>, summing to one, off the map all grass as
/// <c>ValleyTexture.Blended</c> reads it. R is the channel the bake will write when the shader is wired in.
/// </para>
/// <para>
/// ⚠️ <b>A test scene, not a valley.</b> The lab's village is fixed, so the ground is laid out round it: the placeholders
/// on open grass, their wood on a wood, the river below the granary at the real two tiles wide, and every junction
/// of kinds on screen at the opening zoom.
/// </para>
/// <para>
/// A pure function of the tile: integer hashes (<c>at_hash</c>'s, so the shader's noise is the same noise), no RNG, no
/// clock, no sim.
/// </para>
/// </remarks>
internal static class GrassLabGround
{
    /// <summary>The valley's size, as the game's map is.</summary>
    internal const int TilesWide = 120;

    internal const int TilesTall = 80;

    /// <summary>Half the bake's 16: the weights are smooth, and bilinear filtering reads them between texels.</summary>
    internal const int PixelsPerTile = 8;

    internal const int ImageWide = TilesWide * PixelsPerTile;

    internal const int ImageTall = TilesTall * PixelsPerTile;

    /// <summary>The weight at which a kind wins its contour.</summary>
    internal const float Contour = 0.5f;

    /// <summary>
    /// ⛔ <c>ValleyTexture.WarpOctaves</c>' numbers, copied because that file is out of this lane's reach. D342's rule binds
    /// them: an octave's wavelength must exceed the narrowest feature it may touch, and the river is two tiles wide.
    /// </summary>
    private static readonly (float Tiles, float CellsPerTile, uint Salt)[] WarpOctaves =
    {
        (0.55f, 0.12f, 61u),
        (0.30f, 0.45f, 62u),
        (0.12f, 0.95f, 63u),
    };

    /// <summary>A wood's edge carries no promise the river's does, so it wanders further (<c>HowFreeToWander</c>).</summary>
    private const float WoodWander = 0.9f;

    /// <summary>The narrowest the river's half-width gets, in tiles: a little over one, so warp cannot pinch it under two.</summary>
    private const float RiverHalfWidth = 1.15f;

    /// <summary>Where the woods stand: centre and radii, in tiles from the window's centre. The first is the placeholders'.</summary>
    private static readonly (Vector2 At, Vector2 Radii)[] Woods =
    {
        (new Vector2(9.5f, -3f), new Vector2(5.2f, 5.6f)),
        (new Vector2(16f, 11f), new Vector2(7f, 5f)),
        (new Vector2(-22f, -9f), new Vector2(8f, 5.5f)),
        (new Vector2(-38f, -24f), new Vector2(12f, 8f)),
        (new Vector2(-42f, 20f), new Vector2(10f, 7f)),
        (new Vector2(44f, -20f), new Vector2(11f, 8f)),
    };

    /// <summary>Where the stone lies: one against the wood, one against the river.</summary>
    private static readonly (Vector2 At, Vector2 Radii)[] Stones =
    {
        (new Vector2(4.5f, -10.5f), new Vector2(5f, 2.4f)),
        (new Vector2(-9f, 9f), new Vector2(3.6f, 2.2f)),
        (new Vector2(40f, 24f), new Vector2(8f, 4f)),
    };

    /// <summary>The weights at a tile: wood, stone and water, and the grass what is left.</summary>
    internal static (float Grass, float Wood, float Stone, float Water) WeightsAt(Vector2 tile)
    {
        Vector2 at = Warped(tile);
        float water = Ramp(RiverInside(at));
        float wander = WoodWander * ((2f * Noise(tile.X * 0.22f, tile.Y * 0.22f, 64u)) - 1f);
        float wood = (1f - water) * Strongest(Woods, at, wander);
        float stone = (1f - water - wood) * Strongest(Stones, at, 0f);
        return (1f - water - wood - stone, wood, stone, water);
    }

    /// <summary>The whole map, as the texture's bytes; R is whatever the others leave, so a texel sums to 255 exactly.</summary>
    internal static byte[] Bake()
    {
        var bytes = new byte[ImageWide * ImageTall * 4];
        for (int y = 0; y < ImageTall; y++)
        {
            for (int x = 0; x < ImageWide; x++)
            {
                (_, float wood, float stone, float water) = WeightsAt(TileOf(x, y));
                int g = Mathf.RoundToInt(wood * 255f);
                int b = Mathf.RoundToInt(stone * 255f);
                int a = Mathf.RoundToInt(water * 255f);
                int at = ((y * ImageWide) + x) * 4;
                bytes[at] = (byte)Math.Max(0, 255 - g - b - a);
                bytes[at + 1] = (byte)g;
                bytes[at + 2] = (byte)b;
                bytes[at + 3] = (byte)a;
            }
        }

        return bytes;
    }

    /// <summary>The map as a texture. Linear filtering and the clamp are the shader's sampler hints.</summary>
    internal static ImageTexture Texture() =>
        ImageTexture.CreateFromImage(Image.CreateFromData(ImageWide, ImageTall, false, Image.Format.Rgba8, Bake()));

    /// <summary>The tile a texel's centre stands on, in tiles from the map's centre.</summary>
    internal static Vector2 TileOf(int x, int y) => new(
        ((x + 0.5f) / PixelsPerTile) - (TilesWide / 2f),
        ((y + 0.5f) / PixelsPerTile) - (TilesTall / 2f));

    /// <summary>
    /// ⭐ The narrowest the river gets across the map, in tiles of water. Two is what the game's river is, and what the
    /// bleed's limit rests on (`terrain-shader.md §3.2`).
    /// </summary>
    internal static float NarrowestRiver()
    {
        float narrowest = float.MaxValue;
        for (int x = 0; x < ImageWide; x++)
        {
            int wet = 0;
            for (int y = 0; y < ImageTall; y++)
            {
                if (WeightsAt(TileOf(x, y)).Water >= Contour)
                {
                    wet++;
                }
            }

            narrowest = Math.Min(narrowest, wet / (float)PixelsPerTile);
        }

        return narrowest;
    }

    /// <summary>The sample point, wandered by the bake's three octaves. Bounded, as the bake's is.</summary>
    private static Vector2 Warped(Vector2 tile)
    {
        Vector2 at = tile;
        foreach ((float tiles, float cells, uint salt) in WarpOctaves)
        {
            at.X += tiles * ((2f * Noise(tile.X * cells, tile.Y * cells, salt)) - 1f);
            at.Y += tiles * ((2f * Noise((tile.X * cells) + 7f, (tile.Y * cells) + 3f, salt + 10u)) - 1f);
        }

        return at;
    }

    /// <summary>How far inside the river a point is, in tiles; negative on the land.</summary>
    private static float RiverInside(Vector2 at)
    {
        float centre = 10f + (2.6f * Mathf.Sin((at.X * 0.11f) + 0.6f)) + (1.2f * Mathf.Sin((at.X * 0.29f) + 2f));
        float half = RiverHalfWidth + (0.5f * Noise(at.X * 0.07f, 0f, 65u));
        return half - Math.Abs(at.Y - centre);
    }

    /// <summary>The strongest of a kind's patches at a point.</summary>
    private static float Strongest((Vector2 At, Vector2 Radii)[] patches, Vector2 at, float wander)
    {
        float strongest = 0f;
        foreach ((Vector2 centre, Vector2 radii) in patches)
        {
            float inside = (1f - ((at - centre) / radii).Length()) * Math.Min(radii.X, radii.Y);
            strongest = Math.Max(strongest, Ramp(inside + wander));
        }

        return strongest;
    }

    /// <summary>From tiles inside a contour to a weight: a half at the contour, whole a tile in, as the bake's kernel is.</summary>
    private static float Ramp(float tilesInside) => Mathf.SmoothStep(-1f, 1f, tilesInside);

    /// <summary><c>at_value_noise</c>, in C#: one lattice, smoothstep between its corners.</summary>
    private static float Noise(float x, float y, uint salt)
    {
        int cx = (int)Math.Floor(x);
        int cy = (int)Math.Floor(y);
        float fx = x - cx;
        float fy = y - cy;
        float ux = fx * fx * (3f - (2f * fx));
        float uy = fy * fy * (3f - (2f * fy));
        float top = Mathf.Lerp(Rand(cx, cy, salt), Rand(cx + 1, cy, salt), ux);
        float bottom = Mathf.Lerp(Rand(cx, cy + 1, salt), Rand(cx + 1, cy + 1, salt), ux);
        return Mathf.Lerp(top, bottom, uy);
    }

    /// <summary><c>at_rand</c>: a number in [0, 1) for a lattice cell and a salt, 24 bits, exact in a float.</summary>
    private static float Rand(int cx, int cy, uint salt) =>
        (Hash(unchecked((uint)cx + salt), unchecked((uint)cy + (salt * 7u))) >> 8) / 16777216f;

    /// <summary><c>at_hash</c>: PCG2D (Jarzynski &amp; Olano 2020), the first component.</summary>
    private static uint Hash(uint x, uint y)
    {
        unchecked
        {
            x = (x * 1664525u) + 1013904223u;
            y = (y * 1664525u) + 1013904223u;
            x += y * 1664525u;
            y += x * 1664525u;
            x ^= x >> 16;
            y ^= y >> 16;
            x += y * 1664525u;
            y += x * 1664525u;
            x ^= x >> 16;
            return x;
        }
    }
}
