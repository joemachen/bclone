namespace Bclone.Sim.Logging;

/// <summary>
/// A single structured log record.
/// </summary>
/// <remarks>
/// Every entry carries the <see cref="Tick"/> it was emitted at, which is the
/// whole point (METHODOLOGY.md §4): any log line can be tied back to an exact
/// simulation state, and — because the sim is deterministic — that state can be
/// reproduced exactly by replaying the same seed to the same tick.
///
/// This is a record so value equality comes for free; the determinism test
/// compares whole log sequences.
/// </remarks>
public sealed record LogEntry(
    ulong Tick,
    LogLevel Level,
    string Subsystem,
    string Message,
    LogCategory Category = LogCategory.Ordinary)
{
    /// <summary>
    /// Stable, human-readable rendering. Deliberately deterministic — no
    /// wall-clock timestamp, because the tick <em>is</em> the timestamp.
    /// </summary>
    /// <remarks>
    /// <b>⭐ The category appears only when there IS one</b>, so the tens of thousands of debug
    /// lines in an audit trail are byte-identical to what they were and the greps this project
    /// lives by keep working. A categorised line gains one bracketed word, which is worth having
    /// when reading a played log — <c>grep "\[death\]"</c> is a better question than
    /// <c>grep -i died</c>.
    /// </remarks>
    public override string ToString() =>
        Category == LogCategory.Ordinary
            ? $"[t{Tick,8}] {Level.ToString().ToUpperInvariant(),-5} {Subsystem,-8} {Message}"
            : $"[t{Tick,8}] {Level.ToString().ToUpperInvariant(),-5} {Subsystem,-8} "
                + $"[{Category.ToString().ToLowerInvariant()}] {Message}";
}
