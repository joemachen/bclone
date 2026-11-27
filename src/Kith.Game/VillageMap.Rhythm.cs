using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐⭐ THE OTHER OUTDOOR TRADES, VARIED (A297, <c>specs/varied-work.md §9</c>; Joe: <i>"just power through all these"</i> — the
/// recommended option taken). The forager, the builder, the farmer, the hunter and the herdsman each had one two-beat motion on the
/// people's clock (A130). Each now works in <b>phrases</b>: a run of strokes, a breather opening about one phrase in six, and a motion
/// of its own — the forager straightening to drop the pickings in the basket, the builder stooping for a plank, the sower tossing seed
/// from a basket, the hunter drawing, loosing and lowering the bow, the herdsman pitching hay with a fork. On the people's clock, as
/// every swing (§3), so it slows with a paused or capped game and no two people keep step. ⛔ View only.
/// </remarks>
public partial class VillageMap
{
    internal enum RhythmPart
    {
        Work,
        Rest,
        Second,
    }

    /// <summary>A phrase's moment: the beat to paint, the part, how far through it, and the motion to move the picture by.</summary>
    internal readonly record struct RhythmMoment(int Beat, RhythmPart Part, float Within, PersonMotion Motion);

    /// <summary>
    /// A trade's phrase: its stroke period and count, its two stroke beats, its second motion's length and how often it comes (1 =
    /// every phrase).
    /// </summary>
    private readonly record struct Phrase(float Period, int Strokes, int Up, int Down, float SecondSeconds, float SecondShare);

    /// <summary>Pure: the phrase a trade at a state works in, or null — the trades this piece varies, at their outdoor work.</summary>
    private static Phrase? PhraseOf(JobKind? trade, VillagerState state) => (trade, state) switch
    {
        (JobKind.Forager, VillagerState.Gathering) => new Phrase(2.4f, 3, 1, 2, 1.0f, 1f),
        (JobKind.Builder, VillagerState.Building) => new Phrase(1.2f, 5, 1, 2, 1.5f, 0.4f),
        (JobKind.Farmer, VillagerState.Sowing) => new Phrase(2.4f, 4, PersonPainter.SowReach, PersonPainter.SowToss, 0f, 0f),
        (JobKind.Farmer, VillagerState.Reaping or VillagerState.Tending) => new Phrase(2.4f, 4, 1, 2, 0f, 0f),
        (JobKind.Hunter, VillagerState.Hunting) => new Phrase(1.1f, 2, 1, 2, 2.2f, 1f),
        (JobKind.Herdsman, VillagerState.CuttingHay or VillagerState.FeedingTheHerd) => new Phrase(2.4f, 4, PersonPainter.HayLift, PersonPainter.HayPitch, 0f, 0f),
        _ => null,
    };

    /// <summary>Whether a trade at a state works in phrases (§9).</summary>
    internal static bool WorksInPhrases(JobKind? trade, VillagerState state) => PhraseOf(trade, state) is not null;

    /// <summary>
    /// ⭐ Pure: a person's moment in their trade's phrase on the people's <paramref name="clock"/>. A phrase is a breather (about one
    /// in six, its first 1.6 s), the strokes, then the second motion where the trade has one and this phrase draws it.
    /// </summary>
    internal static RhythmMoment? RhythmAt(JobKind? trade, VillagerState state, int id, double clock)
    {
        if (PhraseOf(trade, state) is not Phrase p)
        {
            return null;
        }

        double offset = PersonPainter.Hash01(id, "phrase") * 10.0;
        double strokes = p.Period * p.Strokes;
        // Every phrase has room for a breather and a second motion; a phrase without them holds the last stroke's rest.
        double length = BreatherSeconds + strokes + p.SecondSeconds;
        double at = clock + offset;
        int n = (int)Math.Floor(at / length);
        double u = at - (n * length);
        int seed = unchecked((n * 2654435761u).GetHashCode() ^ (id * 40503));
        bool rests = PersonPainter.Hash01(seed, "rest") < (1f / 6f);
        bool second = p.SecondSeconds > 0f && PersonPainter.Hash01(seed, "second") < p.SecondShare;

        if (u < BreatherSeconds)
        {
            if (rests)
            {
                float w = (float)(u / BreatherSeconds);
                float lean = Mathf.Sin(Mathf.Pi * w);
                return new RhythmMoment(0, RhythmPart.Rest, w, new PersonMotion(0, 0f, -0.004f * lean, -3.5f * lean, 1f));
            }

            // No breather this phrase: the strokes run on through its first 1.6 s.
            u += strokes;
        }

        double s = u - BreatherSeconds;
        if (s >= strokes && s < strokes + p.SecondSeconds && !second)
        {
            s -= strokes;   // no second motion this phrase: strokes again
        }

        if (s < strokes || !second)
        {
            float phase = (float)((s % p.Period) / p.Period);
            int beat = phase < 0.62f ? p.Up : p.Down;
            bool down = beat == p.Down;
            float stoop = trade == JobKind.Forager ? 0.06f : trade == JobKind.Farmer ? 0.02f : 0f;
            return new RhythmMoment(beat, RhythmPart.Work, phase, new PersonMotion(beat, 0f, down ? 0.012f : 0f, 0f, down ? 1f - stoop : 1f));
        }

        float within = (float)((s - strokes) / p.SecondSeconds);
        return trade switch
        {
            // straightened, the pickings dropped in: up a little, the basket at the hip
            JobKind.Forager => new RhythmMoment(0, RhythmPart.Second, within, new PersonMotion(0, 0f, -0.006f * Mathf.Sin(Mathf.Pi * within), 0f, 1f)),
            // stooped for a plank and up with it
            JobKind.Builder => new RhythmMoment(0, RhythmPart.Second, within, within < 0.5f
                ? new PersonMotion(0, 0f, 0.012f, 0f, 0.9f) : new PersonMotion(0, 0f, 0f, 0f, 1f)),
            // the bow: drawn, loosed (the arrow away), lowered
            JobKind.Hunter => new RhythmMoment(within < 0.45f ? 2 : within < 0.6f ? 1 : 0, RhythmPart.Second, within, new PersonMotion(0, 0f, 0f, 0f, 1f)),
            _ => new RhythmMoment(0, RhythmPart.Second, within, new PersonMotion(0, 0f, 0f, 0f, 1f)),
        };
    }

    private static readonly Color Seed = new("#d8c48a"), ArrowShaft = new("#6e5034"), Fletch = new("#e8e2d2");

    /// <summary>Over the person: the seed in the air from a sower's toss, the arrow away from a hunter's loose.</summary>
    private void DrawTheRhythm(JobKind? trade, VillagerState state, RhythmMoment m, Vector2 feet, float tall, int id)
    {
        float k = tall / 30f;
        if (trade == JobKind.Farmer && state == VillagerState.Sowing && m.Beat == PersonPainter.SowToss)
        {
            float t = Mathf.Clamp((m.Within - 0.62f) / 0.38f, 0f, 1f);
            for (int i = 0; i < 6; i++)
            {
                float a = -0.5f + (i * 0.35f) + (PersonPainter.Hash01(id + i, "seed") * 0.2f);
                Vector2 p = feet + new Vector2(6f * k, -14f * k) + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (4f + (10f * t)) * k) + new Vector2(0f, 8f * k * t * t);
                DrawCircle(p, Mathf.Max(0.7f, 0.6f * k), AtNight(Seed with { A = 1f - (0.6f * t) }));
            }
        }

        if (trade == JobKind.Hunter && m.Part == RhythmPart.Second && m.Within is >= 0.45f and < 0.75f)
        {
            float t = (m.Within - 0.45f) / 0.3f;
            Vector2 from = feet + new Vector2(10f * k, -18f * k) + new Vector2(40f * k * t, -4f * k * t);
            Vector2 to = from + new Vector2(7f * k, -0.7f * k);
            DrawLine(from, to, AtNight(ArrowShaft with { A = 1f - t }), Mathf.Max(1f, 0.5f * k), true);
            DrawLine(from, from + new Vector2(-1.5f * k, -0.8f * k), AtNight(Fletch with { A = 1f - t }), Mathf.Max(1f, 0.5f * k), true);
        }
    }
}
