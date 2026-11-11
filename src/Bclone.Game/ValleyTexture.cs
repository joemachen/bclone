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
    internal const float KernelTiles = 1f;

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

    /// <summary>
    /// How many image pixels a tile gets in <em>this</em> bake — <see cref="PixelsPerTile"/> for the
    /// game, fewer for the new-game screen's preview (D479), which shows the whole valley at a few
    /// pixels a tile and bakes again on every change.
    /// </summary>
    /// <remarks>
    /// ⭐ <b>A coarser bake is the same field sampled at fewer points</b>, not a second painter: each
    /// pixel asks the noise and the blend at the 16-a-tile pixel under its centre, so the preview
    /// shows the valley the game will draw. At <see cref="PixelsPerTile"/> that pixel is itself, and
    /// the game's bake is byte for byte what it was.
    /// </remarks>
    private readonly int _perTile;

    /// <summary>
    /// ⭐ How many texels a tile gets in the ground's MIX (<c>terrain-shader.md §5.2</c>, call 1, A22–A23) — the lab's fake
    /// map's own resolution, which is what Joe approved the look on.
    /// </summary>
    /// <remarks>
    /// A quarter of the colour's pixels, because the shader reads the mix bilinearly and sharpens it: a smooth field does
    /// not need the colour's sixteen. ⚠️ <b>It must divide <see cref="PixelsPerTile"/></b>, so a texel's centre is a
    /// sample point the colour's arithmetic can name exactly (<see cref="TexelCentre"/>).
    /// </remarks>
    internal const int WeightsPerTile = 8;

    /// <summary>
    /// ⭐⭐ The weights <see cref="Winner"/> throws away, kept — <b>only for a bake that asks</b> (A22–A23, Joe:
    /// <i>"keep them as lean as possible"</i>). The new-game preview and the title draw the colour and never pay for this.
    /// </summary>
    private readonly bool _withWeights;

    /// <summary>
    /// The ground's mix, RGBA8, in the lab's fake map's layout (<c>terrain-shader.md §4</c>): <b>R grass, G wood, B seam,
    /// A water</b>, each a kind's share of the jittered weights, so the four sum to one and the top one is
    /// <see cref="Winner"/>'s (worked ground counts as grass, as everywhere in the bake).
    /// </summary>
    private byte[]? _weights;

    /// <summary>
    /// RG8 beside the mix: <b>R the share of the wood that is young, G the share of the seam that is iron</b> — fractions, not
    /// the lab's pick, so forest meets young wood and stone meets iron the way every land edge meets (§5.2, HANDOFF art
    /// trap 18). 0 where the mix has no wood, or no seam.
    /// </summary>
    private byte[]? _shares;

    private Image? _weightsImage;
    private ImageTexture? _weightsTexture;
    private Image? _sharesImage;
    private ImageTexture? _sharesTexture;

    internal ValleyTexture(int pixelsPerTile = PixelsPerTile, bool withWeights = false)
    {
        _perTile = Mathf.Clamp(pixelsPerTile, 1, PixelsPerTile);
        _withWeights = withWeights;
    }

    /// <summary>The ground's mix (<see cref="_weights"/>), or null for a bake that did not ask for it.</summary>
    internal ImageTexture? WeightsTexture => _weightsTexture;

    /// <summary>The young and iron shares (<see cref="_shares"/>), or null for a bake that did not ask for it.</summary>
    internal ImageTexture? SharesTexture => _sharesTexture;

    /// <summary>How long the last bake spent handing the mix to the GPU — the cost a felling adds (§5.2, call 1).</summary>
    internal double LastWeightsUploadMs { get; private set; }

    /// <summary>Bake <paramref name="world"/> whole, as if for the first time — the preview's refresh.</summary>
    /// <remarks>
    /// The incremental <see cref="Refresh"/> repaints a margin round every changed tile, which for a
    /// whole new valley is more work than painting it once.
    /// </remarks>
    internal void BakeAfresh(SimWorld world)
    {
        System.ArgumentNullException.ThrowIfNull(world);
        _image = null;
        FirstBake(world);
    }

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
            UploadTheWeights(fresh: false);
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

        int wide = _width * _perTile;
        int tall = _height * _perTile;
        _pixels = new byte[wide * tall * 4];

        if (_withWeights)
        {
            _weights = new byte[_width * WeightsPerTile * _height * WeightsPerTile * 4];
            _shares = new byte[_width * WeightsPerTile * _height * WeightsPerTile * 2];
        }

        _shadowTerrain = new Terrain[_width * _height];

        for (int i = 0; i < _shadowTerrain.Length; i++)
        {
            _shadowTerrain[i] = world.Map.Tiles[i];
        }

        long started = Stopwatch.GetTimestamp();
        PaintTiles(world, 0, 0, _width - 1, _height - 1);

        _image = Image.CreateFromData(wide, tall, false, Image.Format.Rgba8, _pixels);
        _texture = ImageTexture.CreateFromImage(_image);
        UploadTheWeights(fresh: true);

        LastBakeMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        LastBakedTiles = _width * _height;
        _bakedAtGeneration = world.TerrainGeneration;
    }

    /// <summary>
    /// Hand the mix and the shares to the GPU — whole, as the colour goes (an <c>ImageTexture</c> has no partial update),
    /// and timed, because it is what every felling adds once the valley draws through the shader (§5.2, call 1).
    /// </summary>
    private void UploadTheWeights(bool fresh)
    {
        if (_weights is null || _shares is null)
        {
            return;
        }

        long started = Stopwatch.GetTimestamp();
        int wide = _width * WeightsPerTile;
        int tall = _height * WeightsPerTile;

        if (fresh)
        {
            _weightsImage = Image.CreateFromData(wide, tall, false, Image.Format.Rgba8, _weights);
            _weightsTexture = ImageTexture.CreateFromImage(_weightsImage);
            _sharesImage = Image.CreateFromData(wide, tall, false, Image.Format.Rg8, _shares);
            _sharesTexture = ImageTexture.CreateFromImage(_sharesImage);
        }
        else
        {
            _weightsImage!.SetData(wide, tall, false, Image.Format.Rgba8, _weights);
            _weightsTexture!.Update(_weightsImage);
            _sharesImage!.SetData(wide, tall, false, Image.Format.Rg8, _shares);
            _sharesTexture!.Update(_sharesImage);
        }

        LastWeightsUploadMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
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

        for (int y = 0; y < _perTile; y++)
        {
            for (int x = 0; x < _perTile; x++)
            {
                // The full-resolution pixel under this one's centre — itself at `PixelsPerTile`.
                int px = (tx * PixelsPerTile) + (((2 * x) + 1) * PixelsPerTile / (2 * _perTile));
                int py = (ty * PixelsPerTile) + (((2 * y) + 1) * PixelsPerTile / (2 * _perTile));

                Color colour = flat
                    ? Dressed(painted, px, py, 1f)
                    : Blended(map, px, py);

                int at = ((((ty * _perTile) + y) * _width * _perTile) + (tx * _perTile) + x) * 4;
                byte[] pixels = _pixels!;

                pixels[at] = (byte)Mathf.Clamp(colour.R * 255f, 0f, 255f);
                pixels[at + 1] = (byte)Mathf.Clamp(colour.G * 255f, 0f, 255f);
                pixels[at + 2] = (byte)Mathf.Clamp(colour.B * 255f, 0f, 255f);
                pixels[at + 3] = 255;
            }
        }

        if (_weights is not null)
        {
            PaintTheMix(map, tx, ty, painted, flat);
        }
    }

    /// <summary>
    /// ⭐ One tile's texels of the mix and the shares — <b>from the same weights <see cref="Winner"/> chooses by</b>, so the
    /// mix's top kind is the colour's kind and the two cannot disagree about where anything is (D338).
    /// </summary>
    /// <remarks>
    /// A flat tile is its own kind whole, as the colour's fast path paints it; the reach that makes that sound for the
    /// colour makes it sound here, and the self-check's slow bake compares the two for the mix too.
    /// </remarks>
    private void PaintTheMix(GeneratedMap map, int tx, int ty, Terrain painted, bool flat)
    {
        Span<Terrain> kinds = stackalloc Terrain[9];
        Span<float> weights = stackalloc float[9];
        Span<float> mix = stackalloc float[6];
        int stride = _width * WeightsPerTile;

        for (int y = 0; y < WeightsPerTile; y++)
        {
            for (int x = 0; x < WeightsPerTile; x++)
            {
                int wx = (tx * WeightsPerTile) + x;
                int wy = (ty * WeightsPerTile) + y;

                if (flat)
                {
                    kinds[0] = painted;
                    weights[0] = 1f;
                    MixOf(kinds, weights, 1, mix);
                }
                else
                {
                    int found = Weigh(map, TexelCentre(wx), TexelCentre(wy), kinds, weights, out _);
                    MixOf(kinds, weights, found, mix);
                }

                int at = ((wy * stride) + wx) * 4;
                _weights![at] = Byte(mix[0]);
                _weights[at + 1] = Byte(mix[1]);
                _weights[at + 2] = Byte(mix[2]);
                _weights[at + 3] = Byte(mix[3]);

                int share = ((wy * stride) + wx) * 2;
                _shares![share] = Byte(mix[4]);
                _shares[share + 1] = Byte(mix[5]);
            }
        }
    }

    /// <summary>
    /// ⛔ Where a texel of the mix samples, in the colour's pixel coordinates — <b>its centre, exactly</b>.
    /// </summary>
    /// <remarks>
    /// The colour's pixel <c>px</c> is sampled at <c>px + 0.5</c> (<see cref="Weigh"/>'s first line). A texel of the mix
    /// spans <c>PixelsPerTile / WeightsPerTile</c> of them, so its centre is half that past its start, and the argument
    /// that lands there is half a colour pixel less. At 16 and 8: <c>2·wx + 0.5</c>. ⚠️ Sampling at the colour pixel
    /// under the centre instead (as the coarse preview does) puts every texel 1/32 of a tile off, and the shader's
    /// bilinear read would then draw every edge that far from the colour's.
    /// </remarks>
    private static float TexelCentre(int texel)
    {
        const float span = PixelsPerTile / (float)WeightsPerTile;
        return (texel * span) + (span / 2f) - 0.5f;
    }

    /// <summary>
    /// The six numbers of the mix from a pixel's weighed kinds: grass, wood, seam, water (shares of the whole), then the
    /// young share of the wood and the iron share of the seam.
    /// </summary>
    private static void MixOf(ReadOnlySpan<Terrain> kinds, ReadOnlySpan<float> weights, int found, Span<float> mix)
    {
        float grass = 0f, forest = 0f, sapling = 0f, rock = 0f, iron = 0f, water = 0f;

        for (int i = 0; i < found; i++)
        {
            switch (kinds[i])
            {
                case Terrain.Forest: forest += weights[i]; break;
                case Terrain.Sapling: sapling += weights[i]; break;
                case Terrain.Rock: rock += weights[i]; break;
                case Terrain.IronDeposit: iron += weights[i]; break;
                case Terrain.Water: water += weights[i]; break;
                default: grass += weights[i]; break;
            }
        }

        float wood = forest + sapling;
        float seam = rock + iron;
        float all = grass + wood + seam + water;

        if (all <= 0f)
        {
            // Nothing weighed — the bake's own answer for that case is grass (`Winner`).
            grass = 1f;
            all = 1f;
        }

        mix[0] = grass / all;
        mix[1] = wood / all;
        mix[2] = seam / all;
        mix[3] = water / all;
        mix[4] = wood > 0f ? sapling / wood : 0f;
        mix[5] = seam > 0f ? iron / seam : 0f;
    }

    /// <summary>A share in [0, 1] as a byte, rounded to the nearest — the mix is data, read back by a sharpening.</summary>
    private static byte Byte(float share) => (byte)((Mathf.Clamp(share, 0f, 1f) * 255f) + 0.5f);

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
        Terrain kind = Winner(map, px, py, out float confidence);
        return Dressed(kind, px, py, confidence);
    }

    /// <summary>The kind <see cref="Blended"/> paints at one pixel, and how decisively it won — the bake's own answer, which
    /// the seam check (<see cref="SeamsHugTheirTiles"/>) asks too, so it measures what is drawn and not a copy of it.</summary>
    private Terrain Winner(GeneratedMap map, int px, int py, out float confidence)
    {
        Span<Terrain> kinds = stackalloc Terrain[9];
        Span<float> weights = stackalloc float[9];
        int found = Weigh(map, px, py, kinds, weights, out float total);

        if (found == 0)
        {
            confidence = 1f;
            return Terrain.Grass;
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
        confidence = total <= 0f ? 1f : Mathf.Clamp(weights[winner] / total, 0f, 1f);

        return kinds[winner];
    }

    /// <summary>
    /// ⭐ Every kind's weight at one sample point, warped and jittered — <b>what <see cref="Winner"/> chooses from, and what
    /// the mix keeps</b> (A23). Returns how many kinds were found; <paramref name="total"/> is their weight before the
    /// jitter, which <see cref="Winner"/>'s confidence divides by.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Pixel coordinates as a float, so a texel of the mix can sample its exact centre</b> (<see cref="TexelCentre"/>).
    /// An int pixel converts exactly and every sum below is the one the int version did, so the colour is byte for byte
    /// what it was — which the probe's bake comparison, not this comment, is the proof of.
    /// </remarks>
    private int Weigh(GeneratedMap map, float px, float py, Span<Terrain> kinds, Span<float> weights, out float total)
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
        int found = 0;
        total = 0f;

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
            return 0;
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

        return found;
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

        // ⭐ Every bake below keeps the mix too (A23), so each comparison the colour has always had is made for the mix
        // in the same breath; `plain` is the game's bake as it is today, with no mix, to prove the mix touches no colour.
        // ⚠️ `plain` third, not first: its time is the line's "whole valley", and the first bake of a run pays the warm-up —
        // timed first it read 1,374–1,430 ms against the 1,202 the old line's fourth bake read (A23).
        var first = new ValleyTexture(withWeights: true);
        first.Refresh(world);

        var second = new ValleyTexture(withWeights: true);
        second.Refresh(world);

        var plain = new ValleyTexture();
        plain.Refresh(world);

        string mix = second.TheMixCheck(world.Map, plain);
        if (!SameMix(first, second))
        {
            mix = "[widths] valley mix: ⛔ two bakes of one valley disagree about the mix — something in the "
                + "weighing is reading state that moves";
        }

        string colour = TheColourCheck(world, first, second, plain.LastBakeMs, ref mix);
        return $"{colour}\n{mix}";
    }

    /// <summary>
    /// The colour's own guards, as they always were (D342), on bakes that keep the mix — and each one asked of the mix too,
    /// which replaces <paramref name="mix"/>'s line with what failed.
    /// </summary>
    private static string TheColourCheck(SimWorld world, ValleyTexture first, ValleyTexture second, double plainMs, ref string mix)
    {
        if (!SamePixels(first._pixels!, second._pixels!))
        {
            return "[widths] valley bake: ⛔ two bakes of one valley disagree — something "
                + "in the field is reading state that moves";
        }

        // ⭐⭐ THE ARM THAT ACTUALLY GUARDS THE REACH. Every tile whose neighbourhood
        // looks uniform is painted a flat colour without consulting the field at all — and
        // that is only sound if the reach is genuinely as far as a warped sample can travel.
        // **Paint the whole valley the slow way and require it to be byte-identical.**
        var slowly = new ValleyTexture(withWeights: true) { _neverFlat = true };
        slowly.Refresh(world);

        if (!SamePixels(first._pixels!, slowly._pixels!))
        {
            return "[widths] valley bake: ⛔ the flat-tile fast path disagrees with the "
                + $"field — a reach of {TilesOfInfluence} tiles is shorter than a warped "
                + "sample travels, so tiles are being painted flat that have a boundary in them";
        }

        // ⚠️ A failure already said is kept: a later arm overwrote the determinism arm's red in the red-check (A24's M1).
        if (mix.Contains('✅', System.StringComparison.Ordinal) && !SameMix(first, slowly))
        {
            mix = "[widths] valley mix: ⛔ the flat-tile fast path disagrees with the weighing — a flat tile's mix is "
                + "not the one-kind mix the slow path weighs there";
        }

        // ⚠️ Each tile's own kind, to put back (A23): this restored every felled tile as forest, so a sapling the guard
        // felled came back a grown wood, and the probe went on with a valley the guard had changed.
        var was = new System.Collections.Generic.List<Terrain>();

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
            was.Add(world.Map.TerrainAt(moved[i]));
            world.Map.SetTerrain(moved[i], Terrain.Grass);
        }

        // The counter the view watches, bumped by hand: this is a guard, not a village.
        first.Refresh(world, force: false, generation: world.TerrainGeneration + 1);
        double patchMs = first.LastBakeMs;
        double uploadMs = first.LastWeightsUploadMs;

        var fromScratch = new ValleyTexture(withWeights: true);
        fromScratch.Refresh(world);

        for (int i = 0; i < moved.Count; i++)
        {
            world.Map.SetTerrain(moved[i], was[i]);
        }

        if (!SamePixels(first._pixels!, fromScratch._pixels!))
        {
            return $"[widths] valley bake: ⛔ after felling {moved.Count} scattered tiles the "
                + "incremental re-bake differs from a full one — TilesOfInfluence is too "
                + "small and the valley has stale seams in it";
        }

        // A failure already said stands; only a mix still green is asked this one.
        if (mix.Contains('✅', System.StringComparison.Ordinal))
        {
            mix = SameMix(first, fromScratch)
                ? $"{mix} · {moved.Count} felled tiles patched with it, the mix's upload {uploadMs:F1}ms"
                : $"[widths] valley mix: ⛔ after felling {moved.Count} scattered tiles the incremental mix differs from "
                    + "a full one — the mix is not repainted with the colour, or not as far";
        }

        // ⚠️ The game's bake keeps no mix until the valley draws through the shader (§5.4 piece 2), so its whole-valley
        // time is still the colour's alone — `plain`'s; the slow and patched bakes here keep the mix and say so.
        return $"[widths] valley bake: ✅ {first.PixelCount / 1000}k pixels at "
            + $"{PixelsPerTile}/tile, reach {TilesOfInfluence} tiles · whole valley "
            + $"{plainMs:F0}ms once ({slowly.LastBakeMs:F0}ms with the fast path "
            + $"off and the mix, and identical), {moved.Count} felled tiles patched in {patchMs:F1}ms with the mix";
    }

    /// <summary>Whether two bakes hold the same mix and the same shares, byte for byte.</summary>
    private static bool SameMix(ValleyTexture left, ValleyTexture right) =>
        SamePixels(left._weights!, right._weights!) && SamePixels(left._shares!, right._shares!);

    /// <summary>
    /// ⭐⭐ The mix's own line — <b>and the arm that matters: is the kind the shader will draw from the mix the kind the
    /// colour draws?</b> (<c>terrain-shader.md §5.4</c> piece 1, A23.)
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every colour pixel is asked twice: the bake's own kind (<see cref="Winner"/>, or the tile's own where the fast path
    /// paints it flat), and the kind the mix gives at that pixel's centre <b>read the way the shader will read it</b> —
    /// bilinear between the four nearest texels, clamped at the edge, the top of grass, wood, seam and water, then the
    /// shares at a half for young wood and iron. That is the mix at the shader's hard power (48), which is the colour's
    /// contour (§3.2).
    /// </para>
    /// <para>
    /// ⛔ <b>The water is held hardest</b> (D342: a river drawn off its tiles puts villagers on water). For every pixel where
    /// the two disagree about water, the distance to the nearest pixel where the bake agrees with what the mix draws there —
    /// how far the drawn bank strays from the bake's. ⚠️ <b>The floors were set from the founding valley's reading</b>, not
    /// guessed; the line prints the reading every run.
    /// </para>
    /// <para>
    /// And <b>the mix must not touch the colour</b>: this bake's colour against <paramref name="plain"/>'s, a bake with no
    /// mix at all, byte for byte.
    /// </para>
    /// </remarks>
    private string TheMixCheck(GeneratedMap map, ValleyTexture plain)
    {
        if (!SamePixels(_pixels!, plain._pixels!))
        {
            return "[widths] valley mix: ⛔ a bake that keeps the mix paints different colour from one that does not — "
                + "the weighing has changed what the valley looks like";
        }

        int wide = _width * PixelsPerTile;
        int tall = _height * PixelsPerTile;
        var baked = new byte[wide * tall];
        var drawn = new byte[wide * tall];
        int boundary = 0;
        int disagree = 0;

        for (int ty = 0; ty < _height; ty++)
        {
            for (int tx = 0; tx < _width; tx++)
            {
                var tile = new GridPos(_minX + tx, _minY + ty);
                Terrain here = map.TerrainAt(tile);
                Terrain painted = IsWorked(here) ? Terrain.Grass : here;
                bool flat = NeighbourhoodIsFlat(map, tile, painted);

                for (int y = 0; y < PixelsPerTile; y++)
                {
                    for (int x = 0; x < PixelsPerTile; x++)
                    {
                        int px = (tx * PixelsPerTile) + x;
                        int py = (ty * PixelsPerTile) + y;
                        int at = (py * wide) + px;

                        Terrain bake = flat ? painted : Winner(map, px, py, out _);
                        Terrain read = KindTheMixDraws(px, py);
                        baked[at] = (byte)bake;
                        drawn[at] = (byte)read;

                        boundary += flat ? 0 : 1;
                        disagree += bake == read ? 0 : 1;
                    }
                }
            }
        }

        (int waterOff, int farthest) = HowFarTheWaterStrays(baked, drawn, wide, tall);
        double agree = boundary == 0 ? 1d : 1d - (disagree / (double)boundary);
        float farthestTiles = farthest / (float)PixelsPerTile;
        int bytes = (_weights!.Length + _shares!.Length) / 1000;

        string said = $"{_width * WeightsPerTile}×{_height * WeightsPerTile} RGBA8 + RG8 at {WeightsPerTile}/tile, {bytes}k bytes · "
            + $"read as the shader reads it, its kind is the colour's at {agree:P2} of {boundary:N0} boundary pixels "
            + $"({disagree:N0} differ); water drawn where the bake has none, or none where it has, at {waterOff:N0} pixels, "
            + $"at most {farthestTiles:F3} tiles from the bake's bank · whole valley {plain.LastBakeMs:F0}ms for the colour, "
            + $"{LastBakeMs:F0}ms with the mix";

        SaveTheMix();

        return agree >= MixAgreesFloor && farthest <= MixWaterStraysAtMostPixels
            ? $"[widths] valley mix: ✅ {said}"
            : $"[widths] valley mix: ⛔ under {MixAgreesFloor:P1} or water past {MixWaterStraysAtMostPixels}px — {said}";
    }

    /// <summary>
    /// The least share of boundary pixels where the mix's kind is the colour's (A23: the founding valley reads 99.85 %,
    /// 2,361 of 1,627,392 differing — forest against young wood and stone against iron where three kinds meet, and the
    /// bilinear read's rounding at every edge).
    /// </summary>
    private const double MixAgreesFloor = 0.995;

    /// <summary>
    /// How far, in colour pixels, water drawn from the mix may lie from the bake's bank (A23: the founding valley reads
    /// 1 — 444 pixels, none past a sixteenth of a tile; the floor is the spec's eighth, D342).
    /// </summary>
    private const int MixWaterStraysAtMostPixels = 2;

    /// <summary>
    /// The kind the shader will draw at a colour pixel from the mix, at the hard power: bilinear, clamped, the top of the
    /// four, then a share at a half.
    /// </summary>
    private Terrain KindTheMixDraws(int px, int py)
    {
        Span<float> mix = stackalloc float[6];
        const float texelsPerPixel = WeightsPerTile / (float)PixelsPerTile;
        float u = ((px + 0.5f) * texelsPerPixel) - 0.5f;
        float v = ((py + 0.5f) * texelsPerPixel) - 0.5f;
        int u0 = Mathf.FloorToInt(u);
        int v0 = Mathf.FloorToInt(v);
        float fu = u - u0;
        float fv = v - v0;
        int wide = _width * WeightsPerTile;
        int tall = _height * WeightsPerTile;

        for (int c = 0; c < 6; c++)
        {
            float top = Mathf.Lerp(Texel(u0, v0, c), Texel(u0 + 1, v0, c), fu);
            float bottom = Mathf.Lerp(Texel(u0, v0 + 1, c), Texel(u0 + 1, v0 + 1, c), fu);
            mix[c] = Mathf.Lerp(top, bottom, fv);
        }

        int best = 0;
        for (int c = 1; c < 4; c++)
        {
            if (mix[c] > mix[best])
            {
                best = c;
            }
        }

        return best switch
        {
            1 => mix[4] >= 0.5f ? Terrain.Sapling : Terrain.Forest,
            2 => mix[5] >= 0.5f ? Terrain.IronDeposit : Terrain.Rock,
            3 => Terrain.Water,
            _ => Terrain.Grass,
        };

        float Texel(int x, int y, int channel)
        {
            x = Mathf.Clamp(x, 0, wide - 1);
            y = Mathf.Clamp(y, 0, tall - 1);
            int at = (y * wide) + x;
            return channel < 4 ? _weights![(at * 4) + channel] / 255f : _shares![(at * 2) + channel - 4] / 255f;
        }
    }

    /// <summary>
    /// ⛔ Where the mix and the bake disagree about water: how many pixels, and the farthest any of them lies from a pixel
    /// where the bake agrees with what the mix draws there — searched out to a tile, and a tile reported past it.
    /// </summary>
    private static (int Pixels, int Farthest) HowFarTheWaterStrays(byte[] baked, byte[] drawn, int wide, int tall)
    {
        const byte water = (byte)Terrain.Water;
        int pixels = 0;
        int farthest = 0;

        for (int py = 0; py < tall; py++)
        {
            for (int px = 0; px < wide; px++)
            {
                int at = (py * wide) + px;
                bool drawnWet = drawn[at] == water;
                if ((baked[at] == water) == drawnWet)
                {
                    continue;
                }

                pixels++;
                int reach = PixelsPerTile + 1;
                for (int r = 1; r <= PixelsPerTile && reach > PixelsPerTile; r++)
                {
                    for (int dy = -r; dy <= r && reach > PixelsPerTile; dy++)
                    {
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int x = px + dx;
                            int y = py + dy;
                            if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != r
                                || x < 0 || y < 0 || x >= wide || y >= tall)
                            {
                                continue;
                            }

                            if ((baked[(y * wide) + x] == water) == drawnWet)
                            {
                                reach = r;
                                break;
                            }
                        }
                    }
                }

                farthest = System.Math.Max(farthest, reach);
            }
        }

        return (pixels, farthest);
    }

    /// <summary>
    /// The mix and the shares as PNGs beside the bake's (<c>logs/</c>, which <c>.gitignore</c> covers) — the probe cannot
    /// see a drawing, but a session can open a file (D342).
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Saved opaque, for eyes</b>: the mix keeps water in alpha, so as it stands a viewer shows the river and nothing
    /// else (A24, the first look). Here alpha is 255 and the water reads as black: red grass, green wood, blue seam.
    /// </remarks>
    private void SaveTheMix()
    {
        if (_weights is null || _sharesImage is null)
        {
            return;
        }

        string folder = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "logs");
        System.IO.Directory.CreateDirectory(folder);

        byte[] seen = (byte[])_weights.Clone();
        for (int i = 3; i < seen.Length; i += 4)
        {
            seen[i] = 255;
        }

        Image.CreateFromData(_width * WeightsPerTile, _height * WeightsPerTile, false, Image.Format.Rgba8, seen)
            .SavePng(System.IO.Path.Combine(folder, "valley-mix.png"));
        _sharesImage.SavePng(System.IO.Path.Combine(folder, "valley-mix-shares.png"));
    }

    /// <summary>
    /// ⭐ The probe's <c>seams drawn:</c> line (D561) — <b>the drawn stone and iron lie on their own tiles</b>. Joe: the
    /// quarry's ground <i>"doesn't seem to line up very well with the map"</i>: the outline is traced from the seam's tiles,
    /// so the drawn seam has to lie on them. Two shares, over every pixel near a seam in this valley: the
    /// pixels drawn as a seam that lie on, or within half a tile of, a tile of that seam; and the seam tiles whose centre is
    /// drawn as themselves.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Measured before it was tuned, and the tuning was dropped.</b> D561 set out to lower rock's and iron's
    /// <see cref="HowFreeToWander"/> so the seam would hug its tiles. The founding valley read 99.8 % within half a tile and
    /// 91.1 % of tiles drawn as themselves at 0.55 — and 99.8 / 92.1 / 91.4 / 91.7 % at 0.30 / 0.15 / 0.08: the wander was
    /// never what moved the edge (83 % lie strictly on their own tile at every value; the rest is the valley's own warp and
    /// smoothing, river and woods alike). The floors sit just under today's reading; an exaggerated wander (3.0) drowns the
    /// seam's own tiles to 71 % and fails it.
    /// </remarks>
    internal static string SeamsHugTheirTiles(SimWorld world)
    {
        System.ArgumentNullException.ThrowIfNull(world);
        var bake = new ValleyTexture();
        bake.Refresh(world);
        (double drawnOnTiles, double tilesDrawn, int pixels, int tiles) = bake.SeamHug(world.Map);

        string said = $"{drawnOnTiles:P1} of {pixels:N0} pixels drawn as stone or iron lie within half a tile of their own seam, "
            + $"{tilesDrawn:P1} of {tiles:N0} seam tiles are drawn as themselves at their centre (rock and iron wander {HowFreeToWander(Terrain.Rock):F2})";
        return drawnOnTiles >= SeamDrawnNearItsTilesFloor && tilesDrawn >= SeamTilesDrawnFloor
            ? $"[widths] seams drawn: ✅ {said}"
            : $"[widths] seams drawn: ⛔ under {SeamDrawnNearItsTilesFloor:P1} / {SeamTilesDrawnFloor:P0} — {said}";
    }

    /// <summary>The least share of drawn stone and iron that may lie within half a tile of its own seam (D561; reads 99.8 %).</summary>
    private const double SeamDrawnNearItsTilesFloor = 0.995;

    /// <summary>The least share of seam tiles drawn as themselves at their centre (D561; reads 91.1 %).</summary>
    private const double SeamTilesDrawnFloor = 0.85;

    private (double DrawnOnTiles, double TilesDrawn, int Pixels, int Tiles) SeamHug(GeneratedMap map)
    {
        int pixels = 0, onTiles = 0, tiles = 0, drawn = 0;
        int wide = map.Width * PixelsPerTile;
        int tall = map.Height * PixelsPerTile;
        for (int py = 0; py < tall; py++)
        {
            for (int px = 0; px < wide; px++)
            {
                float cx = ((px + 0.5f) / PixelsPerTile) - 0.5f;
                float cy = ((py + 0.5f) / PixelsPerTile) - 0.5f;
                int tx = Mathf.RoundToInt(cx);
                int ty = Mathf.RoundToInt(cy);
                if (!ASeamWithin(map, tx, ty))
                {
                    continue;
                }

                Terrain kind = Winner(map, px, py, out _);
                if (!TerrainRules.IsSeam(kind))
                {
                    continue;
                }

                pixels++;
                bool near = false;
                for (int oy = -1; oy <= 1 && !near; oy++)
                {
                    for (int ox = -1; ox <= 1 && !near; ox++)
                    {
                        var at = new GridPos(_minX + Mathf.RoundToInt(cx + (ox * 0.5f)), _minY + Mathf.RoundToInt(cy + (oy * 0.5f)));
                        near = map.Contains(at) && map.TerrainAt(at) == kind;
                    }
                }

                onTiles += near ? 1 : 0;
            }
        }

        for (int ty = 0; ty < map.Height; ty++)
        {
            for (int tx = 0; tx < map.Width; tx++)
            {
                Terrain kind = map.TerrainAt(new GridPos(_minX + tx, _minY + ty));
                if (!TerrainRules.IsSeam(kind))
                {
                    continue;
                }

                tiles++;
                int centre = (PixelsPerTile / 2) - 1;
                drawn += Winner(map, (tx * PixelsPerTile) + centre, (ty * PixelsPerTile) + centre, out _) == kind ? 1 : 0;
            }
        }

        return (pixels == 0 ? 1d : onTiles / (double)pixels, tiles == 0 ? 1d : drawn / (double)tiles, pixels, tiles);
    }

    private bool ASeamWithin(GeneratedMap map, int tx, int ty)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                var at = new GridPos(_minX + tx + dx, _minY + ty + dy);
                if (map.Contains(at) && TerrainRules.IsSeam(map.TerrainAt(at)))
                {
                    return true;
                }
            }
        }

        return false;
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
    private static float Wobble(float px, float py, int salt, float perTile = 1.6f)
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
