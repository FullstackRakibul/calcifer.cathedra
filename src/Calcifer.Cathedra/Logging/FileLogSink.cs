using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Options;

namespace Calcifer.Cathedra.Logging;

/// <summary>
/// The single, thread-safe file writer behind <see cref="FileLogWriter"/>. Pre-formatted lines are
/// pushed onto a bounded queue and drained by one dedicated background thread, so callers never
/// block on disk I/O and the file is only ever touched by one thread (no locking on the hot path,
/// no corruption under concurrency).
///
/// Rotation: a file per UTC day (<c>cathedra-YYYY-MM-DD.log</c>) when <see cref="FileLogOptions.UseDailyRotation"/>
/// is set, plus a numbered part when the current file exceeds <see cref="FileLogOptions.MaxFileSizeMB"/>.
/// Retention: files older than <see cref="FileLogOptions.RetainDays"/> are purged at startup and at
/// each day boundary.
///
/// Registered as a singleton; implements <see cref="IDisposable"/> so the host flushes the queue on
/// shutdown.
/// </summary>
public sealed class FileLogSink : IDisposable
{
    private readonly FileLogOptions _options;
    private readonly BlockingCollection<string> _queue;
    private readonly Thread _worker;
    private readonly string _directory;

    private DateOnly _currentDate;
    private int _currentPart;
    private string _currentFilePath = string.Empty;
    private long _currentFileSize;

    public FileLogSink(IOptions<FileLogOptions> options)
    {
        _options = options.Value;

        _directory = Path.IsPathRooted(_options.LogPath)
            ? _options.LogPath
            : Path.Combine(AppContext.BaseDirectory, _options.LogPath);
        System.IO.Directory.CreateDirectory(_directory);

        // Bounded queue: if logging massively outpaces disk, Add blocks briefly rather than
        // growing memory unbounded. 10k lines is generous for normal traffic.
        _queue = new BlockingCollection<string>(boundedCapacity: 10_000);

        _currentDate = DateOnly.FromDateTime(DateTime.UtcNow);
        ResolveCurrentFile();
        PurgeOldFiles();

        _worker = new Thread(DrainLoop)
        {
            IsBackground = true,
            Name = "Cathedra.FileLogSink",
        };
        _worker.Start();
    }

    /// <summary>The absolute directory log files are written to (useful for diagnostics/tests).</summary>
    public string LogDirectory => _directory;

    /// <summary>The file currently being written to (useful for diagnostics/tests).</summary>
    public string CurrentFilePath => _currentFilePath;

    /// <summary>Queue a pre-formatted line. Non-blocking unless the queue is full; never throws after dispose.</summary>
    public void Enqueue(string line)
    {
        if (_queue.IsAddingCompleted)
            return;
        try
        {
            _queue.Add(line);
        }
        catch (InvalidOperationException)
        {
            // Race with CompleteAdding during shutdown — drop silently.
        }
    }

    private void DrainLoop()
    {
        foreach (var line in _queue.GetConsumingEnumerable())
        {
            try
            {
                WriteLine(line);
            }
            catch
            {
                // A logging failure must never crash the app or the worker thread.
            }
        }
    }

    private void WriteLine(string line)
    {
        RollIfNeeded(line.Length);
        var bytes = Encoding.UTF8.GetBytes(line + Environment.NewLine);
        using var fs = new FileStream(
            _currentFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        fs.Write(bytes, 0, bytes.Length);
        _currentFileSize += bytes.Length;
    }

    private void RollIfNeeded(int incomingLength)
    {
        // Day boundary: switch to a new dated file and purge expired ones.
        if (_options.UseDailyRotation)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (today != _currentDate)
            {
                _currentDate = today;
                _currentPart = 0;
                ResolveCurrentFile();
                PurgeOldFiles();
                return;
            }
        }

        // Size boundary: start the next numbered part.
        if (_options.MaxFileSizeMB > 0)
        {
            var capBytes = (long)_options.MaxFileSizeMB * 1024 * 1024;
            if (_currentFileSize + incomingLength > capBytes)
            {
                _currentPart++;
                ResolveCurrentFile();
            }
        }
    }

    /// <summary>Compute the current file path and its existing size for the active date/part.</summary>
    private void ResolveCurrentFile()
    {
        var datePart = _options.UseDailyRotation
            ? _currentDate.ToString("yyyy-MM-dd")
            : "current";

        var name = _currentPart == 0
            ? $"{_options.FilePrefix}-{datePart}.log"
            : $"{_options.FilePrefix}-{datePart}.{_currentPart:D3}.log";

        _currentFilePath = Path.Combine(_directory, name);
        _currentFileSize = File.Exists(_currentFilePath)
            ? new FileInfo(_currentFilePath).Length
            : 0;

        // If size rollover is on and we landed on a part that's already full, advance.
        if (_options.MaxFileSizeMB > 0)
        {
            var capBytes = (long)_options.MaxFileSizeMB * 1024 * 1024;
            while (_currentFileSize >= capBytes)
            {
                _currentPart++;
                name = $"{_options.FilePrefix}-{datePart}.{_currentPart:D3}.log";
                _currentFilePath = Path.Combine(_directory, name);
                _currentFileSize = File.Exists(_currentFilePath)
                    ? new FileInfo(_currentFilePath).Length
                    : 0;
            }
        }
    }

    private void PurgeOldFiles()
    {
        if (_options.RetainDays <= 0)
            return;

        var cutoff = DateTime.UtcNow.AddDays(-_options.RetainDays);
        try
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(
                         _directory, $"{_options.FilePrefix}-*.log"))
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    try { File.Delete(file); } catch { /* in use or gone — skip */ }
                }
            }
        }
        catch
        {
            // Directory listing failed — non-fatal.
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        // Give the worker a moment to flush the remaining queue.
        _worker.Join(TimeSpan.FromSeconds(5));
        _queue.Dispose();
    }
}
