using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐⭐ W3'S BREWERY AND TAVERN, DRAWN AS REAL BUILDINGS (A199, <c>specs/tavern-and-brewery-art.md</c>; Joe: <i>"art: tavern and
/// brewery real buildings"</i>, then <i>"All as recommended"</i> on the six calls). Both are A86's yards (<c>building-yards.md</c>):
/// the brewery in the bakery's layout, its copper the signature, opened while it brews (§3.1); the tavern in the library's,
/// its sign, a trestle and benches and its ale by the door (§3.2). Two things move with the sim and are drawn each frame over
/// the cached roof, carried as a level on its <see cref="RoofCache"/> as the smoke is: <b>lamplight</b> out of the tavern's
/// windows and door while a drinker sits inside (call 3), and <b>steam</b> off the open copper while a batch is brewed (call 2).
/// View only; nothing here is hashed.
/// </remarks>
public partial class VillageMap
{
    private static readonly Color Copper = new("#b8733a");
    private static readonly Color CopperLit = new("#e3a874");
    private static readonly Color Wort = new("#8a4f12");
    private static readonly Color Froth = new("#c28a3c");
    private static readonly Color Brick = new("#a2614a");
    private static readonly Color BrickJoint = new("#5a3428");
    private static readonly Color BarleySack = new("#ddd3b4");
    private static readonly Color SignBoard = new("#8a2f3e");
    private static readonly Color SignGold = new("#d9b04a");
    private static readonly Color CupAle = new("#9a5e14");

    /// <summary>The lamplight's warm colour, and how strong it is at the wall (0..1 alpha), fully lit.</summary>
    internal static readonly Color LampLight = new("#ffc552");

    internal const float LampAtTheWall = 0.8f;

    /// <summary>A window's lit slit, and how far its light falls onto the ground, in tiles.</summary>
    internal static readonly Color LampWindow = new("#ffd37a");

    internal const float LampReach = 0.45f, LampSideReach = 0.3f;

    /// <summary>The steam's wash: near white, a pale grey rim — never the smoke's grey (call 2).</summary>
    internal static readonly Color SteamWarm = new("#f6f3ec"), SteamCool = new("#eceff2"), SteamEdge = new("#cdc8c0");

    /// <summary>Puffs of steam over the copper at once, a puff's life in the people's seconds.</summary>
    internal const int SteamPuffs = 5;

    internal const float SteamSeconds = 2.6f;

    /// <summary>How high steam rises in its life, how far it drifts, and how big it grows, in tiles.</summary>
    private const float SteamRise = 0.6f, SteamDrift = 0.18f, SteamFrom = 0.08f, SteamTo = 0.32f;

    /// <summary>The taverns (workplace ids) with somebody sat inside this tick — kept once a tick where the fires are noted.</summary>
    private readonly HashSet<int> _tavernsLit = new();

    /// <summary>The breweries (workplace ids) with a batch being brewed this tick.</summary>
    private readonly HashSet<int> _coppersBrewing = new();

    private Texture2D[]? _steamPuffs;

    /// <summary>
    /// Once a tick, per villager (from <see cref="NoteTheFires"/>): a drinker sat in a tavern lights it — ⛔ <b>not the keeper</b>,
    /// who is inside the whole working day (call 3) — and a brewer at a batch has the copper steaming.
    /// </summary>
    private void NoteTheTavernAndTheCopper(SimWorld world, Villager villager)
    {
        if (LightsTheTavern(villager.State) && world.TavernAt(new GridPos(villager.ErrandX, villager.ErrandY)) is Workplace tavern)
        {
            _tavernsLit.Add(tavern.Id);
        }
        else if (villager.State == VillagerState.WorkingABatch && world.FindWorkplace(villager.WorkplaceId)?.Kind is JobKind kind && SteamsTheCopper(kind))
        {
            _coppersBrewing.Add(villager.WorkplaceId);
        }
    }

    /// <summary>⭐ Pure (call 3): who lights the tavern — a drinker sat inside, never the keeper at work nor anyone on the way.</summary>
    internal static bool LightsTheTavern(VillagerState state) => state == VillagerState.AtTheTavern;

    /// <summary>Pure (call 2): whose batch steams — the brewer's copper; the oven and the mill have their own looks.</summary>
    internal static bool SteamsTheCopper(JobKind kind) => kind == JobKind.Brewer;

    /// <summary>Pure (call 5): the workplaces with a stove that burns in winter while staffed and stocked — the school, and the tavern (the sim's `HearthSystem`).</summary>
    internal static bool HasAStove(JobKind kind) => kind is JobKind.Teacher or JobKind.TavernKeeper;

    /// <summary>Whether a piece is lamplight, drawn each frame at its level rather than laid into the roof's runs.</summary>
    internal static bool IsLamplight(RoofPiece piece) => piece.Part.StartsWith("lamplight", StringComparison.Ordinal);

    /// <summary>The copper's middle among a building's pieces, where its steam rises from; null if it has none.</summary>
    internal static Vector2? CopperOf(List<RoofPiece> pieces) =>
        pieces.FirstOrDefault(p => p.Kind == RoofPieceKind.Fill && p.Part == "copper") is RoofPiece copper ? CentreOf(copper.Points) : null;

    // ------------------------------------------------------------------ the yards (§3.1, §3.2)

    /// <summary>
    /// ⭐ The brewery's yard (§3.1), in the bakery's layout: the copper, the mash tun and the racked casks always; while brewing,
    /// the copper open — the wort in it and a paddle across — and barley sacks by the tun (a batch keeps no stock, so its piles
    /// show while working, §2).
    /// </summary>
    private static void BreweryYard(YardKit k, YardLayout l, YardState s, int seed)
    {
        float t = l.Turn;
        YardGround(k, l.YardAt, l.YardLong, l.YardShort, t, seed);
        Vector2 copper = l.P(0f, -0.52f);
        CopperVessel(k, copper, 0.15f, s.Working);
        MashTun(k, l.P(-0.42f, 0.14f), 0.085f);
        Stillage(k, l.P(0f, 0.72f), t, 3);
        if (s.Working)
        {
            Sacks(k, "barley", l.P(0.55f, 0.05f), t - (Mathf.Pi / 2f), 2, seed, BarleySack);
        }
    }

    /// <summary>
    /// ⭐ The tavern's yard (§3.2), in the library's layout — the front strip, the door in it: the sign on its arm beside the
    /// door, a trestle and its benches on the roomier side, the step; casks of ale by the door by the level of its own store
    /// (the stock rule); and the lamplight's pieces (windows front and sides, the door), drawn each frame at its level.
    /// </summary>
    private static void TavernYard(YardKit k, YardLayout l, YardState s, int seed)
    {
        float t = l.Turn;
        YardGround(k, l.YardAt, l.YardLong, l.YardShort, t, seed);
        float door = l.DoorAt;
        float roomy = door > 0.05f ? -1f : door < -0.05f ? 1f : (seed & 2) == 0 ? 1f : -1f;
        Trestle(k, l.P(door + (roomy * 0.55f), -0.15f), t);
        for (int i = 0; i < s.Stock; i++)
        {
            CaskLying(k, "ale", l.P(door - (roomy * (0.24f + (0.17f * (i % 2)))), 0.45f - (0.42f * (i / 2))), t, 0.17f, 0.055f);
        }

        // The door's way, out of the building into the yard (P's "across" points back at the building).
        Vector2 way = -new Vector2(-l.YardAlong.Y, l.YardAlong.X);
        HangingSign(k, l.P(door + (roomy * 0.12f), 0.92f), l.YardAlong * roomy, way);

        // The light: in the roof's frame, so it falls from the walls whichever way the tavern is turned.
        Vector2 across = l.YardAlong;
        float front = (l.RoofAt.Dot(way)) + (Mathf.Abs(way.X) > 0.5f ? l.RoofHw : l.RoofHd) + RoofEaves;
        float half = (Mathf.Abs(way.X) > 0.5f ? l.RoofHd : l.RoofHw) + RoofEaves;
        float depthHalf = (Mathf.Abs(way.X) > 0.5f ? l.RoofHw : l.RoofHd) + RoofEaves;
        float doorAcross = door * l.YardLong;
        Vector2 roofMiddle = l.RoofAt;
        void Window(string part, Vector2 a, Vector2 b, Vector2 outward, float reach)
        {
            Vector2 along2 = (b - a).Normalized();
            k.Pieces.Add(new RoofPiece(RoofPieceKind.Fill, part, k.On(new[]
            {
                a, b, b + (outward * reach) + (along2 * reach * 0.5f), a + (outward * reach) - (along2 * reach * 0.5f),
            }), LampLight));
            k.Pieces.Add(new RoofPiece(RoofPieceKind.Line, part + " window", k.On(new[] { a.Lerp(b, 0.3f), a.Lerp(b, 0.7f) }), LampWindow));
        }

        // Two front windows, one each side of the door, and the open door.
        foreach (float side in new[] { -1f, 1f })
        {
            float middle = Mathf.Clamp(doorAcross + (side * 0.45f), -half + 0.2f, half - 0.2f);
            if (Mathf.Abs(middle - doorAcross) < 0.2f)
            {
                middle = doorAcross + (side * 0.2f);
            }

            Vector2 at = (way * front) + (across * middle);
            Window("lamplight front", at - (across * 0.15f), at + (across * 0.15f), way, LampReach);
        }

        Vector2 doorAt = (way * front) + (across * doorAcross);
        Window("lamplight door", doorAt - (across * 0.07f), doorAt + (across * 0.07f), way, LampReach * 0.7f);

        // A window in each side wall, at the middle of the roof's depth.
        foreach (float side in new[] { -1f, 1f })
        {
            Vector2 at = roofMiddle + (across * side * half);
            Window("lamplight side", at - (way * depthHalf * 0.35f), at + (way * depthHalf * 0.35f), across * side, LampSideReach);
        }

    }

    // ------------------------------------------------------------------ the things

    /// <summary>The copper from above: a ring of brick round a round copper vessel — shut under a wooden lid, or open, the wort in it and a paddle across.</summary>
    private static void CopperVessel(YardKit k, Vector2 at, float r, bool open)
    {
        k.Shade("copper shadow", Oval(at, r * 1.25f, r * 1.25f, 0f, 16), 0.45f);
        k.Thing("copper setting", Oval(at, r * 1.25f, r * 1.25f, 0f, 20), Brick, 0.9f);
        var joints = new List<Vector2>();
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.Tau / 10f;
            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            joints.Add(at + (d * r * 1.02f));
            joints.Add(at + (d * r * 1.23f));
        }

        k.Strokes("copper setting joints", joints, BrickJoint with { A = 0.6f });
        k.Thing("copper", Oval(at, r, r, 0f, 20), Copper, 0.9f);
        k.Fill("copper shine", new[] { at + new Vector2(-r * 0.85f, -r * 0.1f), at + new Vector2(-r * 0.2f, -r * 0.86f), at + new Vector2(-r * 0.55f, -r * 0.62f) }, CopperLit with { A = 0.75f });
        if (open)
        {
            k.Thing("wort", Oval(at, r * 0.78f, r * 0.78f, 0f, 18), Wort, 0.7f);
            k.Fill("wort froth", Oval(at + new Vector2(-r * 0.25f, -r * 0.2f), r * 0.18f, r * 0.18f, 0f, 8), Froth with { A = 0.6f });
            k.Fill("wort froth", Oval(at + new Vector2(r * 0.2f, r * 0.1f), r * 0.12f, r * 0.12f, 0f, 8), Froth with { A = 0.5f });
            k.Line("paddle", at + new Vector2(-r * 0.3f, r * 0.75f), at + new Vector2(r * 0.45f, -r * 1.1f), TimberDark, 2.2f);
            k.Thing("paddle", Box(at + new Vector2(r * 0.5f, -r * 1.2f), 0.04f, 0.08f, -0.4f), Timber, 0.8f);
        }
        else
        {
            k.Thing("copper lid", Oval(at, r * 0.8f, r * 0.8f, 0f, 18), TimberLight, 0.85f);
            var boards = new List<Vector2>();
            foreach (float x in new[] { -0.35f, 0f, 0.35f })
            {
                float h = r * 0.78f * Mathf.Sqrt(1f - (x * x));
                boards.Add(at + new Vector2(r * x, -h));
                boards.Add(at + new Vector2(r * x, h));
            }

            k.Strokes("copper lid", boards, TimberDark with { A = 0.6f });
            k.Fill("copper lid", Box(at, r * 0.5f, r * 0.12f, 0f), TimberDark);
        }
    }

    /// <summary>The mash tun: a wide open tub of staves with an iron hoop.</summary>
    private static void MashTun(YardKit k, Vector2 at, float r)
    {
        k.Shade("mash tun shadow", Oval(at, r, r, 0f, 14), 0.35f);
        k.Thing("mash tun", Oval(at, r, r, 0f, 18), Timber, 0.9f);
        k.Pieces.Add(new RoofPiece(RoofPieceKind.Line, "mash tun hoop", k.On(Oval(at, r * 0.86f, r * 0.86f, 0f, 16)), YardIron with { A = 0.8f }, Closed: true));
        k.Fill("mash tun", Oval(at, r * 0.72f, r * 0.72f, 0f, 14), new Color("#6a4a2c"));
    }

    /// <summary>A cask on its side from above: a bellied body of staves and two iron hoops.</summary>
    private static void CaskLying(YardKit k, string part, Vector2 at, float turn, float length, float r)
    {
        float h = length / 2f;
        k.Thing(part, new[]
        {
            Tn(at, -h, -r * 0.8f, turn), Tn(at, -h / 2f, -r, turn), Tn(at, h / 2f, -r, turn), Tn(at, h, -r * 0.8f, turn),
            Tn(at, h, r * 0.8f, turn), Tn(at, h / 2f, r, turn), Tn(at, -h / 2f, r, turn), Tn(at, -h, r * 0.8f, turn),
        }, Timber, 0.9f);
        k.Strokes(part + " hoop", new List<Vector2>
        {
            Tn(at, -length * 0.3f, -r * 0.95f, turn), Tn(at, -length * 0.3f, r * 0.95f, turn),
            Tn(at, length * 0.3f, -r * 0.95f, turn), Tn(at, length * 0.3f, r * 0.95f, turn),
        }, YardIron with { A = 0.85f }, width: 1.2f);
    }

    /// <summary>Empty casks racked on a timber stillage — the brewery's dressing.</summary>
    private static void Stillage(YardKit k, Vector2 at, float turn, int casks)
    {
        float half = 0.04f + (0.055f * casks);
        foreach (float y in new[] { -0.075f, 0.075f })
        {
            k.Line("stillage", Tn(at, -half, y, turn), Tn(at, half, y, turn), TimberDark, 1.6f);
        }

        for (int i = 0; i < casks; i++)
        {
            CaskLying(k, "cask", Tn(at, (i - ((casks - 1) / 2f)) * 0.1f, 0f, turn), turn + (Mathf.Pi / 2f), 0.19f, 0.045f);
        }
    }

    /// <summary>An outside table with a bench either side, a cup of ale left on it.</summary>
    private static void Trestle(YardKit k, Vector2 at, float turn)
    {
        k.Shade("trestle shadow", Box(at, 0.5f, 0.36f, turn), 0.25f);
        k.Thing("trestle", Box(at, 0.46f, 0.15f, turn), TimberLight, 0.85f);
        k.Strokes("trestle", new List<Vector2>
        {
            Tn(at, -0.21f, -0.035f, turn), Tn(at, 0.21f, -0.035f, turn), Tn(at, -0.21f, 0.035f, turn), Tn(at, 0.21f, 0.035f, turn),
        }, TimberDark with { A = 0.5f });
        foreach (float y in new[] { -0.15f, 0.15f })
        {
            k.Thing("trestle bench", Box(Tn(at, 0f, y, turn), 0.4f, 0.065f, turn), TimberLight, 0.8f);
        }

        k.Thing("cup", Oval(Tn(at, -0.1f, 0f, turn), 0.022f, 0.022f, 0f, 8), CupAle, 0.7f);
        k.Thing("cup", Oval(Tn(at, 0.12f, 0.02f, turn), 0.022f, 0.022f, 0f, 8), CupAle, 0.7f);
    }

    /// <summary>The sign: an iron arm out from the front wall beside the door, the board hung from it in the keeper's red, a gold tankard on it.</summary>
    private static void HangingSign(YardKit k, Vector2 wall, Vector2 across, Vector2 outward)
    {
        float turn = across.Angle();
        k.Line("sign arm", wall, wall + (across * 0.26f) + (outward * 0.02f), YardIron, 1.6f);
        k.Line("sign arm", wall - (outward * 0.04f), wall + (across * 0.11f), YardIron with { A = 0.9f }, 0.8f);
        Vector2 board = wall + (across * 0.17f) + (outward * 0.09f);
        k.Shade("sign shadow", Box(board, 0.2f, 0.12f, turn), 0.5f);
        k.Thing("sign", Box(board, 0.2f, 0.12f, turn), SignBoard, 0.95f);
        k.Thing("sign tankard", Box(Tn(board, -0.01f, 0.004f, turn), 0.06f, 0.07f, turn), SignGold, 0.6f);
        k.Fill("sign tankard", Box(Tn(board, 0.035f, 0.004f, turn), 0.025f, 0.035f, turn), SignGold);
    }

    // ------------------------------------------------------------------ drawn each frame: the light and the steam

    /// <summary>
    /// A building's lamplight at its level (call 3): <paramref name="fans"/> of the fans of light falling from the walls — the tavern's by
    /// day, none by night, when the light is the soft pools' (A249, <see cref="DrawTheGlow"/>) — and <paramref name="slits"/> of the lit windows.
    /// </summary>
    private void DrawTheLamplight(RoofCache held, float fans, float slits, float alpha)
    {
        float a = held.Light * alpha;
        if (a <= 0f)
        {
            return;
        }

        float width = Mathf.Max(2f, 0.055f * _pixelsPerTile);
        foreach (RoofPiece lamp in held.Lamps)
        {
            if (lamp.Kind == RoofPieceKind.Fill && lamp.Points.Length == 4 && fans > 0f)
            {
                var points = new Vector2[4];
                for (int i = 0; i < 4; i++)
                {
                    points[i] = ToScreen(lamp.Points[i]);
                }

                Color wall = Lifted(LampLight, 1f) with { A = LampAtTheWall * a * fans };
                Color far = Lifted(LampLight, 1f) with { A = 0f };
                DrawPolygon(points, new[] { wall, wall, far, far });
            }
            else if (lamp.Kind == RoofPieceKind.Line && lamp.Points.Length == 2 && slits > 0f)
            {
                DrawLine(ToScreen(lamp.Points[0]), ToScreen(lamp.Points[1]), Lifted(LampWindow, 1f) with { A = a * slits }, width);
            }
        }
    }

    /// <summary>
    /// ⭐ Pure: a puff of steam at this moment — where it is (tiles from the copper), its size, how much shows (call 2): smaller,
    /// quicker and lower than a chimney's smoke.
    /// </summary>
    internal static (Vector2 Offset, float Radius, float Show) SteamAt(int puff, float phase, double clock)
    {
        float u = (float)(((clock / SteamSeconds) + ((float)puff / SteamPuffs) + phase) % 1.0);
        float sway = 0.04f * u * Mathf.Sin((u * 7f) - ((float)clock * 2f) + (phase * Mathf.Tau));
        var offset = new Vector2((SteamDrift * u * u) + sway, -SteamRise * u);
        float show = Mathf.Min(1f, u / 0.12f) * Mathf.Pow(1f - u, 1.5f);
        return (offset, SteamFrom + ((SteamTo - SteamFrom) * u), show);
    }

    /// <summary>The steam off one brewery's open copper, its level carried on the roof's cache.</summary>
    private void DrawTheSteam(RoofCache held, bool brewing, float alpha)
    {
        held.Steam = SmokeOn(held.Steam, brewing, _peopleFrameSeconds);
        if (held.Steam <= 0f || held.Copper is not Vector2 copper)
        {
            return;
        }

        Texture2D[] puffs = TheSteamPuffs();
        float phase = Hash01(held.Signature.Seed, 3, 7919);
        for (int i = 0; i < SteamPuffs; i++)
        {
            (Vector2 offset, float radius, float show) = SteamAt(i, phase, _peopleClock);
            float a = Mathf.Min(1f, show * held.Steam * alpha * 1.3f);
            if (a <= 0.004f)
            {
                continue;
            }

            Vector2 at = ToScreen(copper + offset);
            var size = new Vector2(2f * radius, 1.6f * radius) * _pixelsPerTile;
            DrawTextureRect(puffs[i % PuffPaintings], new Rect2(at - (size / 2f), size), tile: false, modulate: new Color(1f, 1f, 1f, a));
        }
    }

    /// <summary>The steam's paintings: the smoke's own puff, painted near white (each painted once).</summary>
    private Texture2D[] TheSteamPuffs()
    {
        if (_steamPuffs is not null)
        {
            return _steamPuffs;
        }

        _steamPuffs = new Texture2D[PuffPaintings];
        for (int v = 0; v < PuffPaintings; v++)
        {
            Image image = Image.CreateFromData(PuffPixels, PuffPixels, false, Image.Format.Rgba8, PaintAPuff(v, SteamWarm, SteamCool, SteamEdge));
            image.GenerateMipmaps();
            _steamPuffs[v] = ImageTexture.CreateFromImage(image);
        }

        return _steamPuffs;
    }
}
