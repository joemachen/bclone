using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐⭐ SICK ON THE MAP (A321, <c>specs/sickbed-art.md §9</c>; Joe: <i>"Yes, as recommended"</i>). W6's sick state
/// (<c>health.md §5.5</c>) shown in the world: <b>Joe's painted sick face over the house</b> while somebody lies in bed there, and
/// over the head of anyone ill out of doors (walking home, or lying where they sleep with no roof); <b>the home strewn</b> — straw
/// before the door, a sprig of herbs hung by it — while somebody in the house lies sick. The face keeps its size on screen at every
/// zoom, so a sick house reads from far out. ⛔ <b>No smoke of its own</b> (§9 call 2): smoke means firewood burning (A139), and the
/// sim lights no fire for the sick. View only; nothing here is hashed. Only with the people painted — the dots are as they were.
/// </remarks>
public partial class VillageMap
{
    /// <summary>The sick face's size on screen, in pixels, at every zoom (§9: a sick house reads from three steps out).</summary>
    internal const float SickFacePixels = 30f;

    /// <summary>The straw's painting, and the sprig's, in pixels.</summary>
    internal const int StrawPixels = 96, SprigWide = 48, SprigTall = 64;

    /// <summary>The straw bed's size, and the sprig's height, in tiles.</summary>
    private const float StrawTiles = 0.95f, SprigTiles = 0.42f;

    /// <summary>The homes somebody lies sick in, by household — noted once a tick where the states change (<see cref="NoteTheSick"/>).</summary>
    private readonly HashSet<int> _homesSick = new();

    private Texture2D? _straw, _sprig;

    /// <summary>Where the sick face goes, if anywhere.</summary>
    internal enum SickMarkAt
    {
        None,
        OverTheHouse,
        OverTheHead,
    }

    /// <summary>
    /// ⭐ Pure: in bed at home, over the house; ill and out of doors — on the way home (<c>SickDays</c> set before they lie down),
    /// or in bed with no roof — over the head; well, nowhere.
    /// </summary>
    internal static SickMarkAt SickMarkOf(VillagerState state, int sickDays, bool housed) =>
        state == VillagerState.SickInBed && housed ? SickMarkAt.OverTheHouse
        : state == VillagerState.SickInBed || sickDays > 0 ? SickMarkAt.OverTheHead
        : SickMarkAt.None;

    /// <summary>
    /// ⭐ Pure: the states whose being indoors depends on a house — resting, and ⭐ <b>lying sick</b> (A321: before it, the view
    /// asked only of someone resting, and a person in bed at home was drawn standing at their door).
    /// </summary>
    internal static bool AsksForAHouse(VillagerState state) => state is VillagerState.Resting or VillagerState.SickInBed;

    /// <summary>Once a tick, for each villager (<see cref="NoteTheFires"/>): whose home has somebody lying sick in it.</summary>
    private void NoteTheSick(SimWorld world, Villager villager)
    {
        if (villager.State == VillagerState.SickInBed && world.HouseholdOf(villager) is { HasHome: true } own)
        {
            _homesSick.Add(own.Id);
        }
    }

    /// <summary>The straw before each sick home's door and the herbs hung by it — over the houses, under the people.</summary>
    private void DrawTheSickHomes(SimWorld world)
    {
        if (_homesSick.Count == 0)
        {
            return;
        }

        foreach (Household household in world.Households)
        {
            if (!_homesSick.Contains(household.Id) || household.HomePosition is not Point site || world.DoorOf(household) is not Door door)
            {
                continue;
            }

            Vector2 centre = ToScreen(site);
            Vector2 step = ToScreen(_paintedSteps.TryGetValue(door.Tile, out Vector2 painted) ? painted : DoorWallOf(door));
            Vector2 outward = (step - centre).Normalized();
            if (outward == Vector2.Zero)
            {
                continue;
            }

            // The straw: its painting's top edge at the step, laid outward from the wall.
            float side = StrawTiles * _pixelsPerTile;
            DrawSetTransform(step - (outward * 0.12f * _pixelsPerTile), outward.Angle() - (Mathf.Pi / 2f), Vector2.One);
            DrawTextureRect(TheStraw(), new Rect2(new Vector2(-side / 2f, 0f), new Vector2(side, side)), tile: false, modulate: AtNight(Colors.White));
            DrawSetTransformMatrix(Transform2D.Identity);

            // The sprig: hung on the wall beside the door, upright.
            float tall = SprigTiles * _pixelsPerTile;
            Vector2 tie = step - (outward * 0.18f * _pixelsPerTile) + (outward.Orthogonal() * 0.34f * _pixelsPerTile);
            var size = new Vector2(tall * SprigWide / SprigTall, tall);
            DrawTextureRect(TheSprig(), new Rect2(tie - new Vector2(size.X / 2f, size.Y * 0.08f), size), tile: false, modulate: AtNight(Colors.White));
        }
    }

    /// <summary>Joe's sick face over each house somebody lies sick in — over the people, at its own size on screen.</summary>
    private void DrawTheSickFacesOverHouses(SimWorld world)
    {
        if (_homesSick.Count == 0)
        {
            return;
        }

        foreach (Household household in world.Households)
        {
            if (_homesSick.Contains(household.Id) && household.HomePosition is Point site)
            {
                DrawTheSickFace(ToScreen(site) - new Vector2(0f, SickFacePixels * 0.5f));
            }
        }
    }

    /// <summary>The sick face, centred here, <see cref="SickFacePixels"/> across, a soft shadow under it.</summary>
    private void DrawTheSickFace(Vector2 centre)
    {
        if (UiIcons.Find("health", "sick") is not Texture2D face)
        {
            return;
        }

        float s = SickFacePixels;
        DrawCircle(centre + new Vector2(s * 0.1f, s * 0.14f), s * 0.44f, new Color(0.16f, 0.12f, 0.08f, 0.28f));
        DrawTextureRect(face, new Rect2(centre - new Vector2(s / 2f, s / 2f), new Vector2(s, s)), tile: false);
    }

    private Texture2D TheStraw() => _straw ??= Painted(PaintTheStraw(), StrawPixels, StrawPixels);

    private Texture2D TheSprig() => _sprig ??= Painted(PaintTheSprig(), SprigWide, SprigTall);

    private static Texture2D Painted(byte[] rgba, int wide, int tall)
    {
        Image image = Image.CreateFromData(wide, tall, false, Image.Format.Rgba8, rgba);
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>The straw's colours: four golds, and the pen's shadow under a few stalks.</summary>
    internal static readonly Color[] StrawGolds = { new("#dec478"), new("#ceb060"), new("#ecd696"), new("#b8964a") };

    private static readonly Color StrawShadow = new(0.35f, 0.27f, 0.16f, 0.55f), StrawBed = new(0.84f, 0.73f, 0.43f, 0.55f);

    /// <summary>
    /// ⭐ Pure: the straw strewn before a sick home's door, RGBA8, <see cref="StrawPixels"/> square — a soft bed of it, and short
    /// stalks scattered over it, thickest by the wall. Its top edge is the wall; it is laid outward from there.
    /// </summary>
    internal static byte[] PaintTheStraw()
    {
        const int n = StrawPixels;
        var rgba = new byte[n * n * 4];

        // The bed: a soft wash, wider along the wall than out from it.
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - (n / 2f)) / (n * 0.42f), dy = (y + 0.5f - (n * 0.36f)) / (n * 0.32f);
                float a = Mathf.Clamp(1f - Mathf.SmoothStep(0.55f, 1f, Mathf.Sqrt((dx * dx) + (dy * dy))), 0f, 1f) * StrawBed.A;
                Over(rgba, n, x, y, StrawBed, a);
            }
        }

        // The stalks: short strokes at every angle, the odd one with the pen's shadow under it.
        for (int i = 0; i < 170; i++)
        {
            float u = (Hash01(i, 1, 6151) + Hash01(i, 2, 6151) + Hash01(i, 3, 6151) - 1.5f) * 0.62f;
            float v = Mathf.Abs(Hash01(i, 4, 6151) + Hash01(i, 5, 6151) - 1f) * 0.7f;
            var from = new Vector2((0.5f + u) * n, (0.06f + v) * n);
            float angle = Hash01(i, 6, 6151) * Mathf.Pi;
            float length = (0.07f + (0.06f * Hash01(i, 7, 6151))) * n;
            Vector2 to = from + (new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length);
            if (i % 5 == 0)
            {
                Stroke(rgba, n, n, from + new Vector2(0.8f, 1f), to + new Vector2(0.8f, 1f), 1.3f, StrawShadow);
            }

            Stroke(rgba, n, n, from, to, 1.15f, StrawGolds[i % StrawGolds.Length]);
        }

        return rgba;
    }

    /// <summary>The sprig's colours: three leaf greens, the stem, the tie, and the pen.</summary>
    private static readonly Color[] SprigLeaves = { new("#709652"), new("#84a860"), new("#608646") };

    private static readonly Color SprigStem = new("#46603a"), SprigTie = new("#963c30"), SprigInk = new("#2a3220");

    /// <summary>
    /// ⭐ Pure: a sprig of herbs hung by a sick home's door, RGBA8, <see cref="SprigWide"/> × <see cref="SprigTall"/> — a bundle
    /// tied at the top, its stems and leaves hanging down.
    /// </summary>
    internal static byte[] PaintTheSprig()
    {
        const int w = SprigWide, h = SprigTall;
        var rgba = new byte[w * h * 4];
        var tie = new Vector2(w / 2f, h * 0.1f);
        for (int i = 0; i < 13; i++)
        {
            float angle = Mathf.DegToRad(90f + ((Hash01(i, 1, 7177) - 0.5f) * 60f));
            float length = (0.45f + (0.35f * Hash01(i, 2, 7177))) * h;
            Vector2 tip = tie + (new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length);
            Stroke(rgba, w, h, tie, tip, 1.1f, SprigStem);
            for (int k = 1; k <= 3; k++)
            {
                Vector2 at = tie.Lerp(tip, k / 3f);
                float r = (2.2f + (1.2f * Hash01(i, 10 + k, 7177))) * (0.75f + (0.25f * k / 3f));
                Blot(rgba, w, h, at, r + 0.9f, SprigInk);
                Blot(rgba, w, h, at, r, SprigLeaves[(i + k) % SprigLeaves.Length]);
            }
        }

        Blot(rgba, w, h, tie, 3.6f, SprigInk);
        Blot(rgba, w, h, tie, 2.8f, SprigTie);
        return rgba;
    }

    /// <summary>A stroke of this width from one point to another, its edge soft by a pixel, laid over what is there.</summary>
    private static void Stroke(byte[] rgba, int w, int h, Vector2 from, Vector2 to, float width, Color colour)
    {
        int x0 = Mathf.Max(0, (int)(Mathf.Min(from.X, to.X) - width - 1)), x1 = Mathf.Min(w - 1, (int)(Mathf.Max(from.X, to.X) + width + 1));
        int y0 = Mathf.Max(0, (int)(Mathf.Min(from.Y, to.Y) - width - 1)), y1 = Mathf.Min(h - 1, (int)(Mathf.Max(from.Y, to.Y) + width + 1));
        Vector2 run = to - from;
        float lengthSquared = Mathf.Max(run.LengthSquared(), 0.0001f);
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp((p - from).Dot(run) / lengthSquared, 0f, 1f);
                float d = p.DistanceTo(from + (run * t));
                Over(rgba, w, x, y, colour, Mathf.Clamp((width / 2f) + 0.5f - d, 0f, 1f) * colour.A);
            }
        }
    }

    /// <summary>A round blot of this radius, its edge soft by a pixel, laid over what is there.</summary>
    private static void Blot(byte[] rgba, int w, int h, Vector2 at, float radius, Color colour)
    {
        for (int y = Mathf.Max(0, (int)(at.Y - radius - 1)); y <= Mathf.Min(h - 1, (int)(at.Y + radius + 1)); y++)
        {
            for (int x = Mathf.Max(0, (int)(at.X - radius - 1)); x <= Mathf.Min(w - 1, (int)(at.X + radius + 1)); x++)
            {
                float d = new Vector2(x + 0.5f, y + 0.5f).DistanceTo(at);
                Over(rgba, w, x, y, colour, Mathf.Clamp(radius + 0.5f - d, 0f, 1f) * colour.A);
            }
        }
    }

    /// <summary>One pixel of colour laid over another at this cover (straight alpha).</summary>
    private static void Over(byte[] rgba, int w, int x, int y, Color colour, float cover)
    {
        if (cover <= 0f)
        {
            return;
        }

        int at = ((y * w) + x) * 4;
        float under = rgba[at + 3] / 255f;
        float a = cover + (under * (1f - cover));
        for (int c = 0; c < 3; c++)
        {
            float top = c == 0 ? colour.R : c == 1 ? colour.G : colour.B;
            float below = rgba[at + c] / 255f;
            rgba[at + c] = (byte)Mathf.RoundToInt(((top * cover) + (below * under * (1f - cover))) / a * 255f);
        }

        rgba[at + 3] = (byte)Mathf.RoundToInt(a * 255f);
    }

    /// <remarks>The probe's <c>sick on the map:</c> line (A321, <c>sickbed-art.md §9</c>) — the expectations named from the spec.</remarks>
    internal static string TheSickOnTheMapHolds()
    {
        try
        {
            var faults = new List<string>();
            void Want(bool ok, string what)
            {
                if (!ok)
                {
                    faults.Add(what);
                }
            }

            // In bed at home is indoors (the bug A321 found: drawn standing at the door); in bed with no roof is seen.
            Want(AsksForAHouse(VillagerState.SickInBed) && AsksForAHouse(VillagerState.Resting) && !AsksForAHouse(VillagerState.Cutting),
                "lying sick does not ask whether there is a house");
            Want(!ShownAsAPerson(VillagerState.SickInBed, housed: AsksForAHouse(VillagerState.SickInBed)), "somebody in bed at home is drawn outside it");
            Want(ShownAsAPerson(VillagerState.SickInBed, housed: false), "somebody in bed with no roof is not drawn");

            // Where the face goes.
            Want(SickMarkOf(VillagerState.SickInBed, 4, housed: true) == SickMarkAt.OverTheHouse, "in bed at home: the face is not over the house");
            Want(SickMarkOf(VillagerState.SickInBed, 4, housed: false) == SickMarkAt.OverTheHead, "in bed with no roof: the face is not over the head");
            Want(SickMarkOf(VillagerState.TravelingHome, 5, housed: false) == SickMarkAt.OverTheHead, "ill and walking home: the face is not over the head");
            Want(SickMarkOf(VillagerState.Cutting, 0, housed: false) == SickMarkAt.None && SickMarkOf(VillagerState.Resting, 0, housed: true) == SickMarkAt.None,
                "a well person has a face");
            Want(UiIcons.Find("health", "sick") is Texture2D, "Joe's sick face is not there to draw (icons/health/sick.png)");
            Want(SickFacePixels >= 24f && SickFacePixels <= 40f, $"the face is {SickFacePixels} px (want it to read: 24–40)");

            // The straw and the sprig: painted, gold and green, thickest by the wall; no smoke of its own (call 2).
            byte[] straw = PaintTheStraw();
            int strawCover = 0, nearWall = 0, farOut = 0;
            float gold = 0f;
            for (int i = 0; i < straw.Length; i += 4)
            {
                if (straw[i + 3] < 128)
                {
                    continue;
                }

                strawCover++;
                gold += (straw[i] + straw[i + 1] - (2 * straw[i + 2])) / 255f;
                int y = i / 4 / StrawPixels;
                nearWall += y < StrawPixels / 2 ? 1 : 0;
                farOut += y >= StrawPixels / 2 ? 1 : 0;
            }

            float share = strawCover / (float)(StrawPixels * StrawPixels);
            Want(share > 0.2f && share < 0.75f, $"the straw covers {share:P0} of its painting (want a bed, not a block: 20–75 %)");
            Want(strawCover > 0 && gold / strawCover > 0.3f, "the straw is not gold");
            Want(nearWall > farOut, $"the straw is not thickest by the wall ({nearWall} by it, {farOut} out)");
            byte[] sprig = PaintTheSprig();
            int green = 0, red = 0;
            for (int i = 0; i < sprig.Length; i += 4)
            {
                if (sprig[i + 3] < 200)
                {
                    continue;
                }

                green += sprig[i + 1] > sprig[i] + 20 && sprig[i + 1] > sprig[i + 2] + 20 ? 1 : 0;
                red += sprig[i] > sprig[i + 1] + 50 ? 1 : 0;
            }

            Want(green > 200 && red > 5, $"the sprig is not green leaves tied in red ({green} green, {red} red)");
            Want(!HearthBurns(Season.Summer, occupied: true, firewood: 5), "a home smokes out of winter, sick or not (call 2: no smoke of its own)");

            return faults.Count == 0
                ? $"[widths] sick on the map: ✅ in bed at home indoors (not at the door), in bed with no roof seen; the face over the house in bed at home, over the head ill out of doors, none when well, {SickFacePixels} px at every zoom; the straw a gold bed thickest by the wall ({share:P0}), the sprig green tied in red; no smoke of its own"
                : $"[widths] sick on the map: ⛔ {string.Join("; ", faults)}";
        }
        catch (Exception e)
        {
            return $"[widths] sick on the map: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}
