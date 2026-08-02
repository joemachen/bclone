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
    private const ulong FixtureFiftyYearHash = 4673658241522176988UL;
    private const ulong ShippedFiftyYearHash = 12610424914054256081UL;

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
    /// A cap that binds nothing proves nothing. If an unlimited village never reached 40
    /// firewood anyway, the test above would pass on a village that simply never got going —
    /// which is D52's lesson exactly: a comparative test has two villages and either can be
    /// the broken one.
    /// </remarks>
    [Fact]
    public void TheUnlimitedVillageReallyWouldHavePassedThatLimit()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());

        loop.Step(config.TicksPerYear * 40);

        int firewood = loop.World.FirewoodInSheds();
        _output.WriteLine($"unlimited village holds {firewood} firewood after 40 years");

        Assert.True(firewood > 40, $"Only {firewood} firewood, so a limit of 40 binds nothing.");
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
}
