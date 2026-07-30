namespace Bclone.Sim.Logging;

/// <summary>
/// Fans one entry out to several sinks.
/// </summary>
/// <remarks>
/// <para>
/// The game wants two things at once and they disagree about volume: a short INFO
/// stream for the village log on screen (D8 — the story the player reads), and
/// everything down to DEBUG on disk so a run can be audited afterwards. One sink cannot
/// be both, and duplicating the call sites would be how the two eventually diverge.
/// </para>
/// <para>
/// Each sink keeps its own <see cref="ISimLogger.MinimumLevel"/> and does its own
/// filtering, so the composite deliberately reports the <em>lowest</em> of them: if any
/// sink still wants DEBUG, the sim must keep producing it.
/// </para>
/// </remarks>
public sealed class CompositeLogSink : ISimLogger, IDisposable
{
    private readonly ISimLogger[] _sinks;
    private bool _disposed;

    public CompositeLogSink(params ISimLogger[] sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);

        if (sinks.Length == 0)
        {
            throw new ArgumentException("A composite with no sinks discards everything, which is what NullSimLogger is for.", nameof(sinks));
        }

        _sinks = sinks;

        LogLevel lowest = LogLevel.Error;
        foreach (ISimLogger sink in sinks)
        {
            if (sink.MinimumLevel < lowest)
            {
                lowest = sink.MinimumLevel;
            }
        }

        MinimumLevel = lowest;
    }

    public LogLevel MinimumLevel { get; }

    public void Log(ulong tick, LogLevel level, string subsystem, string message)
    {
        if (_disposed)
        {
            return;
        }

        for (int i = 0; i < _sinks.Length; i++)
        {
            _sinks[i].Log(tick, level, subsystem, message);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (ISimLogger sink in _sinks)
        {
            (sink as IDisposable)?.Dispose();
        }
    }
}
