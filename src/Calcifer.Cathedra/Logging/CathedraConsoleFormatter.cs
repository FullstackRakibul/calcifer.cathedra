using System.Text;
using Calcifer.Cathedra.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Calcifer.Cathedra.Logging;

/// <summary>
/// A console formatter that keeps the familiar single-line layout but surfaces the request's
/// correlation id inline — <c>[cid:&lt;id&gt;]</c> — by reading it from the active logging scopes.
/// This makes the spec's promise visible: every line emitted during a request shows the same id,
/// because RequestLoggingMiddleware opens a <c>CorrelationId</c> scope around the whole request.
/// Registered via <c>AddConsole(o =&gt; o.FormatterName = CathedraConsoleFormatter.Name)</c>.
/// </summary>
public sealed class CathedraConsoleFormatter : ConsoleFormatter
{
    public const string FormatterName = "cathedra";

    public CathedraConsoleFormatter() : base(FormatterName)
    {
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (message is null && logEntry.Exception is null)
            return;

        var correlationId = ExtractCorrelationId(scopeProvider);

        var sb = new StringBuilder();
        sb.Append(ShortLevel(logEntry.LogLevel)).Append(": ");
        sb.Append(logEntry.Category).Append('[').Append(logEntry.EventId.Id).Append(']');
        sb.AppendLine();

        sb.Append("      ");
        if (correlationId is not null)
            sb.Append("[cid:").Append(correlationId).Append("] ");
        sb.Append(message);

        if (logEntry.Exception is not null)
        {
            sb.AppendLine();
            sb.Append(logEntry.Exception);
        }

        textWriter.WriteLine(sb.ToString());
    }

    private static string? ExtractCorrelationId(IExternalScopeProvider? scopeProvider)
    {
        if (scopeProvider is null)
            return null;

        string? found = null;
        scopeProvider.ForEachScope((scope, _) =>
        {
            switch (scope)
            {
                case IReadOnlyDictionary<string, object> dict
                    when dict.TryGetValue(CorrelationId.ItemKey, out var v) && v is not null:
                    found = v.ToString();
                    break;
                case IEnumerable<KeyValuePair<string, object>> pairs:
                    foreach (var kvp in pairs)
                        if (kvp.Key == CorrelationId.ItemKey && kvp.Value is not null)
                            found = kvp.Value.ToString();
                    break;
            }
        }, (object?)null);

        return found;
    }

    private static string ShortLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => "trce",
        LogLevel.Debug => "dbug",
        LogLevel.Information => "info",
        LogLevel.Warning => "warn",
        LogLevel.Error => "fail",
        LogLevel.Critical => "crit",
        _ => "info",
    };
}
