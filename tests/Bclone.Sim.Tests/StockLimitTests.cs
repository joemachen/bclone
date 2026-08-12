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
    private const ulong FixtureFiftyYearHash = 8652554140921204871UL;
    private const ulong ShippedFiftyYearHash = 2151271050042369210UL;

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

        Assert.True(
            limited <= 40 + config.FirewoodPerSplit,
            $"Firewood settled at {limited} against a limit of 40.");
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
}
