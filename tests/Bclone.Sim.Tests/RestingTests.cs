using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// A villager with nothing to do rests, and resting takes time — Joe, 2026-08-28.
/// </summary>
/// <remarks>
/// <para>
/// <i>"Villagers should idle when they aren't hungry, have no jobs to do, no school… should
/// villagers also rest? maybe idle IS resting. that will really decrease the productivity. i'm
/// such a slave driver."</i>
/// </para>
/// <para>
/// <b>⛔ He is not — the sim had no way to say somebody was simply FINE.</b> <c>Resting</c> was
/// the last line of <c>Decide</c> and lasted exactly one tick, so a villager with no job re-asked
/// *"is there anything at all?"* every tick of their life. <b>Idleness had no positive
/// definition; it was the absence of an answer.</b> That is why D236's livelock could consume the
/// whole spare labour force for forty years and look like an ordinary village — there was no
/// state it was failing to be in.
/// </para>
/// <para>
/// ⭐ <b>And it is where the pub goes.</b> A rest spell is a span of time with a place attached.
/// Today the place is home; a tavern or a church is the same span spent somewhere worth walking
/// to, so the social buildings need a destination rather than a mechanism.
/// </para>
/// </remarks>
public sealed class RestingTests
{
    private readonly ITestOutputHelper _output;

    public RestingTests(ITestOutputHelper output) => _output = output;

    /// <summary>⭐ Somebody with nothing to do is actually resting, for a measurable while.</summary>
    [Fact]
    public void AVillagerWithNothingToDoSpendsRealTimeResting()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        ColdStartTests.PlayTheOpening(world);
        loop.Step(config.TicksPerYear * 10);

        int resting = 0;
        int able = 0;
        for (int tick = 0; tick < config.TicksPerYear * 3; tick++)
        {
            loop.StepOnce();

            foreach (Villager villager in world.Villagers)
            {
                if (!villager.CanWork || !villager.Alive)
                {
                    continue;
                }

                able++;
                if (villager.State == VillagerState.Resting)
                {
                    resting++;
                }
            }
        }

        _output.WriteLine($"{resting} of {able} able-adult ticks were spent resting "
            + $"({(able == 0 ? 0 : resting * 100 / able)}%)");

        // ⚠️ ANTI-VACUITY (D7): a dead village rests very thoroughly.
        Assert.True(able > 0, "Nobody able was alive, so this measures nothing.");

        // ⭐ THE CLAIM, AND IT IS A RANGE ON PURPOSE. A village where nobody ever rests has not
        // got the feature; a village where everybody always rests has stopped working. **The
        // number in the middle is the village's slack**, and it is the thing to watch when the
        // rest spell is tuned.
        int share = resting * 100 / able;
        Assert.True(share > 0, "Nobody rested at all over three years — the spell is not reached.");
        Assert.True(
            share < 90,
            $"{share}% of able-adult ticks were spent resting. The village has stopped working.");
    }

    /// <summary>
    /// ⭐⭐ Somebody who <b>holds a job</b> rests in spells too, not one tick at a time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, 2026-09-02:</b> *"I want villagers to have an idle rest period, even in their
    /// respective work seasons … they should have an idle/rest mode along with their work mode."*
    /// </para>
    /// <para>
    /// ⛔ <b>THE STATE WAS ALREADY THERE AND THE SPAN WAS NOT.</b> `Decide` set
    /// <c>State = Resting</c> for anybody who ran out of work, but only set
    /// <c>ActionTicksRemaining</c> for the jobless — so a job-holder rested for **one tick** and
    /// re-asked on the next. Measured before the change: job-holders were already in `Resting`
    /// for **9–16% of their ticks** across four shipped seeds. *The quantity was there; it
    /// flickered.*
    /// </para>
    /// <para>
    /// ⚠️ <b>This is the half `RestingLastsTheConfiguredSpan` cannot see.</b> That guard watches
    /// the whole roster and is satisfied by any one villager — and the jobless have always had
    /// their spell, so it would stay green if this reverted tomorrow. **The claim here is
    /// specifically about somebody holding a seat.**
    /// </para>
    /// </remarks>
    [Fact]
    public void SomebodyWhoHoldsAJobRestsInSpellsToo()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        loop.Step(config.TicksPerYear * 10);

        // ⛔⛔ A COUNTDOWN, NOT A RUN OF TICKS — AND THE RED CHECK IS WHAT TAUGHT ME THAT.
        //
        // The first draft counted consecutive ticks in `Resting`, and **it passed with the change
        // reverted**: 129 ticks unbroken either way. A flickering villager re-enters `Resting`
        // every single tick, so "how long were they in the state" cannot tell a spell from a
        // flicker — *it is the same picture*.
        //
        // ⭐ What actually changed is that a spell holds `ActionTicksRemaining` above zero, which
        // locks the villager out of `Decide` (`BehaviorSystem.ActOne`). **That is the difference,
        // so that is what is asserted.**
        long resting = 0;
        long inASpell = 0;

        for (int tick = 0; tick < config.TicksPerYear * 3; tick++)
        {
            loop.StepOnce();

            foreach (Villager villager in world.Villagers)
            {
                // The job is read on the same tick as the state: somebody who rests and is then
                // shed must not be counted on the strength of a job they no longer hold.
                if (!villager.Alive || !villager.HasJob
                    || villager.State != VillagerState.Resting)
                {
                    continue;
                }

                resting++;
                if (villager.ActionTicksRemaining > 0)
                {
                    inASpell++;
                }
            }
        }

        int share = resting == 0 ? 0 : (int)(inASpell * 100 / resting);
        _output.WriteLine($"job-holders were resting on {resting} tick-observations, and "
            + $"{share}% of those were inside a spell (rest_ticks is {config.RestTicks})");

        Assert.True(resting > 0, "Nobody with a job ever rested, so this proves nothing.");

        // ⛔⛔ A SHARE, AND TWO EARLIER DRAFTS OF THIS GUARD WERE BLIND BECAUSE THEY WERE NOT.
        //
        // Draft one counted consecutive ticks in `Resting` — **129 either way**, because a
        // flickering villager re-enters the state every tick and looks identical to one in a
        // spell. Draft two asked whether any job-holder was ever seen mid-countdown — **true
        // either way**, because the slack pass can hire somebody three ticks into a spell they
        // started while jobless. *Both existence tests; both green against the reverted change.*
        //
        // ⭐ The discriminator is PROPORTION. With the span scoped to the jobless, the only
        // job-holders mid-spell are the just-hired — a handful of ticks after a labour pass.
        // **Measured on this fixture: 0% with the span scoped to the jobless, 56% with it
        // widened.** The bar sits at 25, well clear of both.
        Assert.True(
            share >= 25,
            $"Only {share}% of the ticks a job-holder spent resting were inside a spell — they "
            + "are flickering through `Resting` one tick at a time and re-deciding on the next, "
            + "which is what scoping the span to `IsLaborer` did.");
    }

    /// <summary>⛔ A rest spell lasts the configured span, not one tick.</summary>
    /// <remarks>
    /// <b>The whole of the change, asserted directly.</b> Before this, `Resting` was set and then
    /// re-decided on the very next tick — so the state existed and meant nothing.
    /// </remarks>
    [Fact]
    public void RestingLastsTheConfiguredSpan()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        ColdStartTests.PlayTheOpening(world);
        loop.Step(config.TicksPerYear * 10);

        // ⛔ WHOEVER ACTUALLY RESTS, NOT `Villagers[0]` (2026-09-01). This watched one arbitrary
        // villager, and a rest SPELL is only given to somebody with no job at all
        // (`BehaviorSystem`, `IsLaborer`) — so the guard passed or failed on whether villager
        // zero happened to be unemployed that decade. It went red when the fixture's winter got
        // hungrier and Dorcas picked up a trade: **the rest spell was working perfectly and the
        // guard was looking at the wrong person.**
        //
        // ⚠️ This will matter again the moment job-holders start resting too — watching the
        // whole roster is the version of this claim that survives that change.
        var runs = new int[world.Villagers.Count];
        int longest = 0;
        string who = "nobody";

        for (int tick = 0; tick < config.TicksPerYear; tick++)
        {
            loop.StepOnce();

            for (int i = 0; i < world.Villagers.Count && i < runs.Length; i++)
            {
                Villager villager = world.Villagers[i];
                if (villager.Alive && villager.State == VillagerState.Resting)
                {
                    runs[i]++;
                    if (runs[i] > longest)
                    {
                        longest = runs[i];
                        who = villager.Name;
                    }
                }
                else
                {
                    runs[i] = 0;
                }
            }
        }

        _output.WriteLine($"{who}'s longest unbroken rest was {longest} ticks "
            + $"(rest_ticks is {config.RestTicks})");

        Assert.True(
            longest >= config.RestTicks,
            $"The longest rest was {longest} ticks against a rest_ticks of {config.RestTicks} — "
                + "resting is still being re-decided every tick.");
    }

    /// <summary>⛔ Zero is refused at load rather than silently meaning "never rest".</summary>
    /// <remarks>
    /// A rest of nought ticks is the old behaviour — re-decide every tick, for ever — and would
    /// be indistinguishable from the feature being broken. If the village should never rest,
    /// that is a design change and it should look like one.
    /// </remarks>
    [Fact]
    public void ARestOfNoTicksIsRefusedAtLoad()
    {
        SimConfig config = VillageFixtures.Village;

        SimConfigException blew = Assert.Throws<SimConfigException>(
            () => (config with { RestTicks = 0 }).Validate());

        _output.WriteLine(blew.Message);
        Assert.Contains("rest_ticks", blew.Message, System.StringComparison.Ordinal);
    }
}
