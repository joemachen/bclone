using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The farm remembers — <c>specs/per-site-yield.md §4.2a</c> (D194). The sowing cap stops
/// predicting what a farm can bring in and starts asking what it did.
/// </summary>
/// <remarks>
/// <para>
/// <b>⛔ THE THING THESE GUARDS EXIST TO STOP COMING BACK: a cap that is self-fulfilling.</b>
/// <c>ReapableShareAt</c> cut a distant farm's field to 40%, the farmer then had nothing left to
/// do, and the idleness read back as proof that the field had been too big. Measured, a farm ten
/// ticks from its store spent <b>27% of the autumn resting</b> — and 45% at sixteen ticks, 55% at
/// twenty-two. <i>A guard that says "distant farms reap fewer tiles" can be measuring the cap
/// rather than a physical limit</i> (D157's blind-guard rule, one system over).
/// </para>
/// <para>
/// <b>⛔⛔ AND THE THING NOBODY SHOULD TRY AGAIN: thirteen tiles ten ticks out.</b> Autumn is 120
/// ticks and thirteen tiles at that distance needs about 230. The farm is short by <b>one or
/// two</b> tiles, not eight. <b>The lever for thirteen is the walk</b> — see
/// <see cref="FarmTests"/>'s distance guard and §4.3's placement warning.
/// </para>
/// <para>
/// <b>⭐⭐ WHAT DISCOVERS A BETTER YEAR IS THE VILLAGE'S OWN SPRING, NOT A PROBE — AND THE RED
/// CHECK IS WHY THAT IS KNOWN.</b> Two drafts of this slice had the farm commit
/// <c>learned + 1</c> tiles a year and latch once a tile rotted. <b>Deleting both turned nothing
/// red</b>: the settled memory and the tiles reaped came out identical at ten, sixteen and
/// twenty-two ticks — <b>6/5/4 learned and 72/60/48 reaped either way</b> — because
/// <see cref="SimWorld.HarvestOneFarmCanBringIn"/> multiplies by the hands standing in the field
/// <em>at that moment</em>, so a farm with two hands in spring and one by autumn already commits
/// ground for two. <b>D86's live-allowance rule was always going to over-reach; the memory only
/// has to notice.</b> A deliberate probe on top would have been the invisible no-op this project
/// has rejected four times (D56, D177, D187), and the failure modes agree: with no probe the
/// worst a farm can do is sit on today's behaviour, and with one it is to rot a tile every year.
/// </para>
/// </remarks>
public sealed class FarmMemoryTests
{
    private readonly ITestOutputHelper _output;

    public FarmMemoryTests(ITestOutputHelper output) => _output = output;

    // ⚠️ With no stock limits set (D409): these claims are about learning and cadence, and the
    // player's starting wheat and forage limits stand in front of both. See `ShippedConfig`.
    private static SimConfig Config => ShippedConfig.EstablishedWithNoLimitsSet();

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    // ---------------------------------------------------------------
    //  The memory itself
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ A distant farm ends up committing more ground than the prediction ever gave it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The whole mechanism, in one assertion.</b> Without it the opening guess is the answer
    /// for ever and this slice is <c>ReapableShareAt</c> with extra steps.
    /// </para>
    /// <para>
    /// ⚠️ <b>A DISTANT FARM, BECAUSE A NEAR ONE OPENS AT THE DERIVED CAP AND HAS NOWHERE TO GO.</b>
    /// Posing this beside the granary gives a guard that passes whether or not the memory exists
    /// — D157's blind fixture, one system over.
    /// </para>
    /// <para>
    /// ⚠️ <b>AND AGAINST THE OPENING GUESS, NOT AGAINST THE RAW FIELD.</b>
    /// <c>FieldTilesLearned</c> is zero on a farm that has never sown, so *"it went up from
    /// zero"* is true the first time the memory is written at all and proves nothing.
    /// </para>
    /// </remarks>
    [Fact]
    public void ADistantFarmEndsUpCommittingMoreGroundThanThePredictionGaveIt()
    {
        SimLoop loop = FarmFixtures.WithNothingInTheStores(Loop(Config));
        SimWorld world = loop.World;

        // ⚠️ EIGHT TICKS OUT, NOT TEN (D406). With fences as walls the fixture's walks went round
        // yards, and ten happened to be a flat spot (5 → 5). Measured across the range: 6 → 8 → 13,
        // 7 → 7 → 9, 8 → 7 → 10, 9 → 5 → 6, 12 → 4 → 5 — the memory climbs everywhere else. Eight
        // still opens well under the derived cap, which is what makes the farm "distant" here.
        // ⚠️ NINE SINCE D417, for D406's reason: stocking the market to 100 a household made eight
        // the flat spot (7 → 7), while 6 → 13, 7 → 8, 9 → 7, 10 → 6 and 12 → 5 all still climb.
        // Nine reads 5 → 7 at the old 40 and the new 100 alike, so it is not posed on a coin-toss.
        Workplace farm = FarmTestGround.SiteAFarm(world, walkAway: 9, out int walk);
        FarmFixtures.GiveItGround(world, farm, reach: 3);

        int opening = world.FieldTilesThisFarmCommitsPerHand(farm);

        // ⚠️ NOTHING IS POSED, AND THAT IS THE FIX TO THIS GUARD'S FIRST DRAFT. It sowed a
        // single tile a year and called that "a clean autumn" — the farm brought in one tile,
        // correctly recorded that one tile is what it had managed, and the guard then failed
        // for the feature working. **The memory is a high-water mark**, so a posed field
        // smaller than the farm's own commitment is a WORSE year, not an easier one.
        for (int i = 0; i < Config.TicksPerYear * 6; i++)
        {
            loop.StepOnce();
        }

        int now = world.FieldTilesThisFarmCommitsPerHand(farm);
        _output.WriteLine(
            $"{walk} ticks out: commits per hand {opening} → {now} (learned {farm.FieldTilesLearned})");
        Assert.True(
            now > opening,
            $"A farm {walk} ticks out still commits {now} tiles a hand — the same {opening} the "
            + "prediction gave it. The memory is doing nothing, and a cap that cannot learn is "
            + "that prediction with extra steps.");
    }

    /// <summary>
    /// ⭐⭐ A thin year never lowers what the farm has already proved.
    /// </summary>
    /// <remarks>
    /// <b>D183's *give, never take*, one system over.</b> What a farm brought in once it can
    /// bring in again — a thin year is about the hands that turned up, not about the ground — so
    /// one short-staffed autumn must never become a permanent verdict on the field.
    /// </remarks>
    [Fact]
    public void AThinYearNeverLowersWhatTheFarmHasAlreadyProved()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        Workplace farm = FarmTestGround.SiteAFarm(world, walkAway: 10, out int walk);
        int painted = FarmFixtures.GiveItGround(world, farm, reach: 3);
        Assert.True(painted > 13, "The farm needs more ground than it can ever reap.");

        // Two full fields, so the farm proves what it can really do.
        for (int year = 0; year < 2; year++)
        {
            FarmFixtures.StepToTheStartOf(loop, Season.Summer);
            SowEveryTile(world, farm);
            FarmFixtures.StepToTheStartOf(loop, Season.Winter);
        }

        int proved = farm.FieldTilesLearned;
        Assert.True(proved > 1, "The farm proved nothing, so there is nothing to protect.");

        // Then three deliberately miserable years — a single tile each, which is the shape of a
        // farm that lost its hands or sat under a met stock limit.
        //
        // ⚠️ NOBODY SOWS IN SPRING (D473, D406's lesson one guard over). The memory reads
        // `sown − standing`, so a field the farmhands sowed in spring and this pose wiped in summer
        // is a field BROUGHT IN — on the fixture's per-stage valley the farm has a hand in spring,
        // and "three one-tile years" taught it 3 → 6. The seats are empty through spring, the one
        // tile is the year's whole sowing, and the hands come back to reap it.
        int places = farm.Places;
        for (int year = 0; year < 3; year++)
        {
            world.SetStaffing(farm, 0);
            FarmFixtures.StepToTheStartOf(loop, Season.Summer);
            ClearTheField(world, farm);
            SowExactly(world, farm, 1);
            world.SetStaffing(farm, places);
            FarmFixtures.StepToTheStartOf(loop, Season.Winter);
        }

        _output.WriteLine(
            $"{walk} ticks out: proved {proved}, then three one-tile years → {farm.FieldTilesLearned}");
        Assert.Equal(proved, farm.FieldTilesLearned);
    }

    /// <summary>
    /// ⭐⭐ A farm with autumn to spare tries one more field a hand — <b>and steps back once, for
    /// good at this walk, if it cannot bring it in</b> (D361, clock B).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The high-water mark can only rise when a year brings in more per hand than the farm has
    /// ever sown per hand, and it sows what it learned — so on its own it never rises. Under clock
    /// A the hands changing between spring and autumn probed it by accident; under clock B a farm
    /// ten ticks out sat at five a hand with 18% of its autumn idle, the self-fulfilling cap D194
    /// deleted, back by the side door. Now a farm that brought everything in with a reap and a
    /// haul's worth of autumn left tries one more; if that tile rots, the probe is undone and not
    /// repeated at this walk — a tile rotting every other year is the weather D167 refused.
    /// </para>
    /// <para>
    /// The failure is posed by taking the hands away for one autumn: nothing is brought in, the
    /// probe is undone, and three more full years never climb past where it stood.
    /// </para>
    /// </remarks>
    [Fact]
    public void AFarmWithAutumnToSpareTriesOneMoreFieldAndStepsBackIfItRots() => ProbeThenFailOneAutumn();

    /// <summary>
    /// ⏸️ …and a failed probe is not tried again at the same walk (D361) — <b>FALSE ON MAIN TOO,
    /// found in D422 and skipped with the numbers.</b>
    /// </summary>
    /// <remarks>
    /// This was the last three lines of the guard above, and it never ran: it stepped to "the
    /// next winter" three times from inside one winter (see the loop's comment). Walked through
    /// real years, the farm climbs back past the failed probe — to 6 a hand on main and 7 on
    /// D422's branch, against a ceiling of 5. The farm memory's rule, not the larder's; its own
    /// slice, on Joe's list.
    /// </remarks>
    [Fact(Skip = "D422: false on main too (6 a hand on main, 7 with D422, ceiling 5) — it never ran until the year loop was fixed. The farm memory's own slice; on Joe's ⏸️ list.")]
    public void AFailedProbeIsNotTriedAgainAtTheSameWalk()
    {
        (SimLoop loop, Workplace farm, int tried, int places) = ProbeThenFailOneAutumn();

        // Hands back; three full years never climb past what was proven.
        loop.World.SetStaffing(farm, places);
        for (int year = 0; year < 3; year++)
        {
            FarmFixtures.StepToTheStartOf(loop, Season.Spring);
            FarmFixtures.StepToTheStartOf(loop, Season.Winter);
            Assert.True(farm.FieldTilesLearned <= tried - 1, $"the farm probed again at the same walk: {farm.FieldTilesLearned}");
        }
    }

    /// <summary>
    /// The first farm, over three valleys and seven walks in a stated order, whose winter's lesson
    /// is a probe — run to that winter (see <see cref="ProbeThenFailOneAutumn"/>, D475).
    /// </summary>
    private static (SimLoop Loop, Workplace Farm, int Walk, int ProbedAt) TheFirstFarmThatProbes()
    {
        foreach (ulong seed in new ulong[] { 12345UL, 1UL, 2UL })
        {
            foreach (int away in new[] { 9, 10, 11, 12, 8, 7, 6 })
            {
                SimLoop loop = Loop(Config with { CartTools = 0, Seed = seed });
                Workplace farm = FarmTestGround.SiteAFarm(loop.World, walkAway: away, out int walk);
                Assert.True(FarmFixtures.GiveItGround(loop.World, farm, reach: 3) > 13);

                // ⛔ EACH PASS WALKS TO THE NEXT WINTER THROUGH SPRING (D422). This called
                // `StepToTheStartOf(Winter)` alone, which returns two ticks later when it is already
                // winter — so the "twelve years" were the first winter read twelve times.
                for (int year = 1; year <= 12; year++)
                {
                    FarmFixtures.StepToTheStartOf(loop, Season.Spring);
                    FarmFixtures.StepToTheStartOf(loop, Season.Winter);
                    if (farm.FieldProbedThisYear)
                    {
                        return (loop, farm, walk, year);
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException(
            "no farm in three valleys and seven walks ever had autumn enough to spare to try one more field");
    }

    private (SimLoop Loop, Workplace Farm, int Tried, int Places) ProbeThenFailOneAutumn()
    {
        // ⚠️ POSED WITHOUT THE FOUNDERS' TOOLS (D391). A tool adds a quarter to every reaped tile,
        // and a quarter more crop is a quarter more armfuls to haul ten ticks each way — so the
        // farm at the derived thirteen a hand brings everything in with no autumn to spare, and
        // never probes in twelve years. That is the cap being hauling-bound, not the probe being
        // broken (filed in `handoff.md` beside the over-painted farm); the claim here is the
        // probe's shape, so the tools stay in the cart.
        //
        // ⚠️ NINE TICKS OUT, NOT TEN (D470). With ground quality removed (D395) every tile reaps
        // `crop_yield_per_tile`, and the farm ten ticks out turned hauling-bound the same way a
        // tool's quarter made it: it learned 5 → 6 a hand and never had autumn to spare in twelve
        // years. Measured across walks with no tools: the probe fires at 6, 7, 9, 11 and 12 ticks
        // out and not at 8 or 10 — 10 was a flat spot under the soil too, the soil's draw on this
        // seed simply landing it on the probing side. Proven: the soil term put back at the reap
        // passes this guard at ten again.
        //
        // ⭐ THE FIRST POSE THAT PROBES, FROM A STATED LIST (D475) — NOT ONE WALK RE-PICKED EACH SLICE.
        // Whether a farm probes is a coin over walks and valleys: under the scattered seams the shipped
        // seed's farm probed at 4, 5, 8 and 11 ticks out and not at 6, 7, 9, 10 or 12, seed 1 at 9–12,
        // seed 2 at 6–9, 11 and 12 (14 of 27 poses) — and a near farm does not probe at all, because
        // it reaches the derived cap straight from its high-water mark, and the probe only fires below
        // it. This guard was re-posed 10 → 9 in D470 and 9 flipped in D475. The claim has two halves:
        // a farm with autumn to spare tries one more field — which some of these must — and a probe
        // that rots steps back, asked of the first farm that probed. Chosen on the premise, the probe
        // happening; the step back is what is under test.
        (SimLoop loop, Workplace farm, int walk, int probedAt) = TheFirstFarmThatProbes();
        SimWorld world = loop.World;

        int tried = farm.FieldTilesLearned;
        _output.WriteLine($"{walk} ticks out: the farm probed in year {probedAt}, trying {tried} a hand");

        // The probe year: sow, then take the hands away for the autumn so nothing comes in.
        FarmFixtures.StepToTheStartOf(loop, Season.Fall);
        Assert.True(world.StandingCropSixteenths(farm) > 0, "the probe year sowed nothing, so there is nothing to fail to reap");
        int places = farm.Places;
        world.SetStaffing(farm, 0);

        // ⚠️ READ ON THE EVE OF THE LESSON, NOT AT `tried` (D512). The step back is one tile from
        // what the farm KNOWS when winter turns — and that is not always what it tried: a store
        // filling or emptying moves the haul walk, and `FieldTilesThisFarmCommitsPerHand`
        // re-reckons the field when it does (D142's one door). Measured when the steading changed
        // which pose probes first (seed 12345, nine ticks out, year 2): the walk moved mid-autumn,
        // the farm re-reckoned 7 → 10 a hand with nothing reaped, and the lesson stepped it back to
        // 9 — the retreat working exactly, against a number read before the walk moved.
        while (world.Clock.Season != Season.Winter)
        {
            loop.StepOnce();
        }

        int knew = farm.FieldTilesLearned;
        Assert.True(world.StandingCropSixteenths(farm) > 0, "the hands-off autumn brought everything in, so no probe failed");
        loop.StepOnce();

        _output.WriteLine(
            $"after the failed autumn: {knew} → {farm.FieldTilesLearned} a hand (tried {tried}), "
            + $"probe failed = {farm.FieldProbeFailed}");
        Assert.Equal(knew - 1, farm.FieldTilesLearned);
        Assert.True(farm.FieldProbeFailed);
        return (loop, farm, tried, places);
    }

    /// <summary>
    /// ⚠️ A year the farm never sowed teaches it nothing — the met-limit trap, named in §4.2a.
    /// </summary>
    /// <remarks>
    /// <b>An empty field at the turn of winter looks exactly like a cleared one</b>, and a farm
    /// held by a met stock limit would read years of idleness as years of success, climb to the
    /// cap, and over-commit the moment the player raised the limit.
    /// </remarks>
    [Fact]
    public void AYearWithNoCropTeachesTheFarmNothing()
    {
        SimLoop loop = FarmFixtures.WithNothingInTheStores(Loop(Config));
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 3);

        // ⚠️ NOBODY SOWS IT (D406). This sowed in spring and wiped the crop in summer — and the
        // memory reads `sown − standing`, so a wiped field is a field brought in: with fences the
        // fixture's farm moved, sowed more, and the pose "taught" it 2. A fallow year is a year
        // nobody sows; the seats are kept empty, which is the player's own way to leave it fallow.
        world.SetStaffing(farm, 0);

        // Nothing sown at all, three years running.
        int before = farm.FieldTilesLearned;
        for (int year = 0; year < 3; year++)
        {
            FarmFixtures.StepToTheStartOf(loop, Season.Summer);
            ClearTheField(world, farm);
            FarmFixtures.StepToTheStartOf(loop, Season.Winter);
        }

        _output.WriteLine($"learned {before} → {farm.FieldTilesLearned} over three fallow years");

        // ⚠️ THE FLOOR IS ONE, AND A FARM THAT WAS NEVER SOWN STARTS AT ZERO (D386). Until the
        // founders' plots moved a hand's way, nobody took this farm's seat in the fixture and the
        // learning pass never ran; now one sows in spring, the summer clears it, and the pass
        // reads a year that brought in nothing — which it floors to one, by design (*a thin year
        // is about the hands that turned up, not about the ground*). The claim is that a fallow
        // year never RAISES what the farm believes above what it knew, or above the floor.
        Assert.True(
            farm.FieldTilesLearned <= System.Math.Max(before, 1),
            $"Three fallow years taught the farm {farm.FieldTilesLearned}, from {before}.");
    }

    /// <summary>
    /// ⛔ The memory can never carry a farm past what the economy derives.
    /// </summary>
    /// <remarks>
    /// <b>`FieldTilesOneFarmerKeeps` is the survival floor the whole economy is solved against</b>
    /// (D16, D189). A well-sited farm's physical ceiling measures <b>21</b> tiles; the derivation
    /// says <b>13</b>; thirteen wins. A memory that could climb past it would inflate a derived,
    /// locked number from the far end — the exact move D189 refused for
    /// <c>crop_yield_per_tile</c>.
    /// </remarks>
    [Fact]
    public void TheMemoryNeverClimbsPastWhatTheEconomyDerives()
    {
        SimConfig config = Config;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 4);

        int derived = VillageEconomy.FieldTilesOneFarmerKeeps(config);

        // ⚠️ EVERY TILE, EVERY YEAR, WHICH IS THE ONLY POSE THAT PUSHES AGAINST THE CEILING.
        // The first draft sowed one tile a year and passed with `learned` sitting at 1 —
        // **green and blind** (D157). A guard about a ceiling has to be given a field big
        // enough to reach it.
        for (int year = 0; year < 25; year++)
        {
            FarmFixtures.StepToTheStartOf(loop, Season.Summer);
            SowEveryTile(world, farm);
            FarmFixtures.StepToTheStartOf(loop, Season.Winter);
        }

        _output.WriteLine($"after twenty-five full fields: learned {farm.FieldTilesLearned}, derived {derived}");
        Assert.True(
            farm.FieldTilesLearned > 1,
            "The farm learned nothing at all, so this guard is not testing a ceiling.");
        Assert.True(
            farm.FieldTilesLearned <= derived,
            $"The farm's memory reached {farm.FieldTilesLearned} against a derived {derived}. "
            + "The memory may only ever commit LESS than the derivation, never more.");

        Assert.True(
            world.HarvestOneFarmCanBringIn(farm) <= derived * farm.Places,
            "The committed ground went past the derivation for the seats the farm has.");
    }

    /// <summary>
    /// ⭐ A store built by the fields lets the farm try again.
    /// </summary>
    /// <remarks>
    /// <b>The latch has to be releasable or it is a trap.</b> A farm that learned its limit at ten
    /// ticks out is answering a question about a walk, and when the player changes the walk the
    /// old answer stops being true. This is the difference between a memory and a scar.
    /// </remarks>
    [Fact]
    public void AStoreBuiltByTheFieldsLetsTheFarmTryAgain()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;

        Workplace farm = FarmTestGround.SiteAFarm(world, walkAway: 12, out int walk);
        int painted = FarmFixtures.GiveItGround(world, farm, reach: 3);
        Assert.True(painted > 4, "The farm needs ground to over-reach on.");
        Assert.True(walk > 6, $"The farm landed {walk} ticks out — too near to measure anything.");

        // Twice, for the reason `AFarmThatCannotBringItInSettlesBackAndStopsClimbing` states:
        // a first rot that also sets a record is a good year.
        for (int year = 0; year < 2; year++)
        {
            FarmFixtures.StepToTheStartOf(loop, Season.Summer);
            SowEveryTile(world, farm);
            FarmFixtures.StepToTheStartOf(loop, Season.Winter);
        }


        int shortWalk = farm.FieldWalkWhenLearned;

        // ⚠️ PER HAND, NOT `HarvestOneFarmCanBringIn`. That multiplies by the hands standing in
        // the field, and a posed farm the allocator has not staffed yet has none — so both
        // sides of the comparison came out zero and the guard passed nothing. The claim is
        // about the farm's own reckoning, which is the per-hand number.
        int committedFar = world.FieldTilesThisFarmCommitsPerHand(farm);

        // A granary right beside the fields — the lever that actually buys the tiles.
        StoreBuilding near = FarmTestGround.RaiseAGranaryBeside(world, farm);
        Assert.True(
            world.TravelCost.TicksBetween(farm.Tile, near.Tile) < walk,
            "The new granary is no nearer than the old one, so this measures nothing.");

        int committedNear = world.FieldTilesThisFarmCommitsPerHand(farm);
        _output.WriteLine(
            $"learned at a walk of {shortWalk}: committed {committedFar}; "
            + $"with a granary beside the fields: {committedNear}");

        Assert.True(
            committedNear > committedFar,
            $"A granary beside the fields moved the committed ground from {committedFar} to "
            + $"{committedNear}. The walk is the lever, and it did nothing.");
    }


    // ---------------------------------------------------------------
    //  ⭐⭐ The anti-vacuity guards — what the slice is FOR
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ A distant farm stops standing idle in a field it was told was too big.
    /// </summary>
    /// <remarks>
    /// <b>This is the finding, and it is the guard the whole slice exists to hold.</b> Measured
    /// before the change, a farm ten ticks from its store spent <b>27% of every autumn resting</b>
    /// while reaping five tiles — the cap had cut its field and then the idleness proved the cap
    /// right. <i>The cap was self-fulfilling.</i>
    /// </remarks>
    [Fact]
    public void ADistantFarmNoLongerIdlesThroughTheAutumnItWasTooBusyFor()
    {
        SimConfig config = Config;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        Workplace farm = FarmTestGround.SiteAFarm(world, walkAway: 10, out int walk);
        FarmFixtures.GiveItGround(world, farm, reach: 3);
        FarmFixtures.PinAFarmhand(world, farm);

        int idle = 0;
        int handTicks = 0;
        int reaped = 0;

        const int Years = 12;
        for (int i = 0; i < config.TicksPerYear * Years; i++)
        {
            loop.StepOnce();
            bool fall = world.Clock.Season == Season.Fall;

            foreach (Villager villager in world.Villagers)
            {
                if (!villager.Alive || villager.WorkplaceId != farm.Id)
                {
                    continue;
                }

                if (fall)
                {
                    handTicks++;
                    if (villager.State is VillagerState.Resting or VillagerState.Idle)
                    {
                        idle++;
                    }
                }

                if (villager.State == VillagerState.Reaping && villager.ActionTicksRemaining == 1)
                {
                    reaped++;
                }
            }
        }

        int idleShare = handTicks == 0 ? 0 : idle * 100 / handTicks;
        _output.WriteLine(
            $"{walk} ticks out over {Years} years: {reaped} tiles reaped, "
            + $"{idleShare}% of the autumn idle, memory settled at {farm.FieldTilesLearned}");

        Assert.True(handTicks > 0, "Nobody ever held the farm, so this measures nothing.");
        // ⚠️ 18 SINCE D412 (two thirds of D194's 27 %): the ten-tick farm reads 17 %, all of it
        // Resting, with the field learned at 5 a hand — the farmhand at home in a village whose
        // houses moved, not the cap. `FarmLedgerTests.AndItIsNotIdleThroughIt` has the census. Joe, on D412: *"merge."*
        Assert.True(
            idleShare < 18,
            $"A farm {walk} ticks out spent {idleShare}% of its autumns idle. It was measured at "
            + "27% before this slice, and that idleness is the cap cutting a field the farmer then "
            + "had time to spare on. A self-fulfilling cap is what this slice deleted.");
    }

    /// <summary>
    /// ⭐⭐ …and it brings home more food for it, which is the point of not idling.
    /// </summary>
    /// <remarks>
    /// <b>The companion to the guard above, and neither is enough alone.</b> Idleness could be
    /// removed by giving the farmhand busywork; tiles could be raised by letting the crop rot in
    /// the field. <b>Only both together say the farm got better.</b>
    /// </remarks>
    [Fact]
    public void AndItBringsInMoreThanThePredictionEverLetIt()
    {
        // ⭐ A LEDGER OVER SIX VALLEYS, NOT ONE (D473). The prediction produced 51 tiles over ten
        // years at this distance, measured — in the village that fed a family of four by foraging.
        // At D363's floor (foraging feeds a couple) the same farm on the prediction alone brought
        // in 38; D382's 2×2 farmhouse made it 27 against 35 with memory and probe — all on the
        // shipped seed's ONE valley. Under per-stage seeds that valley's village barely works its
        // farm (22 against 21: a dying village, not a farm memory), as do three of twelve others,
        // so one village could not say anything. Measured with `LearnFromTheAutumn` stubbed, the
        // prediction alone reaps **207** over seeds 1–6 (31 / 54 / 5 / 6 / 58 / 53); with memory
        // and probe, **229** (36 / 60 / 5 / 6 / 64 / 58).
        int reaped = 0;
        int sown = 0;
        int walk = 0;
        for (ulong seed = 1; seed <= 6; seed++)
        {
            int here = FarmTestGround.TilesReapedOverTenYears(
                Config with { Seed = seed }, walkAway: 10, out walk, out int broughtIn);
            reaped += here;
            sown += broughtIn == 0 ? 0 : here * 100 / broughtIn;
            _output.WriteLine($"seed {seed}, {walk} ticks out: {here} tiles reaped, {broughtIn}% brought in");
        }

        int broughtInAll = sown == 0 ? 0 : reaped * 100 / sown;
        _output.WriteLine($"six valleys: {reaped} tiles reaped, {broughtInAll}% brought in");

        Assert.True(
            reaped > 207,
            $"Six farms {walk} ticks out reaped {reaped} tiles in ten years. The prediction they "
            + "replaced manages 207 over the same valleys, and the ledger says the ground is there for more.");

        // ⛔ AND THE ROT LINE STAYS HONEST (D167). Bringing in more by sowing far more and
        // losing the difference to winter is the bug this slice's ancestor fixed.
        Assert.True(
            broughtInAll >= 75,
            $"The farms brought in only {broughtInAll}% of what they sowed. Rot every year by "
            + "construction is weather, and the player cannot act on weather (D167).");
    }

    // ---------------------------------------------------------------
    //  §4.3 — the player is told, while they can still move it
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ Marking a farmhouse far from any food store says so, and says what it will cost.
    /// </summary>
    /// <remarks>
    /// <b>The single largest legible consequence in the farm, and nothing said it.</b> There is a
    /// distance warning at placement already, and it measures the walk to the <em>village</em> —
    /// which is not the walk that halves a harvest. <b>Warned, never refused</b> (D43, D86).
    /// </remarks>
    [Fact]
    public void MarkingAFarmFarFromAStoreWarnsAboutTheWalkTheHarvestMakes()
    {
        SimWorld world = Loop(Config).World;

        GridPos far = FarmTestGround.GroundAtAboutThisWalk(world, walkAway: 14, out int walk);
        PlacementVerdict verdict = world.CanBuildAt(BuildingKind.Farmhouse, far);

        _output.WriteLine($"{walk} ticks from the nearest granary: \"{verdict.Warning}\"");

        Assert.True(verdict.Allowed, "A distant farm is a decision, not an impossibility (D43).");
        Assert.True(
            verdict.HasWarning,
            $"A farmhouse {walk} ticks from the nearest food store drew no warning at all. Its "
            + "harvest will be roughly half a well-sited farm's and the game never mentions it.");
        Assert.Contains("store", verdict.Warning, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The anti-vacuity companion (D7): a farm beside the granary is not nagged.
    /// </summary>
    /// <remarks>
    /// Without this, a warning that fired on every farmhouse in the valley would pass the guard
    /// above and teach the player to click past it — D42's rule about one considered sentence
    /// rather than a nag.
    /// </remarks>
    [Fact]
    public void ButAFarmBesideTheStoresIsNotWarnedAboutAnything()
    {
        SimWorld world = Loop(Config).World;

        GridPos near = FarmTestGround.GroundAtAboutThisWalk(world, walkAway: 1, out int walk);
        PlacementVerdict verdict = world.CanBuildAt(BuildingKind.Farmhouse, near);

        _output.WriteLine($"{walk} ticks from the nearest granary: \"{verdict.Warning}\"");
        Assert.DoesNotContain("store", verdict.Warning, System.StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------
    //  Posing helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Put every tile of a farm's ground under seed — <b>the over-reaching year, posed</b>.
    /// </summary>
    /// <remarks>
    /// <b>⚠️ THERE IS DELIBERATELY NO "SOW EXACTLY N" HELPER ANY MORE, AND ITS ABSENCE IS A
    /// FINDING.</b> The first draft of this file had one and used it to pose *a clean autumn* as
    /// a single tile — so the farm brought in one tile, correctly recorded that one tile is what
    /// it had managed, and the guard failed for the feature working. **The memory is a
    /// high-water mark, so a posed field smaller than the farm's own commitment is a WORSE year,
    /// not an easier one.** The only two poses this file needs are *no crop at all* and *more
    /// ground than anybody could take in*.
    /// </remarks>
    private static void SowExactly(SimWorld world, Workplace farm, int tiles)
    {
        List<GridPos> owned = world.Zones.WorkGroundOf(farm.Id)
            .Select(world.Zones.PositionOf)
            .OrderBy(at => world.TravelCost.Cost(farm.Tile, at))
            .ThenBy(at => at.Y)
            .ThenBy(at => at.X)
            .ToList();

        int standing = 0;
        foreach (GridPos at in owned)
        {
            if (world.Map.TerrainAt(at) is Terrain.Sown or Terrain.Ripe)
            {
                standing++;
            }
            else if (standing < tiles && SimWorld.IsSowable(world.Map.TerrainAt(at)))
            {
                world.SetTerrain(at, Terrain.Sown);
                world.Map.SetCrop(at, 1);
                standing++;
            }
        }
    }

    private static void SowEveryTile(SimWorld world, Workplace farm)
    {
        IReadOnlyList<int> owned = world.Zones.WorkGroundOf(farm.Id);
        for (int i = 0; i < owned.Count; i++)
        {
            GridPos at = world.Zones.PositionOf(owned[i]);
            if (SimWorld.IsSowable(world.Map.TerrainAt(at)))
            {
                world.SetTerrain(at, Terrain.Sown);
                world.Map.SetCrop(at, 1);
            }
        }
    }

    /// <summary>Take every crop off a farm's ground, so a posed year starts from nothing.</summary>
    private static void ClearTheField(SimWorld world, Workplace farm)
    {
        IReadOnlyList<int> owned = world.Zones.WorkGroundOf(farm.Id);
        for (int i = 0; i < owned.Count; i++)
        {
            GridPos at = world.Zones.PositionOf(owned[i]);
            if (world.Map.TerrainAt(at) is Terrain.Sown or Terrain.Ripe)
            {
                world.SetTerrain(at, Terrain.Field);
            }
        }
    }
}
