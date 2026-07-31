using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The village the game actually loads, run rather than merely checked.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every acceptance test in this suite uses <c>VillageFixtures.Village</c>, and the
/// game uses <c>data/sim.config.json</c>.</b> Two files that must agree, which is the
/// shape of half the bugs in this project's history. There were guards that the shipped
/// file meets the economy's stated <em>targets</em> — but nothing that ever ran a
/// village on it, so the config could be arithmetically sound and still produce a
/// settlement that starves.
/// </para>
/// <para>
/// Added after Joe watched the real game and asked "people seem to not be able to find
/// anything to eat. is that expected?" — a question no test in the suite could have
/// raised, because no test was playing the same game he was.
/// </para>
/// </remarks>
public sealed class ShippedConfigTests
{
    private readonly ITestOutputHelper _output;

    public ShippedConfigTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Shipped =>
        SimConfigLoader.LoadFromFile(System.IO.Path.Combine(RepoRoot(), "data", "sim.config.json"));

    private static string RepoRoot()
    {
        var directory = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(directory.FullName, "bclone.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new System.InvalidOperationException("Could not find the repo root.");
    }

    /// <summary>
    /// The larder-logs invariant, asserted against the file the game actually loads.
    /// </summary>
    /// <remarks>
    /// <see cref="LogsNeverRestInLardersTests"/> proves this for
    /// <c>VillageFixtures.Village</c>. It is repeated here on purpose: the two configs
    /// can and do diverge, and the leak this guards was found in the <em>shipped</em>
    /// one — 240 logs frozen in two houses — while the fixture village was only losing
    /// 90 and surviving it. A guard that only watches the fixture would have called
    /// this fixed.
    /// </remarks>
    [Fact]
    public void TheShippedVillageNeverStrandsLogsInALarder()
    {
        SimConfig config = Shipped;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        int worst = 0;
        for (int year = 1; year <= 300; year++)
        {
            loop.Step(config.TicksPerYear);

            for (int i = 0; i < world.Households.Count; i++)
            {
                int logs = world.Households[i].Stockpile.Logs;
                if (logs > worst)
                {
                    worst = logs;
                }
            }
        }

        _output.WriteLine($"300 years on the shipped config; most logs ever in a larder: {worst}.");
        Assert.True(worst == 0, $"A household held {worst} logs, which nothing can ever spend.");
    }

    [Fact]
    public void TheVillageTheGameLoadsHoldsForThreeCenturies()
    {
        SimConfig config = Shipped;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());

        int lowest = int.MaxValue;
        int highest = 0;
        for (int year = 1; year <= 300; year++)
        {
            loop.Step(config.TicksPerYear);
            if (year >= 40)
            {
                lowest = System.Math.Min(lowest, loop.World.Population);
                highest = System.Math.Max(highest, loop.World.Population);
            }
        }

        int starved = 0, froze = 0, aged = 0;
        foreach (Villager villager in loop.World.Villagers)
        {
            switch (villager.CauseOfDeath)
            {
                case CauseOfDeath.Starvation: starved++; break;
                case CauseOfDeath.Cold: froze++; break;
                case CauseOfDeath.OldAge: aged++; break;
            }
        }

        _output.WriteLine(
            $"Year 300: {loop.World.Population} alive; between {lowest} and {highest} after year 40. " +
            $"{starved} starved, {froze} froze, {aged} of old age.");

        Assert.True(loop.World.Population >= config.StartingPopulation,
            $"The shipped village finished at {loop.World.Population}.");
        Assert.True(lowest >= config.StartingPopulation,
            $"The shipped village dropped to {lowest}.");
        Assert.True(aged > starved + froze,
            $"Only {aged} of {starved + froze + aged} deaths were old age in the shipped village.");
    }

    [Fact]
    public void NobodyStarvesBesideAFullStore()
    {
        // The failure Joe actually saw, stated as an invariant rather than a curve.
        // A household at zero food while a store it can reach is holding plenty means
        // fetching is broken — which it was: a home nearer the market than the granary
        // walked over for food and came home with firewood, because CollectFromStore
        // branched on the building's KIND rather than on what was wanted and what was
        // there.
        SimConfig config = Shipped;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());

        int worstStreak = 0;
        int streak = 0;

        for (int i = 0; i < config.TicksPerYear * 80; i++)
        {
            loop.StepOnce();
            SimWorld world = loop.World;

            bool anyoneStrandedNow = false;
            foreach (Household household in world.Households)
            {
                if (world.LivingMembersOf(household) == 0 || household.Stockpile.Food > 0)
                {
                    continue;
                }

                // Empty larder. Is there food they could have walked to?
                StoreBuilding? source = world.NearestStore(
                    household.HomePosition, StoreKind.Granary, static store => store.Store.Food > 0);

                if (source is not null)
                {
                    anyoneStrandedNow = true;
                }
            }

            streak = anyoneStrandedNow ? streak + 1 : 0;
            worstStreak = System.Math.Max(worstStreak, streak);
        }

        _output.WriteLine(
            $"Longest run of ticks with an empty larder and a reachable full granary: {worstStreak}.");

        // A short streak is honest — somebody has to walk there, and the round trip is
        // real (D30). A long one means nobody is going.
        int aRoundTrip = VillageEconomy.RoundTripTicks(config) * 4;
        Assert.True(worstStreak < aRoundTrip,
            $"A household sat on an empty larder for {worstStreak} ticks with a full granary " +
            $"within reach — four round trips is {aRoundTrip}. Fetching is not working.");
    }

    [Fact]
    public void TheShippedVillageSurvivesBeingAskedToBuild()
    {
        // Placement is the newest way to break the village: building competes for hands
        // and eats the logs the woodcutter needs. Marking one of everything is a
        // reasonable thing for a player to do in their first hour, so it should not be
        // fatal — and if it ever is, that should fail here rather than in Joe's game.
        SimConfig config = Shipped;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        loop.Step(config.TicksPerYear * 15);

        SimWorld world = loop.World;
        GridPos home = world.Households[0].HomePosition;

        int marked = 0;
        foreach (BuildingKind kind in new[]
                 {
                     BuildingKind.Granary, BuildingKind.Shed,
                     BuildingKind.Market, BuildingKind.WoodcutterHut,
                 })
        {
            for (int radius = 2; radius < 10 && marked < 4; radius++)
            {
                bool placed = false;
                for (int dy = -radius; dy <= radius && !placed; dy++)
                {
                    for (int dx = -radius; dx <= radius && !placed; dx++)
                    {
                        var spot = new GridPos(home.X + dx, home.Y + dy);
                        if (world.CanBuildAt(kind, spot).Allowed && world.Mark(kind, spot).Allowed)
                        {
                            marked++;
                            placed = true;
                        }
                    }
                }

                if (placed)
                {
                    break;
                }
            }
        }

        _output.WriteLine($"{marked} buildings marked out in one go.");
        Assert.Equal(4, marked);

        for (int year = 1; year <= 100; year++)
        {
            loop.Step(config.TicksPerYear);
        }

        _output.WriteLine(
            $"A century later: {world.Population} alive, " +
            $"{CountStores(world, StoreKind.Granary)} granaries, {CountStores(world, StoreKind.Shed)} sheds.");

        Assert.True(world.Population >= config.StartingPopulation,
            $"Marking four buildings killed the village — it finished at {world.Population}.");
    }

    private static int CountStores(SimWorld world, StoreKind kind)
    {
        int count = 0;
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            if (store.Kind == kind)
            {
                count++;
            }
        }

        return count;
    }
}
