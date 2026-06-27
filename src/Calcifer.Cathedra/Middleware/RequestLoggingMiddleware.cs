using System.Diagnostics;
using Calcifer.Cathedra.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Calcifer.Cathedra.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1) Establish a correlation id: honor an inbound header, else mint one.
        var correlationId =
            context.Request.Headers.TryGetValue(CorrelationId.HeaderName, out var h)
            && !string.IsNullOrWhiteSpace(h)
                ? h.ToString()
                : Guid.NewGuid().ToString("N");

        context.Items[CorrelationId.ItemKey] = correlationId;
        context.Response.Headers[CorrelationId.HeaderName] = correlationId;

        // 2) Every log line within this request inherits the id via the scope.
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        var sw = Stopwatch.StartNew();
        _logger.LogInformation("HTTP {Method} {Path} started",
            context.Request.Method, context.Request.Path);

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            _logger.LogInformation("HTTP {Method} {Path} -> {StatusCode} in {ElapsedMs}ms",
                context.Request.Method, context.Request.Path,
                context.Response.StatusCode, sw.ElapsedMilliseconds);
        }
    }
}
