using System.Text;
using Calcifer.Cathedra.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Calcifer.Cathedra.Logging;

/// <summary>
/// An <see cref="ILogWriter"/> that writes structured, human-readable lines to the rotating file
/// managed by <see cref="FileLogSink"/>. Each line carries the UTC timestamp, level, correlation id,
/// client IP, module/service name, and the formatted message; exceptions are appended with type,
/// message, and stack trace.
///
/// Correlation id and IP are read from the active <see cref="HttpContext"/> via
/// <see cref="IHttpContextAccessor"/>; outside a request (e.g. background jobs) they fall back to
/// <c>N/A</c>. The "module" is the logger category, set per consumer by the DI factory in
/// <c>AddFileLogging</c> so it reads like <c>ILogger&lt;T&gt;</c>.
/// </summary>
public sealed class FileLogWriter : ILogWriter
{
    private const string NotAvailable = "N/A";

    private readonly FileLogSink _sink;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly FileLogOptions _options;
    private readonly string _module;

    public FileLogWriter(
        FileLogSink sink,
        IHttpContextAccessor httpContextAccessor,
        IOptions<FileLogOptions> options,
        string module)
    {
        _sink = sink;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
        _module = string.IsNullOrWhiteSpace(module) ? "Cathedra" : module;
    }

    public void Debug(string message, params object?[] args) => Write(LogLevel.Debug, null, message, args);
    public void Info(string message, params object?[] args) => Write(LogLevel.Information, null, message, args);
    public void Warn(string message, params object?[] args) => Write(LogLevel.Warning, null, message, args);
    public void Error(string message, params object?[] args) => Write(LogLevel.Error, null, message, args);

    public void Error(Exception exception, string message, params object?[] args) =>
        Write(LogLevel.Error, exception, message, args);

    private void Write(LogLevel level, Exception? exception, string message, object?[] args)
    {
        if (level < _options.LogLevel)
            return;

        var (correlationId, clientIp) = ResolveRequestContext();
        var line = Format(level, correlationId, clientIp, _module, message, args, exception);
        _sink.Enqueue(line);
    }

    private (string correlationId, string clientIp) ResolveRequestContext()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null)
            return (NotAvailable, NotAvailable);

        var correlationId = ctx.GetCorrelationId();
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? NotAvailable;
        return (string.IsNullOrEmpty(correlationId) ? NotAvailable : correlationId, ip);
    }

    /// <summary>
    /// Renders one entry, e.g.
    /// <c>[2026-06-27T14:23:45.123Z] [INFO] [cid:abc-123] [ip:192.168.1.100] [module:AuthService] User "admin" logged in.</c>
    /// </summary>
    internal static string Format(
        LogLevel level,
        string correlationId,
        string clientIp,
        string module,
        string message,
        object?[] args,
        Exception? exception)
    {
        var sb = new StringBuilder();
        sb.Append('[').Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")).Append("] ");
        sb.Append('[').Append(ShortLevel(level)).Append("] ");
        sb.Append("[cid:").Append(correlationId).Append("] ");
        sb.Append("[ip:").Append(clientIp).Append("] ");
        sb.Append("[module:").Append(module).Append("] ");
        sb.Append(SafeFormat(message, args));

        if (exception is not null)
        {
            sb.Append(Environment.NewLine);
            sb.Append(exception); // type + message + full stack trace
        }

        return sb.ToString();
    }

    /// <summary>Fills <c>{Placeholder}</c>/<c>{0}</c> style templates without throwing on mismatch.</summary>
    private static string SafeFormat(string message, object?[] args)
    {
        if (args is null || args.Length == 0)
            return message;

        try
        {
            // Support positional {0},{1} templates directly.
            return string.Format(message, args);
        }
        catch (FormatException)
        {
            // Named {Placeholder} template (ILogger style) or malformed — append args instead.
            return $"{message} [{string.Join(", ", args)}]";
        }
    }

    private static string ShortLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRIT",
        _ => "INFO",
    };
}
