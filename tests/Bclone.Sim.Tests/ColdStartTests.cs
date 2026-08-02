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
