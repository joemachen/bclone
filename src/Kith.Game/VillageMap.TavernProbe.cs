using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// The probe's <c>tavern and brewery:</c> line (A199, <c>specs/tavern-and-brewery-art.md §5</c>), named from the spec (art traps
/// 17, 25) — it rides on <c>building yards:</c>'s return, as <c>stock piles:</c> does (trap 73), so a crash there loses both.
/// </remarks>
public partial class VillageMap
{
    internal string TheTavernAndBreweryHold()
    {
        try
        {
            var faults = new List<string>();
            const float ppt = 40f;
            var centre = new Vector2(500f, 500f);
            Color colour = new("#9a6b3f");
            RoofLook brewery = RoofOf(JobKind.Brewer);
            RoofLook tavern = RoofOf(JobKind.TavernKeeper);

            List<RoofPiece> Draw(RoofLook look, float wide, float deep, ushort facing, YardState s) =>
                RoofDrawing(look, centre, wide, deep, facing, colour, ppt, null, centre, 5, false, s);
            Vector2 Local(Vector2 at, ushort facing)
            {
                float a = facing * Mathf.Tau / 65536f;
                Vector2 d = (at - centre) / ppt;
                return new Vector2((d.X * Mathf.Cos(a)) + (d.Y * Mathf.Sin(a)), (-d.X * Mathf.Sin(a)) + (d.Y * Mathf.Cos(a)));
            }

            static int Count(List<RoofPiece> pieces, string part) => pieces.Count(p => p.Part == part && p.Kind == RoofPieceKind.Fill);
            static bool Has(List<RoofPiece> pieces, string part) => pieces.Any(p => p.Part == part);

            // The table (§3): their yards, the brewery shingle, the tavern thatch, a chimney each.
            if (brewery.Yard != YardKind.Brewery || brewery.Material != RoofMaterial.Shingle || brewery.Chimneys != 1
                || tavern.Yard != YardKind.Tavern || tavern.Material != RoofMaterial.Thatch || tavern.Chimneys != 1)
            {
                faults.Add($"the brewery is {brewery} and the tavern {tavern} (want the brewery's yard, shingle, a chimney; the tavern's, thatch, a chimney)");
            }

            // §3.1: the copper, the tun and the racked casks always; open, the wort, the paddle and barley only while brewing.
            var idle = default(YardState);
            var brewing = new YardState(Working: true);
            List<RoofPiece> shut = Draw(brewery, 1.6f, 0.8f, 0, idle);
            List<RoofPiece> open = Draw(brewery, 1.6f, 0.8f, 0, brewing);
            foreach (string part in new[] { "copper", "copper setting", "mash tun", "stillage", "cask" })
            {
                if (!Has(shut, part) || !Has(open, part))
                {
                    faults.Add($"the brewery's {part}: idle {Has(shut, part)}, brewing {Has(open, part)} (want both)");
                }
            }

            foreach ((string part, bool whileBrewing) in new[] { ("wort", true), ("paddle", true), ("barley", true), ("copper lid", false) })
            {
                if (Has(open, part) != whileBrewing || Has(shut, part) == whileBrewing)
                {
                    faults.Add($"the brewery's {part}: brewing {Has(open, part)}, idle {Has(shut, part)} (want {whileBrewing}, {!whileBrewing})");
                }
            }

            (StockRead bStock, StockRead bStock2, bool bWorking) = StockReadOf(YardKind.Brewery);
            (StockRead tStock, StockRead tStock2, bool tWorking) = StockReadOf(YardKind.Tavern);
            if (bStock != StockRead.None || bStock2 != StockRead.None || !bWorking || tStock != StockRead.Ale || tStock2 != StockRead.None || tWorking)
            {
                faults.Add($"the brewery reads {bStock}, {bStock2}, while working {bWorking}; the tavern {tStock}, {tStock2}, {tWorking} (want None, None, true; Ale, None, false)");
            }

            var store = new Stockpile(Stockpile.Kinds);
            store.Add(Goods.Ale, 7);
            store.Add(Goods.Barley, 40);
            if (Read(store, StockRead.Ale, Array.Empty<Goods>()) != 7)
            {
                faults.Add($"reading the tavern's ale gives {Read(store, StockRead.Ale, Array.Empty<Goods>())} (want 7, its own good)");
            }

            // §3.2: the sign, the trestle and its benches always; the casks of ale one a level, none at none.
            for (byte level = 0; level <= 4; level++)
            {
                List<RoofPiece> drawn = Draw(tavern, 1.6f, 1.6f, 0, new YardState(Stock: level));
                int casks = Count(drawn, "ale");
                if (casks != level || !Has(drawn, "sign") || !Has(drawn, "sign arm") || Count(drawn, "trestle") != 1 || Count(drawn, "trestle bench") != 2)
                {
                    faults.Add($"the tavern at ale level {level}: {casks} casks, sign {Has(drawn, "sign")}, arm {Has(drawn, "sign arm")}, "
                        + $"{Count(drawn, "trestle")} trestles, {Count(drawn, "trestle bench")} benches (want {level}, true, true, 1, 2)");
                }
            }

            // Every thing on its building's ground — the extent, the drawn size ÷ 0.8 — at four turns; the light excepted.
            foreach ((string what, RoofLook look, float w, float d, YardState s) in new[]
            {
                ("brewery", brewery, 1.6f, 0.8f, brewing), ("brewery idle", brewery, 1.6f, 0.8f, idle),
                ("tavern", tavern, 1.6f, 1.6f, new YardState(Stock: 4)),
            })
            {
                foreach (ushort facing in new ushort[] { 0, 10000, 30000, 50000 })
                {
                    float gw = (w / 0.8f / 2f) + 0.03f;
                    float gd = (d / 0.8f / 2f) + 0.03f;
                    string[] outside = Draw(look, w, d, facing, s)
                        .Where(p => !IsLamplight(p) && !p.Part.Contains("shadow", StringComparison.Ordinal) && !p.Part.StartsWith("face", StringComparison.Ordinal)
                            && !p.Part.StartsWith("chimney", StringComparison.Ordinal) && !p.Part.StartsWith("ground", StringComparison.Ordinal))
                        .Where(p => p.Points.Any(q => Mathf.Abs(Local(q, facing).X) > gw || Mathf.Abs(Local(q, facing).Y) > gd))
                        .Select(p => p.Part).Distinct().ToArray();
                    if (outside.Length > 0)
                    {
                        faults.Add($"the {what} at turn {facing} has {string.Join(", ", outside)} off its ground");
                        break;
                    }
                }
            }

            // The sign stands by the door, the casks on one side of it and the trestle on the other, wherever the door is.
            var frame = new RoofFrame(Vector2.Zero, 0, 1f);
            foreach (float doorX in new[] { -0.4f, 0f, 0.4f })
            {
                YardLayout layout = LayoutOf(YardKind.Tavern, 0.8f, 0.8f, new Vector2(0, 1), new Vector2(doorX, 0.8f), 5);
                var pieces = new List<RoofPiece>();
                YardUnder(pieces, frame, YardKind.Tavern, layout, new YardState(Stock: 2), 5);
                float Mid(string part) => pieces.Where(p => p.Part == part && p.Kind == RoofPieceKind.Fill).Select(p => CentreOf(p.Points).X).DefaultIfEmpty(float.NaN).Average();
                float sign = Mid("sign"), casks = Mid("ale"), table = Mid("trestle");
                if (!(Mathf.Abs(sign - doorX) < 0.4f) || !(Mathf.Sign(casks - doorX) == -Mathf.Sign(table - doorX)) || Mathf.Sign(sign - doorX) != Mathf.Sign(table - doorX))
                {
                    faults.Add($"a tavern's door at x {doorX:F1}: its sign at {sign:F2}, its casks at {casks:F2}, its table at {table:F2} (want the sign by the door, on the table's side; the casks on the other)");
                }

                // ⭐ The light (call 3): five fans — two windows in front, the door, a window in each side — each from the roof's
                // eave outward, and a lit slit at each.
                Vector2 roofHalf = new(layout.RoofHw + RoofEaves, layout.RoofHd + RoofEaves);
                var fans = pieces.Where(p => IsLamplight(p) && p.Kind == RoofPieceKind.Fill).ToList();
                int slits = pieces.Count(p => IsLamplight(p) && p.Kind == RoofPieceKind.Line);
                int off = fans.Count(f =>
                {
                    Vector2 a = f.Points[0] - layout.RoofAt, b = f.Points[1] - layout.RoofAt, far = ((f.Points[2] + f.Points[3]) / 2f) - layout.RoofAt;
                    bool onEave(Vector2 q) => Mathf.Abs(Mathf.Max(Mathf.Abs(q.X) - roofHalf.X, Mathf.Abs(q.Y) - roofHalf.Y)) < 0.02f;
                    float outward = Mathf.Max(Mathf.Abs(far.X) - roofHalf.X, Mathf.Abs(far.Y) - roofHalf.Y);
                    return !onEave(a) || !onEave(b) || outward < 0.2f;
                });
                int front = fans.Count(f => f.Points[0].Y > layout.RoofAt.Y + roofHalf.Y - 0.02f);
                if (fans.Count != 5 || slits != 5 || off > 0 || front != 3)
                {
                    faults.Add($"a tavern's door at x {doorX:F1}: {fans.Count} fans of light, {slits} lit slits, {off} not falling out from the eave, {front} in front (want 5, 5, 0, 3)");
                }
            }

            // The brewery and every other kind have no lamplight; only a brewery has a copper to steam.
            if (open.Any(IsLamplight) || Draw(RoofOf(BuildingKind.Library), 1.6f, 1.6f, 0, idle).Any(IsLamplight)
                || CopperOf(open) is null || CopperOf(Draw(RoofOf(JobKind.Baker), 1.6f, 0.8f, 0, brewing)) is not null)
            {
                faults.Add("lamplight drawn on a building that is not the tavern, or the copper not found on the brewery alone");
            }

            // ⭐ Laid into the cache, the light is kept apart from the roof's runs (drawn each frame at its level), and the copper's middle kept.
            var far = new Vector2(-9000f, -9000f);
            RoofCache held = CachedRoof(new RoofSignature(tavern, far, 1.6f, 1.6f, 0, colour, false, null, far, 5, new YardState(Stock: 2)));
            List<RoofPiece> laid = RoofDrawing(tavern, far, 1.6f, 1.6f, 0, colour, 1f, null, far, 5, false, new YardState(Stock: 2));
            int inRuns = held.Runs.Sum(r => r.Triangles);
            int without = RoofRuns(laid.Where(p => !IsLamplight(p)).ToList(), _pixelsPerTile).Sum(r => r.Triangles);
            RoofCache brewed = CachedRoof(new RoofSignature(brewery, far + new Vector2(10f, 0f), 1.6f, 0.8f, 0, colour, false, null, far, 5, brewing));
            _roofCache.Remove(far);
            _roofCache.Remove(far + new Vector2(10f, 0f));
            if (held.Lamps.Length != 10 || inRuns != without || brewed.Copper is null)
            {
                faults.Add($"the tavern's cache holds {held.Lamps.Length} lamplight pieces and {inRuns} triangles in its runs (want 10, and {without}: none of the light); the brewery's copper {(brewed.Copper is null ? "lost" : "kept")}");
            }

            // Who lights it (call 3), whose batch steams (call 2), who has a stove (call 5).
            if (!LightsTheTavern(VillagerState.AtTheTavern) || LightsTheTavern(VillagerState.KeepingTheTavern) || LightsTheTavern(VillagerState.WalkingToTheTavern)
                || !SteamsTheCopper(JobKind.Brewer) || SteamsTheCopper(JobKind.Baker) || SteamsTheCopper(JobKind.Miller)
                || !HasAStove(JobKind.TavernKeeper) || !HasAStove(JobKind.Teacher) || HasAStove(JobKind.Brewer))
            {
                faults.Add("the rules: a drinker lights the tavern and the keeper or a walker does not; the brewer's batch steams and the baker's and miller's do not; the tavern and the school have a stove and the brewery none");
            }

            // The light and the steam come and go over a second, held while paused.
            float rising = SmokeOn(0f, true, 0.5), falling = SmokeOn(1f, false, 0.5), held0 = SmokeOn(0.4f, false, 0.0);
            if (!(rising > 0.4f && rising < 0.6f) || !(falling > 0.4f && falling < 0.6f) || held0 != 0.4f)
            {
                faults.Add($"the light over half a second rises to {rising:F2}, falls to {falling:F2}, paused holds {held0:F2} (want ½, ½, 0.40)");
            }

            // Call 4: their trades' own colours, on the plain map and the painted one alike.
            if (ColourOf(JobKind.Brewer) != TradeGlyph.ColourOf(JobKind.Brewer) || ColourOf(JobKind.TavernKeeper) != TradeGlyph.ColourOf(JobKind.TavernKeeper)
                || ColourOf(JobKind.Brewer) == MarketColour || ColourOf(JobKind.TavernKeeper) == MarketColour)
            {
                faults.Add($"the brewery draws {ColourOf(JobKind.Brewer).ToHtml(false)} and the tavern {ColourOf(JobKind.TavernKeeper).ToHtml(false)} (want their trades' {TradeGlyph.ColourOf(JobKind.Brewer).ToHtml(false)}, {TradeGlyph.ColourOf(JobKind.TavernKeeper).ToHtml(false)})");
            }

            // Call 2: the steam rises from the copper and is pale — lighter than the smoke's paintings, never its grey.
            static float Lightness(byte[] rgba)
            {
                double sum = 0, weight = 0;
                for (int i = 0; i < rgba.Length; i += 4)
                {
                    double a = rgba[i + 3] / 255.0;
                    sum += a * (rgba[i] + rgba[i + 1] + rgba[i + 2]) / 765.0;
                    weight += a;
                }

                return (float)(sum / Math.Max(weight, 1e-9));
            }

            float steamLight = Lightness(PaintAPuff(0, SteamWarm, SteamCool, SteamEdge)), smokeLight = Lightness(PaintThePuff(0));
            (Vector2 late, _, _) = SteamAt(0, 0.6f, 0.0);
            (Vector2 early, float r0, _) = SteamAt(0, 0.1f, 0.0);
            (_, float r1, _) = SteamAt(0, 0.6f, 0.0);
            if (steamLight < 0.85f || smokeLight > 0.6f || !(late.Y < early.Y) || !(r1 > r0))
            {
                faults.Add($"the steam paints at lightness {steamLight:F2} against the smoke's {smokeLight:F2} (want ≥ 0.85 and the smoke ≤ 0.6), rising {early.Y:F2} → {late.Y:F2}, growing {r0:F2} → {r1:F2}");
            }

            return faults.Count == 0
                ? "[widths] tavern and brewery: ✅ the brewery shingle and the tavern thatch, each its yard and chimney; the copper, tun and racked casks always, the copper open with the wort, paddle and barley only while brewing, shut under its lid otherwise; the tavern's sign, trestle and benches always, its ale one cask a level from its own store, none at none; every thing on its ground at four turns; the sign by the door wherever it is, the table beside it, the casks the other side; five fans of lamplight out from the eave and five lit slits, kept out of the roof's runs, on the tavern alone; a drinker lights it, the keeper does not; the brewer's batch steams, pale; the tavern's stove; their trades' colours; the light and steam over a second, held while paused (A199)"
                : $"[widths] tavern and brewery: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            return $"[widths] tavern and brewery: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}
