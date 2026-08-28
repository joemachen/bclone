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
    public void EveryWorkplaceStartsStaffedByEveryoneWhoFits()
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

    /// <summary>
    /// ⛔ Raising a closed workplace off zero opens it again — <b>and that is the only way
    /// back, because "village decides" is gone</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This test used to be <c>HandingItBackRestoresTheVillagesOwnJudgement</c></b> and it
    /// called <c>SetStaffing(stand, null)</c>. Joe, 2026-08-16: *"i want village decides gone
    /// entirely from all aspects of the game for now."* There is no control that does that any
    /// more and <c>SetStaffing</c> no longer takes a null, so the test turns round rather than
    /// being deleted — <b>the claim it was really making survives intact</b>: a workplace the
    /// player closed must become available again when they change their mind.
    /// </para>
    /// <para>
    /// Asserted on the MECHANISM, not on the village's appetite, and that correction is worth
    /// keeping. An earlier version demanded the stand be staffed again within a few years and
    /// failed — correctly. The village only wants foresters when it wants wood, so a given hut
    /// may honestly go decades unused; demanding otherwise would assert that the quota is
    /// broken.
    /// </para>
    /// </remarks>
    [Fact]
    public void RaisingAClosedWorkplaceOffZeroOpensItAgain()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        loop.Step(Config.TicksPerYear * 5);

        Workplace stand = world.Workplaces.First(w => w.Kind == JobKind.Forester);

        world.SetStaffing(stand, 0);
        Assert.Equal(0, stand.Places);
        Assert.True(stand.IsFull, "A closed workplace must never accept a worker.");

        world.SetStaffing(stand, stand.Capacity);

        Assert.Equal(stand.Capacity, stand.Places);
        Assert.False(
            stand.IsFull,
            "A workplace opened back up still reads as full, so nobody can ever be sent " +
            "there — the number did not really change.");
        Assert.True(stand.OpenPositions > 0);
    }

    /// <summary>
    /// Player intent is sim state, so it has to be in the hash (D42's rule, D51's case).
    /// </summary>
    /// <remarks>
    /// And <b>untouched must hash differently from zero</b>. "Nobody has turned this down" and
    /// "nobody works here, I mean it" are different states of the world that produce different
    /// histories, and a hash that conflated them would let a determinism test pass across a
    /// real divergence — the vacuity D7 exists to prevent. <b>The player can no longer reach
    /// the first state deliberately</b> (2026-08-16), but every workplace still starts in it,
    /// so the distinction is as load-bearing as it ever was.
    /// </remarks>
    [Fact]
    public void TheHashCoversStaffingAndTellsUntouchedFromZero()
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
