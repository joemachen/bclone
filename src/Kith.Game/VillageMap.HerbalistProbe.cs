using Godot;

namespace Kith.Game;

/// <remarks>
/// The probe's <c>herbalist's hut:</c> line (A326, <c>specs/herbalist-art.md §5</c>), named from the spec (art traps 17, 25) — it
/// rides on <c>building yards:</c>'s return after the tavern's (trap 73), so a crash there loses all four lines. The hut has no
/// <c>JobKind</c> yet (W6 piece 2 makes it), so the probe draws the row the slice will add (§3) itself.
/// </remarks>
public partial class VillageMap
{
    internal string TheHerbalistsHutHolds()
    {
        try
        {
            var faults = new List<string>();
            const float ppt = 40f;
            var centre = new Vector2(500f, 500f);
            Color colour = new("#8b78a6");

            // §3: the row W6 piece 2 adds to RoofOf(JobKind) — thatch, a chimney, this yard.
            var hut = new RoofLook(RoofForm.Roof, RoofMaterial.Thatch, Chimneys: 1, Yard: YardKind.Herbalist);
            List<RoofPiece> Draw(float wide, float deep, ushort facing, YardState s) =>
                RoofDrawing(hut, centre, wide, deep, facing, colour, ppt, null, centre, 5, false, s);
            Vector2 Local(Vector2 at, ushort facing)
            {
                float a = facing * Mathf.Tau / 65536f;
                Vector2 d = (at - centre) / ppt;
                return new Vector2((d.X * Mathf.Cos(a)) + (d.Y * Mathf.Sin(a)), (-d.X * Mathf.Sin(a)) + (d.Y * Mathf.Cos(a)));
            }

            static int Count(List<RoofPiece> pieces, string part) => pieces.Count(p => p.Part == part && p.Kind == RoofPieceKind.Fill);

            // §2: the hut at the back of its plot, less than half its depth; the garden in front, on the door's side, clear of it.
            var frame = new RoofFrame(Vector2.Zero, 0, 1f);
            foreach (Vector2 way in new[] { new Vector2(0, 1), new Vector2(1, 0), new Vector2(0, -1), new Vector2(-1, 0) })
            {
                YardLayout l = LayoutOf(YardKind.Herbalist, 0.8f, 0.8f, way, way * 0.8f, 5);
                float roofFront = l.RoofAt.Dot(way) + (Mathf.Abs(way.X) > 0.5f ? l.RoofHw : l.RoofHd);
                float roofDepth = 2f * (Mathf.Abs(way.X) > 0.5f ? l.RoofHw : l.RoofHd);
                float gardenBack = l.YardAt.Dot(way) - l.YardShort;
                if (l.RoofAt.Dot(way) >= 0f || roofDepth > 1.0f || l.YardAt.Dot(way) <= 0f || gardenBack <= roofFront)
                {
                    faults.Add($"door {way}: the hut at {l.RoofAt} {roofDepth:0.00} deep, the garden at {l.YardAt} from {gardenBack:0.00} "
                        + $"(want the hut behind, under 1.0 of 2.0, the garden in front, clear of the hut's front at {roofFront:0.00})");
                }
            }

            // §2: six beds and the path, the path in the slot nearest the door, no bed on it; the wattle; the rack of five bundles.
            foreach (float doorX in new[] { -0.75f, -0.3f, 0f, 0.4f, 0.8f })
            {
                YardLayout l = LayoutOf(YardKind.Herbalist, 0.8f, 0.8f, new Vector2(0, 1), new Vector2(doorX, 0.8f), 5);
                var pieces = new List<RoofPiece>();
                YardUnder(pieces, frame, YardKind.Herbalist, l, new YardState(Stock: 2), 5);
                float Along(Vector2 p) => (p - l.YardAt).Dot(l.YardAlong) / l.YardLong;
                RoofPiece? path = pieces.FirstOrDefault(p => p.Part == "garden path");
                float pathAt = path is null ? float.NaN : path.Points.Average(p => Along(p));
                float doorAt = Mathf.Clamp(new Vector2(doorX, 0.8f).Dot(l.YardAlong) / l.YardLong, -0.84f, 0.84f);
                int onPath = pieces.Where(p => p.Part == "herb bed" && p.Kind == RoofPieceKind.Fill)
                    .Count(p => p.Points.Any(q => Mathf.Abs(Along(q) - pathAt) < 0.07f / l.YardLong));
                if (Count(pieces, "herb bed") != 6 || path is null || Mathf.Abs(pathAt - doorAt) > 0.15f || onPath > 0
                    || !pieces.Any(p => p.Part == "wattle") || Count(pieces, "herb bundle") != 5 || !pieces.Any(p => p.Part == "drying rack"))
                {
                    faults.Add($"the door at {doorX}: {Count(pieces, "herb bed")} beds, the path at {pathAt:0.00} for a door at {doorAt:0.00}, "
                        + $"{onPath} beds on it, wattle {pieces.Any(p => p.Part == "wattle")}, {Count(pieces, "herb bundle")} bundles "
                        + "(want 6, within 0.15, 0, true, 5)");
                }
            }

            // §2: the remedies two jars a level, to six; the shelf only with a jar on it.
            for (byte level = 0; level <= 4; level++)
            {
                List<RoofPiece> drawn = Draw(1.6f, 1.6f, 0, new YardState(Stock: level));
                int want = Math.Min(level * 2, 6);
                if (Count(drawn, "remedy") != want || (Count(drawn, "remedy shelf") == 1) != (level > 0))
                {
                    faults.Add($"remedies at level {level}: {Count(drawn, "remedy")} jars, {Count(drawn, "remedy shelf")} shelves (want {want}, {(level > 0 ? 1 : 0)})");
                }
            }

            // Every thing on its building's ground — the extent, the drawn size ÷ 0.8 — at four turns, at 2 × 2 and at 2 × 1.
            foreach ((float w, float d) in new[] { (1.6f, 1.6f), (1.6f, 0.8f) })
            {
                foreach (ushort facing in new ushort[] { 0, 10000, 30000, 50000 })
                {
                    float gw = (w / 0.8f / 2f) + 0.03f;
                    float gd = (d / 0.8f / 2f) + 0.03f;
                    string[] outside = Draw(w, d, facing, new YardState(Stock: 4))
                        .Where(p => !p.Part.Contains("shadow", StringComparison.Ordinal) && !p.Part.StartsWith("face", StringComparison.Ordinal)
                            && !p.Part.StartsWith("chimney", StringComparison.Ordinal) && !p.Part.StartsWith("ground", StringComparison.Ordinal)
                            && !p.Part.StartsWith("step", StringComparison.Ordinal))
                        .Where(p => p.Points.Any(q => Mathf.Abs(Local(q, facing).X) > gw || Mathf.Abs(Local(q, facing).Y) > gd))
                        .Select(p => p.Part).Distinct().ToArray();
                    if (outside.Length > 0)
                    {
                        faults.Add($"the hut {w / 0.8f:0} × {d / 0.8f:0} at turn {facing} has {string.Join(", ", outside)} off its ground");
                        break;
                    }
                }
            }

            return faults.Count == 0
                ? "[widths] herbalist's hut: ✅ the hut at the back under half the plot, the garden in front on the door's side at four doors; six beds, the path in the slot nearest the door at five doors and no bed on it, the wattle, five bundles on the rack; the remedies two jars a level to six, the shelf only with a jar; every thing on its ground at four turns, at 2 × 2 and 2 × 1 (A326)"
                : $"[widths] herbalist's hut: ⛔ {string.Join("; ", faults)}";
        }
        catch (Exception e)
        {
            return $"[widths] herbalist's hut: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}
