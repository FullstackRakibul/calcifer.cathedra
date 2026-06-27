using Microsoft.AspNetCore.Http;

namespace Calcifer.Cathedra.Http;

public static class CorrelationId
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    /// <summary>The id set by RequestLoggingMiddleware, or the framework trace id as a fallback.</summary>
    public static string GetCorrelationId(this HttpContext context)
    {
        if (context.Items.TryGetValue(ItemKey, out var v) && v is string s && !string.IsNullOrEmpty(s))
            return s;
        return context.TraceIdentifier;
    }
}
