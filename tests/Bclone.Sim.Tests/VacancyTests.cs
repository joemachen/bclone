using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// A death does not leave the work undone until the next labour pass (D47).
/// </summary>
/// <remarks>
/// This is the half of D46 that makes the slower cadence affordable. Sharing the work
/// out every three years instead of every year is a better rhythm to watch, but on its
/// own it means the village limps: the one person who splits logs dies in early winter
/// and the hut stands empty until somebody gets round to noticing. Filling the vacancy
/// the moment it appears is what pays for the slower reshuffle.
/// </remarks>
public sealed class VacancyTests
{
    private readonly ITestOutputHelper _output;

    public VacancyTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ADeadWorkerNeverKeepsHoldingTheirJob()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        int worstStreak = 0;
        int streak = 0;

        for (ulong tick = 0; tick < (ulong)config.TicksPerYear * 120UL; tick++)
        {
            loop.StepOnce();

            bool held = false;
            for (int i = 0; i < world.Villagers.Count; i++)
            {
                Villager villager = world.Villagers[i];
                if (!villager.Alive && villager.HasJob)
                {
                    held = true;
                    break;
                }
            }

            streak = held ? streak + 1 : 0;
            if (streak > worstStreak)
            {
                worstStreak = streak;
            }
        }

        _output.WriteLine($"120 years; longest a dead villager kept a job: {worstStreak} ticks.");

        // One tick, not zero: MortalitySystem runs last in the tick order and
        // LabourSystem runs two steps before it, so a death at tick T is answered at
        // T+1. That is a quarter of a day, and making it zero would mean reordering
        // the systems — which is a behavioural change, not a tidy-up (D5).
        Assert.True(
            worstStreak <= 1,
            $"A dead villager held a job for {worstStreak} consecutive ticks. The work they were " +
            "doing was not being done, and nobody was told.");
    }

    /// <summary>Anti-vacuity (D7): people must actually be dying during the window.</summary>
    [Fact]
    public void PeopleDoDieWhileWeAreWatching()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());

        loop.Step(config.TicksPerYear * 120);

        int dead = 0;
        int everWorked = 0;
        foreach (Villager villager in loop.World.Villagers)
        {
            if (!villager.Alive)
            {
                dead++;
                if (villager.JobReason.Length > 0)
                {
                    everWorked++;
                }
            }
        }

        _output.WriteLine($"{dead} died in 120 years, {everWorked} of them having held work.");
        Assert.True(dead > 0, "Nobody died in 120 years, so the guard above is vacuous.");
    }

    [Fact]
    public void WorkTheVillageWantsDoneAndNobodyIsDoingIsReported()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        loop.Step(config.TicksPerYear * 5);

        // SAMPLED IN A SEASON THAT WANTS WORK DOING, not at whatever instant five years
        // happens to land on — and asserted, not hoped for (D52).
        //
        // This compared "before anyone left" against "after everyone left" at an arbitrary
        // tick. UnmannedWork only counts a workplace the village actually WANTS staffed,
        // so on a tick where the quota happens to be zero — winter, or a lean spell —
        // emptying every workplace changes nothing and the test reads as a broken alert.
        // The alert was fine; the instant was.
        while (loop.World.Clock.Season != Season.Summer)
        {
            loop.StepOnce();
        }

        LabourQuota wanted = LabourQuota.For(loop.World);
        Assert.True(
            wanted.Foragers + wanted.Foresters + wanted.Woodcutters > 0,
            "The village wants no work of any kind at the moment this is sampled, so "
            + "emptying its workplaces cannot possibly change the answer. That is a broken "
            + "control, not a broken alert.");

        // A healthy village should have nothing to report — the alert has to stay rare
        // or the player learns to ignore it, which is worse than not having it.
        IReadOnlyList<Workplace> quiet = LabourSystem.UnmannedWork(world);
        _output.WriteLine($"settled village: {quiet.Count} unmanned workplaces wanted.");

        // Now empty the village of workers and confirm it does speak up.
        var wasAlive = new List<Villager>();
        foreach (Villager villager in world.Villagers)
        {
            if (villager.Alive)
            {
                wasAlive.Add(villager);
            }
        }

        Assert.NotEmpty(wasAlive);

        IReadOnlyList<Workplace> beforeAnyoneLeft = LabourSystem.UnmannedWork(world);
        foreach (Workplace workplace in world.Workplaces)
        {
            workplace.WorkerIds.Clear();
        }

        IReadOnlyList<Workplace> afterEveryoneLeft = LabourSystem.UnmannedWork(world);
        _output.WriteLine(
            $"before {beforeAnyoneLeft.Count}, after clearing every workplace {afterEveryoneLeft.Count}.");

        Assert.True(
            afterEveryoneLeft.Count > beforeAnyoneLeft.Count,
            "Emptying every workplace produced no more warnings than a staffed village, so the " +
            "alert cannot tell the difference and is decoration.");
    }
}
