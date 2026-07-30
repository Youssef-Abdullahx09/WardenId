# Client Aggregate — Domain Layer Design

Date: 2026-07-24
Module: `WardenId.ClientManagement.Domain`
Scope: **Domain layer only.** No Application/Infrastructure/Presentation projects in this pass — those, along with the `Scope`, `ApiResource`, and `InitialAccessToken` aggregates, are deferred to future specs.

## Context

`WardenId.ClientManagement.Domain` currently has stub files only (`Client.cs`, `GrantType.cs`, `RedirectUri.cs`) with no validation, factories, or behaviors. This spec fleshes out the `Client` aggregate following the conventions established in `WardenId.Tenancy.Domain` (see `Tenant.cs`, `Slug.cs`, `SigningKey.cs`, `TenantErrors.cs`).

`AssignedScopes`/`AssignScope`/`RemoveScope` operate on bare `Guid` scope IDs for now — the `Scope` aggregate doesn't exist yet, so there's no cross-aggregate tenant-ownership check. That check gets added when `Scope` is built.

## Enums

- `GrantType` — `[Flags]` enum, power-of-two values: `AuthorizationCode = 1, ClientCredentials = 2, RefreshToken = 4, DeviceCode = 8`. (Existing stub has sequential 1-4 values; must change to support combining grants on one client.)
- `ClientType` — `Confidential = 1, Public = 2`
- `ClientAuthenticationMethod` — `SharedSecret = 1, PrivateKeyJwt = 2`

## Value Objects

All follow the `Slug`-style split: `Create(raw)` for validated external input, `FromTrustedSource(value)` for EF materialization (skips validation).

- **`RedirectUri`** — wraps a single URI string. `Create` validates: non-empty, well-formed absolute URI, no wildcard segments, no fragment (RFC 9700 exact-match only). Used for both `RedirectUris` and `PostLogoutRedirectUris` — same type, same rules, both stored as owned collections on `Client`.
- **`CorsOrigin`** — wraps an origin string (scheme + host [+ port], no path/query). `Create` validates format.
- **`TokenLifetimeOverrides`** — VO with `AccessTokenSeconds`, `IdTokenSeconds`, `RefreshTokenSeconds` (`int?` each, null = inherit tenant default). `Create` validates any provided value is positive. `Client.TokenLifetimeOverrides` itself is nullable (whole VO absent = inherit everything).
- **`ClientId`** — the OAuth `client_id` string, distinct from the aggregate's `Id` (Guid). System-generated only: `Generate()` produces a crypto-random, url-safe opaque string (32 chars). `FromTrustedSource(value)` for EF. No `Create(raw)` — callers never supply their own.

## Child Entities

Both derive from `SharedKernel.Domain.Entity` (gets `Id`, audit fields for free), mirroring `SigningKey` in Tenancy.

- **`ClientSecret`** — `HashedValue`, `Label`, `ExpiresAt` (`DateTimeOffset?`). `Create(hashedValue, label, expiresAt)`. A client can hold multiple secrets simultaneously (rotation overlap window) — not deleted on rotation, just expiry set.
- **`ClientSigningKey`** — `Kid`, `PublicKeyJwk`, `Algorithm`, `ExpiresAt`. `Create(kid, publicKeyJwk, algorithm, expiresAt)`. Only populated for `PrivateKeyJwt` clients.

## Client Aggregate

### State

`ClientId` (VO), `TenantId` (Guid), `ClientType`, `DisplayName`, `Description`, `Enabled` (bool), `IsFirstParty` (bool), `RedirectUris` (owned `List<RedirectUri>`), `PostLogoutRedirectUris` (owned `List<RedirectUri>`), `AllowedGrantTypes` (flags `GrantType`), `RequirePkce` (bool), `CorsOrigins` (owned `List<CorsOrigin>`), `TokenLifetimeOverrides` (nullable VO), `ClientAuthenticationMethod`, `ConsentRequired` (bool), `Secrets` (`List<ClientSecret>`), `SigningKeys` (`List<ClientSigningKey>`), `AssignedScopeIds` (`List<Guid>`).

All collections exposed as `IReadOnlyCollection<T>` (backing `_field` private), same pattern as `Tenant.SigningKeys`.

### Create

```
Client.Create(tenantId, clientType, displayName, allowedGrantTypes, clientAuthenticationMethod, isFirstParty, consentRequired) -> Result<Client>
```

- `DisplayName` required, ≤200 chars (same validation shape as `Tenant.Create`)
- `ClientType.Public` ⇒ force `RequirePkce = true` unconditionally (non-negotiable per design)
- `ClientType.Confidential` ⇒ `RequirePkce` defaults `false` (optional, not forced)
- `ClientId` assigned via `ClientId.Generate()`
- Raises `ClientCreatedDomainEvent`

Secrets/signing keys are NOT passed into `Create` — they're added afterward via `AddSecret`/`AddSigningKey`, so `Create` doesn't need to branch on "Confidential requires a secret or signing key at creation time." (Deferred: enforcing that a Confidential client eventually has at least one credential is an Application-layer concern once persistence/handlers exist.)

### Behaviors

- `AddRedirectUri(uri)` / `RemoveRedirectUri(uri)` — dedupe on add, no-op on remove-missing
- `AddPostLogoutRedirectUri(uri)` / `RemovePostLogoutRedirectUri(uri)` — same shape
- `AddCorsOrigin(origin)` / `RemoveCorsOrigin(origin)` — same shape
- `ValidateRedirectUri(requested)` → `bool` — exact string match against `RedirectUris`. Lives on the aggregate so it's unit-testable and reusable by conformance tests, not duplicated in an authorize-endpoint handler.
- `AddSecret(secret)` — fails with validation error if `ClientType.Public`
- `RotateSecret(newSecret, overlapExpiresAt)` — adds `newSecret`; sets `ExpiresAt = overlapExpiresAt` on all currently-non-expired existing secrets (doesn't remove them — dual validity during rotation window). Raises `ClientSecretRotatedDomainEvent`
- `ExpireSecret(secretId)` — sets that secret's `ExpiresAt` to now immediately (kill a leaked secret), independent of rotation. Fails if secret not found. Raises `ClientSecretExpiredDomainEvent`
- `AddSigningKey(key)` — fails with validation error if `ClientAuthenticationMethod != PrivateKeyJwt`
- `AssignScope(scopeId)` — dedupe (no-op if already assigned). Raises `ClientScopesChangedDomainEvent`
- `RemoveScope(scopeId)` — no-op if not assigned. Raises `ClientScopesChangedDomainEvent`
- `Disable()` — sets `Enabled = false`. Raises `ClientDisabledDomainEvent` (the event is the hook for downstream active-token/session revocation — not implemented in this pass, just the event)
- `Enable()` — sets `Enabled = true`. No event (symmetry for re-enabling; revocation only matters going one direction)
- `UpdateTokenLifetimeOverrides(overrides)` — replaces the VO wholesale (including setting it back to null to fully inherit tenant defaults)

### Errors & Events

- `ClientErrorCodes` / `ClientErrors` static classes, same shape as `TenantErrorCodes`/`TenantErrors` (`MetadataKey`, `NotFound`, `Validation(reason)`, plus specific factory methods per invariant above: `ClientDisplayNameRequired`, `ClientDisplayNameTooLong`, `ClientPkceRequiredForPublic` n/a since forced not rejected, `ClientPublicCannotHaveSecret`, `ClientSecretNotFound`, `ClientSigningKeyRequiresPrivateKeyJwt`, `ClientScopeAlreadyAssigned` n/a since no-op — only real failure cases get error factories).
- `Events/` folder: `ClientCreatedDomainEvent(ClientId ClientId)`, `ClientSecretRotatedDomainEvent(Guid SecretId)`, `ClientSecretExpiredDomainEvent(Guid SecretId)`, `ClientScopesChangedDomainEvent(Guid ScopeId, bool Assigned)`, `ClientDisabledDomainEvent()`.

## Explicitly Out of Scope (this pass)

- `Properties` metadata bag — no consumer yet, add when a real feature needs it.
- `SoftDelete()` — no existing pattern even on `Tenant`; soft-delete is owned by the EF interceptor, not a domain method.
- `Scope`, `ApiResource`, `InitialAccessToken` aggregates.
- Application/Infrastructure/Presentation projects, CQRS commands/queries, EF configuration, Gateway wiring.
- Enforcing "Confidential must have ≥1 secret or signing key" — becomes an Application-layer concern once there's a handler to enforce it against persisted state.
