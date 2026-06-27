# Security Policy

## Supported versions

Calcifer.Cathedra is in active early development. Security fixes are applied to the latest
`main` branch only.

| Version | Supported          |
| ------- | ------------------ |
| `main`  | :white_check_mark: |
| older   | :x:                |

## Reporting a vulnerability

**Please do not open a public GitHub issue for security vulnerabilities.**

Report privately instead, using either of the following:

- Email: **rakibul.979.hasan@gmail.com** with the subject `SECURITY: Calcifer.Cathedra`
- Or, if the repository is on GitHub, use **Security → Report a vulnerability**
  (Private Vulnerability Reporting).

Please include:

- A clear description of the issue and its impact.
- Steps to reproduce (a minimal proof of concept is ideal).
- Affected component/file (e.g. `GlobalExceptionHandler`, `FileLogSink`, an endpoint) and commit hash.
- Any suggested remediation, if you have one.

### What to expect

- **Acknowledgement** within **3 business days**.
- An initial assessment and severity rating within **7 business days**.
- Coordinated disclosure: we will agree on a timeline before any public detail is shared, and will
  credit you in the release notes unless you prefer to remain anonymous.

## Scope

In scope:

- The kernel (`Calcifer.Cathedra`): response envelope, request-logging middleware, global exception
  handler, module loader, persistence base, and the file logger.
- The sample host (`Calcifer.Sample.Api`) where it demonstrates an insecure default.

Out of scope:

- Vulnerabilities in third-party dependencies — report those upstream (we will still want to know so
  we can bump the dependency).
- The deliberately-failing demo endpoint `GET /api/v1/public/boom` (it throws on purpose to exercise
  the exception handler).
- Findings that require physical access to the host, or that depend on a misconfiguration outside the
  defaults shipped in this repository.

## Security-relevant design notes

These behaviors are intentional and worth knowing before reporting:

- **No stack-trace leakage in Production.** `GlobalExceptionHandler` returns a generic error message
  when `ASPNETCORE_ENVIRONMENT` is not `Development`; the real exception stays in the logs only. If you
  can make a Production response leak an exception message or stack trace, that is a valid report.
- **Correlation IDs.** An inbound `X-Correlation-ID` header is echoed back and written to logs. It is
  treated as an opaque trace token, not a trust boundary — do not rely on it for authorization.
- **File logging.** The file logger (`logs/cathedra/`) records correlation id, client IP, and message
  text. Treat log files as potentially sensitive and protect the log directory accordingly. Avoid
  logging secrets via `ILogWriter`.
- **Authentication / authorization.** The current core ships **no** auth; the sample uses an anonymous
  `ICurrentUser`. Do not deploy the sample host as-is to an untrusted network.

## Hardening recommendations for deployers

- Run behind HTTPS and a reverse proxy; set `ASPNETCORE_ENVIRONMENT=Production`.
- Do not expose the Swagger UI / OpenAPI document in Production (it is gated to Development by default —
  keep it that way).
- Restrict filesystem permissions on the log directory and apply log retention.
- Supply a real `ICurrentUser` and an authentication/authorization layer before going live.
