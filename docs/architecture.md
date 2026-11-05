# Architecture decisions

## Context

OpsGuardian models a small but realistic operational workflow: incidents are created, assigned, acknowledged, resolved, reopened and evaluated against an SLA. The design favors explicit business rules and operational safety over framework-heavy abstractions.

## Dependency direction

Dependencies point inward:

```text
Web -> Infrastructure -> Application -> Domain
          |                  ^
          +------------------+
```

The Domain project has no infrastructure dependencies. Application services depend on repository, clock, unit-of-work and outbox ports. Infrastructure implements those ports with a single EF Core unit of work. The Web project owns delivery concerns.

## Consistency and the outbox

Every state-changing use case adds an outbox row through the same `OpsGuardianDbContext` before calling `SaveChangesAsync`. The incident mutation and integration intent therefore commit atomically. The sample dispatcher marks messages as processed and exposes the seam where a broker publisher would be introduced.

Production evolution would add idempotency keys, retry scheduling, poison-message handling and provider-specific locking for multiple dispatcher replicas.

## SLA monitoring

SLA duration is a domain rule:

| Severity | Response window |
| --- | ---: |
| Critical | 1 hour |
| High | 4 hours |
| Medium | 12 hours |
| Low | 24 hours |

The monitor asks the repository only for active overdue incidents. The aggregate independently verifies the deadline and ensures the breach transition is idempotent.

## Authentication and authorization

Development and test environments use an explicit demo authentication scheme. Every other environment requires OpenID Connect configuration at startup. Incident writes and response actions require the `IncidentManager` role; reads require an authenticated principal.

The UI is not the security boundary. POST handlers repeat authorization checks, and Razor Pages antiforgery validation remains enabled.

## Database lifecycle

SQL Server is the primary provider. SQLite is supported only to make integration tests isolated, fast and deterministic. Development may call `EnsureCreated` against the local database. Production never creates or migrates its own schema.

## Observability

The app registers ASP.NET Core, outbound HTTP and runtime instrumentation. OTLP export is opt-in through `OTEL_EXPORTER_OTLP_ENDPOINT`. Health endpoints separate process liveness from database readiness so an orchestrator can make different restart and traffic-routing decisions.

## Security posture

- No tracked secrets or remote database addresses
- Fail-closed production authentication and database configuration
- Content Security Policy and restrictive browser headers
- HTTPS redirection and HSTS outside Development
- Global fixed-window rate limiting
- Central dependency pinning and lock files
- Build failure on compiler or analyzer warnings
- CI vulnerability audit including transitive dependencies
