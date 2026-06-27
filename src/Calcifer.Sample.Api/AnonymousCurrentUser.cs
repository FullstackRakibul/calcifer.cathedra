using Calcifer.Cathedra.Persistence;

namespace Calcifer.Sample.Api;

/// <summary>
/// Stand-in <see cref="ICurrentUser"/> for the sample host. The core needs an identity to stamp
/// audit columns, but Auth doesn't exist yet — so this reports an unauthenticated "system" user.
/// When the Auth module ships, the host swaps this for one backed by the authenticated principal.
/// </summary>
public sealed class AnonymousCurrentUser : ICurrentUser
{
    public string? UserId => null;
    public string? UserName => "system";
    public bool IsAuthenticated => false;
}
