namespace Calcifer.Cathedra.Persistence;

/// <summary>
/// Ambient identity of the caller for the current request, used to stamp audit columns. The host
/// supplies an implementation (e.g. from the authenticated principal); the sample registers an
/// anonymous one so the core runs before Auth exists.
/// </summary>
public interface ICurrentUser
{
    /// <summary>A stable identifier for the user, or <c>null</c> when unauthenticated.</summary>
    string? UserId { get; }

    /// <summary>A display name for audit trails, or <c>null</c> when unauthenticated.</summary>
    string? UserName { get; }

    bool IsAuthenticated { get; }
}
