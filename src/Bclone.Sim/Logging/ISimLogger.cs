namespace Bclone.Sim.Logging;

/// <summary>
/// Sink for structured, tick-stamped log entries.
/// </summary>
/// <remarks>
/// Note the tick is a parameter rather than something the logger looks up.
/// The sim owns the tick; the logger just records what it is told. That keeps
/// the logger free of any reference back into sim state, so a sink can be
/// swapped (in-memory for tests, file for dev) without touching the sim.
/// </remarks>
public interface ISimLogger
{
    /// <summary>Entries below this level are discarded.</summary>
    LogLevel MinimumLevel { get; }

    /// <summary>Record an entry. Implementations must not throw.</summary>
    void Log(ulong tick, LogLevel level, string subsystem, string message);
}

/// <summary>A sink that discards everything. Useful for benchmarks and for
/// tests that do not care about output.</summary>
public sealed class NullSimLogger : ISimLogger
{
    public static readonly NullSimLogger Instance = new();

    private NullSimLogger() { }

    public LogLevel MinimumLevel => LogLevel.Error;

    public void Log(ulong tick, LogLevel level, string subsystem, string message)
    {
        // Intentionally empty — this is the one place discarding is the point.
    }
}
