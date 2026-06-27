using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Calcifer.Cathedra.Modules;

/// <summary>
/// The contract every feature module implements. The kernel discovers modules, registers their
/// services, maps their endpoints, then runs their async lifecycle — so a module is fully
/// self-contained: it adds its own schema, services, and routes and plugs into the core's
/// response/logging/error machinery for free.
/// </summary>
public interface IModule : IModuleDescriptor
{
    /// <summary>
    /// Register the module's services into DI. Called once at startup, before the app is built.
    /// </summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// Map the module's HTTP endpoints. Called after the app is built, inside the request pipeline
    /// wiring. Endpoints use <c>ApiResults</c> so they share the platform envelope.
    /// </summary>
    void MapEndpoints(WebApplication app);

    /// <summary>
    /// One-time async initialization (e.g. apply migrations, seed reference data) run after
    /// endpoints are mapped. The provided scope is request-independent (a startup scope).
    /// </summary>
    Task InitializeAsync(IServiceProvider services, CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>
    /// Async start hook (e.g. warm caches, register background work) run after all modules have
    /// initialized. Default is a no-op.
    /// </summary>
    Task StartAsync(IServiceProvider services, CancellationToken ct = default) => Task.CompletedTask;
}
