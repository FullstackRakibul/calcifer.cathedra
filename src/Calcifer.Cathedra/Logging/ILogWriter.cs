namespace Calcifer.Cathedra.Logging;

/// <summary>
/// A small, framework-agnostic logging surface offered alongside <c>ILogger&lt;T&gt;</c>. Modules
/// may depend on this instead of the Microsoft abstractions when they want a narrower API; the
/// default <see cref="LogWriter"/> simply forwards to <c>ILogger</c>, so correlation-id scopes and
/// configured sinks still apply.
/// </summary>
public interface ILogWriter
{
    void Debug(string message, params object?[] args);
    void Info(string message, params object?[] args);
    void Warn(string message, params object?[] args);
    void Error(string message, params object?[] args);
    void Error(Exception exception, string message, params object?[] args);
}

/// <summary>
/// A category-typed <see cref="ILogWriter"/>, analogous to <c>ILogger&lt;T&gt;</c>: inject
/// <c>ILogWriter&lt;MyService&gt;</c> and every line is stamped <c>[module:MyService]</c> in the file
/// (and categorized for console). Registered as an open generic by <c>AddFileLogging</c>.
/// </summary>
public interface ILogWriter<T> : ILogWriter;
