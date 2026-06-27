# Calcifer.Cathedra — Core Service

The **core** of the Cathedra platform: a runnable, modular service whose kernel owns three
cross-cutting concerns so every future module gets them for free.

1. **Standardized response** — a single `ApiResponse<T>` envelope every endpoint returns.
2. **Logging** — `ILogger<T>` plus a request-logging middleware.
3. **Global exception handler** — any unhandled exception becomes the same envelope shape.

All three are unified by a **correlation id**: one id per request, stamped on every log line, echoed
in the `X-Correlation-ID` response header, and embedded in both success and error response bodies.

Built and verified against **.NET 10**. See [CORE_SERVICE_RESPONSE_LOGGING.md](CORE_SERVICE_RESPONSE_LOGGING.md)
for the full design note.

## Projects

| Project | Type | Role |
| --- | --- | --- |
| `src/Calcifer.Cathedra` | class library | The kernel: response envelope, logging, exception handling, module system, persistence base. |
| `src/Calcifer.Public` | class library | The demo module that proves the core (`/api/v1/public/*`). |
| `src/Calcifer.Sample.Api` | web app | The host that boots the kernel and discovers modules. |

### Kernel layout (`src/Calcifer.Cathedra`)

```
Diagnostics/   GlobalExceptionHandler.cs        exception -> ApiResponse envelope
Http/          ApiError, ApiResponse,           the envelope + helpers + correlation accessor
               ApiResults, CorrelationId
Middleware/    RequestLoggingMiddleware.cs       correlation id + request/response logging
Domain/        BaseEntity, IAuditable,          shared entity primitives + Result<T>
               ISoftDelete, Result
Logging/       ILogWriter, LogWriter,           optional logger + console formatter that prints
               CathedraConsoleFormatter, ...     [cid:<id>] inline on every line
Modules/       IModule, IModuleDescriptor,       the module contract, loader, registry, options,
               IModuleRegistry, ModuleRegistry,  and the AddCathedra / UseCathedraAsync bootstrapper
               ModuleLoader, CathedraOptions,
               CathedraBootstrapper
Persistence/   ICurrentUser,                     audit + soft-delete DbContext base
               CathedraDbContextBase,
               CathedraDbContext
```

## Run it

```bash
dotnet run --project src/Calcifer.Sample.Api
```

On startup the console logs module discovery:

```
Cathedra: discovered 1 module — Public (v0.1.0)
```

### API explorer (Swagger)

In **Development** the kernel serves an OpenAPI document and an interactive Swagger UI. Every
module's endpoints appear automatically (no per-module wiring):

- Swagger UI: `http://localhost:<port>/swagger`
- OpenAPI JSON: `http://localhost:<port>/openapi/v1.json`

Launching from Visual Studio opens `/swagger` automatically (`launchUrl` in `launchSettings.json`).
The explorer is gated to Development; it is not exposed in Production.

### Endpoints

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/v1/public/welcome` | Success envelope carrying a `WelcomeDto`. |
| GET | `/api/v1/public/time` | Success envelope carrying the server UTC time. |
| GET | `/api/v1/public/boom` | Throws — proves the global exception handler returns the same envelope. |

### Verify the three concerns

```bash
# Success: enveloped body + matching X-Correlation-ID header
curl -i http://localhost:<port>/api/v1/public/welcome

# Error: HTTP 500 with the SAME envelope shape (not a stack-trace page)
curl -i http://localhost:<port>/api/v1/public/boom

# Correlation id is honored if you supply one
curl -i -H "X-Correlation-ID: my-trace-1" http://localhost:<port>/api/v1/public/welcome
```

In **Development** the error body shows the real exception message; in **Production**
(`ASPNETCORE_ENVIRONMENT=Production`) it becomes a generic message while the real one stays in the
logs. Console lines print the request's id inline, e.g.
`[cid:my-trace-1] HTTP GET /api/v1/public/welcome -> 200 in 4ms`.

## File logging

Alongside the console, the kernel can write structured, daily-rotated log files. It is opt-in: the
host calls `AddFileLogging(...)`, which points `ILogWriter` at a file sink (composed with the console
by default).

### Activate it (`Program.cs`)

```csharp
// Call AFTER builder.AddCathedra(...) so it replaces the kernel's default console-only ILogWriter.
builder.Services.AddFileLogging(builder.Configuration);
```

Options (all optional; defaults shown) in `appsettings.json`:

```json
"Cathedra": {
  "FileLogging": {
    "LogPath": "logs/cathedra",
    "LogLevel": "Information",
    "MaxFileSizeMB": 10,
    "RetainDays": 30,
    "UseDailyRotation": true
  }
}
```

`AddFileLogging` overloads: `configure` (code-based option overrides), `alsoLogToConsole` (false =
file only), and `category` (default module name).

### Where logs go

`logs/cathedra/` under the app base directory (`bin/.../net10.0/logs/cathedra/` when running locally),
or an absolute `LogPath`. Files are `cathedra-YYYY-MM-DD.log`, with a numbered part
(`cathedra-YYYY-MM-DD.001.log`) once a file passes `MaxFileSizeMB`. Files older than `RetainDays`
are purged at startup and at each day boundary.

### Line format

```
[2026-06-27T17:51:43.160Z] [INFO] [cid:typed-001] [ip:::1] [module:PublicService] Building welcome payload for anonymous
[2026-06-27T17:51:43.265Z] [ERROR] [cid:838be4...] [ip:::1] [module:Cathedra] Unhandled exception. Method=GET Path=/api/v1/public/boom
System.InvalidOperationException: Deliberate failure to test the global exception handler.
   at ...
```

UTC ISO-8601 timestamp · level · correlation id · client IP · module · message; exceptions append
type + message + full stack trace on following lines. Correlation id and IP come from the current
`HttpContext` (via `IHttpContextAccessor`); outside a request they fall back to `N/A`.

### Using it from code

```csharp
// Auto-categorized: lines are stamped [module:MyService]
public MyService(ILogWriter<MyService> log) => _log = log;
_log.Info("User {0} logged in", userId);
_log.Error(ex, "Failed to assign permission {0}", permissionId);

// Or the non-generic ILogWriter (uses the default category)
public OtherService(ILogWriter log) => _log = log;
```

Writes are non-blocking: lines are queued and flushed by a single background thread, so concurrent
requests never block on disk or corrupt the file. The sink flushes on host shutdown. `ILogger<T>`
is unchanged — file logging only governs the `ILogWriter` abstraction.

## What the core guarantees every module

- **One response shape** via `ApiResults.Ok(ctx, data)` / `ApiResults.From(ctx, result)`.
- **Free logging with correlation** — inject `ILogger<T>` anywhere; every line is tagged with the
  request's id via the middleware scope.
- **Uniform failure** — an unhandled exception becomes the same envelope with the same id; no leaked
  stack traces in Production.
- **Traceability end to end** — the client's `X-Correlation-ID` matches the body's `correlationId`
  and every server log line for that request.

## Adding a module

1. Reference `Calcifer.Cathedra` and add `<FrameworkReference Include="Microsoft.AspNetCore.App" />`.
2. Implement `IModule` (`Name`, `Version`, `ConfigureServices`, `MapEndpoints`, optional
   `InitializeAsync`/`StartAsync`).
3. Point the host at its assembly: `builder.AddCathedra(o => o.AddModuleAssemblyContaining<YourModule>());`
   — discovery and lifecycle are automatic.

## Security

See [SECURITY.md](SECURITY.md) for the vulnerability reporting process and security-relevant design
notes (Production error handling, correlation IDs, file logging, and the no-auth default).

## License

Licensed under the [MIT License](LICENSE) © 2026 Rakibul Hasan.
