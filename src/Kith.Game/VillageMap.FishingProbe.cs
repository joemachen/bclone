using Kith.Sim.World;
using Godot;

namespace Kith.Game;

public partial class VillageMap
{
    /// <summary>
    /// ⭐ The probe's <c>fishing:</c> line (A277, <c>fishing-animation.md §5</c>) — printed with the people's lines, so the shell's probe
    /// list is untouched. Every number is the spec's (a cast of 10 ticks at 0.75 a second; the throw 1.4 s, the catch 2 s; quick under
    /// 5 s; half small, a quarter big, a quarter the net), never read back from the code's constants.
    /// </summary>
    /// <remarks>⚠️ Nothing is drawn headless (D340): that the fisher stands on the dock and the line meets the rod is the renders'.</remarks>
    private string TheFishingHolds()
    {
        try
        {
            var faults = new List<string>();
            const double oneX = 1.0 / 0.75, tenX = 1.0 / 7.5;

            // The throw, the wait, the catch — at 1×, a 10-tick cast (13.3 s).
            (float Elapsed, CastVersion Version, int Beat, CastPart Part)[] wanted =
            {
                (0.1f, CastVersion.SmallFish, PersonPainter.RodBack, CastPart.Throw),       // 0.13 s: the rod back
                (0.5f, CastVersion.SmallFish, PersonPainter.RodForward, CastPart.Throw),    // 0.67 s: forward
                (2.0f, CastVersion.SmallFish, PersonPainter.RodWait, CastPart.Wait),        // 2.67 s: waiting (the dip's off-beat)
                (1.2f, CastVersion.SmallFish, PersonPainter.RodDip, CastPart.Wait),         // 1.6 s: the wait opens on a dip
                (8.6f, CastVersion.SmallFish, PersonPainter.RodDip, CastPart.Catch),        // 1.87 s left: the float pulled under
                (8.9f, CastVersion.SmallFish, PersonPainter.RodStrike, CastPart.Catch),     // 1.47 s left: struck
                (8.9f, CastVersion.BigFish, PersonPainter.RodBent, CastPart.Catch),         // a big one bends it
                (9.5f, CastVersion.SmallFish, PersonPainter.RodHaul, CastPart.Catch),       // 0.67 s left: hauled in
                (0.6f, CastVersion.Net, PersonPainter.NetThrow, CastPart.Throw),            // the net: the rod laid by
                (9.5f, CastVersion.Net, PersonPainter.NetThrow, CastPart.Catch),
            };
            foreach ((float elapsed, CastVersion version, int beat, CastPart part) in wanted)
            {
                CastMoment m = CastAt(version, elapsed, 10, oneX);
                if (m.Beat != beat || m.Part != part || m.Quick)
                {
                    faults.Add($"{version} {elapsed} ticks in at 1×: beat {m.Beat} {m.Part}{(m.Quick ? " quick" : "")} (want {beat} {part})");
                }
            }

            // ⭐ At 10× a cast is 1.3 s: nobody throws or hauls — the wait and the dip, and the catch a splash at its end (§1.2).
            var quickBeats = new HashSet<int>();
            bool quickCatch = false;
            for (int i = 0; i < 40; i++)
            {
                CastMoment m = CastAt(CastVersion.SmallFish, i * 0.25f, 10, tenX);
                quickBeats.Add(m.Beat);
                quickCatch |= m.Part == CastPart.Catch && m.Quick;
            }

            if (quickBeats.Any(b => b != PersonPainter.RodWait && b != PersonPainter.RodDip) || !quickCatch)
            {
                faults.Add($"at 10× the beats were {string.Join(",", quickBeats.Order())} (want the wait and the dip only), a quick catch {quickCatch}");
            }

            // ⭐ Shuffled by the person and the cast's start: the same again on a second ask (a reload); half small, a quarter each.
            int small = 0, big = 0, net = 0, differs = 0;
            for (ulong start = 1000; start < 1400; start++)
            {
                CastVersion v = VersionOf(7, start);
                small += v == CastVersion.SmallFish ? 1 : 0;
                big += v == CastVersion.BigFish ? 1 : 0;
                net += v == CastVersion.Net ? 1 : 0;
                differs += VersionOf(8, start) != v ? 1 : 0;
                if (VersionOf(7, start) != v)
                {
                    faults.Add($"cast {start}: asked twice, two versions");
                    break;
                }
            }

            if (small is < 160 or > 240 || big is < 60 or > 140 || net is < 60 or > 140 || differs < 100)
            {
                faults.Add($"400 casts: {small} small, {big} big, {net} nets (want about 200, 100, 100); {differs} differ for the next person (want many)");
            }

            // ⭐ The fisher stands on the dock: along the water's side, between its ends, as far aside as it is (Yards' Dock, §3.3).
            for (int s = 0; s < 4; s++)
            {
                Vector2 way = SideOut(s), at = DockStandLocal(s);
                float along = at.Dot(way), aside = at.Dot(new Vector2(-way.Y, way.X));
                if (along < 0.95f - 0.45f + 0.05f || along > 0.95f + 0.45f || Mathf.Abs(aside - 0.22f) > 1e-4f)
                {
                    faults.Add($"water side {s}: stands {along:F2} out and {aside:F2} aside (want on the dock, 0.55–1.40 out, 0.22 aside)");
                }
            }

            // The rod's tip goes back over the shoulder, then out toward the water; the poses all paint differently; the net's lays
            // the rod by. And an elder fishing holds the rod (the stick everywhere else).
            float back = PersonPainter.RodTip(PersonPainter.RodBack).X, wait = PersonPainter.RodTip(PersonPainter.RodWait).X,
                forward = PersonPainter.RodTip(PersonPainter.RodForward).X;
            if (!(back < wait && wait < forward))
            {
                faults.Add($"the rod's tip: back {back:F2}, waiting {wait:F2}, forward {forward:F2} (want back < waiting < forward)");
            }

            FigureKey fisher = PersonKeyOf(21, LifeStage.Adult, JobKind.Fisher, VillagerState.Fishing, Vector2.Zero, null, false, false);
            var painted = new List<byte[]>();
            for (int beat = 0; beat <= PersonPainter.MostBeats; beat++)
            {
                if (beat is 1 or 2)
                {
                    continue;
                }

                painted.Add(PersonPainter.Paint(fisher with { Beat = beat }).Rgba);
            }

            int alike = 0;
            for (int a = 0; a < painted.Count; a++)
            {
                for (int b = a + 1; b < painted.Count; b++)
                {
                    alike += painted[a].AsSpan().SequenceEqual(painted[b]) ? 1 : 0;
                }
            }

            FigureKey elder = PersonKeyOf(22, LifeStage.Elder, JobKind.Fisher, VillagerState.Fishing, Vector2.Zero, null, false, false);
            FigureKey elderChopping = PersonKeyOf(23, LifeStage.Elder, JobKind.Woodcutter, VillagerState.Cutting, Vector2.Zero, null, false, false);
            bool elderRod = elder.Hold == FigureHold.Tool
                && !PersonPainter.Paint(elder with { Beat = PersonPainter.RodWait }).Rgba.AsSpan().SequenceEqual(PersonPainter.Paint(elder with { Beat = PersonPainter.NetThrow }).Rgba);
            if (alike > 0 || !elderRod || elderChopping.Hold == FigureHold.Tool)
            {
                faults.Add($"{alike} of the cast's {painted.Count} poses paint alike (want none); an elder fishing holds the rod {elderRod}, an elder chopping {elderChopping.Hold} (want the stick)");
            }

            return faults.Count == 0
                ? "[widths] fishing: ✅ at 1× the rod goes back, forward, waits and dips, is struck (bent by a big fish) and hauled in, the net laid out and hauled; at 10× only the wait, the dip and the splash; each cast small, big or the net, half and a quarter each, the same on a second ask and not the next person's; the fisher stands on the dock on every side; the rod's tip back, then out over the water; every pose its own; an elder fishing holds the rod"
                : $"[widths] fishing: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            return $"[widths] fishing: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}
