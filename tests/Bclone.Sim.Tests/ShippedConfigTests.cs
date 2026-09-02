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

        // ⭐ AND THE THIRD RATE, WHICH IS NEW AND IS THE ONE THAT CAN QUIETLY NOT DIVIDE
        // (D192). The threshold is the product of the OTHER TWO tick-counts, so exactness is
        // guaranteed for them and merely true for this one: `thaw_days_at_a_fire` is a free
        // number, and a value that does not divide the threshold would round a stated five
        // days into five-and-a-bit with nothing on screen to say so.
        _output.WriteLine(
            $"a fire brings somebody back from the brink in {config.ThawDaysAtAFire} days: "
            + $"{config.ThawPerTickAtAFire} a tick against {config.ExposurePerTickOutdoors} "
            + $"outdoors and {config.ExposurePerTickSheltered} under a fireless roof");

        Assert.Equal(
            config.ExposureThreshold,
            config.ThawPerTickAtAFire * config.ThawTicksAtAFire);

        // ⛔ AND IT MUST OUTPACE THE COLD IT UNDOES, or a hearth is not safety — a villager
        // who thaws slower than the roof over them chills is somebody the fire cannot save.
        Assert.True(
            config.ThawPerTickAtAFire > config.ExposurePerTickOutdoors,
            $"A fire gives back {config.ThawPerTickAtAFire} a tick against "
            + $"{config.ExposurePerTickOutdoors} lost outdoors — getting warm is no faster than "
            + "getting cold, which is the fifteen-day thaw Joe asked to be rid of.");
    }

    [Fact]
    public void TheShippedBuildingsAreBigEnoughForTheEconomyTheyServe()
    {
        SimConfig shipped = Shipped;

        int woodcutterSeats = VillageEconomy.RequiredWoodcutterSeats(shipped);
        int foresterSeats = VillageEconomy.RequiredForesterSeats(shipped);

        _output.WriteLine(
            $"woodcutter's hut needs {woodcutterSeats} seats " +
            $"(config {shipped.WoodcutterHutCapacity}); forester's hut needs " +
            $"{foresterSeats} (config {shipped.ForesterHutCapacity}).");

        // ⛔⛔ THE CLAIM MOVED ON 2026-09-01, AND D50 IS THE REASON IT COULD.
        //
        // This asserted `WoodcutterHutCapacity >= woodcutterSeats` — **one hut must be big enough
        // for the whole horizon village** — and it fired the moment Joe doubled the firewood burn:
        // heating 20 households needs 3 seats and the hut holds 2. The message it carried is the
        // run where *"the village physically could not make more firewood however many hands were
        // free, and thirty-six people froze."*
        //
        // ⭐⭐ **WHAT CHANGED IS THE RECOURSE, NOT THE ARITHMETIC.** D50's village had none: one
        // hut, a fixed capacity, and no way out. Joe's ruling (2026-09-01): *"I want 2 seats at a
        // woodcutter. Players have to build another building if they want more woodcutters. Or
        // they can upgrade the buildings later."* A hut is a **cap** now (D256, D262, D267), and
        // the answer to needing more is another hut.
        //
        // ⚠️ **SO THE PROTECTION BECOMES LEGIBILITY, AND IT IS A STRICTLY HARDER THING TO GET
        // RIGHT.** *"Build another one"* is only an answer if the player is told they need to —
        // otherwise a capped hut is D50 wearing a design's clothes, which is precisely what
        // competing rings (D260) had to fix before the forager's two seats meant anything.
        // `LabourQuota.Needed` keeps the honest figure beside the capped one so the professions
        // panel can say it.
        Assert.True(
            woodcutterSeats > shipped.WoodcutterHutCapacity,
            $"Heating {shipped.EconomyHorizonHouseholds} households needs {woodcutterSeats} seats "
            + $"and one hut holds {shipped.WoodcutterHutCapacity}, so this guard is checking a "
            + "shortfall that no longer occurs — which is good news, and means the claim below "
            + "has stopped being exercised. Re-point it or retire it.");

        // ⛔⛔ THE FORESTER'S ARM COMPARED A KEY NOTHING READS, AND SAID SO CONVINCINGLY.
        // It asserted `forester_hut_capacity >= foresterSeats` — but the forester's hut is the
        // one building whose row still leaves `Seats` null, so `SimWorld.SeatsIn` hands it
        // `RequiredForesterSeats` and **the config key seats nobody**. The check could only ever
        // fail by someone editing a dead number, and it duly failed the moment the firewood burn
        // doubled (2026-09-01) while the hut was already seating the 3 it needed.
        //
        // ⭐ So the claim becomes the one that is actually load-bearing: **the hut must still be
        // deriving**, because the instant somebody states its seats it inherits `woodcutter_hut_
        // capacity`'s whole history (D16, D50 — yields moved, capacities did not, thirty-six
        // people froze). *A guard comparing a number the game does not use is worse than no
        // guard: it reads as protection.*
        Assert.Null(
            shipped.BuildingRows.Single(row => row.Id == (int)BuildingKind.ForesterHut).Seats);

        // ⭐⭐ AND THE VILLAGE SAYS SO OUT LOUD — the half that makes "build another hut" an
        // answer rather than a silent shortage. Posed on a horizon-sized village so the demand
        // is real rather than a founding's.
        SimLoop loop = SimFactory.CreatePhase0(shipped, new InMemoryLogSink());
        loop.Step(shipped.TicksPerYear * 40);

        LabourQuota quota = LabourQuota.For(loop.World);
        int seats = LabourQuota.TotalCapacityFor(loop.World, JobKind.Woodcutter);

        _output.WriteLine(
            $"after forty years: {loop.World.Population} alive, woodcutter seats {seats}, "
            + $"village wants {quota.For(JobKind.Woodcutter)}, needs {quota.Needed(JobKind.Woodcutter)}");

        Assert.True(
            quota.Needed(JobKind.Woodcutter) >= quota.For(JobKind.Woodcutter),
            "The honest need is below the capped figure, so `Needed` is not the uncapped one.");

        _output.WriteLine(
            $"forester's hut states no seats and is handed {foresterSeats} by the economy; "
            + $"forester_hut_capacity ({shipped.ForesterHutCapacity}) seats nobody.");
    }

    /// <summary>
    /// ⭐ The shipped crop is worth what the derivation says it has to be worth.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>`crops-and-orchards.md §1`'s target, checked against the file the game loads.</b>
    /// <c>crop_yield_per_tile</c> is typed in <c>data/sim.config.json</c> and
    /// <see cref="VillageEconomy.RequiredCropYield"/> says what it must be — the same
    /// arrangement <c>gather_yield</c> has, and this guard is the reason that arrangement is
    /// safe rather than merely tidy.
    /// </para>
    /// <para>
    /// <b>METHODOLOGY §3, and the six recorded times these two configs diverged</b> — D48 (a
    /// timber leak four times worse in the shipped file), D50 (three woodcutter seats where the
    /// economy needed eight), D49 (thirty-day seasons that reached the game and not the tests).
    /// The fixture derives this number; the shipped file states it, and a stated number is
    /// exactly the kind that stops tracking the derivation it came from.
    /// </para>
    /// <para>
    /// <b>Equality, not a floor</b>, unlike the capacities above. A farm worth materially more
    /// than a gatherer's hut deletes gathering as a choice, and one worth materially less is a
    /// building nobody rationally places — the target is parity, so both directions are wrong
    /// and both should fail here.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheShippedCropIsWorthAGatherersYear()
    {
        SimConfig shipped = Shipped;

        int required = VillageEconomy.RequiredCropYield(shipped);
        int tiles = VillageEconomy.FieldTilesOneFarmerKeeps(shipped);

        _output.WriteLine(
            $"a farmer keeps {tiles} tiles; crop_yield_per_tile is {shipped.CropYieldPerTile} " +
            $"and the derivation asks for {required}. One farmer's year: " +
            $"{VillageEconomy.FoodFarmedPerYearAtWorst(shipped)} food at their weakest, against " +
            $"a gatherer's {VillageEconomy.FoodGatheredPerYearAtWorst(shipped)}.");

        Assert.Equal(required, shipped.CropYieldPerTile);

        Assert.True(
            tiles > 0,
            "A farmer cannot work a single tile in a season, so a farm can never do anything.");

        // And the seats have to be able to hold the hands the ground needs, or the farm is a
        // building that is overstretched the day it is raised — D50's shape.
        Assert.True(
            VillageEconomy.RequiredFarmerSeats(shipped) >= 1,
            "A farmhouse with no seats is not a building.");
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
    /// an empty valley feeds people, and the answer is deliberately <b>no</b> — that is D143,
    /// Joe's ruling, in as many words: <i>"an unattended village should die out. The user needs
    /// to play the game at some point."</i>
    /// </para>
    /// <para>
    /// <b>⚠️ CORRECTED BY D159.</b> This used to go on to say that adding <c>PlayTheOpening</c>
    /// *"would not rescue it either — an opening marked once and never revisited does not
    /// survive"*. **That was the misattribution D157 overturned**: a scripted opening that never
    /// reacts grows a village to 21 people; what killed those runs was a footprint the village
    /// would never clear. So the reason to restore this against a *played* opening is real and
    /// the excuse for not doing it was not. It is a straightforward rewrite — give it
    /// <c>PlayTheOpening</c> and assert the peak and the causes of death, the way D143 re-based
    /// the other six long-horizon guards.
    /// </para>
    /// </remarks>
    [Fact(Skip = "D143: an unattended village is supposed to die out, and this runs 300 years "
        + "with nobody marking anything on a config that starts with no buildings — so it "
        + "measures the empty valley, not three-century stability. Restore by giving it "
        + "PlayTheOpening and asserting the peak and the causes of death (D143's re-base).")]
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
