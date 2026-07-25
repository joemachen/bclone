using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Xunit;

namespace Bclone.Sim.Tests;

/// <summary>
/// The driver is a pure function of the deltas it is handed — it never reads a
/// clock — so all of this is testable without any timing flakiness.
/// </summary>
public sealed class FixedTimestepDriverTests
{
    private static FixedTimestepDriver Build(
        double ticksPerSecond = 10.0,
        int maxTicksPerFrame = 250,
        ISimLogger? logger = null) =>
        new(ticksPerSecond, maxTicksPerFrame, logger);

    [Fact]
    public void SecondsPerTick_IsInverseOfTargetRate()
    {
        Assert.Equal(0.1, Build(10.0).SecondsPerTick, precision: 10);
    }

    [Fact]
    public void PartialDelta_YieldsNoTicks()
    {
        var driver = Build();
        Assert.Equal(0, driver.Advance(0.05));
    }

    [Fact]
    public void ExactDelta_YieldsOneTick()
    {
        var driver = Build();
        Assert.Equal(1, driver.Advance(0.1));
    }

    [Fact]
    public void LargerDelta_YieldsProportionalTicks()
    {
        var driver = Build();
        Assert.Equal(10, driver.Advance(1.0));
    }

    [Fact]
    public void Remainder_CarriesIntoTheNextCall()
    {
        // Three 0.04s frames make one 0.1s tick with 0.02s left over. Losing that
        // remainder would make the sim run slow by a few percent forever.
        var driver = Build();

        Assert.Equal(0, driver.Advance(0.04));
        Assert.Equal(0, driver.Advance(0.04));
        Assert.Equal(1, driver.Advance(0.04));
    }

    [Fact]
    public void Alpha_StaysInUnitInterval()
    {
        var driver = Build();

        for (int i = 0; i < 500; i++)
        {
            driver.Advance(1.0 / 60.0);
            double alpha = driver.Alpha;
            Assert.True(alpha >= 0.0 && alpha < 1.0, $"Alpha escaped [0,1): {alpha}");
        }
    }

    [Fact]
    public void Alpha_ReportsProgressTowardNextTick()
    {
        var driver = Build();
        driver.Advance(0.05);
        Assert.Equal(0.5, driver.Alpha, precision: 6);
    }

    [Fact]
    public void SpeedMultiplier_ScalesTickCount()
    {
        Assert.Equal(10, Build().Advance(1.0));
        Assert.Equal(40, new FixedTimestepDriver(10.0, 250) { SpeedMultiplier = 4.0 }.Advance(1.0));
    }

    [Fact]
    public void PausedDriver_YieldsNoTicks()
    {
        var driver = Build();
        driver.SpeedMultiplier = 0.0;

        Assert.True(driver.IsPaused);
        Assert.Equal(0, driver.Advance(10.0));
    }

    [Fact]
    public void NegativeSpeedMultiplier_Throws()
    {
        var driver = Build();
        Assert.Throws<ArgumentOutOfRangeException>(() => driver.SpeedMultiplier = -1.0);
    }

    // ---------------------------------------------------------------
    //  Spiral-of-death guard
    // ---------------------------------------------------------------

    [Fact]
    public void HugeDelta_ClampsToMaxTicksPerFrame()
    {
        var driver = Build(maxTicksPerFrame: 25);

        // 60 seconds of backlog at 10 ticks/s would be 600 ticks.
        Assert.Equal(25, driver.Advance(60.0));
    }

    [Fact]
    public void ClampedBacklog_IsDroppedRatherThanCarried()
    {
        // Carrying the backlog is what turns one slow frame into a permanent
        // catch-up spiral: every frame runs max ticks, taking longer, adding more.
        var driver = Build(maxTicksPerFrame: 25);

        driver.Advance(60.0);

        Assert.Equal(0, driver.Advance(0.0));
        Assert.True(driver.DroppedTickCount > 0);
    }

    [Fact]
    public void ClampedBacklog_LogsAWarning()
    {
        // METHODOLOGY.md §4 — a dropped backlog is exactly the kind of thing that
        // must never happen silently.
        var sink = new InMemoryLogSink();
        var driver = Build(maxTicksPerFrame: 25, logger: sink);

        driver.Advance(60.0, simTick: 42UL);

        LogEntry warning = Assert.Single(sink.Entries, e => e.Level == LogLevel.Warn);
        Assert.Equal(42UL, warning.Tick);
        Assert.Equal("driver", warning.Subsystem);
    }

    [Fact]
    public void ExactlyMaxTicks_WithNoRemainder_DoesNotWarn()
    {
        // Hitting the cap exactly is not a backlog — only leftover time is.
        var sink = new InMemoryLogSink();
        var driver = Build(maxTicksPerFrame: 25, logger: sink);

        Assert.Equal(25, driver.Advance(2.5));
        Assert.DoesNotContain(sink.Entries, e => e.Level == LogLevel.Warn);
    }

    // ---------------------------------------------------------------
    //  Bad input
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteDelta_IsRejectedAndLogged(double delta)
    {
        // A NaN would poison the accumulator permanently and silently stop the
        // sim advancing — a bug that would be miserable to track down later.
        var sink = new InMemoryLogSink();
        var driver = Build(logger: sink);

        Assert.Equal(0, driver.Advance(delta));
        Assert.Contains(sink.Entries, e => e.Level == LogLevel.Error);

        Assert.Equal(1, driver.Advance(0.1));
    }

    [Fact]
    public void NegativeDelta_IsRejectedAndLogged()
    {
        var sink = new InMemoryLogSink();
        var driver = Build(logger: sink);

        Assert.Equal(0, driver.Advance(-1.0));
        Assert.Contains(sink.Entries, e => e.Level == LogLevel.Warn);
    }

    [Fact]
    public void InvalidConstructorArguments_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedTimestepDriver(0.0, 250));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedTimestepDriver(-1.0, 250));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedTimestepDriver(double.NaN, 250));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedTimestepDriver(10.0, 0));
    }

    [Fact]
    public void ResetAccumulator_DiscardsPendingTime()
    {
        var driver = Build();
        driver.Advance(0.09);
        driver.ResetAccumulator();

        Assert.Equal(0.0, driver.Alpha);
        Assert.Equal(0, driver.Advance(0.09));
    }

    [Fact]
    public void DriverBuiltFromConfig_UsesConfiguredValues()
    {
        var config = new SimConfig { TargetTicksPerSecond = 20.0, MaxTicksPerFrame = 7 };
        var driver = new FixedTimestepDriver(config);

        Assert.Equal(0.05, driver.SecondsPerTick, precision: 10);
        Assert.Equal(7, driver.MaxTicksPerFrame);
    }

    // ---------------------------------------------------------------
    //  Accumulator must not drift (regression)
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(2.5, 25)]
    [InlineData(1.0, 10)]
    [InlineData(10.0, 100)]
    [InlineData(0.3, 3)]
    public void WholeSecondDeltas_YieldExactTickCounts(double seconds, int expected)
    {
        // Regression: subtracting SecondsPerTick in a loop accumulates binary
        // rounding error, so 2.5s at 10 ticks/s produced 24 ticks instead of 25.
        // Harmless for determinism, but the game clock drifted behind real time
        // a little more every frame.
        Assert.Equal(expected, Build().Advance(seconds));
    }

    [Fact]
    public void ManySmallFramesEqualOneLargeFrame()
    {
        var incremental = Build();
        int total = 0;
        for (int i = 0; i < 250; i++)
        {
            total += incremental.Advance(0.01);
        }

        Assert.Equal(Build().Advance(2.5), total);
    }

    [Fact]
    public void SpeedMultiplier_DoesNotIntroduceDrift()
    {
        var driver = Build();
        driver.SpeedMultiplier = 4.0;

        int total = 0;
        for (int i = 0; i < 10; i++)
        {
            total += driver.Advance(1.0);
        }

        Assert.Equal(400, total);
    }

    [Fact]
    public void SteadyFrames_ProduceTheExpectedLongRunRate()
    {
        // 10 seconds of 60fps frames at 10 ticks/s should yield ~100 ticks.
        // Guards against a slow drift that would only show up hours in.
        var driver = Build();
        int total = 0;

        for (int i = 0; i < 600; i++)
        {
            total += driver.Advance(1.0 / 60.0);
        }

        Assert.InRange(total, 99, 100);
    }
}
