using Kith.Sim.Config;
using Kith.Sim.Logging;
using Xunit;

namespace Kith.Sim.Tests;

/// <summary>
/// The player's settings file (D504, `specs/settings-persistence.md §8`): what a missing, a broken and a
/// half-right file each read as, and that a save is whole or nothing.
/// </summary>
public sealed class PlayerSettingsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"Kith-settings-{Guid.NewGuid():N}");

    private string FilePath => Path.Combine(_folder, "settings.json");

    public PlayerSettingsTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private PlayerSettings LoadText(string text, List<PlayerSettingsProblem> problems)
    {
        File.WriteAllText(FilePath, text);
        return PlayerSettingsFile.Load(FilePath, problems);
    }

    private static PlayerSettings Everything() => new()
    {
        UiScalePercent = 95,
        Routes = "all",
        RosterSort = "age",
        ProfessionsSort = "max",
        BuildingsSort = "condition",
        Toggles = new Dictionary<string, bool> { ["grid"] = true, ["snap"] = true, ["animals"] = false, ["paths"] = true },
        Windows = new Dictionary<string, WindowPrefs>
        {
            ["Professions"] = new(true, false, new WindowPlace(PlayerSettings.LeftSide, 420, 96)),
            ["What's here"] = new(false, null, new WindowPlace(PlayerSettings.RightSide, 12, 300)),
            ["Stock limits"] = new(true, true, null),
        },
        NewGameRows = new Dictionary<string, string> { ["woods"] = "20", ["stone"] = "rich" },
    };

    // ---- §8.1 ---------------------------------------------------------------------------------

    [Fact]
    public void NoFileIsEveryDefaultAndSaysNothing()
    {
        var problems = new List<PlayerSettingsProblem>();
        PlayerSettings read = PlayerSettingsFile.Load(FilePath, problems);

        Assert.Equal(new PlayerSettings().ToJson(), read.ToJson());
        Assert.Empty(problems);
    }

    // ---- §8.2 ---------------------------------------------------------------------------------

    [Fact]
    public void EverySettingSurvivesASaveAndALoad()
    {
        PlayerSettings saved = Everything();
        PlayerSettingsFile.Save(FilePath, saved);

        var problems = new List<PlayerSettingsProblem>();
        PlayerSettings read = PlayerSettingsFile.Load(FilePath, problems);

        Assert.Empty(problems);
        Assert.Equal(saved.ToJson(), read.ToJson());
        Assert.Equal(95, read.UiScalePercent);
        Assert.Equal("all", read.Routes);
        Assert.Equal("age", read.RosterSort);
        Assert.Equal("max", read.ProfessionsSort);
        Assert.Equal("condition", read.BuildingsSort);
        Assert.False(read.Toggles["animals"]);
        Assert.Equal(new WindowPlace(PlayerSettings.RightSide, 12, 300), read.Windows["What's here"].Place);
        Assert.Null(read.Windows["What's here"].Open);
        Assert.Equal("rich", read.NewGameRows["stone"]);
    }

    /// <summary>The file is for reading by hand too: keys sorted, an apostrophe written as one.</summary>
    [Fact]
    public void TheFileReadsCleanlyByHand()
    {
        string json = Everything().ToJson();

        Assert.Contains("\"What's here\"", json, StringComparison.Ordinal);
        Assert.True(json.IndexOf("\"animals\"", StringComparison.Ordinal) < json.IndexOf("\"grid\"", StringComparison.Ordinal));
        Assert.True(json.IndexOf("\"Professions\"", StringComparison.Ordinal) < json.IndexOf("\"Stock limits\"", StringComparison.Ordinal));
    }

    // ---- §8.3 ---------------------------------------------------------------------------------

    [Theory]
    [InlineData("{ \"ui_scale_percent\": 90,")]
    [InlineData("[1, 2, 3]")]
    [InlineData("not json at all")]
    [InlineData("{ \"routes\": \"all\", \"routes\": \"off\" }")]
    public void ABrokenFileIsEveryDefaultAndIsKept(string text)
    {
        var problems = new List<PlayerSettingsProblem>();
        PlayerSettings read = LoadText(text, problems);

        Assert.Equal(new PlayerSettings().ToJson(), read.ToJson());
        PlayerSettingsProblem problem = Assert.Single(problems);
        Assert.Equal(LogLevel.Error, problem.Level);
        Assert.Contains(FilePath, problem.Sentence, StringComparison.Ordinal);
        Assert.Equal(text, File.ReadAllText(FilePath + PlayerSettingsFile.BrokenSuffix));
    }

    // ---- §8.4 ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(30, 75)]
    [InlineData(200, 150)]
    [InlineData(77, 75)]
    [InlineData(78, 80)]
    [InlineData(148, 150)]
    public void AScaleOffTheDialIsBroughtOntoIt(int written, int used)
    {
        var problems = new List<PlayerSettingsProblem>();
        PlayerSettings read = LoadText($"{{ \"ui_scale_percent\": {written} }}", problems);

        Assert.Equal(used, read.UiScalePercent);
        Assert.Equal(LogLevel.Warn, Assert.Single(problems).Level);
    }

    [Fact]
    public void AScaleOnTheDialIsReadAsWritten()
    {
        var problems = new List<PlayerSettingsProblem>();
        Assert.Equal(PlayerSettings.LargestUiScalePercent, LoadText("{ \"ui_scale_percent\": 150 }", problems).UiScalePercent);
        Assert.Empty(problems);
    }

    // ---- §8.4a: version 2 — what was 75 % is 100 % (hud.md §11, A213; Joe's call 6, A215) ---------

    /// <summary>The dial runs 75–150, default 100, and its 100 draws what 75 drew before (×0.75, one constant).</summary>
    [Fact]
    public void TheDialRunsSeventyFiveToOneFiftyAndItsHundredDrawsWhatSeventyFiveDid()
    {
        Assert.Equal(2, PlayerSettings.CurrentVersion);
        Assert.Equal(75, PlayerSettings.SmallestUiScalePercent);
        Assert.Equal(150, PlayerSettings.LargestUiScalePercent);
        Assert.Equal(100, PlayerSettings.DefaultUiScalePercent);
        Assert.Equal(5, PlayerSettings.UiScaleStep);
        Assert.Equal(75, PlayerSettings.DrawnPercentAtTheDialsHundred);
    }

    [Theory]
    [InlineData(75, 100)]
    [InlineData(55, 75)]
    [InlineData(115, 150)]
    [InlineData(80, 105)]
    [InlineData(90, 120)]
    [InlineData(100, 135)]
    public void AVersionOneScaleIsReadAtFourThirdsOnTheNewDial(int written, int read)
    {
        var problems = new List<PlayerSettingsProblem>();
        PlayerSettings settings = LoadText($"{{ \"version\": 1, \"ui_scale_percent\": {written} }}", problems);

        Assert.Equal(read, settings.UiScalePercent);
        Assert.Equal(1, settings.MigratedFrom);
        PlayerSettingsProblem said = Assert.Single(problems);
        Assert.Equal(LogLevel.Info, said.Level);
        Assert.Contains($"{written} %", said.Sentence, StringComparison.Ordinal);
        Assert.Contains($"{read} %", said.Sentence, StringComparison.Ordinal);
    }

    [Fact]
    public void AVersionTwoHundredIsReadAsAHundred()
    {
        var problems = new List<PlayerSettingsProblem>();
        PlayerSettings settings = LoadText("{ \"version\": 2, \"ui_scale_percent\": 100 }", problems);

        Assert.Equal(100, settings.UiScalePercent);
        Assert.Null(settings.MigratedFrom);
        Assert.Empty(problems);
    }

    /// <summary>
    /// ⭐ Call 6: a version-1 file's windows forget whether they were shown — once — so the new defaults open (the roster
    /// closed, the log and Professions open), and keep their folds and places.
    /// </summary>
    [Fact]
    public void AVersionOneFilesWindowsForgetShownAndKeepTheirFoldsAndPlaces()
    {
        var problems = new List<PlayerSettingsProblem>();
        PlayerSettings settings = LoadText(
            """
            { "version": 1, "windows": {
                "The village": { "shown": true, "open": true, "side": "left", "x": 12, "y": 80 },
                "Stock limits": { "shown": true },
                "Professions": { "shown": false, "open": false } } }
            """,
            problems);

        Assert.Equal(new WindowPrefs(null, true, new WindowPlace(PlayerSettings.LeftSide, 12, 80)), settings.Windows["The village"]);
        Assert.Equal(new WindowPrefs(null, null, null), settings.Windows["Stock limits"]);
        Assert.Equal(new WindowPrefs(null, false, null), settings.Windows["Professions"]);
        Assert.Equal(1, settings.MigratedFrom);
        Assert.Contains("windows", Assert.Single(problems).Sentence, StringComparison.Ordinal);
    }

    /// <summary>Written back it is version 2, and read again nothing moves a second time — the ×4/3 is applied once.</summary>
    [Fact]
    public void AVersionOneFileWrittenBackIsVersionTwoAndIsNotMovedAgain()
    {
        var problems = new List<PlayerSettingsProblem>();
        PlayerSettings once = LoadText("""{ "version": 1, "ui_scale_percent": 75, "windows": { "Village log": { "shown": true, "open": false } } }""", problems);
        PlayerSettingsFile.Save(FilePath, once);
        Assert.Contains("\"version\": 2", File.ReadAllText(FilePath), StringComparison.Ordinal);

        problems.Clear();
        PlayerSettings twice = PlayerSettingsFile.Load(FilePath, problems);
        Assert.Empty(problems);
        Assert.Null(twice.MigratedFrom);
        Assert.Equal(100, twice.UiScalePercent);
        Assert.Equal(once.ToJson(), twice.ToJson());
    }

    /// <summary>A version-1 file with nothing to move says nothing, and a file with no version is this build's.</summary>
    [Fact]
    public void AVersionOneFileWithNothingToMoveSaysNothingAndNoVersionIsThisBuilds()
    {
        var problems = new List<PlayerSettingsProblem>();
        Assert.Equal("off", LoadText("{ \"version\": 1, \"routes\": \"off\" }", problems).Routes);
        Assert.Empty(problems);

        PlayerSettings unversioned = LoadText("{ \"ui_scale_percent\": 90 }", problems);
        Assert.Equal(90, unversioned.UiScalePercent);
        Assert.Null(unversioned.MigratedFrom);
        Assert.Empty(problems);
    }

    // ---- §8.5 ---------------------------------------------------------------------------------

    [Fact]
    public void RoutesThatAreNotALevelAreTheDefault()
    {
        var problems = new List<PlayerSettingsProblem>();
        PlayerSettings read = LoadText("{ \"routes\": \"everybody\", \"ui_scale_percent\": 90 }", problems);

        Assert.Null(read.Routes);
        Assert.Equal(90, read.UiScalePercent);
        Assert.Contains("routes", Assert.Single(problems).Sentence, StringComparison.Ordinal);
    }

    /// <summary>The village panel's order (D609): a word the buttons do not have is the default, said once.</summary>
    [Fact]
    public void ARosterSortThatIsNotAButtonIsTheDefault()
    {
        var problems = new List<PlayerSettingsProblem>();
        PlayerSettings read = LoadText("{ \"roster_sort\": \"height\", \"routes\": \"off\" }", problems);

        Assert.Null(read.RosterSort);
        Assert.Equal("off", read.Routes);
        Assert.Contains("roster_sort", Assert.Single(problems).Sentence, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("age")]
    [InlineData("trade")]
    public void EveryRosterSortIsReadAsWritten(string sort)
    {
        var problems = new List<PlayerSettingsProblem>();
        Assert.Equal(sort, LoadText($"{{ \"roster_sort\": \"{sort}\" }}", problems).RosterSort);
        Assert.Empty(problems);
    }

    /// <summary>Professions' order (Joe's play note 6, D896): a word with no heading is none, said once.</summary>
    [Fact]
    public void AProfessionsSortThatIsNotAHeadingIsTheDefault()
    {
        var problems = new List<PlayerSettingsProblem>();
        PlayerSettings read = LoadText("{ \"professions_sort\": \"height\", \"routes\": \"off\" }", problems);

        Assert.Null(read.ProfessionsSort);
        Assert.Equal("off", read.Routes);
        Assert.Contains("professions_sort", Assert.Single(problems).Sentence, StringComparison.Ordinal);
    }

    /// <summary>The Buildings list's order (Joe's play note 1, D900): a word with no heading is the default, said once; each heading's is read.</summary>
    [Fact]
    public void ABuildingsSortThatIsNotAHeadingIsTheDefault()
    {
        var problems = new List<PlayerSettingsProblem>();
        PlayerSettings read = LoadText("{ \"buildings_sort\": \"height\", \"routes\": \"off\" }", problems);

        Assert.Null(read.BuildingsSort);
        Assert.Equal("off", read.Routes);
        Assert.Contains("buildings_sort", Assert.Single(problems).Sentence, StringComparison.Ordinal);
        foreach (string sort in new[] { "name", "kind", "condition" })
        {
            Assert.Equal(sort, LoadText($"{{ \"buildings_sort\": \"{sort}\" }}", new List<PlayerSettingsProblem>()).BuildingsSort);
        }
    }

    [Theory]
    [InlineData("type")]
    [InlineData("assigned")]
    [InlineData("max")]
    public void EveryProfessionsSortIsReadAsWritten(string sort)
    {
        var problems = new List<PlayerSettingsProblem>();
        Assert.Equal(sort, LoadText($"{{ \"professions_sort\": \"{sort}\" }}", problems).ProfessionsSort);
        Assert.Empty(problems);
    }

    // ---- §8.6 ---------------------------------------------------------------------------------

    [Fact]
    public void AWrongTypedValueIsThatKeysDefaultAndTheRestStillCount()
    {
        var problems = new List<PlayerSettingsProblem>();
        PlayerSettings read = LoadText(
            """
            {
              // a hand edit, comments and a trailing comma allowed (D3)
              "ui_scale_percent": "big",
              "toggles": { "grid": "yes", "snap": true, },
              "windows": { "Professions": { "open": 1, "shown": false, "side": "middle", "x": 4, "y": 5 } },
              "new_game": { "woods": 20, "stone": false },
            }
            """,
            problems);

        Assert.Null(read.UiScalePercent);
        Assert.False(read.Toggles.ContainsKey("grid"));
        Assert.True(read.Toggles["snap"]);
        Assert.Equal(new WindowPrefs(false, null, null), read.Windows["Professions"]);
        Assert.Equal("20", read.NewGameRows["woods"]);
        Assert.False(read.NewGameRows.ContainsKey("stone"));
        Assert.Equal(5, problems.Count);
        Assert.All(problems, p => Assert.Equal(LogLevel.Warn, p.Level));
    }

    // ---- §8.7 ---------------------------------------------------------------------------------

    [Fact]
    public void ANewerFileIsReadForWhatThisBuildKnows()
    {
        var problems = new List<PlayerSettingsProblem>();
        PlayerSettings read = LoadText(
            "{ \"version\": 99, \"routes\": \"off\", \"weather\": \"rain\", \"windows\": { \"Tree\": { \"pinned\": true, \"open\": false } } }",
            problems);

        Assert.Equal("off", read.Routes);
        Assert.False(read.Windows["Tree"].Open);
        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, p => p.Sentence.Contains("version 99", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Sentence.Contains("weather", StringComparison.Ordinal)
            && p.Sentence.Contains("windows.Tree.pinned", StringComparison.Ordinal));
    }

    [Fact]
    public void APartialFileLeavesTheRestAtTheirDefaults()
    {
        var problems = new List<PlayerSettingsProblem>();
        PlayerSettings read = LoadText("{ \"toggles\": { \"grid\": true } }", problems);

        Assert.Empty(problems);
        Assert.Null(read.UiScalePercent);
        Assert.Null(read.Routes);
        Assert.Empty(read.Windows);
        Assert.Empty(read.NewGameRows);
        Assert.True(Assert.Single(read.Toggles).Value);
    }

    // ---- §8.8 ---------------------------------------------------------------------------------

    [Fact]
    public void ASaveReplacesTheFileWholeAndLeavesNoPartBehind()
    {
        File.WriteAllText(FilePath, "{ \"routes\": \"off\" }");

        PlayerSettingsFile.Save(FilePath, Everything());

        Assert.False(File.Exists(FilePath + PlayerSettingsFile.PartSuffix));
        Assert.Equal(Everything().ToJson(), File.ReadAllText(FilePath));
    }

    [Fact]
    public void ASaveMakesItsFolder()
    {
        string deeper = Path.Combine(_folder, "app_userdata", "bclone", "settings.json");

        PlayerSettingsFile.Save(deeper, Everything());

        Assert.True(File.Exists(deeper));
    }
}
