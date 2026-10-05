using Bclone.Sim.Config;
using Bclone.Sim.Logging;
using Xunit;

namespace Bclone.Sim.Tests;

/// <summary>
/// The player's settings file (D504, `specs/settings-persistence.md §8`): what a missing, a broken and a
/// half-right file each read as, and that a save is whole or nothing.
/// </summary>
public sealed class PlayerSettingsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"bclone-settings-{Guid.NewGuid():N}");

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
    [InlineData(30, 55)]
    [InlineData(200, 115)]
    [InlineData(77, 75)]
    [InlineData(78, 80)]
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
        Assert.Equal(PlayerSettings.LargestUiScalePercent, LoadText("{ \"ui_scale_percent\": 115 }", problems).UiScalePercent);
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
