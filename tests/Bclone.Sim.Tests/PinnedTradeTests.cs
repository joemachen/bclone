using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The player can keep a named villager on a trade — Joe, 2026-08-22, built 2026-08-28.
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ Raised against D182's finding that careers are DISCONTINUOUS</b> — Agnes held foraging
/// 44% of her adult life against Mabel's 70% on trading, because the reshuffle moves people every
/// three years. <b>Joe's ruling on the discontinuity itself was that it is fine</b> (*"that is
/// just the natural flow of life, which makes it acceptable"*); what he wanted beside it was
/// agency.
/// </para>
/// <para>
/// <b>⛔⛔ IT PINS A TRADE AND NEVER A WORKPLACE, AND THAT IS THE WHOLE REASON §2.2 SURVIVES IT.</b>
/// The Banished pattern that pillar deletes is slotting a named worker into a <em>building</em>.
/// <see cref="LabourTests.NoPublicApiLetsACallerAssignAVillagerToAWorkplace"/> makes that
/// unexpressible and <b>this feature does not trip it</b> — the player says what Hattie does,
/// the sim still says which hut, and every <c>JobReason</c> sentence stays answerable.
/// </para>
/// <para>
/// ⭐ <b>Joe offered to overrule that guard and it turned out not to need overruling.</b> It is
/// D51's precedent one axis over: <c>SetStaffing</c> was allowed because it sets a <em>count</em>
/// and leaves the person to the sim; this is allowed because it sets a <em>trade</em> and leaves
/// the place to the sim.
/// </para>
/// </remarks>
public sealed class PinnedTradeTests
{
    private readonly ITestOutputHelper _output;

    public PinnedTradeTests(ITestOutputHelper output) => _output = output;

    private static JobKind? TradeOf(SimWorld world, Villager villager)
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

    /// <summary>⭐⭐ A pin survives the reshuffle, which is the whole of what it buys.</summary>
    /// <remarks>
    /// <b>Both cadences have to be beaten, and they fail differently.</b> The slack pass
    /// (every 60 ticks) releases the furthest-travelling holder of an over-quota trade; the
    /// reshuffle (every three years) tears <em>every</em> allocation down and rebuilds from
    /// nothing. A fix for one is not a fix for the other — refusing to shed somebody protects
    /// the first, and refusing to offer them elsewhere is what survives the second.
    /// </remarks>
    [Fact]
    public void SomebodyKeptOnATradeIsStillOnItYearsLater()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        ColdStartTests.PlayTheOpening(world);
        loop.Step((config.TicksPerYear * 10) + (config.TicksPerSeason / 2));

        // ⛔⛔⛔ THIS GUARD MEASURED **0 OF 4,311 TICKS** THROUGH FOUR SEPARATE FIXES, AND THE
        // FIRST TWO DIAGNOSES WERE BOTH WRONG. Worth writing down in full, because each wrong
        // answer was plausible and the mechanism needed **five** things at once:
        //
        //   1. `ReleaseUnfit` lets go of a pinned villager holding some OTHER trade. Without it
        //      she keeps her old job for ever, because `BuildCandidates` only offers work to
        //      somebody with none.
        //   2. `ShedSurplus` never sheds a pinned villager.
        //   3. `BuildCandidates` never offers a pinned villager a different trade — which is what
        //      survives the reshuffle, where (2) does not, because the reshuffle tears every
        //      allocation down and on that tick she looks like anybody else.
        //   4. `LabourQuota` floors the trade at the number pinned to it.
        //   5. **A pin outranks cost in the candidate sort.** Without this the other four are
        //      worthless: `MakeRoomForPins` let go of the incumbent and the cost sort hired him
        //      straight back, because he lived nearer.
        //
        // ⚠️ **The two wrong diagnoses, kept because they were both reasonable.** First: *"there
        // is no forester's hut in this fixture"* — false, `PlayTheOpening`'s village builds one.
        // Second: *"the seat is taken, so displace the incumbent"* — true and **not enough**; the
        // room that gates hiring is in the QUOTA, not the building, and the hut had a free chair
        // the whole time. **Four correct mechanisms still measured zero.**
        JobKind wanted = JobKind.Forester;
        Assert.Contains(
            world.Workplaces,
            place => place.Kind == wanted && !place.IsSite);

        // Pin somebody who is NOT already doing it, or the guard passes on inertia.
        Villager? mover = null;
        foreach (Villager candidate in world.Villagers)
        {
            if (candidate.CanWork && TradeOf(world, candidate) != wanted)
            {
                mover = candidate;
                break;
            }
        }

        Assert.NotNull(mover);
        _output.WriteLine(
            $"{mover!.Name} was on {TradeOf(world, mover)?.ToString() ?? "nothing"}; "
            + $"pinning them to {wanted}.");

        world.SetPinnedTrade(mover, wanted);

        foreach (Workplace place in world.Workplaces)
        {
            if (place.Kind == wanted)
            {
                _output.WriteLine(
                    $"  {place.Name}: capacity {place.Capacity}, places {place.Places}, "
                    + $"site {place.IsSite}, workers {place.WorkerIds.Count}");
            }
        }

        _output.WriteLine($"  quota wants {LabourQuota.For(world).For(wanted)} of {wanted}");

        // ⭐ Long enough to cross several slack passes AND at least two full reshuffles, which
        // is the pair of cadences a pin has to beat.
        int held = 0;
        int sampled = 0;
        for (int tick = 0; tick < config.TicksPerYear * 9; tick++)
        {
            loop.StepOnce();

            if (!mover.Alive || !mover.CanWork)
            {
                break;
            }

            // ⚠️ Skip the year edge: the reshuffle tears every allocation down on that exact
            // tick and rebuilds within it, so sampling there measures the hole rather than the
            // pin — the trap HANDOFF.md records and LabourCadenceTests already skips.
            if (world.Tick % (ulong)config.TicksPerYear == 0UL)
            {
                continue;
            }

            sampled++;
            if (TradeOf(world, mover) == wanted)
            {
                held++;
            }
        }

        _output.WriteLine($"held {wanted} on {held} of {sampled} sampled ticks");
        _output.WriteLine($"  reason: {mover.JobReason}");
        _output.WriteLine($"  pinned: {mover.PinnedTrade}, workplace {mover.WorkplaceId}, trade {TradeOf(world, mover)}");

        Assert.True(sampled > 0, "Nobody was alive to sample, so this measures nothing.");

        // Not 100%: a pinned villager still has to be matched to a seat, and there is a tick or
        // two after each reshuffle before the allocator has placed everybody. The claim is that
        // they are kept there, not that they are teleported.
        Assert.True(
            held * 100 / sampled >= 95,
            $"Pinned to {wanted} and held it on only {held} of {sampled} ticks — the reshuffle "
                + "or the slack pass is still moving them.");
    }

    /// <summary>⛔ Pinned to a trade the village cannot do, and it SAYS so.</summary>
    /// <remarks>
    /// <b>This sentence exists because chasing a red check turned it up.</b> While diagnosing the
    /// guard above, one hypothesis was that the pinned trade had nowhere to be done — and although
    /// that turned out <em>not</em> to be what was wrong, it is a state the village can genuinely
    /// reach, and it said nothing at all about it.
    /// <para>
    /// ⛔ <b>A player can make that mistake in one click</b> — keep somebody on forestry before
    /// building the hut — and would have got the same silence. It is the player's own standing
    /// order starving them: the most answerable kind of idleness there is, and it was the least
    /// explained.
    /// </para>
    /// </remarks>
    [Fact]
    public void PinnedToATradeTheVillageCannotDoSaysSo()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        ColdStartTests.PlayTheOpening(world);
        loop.Step(config.TicksPerYear * 5);

        // ⚠️⚠️ FOUND IN THE VILLAGE, NOT NAMED — the same mistake as the guard above, made twice
        // in one sitting. Naming Forester here failed because `PlayTheOpening` DOES end up with a
        // forester's hut; naming any trade at all is a guess about a fixture that changes.
        JobKind? nowhere = null;
        foreach (JobKind kind in System.Enum.GetValues<JobKind>())
        {
            bool anywhere = false;
            foreach (Workplace place in world.Workplaces)
            {
                anywhere |= place.Kind == kind && !place.IsSite;
            }

            if (!anywhere)
            {
                nowhere = kind;
                break;
            }
        }

        Assert.True(nowhere is not null, "Every trade has a workplace, so this cannot be posed.");
        _output.WriteLine($"nowhere does {nowhere}");

        Villager somebody = world.Villagers[0];
        world.SetPinnedTrade(somebody, nowhere!.Value);
        loop.Step(config.TicksPerYear);

        _output.WriteLine($"{somebody.Name}: {somebody.JobReason}");

        Assert.False(somebody.HasJob);
        Assert.Contains("nowhere to do it", somebody.JobReason, System.StringComparison.Ordinal);
        Assert.Contains("hand them back", somebody.JobReason, System.StringComparison.Ordinal);
    }

    /// <summary>⛔ Un-pinning hands them back, and there is only one way to say it.</summary>
    [Fact]
    public void HandingThemBackLetsTheVillageMoveThemAgain()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        Villager somebody = world.Villagers[0];

        world.SetPinnedTrade(somebody, JobKind.Woodcutter);
        Assert.True(somebody.IsPinned);
        Assert.Equal(JobKind.Woodcutter, somebody.PinnedTrade);

        world.SetPinnedTrade(somebody, null);
        Assert.False(somebody.IsPinned);
        Assert.Null(somebody.PinnedTrade);
    }

    /// <summary>⭐ The village says so, both ways — a standing order nobody can see is a bug.</summary>
    [Fact]
    public void TheVillageSaysWhoIsBeingKeptOnWhat()
    {
        SimConfig config = VillageFixtures.Village;
        var sink = new InMemoryLogSink();
        SimWorld world = SimFactory.CreatePhase0(config, sink).World;
        Villager somebody = world.Villagers[0];

        world.SetPinnedTrade(somebody, JobKind.Farmer);
        world.SetPinnedTrade(somebody, null);

        var said = new System.Text.StringBuilder();
        foreach (LogEntry entry in sink.Entries)
        {
            if (entry.Subsystem == "life")
            {
                said.Append(entry.Message).Append(" | ");
            }
        }

        _output.WriteLine(said.ToString());

        Assert.Contains(somebody.Name, said.ToString(), System.StringComparison.Ordinal);
        Assert.Contains("is to stay on", said.ToString(), System.StringComparison.Ordinal);
        Assert.Contains("goes back to whatever", said.ToString(), System.StringComparison.Ordinal);
    }

    /// <summary>⭐⭐ The quota floors on the pins, or the two controls fight for ever.</summary>
    /// <remarks>
    /// <b>Without this the village oscillates.</b> <c>ShedSurplus</c> refuses to release a pinned
    /// villager; if the quota still wanted none of that trade, every slack pass would try to
    /// release somebody it cannot, and <c>ExplainTheIdle</c> would narrate a shortfall the player
    /// created and cannot see. ⚠️ <b>A floor, not a setting</b> — it only raises, and only to the
    /// number actually pinned.
    /// </remarks>
    [Fact]
    public void TheQuotaNeverWantsFewerThanThePeoplePinnedToIt()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        ColdStartTests.PlayTheOpening(world);
        loop.Step(config.TicksPerYear * 5);

        int before = LabourQuota.For(world).For(JobKind.Woodcutter);

        int pinned = 0;
        foreach (Villager villager in world.Villagers)
        {
            if (villager.CanWork && pinned < before + 2)
            {
                world.SetPinnedTrade(villager, JobKind.Woodcutter);
                pinned++;
            }
        }

        int after = LabourQuota.For(world).For(JobKind.Woodcutter);
        _output.WriteLine($"woodcutters wanted {before} -> {after} with {pinned} pinned");

        Assert.True(pinned > before, "The pose did not exceed the quota, so nothing is floored.");
        Assert.True(
            after >= pinned,
            $"{pinned} people are pinned to woodcutting and the village wants {after}.");
    }

    /// <summary>⛔ An unpinned village is byte-identical — the licence for no golden moving.</summary>
    /// <remarks>
    /// <b>The same argument D212 and D216 make for a stock limit nobody set.</b> <c>null</c> means
    /// *the player has not said*, and a village nobody has pinned anybody in must hash exactly as
    /// it did before this existed — which is why <c>PinnedTrade</c> is mixed sparsely.
    /// </remarks>
    [Fact]
    public void PinningNobodyChangesNothingAtAll()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop plain = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimLoop touched = SimFactory.CreatePhase0(config, new InMemoryLogSink());

        // Pin somebody and immediately hand them back: the player expressed an opinion and
        // withdrew it, so the world must be indistinguishable from one where they never did.
        touched.World.SetPinnedTrade(touched.World.Villagers[0], JobKind.Forager);
        touched.World.SetPinnedTrade(touched.World.Villagers[0], null);

        plain.Step(config.TicksPerYear * 20);
        touched.Step(config.TicksPerYear * 20);

        Assert.Equal(StateHash.Compute(plain.World), StateHash.Compute(touched.World));
    }
}
