using Godot;
using Kith.Sim.Core;
using Kith.Sim.World;

namespace Kith.Game;

/// <summary>
/// ⭐ W4's view (`specs/cemetery.md §5.2–§6.3`, step 7, D912) — a cemetery drawn as fenced, mown ground with its graves, not as a roofed
/// building; and the dead who wait for a grave, small and plain under a cloth. ⏳ Stand-ins drawn in code until the art lane's pictures
/// land (§11, `Main.UiIconsProbe.ArtAwaited`). ⚠️ Cozy, never grim: Joe's eyes before it ships.
/// </summary>
public partial class VillageMap
{
    private static readonly Color CemeteryGrass = new("#a7b682");
    private static readonly Color CemeteryRough = new("#8fa069");
    private static readonly Color CemeteryWild = new("#748a55");
    private static readonly Color CemeteryTuft = new("#5f7444");
    private static readonly Color Headstone = new("#c9c4b8");
    private static readonly Color FreshEarth = new("#a98d68");
    private static readonly Color Shroud = new("#ece4d0");

    // ⭐ The art lane's pictures (A287, `cemetery-art.md`; Joe: "All as recommended" — the hedge, three stone shapes, plain linen).
    private static readonly Color Hedge = new("#4e683a"), HedgeLight = new("#5f7a44"), HedgeDark = new("#44592f");
    private static readonly Color GraveGrass = new("#8ea468"), GraveGrassLight = new("#9fb878"), FreshEarthLight = new("#b2926c");
    private static readonly Color StoneShade = new("#96928a"), WornLine = new("#b0a078"), LychRoof = new("#8a6042"),
        LychRoofShade = new("#70503a"), LychPost = new("#6e5034"), ShroudFold = new("#c8bea8");

    /// <summary>The lychgate's opening, tiles; the hedge is open this wide on the front edge.</summary>
    internal const float LychgateWide = 0.7f;

    /// <summary>A grave's stone by its place (§2): a round-topped tablet, a cross, a rough slab — so a row is not a stamp.</summary>
    internal static int StoneShapeOf(int place) => ((place * 7) + 3) % 3;

    /// <summary>A cemetery's grave pattern, kept between frames: laid again only when somebody is buried or a year turns (CLAUDE.md).</summary>
    private readonly Dictionary<int, (long Key, string Graves)> _gravePatterns = new();

    /// <summary>
    /// A cemetery's yard: its wear (0 sound … 3 wild) and one letter a place — <c>.</c> free, <c>f</c> fresh (buried this year),
    /// <c>s</c> a stone — in the sim's own order (row by row from the gate, `cemetery.md §5.2`).
    /// </summary>
    private YardState CemeteryYardOf(SimWorld world, Workplace cemetery)
    {
        int year = world.Clock.Year;
        long key = ((long)world.GraveGeneration << 20) ^ year;
        if (!_gravePatterns.TryGetValue(cemetery.Id, out (long Key, string Graves) held) || held.Key != key)
        {
            IReadOnlyList<int> places = world.GravesLaidIn(cemetery);
            ulong aYear = (ulong)world.Config.TicksPerYear;
            var letters = new char[places.Count];
            for (int i = 0; i < places.Count; i++)
            {
                letters[i] = places[i] == 0 ? '.'
                    : world.FindVillager(places[i]) is { BuriedAtTick: ulong at } && world.Tick - at < aYear ? 'f' : 's';
            }

            held = (key, new string(letters));
            _gravePatterns[cemetery.Id] = held;
        }

        byte wear = world.ConditionOf(cemetery) switch
        {
            Condition.Sound => 0,
            Condition.Worn => 1,
            Condition.Failing => 2,
            _ => 3,
        };

        return new YardState(Stock: wear, Graves: held.Graves, GravesWide: (byte)Math.Clamp(cemetery.ExtentWidth, 1, 255));
    }

    /// <summary>
    /// The cemetery's pieces, in its own frame (A287, `cemetery-art.md §2`, as Joe picked it): mown ground (W4's colours as it wears,
    /// and its tufts), a worn line from the gate; the graves — a long low mound, fresh earth its first year and grassed after, with its
    /// stone standing up at its head as the people do; a low hedge round it, open at the front for the lychgate.
    /// </summary>
    private static void Cemetery(List<RoofPiece> pieces, RoofFrame frame, float hw, float hd, YardState yard, int seed)
    {
        var kit = new YardKit(pieces, frame);
        int wear = yard.Stock;
        Color ground = wear switch { 0 => CemeteryGrass, 1 => CemeteryRough, _ => CemeteryWild };
        kit.Fill("ground", Box(Vector2.Zero, 2f * hw, 2f * hd, 0f), ground);

        // Mown in stripes while it is kept; the worn line from the gate up the middle, where the mourners walk.
        if (wear == 0)
        {
            for (float x = -hw + 0.1f; x < hw; x += 0.42f)
            {
                kit.Fill("mown", Box(new Vector2(x, 0f), 0.18f, 2f * hd, 0f), Colors.White with { A = 0.05f });
            }
        }

        kit.Fill("worn line", Box(new Vector2(0f, hd * 0.08f), 0.12f, 2f * hd * 0.84f, 0f), WornLine with { A = 0.45f });

        // Neglect shows as tufts, more the longer it goes untended — no grave is ever lost under them.
        uint h = (uint)seed * 2654435761u;
        for (int i = 0; i < wear * 7; i++)
        {
            h = (h * 1103515245u) + 12345u;
            float x = (((h >> 8) & 1023) / 1023f * 1.8f - 0.9f) * hw;
            h = (h * 1103515245u) + 12345u;
            float y = (((h >> 8) & 1023) / 1023f * 1.8f - 0.9f) * hd;
            kit.Fill("tuft", Box(new Vector2(x, y), 0.14f, 0.09f, 0.4f), CemeteryTuft with { A = 0.8f });
        }

        // The graves, the back row first so a nearer stone stands in front of a further one; the worn line left clear.
        string graves = yard.Graves ?? string.Empty;
        int wide = Math.Max(1, (int)yard.GravesWide);
        int rows = Math.Max(1, (graves.Length + wide - 1) / wide);
        float cw = 2f * hw / wide;
        float ch = 2f * hd / rows;
        for (int row = rows - 1; row >= 0; row--)
        {
            for (int col = 0; col < wide; col++)
            {
                int k = (row * wide) + col;
                if (k >= graves.Length || graves[k] == '.')
                {
                    continue;
                }

                float aside = wide > 2 ? (col < wide / 2 ? -0.07f : 0.07f) * cw : 0f;
                var head = new Vector2(-hw + ((col + 0.5f) * cw) + aside, hd - ((row + 0.5f) * ch) - (ch * 0.3f));
                Grave(kit, head, graves[k] == 'f', StoneShapeOf(k));
            }
        }

        HedgeRound(kit, hw, hd, seed);
        Lychgate(kit, new Vector2(0f, hd));
    }

    /// <summary>One grave: the mound before the stone (in the cemetery's frame), the stone standing up on screen at its head.</summary>
    private static void Grave(YardKit kit, Vector2 head, bool fresh, int shape)
    {
        Vector2 middle = head + new Vector2(0f, 0.36f);
        Vector2[] mound = Oval(middle, 0.135f, 0.32f, 0f, 16);
        kit.Shade("grave shadow", mound, 0.3f);
        if (fresh)
        {
            kit.Thing("mound", mound, FreshEarth, 0.5f);
            kit.Fill("mound light", Oval(middle + new Vector2(-0.04f, -0.03f), 0.07f, 0.2f, 0f, 12), FreshEarthLight);
        }
        else
        {
            kit.Thing("grassed mound", mound, GraveGrass, 0.18f);
            kit.Fill("mound light", Oval(middle + new Vector2(-0.04f, -0.03f), 0.07f, 0.2f, 0f, 12), GraveGrassLight);
        }

        // The stone: built in screen directions (tiles; up is negative), so it stands up whichever way the cemetery faces.
        Vector2[] Up(params Vector2[] screen)
        {
            var local = new Vector2[screen.Length];
            for (int i = 0; i < screen.Length; i++)
            {
                local[i] = head + kit.Frame.Local(screen[i]);
            }

            return local;
        }

        kit.Shade("stone shadow", Up(new Vector2(-0.1f, -0.02f), new Vector2(0.1f, -0.02f), new Vector2(0.1f, 0.03f), new Vector2(-0.1f, 0.03f)), 0.5f);
        switch (shape)
        {
            case 0: // a round-topped tablet
            {
                const float w = 0.16f, tall = 0.31f;
                var outline = new List<Vector2> { new(-w / 2, 0f) };
                for (int i = 0; i <= 8; i++)
                {
                    float a = Mathf.Pi + (i * Mathf.Pi / 8f);
                    outline.Add(new Vector2(Mathf.Cos(a) * w / 2f, -tall + (w / 2f) + (Mathf.Sin(a) * w / 2f)));
                }

                outline.Add(new Vector2(w / 2, 0f));
                kit.Thing("stone", Up(outline.ToArray()), Headstone, 0.85f);
                kit.Line("stone line", Up(new Vector2(-w * 0.34f, -tall * 0.55f))[0], Up(new Vector2(w * 0.34f, -tall * 0.55f))[0], StoneShade, 1f);
                break;
            }

            case 1: // a cross
                kit.Thing("stone", Up(new Vector2(-0.062f, 0f), new Vector2(0.062f, 0f), new Vector2(0.062f, -0.36f), new Vector2(-0.062f, -0.36f)), Headstone, 0.85f);
                kit.Thing("stone arm", Up(new Vector2(-0.155f, -0.29f), new Vector2(0.155f, -0.29f), new Vector2(0.155f, -0.225f), new Vector2(-0.155f, -0.225f)), Headstone, 0.85f);
                break;

            default: // a rough slab
                kit.Thing("stone", Up(new Vector2(-0.115f, 0f), new Vector2(-0.1f, -0.25f), new Vector2(-0.015f, -0.28f), new Vector2(0.1f, -0.235f), new Vector2(0.115f, 0f)), new Color("#bab6aa"), 0.85f);
                kit.Line("stone line", Up(new Vector2(-0.06f, -0.17f))[0], Up(new Vector2(0.04f, -0.19f))[0], StoneShade, 1f);
                break;
        }
    }

    /// <summary>The low hedge round the cemetery: a dark band, rounded clumps along it, open on the front edge for the lychgate.</summary>
    private static void HedgeRound(YardKit kit, float hw, float hd, int seed)
    {
        const float thick = 0.17f;
        float gate = LychgateWide / 2f;
        (Vector2 From, Vector2 To)[] runs =
        {
            (new Vector2(-hw, -hd), new Vector2(hw, -hd)),
            (new Vector2(-hw, -hd), new Vector2(-hw, hd)),
            (new Vector2(hw, -hd), new Vector2(hw, hd)),
            (new Vector2(-hw, hd), new Vector2(-gate, hd)),
            (new Vector2(gate, hd), new Vector2(hw, hd)),
        };
        int n = 0;
        foreach ((Vector2 from, Vector2 to) in runs)
        {
            Vector2 along = to - from;
            float length = along.Length();
            Vector2 middle = (from + to) / 2f;
            Vector2 inward = -middle.Normalized() * (thick * 0.5f);
            Vector2 inset = new Vector2(Mathf.Abs(along.X) > Mathf.Abs(along.Y) ? 0f : inward.X, Mathf.Abs(along.X) > Mathf.Abs(along.Y) ? inward.Y : 0f);
            float turn = along.Angle();
            kit.Fill("hedge", Box(middle + inset, length + thick, thick, turn), HedgeDark);
            int clumps = Math.Max(2, Mathf.RoundToInt(length / 0.17f));
            for (int i = 0; i <= clumps; i++)
            {
                Vector2 at = from + (along * (i / (float)clumps)) + inset;
                float r = thick * (0.55f + (0.2f * Hash01(seed, n, 61)));
                Color c = Hash01(seed, n, 62) < 0.5f ? Hedge : HedgeLight;
                kit.Fill("hedge", Blob(at, r, r, 0f, seed + n, 0.18f, 10), c);
                n++;
            }
        }

        kit.Outline("hedge edge", Box(Vector2.Zero, (2f * hw) + 0.04f, (2f * hd) + 0.04f, 0f), 0.35f, 1f);
    }

    /// <summary>The lychgate on the front edge, where the graves begin: two posts and a little shingled roof over the way in.</summary>
    private static void Lychgate(YardKit kit, Vector2 at)
    {
        float half = LychgateWide / 2f;
        foreach (float x in new[] { -half, half })
        {
            kit.Thing("gate post", Box(at + new Vector2(x, 0f), 0.07f, 0.12f, 0f), LychPost, 0.85f);
        }

        Vector2[] roof = Box(at, LychgateWide + 0.24f, 0.3f, 0f);
        kit.Shade("gate shadow", roof, 0.6f);
        kit.Fill("gate", Box(at + new Vector2(0f, -0.075f), LychgateWide + 0.24f, 0.15f, 0f), LychRoof);
        kit.Fill("gate", Box(at + new Vector2(0f, 0.075f), LychgateWide + 0.24f, 0.15f, 0f), LychRoofShade);
        var shingles = new List<Vector2>();
        for (float x = -half - 0.08f; x < half + 0.1f; x += 0.07f)
        {
            shingles.Add(at + new Vector2(x, -0.14f));
            shingles.Add(at + new Vector2(x, 0.14f));
        }

        kit.Strokes("gate", shingles, TimberDark with { A = 0.4f }, 0f, 1f);
        kit.Outline("gate", roof, 0.9f, 1f);
        kit.Line("gate ridge", at + new Vector2(-half - 0.12f, 0f), at + new Vector2(half + 0.12f, 0f), Ink with { A = 0.9f }, 1f);
    }

    /// <summary>
    /// ⭐ The dead who wait for a grave (`cemetery.md §6.3`; Joe's call 4 a) — where they fell, small and plain under a cloth; nothing
    /// drawn for one laid out at home, nor for one in a groundskeeper's arms (drawn with them). Nothing rots and no clock is shown.
    /// </summary>
    private void DrawTheWaitingDead(SimWorld world)
    {
        foreach (int id in world.Unburied)
        {
            if (world.FindVillager(id) is not Villager dead || world.IsBeingCarried(dead) || AtHome(world, dead))
            {
                continue;
            }

            DrawTheCloth(ToScreen(dead.Position), 1f);
        }

        // In a groundskeeper's arms: the same cloth, small, at their side.
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager keeper = world.Villagers[i];
            if (keeper.Alive && keeper.CarryingTheDead != 0)
            {
                DrawTheCloth(DrawnCentre(keeper) + new Vector2(_pixelsPerTile * 0.18f, -_pixelsPerTile * 0.1f), 0.7f);
            }
        }
    }

    private static bool AtHome(SimWorld world, Villager dead) =>
        world.FindHousehold(dead.HouseholdId) is { HasHome: true } && dead.Tile == world.HomePlaceOf(dead);

    /// <summary>
    /// ⭐ The dead waiting, under plain linen (A287, `cemetery-art.md §2`; Joe: "All as recommended" — cozy, never grim): a person's
    /// length, rounded at both ends, a soft shadow, three soft folds.
    /// </summary>
    private void DrawTheCloth(Vector2 at, float scale)
    {
        float w = _pixelsPerTile * 0.55f * scale;
        float h = _pixelsPerTile * 0.22f * scale;
        Vector2[] Stadium(Vector2 c)
        {
            const int n = 10;
            var shape = new Vector2[(2 * n) + 2];
            for (int i = 0; i <= n; i++)
            {
                float a = (Mathf.Pi / 2f) + (i * Mathf.Pi / n);
                shape[i] = c + new Vector2((-w / 2f) + (h / 2f) + (Mathf.Cos(a) * h / 2f), Mathf.Sin(a) * h / 2f);
                shape[n + 1 + i] = c + new Vector2((w / 2f) - (h / 2f) - (Mathf.Cos(a) * h / 2f), -Mathf.Sin(a) * h / 2f);
            }

            return shape;
        }

        DrawColoredPolygon(Stadium(at + new Vector2(h * 0.18f, h * 0.3f)), AtNight(new Color(0.16f, 0.12f, 0.08f, 0.22f)));
        Vector2[] cloth = Stadium(at);
        DrawColoredPolygon(cloth, AtNight(Shroud));
        foreach (float k in new[] { -0.25f, 0.05f, 0.3f })
        {
            DrawArc(at + new Vector2(w * k, 0f), h * 0.42f, -0.9f, 0.9f, 6, AtNight(ShroudFold), Mathf.Max(1f, h * 0.08f), true);
        }

        var ring = new Vector2[cloth.Length + 1];
        cloth.CopyTo(ring, 0);
        ring[^1] = cloth[0];
        DrawPolyline(ring, AtNight(Ink with { A = 0.7f }), 1f, antialiased: true);
    }

    /// <summary>
    /// ⭐ The probe's <c>cemetery:</c> line (W4 step 7, D912) — the view has no tests (D11), so the drawing is posed here: a cemetery is
    /// ground and graves and no roof; a stone a grave, a mound before the first year's; tufts only as it wears; the card's words a
    /// cemetery's, never the builders'.
    /// </summary>
    internal string TheCemeteryHolds()
    {
        try
        {
            var faults = new List<string>();
            RoofLook look = RoofOf(JobKind.Groundskeeper);
            if (look.Form != RoofForm.Cemetery)
            {
                faults.Add($"a groundskeeper's building draws as {look.Form}, not a cemetery");
            }

            List<RoofPiece> Draw(YardState yard) =>
                RoofDrawing(look, new Vector2(10f, 10f), 4f * 0.96f, 5f * 0.96f, 0, Colors.White, 1f, null, new Vector2(10f, 10f), 5, false, yard);
            int Count(List<RoofPiece> pieces, string part) => pieces.Count(p => p.Part == part && p.Kind == RoofPieceKind.Fill);

            List<RoofPiece> kept = Draw(new YardState(Stock: 0, Graves: "sf" + new string('.', 18), GravesWide: 4));
            if (Count(kept, "stone") != 2 || Count(kept, "mound") != 1 || Count(kept, "tuft") != 0)
            {
                faults.Add($"two graves, one fresh, drew {Count(kept, "stone")} stones, {Count(kept, "mound")} mounds and {Count(kept, "tuft")} tufts");
            }

            if (kept.Any(p => p.Kind == RoofPieceKind.Textured) || Count(kept, "hedge") == 0 || Count(kept, "gate") == 0 || kept.Any(p => p.Part == "fence"))
            {
                faults.Add("the cemetery has a roof, or no hedge (A287), or no lychgate, or W4's fence still");
            }

            // ⭐ The stones stand up on screen whichever way the cemetery faces (A287): every stone taller than it is wide, at a
            // quarter turn too; the shapes vary along a row; the grassed mounds are there but are not counted as fresh.
            foreach (ushort facing in new ushort[] { 0, 16384 })
            {
                List<RoofPiece> turned = RoofDrawing(look, new Vector2(10f, 10f), 4f * 0.96f, 5f * 0.96f, facing, Colors.White, 40f, null,
                    new Vector2(10f, 10f), 5, false, new YardState(Stock: 0, Graves: "sss" + new string('.', 17), GravesWide: 4));
                foreach (RoofPiece s in turned.Where(p => p.Part == "stone" && p.Kind == RoofPieceKind.Fill))
                {
                    float tall = s.Points.Max(q => q.Y) - s.Points.Min(q => q.Y), wide = s.Points.Max(q => q.X) - s.Points.Min(q => q.X);
                    if (tall <= wide)
                    {
                        faults.Add($"at facing {facing} a stone lies down ({tall:F1} tall, {wide:F1} wide)");
                        break;
                    }
                }
            }

            int shapes = Enumerable.Range(0, 4).Select(StoneShapeOf).Distinct().Count();
            if (shapes < 3 || Count(kept, "grassed mound") != 1)
            {
                faults.Add($"a row of four stones in {shapes} shapes (want 3); {Count(kept, "grassed mound")} grassed mounds for one old grave (want 1)");
            }

            List<RoofPiece> wild = Draw(new YardState(Stock: 3, Graves: "s" + new string('.', 19), GravesWide: 4));
            if (Count(wild, "tuft") == 0 || Count(wild, "stone") != 1)
            {
                faults.Add($"a wild cemetery drew {Count(wild, "tuft")} tufts and lost its stone ({Count(wild, "stone")})");
            }

            foreach (Condition state in Enum.GetValues<Condition>())
            {
                string? said = Main.CemeteryWearLine(new WearReport(state, 50, 1, 1, 0, false))?.Text;
                if (said is null || said.Contains("builder", StringComparison.Ordinal) || said.Contains("closed", StringComparison.Ordinal))
                {
                    faults.Add($"{state} reads \"{said}\"");
                }
            }

            return faults.Count == 0
                ? $"[widths] cemetery: ✅ ground and graves, no roof, a low hedge and a lychgate (A287) — {Count(kept, "stone")} stones standing up at either facing, three shapes along a row, {Count(kept, "mound")} fresh mound and {Count(kept, "grassed mound")} grassed, "
                  + $"{Count(wild, "tuft")} tufts wild, its stone still there; four states in a cemetery's words"
                : $"[widths] cemetery: ⛔ {string.Join("; ", faults)}";
        }
        catch (Exception e)
        {
            return $"[widths] cemetery: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}
