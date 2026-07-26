using System.Reflection;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The Phase 1 pillar: villagers take work themselves, and you can always find out
/// why (DESIGN.md §2.2).
/// </summary>
public sealed class LabourTests
{
    private readonly ITestOutputHelper _output;

    public LabourTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Build(SimConfig config, ulong? seed = null) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed);

    // ---------------------------------------------------------------
    //  The pillar should be impossible to violate, not merely discouraged
    // ---------------------------------------------------------------

    [Fact]
    public void NoPublicApiLetsACallerAssignAVillagerToAWorkplace()
    {
        // The Banished pattern being deleted is slotting N workers into a building.
        // The surest way not to drift back toward it is to make it unexpressible, so
        // this asserts the absence of the API rather than trusting nobody adds one.
        var offenders = new List<string>();

        foreach (Type type in typeof(SimWorld).Assembly.GetExportedTypes())
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                string name = method.Name;
                bool soundsLikeAssignment =
                    name.Contains("Assign", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Hire", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("SetJob", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("SetWorker", StringComparison.OrdinalIgnoreCase);

                if (soundsLikeAssignment)
                {
                    offenders.Add($"{type.Name}.{name}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "Labour assignment must not be callable from outside the sim. Found: " +
            string.Join(", ", offenders));
    }

    // ---------------------------------------------------------------
    //  Assignment behaviour
    // ---------------------------------------------------------------

    [Fact]
    public void AdultsTakeWorkWithoutAnyoneAssigningThem()
    {
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerSeason);

        int working = 0;
        foreach (Villager villager in loop.World.Villagers)
        {
            if (villager.HasJob)
            {
                working++;
            }
        }

        Assert.Equal(4, working);
    }

    [Fact]
    public void ChildrenNeverHoldJobs()
    {
        SimLoop loop = Build(Config);

        for (int i = 0; i < 30_000; i++)
        {
            loop.StepOnce();

            foreach (Villager villager in loop.World.Villagers)
            {
                if (villager.LifeStage == LifeStage.Child)
                {
                    Assert.False(villager.HasJob,
                        $"{villager.Name} is {villager.AgeYears} and holds a job at tick {loop.World.Tick}.");
                }
            }
        }
    }

    [Fact]
    public void TheDeadGiveUpTheirJobs()
    {
        SimLoop loop = Build(Config);
        loop.Step(30_000);

        foreach (Villager villager in loop.World.Villagers)
        {
            if (!villager.Alive)
            {
                Assert.False(villager.HasJob, $"{villager.Name} is dead and still employed.");
            }
        }
    }

    [Fact]
    public void AWorkplaceNeverExceedsItsCapacity()
    {
        SimLoop loop = Build(Config with { ForageSiteCapacity = 3 });
        loop.Step(30_000);

        foreach (Workplace workplace in loop.World.Workplaces)
        {
            Assert.True(workplace.WorkerIds.Count <= workplace.Capacity,
                $"{workplace.Name} has {workplace.WorkerIds.Count} workers and room for " +
                $"{workplace.Capacity}.");
        }
    }

    [Fact]
    public void NobodyWalksAcrossTheMapForOneLog()
    {
        // The named failure mode in DESIGN.md §2.2. Catchment is the guard.
        SimLoop loop = Build(Config with { ForagerCatchmentTiles = 6 });
        loop.Step(30_000);

        foreach (Villager villager in loop.World.Villagers)
        {
            if (!villager.HasJob)
            {
                continue;
            }

            Workplace workplace = loop.World.FindWorkplace(villager.WorkplaceId)!;
            Assert.True(LabourSystem.InCatchment(loop.World, villager, workplace),
                $"{villager.Name} works at {workplace.Name} from outside its catchment.");
        }
    }

    [Fact]
    public void AVillagerTooFarAwayTakesNoWork()
    {
        // A catchment of one tile reaches nobody: every home is further than that
        // from the berry patch.
        SimLoop loop = Build(Config with { ForagerCatchmentTiles = 1 });
        loop.Step(Config.TicksPerSeason * 2);

        foreach (Villager villager in loop.World.Villagers)
        {
            Assert.False(villager.HasJob);
        }
    }

    // ---------------------------------------------------------------
    //  Legibility — the phase's actual deliverable
    // ---------------------------------------------------------------

    [Fact]
    public void EveryWorkerCanSayWhyTheyHoldTheirJob()
    {
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerSeason * 8);

        foreach (Villager villager in loop.World.Villagers)
        {
            if (!villager.HasJob)
            {
                continue;
            }

            Assert.False(string.IsNullOrWhiteSpace(villager.JobReason),
                $"{villager.Name} holds a job with no stated reason.");
        }
    }

    [Fact]
    public void TheReasonNamesThePlaceAndTheDistance()
    {
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerSeason);

        Villager worker = loop.World.Villagers[0];
        _output.WriteLine($"{worker.Name}: {worker.JobReason}");

        Assert.Contains("berry patch", worker.JobReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tiles", worker.JobReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheReasonNamesTheRunnerUpWhenThereWasOne()
    {
        // "Why Otto and not Bess?" has to have an answer, or the assignment is
        // opaque even when it is correct.
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerSeason);

        bool anyNamedARival = false;
        foreach (Villager villager in loop.World.Villagers)
        {
            if (villager.HasJob && villager.JobReason.Contains("was", StringComparison.Ordinal))
            {
                anyNamedARival = true;
                _output.WriteLine($"{villager.Name}: {villager.JobReason}");
            }
        }

        Assert.True(anyNamedARival,
            "With four candidates for one patch, at least one reason should name a rival.");
    }

    [Fact]
    public void LosingAJobSaysWhy()
    {
        SimLoop loop = Build(Config);
        loop.Step(30_000);

        foreach (Villager villager in loop.World.Villagers)
        {
            if (!villager.Alive && !string.IsNullOrEmpty(villager.JobReason))
            {
                Assert.StartsWith("No work:", villager.JobReason, StringComparison.Ordinal);
                _output.WriteLine($"{villager.Name}: {villager.JobReason}");
                return;
            }
        }
    }

    // ---------------------------------------------------------------
    //  Determinism — where the ordering hazards live
    // ---------------------------------------------------------------

    [Fact]
    public void TieBreakingIsStableAcrossRuns()
    {
        // Four founders, two households, symmetric distances: a textbook tie. The
        // same villager must win it every single run, or the village desyncs.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            SimLoop a = Build(Config with { ForageSiteCapacity = 1 });
            SimLoop b = Build(Config with { ForageSiteCapacity = 1 });

            a.Step(Config.TicksPerSeason);
            b.Step(Config.TicksPerSeason);

            Workplace patchA = a.World.Workplaces[0];
            Workplace patchB = b.World.Workplaces[0];

            Assert.Equal(patchA.WorkerIds, patchB.WorkerIds);
        }
    }

    [Fact]
    public void LabourAssignmentIsDeterministic()
    {
        SimLoop a = Build(Config);
        SimLoop b = Build(Config);

        a.Step(30_000);
        b.Step(30_000);

        Assert.Equal(StateHash.Compute(a.World), StateHash.Compute(b.World));

        for (int i = 0; i < a.World.Villagers.Count; i++)
        {
            Assert.Equal(a.World.Villagers[i].WorkplaceId, b.World.Villagers[i].WorkplaceId);
            Assert.Equal(a.World.Villagers[i].JobReason, b.World.Villagers[i].JobReason);
        }
    }

    [Fact]
    public void TheHashCoversJobAssignments()
    {
        // Anti-vacuity: a hash that ignored jobs would let the labour system desync
        // while the determinism suite stayed green.
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerSeason);

        ulong before = StateHash.Compute(loop.World);
        loop.World.Villagers[0].WorkplaceId = 0;

        Assert.NotEqual(before, StateHash.Compute(loop.World));
    }

    [Fact]
    public void TheVillageStillSustainsItselfOnJobBasedForaging()
    {
        // Foraging is now gated on holding a job. If assignment is too slow, too
        // narrow, or drops workers, the village starves - so the economy is the
        // real test of the labour system.
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerYear * 150);

        _output.WriteLine(
            $"Year {loop.World.Clock.Year}: {loop.World.Population} alive, " +
            $"{loop.World.Workplaces[0].WorkerIds.Count} foraging.");

        Assert.True(loop.World.Population > Config.StartingPopulation);
    }
}
