using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The first secondary processing chain: logs in, firewood out (D29).
/// </summary>
/// <remarks>
/// The fuel itself is not burned yet — that is a later slice. What is under test here
/// is the <em>chain</em>, because it is the shape every processing chain after this one
/// will use: a workplace that consumes an input, a quota that propagates demand back
/// down to the workplace feeding it, and a worker who can be idle for a reason no other
/// worker can be idle for.
/// </remarks>
public sealed class FirewoodTests
{
    private readonly ITestOutputHelper _output;

    public FirewoodTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Build(SimConfig config, ulong? seed = null) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed);

    [Fact]
    public void TheVillageActuallyMakesFirewood()
    {
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerYear * 20);

        int firewood = loop.World.LifetimeFirewoodCut();

        _output.WriteLine($"{firewood} firewood cut in twenty years");
        Assert.True(firewood > 0, "Nobody ever split a log.");
    }

    [Fact]
    public void FirewoodIsMadeOutOfLogsAndNotOutOfNothing()
    {
        // The whole point of a processing chain: the output costs the input. A hut
        // that produced firewood from thin air would pass the test above.
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerYear * 20);

        int logsFelled = loop.World.LifetimeLogsFelled();
        int firewoodMade = loop.World.LifetimeFirewoodCut();

        int logsSpentOnFirewood = firewoodMade / Config.FirewoodPerSplit * Config.LogsPerSplit;
        _output.WriteLine(
            $"{logsFelled} logs felled, {firewoodMade} firewood made " +
            $"(≈{logsSpentOnFirewood} logs consumed)");

        Assert.True(firewoodMade > 0);
        Assert.True(logsFelled >= logsSpentOnFirewood,
            "More firewood was made than there were logs to make it from.");
    }

    [Fact]
    public void AWoodcutterWithNoLogsSaysSo()
    {
        // The hut is the first workplace that can be idle for want of an INPUT rather
        // than a worker or a season. A manned building doing nothing, with nothing
        // said about it, is exactly the opaque simulation §2.2 warns against.
        SimLoop loop = Build(Config);

        string? note = null;
        for (int i = 0; i < Config.TicksPerYear * 30 && note is null; i++)
        {
            loop.StepOnce();

            // Empty the yard from under them.
            foreach (Household household in loop.World.Households)
            {
                household.Stockpile.TryTakeLogs(household.Stockpile.Logs);
                loop.World.StorageShed.Store.TryTakeLogs(loop.World.StorageShed.Store.Logs);
            }

            foreach (Villager villager in loop.World.Villagers)
            {
                if (villager.Alive
                    && loop.World.FindWorkplace(villager.WorkplaceId)?.Kind == JobKind.Woodcutter
                    && villager.WorkNote.Length > 0)
                {
                    note = $"{villager.Name}: {villager.WorkNote}";
                }
            }
        }

        _output.WriteLine(note ?? "(no woodcutter ever explained an idle day)");
        Assert.NotNull(note);
        Assert.Contains("no logs", note, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheChainDoesNotStarveInTheMiddle()
    {
        // The failure mode processing introduces, and the reason demand propagates
        // backwards: a village that staffs only the stand ends up freezing in a yard
        // full of logs, and one that staffs only the hut runs the yard dry and stops.
        // Whenever the village wants firewood, it must also want the logs to make it.
        SimConfig config = Config;
        SimLoop loop = Build(config);

        // Sampled per season, not per year. Nothing burns firewood yet, so once the
        // village has enough stacked it correctly stops wanting more — and a yearly
        // sample lands almost entirely in that quiet period.
        int checkedSeasons = 0;
        for (int season = 1; season <= 60 * 4; season++)
        {
            loop.Step(config.TicksPerSeason);

            LabourQuota quota = LabourQuota.For(loop.World);
            if (quota.Woodcutters <= 0)
            {
                continue;
            }

            checkedSeasons++;
            Assert.True(quota.Loggers > 0 || loop.World.StorageShed.Store.Logs >= config.LogsPerSplit,
                $"Season {season}: the village wants {quota.Woodcutters} woodcutters but no " +
                $"loggers, and only {loop.World.StorageShed.Store.Logs} logs in store — {quota}");
        }

        _output.WriteLine($"{checkedSeasons} seasons where the village wanted firewood made");
        Assert.True(checkedSeasons > 0, "The village never wanted firewood at all.");
    }

    [Fact]
    public void HeatingTheVillageCostsFewerHandsThanFeedingItLeavesSpare()
    {
        // The stated fuel target, asserted rather than hoped for. Phase 0 rejected
        // warmth outright as "a second overlapping death system", and the honest
        // reading of that fear is this arithmetic: if heating the village costs more
        // hands than it has spare after feeding itself, cold is not a pressure, it is
        // a slow extinction with extra steps.
        SimConfig config = Config;

        int households = config.EconomyHorizonHouseholds;
        int handsForFuel = VillageEconomy.HandsNeededForFuel(config, households);
        int spare = VillageEconomy.SpareHandsAt(config, households);
        int budget = VillageEconomy.FuelBudgetInHands(config, households);

        _output.WriteLine(
            $"{households} households: {spare} hands spare after feeding everyone, " +
            $"{budget} budgeted for fuel, {handsForFuel} actually needed " +
            $"({VillageEconomy.FirewoodPerHouseholdPerWinter(config)} firewood per home per winter, " +
            $"{VillageEconomy.FirewoodMadePerYearAtWorst(config)} made per woodcutter-year, " +
            $"{VillageEconomy.WoodCutPerYearAtWorst(config)} logs per logger-year, " +
            $"batch of {config.FirewoodPerSplit})");

        Assert.True(handsForFuel <= budget,
            $"Heating {households} homes takes {handsForFuel} hands against a budget of {budget} " +
            $"(half the {spare} the village can spare). A village that is warm by construction " +
            $"and has nothing left over is the D16 failure wearing a coat.");
    }

    [Fact]
    public void TheShippedConfigFileMeetsTheFuelTarget()
    {
        // The fixtures deriving correctly is not much use if the file the game
        // actually loads has drifted. This is the one that guards the real village —
        // the same guard the food economy already has.
        string path = System.IO.Path.Combine(RepoRoot(), "data", "sim.config.json");
        SimConfig shipped = SimConfigLoader.LoadFromFile(path);

        int households = shipped.EconomyHorizonHouseholds;
        int needed = VillageEconomy.HandsNeededForFuel(shipped, households);
        int budget = VillageEconomy.FuelBudgetInHands(shipped, households);

        _output.WriteLine(
            $"shipped: batch of {shipped.FirewoodPerSplit} => {needed} hands for fuel, " +
            $"budget {budget} (required batch is {VillageEconomy.RequiredFirewoodPerSplit(shipped)})");

        Assert.True(needed <= budget,
            $"data/sim.config.json heats {households} homes with {needed} hands against a budget " +
            $"of {budget}; firewood_per_split should be at least " +
            $"{VillageEconomy.RequiredFirewoodPerSplit(shipped)}.");
    }

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

    [Fact]
    public void TheVillageHoldsAStableSizeForACenturyAndAHalfWithFuelOn()
    {
        // THE acceptance test for D17/D29, and D31 is what it is measuring: a stable
        // size, not growth. Failure stays possible — people do starve here — but a
        // baseline that dies can only mean the constants are wrong while there is no
        // player to blame for it.
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());

        int lowestAfterSettling = int.MaxValue;
        for (int year = 1; year <= 150; year++)
        {
            loop.Step(config.TicksPerYear);

            // Ignore the founding decades: four people growing into a village is not
            // yet the steady state this is about.
            if (year >= 40)
            {
                lowestAfterSettling = System.Math.Min(lowestAfterSettling, loop.World.Population);
            }
        }

        int froze = 0, starved = 0, aged = 0;
        foreach (Villager villager in loop.World.Villagers)
        {
            switch (villager.CauseOfDeath)
            {
                case CauseOfDeath.Cold: froze++; break;
                case CauseOfDeath.Starvation: starved++; break;
                case CauseOfDeath.OldAge: aged++; break;
            }
        }

        _output.WriteLine(
            $"Year 150: {loop.World.Population} alive, never below {lowestAfterSettling} after year 40. " +
            $"Deaths: {froze} froze, {starved} starved, {aged} of old age, " +
            $"{loop.World.Villagers.Count} ever born.");

        Assert.True(lowestAfterSettling >= config.StartingPopulation,
            $"The village dropped to {lowestAfterSettling} — below the four it was founded with.");

        // Most people should die of old age. A village where the usual way to go is
        // freezing or starving has stopped being a settlement under pressure and
        // become a disaster, and the generational arc D12 exists for flattens out.
        Assert.True(aged > froze + starved,
            $"Only {aged} of {froze + starved + aged} deaths were old age; the pressure systems " +
            "have stopped being pressure and become the normal way to die.");
    }

    [Fact]
    public void FirewoodProductionIsDeterministic()
    {
        SimConfig config = Config;
        SimLoop a = Build(config);
        SimLoop b = Build(config);

        a.Step(config.TicksPerYear * 40);
        b.Step(config.TicksPerYear * 40);

        Assert.Equal(a.World.LifetimeFirewoodCut(), b.World.LifetimeFirewoodCut());

        for (int i = 0; i < a.World.Villagers.Count; i++)
        {
            Assert.Equal(a.World.Villagers[i].WorkNote, b.World.Villagers[i].WorkNote);
        }
    }
}
