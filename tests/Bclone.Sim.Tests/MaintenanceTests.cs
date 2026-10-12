using System.Text.Json.Nodes;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Persistence;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Building condition and maintenance in the village — <c>specs/building-condition.md</c> (D552; Joe, D553: *"yes to
/// all"*). Commit 1: every standing thing is stamped when it is raised, the stamps are hashed and saved.
/// </summary>
public sealed class MaintenanceTests
{
    private readonly ITestOutputHelper _output;

    public MaintenanceTests(ITestOutputHelper output) => _output = output;

    private static SimLoop AVillage() => SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());

    private static StoreBuilding RaiseAGranary(SimWorld world)
    {
        ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
        Workplace site = world.Workplaces.Single(w => w.Construction?.Kind == BuildingKind.Granary);
        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        world.Complete(site);
        return world.StoreBuildings.Where(s => s.RaisedAs == BuildingKind.Granary).OrderByDescending(s => s.Id).First();
    }

    // ---------------------------------------------------------------
    //  §4.1 — the stamps
    // ---------------------------------------------------------------

    /// <summary>A thing raised later is stamped with the tick it was finished; the warm start's stand at tick 0; nothing is mended yet.</summary>
    [Fact]
    public void WhatIsRaisedIsStampedWithTheTickItWasFinished()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;

        Assert.All(world.Workplaces.Where(w => !w.IsSite), w => Assert.Equal(0UL, w.RaisedAtTick));
        Assert.All(world.Households.Where(h => h.HasHome), h => Assert.Equal(0UL, h.HomeRaisedAtTick));

        loop.Step(123);
        ulong now = world.Tick;

        Workplace lodge = HuntingTests.RaiseALodgeFor(world);
        Assert.Equal(now, lodge.RaisedAtTick);
        Assert.Equal(0UL, lodge.RepairedAtTick);

        StoreBuilding granary = RaiseAGranary(world);
        Assert.Equal(now, granary.RaisedAtTick);

        Well well = ObstacleTests.RaiseAWellNearTheFounding(world);
        Assert.Equal(now, well.RaisedAtTick);

        _output.WriteLine($"raised at {now}: {lodge.Name}, {granary.Name}, {well.Name}");
    }

    /// <summary>A house the village raises for a family is stamped too — the home is a household's, not a class of its own.</summary>
    [Fact]
    public void AHouseTheVillageRaisesIsStamped()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        int founders = world.Households.Count;

        // Forty years: what `HousesAreBuiltTests.TheVillageStillHousesItself` needs for the fixture to form and house a household.
        loop.Step(world.Config.TicksPerYear * 40);

        List<Household> newHomes = world.Households.Where(h => h.Id > founders && h.HasHome).ToList();
        _output.WriteLine($"{newHomes.Count} households housed after the founders, of {world.Households.Count}");
        Assert.NotEmpty(newHomes);
        // A new couple may inherit a founders' house, raised at tick 0 (`HouseTheRoofless`), so 0 is honest for those;
        // at least one house was raised for a family later and carries its tick. Older houses may already have been
        // mended (commit 2): a repair stamp is later than the raise, or never.
        Assert.Contains(newHomes, h => h.HomeRaisedAtTick > 0);
        Assert.All(newHomes, h =>
        {
            Assert.InRange(h.HomeRaisedAtTick, 0UL, world.Tick);
            Assert.True(h.HomeRepairedAtTick == 0 || h.HomeRepairedAtTick > h.HomeRaisedAtTick);
        });
    }

    // ---------------------------------------------------------------
    //  §6 — hashed and saved
    // ---------------------------------------------------------------

    /// <summary>Both stamps on every kind of standing thing are in the hash: a village mended a tick later is a different village.</summary>
    [Fact]
    public void TheStampsAreHashedOnEveryKind()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        loop.Step(7);
        Workplace lodge = HuntingTests.RaiseALodgeFor(world);
        StoreBuilding granary = RaiseAGranary(world);
        Well well = ObstacleTests.RaiseAWellNearTheFounding(world);
        Household home = world.Households.First(h => h.HasHome);
        var library = new Library { Position = Point.CentreOf(new GridPos(40, 40)), Name = "library", Shelves = 2 };
        world.Libraries.Add(library);
        var hall = new TownHall { Position = Point.CentreOf(new GridPos(41, 40)), Name = "town hall", RaisedAtTick = 3 };
        world.TownHall = hall;
        var bridge = new Bridge { Near = Point.CentreOf(new GridPos(42, 40)), Far = Point.CentreOf(new GridPos(42, 44)), Kind = BuildingKind.Bridge, Name = "bridge" };
        world.Bridges.Add(bridge);

        ulong before = StateHash.Compute(world);
        void Differs(string what, Action change, Action undo)
        {
            change();
            Assert.True(before != StateHash.Compute(world), $"{what} is not in the hash");
            undo();
            Assert.Equal(before, StateHash.Compute(world));
        }

        Differs("a workplace's raise", () => lodge.RaisedAtTick++, () => lodge.RaisedAtTick--);
        Differs("a workplace's repair", () => lodge.RepairedAtTick = 5, () => lodge.RepairedAtTick = 0);
        Differs("a store's raise", () => granary.RaisedAtTick++, () => granary.RaisedAtTick--);
        Differs("a store's repair", () => granary.RepairedAtTick = 5, () => granary.RepairedAtTick = 0);
        Differs("a well's raise", () => well.RaisedAtTick++, () => well.RaisedAtTick--);
        Differs("a well's repair", () => well.RepairedAtTick = 5, () => well.RepairedAtTick = 0);
        Differs("a home's raise", () => home.HomeRaisedAtTick = 5, () => home.HomeRaisedAtTick = 0);
        Differs("a home's repair", () => home.HomeRepairedAtTick = 5, () => home.HomeRepairedAtTick = 0);
        Differs("a library's raise", () => library.RaisedAtTick = 5, () => library.RaisedAtTick = 0);
        Differs("a library's repair", () => library.RepairedAtTick = 5, () => library.RepairedAtTick = 0);
        Differs("the hall's raise", () => world.TownHall = new TownHall { Position = hall.Position, Name = hall.Name, RaisedAtTick = 4 }, () => world.TownHall = hall);
        Differs("the hall's repair", () => hall.RepairedAtTick = 5, () => hall.RepairedAtTick = 0);
        Differs("a bridge's repair", () => bridge.RepairedAtTick = 5, () => bridge.RepairedAtTick = 0);
    }

    /// <summary>A village with stamped things saves and loads to the same hash, stamps included, and a year on is still the same village.</summary>
    [Fact]
    public void TheStampsAreSavedAndTheVillageRunsOnTheSame()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        loop.Step(50);
        Workplace lodge = HuntingTests.RaiseALodgeFor(world);
        lodge.RepairedAtTick = 60;
        Well well = ObstacleTests.RaiseAWellNearTheFounding(world);
        loop.Step(world.Config.TicksPerSeason);

        SimLoop loaded = SaveGame.Load(
            JsonNode.Parse(SaveFile.TextOf(SaveGame.Capture(loop, "test", "now", "code")))!.AsObject(), world.Config);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded.World));
        Workplace loadedLodge = loaded.World.Workplaces.Single(w => w.Id == lodge.Id);
        Assert.Equal((lodge.RaisedAtTick, lodge.RepairedAtTick), (loadedLodge.RaisedAtTick, loadedLodge.RepairedAtTick));
        Assert.Equal(well.RaisedAtTick, loaded.World.Wells.Single().RaisedAtTick);

        loop.Step(world.Config.TicksPerYear);
        loaded.Step(world.Config.TicksPerYear);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded.World));
    }

    /// <summary>A save from before the stamps (format 4) is refused in words — no migrations before v1 (D507). Format 6 since the nomads (D565).</summary>
    [Fact]
    public void AFormatFourSaveIsRefused()
    {
        SimLoop loop = AVillage();
        JsonObject doc = SaveGame.Capture(loop, "test", "now", "code");
        Assert.Equal(6, SaveGame.Format);
        doc["format"] = 4;
        SaveFormatException refused = Assert.Throws<SaveFormatException>(() => SaveGame.Load(doc, loop.World.Config));
        Assert.Contains("Format 4", refused.Message, StringComparison.Ordinal);
        Assert.Contains("reads 6", refused.Message, StringComparison.Ordinal);
    }
}
