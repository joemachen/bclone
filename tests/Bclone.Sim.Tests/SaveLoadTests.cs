using System.Collections;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Persistence;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ Save and load — <b>a loaded village is the village that never stopped</b> (`specs/save-load.md §9`).
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>THE HASH IS NOT THE SAVE</b> (D507). <c>save → load → hash == live</c> is §4's guard and it is
/// kept (§9.1), but it is weak: the sim reads state the hash never mixes — a site's delivered materials,
/// last spring's path prices, the next workplace id — and a save that lost all of them would pass it. So
/// the guards that carry this feature are §9.2 (<b>run a year after loading and match the village that
/// never stopped, hash and log</b>) and §9.4 (<b>every field is saved or named, with its reason</b>).
/// </para>
/// <para>
/// ⛔ <b>Every pose is built by playing</b> — founding, the played opening, years run — never by editing
/// a capture (D419: a round trip posed from its own capture only agrees with itself).
/// </para>
/// </remarks>
public sealed class SaveLoadTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"bclone-saves-{Guid.NewGuid():N}");

    public SaveLoadTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_folder);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // ---------------------------------------------------------------
    //  The poses — each a village played to a moment worth saving at
    // ---------------------------------------------------------------

    /// <summary>Where each pose is saved, and what it is for (§9.2).</summary>
    public static IEnumerable<object[]> Poses() => new[]
    {
        new object[] { "played opening, building and walking (year 1)", 61 },
        new object[] { "played opening, the last tick before spring's re-price (year 3)", (480 * 2) - 1 },
        new object[] { "played opening, winter (year 3)", (480 * 2) + (120 * 3) + 37 },
        new object[] { "played opening, year 6", (480 * 5) + 201 },
        new object[] { "fixture village, winter (year 4)", (480 * 3) + (120 * 3) + 11 },
        new object[] { "established, share-out off (year 5)", (480 * 4) + 66 },

        // ⚠️ The two below were added for zeros (D509): with no pose holding a pending moment or a
        // "nowhere to keep it" latch, dropping either from the save scored 0 reds.
        new object[] { "established, every store closed (year 1)", 300 },
        new object[] { "unattended, emptied, its moment waiting (year 2)", 600 },
    };

    private static (SimLoop Loop, InMemoryLogSink Log) Played(string pose, int ticks)
    {
        var log = new InMemoryLogSink();
        SimLoop loop;
        if (pose.StartsWith("fixture", StringComparison.Ordinal))
        {
            loop = SimFactory.CreatePhase0(VillageFixtures.Village, log);
        }
        else if (pose.StartsWith("established, every store closed", StringComparison.Ordinal))
        {
            // The player closes every store at tick 40: the next load felled has nowhere to go, and the
            // village says so once — a latch the save must keep, or a loaded village says it twice.
            loop = SimFactory.CreatePhase0(ShippedConfig.Established(), log);
            loop.Step(40);
            foreach (StoreBuilding store in loop.World.StoreBuildings.ToList())
            {
                Assert.True(loop.World.SetStocking(store, Stocking.Closed).Allowed);
            }

            ticks -= 40;
        }
        else if (pose.StartsWith("unattended", StringComparison.Ordinal))
        {
            // Founded and left: the village is empty within the year, and its moment waits to be read.
            loop = SimFactory.CreatePhase0(ShippedConfig.Load(), log);
        }
        else if (pose.StartsWith("established", StringComparison.Ordinal))
        {
            loop = SimFactory.CreatePhase0(ShippedConfig.Established(), log);
            loop.World.VillageSharesOutWork = false;
        }
        else
        {
            loop = SimFactory.CreatePhase0(ShippedConfig.Load(), log);
            ColdStartTests.PlayTheOpening(loop.World);
        }

        Assert.Equal(480, loop.World.Config.TicksPerYear);
        loop.Step(ticks);
        return (loop, log);
    }

    private static SimLoop Reload(SimLoop loop, ISimLogger? log = null)
    {
        JsonObject document = SaveGame.Capture(loop, "test", "2026-10-05 12:00", "test");
        return SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), loop.World.Config, log);
    }

    // ---------------------------------------------------------------
    //  §9.1 — save → load → hash == live
    // ---------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Poses))]
    public void ALoadedVillageHashesAsTheOneSaved(string pose, int ticks)
    {
        (SimLoop loop, _) = Played(pose, ticks);
        SimLoop loaded = Reload(loop);

        Assert.Equal(loop.World.Tick, loaded.World.Tick);
        Assert.Equal(StateHash.Compute(loop.World), StateHash.Compute(loaded.World));
    }

    // ---------------------------------------------------------------
    //  §9.2 — save → load → a year → the village that never stopped
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ THE GUARD THAT SEES WHAT THE HASH DOES NOT. A field the sim reads and the save forgot makes the
    /// year diverge — in the hash, or in what the village says. Both are compared, every tick's log line.
    /// </summary>
    [Theory]
    [MemberData(nameof(Poses))]
    public void ALoadedVillageLivesTheYearTheSavedOneLives(string pose, int ticks)
    {
        (SimLoop loop, InMemoryLogSink log) = Played(pose, ticks);
        var loadedLog = new InMemoryLogSink();
        SimLoop loaded = Reload(loop, loadedLog);
        loadedLog.Clear();
        int said = log.Entries.Count;

        for (int t = 0; t < loop.World.Config.TicksPerYear; t++)
        {
            loop.StepOnce();
            loaded.StepOnce();
            if (StateHash.Compute(loop.World) != StateHash.Compute(loaded.World))
            {
                Assert.Fail($"{pose}: the loaded village parted from the saved one at tick {loop.World.Tick}.");
            }
        }

        string[] expected = log.Entries.Skip(said).Select(e => e.ToString()).ToArray();
        string[] actual = loadedLog.Entries.Select(e => e.ToString()).ToArray();
        Assert.True(expected.Length > 100 || loop.World.Population == 0,
            $"{pose}: only {expected.Length} lines in a year — the log comparison would be vacuous.");
        Assert.Equal(expected, actual);
        _output.WriteLine($"{pose}: {expected.Length} log lines and {loop.World.Config.TicksPerYear} hashes the same.");
    }

    /// <summary>The poses pose what they claim to — a guard over a pose that misses its moment is vacuous.</summary>
    [Fact]
    public void ThePosesCatchTheMomentsTheyAreNamedFor()
    {
        (SimLoop building, _) = Played("played opening, building and walking (year 1)", 61);
        Assert.Contains(building.World.Workplaces, w => w.Construction is { } site && !site.IsFinished && site.TotalDelivered > 0);
        Assert.Contains(building.World.Villagers, v => v.Alive && v.LegTicks > Fixed.Zero && v.LegWalked > Fixed.Zero);

        (SimLoop spring, _) = Played("played opening, the last tick before spring's re-price (year 3)", (480 * 2) - 1);
        Assert.True(spring.World.Paths.PathTiles > 0, "no worn path at the re-price pose: last spring's prices are not exercised.");
        Assert.Equal(Season.Winter, spring.World.Clock.Season);

        (SimLoop winter, _) = Played("played opening, winter (year 3)", (480 * 2) + (120 * 3) + 37);
        Assert.Equal(Season.Winter, winter.World.Clock.Season);

        (SimLoop off, _) = Played("established, share-out off (year 5)", (480 * 4) + 66);
        Assert.False(off.World.VillageSharesOutWork);
        Assert.False(Reload(off).World.VillageSharesOutWork);

        (SimLoop closed, _) = Played("established, every store closed (year 1)", 300);
        var said = (bool[])typeof(SimWorld).GetField("_saidThereIsNowhereFor", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(closed.World)!;
        Assert.Contains(true, said);

        (SimLoop emptied, _) = Played("unattended, emptied, its moment waiting (year 2)", 600);
        Assert.Equal(0, emptied.World.Population);
        Assert.NotEmpty(emptied.World.Moments);
    }

    // ---------------------------------------------------------------
    //  §9.3 — save → load → save gives the same text
    // ---------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Poses))]
    public void SavingALoadedVillageWritesTheSameSave(string pose, int ticks)
    {
        (SimLoop loop, _) = Played(pose, ticks);
        string first = SaveFile.TextOf(SaveGame.Capture(loop, "test", "then", "code"));
        string again = SaveFile.TextOf(SaveGame.Capture(Reload(loop), "test", "then", "code"));
        Assert.Equal(first, again);
    }

    // ---------------------------------------------------------------
    //  §9.4 — every field is saved, or named with its reason
    // ---------------------------------------------------------------

    private enum Kept
    {
        /// <summary>In the save, and compared field by field after a load.</summary>
        Saved,

        /// <summary>Not in the save; rebuilt on load from what is — and compared, so the rebuild is proven.</summary>
        Rebuilt,

        /// <summary>Not in the save and not compared: a cache, a counter or scratch, rebuilt on the first ask.</summary>
        Cache,

        /// <summary>Today's data, never the save's (§4): the config and the catalogues built from it.</summary>
        Data,
    }

    /// <summary>
    /// ⭐⭐ EVERY FIELD OF THE VILLAGE, AND WHAT THE SAVE DOES WITH IT. A field added to any class reachable
    /// from <see cref="SimWorld"/> fails <see cref="EveryFieldIsSavedOrNamed"/> until it is entered here — so
    /// the next slice cannot add state and lose it on load without saying so.
    /// </summary>
    private static readonly Dictionary<string, Kept> Fields = Table(
        (typeof(SimWorld), Kept.Saved, new[]
        {
            "Tick", "Rng", "Moments", "Libraries", "TownHall", "Wells", "FirstGranaryTick", "SaidTheyCanWrite",
            "FoodProducedThisYear", "FoodProducedLastYear", "FoodLedgerYears", "FoodEatenThisYear", "FoodEatenLastYear",
            "FoodEverProduced", "_foodEverProducedOf", "FoodEverEaten", "AFreeLibraryIsOwed", "AFreeSmithyIsOwed",
            "AFirstPathHasWorn", "ATownHallIsOwed", "SaidTheFoundersAreGone", "KnowledgeStates", "LastKnowerIds",
            "Villagers", "Households", "TravelCost", "Map", "Zones", "Paths", "StockLimits", "JobLimits",
            "VillageSharesOutWork", "Workplaces", "StoreBuildings", "GroundStacks", "_waitingOnTheGround",
            "ShownTheTechTree", "_saidThereIsNowhereFor", "_saidKnowledgeIsAtRisk", "_saidSiteIsWaiting",
            "_nextWorkplaceId", "NeedsMoreResidentialLand", "WorkIsGoingUndone", "WorkSeenUndoneOnce", "_buildingsNamed",
            "LogsEverFelled", "LogsEverSplit", "StoneEverDug", "IronEverDug", "ToolsEverForged", "IronToolsEverForged",
            "ToolsEverTaken", "WorkActionsBegun", "Seed",
        }),
        (typeof(SimWorld), Kept.Rebuilt, new[] { "_onTheGround" }),
        (typeof(SimWorld), Kept.Data, new[]
        {
            "Config", "GoodsCatalog", "Crops", "JobsCatalog", "TechniquesCatalog", "BuildingsCatalog", "Logger",
        }),
        (typeof(SimWorld), Kept.Cache, new[]
        {
            "_terrainGeneration", "BuildingGeneration", "StandingGeneration", "_standing", "_standingOwner",
            "_standingFootprints", "_standingBuiltAt", "_freeGroundToday", "_freeGroundTodayGeneration",
            "_freeGroundTodayWalls", "_plotsAsked", "_trialWalls", "_trialHome", "AlwaysSweepTheWholeValley",
        }),
        (typeof(Villager), Kept.Saved, new[]
        {
            "Id", "Name", "Surname", "LifespanYears", "HouseholdId", "PartnerId", "LifeStage", "WorkplaceId",
            "LastWorkplaceId", "PinnedTrade", "JobReason", "WorkNote", "CommuteNote", "BirthTick", "Founder", "AgeYears",
            "Vigour", "Stage", "Hunger", "TicksAtMaxHunger", "Cold", "State", "_position", "LegFrom", "LegTo",
            "LegTarget", "LegTicks", "LegWalked", "Carried", "ErrandX", "ErrandY", "ActionTicksRemaining", "JustAte",
            "Alive", "CauseOfDeath", "DiedAtTick", "WintersSurvived", "TotalGathers", "GathersThisSeason",
            "SplitsThisStint", "ToolUses", "ToolGood", "ForgesThisStint", "DigsThisStint", "Rhythm", "Skills",
        }),
        (typeof(Villager), Kept.Cache, new[] { "PacedOnTick", "PaceLeft" }),
        (typeof(Household), Kept.Saved, new[]
        {
            "Id", "_surname", "GivenName", "HomePosition", "HomeFacing", "WhyHere", "FencedTiles", "LastBirthYear",
            "DayForAChild", "ToppingUpFood", "ToppingUpFirewood", "WaterDrawnOnDay", "Stockpile", "_memberIds",
        }),
        (typeof(Workplace), Kept.Saved, new[]
        {
            "Id", "Kind", "_name", "BornAs", "_position", "Facing", "ExtentWidth", "ExtentHeight", "Capacity",
            "GatheringRadius", "Mode", "ForgeGood", "WorkerIds", "Construction", "Store", "FieldSixteenthsSown",
            "FieldHandsAtAutumn", "FieldTilesLearned", "FieldWalkWhenLearned", "FieldClearedAtTick",
            "FieldProbedThisYear", "FieldProbeFailed", "StaffingOverride", "QueueRank",
        }),
        (typeof(Workplace), Kept.Cache, new[] { "_covered", "CachedWoodedTiles", "CachedAtTerrainGeneration" }),
        (typeof(StoreBuilding), Kept.Saved, new[]
        {
            "Id", "Kind", "_name", "BornAs", "_position", "Facing", "RaisedAs", "ExtentWidth", "ExtentHeight", "Store",
            "AllowedGoods", "Stocking", "_limits",
        }),
        (typeof(StoreBuilding), Kept.Data, new[] { "Catalog" }),
        (typeof(ConstructionSite), Kept.Saved, new[]
        {
            "Kind", "Name", "ForHouseholdId", "MovingFrom", "Facing", "Demolishing", "Recipe", "_delivered", "WorkDone",
        }),
        (typeof(BuildingRecipe), Kept.Saved, new[] { "_materials", "WorkTicks" }),
        (typeof(GeneratedMap), Kept.Saved, new[]
        {
            "_terrain", "_crop", "_youngSapling", "_laidBare", "Width", "Height", "MinX", "MinY", "FoundingSite",
            "_everWooded",
        }),
        (typeof(ZoneMap), Kept.Saved, new[]
        {
            "_residentialSub", "_workGroundSub", "_harvestSub", "_destroySub", "_plot", "_lane", "_plotByOwner",
            "_laneByOwner", "_walls", "_fenceByOwner", "_gateFrontByOwner", "_gateFronts",
        }),
        (typeof(ZoneMap), Kept.Rebuilt, new[]
        {
            "_residential", "_workGround", "_tilesByOwner", "_harvest", "_residentialCount", "_workGroundCount",
            "_harvestCount", "_destroy", "_destroyCount", "_width", "_height", "_minX", "_minY", "_subWidth",
            "_subHeight", "ResidentialTiles", "_wholeResidential", "_groundByOwner", "HarvestTiles", "_paintedHarvest",
            "DestroyTiles", "_markedToDestroy",
        }),
        (typeof(ZoneMap), Kept.Cache, new[] { "WallGeneration", "Edits" }),
        (typeof(PathWear), Kept.Saved, new[]
        {
            "_wear", "_priceClass", "TroddenTiles", "PathTiles", "RoutesGeneration", "_routesDirty",
        }),
        (typeof(PathWear), Kept.Rebuilt, new[]
        {
            "_width", "_height", "_minX", "_minY", "_ceiling", "_wornAt", "_packedAt", "_holdsFor",
        }),
        (typeof(PathWear), Kept.Cache, new[] { "Generation" }),
        (typeof(TravelCostField), Kept.Saved, new[] { "_anythingWorn", "_entryCost" }),
        (typeof(TravelCostField), Kept.Rebuilt, new[] { "_ticksPerBaseTile", "_wornCost", "_packedCost" }),
        (typeof(TravelCostField), Kept.Cache, new[]
        {
            // `_map` and `_wear` are the world's own map and wear, compared where they hang; the flow fields
            // and their scratch are a cache; `_builtAtWearGeneration` is saved as whether it is current.
            "_map", "_wear", "_fields", "_obstacles", "_builtAtObstacleGeneration", "_builtAtWallGeneration",
            "_builtAtWearGeneration", "_scratch",
        }),
        (typeof(Stockpile), Kept.Saved, new[] { "_held", "_produced", "Capacity" }),
        (typeof(SkillProgress), Kept.Saved, new[] { "SkillId", "Ticks", "Work", "Mastered", "MasteredHere" }),
        (typeof(GroundStack), Kept.Saved, new[] { "Position", "Goods", "Amount" }),
        (typeof(Moment), Kept.Saved, new[] { "Title", "Body", "WaitsToBeDismissed" }),
        (typeof(Well), Kept.Saved, new[] { "_position", "Facing", "ExtentWidth", "ExtentHeight", "Name", "Kind", "Draws" }),
        (typeof(TownHall), Kept.Saved, new[] { "_position", "Facing", "ExtentWidth", "ExtentHeight", "Name", "RaisedAtTick" }),
        (typeof(Library), Kept.Saved, new[] { "_position", "Facing", "ExtentWidth", "ExtentHeight", "Name", "Shelves", "Records" }),
        (typeof(StockLimits), Kept.Saved, new[] { "_limits" }),
        (typeof(JobLimits), Kept.Saved, new[] { "_targets" }));

    /// <summary>
    /// ⭐⭐ §9.4. Every instance field of every class reachable from <see cref="SimWorld"/> through what is
    /// saved or rebuilt is in <see cref="Fields"/> — and the table names nothing that is not there.
    /// </summary>
    [Fact]
    public void EveryFieldIsSavedOrNamed()
    {
        var unnamed = new List<string>();
        var seen = new HashSet<Type>();
        var queue = new Queue<Type>();
        queue.Enqueue(typeof(SimWorld));
        while (queue.Count > 0)
        {
            Type type = queue.Dequeue();
            if (!seen.Add(type))
            {
                continue;
            }

            foreach (FieldInfo field in InstanceFields(type))
            {
                string key = $"{type.Name}.{NameOf(field)}";
                if (!Fields.TryGetValue(key, out Kept kept))
                {
                    unnamed.Add($"{key} : {field.FieldType.Name}");
                    continue;
                }

                if (kept is Kept.Saved or Kept.Rebuilt)
                {
                    foreach (Type inner in TypesIn(field.FieldType))
                    {
                        queue.Enqueue(inner);
                    }
                }
            }
        }

        Assert.True(unnamed.Count == 0,
            "Fields the save neither saves nor names — save them, or enter them in SaveLoadTests.Fields with "
            + "their reason:\n  " + string.Join("\n  ", unnamed));

        // And the table does not name a field that has gone, which would be a reason nobody can check.
        var stale = Fields.Keys.Where(key => !seen.Any(t => key.StartsWith(t.Name + ".", StringComparison.Ordinal)
            && InstanceFields(t).Any(f => $"{t.Name}.{NameOf(f)}" == key))).ToList();
        Assert.True(stale.Count == 0, "SaveLoadTests.Fields names fields that are not there:\n  " + string.Join("\n  ", stale));
    }

    /// <summary>
    /// ⭐ §9.4's other half: every field the table says is saved or rebuilt <b>is</b> — compared field by field,
    /// deep, between the village saved and the village loaded.
    /// </summary>
    [Theory]
    [MemberData(nameof(Poses))]
    public void EverySavedFieldComesBackAsItWas(string pose, int ticks)
    {
        (SimLoop loop, _) = Played(pose, ticks);
        SimLoop loaded = Reload(loop);

        var differences = new List<string>();
        Compare(loop.World, loaded.World, "world", differences, new HashSet<object>(ReferenceEqualityComparer.Instance));
        Assert.True(differences.Count == 0, $"{pose}:\n  " + string.Join("\n  ", differences.Take(20)));
    }

    // ---------------------------------------------------------------
    //  §9.5 — refusals, in words, and never a half-loaded village
    // ---------------------------------------------------------------

    private string Saved(Action<JsonObject>? change = null)
    {
        (SimLoop loop, _) = Played("fixture village, winter (year 4)", 300);
        JsonObject document = SaveGame.Capture(loop, "0.0.1", "2026-10-05 12:00", NewGame.ShareCode(
            loop.World.Config, NewGame.Defaults(loop.World.Config, loop.World.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        change?.Invoke(document);
        string path = Path.Combine(_folder, $"{Guid.NewGuid():N}{SaveFile.Extension}");
        SaveFile.Write(path, document);
        return path;
    }

    [Fact]
    public void ASoundSaveOpens()
    {
        SaveOpened opened = SaveFile.Open(Saved(), VillageFixtures.Village);
        Assert.Null(opened.Refusal);
        Assert.NotNull(opened.Loop);
        Assert.Equal(300UL, opened.Header!.Tick);
    }

    [Fact]
    public void AnotherFormatIsRefusedNamingTheBuildThatWroteIt()
    {
        string path = Saved(doc => { doc["format"] = 99; doc["build"] = "0.0.3"; });
        SaveOpened opened = SaveFile.Open(path, VillageFixtures.Village);
        Assert.Null(opened.Loop);
        Assert.Equal("Saved by build 0.0.3, whose saves this build cannot read.", opened.Refusal);
        Assert.False(File.Exists(path + SaveFile.BrokenSuffix), "a sound save of another format is not damage");
    }

    public static IEnumerable<object[]> Damage() => new[]
    {
        new object[] { "cut short" },
        new object[] { "not gzip" },
        new object[] { "not JSON" },
        new object[] { "a key missing" },
        new object[] { "a key written twice" },
        new object[] { "a wrong type" },
    };

    [Theory]
    [MemberData(nameof(Damage))]
    public void ADamagedSaveIsRefusedAndKeptAsBad(string damage)
    {
        string path = Saved(damage switch
        {
            "a key missing" => doc => doc["world"]!["villagers"]![0]!.AsObject().Remove("hunger"),
            "a wrong type" => doc => doc["world"]!["villagers"]![0]!["hunger"] = "hungry",
            _ => null,
        });

        byte[] gz = File.ReadAllBytes(path);
        string text = Unzip(gz);
        switch (damage)
        {
            case "cut short":
                File.WriteAllBytes(path, gz[..(gz.Length / 2)]);
                break;
            case "not gzip":
                File.WriteAllText(path, text);
                break;
            case "not JSON":
                File.WriteAllBytes(path, Zip("nonsense"));
                break;
            case "a key written twice":
                File.WriteAllBytes(path, Zip(text.Replace("\"format\": 1,", "\"format\": 1,\n  \"format\": 1,", StringComparison.Ordinal)));
                break;
        }

        byte[] before = File.ReadAllBytes(path);
        SaveOpened opened = SaveFile.Open(path, VillageFixtures.Village);

        Assert.Null(opened.Loop);
        Assert.Equal($"This save can't be read — it's damaged. A copy was kept as {Path.GetFileName(path)}{SaveFile.BrokenSuffix}.", opened.Refusal);
        Assert.Equal(before, File.ReadAllBytes(path + SaveFile.BrokenSuffix));
        Assert.NotNull(opened.Detail);
        _output.WriteLine($"{damage}: {opened.Detail}");
    }

    [Fact]
    public void ASaveHoldingWhatTheDataNoLongerHasIsRefusedNamingIt()
    {
        string path = Saved(doc => doc["world"]!["workplaces"]![0]!["kind"] = "Smokehouse");
        SaveOpened opened = SaveFile.Open(path, VillageFixtures.Village);

        Assert.Null(opened.Loop);
        Assert.Equal("This save holds “Smokehouse”, which this game no longer has, so it can't be opened.", opened.Refusal);
        Assert.False(File.Exists(path + SaveFile.BrokenSuffix), "a sound save over changed data is not damage");
    }

    [Fact]
    public void ASaveThatIsNotThereSaysSo()
    {
        SaveOpened opened = SaveFile.Open(Path.Combine(_folder, "gone.save"), VillageFixtures.Village);
        Assert.Null(opened.Loop);
        Assert.Equal("That save is not there any more.", opened.Refusal);
    }

    // ---------------------------------------------------------------
    //  §9.6 — writing
    // ---------------------------------------------------------------

    [Fact]
    public void WritingLeavesNoPartBehindAndReplacesTheOldSave()
    {
        string path = Saved();
        string again = Saved();
        File.Move(again, path, overwrite: true);
        SaveFile.Write(path, SaveGame.Capture(Played("fixture village, winter (year 4)", 10).Loop, "0.0.1", "now", "code"));

        Assert.Equal(10UL, SaveFile.ReadHeader(path).Header!.Tick);
        Assert.Empty(Directory.GetFiles(_folder, "*" + SaveFile.PartSuffix));
    }

    [Fact]
    public void AutosavesKeepTheNewestThree()
    {
        SimLoop loop = Played("fixture village, winter (year 4)", 0).Loop;
        for (int n = 1; n <= 5; n++)
        {
            loop.Step(10);
            SaveFile.WriteAutosave(_folder, SaveGame.Capture(loop, "0.0.1", "now", "code"), keep: 3);
        }

        Assert.Equal(50UL, SaveFile.ReadHeader(SaveFile.AutosavePath(_folder, 1)).Header!.Tick);
        Assert.Equal(40UL, SaveFile.ReadHeader(SaveFile.AutosavePath(_folder, 2)).Header!.Tick);
        Assert.Equal(30UL, SaveFile.ReadHeader(SaveFile.AutosavePath(_folder, 3)).Header!.Tick);
        Assert.False(File.Exists(SaveFile.AutosavePath(_folder, 4)));
        Assert.Equal(3, Directory.GetFiles(_folder).Length);
    }

    // ---------------------------------------------------------------
    //  §9.7 — a faulted village is never saved
    // ---------------------------------------------------------------

    private sealed class Throws : ISimSystem
    {
        public string Name => "throws";

        public void Execute(SimWorld world) => throw new InvalidOperationException("a test's fault");
    }

    [Fact]
    public void AFaultedVillageRefusesToSave()
    {
        var loop = new SimLoop(SimWorld.Create(VillageFixtures.Village), new ISimSystem[] { new Throws() });
        Assert.Throws<SimSystemException>(() => loop.StepOnce());

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => SaveGame.Capture(loop, "0.0.1", "now", "code"));
        Assert.Contains("cannot be saved", refused.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  Reflection, kept here so the guard reads as one piece
    // ---------------------------------------------------------------

    private static Dictionary<string, Kept> Table(params (Type Type, Kept Kept, string[] Names)[] rows)
    {
        var table = new Dictionary<string, Kept>(StringComparer.Ordinal);
        foreach ((Type type, Kept kept, string[] names) in rows)
        {
            foreach (string name in names)
            {
                table.Add($"{type.Name}.{name}", kept);
            }
        }

        return table;
    }

    private static IEnumerable<FieldInfo> InstanceFields(Type type) =>
        type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

    /// <summary>A field's name as written — an auto-property's backing field by its property's name.</summary>
    private static string NameOf(FieldInfo field) =>
        field.Name.StartsWith('<') ? field.Name[1..field.Name.IndexOf('>', StringComparison.Ordinal)] : field.Name;

    /// <summary>The sim's own classes a field holds — itself, or what its list, array or dictionary holds.</summary>
    private static IEnumerable<Type> TypesIn(Type type)
    {
        if (type.IsArray)
        {
            return TypesIn(type.GetElementType()!);
        }

        if (type.IsGenericType)
        {
            return type.GetGenericArguments().SelectMany(TypesIn);
        }

        return type.Assembly == typeof(SimWorld).Assembly && type.IsClass ? new[] { type } : Array.Empty<Type>();
    }

    private static void Compare(object? expected, object? actual, string path, List<string> differences, HashSet<object> walked)
    {
        if (expected is null || actual is null)
        {
            if (!ReferenceEquals(expected, actual))
            {
                differences.Add($"{path}: {Show(expected)} saved, {Show(actual)} loaded");
            }

            return;
        }

        Type type = expected.GetType();
        if (type != actual.GetType())
        {
            differences.Add($"{path}: a {type.Name} saved, a {actual.GetType().Name} loaded");
            return;
        }

        if (type.IsValueType || expected is string)
        {
            if (!expected.Equals(actual))
            {
                differences.Add($"{path}: {Show(expected)} saved, {Show(actual)} loaded");
            }

            return;
        }

        if (!walked.Add(expected))
        {
            return;
        }

        if (expected is IDictionary saved && actual is IDictionary loaded)
        {
            var keys = saved.Keys.Cast<object>().Select(k => k.ToString()!).OrderBy(k => k, StringComparer.Ordinal).ToList();
            var theirs = loaded.Keys.Cast<object>().Select(k => k.ToString()!).OrderBy(k => k, StringComparer.Ordinal).ToList();
            if (!keys.SequenceEqual(theirs))
            {
                differences.Add($"{path}: keys [{string.Join(", ", keys)}] saved, [{string.Join(", ", theirs)}] loaded");
                return;
            }

            foreach (object key in saved.Keys)
            {
                Compare(saved[key], loaded[key], $"{path}[{key}]", differences, walked);
            }

            return;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() is Type set
            && (set == typeof(HashSet<>) || set == typeof(SortedSet<>)))
        {
            var mine = ((IEnumerable)expected).Cast<object>().Select(o => o.ToString()!).OrderBy(s => s, StringComparer.Ordinal);
            var yours = ((IEnumerable)actual).Cast<object>().Select(o => o.ToString()!).OrderBy(s => s, StringComparer.Ordinal);
            if (!mine.SequenceEqual(yours))
            {
                differences.Add($"{path}: a different set");
            }

            return;
        }

        if (expected is IList list && actual is IList other)
        {
            if (list.Count != other.Count)
            {
                differences.Add($"{path}: {list.Count} saved, {other.Count} loaded");
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                Compare(list[i], other[i], $"{path}[{i}]", differences, walked);
            }

            return;
        }

        foreach (FieldInfo field in InstanceFields(type))
        {
            if (Fields.TryGetValue($"{type.Name}.{NameOf(field)}", out Kept kept) && kept is Kept.Saved or Kept.Rebuilt)
            {
                Compare(field.GetValue(expected), field.GetValue(actual), $"{path}.{NameOf(field)}", differences, walked);
            }
        }
    }

    private static string Show(object? value) => value switch
    {
        null => "null",
        string text => $"\"{text}\"",
        _ => value.ToString() ?? "?",
    };

    private static string Unzip(byte[] gz)
    {
        using var zipped = new GZipStream(new MemoryStream(gz), CompressionMode.Decompress);
        using var reader = new StreamReader(zipped, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static byte[] Zip(string text)
    {
        using var memory = new MemoryStream();
        using (var zipped = new GZipStream(memory, CompressionLevel.Optimal))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            zipped.Write(bytes, 0, bytes.Length);
        }

        return memory.ToArray();
    }
}
