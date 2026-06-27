using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Calcifer.Cathedra.Logging;

public static class FileLoggingServiceCollectionExtensions
{
    /// <summary>
    /// Adds the file-based logger and points <see cref="ILogWriter"/> at it.
    /// <para>
    /// Binds <see cref="FileLogOptions"/> from the <c>Cathedra:FileLogging</c> configuration section
    /// (defaults apply if the section is missing), registers the singleton <see cref="FileLogSink"/>
    /// (the background-thread writer) and <see cref="IHttpContextAccessor"/> (for correlation id and
    /// client IP), then registers <see cref="ILogWriter"/> as either:
    /// </para>
    /// <list type="bullet">
    ///   <item><b>composite</b> (default): writes to the console via <see cref="LogWriter"/>/<c>ILogger</c>
    ///         <i>and</i> to the file, so existing <c>ILogWriter</c> consumers get both; or</item>
    ///   <item><b>file-only</b> (<paramref name="alsoLogToConsole"/> = false).</item>
    /// </list>
    /// <c>ILogger&lt;T&gt;</c> is unaffected — this only governs the <c>ILogWriter</c> abstraction.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configuration">App configuration to bind the options section from. Optional.</param>
    /// <param name="configure">Code-based overrides applied after configuration binding. Optional.</param>
    /// <param name="alsoLogToConsole">When true (default), <c>ILogWriter</c> writes to console + file.</param>
    /// <param name="category">Logger category / module name stamped on file lines and console output.</param>
    public static IServiceCollection AddFileLogging(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        Action<FileLogOptions>? configure = null,
        bool alsoLogToConsole = true,
        string category = "Cathedra")
    {
        // 1) Options: bind from config (if provided), then apply code overrides.
        var optionsBuilder = services.AddOptions<FileLogOptions>();
        if (configuration is not null)
            optionsBuilder.Bind(configuration.GetSection(FileLogOptions.SectionName));
        if (configure is not null)
            optionsBuilder.Configure(configure);

        // 2) Core services. The sink is a singleton (one file writer per process) and disposable,
        //    so the host flushes it on shutdown. HttpContextAccessor enables cid/IP extraction.
        services.AddHttpContextAccessor();
        services.TryAddSingleton<FileLogSink>();

        // 3) A directly-resolvable file writer (for callers who want file-only regardless of the
        //    ILogWriter wiring below).
        services.TryAddSingleton<FileLogWriter>(sp => new FileLogWriter(
            sp.GetRequiredService<FileLogSink>(),
            sp.GetRequiredService<IHttpContextAccessor>(),
            sp.GetRequiredService<IOptions<FileLogOptions>>(),
            category));

        // 4) Point ILogWriter at the file writer, optionally composed with the console writer.
        //    Replace any earlier ILogWriter registration (e.g. the bootstrapper's console default).
        services.RemoveAll<ILogWriter>();
        services.AddSingleton<ILogWriter>(sp =>
        {
            var fileWriter = sp.GetRequiredService<FileLogWriter>();
            if (!alsoLogToConsole)
                return fileWriter;

            var consoleWriter = new LogWriter(
                sp.GetRequiredService<ILoggerFactory>().CreateLogger(category));
            return new CompositeLogWriter(consoleWriter, fileWriter);
        });

        // 5) Category-typed writer: inject ILogWriter<MyService> to get [module:MyService] lines.
        services.Configure<TypedLogWriterOptions>(o => o.AlsoLogToConsole = alsoLogToConsole);
        services.TryAddSingleton(typeof(ILogWriter<>), typeof(TypedLogWriter<>));

        return services;
    }
}
