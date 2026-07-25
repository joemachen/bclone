namespace Bclone.Sim.Logging;

/// <summary>
/// Buffers entries in order. Backs both the determinism test (which compares
/// whole log sequences) and, from Phase 0, the on-screen life log.
/// </summary>
public sealed class InMemoryLogSink : ISimLogger
{
    private readonly List<LogEntry> _entries = new();

    public InMemoryLogSink(LogLevel minimumLevel = LogLevel.Debug)
    {
        MinimumLevel = minimumLevel;
    }

    public LogLevel MinimumLevel { get; }

    /// <summary>Entries in emission order.</summary>
    public IReadOnlyList<LogEntry> Entries => _entries;

    public void Log(ulong tick, LogLevel level, string subsystem, string message)
    {
        if (level < MinimumLevel)
        {
            return;
        }

        _entries.Add(new LogEntry(tick, level, subsystem, message));
    }

    public void Clear() => _entries.Clear();
}
