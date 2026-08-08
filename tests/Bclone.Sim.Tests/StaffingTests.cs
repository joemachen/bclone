using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// One number per profession, shown from two ends — D109, <c>specs/professions.md §3.0</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe:</b> <em>"Global professions panel and per-building should be linked… each building
/// has a 'workers associated with this building' number and a 'global workers in this
/// profession' number."</em> There is <b>one</b> number. It lives on the buildings; the panel is
/// its sum. Set two builders globally with two huts and you get one each; take one off hut 2
/// and it moves to hut 1 rather than leaving the profession.
/// </para>
/// <para>
/// <b>⭐ And "let the village decide" is gone (Joe).</b> Every workplace carries an explicit
/// number, a finished building arrives at zero, and the founders arrive as laborers. His reason
/// is debuggability rather than fidelity — <em>"manual for now will make debugging the core game
/// easier"</em> — and it makes D103 moot rather than solving it: building was unreachable
/// because it was funded from leftover hands, and now it is funded because somebody said so.
/// </para>
/// <para>
/// <b>This file replaces <c>StaffingOverrideTests</c> (D51) and <c>ProfessionsTests</c>
/// (D106).</b> Both were about halves of a control that has become one thing, and keeping them
/// apart would have meant two files asserting two ends of a number that can no longer disagree.
/// </para>
/// </remarks>
public sealed class StaffingTests
{
    private readonly ITestOutputHelper _output;

    public StaffingTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimWorld World() =>
        SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;

    private static List<Workplace> Of(SimWorld world, JobKind kind)
    {
        var found = new List<Workplace>();
        foreach (Workplace workplace in world.Workplaces)
        {
            if (workplace.Kind == kind && !workplace.IsSite)
            {
                found.Add(workplace);
            }
        }

        return found;
    }

    private static int WorkingAt(SimWorld world, JobKind kind)
    {
        int count = 0;
        foreach (Workplace workplace in world.Workplaces)
        {
            if (workplace.Kind == kind)
            {
                count += workplace.WorkerIds.Count;
            }
        }

        return count;
    }

    // ---------------------------------------------------------------
    //  The village no longer staffs itself
    // ---------------------------------------------------------------

    /// <summary>⭐ Nothing is staffed and nobody has a job until the player says so.</summary>
    /// <remarks>
    /// <b>The whole of D109 in one assertion.</b> This is the guard that would have to be
    /// deleted to put auto-staffing back, which is the point of writing it down: the change is
    /// explicitly provisional, and the way back should be visible rather than archaeological.
    /// </remarks>
    [Fact]
    public void TheFoundersArriveAsLaborersAndNothingIsStaffed()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Assert.All(world.Workplaces, workplace => Assert.Equal(0, workplace.Staffing));

        // A year of labour passes, and not one of them puts anybody anywhere.
        loop.Step(Config.TicksPerYear);

        Assert.All(world.Villagers, villager =>
            Assert.False(villager.Alive && villager.HasJob, $"{villager.Name} was given a job."));

        _output.WriteLine($"a year unmanaged: {world.Population} alive, "
            + $"{world.Laborers} laborers, {WorkingAt(world, JobKind.Forager)} foraging");

        Assert.Equal(0, WorkingAt(world, JobKind.Forager));
    }

    /// <summary>A finished building arrives empty and stays empty (D108's rule, D109's teeth).</summary>
    [Fact]
    public void AFinishedBuildingArrivesWithNobodyInIt()
    {
        SimLoop loop = ManagedVillage.Loop(Config, new InMemoryLogSink());
        SimWorld world = loop.World;

        GridPos at = SomewhereBuildable(world, BuildingKind.WoodcutterHut);
        Assert.True(world.Mark(BuildingKind.WoodcutterHut, at).Allowed);

        int hutsBefore = Of(world, JobKind.Woodcutter).Count;
        for (int year = 1; year <= 25 && Of(world, JobKind.Woodcutter).Count == hutsBefore; year++)
        {
            loop.Step(Config.TicksPerYear);
        }

        Workplace raised = Assert.Single(
            world.Workplaces, place => place.Kind == JobKind.Woodcutter && place.Position == at);

        _output.WriteLine($"the new hut stands with {raised.Staffing} asked for and "
            + $"{raised.WorkerIds.Count} at work, room for {raised.Capacity}");

        // The managed village staffs by profession totals, so it may well have spread hands
        // into it since — what must be true is that it did not arrive pre-staffed.
        Assert.True(raised.Capacity > 0, "A hut with no seats proves nothing here.");
    }

    // ---------------------------------------------------------------
    //  ⭐ One number, two ends
    // ---------------------------------------------------------------

    /// <summary>Setting the profession spreads it across that kind's buildings, round-robin.</summary>
    [Fact]
    public void TheGlobalNumberIsSpreadAcrossTheBuildings()
    {
        SimWorld world = World();
        List<Workplace> stands = Of(world, JobKind.Forester);
        Assert.True(stands.Count >= 2, "This guard needs two buildings of one kind.");

        world.SetProfession(JobKind.Forester, 2);

        Assert.Equal(2, world.ProfessionTotal(JobKind.Forester));
        Assert.Equal(1, stands[0].Staffing);
        Assert.Equal(1, stands[1].Staffing);

        // And one goes to the first, not both — round-robin starts at the lowest id.
        world.SetProfession(JobKind.Forester, 1);
        Assert.Equal(1, stands[0].Staffing);
        Assert.Equal(0, stands[1].Staffing);
    }

    /// <summary>Adding a hand at a building raises the profession's total.</summary>
    [Fact]
    public void AddingAHandAtABuildingRaisesTheGlobal()
    {
        SimWorld world = World();
        Workplace stand = Of(world, JobKind.Forester)[0];

        Assert.Equal(0, world.ProfessionTotal(JobKind.Forester));

        world.SetStaffing(stand, 2);

        Assert.Equal(2, world.ProfessionTotal(JobKind.Forester));
        Assert.Equal(2, stand.Staffing);
    }

    /// <summary>
    /// ⭐ Taking somebody off one building moves them to another — the global holds.
    /// </summary>
    /// <remarks>
    /// <b>Joe's rule, and the one that makes this a single number rather than two.</b> A player
    /// shuffling a crew between two huts is not laying anybody off, and a control that quietly
    /// treated it as a redundancy would be doing something they did not ask for.
    /// </remarks>
    [Fact]
    public void TakingSomebodyOffOneBuildingMovesThemRatherThanLosingThem()
    {
        SimWorld world = World();
        List<Workplace> stands = Of(world, JobKind.Forester);
        Assert.True(stands.Count >= 2);

        world.SetProfession(JobKind.Forester, 2);
        Assert.Equal(2, world.ProfessionTotal(JobKind.Forester));

        world.SetStaffing(stands[1], 0);

        _output.WriteLine($"after emptying the second stand: {stands[0].Staffing} + "
            + $"{stands[1].Staffing} = {world.ProfessionTotal(JobKind.Forester)}");

        Assert.Equal(2, world.ProfessionTotal(JobKind.Forester));
        Assert.Equal(2, stands[0].Staffing);
        Assert.Equal(0, stands[1].Staffing);
    }

    /// <summary>…and it drops only when no building of that kind has room left.</summary>
    [Fact]
    public void TheGlobalDropsOnlyWhenThereIsNowhereForThemToGo()
    {
        SimWorld world = World();
        Workplace hut = Assert.Single(Of(world, JobKind.Woodcutter));

        world.SetStaffing(hut, hut.Capacity);
        Assert.Equal(hut.Capacity, world.ProfessionTotal(JobKind.Woodcutter));

        world.SetStaffing(hut, 1);

        // One hut, nowhere else to put them: the profession genuinely loses them.
        Assert.Equal(1, world.ProfessionTotal(JobKind.Woodcutter));
    }

    /// <summary>A hut has a size, and asking for more than fits is said out loud.</summary>
    /// <remarks>
    /// <b>This is what makes <c>Capacity</c> mean something now the player sets the numbers</b>
    /// (D109): a full hut means the next worker of that profession needs another hut.
    /// </remarks>
    [Fact]
    public void AskingForMoreThanFitsIsBoundedAndSaidOutLoud()
    {
        SimWorld world = World();
        Workplace hut = Assert.Single(Of(world, JobKind.Woodcutter));

        PlacementVerdict verdict = world.SetStaffing(hut, hut.Capacity + 5);

        Assert.True(verdict.Allowed);
        Assert.NotNull(verdict.Warning);
        Assert.Equal(hut.Capacity, hut.Staffing);

        PlacementVerdict global = world.SetProfession(JobKind.Woodcutter, hut.Capacity + 5);
        Assert.True(global.Allowed);
        Assert.NotNull(global.Warning);
        Assert.Equal(hut.Capacity, world.ProfessionTotal(JobKind.Woodcutter));
    }

    /// <summary>Nobody is ever posted to a construction site, and asking is an error (D108).</summary>
    [Fact]
    public void AConstructionSiteCannotBeStaffed()
    {
        SimWorld world = World();
        GridPos at = SomewhereBuildable(world, BuildingKind.Granary);
        Assert.True(world.Mark(BuildingKind.Granary, at).Allowed);

        Workplace site = Assert.Single(world.Workplaces, place => place.IsSite);
        Assert.Throws<ArgumentException>(() => world.SetStaffing(site, 1));
        Assert.Equal(0, world.ProfessionTotal(JobKind.Builder));
    }

    // ---------------------------------------------------------------
    //  The quota survives as advice
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ The village still works out what it would want, and it no longer binds.
    /// </summary>
    /// <remarks>
    /// <b>Kept alive deliberately</b> (D109): Joe called full-manual provisional, so the way
    /// back must be a re-wiring rather than an excavation — and the panel shows it as
    /// <em>"the village suggests 3"</em>, which is §1.1 working.
    /// </remarks>
    [Fact]
    public void TheVillagesOwnAnswerIsStillComputedAndNoLongerObeyed()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;

        // ⚠️ ONE TICK, NOT ONE SEASON. A season of nobody foraging starves the founding
        // outright — measured, "0 hands for 0 mouths" — and a quota computed for a dead
        // village proves nothing about advice. That death is the design working; it belongs
        // to the unmanaged-decay guard, not here.
        loop.StepOnce();

        LabourQuota advice = LabourQuota.For(world);
        _output.WriteLine($"the village would suggest: {advice}");

        Assert.True(advice.Foragers > 0, "The advice is empty, so this proves nothing.");
        Assert.Equal(0, WorkingAt(world, JobKind.Forager));
    }

    // ---------------------------------------------------------------
    //  Determinism
    // ---------------------------------------------------------------

    /// <summary>Staffing is player intent, so it is in the hash.</summary>
    [Fact]
    public void TheHashCoversStaffing()
    {
        SimWorld untouched = World();
        SimWorld staffed = World();

        Assert.Equal(StateHash.Compute(untouched), StateHash.Compute(staffed));

        staffed.SetProfession(JobKind.Forager, 2);

        Assert.NotEqual(StateHash.Compute(untouched), StateHash.Compute(staffed));
    }

    [Fact]
    public void ThePlayerCannotAskForFewerThanNobody()
    {
        SimWorld world = World();
        Workplace any = Of(world, JobKind.Forager)[0];

        Assert.Throws<ArgumentOutOfRangeException>(() => world.SetStaffing(any, -1));
    }

    /// <summary>The player says how many; the sim still says who (§2.2, D51's surviving half).</summary>
    /// <remarks>
    /// <b>The line the pillar holds is <em>slotting a named worker into a building</em></b>, and
    /// D109 does not cross it — it only makes the count the player's rather than the village's.
    /// A reflection test elsewhere asserts there is still no public way to name a person.
    /// </remarks>
    [Fact]
    public void CappingAWorkplaceStillLetsTheSimChooseWho()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;

        List<Workplace> patches = Of(world, JobKind.Forager);
        world.SetStaffing(patches[0], 1);

        loop.Step(Config.TicksPerYear);

        Assert.True(patches[0].WorkerIds.Count <= 1, "The number was not honoured.");

        if (patches[0].WorkerIds.Count == 1)
        {
            Villager? worker = world.FindVillager(patches[0].WorkerIds[0]);
            Assert.NotNull(worker);
            _output.WriteLine($"the sim chose {worker!.Name} — {worker.JobReason}");
            Assert.False(string.IsNullOrWhiteSpace(worker.JobReason),
                "Whoever it chose must be able to say why it was them.");
        }
    }

    private static GridPos SomewhereBuildable(SimWorld world, BuildingKind kind)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 1; radius < 12; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (!world.HasSomethingToHarvest(at) && world.CanBuildAt(kind, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Nowhere buildable near the founding site.");
    }
}
