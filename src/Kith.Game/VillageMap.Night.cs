using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐⭐ THE NIGHT VIEW, WITH LIT HOUSES (A249, <c>specs/night-view.md</c>; Joe: <i>"i do want to add night time. not a day/night
/// cycle, but a button toggle on the main screen … basically lights on/lights off"</i>, and <i>"houses should show lights on when
/// people are home the same way the tavern does"</i>; his seven calls, <i>"All as recommended"</i>). One tint over everything
/// painted: a uniform in the ground's, the fields' and the flakes' shaders, and the map's own <c>SelfModulate</c>, which reaches
/// no child and no HUD — nothing is baked or laid again. What must still read is drawn ÷ the tint, so the multiply gives it
/// back: the player's marks wholly and in a pale partner of their hue, people to <see cref="PeopleLift"/> of daylight.
/// <b>A view setting only</b>: nothing in the sim knows it, nothing here is hashed, and there is no evening — lit means home.
/// </remarks>
public partial class VillageMap
{
    /// <summary>The deep night (call 1): the one colour the painted valley is multiplied by, and how far its own colour goes grey first.</summary>
    internal static readonly Color DeepNight = new(0.36f, 0.43f, 0.60f);

    internal const float DeepNightGrey = 0.35f;

    /// <summary>How far people come back to daylight, painted land's washes, and the falling flakes.</summary>
    internal const float PeopleLift = 0.8f, WashLift = 0.35f, FlakeLift = 0.45f;

    /// <summary>Day to night and back, in real seconds — a player's switch, so it moves while the village is paused too.</summary>
    internal const float NightFadeSeconds = 1f;

    /// <summary>
    /// A home's lamp comes up and goes down over this, in the people's seconds: 60 % of the times somebody is home last one tick
    /// (<c>night-view.md §2</c>), and a lamp as quick as the tavern's would blink at each.
    /// </summary>
    internal const float HomeLampSeconds = 4f;

    /// <summary>The forge's light.</summary>
    internal static readonly Color ForgeLight = new("#ff8a2c");

    /// <summary>
    /// ⭐ The marks by night, one a mark, by its daytime colour: today's are dark inks chosen for parchment and vanish on a dark
    /// valley (<c>§4</c>: least contrast 3.4 against day's 2.3). A cached outline keeps its daytime colour and is answered at
    /// the draw, so a toggle traces nothing again. ⛔ Only what the player must read is here: a fence, a wall, an ink outline
    /// is part of the valley and goes dark with it.
    /// </summary>
    internal static readonly IReadOnlyDictionary<Color, Color> MarksAtNight = new Dictionary<Color, Color>
    {
        [GhostFine] = new("#8fe0a4"),
        [GhostWarned] = new("#f2cf6a"),
        [GhostRefused] = new("#ff8f86"),
        [SelectedRing] = new("#cda4ff"),
        [ResidentialEdge] = new("#e8bd7c"),
        [HarvestEdge] = new("#ffb765"),
        [DestroyEdge] = new("#ff8a94"),
        [WorkGroundEdge] = new("#7fd6e0"),
        [FullStoreColour] = new("#f0bd6e"),
        [IdleWorkplaceColour] = new("#93cdea"),
        [SiteEdge] = new("#c9d4e0"),
        [DemolishEdge] = new("#f0a26e"),
        [AdultColour] = new("#efe2c4"),
        [ChildColour] = new("#9fd8f0"),
        [ElderColour] = new("#f0c98a"),
        [MealMark] = new("#f2cf6a"),
        [ValleyEdge] = new("#a8967a"),
    };

    /// <summary>The seam a mine in hand would claim, by night: its stakes are the valley's dark timber once they stand.</summary>
    private static readonly Color GhostClaimAtNight = new("#e0b98c");

    private bool _night;
    private float _nightLevel;
    private double _onScreenSeconds;

    /// <summary>Whether the night is on — what the player asked for, whatever the fade is at.</summary>
    public bool NightShown => _night;

    /// <summary>How far the night has come, 0 day to 1 night.</summary>
    internal float NightLevel => _nightLevel;

    /// <summary>The colour the map is multiplied by now: white by day, exactly.</summary>
    internal Color NightTint => _nightLevel <= 0f ? Colors.White : Colors.White.Lerp(DeepNight, _nightLevel);

    /// <summary>
    /// Night or day. A switch on a village on screen fades over <see cref="NightFadeSeconds"/>; the player's setting read as a
    /// village opens is the night at once — a valley does not open by day and then go dark.
    /// </summary>
    public void ShowNight(bool on)
    {
        _night = on;
        if (_onScreenSeconds < NightFadeSeconds)
        {
            NightIsAt(on ? 1f : 0f);
        }
    }

    /// <summary>What a painted person is drawn × — white by day; by night four fifths of the way back to daylight, so they are found.</summary>
    internal Color PersonTint(float shown) => Lifted(new Color(1f, 1f, 1f, shown), PeopleLift);

    /// <summary>The night's clock, each frame (<see cref="_Process"/>).</summary>
    private void TickTheNight(double delta)
    {
        _onScreenSeconds += delta;
        NightIsAt(NightAfter(_nightLevel, _night, delta));
    }

    /// <summary>Pure: the night's level a frame later.</summary>
    internal static float NightAfter(float level, bool on, double seconds) => Mathf.MoveToward(level, on ? 1f : 0f, (float)seconds / NightFadeSeconds);

    private void NightIsAt(float level)
    {
        if (level == _nightLevel)
        {
            return;
        }

        _nightLevel = level;
        SelfModulate = NightTint;
        QueueRedraw();
    }

    /// <summary>The night to the ground, the fields and the flakes, with the valley's other uniforms (<see cref="PaintTheWatercolour"/>).</summary>
    private void HandTheNightOver(ShaderMaterial ground)
    {
        Color tint = NightTint;
        var t = new Vector3(tint.R, tint.G, tint.B);
        float grey = DeepNightGrey * _nightLevel;
        ground.SetShaderParameter("night_tint", t);
        ground.SetShaderParameter("night_grey", grey);
        _fieldsMaterial?.SetShaderParameter("night_tint", t);
        _fieldsMaterial?.SetShaderParameter("night_grey", grey);
        _snowfallMaterial?.SetShaderParameter("night_tint", t.Lerp(Vector3.One, FlakeLift));
    }

    /// <summary>⭐ A colour as it must be drawn so that under the night's tint it comes out <paramref name="share"/> of the way back to itself.</summary>
    internal Color Lifted(Color colour, float share)
    {
        if (_nightLevel <= 0f)
        {
            return colour;
        }

        Color tint = NightTint;
        return new Color(
            colour.R * Mathf.Lerp(1f, 1f / tint.R, share), colour.G * Mathf.Lerp(1f, 1f / tint.G, share), colour.B * Mathf.Lerp(1f, 1f / tint.B, share), colour.A);
    }

    /// <summary>
    /// ⭐ A mark as it is drawn now, by its daytime colour (<see cref="MarksAtNight"/>): that colour by day, exactly; its pale
    /// partner by night, lifted clear of the tint; its own alpha always. A colour that is no mark is the valley's, and is left alone.
    /// </summary>
    internal Color AtNight(Color day) =>
        _nightLevel > 0f && MarksAtNight.TryGetValue(day with { A = 1f }, out Color night) ? AtNight(day, night) : day;

    /// <summary><see cref="AtNight(Color)"/> for a colour the valley has too, where this draw of it is a mark.</summary>
    private Color AtNight(Color day, Color night) =>
        _nightLevel <= 0f ? day : Lifted(day.Lerp(night, _nightLevel) with { A = day.A }, 1f);

    // ------------------------------------------------------------------ who is home

    /// <summary>The households (ids) with one of the family inside their own house this tick — kept once a tick where the fires are noted.</summary>
    private readonly HashSet<int> _homesLit = new();

    /// <summary>Whether <see cref="NoteTheFires"/> has run: before it, no lamp knows whether it is lit.</summary>
    private bool _lampsAreNoted;

    /// <summary>⭐ Pure: whether somebody in this state lights their home — inside, and the house their own family's.</summary>
    internal static bool LightsTheHome(VillagerState state, bool housed, bool inADooredBuilding, bool onTheirOwnHouse) =>
        onTheirOwnHouse && Indoors(state, housed, inADooredBuilding);

    /// <summary>Once a tick, per villager (from <see cref="NoteTheFires"/>): somebody inside their own home lights it. The house is asked only of someone in a state that can be in.</summary>
    private void NoteTheHome(SimWorld world, Villager villager)
    {
        if (!Indoors(villager.State, housed: true, inADooredBuilding: true))
        {
            return;
        }

        Household own = world.HouseholdOf(villager);
        bool inside = GoesInByTheDoor(villager.State) && world.DoorCovering(villager.Tile) is Door { Shut: false };
        if (LightsTheHome(villager.State, own.HasHome, inside, world.HomeFootprintOf(own) is Footprint house && house.Covers(villager.Tile)))
        {
            _homesLit.Add(own.Id);
        }
    }

    // ------------------------------------------------------------------ the light

    /// <summary>Which yards have lamps: the tavern by day and night; a home, the forge and the oven by night only (call 3).</summary>
    internal static bool HasLamps(YardKind yard) => yard is YardKind.Tavern or YardKind.Cabin or YardKind.Cottage or YardKind.Manor or YardKind.Smith or YardKind.Bakery;

    private static bool IsAHome(YardKind yard) => yard is YardKind.Cabin or YardKind.Cottage or YardKind.Manor;

    private static Color LightOf(YardKind yard) => yard == YardKind.Smith ? ForgeLight : LampLight;

    /// <summary>Pure: a lamp's level after a frame — a home's slow, the rest as the smoke; paused, held.</summary>
    internal static float LampOn(float level, bool lit, double frameSeconds, bool home) =>
        home ? Mathf.MoveToward(level, lit ? 1f : 0f, (float)frameSeconds / HomeLampSeconds) : SmokeOn(level, lit, frameSeconds);

    /// <summary>Pure: <see cref="LampOn"/>, and a lamp first drawn is as it is — a village opened paused, or a house panned to, is not dark first.</summary>
    internal static float LampNow(float level, bool lit, double frameSeconds, bool home) =>
        level < 0f ? (lit ? 1f : 0f) : LampOn(level, lit, frameSeconds, home);

    /// <summary>
    /// A home's lamps, the forge's and the oven's, laid with the roof (<c>§5</c>) and kept apart from its runs as the tavern's are: the
    /// door; a cabin one front window and one at the side, a cottage two and one each side, a manor two and two each side.
    /// </summary>
    /// <remarks>Laid beside the roof, not in <see cref="RoofDrawing"/>: what a building is drawn with by day is the same list it was.</remarks>
    internal static List<RoofPiece> LampsOf(RoofSignature s)
    {
        var pieces = new List<RoofPiece>();
        YardKind yard = s.Look.Yard;
        if (!HasLamps(yard) || yard == YardKind.Tavern || s.FallenIn)
        {
            return pieces;
        }

        // The roof's own frame and its part of the ground, as RoofDrawing has them — in tile space.
        var frame = new RoofFrame(s.At, s.Facing, 1f);
        var k = new YardKit(pieces, frame);
        float hw = s.Wide / 2f, hd = s.Deep / 2f;
        (Vector2 way, Vector2 doorMiddle) = s.Door is Door d ? DoorInFrame(frame, s.Wide, s.Deep, s.Facing, d, s.DoorAt, 1f) : (new Vector2(0, 1), new Vector2(0, hd));
        YardLayout layout = LayoutOf(yard, hw, hd, way, doorMiddle, s.Seed);
        bool works = !IsAHome(yard);
        Vector2 roofAt = works ? layout.RoofAt : Vector2.Zero;
        float ew = (works ? layout.RoofHw : hw) + RoofEaves;
        float ed = (works ? layout.RoofHd : hd) + RoofEaves;
        int seed = s.Seed;
        var across = new Vector2(-way.Y, way.X);
        bool sideways = Mathf.Abs(way.X) > 0.5f;
        float depthHalf = sideways ? ew : ed;
        float half = sideways ? ed : ew;
        float front = roofAt.Dot(way) + depthHalf;
        float inset = Mathf.Max(0f, half - 0.2f);
        float door = Mathf.Clamp((doorMiddle - roofAt).Dot(across), -inset, inset);
        Vector2 On(float along, float outward) => (across * (along + roofAt.Dot(across))) + (way * outward);

        void Window(string part, Vector2 a, Vector2 b, Vector2 outward, float reach)
        {
            Vector2 along = (b - a).Normalized();
            k.Pieces.Add(new RoofPiece(RoofPieceKind.Fill, part, k.On(new[]
            {
                a, b, b + (outward * reach) + (along * reach * 0.5f), a + (outward * reach) - (along * reach * 0.5f),
            }), LightOf(yard)));
            k.Pieces.Add(new RoofPiece(RoofPieceKind.Line, part + " window", k.On(new[] { a.Lerp(b, 0.3f), a.Lerp(b, 0.7f) }), LampWindow));
        }

        void Front(float at)
        {
            float middle = Mathf.Clamp(at, -inset, inset);
            if (Mathf.Abs(middle - door) > 0.25f)
            {
                Window("lamplight front", On(middle - 0.11f, front), On(middle + 0.11f, front), way, LampReach * 0.85f);
            }
        }

        Window("lamplight door", On(door - 0.08f, front), On(door + 0.08f, front), way, LampReach * (works ? 1f : 0.8f));
        if (works)
        {
            return pieces;
        }

        float roomy = door > 0.05f ? -1f : door < -0.05f ? 1f : (seed & 2) == 0 ? 1f : -1f;
        Front(door + (roomy * 0.62f));
        if (yard != YardKind.Cabin)
        {
            Front(door - (roomy * 0.62f));
        }

        foreach (float side in yard == YardKind.Cabin ? new[] { roomy } : new[] { -1f, 1f })
        {
            Vector2 wall = On(side * half, roofAt.Dot(way));
            float w = yard == YardKind.Manor ? 0.1f : 0.12f;
            foreach (float z in yard == YardKind.Manor ? new[] { -0.45f, 0.45f } : new[] { 0f })
            {
                Vector2 at = wall + (way * depthHalf * z);
                Window("lamplight side", at - (way * w), at + (way * w), across * side, LampSideReach);
            }
        }

        return pieces;
    }

    /// <summary>One building's lamps, after its roof: the level moved on, then the daytime fans (the tavern's alone) and the lit slits.</summary>
    private void DrawTheLamps(YardKind yard, RoofCache held, int seed, bool burning, float alpha)
    {
        if (held.Lamps.Length == 0)
        {
            return;
        }

        bool home = IsAHome(yard);
        bool lit = home ? _homesLit.Contains(seed) : yard == YardKind.Tavern ? _tavernsLit.Contains(seed) : burning;
        if (_lampsAreNoted)
        {
            held.Light = LampNow(held.Light, lit, _peopleFrameSeconds, home);
        }

        float slits = yard == YardKind.Tavern ? 1f : _nightLevel;
        DrawTheLamplight(held, yard == YardKind.Tavern ? 1f - _nightLevel : 0f, slits, alpha);
    }

    /// <summary>
    /// ⭐ The soft painted light of a lit building by night (call 2), before its roof: a pool at each window and the door, under the
    /// eaves, a wider one before the door, and a faint one round them all.
    /// </summary>
    private void DrawTheGlow(YardKind yard, RoofCache held, float alpha)
    {
        float level = held.Light * alpha * _nightLevel;
        if (level <= 0.004f || held.Lamps.Length == 0)
        {
            return;
        }

        Color warm = Lifted(LightOf(yard), 1f);
        Texture2D pool = ThePool();

        void Pool(Vector2 at, float radius, float strength)
        {
            float px = radius * _pixelsPerTile;
            DrawTextureRect(pool, new Rect2(ToScreen(at) - new Vector2(px, px), new Vector2(px, px) * 2f), tile: false, modulate: warm with { A = strength * level });
        }

        Vector2 sum = Vector2.Zero;
        Vector2 door = Vector2.Zero;
        int lamps = 0;
        foreach (RoofPiece lamp in held.Lamps)
        {
            if (lamp.Kind != RoofPieceKind.Fill)
            {
                continue;
            }

            Vector2 wall = (lamp.Points[0] + lamp.Points[1]) / 2f;
            Vector2 far = (lamp.Points[2] + lamp.Points[3]) / 2f;
            Pool(wall + ((far - wall) * 0.55f), 0.62f, 0.5f);
            sum += far;
            lamps++;
            if (lamp.Part == "lamplight door")
            {
                door = far;
            }
        }

        Pool(door, 1.5f, 0.32f);
        Pool(sum / lamps, 2.3f, 0.2f);
    }

    /// <summary>The pool's painting's side, px.</summary>
    internal const int PoolPixels = 160;

    private Texture2D? _pool;

    private Texture2D ThePool()
    {
        if (_pool is null)
        {
            Image image = Image.CreateFromData(PoolPixels, PoolPixels, false, Image.Format.Rgba8, PaintThePool());
            image.GenerateMipmaps();
            _pool = ImageTexture.CreateFromImage(image);
        }

        return _pool;
    }

    /// <summary>Pure: the pool, painted once, white for the draw to colour — a soft disc whose edge wanders, as a wash dries.</summary>
    internal static byte[] PaintThePool()
    {
        const int n = PoolPixels;
        var rgba = new byte[n * n * 4];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2(((x + 0.5f) / n * 2f) - 1f, ((y + 0.5f) / n * 2f) - 1f);
                float angle = Mathf.Atan2(p.Y, p.X);
                float edge = 0.86f + (0.06f * Mathf.Sin((3f * angle) + 1.1f)) + (0.04f * Mathf.Sin((5f * angle) + 4f)) + (0.02f * Mathf.Sin((11f * angle) + 2f));
                float u = Mathf.Clamp(1f - (p.Length() / edge), 0f, 1f);
                int i = ((y * n) + x) * 4;
                rgba[i] = rgba[i + 1] = rgba[i + 2] = 255;
                rgba[i + 3] = (byte)(255f * u * u * (3f - (2f * u)));
            }
        }

        return rgba;
    }
}
