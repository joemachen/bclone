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
    /// <summary>
    /// ⛔⛔ A quarter of paint is a quarter of a field — <b>the plough runs when the farm HOLDS the
    /// tile, and a farm holds every tile it has any paint on</b> (D350, D352).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two of Joe's screenshots, a day apart.</b> First, furrows a full tile past his paint:
    /// *"the farm field is built outside of its painted area"* — the plough fired on a quarter the
    /// farm did not hold (D350 gated it on the hold). Then a bare notch at the corner of a field:
    /// *"why is part of this painted farm not farmland?"* — a tile a quarter painted was spoken for
    /// and not held, so it was never ploughed. **D352 moved the hold to the first quarter**, so the
    /// gate and the paint are one question; what a quarter yields is the guard below.
    /// </para>
    /// </remarks>
    [Fact]
    public void AQuarterOfPaintPloughsTheTile()
    {
        SimWorld world = Loop(Config).World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        GridPos tile = BareTileBeside(world, farm);

        Assert.Equal(Terrain.Grass, world.Map.TerrainAt(tile));
        Assert.True(world.PaintWorkGround(farm, SubTile.Of(tile, 2, 1)).Allowed);

        _output.WriteLine($"one quarter painted: {world.Map.TerrainAt(tile)}, "
            + $"held {world.Zones.Holds(farm.Id, tile)}, tiles {world.Zones.WorkGroundTiles(farm.Id)}");
        Assert.True(world.Zones.Holds(farm.Id, tile));
        Assert.Equal(Terrain.Field, world.Map.TerrainAt(tile));
        Assert.Equal(1, world.Zones.WorkGroundTiles(farm.Id));
    }

    /// <summary>
    /// ⭐⭐ A tile a quarter painted yields a quarter of a tile's crop — <b>the harvest is in
    /// proportion to the paint</b> (D352).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The honest half of *"a farm works every tile it has any paint on."* Without it a
    /// quarter-tile sliver of paint would bring in a whole tile's wheat, and a player who painted a
    /// round field would be fed as if they had painted its bounding square. Measured at the reap —
    /// what a farmer is carrying the tick they finish — against the same field painted whole, in
    /// two worlds from one seed.
    /// </para>
    /// <para>
    /// ⚠️ <b>A whole tile is sixteen sixteenths</b>, so the multiplier is the identity for every
    /// village that paints whole tiles — which is every golden. The whole-tile arm here is that
    /// claim as a number.
    /// </para>
    /// </remarks>
    [Fact]
    public void AQuarterOfATileYieldsAQuarterOfItsCrop()
    {
        (int reaps, long carried) whole = HarvestOf(paintWhole: true);
        (int reaps, long carried) quarter = HarvestOf(paintWhole: false);

        Assert.True(whole.reaps > 0 && quarter.reaps > 0, "Nothing was reaped in one of the two worlds.");

        double perTileWhole = whole.carried / (double)whole.reaps;
        double perTileQuarter = quarter.carried / (double)quarter.reaps;
        _output.WriteLine(
            $"whole tiles: {whole.reaps} reaps, {perTileWhole:F1} a tile · "
            + $"one quarter each: {quarter.reaps} reaps, {perTileQuarter:F1} a tile");

        // A quarter, to within the floor of an integer division and a farmer's vigour.
        Assert.InRange(perTileQuarter, (perTileWhole / 4) - 1.5, (perTileWhole / 4) + 1.5);

        // And the whole-tile arm is the crop the config promises, scaled by soil and vigour only
        // — not a sixteenth less.
        Assert.True(perTileWhole > Config.CropYieldPerTile * 0.5,
            $"A whole tile brought in {perTileWhole:F1} against a yield of {Config.CropYieldPerTile}.");

        (int, long) HarvestOf(bool paintWhole)
        {
            SimLoop loop = Loop(Config);
            SimWorld world = loop.World;
            Workplace farm = FarmFixtures.RaiseAFarm(world);
            world.SetStaffing(farm, 1);

            var tiles = new List<GridPos>();
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var at = new GridPos(farm.Tile.X + dx, farm.Tile.Y + dy);
                    if (world.Map.Contains(at) && world.Map.TerrainAt(at) == Terrain.Grass
                        && !world.SomethingStandsAt(at))
                    {
                        tiles.Add(at);
                    }
                }
            }

            foreach (GridPos at in tiles)
            {
                if (paintWhole)
                {
                    Assert.True(world.PaintWorkGround(farm, at).Allowed);
                    continue;
                }

                // ⚠️ A QUARTER OF THE TILE IS FOUR OF ITS SIXTEEN SUB-TILES — a 2×2 block — not
                // one. The code calls a sub-tile a "quarter-tile" because it is a quarter of a
                // side; the first draft of this guard painted one and measured a sixteenth.
                foreach ((int qx, int qy) in new[] { (1, 1), (2, 1), (1, 2), (2, 2) })
                {
                    Assert.True(world.PaintWorkGround(farm, SubTile.Of(at, qx, qy)).Allowed);
                }
            }

            Assert.True(FarmFixtures.SowEveryTileOf(world, farm) > 0);
            FarmFixtures.StepToTheStartOf(loop, Season.Fall);

            // ⚠️ Measured as WHEAT IN THE WORLD, not wheat in the farmer's arms (D361). A tile
            // reaped beside the farmhouse is hauled in the same tick the reap finishes — under
            // clock B a diagonal neighbour is one step, so the arms are empty again before the
            // harness looks — and the first draft, watching the arms, counted nothing reaped.
            int reaps = 0;
            long carried = 0;
            for (int i = 0; i < Config.TicksPerSeason; i++)
            {
                bool finishing = world.Villagers.Any(
                    v => v.State == VillagerState.Reaping && v.ActionTicksRemaining == 1);
                long had = WheatInTheWorld(world);

                loop.StepOnce();

                long now = WheatInTheWorld(world);
                if (finishing && now > had)
                {
                    reaps++;
                    carried += now - had;
                }
            }

            return (reaps, carried);
        }

        static long WheatInTheWorld(SimWorld world) =>
            world.StoreBuildings.Sum(s => (long)s.Store[Goods.Wheat])
            + world.Workplaces.Where(w => !w.IsSite).Sum(w => (long)w.Store[Goods.Wheat])
            + world.Households.Sum(h => (long)h.Stockpile[Goods.Wheat])
            + world.Villagers.Sum(v => (long)v.Carried[Goods.Wheat])
            + world.OnTheGround(Goods.Wheat);
    }

    /// <summary>
    /// ⛔⛔ Taking the ground back UN-PLOUGHS it — <b>a field the farm no longer holds is grass
    /// again, crop and all</b> (D350).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe: *"'removing' farm land leaves a field (right click while painting) that can't be
    /// removed."*</b> Nothing had ever turned a <see cref="Terrain.Field"/> back, so the brush's
    /// take-back left furrows standing on ground nobody owned, for ever. D84's rule for a dug seam
    /// — *no scar* — applied to a field: the ground the village stops working is ground again.
    /// </para>
    /// <para>
    /// <b>A standing crop goes with it.</b> A sown or ripe tile the farm no longer holds would
    /// never be reaped and would rot to a bare field at winter — a field that then stands for ever.
    /// It is taken at the stroke, and the crop id cleared with it so *"a field remembers what it
    /// grows"* (<c>CropSystem.Rot</c>) is not remembering a field that is not there.
    /// </para>
    /// <para>
    /// ⚠️ The hold is the last quarter now (D352): rubbing out fifteen of sixteen leaves a field a
    /// sixteenth wide, and the sixteenth is what un-ploughs it.
    /// </para>
    /// </remarks>
    [Fact]
    public void TakingTheGroundBackUnploughsIt()
    {
        SimWorld world = Loop(Config).World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        int given = FarmFixtures.GiveItGround(world, farm, reach: 2);
        Assert.True(given >= 3, $"Need at least three tiles of field to pose this; got {given}.");

        IReadOnlyList<int> owned = world.Zones.WorkGroundOf(farm.Id);
        GridPos bare = world.Zones.PositionOf(owned[0]);
        GridPos standing = world.Zones.PositionOf(owned[1]);
        GridPos byQuarters = world.Zones.PositionOf(owned[2]);

        // A bare field, taken back whole.
        Assert.Equal(Terrain.Field, world.Map.TerrainAt(bare));
        Assert.True(world.EraseWorkGround(farm, bare));
        Assert.Equal(Terrain.Grass, world.Map.TerrainAt(bare));

        // A ripe crop, taken back whole: gone, and the tile has forgotten what it grew.
        world.SetTerrain(standing, Terrain.Ripe);
        world.Map.SetCrop(standing, 1);
        Assert.True(world.EraseWorkGround(farm, standing));
        Assert.Equal(Terrain.Grass, world.Map.TerrainAt(standing));
        Assert.Equal(0, world.Map.CropAt(standing));

        // Quarter by quarter: still a field with one quarter left, grass at none.
        for (int i = 0; i < SubTile.PerWholeTile - 1; i++)
        {
            Assert.True(world.EraseWorkGround(farm, SubTile.Of(byQuarters, i % 4, i / 4)));
        }

        Assert.True(world.Zones.Holds(farm.Id, byQuarters));
        Assert.Equal(Terrain.Field, world.Map.TerrainAt(byQuarters));

        Assert.True(world.EraseWorkGround(farm, SubTile.Of(byQuarters, 3, 3)));
        Assert.False(world.Zones.Holds(farm.Id, byQuarters));
        Assert.Equal(Terrain.Grass, world.Map.TerrainAt(byQuarters));

        _output.WriteLine($"bare {world.Map.TerrainAt(bare)}, ripe {world.Map.TerrainAt(standing)}, "
            + $"by quarters {world.Map.TerrainAt(byQuarters)}");
    }

    /// <summary>⛔ A demolished farm leaves grass, not a field nobody can remove (D350).</summary>
    /// <remarks>
    /// The same door as the brush — <c>ReleaseWorkGround</c> frees the paint, and the tiles it
    /// freed go back to ground. Without this a pulled-down farmhouse left its whole field ploughed
    /// with no building to take the paint back from.
    /// </remarks>
    [Fact]
    public void ADemolishedFarmLeavesGrassBehind()
    {
        SimWorld world = Loop(Config).World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        int given = FarmFixtures.GiveItGround(world, farm, reach: 2);
        Assert.True(given > 0);

        var field = new List<GridPos>();
        IReadOnlyList<int> owned = world.Zones.WorkGroundOf(farm.Id);
        for (int i = 0; i < owned.Count; i++)
        {
            field.Add(world.Zones.PositionOf(owned[i]));
        }

        // One of them mid-crop, so the demolition is asked about a standing field too.
        world.SetTerrain(field[0], Terrain.Sown);
        world.Map.SetCrop(field[0], 1);

        world.Demolish(farm);

        int stillField = 0;
        foreach (GridPos tile in field)
        {
            if (world.Map.TerrainAt(tile) != Terrain.Grass || world.Map.CropAt(tile) != 0)
            {
                stillField++;
            }
        }

        _output.WriteLine($"{field.Count} tiles of field before demolition, {stillField} still field after");
        Assert.Equal(0, stillField);
    }

    /// <summary>A bare, unpainted tile next to the farmhouse, for posing one tile's paint.</summary>
    private static GridPos BareTileBeside(SimWorld world, Workplace farm)
    {
        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                var at = new GridPos(farm.Tile.X + dx, farm.Tile.Y + dy);
                if (world.Map.Contains(at)
                    && world.Map.TerrainAt(at) == Terrain.Grass
                    && world.Zones.WorkGroundOwner(at) == 0
                    && !world.SomethingStandsAt(at))
                {
                    return at;
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No bare tile beside the farmhouse.");
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

    /// <summary>
    /// ⭐ A farmer sows the WHOLE tiles before the quarter-painted margins (D360).
    /// </summary>
    /// <remarks>
    /// D352 made any painted quarter of a tile workable, in proportion — and the sowing cap counts
    /// tiles. So when a square stroke's edge fell a quarter into a row, the one farmer's six tiles
    /// were the six slivers nearest the farmhouse, each worth a quarter, and the year's harvest
    /// was a tile and a half. Joe: *"the farm field is still doing some weird
    /// sowing-on-the-edge-of-the-boundary thing."* Here the slivers are NEARER the farmhouse than
    /// the whole tiles, and the farmer still walks past them.
    /// </remarks>
    [Fact]
    public void AFarmerSowsTheWholeTilesBeforeTheSlivers()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);

        // Three slivers in the row just above the farmhouse — one quarter-row each — and six
        // whole tiles in the two rows beyond them.
        var slivers = new List<GridPos>();
        var whole = new List<GridPos>();
        for (int dx = -1; dx <= 1; dx++)
        {
            var sliver = new GridPos(farm.Tile.X + dx, farm.Tile.Y - 1);
            for (int qx = 0; qx < SubTile.PerTile; qx++)
            {
                Assert.True(world.PaintWorkGround(farm, SubTile.Of(sliver, qx, SubTile.PerTile - 1)).Allowed);
            }

            slivers.Add(sliver);

            for (int dy = -3; dy <= -2; dy++)
            {
                var tile = new GridPos(farm.Tile.X + dx, farm.Tile.Y + dy);
                Assert.True(world.PaintWorkGround(farm, tile).Allowed);
                whole.Add(tile);
            }
        }

        Assert.All(slivers, at => Assert.Equal(SubTile.PerTile, world.Zones.WorkGroundSubTilesOn(at)));
        Assert.All(whole, at => Assert.Equal(SubTile.PerWholeTile, world.Zones.WorkGroundSubTilesOn(at)));

        FarmFixtures.StepToTheStartOf(loop, Season.Spring);

        // A farm ploughs open ground and leaves a tree standing, so a wooded tile is never sowable;
        // only the ploughed whole tiles count.
        whole.RemoveAll(at => world.Map.TerrainAt(at) != Terrain.Field);
        Assert.True(whole.Count >= 3, $"only {whole.Count} whole tiles were ploughed — the fixture is in a wood");

        GridPos? first = world.NextFieldToWork(farm, farm.Tile);
        Assert.NotNull(first);
        _output.WriteLine($"first tile to sow: {first}, painted {world.Zones.WorkGroundSubTilesOn(first.Value)} of 16");
        Assert.Contains(first.Value, whole);

        loop.Step(Config.TicksPerSeason);

        int wholeSown = whole.Count(at => world.Map.TerrainAt(at) == Terrain.Sown);
        int sliversSown = slivers.Count(at => world.Map.TerrainAt(at) == Terrain.Sown);
        _output.WriteLine($"after spring: {wholeSown} of {whole.Count} whole tiles sown, {sliversSown} of {slivers.Count} slivers");
        Assert.True(wholeSown > 0, "a whole spring and no whole tile was sown");
        Assert.True(
            sliversSown == 0 || wholeSown == whole.Count,
            "a sliver was sown while a whole tile still waited — the farmer is working the margin before the field");
    }

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
            + $"{farm.Store[Goods.Wheat]} food in {farm.Name}, {world.FoodTheVillageHolds()} in the village");

        Assert.True(reaped > 0, "A whole autumn passed and not one tile was reaped.");
        Assert.True(
            farm.Store.Produced(Goods.Wheat) > 0 || world.TotalFood() > 0,
            "The crop left the ground and the food is nowhere.");
    }

    // ---------------------------------------------------------------
    //  ⭐ The farm's own store — the first Workplace.Store anything writes to
    // ---------------------------------------------------------------

    /// <summary>The harvest goes into the farm's own buffer first, because it is underfoot.</summary>
    /// <summary>
    /// ⭐⭐ A farm reaps its crop's good and nothing else — <b>wheat rises, produce does
    /// not</b> (D348, Joe: *"option 2"*, a real good rather than a rename).
    /// </summary>
    /// <remarks>
    /// The reap wrote <c>Goods.Produce</c> by name for as long as farms existed. It asks the crop
    /// row now, and this is the guard that the row is what decides: a village with no foragers
    /// working, farmed for one autumn, gains wheat and gains no produce at all.
    /// </remarks>
    [Fact]
    public void AFarmReapsItsCropsGoodAndNothingElse()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 2);
        FarmFixtures.SowEveryTileOf(world, farm);

        FarmFixtures.StepToTheStartOf(loop, Season.Fall);

        loop.Step(Config.TicksPerSeason);

        // ⚠️ THE FARM'S OWN STORE, NOT THE VILLAGE'S. The first draft compared village
        // produce before and after and called any rise "the reap writing produce" — and the
        // fixture village has foragers, who raised it by two in an autumn. *A measurement that
        // cannot tell the farm from the foragers measures nothing.* What the farm's buffer has
        // ever taken in is the reap's alone.
        int wheat = farm.Store.Produced(Goods.Wheat);
        int produce = farm.Store.Produced(Goods.Produce);

        _output.WriteLine(
            $"one autumn: the farm's store took in {wheat} wheat and {produce} produce; the crop "
            + $"row yields {world.Crops.TheOne.Yields}");

        Assert.Equal(Goods.Wheat, world.Crops.TheOne.Yields);
        Assert.True(wheat > 0, "The fields were reaped and no wheat reached the farm.");
        Assert.Equal(0, produce);
    }

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
            $"{farm.Name} holds {farm.Store[Goods.Wheat]} of {Config.FarmStoreCap}; "
            + $"the village holds {world.FoodTheVillageHolds()}");

        Assert.True(
            farm.Store[Goods.Wheat] > 0,
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

        int took = farm.Store.Add(Goods.Wheat, Config.FarmStoreCap + 50);
        int refused = Config.FarmStoreCap + 50 - took;

        _output.WriteLine(
            $"offered {Config.FarmStoreCap + 50}, took {took}, refused {refused}, "
            + $"holding {farm.Store[Goods.Wheat]} of {farm.Store.Capacity}");

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
        SimLoop loop = FarmFixtures.WithNothingInTheStores(Loop(Config));
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 2);
        FarmFixtures.SowEveryTileOf(world, farm);

        FarmFixtures.StepToTheStartOf(loop, Season.Fall);

        // Full before a single tile is reaped, so every armful has to go somewhere else.
        farm.Store.Add(Goods.Wheat, Config.FarmStoreCap);
        int held = world.FoodTheVillageHolds();

        loop.Step(Config.TicksPerSeason);

        int now = world.FoodTheVillageHolds();
        _output.WriteLine(
            $"village food {held} → {now}; {farm.Name} holds {farm.Store[Goods.Wheat]} of "
            + $"{farm.Store.Capacity}");

        // ⚠️ The buffer is no longer asserted full at the end: since D362 the spare hands carry an
        // armful of food out of any buffer a store has room for, so a full farm store is emptied
        // through the season by the village and not only by the farmer walking past it. What this
        // guard is for — the harvest not leaving the world — is the second line.
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
        farm.Store.Add(Goods.Wheat, Config.FarmStoreCap);

        GridPos stood = farm.Tile;
        int before = world.TotalFood() + world.GroundStackAt(stood, Goods.Wheat);

        world.Demolish(farm);

        int spilled = world.GroundStackAt(stood, Goods.Wheat);
        int after = world.TotalFood() + spilled;

        _output.WriteLine(
            $"food anywhere: {before} → {after}; {spilled} left on the ground at {stood}");

        Assert.Equal(before, after);
        Assert.Equal(Config.FarmStoreCap, spilled);
    }

    /// <summary>
    /// ⛔⭐ The derived field is one a farmer can actually bring in — <b>measured, not assumed</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the guard the first derivation did not have, and the reason it was wrong for
    /// a slice.</b> <c>FieldTilesOneFarmerKeeps</c> promised 13 tiles and a farmer reaped 5.3 —
    /// and nothing in the suite noticed, because every other guard asked whether the arithmetic
    /// was self-consistent rather than whether it described the game. **A budget that
    /// over-states capacity is the dangerous direction**: the village believes a farm feeds a
    /// household when it feeds half of one, and the symptom is a harvest that rots.
    /// </para>
    /// <para>
    /// <b>The field is painted at exactly the derived size</b>, so this measures the claim the
    /// derivation actually makes rather than an over-painted farm — which is what the earlier
    /// 5.75 figure was, and why it took a controlled run to tell the two apart.
    /// </para>
    /// <para>
    /// <b>Asserted as a floor, not an equality.</b> The derivation is deliberately conservative
    /// — it charges every reaped tile a walk to a store, while the farm's own buffer really does
    /// absorb the first armful — so reality should beat it. What must never happen again is
    /// reality falling <em>short</em>.
    /// </para>
    /// </remarks>
    [Fact]
    public void AFarmerCanActuallyReapTheFieldTheDerivationGivesThem()
    {
        SimConfig config = Config;
        int promised = VillageEconomy.FieldTilesOneFarmerKeeps(config);

        SimLoop loop = Loop(config);
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);

        // ⚠️ ONE FARMER, STATED RATHER THAN LEFT TO THE QUOTA. The derivation's claim is
        // per-farmer, and a farm staffed by two would have this measuring a pair sharing one
        // field against a promise made about one pair of hands — apples against oranges, and
        // the first draft of this guard passed that way while the derivation was still short.
        world.SetStaffing(farm, 1);

        int painted = FarmFixtures.GiveItGround(world, farm, reach: 2);
        Assert.True(painted >= promised, $"Could not paint {promised} tiles; only got {painted}.");

        const int Years = 4;
        int reaped = 0;
        for (int i = 0; i < config.TicksPerYear * Years; i++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                if (villager.Alive
                    && villager.WorkplaceId == farm.Id
                    && villager.State == VillagerState.Reaping
                    && villager.ActionTicksRemaining == 1)
                {
                    reaped++;
                }
            }
        }

        int perYear = reaped / Years;
        _output.WriteLine(
            $"the derivation promises {promised} tiles a farmer; over {Years} years the farm's "
            + $"{farm.Places} seat(s) reaped {reaped} — {perYear} a year");

        Assert.True(
            perYear >= promised,
            $"The derivation promises {promised} tiles a year and the farm reaped {perYear}. "
            + "A budget that over-states capacity is how a harvest comes to rot while every "
            + "guard says the arithmetic is fine.");
    }

    /// <summary>
    /// ⭐⭐ A farm brings in most of what it sows — <b>the harvest is not mostly rot</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, playing (2026-08-16): *"2x farmers planted 20 fields in the spring, and harvested
    /// only 9 in the fall — review the efficiencies with planting and harvesting for
    /// farmers."*</b> His audit trail said it was worse and permanent: **every year ~17 sown and
    /// ~5 reaped, with twelve to sixteen fields rotting, for ever.**
    /// </para>
    /// <para>
    /// <b>Two causes, and neither was the yield</b> (which he asked to leave alone):
    /// </para>
    /// <list type="number">
    /// <item><b>Nothing capped the sowing.</b> Sowing is cheap — a step between rows, carrying
    /// nothing — and reaping is dear, an armful to a store, so a spring always commits two or
    /// three times what an autumn can take. <c>FieldTilesOneFarmerKeeps</c> already takes the
    /// smaller of the two seasons for exactly this reason; nothing enforced it.</item>
    /// <item><b>They walked home between every tile.</b> The autumn cycle was
    /// <c>home → field → reap → store → home → rest</c>, five times in a season — and the home
    /// leg does nothing at all, because <c>Decide</c> runs there and sends them straight back
    /// out.</item>
    /// </list>
    /// <para>
    /// <b>Measured over ten years, same setup: 150 sown and 140 reaped — 93% brought in, against
    /// roughly 30% in Joe's run.</b>
    /// </para>
    /// <para>
    /// <b>⚠️ Asserted as a share rather than a count</b>, because the count depends on how much
    /// ground the player painted and how many hands the village spared, and neither is this
    /// guard's business. What must stay true is that **a farm's own spring does not condemn its
    /// own autumn** — which is also what makes use-it-or-lose-it mean anything: a rot line every
    /// year by construction is weather, and the player cannot act on weather.
    /// </para>
    /// </remarks>
    [Fact]
    public void AFarmBringsInMostOfWhatItSows()
    {
        SimConfig config = Config;
        SimLoop loop = FarmFixtures.WithNothingInTheStores(Loop(config));
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);

        // Deliberately more ground than the hands can keep — the case Joe was playing, and the
        // one where an uncapped spring does the damage.
        int painted = FarmFixtures.GiveItGround(world, farm, reach: 3);
        Assert.True(painted > VillageEconomy.FieldTilesOneFarmerKeeps(config) * farm.Capacity,
            "The farm was not over-painted, so this measures nothing.");

        const int Years = 10;
        int sown = 0;
        int reaped = 0;

        for (int i = 0; i < config.TicksPerYear * Years; i++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                if (!villager.Alive
                    || villager.WorkplaceId != farm.Id
                    || villager.ActionTicksRemaining != 1)
                {
                    continue;
                }

                if (villager.State == VillagerState.Sowing)
                {
                    sown++;
                }
                else if (villager.State == VillagerState.Reaping)
                {
                    reaped++;
                }
            }
        }

        int broughtIn = sown == 0 ? 0 : reaped * 100 / sown;
        _output.WriteLine(
            $"over {Years} years on {painted} painted tiles: sown {sown}, reaped {reaped} "
            + $"— {broughtIn}% brought in");

        Assert.True(sown > 0, "Nothing was sown, so the guard measures nothing.");
        Assert.True(
            broughtIn >= 75,
            $"Only {broughtIn}% of what the farm sowed was ever reaped. A spring that commits "
            + "ground the autumn cannot take turns use-it-or-lose-it from a consequence into "
            + "weather, and the player cannot act on weather.");
    }

    /// <summary>
    /// ⛔⭐⭐ A farm's harvest falls off sharply with how far it stands from its store — and
    /// <see cref="AFarmBringsInMostOfWhatItSows"/> cannot see that at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ THE GUARD ABOVE REPORTS 93% AND JOE'S VILLAGE WAS AT 46%, AND BOTH NUMBERS ARE
    /// HONEST</b> (D171). <see cref="FarmFixtures.ClearGroundNear"/> puts the farm on the first
    /// buildable tile beside the founding site — a few steps from the stores — so the walk the
    /// derivation budgets and the walk the farmer takes are the same walk. **It was unmoved
    /// because it does not cover the case**, which is D157's finding restated.
    /// </para>
    /// <para>
    /// <b>Measured across distances, ten years each, and this is the finding:</b>
    /// </para>
    /// <code>
    /// farm → granary   brought in
    /// next door               93%
    /// 6 ticks                 52%
    /// 10 ticks                46%   ← Joe's village, to the point
    /// 22 ticks                25%
    /// </code>
    /// <para>
    /// <b>⭐ AND THE BUFFER IS NOT THE LEVER, WHICH IS WHY <c>farm_store_cap</c> WAS LEFT
    /// ALONE.</b> Raising it from one armful to thirteen moves those numbers by nought to seven
    /// points. **Distance dominates**, and that puts this bug where `DESIGN.md §5` has been
    /// pointing for a phase: *"the fix is not a bigger number: it is to stop having one global
    /// yield — let yield be a property of the site."* `FieldTilesOneFarmerKeeps` is one number
    /// for every farm in the valley, so a distant farm sows what a near one could reap.
    /// </para>
    /// <para>
    /// <b>✅ AND PER-SITE YIELD LANDED, SO THIS GUARD IS RE-BASED (D178).</b> It used to
    /// *characterise* the bug — asserting only that a distant farm brought in **less** — and it
    /// said in as many words that a bar of *"a distant farm must also bring in 75%"* would be
    /// asserting a fix nobody had designed. **That fix now exists**, so the guard went red for
    /// the best possible reason: **a farm ten ticks out brings in 96%, the same as one next
    /// door.** *A guard that outlives the rule it was written for looks exactly like a
    /// regression* (D150), and the honest response is to re-base it rather than relax it.
    /// </para>
    /// <para>
    /// <b>⭐⭐ IT ASSERTS BOTH HALVES OF D58 NOW, AND THE SECOND ONE IS THE ONE THAT CAN BE
    /// LOST.</b> The rot is gone — a distant farm commits less ground instead of committing the
    /// same and losing the difference to winter — **and distance still costs**, because that
    /// farm reaps fewer tiles in total and therefore feeds fewer people. **Without the second
    /// assertion the fix would read as "distance is free"**, which is the opposite of the
    /// mechanism D58 settled on and would delete the decision this whole slice exists to create.
    /// </para>
    /// </remarks>
    [Fact]
    public void AFarmsHarvestFallsOffWithDistanceFromItsStore()
    {
        int near = BroughtInWithTheGranary(walkAway: 1, out int nearWalk, out int nearTiles);
        int far = BroughtInWithTheGranary(walkAway: 10, out int farWalk, out int farTiles);

        _output.WriteLine($"{nearWalk} ticks out: {near}% brought in, {nearTiles} tiles reaped");
        _output.WriteLine($"{farWalk} ticks out: {far}% brought in, {farTiles} tiles reaped");

        Assert.True(farWalk > nearWalk, "Both farms landed the same distance out.");

        // ⭐ THE ROT IS GONE — a distant farm no longer commits ground it cannot bring in.
        Assert.True(
            far >= 75,
            $"A farm {farWalk} ticks from its store brought in only {far}% of what it sowed. "
            + "The sowing cap is meant to ask THIS farm's haul, so a distant farm commits less "
            + "ground rather than committing the same and rotting the difference (D178).");
        Assert.True(near >= 75, $"Even the near farm only brought in {near}%.");

        // ⛔ AND DISTANCE STILL COSTS, WHICH IS THE HALF THAT MUST NOT BE LOST. The distant
        // farm wastes nothing and still feeds fewer people, because it commits less ground.
        // Without this the fix would read as "distance is free", which is the opposite of
        // D58's mechanism and would delete the decision the whole slice exists to create.
        Assert.True(
            farTiles < nearTiles,
            $"A farm {farWalk} ticks out reaped {farTiles} tiles against a near farm's "
            + $"{nearTiles} — distance stopped costing anything. Per-site yield without a cost "
            + "for distance is not the mechanism D58 settled on.");
    }

    /// <summary>
    /// Ten years of a farm sited as close as possible to <paramref name="walkAway"/> ticks from
    /// the granary; returns the percentage of what it sowed that it reaped.
    /// </summary>
    private static int BroughtInWithTheGranary(int walkAway, out int walk, out int tilesReaped)
    {
        SimConfig config = Config;
        SimLoop loop = FarmFixtures.WithNothingInTheStores(Loop(config));
        SimWorld world = loop.World;

        GridPos site = world.Map.FoundingSite;
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);

        GridPos best = FarmFixtures.ClearGroundNear(world);
        walk = world.TravelCost.TicksBetween(best, granary.Tile);

        for (int dy = -10; dy <= 10; dy++)
        {
            for (int dx = -10; dx <= 10; dx++)
            {
                var at = new GridPos(site.X + dx, site.Y + dy);
                if (world.HasSomethingToHarvest(at)
                    || !world.CanBuildAt(BuildingKind.Farmhouse, at).Allowed)
                {
                    continue;
                }

                int cost = world.TravelCost.TicksBetween(at, granary.Tile);
                if (cost != TravelCostField.Unreachable
                    && System.Math.Abs(cost - walkAway) < System.Math.Abs(walk - walkAway))
                {
                    best = at;
                    walk = cost;
                }
            }
        }

        Workplace farm = FarmFixtures.RaiseAFarm(world, best);
        FarmFixtures.GiveItGround(world, farm, reach: 3);

        int sown = 0;
        int reaped = 0;

        for (int i = 0; i < config.TicksPerYear * 10; i++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                if (!villager.Alive
                    || villager.WorkplaceId != farm.Id
                    || villager.ActionTicksRemaining != 1)
                {
                    continue;
                }

                if (villager.State == VillagerState.Sowing)
                {
                    sown++;
                }
                else if (villager.State == VillagerState.Reaping)
                {
                    reaped++;
                }
            }
        }

        tilesReaped = reaped;
        return sown == 0 ? 0 : reaped * 100 / sown;
    }

    /// <summary>
    /// The anti-vacuity companion (D7): a farm nobody works commits no ground at all.
    /// </summary>
    /// <remarks>
    /// Without this, a cap that simply refused to sow anything would score a perfect 100% above
    /// and would have quietly deleted farming — the degenerate pass D98 keeps warning about,
    /// where a rule reaches zero and switches a system off instead of bounding it.
    /// </remarks>
    [Fact]
    public void ButAFarmNobodyWorksSowsNothingAtAll()
    {
        SimWorld world = Loop(Config).World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 2);

        world.SetStaffing(farm, 0);

        _output.WriteLine(
            $"{farm.Name} has {farm.WorkerIds.Count} hands and may commit "
            + $"{world.HarvestOneFarmCanBringIn(farm)} tiles");

        Assert.Equal(0, world.HarvestOneFarmCanBringIn(farm));
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
    /// ⭐⭐ A trader runs the farm's buffer dry, so the next armful is a short walk again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>§3.2 ruling 1 has always said *"running it dry is the market's job"* and nothing ever
    /// did it</b> (D170, D171). The market's reach into a farm was built for *sourcing* — a
    /// trader filling a hungry larder may take from a farm that happens to be nearer — and there
    /// was no errand whose purpose was to <em>empty</em> a buffer. Measured in Joe's run: 27
    /// hauls, none of them to the farm, and eight of thirteen tiles brought in.
    /// </para>
    /// <para>
    /// <b>The state that matters is "cannot take another whole load"</b>, because that is
    /// exactly when <c>HaulTheHarvest</c> stops choosing the buffer and starts sending the
    /// farmer to the granary. Derived from <c>crop_yield_per_tile</c>, not tuned (D16).
    /// </para>
    /// </remarks>
    /// <remarks>
    /// <b>⚠️ A YEAR, NOT A SEASON, AND THE STAFFING IS LEFT TO THE VILLAGE.</b> The first draft
    /// forced a villager onto the stall and ran one season, and it failed against working code:
    /// the quota wants <em>no</em> marketers in the opening and the allocator puts a forced hand
    /// straight back. Waiting for the village to want a trader is both honest and what actually
    /// happens.
    /// </remarks>
    [Fact]
    public void AMarketerRunsTheFarmsBufferDry()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);

        // Full enough that it can no longer take a whole armful — the exact state that
        // lengthens the farmer's walk, and the reason this errand exists.
        farm.Store.Add(Goods.Wheat, Config.FarmStoreCap);
        Assert.True(
            farm.Store.FreeSpace < Config.CropYieldPerTile,
            "The buffer can still take a whole load, so there is nothing here to clear.");

        int before = farm.Store[Goods.Wheat];
        loop.Step(Config.TicksPerYear);

        _output.WriteLine(
            $"{farm.Name} held {before} of {farm.Store.Capacity}, now {farm.Store[Goods.Wheat]}; "
            + $"the village holds {world.FoodTheVillageHolds()}");

        Assert.True(
            farm.Store[Goods.Wheat] < before,
            "A year passed and the farm's buffer never moved — §3.2's \"running it dry is the "
            + "market's job\" is still unbuilt.");

        Assert.True(
            farm.Store.FreeSpace >= Config.CropYieldPerTile,
            "The buffer was nibbled but still cannot take a whole armful, so the farmer's walk "
            + "is no shorter and the clearing achieved nothing.");
    }

    /// <summary>
    /// ⛔ And the condition, both arms: a buffer is worth clearing only once it can no longer
    /// take an armful.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Without this the rule could be "traders always strip farms", which passes the guard
    /// above</b> — and that is the churn D34 records killing a village: marketers took "surplus"
    /// from living households, families fetched it straight back, the granary stopped filling
    /// and the settlement died out at five people.
    /// </para>
    /// <para>
    /// <b>⚠️ ASSERTED AS ARITHMETIC RATHER THAN AS BEHAVIOUR, AND THE REASON MATTERS.</b> The
    /// obvious behavioural form — *a farm with room is never touched over a year* — <b>asserts
    /// something the design contradicts</b>: §3.2 ruling 2 lets a trader source from a farm that
    /// happens to be nearer than the granary, whatever room it has. Its first draft passed only
    /// because one season was too short for that to happen, which would have made it a guard
    /// that went red the day somebody moved a granary. Same reasoning, and same restating, as
    /// <see cref="NearerFarm"/> a few methods down.
    /// </para>
    /// </remarks>
    [Fact]
    public void AFarmsBufferIsOnlyWorthClearingWhenItCannotTakeAnArmful()
    {
        SimWorld world = Loop(Config).World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);

        Assert.False(WorthClearing(world, farm), "An empty buffer is nothing to clear.");

        int seed = Config.FarmStoreCap - Config.CropYieldPerTile;
        Assert.True(seed > 0, "The cap cannot hold even one armful, so this proves nothing.");
        farm.Store.Add(Goods.Wheat, seed);

        _output.WriteLine(
            $"holding {farm.Store[Goods.Wheat]} of {farm.Store.Capacity}, {farm.Store.FreeSpace} free "
            + $"against an armful of {Config.CropYieldPerTile}");

        Assert.False(
            WorthClearing(world, farm),
            "A buffer that can still take a whole armful is doing its job, and clearing it is "
            + "the churn that killed the village in D34.");

        farm.Store.Add(Goods.Wheat, Config.FarmStoreCap);

        _output.WriteLine(
            $"holding {farm.Store[Goods.Wheat]} of {farm.Store.Capacity}, {farm.Store.FreeSpace} free");

        Assert.True(WorthClearing(world, farm));
    }

    /// <summary>
    /// The clearing rule, restated: a workplace holding food that can no longer take a whole
    /// armful.
    /// </summary>
    private static bool WorthClearing(SimWorld world, Workplace workplace) =>
        !workplace.IsSite
        && workplace.Store[Goods.Wheat] > 0
        && workplace.Store.FreeSpace < world.Config.CropYieldPerTile;

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
        farm.Store.Add(Goods.Wheat, 50);
        granary.Store.Add(Goods.Produce, 50);

        Workplace? fromTheGranary = NearerFarm(world, granary.Tile, granary);
        Workplace? fromTheFarm = NearerFarm(world, farm.Tile, granary);

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
        granary.Store.Add(Goods.Produce, 50);

        Assert.Null(NearerFarm(world, farm.Tile, granary));
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
        int beat = world.TravelCost.TicksBetween(from, nearest.Tile);
        Workplace? best = null;
        int bestCost = int.MaxValue;

        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace workplace = world.Workplaces[i];
            if (workplace.IsSite || workplace.Store[Goods.Wheat] <= 0)
            {
                continue;
            }

            int cost = world.TravelCost.TicksBetween(from, workplace.Tile);
            if (cost < beat && cost < bestCost)
            {
                best = workplace;
                bestCost = cost;
            }
        }

        return best;
    }
}
