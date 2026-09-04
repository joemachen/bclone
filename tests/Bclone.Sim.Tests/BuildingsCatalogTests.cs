using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ Slice 1 of <c>buildings-catalog.md</c> — <b>a building is a row</b>.
/// </summary>
/// <remarks>
/// <para>
/// The claims this file guards are the ones the slice makes about the <em>built-in ten</em>:
/// the word the village uses, the cost, the store it becomes, the trade worked there, the ring,
/// the buffer and how many live in it all come off the row.
/// <see cref="ModdedBuildingTests"/> is the other half — that a modder can add an eleventh.
/// </para>
/// <para>
/// <b>⛔ THE NAME GUARDS EXIST BECAUSE A RED CHECK FOUND NOTHING.</b> Renaming the granary in the
/// catalogue — the word in the log, in the placement sentence and on the panel — turned <b>zero</b>
/// tests red across the whole suite. D108 fixed a default arm that <em>"called every unrecognised
/// building a woodcutter's hut, in the log, in the panel, and in every placement sentence"</em>, and
/// nothing has ever checked the words it fixed.
/// <em>A break that turns up nothing is a finding.</em>
/// </para>
/// <para>
/// <b>⚠️ THE POSED WORD BELOW IS DELIBERATE NONSENSE, AND THAT IS A CORRECTION.</b> The break was
/// originally run with <em>"barn"</em>, and the guard kept it — which reads as though the granary
/// had been renamed. <b>It had not, and a barn is real content:</b> `TECH-EXAMPLE.md`,
/// `livestock.md` and `tech-tree.md §9` all carry a <b>Timber Barn</b> as its own building, the hay
/// store D52 refuses to put in the shed. <b>A fixture word that will shortly name a different
/// building is a fixture that reads as a bug</b> — so it is a word this game can never have.
/// </para>
/// </remarks>
public sealed class BuildingsCatalogTests
{
    private readonly ITestOutputHelper _output;

    public BuildingsCatalogTests(ITestOutputHelper output) => _output = output;

    private static SimWorld World(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

    private static SimWorld World(SimConfig config, out InMemoryLogSink sink)
    {
        sink = new InMemoryLogSink();
        return SimFactory.CreatePhase0(config, sink).World;
    }

    /// <summary>Everything the village has said, as one string.</summary>
    private static string Said(InMemoryLogSink sink)
    {
        var said = new System.Text.StringBuilder();
        foreach (LogEntry entry in sink.Entries)
        {
            said.Append(entry.Message).Append(" | ");
        }

        return said.ToString();
    }

    // -----------------------------------------------------------------
    //  The words the player reads
    // -----------------------------------------------------------------

    /// <summary>⭐ Every building the village raises says the word its row states.</summary>
    /// <remarks>
    /// <b>Numbered, because there can be a second one</b> — <c>NameFor</c> counts each kind as it
    /// hands out a name, and <em>"granary 2"</em> is how the log and the panel tell two apart.
    /// ⚠️ <b>A house is the exception and it is deliberate:</b> a home is identified by the family
    /// in it, and <em>"house 47"</em> would be the row in a table D56 objected to.
    /// </remarks>
    [Fact]
    public void ABuildingIsCalledWhatItsRowCallsIt()
    {
        SimWorld world = World(VillageFixtures.Village);

        Assert.Equal("granary", world.BuildingsCatalog.NameOf(BuildingKind.Granary));
        Assert.Equal("storage shed", world.BuildingsCatalog.NameOf(BuildingKind.Shed));
        Assert.Equal("market", world.BuildingsCatalog.NameOf(BuildingKind.Market));
        Assert.Equal("woodcutter's hut", world.BuildingsCatalog.NameOf(BuildingKind.WoodcutterHut));
        Assert.Equal("builder's hut", world.BuildingsCatalog.NameOf(BuildingKind.BuilderHut));
        // ⭐ "forager's hut", NOT "gatherer's hut" (Joe, 2026-08-27, from play). D188 settled
        // *"forager and marketer win"* for the TRADE and the building kept the old word, so the
        // roster read "Hattie, 39 — forager" two lines above "Work: gatherer's hut 1". ⚠️ The
        // enum is still `GathererHut` and the config keys still say `gatherer_hut_*`: those are
        // identifiers, not words anybody reads.
        Assert.Equal("forager's hut", world.BuildingsCatalog.NameOf(BuildingKind.GathererHut));
        Assert.Equal("forester's hut", world.BuildingsCatalog.NameOf(BuildingKind.ForesterHut));
        Assert.Equal("farmhouse", world.BuildingsCatalog.NameOf(BuildingKind.Farmhouse));

        // ⚠️ "stockpile", not "storage pile" (Joe, D217) — and it shares a word with the
        // `Stockpile` class without being the same thing.
        Assert.Equal("stockpile", world.BuildingsCatalog.NameOf(BuildingKind.Pile));
    }

    /// <summary>⛔ A hut named after its trade uses the trade's own word for it.</summary>
    /// <remarks>
    /// <para>
    /// <b>The invariant that actually broke, and nothing was watching it</b> (Joe, 2026-08-27:
    /// <i>"forager hut workers still referred to as 'gatherers' in villager inspector
    /// window"</i>). D188 found two vocabularies for one job — <c>ProfessionName</c> saying
    /// <em>Gatherer</em> while <c>TradeOf</c> said <em>forager</em> — and settled it:
    /// <b>"forager and marketer win."</b> But it settled it for the <b>trade</b>. The
    /// <b>building</b> kept the old word for another four days short of a year, so the roster
    /// read <i>"Hattie, 39 — forager"</i> two lines above <i>"Work: gatherer's hut 1"</i>.
    /// </para>
    /// <para>
    /// ⭐ <b>Half a rename is how a settled decision comes undone.</b> The pair below proves the
    /// catalogue holds a word and that the code uses it; neither could notice that the word in
    /// one catalogue disagreed with the word in another.
    /// </para>
    /// <para>
    /// ⚠️ <b>Only the huts that are named after their trade</b>, which is the honest scope — a
    /// farmer works at a <em>farmhouse</em> and a forager also fills a <em>granary</em>, so this
    /// is not a rule about workplaces in general and pretending otherwise would make it a
    /// nuisance the next person deletes.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(BuildingKind.GathererHut, JobKind.Forager)]
    [InlineData(BuildingKind.ForesterHut, JobKind.Forester)]
    [InlineData(BuildingKind.WoodcutterHut, JobKind.Woodcutter)]
    [InlineData(BuildingKind.BuilderHut, JobKind.Builder)]
    public void AHutNamedAfterItsTradeUsesTheTradesWord(BuildingKind kind, JobKind trade)
    {
        SimWorld world = World(VillageFixtures.Village);

        string hut = world.BuildingsCatalog.NameOf(kind);
        string worker = world.JobsCatalog.NameOf(trade);

        _output.WriteLine($"{trade}: the trade is \"{worker}\" and the hut is \"{hut}\"");

        Assert.Contains(worker, hut, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ⭐⭐ And the word actually reaches the sentence the player reads.
    /// </summary>
    /// <remarks>
    /// The guard above proves the catalogue holds the word; this proves <c>NameFor</c> uses it.
    /// <b>Both are needed and the second is the one that matters</b> — D108's bug was a naming
    /// path that ignored the right answer, not a wrong answer stored somewhere.
    /// </remarks>
    [Fact]
    public void TheWordOnTheRowIsTheWordInTheVillageLog()
    {
        SimConfig renamed = VillageFixtures.Village;
        var rows = new List<BuildingRow>(renamed.BuildingRows);
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Id == (int)BuildingKind.Granary)
            {
                rows[i] = rows[i] with { Name = "grommet" };
            }
        }

        SimWorld world = World(renamed with { Buildings = rows }, out InMemoryLogSink sink);
        GridPos site = SomewhereBuildable(world);

        PlacementVerdict verdict = world.Mark(BuildingKind.Granary, site);
        Assert.True(verdict.Allowed, verdict.Reason);

        string said = Said(sink);
        _output.WriteLine(said);

        Assert.Contains("grommet", said, System.StringComparison.Ordinal);
        Assert.DoesNotContain("granary", said, System.StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------
    //  What a row says a building becomes
    // -----------------------------------------------------------------

    /// <summary>⭐ The store kind is a column, and only four buildings have one.</summary>
    [Fact]
    public void OnlyAStoreStores()
    {
        BuildingsCatalog catalog = World(VillageFixtures.Village).BuildingsCatalog;

        Assert.Equal(StoreKind.Granary, catalog.StoresAs(BuildingKind.Granary));
        Assert.Equal(StoreKind.Shed, catalog.StoresAs(BuildingKind.Shed));
        Assert.Equal(StoreKind.Market, catalog.StoresAs(BuildingKind.Market));
        Assert.Equal(StoreKind.Pile, catalog.StoresAs(BuildingKind.Pile));

        Assert.Null(catalog.StoresAs(BuildingKind.Home));
        Assert.Null(catalog.StoresAs(BuildingKind.BuilderHut));
        Assert.Null(catalog.StoresAs(BuildingKind.Farmhouse));
    }

    /// <summary>
    /// ⭐⭐ The building↔trade relation is stated once, on the job, and read backwards here.
    /// </summary>
    /// <remarks>
    /// <b>It used to be written down twice</b> — <c>JobRow.WorksAt</c> said forager → gatherer's
    /// hut and <c>SimWorld.Complete</c>'s switch said gatherer's hut → forager, with <b>nothing
    /// checking that they agreed</b>. That is D148's finding as a data model rather than as a word.
    /// </remarks>
    [Fact]
    public void WhoWorksHereIsTheJobRowReadBackwards()
    {
        SimWorld world = World(VillageFixtures.Village);
        BuildingsCatalog catalog = world.BuildingsCatalog;

        for (int id = 0; id < world.JobsCatalog.Count; id++)
        {
            var trade = (JobKind)id;
            if (world.JobsCatalog.WorksAt(trade) is not BuildingKind at)
            {
                continue;
            }

            Assert.Equal(trade, catalog.EmployedBy(at));
        }

        // ⭐ And a store that nobody works at answers null rather than guessing. The granary was
        // the case that mattered: `Complete`'s old default arm would have made an unknown building
        // a market, staff and all.
        Assert.Null(catalog.EmployedBy(BuildingKind.Granary));
        Assert.Null(catalog.EmployedBy(BuildingKind.Shed));
        Assert.Equal(JobKind.Marketer, catalog.EmployedBy(BuildingKind.Market));
    }

    /// <summary>⭐ The ring, the buffer and the roof are columns, and each is on exactly one row.</summary>
    /// <remarks>
    /// <b>Zero is the answer for every other building, and it has to be:</b> a workplace with an
    /// accidental ring gathers from ground nobody painted, and a store with an accidental buffer
    /// keeps goods the market cannot reach.
    /// </remarks>
    [Fact]
    public void TheRingTheBufferAndTheRoofBelongToOneBuildingEach()
    {
        SimConfig config = VillageFixtures.Village;
        BuildingsCatalog catalog = World(config).BuildingsCatalog;

        for (int id = 0; id < catalog.Count; id++)
        {
            BuildingRow row = catalog[id];
            var kind = (BuildingKind)id;

            Assert.Equal(kind == BuildingKind.GathererHut ? config.GathererHutRingTiles : 0,
                row.GatheringRadius);
            // ⛔ THE BUFFER IS NO LONGER THE FARMHOUSE'S ALONE (2026-09-03). A fishing hut holds
            // its catch for the same reason and by the same column — Joe: *"the fishing hut should
            // have 300 storage space which the marketer fetches."*
            //
            // ⭐ The claim is unchanged and is still worth making: **a building has a buffer on
            // purpose or not at all.** An accidental one keeps goods where the market cannot reach
            // them, which is the failure this guard exists for; it is a named list now rather than
            // a single name.
            int buffer = kind switch
            {
                BuildingKind.Farmhouse => config.FarmStoreCap,
                BuildingKind.FishingHut => config.FishingHutStoreCap,
                BuildingKind.HunterLodge => config.HunterLodgeStoreCap,
                _ => 0,
            };

            Assert.Equal(buffer, row.LocalStoreCap);

            // ⛔⛔ AND THE HUNTING REACH IS A SEPARATE COLUMN THAT MUST ALSO BELONG TO ONE
            // BUILDING. This is the guard that would catch the trap `specs/hunting.md §3`
            // names: if a lodge ever gains a `GatheringRadius`, the assertion above fails and
            // says so — because a ring enrols it in D260's competition and it would start
            // halving FORAGERS' yields over TREES, which is not what a hunter takes.
            Assert.Equal(
                kind == BuildingKind.HunterLodge ? config.HuntingRadius : 0,
                row.HuntingRadius);
            Assert.Equal(kind == BuildingKind.Home ? config.MaxHouseholdSize : 0,
                row.HouseCapacity);
        }
    }

    // -----------------------------------------------------------------
    //  The exemption, and what makes it honest
    // -----------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ A stated capacity is data; a derived one is the survival floor (§2.2).
    /// </summary>
    /// <remarks>
    /// <b>This is the guard on the exemption itself.</b> Six built-in buildings leave a capacity
    /// null because <see cref="VillageEconomy"/> solves for it — and D16 says a survival floor is
    /// derived, never typed. <b>Every other row must state one</b>, which is what stops the
    /// exemption becoming a hole a modder falls into.
    /// </remarks>
    [Fact]
    public void OnlyTheFiveTheEconomySolvesForMayLeaveACapacityUnstated()
    {
        SimConfig config = VillageFixtures.Village;
        BuildingsCatalog catalog = World(config).BuildingsCatalog;

        Assert.Equal(config.GranaryCapacity, catalog[BuildingKind.Granary].StoreCapacity);
        Assert.Null(catalog[BuildingKind.Shed].StoreCapacity);
        Assert.Null(catalog[BuildingKind.Pile].StoreCapacity);
        Assert.Null(catalog[BuildingKind.Market].StoreCapacity);

        Assert.Equal(config.WoodcutterHutCapacity, catalog[BuildingKind.WoodcutterHut].Seats);
        Assert.Equal(config.MarketCapacity, catalog[BuildingKind.Market].Seats);
        Assert.Equal(config.FarmhouseSeats, catalog[BuildingKind.Farmhouse].Seats);
        // ⛔⛔ THE DERIVED SET IS DOWN TO ONE BUILDING, AND IT LOST TWO IN TWO DAYS. The
        // forager's hut states its seats since D262 and **the builder's hut since 2026-08-30**
        // (Joe: *"one builder's hut should have 3 employees only"* — it was solving for every
        // hand a 20-household village could spare, which came to 21). ⭐ Only the forester's hut
        // still derives: what the woodcutters can eat, plus a hand for building.
        Assert.Equal(config.GathererHutCapacity, catalog[BuildingKind.GathererHut].Seats);
        Assert.Equal(config.BuilderHutSeats, catalog[BuildingKind.BuilderHut].Seats);
        Assert.Null(catalog[BuildingKind.ForesterHut].Seats);
    }

    /// <summary>
    /// ⛔ A store nobody derives a capacity for is refused at load, not at the tick it is finished.
    /// </summary>
    [Fact]
    public void AStoreWithNoCapacityAndNoDerivationIsRefusedAtLoad()
    {
        SimConfig config = VillageFixtures.Village;
        var rows = new List<BuildingRow>(config.BuildingRows);
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Id == (int)BuildingKind.Granary)
            {
                rows[i] = rows[i] with { StoreCapacity = null };
            }
        }

        SimConfigException blew = Assert.Throws<SimConfigException>(
            () => (config with { Buildings = rows }).Validate());

        _output.WriteLine(blew.Message);
        Assert.Contains("capacity", blew.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>⛔ A row that stores nothing, employs nobody and houses nobody is refused.</summary>
    /// <remarks>
    /// The village would raise it, spend the timber, and it would do nothing for ever — which is
    /// the *plausible default* shape of bug this project keeps finding, not a crash.
    /// </remarks>
    [Fact]
    public void ABuildingThatDoesNothingIsRefusedAtLoad()
    {
        SimConfig config = VillageFixtures.Village;
        var rows = new List<BuildingRow>(config.BuildingRows)
        {
            new BuildingRow
            {
                Id = NextFreeId(config),
                Name = "folly",
                WorkTicks = 10,
                Materials = new[] { new MaterialCost(Goods.Logs, 5) },
            },
        };

        SimConfigException blew = Assert.Throws<SimConfigException>(
            () => (config with { Buildings = rows }).Validate());

        _output.WriteLine(blew.Message);
        Assert.Contains("folly", blew.Message, System.StringComparison.Ordinal);
    }

    /// <summary>⛔ The cart is not a building, and a row may not claim to be one.</summary>
    [Fact]
    public void NoRowMayStoreAsTheFoundersCart()
    {
        SimConfig config = VillageFixtures.Village;
        var rows = new List<BuildingRow>(config.BuildingRows)
        {
            new BuildingRow { Id = NextFreeId(config), Name = "wagon shed", Stores = StoreKind.Cart, StoreCapacity = 50 },
        };

        SimConfigException blew = Assert.Throws<SimConfigException>(
            () => (config with { Buildings = rows }).Validate());

        _output.WriteLine(blew.Message);
        Assert.Contains("cart", blew.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------
    //  The ceiling
    // -----------------------------------------------------------------

    /// <summary>
    /// ⛔⛔ The naming counter is sized from the catalogue, not from the enum.
    /// </summary>
    /// <remarks>
    /// <b>It read <c>Enum.GetValues&lt;BuildingKind&gt;().Length</c>, so building eleven walked off
    /// the end of it</b> — an <c>IndexOutOfRangeException</c> the first time a modded building was
    /// named, <em>in the middle of a run rather than at load</em>. That is the third ceiling this
    /// family of slices has found by counting rather than by reasoning, after
    /// <c>Stockpile.Kinds</c> at six and <c>AllowedGoods</c> at thirty.
    /// </remarks>
    [Fact]
    public void AnEleventhBuildingCanBeNamedWithoutWalkingOffTheEndOfTheCounter()
    {
        SimConfig config = VillageFixtures.Village;
        var rows = new List<BuildingRow>(config.BuildingRows)
        {
            new BuildingRow
            {
                Id = NextFreeId(config),
                Name = "boathouse",
                Stores = StoreKind.Shed,
                StoreCapacity = 200,
                Materials = new[] { new MaterialCost(Goods.Logs, 10) },
                WorkTicks = 10,
            },
        };

        SimWorld world = World(config with { Buildings = rows }, out InMemoryLogSink sink);
        GridPos site = SomewhereBuildable(world);

        PlacementVerdict verdict = world.Mark((BuildingKind)NextFreeId(config), site);

        Assert.True(verdict.Allowed, verdict.Reason);
        Assert.Contains(
            "boathouse", Said(sink), System.StringComparison.Ordinal);
    }

    /// <summary>
    /// The first id no built-in building occupies — <b>read from the catalogue, not written in</b>.
    /// </summary>
    /// <remarks>
    /// <b>⚠️ THESE THREE GUARDS ALL SAID <c>Id = 11</c> AND ALL THREE BROKE THE DAY AN ELEVENTH
    /// BUILT-IN ARRIVED</b> (the town hall, D252) — not because they were testing the wrong thing,
    /// but because they had a built-in's id typed into them. **Each failed as *"buildings[12]
    /// repeats id 11"*, which is a true sentence about the fixture and says nothing about the
    /// code.** ⭐ Derived, the next built-in costs nobody an afternoon: *read the numbers out of
    /// the fixture rather than writing them into it.*
    /// </remarks>
    private static int NextFreeId(SimConfig config)
    {
        int highest = -1;
        for (int i = 0; i < config.BuildingRows.Count; i++)
        {
            if (config.BuildingRows[i].Id > highest)
            {
                highest = config.BuildingRows[i].Id;
            }
        }

        return highest + 1;
    }

    /// <summary>A tile the village may build on, found rather than assumed.</summary>
    private static GridPos SomewhereBuildable(SimWorld world)
    {
        for (int y = 0; y < world.Map.Height; y++)
        {
            for (int x = 0; x < world.Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (world.Map.TerrainAt(at) == Terrain.Grass && world.CanBuildAt(BuildingKind.Granary, at).Allowed)
                {
                    return at;
                }
            }
        }

        throw new System.InvalidOperationException("No buildable tile in the valley.");
    }
}
