using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

public partial class VillageMap
{
    /// <summary>
    /// ⭐ The probe's <c>forester:</c> line (A296, <c>varied-work.md §8</c>) — printed with the people's lines. Every number is the
    /// spec's (a fell of 4 ticks and a planting of 12 at 0.75 a second; a breather one fell in six; the dig 60 %, the set 25 %; quick
    /// under 2 s), never read back from the code's constants.
    /// </summary>
    /// <remarks>⚠️ Nothing is drawn headless (D340): the axe at the trunk and the sapling set are the renders'.</remarks>
    private string TheForesterHolds()
    {
        try
        {
            var faults = new List<string>();
            const double oneX = 1.0 / 0.75, fourX = 1.0 / 3.0;

            // Felling: axe strokes, a breather one in six; at 4× strokes only.
            int rests = 0, strokes = 0;
            for (ulong s = 7000; s < 7600; s++)
            {
                ForestMoment m = ForestAt(51, s, 0.2f, 4, oneX, s * 0.13, true);
                rests += m.Part == ForestPart.Rest ? 1 : 0;
                strokes += m.Part == ForestPart.Fell && m.Beat is PersonPainter.FellRaise or PersonPainter.FellStrike ? 1 : 0;
            }

            bool quickRest = false;
            for (int i = 0; i <= 40; i++)
            {
                quickRest |= ForestAt(51, (ulong)(800 + i), i * 0.1f, 4, fourX, i * 0.07, true).Part != ForestPart.Fell;
            }

            if (rests is < 65 or > 135 || strokes + rests != 600 || quickRest)
            {
                faults.Add($"600 fells: {rests} breathers (want about 100), {strokes} at the axe; at 4× a rest {quickRest} (want strokes only)");
            }

            // Planting, a 12-tick errand at 1×: the dig, the set, the tread.
            ForestMoment dig = ForestAt(51, 900, 3f, 12, oneX, 0.0, false), set = ForestAt(51, 900, 9f, 12, oneX, 0.0, false),
                tread = ForestAt(51, 900, 11.5f, 12, oneX, 0.0, false);
            if (dig.Part != ForestPart.Dig || dig.Beat is not (1 or 2) || set.Part != ForestPart.Set || tread.Part != ForestPart.Tread)
            {
                faults.Add($"planting at a quarter {dig.Part}/{dig.Beat}, at three quarters {set.Part}, at the end {tread.Part} (want the dig, the set, the tread)");
            }

            (_, PersonMotion stoop) = ForestKeyAndMotion(PersonKeyOf(51, LifeStage.Adult, JobKind.Forester, VillagerState.Cutting, Vector2.Zero, null, false, false), set);
            if (stoop.Squash >= 1f)
            {
                faults.Add("setting the sapling does not stoop");
            }

            // The painter: the fell's beats are the axe's, not the spade's; the spade's dig stays the spade's.
            FigureKey forester = PersonKeyOf(51, LifeStage.Adult, JobKind.Forester, VillagerState.Cutting, Vector2.Zero, null, false, false);
            FigureKey stoneInTheLoad = PersonKeyOf(52, LifeStage.Adult, JobKind.Forester, VillagerState.Cutting, Vector2.Zero, Goods.Stone, false, false);
            if (stoneInTheLoad.Hold != FigureHold.Tool)
            {
                faults.Add($"a forester planting with stone in the load holds {stoneInTheLoad.Hold} (want the spade: his Willowdale's Verren hugged 24 stone)");
            }

            byte[] spadeUp = PersonPainter.Paint(forester with { Beat = 1 }).Rgba, axeUp = PersonPainter.Paint(forester with { Beat = PersonPainter.FellRaise }).Rgba,
                axeDown = PersonPainter.Paint(forester with { Beat = PersonPainter.FellStrike }).Rgba, rest = PersonPainter.Paint(forester with { Beat = PersonPainter.FellRest }).Rgba,
                plain = PersonPainter.Paint(forester with { Beat = 0 }).Rgba;
            if (forester.Hold != FigureHold.Tool || spadeUp.AsSpan().SequenceEqual(axeUp) || axeUp.AsSpan().SequenceEqual(axeDown)
                || rest.AsSpan().SequenceEqual(plain))
            {
                faults.Add($"a forester at work holds {forester.Hold}; the axe's raise, stroke and breather each their own and not the spade's");
            }

            // ⭐ At a standing tree they stand before its trunk; on open ground (planting) where they are.
            if (PaintsPeople && _world is SimWorld world)
            {
                GridPos? forest = null, open = null;
                for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height && (forest is null || open is null); y++)
                {
                    for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width && (forest is null || open is null); x++)
                    {
                        var g = new GridPos(x, y);
                        if (world.Map.TerrainAt(g) == Terrain.Forest)
                        {
                            forest ??= g;
                        }
                        else if (world.Map.TerrainAt(g) == Terrain.Grass && world.DoorCovering(g) is null)
                        {
                            open ??= g;
                        }
                    }
                }

                if (forest is GridPos f && open is GridPos o)
                {
                    var ft = new Vector2(f.X, f.Y);
                    var ot = new Vector2(o.X, o.Y);
                    Vector2 felling = AtTheDoor(ft, VillagerState.Cutting), planting = AtTheDoor(ot, VillagerState.Cutting);
                    if (felling.DistanceTo(ft + new Vector2(0.06f, 0.34f)) > 1e-3f || planting.DistanceTo(ot) > 1e-3f)
                    {
                        faults.Add($"felling at {ft} stands at {felling} (want before the trunk), planting at {ot} at {planting} (want where they are)");
                    }
                }
            }

            return faults.Count == 0
                ? "[widths] forester: ✅ felling with the axe before the trunk, a breather opening one fell in six, strokes only at 4×; planting with the spade — the dig, stooped to set the sapling, the tread; the axe's raise, stroke and breather its own poses, the spade's dig the spade's"
                : $"[widths] forester: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            return $"[widths] forester: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}
