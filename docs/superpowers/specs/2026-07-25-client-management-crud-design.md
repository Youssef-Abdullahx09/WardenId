# ClientManagement CRUD — Application/Infrastructure/Presentation Design

Date: 2026-07-25
Modules: `WardenId.ClientManagement.Application`, `WardenId.ClientManagement.Infrastructure`, `WardenId.ClientManagement.Presentation` (all new)
Scope: **Admin API core CRUD for the `Client` aggregate.** Endpoints 1-5 of the original brief (Create/List/Get/Update/Delete a client). Secrets (6-7), redirect-uris/CORS (8-10), and scope assignment (11-12) are deferred to later specs — see "Explicitly Out of Scope".

## Context

`WardenId.ClientManagement.Domain` already has a fully built `Client` aggregate (see `docs/superpowers/specs/2026-07-24-client-management-domain-design.md`): `Create`, redirect/CORS collection mutators, `AddSecret`/`RotateSecret`/`ExpireSecret`, `AddSigningKey`, `AssignScope`/`RemoveScope`, `Disable`/`Enable`, `UpdateTokenLifetimeOverrides`. No Application/Infrastructure/Presentation project exists yet for this module — this spec builds all three from scratch, following `WardenId.Tenancy.*` conventions exactly.

Investigation before this spec surfaced several gaps against the original brief's assumptions:

- **RBAC/JWT auth is fully greenfield.** No `AddAuthentication`/`AddJwtBearer` call, no `Role` claim, no policy anywhere in the repo. Resolved: this pass uses bare `.RequireAuthorization()` only (matches the one existing precedent, `Tenant` Create). Real RBAC is a future spec.
- **`Scope` aggregate doesn't exist.** Confirmed still true per the domain spec's explicit deferral. Resolved: scope-assignment endpoints are out of scope for this pass.
- **Tenant resolution via route, not `ICurrentTenant`.** `ICurrentTenant.TenantId` reads a JWT claim; there is no route-value reader. Since these are admin routes (`/admin/tenants/{tenantId}/clients/...`, an operator acting on a specific tenant, not a tenant acting on itself), each Endpoint binds `[FromRoute] Guid tenantId` directly and passes it into the Command/Query. `ICurrentTenant` is untouched.
- **Soft-delete is not actually wired up anywhere in shared infra.** See "Soft-Delete Interceptor Fix" below — this blocks endpoint 5 and is fixed as part of this pass.
- **No List/Get (paginated query) pattern exists anywhere in the repo yet.** `PaginationListRequest`/`PaginationListResponse` exist in `WardenId.SharedKernel.Query.Contracts` but are unused. This spec establishes the first real usage.

## New Projects

### `WardenId.ClientManagement.Application`

Mirrors `WardenId.Tenancy.Application`:
- `WardenId.ClientManagement.Application.csproj` — `PackageReference MediatR`, `PackageReference FluentResults`, `ProjectReference` to `WardenId.ClientManagement.Domain` and `WardenId.SharedKernel.Application`.
- `DependencyInjection.cs` — `AddClientManagementApplication()` → `services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()))`.
- `Abstractions/IClientManagementDbContext.cs` — `IDbContext { DbSet<Client> Clients { get; } }`.
- `Abstractions/IClientRepository.cs` — narrow persistence interface for single-aggregate operations (see below).
- `Features/Clients/Create/{Command,Handler}.cs`
- `Features/Clients/List/{Query,Handler,ClientListItem}.cs`
- `Features/Clients/Get/{Query,Handler,ClientDetail}.cs`
- `Features/Clients/Update/{Command,Handler}.cs`
- `Features/Clients/Delete/{Command,Handler}.cs`

### `WardenId.ClientManagement.Infrastructure`

Mirrors `WardenId.Tenancy.Infrastructure`:
- `WardenId.ClientManagement.Infrastructure.csproj` — EF Core + Npgsql (via central package versions), `ProjectReference` to `Application`, `SharedKernel.Persistence`.
- `Persistence/SchemaConsts.cs` — `ClientsTable = "Clients"`, `ClientSecretsTable = "ClientSecrets"`, `ClientSigningKeysTable = "ClientSigningKeys"`, `ClientRedirectUrisTable = "ClientRedirectUris"`, `ClientPostLogoutRedirectUrisTable = "ClientPostLogoutRedirectUris"`, `ClientCorsOriginsTable = "ClientCorsOrigins"`.
- `Persistence/ClientManagementDbContext.cs` — schema `"ClientManagement"`, `DbSet<Client> Clients`, same `OnModelCreating` shape as `TenancyDbContext`.
- `Persistence/Configurations/ClientConfiguration.cs`, `ClientSecretConfiguration.cs`, `ClientSigningKeyConfiguration.cs` (mapping detail below).
- `Persistence/ClientRepository.cs` — implements `IClientRepository`.
- `Persistence/ClientManagementDbContextDesignTimeFactory.cs` — mirrors `TenancyDbContextDesignTimeFactory`.
- `DependencyInjection.cs` — `AddClientManagementInfrastructure(configuration)`: registers `DbContext<ClientManagementDbContext>` with the interceptors, `IClientManagementDbContext`, `IClientRepository`, runs `MigrateAsync` at startup (same pattern as Tenancy).

### `WardenId.ClientManagement.Presentation`

Mirrors `WardenId.Tenancy.Presentation`:
- `WardenId.ClientManagement.Presentation.csproj` — `Asp.Versioning.Http`, `ProjectReference` to `Application`, `SharedKernel.Presentation`.
- `DependencyInjection.cs` — `AddClientManagementPresentation()` → `services.AddClientManagementApplication()`.
- `Features/Clients/Create/{Endpoint,Request}.cs`
- `Features/Clients/List/{Endpoint,Request}.cs`
- `Features/Clients/Get/Endpoint.cs`
- `Features/Clients/Update/{Endpoint,Request}.cs`
- `Features/Clients/Delete/Endpoint.cs`

### Gateway wiring

- `WardenId.Gateway/DependencyInjection/ClientManagementDependencyInjection.cs` — `AddClientManagementModule(configuration)`: calls `AddClientManagementInfrastructure(configuration)` then `AddClientManagementPresentation()` (mirrors `TenancyDependencyInjection.AddTenancyModule`).
- `Program.cs` — add `builder.Services.AddClientManagementModule(builder.Configuration);` alongside the existing `AddTenancyModule`/`AddSharedKernelModule` calls.
- `WardenId.slnx` — register the three new `.csproj` files under `/src/WardenId.ClientManagement/`.
- `Directory.Packages.props` — no new package versions needed; EF Core/Npgsql/MediatR/FluentResults/Asp.Versioning.Http versions already exist there for the Tenancy module.

## Persistence Mapping

### `Client` aggregate root

`ClientConfiguration : IEntityTypeConfiguration<Client>`, table `Clients`:
- `ClientId` (VO) → `HasConversion(id => id.Value, value => ClientId.FromTrustedSource(value))`, `HasMaxLength(32)`, unique index scoped to `TenantId` (`client_id` is unique *per tenant*, not globally — matches the domain spec).
- `TenantId` → plain `Guid` column, indexed (every list/get query filters by it).
- `ClientType`, `ClientAuthenticationMethod` → `HasConversion<string>()`, matches `Tenant.Status` convention.
- `GrantType` → `HasConversion<string>()` (flags enum serialized as a delimited string, e.g. via `Enum.ToString()`/`Enum.Parse()` — `[Flags]` `ToString()` already produces a comma-joined name list, which round-trips through `Enum.Parse` cleanly, so no custom converter logic beyond the two lambdas).
- `DisplayName` → `HasMaxLength(200)`, required.
- `Description` → `HasMaxLength(1000)`, optional.
- `TokenLifetimeOverrides` (nullable owned VO, three `int?` fields) → `OwnsOne`, mapped to nullable columns on the `Clients` table directly (no separate table — it's a single optional composite, not a collection).

### Child entity tables (mirror `SigningKey`/`Tenant.SigningKeys` exactly)

- `ClientSecretConfiguration : IEntityTypeConfiguration<ClientSecret>` → table `ClientSecrets`. `HashedValue` required, `Label` `HasMaxLength(200)`, `ExpiresAt` nullable. Shadow FK `ClientId`, indexed.
- `ClientSigningKeyConfiguration : IEntityTypeConfiguration<ClientSigningKey>` → table `ClientSigningKeys`. `Kid` `HasMaxLength(100)` unique per client, `PublicKeyJwk` `HasColumnType("jsonb")`, `Algorithm` `HasMaxLength(20)`, `ExpiresAt` nullable. Shadow FK `ClientId`, indexed.
- On `ClientConfiguration`: `HasMany(c => c.Secrets).WithOne().OnDelete(DeleteBehavior.Cascade)`, `HasMany(c => c.SigningKeys).WithOne().OnDelete(DeleteBehavior.Cascade)`, both `.Navigation(...).UsePropertyAccessMode(PropertyAccessMode.Field)`.

### Value-object collections (new pattern — no direct precedent in Tenancy)

`RedirectUris`, `PostLogoutRedirectUris`, `CorsOrigins` are collections of identity-less value objects (a bare wrapped string each). Mapped via `OwnsMany` directly on `ClientConfiguration`, each into its own small table:

```csharp
builder.OwnsMany(c => c.RedirectUris, b =>
{
    b.ToTable(SchemaConsts.ClientRedirectUrisTable);
    b.WithOwner().HasForeignKey("ClientId");
    b.Property(r => r.Value).HasColumnName("Value").HasMaxLength(2000).IsRequired();
    b.HasKey("ClientId", "Value");
});
builder.Navigation(c => c.RedirectUris).UsePropertyAccessMode(PropertyAccessMode.Field);
```

Same shape for `PostLogoutRedirectUris` (table `ClientPostLogoutRedirectUris`) and `CorsOrigins` (table `ClientCorsOrigins`, `HasMaxLength(255)`). Composite key `(ClientId, Value)` — natural, since dedup-by-value is already a domain invariant (`AddRedirectUri` no-ops on duplicates) and these have no other identity.

### List/Get query filtering

Follows `TenantRepository`'s manual-predicate convention (no global `HasQueryFilter`): every query explicitly adds `&& !x.IsDeleted`.

## Soft-Delete Interceptor Fix

**Bug found, blocks endpoint 5:** `DbContextBase.CheckAndUpdateEntities` (`src/WardenId.SharedKernel.Persistence/DbContextBase.cs:46-49`) calls `entry.Entity.MarkAsDeleted(userId)` for `EntityState.Deleted` entries but never changes `entry.State`. EF Core still issues a physical `DELETE` for a `Deleted`-state entry regardless of property values set on it — so today, removing an entity via `dbContext.Remove(...)` hard-deletes the row; the `IsDeleted`/`DeletedById`/`DeletedAt` values set immediately beforehand are discarded along with the row. No existing feature (Tenant has no delete endpoint) has exercised this path, so it's gone unnoticed.

**Fix** (in `CheckAndUpdateEntities`, both the `AggregateRoot` and `Entity` deleted-branches):

```csharp
ChangeTracker
    .Entries<AggregateRoot>()
    .Where(e => e.State == EntityState.Deleted)
    .ToList().ForEach(entry =>
    {
        entry.Entity.MarkAsDeleted(userId);
        entry.State = EntityState.Modified;
    });
```

Same change to the parallel `Entity`-typed loop. This turns every `dbContext.Remove(...)` repo-wide into a soft delete going forward — a shared-kernel change, but a strict bug fix (makes the documented CLAUDE.md behavior — "audit fields... all set by EF interceptor logic" — actually true) rather than new behavior, and it's required for `DELETE /{clientId}` to do anything sensible. `ClientRepository.DeleteAsync` will call `dbContext.Clients.Remove(client)` + `SaveChangesAsync`, relying on this fix.

## Domain Additions (small, same shape as existing behaviors)

`Update` needs two setters that don't exist on `Client` yet (`UpdateTokenLifetimeOverrides`/`Disable`/`Enable` already exist and cover the rest of the PATCH body):

- `Client.UpdateDisplayName(string displayName)` → `Result` — same validation as `Create` (required, ≤200 chars), reuses `ClientErrors.ClientDisplayNameRequired()`/`ClientDisplayNameTooLong()`. No new event (display-name change isn't a security-relevant transition like `Disable`).
- `Client.UpdateConsentRequired(bool consentRequired)` → `void`, plain setter, no validation, no event (mirrors `Enable()`'s no-event shape).

Both added directly to `src/WardenId.ClientManagement.Domain/Client.cs`, with unit tests in the existing `WardenId.ClientManagement.Domain.Tests` project (`ClientUpdateDisplayNameTests.cs`, `ClientUpdateConsentRequiredTests.cs`).

## CQRS Features

### Create

`Command(Guid TenantId, ClientType ClientType, string DisplayName, GrantType GrantType, ClientAuthenticationMethod ClientAuthenticationMethod, bool IsFirstParty, bool ConsentRequired, string? Description) : IRequest<Result<Guid>>`.

`Handler` — calls `Client.Create(...)`, `repository.CreateAsync(client, userId, ct)`, returns `client.Id` (the aggregate's `Guid Id`, not `ClientId` — matches `Tenant.Create`'s handler returning `tenant.Id`). No pre-existence check needed (`ClientId` is system-generated and unique by construction, unlike `Tenant.Slug`).

### List

`Query(Guid TenantId, int PageNumber, int PageSize, bool? Enabled, ClientType? ClientType) : IRequest<Result<PaginationListResponse<ClientListItem>>>` — extends `PaginationListRequest` isn't a fit here since that base type doesn't carry filters; `Query` is its own record implementing the same page/pageSize shape rather than inheriting, since `PaginationListRequest` is abstract and its `SortDirection` default doesn't map cleanly onto client-list sorting needs for this first usage. (Revisit consolidating onto the shared base once a second List feature exists and the real common shape is clearer — YAGNI for now.)

`ClientListItem(Guid Id, string ClientId, string DisplayName, ClientType ClientType, bool Enabled, bool IsFirstParty, DateTimeOffset CreatedAt)` — projection record, no secrets/keys/URIs (list view is a summary).

`Handler` — queries `dbContext.Clients.Where(c => c.TenantId == tenantId && !c.IsDeleted)`, applies `Enabled`/`ClientType` filters when provided, orders by `CreatedAt` descending, paginates (`Skip`/`Take`), projects to `ClientListItem`, wraps in `PaginationListResponse<ClientListItem>(totalCount, items)`.

### Get

`Query(Guid TenantId, Guid ClientId) : IRequest<Result<ClientDetail>>` — `ClientId` here is the aggregate's `Guid Id` (route segment `{clientId}`), not the OAuth `ClientId` VO — matches every other endpoint's `{clientId}` route param.

`ClientDetail` — full projection: all `Client` scalar properties, `RedirectUris`/`PostLogoutRedirectUris`/`CorsOrigins` as `string[]`, `Secrets` as `ClientSecretSummary(Guid Id, string Label, DateTimeOffset? ExpiresAt)[]` (**no `HashedValue` — never returned**, per the brief's plaintext-secret constraint extending naturally to hash exposure too), `SigningKeys` as `ClientSigningKeySummary(Guid Id, string Kid, string Algorithm, DateTimeOffset? ExpiresAt)[]` (no `PublicKeyJwk` omission needed — that's public key material, safe to return, but keeping the summary shape consistent), `AssignedScopeIds` as `Guid[]`.

`Handler` — `dbContext.Clients.Where(c => c.Id == clientId && c.TenantId == tenantId && !c.IsDeleted).Include(...)` (secrets/signing keys/URI collections), `FirstOrDefaultAsync`, `ClientErrors.NotFound` if missing, else project to `ClientDetail`.

### Update

`Command(Guid TenantId, Guid ClientId, string? DisplayName, bool? Enabled, bool? ConsentRequired, int? AccessTokenSeconds, int? IdTokenSeconds, int? RefreshTokenSeconds, bool ClearTokenLifetimeOverrides) : IRequest<Result>` — every field optional/nullable except the route-bound `TenantId`/`ClientId`; only provided fields are applied (PATCH semantics). `ClearTokenLifetimeOverrides` is an explicit flag (distinct from "all three token fields null, so leave alone") so callers can deliberately reset to tenant defaults, matching `UpdateTokenLifetimeOverrides(null)`'s documented "fully inherit" behavior.

`Handler`:
1. `repository.GetByIdAsync(tenantId, clientId, ct)` → `ClientErrors.NotFound` if missing.
2. If `DisplayName` provided → `client.UpdateDisplayName(DisplayName)`, short-circuit on failure.
3. If `Enabled` provided → `true` calls `Enable()`, `false` calls `Disable()`.
4. If `ConsentRequired` provided → `UpdateConsentRequired(...)`.
5. If `ClearTokenLifetimeOverrides` → `UpdateTokenLifetimeOverrides(null)`; else if any of the three token fields provided → `TokenLifetimeOverrides.Create(...)` (short-circuit on validation failure) → `UpdateTokenLifetimeOverrides(result.Value)`.
6. `repository.UpdateAsync(client, userId, ct)` (calls `SaveChangesAsync` — EF change-tracking on the already-attached aggregate picks up the mutations, no explicit `Update()` call needed, matches standard EF Core tracked-entity semantics).

### Delete

`Command(Guid TenantId, Guid ClientId) : IRequest<Result>`.

`Handler` — `repository.GetByIdAsync(tenantId, clientId, ct)` → `NotFound` if missing, else `repository.DeleteAsync(client, userId, ct)` → `dbContext.Clients.Remove(client)` + `SaveChangesAsync(userId, ct)`, relying on the interceptor fix to make this a soft delete.

### `IClientRepository`

```csharp
public interface IClientRepository
{
    Task CreateAsync(Client client, Guid userId, CancellationToken ct = default);
    Task<Result<Client>> GetByIdAsync(Guid tenantId, Guid clientId, CancellationToken ct = default);
    Task UpdateAsync(Client client, Guid userId, CancellationToken ct = default);
    Task DeleteAsync(Client client, Guid userId, CancellationToken ct = default);
}
```

`List`/`Get`-for-detail bypass the repository and use `IClientManagementDbContext` directly (per the Context section's rationale — ad-hoc projection/filtering doesn't fit a narrow single-aggregate repo interface). `GetByIdAsync` (used by Update/Delete) loads the full aggregate including child collections (`Include` on Secrets/SigningKeys/URI owned collections) since handlers may call any aggregate behavior against it.

## Endpoints

All under `/api/v{version:apiVersion}/admin/tenants/{tenantId}/clients`, `NewApiVersionSet("Clients")`, `HasApiVersion(new ApiVersion(1))`, `.RequireAuthorization()`, `result.HandleApiFailure()` on failure — exact shape of the existing `Tenants/Create/Endpoint.cs`.

| # | Method | Route | Request → Command/Query | Response |
|---|--------|-------|--------------------------|----------|
| 1 | POST | `/` | `Request(ClientType, DisplayName, GrantType, ClientAuthenticationMethod, IsFirstParty, ConsentRequired, Description?)` → `Create.Command` | `201 Created`, body `{ id }` |
| 2 | GET | `/` | query params `pageNumber, pageSize, enabled?, clientType?` → `List.Query` | `200 OK`, `PaginationListResponse<ClientListItem>` |
| 3 | GET | `/{clientId}` | route → `Get.Query` | `200 OK`, `ClientDetail`, or `404` |
| 4 | PATCH | `/{clientId}` | `Request(DisplayName?, Enabled?, ConsentRequired?, AccessTokenSeconds?, IdTokenSeconds?, RefreshTokenSeconds?, ClearTokenLifetimeOverrides)` → `Update.Command` | `204 No Content`, or `404`/`400` |
| 5 | DELETE | `/{clientId}` | route → `Delete.Command` | `204 No Content`, or `404` |

Every `Request.ToCommand(tenantId, clientId, ...)` mapper takes the route-bound values as explicit parameters (per the tenant-resolution decision) alongside body/query fields.

## Errors

New `ClientErrors` factories needed beyond what Domain already defines (`ClientDisplayNameRequired`/`Too Long`, `PublicClientCannotHaveSecret`, `SecretNotFound`, `SigningKeyRequiresPrivateKeyJwt`, `NotFound`): none — `NotFound` already exists and covers Get/Update/Delete's missing-client case; `Validation(reason)` is generic enough for any Application-layer check. No new error codes/metadata-key cases needed in `HandleApiFailure()` — `NotFound`/`Validation` already map to 404/400 respectively.

## Testing

- **Domain**: unit tests for the two new setters (`ClientUpdateDisplayNameTests`, `ClientUpdateConsentRequiredTests`) in the existing `WardenId.ClientManagement.Domain.Tests` project.
- **Application**: no test project exists for any `*.Application` layer in the repo today (Tenancy.Application has none either) — matching that precedent, no new Application-layer test project is introduced in this pass. (Flagging, not silently deviating: if this pass should break precedent and add handler unit tests, say so before implementation starts.)
- **Infrastructure/Presentation**: no integration/endpoint test project exists anywhere in the repo either. Same precedent-matching call — none added here.
- **Manual verification**: `dotnet build WardenId.slnx`, `dotnet ef migrations add AddClientManagementSchema --project src/WardenId.ClientManagement.Infrastructure --startup-project WardenId.Gateway --context ClientManagementDbContext`, then `dotnet run --project WardenId.Gateway` + exercise all 5 endpoints via Swagger.

## Explicitly Out of Scope (this pass)

- RBAC policy, roles, JWT authentication middleware (`AddAuthentication`/`AddJwtBearer`) — bare `.RequireAuthorization()` only.
- Audit-log consumer/table — domain events already publish through the existing CAP/`DomainEventsInterceptor` pipeline unchanged; nothing subscribes to them yet, and building that subscriber is not part of this pass.
- Secrets endpoints (6-7: issue/expire), redirect-uri endpoints (8-9: add/remove), CORS endpoint (10: replace) — separate future specs, each small enough to design independently once this CRUD core lands.
- Scope assignment endpoints (11-12) — blocked on the `Scope` aggregate not existing; deferred until that aggregate is built.
- `Properties` metadata bag, Dynamic Client Registration, `private_key_jwt` client-auth flow validation, `ApiResource`/audience endpoints — out of scope per the original brief.
