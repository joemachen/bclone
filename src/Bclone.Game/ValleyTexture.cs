using System.Diagnostics;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐⭐ The valley baked into one texture — <b>and the reason the river stops looking like graph
/// paper</b> (D342).
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe, from play, with a screenshot of Foundation beside ours:</b> *"the riverbank lines look
/// okay, but its the underlying grid structure that makes it look blocky and chunky — i want my
/// rivers and land and trees to look like the rivers in the screenshot — how do they achieve that
/// level of smoothness?"*
/// </para>
/// <para>
/// ⛔⛔ <b>THEIR SMOOTHNESS IS NOT A SMOOTHED OUTLINE. IT IS A LEVEL SET.</b> Foundation's terrain
/// is an interpolated heightmap, so *"where is it wet?"* is the contour where a <b>continuous
/// field</b> crosses the water level — and the contour of a continuous field is smooth at every
/// zoom, for free, without anything ever being smoothed. **Ours was *"which cells carry
/// `Terrain.Water`"*, which can only ever be a staircase** — and D337 traced that staircase and
/// corner-cut it, which is why the bank came out as a chain of semicircles. *Chaikin over one-tile
/// steps produces arcs, because one-tile steps are all it was given.*
/// </para>
/// <para>
/// ⭐ <b>So the sim keeps its tiles — Joe's own closed call (`gridless.md §10.2`) — and the VIEW
/// gets a continuous field derived from them.</b> Each output pixel warps its sample point by
/// value noise, weighs each kind of ground over the tiles around it, and takes the winner. **A
/// smoothly interpolated field, thresholded, IS the marching-squares contour** — so the fill and
/// the edge stop being two things that can disagree, which is exactly what they were.
/// </para>
/// <para>
/// ⭐⭐ <b>ONE EVALUATION ANSWERS FOUR COMPLAINTS.</b> The river's blockiness; the shoreline's
/// scallops (<c>DrawTheShoreline</c> is deleted — the bank is now the same contour as the water);
/// <c>PaintForest</c>'s Manhattan diamonds, which come out as rounded woods; and **the framerate**,
/// because a wood's foliage is texture in the field instead of ~5,700 unbatchable
/// <c>DrawCircle</c> calls a frame.
/// </para>
/// <para>
/// ⚠️ <b>Worked ground is deliberately NOT smoothed and stays in the per-tile pass.</b> A ploughed
/// field is man-made and reads as man-made **because** its edges are straight. *The valley is a
/// field; the fields are not.*
/// </para>
/// <para>
/// ⚠️ <b>A bake has a fixed resolution, and that is the honest cost of choosing it over a shader.</b>
/// Sharp at or below <see cref="PixelsPerTile"/> on screen, and up to 3× soft at
/// <c>MaxPixelsPerTile = 48</c> — where the crisp things (buildings, villagers, zone borders, live
/// canopies) carry the detail and soft ground reads as depth rather than as blur.
/// </para>
/// </remarks>
internal sealed class ValleyTexture
{
    /// <summary>How many image pixels a tile gets. ⚠️ Measured, not guessed — see the probe.</summary>
    internal const int PixelsPerTile = 16;


    /// <summary>How far the kernel reaches when weighing a kind of ground, in tiles.</summary>
    private const float KernelTiles = 1f;

    /// <summary>
    /// How many tiles either side <see cref="Blended"/> actually scans. ⚠️ <b>Widen this
    /// with <see cref="KernelTiles"/> or the kernel is silently clipped.</b>
    /// </summary>
    private const int KernelScanTiles = 1;

    /// <summary>
    /// ⛔⛔ The domain warp — <b>and the rule that sets it, learned by getting it
    /// wrong in both directions</b> (D342).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ FIRST TRY: 0.7 tiles at 1.6 noise cells per tile. It tore the river to pieces</b> —
    /// the baked PNG showed a channel full of holes with islands of water floating beside it.
    /// *The old terrain overdraw's comment says exactly why that is fatal: "a river with gaps in
    /// it reads as a bug rather than as a river."*
    /// </para>
    /// <para>
    /// <b>⛔ SECOND TRY: 0.45 tiles at 0.15 cells per tile. The river came back whole and the
    /// STAIRCASE came back with it</b> — one noise cell every seven tiles is a pure translation
    /// at the scale of a single tile step, so it moved the river without disguising a thing.
    /// </para>
    /// <para>
    /// ⭐⭐ <b>THE RULE, AND IT IS THE WHOLE LESSON: A WARP OCTAVE'S WAVELENGTH MUST
    /// EXCEED THE NARROWEST FEATURE IT IS ALLOWED TO TOUCH.</b> Below that, the two sides of a
    /// thin shape get independent displacements and it pinches apart; above it, they move
    /// together and the shape merely bends. **The river is two tiles wide**, so no octave may go
    /// finer than about half a cell per tile — and the fine detail has to come from an octave
    /// whose amplitude is far smaller than the feature, which nibbles the edge without ever
    /// reaching across it.
    /// </para>
    /// <para>
    /// ⚠️ <b>The total is bounded, and it is what sets <see cref="TilesOfInfluence"/>.</b>
    /// An unbounded warp would let a felled tile change pixels outside the margin the incremental
    /// re-bake repaints, and leave a stale seam nothing ever fixes. *The self-check is what keeps
    /// these two numbers honest with each other.*
    /// </para>
    /// <para>
    /// ⛔ <b>And it stays modest because WATER IS IMPASSABLE.</b> Drawing the river a tile
    /// from where its tiles are would put villagers apparently walking on water, which is
    /// §1.1 failing. *A wood's edge carries no such promise, so it gets its raggedness from
    /// <see cref="HowFreeToWander"/> instead — which pushes on the WEIGHT and therefore
    /// cannot move anything anywhere.*
    /// </para>
    /// </remarks>
    private static readonly (float Tiles, float CellsPerTile)[] WarpOctaves =
    {
        // A meander: bends a whole reach of river, well below the two-tile pinch threshold.
        (0.55f, 0.12f),

        // The one that actually kills the staircase. Wavelength ~2.2 tiles — just over the
        // river's width, which is the closest it may safely come.
        (0.30f, 0.45f),

        // Crinkle. Tiny beside any feature, so it cannot sever one however fine it is.
        (0.12f, 0.95f),
    };

    /// <summary>The warp's total reach in tiles — <b>summed, not guessed</b>.</summary>
    private static readonly float WarpTiles = Total();

    private static float Total()
    {
        float sum = 0f;
        for (int i = 0; i < WarpOctaves.Length; i++)
        {
            sum += WarpOctaves[i].Tiles;
        }

        return sum;
    }

    /// <summary>
    /// ⛔⛔ How far one tile can reach — <b>DERIVED, because it was hand-set for one
    /// commit and cost 35% of the bake</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the kernel radius plus the warp's total amplitude, rounded up. <b>Nothing about a
    /// tile can reach further than that</b>, which is exactly the two things it is used for:
    /// </para>
    /// <para>
    /// ⛔ <b>The repaint margin.</b> Too small and a felled tile leaves a seam in the valley
    /// that nothing ever repaints — *the one bug this whole design can have.* The self-check
    /// compares an incremental re-bake against a full one, which is what keeps this number
    /// honest with <see cref="WarpOctaves"/> rather than merely adjacent to it.
    /// </para>
    /// <para>
    /// ⚠️ <b>And the flatness radius, which is where the time goes.</b> A tile whose
    /// whole neighbourhood is one kind of ground skips the per-pixel blend entirely, and *almost
    /// all of the valley is such a tile* — so widening this by one shrinks the fast path
    /// sharply. **Set to 3 by hand "for safety" it took the bake from 1051 ms to 1415 ms while
    /// changing not one pixel.** Derived, it cannot be wrong in either direction.
    /// </para>
    /// <para>
    /// ⛔⛔ <b>DECLARED HERE, BELOW <see cref="WarpTiles"/>, AND THAT IS LOAD-BEARING.</b>
    /// Static field initialisers run in **declaration order**, so while this sat above the warp it
    /// read <c>WarpTiles</c> as 0 and came out as **1** — half what it should be. *It printed
    /// "reach 1 tiles" in the probe and the self-check passed anyway, which is the more
    /// frightening half.*
    /// </para>
    /// </remarks>
    private static readonly int TilesOfInfluence =
        Mathf.RoundToInt(0.5f + WarpTiles) + KernelScanTiles;

    /// <summary>
    /// ⛔⛔ The pixels, as bytes — <b>because <c>Image.SetPixel</c> is an interop
    /// call and there are two and a half million of them</b> (D342).
    /// </summary>
    /// <remarks>
    /// <b>The first version wrote through <c>Image.SetPixel</c>, the way <c>Minimap.Bake</c> does,
    /// and the full bake measured 811 ms.</b> The minimap gets away with it because its image is
    /// **9,600 pixels**; this one is 2,457,000, and every one of them was a marshalled call
    /// across the C#/C++ boundary. *Writing four bytes into an array and handing Godot the whole
    /// block once is the same picture and a different order of magnitude.*
    /// </remarks>
    private byte[]? _pixels;

    /// <summary>
    /// ⛔⛔ Turns the flat-tile fast path off — <b>for the one guard that can catch
    /// a lying flatness radius</b> (D342).
    /// </summary>
    /// <remarks>
    /// <b>Test-only, and it exists because the obvious guard scored zero.</b> See
    /// <see cref="SelfCheck"/>: comparing an incremental re-bake against a full one **cannot**
    /// find a too-small reach, because both bakes share the same flatness decision and a shared
    /// error cancels. *Only comparing the fast path against the slow one can.*
    /// </remarks>
    private bool _neverFlat;

    private Image? _image;
    private ImageTexture? _texture;
    private Terrain[]? _shadowTerrain;
    private int _bakedAtGeneration = -1;
    private int _width;
    private int _height;
    private int _minX;
    private int _minY;

    /// <summary>The baked valley, or null until the first refresh.</summary>
    internal ImageTexture? Texture => _texture;

    /// <summary>How long the last bake took, and how much of the valley it touched.</summary>
    internal double LastBakeMs { get; private set; }

    /// <summary>Tiles repainted by the last bake — the whole map on the first, a handful after.</summary>
    internal int LastBakedTiles { get; private set; }

    /// <summary>How many pixels the image holds, for the probe to report.</summary>
    internal int PixelCount => _pixels is null ? 0 : _pixels.Length / 4;

    /// <summary>
    /// ⛔⛔ Bring the texture up to date — <b>incrementally, which is a standing rule</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>CLAUDE.md</c>, Joe's own line: *"Nothing derivable incrementally may be rebuilt per tick
    /// or per frame. Keep the index and maintain it where the state changes."* **Felling bumps
    /// <c>TerrainGeneration</c> constantly and saplings mature on their own**, so a full re-bake
    /// per bump would be a hitch several times a year of play.
    /// </para>
    /// <para>
    /// ⭐ <b>The view keeps a shadow copy of the terrain and diffs it</b> rather than asking the
    /// sim to maintain a dirty list. 9,600 comparisons is nothing, it needs **zero new sim API**,
    /// and — the real reason — *it cannot miss a change the way a hand-maintained list can.*
    /// </para>
    /// </remarks>
    internal void Refresh(SimWorld world, bool force = false, int generation = -1)
    {
        System.ArgumentNullException.ThrowIfNull(world);

        int now = generation < 0 ? world.TerrainGeneration : generation;

        if (_image is null)
        {
            FirstBake(world);
            return;
        }

        if (_bakedAtGeneration == now && !force)
        {
            return;
        }

        _bakedAtGeneration = now;

        long started = Stopwatch.GetTimestamp();
        int repainted = 0;

        System.Collections.Generic.IReadOnlyList<Terrain> tiles = world.Map.Tiles;
        Terrain[] shadowTerrain = _shadowTerrain!;

        // ⚠️ TERRAIN ONLY, AND `YoungSaplings` DELIBERATELY NOT. A sapling's AGE changes
        // nothing the bake draws — mature woodland and saplings differ by `Terrain`, which is
        // diffed here, and how far along a sapling is only ever shows in the live canopies, which
        // are redrawn every frame anyway. *Diffing it would be a comparison whose answer nothing
        // reads.*
        for (int i = 0; i < shadowTerrain.Length; i++)
        {
            if (shadowTerrain[i] == tiles[i])
            {
                continue;
            }

            shadowTerrain[i] = tiles[i];

            int x = i % _width;
            int y = i / _width;

            PaintTiles(
                world,
                x - TilesOfInfluence,
                y - TilesOfInfluence,
                x + TilesOfInfluence,
                y + TilesOfInfluence);

            repainted++;
        }

        if (repainted > 0)
        {
            // ⚠️ The whole block goes up, not the patch. Godot has no partial-region
            // texture update on `ImageTexture`, and a 9.8 MB memcpy plus one upload is cheap
            // beside the per-pixel interop this replaced. *If felling ever measures dear, the
            // answer is per-chunk textures — measure before building that.*
            _image!.SetData(
                _image.GetWidth(), _image.GetHeight(), false, Image.Format.Rgba8, _pixels);

            _texture!.Update(_image);
        }

        LastBakedTiles = repainted;
        LastBakeMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    private void FirstBake(SimWorld world)
    {
        SimConfig config = world.Config;

        _width = config.MapWidth;
        _height = config.MapHeight;
        _minX = config.MapMinX;
        _minY = config.MapMinY;

        int wide = _width * PixelsPerTile;
        int tall = _height * PixelsPerTile;
        _pixels = new byte[wide * tall * 4];

        _shadowTerrain = new Terrain[_width * _height];

        for (int i = 0; i < _shadowTerrain.Length; i++)
        {
            _shadowTerrain[i] = world.Map.Tiles[i];
        }

        long started = Stopwatch.GetTimestamp();
        PaintTiles(world, 0, 0, _width - 1, _height - 1);

        _image = Image.CreateFromData(wide, tall, false, Image.Format.Rgba8, _pixels);
        _texture = ImageTexture.CreateFromImage(_image);

        LastBakeMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        LastBakedTiles = _width * _height;
        _bakedAtGeneration = world.TerrainGeneration;
    }

    /// <summary>Repaint every pixel of a rectangle of tiles, clipped to the valley.</summary>
    private void PaintTiles(SimWorld world, int fromX, int fromY, int toX, int toY)
    {
        fromX = Mathf.Max(0, fromX);
        fromY = Mathf.Max(0, fromY);
        toX = Mathf.Min(_width - 1, toX);
        toY = Mathf.Min(_height - 1, toY);

        for (int ty = fromY; ty <= toY; ty++)
        {
            for (int tx = fromX; tx <= toX; tx++)
            {
                PaintOneTile(world, tx, ty);
            }
        }
    }

    /// <summary>
    /// ⭐ One tile's worth of pixels — <b>and the flat case is the whole performance story</b>.
    /// </summary>
    /// <remarks>
    /// <b>A tile whose whole neighbourhood is the same kind of ground cannot have a boundary
    /// running through it</b>, warp included, so its pixels are the flat colour and the blend is
    /// never asked for. **Almost all of the valley is such a tile** — the interiors of the grass
    /// and of the woods — so the expensive per-pixel path only runs along the edges, which is
    /// where the whole point of this is.
    /// </remarks>
    private void PaintOneTile(SimWorld world, int tx, int ty)
    {
        GeneratedMap map = world.Map;
        var tile = new GridPos(_minX + tx, _minY + ty);
        Terrain here = map.TerrainAt(tile);

        // ⚠️ Worked ground is not part of the field and is drawn per-tile over the top, so the
        // bake leaves plain earth under it rather than smearing a field into a farm.
        Terrain painted = IsWorked(here) ? Terrain.Grass : here;
        bool flat = !_neverFlat && NeighbourhoodIsFlat(map, tile, painted);

        int px0 = tx * PixelsPerTile;
        int py0 = ty * PixelsPerTile;

        for (int y = 0; y < PixelsPerTile; y++)
        {
            for (int x = 0; x < PixelsPerTile; x++)
            {
                int px = px0 + x;
                int py = py0 + y;

                Color colour = flat
                    ? Dressed(painted, px, py, 1f)
                    : Blended(map, px, py);

                int at = ((py * _width * PixelsPerTile) + px) * 4;
                byte[] pixels = _pixels!;

                pixels[at] = (byte)Mathf.Clamp(colour.R * 255f, 0f, 255f);
                pixels[at + 1] = (byte)Mathf.Clamp(colour.G * 255f, 0f, 255f);
                pixels[at + 2] = (byte)Mathf.Clamp(colour.B * 255f, 0f, 255f);
                pixels[at + 3] = 255;
            }
        }
    }

    /// <summary>
    /// ⛔⛔ Whether every tile within reach is the same kind — <b>and it has to
    /// agree with <see cref="Blended"/> about what lies OUTSIDE the valley</b> (D342).
    /// </summary>
    /// <remarks>
    /// <b>This read off-map tiles as "the same as here" while <see cref="Blended"/> reads them
    /// as grass</b>, so a wood running to the valley's edge was declared flat and painted without
    /// its boundary. *The guard found it on the first run it existed, and it survived two rounds
    /// of me widening <see cref="TilesOfInfluence"/> at it — which was never the fault.*
    /// ⭐ <b>The fast path and the slow path must agree about every input, not just the ones
    /// on the map.</b>
    /// </remarks>
    private static bool NeighbourhoodIsFlat(GeneratedMap map, GridPos tile, Terrain painted)
    {
        for (int dy = -TilesOfInfluence; dy <= TilesOfInfluence; dy++)
        {
            for (int dx = -TilesOfInfluence; dx <= TilesOfInfluence; dx++)
            {
                var near = new GridPos(tile.X + dx, tile.Y + dy);
                Terrain there = map.Contains(near) ? map.TerrainAt(near) : Terrain.Grass;

                if ((IsWorked(there) ? Terrain.Grass : there) != painted)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// ⭐⭐ The winning kind of ground at one pixel, and how strongly it won.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The sample point is WARPED before anything is weighed</b>, and that is what stops the
    /// result reading as geometry. Without it the contour of a bilinear field over a staircase is
    /// a tidy sequence of arcs — *which is what D337's corner-cutting produced and what Joe
    /// called blocky and chunky.* With it, the same contour wanders the way a riverbank wanders.
    /// </para>
    /// <para>
    /// ⚠️ <b>The warp is bounded by <c>WarpTiles</c> on purpose</b>: the incremental re-bake
    /// repaints a fixed margin around a changed tile, and an unbounded warp would let a change
    /// reach outside that margin and leave a stale seam nothing ever repaints.
    /// </para>
    /// </remarks>
    private Color Blended(GeneratedMap map, int px, int py)
    {
        // Pixel centre, in tile-centre coordinates: tile i's centre sits at exactly i.
        float cx = ((px + 0.5f) / PixelsPerTile) - 0.5f;
        float cy = ((py + 0.5f) / PixelsPerTile) - 0.5f;

        for (int o = 0; o < WarpOctaves.Length; o++)
        {
            (float tiles, float cells) = WarpOctaves[o];

            cx += tiles * Wobble(px, py, 917 + (o * 131), cells);
            cy += tiles * Wobble(px, py, 4231 + (o * 197), cells);
        }

        int nearestX = Mathf.RoundToInt(cx);
        int nearestY = Mathf.RoundToInt(cy);

        // ⚠️ Weights of the SAME kind add, which is what makes a lone tile of water a
        // rounded pond rather than a diamond — its weight has nothing to share with. Nine
        // neighbours can hold at most nine kinds, so a flat pair of arrays beats a dictionary and
        // allocates nothing; this runs for every boundary pixel in the valley.
        Span<Terrain> kinds = stackalloc Terrain[9];
        Span<float> weights = stackalloc float[9];
        int found = 0;
        float total = 0f;

        for (int dy = -KernelScanTiles; dy <= KernelScanTiles; dy++)
        {
            for (int dx = -KernelScanTiles; dx <= KernelScanTiles; dx++)
            {
                int gx = nearestX + dx;
                int gy = nearestY + dy;

                float away = ((gx - cx) * (gx - cx)) + ((gy - cy) * (gy - cy));
                if (away >= KernelTiles * KernelTiles)
                {
                    continue;
                }

                // A smooth falloff rather than a tent: a tent leaves creases where the kernel
                // ends, and a crease in a riverbank reads as the grid showing through again.
                // ⚠️ On the SQUARED distance, so nine square roots a pixel become none.
                // It is a different curve and that is fine — what matters is that it is
                // smooth and reaches zero at the kernel's edge, which it still does.
                float weight = 1f - Mathf.SmoothStep(0f, KernelTiles * KernelTiles, away);

                var at = new GridPos(_minX + gx, _minY + gy);
                Terrain kind = map.Contains(at) ? map.TerrainAt(at) : Terrain.Grass;
                if (IsWorked(kind))
                {
                    kind = Terrain.Grass;
                }

                int slot = -1;
                for (int i = 0; i < found; i++)
                {
                    if (kinds[i] == kind)
                    {
                        slot = i;
                        break;
                    }
                }

                if (slot < 0)
                {
                    slot = found++;
                    kinds[slot] = kind;
                    weights[slot] = 0f;
                }

                weights[slot] += weight;
                total += weight;
            }
        }

        if (found == 0)
        {
            return Dressed(Terrain.Grass, px, py, 1f);
        }

        // ⭐⭐ THE EDGE OF EACH KIND IS JITTERED SEPARATELY, WHICH IS WHAT UN-DIAMONDS
        // THE WOODS. `PaintForest` drops Manhattan diamonds of tiles, and a warp big enough to
        // disguise a nine-tile diamond would also drag the river off its own tiles. **Pushing on
        // the WEIGHT instead of on the POSITION cannot move anything** — a kind still only
        // wins where it is present in the neighbourhood — so a wood's boundary can be
        // shoved about freely while the water's is barely touched.
        // ⭐ ONE noise value, weighted differently per kind — not one noise per kind.
        // Independent noise per kind moves a boundary in a mush; a shared one moves it as a
        // boundary, because whatever pushes the wood back is the same thing letting the grass
        // forward. *It is also N times cheaper, and this runs on every boundary pixel.*
        float jitter = (0.72f * Wobble(px, py, 6151, 0.22f))
            + (0.28f * Wobble(px, py, 2371, 0.6f));

        for (int i = 0; i < found; i++)
        {
            weights[i] *= Mathf.Max(0.05f, 1f + (HowFreeToWander(kinds[i]) * jitter));
        }

        int winner = 0;
        for (int i = 1; i < found; i++)
        {
            if (weights[i] > weights[winner])
            {
                winner = i;
            }
        }

        // How decisively it won: ~0.5 at a contour between two kinds, 1 deep inside one.
        float confidence = total <= 0f ? 1f : Mathf.Clamp(weights[winner] / total, 0f, 1f);

        return Dressed(kinds[winner], px, py, confidence);
    }

    /// <summary>
    /// ⭐ The colour of a kind of ground at one pixel — <b>flat colour plus what makes it look
    /// like ground</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐⭐ <b>THIS IS WHERE THE TREES WENT.</b> A wood was ~5,700 <c>DrawCircle</c> calls a frame
    /// and **every one of them broke Godot's 2D batching**, because a circle becomes a
    /// `CommandPolygon` where a rect batches. Here foliage is two octaves of noise on the green,
    /// costing nothing per frame at all — and *the ragged treeline D337 bought with overhanging
    /// canopies comes free*, because the forest field is warped before it is weighed.
    /// </para>
    /// <para>
    /// <b>Water gets depth from how decisively it won</b>, which is the shoreline: shallow at the
    /// bank, deeper mid-channel. **That is the same contour as the fill by construction**, so the
    /// bank can never again be drawn half a tile off the water it belongs to (D338).
    /// </para>
    /// </remarks>
    private static Color Dressed(Terrain kind, int px, int py, float confidence)
    {
        Color flat = VillageMap.ColourOf(kind);

        switch (kind)
        {
            case Terrain.Water:
                // Shallower at the edge. `confidence` is ~0.5 at the contour and 1 mid-river.
                float depth = Mathf.SmoothStep(0.45f, 0.95f, confidence);
                return VillageMap.ShallowsColour.Lerp(flat, depth);

            case Terrain.Forest:
            case Terrain.Sapling:
                bool young = kind == Terrain.Sapling;

                // Two octaves: the first is the crown of a tree, the second the gaps between
                // leaves. A single octave reads as a stain rather than as foliage.
                float canopy = (0.65f * Wobble(px, py, 5501, 3.2f))
                    + (0.35f * Wobble(px, py, 8663, 8.5f));

                float lift = young ? 0.06f : 0.13f;
                float shade = 1f + (lift * canopy);

                return flat with
                {
                    R = Mathf.Clamp(flat.R * shade, 0f, 1f),
                    G = Mathf.Clamp(flat.G * shade, 0f, 1f),
                    B = Mathf.Clamp(flat.B * shade, 0f, 1f),
                };

            case Terrain.Rock:
            case Terrain.IronDeposit:
                float grain = 1f + (0.09f * Wobble(px, py, 2207, 6f));
                return flat with
                {
                    R = Mathf.Clamp(flat.R * grain, 0f, 1f),
                    G = Mathf.Clamp(flat.G * grain, 0f, 1f),
                    B = Mathf.Clamp(flat.B * grain, 0f, 1f),
                };

            default:
                // A whisper of variation on the open ground, so a hundred tiles of grass is not
                // a hundred tiles of exactly one colour.
                float bloom = 1f + (0.035f * Wobble(px, py, 3931, 2.4f));
                return flat with
                {
                    R = Mathf.Clamp(flat.R * bloom, 0f, 1f),
                    G = Mathf.Clamp(flat.G * bloom, 0f, 1f),
                    B = Mathf.Clamp(flat.B * bloom, 0f, 1f),
                };
        }
    }

    /// <summary>
    /// ⛔⛔ The three things about a bake that CAN be checked headless — <b>and one
    /// of them is worth more than the rest together</b> (D342).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>VillageMap._Draw</c> never runs under <c>--headless</c></b> (measured, D340), so
    /// **what the valley looks like is Joe's eyes and nothing else.** These are the claims that
    /// survive that:
    /// </para>
    /// <para>
    /// ⭐⭐ <b>THE INCREMENTAL RE-BAKE MUST EQUAL A FULL ONE.</b> *A stale patch of valley
    /// is the exact bug this design can have* — <c>TilesOfInfluence</c> is a hand-derived
    /// margin, and if it is one tile short then every felled tree leaves a seam that nothing ever
    /// repaints and nobody notices for a month. So: change some ground, refresh incrementally,
    /// bake the same world from scratch, and require the two images to be **byte-identical**.
    /// </para>
    /// <para>
    /// ⭐ <b>Determinism</b> — two bakes of the same terrain agree. Weak on its own
    /// (<c>Wobble</c> is a <c>static</c> of the pixel, so the compiler already refuses the bug the
    /// way it does for <c>CanopyOn</c>), but free, and it is what makes the comparison above mean
    /// anything.
    /// </para>
    /// <para>
    /// ⭐ <b>The wall-clock and the pixel count, printed</b>, so <see cref="PixelsPerTile"/>
    /// is a measurement rather than an opinion and a later session cannot quietly make it dear.
    /// </para>
    /// </remarks>
    internal static string SelfCheck(SimWorld world)
    {
        System.ArgumentNullException.ThrowIfNull(world);

        var first = new ValleyTexture();
        first.Refresh(world);

        var second = new ValleyTexture();
        second.Refresh(world);

        if (!SamePixels(first._pixels!, second._pixels!))
        {
            return "[widths] valley bake: ⛔ two bakes of one valley disagree — something "
                + "in the field is reading state that moves";
        }

        // ⭐⭐ THE ARM THAT ACTUALLY GUARDS THE REACH. Every tile whose neighbourhood
        // looks uniform is painted a flat colour without consulting the field at all — and
        // that is only sound if the reach is genuinely as far as a warped sample can travel.
        // **Paint the whole valley the slow way and require it to be byte-identical.**
        var slowly = new ValleyTexture { _neverFlat = true };
        slowly.Refresh(world);

        if (!SamePixels(first._pixels!, slowly._pixels!))
        {
            return "[widths] valley bake: ⛔ the flat-tile fast path disagrees with the "
                + $"field — a reach of {TilesOfInfluence} tiles is shorter than a warped "
                + "sample travels, so tiles are being painted flat that have a boundary in them";
        }

        // ⚠️ CHANGED IN PLACES THE BAKE ACTUALLY CARES ABOUT, AND SCATTERED. Clearing
        // grass to grass would be a change nothing can see, and the guard would pass having
        // repainted nothing. **Six tiles all in one clump would only ever test one clump's
        // worth of margin**, so this strides the valley: woodland, and woodland that touches
        // something else, which is where a margin that is one tile short shows first.
        var moved = new System.Collections.Generic.List<GridPos>();

        for (int y = 0; y < world.Map.Height && moved.Count < 12; y += 7)
        {
            for (int x = 0; x < world.Map.Width && moved.Count < 12; x += 11)
            {
                var at = new GridPos(world.Map.MinX + x, world.Map.MinY + y);
                if (world.Map.TerrainAt(at) is Terrain.Forest or Terrain.Sapling)
                {
                    moved.Add(at);
                }
            }
        }

        if (moved.Count < 2)
        {
            return "[widths] valley bake: ⛔ fewer than two woodland tiles found to fell "
                + "— the guard could not pose a terrain change and has checked nothing";
        }

        for (int i = 0; i < moved.Count; i++)
        {
            world.Map.SetTerrain(moved[i], Terrain.Grass);
        }

        // The counter the view watches, bumped by hand: this is a guard, not a village.
        first.Refresh(world, force: false, generation: world.TerrainGeneration + 1);
        double patchMs = first.LastBakeMs;

        var fromScratch = new ValleyTexture();
        fromScratch.Refresh(world);

        for (int i = 0; i < moved.Count; i++)
        {
            world.Map.SetTerrain(moved[i], Terrain.Forest);
        }

        if (!SamePixels(first._pixels!, fromScratch._pixels!))
        {
            return $"[widths] valley bake: ⛔ after felling {moved.Count} scattered tiles the "
                + "incremental re-bake differs from a full one — TilesOfInfluence is too "
                + "small and the valley has stale seams in it";
        }

        return $"[widths] valley bake: ✅ {first.PixelCount / 1000}k pixels at "
            + $"{PixelsPerTile}/tile, reach {TilesOfInfluence} tiles · whole valley "
            + $"{fromScratch.LastBakeMs:F0}ms once ({slowly.LastBakeMs:F0}ms with the fast path "
            + $"off, and identical), {moved.Count} felled tiles patched in {patchMs:F1}ms";
    }

    private static bool SamePixels(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Save the bake to a PNG so a human — or a session — can look at it.</summary>
    /// <remarks>
    /// ⭐ <b>The probe cannot see drawing, but it CAN write a file.</b> This is the only way
    /// anything but Joe has ever laid eyes on the valley, and it is how <see cref="WarpTiles"/>
    /// and <see cref="KernelTiles"/> were tuned rather than guessed.
    /// </remarks>
    internal void SaveTo(string path) => _image?.SavePng(path);

    /// <summary>
    /// ⭐ How freely a kind's boundary may wander — <b>and water's may not</b>.
    /// </summary>
    /// <remarks>
    /// <b>A riverbank is where the water stops; a treeline is a suggestion.</b> Water is the one
    /// terrain a villager cannot walk on, so its drawn edge must stay honest about which tiles it
    /// is — people appearing to cross a river is §1.1 failing, and it is the reason the
    /// warp above is kept modest too. **A wood's edge promises nothing**, so it takes the strong
    /// push, and *that is what turns `PaintForest`'s Manhattan diamonds into woods without
    /// touching the generator.* An ore body has no business being a lozenge either.
    /// </remarks>
    private static float HowFreeToWander(Terrain kind) => kind switch
    {
        Terrain.Water => 0.08f,
        Terrain.Forest or Terrain.Sapling => 0.75f,
        Terrain.Rock or Terrain.IronDeposit => 0.55f,
        _ => 0.30f,
    };

    /// <summary>Ground the village has worked — square by intent, so it is not smoothed.</summary>
    private static bool IsWorked(Terrain kind) =>
        kind is Terrain.Field or Terrain.Sown or Terrain.Ripe;

    /// <summary>
    /// Value noise in [−1, 1] — <b>deterministic from the pixel, and from nothing else</b>.
    /// </summary>
    /// <remarks>
    /// ⭐ The rule <c>CanopyOn</c> was made <c>static</c> to enforce (D337): a bake that could
    /// reach the tick would shimmer, and the compiler is a better guard than a test looking for
    /// it. <paramref name="perTile"/> is how many cells of noise fit across a tile.
    /// </remarks>
    private static float Wobble(int px, int py, int salt, float perTile = 1.6f)
    {
        float scale = perTile / PixelsPerTile;
        float fx = px * scale;
        float fy = py * scale;

        int x0 = Mathf.FloorToInt(fx);
        int y0 = Mathf.FloorToInt(fy);
        float tx = Mathf.SmoothStep(0f, 1f, fx - x0);
        float ty = Mathf.SmoothStep(0f, 1f, fy - y0);

        float top = Mathf.Lerp(At(x0, y0), At(x0 + 1, y0), tx);
        float bottom = Mathf.Lerp(At(x0, y0 + 1), At(x0 + 1, y0 + 1), tx);

        return Mathf.Lerp(top, bottom, ty);

        float At(int x, int y)
        {
            unchecked
            {
                uint h = (uint)((x * 73856093) ^ (y * 19349663) ^ (salt * 83492791));
                h ^= h >> 13;
                h *= 0x85EBCA6Bu;
                h ^= h >> 16;
                return ((h % 2000) / 1000f) - 1f;
            }
        }
    }
}
