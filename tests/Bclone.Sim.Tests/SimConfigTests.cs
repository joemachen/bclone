using Bclone.Sim.Config;
using Xunit;

namespace Bclone.Sim.Tests;

public sealed class SimConfigTests
{
    [Fact]
    public void ParsesSnakeCaseJson()
    {
        const string Json = """
        {
          "seed": 999,
          "ticks_per_day": 8,
          "target_ticks_per_second": 20.0,
          "max_ticks_per_frame": 100
        }
        """;

        SimConfig config = SimConfigLoader.Parse(Json);

        Assert.Equal(999UL, config.Seed);
        Assert.Equal(8, config.TicksPerDay);
        Assert.Equal(20.0, config.TargetTicksPerSecond);
        Assert.Equal(100, config.MaxTicksPerFrame);
    }

    [Fact]
    public void AllowsCommentsAndTrailingCommas()
    {
        // Content files are meant to be edited by modders (DESIGN.md §3), so they
        // need to be able to carry explanation.
        const string Json = """
        {
          // the seed for this run
          "seed": 5,
          "ticks_per_day": 4, /* block comments too */
        }
        """;

        SimConfig config = SimConfigLoader.Parse(Json);

        Assert.Equal(5UL, config.Seed);
        Assert.Equal(4, config.TicksPerDay);
    }

    [Fact]
    public void OmittedValues_FallBackToDefaults()
    {
        SimConfig config = SimConfigLoader.Parse("""{ "seed": 1 }""");

        Assert.Equal(1UL, config.Seed);
        Assert.Equal(4, config.TicksPerDay);
        Assert.Equal(250, config.MaxTicksPerFrame);
    }

    [Fact]
    public void MalformedJson_ThrowsWithSourceName()
    {
        var ex = Assert.Throws<SimConfigException>(
            () => SimConfigLoader.Parse("{ not json", "bad-config.json"));

        Assert.Contains("bad-config.json", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "ticks_per_day": 0 }""")]
    [InlineData("""{ "ticks_per_day": -1 }""")]
    [InlineData("""{ "target_ticks_per_second": 0 }""")]
    [InlineData("""{ "target_ticks_per_second": -5 }""")]
    [InlineData("""{ "max_ticks_per_frame": 0 }""")]
    public void OutOfRangeValues_FailLoudly(string json)
    {
        // A nonsense config should stop the run immediately, not cause a baffling
        // symptom a thousand ticks later.
        Assert.Throws<SimConfigException>(() => SimConfigLoader.Parse(json));
    }

    [Fact]
    public void MissingFile_ThrowsWithPath()
    {
        string path = Path.Combine(Path.GetTempPath(), "bclone-definitely-missing.json");

        var ex = Assert.Throws<SimConfigException>(() => SimConfigLoader.LoadFromFile(path));

        Assert.Contains("bclone-definitely-missing.json", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShippedConfigFile_IsValid()
    {
        // Guards against data/sim.config.json drifting out of sync with SimConfig —
        // the kind of break that would otherwise only show up at launch.
        string path = Path.Combine(RepoRoot(), "data", "sim.config.json");
        Assert.True(File.Exists(path), $"Expected shipped config at {path}");

        SimConfig config = SimConfigLoader.LoadFromFile(path);

        Assert.Equal(12345UL, config.Seed);
        config.Validate();
    }

    /// <summary>Walk up from the test binary until the repo marker is found.</summary>
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DESIGN.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root (no DESIGN.md found walking up from the test binary).");
    }
}
