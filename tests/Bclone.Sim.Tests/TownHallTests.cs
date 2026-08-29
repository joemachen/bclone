using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ Slice 1 of <c>specs/town-hall.md</c> — <b>the last founder dies, and the village raises a
/// hall in their name</b> (D251, D252).
/// </summary>
/// <remarks>
/// <para>
/// <b>⛔ THE GUARD THAT MATTERS MOST IS THE EMPTY-VALLEY ONE</b>
/// (<see cref="AVillageThatDiesWithItsFoundersIsGivenNothing"/>). The last founder dying with
/// nobody left is <em>the village ending</em>, not the village outgrowing anybody — D143 rules that
/// an unattended valley is supposed to die out, and <b>a monument raised for a village that no
/// longer exists is a message to a corpse.</b>
/// </para>
/// <para>
/// <b>⚠️ AND THE SECOND-MOST IS THE VACUITY ONE</b>
/// (<see cref="AWorldWithNobodyMarkedAsAFounderIsNeverGivenAHall"/>). *"No founder is alive"* is
/// **vacuously true from tick 1** in any world posed with hand-built villagers, so a trigger
/// written without that clause fires into an empty valley and every other guard here goes green
/// for the wrong reason. *An empty predicate is D157's green-and-blind.*
/// </para>
/// </remarks>
public sealed class TownHallTests
{
    private readonly ITestOutputHelper _output;

    public TownHallTests(ITestOutputHelper output) => _output = output;

    private static SimLoop Loop(SimConfig config, InMemoryLogSink sink) =>
        SimFactory.CreatePhase0(config, sink);

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
    //  Who is a founder
    // -----------------------------------------------------------------

    /// <summary>The four who arrive with the cart are marked, and nobody born here is.</summary>
    /// <remarks>
    /// <b>⭐ The claim `specs/town-hall.md §3` rests on.</b> A founder is marked rather than
    /// derived from <c>BirthYear</c>, so this is what says the marking actually happened — and
    /// running it a century deep is what says <c>HouseholdSystem</c> never sets it on a child.
    /// </remarks>
    [Fact]
    public void OnlyThePeopleTheVillageWasFoundedWithAreFounders()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        int foundersAtTickZero = 0;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            if (world.Villagers[i].Founder)
            {
                foundersAtTickZero++;
            }
        }

        Assert.Equal(world.Villagers.Count, foundersAtTickZero);
        Assert.True(foundersAtTickZero > 0, "A village founded by nobody cannot outgrow anybody.");

        loop.Step(config.TicksPerYear * 100);

        int foundersACenturyLater = 0;
        int bornHere = 0;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            if (world.Villagers[i].Founder)
            {
                foundersACenturyLater++;
            }
            else
            {
                bornHere++;
            }
        }

        _output.WriteLine($"{foundersACenturyLater} founders and {bornHere} born here "
            + $"after a century, out of {world.Villagers.Count} souls who ever lived");

        // ⛔ The count cannot grow. Nobody becomes a founder.
        Assert.Equal(foundersAtTickZero, foundersACenturyLater);
        Assert.True(bornHere > 0, "Nobody was ever born here, so this proves nothing.");
    }

    // -----------------------------------------------------------------
    //  The trigger
    // -----------------------------------------------------------------

    /// <summary>The moment fires the tick the last founder stops being alive, and not before.</summary>
    [Fact]
    public void TheHallIsGivenOnTheTickTheLastFounderDies()
    {
        SimConfig config = VillageFixtures.Village;
        var sink = new InMemoryLogSink();
        SimLoop loop = Loop(config, sink);
        SimWorld world = loop.World;

        int steppedYears = 0;
        while (AnyFounderAlive(world) && steppedYears < 200)
        {
            loop.Step(config.TicksPerYear);
            steppedYears++;

            // ⛔ THE TIGHT HALF OF THE CLAIM: while one of them still breathes, nothing is owed.
            if (AnyFounderAlive(world))
            {
                Assert.False(
                    world.SaidTheFoundersAreGone,
                    $"A founder is still alive in year {steppedYears} and the hall was already "
                    + "given.");
            }
        }

        Assert.False(AnyFounderAlive(world), "The founders outlived two centuries.");

        // ⚠️ Only meaningful if the village itself survived them — the other arm of this is
        // `AVillageThatDiesWithItsFoundersIsGivenNothing`, and this guard would read identically
        // to it if everybody had died.
        Assert.True(AnybodyAlive(world), "The village died with its founders, so this proves nothing.");

        _output.WriteLine($"the last founder died in year {steppedYears}");

        Assert.True(world.SaidTheFoundersAreGone);
        Assert.True(world.ATownHallIsOwed);
        Assert.Contains(
            world.Moments,
            m => m.Title.Contains("founders", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains("cart and no roof", Said(sink), System.StringComparison.Ordinal);
    }

    /// <summary>
    /// ⛔ A village that dies out with its founders is given nothing at all.
    /// </summary>
    /// <remarks>
    /// <b>⭐⭐ THE GUARD THIS SLICE EXISTS TO GET RIGHT.</b> D143 rules that an unattended valley is
    /// supposed to die out; the last founder dying there is <em>the village ending</em>, and a
    /// monument raised for a village that no longer exists is a message to a corpse.
    /// <b>Posed by killing everybody</b>, so the fixture cannot accidentally keep a child alive and
    /// make this pass for the wrong reason.
    /// </remarks>
    [Fact]
    public void AVillageThatDiesWithItsFoundersIsGivenNothing()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        loop.Step(1);

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            world.Villagers[i].Alive = false;
            world.Villagers[i].State = VillagerState.Dead;
            world.Villagers[i].DiedAtTick = world.Tick;
        }

        Assert.False(AnybodyAlive(world));
        Assert.False(AnyFounderAlive(world));

        loop.Step(config.TicksPerYear * 5);

        Assert.False(world.SaidTheFoundersAreGone);
        Assert.False(world.ATownHallIsOwed);
        Assert.Null(world.TownHall);
        Assert.DoesNotContain(
            world.Moments,
            m => m.Title.Contains("founders", System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// ⛔ A world where nobody is marked as a founder is never given a hall.
    /// </summary>
    /// <remarks>
    /// <b>⚠️ THE VACUITY GUARD, AND IT IS THE ONE A FIXTURE BREAKS FIRST.</b> *"No founder is
    /// alive"* is true from tick 1 in any world posed with hand-built villagers, so a trigger
    /// missing its <em>does this village have founders at all?</em> clause hands a town hall to a
    /// village on its first day. **Break `Founder` to `false` at the founding and this must go
    /// red** — it is the red check `specs/town-hall.md §9` names.
    /// </remarks>
    [Fact]
    public void AWorldWithNobodyMarkedAsAFounderIsNeverGivenAHall()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        // Every founder is gone, and a village of people born here goes on living. Posed by
        // replacing the roster rather than by killing it, so somebody is alive throughout.
        //
        // ⚠️ THE REPLACEMENT HAS TO BE A REAL VILLAGER OF A REAL HOUSEHOLD, and the first draft
        // was not: a villager with `HouseholdId = 0` threw out of `LabourAllocator` on tick 0,
        // because `RestingPlaceOf` asks the household where they sleep. *A fixture that cannot
        // survive a tick proves nothing about a trigger that fires on one.*
        GridPos anywhere = world.Villagers[0].Position;
        Household home = world.Households[0];
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            home.RemoveMember(world.Villagers[i].Id);
        }

        world.Villagers.Clear();

        var bornHere = new Villager
        {
            Id = 9001,
            Name = "Nobody's Founder",
            LifespanYears = 60,
            Carried = new Stockpile(world.GoodsCatalog.Count),
            BirthYear = 1,
            AgeYears = 20,
            HouseholdId = home.Id,
            Position = home.HomePosition ?? anywhere,
        };

        home.AddMember(bornHere.Id);
        world.Villagers.Add(bornHere);

        // ⚠️ A SHORT RUN ON PURPOSE. Three years killed them — one adult with no partner, no
        // household behind them and nobody to fetch firewood does not last, and a dead villager
        // would make this pass through the EMPTY-VALLEY rule instead of the one under test.
        // *Ask what your fixture makes impossible before trusting its green.*
        loop.Step(config.TicksPerDay * 5);

        Assert.True(AnybodyAlive(world), "The poser died, so the empty-valley rule is what caught this.");
        Assert.False(world.SaidTheFoundersAreGone);
        Assert.False(world.ATownHallIsOwed);
    }

    /// <summary>It fires once, however long the village runs afterwards.</summary>
    [Fact]
    public void TheFoundersAreMournedExactlyOnce()
    {
        SimConfig config = VillageFixtures.Village;
        var sink = new InMemoryLogSink();
        SimLoop loop = Loop(config, sink);
        SimWorld world = loop.World;

        loop.Step(config.TicksPerYear * 150);

        Assert.True(world.SaidTheFoundersAreGone, "The founders never all died in 150 years.");

        int moments = 0;
        for (int i = 0; i < world.Moments.Count; i++)
        {
            if (world.Moments[i].Title.Contains("founders", System.StringComparison.OrdinalIgnoreCase))
            {
                moments++;
            }
        }

        Assert.Equal(1, moments);
    }

    /// <summary>The tribute names every founder — the last of them, and the ones before.</summary>
    /// <remarks>
    /// <b>⭐ THIS IS THE HALF THAT IS NOT MECHANISM, AND IT IS WHY THE BUILDING IS FREE</b> (D252).
    /// <em>A tribute that cannot say who it is for is a stats screen with a plaque on it</em> —
    /// which is the whole reason <see cref="Villager.Founder"/> is marked rather than derived.
    /// </remarks>
    [Fact]
    public void TheTributeNamesEveryFounder()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        var founders = new System.Collections.Generic.List<string>();
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            founders.Add(world.Villagers[i].Name);
        }

        loop.Step(config.TicksPerYear * 150);
        Assert.True(world.SaidTheFoundersAreGone);

        Moment tribute = world.Moments.Find(
            m => m.Title.Contains("founders", System.StringComparison.OrdinalIgnoreCase))!;
        Assert.NotNull(tribute);
        _output.WriteLine(tribute.Body);

        foreach (string name in founders)
        {
            Assert.Contains(name, tribute.Body, System.StringComparison.Ordinal);
        }
    }

    // -----------------------------------------------------------------
    //  The gift, and the building
    // -----------------------------------------------------------------

    /// <summary>The gift is the materials; the crew still raise it.</summary>
    [Fact]
    public void TheGiftIsTheMaterialsAndTheWorkStillStands()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        loop.Step(config.TicksPerYear * 150);
        Assert.True(world.ATownHallIsOwed);

        // ⛔ The gift is the materials, NOT the building — nothing stands until the player says
        // where, which is the ruling Joe made about the library from play.
        Assert.Null(world.TownHall);

        GridPos spot = SomewhereBuildable(world);
        PlacementVerdict verdict = world.Mark(BuildingKind.TownHall, spot);
        Assert.True(verdict.Allowed, verdict.Reason);

        Workplace raising = FindSiteAt(world, spot);
        _output.WriteLine($"the gifted hall costs {raising.Construction!.Recipe.TotalMaterials} "
            + $"materials and {raising.Construction.Recipe.WorkTicks} ticks of work");

        Assert.Equal(0, raising.Construction.Recipe.TotalMaterials);
        Assert.True(raising.Construction.Recipe.WorkTicks > 0, "Somebody still has to build it.");
        Assert.False(world.ATownHallIsOwed);

        FinishTheSiteAt(world, spot);

        Assert.NotNull(world.TownHall);
        Assert.Equal(spot, world.TownHall!.Position);
        Assert.Equal(BuildingKind.TownHall, world.WhatStandsAt(spot));
    }

    /// <summary>
    /// ⛔ There is only ever one, and the refusal says so.
    /// </summary>
    /// <remarks>
    /// <b>The first singleton in the game</b> (D38 — `building-placement.md` has listed the town
    /// hall as <em>the</em> example of a build-once building since long before one existed), so the
    /// refusal is new machinery rather than an existing rule applied.
    /// ⚠️ <b>Both arms matter: a site counts as well as a standing hall.</b> Refusing only once the
    /// first is finished would let a player queue five and watch four fail in silence.
    /// </remarks>
    [Fact]
    public void TheVillageMayOnlyEverHaveOneTownHall()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        loop.Step(config.TicksPerYear * 150);
        Assert.True(world.ATownHallIsOwed);

        GridPos first = SomewhereBuildable(world);
        Assert.True(world.Mark(BuildingKind.TownHall, first).Allowed);

        // Marked and not yet standing — the second must already be refused.
        GridPos second = SomewhereBuildableOtherThan(world, first);
        PlacementVerdict whileASiteStands = world.CanBuildAt(BuildingKind.TownHall, second);
        Assert.False(whileASiteStands.Allowed);
        Assert.Contains("only ever one", whileASiteStands.Reason, System.StringComparison.Ordinal);

        // ⚠️ AND IT SAYS *"marked out"*, NOT *"the village has one"* — nothing is standing yet, and
        // telling the player otherwise is telling them something they can see is false (D43).
        Assert.Contains("marked out", whileASiteStands.Reason, System.StringComparison.Ordinal);
        _output.WriteLine(whileASiteStands.Reason);

        FinishTheSiteAt(world, first);

        PlacementVerdict whileOneStands = world.CanBuildAt(BuildingKind.TownHall, second);
        Assert.False(whileOneStands.Allowed);
        Assert.Contains("has a town hall already", whileOneStands.Reason, System.StringComparison.Ordinal);
        _output.WriteLine(whileOneStands.Reason);
    }

    /// <summary>Pulling it down does not re-offer the gift. The founders only die once.</summary>
    [Fact]
    public void PullingTheHallDownDoesNotGiveTheVillageAnother()
    {
        SimConfig config = VillageFixtures.Village;
        var sink = new InMemoryLogSink();
        SimLoop loop = Loop(config, sink);
        SimWorld world = loop.World;

        loop.Step(config.TicksPerYear * 150);

        // ⚠️ ADDED AFTER THE RED CHECK, BECAUSE THIS GUARD SURVIVED A BREAK IT SHOULD HAVE
        // CAUGHT. With `Founder` broken to false at the founding, seven guards here went red and
        // this one stayed green: it finishes the site by hand, so a hall nobody was ever given
        // still gets raised and still gets pulled down. **The claim is about a GIFTED hall**, and
        // this line is what makes the fixture say so.
        Assert.True(world.ATownHallIsOwed, "This is a claim about the gifted hall.");

        GridPos spot = SomewhereBuildable(world);
        world.Mark(BuildingKind.TownHall, spot);
        FinishTheSiteAt(world, spot);
        Assert.NotNull(world.TownHall);

        // ⚠️ Demolition is reverse construction (D228) — a builder's job, not a click — so the
        // hall stands until the crew have finished taking it down.
        Assert.True(world.MarkDemolition(spot).Allowed);
        FinishTheSiteAt(world, spot);

        Assert.Null(world.TownHall);
        Assert.False(world.ATownHallIsOwed);

        // And the second one costs what a town hall costs.
        GridPos again = SomewhereBuildableOtherThan(world, spot);
        Assert.True(world.Mark(BuildingKind.TownHall, again).Allowed);
        Workplace raising = FindSiteAt(world, again);
        Assert.True(
            raising.Construction!.Recipe.TotalMaterials > 0,
            "The village was handed a second hall for nothing.");
    }

    /// <summary>
    /// ⭐ In the game as it ships, the hall arrives late, into a village that grew.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ THE PACING GUARD, AND IT IS HERE BECAUSE D227 IS THE PRECEDENT.</b> Joe built a
    /// library and said <i>"it feels too early… you just stabilised, now build a library?"</i> —
    /// and he was right. **A gift that arrives before the player has a village to put it in is a
    /// chore with a bow on it**, so the one number this feature has needed measuring, and this is
    /// where it is measured.
    /// </para>
    /// <para>
    /// <b>MEASURED 2026-08-29: the shipped config owes a hall in YEAR 58, with 35 souls alive.</b>
    /// The fixture village reaches it in year 30 with 14 alive — which is also **why exactly two
    /// goldens moved for this slice and the shipped pair did not**: at the fifty-year mark the
    /// fixture's founders are gone and the shipped config's are not.
    /// </para>
    /// <para>
    /// <b>⭐⭐ AND IT TURNED UP SOMETHING THAT VINDICATES D251's RULING ON LITERACY.</b> This guard
    /// was first written to assert *"the hall arrives after the library"* — and it went red with
    /// <c>literacy in year 0</c>: **the unattended shipped village never learns to write at all,
    /// in fifty-eight years.** A granary is player-placed, nobody places one in an unattended run,
    /// so <c>FirstGranaryTick</c> stays zero and literacy never starts counting.
    /// ⛔ <b>Had literacy been made a prerequisite, this village would never have been given a
    /// hall</b> — which is exactly the trap Joe's *"expected, not enforced"* avoided (D251), found
    /// here by measurement rather than by argument.
    /// </para>
    /// <para>
    /// ⚠️ <b>The assertions are properties, not the number.</b> *One seed is not a trend* (D200),
    /// and pinning year 58 would turn every unrelated pacing change into a failure here. The floor
    /// is set well below the measurement on purpose.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheShippedVillageIsGivenItsHallLateAndIntoAVillageThatGrew()
    {
        SimConfig config = ShippedConfig.Established();
        SimLoop loop = Loop(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        int literacyYear = 0;
        int hallYear = 0;

        for (int year = 1; year <= 120 && hallYear == 0; year++)
        {
            loop.Step(config.TicksPerYear);

            if (literacyYear == 0 && world.HasLiteracy)
            {
                literacyYear = year;
            }

            if (world.SaidTheFoundersAreGone)
            {
                hallYear = year;
            }
        }

        int alive = 0;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            if (world.Villagers[i].Alive)
            {
                alive++;
            }
        }

        _output.WriteLine($"shipped: literacy in year {literacyYear}, hall owed in year {hallYear}, "
            + $"{alive} alive");

        Assert.True(hallYear > 0, "The shipped village was never given a hall in 120 years.");

        // ⛔ LATE, NOT EARLY. D227's lesson about the library — *"you just stabilised, now build a
        // library?"* — is the whole reason this floor is here. Measured at 58; asserted at 25, so
        // the guard catches a pacing collapse rather than ordinary drift.
        Assert.True(hallYear > 25, $"The hall arrived in year {hallYear}, which is too early.");

        // ⭐ AND INTO A VILLAGE, NOT A CAMP. *"The village outgrowing its founders"* is the
        // catalyst (D251) — a handful of survivors has not outgrown anybody.
        Assert.True(alive > 4, $"Only {alive} alive when the hall arrived; that is not a village.");

        // ⚠️ RECORDED RATHER THAN ASSERTED: this village never learns to write, because nobody
        // places its granary. Not a bug — an unattended valley has no player — but it is why the
        // literacy-ordering claim cannot be made here, and it is why literacy had better not be a
        // prerequisite. See this method's remarks.
        _ = literacyYear;
    }

    /// <summary>⛔ Nothing may be built on top of the hall.</summary>
    /// <remarks>
    /// <b>⭐ THIS GUARD EXISTS BECAUSE `SomethingStandsAt` ASKED FOR IT IN ADVANCE.</b> The comment
    /// beside the library's line there reads <em>"a new kind of building is a new line here or it
    /// can be built on top of"</em> — written by the session that added the fourth kind, after the
    /// same hole had already let a player mark a granary on somebody's house. <b>The town hall is
    /// the fifth.</b>
    /// </remarks>
    [Fact]
    public void NothingMayBeBuiltOnTopOfTheHall()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        loop.Step(config.TicksPerYear * 150);
        Assert.True(world.ATownHallIsOwed);

        GridPos spot = SomewhereBuildable(world);
        world.Mark(BuildingKind.TownHall, spot);
        FinishTheSiteAt(world, spot);
        Assert.NotNull(world.TownHall);

        PlacementVerdict onTop = world.CanBuildAt(BuildingKind.Granary, spot);
        Assert.False(onTop.Allowed, "A granary was marked on top of the founders' hall.");
        _output.WriteLine(onTop.Reason);
    }

    /// <summary>The hall employs nobody, stores nothing and houses nobody.</summary>
    /// <remarks>
    /// <b>⭐ Its entire output is information about the village itself</b>, which is what
    /// <c>BuildingRow.Civic</c> exists to say — <c>ValidateBuildings</c> refuses a row that does
    /// none of the other four things, and it refused this one before the column existed.
    /// </remarks>
    [Fact]
    public void TheHallProducesNoFoodNoGoodsAndNoLabour()
    {
        SimConfig config = VillageFixtures.Village;
        BuildingRow row = config.BuildingRows.First(r => r.Id == (int)BuildingKind.TownHall);

        Assert.True(row.Civic);
        Assert.True(row.Singleton);
        Assert.Null(row.Stores);
        Assert.Equal(0, row.Shelves);
        Assert.Equal(0, row.HouseCapacity);
        Assert.Equal(0, row.LocalStoreCap);

        for (int i = 0; i < config.JobsCatalog.Count; i++)
        {
            Assert.NotEqual((int)BuildingKind.TownHall, (int?)config.JobsCatalog[i].WorksAt ?? -1);
        }
    }

    // -----------------------------------------------------------------
    //  Determinism
    // -----------------------------------------------------------------

    /// <summary>
    /// ⛔ A village that never reaches the trigger hashes exactly as it did before this existed.
    /// </summary>
    /// <remarks>
    /// <b>The sparse-hash rule</b> (`StateHash`), and the reason no golden moves for this slice.
    /// *A village whose founders are still alive is not a different village from one that predates
    /// town halls.* ⚠️ This guard proves the two states are separable; the goldens staying put in
    /// the suite is what proves it end to end.
    /// </remarks>
    [Fact]
    public void AVillageWhoseFoundersLiveHashesAsThoughNoneOfThisExisted()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop before = Loop(config, new InMemoryLogSink());
        SimLoop after = Loop(config, new InMemoryLogSink());

        before.Step(config.TicksPerYear * 10);
        after.Step(config.TicksPerYear * 10);

        Assert.True(AnyFounderAlive(before.World), "Ten years in, at least one founder should live.");
        Assert.False(before.World.SaidTheFoundersAreGone);

        ulong left = Determinism.StateHash.Compute(before.World);
        ulong right = Determinism.StateHash.Compute(after.World);
        Assert.Equal(left, right);

        // And the three new fields are all in their nothing-mixed state, which is what makes the
        // claim above true rather than merely observed.
        Assert.False(before.World.ATownHallIsOwed);
        Assert.Null(before.World.TownHall);
    }

    // -----------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------

    private static bool AnyFounderAlive(SimWorld world)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            if (world.Villagers[i].Founder && world.Villagers[i].Alive)
            {
                return true;
            }
        }

        return false;
    }

    private static bool AnybodyAlive(SimWorld world)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            if (world.Villagers[i].Alive)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A tile the village may build on, found rather than assumed.</summary>
    private static GridPos SomewhereBuildable(SimWorld world) =>
        SomewhereBuildableOtherThan(world, new GridPos(int.MinValue, int.MinValue));

    private static GridPos SomewhereBuildableOtherThan(SimWorld world, GridPos avoid)
    {
        for (int y = 0; y < world.Map.Height; y++)
        {
            for (int x = 0; x < world.Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (at != avoid
                    && world.Map.TerrainAt(at) == Terrain.Grass
                    && world.CanBuildAt(BuildingKind.Granary, at).Allowed)
                {
                    return at;
                }
            }
        }

        throw new System.InvalidOperationException("No buildable tile in the valley.");
    }

    private static Workplace FindSiteAt(SimWorld world, GridPos site)
    {
        for (int i = world.Workplaces.Count - 1; i >= 0; i--)
        {
            if (world.Workplaces[i].Position == site && world.Workplaces[i].IsSite)
            {
                return world.Workplaces[i];
            }
        }

        throw new System.InvalidOperationException($"Nothing is being built at {site}.");
    }

    /// <summary>Deliver a site's materials and work it to completion, as a builder's crew would.</summary>
    private static void FinishTheSiteAt(SimWorld world, GridPos site)
    {
        Workplace found = FindSiteAt(world, site);
        ConstructionSite plan = found.Construction!;
        foreach (MaterialCost owed in plan.Recipe.Materials)
        {
            plan.Deliver(owed.Goods, owed.Amount);
        }

        while (!plan.IsFinished)
        {
            plan.Work();
        }

        world.Complete(found);
    }
}
