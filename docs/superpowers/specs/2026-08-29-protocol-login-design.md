# Protocol Login Design

Date: 2026-08-29
Module: `WardenId.Protocol` (new module — Domain/Application/Infrastructure/Presentation), plus one small addition to `WardenId.Tenancy.Application`
Scope: A login page that authenticates a user and issues an access token, refresh token, and ID token. **Not** a full OAuth2/OIDC authorization server — no `/authorize`, no `/token`, no client registration, no redemption of the refresh token. Those are explicitly future passes; see "Explicitly out of scope."

## Context

`Identity.Application`'s `Authenticate` command (see `docs/superpowers/specs/2026-07-27-identity-application-design.md` §Authentication) validates credentials and returns `Result<AuthenticatedUser>` — deliberately stopping short of issuing a token, since that ties to `Tenancy`'s per-tenant ES256 signing keys and was flagged as "a separate future pass." That pass is this spec.

`Tenancy.Application.Abstractions.SigningKeyMaterial` already documents the intended shape: `EncryptedPrivateKey` "is PKCS8 key material protected at rest — only ever decrypted inside the Protocol module, at token-signing time." No `Protocol` module exists yet; this spec creates it, following the same 4-project layered pattern as `Tenancy`/`ClientManagement`/`Identity`.

Every existing module is decoupled from every other — no module's `.csproj` references another module's `Application`/`Domain` project (verified across all four current modules). Modules that need each other's data do so in-process via MediatR (`ISender`), the same mechanism already used for domain-event publishing (`ICapPublisher`) inside `DomainEventsInterceptor`. `Protocol` follows this convention rather than taking a `ProjectReference` to `Identity.Application` or `Tenancy.Application`.

`Microsoft.IdentityModel.JsonWebTokens` (v8.21.0) is already centrally pinned in `Directory.Packages.props` but unreferenced anywhere in the repo — staged for this work.

## Module structure

New projects, added to `WardenId.slnx`:

- **`WardenId.Protocol.Domain`** — `RefreshToken` entity only. Zero framework dependencies (references `SharedKernel.Domain` only, matching every other `<Module>.Domain`).
- **`WardenId.Protocol.Application`** — `Features/Login/` (Command + Handler), `Abstractions/IJwtIssuer`, `Abstractions/IRefreshTokenRepository`, `ProtocolTokenOptions`. References `SharedKernel.Application` + `Protocol.Domain` only.
- **`WardenId.Protocol.Infrastructure`** — `JwtIssuer` (implements `IJwtIssuer`), EF `DbContext`/config/migrations for `RefreshToken`, `AddProtocolInfrastructure` DI wiring. References `SharedKernel.Persistence` + `Protocol.Application` only.
- **`WardenId.Protocol.Presentation`** — `Features/Login/Endpoint.cs` (GET + POST), HTML rendering helper, `AddProtocolPresentation` DI wiring. References `SharedKernel.Presentation` + `Protocol.Application` only.

Gateway: new `WardenId.Gateway/DependencyInjection/ProtocolDependencyInjection.cs` (`AddProtocolModule`), wired into `Program.cs` after `AddIdentityModule`.

## Cross-module communication

`Protocol.Application`'s `Login` handler never touches `Identity` or `Tenancy` code directly — it sends two MediatR requests through the same `ISender` every handler already has:

1. `Identity.Application.Features.Users.Authenticate.Command(TenantId, EmailOrUsername, Password)` — reuses the existing handler unchanged (password verify, lockout, rehash, `PendingActions` all stay exactly as today).
2. A **new** `Tenancy.Application.Features.SigningKeys.GetActiveForSigning.Query(TenantId)` — added to `Tenancy.Application` in this pass, same file layout as `Features/Tenants/Create/`. Returns `Result<ActiveSigningKey>` where `ActiveSigningKey = (Guid KeyId, string Algorithm, byte[] EncryptedPrivateKey)`. The handler is a straight repository read of the tenant's active `SigningKey` — no new decryption logic in `Tenancy`; the encrypted bytes travel to `Protocol` as-is.

`Protocol.Infrastructure`'s `JwtIssuer` decrypts the key itself, via its own `IDataProtector`. The purpose string `"WardenId.Tenancy.SigningKeys.v1"` currently lives as a private const inside `Es256SigningKeyGenerator` — this pass promotes it to a public const (`Tenancy.Application.Abstractions.SigningKeyProtectionPurposes.PrivateKey`) so `Protocol` references the same string without duplicating the literal. Both modules share the app's single Data Protection key ring (per existing convention — see `Es256CheckpointSigner`'s comment on why a second `PersistKeysToDbContext` call would be unsafe); the purpose string alone keeps the derived key material distinct per-consumer.

Rejected: giving `Protocol` a `ProjectReference` to `Identity.Application`/`Tenancy.Application` directly. Would be simpler wiring but breaks the zero-cross-reference convention every other module currently holds, for no benefit — MediatR already gives in-process, same-transaction-free composition without coupling assembly graphs.

## Data model

**`RefreshToken`** (`Protocol.Domain`, own schema `protocol`, own `__EFMigrationsHistory`):

- `Id` (`Guid.CreateVersion7()`, PK)
- `TenantId`, `UserId`
- `TokenHash` (`byte[]`, SHA-256 of the raw opaque token — raw value is generated, returned once in the response, and never persisted, same pattern as `EmailVerificationToken`/`PasswordResetToken`)
- `ExpiresAtUtc`
- `IsRevoked` (`bool`, default `false` — present now so the future redemption pass can revoke-on-rotation without a migration; unused by this pass)
- `CreatedAtUtc`

Static factory `RefreshToken.Create(tenantId, userId, tokenHash, expiresAtUtc)`, private constructor for EF materialization — same convention as every other aggregate.

`IRefreshTokenRepository.CreateAsync(RefreshToken, CancellationToken)` — write-only in this pass; no `GetByHashAsync` needed until redemption exists.

## Login flow

`Protocol.Presentation`, route `/api/v{version:apiVersion}/tenants/{tenantId:guid}/auth/login` (same `tenantId`-in-route shape as the existing `authenticate` endpoint, for consistency within this route family):

- **`GET`** — renders a static HTML login form (`Results.Content(html, "text/html")`; no Razor/static-file middleware exists in the Gateway yet, so this is a hand-written string template, not a `.cshtml`/`wwwroot` asset). Form posts to the same URL.
- **`POST`** — `[FromForm]` `EmailOrUsername`/`Password` → `Login.Command(TenantId, EmailOrUsername, Password)`.

`Login.Handler`:

1. Send `Authenticate.Command`. On failure, propagate the same `Result` (same `UserErrors.InvalidCredentials`/`AccountLocked` — no new error codes here).
2. On success: if `AuthenticatedUser.Status != Active` or `PendingActions.Count > 0`, return `Result.Ok(new LoginResult(AuthenticatedUser, Tokens: null))` — token issuance is skipped, matching `Authenticate`'s existing contract (this pass does not add an MFA challenge step; `VerifyMfaFactor` stays a separate, unwired concern, unchanged from today).
3. Otherwise: send `GetActiveForSigning.Query(TenantId)`. Failure here (tenant has no active key — shouldn't happen post-provisioning, but `Tenant.AssignInitialSigningKey` is the only thing that sets one) maps to a new `TenantErrors.NoActiveSigningKey()` (400 via default `HandleApiFailure` mapping — no new case needed in the switch).
4. Build claims, call `IJwtIssuer.IssueAccessToken(...)` / `IssueIdToken(...)`, generate a 32-byte random refresh token (same generation approach as `ClientSecret`), hash it, `RefreshToken.Create(...)`, `repository.CreateAsync(...)`.
5. Return `Result.Ok(new LoginResult(AuthenticatedUser, new TokenSet(AccessToken, RefreshToken, IdToken, TokenType: "Bearer", ExpiresIn)))`.

**Presentation response**: on success, the POST endpoint renders an HTML page showing the `TokenSet` as formatted JSON (`<pre>` block) — a placeholder page, explicitly meant to be replaced by a real redirect once `/authorize` exists. On failure (bad credentials, locked, pending actions), re-renders the login form with an inline error message; pending-actions/unverified case shows a distinct message (e.g. "verify your email before signing in") rather than issuing tokens.

## Token claims

Configurable via new `ProtocolTokenOptions` (`Protocol.Application`, bound the same way as `IdentityPolicyOptions`):

```
IssuerBaseUri, AccessTokenLifetime (default 15m), IdTokenLifetime (default 15m), RefreshTokenLifetime (default 30d)
```

- **Common claims**: `iss` = `{IssuerBaseUri}/tenants/{tenantId}`, `sub` = `userId`, `iat`, `exp`, `nbf`, header `kid` = active signing key id, `alg: ES256`.
- **Access token** adds: `tenant_id`, `token_use: "access"`.
- **ID token** adds: `email`, `email_verified`, `preferred_username`, `token_use: "id"`.
- No `aud` claim yet — there is no client/audience concept until OAuth client registration exists; adding one now would be a placeholder guess. Noted as a gap to close when `/authorize` lands.
- Refresh token is an opaque random string, not a JWT — nothing to decode, only to hash-and-compare (once redemption exists).

## Error handling

No changes to `Identity` or existing `Tenancy` error codes. New:

- `TenantErrors.NoActiveSigningKey()` (`Tenancy.Domain`, new `TenantErrorCodes` entry) — falls through to `HandleApiFailure`'s default 400 case; not important enough to earn a dedicated HTTP status.

No new codes needed for `Protocol` itself — every failure this pass can produce originates from `Identity` or `Tenancy` errors it re-propagates.

## Manual verification

No test project exists repo-wide yet (`tests/` is empty) — consistent with the rest of the codebase, this pass does not introduce one unprompted. Verification path: register a user via Swagger → verify email → `GET` the login page in a browser → submit credentials → confirm the rendered JSON page contains all four fields → paste `access_token` into jwt.io, confirm claims match §Token claims → verify the ES256 signature against the tenant's public JWK (`SigningKey.PublicKeyJwk`, currently not exposed via any endpoint — decode manually from the DB row for this pass, since a JWKS endpoint is out of scope here too).

## Explicitly out of scope (this pass)

- **Refresh token redemption** (`POST /token`, `grant_type=refresh_token`, rotation, reuse detection) — `RefreshToken.IsRevoked` exists in the schema now so that pass doesn't need a migration, but nothing redeems a token in this pass.
- **Full OAuth2/OIDC authorization-code flow** — `/authorize`, `/token`, client registration, `redirect_uri` validation, consent, `state`/`PKCE`. This login page is a building block toward that, not that flow itself.
- **JWKS endpoint** (`/.well-known/jwks.json` or similar) — public keys exist (`SigningKey.PublicKeyJwk`) but nothing serves them yet.
- **MFA challenge at login** — `Authenticate`'s existing behavior (no MFA gate) is preserved unchanged; `EnrollMfaFactor`/`VerifyMfaFactor` remain unwired to any login path.
- **`aud` claim / audience validation** — no client concept exists to populate it meaningfully yet.
- **Session cookies / redirect-based success response** — success renders tokens as JSON-in-HTML per this pass's decision; cookie-based or redirect-based delivery is deferred to when `/authorize` exists and needs it.
- **Tenant status enforcement at login** — `GetActiveForSigning` does not check `Tenant.Status`; a `Suspended`/`Archived` tenant's users can still authenticate and receive valid tokens today. Revisit once tenant-suspension semantics are defined consistently across modules.
