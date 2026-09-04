using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ The share-out switch — <b>keep the arrangement you made</b> (Joe, 2026-09-03).
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe:</b> *"In the settings, give the user the option to toggle the 'work share' function
/// on/off."* — the three-yearly pass that tears every allocation in the village down and rebuilds
/// it, which is how a fifteen-year woodcutter ends up pushing a market cart.
/// </para>
/// <para>
/// ⛔ <b>WHAT IT MUST NOT DO IS SWITCH THE LABOUR SYSTEM OFF.</b> Slack runs in the reshuffle's
/// place, so empty seats still fill, the unfit are still let go, and a death is still answered the
/// same tick (D47). *The churn stops; the reacting does not.*
/// </para>
/// </remarks>
public sealed class WorkShareSwitchTests
{
    private readonly ITestOutputHelper _output;

    public WorkShareSwitchTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    /// <summary>
    /// ⭐⭐ Left alone, the switch is a <b>provable no-op</b> — the same hash, byte for byte.
    /// </summary>
    /// <remarks>
    /// <b>The sparse-hash rule, and the reason every control in this game gets this guard.</b>
    /// A village played without ever opening Settings must be indistinguishable from one running
    /// the code before the switch existed — which is what makes *"the default changes nothing"*
    /// a test rather than a promise. <c>StockLimitTests</c> is the same claim one control over.
    /// </remarks>
    [Fact]
    public void LeftAloneTheSwitchChangesNothing()
    {
        SimConfig config = Config;

        SimLoop untouched = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        untouched.Step(config.TicksPerYear * 8);

        SimLoop switchedOnExplicitly = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        switchedOnExplicitly.World.VillageSharesOutWork = true;
        switchedOnExplicitly.Step(config.TicksPerYear * 8);

        ulong before = StateHash.Compute(untouched.World);
        ulong after = StateHash.Compute(switchedOnExplicitly.World);

        _output.WriteLine($"untouched {before:X16}, switched on explicitly {after:X16}");
        Assert.Equal(before, after);
    }

    /// <summary>
    /// ⭐⭐ Switched off, the village <b>stops rearranging itself</b> — and the hash says so.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Two claims, and the second is the one that matters.</b> That the run diverges proves
    /// the switch reaches the sim at all; that it is <b>hashed</b> proves two divergent runs can
    /// never agree — the trap D51 records, where a control changes behaviour but not the hash and
    /// a determinism test passes straight across a real divergence.
    /// </remarks>
    [Fact]
    public void SwitchedOffTheVillageKeepsItsArrangement()
    {
        SimConfig config = Config;

        SimLoop sharing = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        sharing.Step(config.TicksPerYear * 8);

        SimLoop keeping = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        keeping.World.VillageSharesOutWork = false;
        keeping.Step(config.TicksPerYear * 8);

        ulong shared = StateHash.Compute(sharing.World);
        ulong kept = StateHash.Compute(keeping.World);

        _output.WriteLine(
            $"sharing {shared:X16} with {sharing.World.Population} alive; "
            + $"keeping {kept:X16} with {keeping.World.Population} alive");

        Assert.NotEqual(shared, kept);
    }

    /// <summary>
    /// ⛔⛔ Switched off, <b>the village still fills a seat nobody is in</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>The claim: a village with the share-out off, asked for a woodcutter, gets one.</b>
    /// Switching off the churn must not switch off employment.
    /// </para>
    /// <para>
    /// ⚠️⚠️ <b>AND THE RED CHECK CORRECTED THIS COMMENT, WHICH ORIGINALLY OVERCLAIMED.</b> It said
    /// the lazy build — <c>else { return; }</c> instead of running slack — would strand every
    /// villager who came of age. <b>It does not, and the guard passes against it.</b>
    /// <c>LabourSystem</c> runs <c>TakeUpSlack</c> on its own seasonal boundary anyway; the
    /// reshuffle boundary is a multiple of it, so all the early return costs is **one season of
    /// latency every three years**, not employment.
    /// </para>
    /// <para>
    /// ⛔ <b>So this guard does NOT discriminate between the two builds, and saying so is the
    /// point.</b> Running slack in the reshuffle's place is still the right code — a player who
    /// changes a profession number on the wrong tick should not wait a season to see it — but it
    /// is a latency fix, not a safety one. *A comment claiming a guard catches something it does
    /// not is worse than no comment: the next session trusts it and stops looking.*
    /// </para>
    /// </remarks>
    [Fact]
    public void SwitchedOffTheVillageStillFillsAnEmptySeat()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        world.VillageSharesOutWork = false;
        loop.Step(config.TicksPerYear);

        // Ask for a woodcutter the village has not got, then let a season turn.
        world.SetJobLimit(JobKind.Woodcutter, 1);
        int before = Working(world, JobKind.Woodcutter);
        loop.Step(config.TicksPerYear);
        int after = Working(world, JobKind.Woodcutter);

        _output.WriteLine(
            $"with the share-out off, woodcutters went from {before} to {after} "
            + $"after the village was asked for one");

        Assert.True(
            after > 0,
            "With the share-out switched off nobody was ever put on the work the player asked "
            + "for. The switch was meant to stop the churn, not the labour system.");
    }

    private static int Working(SimWorld world, JobKind kind)
    {
        int count = 0;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive || villager.WorkplaceId == 0)
            {
                continue;
            }

            Workplace? at = world.FindWorkplace(villager.WorkplaceId);
            if (at is not null && at.Kind == kind)
            {
                count++;
            }
        }

        return count;
    }
}
