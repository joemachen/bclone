using System.Collections.Generic;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ A golden over the <b>seam</b> — a village that farms and clears ground at the same time.
/// </summary>
/// <remarks>
/// <para>
/// <b>D161's rule, taken literally: when two systems meet, the golden goes over the seam rather
/// than over either side.</b> `crops-and-orchards.md §9` names the case to write <em>with</em>
/// the feature: a laborer clearing painted harvest ground beside a ripe field — the crops ×
/// brush seam, which is the one that will silently eat a harvest.
/// </para>
/// <para>
/// <b>⛔ AND IT ALSO CLOSES D157'S OPEN HOLE, which is why it is worth more than one seam.</b>
/// Both fifty-year village goldens paint <b>zero</b> tiles across all 24,000 of their ticks —
/// <c>Zones.HarvestTiles</c> is 0 on every one of them — so <c>NearestHarvest</c> early-returns
/// and step C's central mechanic has had no drift guard at all since it shipped. D158 recorded
/// that as <em>"the first item on the other side"</em>. This is that item, arriving with the
/// slice that needed it anyway.
/// </para>
/// <para>
/// <b>⚠️ A GREEN GOLDEN CAN MEAN "NOT COVERED" RATHER THAN "NO-OP" (D157), so the coverage is
/// asserted rather than assumed.</b> That is the whole finding this file exists because of: the
/// two existing goldens did not move for step C, and the reason turned out to be that they never
/// reach the changed code. So <see cref="TheRunActuallyPaintsClearsSowsAndReaps"/> stands beside
/// the hash and fails if the scenario ever stops being the scenario — a hash over a village that
/// quietly stopped farming would be perfectly stable and perfectly worthless.
/// </para>
/// </remarks>
public sealed class FarmGoldenTests
{
    private readonly ITestOutputHelper _output;

    public FarmGoldenTests(ITestOutputHelper output) => _output = output;

    /// <summary>Years the scenario runs for.</summary>
    /// <remarks>
    /// <b>Twenty, not fifty.</b> Long enough for several full crop years and for the laborers to
    /// work through a real amount of painted ground, short enough that the guard costs seconds
    /// rather than minutes — the suite is already at eleven and D112 records what that costs.
    /// The two fifty-year goldens still watch the long arc; this one watches a mechanism.
    /// </remarks>
    private const int Years = 20;

    // ---------------------------------------------------------------
    //  The golden
    // ---------------------------------------------------------------
    //
    // ⭐ FIRST TAKEN with the farm (`specs/crops-and-orchards.md`, D161). There is no
    // "before" for this one: nothing could sow until this slice, so a run that farms has never
    // existed and this number describes a village the suite has never watched before.
    //
    // ⚠️ WHEN THIS MOVES, SAY WHY IN ONE SENTENCE AND WRITE IT HERE (D152). The two things that
    // will move it are the crop economy (`crop_yield_per_tile`, `sow_ticks`, `reap_ticks`,
    // `farm_store_cap`) and anything that changes what a laborer does with painted ground —
    // which is exactly the pair of systems it is watching.
    //
    // ⭐ RE-TAKEN FOR THE JITTER (D163), one commit after it was first taken, and **it moved for
    // a third reason neither of those** — which is worth recording rather than shrugging at. A
    // villager who came in to get warm now stays until they are warm, instead of being flipped
    // back out on the next tick with their arms still full. This run spans twenty years and
    // twenty winters, and everybody in it now spends ticks at a hearth they used to spend
    // walking, so the whole history downstream differs.
    //
    //   before the jitter was fixed: 7924144676203476477
    private const ulong SeamGoldenHash = 12419260555584221460UL;

    /// <summary>The seam, in one number.</summary>
    [Fact]
    public void AVillageThatFarmsAndClearsAtOnceRunsTheSameWayEveryTime()
    {
        SimWorld world = RunTheScenario(out _);

        ulong actual = StateHash.Compute(world);
        _output.WriteLine($"crops × harvest brush, {Years}y: {actual}");

        Assert.Equal(SeamGoldenHash, actual);
    }

    /// <summary>
    /// ⭐ The scenario is actually the scenario — <b>the anti-vacuity half, and the point</b>.
    /// </summary>
    /// <remarks>
    /// <b>D157's lesson in a guard.</b> Both fifty-year goldens were expected to move for step C
    /// and did not, and the reason was measured rather than assumed: they paint zero tiles, so
    /// the changed code is never reached. A hash is only evidence about the code it executes,
    /// and the only way to know it executes any is to count.
    /// </remarks>
    [Fact]
    public void TheRunActuallyPaintsClearsSowsAndReaps()
    {
        RunTheScenario(out Coverage seen);

        _output.WriteLine(
            $"over {Years} years: {seen.PaintedAtMost} tiles still painted, "
            + $"{seen.Cleared} ticks spent clearing, {seen.SownAtMost} sown at once, "
            + $"{seen.ReapedTotal} reaped, {seen.FoodBuffered} food buffered at the farm");

        Assert.True(seen.PaintedAtMost > 0, "Nothing was ever painted for harvest.");
        Assert.True(seen.Cleared > 0, "No painted tile was ever cleared — D157's hole is still open.");
        Assert.True(seen.SownAtMost > 0, "Nothing was ever sown, so the golden watches no crop.");
        Assert.True(seen.ReapedTotal > 0, "Nothing was ever reaped.");
        Assert.True(
            seen.FoodBuffered > 0,
            "Nothing ever reached the farm's own store, so the run does not cover it.");
    }

    /// <summary>
    /// ⛔⛔ And the laborers never once took a tile of the farm.
    /// </summary>
    /// <remarks>
    /// <b>The seam itself, asserted over a live run rather than at the predicate.</b> D144's
    /// finding is that a rule tested only where it is decided is a rule nobody has tested — five
    /// guards asked <c>Accepts</c> and not one made a villager put anything down. So this counts
    /// the standing crop through twenty years of a village whose laborers are clearing ground
    /// all around it: if a field ever vanishes on a tick when no farmer reaped it, the brush has
    /// eaten the harvest.
    /// </remarks>
    [Fact]
    public void NoLaborerEverClearedAStandingCrop()
    {
        RunTheScenario(out Coverage seen);

        _output.WriteLine(
            $"{seen.ReapedTotal} tiles reaped by farmers, {seen.RottedOrTaken} lost to winter, "
            + $"{seen.VanishedUnexplained} vanished with nobody reaping and no winter");

        Assert.Equal(0, seen.VanishedUnexplained);
    }

    // ---------------------------------------------------------------

    private sealed class Coverage
    {
        internal int PaintedAtMost;
        internal int Cleared;
        internal int SownAtMost;
        internal int ReapedTotal;
        internal int RottedOrTaken;
        internal int VanishedUnexplained;
        internal int FoodBuffered;
    }

    /// <summary>
    /// A village with a farm, a field, and a wood painted for harvest right beside it.
    /// </summary>
    /// <remarks>
    /// <b>The fixture village, not the shipped cold start.</b> D143 rules that an unattended
    /// founding is supposed to die out, so a twenty-year scenario begun in an empty valley would
    /// be watching a settlement fail rather than watching two systems meet. The player's part
    /// here — a farmhouse, its ground, and a painted wood — is done once at the start and never
    /// reacted to, which is `PlayTheOpening`'s shape and is what keeps the run reproducible.
    /// </remarks>
    private static SimWorld RunTheScenario(out Coverage seen)
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        var coverage = new Coverage();

        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 2);

        // The wood beside the field, painted for harvest — the other half of the seam. Painted
        // in a ring OUTSIDE the farm's ground so the two layers are adjacent rather than
        // overlapping: work ground and harvest paint are different zones (D87) and a tile can
        // carry both, but the case that eats a harvest is the laborer walking past the field on
        // the way to a tree, which wants them to be neighbours.
        for (int dy = -6; dy <= 6; dy++)
        {
            for (int dx = -6; dx <= 6; dx++)
            {
                var at = new GridPos(farm.Position.X + dx, farm.Position.Y + dy);
                if (world.Map.Contains(at) && world.Zones.WorkGroundOwner(at) == 0)
                {
                    world.PaintHarvest(at);
                }
            }
        }

        int standing = StandingCrop(world, farm);

        // ⚠️ THE SEASON THE SYSTEMS ARE ABOUT TO RUN ON, SAMPLED BEFORE THE STEP — and this is
        // the harness bug that reads exactly like a broken feature. `SimLoop.StepOnce` runs the
        // systems and *then* advances the tick, so reading `Clock.Season` afterwards gives the
        // season of the tick that has not happened yet. Attributing the winter rot off that
        // reading reported **0 lost to winter and 344 vanished unexplained** — a harvest
        // apparently being eaten by the harvest brush, when what had actually happened is that
        // the guard was looking one tick to the right of the event.
        Season runningOn = world.Clock.Season;
        Season ranOnBefore = runningOn;

        for (int tick = 0; tick < config.TicksPerYear * Years; tick++)
        {
            loop.StepOnce();

            int sown = Sown(world, farm);
            if (sown > coverage.SownAtMost)
            {
                coverage.SownAtMost = sown;
            }

            if (farm.Store.Food > coverage.FoodBuffered)
            {
                coverage.FoodBuffered = farm.Store.Food;
            }

            // A laborer taking a painted tile is what step C is for, and D157 records that
            // neither existing golden ever reaches that code. Counted from the villagers
            // rather than from the paint, because D127 made the paint a STANDING instruction
            // whose wood grows back — so the painted count is flat while the clearing happens.
            for (int i = 0; i < world.Villagers.Count; i++)
            {
                if (world.Villagers[i].State == VillagerState.Clearing)
                {
                    coverage.Cleared++;
                    break;
                }
            }

            // A tile of standing crop that went away this tick was either reaped by a farmer
            // (somebody is carrying it), taken by winter (`CropSystem` on the season turn), or
            // it is the seam this whole file is about.
            int now = StandingCrop(world, farm);
            if (now < standing)
            {
                int lost = standing - now;
                if (runningOn == Season.Winter && ranOnBefore == Season.Fall)
                {
                    coverage.RottedOrTaken += lost;
                }
                else if (SomebodyIsReaping(world, farm))
                {
                    coverage.ReapedTotal += lost;
                }
                else
                {
                    coverage.VanishedUnexplained += lost;
                }
            }

            standing = now;
            ranOnBefore = runningOn;
            runningOn = world.Clock.Season;
        }

        coverage.PaintedAtMost = world.Zones.HarvestTiles;
        seen = coverage;
        return world;
    }

    private static int StandingCrop(SimWorld world, Workplace farm)
    {
        IReadOnlyList<int> owned = world.Zones.WorkGroundOf(farm.Id);
        int standing = 0;

        for (int i = 0; i < owned.Count; i++)
        {
            if (SimWorld.IsStandingCrop(world.Map.TerrainAt(world.Zones.PositionOf(owned[i]))))
            {
                standing++;
            }
        }

        return standing;
    }

    private static int Sown(SimWorld world, Workplace farm)
    {
        IReadOnlyList<int> owned = world.Zones.WorkGroundOf(farm.Id);
        int sown = 0;

        for (int i = 0; i < owned.Count; i++)
        {
            if (world.Map.TerrainAt(world.Zones.PositionOf(owned[i])) == Terrain.Sown)
            {
                sown++;
            }
        }

        return sown;
    }

    /// <summary>Whether anybody at this farm is mid-reap or carrying a harvest home.</summary>
    private static bool SomebodyIsReaping(SimWorld world, Workplace farm)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (villager.WorkplaceId != farm.Id)
            {
                continue;
            }

            if (villager.State is VillagerState.Reaping or VillagerState.HaulingToFarm
                or VillagerState.HaulingToStore or VillagerState.TravelingToField
                or VillagerState.TravelingHome)
            {
                return true;
            }
        }

        return false;
    }
}
