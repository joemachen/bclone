using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ The golden over Phase 5's chains — a posed village with every production building in it, on the shipped data.
/// </summary>
/// <remarks>
/// <para>
/// <b>Phase 5's exit gate, item 2</b> (`specs/phase-5-golden.md`). D642's audit found that no pinned golden reached the
/// smithy, the quarry, the mine, the mill, the bakery, the well, a bridge, the trading post and its boat, or the barn and
/// its herd. Nothing unattended ever builds them, so they are posed — the unlock gates skipped, each unlock keeps its own
/// tests — and **this golden pins the chains, not the unlocks.**
/// </para>
/// <para>
/// <b>⚠️ The pose plays like a player, and that is measured, not assumed (D648, §11).</b> A farm handed over finished at
/// tick 0 took two of four founders off the wood all year and froze them; the farm is <em>marked</em> here instead, and
/// its ground painted the tick it stands. The bridge waits for the village to have held 10 (its unlock is the villagers'
/// own history and cannot be posed), and the herd comes by boat on an order rather than standing at tick 0 (D643).
/// </para>
/// <para>
/// <b>⛔ A GREEN GOLDEN CAN MEAN "NOT COVERED" (D157)</b>, so <see cref="EveryPhaseFiveChainTurnsOver"/> stands beside the
/// hash in the same run and fails if any chain stops turning over — exactly the state D642 found every golden in.
/// </para>
/// </remarks>
public sealed class PhaseFiveGoldenTests
{
    private readonly ITestOutputHelper _output;

    public PhaseFiveGoldenTests(ITestOutputHelper output) => _output = output;

    /// <summary>Years the pose runs for.</summary>
    /// <remarks>
    /// <b>Twenty, measured (§11.1).</b> Every chain has turned over by year 18 — the herd lands in year 11, the first cull
    /// comes in year 16, tools in years 15 and 18 — and nobody dies before year 29, so the pinned village is one that lives.
    /// </remarks>
    private const int Years = 20;

    // ⭐ FIRST TAKEN with the pose (D648). ⚠️ WHEN THIS MOVES, SAY WHY IN ONE SENTENCE AND WRITE IT HERE (D152): any change
    // to a Phase 5 chain will move it, which is the point of it (§5.6).
    private const ulong PhaseFiveGoldenHash = 9279994292569338767UL;

    private const ulong PhaseFiveBeforeAnybodyGotBetter = 6069163189267979265UL;

    // One run for both facts: twenty posed years are the dearest thing in this file, and determinism makes sharing them safe.
    private static readonly Lazy<(ulong Hash, ulong WithoutSkills, Coverage Seen)> Run = new(PoseAndStep);

    [Fact]
    public void ThePosedPhaseFiveVillageRunsTheSameWayEveryTime()
    {
        (ulong actual, ulong withoutSkills, _) = Run.Value;

        _output.WriteLine($"hash after {Years} years: {actual}; ignoring skills: {withoutSkills}");

        Assert.Equal(PhaseFiveGoldenHash, actual);
        Assert.Equal(PhaseFiveBeforeAnybodyGotBetter, withoutSkills);
    }

    [Fact]
    public void EveryPhaseFiveChainTurnsOver()
    {
        Coverage seen = Run.Value.Seen;

        _output.WriteLine(
            $"over {Years} years: wheat {seen.Wheat}, flour {seen.Flour}, bread {seen.Bread}, stone quarried {seen.Stone}, "
            + $"iron mined {seen.Iron}, tools forged {seen.Tools}, water-drawing ticks {seen.WaterTicks}, crossings {seen.Crossings}, "
            + $"days a boat was docked {seen.DockedDays}, cattle landed {seen.Landed}, calves born {seen.Born}, leather {seen.Leather}; "
            + $"alive {seen.Alive} of {seen.People}");

        Assert.True(seen.Wheat > 0, "Nothing was reaped: the marked farm never farmed.");
        Assert.True(seen.Flour > 0, "Nothing was ground at the mill.");
        Assert.True(seen.Bread > 0, "Nothing was baked at the bakery.");
        Assert.True(seen.Stone > 0, "The quarry dug nothing.");
        Assert.True(seen.Iron > 0, "The mine dug nothing.");
        Assert.True(seen.Tools > 0, "The smithy forged nothing.");
        Assert.True(seen.WaterTicks > 0, "Nobody ever drew water at the well.");
        Assert.True(seen.Crossings > 0, "Nobody crossed the bridge.");
        Assert.True(seen.DockedDays > 0, "No boat ever docked at the post.");
        Assert.True(seen.Landed > 0, "No boat ever landed cattle, so the trade bought nothing.");
        Assert.True(seen.Born > 0, "No calf was ever born in the barn.");
        Assert.True(seen.Leather > 0, "No beast was ever culled into leather.");
    }

    // ---------------------------------------------------------------

    private sealed class Coverage
    {
        internal int Wheat;
        internal int Flour;
        internal int Bread;
        internal int Stone;
        internal int Iron;
        internal int Tools;
        internal int WaterTicks;
        internal int Crossings;
        internal int DockedDays;
        internal int Landed;
        internal int Born;
        internal int Leather;
        internal int Alive;
        internal int People;
    }

    /// <summary>§11.1's pose, stepped <see cref="Years"/> years, with what each chain did along the way.</summary>
    private static (ulong, ulong, Coverage) PoseAndStep()
    {
        SimConfig config = ShippedConfig.Established();
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        var seen = new Coverage();

        // Marked, as a player marks one (D648): a finished farm at tick 0 outranks the founders' firewood all year.
        Assert.True(world.Mark(BuildingKind.Farmhouse, FarmFixtures.ClearGroundNear(world)).Allowed);
        FoodChainTests.RaiseA(world, BuildingKind.Mill);
        FoodChainTests.RaiseA(world, BuildingKind.Bakery);
        Workplace quarry = QuarryTests.RaiseAQuarry(world, QuarryTests.TheNearestReachableRock(world));
        QuarryTests.GiveItRock(world, quarry, 6);
        Workplace mine = MineTests.RaiseAMine(world, MineTests.TheNearestReachable(world, Terrain.IronDeposit));
        MineTests.GiveItIron(world, mine, 6);
        ToolsTests.RaiseASmithy(world);
        ObstacleTests.RaiseAWellNearTheFounding(world);
        TradeTests.RaiseAPost(world);
        Workplace barn = HerdTests.RaiseABarn(world);
        HerdTests.GivePasture(world, barn, 20);
        world.TradeKeep.Set(Goods.Firewood, 300);
        world.TradeBuy.Set(Goods.Stone, 50);
        Assert.True(world.SetTradeBuyHead(4).Allowed);

        Workplace? farm = null;
        Bridge? bridge = null;
        int lastLeather = 0, lastHead = 0;
        var carried = new Dictionary<int, int>();
        for (int t = 0; t < Years * config.TicksPerYear; t++)
        {
            loop.StepOnce();

            if (farm is null && world.Workplaces.FirstOrDefault(w => w.Kind == JobKind.Farmer && !w.IsSite) is Workplace stood)
            {
                farm = stood;
                FarmFixtures.GiveItGround(world, farm, reach: 2);
            }

            if (bridge is null && world.MostEverAlive >= config.BridgeUnlockPopulation)
            {
                HashSet<GridPos> before = BridgeTests.Reached(world);
                bridge = BridgeTests.RaiseABridge(world, BridgeTests.ACrossing(world));
                Assert.True(world.Mark(BuildingKind.Well, BridgeTests.AcrossTheRiver(world, before, bridge)).Allowed);
            }

            if (barn.HerdHead > 0 && !world.Villagers.Any(v => v.Alive && v.CanWork && v.PinnedTrade == JobKind.Herdsman))
            {
                Villager hand = world.Villagers.Where(v => v.Alive && v.CanWork).OrderByDescending(v => v.Id).First();
                world.SetPinnedTrade(hand, JobKind.Herdsman);
            }

            // A dig goes into the digger's arms and is hauled to the nearest store, never the face's own (D385), so it is
            // counted where it happens: stone or iron arriving in the arms of whoever is seated at the quarry or the mine.
            foreach (Villager v in world.Villagers)
            {
                Goods? dug = v.WorkplaceId == quarry.Id ? Goods.Stone : v.WorkplaceId == mine.Id ? Goods.Iron : null;
                int now = dug is Goods g ? v.Carried[g] : 0;
                int rise = now - carried.GetValueOrDefault(v.Id);
                if (rise > 0 && dug == Goods.Stone)
                {
                    seen.Stone += rise;
                }
                else if (rise > 0)
                {
                    seen.Iron += rise;
                }

                carried[v.Id] = now;
            }

            seen.Leather += Math.Max(0, barn.Store[Goods.Leather] - lastLeather);
            lastLeather = barn.Store[Goods.Leather];

            // A head more while a boat is docked was landed by it; a head more on any other tick was born.
            if (barn.HerdHead > lastHead)
            {
                if (world.BoatIsDocked)
                {
                    seen.Landed += barn.HerdHead - lastHead;
                }
                else
                {
                    seen.Born += barn.HerdHead - lastHead;
                }
            }

            lastHead = barn.HerdHead;
            seen.WaterTicks += world.Villagers.Count(v => v.Alive && v.State == VillagerState.DrawingWater);
            if (world.BoatIsDocked && world.Tick % (ulong)config.TicksPerDay == 0)
            {
                seen.DockedDays++;
            }
        }

        seen.Wheat = world.WheatEverReaped;
        seen.Flour = world.FlourEverGround;
        seen.Bread = world.BreadEverBaked;
        seen.Tools = world.ToolsEverForged;
        seen.Crossings = world.BridgeCrossings;
        seen.Alive = world.Villagers.Count(v => v.Alive);
        seen.People = world.Villagers.Count;

        return (StateHash.Compute(world), StateHash.ComputeIgnoringSkills(world), seen);
    }
}
