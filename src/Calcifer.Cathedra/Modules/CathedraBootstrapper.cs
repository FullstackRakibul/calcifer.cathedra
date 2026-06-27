using Calcifer.Cathedra.Diagnostics;
using Calcifer.Cathedra.Logging;
using Calcifer.Cathedra.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Calcifer.Cathedra.Modules;

/// <summary>
/// The kernel's entry points. <see cref="AddCathedra"/> discovers modules and registers the core
/// cross-cutting services (response envelope helpers are static, so what gets registered here is the
/// module registry, each module's services, the global exception handler, and logging support).
/// <see cref="UseCathedraAsync"/> wires the request pipeline in the exact order the three concerns
/// require and runs each module's endpoint mapping and async lifecycle.
/// </summary>
public static class CathedraBootstrapper
{
    /// <summary>
    /// Register the core and all discovered modules. Call once during host setup, e.g.
    /// <c>builder.AddCathedra(o =&gt; o.AddModuleAssemblyContaining&lt;PublicModule&gt;());</c>.
    /// </summary>
    public static WebApplicationBuilder AddCathedra(
        this WebApplicationBuilder builder, Action<CathedraOptions>? configure = null)
    {
        var options = new CathedraOptions();
        configure?.Invoke(options);

        var modules = ModuleLoader.Discover(options);

        // --- Cross-cutting kernel services -------------------------------------------------
        // ProblemDetails is registered because UseExceptionHandler() expects it to be present,
        // even though GlobalExceptionHandler writes our own ApiResponse body.
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        // OpenAPI document generation. Endpoints mapped by any module are discovered automatically,
        // so the explorer always reflects every loaded module's routes.
        builder.Services.AddOpenApi(opt =>
        {
            opt.AddDocumentTransformer((doc, _, _) =>
            {
                doc.Info = new()
                {
                    Title = "Calcifer.Cathedra API",
                    Version = "v1",
                    Description = "Core service: standardized response envelope, request logging, and global "
                                + "exception handling — all correlated by X-Correlation-ID.",
                };
                return Task.CompletedTask;
            });
        });

        // The optional custom logger, categorized to the consuming type.
        builder.Services.AddTransient<ILogWriter>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new LogWriter(loggerFactory.CreateLogger("Calcifer"));
        });

        // --- Module registry + each module's own services ----------------------------------
        foreach (var module in modules)
        {
            builder.Services.AddSingleton(module);          // resolvable as IModule
            module.ConfigureServices(builder.Services);     // module-owned registrations
        }

        builder.Services.AddSingleton<IModuleRegistry, ModuleRegistry>();

        // --- Discovery logging (the spec's expected console line) --------------------------
        LogDiscovery(builder, modules);

        return builder;
    }

    /// <summary>
    /// Build the request pipeline and run module lifecycles. Middleware order is deliberate:
    /// logging is outermost (so it sets the correlation id first and logs the final status),
    /// the exception handler sits just inside it, then module endpoints.
    /// </summary>
    public static async Task<WebApplication> UseCathedraAsync(
        this WebApplication app, CancellationToken ct = default)
    {
        app.UseMiddleware<RequestLoggingMiddleware>();  // 1. outermost: correlation id + logging
        app.UseExceptionHandler();                      // 2. catch endpoint exceptions -> envelope

        var registry = app.Services.GetRequiredService<IModuleRegistry>();
        foreach (var m in registry.Modules)
            m.MapEndpoints(app);                        // 3. module endpoints

        // 4. API explorer: serve the OpenAPI document and a Swagger UI over it (Development only).
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();                           // /openapi/v1.json
            app.UseSwaggerUI(ui =>
            {
                ui.SwaggerEndpoint("/openapi/v1.json", "Calcifer.Cathedra API v1");
                ui.RoutePrefix = "swagger";             // UI at /swagger
            });
        }

        using var scope = app.Services.CreateScope();
        foreach (var m in registry.Modules)
            await m.InitializeAsync(scope.ServiceProvider, ct);
        foreach (var m in registry.Modules)
            await m.StartAsync(scope.ServiceProvider, ct);

        return app;
    }

    private static void LogDiscovery(WebApplicationBuilder builder, IReadOnlyList<IModule> modules)
    {
        // A short-lived logger factory honoring the host's logging config, used only at startup.
        using var loggerFactory = LoggerFactory.Create(b =>
        {
            b.AddConfiguration(builder.Configuration.GetSection("Logging"));
            b.AddConsole();
        });
        var logger = loggerFactory.CreateLogger("Cathedra");

        var count = modules.Count;
        var noun = count == 1 ? "module" : "modules";
        var names = string.Join(", ", modules.Select(m => $"{m.Name} (v{m.Version})"));

        if (count == 0)
            logger.LogInformation("Cathedra: discovered 0 modules");
        else
            logger.LogInformation("Cathedra: discovered {Count} {Noun} — {Modules}", count, noun, names);
    }
}
