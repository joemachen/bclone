using System.Text.Json.Nodes;
using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Determinism;
using Kith.Sim.Logging;
using Kith.Sim.Persistence;
using Kith.Sim.World;
using Xunit;

namespace Kith.Sim.Tests;

/// <summary>
/// W4, the cemetery and the groundskeeper (`specs/cemetery.md`, D905). Step 1 (§9): <b>who lies unburied</b> — the dead since
/// <see cref="SimWorld.BurialsFromTick"/>, oldest first, kept where a death happens and rebuilt on load — and the two fields
/// it reads, saved and hashed sparsely.
/// </summary>
public sealed class CemeteryTests
{
    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop AVillage() => SimFactory.CreatePhase0(Config, new InMemoryLogSink());

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
}
