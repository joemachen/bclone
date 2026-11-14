using System.Diagnostics;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The painted paths (A72, <c>specs/painted-paths.md</c>, Joe: <i>"B, painted track"</i>): with the watercolour tick on,
/// the worn tracks are painted into the ground by the valley's shader from a field baked once a network — the same lanes,
/// widths, tapers and junctions the mesh draws, so where a path is drawn does not change; only how it looks.
/// </summary>
/// <remarks>
/// View only. The sim's paths (<c>desire-paths.md</c>) are read, never written; the field is never hashed or saved (D335).
/// With the tick off the mesh is drawn as before, byte for byte.
/// </remarks>
public partial class VillageMap
{
    /// <summary>Texels of the path field a tile (§4): a straight lane's distance is linear across a texel, so eight is exact.</summary>
    internal const int PathTexelsPerTile = 8;

    /// <summary>The signed distance to a track's edge the field holds, tiles: inside negative, nothing past the far end.</summary>
    internal const float PathFieldNear = -0.25f;

    internal const float PathFieldFar = 0.6f;

    /// <summary>The field's texture, the network it was baked for, and how many bakes there have been — for the probe.</summary>
    private ImageTexture? _pathsTexture;

    private TrailNetwork? _pathsOf;

    internal int PathFieldBakes { get; private set; }

    internal double LastPathFieldMs { get; private set; }

    /// <summary>Whether the paths are painted into the ground rather than meshed: the tick on, the look's switch on.</summary>
    private bool PathsArePainted => _watercolour && _watercolourMaterial is not null && ValleyWatercolourLook.Current.PathOn;

    /// <summary>
    /// The field to the shader — <b>only when the network is new</b>: every build of <see cref="TrailNetwork"/> is a new
    /// object, made once a collection (the paths' generation, the walls'), never a frame (CLAUDE.md, D338).
    /// </summary>
    private void HandThePathsOver()
    {
        ShaderMaterial? material = _watercolourMaterial;
        if (material is null || ReferenceEquals(_pathsOf, _network))
        {
            return;
        }

        long started = Stopwatch.GetTimestamp();
        _pathsOf = _network;
        SimConfig config = _world!.Config;
        byte[] field = PathField(_network.Lanes, _network.Junctions, config.MapMinX, config.MapMinY, config.MapWidth, config.MapHeight);
        int w = config.MapWidth * PathTexelsPerTile, h = config.MapHeight * PathTexelsPerTile;
        using (Image image = Image.CreateFromData(w, h, false, Image.Format.Rgb8, field)) // ⛔ `using` (A45)
        {
            if (_pathsTexture is null || _pathsTexture.GetWidth() != w || _pathsTexture.GetHeight() != h)
            {
                _pathsTexture = ImageTexture.CreateFromImage(image);
            }
            else
            {
                _pathsTexture.Update(image);
            }
        }

        material.SetShaderParameter("path_map", _pathsTexture);
        material.SetShaderParameter("paths_on", true);
        PathFieldBakes++;
        LastPathFieldMs = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
    }

    /// <summary>The look's path values to the layer, once, as it is built.</summary>
    private static void HandThePathLookOver(ShaderMaterial material, ValleyWatercolourLook look)
    {
        // Off until a field is handed over: an unset field reads black, which would say "inside a track" everywhere.
        material.SetShaderParameter("paths_on", false);
        material.SetShaderParameter("path_field_range", new Vector2(PathFieldNear, PathFieldFar));
        material.SetShaderParameter("path_worn_srgb", Srgb(look.PathWorn));
        material.SetShaderParameter("path_packed_srgb", Srgb(look.PathPacked));
        material.SetShaderParameter("path_halo_srgb", Srgb(look.PathHalo));
        material.SetShaderParameter("path_halo_tiles", look.PathHaloTiles);
        material.SetShaderParameter("path_halo_strength", look.PathHaloStrength);
        material.SetShaderParameter("path_mottle", look.PathMottle);
        material.SetShaderParameter("path_rim", look.PathRim);
        material.SetShaderParameter("path_ruts", look.PathRuts);
        material.SetShaderParameter("path_wobble_tiles", look.PathWobbleTiles);
        material.SetShaderParameter("path_pen_strength", look.PathPenStrength);
        material.SetShaderParameter("path_pen_worn_share", look.PathPenWornShare);
        material.SetShaderParameter("path_pen_width_share", look.PathPenWidthShare);
        material.SetShaderParameter("path_pen_fade_px", look.PathPenFadePxPerTile);
        material.SetShaderParameter("path_snow_clear", look.PathSnowClear);
    }

    /// <summary>
    /// ⭐ The path field (§4): for every texel of the map, eight a tile, the lane feature whose edge lies nearest — R its
    /// signed distance to the edge (tiles, <see cref="PathFieldNear"/> to <see cref="PathFieldFar"/>, inside negative),
    /// G its half-width there (thousandths of a tile), B whether it is packed. Pure: the same network, the same bytes.
    /// </summary>
    /// <remarks>
    /// The features are the mesh's own (<c>LayTrails</c>): every segment of a lane at the worn half-width, or the packed one
    /// where both its points are packed; a dead end tapering to <see cref="TrailEndWidthShare"/> over its last segment (a
    /// two-point lane dead at both ends from its middle); a lone point a disc; a junction a disc
    /// <see cref="TrailJunctionWidening"/> wider. The least signed distance wins, so a junction's wider disc covers its lanes
    /// as the mesh's does.
    /// </remarks>
    internal static byte[] PathField(
        IReadOnlyList<TrailNetwork.Lane> lanes, IReadOnlyList<(Vector2 At, byte Grade)> junctions, int minX, int minY, int width, int height)
    {
        int m = PathTexelsPerTile, bw = width * m, bh = height * m;
        var best = new float[bw * bh];
        Array.Fill(best, PathFieldFar);
        var bytes = new byte[bw * bh * 3];
        for (int i = 0; i < bw * bh; i++)
        {
            bytes[i * 3] = 255;
        }

        foreach (TrailNetwork.Lane lane in lanes)
        {
            Vector2[] p = lane.Points;
            int n = p.Length;
            if (n == 1)
            {
                bool lone = lane.Grades[0] >= 2;
                float half = lone ? TrailHalfWidth : WornTrailHalfWidth;
                Feature(p[0], p[0], half, half, lone);
                continue;
            }

            int segments = lane.Closed ? n : n - 1;
            for (int k = 0; k < segments; k++)
            {
                Vector2 a = p[k], b = p[(k + 1) % n];
                bool packed = lane.Grades[k] >= 2 && lane.Grades[(k + 1) % n] >= 2;
                float baseHalf = packed ? TrailHalfWidth : WornTrailHalfWidth;
                bool deadStart = !lane.Closed && k == 0 && lane.StartsAtADeadEnd;
                bool deadEnd = !lane.Closed && k == segments - 1 && lane.EndsAtADeadEnd;
                if (deadStart && deadEnd)
                {
                    Vector2 middle = (a + b) / 2f;
                    Feature(a, middle, baseHalf * TrailEndWidthShare, baseHalf, packed);
                    Feature(middle, b, baseHalf, baseHalf * TrailEndWidthShare, packed);
                    continue;
                }

                Feature(a, b, deadStart ? baseHalf * TrailEndWidthShare : baseHalf, deadEnd ? baseHalf * TrailEndWidthShare : baseHalf, packed);
            }
        }

        foreach ((Vector2 at, byte grade) in junctions)
        {
            bool packed = grade >= 2;
            float half = (packed ? TrailHalfWidth : WornTrailHalfWidth) * TrailJunctionWidening;
            Feature(at, at, half, half, packed);
        }

        return bytes;

        // A capsule from a to b whose half-width runs from ha to hb, over the texels within reach of it.
        void Feature(Vector2 a, Vector2 b, float ha, float hb, bool packed)
        {
            float reach = Math.Max(ha, hb) + PathFieldFar;
            // Texel (tx, ty)'s centre is at tile minX − ½ + (tx + ½) / m.
            int x0 = Math.Max(0, (int)MathF.Floor((Math.Min(a.X, b.X) - reach - minX + 0.5f) * m));
            int x1 = Math.Min(bw - 1, (int)MathF.Ceiling((Math.Max(a.X, b.X) + reach - minX + 0.5f) * m));
            int y0 = Math.Max(0, (int)MathF.Floor((Math.Min(a.Y, b.Y) - reach - minY + 0.5f) * m));
            int y1 = Math.Min(bh - 1, (int)MathF.Ceiling((Math.Max(a.Y, b.Y) + reach - minY + 0.5f) * m));
            Vector2 ab = b - a;
            float length2 = ab.LengthSquared();
            for (int ty = y0; ty <= y1; ty++)
            {
                float py = minY - 0.5f + ((ty + 0.5f) / m);
                for (int tx = x0; tx <= x1; tx++)
                {
                    float px = minX - 0.5f + ((tx + 0.5f) / m);
                    var at = new Vector2(px, py);
                    float t = length2 > 1e-8f ? Math.Clamp((at - a).Dot(ab) / length2, 0f, 1f) : 0f;
                    float half = ha + ((hb - ha) * t);
                    float signed = (at - (a + (ab * t))).Length() - half;
                    int i = (ty * bw) + tx;
                    if (signed < best[i])
                    {
                        best[i] = signed;
                        float r = (Math.Max(signed, PathFieldNear) - PathFieldNear) / (PathFieldFar - PathFieldNear);
                        bytes[i * 3] = (byte)Math.Clamp(MathF.Round(r * 255f), 0f, 255f);
                        bytes[(i * 3) + 1] = (byte)Math.Clamp(MathF.Round(half * 1000f), 0f, 255f);
                        bytes[(i * 3) + 2] = packed ? (byte)255 : (byte)0;
                    }
                }
            }
        }
    }

    /// <summary>The field's signed distance at a texel, back in tiles — for the probe.</summary>
    internal static float PathFieldSignedAt(byte[] field, int bw, int tx, int ty) =>
        PathFieldNear + (field[((ty * bw) + tx) * 3] / 255f * (PathFieldFar - PathFieldNear));

    /// <summary>The texel whose centre is nearest a tile-space point, for a field whose map starts at minX, minY.</summary>
    internal static (int X, int Y) PathTexelOf(Vector2 at, int minX, int minY) =>
        ((int)MathF.Floor((at.X - minX + 0.5f) * PathTexelsPerTile), (int)MathF.Floor((at.Y - minY + 0.5f) * PathTexelsPerTile));

    /// <summary>
    /// ⭐ The probe's <c>painted paths:</c> line (A72, <c>painted-paths.md §6</c>): the field on a made-up network, then on
    /// the probe's own valley with the tick on — the field built for the network, the look handed over, the mesh not drawn.
    /// </summary>
    /// <remarks>⚠️ The shader runs only inside a drawn frame, which never happens headless (D340): renders are its check.</remarks>
    internal string ThePaintedPathsHold()
    {
        if (_world is not SimWorld world)
        {
            return "[widths] painted paths: ⛔ no world";
        }

        var faults = new List<string>();
        try
        {
            // A made-up network on a 12 × 6 map whose corner tile is (0, 0): a packed lane along y = 1 from x = 1 to 10; a worn
            // lane down x = 5 from y = 1 to a dead end at y = 5; a packed junction where they meet.
            var packedLane = new TrailNetwork.Lane(new[] { new Vector2(1, 1), new Vector2(5, 1), new Vector2(10, 1) }, new byte[] { 2, 2, 2 }, false, false, false);
            var wornLane = new TrailNetwork.Lane(new[] { new Vector2(5, 1), new Vector2(5, 3), new Vector2(5, 5) }, new byte[] { 1, 1, 1 }, false, false, true);
            var lanes = new[] { packedLane, wornLane };
            var meets = new[] { (new Vector2(5, 1), (byte)2) };
            byte[] field = PathField(lanes, meets, 0, 0, 12, 6);
            int bw = 12 * PathTexelsPerTile;
            float At(float x, float y)
            {
                (int tx, int ty) = PathTexelOf(new Vector2(x, y), 0, 0);
                return PathFieldSignedAt(field, bw, tx, ty);
            }

            byte PackedAt(float x, float y)
            {
                (int tx, int ty) = PathTexelOf(new Vector2(x, y), 0, 0);
                return field[(((ty * bw) + tx) * 3) + 2];
            }

            // ⚠️ Every point asked is a texel's centre (multiples of an eighth, less a sixteenth), so the field is compared with
            // the exact distance there, not with a texel's neighbour's.
            // On the packed lane's middle: inside by its half-width less a sixteenth; a tile off it: past the field's reach.
            float middle = At(8.0625f, 1.0625f), off = At(8.0625f, 2.0625f);
            if (MathF.Abs(middle - (0.0625f - TrailHalfWidth)) > 0.005f || off < PathFieldFar - 0.005f)
            {
                faults.Add($"the packed lane reads {middle:0.####} on its middle and {off:0.###} a tile off (want {0.0625f - TrailHalfWidth:0.####}, and the reach {PathFieldFar})");
            }

            // Packed on the packed lane, not on the worn one; and each lane's half-width, for the ruts, in thousandths.
            if (PackedAt(8.0625f, 1.0625f) != 255 || PackedAt(5.0625f, 3.0625f) != 0)
            {
                faults.Add($"packed reads {PackedAt(8.0625f, 1.0625f)} on the packed lane and {PackedAt(5.0625f, 3.0625f)} on the worn one (want 255, 0)");
            }

            (int hx, int hy) = PathTexelOf(new Vector2(8.0625f, 1.0625f), 0, 0);
            (int wx, int wy) = PathTexelOf(new Vector2(5.0625f, 2.0625f), 0, 0);
            byte packedHalf = field[(((hy * bw) + hx) * 3) + 1], wornHalf = field[(((wy * bw) + wx) * 3) + 1];
            if (packedHalf != 170 || wornHalf != 140)
            {
                faults.Add($"the half-widths read {packedHalf} on the packed lane and {wornHalf} on the worn (want 170, 140 thousandths)");
            }

            // The edges where the mesh's are: the packed lane's at 0.17 of a tile, the worn's at 0.14 — a texel 0.1875 out reads
            // the difference, outside by a little on both.
            float packedEdge = At(8.0625f, 1.1875f), wornEdge = At(5.1875f, 3.0625f);
            if (MathF.Abs(packedEdge - (0.1875f - TrailHalfWidth)) > 0.005f || MathF.Abs(wornEdge - (0.1875f - WornTrailHalfWidth)) > 0.005f)
            {
                faults.Add($"0.1875 out the packed lane reads {packedEdge:0.####} and the worn {wornEdge:0.####} (want {0.1875f - TrailHalfWidth:0.####} and {0.1875f - WornTrailHalfWidth:0.####}: the edges at 0.17 and 0.14)");
            }

            // The dead end narrower than its lane: a sixteenth off its line is outside by its tip, inside two tiles back.
            float atTheTip = At(5.0625f, 4.9375f), alongIt = At(5.0625f, 3.0625f);
            if (!(atTheTip > 0f) || !(alongIt < 0f))
            {
                faults.Add($"the dead end reads {atTheTip:0.####} by its tip and {alongIt:0.####} two tiles back (want outside, inside)");
            }

            // The junction wider than its lanes: on the far side of the packed lane from the worn one, just past the packed
            // lane's edge, the junction's disc still holds the texel — and without the junction it does not.
            float byTheJunction = At(5.0625f, 0.8125f);
            byte[] noJunction = PathField(lanes, Array.Empty<(Vector2, byte)>(), 0, 0, 12, 6);
            (int jx, int jy) = PathTexelOf(new Vector2(5.0625f, 0.8125f), 0, 0);
            float withoutIt = PathFieldSignedAt(noJunction, bw, jx, jy);
            if (!(byTheJunction < 0f) || !(withoutIt > 0f))
            {
                faults.Add($"beside the junction the field reads {byTheJunction:0.####} and without the junction {withoutIt:0.####} (want inside, outside: the disc is wider)");
            }

            // The same bytes twice; an empty network bakes nothing.
            if (!field.AsSpan().SequenceEqual(PathField(lanes, meets, 0, 0, 12, 6)))
            {
                faults.Add("the same network baked twice is not the same bytes");
            }

            byte[] empty = PathField(Array.Empty<TrailNetwork.Lane>(), Array.Empty<(Vector2, byte)>(), 0, 0, 12, 6);
            for (int i = 0; i < empty.Length; i += 3)
            {
                if (empty[i] != 255)
                {
                    faults.Add("an empty network marks a texel");
                    break;
                }
            }

            // The probe's own valley, the tick on: the field baked for the network on screen, the look handed over, no mesh.
            bool wasOn = _watercolour;
            ShowWatercolour(true);
            CollectTheTrailsIfTheyMoved(world);
            _pathsOf = null;
            HandThePathsOver();
            ShaderMaterial? material = _watercolourMaterial;
            ValleyWatercolourLook look = ValleyWatercolourLook.Current;
            SimConfig config = world.Config;
            bool painted = PathsArePainted;
            ShowWatercolour(false);
            bool paintedOff = PathsArePainted;
            ShowWatercolour(wasOn);
            if (!painted || paintedOff || !ReferenceEquals(_pathsOf, _network) || _pathsTexture is null
                || _pathsTexture.GetWidth() != config.MapWidth * PathTexelsPerTile || _pathsTexture.GetHeight() != config.MapHeight * PathTexelsPerTile)
            {
                faults.Add($"with the tick on the paths are painted {painted} (want true), off {paintedOff} (want false); the field {_pathsTexture?.GetWidth()} × {_pathsTexture?.GetHeight()} (want eight texels a tile), baked for the network on screen {ReferenceEquals(_pathsOf, _network)}");
            }

            if (material is null
                || !material.GetShaderParameter("paths_on").AsBool()
                || material.GetShaderParameter("path_map").AsGodotObject() != _pathsTexture
                || material.GetShaderParameter("path_field_range").AsVector2() != new Vector2(PathFieldNear, PathFieldFar)
                || material.GetShaderParameter("path_worn_srgb").AsVector3() != Srgb(look.PathWorn)
                || material.GetShaderParameter("path_packed_srgb").AsVector3() != Srgb(look.PathPacked)
                || material.GetShaderParameter("path_halo_srgb").AsVector3() != Srgb(look.PathHalo)
                || material.GetShaderParameter("path_halo_tiles").AsVector2() != look.PathHaloTiles
                || material.GetShaderParameter("path_halo_strength").AsVector2() != look.PathHaloStrength
                || material.GetShaderParameter("path_mottle").AsVector2() != look.PathMottle
                || material.GetShaderParameter("path_rim").AsVector2() != look.PathRim
                || material.GetShaderParameter("path_ruts").AsSingle() != look.PathRuts
                || material.GetShaderParameter("path_wobble_tiles").AsSingle() != look.PathWobbleTiles
                || material.GetShaderParameter("path_pen_strength").AsSingle() != look.PathPenStrength
                || material.GetShaderParameter("path_pen_worn_share").AsSingle() != look.PathPenWornShare
                || material.GetShaderParameter("path_pen_width_share").AsSingle() != look.PathPenWidthShare
                || material.GetShaderParameter("path_pen_fade_px").AsSingle() != look.PathPenFadePxPerTile
                || material.GetShaderParameter("path_snow_clear").AsSingle() != look.PathSnowClear)
            {
                faults.Add("a painted path value on the layer is not the look file's, or the field not handed over");
            }

            // The look draws a track: on, the dirt lighter than the halo, the packed dirt deeper than the worn, a pen.
            if (!look.PathOn || !(look.PathWorn.Luminance > look.PathPacked.Luminance) || !(look.PathPacked.Luminance > look.PathHalo.Luminance)
                || look.PathPenStrength <= 0f || !(look.PathPenWornShare < 1f) || look.PathWobbleTiles >= WornTrailHalfWidth * 0.5f)
            {
                faults.Add($"{ValleyWatercolourLook.Path} draws no track: on {look.PathOn}, the worn dirt not lighter than the packed or the packed not lighter than the halo, no pen, the worn pen not fainter, or the edge wandering half a lane");
            }

            return faults.Count == 0
                ? $"[widths] painted paths: ✅ on a made-up network the lanes inside and a tile off outside, packed apart from worn, the edges at the mesh's widths, the dead end narrower and the junction wider, the same bytes twice, nothing from no network; the valley's field {config.MapWidth * PathTexelsPerTile} × {config.MapHeight * PathTexelsPerTile} for {_network.Lanes.Count} lanes in {LastPathFieldMs:F1}ms; the look's values handed over; the mesh not drawn with the tick, drawn without"
                : $"[widths] painted paths: ⛔ {string.Join("; ", faults)}";
        }
        catch (Exception e)
        {
            // Trap 33: a probe line that throws never reaches `done.` and leaves a headless Godot running.
            return $"[widths] painted paths: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}
