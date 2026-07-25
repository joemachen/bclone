using System.Text;

namespace Bclone.Sim.Logging;

/// <summary>
/// Appends entries to a file under <c>logs/</c>, and optionally mirrors them to
/// the console for dev builds (METHODOLOGY.md §4).
/// </summary>
/// <remarks>
/// The log <em>filename</em> is timestamped with wall-clock time, which is fine —
/// it is a filesystem concern, not sim state, and nothing inside the sim ever
/// reads it. The caller supplies the name so that <c>Bclone.Sim</c> itself stays
/// clear of <c>DateTime</c> (see BannedSymbols.txt); <c>run.bat</c> and the view
/// layer already know the real time.
/// </remarks>
public sealed class FileLogSink : ISimLogger, IDisposable
{
    private readonly StreamWriter _writer;
    private readonly bool _alsoConsole;
    private bool _disposed;

    public FileLogSink(string path, LogLevel minimumLevel = LogLevel.Debug, bool alsoConsole = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(path, append: true, Encoding.UTF8) { AutoFlush = true };
        _alsoConsole = alsoConsole;
        MinimumLevel = minimumLevel;
    }

    public LogLevel MinimumLevel { get; }

    public void Log(ulong tick, LogLevel level, string subsystem, string message)
    {
        if (_disposed || level < MinimumLevel)
        {
            return;
        }

        var line = new LogEntry(tick, level, subsystem, message).ToString();

        try
        {
            _writer.WriteLine(line);
        }
        catch (IOException ex)
        {
            // Never swallow (METHODOLOGY.md §4). If the log file itself is the
            // thing that broke, the console is the only place left to say so.
            Console.Error.WriteLine($"[FileLogSink] failed to write log entry: {ex.Message}");
        }

        if (_alsoConsole)
        {
            Console.WriteLine(line);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writer.Dispose();
    }
}
