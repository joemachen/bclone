using System.Text.Json.Nodes;
using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Determinism;
using Kith.Sim.Logging;
using Kith.Sim.Persistence;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

/// <summary>
/// W4, the cemetery and the groundskeeper (`specs/cemetery.md`, D905). Step 1 (§9): <b>who lies unburied</b> — the dead since
/// <see cref="SimWorld.BurialsFromTick"/>, oldest first, kept where a death happens and rebuilt on load — and the two fields
/// it reads, saved and hashed sparsely.
/// </summary>
public sealed class CemeteryTests
{
    private readonly ITestOutputHelper _output;

    public CemeteryTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop AVillage() => SimFactory.CreatePhase0(Config, new InMemoryLogSink());

    /// <summary>Six founding households, not two — hands to spare for a groundskeeper, and enough people to die in a pose.</summary>
    private static SimLoop ABiggerVillage() => SimFactory.CreatePhase0(Config with { StartingHouseholds = 6 }, new InMemoryLogSink());

    /// <summary>What the waiting list must always equal: a scan of every villager, by the tick they died, then id.</summary>
    private static List<int> Scanned(SimWorld world) => world.Villagers
        .Where(v => !v.Alive && v.DiedAtTick is ulong at && at >= world.BurialsFromTick && v.BuriedAtTick is null)
        .OrderBy(v => v.DiedAtTick).ThenBy(v => v.Id).Select(v => v.Id).ToList();

    /// <summary>Old age on the next tick: the mortality system's own path, no shortcut past <c>Kill</c>.</summary>
    private static void DiesOnTheNextTick(Villager v) => v.AgeYears = v.LifespanYears;

    private static SimLoop RoundTrip(SimLoop loop, Action<JsonObject>? edit = null)
    {
        JsonObject document = SaveGame.Capture(loop, "test", "now", "test");
        JsonObject parsed = JsonNode.Parse(SaveFile.TextOf(document))!.AsObject();
        edit?.Invoke(parsed);
        return SaveGame.Load(parsed, loop.World.Config, null);
    }

    /// <summary>Removes a key from every object under <paramref name="node"/> — a save from before the key existed.</summary>
    private static int Strip(JsonNode? node, string key)
    {
        int removed = 0;
        if (node is JsonObject o)
        {
            if (o.Remove(key))
            {
                removed++;
            }

            foreach (KeyValuePair<string, JsonNode?> child in o.ToList())
            {
                removed += Strip(child.Value, key);
            }
        }
        else if (node is JsonArray a)
        {
            foreach (JsonNode? child in a)
            {
                removed += Strip(child, key);
            }
        }

        return removed;
    }

    [Fact]
    public void ADeathJoinsTheUnburiedOldestFirstAndThenById()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Assert.Equal(0UL, world.BurialsFromTick);
        Assert.Empty(world.Unburied);

        List<Villager> people = world.Villagers.Where(v => v.Alive).OrderBy(v => v.Id).ToList();
        Assert.True(people.Count >= 3, "the fixture founds at least three");
        DiesOnTheNextTick(people[2]);
        loop.StepOnce();
        DiesOnTheNextTick(people[1]);
        DiesOnTheNextTick(people[0]);
        loop.StepOnce();

        Assert.Equal(new[] { people[2].Id, people[0].Id, people[1].Id }, world.Unburied);
        Assert.Equal(Scanned(world), world.Unburied);
        Assert.All(new[] { people[0], people[1], people[2] }, v => Assert.True(world.AwaitsBurial(v)));
        Assert.All(world.Villagers.Where(v => v.Alive), v => Assert.False(world.AwaitsBurial(v)));
    }

    [Fact]
    public void SomebodyWhoLeftIsNotWaitingForAGrave()
    {
        SimWorld world = AVillage().World;
        Villager adult = world.Villagers.First(v => v.CanWork);
        adult.Alive = false;
        adult.State = VillagerState.Left;
        adult.LeftAtTick = 5;
        Assert.False(world.AwaitsBurial(adult));
        Assert.Empty(Scanned(world));
    }

    [Fact]
    public void LaidToRestLeavesTheListAndIsSavedAndHashed()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Villager first = world.Villagers.First(v => v.Alive);
        DiesOnTheNextTick(first);
        loop.StepOnce();
        Assert.Equal(new[] { first.Id }, world.Unburied);

        ulong unburied = StateHash.Compute(world);
        world.LayToRest(first, world.Tick);
        Assert.Empty(world.Unburied);
        Assert.False(world.AwaitsBurial(first));
        Assert.Equal(world.Tick, first.BuriedAtTick);
        ulong buried = StateHash.Compute(world);
        Assert.NotEqual(unburied, buried);
        first.BuriedAtTick = world.Tick + 1;
        Assert.NotEqual(buried, StateHash.Compute(world)); // the tick itself, not only that there is one
        first.BuriedAtTick = world.Tick;

        SimWorld back = RoundTrip(loop).World;
        Assert.Equal(world.Tick, back.Villagers.Single(v => v.Id == first.Id).BuriedAtTick);
        Assert.Empty(back.Unburied);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(back));
    }

    [Fact]
    public void AVillageWithNobodyBuriedHashesAsItDidBefore()
    {
        // The two fields are sparse: 0 and null mix nothing. Proved against the field set by hand and put back.
        SimWorld world = AVillage().World;
        ulong before = StateHash.Compute(world);
        world.BurialsFromTick = 40;
        Assert.NotEqual(before, StateHash.Compute(world));
        world.BurialsFromTick = 0;
        Assert.Equal(before, StateHash.Compute(world));
    }

    [Fact]
    public void ANewVillagesDeadStillWaitAfterASave()
    {
        // `burials_from` is written in every save, 0 included: a new village's dead must not read as "from before".
        SimLoop loop = AVillage();
        Villager first = loop.World.Villagers.First(v => v.Alive);
        DiesOnTheNextTick(first);
        loop.Step(3);

        JsonObject document = SaveGame.Capture(loop, "test", "now", "test");
        Assert.Equal(1, Strip(JsonNode.Parse(SaveFile.TextOf(document)), "burials_from"));

        SimWorld back = RoundTrip(loop).World;
        Assert.Equal(0UL, back.BurialsFromTick);
        Assert.Equal(new[] { first.Id }, back.Unburied);
        Assert.Equal(StateHash.Compute(loop.World), StateHash.Compute(back));
    }

    [Fact]
    public void AnOlderSavesDeadAreGoneAndItsNextDeathWaits()
    {
        // Joe, D905 (call 6 b): "the dead from before do not get buried too. they are gone."
        SimLoop loop = AVillage();
        List<Villager> people = loop.World.Villagers.Where(v => v.Alive).OrderBy(v => v.Id).ToList();
        DiesOnTheNextTick(people[0]);
        loop.Step(3);
        ulong savedAt = loop.World.Tick;

        SimLoop back = RoundTrip(loop, doc => Assert.Equal(1, Strip(doc, "burials_from")));
        SimWorld world = back.World;
        Assert.Equal(savedAt, world.BurialsFromTick);
        Assert.Empty(world.Unburied);
        Assert.False(world.AwaitsBurial(world.Villagers.Single(v => v.Id == people[0].Id)));

        DiesOnTheNextTick(world.Villagers.Single(v => v.Id == people[1].Id));
        back.StepOnce();
        Assert.Equal(new[] { people[1].Id }, world.Unburied);
        Assert.Equal(Scanned(world), world.Unburied);

        // ...and from then on it is a save like any other: the mark is written, and read back as it was.
        SimWorld again = RoundTrip(back).World;
        Assert.Equal(savedAt, again.BurialsFromTick);
        Assert.Equal(new[] { people[1].Id }, again.Unburied);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(again));
    }

    [Fact]
    public void TheListEqualsAScanOverDecadesOfDeathsAndASaveMidway()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        int years = 40;
        int checks = 0;
        for (int year = 1; year <= years; year++)
        {
            loop.Step(world.Config.TicksPerYear);
            Assert.Equal(Scanned(world), world.Unburied);
            checks++;
            if (year == years / 2)
            {
                SimLoop back = RoundTrip(loop);
                Assert.Equal(world.Unburied, back.World.Unburied);
                Assert.Equal(StateHash.Compute(world), StateHash.Compute(back.World));
            }
        }

        // Anti-vacuity: the fixture's forty years must have killed somebody, or the scan proves nothing.
        Assert.Equal(years, checks);
        Assert.True(world.Unburied.Count >= 3, $"only {world.Unburied.Count} died in {years} years");
    }

    // ---------------------------------------------------------------
    //  Steps 2–3 (§9, D908): three sizes placed like any building, learned at the first death, the small one free
    // ---------------------------------------------------------------

    private static readonly BuildingKind[] Sizes = { BuildingKind.CemeterySmall, BuildingKind.CemeteryMedium, BuildingKind.CemeteryLarge };

    private const string NobodyHasDied = "Nobody has died here yet — the village has no need of a cemetery.";

    private static void SomebodyDies(SimLoop loop)
    {
        DiesOnTheNextTick(loop.World.Villagers.First(v => v.Alive));
        loop.StepOnce();
    }

    /// <summary>Marks a cemetery of this size on the nearest spot to the founding that takes it.</summary>
    private static Workplace MarkACemetery(SimWorld world, BuildingKind kind)
    {
        GridPos centre = world.Map.FoundingSite;
        for (int r = 0; r <= 40; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                    {
                        continue;
                    }

                    int before = world.Workplaces.Count;
                    if (world.Mark(kind, new GridPos(centre.X + dx, centre.Y + dy)).Allowed)
                    {
                        Assert.Equal(before + 1, world.Workplaces.Count);
                        return world.Workplaces[^1];
                    }
                }
            }
        }

        throw new InvalidOperationException($"no {kind} fits within 40 tiles of the founding");
    }

    [Theory]
    [InlineData(BuildingKind.CemeterySmall, 4, 5, 20)]
    [InlineData(BuildingKind.CemeteryMedium, 6, 7, 42)]
    [InlineData(BuildingKind.CemeteryLarge, 8, 10, 80)]
    public void EachSizeIsARowWithItsGravesAndOneGroundskeeper(BuildingKind kind, int width, int height, int graves)
    {
        SimWorld world = AVillage().World;
        BuildingRow row = world.BuildingsCatalog[kind]!;
        Assert.Equal((width, height, graves), (row.ExtentWidth, row.ExtentHeight, row.Graves));
        Assert.Equal(1, row.Seats);
        Assert.Equal(JobKind.Groundskeeper, world.BuildingsCatalog.EmployedBy(kind));
        Assert.Equal((8, 12, 20), (row.WearsInYears, row.FailsInYears, row.RuinedInYears)); // a timber building's clock (call 9)
    }

    [Fact]
    public void TheThreePricesAreTheDataFiles()
    {
        // Joe, D906: "all as recommended" — small 10 logs and 5 stone, medium 20 and 10, large 30 and 20.
        SimConfig c = Config;
        Assert.Equal((10, 5, 20, 10, 30, 20), (c.CemeterySmallLogs, c.CemeterySmallStone, c.CemeteryMediumLogs, c.CemeteryMediumStone,
            c.CemeteryLargeLogs, c.CemeteryLargeStone));
        BuildingRecipe large = BuildingRecipe.For(BuildingKind.CemeteryLarge, c);
        Assert.Equal(new[] { new MaterialCost(Goods.Logs, 30), new MaterialCost(Goods.Stone, 20) }, large.Materials.OrderBy(m => m.Goods));
        Assert.Equal(c.CemeteryLargeWorkTicks, large.WorkTicks);
    }

    [Fact]
    public void ACemeteryWhoseGravesDoNotFillItsGroundIsRefused()
    {
        SimConfig bad = Config with
        {
            Buildings = Config.BuildingRows.Select(r => r.Id == (int)BuildingKind.CemeteryMedium ? r with { Graves = 41 } : r).ToArray(),
        };
        SimConfigException blew = Assert.Throws<SimConfigException>(bad.Validate);
        Assert.Contains("graves", blew.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EverySizeIsLockedUntilSomebodyDiesAndALeaverDoesNotCount()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Assert.All(Sizes, k => Assert.Equal(NobodyHasDied, world.WhyNotYet(k)));

        Villager leaver = world.Villagers.First(v => v.CanWork);
        leaver.Alive = false;
        leaver.State = VillagerState.Left;
        leaver.LeftAtTick = world.Tick;
        loop.StepOnce();
        Assert.All(Sizes, k => Assert.False(world.IsUnlocked(k)));
        Assert.Equal(0, world.DeathsEver);

        SomebodyDies(loop);
        Assert.Equal(1, world.DeathsEver);
        Assert.All(Sizes, k => Assert.True(world.IsUnlocked(k)));
        Assert.Equal(TechState.Known, TechTree.StateOf(world, world.Config.TechNodeRows.Single(n => n.Id == "cemetery")).State);
    }

    [Fact]
    public void TheFirstDeathRaisesOneMomentAndOwesTheSmall()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        world.Moments.Clear();
        Villager first = world.Villagers.First(v => v.Alive);
        SomebodyDies(loop);

        Moment said = Assert.Single(world.Moments);
        Assert.True(said.WaitsToBeDismissed);
        Assert.Contains(first.FullName, said.Body, StringComparison.Ordinal);
        Assert.Contains("the first this village has lost", said.Body, StringComparison.Ordinal);
        Assert.True(world.AFreeCemeteryIsOwed);

        SomebodyDies(loop);
        Assert.Single(world.Moments); // one moment ever: the transition cannot repeat
        Assert.Equal(2, world.DeathsEver);
    }

    [Fact]
    public void TheSmallIsFreeOnceAndEveryOtherIsPriced()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        SomebodyDies(loop);

        Workplace medium = MarkACemetery(world, BuildingKind.CemeteryMedium);
        Assert.True(medium.Construction!.Recipe.TotalMaterials > 0, "the medium was free");
        Assert.True(world.AFreeCemeteryIsOwed, "a medium spent the small's gift");

        Workplace small = MarkACemetery(world, BuildingKind.CemeterySmall);
        Assert.Equal(0, small.Construction!.Recipe.TotalMaterials);
        Assert.True(small.Construction.Recipe.WorkTicks > 0, "the gift raised itself — the work is owed");
        Assert.False(world.AFreeCemeteryIsOwed);

        Workplace again = MarkACemetery(world, BuildingKind.CemeterySmall);
        Assert.Equal(world.Config.CemeterySmallLogs + world.Config.CemeterySmallStone, again.Construction!.Recipe.TotalMaterials);
        Assert.Equal(BuildingKind.CemeterySmall, again.Construction.Kind); // a site is the builders' until it stands
    }

    [Fact]
    public void TheGiftIsSavedAndInTheFingerprint()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        ulong before = StateHash.Compute(world);
        world.AFreeCemeteryIsOwed = true;
        Assert.NotEqual(before, StateHash.Compute(world));
        Assert.True(RoundTrip(loop).World.AFreeCemeteryIsOwed);
        world.AFreeCemeteryIsOwed = false;
        Assert.False(RoundTrip(loop).World.AFreeCemeteryIsOwed);
    }

    [Fact]
    public void AnOlderSaveWithDeadIsLearnedAndOwedOnLoadWithNoMoment()
    {
        // Joe, D906 (call 10): "unlock on load as recommended".
        SimLoop loop = AVillage();
        SomebodyDies(loop);
        loop.World.AFreeCemeteryIsOwed = false; // as an older save would hold it: the key did not exist
        loop.World.Moments.Clear();

        SimWorld back = RoundTrip(loop, doc =>
        {
            Assert.Equal(1, Strip(doc, "a_free_cemetery_is_owed"));
            Assert.Equal(1, Strip(doc, "burials_from"));
        }).World;
        Assert.True(back.AFreeCemeteryIsOwed);
        Assert.Equal(1, back.DeathsEver);
        Assert.All(Sizes, k => Assert.True(back.IsUnlocked(k)));
        Assert.Empty(back.Moments);
        Assert.Empty(back.Unburied); // its dead are gone (call 6)
    }

    [Fact]
    public void AnOlderSaveWithNobodyDeadWaitsForItsFirstDeath()
    {
        SimLoop loop = AVillage();
        SimLoop back = RoundTrip(loop, doc => Assert.Equal(1, Strip(doc, "a_free_cemetery_is_owed")));
        Assert.False(back.World.AFreeCemeteryIsOwed);
        Assert.All(Sizes, k => Assert.False(back.World.IsUnlocked(k)));
        back.World.Moments.Clear();
        SomebodyDies(back);
        Assert.Single(back.World.Moments);
        Assert.True(back.World.AFreeCemeteryIsOwed);
    }

    // ---------------------------------------------------------------
    //  Step 4 (§9, D909): the groundskeeper, a burial, and tending
    // ---------------------------------------------------------------

    /// <summary>A cemetery of this size standing (raised by hand, the work done) — somebody having died first, so it may be marked.</summary>
    private static Workplace StandACemetery(SimLoop loop, BuildingKind kind = BuildingKind.CemeterySmall)
    {
        SimWorld world = loop.World;
        if (world.DeathsEver == 0)
        {
            SomebodyDies(loop);
        }

        Workplace plan = MarkACemetery(world, kind);
        BuildFixtures.StockTheSite(plan);
        for (int i = 0; i <= plan.Construction!.Recipe.WorkTicks; i++)
        {
            plan.Construction.Work();
        }

        GridPos stood = plan.Tile;
        world.Complete(plan);
        return world.Workplaces.Single(w => !w.IsSite && w.Tile == stood && w.Kind == JobKind.Groundskeeper);
    }

    /// <summary>Seat a groundskeeper by hand and pin them to the trade, so the labour pass keeps them there.</summary>
    private static Villager Keep(SimWorld world, Workplace cemetery, Villager? chosen = null)
    {
        world.VillageSharesOutWork = false;
        Villager hand = chosen ?? world.Villagers.First(v => v.Alive && v.CanTakeAJob && !cemetery.WorkerIds.Contains(v.Id));
        foreach (Workplace other in world.Workplaces)
        {
            other.WorkerIds.Remove(hand.Id);
        }

        cemetery.WorkerIds.Add(hand.Id);
        hand.WorkplaceId = cemetery.Id;

        // Pinned, as the player pins somebody to a trade: a founding has no hands to spare, and the labour pass would take the seat back.
        hand.PinnedTrade = JobKind.Groundskeeper;
        return hand;
    }

    /// <summary>Steps until <paramref name="done"/> or two years, and says how long it took.</summary>
    private static int StepUntil(SimLoop loop, Func<bool> done, int years = 2)
    {
        int limit = years * loop.World.Config.TicksPerYear;
        for (int t = 0; t < limit; t++)
        {
            if (done())
            {
                return t;
            }

            loop.StepOnce();
        }

        Assert.True(done(), $"not done in {years} years");
        return limit;
    }

    private static Villager Dies(SimLoop loop, Villager who)
    {
        DiesOnTheNextTick(who);
        loop.StepOnce();
        Assert.False(who.Alive);
        return who;
    }

    [Fact]
    public void AStandingCemeteryWantsOneGroundskeeperAndNoneWhileFoodComesFirst()
    {
        SimLoop loop = ABiggerVillage();
        SimWorld world = loop.World;
        Assert.Equal(0, LabourQuota.GroundskeepersWanted(world));
        StandACemetery(loop);
        Assert.Equal(1, LabourQuota.GroundskeepersWanted(world));
        StandACemetery(loop, BuildingKind.CemeteryMedium);
        Assert.Equal(2, LabourQuota.GroundskeepersWanted(world)); // one a cemetery, whatever its size (D395)

        // In a season food can be gathered, with nothing to eat: the comfort stands down (the tavern keeper's rule).
        while (!SeasonRules.IsGatherable(world.Clock.Season))
        {
            loop.Step(world.Config.TicksPerSeason);
        }

        // A full granary first: a founding is short of food by itself, and the premise is a village that is not.
        world.StoreBuildings.First(st => st.Kind == StoreKind.Granary).Store.Add(Goods.Produce, 2000);
        Assert.False(LabourQuota.VillageIsShortOfFood(world), "the premise: the village is fed");
        Assert.True(LabourQuota.For(world).For(JobKind.Groundskeeper) > 0, "the premise: with food, the quota seats a groundskeeper");
        foreach (Goods good in world.GoodsCatalog.EdibleGoods)
        {
            foreach (Household household in world.Households)
            {
                household.Stockpile.TakeAll(good);
            }

            foreach (StoreBuilding store in world.StoreBuildings)
            {
                store.Store.TakeAll(good);
            }
        }

        Assert.True(LabourQuota.VillageIsShortOfFood(world), "The premise: the village is hungry.");
        Assert.Equal(0, LabourQuota.For(world).For(JobKind.Groundskeeper));
    }

    [Fact]
    public void TheOldestDeadIsBuriedFirstAndTheLineIsSaid()
    {
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(Config, sink);
        SimWorld world = loop.World;
        List<Villager> people = world.Villagers.Where(v => v.Alive && v.CanWork).OrderBy(v => v.Id).ToList();
        Workplace cemetery = StandACemetery(loop);
        world.BurialsFromTick = world.Tick; // the unlock's death is from before this pose: the two below are the ones that wait
        world.RebuildTheUnburied();
        Villager first = Dies(loop, people[1]);
        Villager second = Dies(loop, people[2]);
        Keep(world, cemetery);

        StepUntil(loop, () => first.BuriedAtTick is not null);
        Assert.Null(second.BuriedAtTick); // the oldest first, one at a time
        StepUntil(loop, () => second.BuriedAtTick is not null);
        Assert.True(first.BuriedAtTick < second.BuriedAtTick);
        Assert.Equal(cemetery.Id, first.GraveAt);
        Assert.Equal(cemetery.Id, second.GraveAt);
        Assert.Empty(world.Unburied);
        Assert.Equal(18, world.FreeGraves(cemetery));
        Assert.Contains(sink.Entries, e => e.Message.StartsWith($"{first.FullName} was buried", StringComparison.Ordinal));
    }

    /// <summary>
    /// ⭐ W5 (`skills-for-every-trade.md §2`): a master groundskeeper buries in half the time — the burial begins through
    /// <c>BeginWork</c> now, as every other action does, so the grave-keeping row reaches it. A novice's is today's 12.
    /// </summary>
    [Theory]
    [InlineData(false, 12)]
    [InlineData(true, 6)]
    public void AMasterGroundskeeperBuriesInHalfTheTime(bool master, int ticks)
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        List<Villager> people = world.Villagers.Where(v => v.Alive && v.CanWork).OrderBy(v => v.Id).ToList();
        Workplace cemetery = StandACemetery(loop);
        world.BurialsFromTick = world.Tick;
        world.RebuildTheUnburied();
        Dies(loop, people[1]);
        Villager keeper = Keep(world, cemetery);
        SkillRow skill = world.SkillGrownBy(JobKind.Groundskeeper)!;
        if (master)
        {
            keeper.ProgressIn(skill.Id).Work = Config.MasteryWorkFor(skill);
        }

        int longest = 0;
        for (int i = 0; i < 4000 && world.Unburied.Count > 0; i++)
        {
            loop.Step(1);
            if (keeper.State == VillagerState.Burying)
            {
                longest = Math.Max(longest, keeper.ActionTicksRemaining);
            }
        }

        Assert.Empty(world.Unburied);
        Assert.Equal(ticks, longest);
    }

    /// <summary>A small cemetery kept, the unlock's dead buried in place 0; then a stranger, so the lowest free place is never beside.</summary>
    private static (SimLoop Loop, Workplace Cemetery, Villager Keeper, List<Villager> Adults) AKeptCemeteryWithAStranger()
    {
        SimLoop loop = ABiggerVillage();
        SimWorld world = loop.World;
        Workplace cemetery = StandACemetery(loop);
        Villager keeper = Keep(world, cemetery);
        StepUntil(loop, () => world.Unburied.Count == 0);
        Assert.Equal(0, world.Villagers.Where(v => v.GraveAt == cemetery.Id).MinBy(v => v.BuriedAtTick)!.GravePlace); // the first, the lowest place

        List<Villager> adults = world.Villagers.Where(v => v.Alive && v.CanWork && v.Id != keeper.Id).OrderBy(v => v.Id).ToList();
        return (loop, cemetery, keeper, adults);
    }

    /// <summary>Neither this one nor their partner has a partner any more — a pose that keeps the two ends agreeing.</summary>
    private static void Unpair(SimWorld world, Villager v)
    {
        if (v.PartnerId != 0 && world.FindVillager(v.PartnerId) is Villager partner)
        {
            partner.PartnerId = 0;
        }

        v.PartnerId = 0;
    }

    private static bool Beside(int p, int q, int width) => (p / width == q / width && Math.Abs(p - q) == 1) || Math.Abs(p - q) == width;

    [Fact]
    public void APartnerLiesBesideThePartnerAndNotInTheLowestPlace()
    {
        (SimLoop loop, Workplace cemetery, _, List<Villager> adults) = AKeptCemeteryWithAStranger();
        SimWorld world = loop.World;

        // Three of three households, so the household rule cannot be what lays the two together; married to each other in the pose.
        List<Villager> apart = adults.GroupBy(v => v.HouseholdId).Select(g => g.First()).Take(3).ToList();
        Assert.Equal(3, apart.Count);
        Villager a = apart[0], b = apart[1], stranger = apart[2];
        Unpair(world, a);
        Unpair(world, b);
        (a.PartnerId, b.PartnerId) = (b.Id, a.Id);
        Dies(loop, a);
        StepUntil(loop, () => a.BuriedAtTick is not null);
        Dies(loop, stranger);
        StepUntil(loop, () => stranger.BuriedAtTick is not null);
        Dies(loop, b);
        StepUntil(loop, () => b.BuriedAtTick is not null);

        int width = world.BuildingsCatalog[BuildingKind.CemeterySmall]!.ExtentWidth;
        int lowest = Enumerable.Range(0, 20).First(q => world.Villagers.All(v => v.GraveAt != cemetery.Id || v.GravePlace != q || v.Id == b.Id));
        Assert.True(Beside(a.GravePlace, b.GravePlace, width), $"partners at {a.GravePlace} and {b.GravePlace} are not side by side");
        Assert.NotEqual(lowest, b.GravePlace); // the pose bites: the lowest free place was not beside
    }

    [Fact]
    public void OneOfTheHouseholdLiesBesideTheirOwn()
    {
        (SimLoop loop, Workplace cemetery, _, List<Villager> adults) = AKeptCemeteryWithAStranger();
        SimWorld world = loop.World;

        // Two of one household who are not each other's partners, and a stranger from another.
        IGrouping<int, Villager> home = adults.GroupBy(v => v.HouseholdId).First(g => g.Count() >= 2);
        Villager a = home.ElementAt(0), b = home.ElementAt(1);
        Villager stranger = adults.First(v => v.HouseholdId != home.Key);
        Unpair(world, a);
        Unpair(world, b);
        Dies(loop, a);
        StepUntil(loop, () => a.BuriedAtTick is not null);
        Dies(loop, stranger);
        StepUntil(loop, () => stranger.BuriedAtTick is not null);
        Dies(loop, b);
        StepUntil(loop, () => b.BuriedAtTick is not null);

        int width = world.BuildingsCatalog[BuildingKind.CemeterySmall]!.ExtentWidth;
        Assert.True(Beside(a.GravePlace, b.GravePlace, width), $"one household at {a.GravePlace} and {b.GravePlace}, apart");
    }

    [Fact]
    public void AFullCemeteryTakesNobodyAndTheDeadWait()
    {
        // A one-grave cemetery, so it is full after one burial.
        SimConfig tiny = Config with
        {
            Buildings = Config.BuildingRows.Select(r => r.Id == (int)BuildingKind.CemeterySmall
                ? r with { ExtentWidth = 1, ExtentHeight = 1, Graves = 1 } : r).ToArray(),
        };
        SimLoop loop = SimFactory.CreatePhase0(tiny, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace cemetery = StandACemetery(loop);
        Villager keeper = Keep(world, cemetery);
        StepUntil(loop, () => world.Unburied.Count == 0);
        Assert.Equal(0, world.FreeGraves(cemetery));

        Villager next = Dies(loop, world.Villagers.First(v => v.Alive && v.Id != keeper.Id));
        loop.Step(world.Config.TicksPerYear / 4);
        Assert.Null(next.BuriedAtTick);
        Assert.Equal(new[] { next.Id }, world.Unburied);
        Assert.Equal(0, keeper.CarryingTheDead); // nobody fetched for a grave that is not there
    }

    [Fact]
    public void TheCarrierFallsAndTheDeadLieWhereTheyWereSetDown()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace cemetery = StandACemetery(loop);
        Villager keeper = Keep(world, cemetery);
        int dead = world.Unburied.Single();
        StepUntil(loop, () => keeper.CarryingTheDead == dead);
        loop.StepOnce();
        Dies(loop, keeper);
        Villager body = world.FindVillager(dead)!;
        Assert.Equal(0, keeper.CarryingTheDead);
        Assert.Equal(keeper.Position, body.Position);
        Assert.Contains(dead, world.Unburied);
        Assert.False(world.IsBeingCarried(body));
    }

    /// <summary>A standing cemetery with its keeper seated and nobody waiting — the unlock's dead is from before.</summary>
    private static (SimLoop Loop, Workplace Cemetery, Villager Keeper) AKeptCemeteryNobodyWaitsFor()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace cemetery = StandACemetery(loop);
        world.BurialsFromTick = world.Tick + 1;
        world.RebuildTheUnburied();
        Assert.Empty(world.Unburied);
        return (loop, cemetery, Keep(world, cemetery));
    }

    /// <summary>Trees painted for clearing around the founding — a spare hand's work, so a keeper helping out is seen at it.</summary>
    private static void PaintTreesToClear(SimWorld world, int radius = 7)
    {
        GridPos site = world.Map.FoundingSite;
        int painted = 0;
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (world.PaintHarvest(new GridPos(site.X + dx, site.Y + dy)).Allowed)
                {
                    painted++;
                }
            }
        }

        Assert.True(painted > 0, "nothing near the founding to paint");
    }

    /// <summary>Steps a season and says whether the keeper was seen clearing painted ground, as a laborer does.</summary>
    private static bool SeenClearing(SimLoop loop, Villager keeper, int seasons = 1)
    {
        bool cleared = false;
        for (int t = 0; t < seasons * loop.World.Config.TicksPerSeason; t++)
        {
            loop.StepOnce();
            cleared |= keeper.State == VillagerState.Clearing;
        }

        return cleared;
    }

    /// <summary>
    /// ⭐ Joe's play note (D915, *"Laborer between"*): a fresh cemetery with nobody to bury does not want its keeper, so they help
    /// out as a laborer does — and the seat stays theirs, and the card says why they are not at the graves.
    /// </summary>
    [Fact]
    public void AFreshCemeteryWithNobodyToBuryLetsItsKeeperHelpOut()
    {
        (SimLoop loop, Workplace cemetery, Villager keeper) = AKeptCemeteryNobodyWaitsFor();
        SimWorld world = loop.World;
        Assert.False(world.TheCemeteryWantsKeeping(cemetery));
        PaintTreesToClear(world);
        bool cleared = false;
        for (int t = 0; t < 3 * world.Config.TicksPerSeason && !world.TheCemeteryWantsKeeping(cemetery); t++)
        {
            loop.StepOnce();
            Assert.NotEqual(VillagerState.KeepingTheCemetery, keeper.State);
            cleared |= keeper.State == VillagerState.Clearing; // a spare hand's work
        }

        Assert.True(cleared, "the keeper never took a spare hand's day");

        Assert.Equal(0, cemetery.TendedDaysThisSeason);
        Assert.Equal(cemetery.Id, keeper.WorkplaceId); // the seat stays theirs
        Assert.Contains(keeper.Id, cemetery.WorkerIds);
        Assert.Contains("helping out", keeper.WorkNote, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⭐ And a year's wear calls them back (D915): well before it looks untidy, for the days a season needs and no more; at the
    /// season's turn it is sound again, and they are free until the next year's wear.
    /// </summary>
    [Fact]
    public void AYearsWearCallsTheKeeperBackForTheDaysItNeedsAndNoMore()
    {
        (SimLoop loop, Workplace cemetery, Villager keeper) = AKeptCemeteryNobodyWaitsFor();
        SimWorld world = loop.World;
        int season = world.Config.TicksPerSeason;
        int waited = StepUntil(loop, () => world.TheCemeteryWantsKeeping(cemetery));
        Assert.InRange(waited, (world.Config.CemeteryTendAfterSeasons - 1) * season, world.Config.CemeteryTendAfterSeasons * season);

        StepUntil(loop, () => keeper.State == VillagerState.KeepingTheCemetery && keeper.ActionTicksRemaining > 0, years: 1);
        Assert.True(cemetery.Footprint.Covers(keeper.Tile) || keeper.Tile == cemetery.Tile, "tending somewhere that is not the cemetery");
        Assert.Equal(Condition.Sound, world.ConditionOf(cemetery)); // back well before it is untidy
        Assert.Equal(string.Empty, keeper.WorkNote);

        int most = 0;
        StepUntil(loop, () =>
        {
            most = Math.Max(most, cemetery.TendedDaysThisSeason);
            return cemetery.RepairedAtTick > cemetery.RaisedAtTick;
        }, years: 1);
        Assert.True(most <= world.Config.CemeteryTendedDaysASeason, $"kept {most} days in a season that wanted {world.Config.CemeteryTendedDaysASeason}");
        Assert.False(world.TheCemeteryWantsKeeping(cemetery));
    }

    /// <summary>⭐ A keeper helping out comes back when somebody dies (D915), as the builders come back to a repair.</summary>
    [Fact]
    public void AKeeperHelpingOutComesBackForABurial()
    {
        (SimLoop loop, Workplace cemetery, Villager keeper) = AKeptCemeteryNobodyWaitsFor();
        SimWorld world = loop.World;
        loop.Step(world.Config.TicksPerSeason);
        Assert.NotEqual(VillagerState.KeepingTheCemetery, keeper.State);

        Villager dead = Dies(loop, world.Villagers.First(v => v.Alive && v.Id != keeper.Id && v.CanWork));
        StepUntil(loop, () => dead.BuriedAtTick is not null, years: 1);
        Assert.Equal(cemetery.Id, dead.GraveAt);

        // And the grave filled, back to helping out — not left standing where they dug.
        PaintTreesToClear(world);
        Assert.True(SeenClearing(loop, keeper), $"after the burial the keeper stayed {keeper.State}");
    }

    /// <summary>A keeper who walks over to keep it and finds it kept by the time they arrive goes back to helping out (D915).</summary>
    [Fact]
    public void AKeeperWhoArrivesToFindItKeptHelpsOutAgain()
    {
        (SimLoop loop, Workplace cemetery, Villager keeper) = AKeptCemeteryNobodyWaitsFor();
        SimWorld world = loop.World;
        StepUntil(loop, () => keeper.State == VillagerState.KeepingTheCemetery && keeper.ActionTicksRemaining == 0
            && keeper.Tile != cemetery.Tile);
        cemetery.RepairedAtTick = world.Tick; // kept, as far as the stamps say, while they walk
        PaintTreesToClear(world);
        Assert.True(SeenClearing(loop, keeper), $"they arrived and stayed {keeper.State}");
        Assert.Equal(0, cemetery.TendedDaysThisSeason);
    }

    [Fact]
    public void NobodyUnderAStoneIsMovedOrPulledDown()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace empty = StandACemetery(loop, BuildingKind.CemeteryMedium);
        Assert.True(world.MarkDemolition(empty.Tile).Allowed, "an empty cemetery could not be pulled down");

        Workplace cemetery = StandACemetery(loop);
        Keep(world, cemetery);
        StepUntil(loop, () => world.Unburied.Count == 0);
        PlacementVerdict down = world.MarkDemolition(cemetery.Tile);
        Assert.False(down.Allowed);
        Assert.Contains("lies", down.Reason, StringComparison.Ordinal);
        Assert.False(world.MarkRelocation(cemetery.Tile, new GridPos(cemetery.Tile.X + 12, cemetery.Tile.Y)).Allowed);
    }

    [Fact]
    public void ACemeteryIsWalkedRoundNotThrough()
    {
        SimLoop loop = AVillage();
        Workplace cemetery = StandACemetery(loop, BuildingKind.CemeteryLarge);
        Assert.All(cemetery.Footprint.CoveredTiles(), t => Assert.True(loop.World.StandsOn(t)));
    }

    [Fact]
    public void TheGravesAndTheCarriedAreSavedAndRebuilt()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace cemetery = StandACemetery(loop);
        Villager keeper = Keep(world, cemetery);
        StepUntil(loop, () => world.Unburied.Count == 0);
        Villager buried = world.Villagers.Single(v => v.GraveAt == cemetery.Id);

        Villager next = Dies(loop, world.Villagers.First(v => v.Alive && v.Id != keeper.Id));
        StepUntil(loop, () => keeper.CarryingTheDead == next.Id);

        SimLoop back = RoundTrip(loop);
        SimWorld w = back.World;
        Villager b = w.FindVillager(buried.Id)!;
        Assert.Equal((cemetery.Id, buried.GravePlace, buried.BuriedAtTick), (b.GraveAt, b.GravePlace, b.BuriedAtTick));
        Assert.Equal(next.Id, w.FindVillager(keeper.Id)!.CarryingTheDead);
        Assert.True(w.IsBeingCarried(w.FindVillager(next.Id)!));
        Assert.Equal(world.FreeGraves(cemetery), w.FreeGraves(w.FindWorkplace(cemetery.Id)!));
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(w));

        ulong before = StateHash.Compute(w);
        w.FindVillager(buried.Id)!.GravePlace++;
        Assert.NotEqual(before, StateHash.Compute(w));
        w.FindVillager(buried.Id)!.GravePlace--;
        w.FindVillager(keeper.Id)!.CarryingTheDead = buried.Id;
        Assert.NotEqual(before, StateHash.Compute(w)); // whom they carry, not only that they carry
    }

    [Fact]
    public void TwoVillagesThatBuryTheirDeadAreTheSameVillage()
    {
        ulong Run()
        {
            SimLoop loop = AVillage();
            Workplace cemetery = StandACemetery(loop);
            Keep(loop.World, cemetery);
            loop.Step(2 * loop.World.Config.TicksPerYear);
            Assert.True(loop.World.Villagers.Any(v => v.BuriedAtTick is not null), "nobody was buried, so the run proves nothing");
            return StateHash.Compute(loop.World);
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void OneOfTheDeadNobodyCanWalkToIsPassedOver()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace cemetery = StandACemetery(loop);
        Villager stranded = world.FindVillager(world.Unburied.Single())!;
        Villager next = Dies(loop, world.Villagers.First(v => v.Alive && v.CanWork));

        // The oldest laid in the river: a pose, because nobody falls in water — but a tile nobody can walk to is the case.
        GridPos water = Enumerable.Range(0, world.Map.Width * world.Map.Height)
            .Select(i => new GridPos(i % world.Map.Width + world.Map.MinX, i / world.Map.Width + world.Map.MinY))
            .First(t => world.Map.TerrainAt(t) == Terrain.Water);
        stranded.Position = Point.CentreOf(water);
        Assert.Equal(TravelCostField.Unreachable, world.TravelCost.TicksBetween(cemetery.Tile, water)); // the premise
        Assert.Equal(new[] { stranded.Id, next.Id }, world.Unburied);
        Assert.Same(next, world.NextToBury(cemetery));
    }

    [Fact]
    public void OneOfTheDeadInSomebodysArmsIsNotFetchedByAnother()
    {
        SimLoop loop = ABiggerVillage();
        SimWorld world = loop.World;
        Workplace one = StandACemetery(loop);
        Workplace two = StandACemetery(loop, BuildingKind.CemeteryMedium);
        Villager keeper = Keep(world, one);
        int dead = world.Unburied.Single();
        StepUntil(loop, () => keeper.CarryingTheDead == dead);
        Assert.Same(world.FindVillager(dead), world.Unburied.Select(world.FindVillager).Single()); // still waiting, in arms
        Assert.Null(world.NextToBury(two)); // the other cemetery's keeper is not sent for them
    }

    [Fact]
    public void ACarrierWhoNoLongerKeepsTheCemeterySetsThemDown()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace cemetery = StandACemetery(loop);
        Villager keeper = Keep(world, cemetery);
        int dead = world.Unburied.Single();
        StepUntil(loop, () => keeper.CarryingTheDead == dead);

        keeper.PinnedTrade = null;
        cemetery.WorkerIds.Remove(keeper.Id);
        keeper.WorkplaceId = 0;
        loop.StepOnce();
        Villager body = world.FindVillager(dead)!;
        Assert.Equal(0, keeper.CarryingTheDead);
        Assert.False(world.IsBeingCarried(body));
        Assert.Contains(dead, world.Unburied);
    }

    // ---------------------------------------------------------------
    //  Step 5 (§9, D910): the two causes, and the family at the grave
    // ---------------------------------------------------------------

    private static bool Holds(Villager v, ContentmentCause cause) => (v.ContentmentCauses & (1 << (int)cause)) != 0;

    /// <summary>To the end of the next season read — where every cause is looked at.</summary>
    private static void ToTheNextRead(SimLoop loop)
    {
        int season = loop.World.SeasonNumber;
        while (loop.World.SeasonNumber == season)
        {
            loop.StepOnce();
        }

        loop.StepOnce();
    }

    /// <summary>One of a household of two adults or more, the rest of that household's adults, and an adult of another household.</summary>
    private static (Villager Dead, List<Villager> Family, Villager Stranger) AFamily(SimWorld world, Villager? notThem = null)
    {
        IGrouping<int, Villager> home = world.Villagers.Where(v => v.Alive && v.CanWork && v != notThem)
            .GroupBy(v => v.HouseholdId).First(g => g.Count() >= 2);
        Villager dead = home.First();
        List<Villager> family = home.Skip(1).ToList();
        Villager stranger = world.Villagers.First(v => v.Alive && v.CanWork && v.HouseholdId != home.Key && v != notThem && v.Id != dead.PartnerId);
        return (dead, family, stranger);
    }

    [Fact]
    public void TheirOwnMindTheirDeadInTheOpenAndAStrangerDoesNot()
    {
        SimLoop loop = ABiggerVillage();
        SimWorld world = loop.World;
        (Villager dead, List<Villager> family, Villager stranger) = AFamily(world);
        Dies(loop, dead);
        ToTheNextRead(loop);

        family = family.Where(v => v.Alive && v.CanWork).ToList(); // a founding of six households starves some of its own
        Assert.NotEmpty(family);
        Assert.All(family, v => Assert.True(Holds(v, ContentmentCause.DeadInTheOpen), $"{v.Name} of the household does not mind"));
        if (world.FindVillager(dead.PartnerId) is { Alive: true } partner)
        {
            Assert.True(Holds(partner, ContentmentCause.DeadInTheOpen));
        }

        Assert.False(Holds(stranger, ContentmentCause.DeadInTheOpen));
        Assert.All(family, v => Assert.False(Holds(v, ContentmentCause.CemeteryFull))); // never both
        Assert.Contains("lie in the open", world.ContentmentNoteFor(family[0]) ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void AStandingCemeteryWithRoomIsAPlaceEvenWhileTheyWaitTheirTurn()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        (Villager dead, List<Villager> family, _) = AFamily(world);
        Dies(loop, dead);
        StandACemetery(loop); // room for twenty, and nobody keeps it yet: a founding hires no comfort by itself
        ToTheNextRead(loop);
        Assert.Contains(dead.Id, world.Unburied); // the premise: they still wait
        Assert.All(family, v => Assert.False(Holds(v, ContentmentCause.DeadInTheOpen)));
        Assert.All(family, v => Assert.False(Holds(v, ContentmentCause.CemeteryFull)));
    }

    [Fact]
    public void ACemeterySetToNoGroundskeepersCountsAsNoPlace()
    {
        // Joe, D905 (call 7 a).
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        (Villager dead, List<Villager> family, _) = AFamily(world);
        Dies(loop, dead);
        StandACemetery(loop);
        Assert.True(world.SetJobLimit(JobKind.Groundskeeper, 0).Allowed);
        ToTheNextRead(loop);
        family = family.Where(v => v.Alive && v.CanWork).ToList();
        Assert.NotEmpty(family);
        Assert.All(family, v => Assert.True(Holds(v, ContentmentCause.DeadInTheOpen)));
    }

    [Fact]
    public void EveryCemeteryFullIsAFullCemeteryNotNoPlace()
    {
        SimConfig tiny = Config with
        {
            StartingHouseholds = 6,
            Buildings = Config.BuildingRows.Select(r => r.Id == (int)BuildingKind.CemeterySmall
                ? r with { ExtentWidth = 1, ExtentHeight = 1, Graves = 1 } : r).ToArray(),
        };
        SimLoop loop = SimFactory.CreatePhase0(tiny, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace cemetery = StandACemetery(loop);
        Villager keeper = Keep(world, cemetery);
        StepUntil(loop, () => world.FreeGraves(cemetery) == 0);

        (Villager dead, List<Villager> family, Villager stranger) = AFamily(world, keeper);
        Dies(loop, dead);
        ToTheNextRead(loop);
        family = family.Where(v => v.Alive && v.CanWork).ToList();
        Assert.NotEmpty(family);
        Assert.All(family, v => Assert.True(Holds(v, ContentmentCause.CemeteryFull), $"{v.Name} does not mind a full cemetery"));
        Assert.All(family, v => Assert.False(Holds(v, ContentmentCause.DeadInTheOpen)));
        Assert.False(Holds(stranger, ContentmentCause.CemeteryFull));
    }

    [Fact]
    public void TheDeadFromBeforeAreMindedByNobody()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        (Villager dead, List<Villager> family, _) = AFamily(world);
        Dies(loop, dead);
        world.BurialsFromTick = world.Tick + 1; // as an older save loads: the dead in it are gone
        world.RebuildTheUnburied();
        ToTheNextRead(loop);
        Assert.Empty(world.Unburied); // the premise: nobody has died since
        Assert.All(family.Where(v => v.Alive && v.CanWork), v => Assert.False(Holds(v, ContentmentCause.DeadInTheOpen)));
        Assert.Contains(family, v => v.Alive && v.CanWork);
    }

    [Fact]
    public void TheFamilyComesToTheGraveOnceAndAStrangerDoesNot()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        (Villager dead, List<Villager> family, Villager stranger) = AFamily(world);

        // Houses (a pose: the founders arrive with a cart, not a house) — a visit begins at the door, as an evening does.
        foreach (Household household in world.Households)
        {
            household.HomePosition ??= world.Villagers.First(v => v.HouseholdId == household.Id).Position;
        }

        Dies(loop, dead); // the first death: the cemetery is learned by it
        Workplace cemetery = StandACemetery(loop);
        Keep(world, cemetery, world.Villagers.First(v => v.Alive && v.CanTakeAJob && v.HouseholdId != dead.HouseholdId && v != stranger));
        StepUntil(loop, () => dead.BuriedAtTick is not null);

        var visits = new Dictionary<int, int>();
        var wasThere = new HashSet<int>();
        int days = world.Config.GraveVisitDays + 10;
        for (int t = 0; t < days * world.Config.TicksPerDay; t++)
        {
            loop.StepOnce();
            foreach (Villager v in world.Villagers)
            {
                bool there = v.State == VillagerState.AtTheGrave;
                if (there && !wasThere.Contains(v.Id))
                {
                    visits[v.Id] = visits.GetValueOrDefault(v.Id) + 1;
                }

                if (there)
                {
                    wasThere.Add(v.Id);
                }
                else
                {
                    wasThere.Remove(v.Id);
                }
            }
        }

        foreach (Villager v in family.Where(f => f.Alive))
        {
            Assert.Equal(1, visits.GetValueOrDefault(v.Id));
            Assert.True(v.LastGraveVisitTick >= dead.BuriedAtTick);
        }

        Assert.Equal(0, visits.GetValueOrDefault(stranger.Id));
        Assert.Null(stranger.LastGraveVisitTick);
    }

    [Fact]
    public void AVisitIsSavedAndInTheFingerprint()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Villager v = world.Villagers.First(p => p.Alive);
        ulong before = StateHash.Compute(world);
        v.LastGraveVisitTick = 77;
        ulong at77 = StateHash.Compute(world);
        Assert.NotEqual(before, at77);
        v.LastGraveVisitTick = 78;
        Assert.NotEqual(at77, StateHash.Compute(world));
        Assert.Equal((ulong?)78, RoundTrip(loop).World.FindVillager(v.Id)!.LastGraveVisitTick);
    }

    [Fact]
    public void APartnerInAnotherHouseholdMindsToo()
    {
        // Founding couples share a household, so the household rule alone would hide a partner's own (the red-check found it, D910).
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        List<Villager> adults = world.Villagers.Where(v => v.Alive && v.CanWork).ToList();
        Villager dead = adults[0];
        Villager partner = adults.First(v => v.HouseholdId != dead.HouseholdId);
        Villager partnersHousemate = adults.First(v => v.HouseholdId == partner.HouseholdId && v != partner);
        Unpair(world, dead);
        Unpair(world, partner);
        (dead.PartnerId, partner.PartnerId) = (partner.Id, dead.Id);

        Dies(loop, dead);
        ToTheNextRead(loop);
        Assert.True(Holds(partner, ContentmentCause.DeadInTheOpen), "the partner, in another household, does not mind");
        Assert.False(Holds(partnersHousemate, ContentmentCause.DeadInTheOpen)); // the partner's household is not the dead's
    }

    // ---------------------------------------------------------------
    //  Step 6 (§9, D911): it wears, and its groundskeeper keeps it (call 9)
    // ---------------------------------------------------------------

    private static ulong Years(SimWorld world, int years) => (ulong)years * (ulong)world.Config.TicksPerYear;

    /// <summary>
    /// A timber building's clock (call 9): 8, 12 and 20 years <i>of mild weather</i> — and a winter wears half again
    /// (<c>winter_wear_extra_percent</c> 50), so untended it is worn in its 8th calendar year, overgrown in its 11th and wild in its 18th.
    /// </summary>
    [Fact]
    public void AnUntendedCemeteryWearsOnATimberBuildingsClock()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace cemetery = StandACemetery(loop);
        ulong raised = cemetery.RaisedAtTick;
        (int Year, Condition Is)[] clock =
        {
            (7, Condition.Sound), (8, Condition.Worn), (10, Condition.Worn), (11, Condition.Failing),
            (17, Condition.Failing), (18, Condition.Ruin),
        };
        foreach ((int year, Condition expected) in clock)
        {
            world.Tick = raised + Years(world, year);
            Assert.True(expected == world.ConditionOf(cemetery), $"year {year}: {world.ConditionOf(cemetery)}, not {expected}");
        }
    }

    [Fact]
    public void AWildCemeteryIsNeverClosedNorMendedByTheBuilders()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace cemetery = StandACemetery(loop);
        world.Tick = cemetery.RaisedAtTick + Years(world, 21);
        world.TendWhatStands();
        Assert.Equal(Condition.Ruin, world.ConditionOf(cemetery));
        Assert.False(cemetery.Closed, "a wild cemetery was closed");
        Assert.DoesNotContain(world.Workplaces, w => w.Construction is { Repairing: true } && w.Tile == cemetery.Tile); // no logs owed
        Assert.True(world.TheDeadHaveAPlace().APlace, "a wild cemetery stopped counting as a place");
    }

    [Fact]
    public void ASeasonsTendingMakesItSoundAgainAndALittleDoesNot()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace cemetery = StandACemetery(loop);
        world.Tick = cemetery.RaisedAtTick + Years(world, 9);
        Assert.Equal(Condition.Worn, world.ConditionOf(cemetery));

        cemetery.TendedDaysThisSeason = world.Config.CemeteryTendedDaysASeason - 1;
        world.TendWhatStands();
        Assert.Equal(Condition.Worn, world.ConditionOf(cemetery));
        Assert.Equal(0, cemetery.TendedDaysThisSeason); // the count is the season's, cleared at its turn

        cemetery.TendedDaysThisSeason = world.Config.CemeteryTendedDaysASeason;
        world.TendWhatStands();
        Assert.Equal(Condition.Sound, world.ConditionOf(cemetery));
        Assert.Equal(world.Tick, cemetery.RepairedAtTick);
    }

    [Fact]
    public void AKeptCemeteryStaysSoundForYears()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace cemetery = StandACemetery(loop);
        Keep(world, cemetery);
        loop.Step(9 * world.Config.TicksPerYear);
        Assert.Equal(Condition.Sound, world.ConditionOf(cemetery));
        Assert.True(cemetery.RepairedAtTick > cemetery.RaisedAtTick, "nobody's tending ever counted");
    }

    [Fact]
    public void TheTendingIsSavedAndInTheFingerprint()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace cemetery = StandACemetery(loop);
        ulong before = StateHash.Compute(world);
        cemetery.TendedDaysThisSeason = 3;
        ulong three = StateHash.Compute(world);
        Assert.NotEqual(before, three);
        cemetery.TendedDaysThisSeason = 4;
        Assert.NotEqual(three, StateHash.Compute(world));
        Assert.Equal(4, RoundTrip(loop).World.FindWorkplace(cemetery.Id)!.TendedDaysThisSeason);
    }

    // ---------------------------------------------------------------
    //  Step 7 (§9, D912): what the cemetery, its stones and a household say
    // ---------------------------------------------------------------

    [Fact]
    public void TheCemeterysSentenceSaysWhoKeepsItWhoWaitsAndWhenItIsFull()
    {
        SimConfig tiny = Config with
        {
            Buildings = Config.BuildingRows.Select(r => r.Id == (int)BuildingKind.CemeterySmall
                ? r with { ExtentWidth = 1, ExtentHeight = 1, Graves = 1 } : r).ToArray(),
        };
        SimLoop loop = SimFactory.CreatePhase0(tiny, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace cemetery = StandACemetery(loop);
        (bool kept, string said) = world.SayTheCemetery(cemetery);
        Assert.False(kept);
        Assert.Contains("Nobody keeps it", said, StringComparison.Ordinal);
        Assert.Contains("raise Groundskeepers", said, StringComparison.Ordinal);
        Assert.Contains("One lies waiting", said, StringComparison.Ordinal);

        world.SetJobLimit(JobKind.Groundskeeper, 0);
        Assert.Contains("set to none", world.SayTheCemetery(cemetery).Sentence, StringComparison.Ordinal);
        world.SetJobLimit(JobKind.Groundskeeper, null);

        Villager keeper = Keep(world, cemetery);
        Assert.Equal((true, $"Kept by {keeper.Name}. One lies waiting for a grave."), world.SayTheCemetery(cemetery));
        StepUntil(loop, () => world.FreeGraves(cemetery) == 0);
        Assert.StartsWith("Full", world.SayTheCemetery(cemetery).Sentence, StringComparison.Ordinal);
    }

    [Fact]
    public void AStoneSaysWhoTheyWereHowTheyDiedAndWhomTheyLieBeside()
    {
        (SimLoop loop, Workplace cemetery, _, List<Villager> adults) = AKeptCemeteryWithAStranger();
        SimWorld world = loop.World;
        List<Villager> apart = adults.GroupBy(v => v.HouseholdId).Select(g => g.First()).Take(2).ToList();
        Villager a = apart[0], b = apart[1];
        Unpair(world, a);
        Unpair(world, b);
        (a.PartnerId, b.PartnerId) = (b.Id, a.Id);
        Dies(loop, a);
        StepUntil(loop, () => a.BuriedAtTick is not null);
        Dies(loop, b);
        StepUntil(loop, () => b.BuriedAtTick is not null);

        string stone = world.TheStoneSays(b);
        Assert.StartsWith($"{b.FullName} · {b.AgeYears}. Died in ", stone, StringComparison.Ordinal);
        Assert.Contains(", of old age.", stone, StringComparison.Ordinal); // they died by the pose's old age
        Assert.EndsWith($" Beside {a.Name}.", stone, StringComparison.Ordinal);
        Assert.Contains(cemetery.Id, new[] { b.GraveAt });
    }

    [Fact]
    public void AHouseholdsCardSaysWhoOfTheirsWaitsAndWhenTheyAreCarried()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        (Villager dead, _, _) = AFamily(world);
        Household home = world.FindHousehold(dead.HouseholdId)!;
        Assert.Null(world.WhoOfTheirsWaits(home));
        Dies(loop, dead);
        Point fell = dead.Position;
        dead.Position = Point.CentreOf(world.HomePlaceOf(dead));
        Assert.Equal($"{dead.Name} lies at home, waiting for a grave.", world.WhoOfTheirsWaits(home));
        dead.Position = Point.CentreOf(new GridPos(world.HomePlaceOf(dead).X + 3, world.HomePlaceOf(dead).Y));
        Assert.Equal($"{dead.Name} lies where they fell, waiting for a grave.", world.WhoOfTheirsWaits(home));
        dead.Position = fell;

        Workplace cemetery = StandACemetery(loop);
        Villager keeper = Keep(world, cemetery, world.Villagers.First(v => v.Alive && v.CanTakeAJob && v.HouseholdId != dead.HouseholdId));
        StepUntil(loop, () => keeper.CarryingTheDead == dead.Id);
        Assert.Equal($"{dead.Name} is being carried to the cemetery.", world.WhoOfTheirsWaits(home));
        StepUntil(loop, () => dead.BuriedAtTick is not null);
        Assert.Null(world.WhoOfTheirsWaits(home));
    }

    [Fact]
    public void EveryBurialMovesTheGravesCounterAndALoadRebuildsIt()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace cemetery = StandACemetery(loop);
        int before = world.GraveGeneration;
        Keep(world, cemetery);
        StepUntil(loop, () => world.Unburied.Count == 0);
        Assert.Equal(before + 1, world.GraveGeneration);
        Assert.Equal(world.GraveGeneration, RoundTrip(loop).World.GraveGeneration);
        Assert.Equal(1, world.GravesLaidIn(cemetery).Count(id => id != 0));
    }

    // ---------------------------------------------------------------
    //  Step 8 (§9, D916): the golden that buries people
    // ---------------------------------------------------------------

    /// <summary>What the burying valley reached, counted as it ran.</summary>
    private sealed record Buried(SimWorld World, int Burials, int Visits, int KeptDays, int HelpingOut, int Repairs);

    /// <summary>
    /// The posed village (D916): the fixture's founding; its first death (old age, the mortality system's own path) learns the
    /// cemetery, and the free small one stands by the founding at once; a groundskeeper asked for in Professions and one adult
    /// pinned to it, as the player does, the labour pass left on; then 30 years, the eldest living who is not the keeper dying of
    /// old age every ten, so burials, the family's visit, the keeper's year of helping out and the wear that calls them back
    /// all happen.
    /// </summary>
    private static Buried ABuryingValley()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace cemetery = StandACemetery(loop);
        world.SetJobLimit(JobKind.Groundskeeper, 1);
        Villager keeper = world.Villagers.Where(v => v.Alive && v.CanTakeAJob).OrderBy(v => v.Id).First();
        keeper.PinnedTrade = JobKind.Groundskeeper;
        int visits = 0, kept = 0, helping = 0, repairs = 0;
        ulong repairedAt = cemetery.RepairedAtTick;
        var visited = new HashSet<(int, ulong)>();
        for (int year = 0; year < 30; year++)
        {
            if (year % 10 == 5 && world.Villagers.Where(v => v.Alive && v.Id != keeper.Id && v.CanWork).MaxBy(v => v.AgeYears) is Villager eldest)
            {
                DiesOnTheNextTick(eldest);
            }

            for (int tick = 0; tick < world.Config.TicksPerYear; tick++)
            {
                int before = cemetery.TendedDaysThisSeason;
                loop.StepOnce();
                kept += cemetery.TendedDaysThisSeason > before ? 1 : 0;
                helping += keeper.Alive && keeper.WorkplaceId == cemetery.Id && keeper.WorkNote.StartsWith("Nobody to bury", StringComparison.Ordinal) ? 1 : 0;
                if (cemetery.RepairedAtTick != repairedAt)
                {
                    repairs++;
                    repairedAt = cemetery.RepairedAtTick;
                }

                foreach (Villager v in world.Villagers)
                {
                    if (v.LastGraveVisitTick is ulong at && visited.Add((v.Id, at)))
                    {
                        visits++;
                    }
                }
            }
        }

        return new Buried(world, world.GravesLaidIn(cemetery).Count(id => id != 0), visits, kept, helping, repairs);
    }

    private static readonly Lazy<Buried> TheBuryingValley = new(ABuryingValley);

    /// <summary>
    /// ⭐ PHASE 6 GATE ITEM 2 — A GOLDEN THAT BURIES PEOPLE (D916, `cemetery.md §9` step 8): the valley above at Year 30. Every
    /// other golden has no cemetery, so this is the one that sees the graves, the visits, the keeping and the wear in the hash.
    /// </summary>
    [Fact]
    public void TheGoldenThatBuriesPeople()
    {
        Buried b = TheBuryingValley.Value;
        _output.WriteLine($"burials {b.Burials}, visits {b.Visits}, kept days {b.KeptDays}, helping-out ticks {b.HelpingOut}, repairs {b.Repairs}, alive {b.World.Population}, year {b.World.Clock.Year}, unburied {b.World.Unburied.Count}");
        _output.WriteLine($"hash {StateHash.Compute(b.World)}");
        Assert.True(b.Burials >= 2 && b.Visits > 0 && b.KeptDays > 0 && b.HelpingOut > 0 && b.Repairs > 0,
            "the golden's valley never reached a burial, a visit, a kept day, a keeper helping out and a repair");
        Assert.Equal(BuryingGolden, StateHash.Compute(b.World));
    }

    /// <summary>Taken 2026-11-26 (D916), on its first run.</summary>
    private const ulong BuryingGolden = 16671319007650085495UL; // 12492588746476946015 until W6 (D941): health — ablated (health off where it is read, `SimConfig.HealthOn`), it returns; 10431254814961408444 until W5 (D928): the new trades grow skill — ablated, it returns
}
