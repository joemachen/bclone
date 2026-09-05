using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ Slice 2 of <c>goods-catalog.md</c> — <b>proving a modder can add a good</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>D82's lesson, applied: the new good is what proves the refactor.</b> When goods became an
/// index, Joe's call was to do the refactor <em>when the first new good lands, not before and not
/// after — so the good is what proves the refactor.</em> Slices 1 and 1b made a good a row and
/// opened the set; **without this file none of that is proven**, only asserted.
/// </para>
/// <para>
/// <b>⛔ NO NEW GOOD SHIPS INTO THE GAME.</b> The seventh good exists only inside these tests. The
/// proof is the test, not content nobody asked for — adding <c>pitch</c> to the shipped catalogue
/// would be inventing content under cover of an infrastructure slice.
/// </para>
/// <para>
/// <b>⭐ IT GOES THROUGH REAL JSON, and that is the whole point.</b> Constructing
/// <see cref="GoodRow"/> objects in C# would prove that the sim can hold seven goods; it would not
/// prove that <b>a modder editing a data file</b> can add one, which is D168's actual promise:
/// *"modders should be able to add buildings, essentially add anything to the game."*
/// </para>
/// </remarks>
public sealed class ModdedGoodTests
{
    private readonly ITestOutputHelper _output;

    public ModdedGoodTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The id the modded good takes — <b>the first free one above the built-ins</b>.
    /// </summary>
    /// <remarks>
    /// ⛔⛔ <b>THIS WAS 6, AND 6 STOPPED BEING FREE THE DAY FISH SHIPPED.</b> The validator
    /// checks that every built-in ID is PRESENT, not what it is called — so a row named "pitch"
    /// sitting at 6 satisfied <c>Goods.Fish</c>'s slot, and this whole file went on claiming to
    /// prove *"a good the enum has never heard of"* while testing one the enum knows perfectly
    /// well. **It passed the entire time.** Found when <c>Meat</c> and <c>Leather</c> took 7 and
    /// 8 and the loader finally said something.
    /// </remarks>
    private const int PitchId = 9;

    private static Goods Pitch => (Goods)PitchId;

    /// <summary>
    /// A config with a seventh good, written the way a modder would write it.
    /// </summary>
    /// <remarks>
    /// The built-ins have to be restated because a <c>goods</c> array <b>replaces</b> the
    /// list rather than appending to it — the same wholesale-replacement rule
    /// <c>household_names</c> and <c>skills</c> follow, and the validator refuses a catalogue
    /// missing a built-in id precisely so this cannot be got half-right.
    /// </remarks>
    private static string JsonWithPitch => """
    {
      "goods": [
        { "id": 0, "name": "food",     "stored_by": ["Granary", "Market", "Cart"] },
        { "id": 1, "name": "logs",     "source_name": "woodland",     "yield_per_tile": 12, "stored_by": ["Warehouse"] },
        { "id": 2, "name": "firewood", "stored_by": ["Warehouse", "Market", "Cart"] },
        { "id": 3, "name": "stone",    "source_name": "a stone seam", "yield_per_tile": 12, "stored_by": ["Warehouse", "Cart"] },
        { "id": 4, "name": "tools",    "stored_by": ["Warehouse", "Cart"] },
        { "id": 5, "name": "iron",     "source_name": "an iron seam", "yield_per_tile": 8,  "stored_by": ["Warehouse", "Cart"] },
        { "id": 6, "name": "fish",     "source_name": "the river",    "nutrition": 1, "stored_by": ["Granary", "Market", "Cart"] },
        { "id": 7, "name": "meat",     "source_name": "the woods",    "nutrition": 1, "stored_by": ["Granary", "Market", "Cart"] },
        { "id": 8, "name": "leather",  "source_name": "the woods",    "stored_by": ["Warehouse", "Cart"] },

        // The modder's own good, above every built-in. Nothing in the sim has heard of it.
        { "id": 9, "name": "pitch",    "source_name": "a tar seep",   "yield_per_tile": 5,  "stored_by": ["Warehouse"] }
      ]
    }
    """;

    private static SimConfig ConfigWithPitch() => SimConfigLoader.Parse(JsonWithPitch, "<modded>");

    // -----------------------------------------------------------------
    //  It loads, and the sim knows what it is
    // -----------------------------------------------------------------

    [Fact]
    public void AModderCanAddAGoodInDataAlone()
    {
        SimConfig config = ConfigWithPitch();
        var catalog = new GoodsCatalog(config.GoodsCatalog);

        Assert.Equal(10, catalog.Count);

        // ⭐ Everything the sim used to answer with a switch, answered for a good no switch
        // has ever named.
        Assert.Equal("pitch", catalog.NameOf(Pitch));
        Assert.Equal("a tar seep", catalog.SourceNameOf(Pitch));
        Assert.Equal(5, catalog.YieldPerTileOf(Pitch));

        _output.WriteLine($"pitch: {catalog.NameOf(Pitch)} from {catalog.SourceNameOf(Pitch)}, "
            + $"{catalog.YieldPerTileOf(Pitch)} per tile");
    }

    [Fact]
    public void TheBuiltInSixAreUntouchedByAdditions()
    {
        var catalog = new GoodsCatalog(ConfigWithPitch().GoodsCatalog);

        // ⛔ Ids are appended, never renumbered — the rule every golden is pinned to. If adding
        // a good could shift `Goods.Food` off id 0, every saved limit and every seed would
        // silently mean something else.
        Assert.Equal("food", catalog.NameOf(Goods.Food));
        Assert.Equal("logs", catalog.NameOf(Goods.Logs));
        Assert.Equal("iron", catalog.NameOf(Goods.Iron));
        Assert.Equal(12, catalog.YieldPerTileOf(Goods.Logs));
    }

    // -----------------------------------------------------------------
    //  A village can actually hold it
    // -----------------------------------------------------------------

    [Fact]
    public void AVillageCanStoreAGoodTheEnumHasNeverHeardOf()
    {
        SimWorld world = WorldWithPitch();

        // Every stockpile in the run is sized from the catalogue, so the seventh has a slot —
        // this is the six-good ceiling, gone.
        Assert.Equal(10, world.Households[0].Stockpile.Slots);

        StoreBuilding warehouse = FindWarehouse(world);
        Assert.Equal(10, warehouse.Store.Slots);

        // ⭐ The warehouse takes it because the ROW says so — `stored_by: ["Warehouse"]` — not because
        // anything in the sim was taught about pitch.
        Assert.True(warehouse.Accepts(Pitch), "the warehouse's row says it stores pitch");

        warehouse.Store.Receive(Pitch, 40);
        Assert.Equal(40, warehouse.Store[Pitch]);

        _output.WriteLine($"warehouse holds {warehouse.Store[Pitch]} pitch of {warehouse.Store.Slots} slots");
    }

    [Fact]
    public void AGranaryStillRefusesIt()
    {
        SimWorld world = WorldWithPitch();
        StoreBuilding granary = FindStore(world, StoreKind.Granary);

        // ⛔ The anti-vacuity half: if a new good went everywhere, `stored_by` would be
        // decoration. The granary's row-set does not include pitch, so it refuses — and this
        // is the assertion that would fail if `KindAccepts` quietly fell back to "yes".
        Assert.False(granary.Accepts(Pitch), "a granary is food, and only food");
        Assert.True(granary.Accepts(Goods.Food));
    }

    // -----------------------------------------------------------------
    //  It is part of the world's identity
    // -----------------------------------------------------------------

    [Fact]
    public void TheSeventhGoodEntersTheStateHash()
    {
        SimWorld a = WorldWithPitch();
        SimWorld b = WorldWithPitch();

        Assert.Equal(StateHash.Compute(a), StateHash.Compute(b));

        // ⭐⭐ THE GUARD THAT MATTERS MOST IN THIS FILE. Before slice 1b the hash loops counted
        // to `Enum.GetValues<Goods>().Length`, so a village holding pitch would have hashed
        // *exactly as though it held none*. That is not a crash — it is two runs that read
        // identical and are not, which is the determinism failure this project treats as P0.
        FindWarehouse(a).Store.Receive(Pitch, 1);

        Assert.NotEqual(StateHash.Compute(a), StateHash.Compute(b));
    }

    /// <summary>
    /// ⭐⭐ A good <b>nobody holds</b> does not change the hash — so a catalogue may grow for free.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ THIS WAS FALSE UNTIL 2026-09-03, AND IT COST FIVE GOLDENS TO NOTICE.</b>
    /// <c>MixStore</c> mixed every slot including the empty ones, so <b>adding <c>Meat</c> and
    /// <c>Leather</c> changed the hash of every village that had never seen either</b> — a
    /// fourteen-year-old valley with no hunter in it suddenly read as a different world.
    /// </para>
    /// <para>
    /// ⚠️ <b>It broke the rule written on every other control in the hash</b>: zones, stock
    /// limits, profession targets, ground stacks and pending piles are all sparse precisely so
    /// *a village that never uses a feature hashes byte-identically*. Stores were the last dense
    /// loop, and goods are the one catalogue expected to keep growing — Joe has already asked for
    /// *"different types of game meat"*, and **every one of them would have churned the goldens.**
    /// </para>
    /// <para>
    /// ⭐ The pair of guards below is what keeps the fix honest: an EMPTY slot must vanish, and a
    /// single unit in it must still show.
    /// </para>
    /// </remarks>
    [Fact]
    public void AGoodNobodyHoldsDoesNotChangeTheHash()
    {
        // ⚠️ THE SAME CATALOGUE PLUS ONE ROW — not the hand-written modded one. Comparing
        // against `ConfigWithPitch()` compares two DIFFERENT economies: that JSON restates every
        // built-in and its `stored_by` lists do not match the shipped rows, so the villages
        // diverge for reasons that have nothing to do with the extra good. **The claim is about
        // one added row and nothing else**, so the fixture's own catalogue is the base.
        SimConfig plain = VillageFixtures.Village;

        var grown = new GoodRow[plain.GoodsCatalog.Count + 1];
        for (int i = 0; i < plain.GoodsCatalog.Count; i++)
        {
            grown[i] = plain.GoodsCatalog[i];
        }

        grown[^1] = new GoodRow
        {
            Id = plain.GoodsCatalog.Count,
            Name = "pitch",
            SourceName = "a tar seep",
            StoredBy = new[] { StoreKind.Warehouse },
        };

        SimConfig withPitch = plain with { GoodsCatalog = grown };

        SimLoop a = SimFactory.CreatePhase0(plain, new InMemoryLogSink());
        SimLoop b = SimFactory.CreatePhase0(withPitch, new InMemoryLogSink());

        _output.WriteLine(
            $"at tick 0: {StateHash.Compute(a.World):X16} against {StateHash.Compute(b.World):X16}");

        a.Step(plain.TicksPerYear * 4);
        b.Step(plain.TicksPerYear * 4);

        ulong without = StateHash.Compute(a.World);
        ulong with = StateHash.Compute(b.World);

        _output.WriteLine(
            $"four years in: without pitch in the catalogue {without:X16}, with it {with:X16} "
            + $"({a.World.Households[0].Stockpile.Slots} slots against "
            + $"{b.World.Households[0].Stockpile.Slots})");

        Assert.Equal(without, with);
    }

    [Fact]
    public void OneGrainOfPitchIsEnoughToShowUp()
    {
        // The smallest possible difference, because a hash that only notices large changes is
        // a hash with a blind spot rather than a hash.
        SimWorld before = WorldWithPitch();
        ulong empty = StateHash.Compute(before);

        FindWarehouse(before).Store.Receive(Pitch, 1);
        ulong withOne = StateHash.Compute(before);

        Assert.NotEqual(empty, withOne);
        _output.WriteLine($"{empty:X16} -> {withOne:X16} for a single unit");
    }

    // -----------------------------------------------------------------
    //  The player can have an opinion about it
    // -----------------------------------------------------------------

    [Fact]
    public void APlayerCanSetAStockLimitOnAModdedGood()
    {
        SimWorld world = WorldWithPitch();

        // ⭐ THE FOURTH CEILING, FOUND BY WRITING THIS TEST (D210). `StockLimits` sized itself
        // from `Enum.GetValues<Goods>()`, so a good above the built-in six had no slot: `IndexOf`
        // returned -1 and `Set` answered **false** — *the player sets a limit, the control reports
        // no change, and nothing anywhere says why.* Silent refusal is the worst of the three
        // possible failures, because there is nothing to read.
        Assert.Equal(10, world.StockLimits.Slots);

        Assert.True(world.StockLimits.Set(Pitch, 120), "a modded good can be limited");
        Assert.Equal(120, world.StockLimits.For(Pitch));

        // And it is a limit like any other: met at the number, not before.
        Assert.False(world.StockLimits.IsMet(Pitch, 119));
        Assert.True(world.StockLimits.IsMet(Pitch, 120));
    }

    [Fact]
    public void ALimitOnAModdedGoodIsPartOfTheWorldsIdentity()
    {
        SimWorld a = WorldWithPitch();
        SimWorld b = WorldWithPitch();
        Assert.Equal(StateHash.Compute(a), StateHash.Compute(b));

        // Null is "no opinion" and mixes nothing; a number is an instruction and must be hashed.
        // With the enum as the bound this limit sat in a slot the hash never reached.
        a.StockLimits.Set(Pitch, 120);

        Assert.NotEqual(StateHash.Compute(a), StateHash.Compute(b));
    }

    // -----------------------------------------------------------------
    //  The validator catches what a modder gets wrong
    // -----------------------------------------------------------------

    [Fact]
    public void ACatalogueThatOmitsABuiltInIsRefusedAtLoad()
    {
        // ⛔ The economy names food directly, and every golden is pinned to these ids, so a
        // catalogue may ADD rows and may never drop one. Failing here, at load, with a
        // sentence, is the whole difference between a config error and a null reference deep
        // in the sim.
        string missingFood = """
        {
          "goods": [
            { "id": 1, "name": "logs", "stored_by": ["Warehouse"] }
          ]
        }
        """;

        SimConfigException error = Assert.Throws<SimConfigException>(
            () => SimConfigLoader.Parse(missingFood, "<broken>"));

        Assert.Contains("missing id 0", error.Message);
        _output.WriteLine(error.Message);
    }

    [Fact]
    public void AGoodNoStoreWillTakeIsRefusedAtLoad()
    {
        string homeless = JsonWithPitch.Replace(
            """"source_name": "a tar seep",   "yield_per_tile": 5,  "stored_by": ["Warehouse"]"""",
            """"source_name": "a tar seep",   "yield_per_tile": 5,  "stored_by": []"""");

        SimConfigException error = Assert.Throws<SimConfigException>(
            () => SimConfigLoader.Parse(homeless, "<broken>"));

        // A good nothing can hold would be produced and never put down — and the symptom, a
        // hauler that never completes an errand, reads as a pathfinding bug.
        Assert.Contains("pitch", error.Message);
        _output.WriteLine(error.Message);
    }

    [Fact]
    public void TwoGoodsSharingAnIdAreRefusedAtLoad()
    {
        string duplicated = JsonWithPitch.Replace(
            """{ "id": 9, "name": "pitch",""",
            """{ "id": 5, "name": "pitch",""");

        SimConfigException error = Assert.Throws<SimConfigException>(
            () => SimConfigLoader.Parse(duplicated, "<broken>"));

        // Ids are what a stockpile indexes by, so two goods sharing one share a counter.
        Assert.Contains("repeats id 5", error.Message);
        _output.WriteLine(error.Message);
    }

    // -----------------------------------------------------------------

    private static SimWorld WorldWithPitch()
    {
        // The modded goods list on top of the fixture's economy, so the village is a real one
        // rather than a bare config — it has households, stores and a founding site.
        SimConfig config = VillageFixtures.Village with
        {
            GoodsCatalog = ConfigWithPitch().GoodsCatalog,
        };

        return SimFactory.CreatePhase0(config, new InMemoryLogSink(), 42UL).World;
    }

    private static StoreBuilding FindWarehouse(SimWorld world) => FindStore(world, StoreKind.Warehouse);

    private static StoreBuilding FindStore(SimWorld world, StoreKind kind)
    {
        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            if (world.StoreBuildings[i].Kind == kind)
            {
                return world.StoreBuildings[i];
            }
        }

        throw new System.InvalidOperationException($"No {kind} in this village.");
    }
}
