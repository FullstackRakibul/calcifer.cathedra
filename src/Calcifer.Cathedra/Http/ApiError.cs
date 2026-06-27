namespace Calcifer.Cathedra.Http;

/// <summary>A machine-readable error code plus a human-readable message.</summary>
public sealed record ApiError(string Code, string Message);
