using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The watercolour's own values — <b>what the valley adds on its way toward the concept art</b>
/// (<c>specs/terrain-shader.md §7</c>, A26–A27), kept apart from the grass's approved look and the lab's terrain file.
/// </summary>
/// <remarks>
/// <para>
/// The same rule as <see cref="GrassLook"/> and <see cref="GrassLabTerrain"/>: <b>this class gives its properties no
/// defaults</b>; the numbers live in <see cref="Path"/>, the shader holds none, and the probe asks that every one reaches
/// the layer. A value here is Joe's to approve by eye; until he does, the file says so.
/// </para>
/// </remarks>
[GlobalClass]
public sealed partial class ValleyWatercolourLook : Resource
{
    /// <summary>Where the values live.</summary>
    internal const string Path = "res://looks/valley_watercolour.tres";

    /// <summary>The pen's ink, sRGB: a warm sepia-black (the art direction, A26).</summary>
    [Export]
    public Color InkColour { get; set; }

    /// <summary>How dark the ink lies at the line's heart, 0 to 1: a pen line, not a cut.</summary>
    [Export]
    public float InkStrength { get; set; }

    /// <summary>
    /// The bank's stroke at its middle pressure, in screen pixels — the same at every zoom, as a pen's is on paper. ⛔ The
    /// bank is the only ground the pen draws (A28: woods and seams are not outlined; the trees are).
    /// </summary>
    [Export]
    public float InkWidthPx { get; set; }

    /// <summary>How far the pen's pressure swells and thins the stroke along its length: 0 even, 1 from nothing to double.</summary>
    [Export]
    public float InkPressure { get; set; }

    /// <summary>How long one swell of the pen's pressure is, along the bank, in tiles.</summary>
    [Export]
    public float InkPressureTiles { get; set; }

    /// <summary>Where the pressure is lightest the pen lifts: the share of the pressure's range under which the stroke breaks.</summary>
    [Export]
    public float InkLifts { get; set; }

    /// <summary>How far the ink's edge creeps raggedly into the paper's fibres, in screen pixels.</summary>
    [Export]
    public float InkFeatherPx { get; set; }

    /// <summary>
    /// How far each tree's ink outline reaches past its crown, in tiles (A28: the trees are outlined, the ground is not). In
    /// tiles, because the crowns are a mesh in tiles: a pixel and a half at 32 px a tile, three at the closest zoom.
    /// </summary>
    [Export]
    public float TreeInkTiles { get; set; }

    /// <summary>How much darker the river's wash lies at its own edge, where the pigment pools as it dries (0 to 1).</summary>
    [Export]
    public float PoolStrength { get; set; }

    /// <summary>How far inside its edge a wash's pooling reaches, in screen pixels.</summary>
    [Export]
    public float PoolWidthPx { get; set; }

    /// <summary>
    /// The year's snow at its height, as a share of a blanket (A41, A26 call 3: <i>"winter patchy, with shadows"</i>): below 1
    /// the snow lies in drifts with the ground between.
    /// </summary>
    [Export]
    public float SnowPeak { get; set; }

    /// <summary>The cool tint multiplied into the snow on a drift's lee, sRGB (A41).</summary>
    [Export]
    public Color SnowShadowColour { get; set; }

    /// <summary>How strongly a drift's lee takes the cool tint, 0 to 1 (A41).</summary>
    [Export]
    public float SnowShadowStrength { get; set; }

    /// <summary>The ice at the river's rim in winter, sRGB (A41).</summary>
    [Export]
    public Color IceColour { get; set; }

    /// <summary>How far in from the bank the ice reaches, in tiles (A41).</summary>
    [Export]
    public float IceWidthTiles { get; set; }

    /// <summary>How white the rim's ice lies at the height of winter, 0 to 1 (A41).</summary>
    [Export]
    public float IceStrength { get; set; }

    /// <summary>How much darker the river's channel lies at the height of winter, 0 to 1 (A41).</summary>
    [Export]
    public float RiverWinterDarken { get; set; }

    /// <summary>
    /// How much darker the river's wash lies down its middle, easing to nothing at the bank (A45, <c>river-drawn.md §4.2</c>).
    /// </summary>
    [Export]
    public float ChannelStrength { get; set; }

    /// <summary>How dark the two echo lines inside the bank lie, 0 to 1 (A45, §4.1): lighter than the bank's stroke.</summary>
    [Export]
    public float EchoStrength { get; set; }

    /// <summary>The echo lines' width as a share of the bank's stroke.</summary>
    [Export]
    public float EchoWidthShare { get; set; }

    /// <summary>How far inside the bank the nearer echo lies, in tiles.</summary>
    [Export]
    public float EchoNearTiles { get; set; }

    /// <summary>How far inside the bank the farther echo lies, in tiles.</summary>
    [Export]
    public float EchoFarTiles { get; set; }

    /// <summary>Where the echo lines' pen lifts — the share of its pressure's range under which they break; about half.</summary>
    [Export]
    public float EchoLifts { get; set; }

    /// <summary>How long one swell of the echo lines' pressure is, in tiles: about a dash and its gap.</summary>
    [Export]
    public float EchoDashTiles { get; set; }

    /// <summary>An echo closer than this many screen pixels to the bank fades out (the wide zoom), so the bank stays one stroke.</summary>
    [Export]
    public float EchoFadePx { get; set; }

    /// <summary>How dark the ripples lie, 0 to 1 (A45, §4.3).</summary>
    [Export]
    public float RippleStrength { get; set; }

    /// <summary>A ripple's thickness at its middle, in screen pixels; it tapers to nothing at both ends.</summary>
    [Export]
    public float RippleWidthPx { get; set; }

    /// <summary>The ripples' grid in the river's own coordinates, in tiles: X down the river, Y across it.</summary>
    [Export]
    public Vector2 RippleCellTiles { get; set; }

    /// <summary>The share of cells that hold a ripple (A45 call 2: sparse, like the concept).</summary>
    [Export]
    public float RippleShare { get; set; }

    /// <summary>The shortest and the longest ripple, in tiles.</summary>
    [Export]
    public Vector2 RippleLengthTiles { get; set; }

    /// <summary>How far a ripple bows to one side at its middle, in tiles.</summary>
    [Export]
    public float RippleBowTiles { get; set; }

    /// <summary>How far the ripples keep off each bank, in tiles — inside the echo lines and off the winter ice.</summary>
    [Export]
    public float RippleBankTiles { get; set; }

    /// <summary>How fast the ripples drift downstream, in tiles a real second, at every game speed (A45 call 1).</summary>
    [Export]
    public float RippleDriftTilesPerSecond { get; set; }

    /// <summary>How long a ripple lives before it is drawn anew, in seconds (rounded so a whole number fit the clock's period).</summary>
    [Export]
    public float RippleLifeSeconds { get; set; }

    /// <summary>Below this many pixels a tile the ripples fade out: a ripple a few pixels long reads as noise.</summary>
    [Export]
    public float RippleFadePxPerTile { get; set; }

    /// <summary>The hills (A66, <c>hill-edges.md</c>): a slope of this many height levels a tile is drawn as a 45° tilt.</summary>
    [Export]
    public float HillFullTiltLevels { get; set; }

    /// <summary>How much lighter ground facing the light is washed, a share of its tilt.</summary>
    [Export]
    public float HillLight { get; set; }

    /// <summary>How much darker ground facing away is washed, a share of its tilt.</summary>
    [Export]
    public float HillShade { get; set; }

    /// <summary>How much darker steep ground is again, most where it faces away.</summary>
    [Export]
    public float HillBank { get; set; }

    /// <summary>The steep bank's inked lip: how dark, against the bank's own stroke.</summary>
    [Export]
    public float HillLipStrength { get; set; }

    /// <summary>The lip's width, a share of the bank's pen.</summary>
    [Export]
    public float HillLipWidthShare { get; set; }

    /// <summary>The values as they stand in <see cref="Path"/>.</summary>
    internal static ValleyWatercolourLook Current => GD.Load<ValleyWatercolourLook>(Path);
}
