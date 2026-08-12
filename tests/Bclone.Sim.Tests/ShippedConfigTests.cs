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

    /// <summary>
    /// The shipped file's numbers, on a village that has already been built.
    /// </summary>
    /// <remarks>
    /// These guards are about the hand-typed file's <em>numbers</em> drifting from the
    /// derived fixture — D48, D49 and D50 are all number bugs. Since D70 the shipped file
    /// starts cold, so running them unmodified would assert that four people with no houses
    /// survive three centuries. <c>ColdStartTests</c> owns the founding, and checks that the
    /// real file still starts cold, so this cannot become a way of never testing what ships.
    /// </remarks>
    private static SimConfig Shipped => ShippedConfig.Established();

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
    /// The buildings must be big enough for the village the economy is budgeted for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Capacity is derived too, and this is the guard that was missing.</b> Every
    /// re-derivation so far has moved the <em>yields</em> — how much a trip brings back,
    /// how big a batch is — and left the buildings at capacities picked when a village
    /// was a dozen people. <c>VillageFixtures.Village</c> derives them, so no test ever
    /// noticed that the shipped file did not: it had three woodcutter seats where the
    /// economy required eight, and three tree-stand seats where it required six.
    /// </para>
    /// <para>
    /// It was latent rather than harmless. The village only holds about thirty people,
    /// so it never reached the twenty households the shortfall is measured against —
    /// but the player can now build granaries and grow past the old ceiling on purpose
    /// (D33, D43), which walks them straight into it. A pressure with no answer
    /// available is one thing; a pressure the player unlocks *by succeeding* is a trap.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The shipped calendar has to leave room for the shipped cold model (D45, D49).
    /// </summary>
    /// <remarks>
    /// <b>This is the property the twenty-five was chosen for.</b> An unheated house must
    /// be able to kill inside one winter, or <c>CauseOfDeath.Cold</c> goes dormant until
    /// clothing ships and D17's whole reversal goes with it. It is asserted against the
    /// shipped file specifically because the relationship spans two keys that have moved
    /// independently before: D49 doubled the season in <c>data/sim.config.json</c> and
    /// nowhere else, and this suite ran a fifteen-day winter for four commits without
    /// noticing.
    /// </remarks>
    [Fact]
    public void AnUnheatedHouseCanStillKillWithinOneWinter()
    {
        SimConfig config = Shipped;

        _output.WriteLine(
            $"winter is {config.DaysPerSeason} days; sheltered danger at " +
            $"{config.ExposureDaysSheltered}, outdoors at {config.ExposureDaysOutdoors}.");

        Assert.True(config.ExposureDaysSheltered < config.DaysPerSeason,
            $"Danger under a fireless roof comes at {config.ExposureDaysSheltered} days and winter " +
            $"is {config.DaysPerSeason}, so a house with no fire in it can never kill anybody. " +
            "Cold has gone dormant, which D45 chose this number to prevent.");

        Assert.True(config.ExposureDaysOutdoors < config.ExposureDaysSheltered,
            "Open ground has to be more dangerous than a roof, or shelter means nothing.");

        // And the rates the two day-counts derive have to stay exact integers — the
        // whole reason the threshold is their product rather than a common multiple.
        Assert.Equal(
            config.ExposureThreshold,
            config.ExposurePerTickOutdoors * config.ExposureTicksOutdoors);
        Assert.Equal(
            config.ExposureThreshold,
            config.ExposurePerTickSheltered * config.ExposureTicksSheltered);
    }

    [Fact]
    public void TheShippedBuildingsAreBigEnoughForTheEconomyTheyServe()
    {
        SimConfig shipped = Shipped;

        int hutSeats = VillageEconomy.RequiredWoodcutterSeats(shipped);
        int standSeats = VillageEconomy.RequiredTreeStandSeats(shipped);

        _output.WriteLine(
            $"hut needs {hutSeats} seats (config {shipped.WoodcutterHutCapacity}); " +
            $"stand needs {standSeats} (config {shipped.TreeStandCapacity}).");

        Assert.True(
            shipped.WoodcutterHutCapacity >= hutSeats,
            $"woodcutter_hut_capacity is {shipped.WoodcutterHutCapacity} but heating " +
            $"{shipped.EconomyHorizonHouseholds} households needs {hutSeats} seats. The village " +
            "would be unable to make more firewood however many hands were free.");

        Assert.True(
            shipped.TreeStandCapacity >= standSeats,
            $"tree_stand_capacity is {shipped.TreeStandCapacity} but keeping the huts in logs " +
            $"and still leaving a hand to build needs {standSeats}.");
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
    /// <remarks>
    /// <b>⚠️ AND IT WAS PASSING ON A DEAD VILLAGE, which is how D132 shipped.</b> It ran the
    /// bare shipped config — <c>founding_buildings: false</c>, nothing marked — so once the
    /// thickets retired the settlement had no food source, died by season four, and never
    /// felled a single log. <c>worst == 0</c> because nothing ever happened. Meanwhile Joe's
    /// real game held 50 logs in one larder against 31 in the whole village's stores. **The
    /// guard for the exact bug he hit was green throughout.** D7 is not a style rule: this is
    /// the failure it names, and the anti-vacuity assert below is the whole repair.
    /// </remarks>
    [Fact]
    public void TheShippedVillageNeverStrandsLogsInALarder()
    {
        SimConfig config = Shipped;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        // A pile and no shed, which is the arrangement that stranded them: a pile takes
        // timber, so the village HAS somewhere to put logs, and the old predicate could not
        // see it. This is also just what the opening looks like before anybody builds a shed.
        ColdStartTests.PlayTheOpeningWithoutAShed(world);

        int worst = 0;
        int everCut = 0;
        for (int year = 1; year <= 300; year++)
        {
            loop.Step(config.TicksPerYear);
            everCut = System.Math.Max(everCut, world.LogsInSheds());

            for (int i = 0; i < world.Households.Count; i++)
            {
                int logs = world.Households[i].Stockpile.Logs;
                if (logs > worst)
                {
                    worst = logs;
                }
            }
        }

        _output.WriteLine(
            $"300 years on the shipped config; most logs ever in a larder: {worst}, "
            + $"most ever in store: {everCut}.");

        // ⭐ ANTI-VACUITY FIRST (D7). A village that never fells a log cannot strand one, and
        // for several commits that is exactly what this was proving.
        Assert.True(
            everCut > 0,
            "The village never got a log into a store in three centuries, so it never had one "
            + "to strand and this guard is watching nothing.");

        Assert.True(worst == 0, $"A household held {worst} logs, which nothing can ever spend.");
    }

    /// <remarks>
    /// <para>
    /// <b>⏸️ SKIPPED, because its premise stopped being true and no assertion can fix that.</b>
    /// It runs the shipped config for three centuries <em>with nobody marking anything</em>,
    /// and the shipped config sets <c>founding_buildings: false</c> — the game deliberately
    /// starts you in an empty valley. That combination used to be survivable because the
    /// generator dropped berry patches and tree stands on the map, so an unattended village
    /// could forage and fell without a single building. **Both are retired.** Measured now:
    /// four laborers, zero food gathered, everybody dead by the fourth season.
    /// </para>
    /// <para>
    /// So this is no longer a guard about three-century stability; it is a guard about whether
    /// an empty valley feeds people, and the answer is deliberately no. Adding
    /// <c>PlayTheOpening</c> would not rescue it either — that is exactly the arm that skipped
    /// six guards in <see cref="ColdStartTests"/>, because an opening marked once and never
    /// revisited does not survive. Restore it against a reacting harness, where "the village
    /// the game loads" can mean a village somebody is actually playing.
    /// </para>
    /// </remarks>
    [Fact(Skip = "The shipped config sets founding_buildings: false and the thickets are "
        + "retired, so an unattended valley now has no food source at all — four laborers, "
        + "dead by season four. This measures the empty valley, not three-century stability. "
        + "Restore with a reacting harness.")]
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
                    household.Home(), StoreKind.Granary, static store => store.Store.Food > 0);

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
        GridPos home = world.Households[0].Home();

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
