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

    /// <summary>
    /// ⭐ The green ground (A68, <c>specs/green-ground.md</c>, Joe's C on a mock): on, the grass's own ground is a green
    /// wash by height instead of the parchment. Off, the valley is A64's.
    /// </summary>
    [Export]
    public bool GreenOn { get; set; }

    /// <summary>The green at the river's level, sRGB: the deepest on the sheet, and what a hollow sinks toward.</summary>
    [Export]
    public Color GreenDeep { get; set; }

    /// <summary>The green a fifth of the way up.</summary>
    [Export]
    public Color GreenLow { get; set; }

    /// <summary>The green of open land, three fifths of the way up.</summary>
    [Export]
    public Color GreenMid { get; set; }

    /// <summary>The yellow-green of the highest ground.</summary>
    [Export]
    public Color GreenHigh { get; set; }

    /// <summary>What a crest or a shoulder blooms toward.</summary>
    [Export]
    public Color GreenCrest { get; set; }

    /// <summary>What a slope facing the light turns toward: warmer and lighter.</summary>
    [Export]
    public Color GreenWarm { get; set; }

    /// <summary>What a slope facing away turns toward: a cooler, deeper green. ⛔ A green, never a grey (A67).</summary>
    [Export]
    public Color GreenCool { get; set; }

    /// <summary>The seasons' tints over the summer's green (§3.6): spring's.</summary>
    [Export]
    public Color GreenSpring { get; set; }

    /// <summary>Summer's tint: white, the palette as written.</summary>
    [Export]
    public Color GreenSummer { get; set; }

    /// <summary>Autumn's tint.</summary>
    [Export]
    public Color GreenAutumn { get; set; }

    /// <summary>Winter's tint, under the snow.</summary>
    [Export]
    public Color GreenWinter { get; set; }

    /// <summary>The height levels the palette runs between: the same green for the same height in every valley.</summary>
    [Export]
    public Vector2 GreenLevels { get; set; }

    /// <summary>The level a flat valley (a save from before heights) is painted at.</summary>
    [Export]
    public float GreenFlatLevel { get; set; }

    /// <summary>How far the palette wanders with the place, a share of its whole run: the wash's own unevenness.</summary>
    [Export]
    public float GreenWander { get; set; }

    /// <summary>The crest's bloom: its gain on the field's crest, and the most it may turn the green.</summary>
    [Export]
    public Vector2 GreenCrestTurn { get; set; }

    /// <summary>The hollow's sinking: its gain on the field's crest where that is below zero.</summary>
    [Export]
    public float GreenHollow { get; set; }

    /// <summary>The lit slope's turn toward the warm green: its gain on the tilt toward the light, and the most.</summary>
    [Export]
    public Vector2 GreenWarmTurn { get; set; }

    /// <summary>The far slope's turn toward the cool green: its gain on the tilt away from the light.</summary>
    [Export]
    public float GreenCoolTurn { get; set; }

    /// <summary>How far steep ground turns toward the cool green, whichever way it faces.</summary>
    [Export]
    public float GreenSteep { get; set; }

    /// <summary>How much of the same turn the woods' floor and the seams take, 0 to 1 (§3.7).</summary>
    [Export]
    public float GreenKindsShare { get; set; }

    /// <summary>
    /// The woods' floor on the green ground (§3.7): how much of it is the land's own green (the rest its painted tile as
    /// it was), and how much deeper than the grass beside it that green lies (1 the same).
    /// </summary>
    [Export]
    public Vector2 GreenWood { get; set; }

    /// <summary>How much of the approved grass's strength (D632) lies over the green, 0 to 1.</summary>
    [Export]
    public float GreenGrassShare { get; set; }

    /// <summary>The brush (§3.5): how strongly its streaks show in the wash.</summary>
    [Export]
    public float BrushStrength { get; set; }

    /// <summary>A streak's length along the brush and its width across it, in tiles.</summary>
    [Export]
    public Vector2 BrushTiles { get; set; }

    /// <summary>The tuft marks (§3.5): how dark the pen lies.</summary>
    [Export]
    public float TuftStrength { get; set; }

    /// <summary>The cell a mark is hashed in, in tiles.</summary>
    [Export]
    public float TuftCellTiles { get; set; }

    /// <summary>A mark's length, shortest and longest, in tiles.</summary>
    [Export]
    public Vector2 TuftLengthTiles { get; set; }

    /// <summary>The share of cells that hold a mark.</summary>
    [Export]
    public float TuftShare { get; set; }

    /// <summary>A mark's width at its middle, in screen pixels.</summary>
    [Export]
    public float TuftWidthPx { get; set; }

    /// <summary>Below this many pixels a tile the marks fade out.</summary>
    [Export]
    public float TuftFadePxPerTile { get; set; }

    /// <summary>The hachures (§3.4): how dark, against the bank's own stroke.</summary>
    [Export]
    public float HachureStrength { get; set; }

    /// <summary>A hachure's width at the lip, a share of the bank's pen; it tapers to under a third.</summary>
    [Export]
    public float HachureWidthShare { get; set; }

    /// <summary>Below this many pixels a tile the hachures fade out.</summary>
    [Export]
    public float HachureFadePxPerTile { get; set; }

    /// <summary>
    /// The painted crowns' tint on the green ground (§3.8, Joe: <i>"Yep!"</i>): about a fifth deeper, so the woods are the
    /// darkest thing on the land. Not the winter set, whose snow stays white.
    /// </summary>
    [Export]
    public Color TreeTint { get; set; }

    /// <summary>The steep bank's inked lip: how dark, against the bank's own stroke.</summary>
    [Export]
    public float HillLipStrength { get; set; }

    /// <summary>The lip's width, a share of the bank's pen.</summary>
    [Export]
    public float HillLipWidthShare { get; set; }

    /// <summary>
    /// The painted paths (A72, <c>painted-paths.md</c>, Joe: <i>"B, painted track"</i>): the worn tracks painted into the
    /// ground with the tick on, the mesh not drawn. Off, the mesh is drawn as with the tick off.
    /// </summary>
    [Export]
    public bool PathOn { get; set; }

    /// <summary>The dirt of a lightly worn track (§3.2).</summary>
    [Export]
    public Color PathWorn { get; set; }

    /// <summary>The dirt of a packed track, a little deeper (§3.2).</summary>
    [Export]
    public Color PathPacked { get; set; }

    /// <summary>The drier, yellowed grass where feet stray off the track (§3.3).</summary>
    [Export]
    public Color PathHalo { get; set; }

    /// <summary>The halo's reach past the edge, tiles: x where it is least, y added where the patches are fullest.</summary>
    [Export]
    public Vector2 PathHaloTiles { get; set; }

    /// <summary>The halo's strength: x where it is least, y added where the patches are fullest; worn ground takes 0.7 of it.</summary>
    [Export]
    public Vector2 PathHaloStrength { get; set; }

    /// <summary>The wash's mottling (x, a share of its colour) and its granulation (y).</summary>
    [Export]
    public Vector2 PathMottle { get; set; }

    /// <summary>Pigment pooled inside the edge: how much darker (x) over how many tiles in (y).</summary>
    [Export]
    public Vector2 PathRim { get; set; }

    /// <summary>The ruts on a packed track: how much darker.</summary>
    [Export]
    public float PathRuts { get; set; }

    /// <summary>How far the edge wanders either way, tiles, so a lane's two sides are never ruled parallel.</summary>
    [Export]
    public float PathWobbleTiles { get; set; }

    /// <summary>The pen down each side: how dark on packed ground, against the bank's own stroke.</summary>
    [Export]
    public float PathPenStrength { get; set; }

    /// <summary>The pen on lightly worn ground, a share of the packed pen's strength.</summary>
    [Export]
    public float PathPenWornShare { get; set; }

    /// <summary>The pen's width, a share of the bank's pen.</summary>
    [Export]
    public float PathPenWidthShare { get; set; }

    /// <summary>Below this many pixels a tile the pen fades out.</summary>
    [Export]
    public float PathPenFadePxPerTile { get; set; }

    /// <summary>How much of the snow a track clears (trodden), so the network reads in winter.</summary>
    [Export]
    public float PathSnowClear { get; set; }

    /// <summary>The values as they stand in <see cref="Path"/>.</summary>
    /// <remarks>
    /// ⛔ <b>Loaded once and held</b> (D746, the probe's finalizer crash). `GD.Load` hands back the one cached resource, but a
    /// fresh C# wrapper each call — every one dropped became garbage, and the finalizer disposing one while the main thread
    /// loaded the next crashed .NET (*"gchandle.is_released()"*, then `SwapGCHandleForType`: *"Handle is not initialized"*,
    /// caught in both stacks at once). The values are the same either way; one held reference is never finalized.
    /// </remarks>
    internal static ValleyWatercolourLook Current => _current ??= GD.Load<ValleyWatercolourLook>(Path);

    private static ValleyWatercolourLook? _current;
}
