using Microsoft.Extensions.Logging;

namespace Calcifer.Cathedra.Logging;

/// <summary>
/// Default <see cref="ILogWriter"/>: forwards to the underlying <c>ILogger&lt;LogWriter&gt;</c>.
/// Because it uses the same logging pipeline, the request-scoped correlation id (set by
/// RequestLoggingMiddleware) is attached to these lines too. Registered as a generic so callers
/// can inject <c>ILogWriter</c> and get a logger categorized to their own type.
/// </summary>
public sealed class LogWriter : ILogWriter
{
    private readonly ILogger _logger;

    public LogWriter(ILogger<LogWriter> logger) => _logger = logger;

    // Internal ctor lets the DI factory build a writer categorized to the consuming type.
    internal LogWriter(ILogger logger) => _logger = logger;

    public void Debug(string message, params object?[] args) => _logger.LogDebug(message, args);
    public void Info(string message, params object?[] args) => _logger.LogInformation(message, args);
    public void Warn(string message, params object?[] args) => _logger.LogWarning(message, args);
    public void Error(string message, params object?[] args) => _logger.LogError(message, args);

    public void Error(Exception exception, string message, params object?[] args) =>
        _logger.LogError(exception, message, args);
}
