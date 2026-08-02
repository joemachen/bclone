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
        CartLogs = ShippedConfig.Load().CartLogs,
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

        // The valley's own features remain — a berry patch is not a building.
        Assert.Contains(world.Workplaces, place => place.Kind == JobKind.Forager);
        Assert.DoesNotContain(world.Workplaces, place => place.Kind == JobKind.Woodcutter);

        // And nowhere is painted: where to live is the player's first decision (D64).
        Assert.Equal(0, world.Zones.ResidentialTiles);

        _output.WriteLine(
            $"{world.Population} founders, {world.Households.Count} households, "
            + $"{world.StoreBuildings.Count} building, {world.TheCart!.Store.Food} food in it");
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
    /// <c>LoggersWanted</c>, so the village wanted nobody at the tree stand; and building
    /// timber was drawn only from sheds, of which a cold start has none, so even felled logs
    /// could not become a house.
    /// </para>
    /// <para>
    /// The player's part is scripted here as the two things they can actually do on day one:
    /// paint somewhere to live, and mark a woodcutter's hut. Everything after that is the
    /// village's own machinery.
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

        // And a shed to keep timber in, and the hut that turns it into firewood — without
        // which a house is a roof with no fire under it, and D45 kills at day 25 of 30.
        MarkSomewhereNear(world, BuildingKind.Shed, site, 2);
        MarkSomewhereNear(world, BuildingKind.WoodcutterHut, site, 3);

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

        GridPos site = world.Map.FoundingSite;

        // 1. Somewhere to put things. It costs nothing, which is the point.
        MarkSomewhereNear(world, BuildingKind.Pile, site, 2);

        // 2. Somewhere to live.
        for (int dy = -4; dy <= 4; dy++)
        {
            for (int dx = -4; dx <= 4; dx++)
            {
                world.PaintResidential(new GridPos(site.X + dx, site.Y + dy));
            }
        }

        // 3. Something to make firewood with. AND NO SHED — that is the test.
        MarkSomewhereNear(world, BuildingKind.WoodcutterHut, site, 3);

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

        GridPos site = world.Map.FoundingSite;
        MarkSomewhereNear(world, BuildingKind.Pile, site, 2);

        for (int dy = -4; dy <= 4; dy++)
        {
            for (int dx = -4; dx <= 4; dx++)
            {
                world.PaintResidential(new GridPos(site.X + dx, site.Y + dy));
            }
        }

        MarkSomewhereNear(world, BuildingKind.WoodcutterHut, site, 3);

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
    /// about a third of their ticks where a forager works a twentieth. That is a live
    /// design question (D81) and not something a guard should freeze.
    /// </para>
    /// </remarks>
    [Fact]
    public void NeitherFoundingHouseholdRestsWhileTheOtherWorks()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        PlayTheOpening(world);

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

        Assert.True(
            world.Population > 0,
            "A played opening died out inside forty years without anybody starving or "
            + "freezing — it simply never had a child.");
    }

    private static bool IsWorking(VillagerState state) =>
        state is VillagerState.Gathering or VillagerState.Cutting
            or VillagerState.MakingFirewood or VillagerState.Building
            or VillagerState.CollectingForMarket or VillagerState.DeliveringToHome;

    private static double Share(long part, long whole) => whole == 0 ? 0 : 100.0 * part / whole;

    /// <summary>Joe's opening: somewhere to put things, somewhere to live, a hut. No shed.</summary>
    private static void PlayTheOpening(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        MarkSomewhereNear(world, BuildingKind.Pile, site, 2);

        for (int dy = -4; dy <= 4; dy++)
        {
            for (int dx = -4; dx <= 4; dx++)
            {
                world.PaintResidential(new GridPos(site.X + dx, site.Y + dy));
            }
        }

        MarkSomewhereNear(world, BuildingKind.WoodcutterHut, site, 3);
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
    private static void MarkSomewhereNear(
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
