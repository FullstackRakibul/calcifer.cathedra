using Calcifer.Cathedra.Persistence;

namespace Calcifer.Sample.Api;

/// <summary>
/// Stand-in <see cref="ICurrentUser"/> for the sample host. The core needs an identity to stamp
/// audit columns, but Auth doesn't exist yet — so this reports an unauthenticated "system" user.
/// When the Auth module ships, the host swaps this for one backed by the authenticated principal.
/// </summary>
public sealed class AnonymousCurrentUser : ICurrentUser
{
    public string? UserId => "rh.rabbi73";
    public string? UserName => "TERMINALUSER";
    public bool IsAuthenticated => true;
}
