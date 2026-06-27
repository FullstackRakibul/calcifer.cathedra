using Calcifer.Cathedra.Modules;
using Calcifer.Public.Endpoints;
using Calcifer.Public.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Calcifer.Public;

/// <summary>
/// The demo module that proves the core. It owns nothing cross-cutting — it just registers its
/// service and maps its endpoints, and in return gets the platform envelope, request-scoped logging
/// with a correlation id, and uniform exception handling from the kernel.
/// </summary>
public sealed class PublicModule : IModule
{
    public string Name => "Public";
    public string Version => "0.1.0";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IPublicService, PublicService>();
    }

    public void MapEndpoints(WebApplication app)
    {
        app.MapPublicEndpoints();
    }

    public Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var logger = services.GetRequiredService<ILogger<PublicModule>>();
        logger.LogInformation("Public module initialized");
        return Task.CompletedTask;
    }
}
