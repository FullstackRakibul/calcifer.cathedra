using Calcifer.Public.Models;

namespace Calcifer.Public.Services;

/// <summary>The demo module's application service, exposed to its endpoints via DI.</summary>
public interface IPublicService
{
    /// <summary>Returns the welcome payload that proves the module is alive.</summary>
    WelcomeDto GetWelcome();

    /// <summary>Returns the current server time in UTC.</summary>
    DateTime GetServerTimeUtc();
}
