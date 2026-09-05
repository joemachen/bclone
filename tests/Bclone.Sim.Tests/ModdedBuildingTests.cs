using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ Slice 2 of <c>buildings-catalog.md</c> — <b>proving a modder can add a building</b>.
/// </summary>
/// <remarks>
/// <para>
/// The shape <see cref="ModdedGoodTests"/> and <see cref="ModdedJobTests"/> established, on the
/// last of the four enums D168 named: an eleventh building defined in <b>real JSON</b>, parsed by
/// the shipping loader, and driven through the things a building touches. <b>Constructing
/// <see cref="BuildingRow"/> objects in C# would prove the sim can hold eleven buildings; only this
/// proves a modder editing a data file can add one.</b>
/// </para>
/// <para>
/// <b>⛔ NO NEW BUILDING SHIPS INTO THE GAME.</b> The boathouse exists only here. Adding one to the
/// shipped catalogue would be inventing content under cover of an infrastructure slice — and what
/// goes in the catalogue is Joe's content call and Phase 4's business.
/// </para>
/// </remarks>
public sealed class ModdedBuildingTests
{
    private readonly ITestOutputHelper _output;

    public ModdedBuildingTests(ITestOutputHelper output) => _output = output;

    private const int BoathouseId = 10;

    private static BuildingKind Boathouse => (BuildingKind)BoathouseId;

    /// <summary>The modder's own trade — <b>an id the enum has no name for</b>.</summary>
    /// <remarks>
    /// ⚠️ <b>It was 6 until fishing shipped (2026-09-02) and had to move to 7</b>, because
    /// `JobKind.Fisher` now exists and this alias's whole job is to be a trade the enum cannot
    /// name. Renamed with it: two trades called "fisher" in one file is a failure message nobody
    /// can read.
    /// </remarks>
    // ⚠️ 8, not 7 — `JobKind.Hunter` took 7 when hunting shipped.
    private static JobKind Boatman => (JobKind)8;

    /// <summary>
    /// A catalogue with an eleventh building and a seventh trade that staffs it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The two lists go together on purpose.</b> A building nobody works at would prove only
    /// half of it; the point of the slice is that <c>works_at</c> can name a building that did not
    /// exist when the sim was written — `jobs-catalog.md §3`'s recorded seam.
    /// </para>
    /// <para>
    /// The built-ins are restated because a <c>buildings</c> array <b>replaces</b> the list rather
    /// than appending to it, and the validator refuses a catalogue missing a built-in id so it
    /// cannot be got half-right.
    /// </para>
    /// <para>
    /// ⚠️ <b>Every recipe here is a literal</b>, where the shipped defaults read their numbers off
    /// the config keys. That is the difference the row exists to make: a modder has no
    /// <c>granary_logs</c> to appeal to, so they state the cost.
    /// </para>
    /// </remarks>
    private static string JsonWithABoathouse => """
    {
      "jobs": [
        { "id": 0, "name": "forager",    "plural": "foragers",    "doing": "gathering",          "works_at": "GathererHut" },
        { "id": 1, "name": "forester",   "plural": "foresters",   "doing": "felling timber",     "works_at": "ForesterHut",   "limited_by": "Logs" },
        { "id": 2, "name": "woodcutter", "plural": "woodcutters", "doing": "splitting firewood", "works_at": "WoodcutterHut", "limited_by": "Firewood" },
        { "id": 3, "name": "marketer",   "plural": "traders",     "doing": "the market",         "works_at": "Market" },
        { "id": 4, "name": "builder",    "plural": "builders",    "doing": "building",           "works_at": "BuilderHut" },
        { "id": 5, "name": "farmer",     "plural": "farmers",     "doing": "farming",            "works_at": "Farmhouse",     "limited_by": "Food" },

        { "id": 6, "name": "fisher",     "plural": "fishers",     "doing": "fishing",            "works_at": "FishingHut",    "limited_by": "Fish" },

        // The modder's own trade, and it staffs the modder's own building — by an id the enum
        // has no name for.
        //
        // ⚠️ IT MOVED 6 → 7 WHEN FISHING SHIPPED (2026-09-02), and the move is the point rather
        // than an inconvenience: this row's whole job is to be an id the enum cannot name, and 6
        // stopped being one the day `JobKind.Fisher` existed. **The example mod was a fisherman;
        // the game grew one.** Renamed too, so two trades called "fisher" cannot be confused for
        // each other in a failure message.
        { "id": 7, "name": "hunter",     "plural": "hunters",     "doing": "hunting",            "works_at": "HunterLodge",   "limited_by": "Meat" },
        { "id": 8, "name": "boatman",    "plural": "boatmen",     "doing": "at the water",       "works_at": 10,              "limited_by": "Food" }
      ],

      "buildings": [
        { "id": 0, "name": "granary",          "stores": "Granary", "store_capacity": 2500, "work_ticks": 60, "materials": [ { "goods": "Logs", "amount": 40 }, { "goods": "Stone", "amount": 10 } ] },
        { "id": 1, "name": "warehouse",     "stores": "Warehouse",                            "work_ticks": 45, "materials": [ { "goods": "Logs", "amount": 30 }, { "goods": "Stone", "amount": 8 } ] },
        { "id": 2, "name": "market",           "stores": "Market",  "seats": 2,             "work_ticks": 50, "materials": [ { "goods": "Logs", "amount": 35 }, { "goods": "Stone", "amount": 10 } ] },
        { "id": 3, "name": "woodcutter's hut",                      "seats": 3,             "work_ticks": 40, "materials": [ { "goods": "Logs", "amount": 25 }, { "goods": "Stone", "amount": 3 } ] },
        { "id": 4, "name": "stockpile",        "stores": "Pile" },
        { "id": 5, "name": "house",            "house_capacity": 5,                         "work_ticks": 30, "materials": [ { "goods": "Logs", "amount": 30 } ] },
        { "id": 6, "name": "builder's hut", "seats": 3 },
        { "id": 7, "name": "gatherer's hut", "seats": 2,            "gathering_radius": 8,  "work_ticks": 40, "materials": [ { "goods": "Logs", "amount": 25 }, { "goods": "Stone", "amount": 3 } ] },
        { "id": 8, "name": "forester's hut",                                                "work_ticks": 40, "materials": [ { "goods": "Logs", "amount": 25 }, { "goods": "Stone", "amount": 3 } ] },
        { "id": 9, "name": "farmhouse",                             "seats": 2, "local_store_cap": 100, "work_ticks": 40, "materials": [ { "goods": "Logs", "amount": 25 }, { "goods": "Stone", "amount": 3 } ] },

        // The modder's own building. Nothing in the sim has ever heard of it.
        //
        // ⭐⭐ AND IT DELIBERATELY SITS AT THE LIBRARY'S OWN ID, WHICH IS NOT A COINCIDENCE. It is
        // what caught the literacy gate asking `kind == BuildingKind.Library` instead of asking the
        // ROW whether it has shelves — *a modder's boathouse refused for want of literacy it had
        // no use for.* **Do not move it off 10 to tidy up.**
        { "id": 10, "name": "boathouse", "stores": "Warehouse", "store_capacity": 200, "seats": 2, "local_store_cap": 40, "work_ticks": 25, "materials": [ { "goods": "Logs", "amount": 15 }, { "goods": "Stone", "amount": 2 } ] },

        // ⭐ The modder's own CIVIC building, at the id the town hall holds in the shipped
        // catalogue (D252). Two things at once: the validator refuses a catalogue missing a
        // built-in id, so a row has to be here at all — and `civic` and `singleton` are columns a
        // modder can reach in JSON, which is the claim this whole file exists to make.
        { "id": 11, "name": "moot hall", "civic": true, "singleton": true, "work_ticks": 20, "materials": [ { "goods": "Logs", "amount": 20 } ] },

        // ⭐ THE FISHING HUT, AND `must_touch` IS A COLUMN A MODDER CAN REACH — which is the
        // claim this file exists to make, applied to the newest kind of placement rule.
        { "id": 12, "name": "fishing hut", "seats": 4, "must_touch": "Water", "work_ticks": 40, "materials": [ { "goods": "Logs", "amount": 25 }, { "goods": "Stone", "amount": 3 } ]},
        { "id": 13, "name": "hunter's lodge", "seats": 3, "hunting_radius": 12, "work_ticks": 40, "materials": [ { "goods": "Logs", "amount": 40 }, { "goods": "Stone", "amount": 12 } ] }
      ]
    }
    """;

    private static SimConfig ConfigWithABoathouse() =>
        SimConfigLoader.Parse(JsonWithABoathouse, "<modded>");

    private static SimWorld World(SimConfig config, out InMemoryLogSink sink)
    {
        sink = new InMemoryLogSink();
        return SimFactory.CreatePhase0(config, sink).World;
    }

    private static string Said(InMemoryLogSink sink)
    {
        var said = new System.Text.StringBuilder();
        foreach (LogEntry entry in sink.Entries)
        {
            said.Append(entry.Message).Append(" | ");
        }

        return said.ToString();
    }

    /// <summary>A tile the village may build on, found rather than assumed.</summary>
    private static GridPos SomewhereBuildable(SimWorld world)
    {
        for (int y = 0; y < world.Map.Height; y++)
        {
            for (int x = 0; x < world.Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (world.Map.TerrainAt(at) == Terrain.Grass
                    && world.CanBuildAt(BuildingKind.Granary, at).Allowed)
                {
                    return at;
                }
            }
        }

        throw new System.InvalidOperationException("No buildable tile in the valley.");
    }

    // -----------------------------------------------------------------
    //  It loads, and the sim knows what it is
    // -----------------------------------------------------------------

    [Fact]
    public void AModderCanAddABuildingInDataAlone()
    {
        SimWorld world = World(ConfigWithABoathouse(), out _);
        BuildingsCatalog catalog = world.BuildingsCatalog;

        // 12 → 13 when the fishing hut shipped (2026-09-02); → 14 with the hunter's lodge
        // (2026-09-03). The modder's boathouse is the fifteenth.
        Assert.Equal(14, catalog.Count);

        // ⭐ Everything the sim used to answer with a switch, answered for a building no switch has
        // ever named.
        Assert.Equal("boathouse", catalog.NameOf(Boathouse));
        Assert.Equal(StoreKind.Warehouse, catalog.StoresAs(Boathouse));
        Assert.Equal(200, catalog[Boathouse].StoreCapacity);
        Assert.Equal(2, catalog[Boathouse].Seats);
        Assert.Equal(40, catalog[Boathouse].LocalStoreCap);

        BuildingRecipe recipe = catalog.RecipeOf(BoathouseId);
        Assert.Equal(15, recipe.Of(Goods.Logs));
        Assert.Equal(2, recipe.Of(Goods.Stone));
        Assert.Equal(25, recipe.WorkTicks);

        _output.WriteLine($"{catalog.NameOf(Boathouse)} — {recipe.Describe(world.GoodsCatalog)}, "
            + $"{recipe.WorkTicks} ticks");
    }

    /// <summary>
    /// ⭐⭐ A modded trade staffs a modded building — the seam `jobs-catalog.md §3` recorded.
    /// </summary>
    /// <remarks>
    /// That spec said <em>"until buildings land, a modded trade can only staff a building that
    /// already exists"</em>. <b>This is the sentence being retired</b>: <c>works_at: 10</c> names a
    /// building the enum has no word for, and the relation resolves in both directions.
    /// </remarks>
    [Fact]
    public void AModdedTradeCanStaffAModdedBuilding()
    {
        SimWorld world = World(ConfigWithABoathouse(), out _);

        Assert.Equal(Boathouse, world.JobsCatalog.WorksAt(Boatman));
        Assert.Equal(BoathouseId, world.JobsCatalog.WorksAtId(Boatman));
        Assert.Equal(Boatman, world.BuildingsCatalog.EmployedBy(Boathouse));
    }

    /// <summary>⭐ And it can actually be put up, and named, and worked at.</summary>
    /// <remarks>
    /// <b>The catalogue answering questions is not the same as the village raising one.</b> This
    /// walks the whole path — marked, priced, built, named, and a workplace standing where it was
    /// finished with the seats and the buffer its row asked for.
    /// </remarks>
    [Fact]
    public void TheVillageRaisesItAndWorksAtIt()
    {
        SimWorld world = World(ConfigWithABoathouse(), out InMemoryLogSink sink);
        GridPos site = SomewhereBuildable(world);

        PlacementVerdict verdict = world.Mark(Boathouse, site);
        Assert.True(verdict.Allowed, verdict.Reason);
        Assert.Contains("boathouse", Said(sink), System.StringComparison.Ordinal);

        // Finish it the way a builder would, then check what stands there.
        Workplace built = FinishTheSiteAt(world, site);
        _output.WriteLine($"finished: {built.Name}");

        StoreBuilding? store = null;
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            if (world.StoreBuildings[i].Position == site)
            {
                store = world.StoreBuildings[i];
            }
        }

        Assert.NotNull(store);
        Assert.Equal(StoreKind.Warehouse, store!.Kind);
        Assert.Equal(200, store.Store.Capacity);

        Workplace? stall = null;
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.Workplaces[i].Position == site && !world.Workplaces[i].IsSite)
            {
                stall = world.Workplaces[i];
            }
        }

        Assert.NotNull(stall);
        Assert.Equal(Boatman, stall!.Kind);
        Assert.Equal(2, stall.Capacity);
        Assert.Equal(40, stall.Store.Capacity);
    }

    /// <summary>The built-in ten are untouched by an addition above them.</summary>
    [Fact]
    public void TheBuiltInTenAreUntouchedByAdditions()
    {
        BuildingsCatalog catalog = World(ConfigWithABoathouse(), out _).BuildingsCatalog;

        Assert.Equal("granary", catalog.NameOf(BuildingKind.Granary));
        Assert.Equal("stockpile", catalog.NameOf(BuildingKind.Pile));
        Assert.Equal("farmhouse", catalog.NameOf(BuildingKind.Farmhouse));
        Assert.Equal(StoreKind.Granary, catalog.StoresAs(BuildingKind.Granary));
        Assert.Equal(JobKind.Forager, catalog.EmployedBy(BuildingKind.GathererHut));
    }

    /// <summary>
    /// ⭐⭐ The id is the contract; the order of the lines in the file is not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ THIS TEST EXISTS BECAUSE ITS SIBLINGS ARE GREEN AND BLIND WITHOUT IT, AND THAT IS
    /// MEASURED RATHER THAN FEARED.</b> Breaking <see cref="JobsCatalog"/> to place rows by
    /// <em>file position</em> instead of by their stated id left <b>eight of nine</b> guards in
    /// <see cref="ModdedJobTests"/> passing (D218), because the catalogue there happens to list its
    /// rows in id order — so the two are indistinguishable. <b>D157's finding, third instance.</b>
    /// </para>
    /// <para>
    /// The only cure is a file whose positions disagree with its ids. <em>What the bug looks like
    /// is why it matters: a modder sorts their file alphabetically, and every building in the
    /// village quietly means a different one. Nothing thrown, nothing to see.</em>
    /// </para>
    /// </remarks>
    [Fact]
    public void ReorderingTheFileDoesNotReinterpretTheBuildings()
    {
        string shuffled = """
        {
          "buildings": [
            { "id": 10, "name": "boathouse",       "stores": "Warehouse",    "store_capacity": 200, "work_ticks": 25, "materials": [ { "goods": "Logs", "amount": 15 } ] },
            { "id": 9,  "name": "farmhouse",       "seats": 2, "local_store_cap": 100,         "work_ticks": 40, "materials": [ { "goods": "Logs", "amount": 25 } ] },
            { "id": 8,  "name": "forester's hut",                                              "work_ticks": 40, "materials": [ { "goods": "Logs", "amount": 25 } ] },
            { "id": 7,  "name": "gatherer's hut",  "seats": 2, "gathering_radius": 8,           "work_ticks": 40, "materials": [ { "goods": "Logs", "amount": 25 } ] },
            { "id": 6,  "name": "builder's hut", "seats": 3 },
            { "id": 5,  "name": "house",           "house_capacity": 5,                        "work_ticks": 30, "materials": [ { "goods": "Logs", "amount": 30 } ] },
            { "id": 11, "name": "moot hall",       "civic": true, "singleton": true,           "work_ticks": 20, "materials": [ { "goods": "Logs", "amount": 20 } ] },
            { "id": 12, "name": "fishing hut",     "seats": 4, "must_touch": "Water",            "work_ticks": 40, "materials": [ { "goods": "Logs", "amount": 25 } ] },
            { "id": 13, "name": "hunter's lodge", "seats": 3, "hunting_radius": 12,             "work_ticks": 40, "materials": [ { "goods": "Logs", "amount": 40 } ] },
            { "id": 4,  "name": "stockpile",       "stores": "Pile" },
            { "id": 3,  "name": "woodcutter's hut", "seats": 3,                                "work_ticks": 40, "materials": [ { "goods": "Logs", "amount": 25 } ] },
            { "id": 2,  "name": "market",          "stores": "Market", "seats": 2,             "work_ticks": 50, "materials": [ { "goods": "Logs", "amount": 35 } ] },
            { "id": 1,  "name": "warehouse",    "stores": "Warehouse",                           "work_ticks": 45, "materials": [ { "goods": "Logs", "amount": 30 } ] },
            { "id": 0,  "name": "granary",         "stores": "Granary", "store_capacity": 2500, "work_ticks": 60, "materials": [ { "goods": "Logs", "amount": 40 } ] }
          ]
        }
        """;

        SimConfig config = SimConfigLoader.Parse(shuffled, "<shuffled>");
        var catalog = new BuildingsCatalog(
            config.BuildingRows, new JobsCatalog(config.JobsCatalog));

        // ⛔ If position won, `BuildingKind.Granary` would read "boathouse" here — a village whose
        // every building quietly means something else.
        Assert.Equal("granary", catalog.NameOf(BuildingKind.Granary));
        Assert.Equal("warehouse", catalog.NameOf(BuildingKind.Warehouse));
        Assert.Equal("farmhouse", catalog.NameOf(BuildingKind.Farmhouse));
        Assert.Equal("boathouse", catalog.NameOf(Boathouse));

        // The row that sits in the MIDDLE of a descending list is the sharpest one here (D252).
        // Id 11 is listed sixth of twelve, so a catalogue that indexed by position would read it
        // as the house -- which is exactly D218's finding, that a fixture listing rows in id
        // order cannot tell id from position.
        Assert.Equal("moot hall", catalog.NameOf(BuildingKind.TownHall));
        Assert.True(catalog[BuildingKind.TownHall]!.Civic);

        // And the columns follow their rows, not their lines.
        Assert.Equal(StoreKind.Granary, catalog.StoresAs(BuildingKind.Granary));
        Assert.Equal(2500, catalog[BuildingKind.Granary].StoreCapacity);
        Assert.Equal(8, catalog[BuildingKind.GathererHut].GatheringRadius);
    }

    // -----------------------------------------------------------------
    //  What is refused, and why
    // -----------------------------------------------------------------

    [Fact]
    public void TwoBuildingsSharingAnIdAreRefusedAtLoad()
    {
        string clashing = JsonWithABoathouse.Replace(
            "{ \"id\": 10, \"name\": \"boathouse\"",
            "{ \"id\": 9, \"name\": \"boathouse\"",
            System.StringComparison.Ordinal);

        SimConfigException blew = Assert.Throws<SimConfigException>(
            () => SimConfigLoader.Parse(clashing, "<clashing>"));

        _output.WriteLine(blew.Message);
        Assert.Contains("repeats id", blew.Message, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// ⛔ A catalogue that drops a built-in is refused, and it names which one.
    /// </summary>
    /// <remarks>
    /// <b>⚠️ The first draft of this guard renumbered the forester's hut to 88 rather than deleting
    /// it, and a different check caught it first</b> — <em>"stores nothing, employs nobody and
    /// houses nobody"</em>, because no trade's <c>works_at</c> pointed at 88. The refusal was
    /// correct and the guard was proving the wrong sentence. <em>A test that goes red for the wrong
    /// reason is a test that is not watching what it says it watches.</em>
    /// </remarks>
    [Fact]
    public void ACatalogueMissingABuiltInIsRefusedAtLoad()
    {
        // Struck out by line rather than by an exact substring — the first attempt matched on the
        // row's padding and silently changed nothing.
        var kept = new System.Text.StringBuilder();
        foreach (string line in JsonWithABoathouse.Split('\n'))
        {
            if (!line.Contains("\"id\": 8,", System.StringComparison.Ordinal))
            {
                kept.Append(line).Append('\n');
            }
        }

        string missing = kept.ToString();
        Assert.DoesNotContain("forester's hut", missing, System.StringComparison.Ordinal);

        SimConfigException blew = Assert.Throws<SimConfigException>(
            () => SimConfigLoader.Parse(missing, "<missing>"));

        _output.WriteLine(blew.Message);
        Assert.Contains("missing id 8", blew.Message, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// ⛔ A modded workplace must state its seats — the exemption does not stretch to reach it.
    /// </summary>
    /// <remarks>
    /// This is what makes `buildings-catalog.md §2.2`'s exemption honest rather than a hole. Six
    /// built-in buildings leave a capacity null because <see cref="VillageEconomy"/> solves for
    /// them; a modded one has nothing to appeal to, and left null it would throw <b>on the tick the
    /// building was finished</b> rather than at load.
    /// </remarks>
    [Fact]
    public void AModdedWorkplaceWithNoSeatsIsRefusedAtLoad()
    {
        string seatless = JsonWithABoathouse.Replace(
            "\"store_capacity\": 200, \"seats\": 2,",
            "\"store_capacity\": 200,",
            System.StringComparison.Ordinal);

        SimConfigException blew = Assert.Throws<SimConfigException>(
            () => SimConfigLoader.Parse(seatless, "<seatless>"));

        _output.WriteLine(blew.Message);
        Assert.Contains("seats", blew.Message, System.StringComparison.Ordinal);
    }

    /// <summary>Build the site standing at a tile, the way a builder's crew would.</summary>
    private static Workplace FinishTheSiteAt(SimWorld world, GridPos site)
    {
        Workplace? found = null;
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.Workplaces[i].Position == site && world.Workplaces[i].IsSite)
            {
                found = world.Workplaces[i];
            }
        }

        Assert.NotNull(found);

        ConstructionSite plan = found!.Construction!;
        foreach (MaterialCost owed in plan.Recipe.Materials)
        {
            plan.Deliver(owed.Goods, owed.Amount);
        }

        while (!plan.IsFinished)
        {
            plan.Work();
        }

        world.Complete(found);
        return found;
    }
}
