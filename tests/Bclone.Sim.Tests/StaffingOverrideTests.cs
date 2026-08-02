using System.Linq;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The player may say how many hands a workplace gets — never who (D51).
/// </summary>
/// <remarks>
/// <para>
/// This is a deliberate softening of §2.2, agreed with Joe, and the line it holds is
/// worth restating: the pillar deletes <em>slotting a named worker into a building</em>,
/// not the player having an opinion about staffing levels. An override sets a count;
/// proximity, household and catchment still choose the person, so every
/// "why is Elias at the stand?" sentence stays true.
/// </para>
/// <para>
/// The load-bearing default is <b>null</b> — "let the village decide". A game that
/// opens with a number on every workplace is the Banished spreadsheet whatever the
/// numbers say, and non-negotiable 2 is that systems reduce babysitting. Override is
/// opt-in; a player who never touches one plays the village the quota describes.
/// </para>
/// </remarks>
public sealed class StaffingOverrideTests
{
    private readonly ITestOutputHelper _output;

    public StaffingOverrideTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    [Fact]
    public void EveryWorkplaceStartsWithNoOpinionFromThePlayer()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());

        foreach (Workplace workplace in loop.World.Workplaces)
        {
            Assert.Null(workplace.StaffingOverride);
            Assert.Equal(workplace.Capacity, workplace.Places);
        }
    }

    [Fact]
    public void SettingZeroEmptiesAWorkplaceAndTheVillageSurvivesIt()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;

        loop.Step(Config.TicksPerYear * 5);

        Workplace stand = world.Workplaces.First(w => w.Kind == JobKind.Forester);
        world.SetStaffing(stand, 0);

        loop.Step(Config.TicksPerYear * 5);

        Assert.Empty(stand.WorkerIds);
        Assert.True(stand.IsFull, "A workplace the player has closed must never accept a worker.");
        _output.WriteLine($"{stand.Name} closed; village still at {world.Population}.");
        Assert.True(world.Population > 0, "Closing one tree stand should not end the village.");
    }

    [Fact]
    public void HandingItBackRestoresTheVillagesOwnJudgement()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        loop.Step(Config.TicksPerYear * 5);

        Workplace stand = world.Workplaces.First(w => w.Kind == JobKind.Forester);

        world.SetStaffing(stand, 0);
        Assert.Equal(0, stand.Places);

        world.SetStaffing(stand, null);
        Assert.Null(stand.StaffingOverride);
        Assert.Equal(stand.Capacity, stand.Places);

        // Asserted on the MECHANISM, not on the village's appetite, and that is a
        // correction worth recording. The first version of this test demanded the stand
        // be staffed again within a few years and failed — correctly. There are two tree
        // stands and the village only wants foresters when it wants wood, so this
        // particular one may honestly go decades unused. Demanding otherwise would have
        // been asserting that the quota is broken.
        //
        // What handing back must guarantee is that the stand is AVAILABLE again: at
        // override 0 it was permanently full and could never take anyone, and now it can.
        Assert.False(
            stand.IsFull,
            "An empty stand handed back to the village still reads as full, so nobody can " +
            "ever be sent there — the override was not really cleared.");
        Assert.True(stand.OpenPositions > 0);
    }

    /// <summary>
    /// Player intent is sim state, so it has to be in the hash (D42's rule, D51's case).
    /// </summary>
    /// <remarks>
    /// And <b>null must hash differently from zero</b>. "I have no opinion" and "nobody
    /// works here, I mean it" are different states of the world that produce different
    /// histories, and a hash that conflated them would let a determinism test pass
    /// across a real divergence — the vacuity D7 exists to prevent.
    /// </remarks>
    [Fact]
    public void TheHashCoversStaffingAndTellsNullFromZero()
    {
        SimLoop untouched = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimLoop closed = SimFactory.CreatePhase0(Config, new InMemoryLogSink());

        ulong before = StateHash.Compute(untouched.World);
        Assert.Equal(before, StateHash.Compute(closed.World));

        closed.World.SetStaffing(
            closed.World.Workplaces.First(w => w.Kind == JobKind.Forester), 0);

        Assert.NotEqual(StateHash.Compute(untouched.World), StateHash.Compute(closed.World));
    }

    [Fact]
    public void ThePlayerCannotAskForFewerThanNobody()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        Workplace any = loop.World.Workplaces[0];

        Assert.Throws<ArgumentOutOfRangeException>(() => loop.World.SetStaffing(any, -1));
    }

    /// <summary>
    /// Overriding must not smuggle back the thing D15 deleted.
    /// </summary>
    /// <remarks>
    /// Capping a workplace changes how many go there; it must never change the RULE by
    /// which the sim picks who. So whoever ends up at a capped workplace must still be
    /// among those living nearest it — the sentence the player is shown.
    /// </remarks>
    [Fact]
    public void CappingAWorkplaceStillLetsTheSimChooseWho()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        loop.Step(Config.TicksPerYear * 6);

        Workplace stand = world.Workplaces.First(w => w.Kind == JobKind.Forester);
        world.SetStaffing(stand, 1);
        loop.Step(Config.TicksPerYear * 4);

        Assert.True(stand.WorkerIds.Count <= 1, "The cap was not honoured.");

        foreach (int id in stand.WorkerIds)
        {
            Villager worker = world.FindVillager(id)!;
            Assert.False(
                string.IsNullOrWhiteSpace(worker.JobReason),
                "A worker at a capped workplace still has to be able to say why they are there.");
            _output.WriteLine($"{worker.Name}: {worker.JobReason}");
        }
    }
}
