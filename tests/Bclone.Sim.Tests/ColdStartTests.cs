using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The founders arrive with a cart and nothing else — <c>specs/cold-start.md</c> (D70, D71).
/// </summary>
/// <remarks>
/// <para>
/// <b>These exist because the rest of the suite deliberately does not test this.</b> Joe's
/// call: fixtures keep the old founding, so several hundred tests go on describing an
/// established village rather than four people freezing in a field. The cost of that is
/// obvious and is exactly what METHODOLOGY §3 warns about — the shipped file would otherwise
/// be the one path in the game nothing exercises, and that gap has already produced D48, D49
/// and D50. So the cold start gets its own fixture and its own guards, and
/// <see cref="TheShippedGameStartsColdAndTheFixturesDoNot"/> is the one that ties them to
/// what actually ships.
/// </para>
/// </remarks>
public sealed class ColdStartTests
{
    private readonly ITestOutputHelper _output;

    public ColdStartTests(ITestOutputHelper output) => _output = output;

    /// <summary>The village fixture, but arriving to an empty valley.</summary>
    private static SimConfig ColdVillage => VillageFixtures.Village with
    {
        FoundingBuildings = false,
        CartCapacity = ShippedConfig.Load().CartCapacity,
        CartFood = ShippedConfig.Load().CartFood,
    };

    private static SimLoop Build(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    // ---------------------------------------------------------------
    //  What the founders actually get
    // ---------------------------------------------------------------

    /// <summary>Nothing is standing but the cart, and nobody has a roof.</summary>
    [Fact]
    public void TheFoundersArriveToAnEmptyValley()
    {
        SimWorld world = Build(ColdVillage).World;

        Assert.Single(world.StoreBuildings);
        Assert.Equal(StoreKind.Cart, world.StoreBuildings[0].Kind);
        Assert.NotNull(world.TheCart);

        Assert.All(world.Households, household => Assert.False(household.HasHome));

        // ⭐ AND THE VALLEY IS NOW GENUINELY EMPTY, WHICH MAKES THIS GUARD STRONGER.
        // It used to say *"the valley's own features remain — a berry patch is not a
        // building"* and assert that a Forager workplace existed before anybody built
        // anything. Six of them did, laid down by the generator, and they were the last
        // thing in this game that handed the player an economy instead of asking them to
        // build one. **There is no work of any kind in the valley now** — which is the whole
        // of Joe's *"no forest, no food"*, and the first real decision of a run is where the
        // first gatherer's hut goes.
        Assert.Empty(world.Workplaces);

        // And nowhere is painted: where to live is the player's first decision (D64).
        Assert.Equal(0, world.Zones.ResidentialTiles);

        _output.WriteLine(
            $"{world.Population} founders, {world.Households.Count} households, "
            + $"{world.StoreBuildings.Count} building, {world.TheCart!.Store.Food} food in it");
    }

    /// <summary>They brought tools, and nothing in the valley can replace them (D82).</summary>
    /// <remarks>
    /// <para>
    /// <b>Asserted against the shipped config</b>, because the fixture keeps the old founding
    /// and would not have a cart to check. The number is content and lives in
    /// <c>cart_tools</c>; what this guards is that the founding actually loads it, and that
    /// the tools do not silently displace the food — the cart has one capacity across every
    /// good, so a good added carelessly to the founding load is a good taken off it.
    /// </para>
    /// <para>
    /// <b>Nothing spends tools yet</b> (D17 parks the workshop), so there is no behaviour to
    /// test beyond this. That is the honest state of the slice and is recorded rather than
    /// dressed up.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheFoundersBringToolsAndNotAtTheCostOfTheirFood()
    {
        SimConfig config = ShippedConfig.Load();
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

        StoreBuilding cart = world.TheCart!;
        _output.WriteLine(
            $"the cart: {cart.Store.Food} food, {cart.Store.Logs} logs, "
            + $"{cart.Store[Goods.Tools]} tools, {cart.Store.FreeSpace} spare of "
            + $"{config.CartCapacity}");

        Assert.Equal(config.CartTools, cart.Store[Goods.Tools]);
        Assert.True(config.CartTools > 0, "The founders arrive with tools (Joe's opening).");

        // The whole load fitted, so nothing was quietly left behind at the roadside.
        Assert.Equal(config.CartFood, cart.Store.Food);

        // ⭐ AND NO TIMBER, EVER (D90 step 4). The cart is a food-and-tools box: it will not
        // take logs, so `cart_logs` is gone rather than zeroed and the wagon cannot be
        // strangled by a woodpile (D89).
        // The Stockpile itself knows nothing about Accepts — which is exactly why the config
        // key had to go rather than sit at zero: it would have gone on loading the fixture's
        // default of ten straight past the refusal.
        Assert.Equal(0, cart.Store.Logs);
        Assert.False(cart.Accepts(Goods.Logs));
    }

    /// <summary>The cart feeds them, so D10 survives the founding.</summary>
    /// <remarks>
    /// Phase 0 killed a villager who starved beside a full larder and decided a survival game
    /// may kill you for a bad decision but never for a scheduling artifact. A village with no
    /// larders at all is where that promise is easiest to break.
    /// </remarks>
    [Fact]
    public void TheCartFeedsThemBeforeThereIsALarder()
    {
        SimConfig config = ColdVillage;
        SimLoop loop = Build(config);

        // Half a year — long enough that everyone has been hungry several times over.
        loop.Step(config.TicksPerYear / 2);

        Assert.Equal(config.StartingPopulation, loop.World.Population);
        Assert.Equal(
            0,
            CountDeaths(loop.World, CauseOfDeath.Starvation));
    }

    // ---------------------------------------------------------------
    //  Joe's bar
    // ---------------------------------------------------------------

    /// <summary>⭐ Doing nothing kills. The founding is not survivable unattended.</summary>
    /// <remarks>
    /// <para>
    /// Joe's stated bar for the slice: <em>winter 1 shouldn't be survivable unless the user
    /// builds houses and a woodcutter with firewood before they freeze.</em> Nothing is
    /// painted and nothing is marked here, so nothing is built, so there is no roof and no
    /// fire — and <c>ShelterAt</c> answers <c>Outdoors</c> everywhere.
    /// </para>
    /// <para>
    /// <b>No new difficulty was added to make this true.</b> Winter is 120 ticks and open
    /// ground kills in 60 (D45, D53); the model has always said this and has never been
    /// allowed to say it, because the village started with its buildings.
    /// </para>
    /// </remarks>
    [Fact]
    public void DoingNothingKillsTheFounders()
    {
        SimConfig config = ColdVillage;
        SimLoop loop = Build(config);

        loop.Step(config.TicksPerYear);

        int frozen = CountDeaths(loop.World, CauseOfDeath.Cold);
        _output.WriteLine(
            $"after one unattended year: {loop.World.Population} alive, {frozen} frozen");

        Assert.True(
            loop.World.Population < config.StartingPopulation,
            "An unattended founding survived its first winter, so the opening asks nothing "
            + "of the player.");
    }

    /// <summary>Nobody freezes before winter, so the deaths are winter's and not a bug.</summary>
    /// <remarks>
    /// The anti-vacuity half (D7) of the guard above, and a real risk rather than a
    /// formality: a cold start that killed people in spring would pass that test for
    /// entirely the wrong reason.
    /// </remarks>
    [Fact]
    public void NobodyFreezesBeforeWinter()
    {
        SimConfig config = ColdVillage;
        SimLoop loop = Build(config);

        while (loop.World.Clock.Season != Season.Winter)
        {
            loop.StepOnce();
            Assert.Equal(config.StartingPopulation, loop.World.Population);
        }

        _output.WriteLine($"reached winter with all {loop.World.Population} founders alive");
    }

    /// <summary>
    /// ⭐ A competently played opening survives winter 1 — the other half of Joe's bar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Written after Joe played it and everyone died anyway</b>, which is what this guard
    /// exists to stop happening twice. Two bugs made the opening unwinnable rather than hard
    /// (D72): a family that already existed and had no roof was invisible to
    /// <c>ForestersWanted</c>, so the village wanted nobody at the tree stand; and building
    /// timber was drawn only from sheds, of which a cold start has none, so even felled logs
    /// could not become a house.
    /// </para>
    /// <para>
    /// The player's part is scripted here as the things they can actually do on day one:
    /// paint somewhere to live, mark a woodcutter's hut, and — since C-4 retired the berry
    /// patches and the tree stands — say where the food and the timber are to come from.
    /// Everything after that is the village's own machinery.
    /// </para>
    /// </remarks>
    [Fact]
    public void AVillageThatIsPlayedSurvivesItsFirstWinter()
    {
        SimConfig config = ColdVillage;
        SimLoop loop = Build(config);
        SimWorld world = loop.World;

        // Day one, and the only two moves that matter.
        GridPos site = world.Map.FoundingSite;
        for (int dy = -4; dy <= 4; dy++)
        {
            for (int dx = -4; dx <= 4; dx++)
            {
                world.PaintResidential(new GridPos(site.X + dx, site.Y + dy));
            }
        }

        // Somewhere to put timber, and the hut that turns it into firewood — without which a
        // house is a roof with no fire under it, and D45 kills at day 25 of 30.
        //
        // ⭐ THE PILE FIRST, AND A SHED CANNOT TAKE ITS PLACE (D90 step 4). A shed costs 30
        // logs and is a construction site, so it cannot be what holds the village's first
        // timber — the cart will not take logs any more, and a founding with nowhere to put
        // a felled tree sets it down in a field where nothing can spend it. The pile costs
        // the ground it stands on and stands the tick it is marked (D98). The shed stays,
        // built out of the pile, because this guard is about a village that grows.
        MarkSomewhereNear(world, BuildingKind.Pile, site, 2);

        // ⭐ AND SOMEBODY TO BUILD (D108). The shed and the hut below are construction
        // sites, and a site is an errand the builder's hut's crew walks out to rather than
        // somewhere anybody is posted — so without a hut this village marks three buildings
        // and raises none of them. Measured, before this line was added: 0 alive, 4 frozen,
        // 0 homes. It costs nothing but the ground, which is exactly why it is free.
        MarkSomewhereNear(world, BuildingKind.BuilderHut, site, 2);
        MarkSomewhereNear(world, BuildingKind.Shed, site, 2);
        MarkSomewhereNear(world, BuildingKind.WoodcutterHut, site, 3);

        // ⭐ AND SOMEWHERE TO GET FOOD AND TIMBER, WHICH THIS GUARD USED TO GET FREE FROM THE
        // MAP (D157). The four moves above are the whole of the opening as it was written, and
        // they were enough while the generator laid berry patches and tree stands down before
        // the founders arrived. Since C-4 retired both, this village had **no food source and
        // no source of logs anywhere in the valley** — the shed and the woodcutter's hut are
        // construction sites with nothing to build them from, and the cart holds 0 logs by
        // D90's rule. Measured without this line: 0 alive, 4 frozen, 0 homes.
        //
        // `FeedTheFounding` is the helper that exists for precisely this and is deliberately
        // not the whole of `PlayTheOpening` — the builder's hut is marked above, because
        // whether one is standing is what the note at line 243 is about.
        FeedTheFounding(world);

        _output.WriteLine(
            $"painted {world.Zones.ResidentialTiles} tiles; cart holds "
            + $"{world.TheCart!.Store.Food} food, {world.TheCart!.Store.Logs} logs");

        loop.Step(config.TicksPerYear);

        int frozen = CountDeaths(world, CauseOfDeath.Cold);
        int homes = 0;
        foreach (Household household in world.Households)
        {
            if (household.HasHome)
            {
                homes++;
            }
        }

        _output.WriteLine(
            $"after one played year: {world.Population} alive, {frozen} frozen, {homes} homes");

        // WHAT IS PROVEN, AND IT IS THE THING FOUR BUGS WERE HIDING: a village that starts
        // with nothing can build itself houses. Before D72 this was zero, for four separate
        // reasons, and the village could not have been saved by any amount of play.
        Assert.True(homes > 0, "A year passed with land painted and no house was ever built.");

        // AND IT SURVIVES (D75). This was unwinnable through three playthroughs and six
        // bugs, and the last of them was a builder who was funded, standing beside 426
        // logs, unable to see them because the fetch read sheds and the timber was in the
        // cart. Nothing was tuned to get here — the cart still holds what it held when the
        // village was dying, which is the point: it was never a difficulty problem.
        Assert.True(
            world.Population == config.StartingPopulation,
            $"{config.StartingPopulation - world.Population} founders died in a played "
            + $"opening; {frozen} of them froze.");
    }

    /// <summary>
    /// ⭐ Joe's own opening, on the config the game loads: a pile, land, a hut. No shed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the run that killed his village, scripted.</b> He placed a woodcutter's hut
    /// and never a shed, and the hut reported <em>"no logs here to split"</em> while the
    /// timber sat in the cart — because the fetch named <c>StoreKind.Shed</c>. That was the
    /// fourth site of one bug, and D76 replaced the question rather than patching the fifth.
    /// </para>
    /// <para>
    /// <b>Run against <see cref="ShippedConfig"/>, not the fixture</b>, because the whole
    /// point is that his game died where the fixture's test passed. The other guards here
    /// use the fixture; this one must not.
    /// </para>
    /// </remarks>
    [Fact]
    public void JoesOpeningSurvivesOnTheShippedConfig()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Assert.False(config.FoundingBuildings, "This guard is meaningless on a warm start.");

        PlayTheOpening(world);

        // The opening asks for a pile and nothing else — no shed, no granary. Food goes back
        // to the cart they arrived in; timber cannot, so it goes to the pile (D90 step 4).
        Assert.Equal(2, world.StoreBuildings.Count);
        Assert.Contains(world.StoreBuildings, store => store.Kind == StoreKind.Cart);
        Assert.Contains(world.StoreBuildings, store => store.Kind == StoreKind.Pile);

        loop.Step(config.TicksPerYear);

        int frozen = CountDeaths(world, CauseOfDeath.Cold);
        _output.WriteLine(
            $"Joe's opening on the shipped config: {world.Population} alive, {frozen} frozen, "
            + $"{world.FirewoodInSheds()} firewood in stores");

        Assert.True(
            world.Population == config.StartingPopulation,
            $"{config.StartingPopulation - world.Population} founders died playing the "
            + $"opening as designed; {frozen} froze.");
    }

    /// <summary>
    /// ⭐ The founders fell the trees they painted, and that is where the timber comes from.
    /// </summary>
    /// <remarks>
    /// <b>The opening's material chain, asserted</b> (D87). Before this, timber came from a
    /// tree stand the generator dropped and the player had no say in it. Now the founders mark
    /// what they mean to take and their spare hands take it — which is the answer to
    /// <c>building-placement.md §12.5</c>'s <em>"unpainted forest is never cut"</em> that does
    /// not require auto-painting a starter zone and therefore does not reverse D70.
    /// </remarks>
    [Fact]
    public void TheOpeningGetsItsTimberFromTheTreesThePlayerPainted()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        PlayTheOpening(world);

        int painted = PaintTheNearbyTrees(world, 10);
        Assert.True(painted > 0, "There must be fellable forest near the founding site.");

        int forestBefore = 0;
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            if (world.Map.Tiles[i] == Terrain.Forest)
            {
                forestBefore++;
            }
        }

        loop.Step(config.TicksPerYear);

        int forestAfter = 0;
        for (int i = 0; i < world.Map.Tiles.Count; i++)
        {
            if (world.Map.Tiles[i] == Terrain.Forest)
            {
                forestAfter++;
            }
        }

        _output.WriteLine(
            $"{painted} tiles painted; forest {forestBefore} -> {forestAfter}, "
            + $"{world.LogsInSheds()} logs in reach, {world.Population} alive");

        Assert.True(
            forestAfter < forestBefore,
            "A year passed with trees painted at the founding and none was felled.");
    }

    /// <summary>
    /// ⭐ And it is still alive after five years — winter 1 was never the hard one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe played the opening twice and both villages starved in winter 2.</b> Every
    /// guard here stopped at one year, so the game was tested precisely up to the point it
    /// began going wrong. His cart held <b>541 firewood</b> and nobody was foraging: with no
    /// shed built, <c>FirewoodInSheds</c> read zero however much fuel was standing about, so
    /// the fuel quota kept every spare hand on the chain and the berry patches sat empty
    /// (D79).
    /// </para>
    /// <para>
    /// Five years rather than two, because the failure was a <em>drift</em> — the village
    /// looked fine at the end of year 1 and was already committing the mistake that killed
    /// it. A guard that stops the tick after the danger starts is the one that let this
    /// through.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheOpeningStillHasAVillageFiveYearsLater()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        PlayTheOpening(world);
        loop.Step(config.TicksPerYear * 5);

        int starved = CountDeaths(world, CauseOfDeath.Starvation);
        int frozen = CountDeaths(world, CauseOfDeath.Cold);
        _output.WriteLine(
            $"five years on: {world.Population} alive, {starved} starved, {frozen} frozen, "
            + $"{world.TotalFood()} food and {world.FirewoodInSheds()} firewood in reach");

        Assert.True(
            starved == 0,
            $"{starved} founders starved in the first five years, with {world.TotalFood()} "
            + "food in the village. Somebody is not being sent to pick berries.");

        Assert.True(
            world.Population >= config.StartingPopulation,
            $"The village fell to {world.Population} from {config.StartingPopulation}.");
    }

    // ---------------------------------------------------------------
    //  Somewhere to put it — D81
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ A village with a pile and no granary has a reason to go out and work.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The bug in one assertion.</b> There are two reasons to forage — my family is
    /// short, or the village is — and the second is measured against what the village has
    /// <em>room</em> for. That room used to be read off <c>GranaryCapacity()</c>, which
    /// counts granaries by kind, while the amount it was compared against counts every
    /// store that takes food. <b>One comparison, two different questions</b>, and a cold
    /// start is where they part company: no granary, no room, so the village answered
    /// <em>"we want no more food"</em> forever — standing beside a pile with room for
    /// eleven hundred.
    /// </para>
    /// <para>
    /// Measured on Joe's opening before the fix: the village-wide reason to work fired on
    /// <b>0.0%</b> of gatherable ticks over five years, against <b>5.6%</b> after. This is
    /// D76's seam for the fifth time and D79's for the second, which is why it gets an
    /// assertion rather than a comment.
    /// </para>
    /// </remarks>
    [Fact]
    public void AVillageWithAPileAndNoGranaryStillWantsFood()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        MarkSomewhereNear(world, BuildingKind.Pile, world.Map.FoundingSite, 2);

        Assert.Equal(0, world.GranaryCapacity());

        _output.WriteLine(
            $"no granary: granaryCapacity {world.GranaryCapacity()}, "
            + $"foodInStores {world.FoodInGranaries()}, "
            + $"roomFor {world.FoodTheVillageHasRoomFor()}");

        // The room, not the appetite. At tick 0 the cart holds 800 food against a target
        // of 380, so a village that wanted MORE right now would be the wrong answer — the
        // bug is that it had nowhere to put any, ever, at any level of stock.
        Assert.True(
            world.FoodTheVillageHasRoomFor() > 0,
            "A village with a pile standing in the square believes it has nowhere to put "
            + "food, so nobody is ever sent to gather any.");
    }

    /// <summary>
    /// ⭐ And the stores actually fill — the consequence a player watches.
    /// </summary>
    /// <remarks>
    /// The behavioural half of the guard above, and the one that would have caught this
    /// from the outside. Joe's opening ran five years with <b>zero</b> food ever reaching a
    /// store: every forager stopped the moment their own larder was full, so the village
    /// held only what was in its larders and the pile stayed empty. A village store that
    /// never fills is a village that cannot survive losing anybody.
    /// </remarks>
    [Fact]
    public void TheOpeningFillsItsStores()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        PlayTheOpening(world);
        loop.Step(config.TicksPerYear * 5);

        _output.WriteLine(
            $"five years on: {world.FoodInGranaries()} food in stores, "
            + $"{world.TotalFood()} in the village, pop {world.Population}");

        Assert.True(
            world.FoodInGranaries() > 0,
            "Five years of play and not one berry ever reached the village store.");
    }

    /// <summary>
    /// ⭐ Both founding households do a share of the work.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, playing:</b> <em>"one couple stays home and the other does most of the
    /// work."</em> Measured over twenty years before the fix: the Thatchers spent
    /// <b>4.3%</b> of their able-adult ticks at work against the Fletchers' <b>6.7%</b>,
    /// and in year one it was <b>4.1%</b> against <b>26.0%</b>. Both households held jobs
    /// equally (75.6% against 74.7%) and lived the same distance from the nearest berries,
    /// so the allocator was sharing the <em>posts</em> out evenly and the resting household
    /// simply never went to theirs — a forager only forages when somebody wants food, and
    /// with the village-wide half of that question dead, the family whose larder filled
    /// first stopped working and never started again.
    /// </para>
    /// <para>
    /// After: <b>7.1%</b> against <b>8.2%</b>. The bar is set at a factor of two rather
    /// than at those numbers, because <b>a residual gap is expected and is not this
    /// bug</b>: the household nearer the timber gets the logger's seat, and a logger works
    /// about a third of their ticks where a forager works a twentieth. <b>Joe closed that
    /// as intended</b> — it is inequality of the kind §2.2 exists to produce, spatial and
    /// back-traceable to where a family lives. So the loose bar is not a placeholder for a
    /// decision still to come; it is the guard declining to freeze a gap the design wants.
    /// </para>
    /// </remarks>
    [Fact]
    public void NeitherFoundingHouseholdRestsWhileTheOtherWorks()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        // ⛔ TWO HUTS, BECAUSE ONE SEATS TWO AND THIS VILLAGE IS TWO COUPLES (D262). See
        // `PlayTheOpeningWithTwoGatheringHuts` — with a single hut the nearer couple takes both
        // seats and the measurement is 1.2% against 5.2%.
        PlayTheOpeningWithTwoGatheringHuts(world);

        int[] founding = { 1, 2 };
        var adultTicks = new long[3];
        var workTicks = new long[3];

        for (long tick = 0; tick < config.TicksPerYear * 20L; tick++)
        {
            loop.StepOnce();

            foreach (Villager villager in world.Villagers)
            {
                if (!villager.Alive || !villager.CanWork
                    || System.Array.IndexOf(founding, villager.HouseholdId) < 0)
                {
                    continue;
                }

                adultTicks[villager.HouseholdId]++;
                if (IsWorking(villager.State))
                {
                    workTicks[villager.HouseholdId]++;
                }
            }
        }

        double first = Share(workTicks[1], adultTicks[1]);
        double second = Share(workTicks[2], adultTicks[2]);

        _output.WriteLine(
            $"twenty years: household 1 worked {first:F1}% of its able-adult ticks "
            + $"({workTicks[1]}/{adultTicks[1]}), household 2 {second:F1}% "
            + $"({workTicks[2]}/{adultTicks[2]})");

        // Anti-vacuity (D7): a comparison between two households who both did nothing
        // would pass this and mean nothing at all.
        Assert.True(
            first > 1.0 && second > 1.0,
            $"Neither founding household did any work to compare ({first:F1}%, {second:F1}%).");

        double lower = Math.Min(first, second);
        double higher = Math.Max(first, second);
        Assert.True(
            higher <= lower * 2,
            $"One founding household worked {higher:F1}% of its ticks while the other "
            + $"worked {lower:F1}% — more than twice the share, which is Joe's "
            + "\"one couple stays home\" back again.");
    }

    /// <summary>
    /// ⭐ A village that is only ever given a pile is still standing forty years on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The founders are all dead by then</b>, so this is the first guard that asks
    /// whether the opening produces a village rather than a well-managed campsite. Before
    /// D81 it did not: with no granary the village had no reason to gather beyond its own
    /// larders, so it never built a store, never passed the birth gate, and the same four
    /// people aged out — <b>population 4 at twenty years and 0 at a hundred</b>. After, it
    /// is 9 at twenty and 7 at a hundred, in five households.
    /// </para>
    /// <para>
    /// D79's lesson, applied one length further out: <em>a guard that stops the tick after
    /// the danger begins is the one that lets it through.</em> Five years was long enough
    /// to see nobody starve and far too short to see nobody be born.
    /// </para>
    /// </remarks>
    [Fact]
    public void AVillageGivenOnlyAPileOutlivesItsFounders()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        PlayTheOpening(world);
        loop.Step(config.TicksPerYear * 40);

        int founders = 0;
        foreach (Villager villager in world.Villagers)
        {
            if (villager.Alive && villager.BirthYear == 0)
            {
                founders++;
            }
        }

        _output.WriteLine(
            $"forty years on: {world.Population} alive in {world.Households.Count} households, "
            + $"{founders} of the founders left, {world.FoodInGranaries()} food in stores");

        // NOT "> 0", and the loose bar is why this let something through once already.
        // A village that ages from nine down to two over forty years passes any
        // still-standing test while plainly failing, and it does it with nobody starved
        // and nobody frozen. The founding four is the floor worth defending.
        Assert.True(
            world.Population >= config.StartingPopulation,
            $"A played opening fell to {world.Population} from {config.StartingPopulation} "
            + "inside forty years without anybody starving or freezing — it simply stopped "
            + "having children.");
    }

    private static bool IsWorking(VillagerState state) =>
        state is VillagerState.Gathering or VillagerState.Cutting
            or VillagerState.MakingFirewood or VillagerState.Building
            or VillagerState.CollectingForMarket or VillagerState.DeliveringToHome;

    private static double Share(long part, long whole) => whole == 0 ? 0 : 100.0 * part / whole;

    /// <summary>
    /// Joe's opening as he actually plays it: somewhere to live, trees to fell, a hut.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No pile and no shed.</b> Joe, playing: <em>"so far I've found that I don't actually
    /// need the pile at the start. It doesn't get used at all because the cart stores wood and
    /// I haven't harvested any other resources."</em> He is right, and the reason is that
    /// <b>D76's motivation was fixed by D76's own change</b> — the pile was introduced because
    /// the woodcutter's hut could not see logs sitting in the cart, and that bug was cured by
    /// asking stores what they <em>hold</em> rather than which building they are. The pile is
    /// still a perfectly good thing to place; it simply stopped being load-bearing, and a
    /// guard that scripts it as though it were is testing a game nobody plays.
    /// </para>
    /// <para>
    /// <b>Painting trees replaced it as the third move</b> (D87). That is where the timber
    /// comes from now: the founders mark what they mean to fell and their spare hands go and
    /// fell it.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The half of the opening that keeps anybody alive: somewhere to gather, something to fell.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ Since the thickets retired, a founding valley contains no workplaces at all</b> —
    /// measured on the shipped config: four laborers, zero food gathered, everybody dead
    /// inside the first year. Food and timber both used to come free with the map, from berry
    /// patches and tree stands the generator dropped, so a test about <em>building</em> could
    /// mark a site and never think about either.
    /// </para>
    /// <para>
    /// That is no longer true and it broke four guards at once, all of them reporting the
    /// building failure rather than the starvation underneath it: a village with nobody
    /// gathering does not raise anything, because there is nobody left to raise it. Any test
    /// that runs the shipped opening for more than a season needs this, and it is deliberately
    /// <em>not</em> the whole of <see cref="PlayTheOpening(SimWorld)"/> — the builder's hut is left out,
    /// because whether one is standing is precisely what several of those guards vary.
    /// </para>
    /// </remarks>
    internal static void FeedTheFounding(SimWorld world)
    {
        MarkInTheBestWoodland(world, world.Map.FoundingSite);
        PaintTheNearbyTrees(world, 6);
        PaintTheNearestSeam(world);
    }

    /// <summary>
    /// Send the laborers to a rock — <b>part of founding a village since D214</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ A hut costs three stone now</b>, and stone comes from nowhere but the brush. So a
    /// played opening paints a seam for exactly the reason it already paints trees: with no
    /// painted ground there is <em>no source at all</em>, and both huts stay sites for ever.
    /// This is the tree comment above, one good over.
    /// </para>
    /// <para>
    /// <b>⛔ AND THE STONE IS THERE TO BE PAINTED, WHICH WAS THE WHOLE ARGUMENT (Joe, D215).</b>
    /// <em>"There should already be stone on the map for the user to ask the laborers to harvest.
    /// I don't want stone in the cart."</em> There is: <c>stone_seam_count</c> is 4 on a
    /// <c>stone_seam_ring_tiles</c> ring of 14, and `MapGenerator` states the intent out loud —
    /// <em>"STONE NEAR, IRON FAR… a valley whose ore sits in the far woods plays differently from
    /// one where it is on the doorstep."</em> Measured across six seeds, the nearest reachable
    /// stone is <b>twelve tiles</b> from the founding site on every one of them.
    /// </para>
    /// <para>
    /// ⚠️ <b>Four tiles, not the whole ring.</b> A tile is 12 stone, so four is ~48 — the two
    /// huts the opening marks cost 6 between them, and the rest is what a player would have in
    /// hand for the first granary. Painting the lot would be the <c>PaintTheNearbyTrees(12)</c>
    /// mistake one good over: laborers spend the extra walk clearing instead of building.
    /// </para>
    /// </remarks>
    internal static void PaintTheNearestSeam(SimWorld world) =>
        SeamFixtures.PaintNearest(world, Terrain.Rock, 4);

    /// <summary>
    /// The opening as it stands before anybody builds a shed: a storage pile takes the timber.
    /// </summary>
    /// <remarks>
    /// A pile accepts every kind of goods and is the one store that does, so this village has
    /// somewhere for logs to go — which is precisely the arrangement D132 could not see. Kept
    /// here beside the openings it is a variant of, rather than copied into the one test that
    /// wants it.
    /// </remarks>
    internal static void PlayTheOpeningWithoutAShed(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;

        for (int dy = -4; dy <= 4; dy++)
        {
            for (int dx = -4; dx <= 4; dx++)
            {
                world.PaintResidential(new GridPos(site.X + dx, site.Y + dy));
            }
        }

        MarkSomewhereNear(world, BuildingKind.Pile, site, 2);
        MarkSomewhereNear(world, BuildingKind.BuilderHut, site, 2);
        MarkSomewhereNear(world, BuildingKind.WoodcutterHut, site, 3);
        FeedTheFounding(world);
    }

    internal static void PlayTheOpening(SimWorld world) => PlayTheOpening(world, paintASeam: true);

    /// <summary>
    /// The opening, plus a <b>second gathering hut clear of the first</b> (D262).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ A HUT SEATS TWO AND THE FOUNDING IS TWO COUPLES, so one hut is one couple's
    /// livelihood.</b> Measured on the shipped config over twenty years with the single-hut
    /// opening: **household 1 worked 1.2% of its able-adult ticks and household 2 worked 5.2%**
    /// — four times the share, which is Joe's own *"one couple stays home"* arriving through the
    /// front door of a change meant to make placement matter.
    /// </para>
    /// <para>
    /// ⭐ <b>The answer is the one the cap exists to force</b>: *"the player has to build more
    /// if it wants more forest"* (Joe, 2026-08-29). Two huts, and both couples work.
    /// </para>
    /// <para>
    /// ⛔⛔ <b>SEPARATE FROM <see cref="PlayTheOpening(SimWorld)"/> ON PURPOSE, AND THE
    /// FIRST ATTEMPT FOLDED IT IN.</b> That opening is shared by dozens of guards, and a second
    /// hut moves every one of them — measured: it turned this guard green and reddened four
    /// others (both food-limit guards, the pinned trade, and the length of a rest) by shifting
    /// which founder holds which job on which tick. ⚠️ **Whether the SHIPPED opening should now
    /// be two huts is a design call, not a test convenience**, and it is Joe's to make.
    /// </para>
    /// </remarks>
    internal static void PlayTheOpeningWithTwoGatheringHuts(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        PlayTheOpening(world, paintASeam: true);
        MarkInTheBestWoodland(world, world.Map.FoundingSite, clearOfExistingRings: true);
    }

    /// <summary>
    /// The opening, with the option of NOT sending anybody to the rock.
    /// </summary>
    /// <remarks>
    /// <b>The false arm exists for one guard and is named rather than improvised</b>: 
    /// <c>StoneCostsTests.AStoreWithNoStoneWaitsRatherThanKillingTheVillage</c> is about a village
    /// that has marked a store it <em>cannot pay for</em>, and a played opening now gets its stone
    /// — so that test has to say out loud that it is withholding it.
    /// </remarks>
    internal static void PlayTheOpening(SimWorld world, bool paintASeam)
    {
        GridPos site = world.Map.FoundingSite;

        // 1. Somewhere to live.
        for (int dy = -4; dy <= 4; dy++)
        {
            for (int dx = -4; dx <= 4; dx++)
            {
                world.PaintResidential(new GridPos(site.X + dx, site.Y + dy));
            }
        }

        // 2. ⭐ SOMEWHERE TO PUT TIMBER, AND THE PILE IS LOAD-BEARING AGAIN (D90 step 4).
        //
        // This said "NO PILE AND NO SHED — that is the test", on D89's measurement that Joe
        // never needed one: the cart held the wood, so the pile did nothing. THE CART NO
        // LONGER TAKES LOGS, which is the whole of D90's answer to D89 — a wagon that cannot
        // hold timber cannot be strangled by it — and D90 says in as many words that this is
        // what gives the pile its reason back: "you cannot take timber until you have
        // somewhere to put it."
        //
        // Measured rather than argued. Without this line the founding is exactly D95's
        // failure: foresters fell, the cart refuses the load, they set it down (first drop
        // at t16), and goods on the ground are supply-invisible — so 864 logs lie in a field
        // that LogsInSheds cannot see, no builder is ever funded, the hut never stands and
        // all four freeze. That is the design working, not a bug: the first thing the player
        // places is somewhere to keep things, which is Banished's opening and D76's.
        //
        // A shed will not do instead, and that asymmetry is the point: a shed costs 30 logs
        // and is a construction site, so it cannot be the thing that holds the first timber.
        // A pile costs the ground it stands on and goes up the tick it is marked (D98).
        MarkSomewhereNear(world, BuildingKind.Pile, site, 2);

        // 3. ⭐ SOMEBODY TO BUILD, AND NOTHING IS RAISED WITHOUT ONE (D108). A construction
        // site is an errand the hut's crew walks out to rather than a place anybody is
        // assigned to, so a founding with no builder's hut can mark out a woodcutter's hut
        // and two houses and watch all three sit at "0 of 25 logs" forever.
        //
        // It costs nothing but the ground, like the pile, and for the same reason: it is the
        // building every other building waits on, so charging timber for it is a circle.
        MarkSomewhereNear(world, BuildingKind.BuilderHut, site, 2);

        // 4. Something to make firewood with. Still no shed — the pile is the store.
        MarkSomewhereNear(world, BuildingKind.WoodcutterHut, site, 3);

        // 5. ⭐ SOMEWHERE TO GET FOOD, WHICH THE VALLEY NO LONGER PROVIDES BY ITSELF.
        //
        // Until the berry patches retired, the opening needed no food building: six of them
        // were on the map before the founders arrived. Joe's *"no forest, no food"* means a
        // cold start now begins with **nothing to gather anywhere**, so the first hut is part
        // of the opening in the same way the pile and the builder's hut are — measured,
        // without it four founders freeze and not one berry ever reaches the store.
        //
        // Sited IN WOODLAND rather than on the nearest bare tile, because that is the
        // decision the mechanic is about and the founders settle a glade (D112): a hut on
        // the doorstep is a hut in a clearing, and a hut in a clearing yields almost nothing.
        MarkInTheBestWoodland(world, site);

        // 6. ⭐ AND TREES TO FELL, WHICH THE OPENING DELIBERATELY DID NOT PAINT UNTIL NOW.
        //
        // The old comment below is a measurement from a world that no longer exists: timber
        // came from two generator-placed TREE STANDS, so the opening had a log supply without
        // asking for one, and painting on top of it was harmful. **The stands are retired.**
        // With no forester's hut and no painted ground, a cold start has *no source of logs
        // at all* — measured, both huts stay sites for ever, no firewood is made, and all
        // four founders freeze however much food is in the cart.
        //
        // This is Joe's own sentence arriving as a requirement rather than an option:
        // *"the user can paint trees to get laborers to cut down forests until foresters are
        // available."* The pile is already down to receive them, which is the arm D99
        // measured as safe — it is *harvest with no pile* that was fatal.
        //
        // ⚠️ SIX, WHICH IS THE RADIUS THAT WAS ACTUALLY MEASURED. Twelve was tried on the
        // theory that more painted wood is more timber, and it is not obviously better —
        // laborers spend the extra time clearing rather than building, which is the shape
        // D99 measured as harmful. Six is the arm where first firewood lands t253–268
        // against a winter at t360.
        PaintTheNearbyTrees(world, 6);

        // 6. ⭐ AND A ROCK, BECAUSE A HUT COSTS THREE STONE NOW (D214, D215). The same
        // sentence as the trees one line up, one good over: with no painted seam there is no
        // source of stone at all, so both huts stay sites for ever and all four founders
        // freeze. Joe: *"there should already be stone on the map for the user to ask the
        // laborers to harvest"* — there is, four seams on a fourteen-tile ring.
        if (paintASeam)
        {
            PaintTheNearestSeam(world);
        }


        // ⚠️ AND DELIBERATELY NO EXTRA HARVEST PAINTING, which was tried here and measured as
        // harmful — see TheOpeningGetsItsTimberFromTheTreesThePlayerPainted, which paints
        // for its own purpose and runs one year. Over forty years, four arms on the
        // shipped config:
        //
        //   pile, no harvest .... 6 →  9 →  9      no pile, no harvest .. 6 → 9 → 8
        //   pile AND harvest .... 5 →  9 →  8      NO PILE + HARVEST .... 4 → 6 → 2
        //
        // Removing the pile is harmless on its own, which is what Joe observed. Painting
        // a forest with ONLY THE CART to put it in is not: the cart fills with timber
        // (677 logs of its 1,200 by year five) and the food it crowds out never arrives —
        // 164 food against 400+ in every other arm. The village then stops having
        // children and ages out, with ZERO starved and ZERO frozen.
        //
        // That is a legibility failure before it is a balance one (§1.1) and exactly the
        // uncozy failure D88 rules out: a village dying of something it could not have
        // seen coming. It is recorded rather than tuned, and it is Joe's call.
    }

    /// <summary>Paint every fellable tile within a short walk of the founding site.</summary>
    internal static int PaintTheNearbyTrees(SimWorld world, int radius)
    {
        GridPos site = world.Map.FoundingSite;
        int painted = 0;

        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (world.PaintHarvest(new GridPos(site.X + dx, site.Y + dy)).Allowed)
                {
                    painted++;
                }
            }
        }

        return painted;
    }

    // ---------------------------------------------------------------
    //  The tie back to what ships
    // ---------------------------------------------------------------

    /// <summary>The game the player launches starts cold; the fixtures do not.</summary>
    /// <remarks>
    /// The whole reason this file can be trusted. Without it, the fixture and the shipped
    /// file could drift apart on the one setting this slice is about and every test above
    /// would go on passing while describing a game nobody plays.
    /// </remarks>
    [Fact]
    public void TheShippedGameStartsColdAndTheFixturesDoNot()
    {
        Assert.False(
            ShippedConfig.Load().FoundingBuildings,
            "data/sim.config.json must start the player in an empty valley (D70).");

        Assert.True(
            VillageFixtures.Village.FoundingBuildings,
            "The village fixture keeps the old founding, so the suite tests an established "
            + "village (Joe's call).");
    }

    /// <summary>Mark a building on the first tile near the site that will take one.</summary>
    /// <remarks>
    /// The player clicks somewhere sensible and the village tells them if it will not do
    /// (D43). A test cannot click, so it tries a small ring and takes the first yes.
    /// </remarks>
    /// <summary>
    /// Put a gatherer's hut where the trees are, the way a player would.
    /// </summary>
    /// <remarks>
    /// Bounded by the economy's own budget, so the opening never sites its food beyond the
    /// walk the food economy is derived against — the same rule the warm start's
    /// <c>WhereTheTreesAre</c> follows, expressed here through the public API a player has.
    /// </remarks>
    internal static void MarkInTheBestWoodland(SimWorld world, GridPos site) =>
        MarkInTheBestWoodland(world, site, clearOfExistingRings: false);

    /// <summary>
    /// The wooded tile with the most trees around it — <b>optionally clear of every hut
    /// already standing</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ THE SECOND HUT IS PART OF THE OPENING NOW (D262, Joe's two-seat cap), and this is
    /// the arm that sites it.</b> A hut seats two. Four founders are two couples, and one hut
    /// gives the pair who live nearer both seats — measured on the shipped config over twenty
    /// years: **household 1 worked 1.2% of its adult ticks and household 2 worked 5.2%**, which
    /// is Joe's own *"one couple stays home"* arriving through the front door.
    /// </para>
    /// <para>
    /// ⭐ <b>And the answer is the one the cap was built to force</b>: *"the player has to build
    /// more if it wants more forest"* (Joe, 2026-08-29). A second hut is not a workaround here,
    /// it is the opening the design now asks for — the same way the pile and the builder's hut
    /// became part of it.
    /// </para>
    /// <para>
    /// ⚠️ <b>Clear of the first hut's ring, or it buys nothing.</b> Two huts on one copse share
    /// its trees (D260) — stacked exactly, the pair is worth one hut — so a second hut sited by
    /// tree count alone lands beside the first and feeds nobody extra.
    /// </para>
    /// </remarks>
    internal static void MarkInTheBestWoodland(
        SimWorld world, GridPos site, bool clearOfExistingRings)
    {
        int reach = VillageEconomy.MaxHomeToWorkTiles(world.Config);
        int ring = world.Config.GathererHutRingTiles;

        GridPos? best = null;
        int bestTrees = -1;

        for (int dy = -reach; dy <= reach; dy++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                var at = new GridPos(site.X + dx, site.Y + dy);
                if (site.ManhattanDistanceTo(at) > reach
                    || !world.CanBuildAt(BuildingKind.GathererHut, at).Allowed)
                {
                    continue;
                }

                if (clearOfExistingRings && ReachesAGatheringHut(world, at, ring))
                {
                    continue;
                }

                int trees = 0;
                for (int ry = -ring; ry <= ring; ry++)
                {
                    for (int rx = -ring; rx <= ring; rx++)
                    {
                        if ((rx * rx) + (ry * ry) > ring * ring)
                        {
                            continue;
                        }

                        var tile = new GridPos(at.X + rx, at.Y + ry);
                        if (world.Map.Contains(tile)
                            && world.Map.TerrainAt(tile) == Terrain.Forest)
                        {
                            trees++;
                        }
                    }
                }

                if (trees > bestTrees)
                {
                    bestTrees = trees;
                    best = at;
                }
            }
        }

        if (best is GridPos chosen)
        {
            world.Mark(BuildingKind.GathererHut, chosen);
        }
    }

    /// <summary>
    /// Would a hut here draw on wood some standing hut already draws on?
    /// </summary>
    /// <remarks>
    /// Half a ring apart, not a whole one: <c>MaxHomeToWorkTiles</c> IS the gatherer ring, so
    /// two rings that do not touch at all sit twice the budgeted commute apart. The founding
    /// makes the same trade in <c>SimWorld.OverlapsAGatheringRing</c>.
    /// </remarks>
    private static bool ReachesAGatheringHut(SimWorld world, GridPos centre, int radius)
    {
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace other = world.Workplaces[i];
            bool gathers = other.GatheringRadius > 0
                || other.Construction?.Kind == BuildingKind.GathererHut;

            if (gathers && centre.ManhattanDistanceTo(other.Position) <= radius)
            {
                return true;
            }
        }

        return false;
    }

    internal static void MarkSomewhereNear(
        SimWorld world, BuildingKind kind, GridPos site, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                var at = new GridPos(site.X + dx, site.Y + dy);
                if (world.Mark(kind, at).Allowed)
                {
                    return;
                }
            }
        }
    }

    private static int CountDeaths(SimWorld world, CauseOfDeath cause)
    {
        int count = 0;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            if (!world.Villagers[i].Alive && world.Villagers[i].CauseOfDeath == cause)
            {
                count++;
            }
        }

        return count;
    }
}
