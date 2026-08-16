using System.Collections.Generic;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The farm — <c>specs/crops-and-orchards.md</c> (D161). Sowing, reaping, the local store, the
/// hauling, and the two seams that will silently eat a harvest.
/// </summary>
/// <remarks>
/// <para>
/// <b>D161's rule: when two systems meet, the guard goes over the seam rather than over either
/// side.</b> Crops meet the harvest brush, building placement, the labour quota, the food
/// economy, the farm's own store and the market. The demand seam has a file to itself
/// (<see cref="FarmDemandTests"/>) because it had to be proved before anything else existed;
/// the rest are here.
/// </para>
/// </remarks>
public sealed class FarmTests
{
    private readonly ITestOutputHelper _output;

    public FarmTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    // ---------------------------------------------------------------
    //  The ground
    // ---------------------------------------------------------------

    /// <summary>Painting ground for a farm breaks it, so the player can see the field.</summary>
    /// <remarks>
    /// Deliberately not load-bearing: sowing takes <see cref="Terrain.Grass"/> as readily as
    /// <see cref="Terrain.Field"/>, so a tile still under trees when the brush went over it
    /// joins the field the moment the laborers clear it. The plough is what the player can
    /// see; the sowing is what is true.
    /// </remarks>
    [Fact]
    public void PaintingGroundForAFarmPloughsIt()
    {
        SimWorld world = Loop(Config).World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        int given = FarmFixtures.GiveItGround(world, farm, reach: 2);

        int ploughed = 0;
        IReadOnlyList<int> owned = world.Zones.WorkGroundOf(farm.Id);
        for (int i = 0; i < owned.Count; i++)
        {
            if (world.Map.TerrainAt(world.Zones.PositionOf(owned[i])) == Terrain.Field)
            {
                ploughed++;
            }
        }

        _output.WriteLine($"{given} tiles painted, {ploughed} of them ploughed");
        Assert.Equal(given, ploughed);
    }

    /// <summary>A farm keeps less ground than a forester, because it works each tile twice.</summary>
    /// <remarks>
    /// <b>The overstretched warning has to be telling the truth for a farm too</b> (D86, D148):
    /// a farmer sows in spring and reaps in autumn, both confined to one season, where a
    /// forester visits each tile once at any time of year. One number for both would have the
    /// panel calling a farm comfortable while most of its field lay fallow every year.
    /// </remarks>
    [Fact]
    public void AFarmsAllowanceIsTheGroundOneFarmerCanActuallyWork()
    {
        SimWorld world = Loop(Config).World;

        int forFarm = world.TilesOneWorkerKeeps(JobKind.Farmer);
        int forForester = world.TilesOneWorkerKeeps(JobKind.Forester);

        _output.WriteLine($"a farmer keeps {forFarm} tiles, a forester {forForester}");

        Assert.Equal(VillageEconomy.FieldTilesOneFarmerKeeps(Config), forFarm);
        Assert.Equal(Config.WorkGroundTilesPerWorker, forForester);
        Assert.True(
            forFarm <= forForester,
            "A farmer works each tile twice in two fixed seasons — they cannot keep more "
            + "ground than a forester who visits each tile once at any time of year.");
    }

    // ---------------------------------------------------------------
    //  ⭐ The work
    // ---------------------------------------------------------------

    /// <summary>⭐⭐ A farmer sows in spring, and by autumn the field is standing ripe.</summary>
    /// <remarks>
    /// <b>The whole slice in one assertion.</b> Every crop step before this was provably
    /// invisible — nothing could sow, so <c>CropSystem</c> ran on a valley that never had a
    /// field in it. This is the tick where the year starts happening to the ground.
    /// </remarks>
    [Fact]
    public void AFarmerSowsInSpringAndTheFieldStandsRipeByAutumn()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 2);

        FarmFixtures.StepToTheStartOf(loop, Season.Spring);
        loop.Step(Config.TicksPerSeason);

        int sown = TilesOf(world, farm, Terrain.Sown);
        _output.WriteLine($"{world.Clock.SeasonAndYear()}: {sown} tiles sown");
        Assert.True(sown > 0, "A whole spring passed and not one tile was put under seed.");

        FarmFixtures.StepToTheStartOf(loop, Season.Fall);

        int ripe = TilesOf(world, farm, Terrain.Ripe);
        _output.WriteLine($"{world.Clock.SeasonAndYear()}: {ripe} tiles standing ripe");
        Assert.True(ripe > 0, "Nothing ripened, so nothing was ever really sown.");
    }

    /// <summary>⭐⭐ And the harvest comes in rather than rotting where it stood.</summary>
    /// <remarks>
    /// <b>This is the guard D146 would have failed.</b> A quota that does not want farmers in
    /// autumn leaves the crop standing, winter takes it, and the blame falls on
    /// <c>CropSystem</c> — which is why the demand arm was built first and why this checks the
    /// outcome rather than the intention.
    /// </remarks>
    [Fact]
    public void TheHarvestIsReapedAndTheFoodExists()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 2);
        int tiles = FarmFixtures.SowEveryTileOf(world, farm);

        FarmFixtures.StepToTheStartOf(loop, Season.Fall);
        Assert.True(TilesOf(world, farm, Terrain.Ripe) > 0, "Nothing ripened to reap.");

        loop.Step(Config.TicksPerSeason);

        int reaped = tiles - TilesOf(world, farm, Terrain.Ripe);
        _output.WriteLine(
            $"{world.Clock.SeasonAndYear()}: {reaped} of {tiles} tiles reaped; "
            + $"{farm.Store.Food} food in {farm.Name}, {world.FoodTheVillageHolds()} in the village");

        Assert.True(reaped > 0, "A whole autumn passed and not one tile was reaped.");
        Assert.True(
            farm.Store.LifetimeGathered > 0 || world.TotalFood() > 0,
            "The crop left the ground and the food is nowhere.");
    }

    // ---------------------------------------------------------------
    //  ⭐ The farm's own store — the first Workplace.Store anything writes to
    // ---------------------------------------------------------------

    /// <summary>The harvest goes into the farm's own buffer first, because it is underfoot.</summary>
    [Fact]
    public void TheHarvestFillsTheFarmsOwnStoreFirst()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 2);
        FarmFixtures.SowEveryTileOf(world, farm);

        FarmFixtures.StepToTheStartOf(loop, Season.Fall);
        loop.Step(Config.TicksPerSeason);

        _output.WriteLine(
            $"{farm.Name} holds {farm.Store.Food} of {Config.FarmStoreCap}; "
            + $"the village holds {world.FoodTheVillageHolds()}");

        Assert.True(
            farm.Store.Food > 0,
            "Nothing ever reached Workplace.Store — professions.md §4's fifth element is "
            + "still dead, and the buffer farm_store_cap describes does not exist.");
    }

    /// <summary>⛔⛔ A full farm store refuses the overflow — it does not swallow it.</summary>
    /// <remarks>
    /// <para>
    /// <b>D96 exactly, and D144 one deposit path over.</b> <c>Stockpile.Add</c> returns what it
    /// actually took and its own remarks say the return value must not be ignored; ignoring it
    /// put 17,451 food into a full granary and out of the world over fifty years, and destroyed
    /// every batch of firewood made after the woodyard filled. <b>This store had never been
    /// written to by anything</b>, so this path has never been exercised — which is precisely
    /// why `crops-and-orchards.md §6` gives it a seam of its own.
    /// </para>
    /// <para>
    /// Posed directly rather than waited for: the cap is 100 and a season's harvest may or may
    /// not reach it depending on the seed, and a guard that only fires on a lucky year is a
    /// guard that goes vacuous the first time a number moves (D143's fixed-year lesson).
    /// </para>
    /// </remarks>
    [Fact]
    public void AFullFarmStoreRefusesAHarvestRatherThanEatingIt()
    {
        SimWorld world = Loop(Config).World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);

        Assert.Equal(Config.FarmStoreCap, farm.Store.Capacity);

        int took = farm.Store.Add(Goods.Food, Config.FarmStoreCap + 50);
        int refused = Config.FarmStoreCap + 50 - took;

        _output.WriteLine(
            $"offered {Config.FarmStoreCap + 50}, took {took}, refused {refused}, "
            + $"holding {farm.Store.Food} of {farm.Store.Capacity}");

        Assert.Equal(Config.FarmStoreCap, took);
        Assert.Equal(50, refused);
        Assert.True(farm.Store.IsFull);
    }

    /// <summary>
    /// ⭐ And a farmer whose buffer is full walks the harvest to a store instead of losing it.
    /// </summary>
    /// <remarks>
    /// <b>The behaviour half of the guard above, and the two are not the same claim.</b> D144's
    /// finding is that a rule answered by a predicate and never by a villager is a rule nobody
    /// has tested — five guards asked <c>Accepts</c> and not one made anybody put anything
    /// down. So this fills the farm to its cap first and then watches where a season's harvest
    /// actually ends up.
    /// </remarks>
    [Fact]
    public void AFarmerWhoseBufferIsFullCarriesTheHarvestFurther()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 2);
        FarmFixtures.SowEveryTileOf(world, farm);

        FarmFixtures.StepToTheStartOf(loop, Season.Fall);

        // Full before a single tile is reaped, so every armful has to go somewhere else.
        farm.Store.Add(Goods.Food, Config.FarmStoreCap);
        int held = world.FoodTheVillageHolds();

        loop.Step(Config.TicksPerSeason);

        int now = world.FoodTheVillageHolds();
        _output.WriteLine(
            $"village food {held} → {now}; {farm.Name} holds {farm.Store.Food} of "
            + $"{farm.Store.Capacity}");

        Assert.Equal(Config.FarmStoreCap, farm.Store.Food);
        Assert.True(
            now > held,
            "A season's harvest went into a full buffer and out of the world — D96 and D144, "
            + "on the one deposit path that had never been exercised.");
    }

    /// <summary>
    /// ⛔⛔ Pulling down a full farm puts its harvest on the ground, not out of the world.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Found by reading <c>RetireWorkplace</c> rather than by a failing test, and it is D96
    /// and D144's shape arriving through the new door.</b> That method has never done anything
    /// about <see cref="Workplace.Store"/> and was right not to for five phases, because
    /// nothing in the sim had ever written to one. The farm's buffer is the moment that stopped
    /// being true — so a player who demolished a full farmhouse would have destroyed up to
    /// <c>farm_store_cap</c> of food silently, which is exactly the failure this project has
    /// now shipped twice and found by playing both times.
    /// </para>
    /// <para>
    /// <b>On the ground rather than into a store</b>, which is D96's own rule: goods nothing
    /// will take go down where they are and somebody carries them in. A village that has just
    /// pulled down a full farm is precisely a village that may have nowhere to put a hundred
    /// food.
    /// </para>
    /// </remarks>
    [Fact]
    public void DemolishingAFullFarmSpillsItsHarvestRatherThanDestroyingIt()
    {
        SimWorld world = Loop(Config).World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        farm.Store.Add(Goods.Food, Config.FarmStoreCap);

        GridPos stood = farm.Position;
        int before = world.TotalFood() + world.GroundStackAt(stood, Goods.Food);

        world.Demolish(farm);

        int spilled = world.GroundStackAt(stood, Goods.Food);
        int after = world.TotalFood() + spilled;

        _output.WriteLine(
            $"food anywhere: {before} → {after}; {spilled} left on the ground at {stood}");

        Assert.Equal(before, after);
        Assert.Equal(Config.FarmStoreCap, spilled);
    }

    // ---------------------------------------------------------------
    //  ⛔ The seam: crops × the harvest brush
    // ---------------------------------------------------------------

    /// <summary>⛔⛔ A laborer clearing painted ground must never reap the farm.</summary>
    /// <remarks>
    /// <para>
    /// <b>The seam `crops-and-orchards.md §6` names as the one that will silently eat a
    /// harvest.</b> `TerrainRules.Yields` answers *"what may the harvest brush take?"*, and it
    /// is read by <c>HasSomethingToHarvest</c>, <c>GroundIsClearAt</c>, <c>NearestHarvest</c>
    /// and D157's footprint-clearing pass. Saying yes to <see cref="Terrain.Ripe"/> there would
    /// make a ripe field look exactly like a wood to every one of them.
    /// </para>
    /// <para>
    /// <b>Killed at the door rather than guarded after the fact</b>, which is why this test
    /// reads like it is testing nothing: the paint never lands, so there is no errand to
    /// intercept. Two verbs that look alike and must not share a door (D145's rule, stated
    /// before the bug rather than after it).
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(Terrain.Ripe)]
    [InlineData(Terrain.Sown)]
    [InlineData(Terrain.Field)]
    public void TheHarvestBrushCannotTakeAField(Terrain field)
    {
        SimWorld world = Loop(Config).World;
        var tile = new GridPos(world.Map.FoundingSite.X + 4, world.Map.FoundingSite.Y + 4);

        world.SetTerrain(tile, field);
        world.Map.SetCrop(tile, 1);

        bool painted = world.PaintHarvest(tile).Allowed && world.Zones.IsHarvest(tile);

        _output.WriteLine(
            $"{field}: yields={TerrainRules.Yields(field)?.ToString() ?? "nothing"} "
            + $"painted={painted} harvestable={world.HasSomethingToHarvest(tile)}");

        Assert.Null(TerrainRules.Yields(field));
        Assert.False(world.HasSomethingToHarvest(tile));
        Assert.False(painted);
    }

    /// <summary>
    /// The anti-vacuity companion (D7): the same brush still takes a wood.
    /// </summary>
    /// <remarks>
    /// Without this, a brush that had simply stopped working at all would pass the guard above
    /// while breaking step C's central mechanic — which is the shape of the vacuous guard this
    /// project has now caught four times.
    /// </remarks>
    [Fact]
    public void AndTheSameBrushStillTakesAWood()
    {
        SimWorld world = Loop(Config).World;
        var tile = new GridPos(world.Map.FoundingSite.X + 4, world.Map.FoundingSite.Y + 4);

        world.SetTerrain(tile, Terrain.Forest);

        Assert.Equal(Goods.Logs, TerrainRules.Yields(Terrain.Forest));
        Assert.True(world.PaintHarvest(tile).Allowed);
        Assert.True(world.Zones.IsHarvest(tile));
    }

    // ---------------------------------------------------------------
    //  ⭐ The seam: crops × the market
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ A trader sources from a farm only when the farm is nearer than the granary.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Joe's ruling: *"focus on granary first and only grab from a farm if it happens to be
    /// near by."* Stated as a comparison rather than a radius, so there is no number to tune
    /// and none anybody would have to derive (D16).
    /// </para>
    /// <para>
    /// <b>Both arms, because either alone passes against a rule that is wrong the other way.</b>
    /// A market that always preferred the granary would pass a farm-never-used test; one that
    /// always preferred the farm would pass a farm-sometimes-used test.
    /// </para>
    /// </remarks>
    [Fact]
    public void AFarmIsSourcedOnlyWhenItIsNearerThanTheGranary()
    {
        SimWorld world = Loop(Config).World;
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);

        // A farm right beside the granary is never STRICTLY nearer from the granary's own
        // doorstep, and one out in the fields is nearer to somebody standing in the fields.
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        farm.Store.Add(Goods.Food, 50);
        granary.Store.Add(Goods.Food, 50);

        Workplace? fromTheGranary = NearerFarm(world, granary.Position, granary);
        Workplace? fromTheFarm = NearerFarm(world, farm.Position, granary);

        _output.WriteLine(
            $"granary at {granary.Position}, {farm.Name} at {farm.Position}; "
            + $"standing at the granary → {fromTheGranary?.Name ?? "the granary"}; "
            + $"standing at the farm → {fromTheFarm?.Name ?? "the granary"}");

        Assert.Null(fromTheGranary);
        Assert.Same(farm, fromTheFarm);
    }

    /// <summary>And an empty farm is never a source, however near it is.</summary>
    [Fact]
    public void AnEmptyFarmIsNeverASource()
    {
        SimWorld world = Loop(Config).World;
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        granary.Store.Add(Goods.Food, 50);

        Assert.Null(NearerFarm(world, farm.Position, granary));
    }

    // ---------------------------------------------------------------

    private static int TilesOf(SimWorld world, Workplace farm, Terrain terrain)
    {
        IReadOnlyList<int> owned = world.Zones.WorkGroundOf(farm.Id);
        int count = 0;

        for (int i = 0; i < owned.Count; i++)
        {
            if (world.Map.TerrainAt(world.Zones.PositionOf(owned[i])) == terrain)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// The market's own rule, asked from outside: a workplace store strictly nearer than the
    /// nearest store building holding the good.
    /// </summary>
    /// <remarks>
    /// <b>Restated here rather than reached into, deliberately.</b> The rule lives in one
    /// private method in <c>BehaviorSystem</c> and a test that called it would be asserting
    /// against itself; this asserts the arithmetic the rule is made of, and
    /// <see cref="MarketTests"/> plus the acceptance runs cover the behaviour end to end.
    /// </remarks>
    private static Workplace? NearerFarm(SimWorld world, GridPos from, StoreBuilding nearest)
    {
        int beat = world.TravelCost.TicksBetween(from, nearest.Position);
        Workplace? best = null;
        int bestCost = int.MaxValue;

        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace workplace = world.Workplaces[i];
            if (workplace.IsSite || workplace.Store.Food <= 0)
            {
                continue;
            }

            int cost = world.TravelCost.TicksBetween(from, workplace.Position);
            if (cost < beat && cost < bestCost)
            {
                best = workplace;
                bestCost = cost;
            }
        }

        return best;
    }
}
