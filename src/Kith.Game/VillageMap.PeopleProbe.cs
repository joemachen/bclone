using System.Diagnostics;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

public partial class VillageMap
{
    /// <summary>
    /// ⭐ The probe's <c>painted villagers:</c> line (A93, <c>villager-art.md §3b</c>). Every expectation is named here from the
    /// spec — the canvas, the poses, who is indoors — never read back from the code's own tables (art traps 17, 25).
    /// </summary>
    /// <remarks>⚠️ Nothing is drawn headless (D340): how the people <i>look</i> is the renders' and Joe's.</remarks>
    internal string ThePaintedPeopleHold()
    {
        try
        {
            var faults = new List<string>();

            // The tick: people with it on, never with it off.
            bool was = _watercolour;
            _watercolour = false;
            bool off = PaintsPeople;
            _watercolour = true;
            bool on = PaintsPeople;
            _watercolour = was;
            if (off || !on)
            {
                faults.Add($"people with the tick off {off}, on {on} (want false, true)");
            }

            // Indoors and out of sight (A90 call 3): resting at home, at school, teaching; everyone else on the map.
            foreach (VillagerState s in new[] { VillagerState.Resting, VillagerState.AtSchool, VillagerState.Teaching })
            {
                if (ShownAsAPerson(s, housed: true))
                {
                    faults.Add($"{s}, housed, is drawn (want indoors, not drawn)");
                }
            }

            foreach (VillagerState s in new[] { VillagerState.Cutting, VillagerState.TravelingHome, VillagerState.SeekingShelter, VillagerState.Idle, VillagerState.WalkingToSchool })
            {
                if (!ShownAsAPerson(s, housed: true) || !ShownAsAPerson(s, housed: false))
                {
                    faults.Add($"{s} is not drawn (want drawn)");
                }
            }

            // ⭐ Joe's new village (A133): four founders with no house and nothing to do, resting in the open — on the map.
            if (!ShownAsAPerson(VillagerState.Resting, housed: false))
            {
                faults.Add("resting in the open, no house yet, is not drawn (want drawn: they are not indoors)");
            }

            // ⭐ And asked of a real founder, as drawing and clicking ask it: the new game's first villager, no house yet, set
            // resting for the question and put back at once.
            if (_world is { } founded && founded.Villagers.FirstOrDefault(v => v.Alive && !founded.HouseholdOf(v).HasHome) is { } founder)
            {
                VillagerState stateBefore = founder.State;
                founder.State = VillagerState.Resting;
                bool shown;
                try
                {
                    shown = ShownOnTheMap(founded, founder);
                }
                finally
                {
                    founder.State = stateBefore;
                }

                if (!shown)
                {
                    faults.Add($"founder {founder.Id}, resting with no house yet, is not on the map (want drawn and clickable)");
                }
            }
            else
            {
                faults.Add("no founder without a house in the new game to ask (want one)");
            }

            // The poses (§3a): toward, away, from the side and mirrored for the left; at work facing us with the tool out.
            FigureKey K(VillagerState s, float hx, float hy, LifeStage stage = LifeStage.Adult, JobKind? job = JobKind.Woodcutter, Goods? good = null) =>
                PersonKeyOf(7, stage, job, s, new Vector2(hx, hy), good, false, false);
            void Pose(string what, FigureKey k, FigurePose pose, bool flip, FigureHold hold, bool toolOut, bool walking)
            {
                if (k.Pose != pose || k.Flip != flip || k.Hold != hold || k.ToolOut != toolOut || k.Walking != walking)
                {
                    faults.Add($"{what}: {k.Pose}{(k.Flip ? " mirrored" : "")}, {k.Hold}, tool {k.ToolOut}, walking {k.Walking} (want {pose}{(flip ? " mirrored" : "")}, {hold}, tool {toolOut}, walking {walking})");
                }
            }

            Pose("walking down the screen", K(VillagerState.TravelingHome, 0f, 1f), FigurePose.Front, false, FigureHold.None, false, true);
            Pose("walking up the screen", K(VillagerState.TravelingHome, 0f, -1f), FigurePose.Back, false, FigureHold.None, false, true);
            Pose("walking right", K(VillagerState.TravelingHome, 1f, 0.2f), FigurePose.Side, false, FigureHold.None, false, true);
            Pose("walking left", K(VillagerState.TravelingHome, -1f, 0.2f), FigurePose.Side, true, FigureHold.None, false, true);
            Pose("cutting, standing at the tree", K(VillagerState.Cutting, 0f, 0f), FigurePose.Front, false, FigureHold.Tool, true, false);

            // ⭐ A work state that is moving walks (A132, Joe: "these people do not move their legs at all") — the tool in hand.
            Pose("cutting, walking to the next tree", K(VillagerState.Cutting, 1f, 0f), FigurePose.Side, false, FigureHold.Tool, true, true);
            Pose("a labourer clearing, walking tile to tile", K(VillagerState.Clearing, 0f, 1f, job: null), FigurePose.Front, false, FigureHold.None, false, true);
            Pose("walking to the trees", K(VillagerState.TravelingToTrees, 1f, 0f), FigurePose.Side, false, FigureHold.Tool, true, true);
            Pose("cold, going in", K(VillagerState.SeekingShelter, 0f, 1f), FigurePose.Front, false, FigureHold.Hug, false, true);
            Pose("carrying stone home", K(VillagerState.TravelingHome, 0f, 1f, good: Goods.Stone), FigurePose.Front, false, FigureHold.Front, false, true);
            Pose("carrying logs home (on the shoulder)", K(VillagerState.TravelingHome, 0f, 1f, good: Goods.Logs), FigurePose.Front, false, FigureHold.None, false, true);
            Pose("idle, standing", K(VillagerState.Idle, 0f, 0f), FigurePose.Front, false, FigureHold.Talk, false, false);
            Pose("an elder walking", K(VillagerState.TravelingHome, 0f, 1f, LifeStage.Elder, null), FigurePose.Front, false, FigureHold.None, true, true);
            Pose("a child at the trees' work", K(VillagerState.Cutting, 0f, 0f, LifeStage.Child), FigurePose.Front, false, FigureHold.None, false, false);

            // The painting: an adult 64 px tall on a canvas 1.7 × 1.55 of that at a 4× supersample → 110 × 100, the feet at
            // (0.85, 1.32) of 64 = (54.4, 84.48); every trade and the labourer painted, the same key the same bytes.
            JobKind?[] trades = { JobKind.Forager, JobKind.Forester, JobKind.Woodcutter, JobKind.Marketer, JobKind.Builder,
                JobKind.Farmer, JobKind.Fisher, JobKind.Hunter, JobKind.Smith, JobKind.Quarrier, JobKind.Miner, JobKind.Miller,
                JobKind.Baker, JobKind.Herdsman, JobKind.Merchant, JobKind.Tailor, JobKind.Carpenter, JobKind.Teacher, null };
            var watch = Stopwatch.StartNew();
            int painted = 0;
            var gowns = new List<(string Trade, Vector3 Lab)>();
            foreach (JobKind? t in trades)
            {
                foreach ((FigurePose pose, bool flip) in new[] { (FigurePose.Front, false), (FigurePose.Back, false), (FigurePose.Side, false), (FigurePose.Side, true) })
                {
                    var key = new FigureKey(41, LifeStage.Adult, t, pose, flip, FigureHold.None, true, false, null, false, false, false);
                    PaintedFigure f = PersonPainter.Paint(key);
                    painted++;
                    if (f.Width != 110 || f.Height != 100 || Mathf.Abs(f.FeetX - 54.4f) > 0.01f || Mathf.Abs(f.FeetY - 84.48f) > 0.01f)
                    {
                        faults.Add($"{t?.ToString() ?? "a labourer"} {pose} is {f.Width} × {f.Height}, feet ({f.FeetX:F2}, {f.FeetY:F2}) (want 110 × 100, (54.40, 84.48))");
                    }

                    if (pose == FigurePose.Front && !flip)
                    {
                        gowns.Add((t?.ToString() ?? "labourer", GownLab(f)));
                    }
                }
            }

            double bakeMs = watch.Elapsed.TotalMilliseconds / painted;
            var one = new FigureKey(41, LifeStage.Adult, JobKind.Farmer, FigurePose.Front, false, FigureHold.Tool, false, true, null, true, false, false);
            byte[] first = PersonPainter.Paint(one).Rgba, again = PersonPainter.Paint(one).Rgba;
            byte[] someoneElse = PersonPainter.Paint(one with { Id = 42 }).Rgba;
            if (!first.AsSpan().SequenceEqual(again) || first.AsSpan().SequenceEqual(someoneElse))
            {
                faults.Add($"the same key painted twice the same {first.AsSpan().SequenceEqual(again)}, another person different {!first.AsSpan().SequenceEqual(someoneElse)} (want true, true)");
            }

            // ⭐ Cloaks dyed by household (A143, villager-art.md §9) — the seven named from the spec, not read back from the painter.
            string[] dyes = { "#9a4a3c", "#4a6488", "#b8a050", "#56703f", "#6c5070", "#6e5238", "#9a948a" };
            for (int d = 0; d < dyes.Length; d++)
            {
                PersonPainter.Rgb want = PersonPainter.Rgb.Hex(dyes[d]);
                PersonPainter.Rgb got = PersonPainter.CloakOf(d);
                if (got.R != want.R || got.G != want.G || got.B != want.B)
                {
                    faults.Add($"dye {d} is not {dyes[d]}");
                }
            }

            var founding = new HashSet<int>();
            for (int h = 1; h <= 7; h++)
            {
                founding.Add(CloakDyeOf(h));
            }

            if (founding.Count != 7 || CloakDyeOf(8) != CloakDyeOf(1) || CloakDyeOf(1) != 0)
            {
                faults.Add($"seven households in founding order share dyes ({founding.Count} of 7), or the eighth is not the first's again");
            }

            byte[] cloakA = PersonPainter.Paint(one).Rgba, cloakB = PersonPainter.Paint(one with { Dye = 1 }).Rgba;
            var bare = one with { Coat = false };
            byte[] bareA = PersonPainter.Paint(bare).Rgba, bareB = PersonPainter.Paint(bare with { Dye = 1 }).Rgba;
            if (cloakA.AsSpan().SequenceEqual(cloakB) || !bareA.AsSpan().SequenceEqual(bareB))
            {
                faults.Add($"two dyes paint the same cloak {cloakA.AsSpan().SequenceEqual(cloakB)}, or the dye changes someone with no coat {!bareA.AsSpan().SequenceEqual(bareB)} (want false, false)");
            }

            // ⚠️ And every part of the cloak takes the dye (A143's red-check: one panel left moss scored zero against the whole
            // picture differing). A woad cloak, seen from the front, the side and the back, keeps no pixel of A93's moss or its shades.
            PersonPainter.Rgb moss = PersonPainter.Rgb.Hex("#56703f");
            foreach (FigurePose cloakPose in new[] { FigurePose.Front, FigurePose.Side, FigurePose.Back })
            {
                byte[] woad = PersonPainter.Paint(one with { Dye = 1, Pose = cloakPose }).Rgba;
                int mossy = 0;
                for (int px = 0; px + 3 < woad.Length; px += 4)
                {
                    if (woad[px + 3] < 200)
                    {
                        continue;
                    }

                    foreach (float k in new[] { 1f, 0.85f, 0.65f })
                    {
                        if (Mathf.Abs(woad[px] - (moss.R * k)) < 5 && Mathf.Abs(woad[px + 1] - (moss.G * k)) < 5 && Mathf.Abs(woad[px + 2] - (moss.B * k)) < 5)
                        {
                            mossy++;
                            break;
                        }
                    }
                }

                if (mossy > 0)
                {
                    faults.Add($"a woad cloak from the {cloakPose} keeps {mossy} moss pixels (want none)");
                }
            }

            // A child stands 0.62 of an adult; mirrored for the left, the person's weight sits on the other side of the feet.
            // ⚠️ Each from its own feet: a child is painted on a smaller canvas (the first version of this check measured the
            // child from an adult's feet and scored zero when the child was made 0.8 — A93's red-check).
            PaintedFigure adult = PersonPainter.Paint(new FigureKey(41, LifeStage.Adult, null, FigurePose.Front, false, FigureHold.None, false, false, null, false, false, false));
            PaintedFigure child = PersonPainter.Paint(new FigureKey(41, LifeStage.Child, null, FigurePose.Front, false, FigureHold.None, false, false, null, false, false, false));
            float childTall = child.FeetY - TopRow(child);
            float adultTall = adult.FeetY - TopRow(adult);
            if (Mathf.Abs((childTall / adultTall) - 0.62f) > 0.08f)
            {
                faults.Add($"a child stands {childTall / adultTall:F2} of an adult (want 0.62 ± 0.08)");
            }

            var side = new FigureKey(41, LifeStage.Adult, JobKind.Woodcutter, FigurePose.Side, false, FigureHold.Front, true, false, Goods.Stone, false, false, false);
            float mirrored = MirrorOverlap(PersonPainter.Paint(side), PersonPainter.Paint(side with { Flip = true }));
            if (mirrored < 0.9f)
            {
                faults.Add($"walking left is {mirrored:P0} the mirror of walking right about the feet (want 90% or more)");
            }

            // The trades apart by the gown alone (the measure §3 used, A85's threshold): pairs whose gowns differ by ΔE < 10.
            var alike = new List<string>();
            for (int a = 0; a < gowns.Count; a++)
            {
                for (int b = a + 1; b < gowns.Count; b++)
                {
                    if (gowns[a].Lab.DistanceTo(gowns[b].Lab) < 10f)
                    {
                        alike.Add($"{gowns[a].Trade}/{gowns[b].Trade}");
                    }
                }
            }

            if (alike.Count > PeopleGownsAlikeAtMost)
            {
                faults.Add($"{alike.Count} trade pairs alike by the gown: {string.Join(", ", alike)} (want at most {PeopleGownsAlikeAtMost})");
            }

            // Kept: the same key a second time is the same texture, painted once.
            var probeKey = new FigureKey(-41, LifeStage.Adult, JobKind.Baker, FigurePose.Front, false, FigureHold.None, false, false, null, false, false, false);
            int bakesBefore = _personBakes;
            ulong frame = Engine.GetProcessFrames();
            int budget = _bakesThisFrame;
            PersonCache? firstHeld = CachedPerson(probeKey, frame, true), secondHeld = CachedPerson(probeKey, frame, true);
            _bakesThisFrame = budget;
            bool kept = firstHeld is not null && ReferenceEquals(firstHeld, secondHeld) && _personBakes == bakesBefore + 1
                && firstHeld.Texture.TextureFilter == TextureFilterEnum.LinearWithMipmaps
                && firstHeld.Texture.DiffuseTexture.GetImage().HasMipmaps();
            _people.Remove(probeKey);
            if (!kept)
            {
                faults.Add($"the cache: the same person {(ReferenceEquals(firstHeld, secondHeld) ? "kept" : "painted again")}, {_personBakes - bakesBefore} paintings (want kept, 1), mipmapped and drawn with mipmaps");
            }

            string moving = ThePeopleMoveHold() + "\n" + TheDoorsAndTheSmokeHold() + "\n" + TheBrewerAndKeeperHold() + "\n" + ThePaperInHandHolds() + "\n" + TheFishingHolds() + "\n" + TheFaceWorkHolds() + "\n" + TheWoodcutterHolds() + "\n" + TheForesterHolds() + "\n" + TheOutdoorWorkHolds() + "\n" + TheAnimalsHold() + "\n" + TheTreeSwayHolds() + "\n" + TheGroundFadeHolds();
            return faults.Count == 0
                ? $"[widths] painted villagers: ✅ painted only with the tick; cloaks in seven dyes by household, in founding order, none on the coatless (A143); resting at home, at school and teaching indoors and not drawn, resting in the open before the first house on the map (a founder asked); toward, away, from the side and mirrored for the left, at work facing us with the tool out and walking with it between the work, cold hugging, a load in the arms or on the shoulder, idle talking, an elder's stick, no tool for a child; every trade and the labourer painted in four poses 110 × 100 with the feet at (54.4, 84.48), {bakeMs:F1} ms a painting; the same key the same bytes and another person different; a child 0.62 of an adult; walking left {mirrored:P0} the mirror of walking right; {alike.Count} of 171 trade pairs alike by the gown ({string.Join(", ", alike)}); painted once and kept, mipmapped\n{moving}"
                : $"[widths] painted villagers: ⛔ {string.Join("; ", faults)}\n{moving}";
        }
        catch (System.Exception e)
        {
            return $"[widths] painted villagers: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }

    /// <summary>
    /// ⭐ The probe's <c>people moving:</c> line (A130, <c>villager-animation.md §3</c>, §7) — printed with the line above, so
    /// the shell's probe list is untouched. Every number is the spec's, never read back from the code's constants.
    /// </summary>
    /// <remarks>⚠️ Nothing is drawn headless (D340): that the draw uses the transform and the shadow is the renders' and Joe's.</remarks>
    private string ThePeopleMoveHold()
    {
        try
        {
            var faults = new List<string>();
            const float adultStep = 0.36f;
            const double frame = 1.0 / 60.0;

            // The step is paid for by distance: at 1× (0.75 tiles a second) the feet keep to the ground.
            float oneX = WalkedOn(5f, 0.75f * (float)frame, adultStep, frame) - 5f;
            if (Mathf.Abs(oneX - (0.75f * (float)frame)) > 1e-5f)
            {
                faults.Add($"a 1× frame walked {oneX:F5} tiles (want {0.75 * frame:F5}, the ground moved)");
            }

            // Paused: no step at all, whatever the point did. A jump (a birth, a move of house) is not walked.
            float paused = WalkedOn(5f, 0.05f, adultStep, 0.0), jumped = WalkedOn(5f, 3f, adultStep, frame);
            if (paused != 5f || jumped != 5f)
            {
                faults.Add($"paused walked {paused - 5f:F4}, a 3-tile jump walked {jumped - 5f:F4} (want 0, 0)");
            }

            // ⭐ At 4× (3 tiles a second) a second of frames is at most 2.4 steps (Joe's call 2), not 8.
            float fourX = 0f;
            for (int k = 0; k < 60; k++)
            {
                fourX = WalkedOn(fourX, 3f * (float)frame, adultStep, frame);
            }

            if (Mathf.Abs((fourX / adultStep) - 2.4f) > 0.01f)
            {
                faults.Add($"a second at 4× is {fourX / adultStep:F2} steps (want 2.40)");
            }

            // The clock: the game's speed up to 1.5×, still while paused (Joe's call 3).
            (double clockWas, double secondsWas, double paceWas, bool pausedWas) = (_peopleClock, _peopleFrameSeconds, _flowPace, Paused);
            var clocked = new List<double>();
            foreach ((double pace, bool stop) in new[] { (1.0, false), (10.0, false), (4.0, true) })
            {
                _peopleClock = 0;
                _flowPace = pace;
                Paused = stop;
                TickThePeopleClock(0.5);
                clocked.Add(_peopleClock);
            }

            (_peopleClock, _peopleFrameSeconds, _flowPace, Paused) = (clockWas, secondsWas, paceWas, pausedWas);
            if (Math.Abs(clocked[0] - 0.5) > 1e-9 || Math.Abs(clocked[1] - 0.75) > 1e-9 || clocked[2] != 0.0)
            {
                faults.Add($"half a second at 1×, 10× and paused is {clocked[0]:F2}, {clocked[1]:F2}, {clocked[2]:F2} s of the people's clock (want 0.50, 0.75, 0.00)");
            }

            // ⭐ Which way they walk is which way the drawn point moves (A132): it turns them; a pause keeps them as they were;
            // a slow frame does not stand them; a quarter second still does; a jump is not a walk.
            (Vector2 h1, float s1) = HeadingOn(Vector2.Zero, StandAfterSeconds, new Vector2(0.01f, 0f), frame);
            (Vector2 h2, float s2) = HeadingOn(h1, s1, Vector2.Zero, 0.0);
            (Vector2 h3, float s3) = HeadingOn(h1, s1, Vector2.Zero, 0.1);
            (Vector2 h4, _) = HeadingOn(h3, s3, Vector2.Zero, 0.2);
            (Vector2 h5, _) = HeadingOn(Vector2.Zero, StandAfterSeconds, new Vector2(0f, 3f), frame);
            if (h1 != Vector2.Right || h2 != h1 || s2 != s1 || h3 != h1 || h4 != Vector2.Zero || h5 != Vector2.Zero)
            {
                faults.Add($"the heading: moving right {h1}, paused {h2}, a tenth still {h3}, then a fifth more {h4}, a jump {h5} (want (1, 0), (1, 0), (1, 0), (0, 0), (0, 0))");
            }

            // ⭐ Joe's case, end to end: a labourer clearing, gliding a tile down the screen at 1× — the legs change.
            Vector2 gone = Vector2.Zero, way = Vector2.Zero;
            float walkedOn = 0f, stillOn = StandAfterSeconds;
            var clearingBeats = new HashSet<int>();
            for (int k = 0; k < 80; k++)
            {
                Vector2 step = new(0f, 0.75f * (float)frame);
                walkedOn = WalkedOn(walkedOn, step.Length(), StepTiles, frame);
                (way, stillOn) = HeadingOn(way, stillOn, step, frame);
                gone += step;
                FigureKey clearing = PersonKeyOf(11, LifeStage.Adult, null, VillagerState.Clearing, way, null, false, false);
                clearingBeats.Add(MotionOf(clearing, true, walkedOn, k * frame, true).Beat);
            }

            if (clearingBeats.Count != 2 || !clearingBeats.Contains(0) || !clearingBeats.Contains(1))
            {
                faults.Add($"a labourer clearing, walking {gone.Y:F2} tiles at 1×, shows beats {string.Join(", ", clearingBeats)} (want 0 and 1)");
            }

            // Walking: the two steps alternate, toward us and from the side; the body rises 2.2 % of its height, a half more under a load.
            FigureKey walker = PersonKeyOf(7, LifeStage.Adult, JobKind.Woodcutter, VillagerState.TravelingHome, new Vector2(0f, 1f), null, false, false);
            FigureKey sideways = walker with { Pose = FigurePose.Side };
            FigureKey loaded = walker with { Good = Goods.Stone };
            var beatsToward = new HashSet<int>();
            var beatsSide = new HashSet<int>();
            float highest = 0f, highestLoaded = 0f;
            for (int k = 0; k < 72; k++)
            {
                float w = k * 0.01f;
                PersonMotion m = MotionOf(walker, false, w, 0, true);
                beatsToward.Add(m.Beat);
                beatsSide.Add(MotionOf(sideways, false, w, 0, true).Beat);
                highest = Mathf.Min(highest, m.Dy);
                highestLoaded = Mathf.Min(highestLoaded, MotionOf(loaded, false, w, 0, true).Dy);
            }

            if (!beatsToward.SetEquals(new[] { 0, 1 }) || !beatsSide.SetEquals(new[] { 0, 1 }))
            {
                faults.Add($"two steps walked show poses {{{string.Join(",", beatsToward)}}} toward us, {{{string.Join(",", beatsSide)}}} from the side (want {{0,1}} each)");
            }

            if (Mathf.Abs(highest + 0.022f) > 0.002f || Mathf.Abs(highestLoaded + 0.032f) > 0.002f)
            {
                faults.Add($"a step rises {-highest:F3}, under a load {-highestLoaded:F3} of the height (want 0.022, 0.032)");
            }

            // ⭐ Toward or away the legs show least, so the weight goes foot to foot: a tilt each step there, none from the
            // side (A130, after Joe's play: "their legs look stationary").
            float tiltToward = 0f, tiltSide = 0f;
            for (int k = 0; k < 72; k++)
            {
                tiltToward = Mathf.Max(tiltToward, Mathf.Abs(MotionOf(walker, false, k * 0.01f, 0, true).Turn));
                tiltSide = Mathf.Max(tiltSide, Mathf.Abs(MotionOf(sideways, false, k * 0.01f, 0, true).Turn));
            }

            if (Mathf.Abs(tiltToward - 1.3f) > 0.1f || tiltSide != 0f)
            {
                faults.Add($"walking toward us tilts {tiltToward:F2}°, from the side {tiltSide:F2}° (want 1.3°, 0°)");
            }

            // At work with an axe: the tool raised, then the stroke — about 38 % of a 1.6 s beat; the crew out of step (D28).
            FigureKey chopping = PersonKeyOf(7, LifeStage.Adult, JobKind.Woodcutter, VillagerState.Cutting, Vector2.Zero, null, false, false);
            FigureKey crewmate = PersonKeyOf(8, LifeStage.Adult, JobKind.Woodcutter, VillagerState.Cutting, Vector2.Zero, null, false, false);
            int strokes = 0, together = 0, samples = 160;
            var workBeats = new HashSet<int>();
            for (int k = 0; k < samples; k++)
            {
                double t = k * 1.6 / samples;
                int a = MotionOf(chopping, true, 0f, t, true).Beat, b = MotionOf(crewmate, true, 0f, t, true).Beat;
                workBeats.Add(a);
                strokes += a == 2 ? 1 : 0;
                together += a == b ? 1 : 0;
            }

            if (!workBeats.SetEquals(new[] { 1, 2 }) || Mathf.Abs(((float)strokes / samples) - 0.38f) > 0.05f)
            {
                faults.Add($"a woodcutter's beat shows poses {{{string.Join(",", workBeats)}}}, the stroke {(float)strokes / samples:P0} of it (want {{1,2}}, 38%)");
            }

            if (together == samples)
            {
                faults.Add("two woodcutters chop in step all the beat (want out of step)");
            }

            // Too small to see a pose (under 16 px): one pose only, the picture moved instead — a walk tilts, a swing leans.
            bool farOnePose = true;
            float farTilt = 0f, farLean = 0f;
            for (int k = 0; k < 40; k++)
            {
                PersonMotion w = MotionOf(walker, false, k * 0.02f, 0, false), c = MotionOf(chopping, true, 0f, k * 0.04, false);
                farOnePose &= w.Beat == 0 && c.Beat == 0;
                farTilt = Mathf.Max(farTilt, Mathf.Abs(w.Turn));
                farLean = Mathf.Max(farLean, Mathf.Abs(c.Turn));
            }

            if (!farOnePose || farTilt < 1f || farLean < 1.5f)
            {
                faults.Add($"far out: one pose {farOnePose}, the walk tilts {farTilt:F1}°, the swing leans {farLean:F1}° (want true, ~1.3°, ~2.2°)");
            }

            // A tailor at work has no tool that swings: one pose, leaning into the work.
            FigureKey sewing = PersonKeyOf(9, LifeStage.Adult, JobKind.Tailor, VillagerState.WorkingABatch, Vector2.Zero, null, false, false);
            bool sewingOnePose = true;
            float sewingLean = 0f;
            for (int k = 0; k < 40; k++)
            {
                PersonMotion m = MotionOf(sewing, true, 0f, k * 0.08, true);
                sewingOnePose &= m.Beat == 0;
                sewingLean = Mathf.Max(sewingLean, Mathf.Abs(m.Turn));
            }

            if (!sewingOnePose || sewingLean < 1.5f)
            {
                faults.Add($"a tailor at work: one pose {sewingOnePose}, leaning {sewingLean:F1}° (want true, ~2.2°)");
            }

            // The cold shake; nobody else shakes.
            FigureKey cold = PersonKeyOf(7, LifeStage.Adult, JobKind.Fisher, VillagerState.SeekingShelter, new Vector2(0f, 1f), null, false, false);
            float shake = Mathf.Abs(MotionOf(cold, false, 0f, 0.1, true).Dx - MotionOf(cold, false, 0f, 0.3, true).Dx);
            float calm = Mathf.Abs(MotionOf(walker, false, 0f, 0.1, true).Dx) + Mathf.Abs(MotionOf(walker, false, 0f, 0.3, true).Dx);
            if (shake < 0.01f || calm != 0f)
            {
                faults.Add($"the cold shake moves {shake:F3}, a warm walker {calm:F3} of the height (want ~0.02, 0)");
            }

            // The poses painted are different pictures on the same canvas: the other step, the raised axe and the stroke, the palm lowered.
            var axe = new FigureKey(41, LifeStage.Adult, JobKind.Woodcutter, FigurePose.Front, false, FigureHold.Tool, false, true, null, false, false, false);
            var stepSide = new FigureKey(41, LifeStage.Adult, JobKind.Woodcutter, FigurePose.Side, false, FigureHold.None, true, false, null, false, false, false);
            var stepToward = stepSide with { Pose = FigurePose.Front };
            var talk = new FigureKey(41, LifeStage.Adult, JobKind.Baker, FigurePose.Front, false, FigureHold.Talk, false, false, null, false, false, false);
            var differ = new List<string>();
            foreach ((string what, FigureKey a, FigureKey b) in new[]
            {
                ("the axe raised / at rest", axe with { Beat = 1 }, axe), ("the stroke / the axe raised", axe with { Beat = 2 }, axe with { Beat = 1 }),
                ("the other step from the side", stepSide with { Beat = 1 }, stepSide), ("the other step toward us", stepToward with { Beat = 1 }, stepToward),
                ("the palm lowered", talk with { Beat = 1 }, talk),
            })
            {
                PaintedFigure pa = PersonPainter.Paint(a), pb = PersonPainter.Paint(b);
                if (pa.Width != pb.Width || pa.Height != pb.Height || pa.Rgba.AsSpan().SequenceEqual(pb.Rgba))
                {
                    differ.Add(what);
                }
            }

            if (differ.Count > 0)
            {
                faults.Add($"the same picture for {string.Join(", ", differ)} (want a different pose on the same canvas)");
            }

            // ⚠️ And the legs themselves moved — the check above passes on the arm or the other foot alone (A130's red-check
            // scored it zero twice): from the side, the feet's rows differ; toward us, the right foot's (the one beat 1 lifts).
            int sideFeet = FeetPixelsDiffering(PersonPainter.Paint(stepSide with { Beat = 1 }), PersonPainter.Paint(stepSide), rightOnly: false);
            int towardRight = FeetPixelsDiffering(PersonPainter.Paint(stepToward with { Beat = 1 }), PersonPainter.Paint(stepToward), rightOnly: true);
            var gowned = stepToward with { Trade = JobKind.Merchant };
            int gownedRight = FeetPixelsDiffering(PersonPainter.Paint(gowned with { Beat = 1 }), PersonPainter.Paint(gowned), rightOnly: true);
            if (sideFeet < 10 || towardRight < 10 || gownedRight < 10)
            {
                faults.Add($"the other step moves {sideFeet} pixels of the feet from the side, {towardRight} of the right foot toward us, {gownedRight} in an ankle-length gown (want 10 or more each)");
            }

            // ⭐ …and SEEN at the game's size (A131, Joe: "their legs look stationary"; HANDOFF art trap 60). Shrunk to the
            // closest zoom (34.6 px tall) on grass, the step toward us changes about as much of the screen as the step from
            // the side does (measured 48–107 there): at least 40 pixels — for a labourer, an ankle-length gown, an elder.
            // A131 measured 19, 6 and 4 before the fix.
            var elderToward = stepToward with { Stage = LifeStage.Elder, Trade = JobKind.Farmer, ToolOut = true };
            var seen = new List<(string Who, int Pixels)>();
            foreach ((string what, FigureKey k) in new[] { ("a labourer", stepToward with { Trade = null }), ("a merchant's gown", gowned), ("an elder", elderToward) })
            {
                seen.Add((what, VisibleChange(PersonPainter.Paint(k), PersonPainter.Paint(k with { Beat = 1 }), 0.72f * 48f)));
            }

            if (seen.Any(s => s.Pixels < 40))
            {
                faults.Add($"walking toward us at the closest zoom the other step changes {string.Join(", ", seen.Select(s => $"{s.Pixels} px for {s.Who}"))} (want 40 or more each, as from the side)");
            }

            // A long pause shows one pose for minutes; the other is kept while it is (A130, after play), not let go at 600 frames.
            ulong at = Engine.GetProcessFrames();
            var stillOne = new FigureKey(-43, LifeStage.Adult, JobKind.Fisher, FigurePose.Front, false, FigureHold.None, true, false, null, false, false, false);
            int budgetWas = _bakesThisFrame;
            ulong sweptWas = _peopleSwept;
            PersonCache? stepA = CachedPerson(stillOne, at, true), stepB = CachedPerson(stillOne with { Beat = 1 }, at, true);
            if (stepA is not null)
            {
                stepA.LastSeen = at + 700;
            }

            KeepTheOtherPoses(stillOne, at + 700);
            _peopleSwept = 0;
            ForgetUnseenPeople(at + 700);
            bool otherKept = stepB is not null && _people.ContainsKey(stillOne with { Beat = 1 });
            _people.Remove(stillOne);
            _people.Remove(stillOne with { Beat = 1 });
            (_bakesThisFrame, _peopleSwept) = (budgetWas, sweptWas);
            if (!otherKept)
            {
                faults.Add("the other step of a person shown 700 frames in one pose was let go (want kept while any of their poses is drawn)");
            }

            // ⭐ The shadow is its own painting, under the feet to the lower right, and no longer in a person's (A130).
            PaintedFigure shade = PersonPainter.PaintShadow(), person = PersonPainter.Paint(stepToward);
            int sx = (int)(shade.FeetX + (0.28f * 64f)), sy = (int)(shade.FeetY + (0.03f * 64f));
            byte inShadow = shade.Rgba[(((sy * shade.Width) + sx) * 4) + 3], inPerson = person.Rgba[(((sy * person.Width) + sx) * 4) + 3];
            if (shade.Width != person.Width || inShadow < 40 || inPerson != 0)
            {
                faults.Add($"the shadow {shade.Width} wide (a person {person.Width}); at the feet's lower right the shadow's alpha {inShadow}, a person's {inPerson} (want the same canvas, 40 or more, 0)");
            }

            return faults.Count == 0
                ? $"[widths] people moving: ✅ a step paid by the ground walked ({oneX * 60f:F2} tiles a second at 1×), none paused, none for a jump, at most 2.4 steps a second at 4×; the people's clock 1×, capped at 1.5×, still when paused; which way they walk the way the drawn point moves, kept through a pause and a slow frame, a quarter second still and they stand; a labourer clearing at 1× stepping; two steps alternating toward us and from the side, a tilt foot to foot toward us and none from the side, rising {-highest:F3} of the height, {-highestLoaded:F3} under a load; a woodcutter's axe raised then struck ({(float)strokes / samples:P0} of the beat), a crew out of step; far out one pose, the walk tilting and the swing leaning; a tailor leaning into the work; the cold shaking and nobody else; five poses each a different picture and the feet moved, in a long gown too; the step toward us seen at the closest zoom ({string.Join(", ", seen.Select(s => $"{s.Pixels} px for {s.Who}"))}); the other pose kept through a long pause; the shadow its own painting, out of a person's"
                : $"[widths] people moving: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            return $"[widths] people moving: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }

    /// <summary>The most trade pairs whose gowns may look alike (ΔE &lt; 10) before the probe calls it a regression.</summary>
    /// <remarks>
    /// Five as built (A93): woodcutter/marketer, marketer/carpenter, quarrier/labourer, miner/herdsman, miller/baker (the
    /// last two in linen aprons) — every one of them apart by the hat or the tool. The mocks' B confused four by colour alone.
    /// </remarks>
    private const int PeopleGownsAlikeAtMost = 5;

    /// <summary>The gown's mean colour in CIE Lab, over its middle below the belt (x within 0.07, y from -0.42 to -0.30 of the height).</summary>
    private static Vector3 GownLab(PaintedFigure f)
    {
        double r = 0, g = 0, b = 0;
        int n = 0;
        for (int y = (int)(f.FeetY - (0.42f * 64f)); y <= (int)(f.FeetY - (0.30f * 64f)); y++)
        {
            for (int x = (int)(f.FeetX - (0.07f * 64f)); x <= (int)(f.FeetX + (0.07f * 64f)); x++)
            {
                int k = ((y * f.Width) + x) * 4;
                if (f.Rgba[k + 3] < 250)
                {
                    continue;
                }

                r += f.Rgba[k];
                g += f.Rgba[k + 1];
                b += f.Rgba[k + 2];
                n++;
            }
        }

        return n == 0 ? Vector3.Zero : Lab((float)(r / n / 255), (float)(g / n / 255), (float)(b / n / 255));
    }

    private static Vector3 Lab(float r, float g, float b)
    {
        static float Lin(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        static float F(float t) => t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : (7.787f * t) + (16f / 116f);
        float lr = Lin(r), lg = Lin(g), lb = Lin(b);
        float x = ((0.4124f * lr) + (0.3576f * lg) + (0.1805f * lb)) / 0.95047f;
        float y = (0.2126f * lr) + (0.7152f * lg) + (0.0722f * lb);
        float z = ((0.0193f * lr) + (0.1192f * lg) + (0.9505f * lb)) / 1.08883f;
        return new Vector3((116f * F(y)) - 16f, 500f * (F(x) - F(y)), 200f * (F(y) - F(z)));
    }

    /// <summary>
    /// How many pixels of the feet (the bottom 0.15 of an adult's height, to the feet) are solid in one picture and not the
    /// other — on the right of the feet only, if asked.
    /// </summary>
    private static int FeetPixelsDiffering(PaintedFigure a, PaintedFigure b, bool rightOnly)
    {
        int n = 0;
        for (int y = (int)(a.FeetY - (0.15f * 64f)); y < a.Height && y <= (int)a.FeetY + 1; y++)
        {
            for (int x = rightOnly ? (int)a.FeetX + 1 : 0; x < a.Width; x++)
            {
                int k = (((y * a.Width) + x) * 4) + 3;
                n += (a.Rgba[k] >= 128) != (b.Rgba[k] >= 128) ? 1 : 0;
            }
        }

        return n;
    }

    /// <summary>
    /// How many screen pixels visibly differ between two paintings of one person drawn <paramref name="tall"/> pixels tall
    /// (an adult's painting is 64): each shrunk by averaging its pixels (premultiplied), laid on summer grass, and a pixel
    /// counted when its brightness differs by more than 24 of 255 — A131's bench, in the probe.
    /// </summary>
    private static int VisibleChange(PaintedFigure a, PaintedFigure b, float tall)
    {
        float k = 64f / tall;
        int w = (int)MathF.Ceiling(a.Width / k), h = (int)MathF.Ceiling(a.Height / k);
        float Shown(PaintedFigure f, int x, int y)
        {
            float r = 0f, g = 0f, bl = 0f, al = 0f;
            int n = 0;
            for (int sy = (int)(y * k); sy < Math.Min(f.Height, (int)((y + 1) * k)); sy++)
            {
                for (int sx = (int)(x * k); sx < Math.Min(f.Width, (int)((x + 1) * k)); sx++)
                {
                    int i = ((sy * f.Width) + sx) * 4;
                    float alpha = f.Rgba[i + 3] / 255f;
                    r += f.Rgba[i] * alpha;
                    g += f.Rgba[i + 1] * alpha;
                    bl += f.Rgba[i + 2] * alpha;
                    al += alpha;
                    n++;
                }
            }

            if (n == 0)
            {
                return 0f;
            }

            float cover = al / n;
            float R = (r / n) + (122f * (1f - cover)), G = (g / n) + (140f * (1f - cover)), B = (bl / n) + (72f * (1f - cover));
            return (0.299f * R) + (0.587f * G) + (0.114f * B);
        }

        int changed = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                changed += MathF.Abs(Shown(a, x, y) - Shown(b, x, y)) > 24f ? 1 : 0;
            }
        }

        return changed;
    }

    /// <summary>The first row with anything solid in it.</summary>
    private static int TopRow(PaintedFigure f)
    {
        for (int y = 0; y < f.Height; y++)
        {
            for (int x = 0; x < f.Width; x++)
            {
                if (f.Rgba[(((y * f.Width) + x) * 4) + 3] >= 200)
                {
                    return y;
                }
            }
        }

        return f.Height;
    }

    /// <summary>How much of <paramref name="a"/>'s solid silhouette <paramref name="b"/> covers once turned about the feet (intersection over union).</summary>
    private static float MirrorOverlap(PaintedFigure a, PaintedFigure b)
    {
        int both = 0, either = 0;
        for (int y = 0; y < a.Height; y++)
        {
            for (int x = 0; x < a.Width; x++)
            {
                int mx = (int)Mathf.Round((2f * a.FeetX) - x - 1f);
                bool inA = a.Rgba[(((y * a.Width) + x) * 4) + 3] >= 128;
                bool inB = mx >= 0 && mx < b.Width && b.Rgba[(((y * b.Width) + mx) * 4) + 3] >= 128;
                both += inA && inB ? 1 : 0;
                either += inA || inB ? 1 : 0;
            }
        }

        return either == 0 ? 0f : (float)both / either;
    }
}
