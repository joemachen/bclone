using Kith.Sim.World;
using Godot;

namespace Kith.Game;

public partial class VillageMap
{
    /// <summary>
    /// ⭐ The probe's <c>brewer and keeper:</c> line (A203–A204, <c>tavern-figures-art.md §5</c>), riding on
    /// <c>painted villagers:</c> (art trap 73). Every colour and place is named here from the spec — the leather apron
    /// <c>#6a4a34</c>, the yard's oak <c>#8a6440</c> and iron <c>#4b4e54</c>, the hem at the ankle (−0.05 of the height) and at
    /// the calf (−0.15), the cloth on the shoulder — never read back from the painter's own tables (art traps 17, 25).
    /// </summary>
    /// <remarks>⚠️ How they <i>look</i> is the mock's (the same painter) and Joe's play.</remarks>
    private static string TheBrewerAndKeeperHold()
    {
        try
        {
            var faults = new List<string>();
            var leather = new Color("#6a4a34");
            var linen = new Color("#f0ebdf");
            var oak = new Color("#8a6440");
            var hoop = new Color("#4b4e54");
            var wood = new Color("#8a6038");
            var blob = new Color("#b4b4b4");
            FigureKey Still(JobKind? trade, FigurePose pose = FigurePose.Front, LifeStage stage = LifeStage.Adult) =>
                new(41, stage, trade, pose, false, FigureHold.None, pose != FigurePose.Front, false, null, false, false, false);

            // §3.1: the brewer's apron is leather where the baker's is linen, between the belt and the hem.
            PaintedFigure brewer = PersonPainter.Paint(Still(JobKind.Brewer)), baker = PersonPainter.Paint(Still(JobKind.Baker));
            int brewerLeather = Count(brewer, leather, -0.08f, 0.08f, -0.45f, -0.2f), brewerLinen = Count(brewer, linen, -0.08f, 0.08f, -0.45f, -0.2f);
            int bakerLeather = Count(baker, leather, -0.08f, 0.08f, -0.45f, -0.2f), bakerLinen = Count(baker, linen, -0.08f, 0.08f, -0.45f, -0.2f);
            if (brewerLeather < 60 || brewerLinen > 5 || bakerLinen < 60 || bakerLeather > 5)
            {
                faults.Add($"the aprons: the brewer's {brewerLeather} leather and {brewerLinen} linen px, the baker's {bakerLeather} and {bakerLinen} (want the brewer 60+ leather and the baker 60+ linen, each ≤5 of the other)");
            }

            // §3.1: the oar, carried to the brewery — a pole above the head with the tool out, nothing there without it.
            FigureKey toWork = Still(JobKind.Brewer) with { Hold = FigureHold.Tool, Walking = true, ToolOut = true };
            PaintedFigure oar = PersonPainter.Paint(toWork), noOar = PersonPainter.Paint(toWork with { Hold = FigureHold.None, ToolOut = false });
            int poleAbove = Count(oar, wood, 0.1f, 0.35f, -1.12f, -1.02f), blade = Count(oar, new Color("#b08a5a"), 0.1f, 0.35f, -0.27f, 0f);
            int noPole = Count(noOar, wood, 0.1f, 0.35f, -1.12f, -1.02f);
            if (poleAbove < 4 || blade < 20 || noPole > 0)
            {
                faults.Add($"the oar: {poleAbove} px of pole above the head and {blade} of blade at the foot with the tool out, {noPole} without (want 4+, 20+, 0)");
            }

            // §3.2: the keeper's gown (and apron) to the ankle — linen at −0.1 of the height, where the calf-length baker has none.
            PaintedFigure keeper = PersonPainter.Paint(Still(JobKind.TavernKeeper));
            int keeperLow = Count(keeper, linen, -0.06f, 0.06f, -0.11f, -0.07f), bakerLow = Count(baker, linen, -0.06f, 0.06f, -0.11f, -0.07f);
            if (keeperLow < 10 || bakerLow > 0)
            {
                faults.Add($"the hem: {keeperLow} px of the keeper's apron at the ankle, {bakerLow} of the baker's (want 10+, 0)");
            }

            // §3.2: the cloth over one shoulder, front, back and side; none on the baker; a child keeper is any child.
            int clothFront = Count(keeper, linen, -0.16f, -0.075f, -0.74f, -0.58f);
            int clothBack = Count(PersonPainter.Paint(Still(JobKind.TavernKeeper, FigurePose.Back)), linen, 0.075f, 0.16f, -0.74f, -0.6f);
            int clothSide = Count(PersonPainter.Paint(Still(JobKind.TavernKeeper, FigurePose.Side)), linen, -0.075f, 0.025f, -0.74f, -0.6f);
            int bakerShoulder = Count(baker, linen, -0.16f, -0.075f, -0.74f, -0.58f);
            bool childPlain = PersonPainter.Paint(Still(JobKind.TavernKeeper, stage: LifeStage.Child)).Rgba.AsSpan()
                .SequenceEqual(PersonPainter.Paint(Still(null, stage: LifeStage.Child)).Rgba);
            if (clothFront < 15 || clothBack < 15 || clothSide < 10 || bakerShoulder > 2 || !childPlain)
            {
                faults.Add($"the cloth: {clothFront} px front, {clothBack} back, {clothSide} side, {bakerShoulder} on the baker's shoulder, a child keeper {(childPlain ? "any child" : "drawn differently")} (want 15+, 15+, 10+, ≤2, any child)");
            }

            // §3.3: ale is a cask in the arms — oak and hoops at the chest, front and side, for anyone; hidden walking away.
            FigureKey ale = Still(JobKind.TavernKeeper) with { Hold = FigureHold.Front, Walking = true, Good = Goods.Ale };
            PaintedFigure caskFront = PersonPainter.Paint(ale), caskSide = PersonPainter.Paint(ale with { Pose = FigurePose.Side });
            PaintedFigure caskBack = PersonPainter.Paint(ale with { Pose = FigurePose.Back }), porter = PersonPainter.Paint(ale with { Trade = null });
            int oakFront = Count(caskFront, oak, -0.11f, 0.11f, -0.68f, -0.39f), hoopFront = Count(caskFront, hoop, -0.11f, 0.11f, -0.68f, -0.39f);
            int oakSide = Count(caskSide, oak, -0.05f, 0.19f, -0.68f, -0.39f), oakPorter = Count(porter, oak, -0.11f, 0.11f, -0.68f, -0.39f);
            PaintedFigure emptyArms = PersonPainter.Paint(ale with { Good = null });
            bool hiddenBehind = caskBack.Rgba.AsSpan().SequenceEqual(PersonPainter.Paint(ale with { Pose = FigurePose.Back, Good = null }).Rgba);
            int grey = Count(caskFront, blob, -0.3f, 0.3f, -0.8f, -0.3f), greyWithout = Count(emptyArms, blob, -0.3f, 0.3f, -0.8f, -0.3f);
            if (oakFront < 60 || hoopFront < 10 || oakSide < 40 || oakPorter < 60 || !hiddenBehind || grey > greyWithout)
            {
                faults.Add($"the cask: {oakFront} oak and {hoopFront} hoop px at the chest, {oakSide} side on, {oakPorter} on a porter, walking away {(hiddenBehind ? "hidden" : "drawn")}, {grey} px of the old grey against {greyWithout} with empty arms (want 60+, 10+, 40+, 60+, hidden, no more)");
            }

            // §3.4: the two colours kept as chosen.
            if (TradeGlyph.ColourOf(JobKind.Brewer) != new Color("#9a5e14") || TradeGlyph.ColourOf(JobKind.TavernKeeper) != new Color("#8a2f3e"))
            {
                faults.Add($"the colours: the brewer {TradeGlyph.ColourOf(JobKind.Brewer).ToHtml(false)}, the keeper {TradeGlyph.ColourOf(JobKind.TavernKeeper).ToHtml(false)} (want 9a5e14, 8a2f3e)");
            }

            return faults.Count == 0
                ? $"[widths] brewer and keeper: ✅ the brewer's apron leather ({brewerLeather} px) where the baker's is linen ({bakerLinen}); the oar above the head with the tool out ({poleAbove} px, a {blade} px blade), none without; the keeper's apron to the ankle ({keeperLow} px, the baker's {bakerLow}); the cloth on the shoulder front {clothFront}, back {clothBack}, side {clothSide}, none on the baker, a child keeper any child; ale a cask in the arms — {oakFront} oak and {hoopFront} hoop px, {oakSide} side on, {oakPorter} on a porter, none walking away, no grey; the brewer #9a5e14 and the keeper #8a2f3e"
                : $"[widths] brewer and keeper: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            return $"[widths] brewer and keeper: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }

    /// <summary>Opaque pixels within <paramref name="within"/> a channel of a colour (22: the painted wash darkens a fill ~15), inside a box given in figure units (the feet at 0, the crown at −1).</summary>
    private static int Count(PaintedFigure f, Color want, float x0, float x1, float y0, float y1, int within = 22)
    {
        float unit = f.Height / PersonPainter.CanvasH; // the figure's height in pixels (a child's 0.62 of an adult's)
        int r = (int)Mathf.Round(want.R * 255f), g = (int)Mathf.Round(want.G * 255f), b = (int)Mathf.Round(want.B * 255f);
        int n = 0;
        for (int y = Mathf.Max(0, (int)(f.FeetY + (y0 * unit))); y < Mathf.Min(f.Height, (int)(f.FeetY + (y1 * unit)) + 1); y++)
        {
            for (int x = Mathf.Max(0, (int)(f.FeetX + (x0 * unit))); x < Mathf.Min(f.Width, (int)(f.FeetX + (x1 * unit)) + 1); x++)
            {
                int k = ((y * f.Width) + x) * 4;
                if (f.Rgba[k + 3] >= 250 && System.Math.Abs(f.Rgba[k] - r) <= within && System.Math.Abs(f.Rgba[k + 1] - g) <= within && System.Math.Abs(f.Rgba[k + 2] - b) <= within)
                {
                    n++;
                }
            }
        }

        return n;
    }
}
