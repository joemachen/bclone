using System.Text.Json;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The new-game screen's rules, without the screen (D477, `specs/new-game-screen.md §8`): the seed
/// text, the rows and the share code, and the river's four directions.
/// </summary>
public class NewGameScreenTests(ITestOutputHelper output)
{
    private static SimConfig Shipped => ShippedConfig.Load();

    private static readonly string[] Courses = { "we", "ns", "nwse", "swne" };

    // ---- §8.1 Seeds are forever ---------------------------------------------------------------

    /// <summary>
    /// ⛔ <b>A seed somebody wrote down names the same valley forever.</b> If one of these moves, a
    /// forum post's valley moved with it — the rule is wrong, not the pin.
    /// </summary>
    [Theory]
    [InlineData("mossy-lantern-41", 5293820756258946470UL)]
    [InlineData("joe", 6108232914491271416UL)]
    [InlineData("fernhollow", 6941606793966775994UL)]
    [InlineData("Ælfwyn's valley", 16434584379206539583UL)]
    [InlineData("a", 16677246551406920746UL)]
    [InlineData("18446744073709551616", 17714302075635298028UL)] // one past ulong: hashed, not wrapped
    public void ASeedTextNamesTheSameValleyForever(string text, ulong seed) =>
        Assert.Equal(seed, SeedText.ToSeed(text));

    /// <summary>A plain number stays that number, so every seed already quoted keeps its valley.</summary>
    [Theory]
    [InlineData("12345", 12345UL)]
    [InlineData("41219", 41219UL)]
    [InlineData(" 007 ", 7UL)]
    [InlineData("18446744073709551615", ulong.MaxValue)]
    public void APlainNumberIsThatSeed(string text, ulong seed) =>
        Assert.Equal(seed, SeedText.ToSeed(text));

    /// <summary>A seed is read aloud and retyped: case and spacing do not make a new valley.</summary>
    [Theory]
    [InlineData("Joe", "joe")]
    [InlineData("  joe ", "joe")]
    [InlineData("Mossy   Lantern", "mossy lantern")]
    [InlineData("mossy\tlantern", "mossy lantern")]
    public void CaseAndSpacingDoNotMakeANewValley(string typed, string same) =>
        Assert.Equal(SeedText.ToSeed(same), SeedText.ToSeed(typed));

    [Theory]
    [InlineData("", "Type a seed")]
    [InlineData("   ", "Type a seed")]
    [InlineData("oak#river=3", "cannot hold")]
    public void ASeedThatIsNotOneIsRefusedInWords(string text, string sentence)
    {
        Assert.False(SeedText.TryToSeed(text, out _, out string? refusal));
        Assert.Contains(sentence, refusal, StringComparison.Ordinal);
    }

    // ---- §8.2 Defaults reproduce the config's valley ------------------------------------------

    /// <summary>
    /// The screen at its defaults founds exactly the valley the config file names — the whole world,
    /// not only the map.
    /// </summary>
    [Fact]
    public void TheDefaultsFoundTheConfigsValley()
    {
        SimConfig shipped = Shipped;
        NewGameSettings defaults = NewGame.Defaults(shipped, shipped.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SimConfig founded = NewGame.Apply(shipped, defaults);

        SimWorld fromFile = SimWorld.Create(shipped);
        SimWorld fromScreen = SimWorld.Create(founded);

        Assert.Equal(StateHash.MixMap(0UL, fromFile.Map), StateHash.MixMap(0UL, fromScreen.Map));
        Assert.Equal(StateHash.Compute(fromFile), StateHash.Compute(fromScreen));
        Assert.Equal(fromFile.Name, fromScreen.Name);
    }

    /// <summary>Each shipped row's default is the shipped file's value — never a number in code.</summary>
    [Fact]
    public void EveryRowDefaultsToTheShippedValue()
    {
        NewGameSettings defaults = NewGame.Defaults(Shipped, "oak");

        Assert.Equal("3", defaults.Values["river"]);
        Assert.Equal("we", defaults.Values["flow"]);
        Assert.Equal("35", defaults.Values["woods"]);
        Assert.Equal("moderate", defaults.Values["stone"]);
        Assert.Equal("moderate", defaults.Values["iron"]);
        Assert.Equal(new[] { "river", "flow", "woods", "stone", "iron" }, Shipped.NewGameOptions.Select(r => r.Id));
    }

    // ---- §8.3 West to east is today's river ----------------------------------------------------
    // Guarded by `MapGenerationTests.DrawOrderIsTheContract` and the per-seed terrain fingerprints:
    // `we` is the default and the shipped file's value, so a change to its carve moves both.

    // ---- §8.4 A diagonal river has no ford -----------------------------------------------------

    /// <summary>
    /// Every river divides the valley: its banks are separate land, so nobody walks across without a
    /// bridge — the diagonals as much as the straight ones.
    /// </summary>
    /// <remarks>
    /// The route field steps east, west, south and north only (`TerrainCostField`), and
    /// <c>LineOfSight</c> refuses a leg through the corner where two water tiles meet, so land that is
    /// not four-connected is not walkable between. A ford would join the banks into one piece.
    /// </remarks>
    [Theory]
    [InlineData("we")]
    [InlineData("ns")]
    [InlineData("nwse")]
    [InlineData("swne")]
    public void EveryRiverDividesTheValley(string course)
    {
        SimConfig config = Shipped with { RiverCourse = course };

        for (ulong seed = 1; seed <= 40; seed++)
        {
            GeneratedMap map = MapGenerator.Generate(config, seed);
            int[] pieces = LandPieces(map);
            int land = pieces.Sum();

            Assert.True(pieces.Length >= 2,
                $"{course}, seed {seed}: the land is one piece ({land} tiles) — the river has a ford.");

            int secondLargest = pieces.OrderDescending().Skip(1).First();
            Assert.True(secondLargest * 10 >= land,
                $"{course}, seed {seed}: the far bank is {secondLargest} of {land} land tiles — the river "
                + "cuts off a corner, not the valley.");
        }
    }

    /// <summary>The sizes of the four-connected pieces of land, largest first.</summary>
    private static int[] LandPieces(GeneratedMap map)
    {
        int w = map.Width;
        int h = map.Height;
        var piece = new int[w * h];
        var sizes = new List<int>();
        var queue = new Queue<int>();

        for (int start = 0; start < w * h; start++)
        {
            if (piece[start] != 0 || IsWater(map, start % w, start / w))
            {
                continue;
            }

            int id = sizes.Count + 1;
            int size = 0;
            piece[start] = id;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                size++;
                int x = at % w;
                int y = at / w;
                foreach ((int nx, int ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                    {
                        continue;
                    }

                    int next = (ny * w) + nx;
                    if (piece[next] == 0 && !IsWater(map, nx, ny))
                    {
                        piece[next] = id;
                        queue.Enqueue(next);
                    }
                }
            }

            sizes.Add(size);
        }

        return sizes.OrderDescending().ToArray();
    }

    private static bool IsWater(GeneratedMap map, int x, int y) =>
        map.TerrainAt(new GridPos(x + map.MinX, y + map.MinY)) == Terrain.Water;

    /// <summary>A diagonal river is as wide across as a straight one of the same width — never thinner.</summary>
    /// <remarks>
    /// Width is measured <b>across the course</b>: a west–east river's water per column, against a
    /// diagonal's water per line of <c>x ± y</c> times √2 (two tiles on one such line are √2 apart).
    /// ⚠️ Not total water — a 45° river crosses a 120 × 80 valley along about 113 tiles to a straight
    /// one's 120, so it holds less water at the same width (the first pose of this guard asked that,
    /// and read 0.90×).
    /// </remarks>
    [Theory]
    [InlineData("nwse")]
    [InlineData("swne")]
    public void ADiagonalRiverIsAsWideAcrossAsAStraightOne(string course)
    {
        double straight = 0;
        double diagonal = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            straight += WidthAcross(MapGenerator.Generate(Shipped with { RiverCourse = "we" }, seed), "we");
            diagonal += WidthAcross(MapGenerator.Generate(Shipped with { RiverCourse = course }, seed), course);
        }

        double ratio = diagonal / straight;
        output.WriteLine($"{course}: {diagonal / 20:F2} tiles across against {straight / 20:F2} west to east — {ratio:F2}×");
        Assert.InRange(ratio, 0.85, 1.2);
    }

    /// <summary>The river's mean width across its course, over the lines across it that hold water.</summary>
    private static double WidthAcross(GeneratedMap map, string course)
    {
        var water = new Dictionary<int, int>();
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                if (IsWater(map, x, y))
                {
                    int line = course switch { "we" => x, "nwse" => x + y, _ => x - y };
                    water[line] = water.GetValueOrDefault(line) + 1;
                }
            }
        }

        double perLine = water.Values.Average();
        return course == "we" ? perLine : perLine * Math.Sqrt(2);
    }

    // ---- §8.5 The founders never settle on a bank, whichever way the river runs ---------------

    [Theory]
    [InlineData("ns")]
    [InlineData("nwse")]
    [InlineData("swne")]
    public void TheFoundersSettleOnDryGroundWhicheverWayTheRiverRuns(string course)
    {
        SimConfig config = Shipped with { RiverCourse = course };
        int dry = config.StartingResidentialRadius;

        for (ulong seed = 1; seed <= 50; seed++)
        {
            GeneratedMap map = MapGenerator.Generate(config, seed);
            GridPos f = map.FoundingSite;
            for (int dy = -dry; dy <= dry; dy++)
            {
                for (int dx = -dry; dx <= dry; dx++)
                {
                    var at = new GridPos(f.X + dx, f.Y + dy);
                    if (Math.Abs(dx) + Math.Abs(dy) <= dry && map.Contains(at) && map.TerrainAt(at) == Terrain.Water)
                    {
                        Assert.Fail($"{course}, seed {seed}: water inside the starter zone at {at} — the founders settled on the bank.");
                    }
                }
            }
        }
    }

    // ---- §8.6 "Any" takes no draw --------------------------------------------------------------

    /// <summary>
    /// Under <c>any</c> a valley is the valley of the direction its seed chose, to the tile — so
    /// <c>any</c> spends no draw — and over a sample every direction is chosen.
    /// </summary>
    [Fact]
    public void AnyIsTheDirectionTheSeedChoseAndTakesNoDraw()
    {
        SimConfig any = Shipped with { RiverCourse = "any" };
        var chosen = new HashSet<string>();

        for (ulong seed = 1; seed <= 40; seed++)
        {
            string course = MapGenerator.CourseOf(any, seed);
            chosen.Add(course);

            SimConfig named = Shipped with { RiverCourse = course };
            Assert.Equal(
                StateHash.MixMap(0UL, MapGenerator.Generate(named, seed)),
                StateHash.MixMap(0UL, MapGenerator.Generate(any, seed)));
        }

        Assert.Equal(Courses.Order(), chosen.Order());
    }

    // ---- §8.7 The share code ------------------------------------------------------------------

    [Fact]
    public void TheShareCodeCarriesEveryRowInOrder()
    {
        string code = NewGame.ShareCode(Shipped, NewGame.Defaults(Shipped, "Mossy  Lantern"));
        Assert.Equal("mossy lantern#river=3,flow=we,woods=35,stone=moderate,iron=moderate", code);
    }

    /// <summary>Every row at both of its ends, written out and read back, is the same settings.</summary>
    [Fact]
    public void AShareCodeReadsBackToTheSameSettings()
    {
        SimConfig config = Shipped;
        NewGameSettings start = NewGame.Defaults(config, "oak");

        foreach (NewGameRow row in config.NewGameOptions)
        {
            foreach (string end in Ends(row))
            {
                var values = new Dictionary<string, string>(start.Values) { [row.Id] = end };
                NewGameSettings set = start with { Values = values };
                string code = NewGame.ShareCode(config, set);

                Assert.True(NewGame.TryRead(config, code, NewGame.Defaults(config, "elm"), out NewGameSettings read, out string? refusal), refusal);
                Assert.Equal(code, NewGame.ShareCode(config, read));
            }
        }
    }

    /// <summary>A bare seed keeps the rows as they are; only a code sets them.</summary>
    [Fact]
    public void ABareSeedLeavesTheRowsAlone()
    {
        SimConfig config = Shipped;
        var values = new Dictionary<string, string>(NewGame.Defaults(config, "oak").Values) { ["woods"] = "45" };
        NewGameSettings current = new("oak", null, values);

        Assert.True(NewGame.TryRead(config, "  Heron Ford ", current, out NewGameSettings read, out _));
        Assert.Equal("Heron Ford", read.SeedText);
        Assert.Equal("45", read.Values["woods"]);
    }

    [Theory]
    [InlineData("oak#woods=105", "goes from 0 to 100")]
    [InlineData("oak#woods=37", "in steps of 5")]
    [InlineData("oak#hills=3", "no setting called \"hills\"")]
    [InlineData("oak#woods", "id=value")]
    [InlineData("oak#stone=heaps", "one of sparse, moderate, rich")]
    [InlineData("#woods=40", "Type a seed")]
    public void ACodeTheRulesRefuseChangesNothingAndSaysWhy(string code, string sentence)
    {
        SimConfig config = Shipped;
        NewGameSettings current = NewGame.Defaults(config, "elm");

        Assert.False(NewGame.TryRead(config, code, current, out NewGameSettings read, out string? refusal));
        Assert.Same(current, read);
        Assert.Contains(sentence, refusal, StringComparison.Ordinal);
    }

    // ---- §8.8 Every row's ends found a valley --------------------------------------------------

    /// <summary>Each row at each end — or each level — loads through the config's own validation.</summary>
    [Fact]
    public void EveryRowsEndsFoundAValley()
    {
        SimConfig config = Shipped;
        NewGameSettings start = NewGame.Defaults(config, "oak");

        foreach (NewGameRow row in config.NewGameOptions)
        {
            foreach (string end in Ends(row))
            {
                var values = new Dictionary<string, string>(start.Values) { [row.Id] = end };
                SimConfig founded = NewGame.Apply(config, start with { Values = values });
                Assert.NotNull(SimWorld.Create(founded).Map);
            }
        }
    }

    /// <summary>A row sets what it says: the founded config carries each end's values.</summary>
    [Fact]
    public void ARowSetsItsKeys()
    {
        SimConfig config = Shipped;
        NewGameSettings start = NewGame.Defaults(config, "oak");

        SimConfig set = NewGame.Apply(config, start with
        {
            Values = new Dictionary<string, string>(start.Values)
            {
                ["river"] = "5", ["flow"] = "nwse", ["woods"] = "45", ["stone"] = "rich", ["iron"] = "sparse",
            },
        });

        Assert.Equal(5, set.RiverWidthTiles);
        Assert.Equal("nwse", set.RiverCourse);
        Assert.Equal(45, set.ForestCoveragePercent);
        Assert.Equal(12, set.ExtraStoneSeams);
        Assert.Equal(0, set.ExtraIronSeams);
        Assert.Equal(SeedText.ToSeed("oak"), set.Seed);
    }

    /// <summary>
    /// A <c>scale</c> row — no shipped row is one since the seams' scatter left the screen (D481), so a
    /// modder's is posed — sets every key to the file's value times the percentage.
    /// </summary>
    [Fact]
    public void AScaleRowScalesEachOfItsKeys()
    {
        SimConfig config = Shipped with
        {
            NewGameOptions = new[]
            {
                new NewGameRow
                {
                    Id = "scatter", Label = "Seam scatter", Kind = NewGame.ScaleKind,
                    Keys = new[] { "seam_angle_scatter_percent", "seam_size_scatter_percent" },
                    Min = 0, Max = 100, Step = 10, Unit = "%",
                },
            },
        };
        NewGameSettings start = NewGame.Defaults(config, "oak");
        Assert.Equal("100", start.Values["scatter"]);

        SimConfig set = NewGame.Apply(config, start with { Values = new Dictionary<string, string> { ["scatter"] = "50" } });

        Assert.Equal((config.SeamAngleScatterPercent + 1) / 2, set.SeamAngleScatterPercent);
        Assert.Equal((config.SeamSizeScatterPercent + 1) / 2, set.SeamSizeScatterPercent);
        Assert.Equal(config.SeamReachScatterPercent, set.SeamReachScatterPercent);
    }

    /// <summary>
    /// River width shows its ends as words, not a number (D481, Joe: <em>"narrow ◂——▸ wide"</em>) —
    /// and the share code still carries the number, so a code names the same valley.
    /// </summary>
    [Fact]
    public void RiverWidthIsNamedAtItsEndsAndCodedAsANumber()
    {
        NewGameRow river = Shipped.NewGameOptions.Single(r => r.Id == "river");
        Assert.True(river.HasEndLabels);
        Assert.Equal(("narrow", "wide"), (river.MinLabel, river.MaxLabel));

        NewGameSettings start = NewGame.Defaults(Shipped, "oak");
        string code = NewGame.ShareCode(Shipped, start with { Values = new Dictionary<string, string>(start.Values) { ["river"] = "5" } });
        Assert.Contains("river=5", code, StringComparison.Ordinal);
    }

    /// <summary>One end word without the other is a row the screen could not draw honestly.</summary>
    [Fact]
    public void AnEndLabelWithoutItsPairIsRefused()
    {
        var rows = new[]
        {
            new NewGameRow
            {
                Id = "river", Label = "River width", Kind = NewGame.RangeKind,
                Keys = new[] { "river_width_tiles" }, Min = 0, Max = 6, MinLabel = "narrow",
            },
        };

        var thrown = Assert.Throws<SimConfigException>(() => NewGame.ValidateRows(rows));
        Assert.Contains("min_label and max_label", thrown.Message, StringComparison.Ordinal);
    }

    private static IEnumerable<string> Ends(NewGameRow row) =>
        row.Kind == NewGame.LevelsKind
            ? row.Levels.Select(l => l.Value)
            : new[] { row.Min.ToString(System.Globalization.CultureInfo.InvariantCulture), row.Max.ToString(System.Globalization.CultureInfo.InvariantCulture) };

    // ---- The rows themselves ------------------------------------------------------------------

    /// <summary>
    /// ⛔ A row whose key the config does not have would be a slider that moves nothing — the
    /// serialiser ignores a key it does not know — so the config refuses it.
    /// </summary>
    [Fact]
    public void ARowForAKeyTheConfigDoesNotHaveIsRefused()
    {
        var rows = new[]
        {
            new NewGameRow { Id = "hills", Label = "Hills", Kind = NewGame.RangeKind, Keys = new[] { "hill_height" }, Min = 0, Max = 3 },
        };

        SimConfigException refused = Assert.Throws<SimConfigException>(() => (Shipped with { NewGameOptions = rows }).Validate());
        Assert.Contains("not a config key", refused.Message, StringComparison.Ordinal);
    }

    // ---- The stats under the preview ----------------------------------------------------------

    /// <summary>
    /// The stats are read off the valley: on the shipped seed 12345, founded, they agree with the counts
    /// `MapGenerationTests.EachSeedsTerrainIsWhatItWas` pins independently (water 410, forest 2,523,
    /// stone 134, iron 40), and more forest cover reads as more woods.
    /// </summary>
    [Fact]
    public void TheStatsAreReadOffTheValley()
    {
        // The fingerprint's own pose: the shipped config, founded (`EachSeedsTerrainIsWhatItWas`).
        SimConfig config = ShippedConfig.Established() with { Seed = 12345UL };
        GeneratedMap map = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink()).World.Map;
        ValleySummary s = NewGame.Summarise(config, map);
        int land = (map.Width * map.Height) - 410;
        output.WriteLine($"fixture map {map.Width} x {map.Height}: wooded {s.WoodedPercent}% of {land} land tiles");

        Assert.Equal(((2523 * 100) + (land / 2)) / land, s.WoodedPercent);
        Assert.Equal(134, s.StoneTiles);
        Assert.Equal(40, s.IronTiles);
        Assert.Equal(MapGenerator.SeamsOf(config, 12345UL, Terrain.Rock).Count, s.StoneSeams);
        Assert.Equal("we", s.Course);

        SimConfig wooded = config with { ForestCoveragePercent = 50 };
        Assert.True(NewGame.Summarise(wooded, MapGenerator.Generate(wooded, 12345UL)).WoodedPercent
            > NewGame.Summarise(config, MapGenerator.Generate(config, 12345UL)).WoodedPercent);

        SimConfig any = config with { RiverCourse = "any" };
        Assert.Equal(MapGenerator.CourseOf(any, 12345UL), NewGame.Summarise(any, MapGenerator.Generate(any, 12345UL)).Course);
    }

    // ---- The village name ---------------------------------------------------------------------

    [Fact]
    public void ATypedNameIsTheVillagesAndABlankOneIsTheSeeds()
    {
        SimConfig config = Shipped;
        NewGameSettings start = NewGame.Defaults(config, "oak");

        SimWorld named = SimWorld.Create(NewGame.Apply(config, start with { VillageName = "  Little Hythe " }));
        SimWorld blank = SimWorld.Create(NewGame.Apply(config, start with { VillageName = "   " }));
        SimWorld unnamed = SimWorld.Create(NewGame.Apply(config, start));

        Assert.Equal("Little Hythe", named.Name);
        Assert.Equal(unnamed.Name, blank.Name);
        Assert.Equal(StateHash.Compute(unnamed), StateHash.Compute(named));
    }

    [Fact]
    public void ANameTooLongIsRefused() =>
        Assert.Throws<SimConfigException>(() =>
            NewGame.Apply(Shipped, NewGame.Defaults(Shipped, "oak") with { VillageName = new string('a', 25) }));

    [Fact]
    public void TheRowsSurviveAJsonRoundTrip()
    {
        // The shipped rows travel inside the config the screen serialises; a level's values must
        // come back as the same json.
        string json = JsonSerializer.Serialize(Shipped.NewGameOptions);
        Assert.Contains("\"river_course\":\"nwse\"", json, StringComparison.Ordinal);
    }

    // ---- settings-persistence.md §8.9 The screen opens on the valley last founded --------------

    /// <summary>
    /// ⭐ A remembered row is kept only through the screen's own door (<see cref="NewGame.IsAllowed"/>):
    /// an allowed value is kept, a refused one opens at the config's default and says why, a row this
    /// build does not have is dropped without a word — and the seed is always the fresh roll's.
    /// </summary>
    [Fact]
    public void TheScreenOpensOnTheRowsLastFoundedThroughItsOwnDoor()
    {
        SimConfig config = Shipped;
        NewGameSettings fresh = NewGame.Defaults(config, "oak");
        var remembered = new Dictionary<string, string>
        {
            ["woods"] = "20",
            ["stone"] = "rich",
            ["river"] = "99",
            ["hills"] = "tall",
        };

        var problems = new List<PlayerSettingsProblem>();
        NewGameSettings opened = NewGame.Remembered(config, fresh, remembered, problems);

        Assert.Equal("oak", opened.SeedText);
        Assert.Equal("20", opened.Values["woods"]);
        Assert.Equal("rich", opened.Values["stone"]);
        Assert.Equal(fresh.Values["river"], opened.Values["river"]);
        Assert.False(opened.Values.ContainsKey("hills"));
        Assert.Contains("river=99", Assert.Single(problems).Sentence, StringComparison.Ordinal);
    }

    /// <summary>A remembered screen names the same valley as the same values chosen by hand.</summary>
    [Fact]
    public void ARememberedScreenSharesTheValleyItShows()
    {
        SimConfig config = Shipped;
        NewGameSettings fresh = NewGame.Defaults(config, "oak");
        NewGameSettings opened = NewGame.Remembered(
            config, fresh, new Dictionary<string, string> { ["woods"] = "20", ["flow"] = "ns" }, new List<PlayerSettingsProblem>());

        Assert.True(NewGame.TryRead(config, "oak#woods=20,flow=ns", fresh, out NewGameSettings typed, out _));
        Assert.Equal(NewGame.ShareCode(config, typed), NewGame.ShareCode(config, opened));
    }
}
