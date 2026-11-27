using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐⭐ THE FORESTER, FELLING AND PLANTING (A296, <c>specs/varied-work.md §8</c>; Joe: <i>"Start the forester"</i>, then <i>"just
/// power through all these"</i> — the recommended option taken). The sim's forester errand is one state, <c>Cutting</c>, at a tile
/// of the hut's work ground: felling when the tile is still forest, planting when it is not (<c>IsPlantingErrand</c>). Until now both
/// were drawn with the painter's spade, standing in the tree. Felling: <b>the axe</b>, standing in front of the trunk, strokes and a
/// breather now and then — the tree's fall when the tile changes is A198's. Planting: <b>the spade</b> — dig, stoop to set the
/// sapling, tread it in — the young sapling when the tile changes is A198's. ⛔ The view's, never the sim's.
/// </remarks>
public partial class VillageMap
{
    /// <summary>A forester felling stands this far from the tree's middle, tiles on screen: in front of the trunk, not in the crown.</summary>
    internal static readonly Vector2 BeforeTheTrunk = new(0.06f, 0.34f);

    /// <summary>Planting's shares: the dig, then setting the sapling, then treading it in.</summary>
    internal const float DigShare = 0.6f, SetShare = 0.25f;

    internal enum ForestPart
    {
        Rest,
        Fell,
        Dig,
        Set,
        Tread,
    }

    /// <summary>Where in an errand a forester is: the beat, the part, how far through it, felling or planting, quick.</summary>
    internal readonly record struct ForestMoment(int Beat, ForestPart Part, float Within, bool Felling, bool Quick);

    /// <summary>Pure: whether a forester's errand is a felling — their tile still forest (the sim's own test, read back).</summary>
    internal static bool IsFelling(SimWorld world, Villager villager) =>
        villager.ErrandX == 0 && villager.ErrandY == 0
            ? true
            : world.Map.Contains(new GridPos(villager.ErrandX, villager.ErrandY))
              && world.Map.TerrainAt(new GridPos(villager.ErrandX, villager.ErrandY)) == Terrain.Forest;

    /// <summary>
    /// ⭐ Pure: the moment of a forester's errand. Felling: a breather opening one in six (as the face's), then axe strokes on the
    /// people's clock. Planting: the dig (spade strokes) for the first 60 %, setting the sapling (stooped) for the next 25 %, treading
    /// it in for the rest. Under <see cref="QuickDigSeconds"/> strokes only.
    /// </summary>
    internal static ForestMoment ForestAt(int id, ulong startTick, float elapsedTicks, int lengthTicks, double secondsPerTick, double clock, bool felling)
    {
        int seed = unchecked((int)(startTick * 2654435761UL) ^ (id * 40503));
        double cycle = lengthTicks * secondsPerTick;
        double t = Math.Clamp(elapsedTicks, 0f, lengthTicks) * secondsPerTick;
        bool quick = cycle < QuickDigSeconds;
        float axe = PersonPainter.SwingSeconds(JobKind.Woodcutter, LifeStage.Adult);
        float spade = PersonPainter.SwingSeconds(JobKind.Forester, LifeStage.Adult);
        float Phase(float seconds) => (float)(((clock / seconds) + PersonPainter.Hash01(id, "beat")) % 1.0);

        if (felling)
        {
            double restFor = Math.Min(BreatherSeconds, cycle * 0.3);
            if (!quick && PersonPainter.Hash01(seed, "rest") < (1f / 6f) && t < restFor)
            {
                return new ForestMoment(PersonPainter.FellRest, ForestPart.Rest, (float)(t / restFor), true, false);
            }

            float p = Phase(axe);
            return new ForestMoment(p < 0.62f ? PersonPainter.FellRaise : PersonPainter.FellStrike, ForestPart.Fell, p, true, quick);
        }

        double u = cycle <= 0 ? 0 : t / cycle;
        if (quick || u < DigShare)
        {
            float p = Phase(spade);
            return new ForestMoment(p < 0.62f ? 1 : 2, ForestPart.Dig, quick ? p : (float)(u / DigShare), false, quick);
        }

        return u < DigShare + SetShare
            ? new ForestMoment(0, ForestPart.Set, (float)((u - DigShare) / SetShare), false, false)
            : new ForestMoment(0, ForestPart.Tread, (float)((u - DigShare - SetShare) / (1.0 - DigShare - SetShare)), false, false);
    }

    /// <summary>A forester at work on an errand this frame, or null.</summary>
    private ForestMoment? ForestryOf(SimWorld world, Villager villager, FigureKey still)
    {
        if (villager.State != VillagerState.Cutting || still.Trade != JobKind.Forester || still.Hold != FigureHold.Tool || still.Walking)
        {
            return null;
        }

        bool felling = IsFelling(world, villager);
        int baseTicks = felling ? world.Config.CutTicks : VillageEconomy.PlantTicks(world.Config);
        int length = Math.Max(1, world.WorkTicksFor(villager, JobKind.Forester, baseTicks));
        int remaining = Math.Clamp(villager.ActionTicksRemaining, 0, length);
        float elapsed = length - remaining + (float)_alpha;
        ulong start = world.Tick - (ulong)(length - remaining);
        double secondsPerTick = 1.0 / (world.Config.TargetTicksPerSecond * Math.Max(0.05, _flowPace));
        return ForestAt(villager.Id, start, elapsed, length, secondsPerTick, _peopleClock, felling);
    }

    /// <summary>Pure: the figure and motion for a forester's moment — the axe's strokes dipping the body; the breather leaning back;
    /// the spade's dig; stooped to set the sapling; a small tread up and down.</summary>
    internal static (FigureKey Key, PersonMotion Motion) ForestKeyAndMotion(FigureKey still, ForestMoment m) => m.Part switch
    {
        ForestPart.Rest => (still with { Beat = m.Beat },
            new PersonMotion(m.Beat, 0f, -0.004f * Mathf.Sin(Mathf.Pi * m.Within), -3.5f * Mathf.Sin(Mathf.Pi * m.Within), 1f)),
        ForestPart.Fell => (still with { Beat = m.Beat }, new PersonMotion(m.Beat, 0f, m.Beat == PersonPainter.FellStrike ? 0.012f : 0f, 0f, 1f)),
        ForestPart.Dig => (still with { Beat = m.Beat }, new PersonMotion(m.Beat, 0f, m.Beat == 2 ? 0.012f : 0f, 0f, m.Beat == 2 ? 0.97f : 1f)),
        ForestPart.Set => (still with { Beat = 0 }, new PersonMotion(0, 0f, 0.014f, 0f, 0.86f)),
        _ => (still with { Beat = 0 }, new PersonMotion(0, 0f, 0.006f * Mathf.Abs(Mathf.Sin(Mathf.Tau * 2f * m.Within)), 0f, 1f)),
    };
}
