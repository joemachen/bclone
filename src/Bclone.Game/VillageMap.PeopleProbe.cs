using System.Diagnostics;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

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
                if (ShownAsAPerson(s))
                {
                    faults.Add($"{s} is drawn (want indoors, not drawn)");
                }
            }

            foreach (VillagerState s in new[] { VillagerState.Cutting, VillagerState.TravelingHome, VillagerState.SeekingShelter, VillagerState.Idle, VillagerState.WalkingToSchool })
            {
                if (!ShownAsAPerson(s))
                {
                    faults.Add($"{s} is not drawn (want drawn)");
                }
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
            Pose("cutting, mid-leg", K(VillagerState.Cutting, 1f, 0f), FigurePose.Front, false, FigureHold.Tool, true, false);
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

            return faults.Count == 0
                ? $"[widths] painted villagers: ✅ painted only with the tick; resting, at school and teaching indoors and not drawn; toward, away, from the side and mirrored for the left, at work facing us with the tool out, cold hugging, a load in the arms or on the shoulder, idle talking, an elder's stick, no tool for a child; every trade and the labourer painted in four poses 110 × 100 with the feet at (54.4, 84.48), {bakeMs:F1} ms a painting; the same key the same bytes and another person different; a child 0.62 of an adult; walking left {mirrored:P0} the mirror of walking right; {alike.Count} of 171 trade pairs alike by the gown ({string.Join(", ", alike)}); painted once and kept, mipmapped"
                : $"[widths] painted villagers: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            return $"[widths] painted villagers: ⛔ threw {e.GetType().Name}: {e.Message}";
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
