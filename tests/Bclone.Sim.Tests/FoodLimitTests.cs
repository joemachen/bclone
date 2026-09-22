using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The food limit reaches the person who would produce toward it — <b>it did not</b> (D216).
/// </summary>
/// <remarks>
/// <para>
/// Joe, playing: <em>"It seems like labor is taking priority over professions — if there are trees
/// marked for harvest, foragers will gather trees even though the food limit is not yet met [set to
/// 2000]. Assigned professions are first priority, labor work is secondary."</em>
/// </para>
/// <para>
/// <b>⭐ THE PRIORITY WAS NEVER WRONG, AND THAT IS WORTH SAYING.</b> The harvest branch sits below
/// every job in <c>Decide</c> (D87), so a forager who reaches it has already declined their own
/// work this tick. <b>What was wrong is why they declined it:</b> the work gate read the
/// <em>derived</em> target and never the player's limit, so <c>food_limit = 2000</c> and no limit
/// at all produced <b>byte-identical behaviour</b> — 959 forager ticks gathering and 871 clearing
/// in both arms. A control that changes nothing, on the good the whole economy is derived from.
/// </para>
/// <para>
/// <b>D62's *derived floor, player ceiling*, finally wired.</b> The floor half is untouched:
/// <c>TargetFoodForTheGranary</c> is what the <em>birth</em> gate reads and stays derived (D153).
/// The player's number governs work; the derived number governs children.
/// </para>
/// </remarks>
public sealed class FoodLimitTests
{
    private readonly ITestOutputHelper _output;

    public FoodLimitTests(ITestOutputHelper output) => _output = output;

    private sealed record Tally(int Gathering, int Clearing, int Food, int MostHeld);

    private static Tally RunAForagingYear(int foodLimit, ITestOutputHelper output)
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        ColdStartTests.PlayTheOpening(world);
        loop.Step(config.TicksPerYear * 10);

        if (foodLimit > 0)
        {
            world.SetStockLimit(Goods.Produce, foodLimit);
        }

        // Plenty of painted trees, so laborer work is always available to be chosen wrongly.
        ColdStartTests.PaintTheNearbyTrees(world, 10);

        int gathering = 0;
        int clearing = 0;
        int mostHeld = 0;

        for (int tick = 0; tick < config.TicksPerYear * 3; tick++)
        {
            loop.StepOnce();
            mostHeld = System.Math.Max(mostHeld, world.FoodTheVillageHolds());

            for (int i = 0; i < world.Villagers.Count; i++)
            {
                Villager v = world.Villagers[i];
                if (!v.Alive || KindOf(world, v) != JobKind.Forager)
                {
                    continue;
                }

                if (v.State is VillagerState.Gathering or VillagerState.TravelingToFood)
                {
                    gathering++;
                }
                else if (v.State == VillagerState.Clearing)
                {
                    clearing++;
                }
            }
        }

        output.WriteLine(
            $"limit {foodLimit}: forager ticks gathering {gathering}, clearing {clearing}; "
            + $"village holds {world.FoodTheVillageHolds()} (most {mostHeld}), wants {world.FoodTheVillageHasRoomFor()}");

        return new Tally(gathering, clearing, world.FoodTheVillageHolds(), mostHeld);
    }

    /// <summary>⭐⭐ A limit the village has not met keeps its foragers foraging.</summary>
    [Fact]
    public void AFoodLimitKeepsForagersOnFoodRatherThanOnPaintedTrees()
    {
        Tally unset = RunAForagingYear(0, _output);
        Tally asked = RunAForagingYear(2000, _output);

        Assert.True(
            asked.Clearing < unset.Clearing,
            $"A food limit of 2000 left foragers clearing {asked.Clearing} ticks against "
            + $"{unset.Clearing} with no limit at all — the control is not reaching them.");

        // ⚠️ THE ANTI-VACUITY MOVED WITH D385. It compared the limited village's food against the
        // unlimited one's and expected more: true while the unlimited village stopped near its
        // derived target (789 of 770) because the foragers' loads went into larders. With every
        // load pooled in a store, the unlimited village fills its granary to the brim (2,179) and
        // no limit under the brim can beat it — and the limited village hovers about its number,
        // seasons off when it reads met. The claim that survives is that the number reached the
        // foragers: at some point in three years they brought the stores up to what was asked.
        Assert.True(
            asked.MostHeld >= 2000,
            $"A food limit of 2000 was never reached (most held {asked.MostHeld}) — the foragers did not work toward the number asked.");
    }

    /// <summary>
    /// ⛔ The anti-vacuity half, and the licence for no golden moving: unset changes nothing.
    /// </summary>
    /// <remarks>
    /// <c>null</c> is the default and means <em>"the player has not said"</em>, so a village
    /// nobody has given a number to must behave exactly as it did before this was wired — which
    /// is the same argument D212 makes one control over.
    /// </remarks>
    [Fact]
    public void WithNoLimitTheVillageWantsWhatItAlwaysDid()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

        Assert.Null(world.StockLimits.For(Goods.Produce));

        // ⭐ THE SHELVES' TARGET **AND THE CUPBOARDS'** SINCE D399. The unset default is still
        // *"the player has not said"* and still derived rather than typed — what moved is what
        // the village derives: it produces for the shelves and for the larders both, because a
        // larder is outside the number the rules read (D394) and somebody still had to bring that
        // food in. Leaving the cupboards out is what made the forager's own-larder reason
        // load-bearing, and taking that reason away (D399) took 93 villagers to 1.
        Assert.Equal(
            world.TargetFoodForTheGranary() + world.FoodTheLardersWant(),
            world.FoodTheVillageHasRoomFor());
        Assert.True(world.FoodTheLardersWant() > 0, "No household wants anything, so the sum says nothing.");
    }

    /// <summary>
    /// ⭐ And a forager who stops says which of the two reasons it was.
    /// </summary>
    /// <remarks>
    /// <b>The two have opposite answers</b> — <em>raise the limit</em> against <em>build a
    /// granary</em> — and neither was on the screen. A forager who silently walks off to fell a
    /// tree is §1.1's failure whatever the sim is actually doing.
    /// </remarks>
    /// <summary>
    /// ⭐ The village's food is the shelves and the huts, never the larders — and a quoted total
    /// says where it is (D394).
    /// </summary>
    /// <remarks>
    /// Joe, at a farm reading *"it has 3092"* beside a bar reading 245: *"help me understand where
    /// all of the food is?"* — 2,103 of it in a hunter's lodge. His rule for the limit: *"what is
    /// in storage, in transit to storage and in markets — what is available to villagers outside
    /// of their home storage."* So the three readers partition the village's food, the larders
    /// are outside the number the rules read, and the limit's sentence names the huts.
    /// </remarks>
    [Fact]
    public void TheVillagesFoodIsTheShelvesAndTheHutsAndTheSentenceSaysWhere()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace lodge = HuntingTests.RaiseALodgeFor(world);
        lodge.Store.Add(Goods.Meat, 2_000);
        foreach (Household household in world.Households)
        {
            household.Stockpile.Add(Goods.Produce, 300);
        }

        int shelves = world.FoodInGranaries();
        int huts = world.FoodWaitingInHuts();
        int larders = world.FoodInLarders();
        _output.WriteLine($"{shelves} on the shelves, {huts} in the huts, {larders} in the larders; the village holds {world.FoodTheVillageHolds()}");

        // The partition, and the larders outside it.
        Assert.Equal(shelves + huts, world.FoodTheVillageHolds());
        Assert.True(huts >= 2_000, "the lodge's meat is not counted as waiting in the huts");
        Assert.True(larders >= 300 * world.Households.Count, "the larders read less than was put in them");
        Assert.Equal(world.TotalFood() - world.Villagers.Sum(v => world.FoodIn(v.Carried)), shelves + huts + larders);

        // The sentence names the huts when the total is more than the shelves.
        world.SetStockLimit(Goods.Produce, 100);
        string? why = world.WhyTheVillageWantsNoMoreFood();
        _output.WriteLine(why ?? "(wants food)");
        Assert.NotNull(why);
        Assert.Contains($"{shelves} on the shelves and {huts} still in the huts", why, StringComparison.Ordinal);

        // A lodge two thirds full has outrun the carrying, and says so in armfuls.
        Assert.True(world.BufferIsSwollen(lodge));
        Assert.Equal((2_000 + config.CarryCapacity - 1) / config.CarryCapacity, world.ArmfulsWaitingIn(lodge));
    }

    [Fact]
    public void AVillageThatWantsNoMoreFoodSaysWhichReasonItIs()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        ColdStartTests.PlayTheOpening(world);
        loop.Step(config.TicksPerYear * 10);

        // Nothing wanted at all: the limit is the reason, and it names the limit.
        world.SetStockLimit(Goods.Produce, 0);
        string? byLimit = world.WhyTheVillageWantsNoMoreFood();
        _output.WriteLine($"limit 0: {byLimit}");

        Assert.NotNull(byLimit);
        Assert.Contains("asked the village to keep", byLimit);

        // Room to spare and a high limit: the village wants more, so there is nothing to say.
        world.SetStockLimit(Goods.Produce, 100000);
        _output.WriteLine($"limit 100000: {world.WhyTheVillageWantsNoMoreFood() ?? "(still wants food)"}");
        Assert.Null(world.WhyTheVillageWantsNoMoreFood());
    }

    /// <summary>⭐⭐ A met limit stops the work — and the forager is still a forager.</summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, 2026-08-27, in his own words:</b> <i>"food workers should stop working if the
    /// food limit is hit… the limits should dictate if a job keeps going. once limits are hit,
    /// the job is done until stores fall below limits."</i>
    /// </para>
    /// <para>
    /// <b>⛔ D216 landed half of this and the half it missed is the half he felt.</b> There are
    /// two reasons to go out — <em>the village is short</em> and <em>my family is short</em> —
    /// and only the first ever read the limit. A household below its own target kept sending its
    /// forager to the berry patch with the village's stores capped and full.
    /// </para>
    /// <para>
    /// ⭐ <b>And the seat is KEPT, which was Joe's call over shrinking the quota.</b> Cutting
    /// forager seats would churn people between trades, and proficiency accrues per trade — it
    /// would spend Phase 3's whole pillar to enforce a stock limit. They stay foragers with
    /// nothing to forage for, and fall through to labouring like anyone else who has declined
    /// their own work this tick (D87).
    /// </para>
    /// </remarks>
    [Fact]
    public void AMetFoodLimitStopsTheGatheringAndLeavesTheTrade()
    {
        // ⚠️ POSED WITHOUT THE FOUNDERS' TOOLS (D391). A tool adds a quarter to every gather,
        // and with them this village's stores are full to the brim by year ten (2,452 held) —
        // where the foragers are stood down for want of ROOM, a different rule from the limit
        // this guard is about, and "foragers held their trade" reads zero for a reason that is
        // not the one being tested. The claim is the limit's alone, so the tools stay in the cart.
        // ⚠️ AND POSED AT YEAR SEVEN, NOT TEN (D396). With the paths fading slower the same village
        // without tools reads 2,542 on a 2,500 granary at year ten — at the brim again, the
        // room rule again, foragers holding the trade for 0 ticks for the wrong reason. At year
        // seven it holds 2,384 with room to spare (measured year by year: 2,349 · 2,312 · 2,384
        // · 2,572 · 2,484 · 2,542 for years 5–10), and the premise is asserted below rather than
        // assumed, so the next slice that fills the fixture faster is told so by name.
        SimConfig config = VillageFixtures.Village with { CartTools = 0 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        ColdStartTests.PlayTheOpening(world);
        loop.Step(config.TicksPerYear * 7);
        Assert.True(
            world.FoodInGranaries() < world.GranaryCapacity(),
            $"The granary is at its brim ({world.FoodInGranaries()} of {world.GranaryCapacity()}) before the limit is set, "
            + "so the foragers would be stood down for ROOM, not for the limit — pose this guard earlier.");

        // ⚠️ Read the limit OUT of the village rather than writing a number in — an instrument
        // that assumes a simpler world measures something else. Half of what it already holds
        // is met by definition, whatever this fixture's economy happens to be doing.
        int holds = world.FoodTheVillageHolds();
        Assert.True(holds > 0, "The village stored no food in ten years, so this is vacuous.");
        world.SetStockLimit(Goods.Produce, holds / 2);
        Assert.True(world.FoodLimitIsMet(), "The limit must be met, or nothing is being tested.");

        // ⚠️ COUNTED ONLY ON THE TICKS WHERE THE LIMIT IS ACTUALLY MET, and the first draft of
        // this guard was not — it asserted zero gathering over two whole years and measured 327.
        // **That was the feature working, not failing.** The village eats, its stores fall back
        // through the limit, and foraging correctly resumes: *"until stores fall below limits"*
        // is the second half of Joe's own sentence. Asserting a flat zero would have demanded a
        // village that starves rather than one that obeys a cap.
        int gatheringWhileMet = 0;
        int foragerTicksWhileMet = 0;
        int ticksMet = 0;
        string note = string.Empty;

        // ⚠️ TRIPS IN FLIGHT ARE NAMED, NOT ESTIMATED (D385). With every gather pooled in the
        // granary and every larder filled from it an armful at a time, the granary hovers about
        // the limit — met, dipped by a fetch, met again — and a forager sent out on a dip is
        // still walking when it reads met. That was 17 % of forager-ticks against a bar of 10
        // that had been "a few percent is a trip in flight". So the trip in flight is tracked:
        // whoever was already out when the limit was last met is in flight until they come in,
        // and a gathering tick under a met limit counts only against somebody who was NOT.
        bool wasMet = false;
        var inFlight = new HashSet<int>();

        for (int tick = 0; tick < config.TicksPerYear * 2; tick++)
        {
            loop.StepOnce();

            bool met = world.FoodLimitIsMet();
            if (met && !wasMet)
            {
                inFlight.Clear();
                foreach (Villager out_ in world.Villagers)
                {
                    if (out_.Alive && out_.State is VillagerState.Gathering or VillagerState.TravelingToFood)
                    {
                        inFlight.Add(out_.Id);
                    }
                }
            }

            wasMet = met;
            if (!met)
            {
                continue;
            }

            ticksMet++;
            for (int i = 0; i < world.Villagers.Count; i++)
            {
                Villager v = world.Villagers[i];
                if (!v.Alive || KindOf(world, v) != JobKind.Forager)
                {
                    continue;
                }

                foragerTicksWhileMet++;
                bool gathering = v.State is VillagerState.Gathering or VillagerState.TravelingToFood;
                if (!gathering)
                {
                    inFlight.Remove(v.Id);
                }
                else if (!inFlight.Contains(v.Id))
                {
                    gatheringWhileMet++;
                }

                // ⚠️ THE LIMIT'S NOTE, ONCE SEEN, IS THE ONE KEPT (D396). This village has no tools
                // (posed above), so every forager's note reads *"Working without a tool"* on any
                // tick the limit note is not rewritten — the whole of winter, when nothing can be
                // gathered and the forager branch says nothing. Last-wins captured the winter
                // note and the claim below is about the limit's sentence, not the tool's.
                if (v.WorkNote.Length > 0 && (note.Length == 0 || v.WorkNote.Contains("asked the village to keep", StringComparison.Ordinal)))
                {
                    note = v.WorkNote;
                }
            }
        }

        _output.WriteLine(
            $"limit {holds / 2} against {holds} held: the limit was met on {ticksMet} ticks of "
            + $"two years, over which foragers held their trade for {foragerTicksWhileMet} ticks "
            + $"and gathered on {gatheringWhileMet}. The note reads: "
            + $"{(note.Length > 0 ? note : "(nothing)")}");

        Assert.True(ticksMet > 0, "The limit was never met, so this measures nothing.");

        // ⭐ THE SEAT IS KEPT. Somebody is still a forager while the limit stands — if the trade
        // had been emptied instead this would be zero and the assertion below would pass
        // vacuously, which is the failure mode that matters here.
        Assert.True(
            foragerTicksWhileMet > 0,
            "Nobody held the forager's trade at all, so the seat was cut rather than the work "
                + "stopped — which is the option Joe did not choose.");

        // ⭐⭐ AND THE WORK STOPS, for as long as the limit is met — bar the trip already underway.
        //
        // ⚠️ **THIS ASSERTED A FLAT ZERO AND D250's REST SPELL MOVED IT TO 12 OF 300, WHICH IS
        // THE FEATURE BEHAVING CORRECTLY.** `Decide` is not re-run mid-action, so a forager who
        // set out while the village still wanted food **finishes that trip** when the limit is
        // reached on the walk. *You do not drop an armful because a number was met while you were
        // carrying it* — and the flat zero only ever passed because the timing happened to align.
        //
        // ⭐ The claim is *the work stops*, not *the world stops mid-stride*. A few percent is a
        // trip in flight; a large share would mean the limit is not reaching the decision at all,
        // which is the bug this guard exists for.
        int gatheringShare = foragerTicksWhileMet == 0
            ? 0
            : gatheringWhileMet * 100 / foragerTicksWhileMet;

        _output.WriteLine($"  {gatheringShare}% of forager-ticks under a met limit were gathering on a trip that began under it");

        Assert.True(
            gatheringShare <= 10,
            $"{gatheringWhileMet} of {foragerTicksWhileMet} forager-ticks were spent gathering "
                + "while the limit was met. That is more than trips already in flight — the "
                + "limit is not reaching the decision.");

        // ⭐ AND IT SAYS WHY, naming the player's own number. A stop nobody can account for is
        // the silent stall §1.1 forbids — we would have traded a loop for a mystery.
        Assert.Contains("asked the village to keep", note);
    }

    private static JobKind? KindOf(SimWorld world, Villager villager)
    {
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.Workplaces[i].Id == villager.WorkplaceId)
            {
                return world.Workplaces[i].Kind;
            }
        }

        return null;
    }
}
