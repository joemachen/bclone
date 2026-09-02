using Bclone.Sim.Config;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ Slice 2 of <c>jobs-catalog.md</c> — <b>proving a modder can add a trade</b>.
/// </summary>
/// <remarks>
/// <para>
/// The shape <see cref="ModdedGoodTests"/> established, one enum over: a seventh trade defined in
/// <b>real JSON</b>, parsed by the shipping loader, and driven through the things a trade touches.
/// <b>Constructing <see cref="JobRow"/> objects in C# would prove the sim can hold seven trades;
/// only this proves a modder editing a data file can add one.</b>
/// </para>
/// <para>
/// <b>⛔ NO NEW TRADE SHIPS INTO THE GAME.</b> The fisherman exists only here. Adding one to the
/// shipped catalogue would be inventing content under cover of an infrastructure slice — and
/// `professions.md` has a fisher on its list with a spec of its own to be written.
/// </para>
/// </remarks>
public sealed class ModdedJobTests
{
    private readonly ITestOutputHelper _output;

    public ModdedJobTests(ITestOutputHelper output) => _output = output;

    /// <summary>The modder's own trade — <b>an id the enum has no name for</b>.</summary>
    /// <remarks>
    /// ⚠️ 6 → 7 when fishing shipped (2026-09-02). `JobKind.Boatman` exists now, and this
    /// alias's whole job is to be a trade the enum cannot name.
    /// </remarks>
    private static JobKind Boatman => (JobKind)7;

    /// <summary>
    /// A catalogue with a seventh trade, written the way a modder would write it.
    /// </summary>
    /// <remarks>
    /// The six built-ins are restated because a <c>jobs</c> array <b>replaces</b> the list rather
    /// than appending to it — the wholesale-replacement rule <c>goods</c>, <c>skills</c> and
    /// <c>household_names</c> all follow, and the validator refuses a catalogue missing a built-in
    /// id so it cannot be got half-right.
    /// </remarks>
    private static string JsonWithBoatman => """
    {
      "jobs": [
        { "id": 0, "name": "forager",    "plural": "foragers",    "doing": "gathering",          "works_at": "GathererHut" },
        { "id": 1, "name": "forester",   "plural": "foresters",   "doing": "felling timber",     "works_at": "ForesterHut",   "limited_by": "Logs" },
        { "id": 2, "name": "woodcutter", "plural": "woodcutters", "doing": "splitting firewood", "works_at": "WoodcutterHut", "limited_by": "Firewood" },
        { "id": 3, "name": "marketer",   "plural": "traders",     "doing": "the market",         "works_at": "Market" },
        { "id": 4, "name": "builder",    "plural": "builders",    "doing": "building",           "works_at": "BuilderHut" },
        { "id": 5, "name": "farmer",     "plural": "farmers",     "doing": "farming",            "works_at": "Farmhouse",     "limited_by": "Food" },

        { "id": 6, "name": "fisher",     "plural": "fishers",     "doing": "fishing",            "works_at": "FishingHut",    "limited_by": "Fish" },

        // The modder's own trade. Nothing in the sim has ever heard of it.
        //
        // ⚠️ IT MOVED 6 → 7 WHEN FISHING SHIPPED (2026-09-02), and that is the point rather than
        // an inconvenience: this row exists to be an id the enum cannot name, and 6 stopped being
        // one the day `JobKind.Boatman` existed. **The example mod was a fisherman; the game grew
        // one.** Renamed too — two trades called "fisher" is a failure message nobody can read.
        { "id": 7, "name": "boatman",    "plural": "boatmen",     "doing": "at the water",       "limited_by": "Food" }
      ]
    }
    """;

    private static SimConfig ConfigWithBoatman() =>
        SimConfigLoader.Parse(JsonWithBoatman, "<modded>");

    // -----------------------------------------------------------------

    [Fact]
    public void AModderCanAddATradeInDataAlone()
    {
        var catalog = new JobsCatalog(ConfigWithBoatman().JobsCatalog);

        // 7 → 8 when the built-in fisher shipped (2026-09-02).
        Assert.Equal(8, catalog.Count);

        // ⭐ Everything the sim used to answer with a switch, answered for a trade no switch has
        // ever named.
        Assert.Equal("boatman", catalog.NameOf(Boatman));
        Assert.Equal("boatmen", catalog.PluralOf(Boatman));
        Assert.Equal("at the water", catalog.DoingOf(Boatman));

        _output.WriteLine($"{catalog.NameOf(Boatman)} / {catalog.PluralOf(Boatman)} — "
            + $"{catalog.DoingOf(Boatman)}");
    }

    [Fact]
    public void ATradeMayHaveNoWorkplaceAtAll()
    {
        var catalog = new JobsCatalog(ConfigWithBoatman().JobsCatalog);

        // ⭐ Legal and deliberate. A laborer is already *"the villagers no job currently wants"*
        // (D66) — a trade with no building of its own is a shape this game already has, so a
        // modder is not forced to invent one.
        Assert.Null(catalog.WorksAt(Boatman));
        Assert.Equal(BuildingKind.Farmhouse, catalog.WorksAt(JobKind.Farmer));
    }

    [Fact]
    public void TheBuiltInSixAreUntouchedByAdditions()
    {
        var catalog = new JobsCatalog(ConfigWithBoatman().JobsCatalog);

        // ⛔ Ids are appended, never renumbered — every golden and every saved staffing figure is
        // pinned to them.
        Assert.Equal("forager", catalog.NameOf(JobKind.Forager));
        Assert.Equal("farmer", catalog.NameOf(JobKind.Farmer));

        // ⭐ And the split that must NOT be settled by accident (D188): the marketer is "traders"
        // to the staffing panel and "marketer" on the roster. Both words survive the round trip.
        Assert.Equal("marketer", catalog.NameOf(JobKind.Marketer));
        Assert.Equal("traders", catalog.PluralOf(JobKind.Marketer));
    }

    /// <summary>
    /// ⭐⭐ The id is the contract; the order of the lines in the file is not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ THIS TEST EXISTS BECAUSE THE OTHERS WERE GREEN AND BLIND, AND A RED CHECK FOUND IT.</b>
    /// Breaking <c>JobsCatalog</c> to place rows by <em>file position</em> instead of by their
    /// stated id left <b>every one of the other guards passing</b> — because the catalogue above
    /// happens to list its rows in id order, so the two are indistinguishable.
    /// </para>
    /// <para>
    /// <b>That is D157's finding for the third time:</b> a guard whose fixture makes the bug
    /// impossible reports a number that is true and proves nothing. <em>The only way to test
    /// "position is not the contract" is with a file whose positions disagree with its ids.</em>
    /// </para>
    /// </remarks>
    [Fact]
    public void ReorderingTheFileDoesNotReinterpretTheTrades()
    {
        // The same seven trades, listed backwards. A modder who sorts their file alphabetically,
        // or appends where it reads best, must get the same village.
        string shuffled = """
        {
          "jobs": [
            { "id": 6, "name": "fisher",     "plural": "fishers",     "doing": "fishing", "works_at": "FishingHut", "limited_by": "Fish" },
            { "id": 7, "name": "boatman",    "plural": "boatmen",     "doing": "at the water" },
            { "id": 5, "name": "farmer",     "plural": "farmers",     "doing": "farming",            "works_at": "Farmhouse",     "limited_by": "Food" },
            { "id": 4, "name": "builder",    "plural": "builders",    "doing": "building",           "works_at": "BuilderHut" },
            { "id": 3, "name": "marketer",   "plural": "traders",     "doing": "the market",         "works_at": "Market" },
            { "id": 2, "name": "woodcutter", "plural": "woodcutters", "doing": "splitting firewood", "works_at": "WoodcutterHut", "limited_by": "Firewood" },
            { "id": 1, "name": "forester",   "plural": "foresters",   "doing": "felling timber",     "works_at": "ForesterHut",   "limited_by": "Logs" },
            { "id": 0, "name": "forager",    "plural": "foragers",    "doing": "gathering",          "works_at": "GathererHut" }
          ]
        }
        """;

        var catalog = new JobsCatalog(SimConfigLoader.Parse(shuffled, "<shuffled>").JobsCatalog);

        // ⛔ If position won, `Goods.Forager` would read "boatman" here — a village whose every
        // trade quietly means something else, with nothing to see and nothing thrown.
        Assert.Equal("forager", catalog.NameOf(JobKind.Forager));
        Assert.Equal("forester", catalog.NameOf(JobKind.Forester));
        Assert.Equal("farmer", catalog.NameOf(JobKind.Farmer));
        Assert.Equal("boatman", catalog.NameOf(Boatman));

        // And the cross-references survive with them, not just the names.
        Assert.Equal(BuildingKind.GathererHut, catalog.WorksAt(JobKind.Forager));
        Assert.Equal(Goods.Firewood, catalog.LimitedBy(JobKind.Woodcutter));

        _output.WriteLine($"backwards file, forager still reads: {catalog.NameOf(JobKind.Forager)}");
    }

    // -----------------------------------------------------------------
    //  The quota has room for it
    // -----------------------------------------------------------------

    [Fact]
    public void TheQuotaHasASlotForATradeTheEnumHasNeverHeardOf()
    {
        // ⭐ Six named fields could never grow. The quota is an array sized to the catalogue, so
        // a seventh trade has somewhere to be counted.
        var quota = new LabourQuota(
            hands: 8, mouths: 10, foragersToFeedEveryone: 2,
            foragers: 2, foresters: 1, woodcutters: 1, marketers: 1, builders: 1, farmers: 2,
            slots: 7);

        Assert.Equal(7, quota.Slots);

        // The built-ins still read exactly as they did, through the named readers.
        Assert.Equal(2, quota.Foragers);
        Assert.Equal(2, quota.Farmers);
        Assert.Equal(1, quota.Marketers);

        // And the seventh reads zero rather than throwing — the village simply has no opinion
        // about it yet.
        Assert.Equal(0, quota.For(Boatman));
    }

    [Fact]
    public void AskingAboutATradeWithNoSlotIsZeroRatherThanACrash()
    {
        var quota = new LabourQuota(
            hands: 4, mouths: 4, foragersToFeedEveryone: 1,
            foragers: 1, foresters: 1, woodcutters: 1);

        // ⚠️ The old switch's default arm said zero for an unknown trade, and the index says the
        // same. *The village wants nobody on a trade it has never heard of* is the honest answer,
        // and it must not become an exception thrown from inside the allocator.
        Assert.Equal(0, quota.For((JobKind)42));
    }

    // -----------------------------------------------------------------
    //  The validator catches what a modder gets wrong
    // -----------------------------------------------------------------

    [Fact]
    public void ACatalogueThatOmitsABuiltInIsRefusedAtLoad()
    {
        string missingForager = """
        {
          "jobs": [
            { "id": 1, "name": "forester", "plural": "foresters", "doing": "felling timber" }
          ]
        }
        """;

        SimConfigException error = Assert.Throws<SimConfigException>(
            () => SimConfigLoader.Parse(missingForager, "<broken>"));

        Assert.Contains("missing id 0", error.Message);
        _output.WriteLine(error.Message);
    }

    [Fact]
    public void ATradeWithNoPluralIsRefusedAtLoad()
    {
        string noPlural = JsonWithBoatman.Replace(
            """fisher",     "plural": "fishers",""",
            """fisher",     "plural": "",""");

        SimConfigException error = Assert.Throws<SimConfigException>(
            () => SimConfigLoader.Parse(noPlural, "<broken>"));

        // ⭐ Both words are required precisely because they are allowed to differ (D188). A row
        // that gives only one of them would force the sim to invent the other, which is how a
        // vocabulary split gets settled by accident.
        Assert.Contains("name and a plural", error.Message);
        _output.WriteLine(error.Message);
    }

    [Fact]
    public void TwoTradesSharingAnIdAreRefusedAtLoad()
    {
        string duplicated = JsonWithBoatman.Replace(
            """{ "id": 6, "name": "fisher",""",
            """{ "id": 5, "name": "fisher",""");

        SimConfigException error = Assert.Throws<SimConfigException>(
            () => SimConfigLoader.Parse(duplicated, "<broken>"));

        // Ids are what a staffing figure is stored and hashed under, so two trades sharing one
        // would share a quota.
        Assert.Contains("repeats id 5", error.Message);
        _output.WriteLine(error.Message);
    }
}
