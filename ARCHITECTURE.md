# Calcifer.Cathedra — Architecture Summary

> **Scope note (read first).** This document describes the codebase **as it exists today** (July 2026).
> The project brief for "Calcifer.Api" mentions Auth, Rbac, and Licensing modules, JWT authentication,
> license-based feature gating, permission caching, and a test suite. **None of those exist in this
> repository yet.** What exists is the *kernel* those features are designed to plug into: a modular
> monolith seed with a module system, a uniform response envelope, correlated logging, a global
> exception handler, and an audit/soft-delete persistence base. Where the planned features are
> relevant, they are called out explicitly as **roadmap**, not as implemented behavior.

---

## 1. Executive Summary

Calcifer.Cathedra is a **modular monolith kernel** for .NET 10. It is not an application so much as
a *platform seed*: a class library (`Calcifer.Cathedra`) that owns the cross-cutting concerns every
future feature module needs — one response shape, one logging discipline, one failure mode — plus a
plugin contract (`IModule`) through which feature modules self-register their services, endpoints,
and lifecycle. A demo module (`Calcifer.Public`) and a thin host (`Calcifer.Sample.Api`) prove the
kernel works end to end.

The unifying design idea is the **correlation id**: every request gets one (honored from the
`X-Correlation-ID` header or minted), and it appears in every log line (via `ILogger` scopes and the
custom console/file formatters), in the response header, and inside both success and error response
bodies. A client, a support engineer, and a log file can all agree on which request they are talking
about. The second design idea is that **modules pay nothing for infrastructure**: implementing
`IModule` and returning `ApiResults.Ok(ctx, data)` buys a module the envelope, request logging,
exception handling, OpenAPI listing, audit stamping, and soft delete for free.

The result is deliberately small (~30 source files, 3 projects) and deliberately biased toward
convention: it trades the flexibility of "every module does it its own way" for the guarantee that
every module behaves identically at the HTTP boundary.

## 2. Architecture Classification

**Label: Modular Monolith (kernel + plugin modules) with Clean Architecture influences.**

- *Modular monolith*: one process, one deployable, but features live in separate assemblies wired
  through a discovery mechanism ([ModuleLoader.cs](src/Calcifer.Cathedra/Modules/ModuleLoader.cs)),
  not through direct references between features.
- *Clean Architecture influences*: a `Result<T>` type separates business outcomes from HTTP concerns,
  domain primitives (`BaseEntity`, `IAuditable`, `ISoftDelete`) are isolated in `Domain/`, and
  services are interface-driven (`IPublicService`, `ILogWriter`, `ICurrentUser`). It is *not* full
  Clean Architecture: the kernel intentionally references ASP.NET Core (`HttpContext`, `IResult`,
  `WebApplication`) rather than abstracting it away, and modules do too.
- It is also a *microkernel (plugin) architecture* in the classic sense: a small core with a
  well-defined extension contract, and all features delivered as plugins.

## 3. Project Structure Overview

```
calcifer.cathedra/
├── Calcifer.slnx                       # solution
├── README.md / SECURITY.md / LICENSE   # docs, vuln-reporting policy, MIT license
└── src/
    ├── Calcifer.Cathedra/              # ★ THE KERNEL (class library)
    │   ├── Diagnostics/
    │   │   └── GlobalExceptionHandler.cs   # IExceptionHandler → ApiResponse envelope, env-aware messages
    │   ├── Http/
    │   │   ├── ApiResponse.cs              # the uniform envelope (Success/Data/Error/CorrelationId/Timestamp)
    │   │   ├── ApiError.cs                 # code + message error payload
    │   │   ├── ApiResults.cs               # Ok()/From() helpers + error-code → HTTP-status convention
    │   │   └── CorrelationId.cs            # header/item-key constants + HttpContext accessor
    │   ├── Middleware/
    │   │   └── RequestLoggingMiddleware.cs # mints/honors correlation id, logs start/finish + duration
    │   ├── Domain/
    │   │   ├── BaseEntity.cs               # shared entity primitive
    │   │   ├── IAuditable.cs / ISoftDelete.cs
    │   │   └── Result.cs                   # Result<T> business-outcome type (implicit conversions)
    │   ├── Logging/
    │   │   ├── ILogWriter.cs / LogWriter / TypedLogWriter<T>   # optional logging abstraction
    │   │   ├── CathedraConsoleFormatter.cs                     # prints [cid:<id>] inline on console lines
    │   │   ├── FileLogSink / FileLogWriter / FileLogOptions    # opt-in daily-rotated file logging
    │   │   └── CompositeLogWriter.cs                           # console + file fan-out
    │   ├── Modules/
    │   │   ├── IModule.cs / IModuleDescriptor.cs   # the plugin contract
    │   │   ├── ModuleLoader.cs                     # reflection-based discovery, dedupe, ordering
    │   │   ├── IModuleRegistry.cs / ModuleRegistry.cs
    │   │   ├── CathedraOptions.cs                  # which assemblies/instances to load
    │   │   └── CathedraBootstrapper.cs             # AddCathedra() / UseCathedraAsync() entry points
    │   └── Persistence/
    │       ├── ICurrentUser.cs                     # identity abstraction for audit stamping
    │       ├── CathedraDbContextBase.cs            # audit columns, soft delete, global query filter
    │       └── CathedraDbContext.cs                # shared concrete context
    │
    ├── Calcifer.Public/                # ★ THE DEMO MODULE (class library)
    │   ├── PublicModule.cs                 # IModule implementation
    │   ├── Endpoints/PublicEndpoints.cs    # /api/v1/public/{welcome,multiply,time,boom}
    │   ├── Services/IPublicService.cs / PublicService.cs
    │   └── Models/WelcomeDto.cs
    │
    └── Calcifer.Sample.Api/            # ★ THE HOST (web app, ~37-line Program.cs)
        ├── Program.cs                      # DbContext + ICurrentUser + AddCathedra + AddFileLogging
        ├── AnonymousCurrentUser.cs         # placeholder identity until an Auth module exists
        └── appsettings*.json               # Logging + Cathedra:FileLogging config
```

Note the dependency direction: **modules and the host reference the kernel; the kernel references
nothing of theirs**. `Calcifer.Public` has exactly one project reference (the kernel). The host
references kernel + module purely so the module assembly is loadable — discovery itself is by
reflection, not by compile-time wiring.

## 4. Technology Stack

| Concern | Technology | Where |
| --- | --- | --- |
| Runtime / language | .NET 10, C# (nullable + implicit usings enabled) | all three `.csproj` files |
| Web framework | ASP.NET Core Minimal APIs via `FrameworkReference Microsoft.AspNetCore.App` | kernel + module |
| Persistence | Entity Framework Core 10.0.0 (`Microsoft.EntityFrameworkCore`) | kernel `Persistence/` |
| Database (sample) | EF Core **In-Memory** provider 10.0.0 — no real database is configured yet | `Calcifer.Sample.Api.csproj` |
| API docs | `Microsoft.AspNetCore.OpenApi` 10.0.0 (built-in doc gen) + `Swashbuckle.AspNetCore.SwaggerUI` 9.0.6 (UI only) | `CathedraBootstrapper.cs` |
| Logging | `Microsoft.Extensions.Logging` + custom console formatter + custom async file sink (no Serilog/NLog) | kernel `Logging/` |
| Error handling | ASP.NET Core `IExceptionHandler` + `AddProblemDetails()` (registered only because `UseExceptionHandler` requires it) | `Diagnostics/` |
| Testing | **None present** — no test projects exist (roadmap) | — |
| Containers / CI-CD | **None present** — no Dockerfile, no pipeline definitions (roadmap) | — |
| Auth | **None present** — `AnonymousCurrentUser` is an explicit placeholder ("anonymous until the Auth module exists") | `Calcifer.Sample.Api/AnonymousCurrentUser.cs` |

## 5. Key Design Patterns

| Pattern | Where | Notes |
| --- | --- | --- |
| **Microkernel / Plugin** | `IModule`, `ModuleLoader`, `CathedraBootstrapper` | Core discovers plugins by scanning assemblies for concrete `IModule` types with parameterless constructors; explicit registration (`AddModule<T>()`) bypasses scanning. |
| **Template Method** | [CathedraDbContextBase.cs](src/Calcifer.Cathedra/Persistence/CathedraDbContextBase.cs) | Overrides `SaveChanges`/`SaveChangesAsync` to stamp audit columns and convert deletes to soft deletes before delegating to EF. |
| **Result / Railway-oriented** | [Result.cs](src/Calcifer.Cathedra/Domain/Result.cs) | `Result<T>` with implicit conversions from `T` and `ApiError`; `ApiResults.From()` maps error-code suffixes (`NOT_FOUND`→404, `TAKEN`/`CONFLICT`→409, `UNAUTH`→401…) to status codes, so services never touch HTTP. |
| **Composite** | `CompositeLogWriter` | Fans `ILogWriter` calls out to console + file sinks. |
| **Options** | `CathedraOptions`, `FileLogOptions` | Fluent configuration of discovery; file logging bound from `Cathedra:FileLogging`. |
| **Decorator-ish middleware pipeline** | `CathedraBootstrapper.UseCathedraAsync` | Deliberate ordering: logging outermost → exception handler → module endpoints; documented in code as intentional. |
| **Registry** | `ModuleRegistry` / `IModuleRegistry` | Holds the ordered module list for pipeline wiring and lifecycle. |
| **Null-object** | `AnonymousCurrentUser` | Satisfies `ICurrentUser` until a real Auth module exists. |
| **Extension-method bootstrapping** | `AddCathedra()` / `UseCathedraAsync()` / `MapPublicEndpoints()` / `AddFileLogging()` | The idiomatic ASP.NET Core composition style; the host's `Program.cs` is 37 lines. |

Patterns the brief asked about that are **not** used: there is **no Repository or Unit of Work
layer** — modules are expected to use the `DbContext` (base) directly; EF Core's `DbContext` already
is a unit of work. There are **no Controllers** anywhere; Minimal APIs are the only HTTP surface.

## 6. Architectural Layers

The kernel is organized by *concern* rather than by strict onion layers:

- **Presentation** — Minimal API endpoint mapping ([PublicEndpoints.cs](src/Calcifer.Public/Endpoints/PublicEndpoints.cs)),
  route group `/api/v1/public` with `.WithName/.WithSummary/.WithTags` OpenAPI metadata. All handlers
  return through `ApiResults`, so the envelope is impossible to forget.
- **Application** — module services behind interfaces (`IPublicService`/`PublicService`), registered
  scoped by the module itself, returning DTOs (`WelcomeDto`) or `Result<T>`.
- **Domain** — kernel primitives only so far: `BaseEntity`, `IAuditable`, `ISoftDelete`, `Result<T>`.
  No business entities exist yet; they will live inside feature modules.
- **Infrastructure** — `CathedraDbContextBase` (audit + soft delete + global `!IsDeleted` query
  filter built via expression trees), the file log sink (single background thread, queued writes,
  daily rotation, size-based part files, retention purge), and `ICurrentUser` as the seam between
  persistence and whatever auth eventually exists.
- **Cross-cutting** — `RequestLoggingMiddleware` (correlation id + timing), `GlobalExceptionHandler`
  (same envelope on failure; real message in Development, generic in Production, full detail always
  in logs), `CathedraConsoleFormatter` (`[cid:…]` inline on every console line).

**Request flow:**

```
Client ──HTTP──▶ RequestLoggingMiddleware        (1) mint/honor X-Correlation-ID,
                      │                              open ILogger scope, start stopwatch
                      ▼
                 UseExceptionHandler             (2) any escape → GlobalExceptionHandler
                      │                              → ApiResponse.Fail + 500, same cid
                      ▼
                 Module endpoint (Minimal API)   (3) handler → IService → Result<T>/DTO
                      │
                      ▼
                 ApiResults.Ok / .From           (4) wrap in ApiResponse<T>, stamp cid,
                      │                              map error code → HTTP status
                      ▼
Client ◀─────── JSON envelope + X-Correlation-ID header
                 (middleware logs "→ 200 in 4ms" on the way out)
```

**Composition (dependency direction):**

```
        ┌──────────────────────┐
        │  Calcifer.Sample.Api │  host: DbContext, ICurrentUser, AddCathedra, AddFileLogging
        └────────┬─────────────┘
                 │ references (for assembly loading only)
     ┌───────────┴───────────┐
     ▼                       ▼
┌───────────────┐   ┌────────────────────┐
│ Calcifer.Public│──▶│ Calcifer.Cathedra  │  kernel: envelope, logging, errors,
│  (IModule)     │   │  (no refs outward) │  module system, persistence base
└───────────────┘   └────────────────────┘
    discovered at runtime by ModuleLoader (reflection),
    NOT invoked directly by the host
```

## 7. Module System

The heart of the architecture. The contract ([IModule.cs](src/Calcifer.Cathedra/Modules/IModule.cs)):

- `Name` / `Version` / `Order` (from `IModuleDescriptor`) — identity and deterministic load order
  (`OrderBy(Order).ThenBy(Name)`).
- `ConfigureServices(IServiceCollection)` — module-owned DI registrations, called before build.
- `MapEndpoints(WebApplication)` — module-owned routes, called during pipeline wiring.
- `InitializeAsync` / `StartAsync` — optional async lifecycle (migrations/seeding, then warm-up),
  run in two full passes (*all* modules initialize before *any* module starts), with default no-op
  implementations so trivial modules stay trivial.

Discovery ([ModuleLoader.cs](src/Calcifer.Cathedra/Modules/ModuleLoader.cs)) scans configured
assemblies for concrete `IModule` types, requires a public parameterless constructor (throws a clear
error otherwise), dedupes by concrete type with explicitly-registered instances winning, and
tolerates `ReflectionTypeLoadException` (skips unloadable types rather than crashing startup).
Startup logs the spec-mandated line: `Cathedra: discovered 1 module — Public (v0.1.0)`.

**Core vs. optional modules, license gating:** the brief's Core/Optional distinction and
license-based feature gating are **not implemented**. Today every discovered module loads
unconditionally. The `Order` property and the options-driven loader are the natural seams where a
gate ("skip modules the license doesn't cover") would go — a one-place change in
`ModuleLoader.Discover` or `AddCathedra`.

## 8. Data Access Strategy

- **EF Core 10 with direct `DbContext` usage** — no repository abstraction. Modules derive from
  `CathedraDbContextBase` (or use the shared `CathedraDbContext`) and add `DbSet<T>`s.
- **Audit trail**: any entity implementing `IAuditable` gets `CreatedAtUtc/CreatedBy` on insert and
  `UpdatedAtUtc/UpdatedBy` on update, stamped from `ICurrentUser` inside `SaveChanges`.
- **Soft delete**: `EntityState.Deleted` on an `ISoftDelete` entity is rewritten to `Modified` with
  `IsDeleted/DeletedAtUtc/DeletedBy` set, and a global query filter (`e => !e.IsDeleted`, built via
  expression trees for every `ISoftDelete` entity type) hides deleted rows from all queries.
- **Provider**: the sample host uses the in-memory provider (`UseInMemoryDatabase("Calcifer")`) so it
  runs with zero setup. SQL Server, migrations, and any permission cache are **roadmap** — nothing in
  the kernel assumes a specific provider.

## 9. Security & Authentication

**Current state: there is no authentication or authorization.** This is explicit and documented:
`AnonymousCurrentUser` names itself a placeholder "until the Auth module exists", and
[SECURITY.md](SECURITY.md) lists the no-auth default among its security-relevant design notes.
JWT, RBAC (`Module:Resource:Action`), license validation, and the License→RBAC→Auth filter chain
from the brief are all **roadmap**.

What *is* implemented is security hygiene at the seams:

- **No stack-trace leakage in Production**: `GlobalExceptionHandler` returns the real exception
  message only in Development; Production clients get `"An internal error occurred…"` while the full
  exception goes to logs. SECURITY.md explicitly declares a Production leak a valid vulnerability report.
- **Swagger/OpenAPI is Development-only** (`app.Environment.IsDevelopment()` gate in the bootstrapper).
- **Error-code → status mapping already reserves auth semantics**: `ApiResults.MapStatus` maps
  `UNAUTH`/`CREDENTIALS`/`REFRESH` → 401 and `FORBIDDEN` → 403 — the envelope convention is
  auth-ready before auth exists.
- **A vulnerability disclosure policy** with response-time commitments (3/7 business days) — unusual
  and commendable for a project this young.

## 10. API Design

- REST-style routes under `/api/v1/*` via Minimal API route groups (`MapGroup("/api/v1/public")`).
- **Minimal APIs only** — no Controllers, legacy or otherwise.
- Every response is `ApiResponse<T>`: `{ success, data, message, error{code,message}, correlationId,
  timestampUtc }`. Failure uses the *same shape* with `error` populated — including unhandled 500s.
- OpenAPI JSON at `/openapi/v1.json`, Swagger UI at `/swagger` (Development only); module endpoints
  appear automatically with no per-module wiring.
- Current surface: `GET /welcome`, `GET /multiply?num1&num2`, `GET /time`, and `GET /boom`
  (deliberately throws to prove the error envelope).

## 11. Logging & Observability

Two parallel channels, deliberately decoupled:

1. **`ILogger<T>`** (standard) — enriched by the middleware's scope so every line carries the
   correlation id; `CathedraConsoleFormatter` prints it inline: `[cid:my-trace-1] HTTP GET /api/v1/public/welcome -> 200 in 4ms`.
2. **`ILogWriter` / `ILogWriter<T>`** (custom, optional) — defaults to a console-backed writer;
   `AddFileLogging()` (called after `AddCathedra` so it replaces the default) swaps in a composite
   console+file writer. The file sink is non-blocking (queued lines, one background flush thread),
   daily-rotated with size-based part files and `RetainDays` purging, and each line carries UTC
   timestamp, level, correlation id, client IP, and module category. `GlobalExceptionHandler` takes
   `ILogWriter?` as an *optional* constructor dependency so the kernel never hard-requires it.

There is no distributed tracing (OpenTelemetry), metrics, or health-check endpoint yet — reasonable
omissions at this stage, and the first observability additions worth making.

## 12. Testing Strategy

**There are no tests in the repository** — no xUnit, Moq, or `WebApplicationFactory` projects exist.
The architecture is nonetheless *highly testable by construction*: services are interface-driven,
`ModuleLoader.Discover` is a pure static function over options, `Result<T>` makes service outcomes
assertable without HTTP, and the in-memory EF provider is already wired in. The natural first suite:
unit tests for `ModuleLoader` (dedupe/ordering/ctor errors), `ApiResults.MapStatus`, and
`CathedraDbContextBase` audit/soft-delete behavior, plus one `WebApplicationFactory` integration test
asserting envelope + correlation-id round-trip on `/welcome` and `/boom`. Treat this as the top
recommendation in §17.

## 13. Deployment & Configuration

- Single deployable: `dotnet run --project src/Calcifer.Sample.Api`.
- `appsettings.json` + `appsettings.Development.json` overrides; file logging configured under
  `Cathedra:FileLogging` (path, level, size cap, rotation, retention).
- Environment-driven behavior via `ASPNETCORE_ENVIRONMENT` (error detail, Swagger exposure).
- **No Docker, no CI/CD, no user-secrets usage yet** (nothing secret exists to protect yet).

## 14. Key Architectural Decisions (and why)

- **Minimal APIs over Controllers** — modules map endpoints directly in `MapEndpoints(WebApplication)`;
  route groups give versioned prefixes and OpenAPI metadata without attribute routing, controller
  activation, or filters. The kernel's envelope helpers (`ApiResults`) replace what result filters
  would do in MVC, with less machinery.
- **Module system over a plain layered app** — features become self-contained assemblies with their
  own services, routes, and lifecycle, coupled only to the kernel contract. This is what keeps the
  monolith *modular*: `Calcifer.Public` could be deleted, and nothing else would need to change.
- **`FrameworkReference` instead of abstracting ASP.NET Core** — a pragmatic anti-purism decision,
  documented in the `.csproj`: the kernel *is* web infrastructure, so pretending it isn't would just
  add adapter layers. Trade-off accepted: modules can't be reused outside ASP.NET Core.
- **Envelope + correlation id as the non-negotiable core** — the whole kernel exists so this can't
  be done inconsistently. `ApiResults` is the only sanctioned way to respond, and the exception
  handler guarantees even crashes speak the same dialect.
- **`Result<T>` with a code→status convention** — services express outcomes in domain terms
  (`USER_NOT_FOUND`); `MapStatus` centralizes the HTTP translation, so status-code policy lives in
  exactly one `switch`.
- **Direct DbContext, no repositories** — EF Core already provides unit-of-work and queryability;
  the kernel instead standardizes the *behaviors* (audit, soft delete) at the `DbContext` base, which
  a repository layer could not enforce as universally.
- **Custom file logger instead of Serilog** — keeps the kernel dependency-free beyond EF/OpenAPI and
  makes the `[cid]`/`[ip]`/`[module]` line format a first-class guarantee. Trade-off: it re-implements
  rotation/retention that mature libraries provide; revisit if sinks multiply.
- **In-memory database in the sample host** — zero-setup onboarding; the persistence base is
  provider-agnostic so this is a host choice, not a kernel one.
- **In-memory permission cache / license gating** — *decisions not yet made in code*; they exist only
  in the brief.

## 15. Comparison Matrix

| Aspect | **Calcifer (Modular Monolith kernel)** | Traditional Monolith | Microservices | Clean Architecture (single app) | 3-Tier |
| --- | --- | --- | --- | --- | --- |
| Deployment | Single deployable unit | Single deployable unit | Many independent services | Single deployable unit | Single (or 3 physical tiers) |
| Scalability | Vertical; horizontal by cloning the whole app | Vertical | Horizontal per service | Vertical; clone-whole-app | Per-tier, coarse |
| Technology heterogeneity | Single stack (.NET 10) | Single stack | Polyglot possible | Single stack | Single stack (per tier at best) |
| Data management | One shared `DbContext`/database (modules *may* derive their own context) | Single database | Database per service | Single database | Single database |
| Coupling | Loose: modules → kernel contract only; no module→module references exist | Tight, often accidental | Very loose (network contracts) | Loose via dependency rule | Moderate (layer-to-layer) |
| Testability | High by construction (interfaces, pure loader, `Result<T>`) — **but no tests written yet** | Low | High per service, hard end-to-end | High | Moderate |
| Maintainability | High: cross-cutting concerns solved once, in one place | Degrades over time | High per service, complex overall | High | Moderate |
| Team fit | 1 dev – small team; module boundaries later support team-per-module | Large single team | Multiple small teams | Small–medium | Medium |
| Deployment complexity | Low (one `dotnet run`) | Low | High (orchestration, service mesh) | Low | Low–medium |
| Operational complexity | Low (correlation ids already ease debugging) | Low | High (distributed tracing mandatory) | Low–medium | Low |
| Security | Consistent seams (envelope, error hygiene) but **no auth yet** | Varies | Per-service, high effort | High if enforced | Perimeter-based, medium |
| Consistency model | Strong (one DB, one transaction) | Strong | Eventual consistency required | Strong | Strong |
| Cost | Low | Low | High (infra + ops) | Low | Low |
| **Migration path outward** | **Designed-in: a module is a proto-service** | Painful rewrite | — | Moderate | Painful |

The last row is the differentiator: Calcifer sits where a monolith's cost/simplicity meets a
microservice's boundary discipline, at the price of sharing one process and one database.

## 16. Strengths, Weaknesses, Microservice Readiness

### Strengths
- **Cross-cutting concerns are unforgeable.** Envelope, correlation, error hygiene, audit, and soft
  delete are kernel behaviors, not conventions modules must remember. A new module gets them by
  existing.
- **Tiny host, self-describing startup.** `Program.cs` is 37 lines; adding a module is three
  documented steps (reference kernel, implement `IModule`, point discovery at the assembly).
- **Honest, documented trade-offs.** Code comments explain *why* (middleware order, optional
  `ILogWriter`, ProblemDetails registration), and SECURITY.md documents the insecure defaults rather
  than hiding them.
- **Debuggability from day one.** Correlation id across header/body/logs is the kind of concern
  usually retrofitted after the first production incident.

### Weaknesses / Trade-offs
- **Nothing real runs on it yet.** One demo module, in-memory database, no auth, no tests. The
  architecture is *promising*, not *proven* — its claims haven't been stress-tested by a module with
  actual business complexity.
- **No test suite** despite high testability — the largest gap between design intent and reality.
- **Shared `DbContext` is a latent coupling point.** If all modules pile `DbSet`s onto
  `CathedraDbContext`, module isolation erodes at the data layer; per-module contexts (which the base
  class supports) should be the enforced norm.
- **Reflection discovery has sharp edges**: parameterless-constructor requirement, silent skipping of
  unloadable types, and no gating/health concept for modules that fail `InitializeAsync` (a throw
  aborts startup — arguably correct, but undocumented).
- **Custom logging stack** re-implements what Serilog gives for free, and there's no
  OpenTelemetry/metrics/health-check story yet.
- **`MapStatus` string conventions** (`code.EndsWith("TAKEN")`) are convenient but stringly-typed; a
  typo in an error code silently becomes a 400.

### Microservice Readiness
A module is deliberately shaped like a proto-service: it owns its services, routes, DTOs, and
(potentially) its own `DbContext`. Extracting one means: (1) new host `Program.cs` (copy the 37-line
sample), (2) give the module its own database/context and connection string, (3) replace any
in-process calls to other modules with HTTP/messaging — today there are none, which is the ideal
starting position, (4) put a gateway/reverse proxy in front to preserve `/api/v1/*` routing, and
(5) keep propagating `X-Correlation-ID`, which the middleware already honors from inbound headers —
distributed tracing semantics are accidentally half-built. Best future candidates for extraction:
a Licensing module (naturally isolated, read-heavy) before an Auth module (everything depends on it).
**Verdict: high readiness in structure, untested in practice — and extraction should wait until a
module has an actual scaling or team reason to leave.**

## 17. Conclusion & Recommendations

Calcifer.Cathedra is a well-reasoned modular-monolith kernel whose value proposition is uniformity:
one envelope, one failure shape, one trace id, enforced by construction rather than convention. For
a small team building a licensable, feature-gated product (the evident roadmap), it is the right
architecture: monolith economics with module boundaries that keep the microservice door open. It is
**not** yet an application architecture that has survived contact with real features.

Recommended next steps, in order:

1. **Add the test projects now**, before the first real module — `ModuleLoader`, `MapStatus`,
   `CathedraDbContextBase` unit tests + one `WebApplicationFactory` envelope/correlation integration
   test. Every later module inherits the harness.
2. **Build the Auth module next** (it's already the named placeholder in `AnonymousCurrentUser`), and
   let it be the first real consumer that pressure-tests the module contract, per-module DbContext,
   and `InitializeAsync` migrations/seeding.
3. **Decide the module-gating story** (Core vs. Optional, license checks) inside
   `ModuleLoader.Discover`/`AddCathedra` while there is still only one module — retrofitting gating
   is much harder.
4. **Enforce per-module DbContexts** as the convention, keeping `CathedraDbContext` for the sample only.
5. **Add health checks and OpenTelemetry** (the correlation-id plumbing maps cleanly onto trace/span
   propagation), and consider typed error codes or a code registry to de-string `MapStatus`.
6. Document module failure semantics (what happens when `InitializeAsync` throws) and add a startup
   summary of module lifecycle outcomes alongside the discovery log line.
