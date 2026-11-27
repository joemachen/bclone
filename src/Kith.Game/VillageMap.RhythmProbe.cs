using Kith.Sim.World;
using Godot;

namespace Kith.Game;

public partial class VillageMap
{
    /// <summary>
    /// ⭐ The probe's <c>outdoor work:</c> line (A297, <c>varied-work.md §9</c>) — printed with the people's lines. Every number is the
    /// spec's (a breather about one phrase in six; the forager's drop every phrase; the builder's plank two in five; the hunter's loose
    /// every phrase), never read back from the code's constants.
    /// </summary>
    /// <remarks>⚠️ Nothing is drawn headless (D340): the seed in the air and the arrow away are the renders'.</remarks>
    private string TheOutdoorWorkHolds()
    {
        try
        {
            var faults = new List<string>();
            (JobKind Trade, VillagerState State, string Name)[] trades =
            {
                (JobKind.Forager, VillagerState.Gathering, "forager"), (JobKind.Builder, VillagerState.Building, "builder"),
                (JobKind.Farmer, VillagerState.Sowing, "sower"), (JobKind.Farmer, VillagerState.Reaping, "reaper"),
                (JobKind.Hunter, VillagerState.Hunting, "hunter"), (JobKind.Herdsman, VillagerState.CuttingHay, "haymaker"),
            };

            // Over 20 minutes of the people's clock, sampled every 0.05 s: some rest, mostly work, the second motions where they belong.
            foreach ((JobKind trade, VillagerState state, string name) in trades)
            {
                int rest = 0, work = 0, second = 0, n = 0;
                for (double c = 0; c < 1200.0; c += 0.05)
                {
                    if (RhythmAt(trade, state, 61, c) is not RhythmMoment m)
                    {
                        continue;
                    }

                    n++;
                    rest += m.Part == RhythmPart.Rest ? 1 : 0;
                    work += m.Part == RhythmPart.Work ? 1 : 0;
                    second += m.Part == RhythmPart.Second ? 1 : 0;
                }

                bool hasSecond = trade is JobKind.Forager or JobKind.Builder or JobKind.Hunter;
                float restShare = n == 0 ? 0f : rest / (float)n, secondShare = n == 0 ? 0f : second / (float)n;
                if (n == 0 || restShare is < 0.01f or > 0.12f || work < n / 2 || (hasSecond ? secondShare < 0.02f : second > 0))
                {
                    faults.Add($"{name}: {restShare:P0} resting, {work * 100 / Math.Max(1, n)} % at work, {secondShare:P0} its own motion (want a little rest, mostly work{(hasSecond ? ", some of its own" : ", none")})");
                }
            }

            // Not every trade: a woodcutter's swing and a teacher's book are not phrased here; the same moment on a second ask.
            if (RhythmAt(JobKind.Woodcutter, VillagerState.MakingFirewood, 61, 3.0) is not null || RhythmAt(JobKind.Teacher, VillagerState.Teaching, 61, 3.0) is not null
                || RhythmAt(JobKind.Builder, VillagerState.Building, 61, 77.7) != RhythmAt(JobKind.Builder, VillagerState.Building, 61, 77.7))
            {
                faults.Add("phrases reach a trade this piece does not vary, or a moment differs on a second ask");
            }

            // Two builders out of step.
            int differ = 0;
            for (double c = 0; c < 60.0; c += 0.25)
            {
                differ += RhythmAt(JobKind.Builder, VillagerState.Building, 61, c)?.Beat != RhythmAt(JobKind.Builder, VillagerState.Building, 62, c)?.Beat ? 1 : 0;
            }

            if (differ < 40)
            {
                faults.Add($"two builders differ in {differ} of 240 moments (want out of step)");
            }

            // The painter: the sower's basket and the haymaker's fork are their own poses, not the fork's and the crook's.
            FigureKey farmer = PersonKeyOf(63, LifeStage.Adult, JobKind.Farmer, VillagerState.Sowing, Vector2.Zero, null, false, false);
            FigureKey herd = PersonKeyOf(64, LifeStage.Adult, JobKind.Herdsman, VillagerState.CuttingHay, Vector2.Zero, null, false, false);
            byte[] fork = PersonPainter.Paint(farmer with { Beat = 1 }).Rgba, sow = PersonPainter.Paint(farmer with { Beat = PersonPainter.SowReach }).Rgba,
                toss = PersonPainter.Paint(farmer with { Beat = PersonPainter.SowToss }).Rgba, crook = PersonPainter.Paint(herd with { Beat = 0 }).Rgba,
                hay = PersonPainter.Paint(herd with { Beat = PersonPainter.HayLift }).Rgba;
            if (fork.AsSpan().SequenceEqual(sow) || sow.AsSpan().SequenceEqual(toss) || crook.AsSpan().SequenceEqual(hay))
            {
                faults.Add("the sower's basket or the haymaker's fork paints as the fork's or the crook's");
            }

            return faults.Count == 0
                ? "[widths] outdoor work: ✅ the forager, builder, sower, reaper, hunter and haymaker in phrases — a little rest, mostly work, the forager's drop, the builder's plank and the hunter's loose of their own; no other trade phrased; two builders out of step; the sower's basket and the haymaker's fork their own poses"
                : $"[widths] outdoor work: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            return $"[widths] outdoor work: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}
