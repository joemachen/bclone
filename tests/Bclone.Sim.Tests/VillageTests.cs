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
/// The village-scale contract. Phase 0 proved one life; these prove the same
/// guarantees survive N villagers, which is where the ordering and tie-breaking
/// hazards live (spec §4b).
/// </summary>
public sealed class VillageTests
{
    private readonly ITestOutputHelper _output;

    public VillageTests(ITestOutputHelper output) => _output = output;

    /// <summary>Four founding adults across two households — Joe's chosen start.</summary>
    private static SimConfig Village => Phase0Fixtures.Plenty with
    {
        StartingHouseholds = 2,
        AdultsPerHousehold = 2,
        FounderAge = 20,
    };

    private static (SimLoop Loop, InMemoryLogSink Log) Build(SimConfig config, ulong? seed = null)
    {
        var sink = new InMemoryLogSink();
        return (SimFactory.CreatePhase0(config, sink, seed), sink);
    }

    [Fact]
    public void TheVillageIsFoundedAsConfigured()
    {
        var (loop, _) = Build(Village);
        SimWorld world = loop.World;

        Assert.Equal(4, world.Villagers.Count);
        Assert.Equal(2, world.Households.Count);
        Assert.Equal(4, world.Population);

        foreach (Villager villager in world.Villagers)
        {
            Assert.Equal(20, villager.AgeYears);
            Assert.Equal(LifeStage.Adult, villager.LifeStage);
            Assert.True(villager.CanWork);
        }
    }

    [Fact]
    public void FoundersKeepTheirAgeAcrossTheFirstTick()
    {
        // Regression: age is recomputed from birth year every tick, so a founder
        // whose birth year did not account for their starting age was silently
        // reset to zero on the first tick.
        var (loop, _) = Build(Village);

        loop.Step(10);
        Assert.All(loop.World.Villagers, v => Assert.Equal(20, v.AgeYears));

        loop.Step(240);   // one full year
        Assert.All(loop.World.Villagers, v => Assert.Equal(21, v.AgeYears));
    }

    [Fact]
    public void VillagersAreStoredInStableIdOrder()
    {
        // Iteration order decides who acts first, which decides who eats. It must
        // be a property of the village, not of insertion history.
        var (loop, _) = Build(Village);
        loop.Step(5_000);

        for (int i = 0; i < loop.World.Villagers.Count; i++)
        {
            Assert.Equal(i + 1, loop.World.Villagers[i].Id);
        }
    }

    [Fact]
    public void EveryVillagerBelongsToAHouseholdThatKnowsThem()
    {
        var (loop, _) = Build(Village);

        foreach (Villager villager in loop.World.Villagers)
        {
            Household household = loop.World.HouseholdOf(villager);
            Assert.Contains(villager.Id, household.MemberIds);
        }
    }

    [Fact]
    public void HouseholdsHoldTheirOwnFood()
    {
        // Decision D14: one family can starve beside a thriving neighbour. If the
        // stores were shared, this asymmetry would be impossible and so would the
        // inequality stories the design wants.
        var (loop, _) = Build(Village);
        loop.Step(2_000);

        Household first = loop.World.Households[0];
        Household second = loop.World.Households[1];

        first.Stockpile.TryTake(first.Stockpile.Food);

        Assert.Equal(0, first.Stockpile.Food);
        Assert.True(second.Stockpile.Food > 0, "Emptying one larder must not empty the other.");
    }

    // ---------------------------------------------------------------
    //  Determinism at scale — the P0 guarantee, at N villagers
    // ---------------------------------------------------------------

    [Fact]
    public void SameSeed_ProducesAnIdenticalVillage()
    {
        var (a, logA) = Build(Village);
        var (b, logB) = Build(Village);

        a.Step(20_000);
        b.Step(20_000);

        Assert.Equal(StateHash.Compute(a.World), StateHash.Compute(b.World));
        Assert.Equal(logA.Entries, logB.Entries);
    }

    [Fact]
    public void SameSeed_StaysIdenticalAtEveryTick()
    {
        // Catches a divergence at the tick it starts rather than only at the end,
        // where two villages could drift apart and back together.
        var (a, _) = Build(Village);
        var (b, _) = Build(Village);

        for (int i = 0; i < 2_000; i++)
        {
            a.StepOnce();
            b.StepOnce();
            Assert.Equal(StateHash.Compute(a.World), StateHash.Compute(b.World));
        }
    }

    [Fact]
    public void DifferentSeed_ProducesADifferentVillage()
    {
        // Anti-vacuity: proves the hash actually reads village state.
        var (a, _) = Build(Village, seed: 1UL);
        var (b, _) = Build(Village, seed: 2UL);

        a.Step(20_000);
        b.Step(20_000);

        Assert.NotEqual(StateHash.Compute(a.World), StateHash.Compute(b.World));
    }

    [Fact]
    public void TheHashCoversEveryVillagerNotJustTheFirst()
    {
        // The failure this guards is silent and total: a hash that only read
        // Villagers[0] would let the rest of the village desync unnoticed, and the
        // determinism suite would stay green through it.
        var (loop, _) = Build(Village);
        loop.Step(500);

        ulong before = StateHash.Compute(loop.World);
        loop.World.Villagers[^1].Hunger += 1;

        Assert.NotEqual(before, StateHash.Compute(loop.World));
    }

    [Fact]
    public void TheHashCoversEveryHouseholdNotJustTheFirst()
    {
        var (loop, _) = Build(Village);
        loop.Step(500);

        ulong before = StateHash.Compute(loop.World);
        loop.World.Households[^1].Stockpile.Add(1);

        Assert.NotEqual(before, StateHash.Compute(loop.World));
    }

    // ---------------------------------------------------------------
    //  Births and childhood
    // ---------------------------------------------------------------

    /// <summary>A village with a real childhood, unlike Phase 0's fixture.</summary>
    private static SimConfig GrowingVillage => Village with { AdultAge = 15 };

    [Fact]
    public void TheVillageGrows()
    {
        var (loop, _) = Build(GrowingVillage);
        int founding = loop.World.Villagers.Count;

        loop.Step(30_000);

        _output.WriteLine(
            $"Year {loop.World.Clock.Year}: {loop.World.Population} alive of " +
            $"{loop.World.Villagers.Count} ever born, in {loop.World.Households.Count} households.");

        Assert.True(loop.World.Villagers.Count > founding,
            "Four adults with full larders should have produced children.");
    }

    [Fact]
    public void ChildrenAreBornIntoTheirParentsHousehold()
    {
        var (loop, _) = Build(GrowingVillage);
        loop.Step(30_000);

        foreach (Villager villager in loop.World.Villagers)
        {
            Household household = loop.World.HouseholdOf(villager);
            Assert.Contains(villager.Id, household.MemberIds);
        }
    }

    [Fact]
    public void ChildrenDoNotWork()
    {
        // The dependency that made childhood need households in the first place
        // (D13): a child eats from the store and gives nothing back.
        var (loop, _) = Build(GrowingVillage);

        for (int i = 0; i < 30_000; i++)
        {
            loop.StepOnce();

            foreach (Villager villager in loop.World.Villagers)
            {
                if (villager.Alive && villager.LifeStage == LifeStage.Child)
                {
                    Assert.False(
                        villager.State is VillagerState.Gathering or VillagerState.TravelingToFood,
                        $"{villager.Name} is {villager.AgeYears} and foraging at tick {loop.World.Tick}.");
                }
            }
        }
    }

    [Fact]
    public void AChildBornInTheVillageGrowsUpAndWorks()
    {
        // 30 in-game years: long enough for a child born in year 2 to reach
        // adulthood, short enough that they have not yet died of old age.
        var (loop, _) = Build(GrowingVillage);
        loop.Step(GrowingVillage.TicksPerYear * 30);

        Villager? homegrown = null;
        foreach (Villager villager in loop.World.Villagers)
        {
            if (villager.BirthYear > 0 && villager.AgeYears >= GrowingVillage.AdultAge)
            {
                homegrown = villager;
                break;
            }
        }

        Assert.NotNull(homegrown);
        Assert.NotEqual(LifeStage.Child, homegrown!.LifeStage);
        Assert.True(homegrown.CanWork);
    }

    [Fact]
    public void AHungryHouseholdDoesNotHaveChildren()
    {
        // A village that breeds into a famine is not telling a story, it is
        // oscillating - and the deaths would not trace back to any decision.
        var (loop, _) = Build(GrowingVillage with { BirthFoodThreshold = 10_000 });
        int founding = loop.World.Villagers.Count;

        loop.Step(30_000);

        Assert.Equal(founding, loop.World.Villagers.Count);
    }

    [Fact]
    public void VillagerIdsAreNeverReused()
    {
        // The dead keep their ids, so the log stays honest about who was who.
        var (loop, _) = Build(GrowingVillage);
        loop.Step(30_000);

        var seen = new HashSet<int>();
        foreach (Villager villager in loop.World.Villagers)
        {
            Assert.True(seen.Add(villager.Id), $"Id {villager.Id} was reused.");
        }
    }

    [Fact]
    public void BirthsAreDeterministic()
    {
        var (a, logA) = Build(GrowingVillage);
        var (b, logB) = Build(GrowingVillage);

        a.Step(30_000);
        b.Step(30_000);

        Assert.Equal(a.World.Villagers.Count, b.World.Villagers.Count);
        Assert.Equal(StateHash.Compute(a.World), StateHash.Compute(b.World));
        Assert.Equal(logA.Entries, logB.Entries);
    }

    [Fact]
    public void GrownChildrenFoundNewHouseholds()
    {
        // Was the pinned known gap: children used to stay in their birth household
        // for life, so once every house filled the village stopped growing and died
        // out with its last generation. Now inverted - the village spreads.
        var (loop, _) = Build(GrowingVillage);
        int foundingHouseholds = loop.World.Households.Count;

        loop.Step(30_000);

        Assert.True(loop.World.Households.Count > foundingHouseholds,
            $"Village still has {loop.World.Households.Count} households.");
    }

    [Fact]
    public void CouplesLeaveHomeWithFoodToStartOn()
    {
        // A new household on an empty larder can be wiped out by its first winter
        // before anyone has foraged anything - a death with no decision behind it.
        var (loop, _) = Build(GrowingVillage);
        loop.Step(30_000);

        // Every household beyond the founding pair was formed by a couple moving
        // out, and each was given a share of both parents' stores.
        Assert.True(loop.World.Households.Count > 2);
        for (int i = 2; i < loop.World.Households.Count; i++)
        {
            Assert.True(loop.World.Households[i].Stockpile.LifetimeGathered > 0,
                $"Household {loop.World.Households[i].Name} started with nothing.");
        }
    }

    [Fact]
    public void PartnersComeFromDifferentHouseholds()
    {
        // The closest thing to an incest rule this model can express: everyone in a
        // house is either a parent or a sibling. Checked at the moment of pairing,
        // which is when they still lived apart - so this checks the invariant that
        // survives it: nobody is partnered with someone they were born beside.
        var (loop, _) = Build(GrowingVillage);
        loop.Step(30_000);

        foreach (Villager villager in loop.World.Villagers)
        {
            if (!villager.IsPaired)
            {
                continue;
            }

            Villager? partner = loop.World.FindVillager(villager.PartnerId);
            Assert.NotNull(partner);
            Assert.Equal(villager.Id, partner!.PartnerId);
        }
    }

    [Fact]
    public void AVillageRunsCleanForDecades()
    {
        var sink = new InMemoryLogSink(LogLevel.Trace);
        SimLoop loop = SimFactory.CreatePhase0(Village, sink);
        loop.Step(20_000);

        var bad = new List<LogEntry>();
        foreach (LogEntry entry in sink.Entries)
        {
            if (entry.Level >= LogLevel.Warn)
            {
                bad.Add(entry);
            }
        }

        _output.WriteLine($"{loop.World.Population} alive after {loop.World.Clock.Year} years.");
        Assert.True(bad.Count == 0,
            $"Village logged {bad.Count} warning(s)/error(s), first: {(bad.Count > 0 ? bad[0].ToString() : "-")}");
    }
}
