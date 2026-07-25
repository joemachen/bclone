using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Xunit;

namespace Bclone.Sim.Tests;

/// <summary>
/// The P0 suite. A failure in here is a P0 bug (METHODOLOGY.md §3) — it means the
/// project's central architectural guarantee has broken, and every golden test,
/// save file, and replay downstream of it is suspect.
/// </summary>
/// <remarks>
/// Note the anti-vacuity tests further down. A determinism test that cannot fail
/// is worse than no test at all: it stays green forever and buys false confidence.
/// Those tests exist to prove this suite still has teeth.
/// </remarks>
public sealed class DeterminismTests
{
    private const int TickCount = 10_000;

    private static SimConfig Config => new()
    {
        Seed = 12345UL,
        TicksPerDay = 4,
        TargetTicksPerSecond = 10.0,
        MaxTicksPerFrame = 250,
    };

    private static (SimLoop Loop, InMemoryLogSink Log) BuildRun(ulong? seedOverride = null)
    {
        var sink = new InMemoryLogSink();
        var world = SimWorld.Create(Config, sink, seedOverride);
        var loop = new SimLoop(world, new ISimSystem[]
        {
            new RngChurnSystem(),
            new PeriodicLogSystem(100UL),
        });
        return (loop, sink);
    }

    // ---------------------------------------------------------------
    //  The contract
    // ---------------------------------------------------------------

    [Fact]
    public void SameSeed_ProducesIdenticalState()
    {
        var (a, _) = BuildRun();
        var (b, _) = BuildRun();

        a.Step(TickCount);
        b.Step(TickCount);

        Assert.Equal(StateHash.Compute(a.World), StateHash.Compute(b.World));
        Assert.Equal(a.World.Tick, b.World.Tick);
        Assert.Equal(a.World.Rng, b.World.Rng);
    }

    [Fact]
    public void SameSeed_ProducesIdenticalLog()
    {
        var (a, logA) = BuildRun();
        var (b, logB) = BuildRun();

        a.Step(TickCount);
        b.Step(TickCount);

        Assert.NotEmpty(logA.Entries);
        Assert.Equal(logA.Entries, logB.Entries);
    }

    [Fact]
    public void SameSeed_ProducesIdenticalStateAtEveryTick()
    {
        // Catches a divergence at the tick it starts rather than only at the end,
        // where two runs could in principle drift apart and back together.
        var (a, _) = BuildRun();
        var (b, _) = BuildRun();

        for (int i = 0; i < 1_000; i++)
        {
            a.StepOnce();
            b.StepOnce();
            Assert.Equal(StateHash.Compute(a.World), StateHash.Compute(b.World));
        }
    }

    // ---------------------------------------------------------------
    //  Anti-vacuity: prove the above can actually fail
    // ---------------------------------------------------------------

    [Fact]
    public void DifferentSeed_ProducesDifferentState()
    {
        var (a, _) = BuildRun(seedOverride: 1UL);
        var (b, _) = BuildRun(seedOverride: 2UL);

        a.Step(TickCount);
        b.Step(TickCount);

        Assert.NotEqual(StateHash.Compute(a.World), StateHash.Compute(b.World));
    }

    [Fact]
    public void StateHash_ChangesAsTheWorldAdvances()
    {
        var (loop, _) = BuildRun();

        ulong before = StateHash.Compute(loop.World);
        loop.Step(1);
        ulong after = StateHash.Compute(loop.World);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void StateHash_DiffersForDifferentTickCounts()
    {
        var (a, _) = BuildRun();
        var (b, _) = BuildRun();

        a.Step(100);
        b.Step(101);

        Assert.NotEqual(StateHash.Compute(a.World), StateHash.Compute(b.World));
    }

    // ---------------------------------------------------------------
    //  Decoupling: playback must not leak into the simulation
    // ---------------------------------------------------------------

    [Fact]
    public void BatchedSteps_EqualSingleSteps()
    {
        // If this ever fails, frame pacing has started influencing sim outcomes.
        var (oneBatch, _) = BuildRun();
        var (manyBatches, _) = BuildRun();
        var (oneAtATime, _) = BuildRun();

        oneBatch.Step(TickCount);

        for (int i = 0; i < 100; i++)
        {
            manyBatches.Step(TickCount / 100);
        }

        for (int i = 0; i < TickCount; i++)
        {
            oneAtATime.StepOnce();
        }

        ulong expected = StateHash.Compute(oneBatch.World);
        Assert.Equal(expected, StateHash.Compute(manyBatches.World));
        Assert.Equal(expected, StateHash.Compute(oneAtATime.World));
    }

    [Fact]
    public void PlaybackSpeed_DoesNotAffectState()
    {
        // A run at 4x must produce exactly the same history as a run at 1x.
        // This is the payoff of scaling tick COUNT rather than tick SIZE.
        var config = Config;

        ulong RunAtSpeed(double speed)
        {
            var (loop, _) = BuildRun();
            var driver = new FixedTimestepDriver(config) { SpeedMultiplier = speed };

            int remaining = 2_000;
            while (remaining > 0)
            {
                int ticks = driver.Advance(1.0 / 60.0, loop.World.Tick);
                if (ticks > remaining)
                {
                    ticks = remaining;
                }

                loop.Step(ticks);
                remaining -= ticks;
            }

            return StateHash.Compute(loop.World);
        }

        Assert.Equal(RunAtSpeed(1.0), RunAtSpeed(4.0));
    }

    [Fact]
    public void PausedDriver_LeavesSimUntouched()
    {
        var (loop, _) = BuildRun();
        var driver = new FixedTimestepDriver(Config) { SpeedMultiplier = 0.0 };

        ulong before = StateHash.Compute(loop.World);

        for (int i = 0; i < 600; i++)
        {
            loop.Step(driver.Advance(1.0 / 60.0, loop.World.Tick));
        }

        Assert.True(driver.IsPaused);
        Assert.Equal(0UL, loop.World.Tick);
        Assert.Equal(before, StateHash.Compute(loop.World));
    }

    // ---------------------------------------------------------------
    //  Config isolation
    // ---------------------------------------------------------------

    [Fact]
    public void PlaybackConfig_DoesNotAffectState()
    {
        // target_ticks_per_second and max_ticks_per_frame are playback-only.
        // If either ever reaches sim logic, this fails.
        static ulong Run(double ticksPerSecond, int maxTicksPerFrame)
        {
            var config = new SimConfig
            {
                Seed = 12345UL,
                TicksPerDay = 4,
                TargetTicksPerSecond = ticksPerSecond,
                MaxTicksPerFrame = maxTicksPerFrame,
            };

            var world = SimWorld.Create(config, new InMemoryLogSink());
            var loop = new SimLoop(world, new ISimSystem[] { new RngChurnSystem() });
            loop.Step(1_000);
            return StateHash.Compute(world);
        }

        Assert.Equal(Run(10.0, 250), Run(240.0, 5));
    }
}
