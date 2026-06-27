using Calcifer.Cathedra.Http;
using Calcifer.Cathedra.Logging;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Calcifer.Cathedra.Diagnostics;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;
    private readonly ILogWriter? _logWriter;

    // ILogWriter is optional: it is only registered when the host calls AddFileLogging(), so the
    // kernel never hard-depends on the file logger. When present, exceptions are recorded to the
    // file (with full stack trace) in addition to the standard ILogger pipeline.
    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment env,
        ILogWriter? logWriter = null)
    {
        _logger = logger;
        _env = env;
        _logWriter = logWriter;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx, Exception exception, CancellationToken ct)
    {
        var correlationId = ctx.GetCorrelationId();

        _logger.LogError(exception,
            "Unhandled exception. CorrelationId={CorrelationId} Method={Method} Path={Path}",
            correlationId, ctx.Request.Method, ctx.Request.Path);

        _logWriter?.Error(exception,
            "Unhandled exception. Method={0} Path={1}", ctx.Request.Method, ctx.Request.Path);

        var response = ApiResponse<object?>.Fail(
            "INTERNAL_ERROR",
            _env.IsDevelopment()
                ? exception.Message
                : "An internal error occurred while processing your request.");
        response.CorrelationId = correlationId;

        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await ctx.Response.WriteAsJsonAsync(response, ct);
        return true;
    }
}
