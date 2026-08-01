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
        // The Banished pattern being deleted is slotting a NAMED WORKER into a
        // building. The surest way not to drift back toward it is to make it
        // unexpressible, so this asserts the absence of the API rather than trusting
        // nobody adds one.
        //
        // NARROWED FOR D51, deliberately and with the line restated. The player may now
        // say how many hands a workplace gets — SimWorld.SetStaffing — and that is not
        // this pattern: it sets a COUNT, and proximity, household and catchment still
        // choose the person, so every "why is Elias here?" sentence stays true. What
        // must remain impossible is naming a villager and binding them to a workplace.
        // So the guard now tests the signature rather than the verb: no public method
        // may take both a villager and a workplace.
        var offenders = new List<string>();

        foreach (Type type in typeof(SimWorld).Assembly.GetExportedTypes())
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.ReturnType != typeof(void))
                {
                    // A method that hands something back is answering a question, not
                    // issuing an order — "how far is Elias from the stand?" is a reader,
                    // whatever its parameters are.
                    continue;
                }

                bool takesAVillager = false;
                bool takesAWorkplace = false;
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    takesAVillager |= parameter.ParameterType == typeof(Villager);
                    takesAWorkplace |= parameter.ParameterType == typeof(Workplace);
                }

                if (takesAVillager && takesAWorkplace)
                {
                    offenders.Add($"{type.Name}.{method.Name}");
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
            Assert.True(LabourAllocator.InCatchment(loop.World, villager, workplace),
                $"{villager.Name} works at {workplace.Name} from outside its catchment.");
        }
    }

    [Fact]
    public void AVillagerTooFarAwayTakesNoWork()
    {
        // A catchment of one tile reaches almost nobody — but not literally nobody any
        // more, and that change is the point. Homes are now placed with regard to the
        // work (D18), so one of them can end up right beside a patch and legitimately
        // reach it across a one-tile catchment. The old assertion, "every villager is
        // jobless", was true only while homes were dropped on a spiral that ignored
        // where the work was.
        //
        // So this asserts the invariant that actually matters and is stronger: nobody
        // EVER holds a job outside their catchment. That is the rule §2.2 is about —
        // no villager walks across the map for one log — and it cannot be satisfied by
        // accident of layout.
        SimConfig config = Config with { ForagerCatchmentTiles = 1 };
        SimLoop loop = Build(config);
        loop.Step(config.TicksPerSeason * 2);

        int refused = 0;
        foreach (Villager villager in loop.World.Villagers)
        {
            if (!villager.HasJob)
            {
                refused++;
                continue;
            }

            Workplace held = loop.World.FindWorkplace(villager.WorkplaceId)!;
            int cost = loop.World.TravelCost.Cost(
                loop.World.HomeOf(villager), held.Position);

            Assert.True(cost <= held.CatchmentRadius,
                $"{villager.Name} holds {held.Name} at {cost} against a reach of " +
                $"{held.CatchmentRadius}.");
        }

        _output.WriteLine($"{refused} of {loop.World.Villagers.Count} turned away by a one-tile reach.");
        Assert.True(refused > 0, "A one-tile catchment turned nobody away, so it constrained nothing.");
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

        // The place they ACTUALLY hold, not "the berry patch" by name. The valley is
        // generated now (D18), so which of several thickets is nearest to villager
        // zero is a property of the seed — and a test that pinned the name was
        // asserting the map rather than the sentence.
        Workplace? held = loop.World.FindWorkplace(worker.WorkplaceId);
        Assert.NotNull(held);
        Assert.Contains(held!.Name, worker.JobReason, StringComparison.OrdinalIgnoreCase);
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

    // ---------------------------------------------------------------
    //  Winter (D44, D52)
    // ---------------------------------------------------------------

    [Fact]
    public void NobodyIsStaffedToABerryPatchInWinter()
    {
        // D44's bug, asserted. The quota had no idea what season it was, so the food
        // floor was staffed all winter and every spare hand was poured into foraging
        // on top of it — onto patches with nothing on them. BehaviorSystem then sent
        // the lot of them home. A quarter of the working year, resting, by whoever
        // held the commonest job in the village.
        SimLoop loop = Build(Config);

        int wintersSeen = 0;
        for (int year = 1; year <= 20; year++)
        {
            for (int season = 0; season < 4; season++)
            {
                loop.Step(Config.TicksPerSeason);

                LabourQuota quota = LabourQuota.For(loop.World);
                if (FoodSource.IsGatherable(loop.World.Clock.Season))
                {
                    continue;
                }

                wintersSeen++;
                Assert.True(quota.Foragers == 0,
                    $"The village wants {quota.Foragers} foraging in " +
                    $"{loop.World.Clock.SeasonAndYear()}, when there is nothing to pick.");
            }
        }

        _output.WriteLine($"{wintersSeen} winter readings, none of them staffing a berry patch.");
        Assert.True(wintersSeen > 0, "No winter was ever sampled, so this guard is vacuous (D7).");
    }

    [Fact]
    public void NobodyIsPutOnTheStandWhenNoOneWantsTheTimber()
    {
        // D52, and the reason the first idle-winter fix was wrong. Spare winter hands
        // were sent to the woods, bounded only by the tree stands' seats and by "is any
        // shed not yet full?" — which bounds the SHED, not the work. Demand for timber
        // is answered twice over in LabourQuota.For, by LoggersWanted for the houses
        // and by the hut chain for firewood, and both are funded before that fill ran.
        // So when BOTH said nobody, the fill still staffed every seat.
        //
        // What it cost, because a staffing bug is never only a staffing bug: THE SHED
        // IS ONE ROOM (D33), logs and firewood share its capacity, so timber nobody
        // wanted crowded out the fuel. The birth gate reads a household's own firewood,
        // and the village quietly stopped having children — a mean population of 14
        // against 22 without the fill, with a full larder, a full shed, nobody starving
        // and nobody freezing.
        //
        // Asserted against the two demand questions rather than against the shed,
        // because the shed is downstream. A shed legitimately fills; what must never
        // happen is a hand being spent on a good the village has no use for.
        SimConfig config = Config;
        SimLoop loop = Build(config);

        int idleDemandSeen = 0;
        int inWinter = 0;

        for (int season = 1; season <= 200 * 4; season++)
        {
            loop.Step(config.TicksPerSeason);

            if (LabourQuota.LoggersWanted(loop.World) > 0
                || LabourQuota.WoodcuttersWanted(loop.World) > 0)
            {
                continue;
            }

            idleDemandSeen++;
            if (!FoodSource.IsGatherable(loop.World.Clock.Season))
            {
                inWinter++;
            }

            LabourQuota quota = LabourQuota.For(loop.World);
            Assert.True(quota.Loggers == 0,
                $"The village wants {quota.Loggers} at the stand in " +
                $"{loop.World.Clock.SeasonAndYear()}, with no house waiting on timber and no " +
                $"firewood wanted. It already holds {loop.World.TotalLogs()} logs.");
        }

        _output.WriteLine(
            $"{idleDemandSeen} seasons wanted no timber at all, {inWinter} of them winters.");

        // Anti-vacuity (D7), and the winter half matters on its own: the fill only ever
        // ran in winter, so a run that never sampled one would prove nothing about it.
        Assert.True(inWinter > 0,
            "The village never once went a winter with no use for timber, so this guard is " +
            "vacuous (D7) — the case it exists for did not happen.");
    }
}
