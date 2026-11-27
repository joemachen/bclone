using Kith.Sim.World;
using Godot;

namespace Kith.Game;

public partial class VillageMap
{
    /// <summary>
    /// ⭐ The probe's <c>woodcutter:</c> line (A292, <c>varied-work.md §7</c>) — printed with the people's lines. Every number is the
    /// spec's (a split of 4 ticks at 0.75 a second; the round set in its first 0.9 s; quick under 2 s; one stroke two in five, the
    /// wedge and maul one in five, a breather one in six), never read back from the code's constants.
    /// </summary>
    /// <remarks>⚠️ Nothing is drawn headless (D340): the woodcutter at the block and the halves falling are the renders'.</remarks>
    private string TheWoodcutterHolds()
    {
        try
        {
            var faults = new List<string>();
            const double oneX = 1.0 / 0.75, fourX = 1.0 / 3.0;

            // ⭐ The yard keeps a chopping block, out in the yard beside the hut (not under its roof), for the woodcutter to stand at.
            RoofLook look = RoofOf(JobKind.Woodcutter);
            List<RoofPiece> laid = RoofDrawing(look, new Vector2(10f, 10f), 2f * 0.8f, 1f * 0.8f, 0, Colors.White, 1f, null,
                new Vector2(10f, 10f), 7, false, new YardState(Working: true));
            Vector2? block = BlockIn(laid);
            if (block is not Vector2 b || b.DistanceTo(new Vector2(10f, 10f)) < 0.3f || b.DistanceTo(new Vector2(10f, 10f)) > 2f)
            {
                faults.Add($"a woodcutter's yard keeps its block at {block?.ToString() ?? "nowhere"} (want out in its yard, 0.3–2 tiles from the hut's middle)");
            }

            // ⭐ Kept as the yard is laid, and a splitting woodcutter on the hut's spot stood at it (the table put back as it was).
            if (block is Vector2 kept)
            {
                var signature = new RoofSignature(look, new Vector2(10f, 10f), 1.6f, 0.8f, 0, Colors.White, false, null, new Vector2(10f, 10f), 7);
                bool had = _choppingBlocks.TryGetValue(signature.At, out Vector2 was);
                KeepTheStep(signature, laid); // the game's own call as a roof is laid (A292: a red-check found KeepTheBlock asked directly scored 0)
                Vector2? found = BlockAt(new Vector2(10f, 10f));
                Vector2 stood = AtTheDoor(new Vector2(10f, 10f), VillagerState.MakingFirewood);
                Vector2 want = kept + FromTheBlock - new Vector2(0f, PersonTiles * PersonPointUp);
                if (had)
                {
                    _choppingBlocks[signature.At] = was;
                }
                else
                {
                    _choppingBlocks.Remove(signature.At);
                }

                if (found != kept || (PaintsPeople && stood.DistanceTo(want) > 1e-3f))
                {
                    faults.Add($"the block kept {found?.ToString() ?? "not"}; a splitting woodcutter stands at {stood} (want {want}, just right of the block)");
                }
            }

            // A plain split, a resting one, a one-stroke one: found by the shuffle, as the game finds them.
            ulong one = 0, resting = 0;
            for (ulong s = 500; s < 3000 && (one == 0 || resting == 0); s++)
            {
                SplitMoment first = SplitAt(41, s, 0.2f, 4, oneX, 0.0);
                if (first.Part == SplitPart.Set && first.Strokes == 1 && !first.Maul && one == 0)
                {
                    one = s;
                }

                if (first.Part == SplitPart.Rest && resting == 0)
                {
                    resting = s;
                }
            }

            SplitMoment set = SplitAt(41, one, 0.3f, 4, oneX, 0.0), stroke = SplitAt(41, one, 1.5f, 4, oneX, 0.0),
                split = SplitAt(41, one, 3.9f, 4, oneX, 0.0), rest = SplitAt(41, resting, 0.3f, 4, oneX, 0.0);
            if (one == 0 || resting == 0 || set.Part != SplitPart.Set || stroke.Part != SplitPart.Stroke || stroke.Beat is not (1 or 2)
                || split.Part != SplitPart.Split || split.Within < 0.9f || rest.Beat != PersonPainter.Breather)
            {
                faults.Add($"at 1×: 0.4 s in {set.Part}, 2 s in {stroke.Part}/{stroke.Beat}, 0.13 s left {split.Part} {split.Within:F2} apart, a resting split's start {rest.Part} (want set, a stroke, split and apart, the breather)");
            }

            bool quickOther = false;
            for (int i = 0; i <= 40; i++)
            {
                SplitMoment q = SplitAt(41, (ulong)(900 + i), i * 0.1f, 4, fourX, i * 0.07);
                quickOther |= q.Part != SplitPart.Stroke || !q.Quick;
            }

            if (quickOther)
            {
                faults.Add("at 4× a split set, rested or split (want strokes only)");
            }

            // ⭐ Shuffled by the person and the split; the same on a second ask.
            int ones = 0, mauls = 0, breathers = 0;
            for (ulong s = 5000; s < 5600; s++)
            {
                SplitMoment m = SplitAt(41, s, 0.1f, 4, oneX, 0.0);
                ones += m.Strokes == 1 && !m.Maul ? 1 : 0;
                mauls += m.Maul ? 1 : 0;
                breathers += m.Part == SplitPart.Rest ? 1 : 0;
                if (SplitAt(41, s, 0.1f, 4, oneX, 0.0) != m)
                {
                    faults.Add($"split {s}: asked twice, two answers");
                    break;
                }
            }

            if (ones is < 190 or > 290 || mauls is < 80 or > 160 || breathers is < 65 or > 135)
            {
                faults.Add($"600 splits: {ones} one-stroke, {mauls} mauls, {breathers} breathers (want about 240, 120, 100)");
            }

            // The poses: the axe's breather and the maul are their own, a woodcutter at work holds the axe.
            FigureKey woodcutter = PersonKeyOf(41, LifeStage.Adult, JobKind.Woodcutter, VillagerState.MakingFirewood, Vector2.Zero, null, false, false);
            byte[] raised = PersonPainter.Paint(woodcutter with { Beat = 1 }).Rgba, breath = PersonPainter.Paint(woodcutter with { Beat = PersonPainter.Breather }).Rgba,
                maul = PersonPainter.Paint(woodcutter with { Beat = PersonPainter.MalletRaise }).Rgba, still = PersonPainter.Paint(woodcutter with { Beat = 0 }).Rgba;
            bool distinct = !raised.AsSpan().SequenceEqual(breath) && !raised.AsSpan().SequenceEqual(maul) && !breath.AsSpan().SequenceEqual(still)
                && !maul.AsSpan().SequenceEqual(still);
            if (woodcutter.Hold != FigureHold.Tool || !distinct)
            {
                faults.Add($"a woodcutter at the block holds {woodcutter.Hold}; the breather and the maul each their own pose {distinct}");
            }

            return faults.Count == 0
                ? "[widths] woodcutter: ✅ the yard keeps its chopping block out beside the hut; at 1× the round set in a split's first 0.9 s, the strokes, the split and the halves apart, a breather opening one in six; one stroke two in five, the wedge and maul one in five, the same on a second ask; at 4× strokes only; the axe's breather and the maul their own poses"
                : $"[widths] woodcutter: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            return $"[widths] woodcutter: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}
