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
    /// The cemetery's pieces, in its own frame: the mown ground (rougher as it wears), its graves — a stone each, a fresh mound
    /// before it the first year — a low fence and the gate on the front edge.
    /// </summary>
    private static void Cemetery(List<RoofPiece> pieces, RoofFrame frame, float hw, float hd, YardState yard, int seed)
    {
        var kit = new YardKit(pieces, frame);
        int wear = yard.Stock;
        Color ground = wear switch { 0 => CemeteryGrass, 1 => CemeteryRough, _ => CemeteryWild };
        kit.Fill("ground", Box(Vector2.Zero, 2f * hw, 2f * hd, 0f), ground);

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

        string graves = yard.Graves ?? string.Empty;
        int wide = Math.Max(1, (int)yard.GravesWide);
        int rows = Math.Max(1, (graves.Length + wide - 1) / wide);
        float cw = 2f * hw / wide;
        float ch = 2f * hd / rows;
        for (int k = 0; k < graves.Length; k++)
        {
            if (graves[k] == '.')
            {
                continue;
            }

            var cell = new Vector2(-hw + ((k % wide) + 0.5f) * cw, hd - ((k / wide) + 0.5f) * ch);
            if (graves[k] == 'f')
            {
                kit.Thing("mound", Box(cell + new Vector2(0f, ch * 0.1f), cw * 0.46f, ch * 0.5f, 0f), FreshEarth, 0.4f);
            }

            kit.Thing("stone", Box(cell - new Vector2(0f, ch * 0.24f), cw * 0.4f, ch * 0.16f, 0f), Headstone, 0.7f);
        }

        // The fence: a low rail and its posts; the gate a little roofed pair on the front edge, where the sim's places begin.
        kit.Outline("fence", Box(Vector2.Zero, 2f * hw, 2f * hd, 0f), 0.85f, 1.5f);
        for (float x = -hw; x <= hw + 0.01f; x += Math.Max(0.35f, hw / 4f))
        {
            kit.Fill("post", Box(new Vector2(x, -hd), 0.07f, 0.07f, 0f), TimberDark);
            kit.Fill("post", Box(new Vector2(x, hd), 0.07f, 0.07f, 0f), TimberDark);
        }

        kit.Thing("gate", Box(new Vector2(0f, hd), Math.Min(0.6f, hw), 0.16f, 0f), TimberLight);
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

    private void DrawTheCloth(Vector2 at, float scale)
    {
        float w = _pixelsPerTile * 0.42f * scale;
        float h = _pixelsPerTile * 0.17f * scale;
        var shape = new[]
        {
            at + new Vector2(-w / 2f, -h / 2f), at + new Vector2(w / 2f, -h / 2f),
            at + new Vector2(w / 2f, h / 2f), at + new Vector2(-w / 2f, h / 2f),
        };
        DrawColoredPolygon(shape, AtNight(Shroud));
        DrawPolyline(new[] { shape[0], shape[1], shape[2], shape[3], shape[0] }, AtNight(Ink with { A = 0.6f }), 1f);
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

            if (kept.Any(p => p.Kind == RoofPieceKind.Textured) || !kept.Any(p => p.Part == "fence") || Count(kept, "gate") == 0)
            {
                faults.Add("the cemetery has a roof, or no fence, or no gate");
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
                ? $"[widths] cemetery: ✅ ground and graves, no roof — {Count(kept, "stone")} stones, {Count(kept, "mound")} fresh mound kept, "
                  + $"{Count(wild, "tuft")} tufts wild, its stone still there; four states in a cemetery's words"
                : $"[widths] cemetery: ⛔ {string.Join("; ", faults)}";
        }
        catch (Exception e)
        {
            return $"[widths] cemetery: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}
