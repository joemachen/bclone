using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// The probe's <c>night view:</c> and <c>homes lit:</c> lines (A252, <c>specs/night-view.md §7</c>), named from the spec (art traps 17, 25).
/// </remarks>
public partial class VillageMap
{
    /// <summary>The valley's grounds as the watercolour draws them by day (Millbrook, A251's pictures: the medians) — what a mark must read on by night.</summary>
    private static readonly (string Name, Color Day)[] GroundsToReadOn =
    {
        ("grass", Color.Color8(117, 138, 81)), ("autumn's grass", Color.Color8(126, 135, 66)), ("snow", Color.Color8(197, 202, 204)),
        ("water", Color.Color8(91, 127, 145)), ("winter's water", Color.Color8(77, 107, 122)), ("housing ground", Color.Color8(141, 137, 80)),
    };

    /// <summary>The least a mark may stand off a night ground by (A250 measured 3.4 on the ground as rendered; daylight's least is 2.3).</summary>
    private const float NightContrast = 3f;

    private static float LuminanceOf(Color c)
    {
        static float Linear(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
        return (0.2126f * Linear(c.R)) + (0.7152f * Linear(c.G)) + (0.0722f * Linear(c.B));
    }

    private static float ContrastOf(Color a, Color b)
    {
        float la = LuminanceOf(a), lb = LuminanceOf(b);
        return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
    }

    /// <summary>The shaders' own sum (<c>night_of</c>), for the probe.</summary>
    private static Color GroundAtNight(Color day)
    {
        float grey = (0.299f * day.R) + (0.587f * day.G) + (0.114f * day.B);
        return new Color(
            Mathf.Lerp(day.R, grey, DeepNightGrey) * DeepNight.R, Mathf.Lerp(day.G, grey, DeepNightGrey) * DeepNight.G, Mathf.Lerp(day.B, grey, DeepNightGrey) * DeepNight.B);
    }

    internal string TheNightHolds()
    {
        var spare = new VillageMap();
        try
        {
            var faults = new List<string>();
            static bool Near(Color a, Color b) => Mathf.Abs(a.R - b.R) + Mathf.Abs(a.G - b.G) + Mathf.Abs(a.B - b.B) + Mathf.Abs(a.A - b.A) < 0.004f;

            // A map starts by day — the title's picture is a map of its own (call 7) — and by day every colour is the one it was, to the bit.
            if (spare.NightShown || spare.NightTint != Colors.White || spare.SelfModulate != Colors.White)
            {
                faults.Add($"a new map starts at night {spare.NightShown}, tint {spare.NightTint}");
            }

            foreach (Color day in MarksAtNight.Keys)
            {
                if (spare.AtNight(day with { A = 0.3f }) != (day with { A = 0.3f }) || spare.Lifted(day, 1f) != day)
                {
                    faults.Add($"by day the mark {day.ToHtml(false)} is drawn {spare.AtNight(day).ToHtml()}");
                }
            }

            // A player's setting read as a village opens is the night at once; a switch on one on screen fades over a second, paused or not.
            spare.ShowNight(true);
            float opened = spare.NightLevel;
            bool tinted = Near(spare.SelfModulate, DeepNight);

            // ⭐ The marks: each comes back out of the tint as its own pale partner, its alpha kept, and reads on every ground.
            string least = string.Empty;
            float leastContrast = float.MaxValue;
            foreach ((Color day, Color night) in MarksAtNight)
            {
                Color drawn = spare.AtNight(day with { A = 0.4f });
                var seen = new Color(drawn.R * DeepNight.R, drawn.G * DeepNight.G, drawn.B * DeepNight.B, drawn.A);
                if (!Near(seen, night with { A = 0.4f }))
                {
                    faults.Add($"the mark {day.ToHtml(false)} is seen {seen.ToHtml()} by night (want {night.ToHtml(false)} at 0.4)");
                }

                if (day == ValleyEdge)
                {
                    continue; // the paper's edge is no mark, and stays a faint one
                }

                foreach ((string name, Color ground) in GroundsToReadOn)
                {
                    float contrast = ContrastOf(night, GroundAtNight(ground));
                    if (contrast < leastContrast)
                    {
                        (leastContrast, least) = (contrast, $"{night.ToHtml(false)} on {name}");
                    }
                }
            }

            if (leastContrast < NightContrast || MarksAtNight.Count != 17)
            {
                faults.Add($"{MarksAtNight.Count} marks have a night colour (want 17) and the least stands off by {leastContrast:F1}, {least} (want {NightContrast:F1})");
            }

            // What is the valley's goes dark with it: ink, a fence, a wall, a palisade's stakes.
            foreach (Color own in new[] { Ink, FenceEdge, StoneWallEdge, PalisadeEdge })
            {
                if (spare.AtNight(own) != own)
                {
                    faults.Add($"the valley's own {own.ToHtml(false)} is drawn as a mark by night");
                }
            }

            Color person = spare.PersonTint(1f);
            float brightness = person.G * DeepNight.G;
            if (!(brightness > 0.85f && brightness < 0.93f))
            {
                faults.Add($"a person is seen at {brightness:F2} of daylight by night (want four fifths of the way back: 0.89)");
            }

            // The three shaders take it, and are told day again — a title's map borrows the live one's uniforms, the night's with them.
            foreach ((string path, string use, int times) in new[]
            {
                ("res://shaders/valley_terrain.gdshader", "night_of(", 4), ("res://shaders/valley_fields.gdshader", "night_of(", 2), (SnowfallShaderPath, "* night_tint", 1),
            })
            {
                string code = GD.Load<Shader>(path).Code;
                int found = code.Split(use).Length - 1;
                if (found != times || !code.Contains("uniform vec3 night_tint = vec3(1.0);", StringComparison.Ordinal))
                {
                    faults.Add($"{path} uses the night {found} times (want {times}), or does not start by day");
                }
            }

            var ground0 = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/valley_terrain.gdshader") };
            spare._fieldsMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/valley_fields.gdshader") };
            spare._snowfallMaterial = new ShaderMaterial { Shader = GD.Load<Shader>(SnowfallShaderPath) };
            Vector3 Told(ShaderMaterial m) => m.GetShaderParameter("night_tint").AsVector3();
            spare.HandTheNightOver(ground0);
            var deep = new Vector3(DeepNight.R, DeepNight.G, DeepNight.B);
            bool night1 = Told(ground0).IsEqualApprox(deep) && Told(spare._fieldsMaterial).IsEqualApprox(deep) && Told(spare._snowfallMaterial).IsEqualApprox(deep.Lerp(Vector3.One, FlakeLift))
                && Mathf.IsEqualApprox(ground0.GetShaderParameter("night_grey").AsSingle(), DeepNightGrey);

            spare.TickTheNight(5.0);
            spare.ShowNight(false);
            float asked = spare.NightLevel;
            spare.TickTheNight(0.5);
            float half = spare.NightLevel;
            spare.TickTheNight(0.6);
            spare.HandTheNightOver(ground0);
            bool day1 = Told(ground0) == Vector3.One && Told(spare._fieldsMaterial) == Vector3.One && Told(spare._snowfallMaterial) == Vector3.One
                && ground0.GetShaderParameter("night_grey").AsSingle() == 0f && spare.SelfModulate == Colors.White;
            if (opened != 1f || !tinted || asked != 1f || !(half > 0.45f && half < 0.55f) || spare.NightLevel != 0f)
            {
                faults.Add($"opened by night it stands at {opened:F2}, tinted {tinted}; turned to day on screen {asked:F2}, half a second on {half:F2}, over a second {spare.NightLevel:F2} (want 1, true, 1, ½, 0)");
            }

            if (!night1 || !day1)
            {
                faults.Add($"the ground, fields and flakes are told the night {night1}, and day again {day1}");
            }

            // ⭐ The lamps: one fan and one lit slit at each; a ruin and every other kind none; each falling out from the eave, at four turns.
            foreach ((string what, RoofLook look, int want) in new[]
            {
                ("cabin", RoofOf(BuildingKind.Home), 3), ("cottage", RoofOf(BuildingKind.Cottage), 5), ("manor", RoofOf(BuildingKind.Manor), 7),
                ("smithy", RoofOf(JobKind.Smith), 1), ("bakery", RoofOf(JobKind.Baker), 1),
                ("brewery", RoofOf(JobKind.Brewer), 0), ("school", RoofOf(JobKind.Teacher), 0), ("library", RoofOf(BuildingKind.Library), 0),
            })
            {
                foreach (ushort facing in new ushort[] { 0, 10000, 30000, 50000 })
                {
                    var s = new RoofSignature(look, new Vector2(40f, 40f), 1.6f, 0.8f, facing, Colors.White, false, null, new Vector2(40f, 40f), 5);
                    List<RoofPiece> lamps = LampsOf(s);
                    YardLayout layout = LayoutOf(look.Yard, 0.8f, 0.4f, new Vector2(0, 1), new Vector2(0, 0.4f), 5);
                    bool home = IsAHome(look.Yard);
                    Vector2 roofAt = home ? Vector2.Zero : layout.RoofAt;
                    Vector2 eave = new Vector2(home ? 0.8f : layout.RoofHw, home ? 0.4f : layout.RoofHd) + new Vector2(RoofEaves, RoofEaves);
                    Vector2 Local(Vector2 at)
                    {
                        float a = facing * Mathf.Tau / 65536f;
                        Vector2 d = at - s.At;
                        return new Vector2((d.X * Mathf.Cos(a)) + (d.Y * Mathf.Sin(a)), (-d.X * Mathf.Sin(a)) + (d.Y * Mathf.Cos(a))) - roofAt;
                    }

                    float Beyond(Vector2 q) => Mathf.Max(Mathf.Abs(q.X) - eave.X, Mathf.Abs(q.Y) - eave.Y);
                    int fans = lamps.Count(p => p.Kind == RoofPieceKind.Fill);
                    int slits = lamps.Count(p => p.Kind == RoofPieceKind.Line);
                    int off = lamps.Count(p => p.Kind == RoofPieceKind.Fill
                        && (Mathf.Abs(Beyond(Local(p.Points[0]))) > 0.02f || Mathf.Abs(Beyond(Local(p.Points[1]))) > 0.02f || Beyond(Local((p.Points[2] + p.Points[3]) / 2f)) < 0.2f));
                    int ruined = LampsOf(s with { FallenIn = true }).Count;
                    if (fans != want || slits != want || off > 0 || ruined > 0 || lamps.Any(p => !IsLamplight(p)))
                    {
                        faults.Add($"a {what} at turn {facing} has {fans} fans and {slits} slits, {off} not falling out from its eave, {ruined} in a ruin (want {want}, {want}, 0, 0)");
                        break;
                    }
                }
            }

            // Laid with the roof and kept out of its runs: what the house is drawn with by day is what it was.
            var far = new Vector2(-9100f, -9100f);
            RoofCache held = CachedRoof(new RoofSignature(RoofOf(BuildingKind.Home), far, 1.6f, 0.8f, 0, Colors.White, false, null, far, 5));
            int runs = held.Runs.Sum(r => r.Triangles);
            int plain = RoofRuns(RoofDrawing(RoofOf(BuildingKind.Home), far, 1.6f, 0.8f, 0, Colors.White, 1f, null, far, 5, false), _pixelsPerTile).Sum(r => r.Triangles);
            float unseen = held.Light;
            _roofCache.Remove(far);
            if (held.Lamps.Length != 6 || runs != plain || unseen >= 0f)
            {
                faults.Add($"a cabin's cache holds {held.Lamps.Length} pieces of light, {runs} triangles in its runs and a lamp at {unseen:F0} before it is drawn (want 6, {plain}, below nothing)");
            }

            // Who lights a home: one of the family, inside, at their own.
            foreach ((string what, VillagerState state, bool housed, bool inside, bool own, bool want) in new[]
            {
                ("resting at home", VillagerState.Resting, true, false, true, true), ("resting elsewhere", VillagerState.Resting, true, false, false, false),
                ("resting with no house", VillagerState.Resting, false, false, true, false), ("warming inside at home", VillagerState.SeekingShelter, true, true, true, true),
                ("warming, the door shut to them", VillagerState.SeekingShelter, true, false, true, false), ("warming at a neighbour's", VillagerState.SeekingShelter, true, true, false, false),
                ("walking home", VillagerState.TravelingHome, true, true, true, false),
            })
            {
                if (LightsTheHome(state, housed, inside, own) != want)
                {
                    faults.Add($"somebody {what} {(want ? "does not light" : "lights")} the house");
                }
            }

            // The slow lamp: four seconds at a home, one at the rest; a visit of one tick never half lights it; held paused; first drawn, as it is.
            float homeHalf = LampOn(0f, true, 2.0, home: true), tick = LampOn(0f, true, 1.0 / 0.75, home: true), works = LampOn(0f, true, 0.5, home: false);
            if (!Mathf.IsEqualApprox(homeHalf, 0.5f) || !(tick < 0.4f) || !Mathf.IsEqualApprox(works, 0.5f) || LampOn(0.3f, true, 0.0, home: true) != 0.3f
                || LampNow(-1f, true, 0.0, home: true) != 1f || LampNow(-1f, false, 0.0, home: true) != 0f || !Mathf.IsEqualApprox(LampNow(0.5f, false, 2.0, home: true), 0f))
            {
                faults.Add($"a home's lamp: two seconds {homeHalf:F2}, one tick {tick:F2}; the forge's in half a second {works:F2}; paused {LampOn(0.3f, true, 0.0, home: true):F2}; first drawn lit {LampNow(-1f, true, 0.0, home: true):F0}, dark {LampNow(-1f, false, 0.0, home: true):F0} (want ½, under 0.4, ½, 0.30, 1, 0)");
            }

            // The pool is light that fades to nothing before its picture's edge.
            byte[] pool = PaintThePool();
            byte At(int x, int y) => pool[(((y * PoolPixels) + x) * 4) + 3];
            if (At(PoolPixels / 2, PoolPixels / 2) < 240 || Enumerable.Range(0, PoolPixels).Any(i => At(i, 0) + At(0, i) + At(i, PoolPixels - 1) + At(PoolPixels - 1, i) > 0))
            {
                faults.Add("the pool of light is faint at its middle or cut off at its edge");
            }

            return faults.Count == 0
                ? $"[widths] night view: ✅ a map starts by day, every mark to the bit; opened by night it is night at once, a switch fades over a second and back; {MarksAtNight.Count} marks come back from the tint as their own pale colours, the least {leastContrast:F1} off its ground ({least}); ink, fences and walls go dark; people at 0.89 of daylight; three shaders told the night and day again; lamps 3 a cabin, 5 a cottage, 7 a manor, 1 the forge and the oven, none else or in a ruin, out from the eave, out of the runs; one of the family inside lights a home; its lamp takes four seconds, and one first drawn is as it is (A252)"
                : $"[widths] night view: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            return $"[widths] night view: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
        finally
        {
            spare.Free();
        }
    }

    /// <summary>
    /// <c>homes lit:</c> — in a village that has houses: noted as the fires are, a home is lit for everyone of its family resting in
    /// it, and no home is lit that nobody living in it stands in.
    /// </summary>
    internal string TheHomesAreLit()
    {
        SimWorld world = _world!;
        var faults = new List<string>();

        // ⚠️ The probe's village has no house: each family's is stood under one of them, posed resting — and every fact touched is
        // put back before anything ticks (`Main.Shopping.cs`'s way).
        var was = new List<(Household Home, Kith.Sim.Core.Point? At)>();
        var posed = new List<(Villager Who, VillagerState State)>();
        foreach (Household household in world.Households.Where(h => h.HomePosition is null))
        {
            if (world.Villagers.FirstOrDefault(v => v.Alive && v.HouseholdId == household.Id) is Villager under)
            {
                was.Add((household, household.HomePosition));
                posed.Add((under, under.State));
                household.HomePosition = under.Position;
                under.State = VillagerState.Resting;
            }
        }

        NoteTheFires(world);
        var resting = new HashSet<int>();
        var stoodIn = new HashSet<int>();
        int houses = 0, lit = _homesLit.Count;
        foreach (Household household in world.Households)
        {
            if (world.HomeFootprintOf(household) is not Footprint house)
            {
                continue;
            }

            houses++;
            foreach (Villager villager in world.Villagers)
            {
                if (villager.Alive && villager.HouseholdId == household.Id && house.Covers(villager.Tile))
                {
                    stoodIn.Add(household.Id);
                    if (villager.State == VillagerState.Resting)
                    {
                        resting.Add(household.Id);
                    }
                }
            }
        }

        if (houses == 0 || resting.Count == 0)
        {
            faults.Add($"nothing to see: {houses} houses, {resting.Count} with somebody resting at home");
        }

        if (!resting.IsSubsetOf(_homesLit) || !_homesLit.IsSubsetOf(stoodIn) || !_lampsAreNoted)
        {
            faults.Add($"{_homesLit.Count} homes lit, {resting.Count} with somebody resting in them, {stoodIn.Count} with any of the family on their ground");
        }

        // And the same people up and walking: no house is lit that nobody is in.
        foreach ((Villager who, _) in posed)
        {
            who.State = VillagerState.TravelingHome;
        }

        NoteTheFires(world);
        int wrong = posed.Count(p =>
        {
            Footprint house = world.HomeFootprintOf(world.HouseholdOf(p.Who))!.Value;
            bool another = world.Villagers.Any(v => v.Alive && v != p.Who && v.HouseholdId == p.Who.HouseholdId && v.State == VillagerState.Resting && house.Covers(v.Tile));
            return _homesLit.Contains(p.Who.HouseholdId) != another;
        });
        if (wrong > 0 || posed.Count == 0)
        {
            faults.Add($"{wrong} of {posed.Count} houses lit with the one posed in it walking, or dark with another of the family resting in it");
        }

        // Put back: with no house, nobody lights one.
        foreach ((Household home, Kith.Sim.Core.Point? at) in was)
        {
            home.HomePosition = at;
        }

        foreach ((Villager who, VillagerState state) in posed)
        {
            who.State = state;
        }

        NoteTheFires(world);
        if (was.Count > 0 && was.Any(w => _homesLit.Contains(w.Home.Id)))
        {
            faults.Add("a family with no house lights one");
        }

        return faults.Count == 0
            ? $"[widths] homes lit: ✅ posed, {lit} of {houses} houses lit, every one with somebody of its family resting in it ({resting.Count}) and none nobody stands in; the same people walking, {posed.Count} dark unless another rests there; with no house, none"
            : $"[widths] homes lit: ⛔ {string.Join("; ", faults)}";
    }
}
