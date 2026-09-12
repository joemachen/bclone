using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Xunit;

namespace Bclone.Sim.Tests;

public sealed class SimLoopTests
{
    private static SimConfig Config => new() { Seed = 7UL };

    private static SimLoop Build(params ISimSystem[] systems) =>
        new(SimWorld.Create(Config, new InMemoryLogSink()), systems);

    [Fact]
    public void NewWorld_StartsAtTickZero()
    {
        Assert.Equal(0UL, Build().World.Tick);
    }

    [Fact]
    public void StepOnce_AdvancesExactlyOneTick()
    {
        var loop = Build();
        loop.StepOnce();
        Assert.Equal(1UL, loop.World.Tick);
    }

    [Fact]
    public void Step_AdvancesRequestedTicks()
    {
        var loop = Build();
        loop.Step(500);
        Assert.Equal(500UL, loop.World.Tick);
    }

    [Fact]
    public void Step_WithZero_IsANoOp()
    {
        var loop = Build();
        loop.Step(0);
        Assert.Equal(0UL, loop.World.Tick);
    }

    [Fact]
    public void Step_WithNegativeCount_Throws()
    {
        var loop = Build();
        Assert.Throws<ArgumentOutOfRangeException>(() => loop.Step(-1));
    }

    [Fact]
    public void SystemsRun_InRegistrationOrder_EveryTick()
    {
        // Ordering is part of the determinism contract, so it gets a test
        // rather than a comment.
        var order = new List<string>();
        var loop = Build(
            new RecordingSystem("first", order),
            new RecordingSystem("second", order),
            new RecordingSystem("third", order));

        loop.Step(3);

        Assert.Equal(
            new[] { "first", "second", "third", "first", "second", "third", "first", "second", "third" },
            order);
    }

    [Fact]
    public void EverySystem_RunsOncePerTick()
    {
        var order = new List<string>();
        var a = new RecordingSystem("a", order);
        var b = new RecordingSystem("b", order);
        var loop = Build(a, b);

        loop.Step(250);

        Assert.Equal(250, a.ExecutionCount);
        Assert.Equal(250, b.ExecutionCount);
    }

    [Fact]
    public void SystemsObserve_TheTickTheyAreComputing()
    {
        // The tick increments after systems run, so the first execution sees 0.
        var observer = new TickObservingSystem();
        var loop = Build(observer);

        loop.StepOnce();

        Assert.Equal(0UL, observer.FirstObservedTick);
        Assert.Equal(1UL, loop.World.Tick);
    }

    [Fact]
    public void SystemException_IsWrappedWithTickAndSystemName()
    {
        // METHODOLOGY.md §4: never swallow. Fail loudly, with enough context to
        // replay the seed to that exact tick.
        var loop = Build(new ThrowingSystem(throwOnTick: 5UL));

        var ex = Assert.Throws<SimSystemException>(() => loop.Step(10));

        Assert.Equal("throwing", ex.SystemName);
        Assert.Equal(5UL, ex.Tick);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void SystemException_IsLoggedAtErrorWithContext()
    {
        var sink = new InMemoryLogSink();
        var world = SimWorld.Create(Config, sink);
        var loop = new SimLoop(world, new ISimSystem[] { new ThrowingSystem(3UL) });

        Assert.Throws<SimSystemException>(() => loop.Step(10));

        LogEntry error = Assert.Single(sink.Entries, e => e.Level == LogLevel.Error);
        Assert.Equal(3UL, error.Tick);
        Assert.Contains("throwing", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⛔⛔ A faulted loop stays faulted and runs nothing more — <b>the error boundary's sim half</b>
    /// (D364, `tick-loop.md §5d`).
    /// </summary>
    /// <remarks>
    /// The world is exactly as the throw left it: the tick has not advanced, no system runs again
    /// (the observer counts), and the same exception comes back on every later step, so the
    /// driver can halt once and be sure nothing moved behind its back.
    /// </remarks>
    [Fact]
    public void AFaultedLoopStaysFaultedAndRunsNothingMore()
    {
        var observer = new TickObservingSystem();
        var loop = Build(observer, new ThrowingSystem(throwOnTick: 3UL));

        Assert.Null(loop.Fault);
        SimSystemException first = Assert.Throws<SimSystemException>(() => loop.Step(10));
        Assert.Same(first, loop.Fault);
        Assert.Equal(3UL, loop.World.Tick);
        int ran = observer.Executions;

        SimSystemException again = Assert.Throws<SimSystemException>(() => loop.StepOnce());
        Assert.Same(first, again);
        Assert.Equal(3UL, loop.World.Tick);
        Assert.Equal(ran, observer.Executions);
    }

    [Fact]
    public void NullSystem_IsRejectedAtConstruction()
    {
        var world = SimWorld.Create(Config, new InMemoryLogSink());
        Assert.Throws<ArgumentException>(() => new SimLoop(world, new ISimSystem[] { null! }));
    }

    [Fact]
    public void LoopWithNoSystems_StillAdvancesTheClock()
    {
        var loop = Build();
        loop.Step(10);
        Assert.Equal(10UL, loop.World.Tick);
    }

    [Fact]
    public void SystemList_IsSnapshottedAtConstruction()
    {
        // A run's system order should be a fact about that run, not something a
        // caller can mutate mid-run.
        var order = new List<string>();
        var systems = new List<ISimSystem> { new RecordingSystem("a", order) };
        var loop = new SimLoop(SimWorld.Create(Config, new InMemoryLogSink()), systems);

        systems.Add(new RecordingSystem("b", order));
        loop.StepOnce();

        Assert.Single(loop.Systems);
        Assert.Equal(new[] { "a" }, order);
    }
}
