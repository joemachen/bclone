using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐⭐ WORK AT A FACE, VARIED (A282, <c>specs/varied-work.md</c>; Joe's play note (c), <i>"stone gathering"</i> named; his pick,
/// <i>"B, as recommended"</i>, the miner too). A quarrier or a miner keeps the pick in hand all stint, the stone or ore they hold
/// stacked at their side a block a dig; each dig ends with the cut block lifted onto the stack; the digging is the pick or a wedge
/// and mallet, and now and then a breather. ⛔ <b>The view's, never the sim's</b>: a dig is the sim's countdown
/// (<c>face_dig_ticks</c>, the villager's <c>ActionTicksRemaining</c>) and its end puts <c>face_per_dig</c> into their load; the
/// stack is that load, read back, and the lift falls where the dig ends.
/// </remarks>
public partial class VillageMap
{
    /// <summary>Real seconds the lift takes at a dig's end (§3, as mocked) — at most; a short dig gives it a share instead.</summary>
    internal const float LiftSeconds = 1.5f;

    /// <summary>A breather takes the first this-many seconds of its dig — at most, as the lift.</summary>
    internal const float BreatherSeconds = 1.6f;

    /// <summary>A dig shorter than this in real seconds (a fast game) is digging only: no lift, no breather (§1.2).</summary>
    /// <remarks>⚠️ Not 3.5, as first built: a quarrier with a tool and some mastery digs in 2 ticks, 2.7 s at 1×, and never lifted
    /// a block in the first film (A282). The lift and the breather take a share of a short dig instead.</remarks>
    internal const float QuickDigSeconds = 2f;

    /// <summary>A short dig's share for the lift and for the breather.</summary>
    private const float LiftShare = 0.45f, BreatherShare = 0.4f;

    /// <summary>The wedge and mallet beat (the hammer's 1.2 s); the pick keeps its own 1.6 s.</summary>
    private const float MalletSeconds = 1.2f;

    /// <summary>The stack shows at most this many blocks (a miner's stint is eight digs).</summary>
    internal const int StackAtMost = 6;

    internal enum DigPart
    {
        Work,
        Rest,
        Lift,
    }

    /// <summary>Where in a dig a person is: the beat, the part, how far through it, whether this dig is the wedge's, and quick.</summary>
    internal readonly record struct DigMoment(int Beat, DigPart Part, float Within, bool Wedge, bool Quick);

    /// <summary>A person at a face this frame: the moment, what they dig, and how many blocks their load stacks to.</summary>
    private readonly record struct Dig(DigMoment Moment, Goods Good, int Stack);

    /// <summary>The states at a face: the quarrier's and the miner's (the same code in the sim, D449).</summary>
    internal static bool AtAFace(VillagerState state) => state is VillagerState.Quarrying or VillagerState.Mining;

    /// <summary>Pure: the blocks a load stacks to — a block for each dig's worth begun, at most <see cref="StackAtMost"/>.</summary>
    internal static int StackOf(int held, int perDig) =>
        held <= 0 || perDig <= 0 ? 0 : Math.Min(StackAtMost, (held + perDig - 1) / perDig);

    /// <summary>
    /// ⭐ Pure: the moment of a dig <paramref name="elapsedTicks"/> into its <paramref name="lengthTicks"/>, at
    /// <paramref name="secondsPerTick"/>. Which dig it is (<paramref name="id"/>, <paramref name="startTick"/>) chooses the wedge (two in
    /// five) or the pick, and a breather (one in six); the strikes keep the people's <paramref name="clock"/>, as every swing does.
    /// </summary>
    internal static DigMoment DigAt(int id, ulong startTick, float elapsedTicks, int lengthTicks, double secondsPerTick, double clock)
    {
        int seed = unchecked((int)(startTick * 2654435761UL) ^ (id * 40503));
        bool wedge = PersonPainter.Hash01(seed, "wedge") < 0.4f;
        double cycle = lengthTicks * secondsPerTick;
        double t = Math.Clamp(elapsedTicks, 0f, lengthTicks) * secondsPerTick;
        double left = cycle - t;
        bool quick = cycle < QuickDigSeconds;
        bool breather = !quick && PersonPainter.Hash01(seed, "rest") < (1f / 6f);

        double liftFor = Math.Min(LiftSeconds, cycle * LiftShare), restFor = Math.Min(BreatherSeconds, cycle * BreatherShare);
        if (!quick && left < liftFor)
        {
            return new DigMoment(0, DigPart.Lift, (float)(1.0 - (left / liftFor)), wedge, false);
        }

        if (breather && t < restFor)
        {
            return new DigMoment(PersonPainter.Breather, DigPart.Rest, (float)(t / restFor), wedge, false);
        }

        float period = wedge ? MalletSeconds : PersonPainter.SwingSeconds(JobKind.Quarrier, LifeStage.Adult);
        float phase = (float)(((clock / period) + PersonPainter.Hash01(id, "beat")) % 1.0);
        int beat = wedge ? (phase < 0.6f ? PersonPainter.MalletRaise : PersonPainter.MalletStrike) : (phase < 0.62f ? 1 : 2);
        return new DigMoment(beat, DigPart.Work, phase, wedge, quick);
    }

    /// <summary>A person's dig this frame, or null: at a face, pick in hand, facing us. The dig's length is the sim's own (the face's
    /// row, shortened by the tool and the mastery as <c>WorkTicksFor</c> says), so where they are in it is the countdown read back.</summary>
    private Dig? DigOf(SimWorld world, Villager villager, FigureKey still)
    {
        if (!AtAFace(villager.State) || still.Hold != FigureHold.Tool || still.Walking
            || still.Trade is not JobKind t || t is not (JobKind.Quarrier or JobKind.Miner))
        {
            return null;
        }

        Goods good = t == JobKind.Miner ? Goods.Iron : Goods.Stone;
        GoodRow row = world.GoodsCatalog[good];
        int length = Math.Max(1, world.WorkTicksFor(villager, t, row.FaceDigTicks));
        int remaining = Math.Clamp(villager.ActionTicksRemaining, 0, length);
        float elapsed = length - remaining + (float)_alpha;
        ulong start = world.Tick - (ulong)(length - remaining);
        double secondsPerTick = 1.0 / (world.Config.TargetTicksPerSecond * Math.Max(0.05, _flowPace));
        DigMoment moment = DigAt(villager.Id, start, elapsed, length, secondsPerTick, _peopleClock);
        return new Dig(moment, good, StackOf(villager.Carried[good], row.FacePerDig));
    }

    /// <summary>
    /// Pure: the figure and its motion for a dig's moment — the pick's stroke dipping the body as every swing does; the wedge
    /// stooped over; the breather leaning back; the lift the block in the arms, stooped to take it and to set it down.
    /// </summary>
    internal static (FigureKey Key, PersonMotion Motion) FaceKeyAndMotion(FigureKey still, DigMoment m, Goods good) => m.Part switch
    {
        DigPart.Lift => (still with { Beat = 0, Hold = FigureHold.Front, Good = good, ToolOut = false },
            m.Within < 0.33f ? new PersonMotion(0, 0f, 0.01f, 0f, 0.9f)
            : m.Within < 0.66f ? new PersonMotion(0, 0f, 0f, 0f, 1f)
            : new PersonMotion(0, 0f, 0.008f, 0f, 0.92f)),
        DigPart.Rest => (still with { Beat = m.Beat },
            new PersonMotion(m.Beat, 0f, -0.004f * Mathf.Sin(Mathf.Pi * m.Within), -3.5f * Mathf.Sin(Mathf.Pi * m.Within), 1f)),
        _ when m.Wedge => (still with { Beat = m.Beat }, new PersonMotion(m.Beat, 0f, 0.012f, 0f, 0.9f)),
        _ => (still with { Beat = m.Beat }, new PersonMotion(m.Beat, 0f, m.Beat == 2 ? 0.012f : 0f, 0f, 1f)),
    };

    private (FigureKey Key, PersonMotion Motion) FaceKeyAndMotion(FigureKey still, Dig d) => FaceKeyAndMotion(still, d.Moment, d.Good);

    // ------------------------------------------------------------------ the drawing

    private static readonly Color StackStone = new("#c4c4be"), StackStoneLine = new("#96968f"), StackOre = new("#7e5446"),
        StackOreLine = new("#5a3a30"), WedgeIron = new("#46464c"), FaceInk = new("#2a2018");

    /// <summary>Where the stack's blocks lie, left of the feet, in thirtieths of the person's height (as mocked).</summary>
    private static readonly Vector2[] StackSpots =
    {
        new(-14f, 2f), new(-8.5f, 2.5f), new(-11.5f, -1.5f), new(-16.5f, -1.8f), new(-13f, -4.6f), new(-10f, -4.2f),
    };

    /// <summary>Under the person: the stack of what they hold (a block more as the lift sets one down), and the wedge in the stone.</summary>
    private void DrawTheStackAndTheWedge(Dig d, Vector2 feet, float tall)
    {
        float k = tall / 30f;
        int blocks = d.Stack + (d.Moment.Part == DigPart.Lift && d.Moment.Within > 0.9f ? 1 : 0);
        bool ore = d.Good == Goods.Iron;
        for (int i = 0; i < Math.Min(blocks, StackSpots.Length); i++)
        {
            DrawTheBlock(feet + (StackSpots[i] * k), k, ore ? StackOre : StackStone, ore ? StackOreLine : StackStoneLine);
        }

        if (d.Moment.Wedge && d.Moment.Part == DigPart.Work)
        {
            Vector2 w = feet + new Vector2(6f * k, 0.5f * k);
            Vector2[] wedge = { w + new Vector2(-1.2f * k, 0f), w + new Vector2(1.2f * k, 0f), w + new Vector2(0f, -2.2f * k) };
            DrawColoredPolygon(wedge, AtNight(WedgeIron));
            DrawPolyline(new[] { wedge[0], wedge[1], wedge[2], wedge[0] }, AtNight(FaceInk), Mathf.Max(1f, 0.4f * k), antialiased: true);
        }
    }

    private void DrawTheBlock(Vector2 c, float k, Color fill, Color line)
    {
        float s = 2.8f * k;
        Vector2[] quad =
        {
            c + new Vector2(-s, -s * 0.6f), c + new Vector2(s, -s * 0.7f), c + new Vector2(s * 1.05f, s * 0.6f), c + new Vector2(-s * 0.95f, s * 0.65f),
        };
        DrawColoredPolygon(quad, AtNight(fill));
        DrawPolyline(new[] { quad[0], quad[1], quad[2], quad[3], quad[0] }, AtNight(FaceInk), Mathf.Max(1f, 0.4f * k), antialiased: true);
        DrawLine(c + new Vector2(-s * 0.9f, -s * 0.1f), c + new Vector2(s * 0.9f, -s * 0.2f), AtNight(line), Mathf.Max(1f, 0.4f * k), true);
    }
}
