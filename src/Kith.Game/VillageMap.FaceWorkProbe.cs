using Kith.Sim.World;
using Godot;

namespace Kith.Game;

public partial class VillageMap
{
    /// <summary>
    /// ⭐ The probe's <c>face work:</c> line (A282, <c>varied-work.md §5</c>) — printed with the people's lines. Every number is the
    /// spec's (a quarry dig of 4 ticks at 0.75 a second; the lift its last 1.5 s, or 45 % of a short dig; quick under 2 s; the wedge two digs in five,
    /// a breather one in six; 10 stone a dig, at most 6 blocks), never read back from the code's constants.
    /// </summary>
    /// <remarks>⚠️ Nothing is drawn headless (D340): the stack beside them and the wedge in the stone are the renders'.</remarks>
    private string TheFaceWorkHolds()
    {
        try
        {
            var faults = new List<string>();
            const double oneX = 1.0 / 0.75, fourX = 1.0 / 3.0;

            // ⭐ The pick stays in hand with stone held, at the quarry and the mine — and only there: a forager's basket of produce
            // is still in the arms.
            FigureKey quarrier = PersonKeyOf(31, LifeStage.Adult, JobKind.Quarrier, VillagerState.Quarrying, Vector2.Zero, Goods.Stone, false, false);
            FigureKey miner = PersonKeyOf(32, LifeStage.Adult, JobKind.Miner, VillagerState.Mining, Vector2.Zero, Goods.Iron, false, false);
            FigureKey forager = PersonKeyOf(33, LifeStage.Adult, JobKind.Forager, VillagerState.Gathering, Vector2.Zero, Goods.Produce, false, false);
            if (quarrier.Hold != FigureHold.Tool || quarrier.Good is not null || miner.Hold != FigureHold.Tool || forager.Hold != FigureHold.Front)
            {
                faults.Add($"holding their load: a quarrier {quarrier.Hold} with {quarrier.Good?.ToString() ?? "nothing"} drawn, a miner {miner.Hold}, a forager {forager.Hold} (want the pick, the stack beside them; the forager's arms)");
            }

            // The stack is the load: a block for each dig's worth begun, at most six.
            (int Held, int Want)[] stacks = { (0, 0), (10, 1), (25, 3), (40, 4), (80, 6) };
            foreach ((int held, int want) in stacks)
            {
                if (StackOf(held, 10) != want)
                {
                    faults.Add($"{held} stone stacks to {StackOf(held, 10)} blocks (want {want})");
                }
            }

            // ⭐ The lift falls in the dig's last 1.5 s, the breather in a resting dig's first 1.6 s; at 4× (a 1.3 s dig) neither.
            ulong plain = 0, resting = 0;
            for (ulong s = 500; s < 2000 && (plain == 0 || resting == 0); s++)
            {
                DigMoment early = DigAt(31, s, 0.5f, 4, oneX, 0.0);
                if (early.Part == DigPart.Work && plain == 0)
                {
                    plain = s;
                }

                if (early.Part == DigPart.Rest && resting == 0)
                {
                    resting = s;
                }
            }

            DigMoment lift = DigAt(31, plain, 3.6f, 4, oneX, 0.0), mid = DigAt(31, plain, 2.0f, 4, oneX, 0.0),
                rest = DigAt(31, resting, 0.5f, 4, oneX, 0.0), restLater = DigAt(31, resting, 2.0f, 4, oneX, 0.0);
            if (plain == 0 || resting == 0 || lift.Part != DigPart.Lift || mid.Part != DigPart.Work || rest.Beat != PersonPainter.Breather
                || restLater.Part != DigPart.Work)
            {
                faults.Add($"at 1×: 0.53 s left {lift.Part}, mid-dig {mid.Part}, a resting dig's start {rest.Part}/{rest.Beat} and later {restLater.Part} (want the lift, work, the breather, work)");
            }

            // ⭐ A skilled quarrier's 2-tick dig (2.7 s at 1×) still lifts — the first build's 3.5 s threshold never let it.
            DigMoment skilled = DigAt(31, plain, 1.8f, 2, oneX, 0.0);
            if (skilled.Part != DigPart.Lift || skilled.Quick)
            {
                faults.Add($"a 2-tick dig at 1×, 0.27 s left: {skilled.Part}{(skilled.Quick ? " quick" : "")} (want the lift)");
            }

            bool quickLifts = false;
            for (int i = 0; i <= 40; i++)
            {
                DigMoment q = DigAt(31, (ulong)(700 + i), i * 0.1f, 4, fourX, i * 0.07);
                quickLifts |= q.Part != DigPart.Work || !q.Quick;
            }

            if (quickLifts)
            {
                faults.Add("at 4× a dig lifted or rested (want digging only)");
            }

            // ⭐ Shuffled by the person and the dig: two in five the wedge, one in six a breather, the same on a second ask.
            int wedges = 0, breathers = 0;
            for (ulong s = 3000; s < 3600; s++)
            {
                DigMoment m = DigAt(31, s, 0.2f, 4, oneX, 0.0);
                wedges += m.Wedge ? 1 : 0;
                breathers += m.Part == DigPart.Rest ? 1 : 0;
                if (DigAt(31, s, 0.2f, 4, oneX, 0.0) != m)
                {
                    faults.Add($"dig {s}: asked twice, two answers");
                    break;
                }
            }

            if (wedges is < 190 or > 290 || breathers is < 65 or > 135)
            {
                faults.Add($"600 digs: {wedges} wedges, {breathers} breathers (want about 240 and 100)");
            }

            // The wedge's strikes are the mallet's, the pick's the pick's; the lift holds the block, stooped; every pose its own.
            DigMoment wedgeDig = default, pickDig = default;
            for (ulong s = 4000; s < 4200; s++)
            {
                DigMoment m = DigAt(31, s, 1.8f, 4, oneX, 0.3);
                if (m.Part == DigPart.Work && m.Wedge && wedgeDig.Beat == 0)
                {
                    wedgeDig = m;
                }

                if (m.Part == DigPart.Work && !m.Wedge && pickDig.Beat == 0)
                {
                    pickDig = m;
                }
            }

            // The lift's first third: stooped to take the block (1.33 s left of the dig).
            (FigureKey liftKey, PersonMotion liftMotion) = FaceKeyAndMotion(quarrier, DigAt(31, plain, 3.0f, 4, oneX, 0.0), Goods.Stone);
            bool mallet = wedgeDig.Beat is PersonPainter.MalletRaise or PersonPainter.MalletStrike;
            bool pick = pickDig.Beat is 1 or 2;
            bool lifted = liftKey.Hold == FigureHold.Front && liftKey.Good == Goods.Stone && !liftKey.ToolOut && liftMotion.Squash < 1f;
            byte[] raised = PersonPainter.Paint(quarrier with { Beat = 1 }).Rgba, malletUp = PersonPainter.Paint(quarrier with { Beat = PersonPainter.MalletRaise }).Rgba,
                breath = PersonPainter.Paint(quarrier with { Beat = PersonPainter.Breather }).Rgba, carry = PersonPainter.Paint(liftKey).Rgba;
            bool distinct = !raised.AsSpan().SequenceEqual(malletUp) && !raised.AsSpan().SequenceEqual(breath) && !raised.AsSpan().SequenceEqual(carry);
            if (!mallet || !pick || !lifted || !distinct)
            {
                faults.Add($"the wedge's beat {wedgeDig.Beat} (want the mallet's), the pick's {pickDig.Beat} (want 1 or 2), the lift holds the block stooped {lifted}, the poses each their own {distinct}");
            }

            return faults.Count == 0
                ? "[widths] face work: ✅ at the quarry and the mine the pick stays in hand with the load held, which stacks beside them a block a dig (at most six), a forager's basket still in the arms; at 1× the block lifted on in the dig's last 1.5 s, a breather opening one dig in six, the wedge and mallet two in five, the same on a second ask; at 4× digging only; mallet, breather and lift each their own pose"
                : $"[widths] face work: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            return $"[widths] face work: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}
