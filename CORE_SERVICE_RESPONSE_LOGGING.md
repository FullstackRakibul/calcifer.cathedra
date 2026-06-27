# Cathedra Core Service — Exception Handler + Logging + Response Envelope

> Building the **core** (the boxed center of your sketch: module loader, contract, shared kernel) as
> a runnable service with three cross-cutting concerns, all owned by the kernel so every future
> module gets them for free:
>
> 1. **Standardized response** — a single `ApiResponse<T>` envelope every endpoint returns.
> 2. **Logging** — `ILogger<T>` plus a request-logging middleware.
> 3. **Global exception handler** — any unhandled exception becomes the same envelope shape.
>
> The thread that unifies all three is a **correlation id**: one id per request, stamped onto every
> log line, echoed in the `X-Correlation-ID` response header, and embedded in both success and error
> response bodies. That's what turns three features into one coherent system.
>
> This supersedes the earlier raw-core note. `PublicModule` stays as the demo that proves the core.

---

## 1. Updated kernel folder structure

```
src/Calcifer.Cathedra/                          ← the core service
├── Diagnostics/
│   └── GlobalExceptionHandler.cs                ← exception → ApiResponse envelope
├── Http/
│   ├── ApiError.cs                              ← { code, message }
│   ├── ApiResponse.cs                           ← the response envelope + helpers
│   ├── ApiResults.cs                            ← Result<T> → IResult (envelope + status)
│   └── CorrelationId.cs                         ← header/key constants + HttpContext accessor
├── Middleware/
│   └── RequestLoggingMiddleware.cs              ← correlation id + request/response logging
├── Domain/
│   ├── BaseEntity.cs
│   ├── IAuditable.cs
│   ├── ISoftDelete.cs
│   └── Result.cs
├── Logging/
│   ├── ILogWriter.cs                            (custom logger — optional alongside ILogger)
│   └── LogWriter.cs
├── Modules/
│   ├── IModule.cs
│   ├── IModuleDescriptor.cs
│   ├── IModuleRegistry.cs
│   ├── ModuleRegistry.cs
│   ├── ModuleLoader.cs
│   ├── CathedraOptions.cs
│   └── CathedraBootstrapper.cs                  ← EDIT: wire all three concerns
└── Persistence/
    ├── ICurrentUser.cs
    ├── CathedraDbContextBase.cs
    └── CathedraDbContext.cs
```

Two new folders in the kernel: `Http/` (the response envelope and helpers) and `Middleware/` (the
request-logging/correlation middleware). `Diagnostics/` holds the exception handler.

---

## 2. The response envelope

Every endpoint in the whole system returns this one shape, success or failure. Consistency here is
what makes the API predictable for any client.

**`Http/ApiError.cs`**
```csharp
namespace Calcifer.Cathedra.Http;

/// <summary>A machine-readable error code plus a human-readable message.</summary>
public sealed record ApiError(string Code, string Message);
```

**`Http/ApiResponse.cs`**
```csharp
namespace Calcifer.Cathedra.Http;

/// <summary>
/// The uniform response envelope for the entire platform. Success carries Data; failure carries
/// Error. CorrelationId ties the response to its log lines and the X-Correlation-ID header.
/// </summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public ApiError? Error { get; init; }
    public string? CorrelationId { get; set; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string code, string message) =>
        new() { Success = false, Error = new ApiError(code, message) };
}
```

---

## 3. Correlation id accessor

**`Http/CorrelationId.cs`**
```csharp
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
```

---

## 4. Result → response mapping

These helpers let endpoints return the envelope in one line, and convert a `Result<T>` (your
business-failure type) into the right envelope with the right HTTP status.

> Assumes the `Result` from `IMPLEMENTATION_PLAN.md` §3.4 exposes `IsSuccess`, `Value`, and
> `Error` (an `ApiError`-shaped `Code`/`Message`). Align the member names if yours differ.

**`Http/ApiResults.cs`**
```csharp
using Calcifer.Cathedra.Domain;
using Microsoft.AspNetCore.Http;

namespace Calcifer.Cathedra.Http;

public static class ApiResults
{
    /// <summary>Wrap a value as a successful envelope (200).</summary>
    public static IResult Ok<T>(HttpContext ctx, T data, string? message = null)
    {
        var resp = ApiResponse<T>.Ok(data, message);
        resp.CorrelationId = ctx.GetCorrelationId();
        return Microsoft.AspNetCore.Http.Results.Json(resp, statusCode: StatusCodes.Status200OK);
    }

    /// <summary>Convert a Result&lt;T&gt; into an enveloped response with a mapped status code.</summary>
    public static IResult From<T>(HttpContext ctx, Result<T> result,
        int successStatus = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            var ok = ApiResponse<T>.Ok(result.Value!);
            ok.CorrelationId = ctx.GetCorrelationId();
            return Microsoft.AspNetCore.Http.Results.Json(ok, statusCode: successStatus);
        }

        var fail = ApiResponse<T>.Fail(result.Error!.Code, result.Error.Message);
        fail.CorrelationId = ctx.GetCorrelationId();
        return Microsoft.AspNetCore.Http.Results.Json(fail, statusCode: MapStatus(result.Error.Code));
    }

    /// <summary>Default code→status convention. Modules can rely on these suffixes/keywords.</summary>
    public static int MapStatus(string code) => code switch
    {
        _ when code.EndsWith("NOT_FOUND")                                   => StatusCodes.Status404NotFound,
        _ when code.EndsWith("TAKEN") || code.EndsWith("CONFLICT")          => StatusCodes.Status409Conflict,
        _ when code.Contains("UNAUTH") || code.Contains("CREDENTIALS")
               || code.Contains("REFRESH")                                  => StatusCodes.Status401Unauthorized,
        _ when code.Contains("FORBIDDEN")                                   => StatusCodes.Status403Forbidden,
        _                                                                   => StatusCodes.Status400BadRequest,
    };
}
```

---

## 5. Request logging middleware (correlation + logging in one place)

This middleware establishes the correlation id, echoes it in the response header, opens a logging
scope so **every** log line in the request automatically carries the id, and logs the request start
and completion with status and elapsed time.

**`Middleware/RequestLoggingMiddleware.cs`**
```csharp
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
```

---

## 6. Global exception handler (now returns the envelope)

Same `IExceptionHandler` approach as before, but it emits the `ApiResponse` envelope so errors look
exactly like every other response — with the same correlation id, and the real message only in
Development.

**`Diagnostics/GlobalExceptionHandler.cs`**
```csharp
using Calcifer.Cathedra.Http;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Calcifer.Cathedra.Diagnostics;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx, Exception exception, CancellationToken ct)
    {
        var correlationId = ctx.GetCorrelationId();

        _logger.LogError(exception,
            "Unhandled exception. CorrelationId={CorrelationId} Method={Method} Path={Path}",
            correlationId, ctx.Request.Method, ctx.Request.Path);

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
```

---

## 7. Wire all three in the bootstrapper

**In `AddCathedra(...)`**, register the exception handler (and ProblemDetails, which avoids a
configuration quirk with `UseExceptionHandler()` even though we write our own body):

```csharp
using Calcifer.Cathedra.Diagnostics;

// inside AddCathedra, with the other registrations:
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
```

**In `UseCathedraAsync(...)`**, order matters. The logging middleware goes **outermost** so it sets
the correlation id first and logs the *final* status (including 500s the handler produces); the
exception handler sits just inside it to catch everything from the endpoints:

```csharp
using Calcifer.Cathedra.Middleware;

public static async Task<WebApplication> UseCathedraAsync(
    this WebApplication app, CancellationToken ct = default)
{
    app.UseMiddleware<RequestLoggingMiddleware>();  // 1. outermost: correlation id + logging
    app.UseExceptionHandler();                      // 2. catch endpoint exceptions -> envelope

    var registry = app.Services.GetRequiredService<IModuleRegistry>();
    foreach (var m in registry.Modules)
        m.MapEndpoints(app);                        // 3. module endpoints

    using var scope = app.Services.CreateScope();
    foreach (var m in registry.Modules)
        await m.InitializeAsync(scope.ServiceProvider, ct);
    foreach (var m in registry.Modules)
        await m.StartAsync(scope.ServiceProvider, ct);

    return app;
}
```

> Why this order: registration order is outer-to-inner. With logging outermost, the correlation id
> is set before anything else runs (so the exception handler can read it), and the logging
> middleware's `finally` runs *after* the exception handler has set the 500 — so the completion log
> reports the true status. Setting a response header doesn't start the response body, so the handler
> can still write the envelope afterward.

---

## 8. PublicModule endpoints return the envelope

Update `PublicEndpoints` so the demo module exercises the response shape. Endpoints take
`HttpContext` to stamp the correlation id via the helper.

**`Calcifer.Public/Endpoints/PublicEndpoints.cs`**
```csharp
using Calcifer.Cathedra.Http;
using Calcifer.Public.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Calcifer.Public.Endpoints;

internal static class PublicEndpoints
{
    public static IEndpointRouteBuilder MapPublicEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/public").WithTags("Public");

        group.MapGet("/welcome", (HttpContext ctx, IPublicService svc) =>
            ApiResults.Ok(ctx, svc.GetWelcome()));

        group.MapGet("/time", (HttpContext ctx, IPublicService svc) =>
            ApiResults.Ok(ctx, new { utc = svc.GetServerTimeUtc() }));

        // Deliberate failure to prove the global exception handler returns the same envelope.
        group.MapGet("/boom", () =>
        {
            throw new InvalidOperationException("Deliberate failure to test the global exception handler.");
        });

        return routes;
    }
}
```

`PublicModule.cs`, `IPublicService`/`PublicService` (injecting `ILogger<PublicService>`), and
`WelcomeDto` are unchanged from the previous raw-core build.

---

## 9. Run and verify the three concerns

Set `Calcifer.Sample.Api` as startup, press **Ctrl+F5**.

1. **Module discovery + logging.** Console:
   ```
   Cathedra: discovered 1 module — Public (v0.1.0)
   ```

2. **Standardized success response.** `GET /api/v1/public/welcome`:
   ```json
   {
     "success": true,
     "data": { "service": "Calcifer", "message": "Public module is alive.", "timestampUtc": "2026-..." },
     "message": null,
     "error": null,
     "correlationId": "9f2c1a...",
     "timestampUtc": "2026-..."
   }
   ```
   **Verify:** the body has the envelope shape; the response has an `X-Correlation-ID` header whose
   value equals the body's `correlationId`; the console logged
   `HTTP GET /api/v1/public/welcome started` and `... -> 200 in Nms`, both carrying that id.

3. **Standardized error response.** `GET /api/v1/public/boom`:
   ```json
   {
     "success": false,
     "data": null,
     "message": null,
     "error": { "code": "INTERNAL_ERROR", "message": "Deliberate failure to test the global exception handler." },
     "correlationId": "9f2c1a...",
     "timestampUtc": "2026-..."
   }
   ```
   **Verify:** HTTP 500 with the **same envelope shape** (not a stack-trace page); the console logged
   the full exception at Error level *and* the completion line `... -> 500 in Nms`, all sharing the
   correlation id. Switch the environment to Production and `error.message` becomes the generic
   message while the real one stays in the logs.

If all three pass, your core service is done: a uniform response on every path, request-scoped
logging, and exception handling — all correlated by one id, all owned by the kernel.

---

## 10. What the core now guarantees every module

- **One response shape.** Any module returning `ApiResults.Ok(ctx, data)` or `ApiResults.From(ctx,
  result)` produces the identical envelope — no module reinvents responses.
- **Free logging with correlation.** Inject `ILogger<T>` anywhere; every line is automatically tagged
  with the request's correlation id via the middleware scope.
- **Uniform failure.** An unhandled exception in any module becomes the same envelope with the same
  id — no leaked stack traces in production.
- **Traceability end to end.** The client gets the id in the `X-Correlation-ID` header; that id
  appears in the response body and in every server log line for that request, so a support ticket
  quoting one id pinpoints the exact logs.

This is the center box of your sketch, built and runnable. Auth, RBAC, and Licensing now plug into a
core that already handles responses, logging, and errors — they only add their own schema, services,
and endpoints.
