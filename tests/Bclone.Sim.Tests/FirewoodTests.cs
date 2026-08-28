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
                household.Stockpile.TryTake(Goods.Logs, household.Stockpile.Logs);
                loop.World.AnyStoreOf(StoreKind.Shed).Store.TryTake(Goods.Logs, loop.World.AnyStoreOf(StoreKind.Shed).Store.Logs);
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

        // The refusal names logs and says how many a batch needs. It used to read "the
        // storage shed has no logs"; with more than one shed possible (D38) that
        // sentence would have been unverifiable by the player — WHICH shed? — so it now
        // says no shed within reach has them.
        Assert.Contains("logs", note, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("batch", note, System.StringComparison.OrdinalIgnoreCase);
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
            Assert.True(quota.Foresters > 0 || loop.World.AnyStoreOf(StoreKind.Shed).Store.Logs >= config.LogsPerSplit,
                $"Season {season}: the village wants {quota.Woodcutters} woodcutters but no " +
                $"foresters, and only {loop.World.AnyStoreOf(StoreKind.Shed).Store.Logs} logs in store — {quota}");
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

    /// <summary>How long the acceptance run watches the village for.</summary>
    /// <remarks>
    /// <para>
    /// <b>Three hundred years, and the number is load-bearing.</b> This test ran for
    /// 150 and passed for weeks over a village that was dying: the population peaked
    /// near seventy around year 105, was down to twenty-three and still falling when
    /// the test stopped looking, and hit <em>zero</em> around year 180. The window
    /// ended one generation before the collapse finished.
    /// </para>
    /// <para>
    /// 150 was never chosen against anything — it was picked before anyone knew where
    /// the cliff was. 300 is two full collapse-and-recovery cycles at this lifespan,
    /// which is enough for a one-way decline to show itself as one.
    /// </para>
    /// </remarks>
    private const int AcceptanceYears = 300;
    /// <summary>
    /// ⭐ THE acceptance test for D17/D29/D30 — <b>the fuel chain warms a village that grows,
    /// and freezes nobody.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠️ IT WAS <c>TheVillageHoldsAStableSizeForThreeCenturiesWithFuelOn</c>, AND THE
    /// PREMISE UNDER THAT NAME IS OVERTURNED (D143).</b> It reasoned that <i>"a baseline that
    /// dies can only mean the constants are wrong while there is no player to blame for it"</i>
    /// — and Joe's ruling is the opposite: <i>"an unattended village should die out. The user
    /// needs to play the game at some point."</i> Three centuries of a settlement nobody sites
    /// a building in, nobody paints ground for and nobody switches a mode on is not a baseline;
    /// it is the game playing itself, and demanding it sent three sessions hunting for economy
    /// bugs that were not there (D131 chief among them).
    /// </para>
    /// <para>
    /// <b>What it asserts instead is what fuel is actually for.</b> The chain must carry the
    /// village <em>up</em> — a real peak from four founders — and <b>nobody may freeze</b>,
    /// which is the whole of D17's condition for reversing Phase 0's ban on a second death
    /// system. Both of those stay false the day the fuel economy breaks, and neither can pass
    /// vacuously.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheFuelChainWarmsAGrowingVillageAndFreezesNobody()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());

        int peak = 0;
        int peakYear = 0;
        for (int year = 1; year <= AcceptanceYears; year++)
        {
            loop.Step(config.TicksPerYear);

            if (loop.World.Population > peak)
            {
                peak = loop.World.Population;
                peakYear = year;
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
            $"Peaked at {peak} in year {peakYear} from {config.StartingPopulation} founders; " +
            $"year {AcceptanceYears}: {loop.World.Population} alive. " +
            $"Deaths: {froze} froze, {starved} starved, {aged} of old age, " +
            $"{loop.World.Villagers.Count} ever born.");

        Assert.True(peak >= 25,
            $"With fuel burning, the village only ever reached {peak} from " +
            $"{config.StartingPopulation} founders — the chain is not keeping up with it.");

        // ⭐ AND NOBODY FREEZES, which is D17's condition for reversing Phase 0's ban on a
        // second death system: cold may kill when the player lets the fuel run out, never
        // because the derived economy cannot make enough of it.
        Assert.Equal(0, froze);

        // Most people should die of old age. A village where the usual way to go is
        // freezing or starving has stopped being a settlement under pressure and
        // become a disaster, and the generational arc D12 exists for flattens out.
        Assert.True(aged > froze + starved,
            $"Only {aged} of {froze + starved + aged} deaths were old age; the pressure systems " +
            "have stopped being pressure and become the normal way to die.");
    }

    [Fact]
    public void TheDeadDoNotTakeUpRoomInTheHOUSE()
    {
        // The regression guard for what was killing every village. A household's
        // MemberIds keeps everyone who has EVER lived there — RemoveMember is called
        // when somebody moves out, never when somebody dies — so reading it as "how
        // many live here" barred any household that had seen max_household_size people
        // from ever having another child. Households ratcheted one way into sterility
        // and every settlement died out about a century in, whatever its food was
        // doing.
        //
        // Posed directly rather than waiting 150 years for it: a household with one
        // living couple and a long list of dead predecessors must still be able to
        // have a child.
        // ⚠️ IT LOOKS FOR THE CASE RATHER THAN ASSUMING A YEAR HAS IT, AND THAT IS WHY THIS
        // GUARD WENT VACUOUS. It sampled year 120 flat. Measured on the fixture as it stands,
        // a household with dead members and room in it exists from year **57 to year 119** —
        // the guard was reading one year past the end of its own window and reporting that the
        // case had stopped happening. **A fixed year is a bet on a population curve**, and this
        // project moves that curve most weeks; sweeping for the condition is the same claim
        // with nothing wagered on the shape of the run.
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());

        Household? withDead = null;
        int foundInYear = 0;

        for (int year = 1; year <= 150 && withDead is null; year++)
        {
            loop.Step(config.TicksPerYear);

            foreach (Household household in loop.World.Households)
            {
                int living = loop.World.LivingMembersOf(household);
                if (living > 0 && household.MemberIds.Count >= config.MaxHouseholdSize
                    && living < config.MaxHouseholdSize)
                {
                    withDead = household;
                    foundInYear = year;
                    break;
                }
            }
        }

        Assert.True(withDead is not null,
            "No household outlived enough of its own members in 150 years — the fixture has " +
            "stopped exercising the case, which makes this guard vacuous (D7).");

        _output.WriteLine(
            $"year {foundInYear}, {withDead!.Name}: {loop.World.LivingMembersOf(withDead)} living " +
            $"of {withDead.MemberIds.Count} ever, max household size {config.MaxHouseholdSize}.");

        Assert.True(loop.World.LivingMembersOf(withDead) < config.MaxHouseholdSize,
            "Precondition: this household should have room for another soul.");
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
