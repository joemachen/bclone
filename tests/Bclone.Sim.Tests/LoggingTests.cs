using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Xunit;

namespace Bclone.Sim.Tests;

public sealed class LoggingTests
{
    private static SimWorld NewWorld(InMemoryLogSink sink) =>
        SimWorld.Create(new SimConfig { Seed = 1UL }, sink);

    [Fact]
    public void EntriesAreStampedWithTheCurrentTick()
    {
        // METHODOLOGY.md §4: every line must be tied back to an exact sim state.
        var sink = new InMemoryLogSink();
        var world = NewWorld(sink);
        var loop = new SimLoop(world);

        loop.Step(42);
        world.Log(LogLevel.Info, "test", "after 42 ticks");

        LogEntry entry = sink.Entries[^1];
        Assert.Equal(42UL, entry.Tick);
        Assert.Equal("test", entry.Subsystem);
    }

    [Fact]
    public void EntriesBelowMinimumLevel_AreDiscarded()
    {
        var sink = new InMemoryLogSink(LogLevel.Warn);
        var world = NewWorld(sink);

        world.Log(LogLevel.Debug, "test", "noise");
        world.Log(LogLevel.Info, "test", "more noise");
        world.Log(LogLevel.Warn, "test", "signal");

        LogEntry only = Assert.Single(sink.Entries);
        Assert.Equal("signal", only.Message);
    }

    [Fact]
    public void EntriesArePreservedInOrder()
    {
        var sink = new InMemoryLogSink();
        var world = NewWorld(sink);
        sink.Clear();

        for (int i = 0; i < 100; i++)
        {
            world.Log(LogLevel.Info, "test", $"entry {i}");
        }

        Assert.Equal(100, sink.Entries.Count);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal($"entry {i}", sink.Entries[i].Message);
        }
    }

    [Fact]
    public void EntriesHaveValueEquality()
    {
        // The determinism test compares whole log sequences, which only works if
        // entries compare by value.
        Assert.Equal(
            new LogEntry(1UL, LogLevel.Info, "sim", "hello"),
            new LogEntry(1UL, LogLevel.Info, "sim", "hello"));

        Assert.NotEqual(
            new LogEntry(1UL, LogLevel.Info, "sim", "hello"),
            new LogEntry(2UL, LogLevel.Info, "sim", "hello"));
    }

    [Fact]
    public void RenderedEntry_ContainsTickLevelAndSubsystem()
    {
        string rendered = new LogEntry(7UL, LogLevel.Warn, "driver", "backlog dropped").ToString();

        Assert.Contains("7", rendered, StringComparison.Ordinal);
        Assert.Contains("WARN", rendered, StringComparison.Ordinal);
        Assert.Contains("driver", rendered, StringComparison.Ordinal);
        Assert.Contains("backlog dropped", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void NullLoggerAcceptsEverythingWithoutThrowing()
    {
        var world = SimWorld.Create(new SimConfig());
        world.Log(LogLevel.Error, "test", "goes nowhere");
    }

    [Fact]
    public void WorldCreation_IsAnnounced()
    {
        var sink = new InMemoryLogSink();
        NewWorld(sink);

        Assert.Contains(sink.Entries, e => e.Level == LogLevel.Info && e.Tick == 0UL);
    }

    [Fact]
    public void FileSink_WritesEntriesToDisk()
    {
        string path = Path.Combine(Path.GetTempPath(), $"bclone-log-test-{Guid.NewGuid():N}.log");

        try
        {
            using (var sink = new FileLogSink(path, LogLevel.Debug, alsoConsole: false))
            {
                sink.Log(3UL, LogLevel.Info, "sim", "written to disk");
            }

            Assert.Contains("written to disk", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
