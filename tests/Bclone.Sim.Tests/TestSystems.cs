using Bclone.Sim.Core;
using Bclone.Sim.Logging;

namespace Bclone.Sim.Tests;

/// <summary>
/// Draws from the seeded RNG every tick, so world state genuinely evolves.
/// </summary>
/// <remarks>
/// The determinism tests need a world that actually changes; a world that never
/// mutates would make them pass trivially and prove nothing. This is the
/// stand-in until Phase 0 supplies real systems.
/// </remarks>
public sealed class RngChurnSystem : ISimSystem
{
    public string Name => "rng-churn";

    /// <summary>Running total of everything drawn — an extra witness that two
    /// runs followed the same path, independent of the state hash.</summary>
    public ulong Accumulator { get; private set; }

    public void Execute(SimWorld world)
    {
        Accumulator = unchecked(Accumulator + world.Rng.NextUInt());
    }
}

/// <summary>Logs on a fixed tick cadence, so log sequences can be compared.</summary>
public sealed class PeriodicLogSystem : ISimSystem
{
    private readonly ulong _everyNTicks;

    public PeriodicLogSystem(ulong everyNTicks = 100UL)
    {
        _everyNTicks = everyNTicks == 0 ? 1UL : everyNTicks;
    }

    public string Name => "periodic-log";

    public void Execute(SimWorld world)
    {
        if (world.Tick % _everyNTicks == 0)
        {
            world.Log(LogLevel.Info, "test", $"heartbeat at tick {world.Tick}");
        }
    }
}

/// <summary>Records the order in which it ran, for ordering tests.</summary>
public sealed class RecordingSystem : ISimSystem
{
    private readonly List<string> _sharedLog;

    public RecordingSystem(string name, List<string> sharedLog)
    {
        Name = name;
        _sharedLog = sharedLog;
    }

    public string Name { get; }

    public int ExecutionCount { get; private set; }

    public void Execute(SimWorld world)
    {
        ExecutionCount++;
        _sharedLog.Add(Name);
    }
}

/// <summary>Always throws — used to prove failures are logged and surfaced,
/// never swallowed.</summary>
public sealed class ThrowingSystem : ISimSystem
{
    private readonly ulong _throwOnTick;

    public ThrowingSystem(ulong throwOnTick) => _throwOnTick = throwOnTick;

    public string Name => "throwing";

    public void Execute(SimWorld world)
    {
        if (world.Tick == _throwOnTick)
        {
            throw new InvalidOperationException("deliberate test failure");
        }
    }
}

/// <summary>Observes <see cref="SimWorld.Tick"/> on its first execution.</summary>
public sealed class TickObservingSystem : ISimSystem
{
    public string Name => "tick-observer";

    public ulong? FirstObservedTick { get; private set; }

    public void Execute(SimWorld world) => FirstObservedTick ??= world.Tick;
}
