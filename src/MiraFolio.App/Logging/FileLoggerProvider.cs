using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;
using MiraFolio.Core.Utilities;

namespace MiraFolio.App.Logging;

[ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logPath;
    private readonly BlockingCollection<string> _queue = new(4096);
    private readonly Thread _writerThread;
    private bool _disposed;

    public static string DefaultLogPath =>
        AppDataPaths.PrepareFile("mirafolio.log");

    public FileLoggerProvider(string? logPath = null)
    {
        try
        {
            _logPath = logPath ?? DefaultLogPath;
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            RotateIfNeeded();
        }
        catch
        {
            // Logging is best-effort and must not prevent app startup. The writer loop also
            // handles a failure to open this fallback path.
            _logPath = Path.Combine(Path.GetTempPath(), "MiraFolio.log");
        }

        _writerThread = new Thread(WriteLoop) { IsBackground = true, Name = "FileLogger" };
        _writerThread.Start();
    }

    public ILogger CreateLogger(string categoryName) =>
        new FileLogger(categoryName, _queue);

    private void WriteLoop()
    {
        try
        {
            using var writer = new StreamWriter(_logPath, append: true) { AutoFlush = true };
            foreach (var line in _queue.GetConsumingEnumerable())
                writer.WriteLine(line);
        }
        catch { /* best-effort */ }
    }

    // Keep last 5 MB; rename to .log.bak on rotation
    private void RotateIfNeeded()
    {
        const long maxBytes = 5 * 1024 * 1024;
        if (!File.Exists(_logPath)) return;
        if (new FileInfo(_logPath).Length < maxBytes) return;

        var bak = _logPath + ".bak";
        if (File.Exists(bak)) File.Delete(bak);
        File.Move(_logPath, bak);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _queue.CompleteAdding();
        _writerThread.Join(TimeSpan.FromSeconds(2));
        _queue.Dispose();
    }
}

internal sealed class FileLogger : ILogger
{
    private readonly string _category;
    private readonly BlockingCollection<string> _queue;

    public FileLogger(string category, BlockingCollection<string> queue)
    {
        _category = category;
        _queue = queue;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        if (_queue.IsAddingCompleted) return;

        var level = logLevel switch
        {
            LogLevel.Trace       => "TRC",
            LogLevel.Debug       => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning     => "WRN",
            LogLevel.Error       => "ERR",
            LogLevel.Critical    => "CRT",
            _                    => "???",
        };

        var shortCat = _category.Length > 30
            ? "…" + _category[^29..]
            : _category;

        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {shortCat}: {formatter(state, exception)}";
        if (exception != null)
            line += Environment.NewLine + exception;

        try { _queue.TryAdd(line, millisecondsTimeout: 50); }
        catch { /* drop if full */ }
    }
}
