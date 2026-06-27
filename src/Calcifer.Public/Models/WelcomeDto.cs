namespace Calcifer.Public.Models;

/// <summary>
/// The payload returned by the welcome endpoint. Serialized as the <c>data</c> of the platform
/// envelope, it proves a module's own shape rides inside the shared <c>ApiResponse&lt;T&gt;</c>.
/// </summary>
public sealed record WelcomeDto(string Service, string Message, DateTime TimestampUtc);
