using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// A building that cannot do its job says so — Joe, D147.
/// </summary>
/// <remarks>
/// <para>
/// <i>"Idle huts should get an indicator like full storage buildings."</i> D140 put a ring on a
/// store with no room; this is the same idea for a workplace that is not working, and it earns
/// its place because <b>three times in one session a building looked idle because of a number
/// set on a different panel</b> — a log limit (D145), a firewood limit (D139), ground nobody
/// ever painted (D86).
/// </para>
/// <para>
/// <b>⚠️ The ring is view code and untestable (D11); the RULE is not, and the rule is the whole
/// risk.</b> "Idle" is not one fact the way "full" is — a workplace has half a dozen reasons for
/// having nothing to do and most of them are fine. A marker that lit up for all of them would be
/// wallpaper, which is what D42 and D123 moved <em>out</em> of the Overview. So these guards are
/// mostly about what must <b>not</b> light up.
/// </para>
/// </remarks>
public sealed class IdleWorkplaceTests
{
    private readonly ITestOutputHelper _output;

    public IdleWorkplaceTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Loop() => SimFactory.CreatePhase0(Config, new InMemoryLogSink());

    private static Workplace FirstOf(SimWorld world, JobKind kind)
    {
        foreach (Workplace workplace in world.Workplaces)
        {
            if (workplace.Kind == kind && !workplace.IsSite)
            {
                return workplace;
            }
        }

        throw new Xunit.Sdk.XunitException($"The founding village has no {kind}.");
    }

    // ---------------------------------------------------------------
    //  What lights up
    // ---------------------------------------------------------------

    /// <summary>⭐ A building nobody is working says so.</summary>
    /// <remarks>
    /// The commonest case by far, and the one the ring exists for: you raised a hut and never
    /// put anybody in it. Since D136 nobody is staffed by default, so this is the state every
    /// new building starts in.
    /// </remarks>
    [Fact]
    public void ABuildingWithNobodyInItSaysSo()
    {
        SimWorld world = Loop().World;
        Workplace hut = FirstOf(world, JobKind.Forester);
        hut.WorkerIds.Clear();

        string? note = world.IdleNote(hut);
        _output.WriteLine(note ?? "(working)");

        Assert.NotNull(note);
        Assert.Contains(hut.Name, note!, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>⭐ And a cap set on another panel is named, which is the point of the feature.</summary>
    /// <remarks>
    /// <b>The one case where "the player already knows" is not good enough.</b> A met log limit
    /// is set on the stock panel and stops a building somewhere else on the map; without this
    /// the hut is silently doing nothing and the only clue is a number two windows away. Every
    /// other reason in this file is visible on the building itself.
    /// </remarks>
    [Fact]
    public void AHutStoppedByAStockLimitNamesTheLimit()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        loop.Step(Config.TicksPerYear * 5);

        Workplace hut = FirstOf(world, JobKind.Woodcutter);

        // ⚠️ THE WORKER IS PUT THERE BY HAND, AND D146 IS WHY. `SetStaffing` is a ceiling, not
        // a summons — it caps what the allocator may fill and cannot conjure demand — so a
        // fixture that merely raises the number and steps is at the mercy of whether the
        // village happens to want a woodcutter that season. `IdleNote` reads `WorkerIds`, so
        // posing the case directly is both honest and stable.
        hut.WorkerIds.Add(world.Villagers[0].Id);

        // And something to split, or the note would be about logs rather than the limit.
        world.AnyStoreOf(StoreKind.Shed).Store.Add(Goods.Logs, Config.LogsPerSplit * 4);
        Assert.Null(world.IdleNote(hut));

        world.SetStockLimit(Goods.Firewood, 0);
        string? note = world.IdleNote(hut);
        _output.WriteLine(note ?? "(working)");

        Assert.NotNull(note);
        Assert.Contains("firewood", note!, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A forester with no ground has nothing to work, and the fix is a brush stroke.</summary>
    [Fact]
    public void AForesterWithNoGroundAsksForSome()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        Workplace hut = FirstOf(world, JobKind.Forester);
        foreach (int index in new System.Collections.Generic.List<int>(world.Zones.WorkGroundOf(hut.Id)))
        {
            world.EraseWorkGround(hut, world.Zones.PositionOf(index));
        }

        Assert.Equal(0, world.Zones.WorkGroundTiles(hut.Id));
        hut.WorkerIds.Add(world.Villagers[0].Id);

        string? note = world.IdleNote(hut);
        _output.WriteLine(note ?? "(working)");

        Assert.NotNull(note);
        Assert.Contains("ground", note!, System.StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------
    //  ⭐ What must NOT light up — the half that keeps it off the wallpaper
    // ---------------------------------------------------------------

    /// <summary>⭐ A building the player emptied on purpose is obeyed, not nagged about.</summary>
    /// <remarks>
    /// D42's rule, and the reason D136 fought to keep <c>null</c> and <c>0</c> different states:
    /// <em>nobody has said</em> and <em>I want nobody here</em> are different instructions, and
    /// only the first one is a problem to point at.
    /// </remarks>
    [Fact]
    public void AHutThePlayerEmptiedIsNotComplainedAbout()
    {
        SimWorld world = Loop().World;
        Workplace hut = FirstOf(world, JobKind.Forester);

        hut.WorkerIds.Clear();
        Assert.NotNull(world.IdleNote(hut));

        world.SetStaffing(hut, 0);
        _output.WriteLine($"staffing 0: {world.IdleNote(hut) ?? "(silent)"}");

        Assert.Null(world.IdleNote(hut));
    }

    /// <summary>⭐ A gatherer in winter is not a fault.</summary>
    /// <remarks>
    /// Seasonal, expected, and nothing the player can do — the definition of a marker that
    /// teaches people to ignore markers. <b>Anti-vacuity matters here:</b> the guard has to
    /// prove the hut really is unable to gather, or it would pass on a summer village.
    /// </remarks>
    [Fact]
    public void AGathererInWinterIsNotFlagged()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        Workplace hut = FirstOf(world, JobKind.Forager);

        bool sawWinter = false;
        for (int tick = 0; tick < Config.TicksPerYear * 2 && !sawWinter; tick++)
        {
            loop.StepOnce();
            if (SeasonRules.IsGatherable(world.Clock.Season) || hut.WorkerIds.Count == 0)
            {
                continue;
            }

            sawWinter = true;
            string? note = world.IdleNote(hut);
            _output.WriteLine($"{world.Clock.Season}, {hut.WorkerIds.Count} at the hut: "
                + (note ?? "(silent)"));

            Assert.Null(note);
        }

        Assert.True(sawWinter, "Never saw a staffed gatherer's hut in winter, so this is vacuous.");
    }

    /// <summary>⭐ A forester that is replanting is working, not idle (D146).</summary>
    [Fact]
    public void AForesterPuttingItsGroundBackIsWorking()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        Workplace hut = FirstOf(world, JobKind.Forester);
        world.SetStaffing(hut, hut.Capacity);

        // ⛔ SOMEBODY IS KEPT ON THE TRADE, RATHER THAN HOPED FOR (D262). This ran a year and
        // trusted the quota to post a forester, and with a two-seat gathering hut it no longer
        // does reliably — measured: *"nobody was ever posted to the forester's hut."* ⭐ The
        // subject here is the IDLE NOTE, not who the village chooses to employ, so the villager
        // is pinned: `SetPinnedTrade` is the player's own control, and using it is a plainer
        // premise than any amount of stepping and hoping.
        foreach (Villager villager in world.Villagers)
        {
            if (villager.Alive && villager.CanWork)
            {
                world.SetPinnedTrade(villager, JobKind.Forester);
                break;
            }
        }

        loop.Step(Config.TicksPerYear);

        // Fell its ground flat, then cap the logs. It may not fell and it has everything to
        // plant — which is work, and D146's whole ordering.
        foreach (int index in world.Zones.WorkGroundOf(hut.Id))
        {
            world.Harvest(world.Zones.PositionOf(index));
        }

        world.SetStockLimit(Goods.Logs, 0);
        Assert.False(world.MayFell(hut));
        Assert.True(hut.WorkerIds.Count > 0, "Nobody was ever posted to the forester's hut.");

        _output.WriteLine(
            $"capped and bald: {world.IdleNote(hut) ?? "(silent — it is planting)"}");

        Assert.Null(world.IdleNote(hut));
    }

    // ---------------------------------------------------------------
    //  § Why nobody is here — Joe, 2026-08-30
    // ---------------------------------------------------------------

    /// <summary>
    /// ⛔⛔ An empty building the village has no use for is <b>not</b> flagged.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ JOE'S COMPLAINT, AND IT IS THIS FILE'S OWN RULE BROKEN BY THIS FILE'S OWN METHOD:</b>
    /// *"i dont like that because the village 'wants' 0 of a type of work, the workplace shows as
    /// unstaffed … they are inconsistent now and i want them to be aligned."* `IdleNote` returned
    /// *"Nobody is working forester's hut 2."* for **every** unstaffed workplace, so a hut standing
    /// quiet because the player's own log limit was met got the same marker as one the village was
    /// crying out for.
    /// </para>
    /// <para>
    /// ⚠️ That is *"a marker that lit up for all of them would be wallpaper"* — the sentence at
    /// the top of this file — arriving in the one branch nobody had applied it to.
    /// </para>
    /// </remarks>
    [Fact]
    public void AHutTheVillageWantsNobodyAtIsNotFlagged()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        Workplace hut = FirstOf(world, JobKind.Forester);

        // The player's own cap, met: the village wants no foresters and this is not a fault.
        // Stepped a full pass so the allocator empties the hut rather than the test doing it.
        world.SetStockLimit(Goods.Logs, 0);
        loop.Step(Config.TicksPerYear + 1);

        _output.WriteLine($"wants {LabourQuota.For(world).For(JobKind.Forester)} foresters; "
            + $"note: {world.IdleNote(hut) ?? "(silent)"}");

        Assert.Equal(0, LabourQuota.For(world).For(JobKind.Forester));
        Assert.Null(world.IdleNote(hut));
    }

    /// <summary>
    /// ⭐ And the sentence says <b>why</b>, in the same words both panels read.
    /// </summary>
    /// <remarks>
    /// <b>⛔ THE POINT IS THAT THERE IS ONE SOURCE.</b> The inspector and the professions column
    /// were each explaining the same decision in their own words, which is how D139 and D195 both
    /// started. <c>WhyTheVillageWantsNone</c> reads the state the quota reads, in the quota's own
    /// order, and calls <c>StoppedByAStockLimit</c> rather than restating it.
    /// </remarks>
    [Fact]
    public void TheVillageSaysWhyItWantsNobodyOnATrade()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        world.SetStockLimit(Goods.Logs, 0);
        loop.StepOnce();

        string? why = LabourQuota.WhyTheVillageWantsNone(world, JobKind.Forester);
        _output.WriteLine(why ?? "(no reason given)");

        Assert.NotNull(why);
        Assert.Contains("limit", why!, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// ⛔ It gives no reason rather than a wrong one when the village <em>does</em> want hands.
    /// </summary>
    /// <remarks>
    /// <b>The anti-vacuity half (D7).</b> A method that always returns a sentence would pass the
    /// guard above while saying something untrue at every other moment of the game — and a wrong
    /// cause on screen is worse than none, because the player acts on it.
    /// </remarks>
    [Fact]
    public void ItNamesNoReasonWhenTheVillageWantsTheWorkDone()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;
        loop.StepOnce();

        int wanted = LabourQuota.For(world).For(JobKind.Forager);
        string? why = LabourQuota.WhyTheVillageWantsNone(world, JobKind.Forager);

        _output.WriteLine($"the village wants {wanted} foragers; reason given: {why ?? "(none)"}");

        Assert.True(wanted > 0, "The fixture wants no foragers, so this guard proves nothing.");
        Assert.Null(why);
    }

    /// <summary>A construction site explains itself in the build queue, not with a ring.</summary>
    [Fact]
    public void AConstructionSiteIsNeverFlagged()
    {
        SimLoop loop = Loop();
        SimWorld world = loop.World;

        GridPos site = world.Map.FoundingSite;
        Assert.True(world.Mark(BuildingKind.Granary, new GridPos(site.X + 5, site.Y + 5)).Allowed);

        Workplace marked = Assert.Single(
            world.Workplaces, place => place.Construction?.Kind == BuildingKind.Granary);

        Assert.Empty(marked.WorkerIds);
        Assert.Null(world.IdleNote(marked));
    }
}
