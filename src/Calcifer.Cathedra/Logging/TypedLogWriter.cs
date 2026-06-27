using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Calcifer.Cathedra.Logging;

/// <summary>
/// Open-generic implementation of <see cref="ILogWriter{T}"/>. It builds a <see cref="FileLogWriter"/>
/// categorized to <typeparamref name="T"/> (so file lines read <c>[module:T]</c>) and, when console
/// output is enabled, composes it with a <see cref="LogWriter"/> categorized to the same type.
/// Whether console is included is decided by <see cref="TypedLogWriterOptions"/>, set by
/// <c>AddFileLogging</c> to match its <c>alsoLogToConsole</c> argument.
/// </summary>
public sealed class TypedLogWriter<T> : ILogWriter<T>
{
    private readonly ILogWriter _inner;

    public TypedLogWriter(
        FileLogSink sink,
        IHttpContextAccessor httpContextAccessor,
        IOptions<FileLogOptions> fileOptions,
        IOptions<TypedLogWriterOptions> typedOptions,
        ILoggerFactory loggerFactory)
    {
        var module = typeof(T).Name;

        var fileWriter = new FileLogWriter(sink, httpContextAccessor, fileOptions, module);

        _inner = typedOptions.Value.AlsoLogToConsole
            ? new CompositeLogWriter(new LogWriter(loggerFactory.CreateLogger(module)), fileWriter)
            : fileWriter;
    }

    public void Debug(string message, params object?[] args) => _inner.Debug(message, args);
    public void Info(string message, params object?[] args) => _inner.Info(message, args);
    public void Warn(string message, params object?[] args) => _inner.Warn(message, args);
    public void Error(string message, params object?[] args) => _inner.Error(message, args);

    public void Error(Exception exception, string message, params object?[] args) =>
        _inner.Error(exception, message, args);
}

/// <summary>Carries the console/file choice from <c>AddFileLogging</c> to the open-generic writer.</summary>
public sealed class TypedLogWriterOptions
{
    public bool AlsoLogToConsole { get; set; } = true;
}
