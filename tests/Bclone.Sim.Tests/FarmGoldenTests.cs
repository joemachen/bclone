using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ A golden over the <b>seam</b> — a village that farms and clears ground at the same time.
/// </summary>
/// <remarks>
/// <para>
/// <b>D161's rule, taken literally: when two systems meet, the golden goes over the seam rather
/// than over either side.</b> `crops-and-orchards.md §9` names the case to write <em>with</em>
/// the feature: a laborer clearing painted harvest ground beside a ripe field — the crops ×
/// brush seam, which is the one that will silently eat a harvest.
/// </para>
/// <para>
/// <b>⛔ AND IT ALSO CLOSES D157'S OPEN HOLE, which is why it is worth more than one seam.</b>
/// Both fifty-year village goldens paint <b>zero</b> tiles across all 24,000 of their ticks —
/// <c>Zones.HarvestTiles</c> is 0 on every one of them — so <c>NearestHarvest</c> early-returns
/// and step C's central mechanic has had no drift guard at all since it shipped. D158 recorded
/// that as <em>"the first item on the other side"</em>. This is that item, arriving with the
/// slice that needed it anyway.
/// </para>
/// <para>
/// <b>⚠️ A GREEN GOLDEN CAN MEAN "NOT COVERED" RATHER THAN "NO-OP" (D157), so the coverage is
/// asserted rather than assumed.</b> That is the whole finding this file exists because of: the
/// two existing goldens did not move for step C, and the reason turned out to be that they never
/// reach the changed code. So <see cref="TheRunActuallyPaintsClearsSowsAndReaps"/> stands beside
/// the hash and fails if the scenario ever stops being the scenario — a hash over a village that
/// quietly stopped farming would be perfectly stable and perfectly worthless.
/// </para>
/// </remarks>
public sealed class FarmGoldenTests
{
    private readonly ITestOutputHelper _output;

    public FarmGoldenTests(ITestOutputHelper output) => _output = output;

    /// <summary>Years the scenario runs for.</summary>
    /// <remarks>
    /// <b>Twenty, not fifty.</b> Long enough for several full crop years and for the laborers to
    /// work through a real amount of painted ground, short enough that the guard costs seconds
    /// rather than minutes — the suite is already at eleven and D112 records what that costs.
    /// The two fifty-year goldens still watch the long arc; this one watches a mechanism.
    /// </remarks>
    private const int Years = 20;

    // ---------------------------------------------------------------
    //  The golden
    // ---------------------------------------------------------------
    //
    // ⭐ FIRST TAKEN with the farm (`specs/crops-and-orchards.md`, D161). There is no
    // "before" for this one: nothing could sow until this slice, so a run that farms has never
    // existed and this number describes a village the suite has never watched before.
    //
    // ⚠️ WHEN THIS MOVES, SAY WHY IN ONE SENTENCE AND WRITE IT HERE (D152). The two things that
    // will move it are the crop economy (`crop_yield_per_tile`, `sow_ticks`, `reap_ticks`,
    // `farm_store_cap`) and anything that changes what a laborer does with painted ground —
    // which is exactly the pair of systems it is watching.
    //
    // ⭐ RE-TAKEN FOR THE JITTER (D163), one commit after it was first taken, and **it moved for
    // a third reason neither of those** — which is worth recording rather than shrugging at. A
    // villager who came in to get warm now stays until they are warm, instead of being flipped
    // back out on the next tick with their arms still full. This run spans twenty years and
    // twenty winters, and everybody in it now spends ticks at a hearth they used to spend
    // walking, so the whole history downstream differs.
    //
    //   before the jitter was fixed: 7924144676203476477
    //
    // ⭐ RE-TAKEN AGAIN FOR THE HAUL (D165), and **it is the only golden in the suite that
    // moved** — which is the pattern working exactly as it should. `HaulTheHarvest` asked the
    // farm store `IsFull` rather than whether it had room for the load, so every reaped tile
    // made two long walks instead of one; a farmer now brings in thirteen tiles an autumn
    // against five. The two fifty-year village goldens and the map golden are all unmoved,
    // because **none of them has a farm in it** — silent about what they do not reach, loud
    // about what they do.
    //
    //   before "with room" meant room for the load: 12419260555584221460
    //
    // ⭐ RE-TAKEN FOR THE FETCH AND THE FARM (D166, D167) — three changes at once, and each
    // reaches this run: nobody walks to a store for a trivial amount, a fetch fills the armful,
    // and **a farm sows only what it can bring in while its reapers go back to the rows instead
    // of walking home between every tile.** The farm was throwing away seventy per cent of its
    // own crop every year; it brings in 93% now.
    //
    //   before the farm stopped rotting its crop: 12068547528605544516
    //
    // ⭐ RE-TAKEN FOR §3.2a — THE MARKET RUNS THE FARM'S BUFFER DRY (D171). `crops-and-orchards.md
    // §3.2` ruling 1 has said since the farm shipped that the buffer is free and *"running it dry
    // is the market's job"*, and nothing ever ran it dry: a trader could take from a farm to fill
    // a hungry larder and never to empty one. A third leg does it now, offered against every other
    // leg on travel cost, and gated on the derived condition that the buffer can no longer take a
    // whole armful.
    //
    // ⚠️ AND THE TWO FIFTY-YEAR GOLDENS DID NOT MOVE, WHICH IS THE CONTRACT HOLDING RATHER THAN A
    // SURPRISE: neither village ever places a farmhouse, so no workplace store in them ever holds
    // food and the new leg is never offered. Silent about what they do not reach (D157, D162).
    //
    //   before the market ran the buffer dry: 11489388314243111802
    //
    // ⭐ RE-TAKEN FOR PER-SITE YIELD (D178) — and this village moves for BOTH of its halves.
    // The soil under its field decides what a tile is worth (`CropYieldAt`), and the walk to
    // its store decides how much ground it commits in spring (`ReapableShareAt`). It is the
    // one golden in the suite that reaches a farm at all, so it is the one that can see them.
    //
    //   before ground was worth going to: 5832742735958199009
    //
    // ⭐⭐ RE-TAKEN FOR THE PROFICIENCY SUBSTRATE (D181) — and unlike every re-take above it,
    // **nobody in this village did anything differently.** Villagers now carry what they have
    // put into each trade; it is hashed; it grows from the first tick; and **no behaviour
    // anywhere reads it** until landing 2. The scenario staffs a farm and a harvest brush for
    // twenty years, so its people accrue and this number moves for that and nothing else.
    //
    //   before people got better at things: 4486163041401162495
    //
    // ⭐⭐ RE-TAKEN AGAIN, SAME DAY, FOR SKILL DECAY BEING DELETED (D183). Proficiency only ever
    // goes up now, and carries a second counter beside it. **`ComputeIgnoringSkills` below is
    // unchanged through both re-takes**, which is the whole licence for moving this twice.
    //
    //   before decay was deleted: 7911818851227652011
    //
    // ⭐⭐ RE-TAKEN FOR THE MARKETER THE VILLAGE NEVER ASKED FOR (D185), AND THIS ONE MOVED FOR
    // A REAL BEHAVIOUR CHANGE -- unlike the two skill re-takes above it. `MarketersWanted`
    // counted errands from HOUSEHOLDS and nothing else, so nobody was ever put on the market
    // because a farm needed emptying, and D171's buffer-clearing leg could not run. This is the
    // one village in the suite with a farm in it, so it is the one that can see the difference:
    // a trader works here now, and twenty years of that is a different twenty years.
    //
    // ⚠️ `SeamBeforeAnybodyGotBetter` MOVES TOO, and that is correct rather than alarming: it
    // fingerprints everything except skill, and what changed here is what people DO. A skill
    // re-take must leave it alone; this is not one.
    //
    //   before the market got staffed for farms: 4043003718136410697
    //
    // ⭐⭐ RE-TAKEN FOR MASTERY BITING (D187) — Phase 3 landing 2, and the moment the skill
    // pillar stopped being bookkeeping. **A master takes half the ticks over an action, rounded
    // up**, so this village's farmers sow and reap faster as their careers run on, and twenty
    // years of that is a different twenty years.
    //
    //   before mastery bit: 6737691834764729296
    //
    // ⭐⭐ RE-TAKEN FOR THE MIXED FOUNDING AND THE SEEDED RHYTHM (D190) -- landing 3, and the
    // commit that discharges D28.
    //
    //   before the founders were people: 9706055072185576047
    //
    // ⭐ RE-TAKEN FOR ELDERS EATING A DEPENDANT'S SHARE (D191).
    //
    //   before elders ate like children: 16167409353535345881
    //
    // ⭐ RE-TAKEN FOR A FIVE-DAY THAW (D192).
    //
    //   before the fire got warmer: 11509711031316440761
    //
    // ⭐⭐ RE-TAKEN BECAUSE THE FARM REMEMBERS WHAT IT BROUGHT IN (D194,
    // `per-site-yield.md §4.2a`). The sowing cap stopped predicting a distant farm's autumn and
    // started reading its own best one, so **this village commits different ground and therefore
    // has a different history**. It is the only village in the suite that plants a farmhouse,
    // which is why it is the only golden that moves — **the two fifty-year goldens are unmoved,
    // and that is the check that matters**: a farm's memory leaking into a village with no farm
    // in it would be a bug, not a re-base.
    //
    //   before the farm remembered: 12485177273367720852
    //
    // ⭐⭐ RE-TAKEN FOR THE STOCKED MARKET (D197) — see the note in `StockLimitTests`.
    //
    //   before the market was stocked: 3714993309705346931
    //   before the market stopped being a dumping ground (D199): 4712803508757490940
    //
    // ⭐⭐ RE-TAKEN BECAUSE A VILLAGER'S ARMS ARE HASHED BY INDEX (D211), and that is the whole
    // reason — measured rather than assumed. `MixVillager` mixed three named carried fields; it
    // mixes the whole carried stockpile now, like every other store in the game. **Restoring
    // those three lines and re-running makes all five moved goldens byte-identical again**,
    // which says the village itself did not move: nothing paints a seam in these runs, so
    // nobody clears one and the carry fix reaches nothing here.
    //
    //   before the arms were hashed by index: 11064751127156165011
    //   before the fixture ate what the game eats (D223): 5494657115974799914
    //   before the village knew things (D225): 4569067148687306339
    // ⭐⭐ RE-TAKEN BECAUSE THE VILLAGE CAN PUT ITS LOGS DOWN AGAIN (2026-08-27). Two changes,
    // both of which reach this village and only this village:
    //
    //   1. `StoreForTheLoad` asks `Accepts` before walking somewhere. It matched on KIND and
    //      fullness alone, so an armful was carried to a store that refuses it, set down at its
    //      door, picked up by the tidy errand — which DOES ask — and carried back to the same
    //      store, for ever.
    //   2. Clearing now outranks tidying (Joe: *"clearing first"*), so one stubborn heap can no
    //      longer consume every spare hand in the village.
    //
    // ⭐⭐ AND THE GOLDENS THAT DID **NOT** MOVE ARE THE RESULT HERE, NOT THE LEFTOVERS (D223).
    // **This is the only golden in the suite that moved.** The two fifty-year villages are
    // byte-identical, and they should be: nothing in them ever refuses a good, so neither change
    // can reach them. **This is the one village that paints a seam and clears it**, which is
    // exactly the village a hauling fix is supposed to touch. A move in the others would have
    // meant the change had leaked.
    //
    //   before a load could be put down where it was accepted: 5913960743801194628
    // ⭐⭐ RE-TAKEN FOR THE REST SPELL (D250, Joe: villagers should idle when they have no job).
    // A laborer with nothing to do now sits for `rest_ticks` before asking again, where they
    // used to re-ask every tick for their whole life. **Every village in the suite has spare
    // hands, so every village golden moves** — this is the widest deliberate re-take since the
    // arms were hashed by index.
    //
    //   before the village was allowed to rest: 1856781046300124051
    //   before the huts were capped at two (D262): 5824784480959670577
    //   before the fire got hungrier (2026-09-01): 6716701650818163431
    private const ulong SeamGoldenHash = 3921975159594428461UL;

    /// <summary>
    /// ⭐ The village underneath the counters — <b>unmoved by anybody getting better at
    /// anything</b> (D181).
    /// </summary>
    /// <remarks>
    /// <b>⭐⭐ AND IT HAS NOW MOVED, WHICH IS THE WHOLE POINT OF IT (D187).</b> This fingerprints
    /// everything except the skill counters, so through landing 1 it was **byte-identical**
    /// while proficiency accrued and did nothing — and the note here said in as many words that
    /// *"when landing 2 makes mastery bite, this number must move too."* **It did.** A skill
    /// system that changes nothing is D56's clothing, and this is the number that can tell the
    /// difference.
    /// <para>
    /// ⚠️ The claim that the *substrate alone* changes nothing is still alive and still
    /// checkable — <c>SkillTests.FiftyYearsOfVillageAndOnlyTheCountersMoved</c> poses a village
    /// with the speed bonus at zero and asserts the pre-skill goldens byte for byte.
    /// </para>
    /// <para>
    /// <b>⚠️ AND IT MOVES FOR ANYTHING THAT IS NOT A SKILL, WHICH IS THE OTHER HALF OF ITS JOB.</b>
    /// D194 gave the farm a memory of what it brought in, so this village commits different ground
    /// — a real behaviour change, and it belongs in this number as much as in the one above.
    /// *"Unmoved by anybody getting better"* is a claim about proficiency, not a claim that the
    /// village never changes.
    /// </para>
    /// </remarks>
    //   before the arms were hashed by index (D211): 4480535409214959852
    //   before the fixture ate what the game eats (D223): 12276508385911985440
    //   before the village knew things (D225): 13041738680192547203
    //   before the founding master had to earn it here (D227): 267111501083800924
    //   before a load could be put down where it was accepted (2026-08-27): 2112570384239951269
    //   before the village was allowed to rest (D250): 15700858930161795428
    //   before the huts were capped at two (D262): 2038884662017358556
    //   before the fire got hungrier (2026-09-01): 2945337393414434771
    private const ulong SeamBeforeAnybodyGotBetter = 12024391942491487759UL;

    /// <summary>The seam, in one number.</summary>
    [Fact]
    public void AVillageThatFarmsAndClearsAtOnceRunsTheSameWayEveryTime()
    {
        SimWorld world = RunTheScenario(out _);

        ulong actual = StateHash.Compute(world);
        ulong withoutSkills = StateHash.ComputeIgnoringSkills(world);
        _output.WriteLine(
            $"crops × harvest brush, {Years}y: {actual} (without skills {withoutSkills})");

        Assert.Equal(SeamGoldenHash, actual);
        Assert.Equal(SeamBeforeAnybodyGotBetter, withoutSkills);
        Assert.NotEqual(withoutSkills, actual);
    }

    /// <summary>
    /// ⭐ The scenario is actually the scenario — <b>the anti-vacuity half, and the point</b>.
    /// </summary>
    /// <remarks>
    /// <b>D157's lesson in a guard.</b> Both fifty-year goldens were expected to move for step C
    /// and did not, and the reason was measured rather than assumed: they paint zero tiles, so
    /// the changed code is never reached. A hash is only evidence about the code it executes,
    /// and the only way to know it executes any is to count.
    /// </remarks>
    [Fact]
    public void TheRunActuallyPaintsClearsSowsAndReaps()
    {
        RunTheScenario(out Coverage seen);

        _output.WriteLine(
            $"over {Years} years: {seen.PaintedAtMost} tiles still painted, "
            + $"{seen.Cleared} ticks spent clearing, {seen.SownAtMost} sown at once, "
            + $"{seen.ReapedTotal} reaped, {seen.FoodBuffered} food buffered at the farm");

        Assert.True(seen.PaintedAtMost > 0, "Nothing was ever painted for harvest.");
        Assert.True(seen.Cleared > 0, "No painted tile was ever cleared — D157's hole is still open.");
        Assert.True(seen.SownAtMost > 0, "Nothing was ever sown, so the golden watches no crop.");
        Assert.True(seen.ReapedTotal > 0, "Nothing was ever reaped.");
        Assert.True(
            seen.FoodBuffered > 0,
            "Nothing ever reached the farm's own store, so the run does not cover it.");
    }

    /// <summary>
    /// ⛔⛔ And the laborers never once took a tile of the farm.
    /// </summary>
    /// <remarks>
    /// <b>The seam itself, asserted over a live run rather than at the predicate.</b> D144's
    /// finding is that a rule tested only where it is decided is a rule nobody has tested — five
    /// guards asked <c>Accepts</c> and not one made a villager put anything down. So this counts
    /// the standing crop through twenty years of a village whose laborers are clearing ground
    /// all around it: if a field ever vanishes on a tick when no farmer reaped it, the brush has
    /// eaten the harvest.
    /// </remarks>
    [Fact]
    public void NoLaborerEverClearedAStandingCrop()
    {
        RunTheScenario(out Coverage seen);

        _output.WriteLine(
            $"{seen.ReapedTotal} tiles reaped by farmers, {seen.RottedOrTaken} lost to winter, "
            + $"{seen.VanishedUnexplained} vanished with nobody reaping and no winter");

        Assert.Equal(0, seen.VanishedUnexplained);
    }

    // ---------------------------------------------------------------

    private sealed class Coverage
    {
        internal int PaintedAtMost;
        internal int Cleared;
        internal int SownAtMost;
        internal int ReapedTotal;
        internal int RottedOrTaken;
        internal int VanishedUnexplained;
        internal int FoodBuffered;
    }

    /// <summary>
    /// A village with a farm, a field, and a wood painted for harvest right beside it.
    /// </summary>
    /// <remarks>
    /// <b>The fixture village, not the shipped cold start.</b> D143 rules that an unattended
    /// founding is supposed to die out, so a twenty-year scenario begun in an empty valley would
    /// be watching a settlement fail rather than watching two systems meet. The player's part
    /// here — a farmhouse, its ground, and a painted wood — is done once at the start and never
    /// reacted to, which is `PlayTheOpening`'s shape and is what keeps the run reproducible.
    /// </remarks>
    private static SimWorld RunTheScenario(out Coverage seen)
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        var coverage = new Coverage();

        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 2);

        // The wood beside the field, painted for harvest — the other half of the seam. Painted
        // in a ring OUTSIDE the farm's ground so the two layers are adjacent rather than
        // overlapping: work ground and harvest paint are different zones (D87) and a tile can
        // carry both, but the case that eats a harvest is the laborer walking past the field on
        // the way to a tree, which wants them to be neighbours.
        for (int dy = -6; dy <= 6; dy++)
        {
            for (int dx = -6; dx <= 6; dx++)
            {
                var at = new GridPos(farm.Position.X + dx, farm.Position.Y + dy);
                if (world.Map.Contains(at) && world.Zones.WorkGroundOwner(at) == 0)
                {
                    world.PaintHarvest(at);
                }
            }
        }

        int standing = StandingCrop(world, farm);

        // ⚠️ THE SEASON THE SYSTEMS ARE ABOUT TO RUN ON, SAMPLED BEFORE THE STEP — and this is
        // the harness bug that reads exactly like a broken feature. `SimLoop.StepOnce` runs the
        // systems and *then* advances the tick, so reading `Clock.Season` afterwards gives the
        // season of the tick that has not happened yet. Attributing the winter rot off that
        // reading reported **0 lost to winter and 344 vanished unexplained** — a harvest
        // apparently being eaten by the harvest brush, when what had actually happened is that
        // the guard was looking one tick to the right of the event.
        Season runningOn = world.Clock.Season;
        Season ranOnBefore = runningOn;

        for (int tick = 0; tick < config.TicksPerYear * Years; tick++)
        {
            loop.StepOnce();

            int sown = Sown(world, farm);
            if (sown > coverage.SownAtMost)
            {
                coverage.SownAtMost = sown;
            }

            if (farm.Store.Food > coverage.FoodBuffered)
            {
                coverage.FoodBuffered = farm.Store.Food;
            }

            // A laborer taking a painted tile is what step C is for, and D157 records that
            // neither existing golden ever reaches that code. Counted from the villagers
            // rather than from the paint, because D127 made the paint a STANDING instruction
            // whose wood grows back — so the painted count is flat while the clearing happens.
            for (int i = 0; i < world.Villagers.Count; i++)
            {
                if (world.Villagers[i].State == VillagerState.Clearing)
                {
                    coverage.Cleared++;
                    break;
                }
            }

            // A tile of standing crop that went away this tick was either reaped by a farmer
            // (somebody is carrying it), taken by winter (`CropSystem` on the season turn), or
            // it is the seam this whole file is about.
            int now = StandingCrop(world, farm);
            if (now < standing)
            {
                int lost = standing - now;
                if (runningOn == Season.Winter && ranOnBefore == Season.Fall)
                {
                    coverage.RottedOrTaken += lost;
                }
                else if (SomebodyIsReaping(world, farm))
                {
                    coverage.ReapedTotal += lost;
                }
                else
                {
                    coverage.VanishedUnexplained += lost;
                }
            }

            standing = now;
            ranOnBefore = runningOn;
            runningOn = world.Clock.Season;
        }

        coverage.PaintedAtMost = world.Zones.HarvestTiles;
        seen = coverage;
        return world;
    }

    private static int StandingCrop(SimWorld world, Workplace farm)
    {
        IReadOnlyList<int> owned = world.Zones.WorkGroundOf(farm.Id);
        int standing = 0;

        for (int i = 0; i < owned.Count; i++)
        {
            if (SimWorld.IsStandingCrop(world.Map.TerrainAt(world.Zones.PositionOf(owned[i]))))
            {
                standing++;
            }
        }

        return standing;
    }

    private static int Sown(SimWorld world, Workplace farm)
    {
        IReadOnlyList<int> owned = world.Zones.WorkGroundOf(farm.Id);
        int sown = 0;

        for (int i = 0; i < owned.Count; i++)
        {
            if (world.Map.TerrainAt(world.Zones.PositionOf(owned[i])) == Terrain.Sown)
            {
                sown++;
            }
        }

        return sown;
    }

    /// <summary>Whether anybody at this farm is mid-reap or carrying a harvest home.</summary>
    private static bool SomebodyIsReaping(SimWorld world, Workplace farm)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (villager.WorkplaceId != farm.Id)
            {
                continue;
            }

            if (villager.State is VillagerState.Reaping or VillagerState.HaulingToFarm
                or VillagerState.HaulingToStore or VillagerState.TravelingToField
                or VillagerState.TravelingHome)
            {
                return true;
            }
        }

        return false;
    }
}
