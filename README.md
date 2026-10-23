# WardenId

WardenId is a self-hosted, multi-tenant OpenID Connect / OAuth 2.0 identity provider for .NET. Each tenant gets its own issuer, its own ES256 signing keys, its own users and clients, and a tamper-evident audit log.

It is a **modular monolith** on **.NET 10**: one ASP.NET Core minimal-API host (`WardenId.Gateway`) composes five bounded-context modules. They talk to each other only through in-process contracts and CAP events.

> Status: under active development.

---

## Features

**Multi-tenancy**
- Tenant provisioning and lifecycle: create, rename, suspend, reactivate, archive.
- Per-tenant issuer `{IssuerBaseUri}/tenants/{slug}` with its own discovery document and JWKS.
- Per-tenant ES256 signing keys, encrypted with DataProtection, with rotation and retirement.

**OAuth 2.0 / OIDC** (built on [OpenIddict](https://documentation.openiddict.com/))
- Authorization Code + PKCE, Client Credentials, and Refresh Token grants (rotation with reuse detection).
- Discovery, JWKS, authorize, token, introspection, revocation, userinfo, and end-session endpoints.
- Login sessions (`sid`), RP-initiated logout, and OIDC back-channel logout.
- Admin APIs for sessions, grants, and token-lifetime policies.

**Identity**
- Users with admin lifecycle: create, update, enable/disable, soft-delete/restore, unlock.
- Self-service registration, email verification, and password reset (sent over SMTP).
- Argon2 password hashing, configurable password policy and history, and a Have I Been Pwned breached-password check.
- Login lockout, TOTP MFA with recovery codes, required actions, and custom user attributes.

**Client management**
- OAuth clients with hashed, rotating secrets.
- Exact-match redirect and post-logout redirect URIs, CORS origins, and back-channel logout URIs.

**Audit**
- Append-only audit trail of every change and authentication event, routed to channels with their own retention.
- RFC 6962 Merkle segments with signed checkpoints. The API serves inclusion and consistency proofs and runs verification.
- Retention purges payloads while keeping hashes, and sealed segments can be exported over CAP.

**Operations**
- OpenTelemetry traces, metrics, and logs over OTLP.
- Health checks for every module database and RabbitMQ.
- Transactional outbox with DotNetCore.CAP (Postgres + RabbitMQ).
- EF Core migrations run automatically at startup.

---

## Tech stack

| Concern | Choice |
|---|---|
| Runtime | .NET 10, ASP.NET Core minimal APIs + Razor Pages (login UI) |
| Protocol | OpenIddict 7 |
| Persistence | PostgreSQL. EF Core 10 (Npgsql) for writes, Dapper for reads |
| Messaging / outbox | DotNetCore.CAP over RabbitMQ with a Postgres outbox |
| Application | MediatR (CQRS), FluentResults |
| API | Asp.Versioning (URL segment), Swashbuckle / OpenAPI |
| Crypto | ES256 signing keys, Argon2id password hashing, ASP.NET Core DataProtection |
| Observability | OpenTelemetry (OTLP exporter) |
| Tests | xUnit, EF Core InMemory, Testcontainers (PostgreSQL) |

---

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL
- RabbitMQ
- An SMTP server for verification and reset emails. For local development, a catcher such as Mailpit or MailHog on `127.0.0.1:1025` is enough.
- Optional: an OTLP collector on `127.0.0.1:4318`. Turn telemetry off with `OpenTelemetry:Enabled=false`.
- Optional: `dotnet-ef` (`dotnet tool install --global dotnet-ef`) for creating migrations.
- Optional: Docker, which the Testcontainers-based tests need.

The repository does not include a compose file. Run the services however you like, for example:

```bash
docker run -d --name wardenid-pg  -p 5432:5432 -e POSTGRES_PASSWORD='P@ssw0rd' postgres:17
docker run -d --name wardenid-mq  -p 5672:5672 -p 15672:15672 \
  -e RABBITMQ_DEFAULT_USER=admin -e RABBITMQ_DEFAULT_PASS='P@ssw0rd' rabbitmq:4-management
docker run -d --name wardenid-smtp -p 1025:1025 -p 8025:8025 axllent/mailpit
```

These credentials match `WardenId.Gateway/appsettings.Development.json`. Use your own values anywhere other than a local machine.

### Build, run, test

```bash
dotnet build WardenId.slnx
dotnet run --project WardenId.Gateway     # http://localhost:5221, Swagger UI at /swagger (Development)
dotnet test WardenId.slnx
```

On first start, the Gateway:
1. Applies every module's EF Core migrations.
2. Starts CAP, which creates the outbox tables.
3. Seeds the **master tenant**, its **admin user**, and a confidential **master client**, using the `MasterTenant` section of configuration.

Look up the development admin credentials and client settings in `appsettings.Development.json`. Change all of them before deploying anywhere.

### Useful URLs (Development)

| URL | What |
|---|---|
| `/swagger` | Swagger UI for the admin/REST API |
| `/tenants/{slug}/.well-known/openid-configuration` | Tenant discovery document |
| `/tenants/{slug}/...` | Tenant protocol endpoints (authorize, token, jwks, userinfo, introspect, revoke, endsession) |
| `/health/live`, `/health/ready` | Liveness and readiness checks (anonymous) |

---

## Configuration

Configuration lives in `WardenId.Gateway/appsettings*.json` and can be overridden with environment variables (for example `ConnectionStrings__DefaultConnectionString`).

| Section | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnectionString` | PostgreSQL connection shared by every module schema |
| `ConnectionStrings:AuditRetention` | Optional separate DB role used only by the audit retention purge |
| `RabbitMq`, `CapPublisher` | Message broker and CAP retry/expiry settings |
| `Smtp` | Outgoing mail for verification and reset flows |
| `MasterTenant` | Bootstrap tenant, admin, and client seeded at startup |
| `ProtocolToken` | `IssuerBaseUri` and access/ID/refresh token and authorization-code lifetimes |
| `ProtocolSession` | Login session cookie and absolute lifetimes, session pruning |
| `ProtocolOpenIddict` | Token/authorization pruning, and `DisableTransportSecurityRequirement` (development only) |
| `ProtocolBackchannelLogout` | Back-channel logout HTTP timeout |
| `Audit` | Sealer, exporter, retention (per channel, in days), and witness settings |
| `Cors:AllowedAdminOrigins` | Origins allowed to call the admin API from a browser |
| `OpenTelemetry` | `Enabled` toggle and standard `OTEL_*` settings. Real environment variables always take precedence |

---

## Architecture

```
                        ┌──────────────────────────────┐
   HTTP ───────────────►│        WardenId.Gateway       │  composition root, auth, health, seeding
                        └──────────────┬───────────────┘
        ┌──────────┬──────────────┬────┴─────┬──────────────┬──────────┐
        ▼          ▼              ▼          ▼              ▼          ▼
     Tenancy   Identity   ClientManagement  Protocol      Audit     SharedKernel
        │          │              │          │              │
        └──── ISender (Integration.Command contracts) ──────┘
        └──── CAP domain events (Postgres outbox → RabbitMQ) ─┘
```

### Modules

| Module | Responsibility | DB schema |
|---|---|---|
| **Tenancy** | Tenants and their ES256 signing keys | own schema |
| **Identity** | Users, credentials, MFA, policies, account lifecycle | own schema |
| **ClientManagement** | OAuth clients, secrets, redirect URIs, CORS origins | own schema |
| **Protocol** | OIDC/OAuth endpoints (OpenIddict), login UI, sessions, logout, grants | `Protocol` + `OpenIddict` |
| **Audit** | Transparency-log audit trail: capture, sealing, proofs, retention, export | own schema |
| **SharedKernel** | Base types, persistence base, CAP/OTel wiring, presentation helpers | — |

### Project layout per module

```
src/WardenId.<Module>.Domain               aggregates, value objects, domain events, errors (no framework deps)
src/WardenId.<Module>.Application          MediatR commands/handlers, abstractions, CAP subscribers
src/WardenId.<Module>.Infrastructure       EF Core DbContext, repositories, migrations, DI
src/WardenId.<Module>.Presentation         minimal-API endpoints + request records
src/WardenId.<Module>.Query(.Contracts)    Dapper read side (query interfaces + implementations)
src/WardenId.<Module>.Integration.Command  cross-module MediatR request/response contracts
tests/WardenId.<Module>.<Layer>.Tests      xUnit tests with hand-written fakes
```

Features are organised as vertical slices under `Features/<Aggregate>/<UseCase>/`. Presentation has an `Endpoint.cs` and a `Request.cs`, and Application has a `Command.cs` and a `Handler.cs`. Endpoints implement `IEndpoint` and are discovered by reflection, so they need no manual registration.

### Key principles

- **Module isolation.** A module never references another module's Domain or Application. Cross-module calls go through `Integration.Command` contracts sent with `ISender`. Cross-module reactions go through CAP subscribers.
- **Results, not exceptions.** Domain and Application layers return `FluentResults.Result<T>`. At the HTTP boundary, `HandleApiFailure()` maps error codes to status codes (404, 409, 423, ...).
- **Outbox eventing.** `DomainEventsInterceptor` publishes aggregate domain events through CAP inside the same transaction as the write.
- **Write/read split.** Writes go through EF Core aggregates, and reads go through Dapper query projects.
- **Audit instead of audit columns.** Rows carry no created-by or modified-by columns. The Audit module records the actor and the diff of every auditable aggregate change.
- **No magic strings.** Routes, claim types, config keys, topics, and similar identifiers come from constants or enums in each project's `Utilities/` folder.

### API surface

- **Admin/REST API** is versioned under `/api/v{version}/...`. Examples: `/api/v1/tenants`, `/api/v1/admin/tenants/{tenantId}/users`, `/api/v1/admin/tenants/{tenantId}/clients`, `/api/v1/audit/events`. These endpoints take a WardenId access token as a Bearer token.
- **Protocol endpoints** are unversioned under the tenant issuer `{IssuerBaseUri}/tenants/{slug}/...`.

Browse the full API in Swagger.

---

## Development

```bash
# Add a migration (one DbContext per module)
dotnet ef migrations add <Name> \
  --project src/WardenId.Tenancy.Infrastructure \
  --startup-project WardenId.Gateway \
  --context TenancyDbContext

# Protocol has two contexts with separate output dirs
#   --context ProtocolDbContext   --output-dir Persistence/ProtocolMigrations
#   --context OpenIddictDbContext --output-dir Persistence/OpenIddictMigrations

# After changing package references (central versions in Directory.Packages.props)
dotnet restore --force-evaluate
```

- NuGet versions are centrally managed in `Directory.Packages.props`, and every project restores from a `packages.lock.json`.
- Nullable reference types and implicit usings are enabled for the whole repository (`Directory.Build.props`).
- `OpenIddictDbContext` must migrate before `ProtocolDbContext`. Startup handles this, so don't apply the Protocol context on its own against a fresh database.
- [`CLAUDE.md`](CLAUDE.md) describes the conventions in full: domain model rules, enum numbering, persistence, CAP subscriber layout, and more.

---

## Documentation

| Document | Content |
|---|---|
| [`CLAUDE.md`](CLAUDE.md) | Architecture and coding conventions |
| [`src/WardenId.Identity.Domain/README.md`](src/WardenId.Identity.Domain/README.md) | Identity domain notes |
