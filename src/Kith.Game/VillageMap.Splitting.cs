using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐⭐ THE WOODCUTTER AT THE BLOCK (A292, <c>specs/varied-work.md §7</c>; Joe's pick, <i>"B, as recommended"</i>). A woodcutter
/// splitting firewood stands at the yard's chopping block (A86's), not on the hut's roof where the sim's place puts them; each split
/// sets a round on the block, takes its strokes and splits it, the halves falling either side and lying until the next round — a
/// clean one-stroke split, a knotty round taking two or three, or a wedge and maul; a breather now and then (the face's rules, A282).
/// ⛔ <b>The view's, never the sim's</b>: a split is the sim's countdown (<c>split_ticks</c>, the villager's
/// <c>ActionTicksRemaining</c>); its length is <c>WorkTicksFor</c>'s; nothing is written back.
/// </remarks>
public partial class VillageMap
{
    /// <summary>Real seconds setting the round on the block takes, at most; a short split gives it a share instead.</summary>
    internal const float SetSeconds = 0.9f;

    /// <summary>The halves fall apart over this many real seconds after the last stroke.</summary>
    private const float HalvesFallSeconds = 0.5f;

    /// <summary>Where the woodcutter stands from the block's middle, tiles on screen: just to its right and a little in front.</summary>
    internal static readonly Vector2 FromTheBlock = new(0.21f, 0.17f);

    internal enum SplitPart
    {
        Rest,
        Set,
        Stroke,
        Split,
    }

    /// <summary>Where in a split a woodcutter is: the beat, the part, how far through it, how many strokes this round takes, the
    /// wedge and maul, quick.</summary>
    internal readonly record struct SplitMoment(int Beat, SplitPart Part, float Within, int Strokes, bool Maul, bool Quick);

    /// <summary>A woodcutter at the block this frame: the moment, and the block's place (view tiles).</summary>
    private readonly record struct Splitting(SplitMoment Moment, Vector2 Block, Vector2 Stand);

    /// <summary>Each woodcutter's yard's chopping block, by the roof's key (its building's tile-space centre), kept as the yard is laid.</summary>
    private readonly Dictionary<Vector2, Vector2> _choppingBlocks = new();

    /// <summary>Pure: the middle of the first chopping block a laid yard holds (view tiles), or null.</summary>
    internal static Vector2? BlockIn(List<RoofPiece> pieces)
    {
        foreach (RoofPiece piece in pieces)
        {
            if (piece.Kind == RoofPieceKind.Fill && piece.Part == "chopping block")
            {
                return CentreOf(piece.Points);
            }
        }

        return null;
    }

    /// <summary>Kept as a roof is laid (<see cref="KeepTheStep"/>'s neighbour): its chopping block, if it has one.</summary>
    private void KeepTheBlock(RoofSignature signature, List<RoofPiece> pieces)
    {
        if (BlockIn(pieces) is Vector2 block)
        {
            _choppingBlocks[signature.At] = block;
        }
    }

    /// <summary>The block a woodcutter on this hut's spot works at, or null — the yard not laid yet (off screen, or the tick off).</summary>
    private Vector2? BlockAt(Vector2 viewTile) => _choppingBlocks.TryGetValue(RoofRounded(viewTile), out Vector2 block) ? block : null;

    /// <summary>
    /// ⭐ Pure: the moment of a split <paramref name="elapsedTicks"/> into its <paramref name="lengthTicks"/>. Which split it is
    /// (<paramref name="id"/>, <paramref name="startTick"/>) chooses its strokes — one (two in five), two (a quarter), three (a
    /// fifth), or the wedge and maul's two (the rest) — and a breather (one in six); the round set at its start, the strokes after.
    /// A quick split (under <see cref="QuickDigSeconds"/>) is strokes only, on the people's <paramref name="clock"/>.
    /// </summary>
    internal static SplitMoment SplitAt(int id, ulong startTick, float elapsedTicks, int lengthTicks, double secondsPerTick, double clock)
    {
        int seed = unchecked((int)(startTick * 2654435761UL) ^ (id * 40503));
        float r = PersonPainter.Hash01(seed, "strokes");
        bool maul = r >= 0.8f;
        int strokes = r < 0.4f ? 1 : r < 0.65f ? 2 : r < 0.8f ? 3 : 2;
        double cycle = lengthTicks * secondsPerTick;
        double t = Math.Clamp(elapsedTicks, 0f, lengthTicks) * secondsPerTick;
        float swing = maul ? 1.2f : PersonPainter.SwingSeconds(JobKind.Woodcutter, LifeStage.Adult);

        int Struck(float phase) => maul ? (phase < 0.6f ? PersonPainter.MalletRaise : PersonPainter.MalletStrike) : (phase < 0.62f ? 1 : 2);

        if (cycle < QuickDigSeconds)
        {
            float phase = (float)(((clock / swing) + PersonPainter.Hash01(id, "beat")) % 1.0);
            return new SplitMoment(Struck(phase), SplitPart.Stroke, phase, strokes, maul, true);
        }

        bool breather = PersonPainter.Hash01(seed, "rest") < (1f / 6f);
        double restFor = breather ? Math.Min(BreatherSeconds, cycle * 0.3) : 0.0;
        if (t < restFor)
        {
            return new SplitMoment(PersonPainter.Breather, SplitPart.Rest, (float)(t / restFor), strokes, maul, false);
        }

        double setFor = Math.Min(SetSeconds, cycle * 0.2);
        double u = t - restFor;
        if (u < setFor)
        {
            return new SplitMoment(0, SplitPart.Set, (float)(u / setFor), strokes, maul, false);
        }

        // The strokes fit what is left of the split, with room after them for the halves to fall.
        double room = cycle - restFor - setFor - HalvesFallSeconds;
        double period = Math.Min(swing, room / strokes);
        double s = u - setFor;
        int n = (int)Math.Floor(s / period);
        if (n < strokes)
        {
            float phase = (float)((s % period) / period);
            return new SplitMoment(Struck(phase), SplitPart.Stroke, phase, strokes, maul, false);
        }

        float apart = (float)Math.Min(1.0, (s - (strokes * period)) / HalvesFallSeconds);
        return new SplitMoment(apart < 0.8f ? (maul ? PersonPainter.MalletStrike : 2) : 0, SplitPart.Split, apart, strokes, maul, false);
    }

    /// <summary>A woodcutter splitting at a laid block this frame, or null.</summary>
    private Splitting? SplittingOf(SimWorld world, Villager villager, FigureKey still)
    {
        if (villager.State != VillagerState.MakingFirewood || still.Trade != JobKind.Woodcutter || still.Hold != FigureHold.Tool || still.Walking)
        {
            return null;
        }

        if (BlockAt(InViewTiles(villager.Position)) is not Vector2 block)
        {
            return null;
        }

        int length = Math.Max(1, world.WorkTicksFor(villager, JobKind.Woodcutter, world.Config.SplitTicks));
        int remaining = Math.Clamp(villager.ActionTicksRemaining, 0, length);
        float elapsed = length - remaining + (float)_alpha;
        ulong start = world.Tick - (ulong)(length - remaining);
        double secondsPerTick = 1.0 / (world.Config.TargetTicksPerSecond * Math.Max(0.05, _flowPace));
        return new Splitting(SplitAt(villager.Id, start, elapsed, length, secondsPerTick, _peopleClock), block, block + FromTheBlock);
    }

    /// <summary>Pure: the figure and its motion for a split's moment — stooped to set the round, the strokes dipping the body (the
    /// maul's stooped over), the breather leaning back.</summary>
    internal static (FigureKey Key, PersonMotion Motion) SplitKeyAndMotion(FigureKey still, SplitMoment m) => m.Part switch
    {
        SplitPart.Set => (still with { Beat = 0 }, new PersonMotion(0, 0f, m.Within < 0.66f ? 0.01f : 0f, 0f, m.Within < 0.66f ? 0.9f : 1f)),
        SplitPart.Rest => (still with { Beat = m.Beat },
            new PersonMotion(m.Beat, 0f, -0.004f * Mathf.Sin(Mathf.Pi * m.Within), -3.5f * Mathf.Sin(Mathf.Pi * m.Within), 1f)),
        _ when m.Maul => (still with { Beat = m.Beat }, new PersonMotion(m.Beat, 0f, 0.012f, 0f, 0.9f)),
        _ => (still with { Beat = m.Beat }, new PersonMotion(m.Beat, 0f, m.Beat == 2 ? 0.012f : 0f, 0f, 1f)),
    };

    private static readonly Color RoundFace = new("#d6b27e"), RoundRing = new("#ba9462"), RoundBark = new("#7a5634");

    /// <summary>On the block, under the woodcutter: the round set on it, the wedge in it, or the two halves fallen either side.</summary>
    private void DrawTheRound(Splitting s, Vector2 feet, float tall)
    {
        SplitMoment m = s.Moment;
        if (m.Quick || m.Part == SplitPart.Rest || (m.Part == SplitPart.Set && m.Within < 0.66f))
        {
            return;
        }

        float k = tall / 30f;
        Vector2 c = feet + ((s.Block - s.Stand) * _pixelsPerTile);
        float r = 2.6f * k;
        if (m.Part == SplitPart.Split)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 at = c + new Vector2(side * ((1.2f * k) + (3.5f * k * m.Within)), 2.5f * k * m.Within * m.Within);
                var half = new Vector2[10];
                for (int i = 0; i < half.Length; i++)
                {
                    float a = (side < 0f ? Mathf.Pi / 2f : -Mathf.Pi / 2f) + (i * Mathf.Pi / (half.Length - 1));
                    half[i] = at + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.8f);
                }

                DrawColoredPolygon(half, AtNight(RoundFace));
                DrawPolyline(half.Append(half[0]).ToArray(), AtNight(RoundBark), Mathf.Max(1f, 0.6f * k), antialiased: true);
            }

            return;
        }

        var face = new Vector2[18];
        for (int i = 0; i < face.Length; i++)
        {
            float a = i * Mathf.Tau / face.Length;
            face[i] = c + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.8f);
        }

        DrawColoredPolygon(face, AtNight(RoundFace));
        DrawPolyline(face.Append(face[0]).ToArray(), AtNight(RoundBark), Mathf.Max(1f, 0.7f * k), antialiased: true);
        DrawArc(c, r * 0.5f, 0f, Mathf.Tau, 14, AtNight(RoundRing), Mathf.Max(1f, 0.4f * k), true);
        if (m.Maul && m.Part == SplitPart.Stroke)
        {
            Vector2 w = c - new Vector2(0f, 0.4f * k);
            Vector2[] wedge = { w + new Vector2(-1.0f * k, 0f), w + new Vector2(1.0f * k, 0f), w + new Vector2(0f, -2.0f * k) };
            DrawColoredPolygon(wedge, AtNight(WedgeIron));
            DrawPolyline(new[] { wedge[0], wedge[1], wedge[2], wedge[0] }, AtNight(FaceInk), Mathf.Max(1f, 0.4f * k), antialiased: true);
        }
    }
}
