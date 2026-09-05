using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Which goods a store will take, set at the building level — Joe, D141.
/// </summary>
/// <remarks>
/// <i>"User should be able to set which materials are stored in which buildings — e.g. a given
/// storage pile will only accept logs, another only firewood, another only iron ore."</i>
/// </remarks>
public sealed class StoreFilterTests
{
    private readonly ITestOutputHelper _output;

    public StoreFilterTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimWorld World() =>
        SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;

    private static StoreBuilding WarehouseIn(SimWorld world)
    {
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            if (store.Kind == StoreKind.Warehouse)
            {
                return store;
            }
        }

        throw new Xunit.Sdk.XunitException("No warehouse in the founding village.");
    }

    /// <summary>⭐ Turning one good off leaves the others on.</summary>
    /// <remarks>
    /// The trap this guards: a mask of zero means <em>no opinion</em>, so the first click has to
    /// start from everything the kind holds and remove one. Starting from zero and adding the
    /// bit would mean the first click <b>emptied the building</b> of every other good instead.
    /// </remarks>
    [Fact]
    public void TurningOneGoodOffLeavesTheRestAlone()
    {
        SimWorld world = World();
        StoreBuilding warehouse = WarehouseIn(world);

        Assert.True(warehouse.Accepts(Goods.Logs));
        Assert.True(warehouse.Accepts(Goods.Firewood));

        Assert.True(world.SetStoreAccepts(warehouse, Goods.Logs, accepted: false).Allowed);

        _output.WriteLine(
            $"{warehouse.Name}: logs {warehouse.Accepts(Goods.Logs)}, firewood {warehouse.Accepts(Goods.Firewood)}, "
            + $"stone {warehouse.Accepts(Goods.Stone)}");

        Assert.False(warehouse.Accepts(Goods.Logs));
        Assert.True(warehouse.Accepts(Goods.Firewood));
        Assert.True(warehouse.Accepts(Goods.Stone));
    }

    /// <summary>⭐ It narrows only — a granary cannot be told to hold timber.</summary>
    /// <remarks>
    /// What a kind can hold is the model (D32), not a preference. The refusal lives in the sim
    /// rather than only in the view, because a control that cannot be misused and a rule that
    /// cannot be broken are different things, and only the second survives another caller.
    /// </remarks>
    [Fact]
    public void AGranaryCannotBeToldToHoldLogs()
    {
        SimWorld world = World();

        StoreBuilding granary = world.StoreBuildings[0];
        Assert.Equal(StoreKind.Granary, granary.Kind);

        PlacementVerdict verdict = world.SetStoreAccepts(granary, Goods.Logs, accepted: true);

        _output.WriteLine($"asking a granary for logs: {verdict.Reason}");

        Assert.False(verdict.Allowed);
        Assert.False(granary.Accepts(Goods.Logs));
        Assert.True(granary.Accepts(Goods.Food));
    }

    /// <summary>A store told to take nothing says so once, and then obeys (D42).</summary>
    [Fact]
    public void AStoreThatWillTakeNothingSaysSoOnce()
    {
        SimWorld world = World();
        StoreBuilding warehouse = WarehouseIn(world);

        PlacementVerdict last = default;
        for (int g = 0; g < Stockpile.Kinds; g++)
        {
            if (warehouse.CanEverHold((Goods)g))
            {
                last = world.SetStoreAccepts(warehouse, (Goods)g, accepted: false);
            }
        }

        _output.WriteLine($"a warehouse that takes nothing: \"{last.Warning}\"");

        Assert.True(last.Allowed, "The game argued with the player instead of obeying (D42).");
        Assert.False(string.IsNullOrWhiteSpace(last.Warning), "It obeyed without saying so.");
        Assert.False(warehouse.Accepts(Goods.Logs));
    }

    /// <summary>
    /// ⭐⭐ THE FILTER IS OBEYED BY THE VILLAGE, not merely answered by the predicate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Joe, playing D141 the day it landed: <i>"my logs-only storage pile allowed firewood. I
    /// set it to logs-only as soon as it was built."</i> He was right, and <b>every guard above
    /// this one passed while it was true</b> — they all ask <c>Accepts</c> and none of them ever
    /// made a villager put something down. <b>A feature tested at its predicate and never at its
    /// deposit is a feature nobody has tested</b> (D144).
    /// </para>
    /// <para>
    /// Two paths were putting goods where they were not wanted, and the first is why his pile in
    /// particular was hit. The woodcutter splits out of the nearest store <em>holding logs</em>
    /// and puts the firewood back into it, asking only whether it was <b>full</b> — so the one
    /// store guaranteed to be chosen was a pile that takes logs, which is exactly what his
    /// filter said. The second: an armful of two goods walked to a store chosen for the first
    /// and emptied both arms into it, because <c>Stockpile</c> is a dumb container and knows
    /// nothing of the filter.
    /// </para>
    /// <para>
    /// So this asserts the rule from the outside — <b>run the village and let it try</b> — which
    /// is the only shape that would have caught either.
    /// </para>
    /// </remarks>
    [Fact]
    public void NothingTheVillageDoesPutsFirewoodInAStoreThatRefusesIt()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        StoreBuilding warehouse = WarehouseIn(loop.World);

        // The moment it exists, exactly as Joe did it.
        Assert.True(loop.World.SetStoreAccepts(warehouse, Goods.Firewood, accepted: false).Allowed);
        Assert.True(warehouse.Accepts(Goods.Logs), "It must still take logs, or nothing is tested.");

        int worst = 0;
        int everMade = 0;
        for (int tick = 0; tick < config.TicksPerYear * 20; tick++)
        {
            loop.StepOnce();
            worst = System.Math.Max(worst, warehouse.Store.Firewood);
            everMade = System.Math.Max(everMade, loop.World.TotalFirewood());
        }

        _output.WriteLine(
            $"20 years: {warehouse.Name} held at most {worst} firewood while refusing it; the village "
            + $"made {everMade} in all, and holds {loop.World.OnTheGround(Goods.Firewood)} "
            + "on the ground.");

        // ⚠️ ANTI-VACUITY FIRST (D7), and it is not decoration here: the guard for D132's bug
        // was green for weeks because its village died before anybody felled a log. A warehouse
        // that never sees firewood because none was ever made proves nothing at all.
        Assert.True(everMade > 0, "Nobody ever split a log in twenty years, so this is vacuous.");
        Assert.True(warehouse.Store.Logs > 0, "Nothing ever went into the store either.");

        Assert.Equal(0, worst);
    }

    /// <summary>⛔ A load the warehouse refuses must still get somewhere, not circle for ever.</summary>
    /// <remarks>
    /// <para>
    /// <b>Joe's Year-44 village, and the most expensive bug this project has had</b> (found
    /// 2026-08-27 in his own audit trail). His warehouse refused a good, so every armful of it was
    /// walked to the warehouse, <b>refused on arrival</b>, and set down at the warehouse's own door — and
    /// <c>NearestGroundStack</c>, which DOES ask <c>Accepts</c>, then declared that heap worth
    /// fetching. Somebody picked it up, <c>StoreForTheLoad</c> sent them straight back to the
    /// same warehouse, and the village did that <b>about fifteen thousand times</b>: 1,439 failed
    /// trips to one tile in the last 900 ticks alone, by fourteen different villagers.
    /// </para>
    /// <para>
    /// ⭐⭐ <b>THE CAUSE IS TWO FINDER FUNCTIONS THAT DISAGREE ABOUT WHERE A GOOD MAY GO.</b>
    /// <c>SimWorld.NearestStore</c> matched on kind and fullness and <b>never asked
    /// <c>Accepts</c></b>; every other finder asks. One said <i>"there is somewhere for this"</i>
    /// and the other walked them somewhere that refused it.
    /// </para>
    /// <para>
    /// ⛔ <b>And it cost far more than a wasted walk.</b> Tidying outranked clearing, so the
    /// loop ate every spare hand in the village: painted ground stopped being cleared after
    /// <b>Year 3</b>, and a granary marked out in Year 23 was still an unbuilt site twenty-one
    /// years later, in silence.
    /// </para>
    /// <para>
    /// ⚠️ <b>The guard above was green through all of it.</b> It asserts the warehouse stays empty —
    /// which was perfectly true — and only <em>prints</em> what ended up on the ground. That is
    /// D144's <i>"tested at its predicate and never at its deposit"</i> arriving a second time,
    /// so this one asserts the <b>outcome</b>: the goods have to actually arrive somewhere.
    /// </para>
    /// </remarks>
    [Fact]
    public void AGoodTheShedRefusesStillReachesAStoreThatWillHaveIt()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        StoreBuilding warehouse = WarehouseIn(world);

        // ⚠️ LOGS, NOT FIREWOOD, AND THE FIRST DRAFT OF THIS GUARD FOUND OUT WHY. Firewood is
        // held by the MARKET as well (`stored_by`), so a marketer's leg — which asks `Accepts`
        // properly — quietly rescued every load and the guard passed against the live bug:
        // 622 firewood in the market, none on the ground. Logs are held by the warehouse and the
        // pile and nothing else, which is Joe's own case and leaves no third party to save it.
        var pile = PileOnClearGroundIn(world);
        Assert.True(pile.Accepts(Goods.Logs), "The pile must take logs, or nothing is tested.");

        // Exactly as Joe did it: the warehouse is told to refuse, and it is not full.
        Assert.True(world.SetStoreAccepts(warehouse, Goods.Logs, accepted: false).Allowed);
        Assert.False(warehouse.Store.IsFull, "A full warehouse would take a different branch entirely.");

        int everMade = 0;
        for (int tick = 0; tick < config.TicksPerYear * 20; tick++)
        {
            loop.StepOnce();
            everMade = System.Math.Max(everMade, world.TotalLogs());
        }

        int onTheGround = world.OnTheGround(Goods.Logs);

        _output.WriteLine(
            $"20 years: the village handled {everMade} logs in all. {warehouse.Name} refused them and "
            + $"holds {warehouse.Store.Logs}; {pile.Name} holds {pile.Store.Logs} with "
            + $"{pile.Store.FreeSpace} free; {onTheGround} are lying on the ground.");

        // ⚠️ ANTI-VACUITY FIRST (D7). A village that never felled anything proves nothing, and
        // the guard above this one records exactly that failure being green for weeks.
        Assert.True(everMade > 0, "Nobody ever felled a tree in twenty years, so this is vacuous.");
        Assert.Equal(0, warehouse.Store.Logs);

        // ⭐⭐ THE CLAIM, AND IT IS ABOUT THE DEPOSIT RATHER THAN THE PREDICATE. A store that
        // would have taken these logs stood there with room the whole time, so logs on the
        // ground are the village failing to DELIVER — not a village with nowhere to put things,
        // which is the genuinely different state D80 drew the line around.
        Assert.True(
            pile.Store.FreeSpace <= 0 || onTheGround == 0,
            $"{onTheGround} logs are lying on the ground while {pile.Name} would take them and "
                + $"has {pile.Store.FreeSpace} free. The load is walked to a store that refuses "
                + "it, set down, picked up, and walked to the same store again.");
    }

    /// <summary>A pile standing on bare ground near the founding site, usable at once.</summary>
    private static StoreBuilding PileOnClearGroundIn(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 1; radius < 12; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (world.HasSomethingToHarvest(at)
                        || !world.CanBuildAt(BuildingKind.Pile, at).Allowed)
                    {
                        continue;
                    }

                    Assert.True(world.Mark(BuildingKind.Pile, at).Allowed);
                    foreach (StoreBuilding store in world.StoreBuildings)
                    {
                        if (store.Kind == StoreKind.Pile && store.Position == at)
                        {
                            return store;
                        }
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No clear ground near the founding site for a pile.");
    }

    /// <summary>⭐ An armful of two goods leaves behind the one the store will not have.</summary>
    /// <remarks>
    /// <para>
    /// The second path D144 fixed, and it needs posing directly: <c>StoreForTheLoad</c> chooses
    /// on the <b>one</b> good it treats as the load — food, else logs, else firewood — so a
    /// villager carrying two walked to a store picked for the first and emptied <em>both</em>
    /// arms into it. <c>Stockpile</c> is a dumb container and has never heard of the filter.
    /// </para>
    /// <para>
    /// <b>It is not covered by the twenty-year run above</b>, checked rather than assumed: with
    /// this fix removed and the woodcutter's kept, that guard still passes. Whether anybody is
    /// interrupted carrying two things depends on the seed, which makes a long run the wrong
    /// instrument — so the case is posed in three lines and the refusal has to hold anyway
    /// (D7 is about the guard being able to fail, and a guard that only sometimes exercises its
    /// case can only sometimes fail).
    /// </para>
    /// <para>
    /// And what the store will not have goes <b>on the ground</b>, not nowhere — D96's rule,
    /// which is already the line under the deposit.
    /// </para>
    /// </remarks>
    [Fact]
    public void AnArmfulLeavesBehindWhatTheStoreWillNotHave()
    {
        SimWorld world = World();
        StoreBuilding warehouse = WarehouseIn(world);

        Assert.True(world.SetStoreAccepts(warehouse, Goods.Firewood, accepted: false).Allowed);

        Villager carrier = world.Villagers[0];
        carrier.Position = warehouse.Position;
        carrier.Carried.TakeAll(Goods.Food);
        carrier.Carried.TakeAll(Goods.Logs);
        carrier.Carried.TakeAll(Goods.Firewood);
        carrier.Carried.Receive(Goods.Logs, 5);
        carrier.Carried.Receive(Goods.Firewood, 7);

        int logsBefore = warehouse.Store.Logs;
        Bclone.Sim.Systems.BehaviorSystem.ArriveWithALoadForTest(world, carrier);

        _output.WriteLine(
            $"{warehouse.Name} refusing firewood took {warehouse.Store.Logs - logsBefore} of 5 logs and "
            + $"{warehouse.Store.Firewood} firewood; {world.OnTheGround(Goods.Firewood)} firewood is "
            + "on the ground.");

        Assert.Equal(logsBefore + 5, warehouse.Store.Logs);
        Assert.Equal(0, warehouse.Store.Firewood);

        // Conservation: the seven pieces still exist somewhere a villager can reach.
        Assert.Equal(0, carrier.CarriedFirewood);
        Assert.Equal(7, world.OnTheGround(Goods.Firewood));
    }

    /// <summary>
    /// ⭐ Filters are silent until somebody sets one — no golden moves for the feature landing.
    /// </summary>
    /// <remarks>
    /// The same sparse-hash contract as <c>Workplace.QueueRank</c> and <c>Workplace.Mode</c>.
    /// A village where nobody has touched a filter must hash exactly as it did before filters
    /// existed, or a control nobody used would have re-taken every golden in the suite.
    /// </remarks>
    [Fact]
    public void FiltersAreSilentUntilSomebodySetsOne()
    {
        SimWorld untouched = World();
        SimWorld filtered = World();

        Assert.Equal(StateHash.Compute(untouched), StateHash.Compute(filtered));

        Assert.True(
            filtered.SetStoreAccepts(WarehouseIn(filtered), Goods.Logs, accepted: false).Allowed);

        Assert.NotEqual(StateHash.Compute(untouched), StateHash.Compute(filtered));
    }
}
