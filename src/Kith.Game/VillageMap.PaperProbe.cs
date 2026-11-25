using System.Reflection;
using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

public partial class VillageMap
{
    /// <summary>
    /// ⭐ The probe's <c>paper in hand:</c> line (A246, <c>library-paper-art.md §8</c>), riding on <c>painted villagers:</c> (art
    /// trap 73). The colour and the places are named here from the spec — blank sheets <c>#faf4e4</c> in the hand at the side,
    /// 0.45 of the height up, clutched at 0.64 when cold — never read back from the painter's own (art traps 17, 25).
    /// </summary>
    /// <remarks>⚠️ How it <i>looks</i> is the mock's (the same painter) and Joe's play.</remarks>
    private string ThePaperInHandHolds()
    {
        try
        {
            var faults = new List<string>();
            var paper = new Color("#faf4e4");
            int least = int.MaxValue;
            FigureKey Walker(LifeStage stage, bool coat, FigurePose pose, int beat) =>
                new(41, stage, null, pose, false, FigureHold.None, true, stage == LifeStage.Elder, null, coat, false, false, beat, 2);

            // §4, look B: in the hand at the side, over the gown and the cloak, every way they walk and on both steps; held still.
            foreach ((string who, LifeStage stage, bool coat) in new[] { ("an adult", LifeStage.Adult, false), ("a cloaked adult", LifeStage.Adult, true), ("an elder", LifeStage.Elder, false), ("a cloaked elder", LifeStage.Elder, true) })
            {
                foreach (FigurePose pose in new[] { FigurePose.Front, FigurePose.Back, FigurePose.Side })
                {
                    (float x0, float x1) = pose switch { FigurePose.Front => (-0.33f, -0.10f), FigurePose.Back => (0.10f, 0.33f), _ => (-0.04f, 0.19f) };
                    int InHand(FigureKey k) => Count(PersonPainter.Paint(k), paper, x0, x1, -0.50f, -0.27f);
                    FigureKey bare = Walker(stage, coat, pose, 0);
                    int first = InHand(bare with { Paper = true }), second = InHand(bare with { Paper = true, Beat = 1 });
                    int without = Math.Max(InHand(bare), InHand(bare with { Beat = 1 }));
                    least = Math.Min(least, Math.Min(first, second));
                    if (first < 60 || second < 60 || without > 10)
                    {
                        faults.Add($"{who} {pose}: {first} and {second} px of paper on the two steps, {without} without it (want 60+, 60+, ≤10)");
                    }

                    // The arm down to it is held still from one step to the other (a cloak covers it, so only without one).
                    int moved = coat ? 0 : Differing(PersonPainter.Paint(bare with { Paper = true }), PersonPainter.Paint(bare with { Paper = true, Beat = 1 }), pose == FigurePose.Side ? 0f : x0, x1, -0.62f, -0.46f);
                    if (moved > 15) // the hem swings beside it and the wash runs a few pixels in
                    {
                        faults.Add($"{who} {pose}: the arm with the paper moves with the step ({moved} px differ)");
                    }
                }
            }

            // Walking left is the mirror of walking right: the paper on the other side of the feet.
            FigureKey sideOn = Walker(LifeStage.Adult, true, FigurePose.Side, 0) with { Paper = true };
            int right = Count(PersonPainter.Paint(sideOn), paper, 0.02f, 0.19f, -0.50f, -0.27f), left = Count(PersonPainter.Paint(sideOn with { Flip = true }), paper, -0.19f, -0.02f, -0.50f, -0.27f);
            if (right < 40 || left < 40)
            {
                faults.Add($"walking left: {left} px before the body, {right} walking right (want 40+ each)");
            }

            // Blank: no ink lines on the top sheet (call 4) — its middle is one colour.
            (int blankAll, int blankPaper) = Middle(PersonPainter.Paint(Walker(LifeStage.Adult, false, FigurePose.Front, 0) with { Paper = true }), paper);
            if (blankPaper < blankAll)
            {
                faults.Add($"the sheet is not blank: {blankAll - blankPaper} of {blankAll} px in its middle are not paper (want 0)");
            }

            // The elder's stick stays in the other hand.
            var wood = new Color("#8a6038");
            FigureKey elder = Walker(LifeStage.Elder, false, FigurePose.Front, 0);
            int stick = Count(PersonPainter.Paint(elder with { Paper = true }), wood, 0.15f, 0.25f, -0.40f, 0f), stickAlone = Count(PersonPainter.Paint(elder), wood, 0.15f, 0.25f, -0.40f, 0f);
            if (stick < 20 || stick != stickAlone)
            {
                faults.Add($"the elder's stick: {stick} px with paper, {stickAlone} without (want the same, 20+)");
            }

            // Clutched to the chest against the cold, front and side; hidden walking away. Both arms full, none.
            FigureKey cold = Walker(LifeStage.Adult, false, FigurePose.Front, 0) with { Hold = FigureHold.Hug, Cold = true, Paper = true };
            int hugFront = Count(PersonPainter.Paint(cold), paper, -0.12f, 0.10f, -0.76f, -0.50f), hugSide = Count(PersonPainter.Paint(cold with { Pose = FigurePose.Side }), paper, 0f, 0.20f, -0.76f, -0.50f);
            bool SameWithout(FigureKey k) => PersonPainter.Paint(k).Rgba.AsSpan().SequenceEqual(PersonPainter.Paint(k with { Paper = false }).Rgba);
            bool hugBack = SameWithout(cold with { Pose = FigurePose.Back });
            bool loaded = SameWithout(Walker(LifeStage.Adult, false, FigurePose.Front, 0) with { Hold = FigureHold.Front, Good = Goods.Ale, Paper = true });
            if (hugFront < 40 || hugSide < 40 || !hugBack || !loaded)
            {
                faults.Add($"cold: {hugFront} px at the chest, {hugSide} side on, walking away {(hugBack ? "hidden" : "drawn")}; with a load {(loaded ? "none" : "drawn")} (want 40+, 40+, hidden, none)");
            }

            // The walker's key: paper from the library's door on (call 3), and a key of its own, so it is painted apart.
            bool shown = ShowsPaper(true, VillagerState.TravelingHome) && ShowsPaper(true, VillagerState.SeekingShelter);
            bool hidden = !ShowsPaper(false, VillagerState.TravelingHome) && !ShowsPaper(true, VillagerState.AtTheLibrary);
            FigureKey KeyOf(bool p) => PersonKeyOf(41, LifeStage.Adult, null, VillagerState.TravelingHome, new Vector2(0f, 1f), null, true, false, 2, p);
            if (!shown || !hidden || !KeyOf(true).Paper || KeyOf(false).Paper || KeyOf(true) == KeyOf(false))
            {
                faults.Add($"the key: home or to shelter with paper {(shown ? "shows it" : "does not")}, without it or inside the library {(hidden ? "none" : "shows it")}, the key {(KeyOf(true).Paper ? "carries it" : "drops it")}");
            }

            // And a villager of the village being run, asked as the draw asks: what the sim says of them reaches the key.
            if (_world is not SimWorld world || world.Villagers.Count == 0)
            {
                faults.Add("no village to ask a villager of");
            }
            else
            {
                Villager founder = world.Villagers[0];
                (bool held, VillagerState doing) = (founder.CarryingPaper, founder.State);
                bool with, within, without;
                try
                {
                    founder.State = VillagerState.TravelingHome;
                    founder.CarryingPaper = true;
                    with = PersonKeyOf(founder, world, Vector2.Down).Paper;
                    founder.State = VillagerState.AtTheLibrary;
                    within = PersonKeyOf(founder, world, Vector2.Down).Paper;
                    founder.State = VillagerState.TravelingHome;
                    founder.CarryingPaper = false;
                    without = PersonKeyOf(founder, world, Vector2.Down).Paper;
                }
                finally
                {
                    (founder.CarryingPaper, founder.State) = (held, doing);
                }

                if (!with || within || without)
                {
                    faults.Add($"a founder walking home: with paper {with}, at the library {within}, without {without} (want true, false, false)");
                }
            }

            // The stand-in is gone.
            if (typeof(VillageMap).GetMethod("DrawThePaper", BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic) is not null)
            {
                faults.Add("the stand-in DrawThePaper is still there");
            }

            return faults.Count == 0
                ? $"[widths] paper in hand: ✅ two blank sheets in the hand of an adult, an elder and both cloaked, toward, away and from the side, on both steps and held still (at least {least} px), none without; mirrored walking left; no ink on it; the elder's stick in the other hand; clutched to the chest when cold (front {hugFront} px, side {hugSide}), hidden walking away; none with both arms full; shown from the library's door on, never inside; the stand-in gone"
                : $"[widths] paper in hand: ⛔ {string.Join("; ", faults)}";
        }
        catch (Exception e)
        {
            return $"[widths] paper in hand: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }

    /// <summary>The pixels that differ by more than a shade between two pictures of one size, inside a box in figure units.</summary>
    private static int Differing(PaintedFigure a, PaintedFigure b, float x0, float x1, float y0, float y1)
    {
        float unit = a.Height / PersonPainter.CanvasH;
        int n = 0;
        for (int y = (int)(a.FeetY + (y0 * unit)); y <= (int)(a.FeetY + (y1 * unit)); y++)
        {
            for (int x = (int)(a.FeetX + (x0 * unit)); x <= (int)(a.FeetX + (x1 * unit)); x++)
            {
                int k = ((y * a.Width) + x) * 4;
                int most = 0;
                for (int c = 0; c < 4; c++)
                {
                    most = Math.Max(most, Math.Abs(a.Rgba[k + c] - b.Rgba[k + c]));
                }

                n += most > 12 ? 1 : 0;
            }
        }

        return n;
    }

    /// <summary>The opaque pixels in the middle of the top sheet, seen from the front, and how many of them are paper.</summary>
    private static (int All, int Paper) Middle(PaintedFigure f, Color paper)
    {
        int all = Count(f, paper, -0.235f, -0.185f, -0.39f, -0.34f, 255);
        return (all, Count(f, paper, -0.235f, -0.185f, -0.39f, -0.34f));
    }
}
