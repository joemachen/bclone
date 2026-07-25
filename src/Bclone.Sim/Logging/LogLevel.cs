namespace Bclone.Sim.Logging;

/// <summary>
/// Severity levels per METHODOLOGY.md §4. Ordered, so a sink can filter
/// with a simple <c>level &gt;= MinimumLevel</c> comparison.
/// </summary>
public enum LogLevel
{
    /// <summary>Firehose detail. Off by default even in dev.</summary>
    Trace = 0,

    /// <summary>Developer-facing detail; on in dev builds.</summary>
    Debug = 1,

    /// <summary>
    /// Notable sim events. From Phase 0 onward this level doubles as the
    /// player-facing life log — the narrative view of what happened
    /// (specs/phase-0-vertical-slice.md §7).
    /// </summary>
    Info = 2,

    /// <summary>Something recoverable but wrong. Never emitted silently.</summary>
    Warn = 3,

    /// <summary>A failure. Log with full context, then handle or fail loudly.</summary>
    Error = 4,
}
