using System.Linq;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The player says how much — <c>specs/stock-limits-and-laborers.md</c> (D62).
/// </summary>
/// <remarks>
/// The guard that matters most here is <see cref="AVillagePlayedWithoutLimitsIsTheVillageThatCameBefore"/>:
/// the whole control has to be a no-op until somebody uses it, or every number the economy
/// has been derived and measured against moves for a feature nobody switched on.
/// </remarks>
public sealed class StockLimitTests
{
    private readonly ITestOutputHelper _output;

    public StockLimitTests(ITestOutputHelper output) => _output = output;

    // Captured from the tree at 12a4e46, BEFORE stock limits existed — which is the only
    // moment those numbers could honestly be taken. A golden is worthless if it is recorded
    // from the code it is meant to be guarding.
    //
    // RE-TAKEN ONCE, DELIBERATELY (D77). The emergency restock changes what villagers do,
    // so it changes the history — that is the point of it, not a regression, and a golden
    // that survived a real behaviour change would be measuring nothing. Re-taken here in
    // its own commit, with the old values kept below so the move is visible rather than
    // silent. This is the ONLY sanctioned reason to touch these: if a change moves them
    // and cannot say why in one sentence, the change is wrong, not the golden.
    //
    //   before the restock (D77): fixture 6240348392465688561, shipped 7734052055491107200
    //   before the supply fix (D79): fixture 13156470216450962100, shipped 13143121898114073713
    //   before the room fix (D81): fixture 14798520869458526773, shipped 3491502518393071633
    //   before stone and tools (D82): fixture 11013974864926656020, shipped 3491502518393071633
    //
    // D79 moved them a second time and for a bigger reason: the fuel quota had been reading
    // firewood in SHEDS, so a village whose fuel sat in a cart or a pile believed it had
    // none and put every spare hand on the chain forever. Correcting what the village can
    // see changes what it does, which changes its history. That is the golden working.
    //
    // D81 MOVED THE FIXTURE ONE AND NOT THE SHIPPED ONE, and the asymmetry is measured
    // rather than lucky. "Is there anywhere to put more food?" now asks every store that
    // takes food instead of only the granaries, so the two answers can only differ once a
    // village's food target climbs past its granary capacity — before that, both bars are
    // the target. Over fifty years the two answers differ on 681 of 24,000 fixture ticks
    // (2.8%, and the whole of the gap is the market's 95 spare units: bar 2945 against
    // 2850) and on 0 of 24,000 shipped ticks. So the shipped hash below is unchanged
    // because nothing about that run changed, not because it went unchecked.
    // D82 MOVED BOTH, and for a reason that is not a behaviour change at all: the state
    // hash now mixes EVERY good rather than three named ones, so two more numbers enter it
    // per store and per household the moment stone and tools exist — even where both are
    // zero. That is deliberate and it is the whole safety property: a good that is not
    // hashed is a good two different worlds can disagree about while reading identical
    // (D51). The founders' twenty tools are in there too.
    //
    // The refactor that preceded it — Stockpile becoming an indexed array — moved NOTHING,
    // which is what made it safe to do this on top.
    // D91 MOVED BOTH AGAIN, and for the plainest reason yet: the valley has stone and
    // iron in it now, terrain is in the state hash, so every village's fingerprint
    // changes on the first tick. Nothing about behaviour moved — nothing yet mines,
    // clears or spends either, and the seams are laid only over open grass so not one
    // tree or tile of water differs (SeamsTests.SeamsAreLaidOnlyOverOpenGround).
    //
    //   before the seams (D91): fixture 4673658241522176988, shipped 12610424914054256081
    //
    // ⭐ D96 MOVED BOTH, AND IT IS THE LARGEST BEHAVIOUR CHANGE ANY OF THESE HAS RECORDED —
    // because it is a conservation leak being closed rather than a rule being adjusted.
    // ArriveAt deposited a load into a store and zeroed the villager's arms without reading
    // what Stockpile.Add had ACTUALLY taken, so anything a full store refused ceased to exist.
    // Measured over these very fifty years: 17,451 food went into the granary's doorstep and
    // out of the world on the shipped config, 22,317 on the fixture.
    //
    // The consequence is a bigger, richer village, and it is measured rather than assumed:
    // food in stores 2,164 -> 3,640, population 29 -> 36, on firewood production that did not
    // move (6,654 ever cut -> 6,548) and with nobody frozen in either. It also required the
    // one change in StoreForTheLoad's ordering — room now outranks kind once the preferred
    // kind is full — without which a forager was still sent to a granary that could not take
    // the load. See specs/goods-on-the-ground.md §3.
    //
    // The goods now on the ground are hashed, but that is NOT what moved these: a village
    // that sets nothing down mixes nothing at all (GoodsOnTheGroundTests
    // .AVillageThatDroppedNothingIsHashedAsThoughTheGroundDidNotExist). What moved them is
    // that fifty years of history is genuinely different.
    //
    // ⭐ D102 MOVED BOTH AGAIN, and this one is the plainest of the lot: A HOUSE IS A
    // CONSTRUCTION SITE NOW. It used to take its timber straight out of the stores and set
    // HomePosition in the same tick; it is marked out, hauled to and worked on like every
    // other building, so a village fifty years old has built its houses on different ticks,
    // in a different order, with different hands. `specs/cold-start.md §7.1b` has carried
    // that inconsistency as open since Joe watched it, and this closes it.
    //
    // Also in here, and measured rather than assumed: builders now prefer what the PLAYER
    // marked over a house the village marked for itself (LabourAllocator.RankOf). Without it
    // the founding died — two house sites went in front of the woodcutter's hut, its timber
    // arrived at t364 against a winter starting at t360, and the shipped guard read 2 alive
    // and 2 frozen. With it the hut is back to t129/t172/t249, exactly where it was.
    //
    // ⭐ D108 MOVED BOTH AGAIN, and in one sentence: THE VILLAGE HAS A BUILDER'S HUT, AND A
    // CONSTRUCTION SITE IS AN ERRAND ITS CREW WALKS OUT TO RATHER THAN A PLACE ANYBODY IS
    // POSTED. Three things in that change the state hash, and all three are the point of it:
    // the founding gains a workplace, every site carries no seats and no workers, and builder
    // demand is the hut's seats rather than the sum of the sites'. Fifty years of a village
    // that builds that way is genuinely different history — which is these goldens working.
    //
    // Measured on the way in, because the cold start is the knife edge everything here dies
    // on: builders funded t121, hut logs t130, hut standing t173, staffed t241, first
    // firewood t251, against a winter at t360 — the same five ticks as before to within one.
    //
    //   before houses were built (D102): fixture 16616051083588314705,
    //                                    shipped 10176218336442890909
    // ⭐ AND THE VALLEY IS WOODED NOW (`forests-and-gathering.md` slice 1), which is the
    // plainest golden move of the lot: about 28% of the map is forest against the two stands
    // and fifty-odd tiles it had before. Homes are sited differently, laborers have vastly more
    // ground they could be asked to clear, and a village fifty years old has lived in a
    // different place. **The founding site keeps a four-tile glade** — measured, because
    // without it the pile stood at t67 instead of t1 and all four founders froze.
    //
    //   before houses were built (D102): fixture 16616051083588314705,
    //                                    shipped 10176218336442890909
    //   before the builder's hut (D108): fixture 16059676616951633422,
    //                                    shipped 15383236497282309390
    //   before the valley was wooded:    fixture 8100668875656351515,
    //                                    shipped 10300327615504873654
    //   before catchment was deleted:    fixture 7753167381277072647,
    //                                    shipped 3051795578767497062
    //
    // ⭐ RE-TAKEN FOR THE FENCE COMING DOWN (`forests-and-gathering.md §3`, D120), and the
    // one-sentence reason is: **a villager may now hold a job the ten-tile catchment used to
    // forbid, so who works where changes and everything downstream of it follows.**
    //
    // ⚠️ Worth recording because it is not what I expected: **the furthest commute anybody
    // actually holds is three tiles**, so no surviving villager is walking further than the
    // fence allowed. The hash moves anyway, because the fence also removed candidates during
    // matching — somebody previously left idle now takes a distant job, and fifty years of
    // that is a different village. A golden that moves for a reason you cannot state is a
    // wrong change; this one moved for a reason that took measuring to state correctly.
    // ⭐ RE-TAKEN FOR STEP C AND FOR TWO BUGS FOUND INSIDE IT (D152). These were already red
    // when this session opened — they carry everything step C did to the economy (D124–D141:
    // the sites retiring, regrowth, persistent harvest paint, the shed ceiling, the log
    // ambition) — and two more landed on top, both of which change every village whether or
    // not anybody sets a limit:
    //
    //   D142 — **every fell in the village was billing three times its own price.** The
    //          action's duration asked the mode rather than the errand, so from D137 a
    //          forester who walked to a tree and came home with logs was charged `PlantTicks`:
    //          12 ticks against a `cut_ticks` of 4. Fifty years of felling at the right speed
    //          is a different village.
    //   D144 — **firewood was destroyed once the woodyard filled.** `Add`'s return value was
    //          discarded, so every batch after a store filled ceased to exist. It goes on the
    //          ground now (D96's rule, which every other producer already had).
    //
    // D143, D145, D146, D148, D150 and D151 are NOT in these numbers, and it is worth saying
    // why: three were test-only, and the other three are controls that do nothing until the
    // player uses one — a met log limit, a felling toggle, a store filter — which is precisely
    // the no-op contract this guard exists to hold them to. **It held.**
    //
    //   before the sites retired: fixture 8652554140921204871,
    //                             shipped 2151271050042369210
    // ⭐ RE-TAKEN FOR THE BIRTH GATE (D153), and the one sentence is: **a house holds five
    // people instead of seven, and births no longer ask a family's own larder or its own
    // firewood.** Fifty years of a village that breeds on those terms is a different village.
    //
    // Measured before the change rather than after, which is what made it a decision: the two
    // deleted terms refused 6-10% and 0-1% of household-years against the granary term's
    // 42-70%, and **nobody starved or froze in any arm, before or after.** The cap moving 7 -> 5
    // is the part that does the work — it takes the shipped village from **dying out entirely
    // over 300 unattended years (final 0) to ending at 20**, still with nobody starving.
    //
    //   before the birth gate lost its household terms: fixture 15720299932978060475,
    //                                                   shipped 9131366701299548068
    // ⭐ RE-TAKEN AGAIN FOR THE FOOD GATE (D155): `birth_food_percent` 80 -> 60, so the village
    // has children sooner and fifty years of it is a different history.
    //
    // ⚠️ AND ONLY THE SHIPPED ONE MOVED, WHICH IS WORTH RECORDING RATHER THAN SHRUGGING AT.
    // The fixture's fifty years are byte-identical: it starts with buildings and a stocked
    // cart, so its granary clears the old 80% bar early anyway and lowering the bar changes
    // nothing it does in that window. The shipped file is a cold start — it spends those fifty
    // years near the bar, which is exactly the state Joe was playing when he asked why nobody
    // was having children.
    //
    //   before the food gate was loosened: shipped 16713992210504644002
    // ⭐ RE-TAKEN FOR THE WORKING AGE (D156): `adult_age` 15 -> 12, so an uneducated child takes
    // work three years earlier — and eats a full meal three years earlier too. Both arms move,
    // because that changes who is available for every job in every one of the fifty years.
    //
    //   before children worked at twelve: fixture 13172587746925380233,
    //                                     shipped 1066427617710206388
    //
    // ⭐ NOT RE-TAKEN FOR THE FARM (D162), AND THE REASON IS THE INTERESTING PART. The handoff
    // for that slice said in terms that **the goldens are supposed to move once a farmer sows**
    // — every crop step before it had been provably invisible, and that was expected to end. It
    // did not, and the explanation was measured rather than shrugged at: **neither of these
    // villages ever places a farmhouse**, so nothing sows, no `Workplace.Store` is ever written
    // to, and `LabourQuota`'s new arm takes zero hands because `FarmerSeatsWithGroundToWork` is
    // zero with no farm in the world.
    //
    // ⚠️ SO THEY ARE UNMOVED BECAUSE THEY DO NOT COVER IT, NOT BECAUSE IT IS A NO-OP — which is
    // D157's finding, restated by the very next slice. **A green golden can mean "not
    // covered".** What covers the farm is `FarmGoldenTests`, which paints, clears, sows and
    // reaps in one run and is the first guard in this suite ever to reach `NearestHarvest`.
    //
    // ⭐⭐ RE-TAKEN FOR THE JITTER (D163), AND THIS IS THE KIND OF MOVE THE GUARD EXISTS FOR.
    // **A villager who came in to get warm now stays until they are warm.** Since D45 anyone
    // reaching a hearth still carrying logs was flipped straight back to `HaulingToStore` and
    // walked out on the next tick — one tick at the fire, every time, for the whole of every
    // winter — so nobody with their arms full ever thawed at all. Fifty years of villages that
    // warm up properly is a different fifty years: they spend ticks indoors they used to spend
    // walking, their loads arrive later, and every downstream decision follows.
    //
    // **⭐ It is also the answer to the question the farm slice left hanging.** D162 recorded
    // these two as *unmoved because they do not cover the farm* — and one commit later they
    // moved for a change that touches every villager in every winter, which is the same guard
    // saying the opposite thing correctly both times. That is what a golden is for: it is
    // silent about what it does not reach and loud about what it does.
    //
    //   before the jitter was fixed: fixture 11001298307494045081,
    //                                shipped 10000897820648583606
    //
    // ⭐ RE-TAKEN FOR THE FETCH (D166), and the one sentence is: **nobody walks to a store for a
    // trivial amount any more, and a fetch fills the armful instead of coming back for the
    // firewood.** Both reach every household in every village, which is why these two moved and
    // why the fix was worth making — measured over thirty years, fetch legs fall 153 → 81 and
    // tile flips 211 → 143, with the population identical and nobody starving or freezing.
    //
    //   before "worth the trip": fixture 17969363278213194222,
    //                            shipped 17964703903020390741
    //
    // ⭐⭐ RE-TAKEN FOR PER-SITE YIELD (D178) — AND THE REASON IS NOT THE ONE THE SPEC EXPECTED,
    // WHICH IS WORTH THE THREE LINES. `per-site-yield.md §6` predicted these two would NOT move:
    // **neither village ever places a farmhouse** (D162), so nothing here reaches the crop yield
    // or the sowing cap. That reasoning was right and it was not the whole story.
    //
    // **They move because soil does.** `StateHash` mixes the map into every village hash, so a
    // change to the ground moves every history in the game whether or not anybody farms it. The
    // spec asked the question in advance and said *"if they do, that is a finding, not a
    // nuisance"* — this is the finding, and it is D157's rule from the other side: **a golden is
    // silent about what it does not reach and loud about what it does**, and what these two
    // reach is the valley itself.
    //
    //   before ground was worth going to: fixture 13985157942708541633,
    //                                     shipped 17566836300829537614
    //
    // ⭐⭐ RE-TAKEN FOR THE PROFICIENCY SUBSTRATE (D181), AND THIS ONE MOVED WITHOUT ANYBODY
    // DOING ANYTHING DIFFERENTLY — which is the opposite of every re-take above it and is worth
    // the paragraph. Villagers now carry what they have put into each trade, it is hashed, and
    // it grows from the first tick. **Nothing reads it**: no behaviour anywhere consults skill
    // until landing 2. So these two moved for the counters alone.
    //
    // **That is asserted rather than claimed** — `SkillTests.FiftyYearsOfVillageAndOnlyThe-
    // CountersMoved` recomputes these same two runs with `StateHash.ComputeIgnoringSkills` and
    // gets the two numbers directly below, byte for byte. If a future change to the substrate
    // makes somebody walk somewhere different, that guard goes red and this one stays green,
    // which is the whole point of having both.
    //
    // ⛔ AND IT IS WHY `skills-catalog.md §11.2.1`'s *"provable no-op: goldens unmoved"* was
    // corrected rather than the guard weakened. Hashing new state that grows and keeping a
    // state-hash golden byte-identical are mutually exclusive; the spec had reasoned by analogy
    // from `crops-and-orchards.md`, where the generator never produces the new terrain values.
    //
    //   before people got better at things: fixture 18149215200660116896,
    //                                       shipped 2960234095731849111
    //
    // ⭐⭐ RE-TAKEN AGAIN, SAME DAY, FOR SKILL DECAY BEING DELETED (D183, Joe: *"let's give to
    // the player, not punish or decay"*). Proficiency now only ever goes up, and it carries a
    // second counter — the honest calendar ticks the panel quotes, beside the weighted work
    // mastery reads, because a tick out on the job is worth more than a tick waiting for one.
    //
    // **Still nobody doing anything differently.** `ComputeIgnoringSkills` returns the same two
    // numbers it did before ANY of this landed — 18149215200660116896 and 2960234095731849111 —
    // so both re-takes today moved these for the counters and for nothing else.
    //
    //   before decay was deleted: fixture 4887770829745874605,
    //                             shipped 5305142457694342096
    //
    // ⭐⭐ RE-TAKEN FOR MASTERY BITING (D187) -- and this is the FIRST skill re-take that is a
    // real behaviour change. Landings 1 and 2's earlier moves were counters; this one is people
    // working faster. A master takes half the ticks over an action, rounded up, so fifty years
    // of a village whose founders got good at things is a genuinely different fifty years --
    // measured at 23 alive against 29 on the shipped seed at a century.
    //
    // ⚠️ `SkillTests.FiftyYearsOfVillageAndOnlyTheCountersMoved` still asserts the OLD values
    // through `ComputeIgnoringSkills`, posed with the bonus at zero -- so the claim that the
    // substrate alone changes nothing is still standing, and still checkable, beside this.
    //
    //   before mastery bit: fixture 17883694128790877833,
    //                       shipped 15628752506897642520
    //
    // ⭐⭐ RE-TAKEN BECAUSE THE MARKETER STOCKS THE MARKET (D197,
    // `storage-and-distribution.md §14.8`). A marketer now carries goods to the market's own
    // store in slack time, so goods sit in a different building and every history downstream
    // differs.
    //
    // ⚠️ THIS MOVED **EVERY** VILLAGE GOLDEN, NOT JUST THE FARM'S — unlike D194 the day before,
    // which moved only the seam. The reason is worth keeping: **every village in the suite has a
    // market in it**, where only one of them plants a farmhouse. *Silent about what they do not
    // reach, loud about what they do* (D157, D162) — and this one they all reach.
    //
    //   before the market was stocked: 11057161405161342300
    private const ulong FixtureFiftyYearHash = 5407508656652631583UL;
    //
    // ⭐ THE SHIPPED ONE ALONE MOVES FOR THE CONSUMPTION CHANGE (D189, Joe): food_per_meal
    // 5 -> 4 and firewood_burn_interval_days 4 -> 3. The FIXTURE hash above is untouched,
    // correctly -- `VillageFixtures.Village` derives its own economy and never reads the
    // shipped file's two numbers, so this pair moving apart is the two configs being two
    // configs rather than drift.
    //
    //   before eating less and burning more: shipped 16062803390206118870
    //
    // ⭐⭐ RE-TAKEN FOR THE MIXED FOUNDING AND THE SEEDED RHYTHM (D190) — Phase 3 landing 3, and
    // the commit that discharges D28. The founders arrive as a master, a journeyman and two
    // novices with seeded trades, and every villager is drawn a personal rhythm at birth that
    // sets their first step and their first hunger a little apart from everybody else's.
    // **No two founders run the same program from tick 0**, so fifty years of village is a
    // different fifty years from the first tick onward.
    //
    //   before the founders were people: fixture 5402933120067190351,
    //                                    shipped 16150256378240105365
    //
    // ⭐ RE-TAKEN FOR ELDERS EATING A DEPENDANT'S SHARE (D191, Joe). An elder used to eat a
    // full adult portion while producing at vigour_min_percent -- not as a ruling about
    // ageing, but because MealCostFor had one branch and it tested for Child. Fifty years of
    // a village whose old people eat half is a different fifty years.
    //
    //   before elders ate like children: fixture 2925726946142789484,
    //                                    shipped 9133620442171355746
    //
    // ⭐ RE-TAKEN FOR A FIVE-DAY THAW (D192, Joe: "fire warm up should be much faster"). A
    // hearth used to give back exactly what open ground took, so coming back from the brink
    // was fifteen days -- half a winter spent thawing. It is five now, so villagers spend
    // materially more of every winter working and fifty years of that is a different fifty.
    //
    //   before the fire got warmer: fixture 18174430941982640321,
    //                               shipped 7791088175599810974
    //   before the market was stocked: 15960035659211257615
    private const ulong ShippedFiftyYearHash = 14089077723027009078UL;

    // ---------------------------------------------------------------
    //  The default is a no-op, and this is the whole slice's licence
    // ---------------------------------------------------------------

    /// <summary>
    /// With nothing set, the run is byte-identical to the one before this feature existed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Stated in hashes rather than in prose, because prose does not fail a build.</b>
    /// Every derived number in <see cref="VillageEconomy"/>, every acceptance band in
    /// <c>ShippedConfigTests</c>, and the 86%-idle winter baseline the next guard is written
    /// against were all measured on a village with no limits. If adding the control moves
    /// any of that, the measurements stop describing the game and nobody finds out for a
    /// phase.
    /// </para>
    /// <para>
    /// It is also what makes the null default (see <see cref="StockLimits.For"/>) load-bearing
    /// rather than merely tidy: null is not "zero on a nicer screen", it is the absence of an
    /// opinion, and the absence of an opinion has to be indistinguishable from the absence of
    /// the feature.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(false, FixtureFiftyYearHash)]
    [InlineData(true, ShippedFiftyYearHash)]
    public void AVillagePlayedWithoutLimitsIsTheVillageThatCameBefore(bool shipped, ulong expected)
    {
        // The established village either way: this golden is about stock limits being a
        // no-op, and it was captured before the cold start existed. ColdStartTests owns the
        // founding.
        SimConfig config = shipped ? ShippedConfig.Established() : VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());

        loop.Step(config.TicksPerYear * 50);

        ulong actual = StateHash.Compute(loop.World);
        _output.WriteLine($"{(shipped ? "shipped" : "fixture")}: 50y hash {actual}");

        Assert.Equal(expected, actual);
    }

    // ---------------------------------------------------------------
    //  Null is not zero
    // ---------------------------------------------------------------

    /// <summary>
    /// <em>"No opinion"</em> and <em>"stop, I mean it"</em> are different worlds.
    /// </summary>
    /// <remarks>
    /// D51 records this trap one control over: two states that read alike to the hash let a
    /// determinism test pass across a real divergence. Here it would be worse than a false
    /// pass — a village told to stop cutting wood and a village never asked would be the same
    /// run, so the control would appear to work while doing nothing.
    /// </remarks>
    [Fact]
    public void NoOpinionAndAnExplicitZeroAreDifferentStates()
    {
        SimConfig config = VillageFixtures.Village;

        SimWorld noOpinion = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

        SimWorld explicitZero = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        explicitZero.StockLimits.Set(Goods.Logs, 0);

        Assert.Null(noOpinion.StockLimits.For(Goods.Logs));
        Assert.Equal(0, explicitZero.StockLimits.For(Goods.Logs));
        Assert.NotEqual(StateHash.Compute(noOpinion), StateHash.Compute(explicitZero));
    }

    /// <summary>Clearing an opinion puts the world back exactly where it was.</summary>
    /// <remarks>
    /// The other half of the above: if null were merely "some other number", setting and then
    /// clearing a limit would leave a fingerprint behind, and a player who changed their mind
    /// would be playing a subtly different village from one who never touched it.
    /// </remarks>
    [Fact]
    public void ClearingALimitLeavesNoTrace()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

        ulong before = StateHash.Compute(world);

        world.StockLimits.Set(Goods.Firewood, 240);
        Assert.NotEqual(before, StateHash.Compute(world));

        world.StockLimits.Set(Goods.Firewood, null);
        Assert.Equal(before, StateHash.Compute(world));
    }

    // ---------------------------------------------------------------
    //  The control itself
    // ---------------------------------------------------------------

    /// <summary>Every good the game has can be limited, so adding one cannot forget to.</summary>
    /// <remarks>
    /// <para>
    /// <b>This used to guard a hand-written list and it fired exactly once</b> — when stone
    /// and tools landed (D82), which is the moment it was written for. The list reads the
    /// enum now, so the containment half is true by construction and would be a tautology
    /// dressed as a test.
    /// </para>
    /// <para>
    /// What is left is not: a limit has to be <em>settable and readable back</em> for every
    /// good, which is what catches the array behind <see cref="StockLimits.Kinds"/> being
    /// sized or indexed against a stale count — an <c>IndexOutOfRangeException</c> in the
    /// middle of a run, or worse, two goods sharing a slot.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryGoodTheGameHasCanBeLimited()
    {
        var limits = new StockLimits();
        Goods[] all = Enum.GetValues<Goods>();

        Assert.Equal(all.Length, StockLimits.Kinds.Count);
        Assert.Equal(all.Length, Stockpile.Kinds);

        // A distinct limit per good, then read every one of them back: two goods sharing a
        // slot would pass a set-and-read of one good and fail here.
        for (int i = 0; i < all.Length; i++)
        {
            limits.Set(all[i], 100 + i);
        }

        for (int i = 0; i < all.Length; i++)
        {
            Assert.Equal(100 + i, limits.For(all[i]));
        }
    }

    /// <summary>Two villages differing only in one good must not hash alike.</summary>
    /// <remarks>
    /// <b>The property that justifies D82 moving both goldens.</b> The state hash mixed
    /// three named goods, so a world holding stone and a world holding none read as the same
    /// run — D51's trap, where two different states are indistinguishable to the one test
    /// that exists to tell states apart. It mixes every good by index now, and this is what
    /// says so for goods that nothing yet produces.
    /// </remarks>
    [Theory]
    [InlineData(Goods.Stone)]
    [InlineData(Goods.Tools)]
    public void AGoodNothingProducesIsStillPartOfTheWorld(Goods goods)
    {
        SimConfig config = VillageFixtures.Village;

        SimWorld without = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        SimWorld with = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

        StoreBuilding shed = with.AnyStoreOf(StoreKind.Shed);
        Assert.True(shed.Accepts(goods), $"A shed must hold {goods} — it is a material.");
        Assert.Equal(7, shed.Store.Receive(goods, 7));

        Assert.NotEqual(StateHash.Compute(without), StateHash.Compute(with));
    }

    /// <summary>A limit is met when the village holds at least that much.</summary>
    [Fact]
    public void ALimitIsMetAtTheNumberAndNotBefore()
    {
        var limits = new StockLimits();

        Assert.False(limits.IsMet(Goods.Logs, 1_000_000));

        limits.Set(Goods.Logs, 200);

        Assert.False(limits.IsMet(Goods.Logs, 199));
        Assert.True(limits.IsMet(Goods.Logs, 200));
        Assert.True(limits.IsMet(Goods.Logs, 201));
    }

    /// <summary>A negative limit is a badly typed zero, not an error to argue about.</summary>
    [Fact]
    public void ANegativeLimitIsReadAsStopMakingThis()
    {
        var limits = new StockLimits();
        limits.Set(Goods.Food, -20);

        Assert.Equal(0, limits.For(Goods.Food));
    }

    // ---------------------------------------------------------------
    //  Laborers — who they are, since it turns out not to be what they do
    // ---------------------------------------------------------------

    /// <summary>A laborer is an able adult no workplace wants — and a child never is.</summary>
    /// <remarks>
    /// <para>
    /// The whole of what a laborer is today. Spec §5.2 measured both errands this slice meant
    /// to give them at <b>0.0% of ticks</b> — producers carry their own output and builders
    /// fetch their own materials — so inventing hauling for them would have been D52's
    /// make-work with a new name. They get work in slice B, when there is something on the
    /// map to gather.
    /// </para>
    /// <para>
    /// Guarded as a <em>reader</em>: it must agree with the roster at every moment, because
    /// the alternative — a stored flag — is the bookkeeping-that-drifts shape this project
    /// keeps paying for.
    /// </para>
    /// </remarks>
    [Fact]
    public void ALaborerIsAnAbleAdultNobodyHasWorkFor()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        loop.Step(config.TicksPerYear * 30);

        int laborers = 0;
        foreach (Villager villager in world.Villagers)
        {
            if (!villager.Alive)
            {
                continue;
            }

            Assert.Equal(villager.CanWork && !villager.HasJob, villager.IsLaborer);

            if (villager.IsLaborer)
            {
                laborers++;
                Assert.NotEqual(LifeStage.Child, villager.LifeStage);
            }
        }

        _output.WriteLine($"{laborers} laborers of {world.Population} alive after 30 years");
    }

    // ---------------------------------------------------------------
    //  It binds
    // ---------------------------------------------------------------

    /// <summary>A limit stops the work, and the village settles at the number asked for.</summary>
    /// <remarks>
    /// <para>
    /// The behavioural claim of the whole slice. Asserted as <em>the stock stops climbing</em>
    /// rather than as <em>the quota reads zero</em>, because a quota is a statement about what
    /// the village wants and the player asked about the shed.
    /// </para>
    /// <para>
    /// The band is generous on purpose. Work already in flight lands after the limit is met —
    /// a woodcutter mid-batch finishes it — so the stock overshoots a little and then holds.
    /// Asserting an exact number would be asserting that nobody was carrying anything at the
    /// moment the limit bit, which is a property of the tick it was read on rather than of
    /// the control.
    /// </para>
    /// </remarks>
    [Fact]
    public void AFirewoodLimitStopsTheWoodcutters()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        // Let the village get on its feet first, then ask it to stop well below where it
        // would otherwise settle.
        loop.Step(config.TicksPerYear * 20);
        int unlimited = world.FirewoodInSheds();

        world.SetStockLimit(Goods.Firewood, 40);
        loop.Step(config.TicksPerYear * 20);

        int limited = world.FirewoodInSheds();
        _output.WriteLine($"firewood in sheds: {unlimited} unlimited, {limited} capped at 40");

        // ⭐⭐ IT ASSERTS THAT PRODUCTION STOPPED, WHICH IS STRONGER THAN THE BOUND IT REPLACES
        // (D192). The old guard allowed one batch of overshoot, which was a guess at how much
        // work can be in flight when the limit bites — and a faster thaw (Joe, D192) put more
        // winter hours into the chain and crossed it at 107 against 90.
        //
        // ⛔ THE FIRST FIX WAS ALSO A GUESS AND WAS ALSO WRONG: `seats × split` came out at 50
        // for a fixture that derives ONE woodcutter seat, so it did not even cover the observed
        // number. *Two guesses at a tolerance is the point at which the tolerance is the wrong
        // thing to assert.*
        //
        // **What the limit promises is that the work stops, not that the stock lands on a
        // particular number** — so that is what this checks: the capped village is far below
        // the uncapped one, and twenty more years does not move it. A limit that merely slowed
        // production would keep climbing here and pass any fixed bound generous enough to
        // absorb the overshoot.
        int settled = world.FirewoodInSheds();
        loop.Step(config.TicksPerYear * 20);
        int stillSettled = world.FirewoodInSheds();

        int frozen = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Cold);
        _output.WriteLine(
            $"twenty years later: {stillSettled} firewood, population {world.Population}, "
            + $"{frozen} ever frozen");

        Assert.True(
            limited < unlimited / 2,
            $"Firewood settled at {limited} against {unlimited} uncapped — the limit is not "
            + "biting at all.");

        Assert.True(
            stillSettled <= settled + config.FirewoodPerSplit,
            $"Firewood went {settled} → {stillSettled} over twenty years under a limit of 40. "
            + "Production did not stop, it merely slowed — which is the bug D139 records one "
            + "job over, where a forester the player posted keeps felling past the number in "
            + "the box.");

        // ⭐⭐ AND NOBODY FROZE FOR IT, WHICH IS THE HALF WORTH ASSERTING MOST. A stock limit is
        // the player saying *stop*, and D62's whole claim is that the player sets a ceiling
        // while the village goes on keeping itself alive. **The sheds do drain to nothing here**
        // — households hold their own fuel, and forty in a shed was never what kept anyone warm
        // — so a guard that only watched the shed would read this as a catastrophe. It is not:
        // thirty people, forty years, nobody cold.
        Assert.Equal(0, frozen);
        Assert.True(
            world.Population >= config.StartingPopulation,
            $"The village fell to {world.Population} under a firewood limit. A ceiling the "
            + "player sets must not be a way to kill them.");
    }

    /// <summary>
    /// Anti-vacuity (D7): the village must actually want to pass the limit it is held under.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A cap that binds nothing proves nothing. If an unlimited village never reached 40
    /// firewood anyway, the test above would pass on a village that simply never got going —
    /// which is D52's lesson exactly: a comparative test has two villages and either can be
    /// the broken one.
    /// </para>
    /// <para>
    /// <b>⚠️ THE PEAK, NOT THE STOCK AT ONE INSTANT, AND D96 IS WHY.</b> This read
    /// <c>FirewoodInSheds()</c> after exactly forty years — which is tick 19,200, which is the
    /// first tick of spring, which is the annual <em>trough</em>: the village has just burned
    /// a winter's fuel. Closing the conservation leak (D96) left the village with 40% more
    /// food, so it grew from 29 people to 36 on the same firewood production — 6,548 ever cut
    /// against 6,654 — and a bigger village ends winter on a thinner stock. It read 20.
    /// </para>
    /// <para>
    /// <b>The old number was luck and the new one is not a fuel failure:</b> nobody freezes in
    /// either village across fifty years. The word this guard needs is <em>passed</em>, and a
    /// maximum is what "passed" means — so it now measures the thing it always claimed to.
    /// A guard whose answer depends on which tick of the year it stops on was never asserting
    /// what its name says.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheUnlimitedVillageReallyWouldHavePassedThatLimit()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());

        int peak = 0;
        for (int tick = 0; tick < config.TicksPerYear * 40; tick++)
        {
            loop.StepOnce();
            int held = loop.World.FirewoodInSheds();
            if (held > peak)
            {
                peak = held;
            }
        }

        _output.WriteLine(
            $"unlimited village peaked at {peak} firewood in forty years, and holds "
            + $"{loop.World.FirewoodInSheds()} at the end of the fortieth winter");

        Assert.True(peak > 40, $"Only ever {peak} firewood, so a limit of 40 binds nothing.");
    }

    // ---------------------------------------------------------------
    //  Obeyed, and said out loud
    // ---------------------------------------------------------------

    /// <summary>A limit under the survival floor is accepted, and the player is told.</summary>
    /// <remarks>
    /// Both halves matter and they are asserted together on purpose. Accepting silently is
    /// the failure mode §7 of the spec names first; refusing is the game arguing with the
    /// player. The verdict type is the same one placement uses (D43), so the two read alike.
    /// </remarks>
    [Fact]
    public void ALimitBelowWhatTheVillageNeedsIsObeyedAndSaidOutLoud()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        loop.Step(config.TicksPerYear * 10);

        PlacementVerdict verdict = world.SetStockLimit(Goods.Food, 5);

        Assert.True(verdict.Allowed, "The village must do as it is told.");
        Assert.False(string.IsNullOrWhiteSpace(verdict.Warning), "It must also say something.");
        Assert.Equal(5, world.StockLimits.For(Goods.Food));
        _output.WriteLine(verdict.Warning);
    }

    /// <summary>A sensible limit is not nagged about.</summary>
    /// <remarks>
    /// The anti-vacuity half of the warning: if every limit warned, the warning would carry
    /// no information and the player would learn to ignore the one that matters.
    /// </remarks>
    [Fact]
    public void AGenerousLimitPassesWithoutComment()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        loop.Step(config.TicksPerYear * 10);

        int floor = VillageEconomy.SurvivalFloorFor(
            config, Goods.Food, world.Population, world.Households.Count);

        PlacementVerdict verdict = world.SetStockLimit(Goods.Food, floor * 4);

        Assert.True(verdict.Allowed);
        Assert.True(
            string.IsNullOrWhiteSpace(verdict.Warning),
            $"A generous limit said: {verdict.Warning}");
    }

    /// <summary>
    /// A log limit set above what the village spends makes it want timber it is not spending.
    /// </summary>
    /// <remarks>
    /// <para>
    /// D130, and Joe's words for it: <i>"the village should want timber it isn't spending if
    /// the user sets a limit above what the village uses — that is a stockpile/growth play
    /// tool for the user."</i> Every other limit in this file is a <b>ceiling</b>; this is the
    /// same number read as a <b>target</b>, and it is the only control in the game that asks
    /// the village to do more work rather than less.
    /// </para>
    /// <para>
    /// <b>It exists because a cheaper habit is not a woodpile.</b> Foresters were wanted only
    /// to feed the fuel chain and the houses already marked, so the village cut exactly what
    /// it was about to burn and never accumulated. Joe hit the wall this leaves — a forester's
    /// hut stuck at 21 of its 25 logs, so no seats, so no foresters, so no logs. Making fuel
    /// cheaper made it <em>worse</em>: quadrupling <c>firewood_per_split</c> cut fuel from 60%
    /// of all timber to 41% and dropped total production 365 → 174, because the fuel chain was
    /// the only thing employing foresters at all.
    /// </para>
    /// <para>
    /// <b>⚠️ THE POPULATION ASSERT IS NOT DECORATION.</b> Taken uncapped the ambition filled
    /// every forester seat, and the hands it drank were never idle — they are the labourers
    /// who carry food to the larders and firewood to the homes. The woodpile worked and the
    /// village halved, ten alive down to four. So the ambition is capped at half the hands
    /// left, the same margin building uses, and this guard fails if that cap is ever lifted.
    /// </para>
    /// </remarks>
    [Fact]
    public void ALogLimitAboveWhatTheVillageSpendsIsAnAmbitionAndNotAceiling()
    {
        SimConfig config = VillageFixtures.Village;
        int years = config.TicksPerYear * 12;

        SimLoop content = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        content.Step(years);

        SimLoop ambitious = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        ambitious.World.SetStockLimit(Goods.Logs, 200);
        ambitious.Step(years);

        int without = content.World.LogsInSheds();
        int with = ambitious.World.LogsInSheds();
        _output.WriteLine(
            $"logs held after 12 years: {without} with no opinion, {with} asked for 200. "
            + $"alive: {content.World.Population} and {ambitious.World.Population}.");

        Assert.True(
            with > without,
            $"A village asked for 200 logs held {with}, no better than the {without} it "
            + "would have kept anyway. The limit is still only a ceiling.");

        // And the stockpile is a want, not a need: it must not be built out of the hands
        // that keep everybody fed.
        Assert.True(
            ambitious.World.Population >= content.World.Population,
            $"Stockpiling cost lives: {ambitious.World.Population} alive against "
            + $"{content.World.Population} in the same village that never bothered.");
    }

    // ---------------------------------------------------------------
    //  ⭐ A limit the player set outranks a hut the player staffed (D145)
    // ---------------------------------------------------------------

    /// <summary>
    /// Post somebody at every workplace of a kind, the way the player does, and hand back
    /// how many seats were filled.
    /// </summary>
    /// <remarks>
    /// <b>This is the half the quota cannot reach, and it is where these bugs live.</b>
    /// <c>LabourQuota</c> decides how many of a job the village <em>asks for</em>; since D109
    /// the player's own number is what staffs the building. So every guard that runs an
    /// unattended village is testing the planner, and the player is testing the doer.
    /// </remarks>
    private static int StaffEvery(SimWorld world, JobKind kind)
    {
        int seats = 0;
        foreach (Workplace workplace in world.Workplaces)
        {
            if (workplace.Kind == kind && !workplace.IsSite)
            {
                world.SetStaffing(workplace, workplace.Capacity);
                seats += workplace.Capacity;
            }
        }

        return seats;
    }

    /// <summary>⭐ A met log limit stops a forester the player posted.</summary>
    /// <remarks>
    /// <b>D139's bug, one job over</b> — found by sweeping the controls after D144 rather than
    /// by Joe hitting it. The check D139 added lives in the woodcutter's branch and nowhere
    /// else, while <c>LabourQuota.StoppedByAStockLimit</c> has arms for <em>both</em> timber
    /// jobs. So the planner obeys a Logs limit and the doer never heard of it, and a forester
    /// the player staffed fells for ever past the number in the box.
    /// </remarks>
    [Fact]
    public void ALogLimitStopsAForesterThePlayerPosted()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        loop.Step(config.TicksPerYear * 20);
        Assert.True(StaffEvery(world, JobKind.Forester) > 0, "Nowhere to post a forester.");

        int atTheLimit = world.LogsInSheds();
        world.SetStockLimit(Goods.Logs, atTheLimit + 20);
        loop.Step(config.TicksPerYear * 15);

        int held = world.LogsInSheds();
        _output.WriteLine(
            $"logs in store: {atTheLimit} when the limit of {atTheLimit + 20} was set, "
            + $"{held} fifteen years later.");

        // Generous, for the reason AFirewoodLimitStopsTheWoodcutters gives: work in flight
        // lands after the limit bites, so the stock overshoots a little and then holds.
        Assert.True(held <= atTheLimit + 20 + (config.CutYield * 4),
            $"Logs settled at {held} against a limit of {atTheLimit + 20}. A forester the "
            + "player posted is still felling past the number in the box (D139, one job over).");
    }

    /// <summary>⭐ And a met food limit stops a gatherer the player posted.</summary>
    /// <remarks>
    /// <para>
    /// The same gap on the third good. <c>LabourQuota</c> zeroes foragers outright when
    /// <c>IsMet(Food, FoodInGranaries())</c>, so the intent that a food limit stops the
    /// gathering is already the design — it simply never reached the branch where gathering
    /// actually happens.
    /// </para>
    /// <para>
    /// <b>⚠️ And it is the one that can cost lives, so it is obeyed rather than argued with
    /// (D42).</b> The saying-so happens once, when the limit is set — <c>SetStockLimit</c>
    /// warns about a number below the survival floor and then does as it is told. A control
    /// that quietly declines to work on the one good that matters is worse than one that
    /// obeys: the player would have no way to tell it apart from a bug.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// <b>⚠️ TWO ARMS, BECAUSE THIS ONE CAN PASS VACUOUSLY AND ALMOST DID.</b> Gathering is
    /// gated on <c>needsFood</c> — my larder is short, <em>or</em> the granary is below what
    /// the village has ROOM for — so a granary near its capacity stops the gatherers whatever
    /// the player asked for. A one-armed version of this guard was green before the fix, and
    /// it was green because <c>FoodTheVillageHasRoomFor</c> was already binding. **The control
    /// arm is what makes the number mean the limit rather than the ceiling** — the same shape
    /// as <c>TheUnlimitedVillageReallyWouldHavePassedThatLimit</c>, and D52's lesson that a
    /// comparative test has two villages and either can be the broken one.
    /// </remarks>
    [Fact]
    public void AFoodLimitStopsAGathererThePlayerPosted()
    {
        SimConfig config = VillageFixtures.Village;
        int settle = config.TicksPerYear * 20;
        int after = config.TicksPerYear * 10;

        SimLoop capped = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        capped.Step(settle);
        Assert.True(StaffEvery(capped.World, JobKind.Forager) > 0, "Nowhere to post a gatherer.");

        int start = capped.World.FoodInGranaries();
        int limit = start + 100;
        capped.World.SetStockLimit(Goods.Food, limit);
        capped.Step(after);

        SimLoop free = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        free.Step(settle);
        StaffEvery(free.World, JobKind.Forager);

        int freePeak = 0;
        for (int tick = 0; tick < after; tick++)
        {
            free.StepOnce();
            freePeak = System.Math.Max(freePeak, free.World.FoodInGranaries());
        }

        _output.WriteLine(
            $"food in granaries: {start} when the limit of {limit} was set, "
            + $"{capped.World.FoodInGranaries()} ten years later; "
            + $"the same village uncapped peaked at {freePeak}.");

        Assert.True(freePeak > limit,
            $"An uncapped village never got past {freePeak} against a limit of {limit}, so the "
            + "limit bound nothing and this guard proves nothing (D7). The granary's own "
            + "capacity is the thing to suspect — gathering is gated on room, not on the limit.");

        Assert.True(capped.World.FoodInGranaries() <= limit + (config.GatherYield * 4),
            $"Food settled at {capped.World.FoodInGranaries()} against a limit of {limit}.");
    }
}
