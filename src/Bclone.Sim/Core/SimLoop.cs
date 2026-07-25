using Bclone.Sim.Logging;

namespace Bclone.Sim.Core;

/// <summary>
/// The only way the simulation advances.
/// </summary>
/// <remarks>
/// <para>
/// Note what is missing: there is no <c>deltaTime</c>, anywhere. <see cref="Step"/>
/// takes a <em>count</em>, never a duration. A tick is an indivisible, dimensionless
/// unit; how much game time it represents is a config interpretation
/// (<c>ticks_per_day</c>), not a property of the loop.
/// </para>
/// <para>
/// That is the whole trick behind the determinism contract. Real time enters the
/// program in exactly one place — <see cref="FixedTimestepDriver"/> — and all it
/// can influence is <em>how many</em> times this method is called, never what
/// happens inside a call.
/// </para>
/// </remarks>
public sealed class SimLoop
{
    private readonly ISimSystem[] _systems;

    public SimLoop(SimWorld world, IReadOnlyList<ISimSystem>? systems = null)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));

        // Copied into a fixed array at construction: the registered set cannot
        // change mid-run, so a run's system order is a fact about that run.
        _systems = systems is null ? Array.Empty<ISimSystem>() : systems.ToArray();

        for (int i = 0; i < _systems.Length; i++)
        {
            if (_systems[i] is null)
            {
                throw new ArgumentException($"System at index {i} is null.", nameof(systems));
            }
        }

        World.Log(LogLevel.Debug, "sim",
            $"SimLoop initialised with {_systems.Length} system(s): {DescribeSystems()}");
    }

    /// <summary>The world this loop advances.</summary>
    public SimWorld World { get; }

    /// <summary>Systems in execution order.</summary>
    public IReadOnlyList<ISimSystem> Systems => _systems;

    /// <summary>Current tick. Convenience passthrough for the view layer.</summary>
    public ulong Tick => World.Tick;

    /// <summary>
    /// Advance exactly one tick: run every system in order, then increment the
    /// tick counter.
    /// </summary>
    /// <remarks>
    /// The increment happens <em>after</em> systems run, so a system reading
    /// <see cref="SimWorld.Tick"/> sees the tick it is currently computing
    /// (0-based). The first call executes systems at tick 0 and leaves
    /// <see cref="SimWorld.Tick"/> at 1.
    /// </remarks>
    public void StepOnce()
    {
        for (int i = 0; i < _systems.Length; i++)
        {
            ISimSystem system = _systems[i];
            try
            {
                system.Execute(World);
            }
            catch (Exception ex)
            {
                // Never swallow (METHODOLOGY.md §4). Log with full context —
                // tick and system name are exactly what is needed to replay
                // this seed to this tick and look — then fail loudly.
                World.Log(LogLevel.Error, "sim",
                    $"System '{system.Name}' threw at tick {World.Tick}: {ex.GetType().Name}: {ex.Message}");
                throw new SimSystemException(system.Name, World.Tick, ex);
            }
        }

        World.Tick++;
    }

    /// <summary>Advance <paramref name="ticks"/> ticks.</summary>
    /// <remarks>
    /// Batching is purely a convenience: N calls of <see cref="StepOnce"/> and one
    /// call of <c>Step(N)</c> are indistinguishable in their effect on world state.
    /// There is a test that proves it (<c>BatchedSteps_EqualSingleSteps</c>), because
    /// the moment that stops being true, the driver's frame pacing starts leaking
    /// into the simulation.
    /// </remarks>
    public void Step(int ticks)
    {
        if (ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks), $"Tick count cannot be negative (got {ticks}).");
        }

        for (int i = 0; i < ticks; i++)
        {
            StepOnce();
        }
    }

    private string DescribeSystems() =>
        _systems.Length == 0 ? "(none)" : string.Join(" -> ", _systems.Select(s => s.Name));
}

/// <summary>
/// Wraps an exception thrown inside a system, tagged with the system name and the
/// tick it happened on — enough to reproduce it exactly from the run's seed.
/// </summary>
public sealed class SimSystemException : Exception
{
    public SimSystemException(string systemName, ulong tick, Exception innerException)
        : base($"System '{systemName}' failed at tick {tick}.", innerException)
    {
        SystemName = systemName;
        Tick = tick;
    }

    public string SystemName { get; }

    public ulong Tick { get; }
}
