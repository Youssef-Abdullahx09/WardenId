# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

WardenId — a multi-tenant identity/auth service (issuer/JWKS-style, per-tenant signing keys). Built as a modular monolith on .NET 10, hosted by a single `WardenId.Gateway` ASP.NET Core minimal-API host.

## Commands

```bash
dotnet build WardenId.slnx                 # build everything
dotnet run --project WardenId.Gateway      # run the API (Swagger UI at /swagger in Development)
dotnet ef migrations add <Name> \
  --project src/WardenId.Tenancy.Infrastructure \
  --startup-project WardenId.Gateway \
  --context TenancyDbContext              # add a migration for a module
```

`Protocol.Infrastructure` has two contexts, each with its own output dir: `--context ProtocolDbContext --output-dir Persistence/ProtocolMigrations` and `--context OpenIddictDbContext --output-dir Persistence/OpenIddictMigrations`.

- Tests: xUnit projects under `tests/` (`WardenId.<Module>.<Layer>.Tests`), hand-written fakes in each project's `Fakes/`, EF Core InMemory for handler tests. Run all with `dotnet test WardenId.slnx`.
- `dotnet ef` (v10.0.8) is available via `dotnet tool` / global install, not a local manifest — there's no `.config/dotnet-tools.json`.
- NuGet package versions are centrally managed in `Directory.Packages.props`; add new packages there, reference by name only in each `.csproj`.
- Every project restores with `packages.lock.json` (`RestorePackagesWithLockFile=true`); regenerate with `dotnet restore --force-evaluate` after changing package references.
- Target framework `net10.0`, nullable + implicit usings enabled repo-wide (`Directory.Build.props`).

## Architecture

### Module structure

The solution is organized into **bounded-context modules**, each split into up to 4 layered projects following clean-architecture dependency rules (Presentation → Application → Domain, Infrastructure → Application+Domain):

- `WardenId.<Module>.Domain` — aggregates, value objects, domain events, error factories. Zero framework dependencies.
- `WardenId.<Module>.Application` — CQRS commands/queries (MediatR), handler logic, abstractions (`I<X>Repository`, `I<Module>DbContext`) that Infrastructure implements.
- `WardenId.<Module>.Infrastructure` — EF Core `DbContext`, repositories, EF configurations/migrations, module-specific DI wiring (`Add<Module>Infrastructure`).
- `WardenId.<Module>.Presentation` — minimal-API endpoints, request records, module DI wiring (`Add<Module>Presentation`).

Modules today: `Tenancy` (tenant provisioning + ES256 signing keys), `Identity` (users, credentials, MFA, lifecycle), `ClientManagement` (OAuth clients, secrets, redirect URIs, CORS origins), `Protocol` (login sessions, token issuance, end-session + back-channel logout, discovery/JWKS), and `Audit` (transparency-log audit trail). `SharedKernel.*` projects are the cross-cutting equivalents (no Domain-specific project; `SharedKernel.Domain` holds base types instead).

Modules never reference each other's `Application`/`Domain` projects. Cross-module calls go through a `WardenId.<Module>.Integration.Command` contract project (MediatR request + response records only) and are sent in-process via `ISender`; the owning module's `Application` project implements the handler (e.g. `Tenancy.Integration.Command/Features/SigningKeys/GetPublicKeys` → `Tenancy.Application/Features/SigningKeys/GetPublicKeys/Handler.cs`).

`WardenId.Gateway` is the only executable. It composes every module via `WardenId.Gateway/DependencyInjection/*DependencyInjection.cs` (one file per module, e.g. `TenancyDependencyInjection.AddTenancyModule`), called from `Program.cs`. Adding a module means: create the 4 layer projects, add its `Add<Module>Module` extension, wire it into `Program.cs` and the corresponding `Add*Module` file, and add the projects to `WardenId.slnx`.

### Vertical-slice feature layout

Inside Application/Presentation, features live under `Features/<Aggregate>/<UseCase>/`, e.g. `Features/Tenants/Create/`:

- **Presentation**: `Endpoint.cs` (implements `IEndpoint`, maps the route) + `Request.cs` (public request record, has a `ToCommand(...)` mapper).
- **Application**: `Command.cs` (or `Query.cs`, a MediatR `IRequest<Result<T>>`) + `Handler.cs` (`IRequestHandler<TCommand, Result<T>>`).

Never suffix type names with `Dto` — name records for what they represent (`TenantPublicKeys`, `ActiveSigningKey`, `Request`). When two modules' contract types share a name, alias at the using site (e.g. `using TenancyActiveSigningKey = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning.ActiveSigningKey;`) rather than renaming either.

`IEndpoint` implementations are auto-discovered and registered via reflection over assemblies whose name ends in `Presentation` (`SharedKernel.Presentation/Utilities/EndpointExtensions.AddEndPoints`) — no manual endpoint registration needed, just implement `IEndpoint` in the right project.

### Result/error handling

- Domain and Application layers return `FluentResults.Result`/`Result<T>` — never throw for expected failures.
- Domain error factories live in `<Module>Errors` static classes (e.g. `TenantErrors`), each `Error` tagged with a metadata error code (`<Module>ErrorCodes`, e.g. `TenantErrorCodes.NotFound`/`Validation`).
- At the HTTP boundary, `result.HandleApiFailure()` (`SharedKernel.Presentation/Utilities/IResultExtensions.cs`) maps the first error's `ErrorCodes.MetadataKey` metadata to an HTTP status (`ErrorCodes.NotFound` → 404, `DuplicateIdentity` → 409, `AccountLocked` → 423, etc., default 400). New domain error codes need a matching case there or they fall back to 400.

### Domain model conventions

- Aggregates derive from `SharedKernel.Domain.AggregateRoot` (→ `Entity`, which carries only `Id`). `Id` is a `Guid.CreateVersion7()` set at construction — never `[Key]`/DB-generated. Rows carry no who/when audit columns: the Audit module's log records the actor and diff of every `IAuditableAggregate` change, and a domain event's actor (`DomainEvent.UserId`) is the `userId` passed to `IDbContext.SaveChangesAsync`.
  - `ICreationTracked` (opt-in, for entities listed or sorted by creation: Tenant, SigningKey, Client, User): the entity declares `CreatedAt { get; private set; }` and `DbContextBase` stamps it on insert.
  - `ISoftDeletable` (opt-in: User, Client): the entity declares `IsDeleted`/`DeletedAt` and its own domain method sets them (`User.SoftDelete`/`Restore`, `Client.Delete`). Removing one from its `DbSet` throws. Queries filter `!IsDeleted` explicitly.
- Lifecycle: deactivation is domain state (`Status`/`Enabled`), reversible, raising an event — never `IsDeleted`. Soft delete only where a row must outlive its removal (restore, or keeping an email/`client_id` taken). Everything else is hard-deleted when removed (children dropped from an aggregate, consumed tokens, pruned sessions). Keys and secrets expire or retire rather than being deleted one by one; a client's secrets and signing keys go only with `Client.Delete`.
- Aggregates raise `DomainEvent`s via `RaiseDomainEvent`; private constructors + static `Create(...)` factories returning `Result<T>` are the norm (see `Tenant.Create`, `Slug.Create`).
- Value objects derive from `SharedKernel.Domain.ValueObject` and implement `GetEqualityComponents()`.
- Enums (domain and Integration.Command contracts alike) give every member an explicit value starting at 1 — never implicit numbering, never 0 (`default(T)` must not be a valid member). `[Flags]` enums use powers of two from 1.
- A value object typically exposes two construction paths: `Create(raw)` (validates, for user input) and `FromTrustedSource(value)` (skips validation, for EF materialization/internal use only) — follow this split when adding new value objects (see `Slug`).

### Persistence

- Each module's `DbContext` extends `SharedKernel.Persistence.DbContextBase`, implements its own `I<Module>DbContext`, sets its own schema (`modelBuilder.HasDefaultSchema(...)`) and its own `__EFMigrationsHistory` table name per module/schema.
- `DomainEventsInterceptor` (registered per DbContext via `AddInterceptors`) runs on `SavingChangesAsync`: opens/reuses a CAP-participating transaction, publishes every pending aggregate's domain events through `ICapPublisher` (DotNetCore.CAP over RabbitMQ + Postgres outbox), then clears them — this is the outbox/eventing mechanism, not manual `IPublisher.Publish`.
- `Protocol.Infrastructure` has two contexts. `ProtocolDbContext` (schema `Protocol`) holds the `Session` aggregate, whose id is the OIDC `sid`, and follows the module convention above, shared interceptors included, so `SessionEndedDomainEvent` reaches the CAP outbox. `OpenIddictDbContext` (schema `OpenIddict`) holds OpenIddict's token/authorization tables (codes, refresh tokens and their families) and is deliberately registered **without** `AddSharedInterceptors` — its rows aren't aggregates. Clients are never written there: OpenIddict's application store is a read-only adapter over `ClientManagement.Integration.Command` (ADR-01). `OpenIddictDbContext` must migrate before `ProtocolDbContext` (the legacy `DropLegacyProtocolSchema` migration drops schema `Protocol`), so never run `dotnet ef database update --context ProtocolDbContext` on a database that hasn't applied OpenIddict's migrations.
- Migrations are applied automatically at startup (`MigrateAsync` in each module's Infrastructure `DependencyInjection`), not via a separate CLI step in deployment.
- Design-time migrations (`dotnet ef`) read connection strings from `WardenId.Gateway/appsettings.json` via each module's `<Module>DbContextDesignTimeFactory` (`IDesignTimeDbContextFactory<T>`) — keep that factory's relative path (`AppConfigurationConstants.PathToGateway`) in sync if the Gateway project moves.

### Cross-cutting / SharedKernel

- `ICurrentUser`/`ICurrentTenant` (`SharedKernel.Presentation`) resolve identity from `HttpContext`: `ICurrentUser.UserId` from the JWT `sub`/`NameIdentifier` claim, `ICurrentTenant.TenantId` from a custom `TenantId` claim, `ICurrentTenant.Slug` from the `{slug}` route value — tenancy is routed (`/api/v{version}/{slug}/...`-style), not header-based. `CustomClaimTypes` lives in `SharedKernel.Application`.
- `ITenantContext` (`SharedKernel.Application.Abstractions`, implemented by `SharedKernel.Presentation.HttpTenantContext`) is the pre-authentication tenant for the request: null until something resolves it (e.g. the OpenIddict `ResolveTenantFromPath` handler sets `Slug` from `/tenants/{slug}/`, then `TenantId` once verified). It's a singleton backed by `HttpContext.Items`, so it's safe from OpenIddict's singleton handlers. Use `ICurrentTenant` for authenticated callers, `ITenantContext` for protocol/issuer flows.
- API versioning is via `Asp.Versioning` with `UrlSegmentApiVersionReader` (`/api/v{version:apiVersion}/...`); every REST/admin endpoint builds an `ApiVersionSet` and calls `.MapToApiVersion(...)`. **Exception:** OIDC protocol endpoints live unversioned under the tenant issuer `{ProtocolToken:IssuerBaseUri}/tenants/{slug}/...` (discovery, JWKS, authorize, token, introspect, revoke, userinfo, endsession) — the issuer string is built only by `Protocol.Application.Utilities.IssuerUri.For`. `/token` is served by OpenIddict (passthrough into `Protocol.Presentation/Features/Token/Endpoint.cs`); per-tenant issuer, eligibility and signing are OpenIddict event handlers in `Protocol.Infrastructure/OpenIddict/Handlers`.
- OpenTelemetry (traces/metrics/logs) and CAP (outbox/messaging) are both configured centrally in `SharedKernel.Infrastructure.DependencyInjection` and can be toggled off via the `OpenTelemetry:Enabled` config flag; standard `OTEL_*` env vars in config are applied to the process environment as fallback only (real env vars always win).
- CAP subscribers (the first ones live in `Protocol.Application/Subscribers`):
  - **Layout:** one folder per topic, `<Module>.Application/Subscribers/<Topic>/`, holding `Message.cs` (a record with only the fields this subscriber reads) and `Subscriber.cs` — a class implementing `ICapSubscriber<Message>` (`SharedKernel.Application.CapPublisher`) with `[CapSubscriber(nameof(<Topic>), Group = "<module>." + nameof(<Topic>))]` on `OnReceive`. Inside the folder's namespace `nameof(<Topic>)` resolves to the namespace segment, so the folder name is the topic.
  - **Group:** always override the attribute's default group, which is the topic. Two subscriptions sharing (topic, group) silently drop one.
  - **Registration:** a private `AddSubscribers()` in the module's Application `DependencyInjection` registers each one with `services.AddScoped<ICapSubscribe, Subscribers.<Topic>.Subscriber>()`, because CAP discovers subscribers from the service collection.
  - **Topic:** the publishing event's type short name (`DomainEventsInterceptor`).
  - **Cross-module messages:** the subscriber's own `Message` record replaces any reference to the publisher's Domain, e.g. Protocol's `Subscribers/UserDisabledDomainEvent/Message(Guid AggregateId)` over Identity's topic. A contract test (test-only reference to the publisher's Domain) pins the topic names and the JSON binding. `DomainEvent.UserId` and `AggregateId` have private setters and are not deserialized, so an event a consumer must read carries its ids as explicit record parameters.
  - **Retries:** throwing makes CAP retry (`CapPublisher:RetryCount` / `RetryInterval`); returning acknowledges the message.
  - **Testability:** keep `ICapPublisher` behind a seam interface (`CapAuditSegmentPublisher`, `CapBackchannelLogoutQueue`) so handlers are testable without CAP.
- Config keys are centralized in `<Layer>.Utilities.ConfigurationKeys`/`AppConfigurationConstants` static classes per project rather than magic strings — follow that pattern for new config sections.
- **No magic strings.** Every string identifier (routes, tags, API version sets, schema/table/column names, claim types, header names/values, algorithms, OAuth/OIDC parameters, policy/cookie/scheme names, `HttpContext.Items` keys, CAP topics/groups, audit codes, cache keys, switch discriminators) comes from a library constant (`OpenIddictConstants`, `SecurityAlgorithms`, `HeaderNames`, `JwtRegisteredClaimNames`, ...), an enum, or a static constants class in the lowest layer that uses it (`EndpointNames`, `ConfigurationKeys`, `<Module>ErrorCodes`, `<Module>Routes`, ...). Human-readable text (summaries, error messages, log templates, UI copy) stays inline.
- **`Utilities/` folders.** Each project keeps its cross-cutting constants and helpers in `<Project>/Utilities/` (namespace `<Project>.Utilities`): `EndpointNames`, `EndpointTags`, `ApiVersionSets`, `<Module>Routes`, `ConfigurationKeys`, `PostgresConventions`, `JsonWebKeyValues`, extension classes like `IResultExtensions`/`EndpointExtensions`, small helpers like `SecureTokenGenerator`/`QrCodeSvg`. Not in Utilities: `DependencyInjection.cs`, domain types and logic, `<Module>Errors` + `<Module>ErrorCodes` (the domain error model), and anything owned by a subsystem or feature folder (`Features/...`, `Persistence/SchemaConsts`, `OpenIddict/...`, `Health/...`), which stays with its owner.
