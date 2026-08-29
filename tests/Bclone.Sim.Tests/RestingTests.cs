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

        // Find somebody actually mid-rest, then watch how long they stay there.
        int longest = 0;
        int run = 0;
        Villager watched = world.Villagers[0];

        for (int tick = 0; tick < config.TicksPerYear; tick++)
        {
            loop.StepOnce();

            if (watched.Alive && watched.State == VillagerState.Resting)
            {
                run++;
                longest = System.Math.Max(longest, run);
            }
            else
            {
                run = 0;
            }
        }

        _output.WriteLine($"{watched.Name}'s longest unbroken rest was {longest} ticks "
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
