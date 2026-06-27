using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Calcifer.Cathedra.Logging;

public static class LoggingBuilderExtensions
{
    /// <summary>
    /// Replace the default console logger with <see cref="CathedraConsoleFormatter"/>, which prints
    /// the request correlation id inline on every line. Scopes are required for the id to flow, so
    /// this enables them too. Call from the host: <c>builder.Logging.AddCathedraConsole();</c>.
    /// </summary>
    public static ILoggingBuilder AddCathedraConsole(this ILoggingBuilder builder)
    {
        builder.AddConsoleFormatter<CathedraConsoleFormatter, ConsoleFormatterOptions>(options =>
        {
            options.IncludeScopes = true;
        });
        builder.AddConsole(options => options.FormatterName = CathedraConsoleFormatter.FormatterName);
        return builder;
    }
}
