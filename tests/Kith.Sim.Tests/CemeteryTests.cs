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
}
