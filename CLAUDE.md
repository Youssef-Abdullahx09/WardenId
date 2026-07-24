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

- No test project exists yet (`tests/` is empty).
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
- `WardenId.<Module>.Presentation` — minimal-API endpoints, request DTOs, module DI wiring (`Add<Module>Presentation`).

Today there is one module, `Tenancy` (tenant provisioning + ES256 signing keys). `SharedKernel.*` projects are the cross-cutting equivalents (no Domain-specific project; `SharedKernel.Domain` holds base types instead).

`WardenId.Gateway` is the only executable. It composes every module via `WardenId.Gateway/DependencyInjection/*DependencyInjection.cs` (one file per module, e.g. `TenancyDependencyInjection.AddTenancyModule`), called from `Program.cs`. Adding a module means: create the 4 layer projects, add its `Add<Module>Module` extension, wire it into `Program.cs` and the corresponding `Add*Module` file, and add the projects to `WardenId.slnx`.

### Vertical-slice feature layout

Inside Application/Presentation, features live under `Features/<Aggregate>/<UseCase>/`, e.g. `Features/Tenants/Create/`:

- **Presentation**: `Endpoint.cs` (implements `IEndpoint`, maps the route) + `Request.cs` (public DTO, has a `ToCommand(...)` mapper).
- **Application**: `Command.cs` (or `Query.cs`, a MediatR `IRequest<Result<T>>`) + `Handler.cs` (`IRequestHandler<TCommand, Result<T>>`).

`IEndpoint` implementations are auto-discovered and registered via reflection over assemblies whose name ends in `Presentation` (`EndpointExtensions.AddEndPoints`) — no manual endpoint registration needed, just implement `IEndpoint` in the right project.

### Result/error handling

- Domain and Application layers return `FluentResults.Result`/`Result<T>` — never throw for expected failures.
- Domain error factories live in `<Module>Errors` static classes (e.g. `TenantErrors`), each `Error` tagged with a metadata error code (`<Module>ErrorCodes`, e.g. `TenantErrorCodes.NotFound`/`Validation`).
- At the HTTP boundary, `result.HandleApiFailure()` (`SharedKernel.Presentation/IResultExtensions.cs`) maps the first error's `ErrorCodes.MetadataKey` metadata to an HTTP status (`ErrorCodes.NotFound` → 404, `DuplicateIdentity` → 409, `AccountLocked` → 423, etc., default 400). New domain error codes need a matching case there or they fall back to 400.

### Domain model conventions

- Aggregates derive from `SharedKernel.Domain.AggregateRoot` (→ `Entity`, → `Id`/audit fields `CreatedById`/`LastModificationById`/`DeletedById`/`IsDeleted`, all set by EF interceptor logic, not manually). `Id` is a `Guid.CreateVersion7()` set at construction — never `[Key]`/DB-generated.
- Aggregates raise `DomainEvent`s via `RaiseDomainEvent`; private constructors + static `Create(...)` factories returning `Result<T>` are the norm (see `Tenant.Create`, `Slug.Create`).
- Value objects derive from `SharedKernel.Domain.ValueObject` and implement `GetEqualityComponents()`.
- A value object typically exposes two construction paths: `Create(raw)` (validates, for user input) and `FromTrustedSource(value)` (skips validation, for EF materialization/internal use only) — follow this split when adding new value objects (see `Slug`).

### Persistence

- Each module's `DbContext` extends `SharedKernel.Persistence.DbContextBase`, implements its own `I<Module>DbContext`, sets its own schema (`modelBuilder.HasDefaultSchema(...)`) and its own `__EFMigrationsHistory` table name per module/schema.
- `DomainEventsInterceptor` (registered per DbContext via `AddInterceptors`) runs on `SavingChangesAsync`: opens/reuses a CAP-participating transaction, publishes every pending aggregate's domain events through `ICapPublisher` (DotNetCore.CAP over RabbitMQ + Postgres outbox), then clears them — this is the outbox/eventing mechanism, not manual `IPublisher.Publish`.
- Migrations are applied automatically at startup (`MigrateAsync` in each module's Infrastructure `DependencyInjection`), not via a separate CLI step in deployment.
- Design-time migrations (`dotnet ef`) read connection strings from `WardenId.Gateway/appsettings.json` via each module's `<Module>DbContextDesignTimeFactory` (`IDesignTimeDbContextFactory<T>`) — keep that factory's relative path (`AppConfigurationConstants.PathToGateway`) in sync if the Gateway project moves.

### Cross-cutting / SharedKernel

- `ICurrentUser`/`ICurrentTenant` (`SharedKernel.Presentation`) resolve identity from `HttpContext`: `ICurrentUser.UserId` from the JWT `sub`/`NameIdentifier` claim, `ICurrentTenant.TenantId` from a custom `TenantId` claim, `ICurrentTenant.Slug` from the `{slug}` route value — tenancy is routed (`/api/v{version}/{slug}/...`-style), not header-based.
- API versioning is via `Asp.Versioning` with `UrlSegmentApiVersionReader` (`/api/v{version:apiVersion}/...`); every endpoint builds an `ApiVersionSet` and calls `.MapToApiVersion(...)`.
- OpenTelemetry (traces/metrics/logs) and CAP (outbox/messaging) are both configured centrally in `SharedKernel.Infrastructure.DependencyInjection` and can be toggled off via the `OpenTelemetry:Enabled` config flag; standard `OTEL_*` env vars in config are applied to the process environment as fallback only (real env vars always win).
- Config keys are centralized in `<Layer>.ConfigurationKeys`/`AppConfigurationConstants` static classes per project rather than magic strings — follow that pattern for new config sections.
