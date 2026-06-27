using Calcifer.Cathedra.Logging;
using Calcifer.Public.Models;
using Microsoft.Extensions.Logging;

namespace Calcifer.Public.Services;

/// <summary>
/// Default <see cref="IPublicService"/>. Injects <c>ILogger&lt;PublicService&gt;</c> to show that any
/// module service logging through the standard abstraction automatically gets the request's
/// correlation id (stamped by RequestLoggingMiddleware's scope) — no extra plumbing in the module.
/// Also injects <see cref="ILogWriter"/> to demonstrate the file logger: those lines land in
/// logs/cathedra/ with the correlation id and client IP attached.
/// </summary>
public sealed class PublicService : IPublicService
{
    private readonly ILogger<PublicService> _logger;
    private readonly ILogWriter<PublicService> _logWriter;

    public PublicService(ILogger<PublicService> logger, ILogWriter<PublicService> logWriter)
    {
        _logger = logger;
        _logWriter = logWriter;
    }

    public WelcomeDto GetWelcome()
    {
		_logger.LogInformation("Building welcome payload");
        _logWriter.Info("Building welcome payload for {0}", "anonymous");
        return new WelcomeDto("Calcifer", "Public module is alive.", DateTime.Now);
    }

    public DateTime GetServerTimeUtc()
    {
        _logger.LogInformation("Reporting server time");
        _logWriter.Info("Reporting server time");
        return DateTime.Now;
    }
}
