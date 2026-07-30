# Identity Application Layer Design

Date: 2026-07-27
Module: `WardenId.Identity.Application` (new project)
Scope: **Application layer only.** No Infrastructure/Presentation/Gateway wiring in this pass.

## Context

Follows `WardenId.Identity.Domain` (see `docs/superpowers/specs/2026-07-26-identity-domain-design.md` and its README) which built the `User` aggregate, its child entities, domain services, and the `IPasswordHasher`/`IBreachedPasswordChecker` ports, but explicitly deferred `IUserRepository` and everything above Domain.

This pass follows the same layered pattern already established by `WardenId.Tenancy.Application` and `WardenId.ClientManagement.Application`: MediatR commands/queries under `Features/<Aggregate>/<UseCase>/`, an `Abstractions/` folder for repository/DbContext ports Infrastructure will implement later, a `DependencyInjection.cs` registering MediatR from the assembly.

Covers all 12 `User` behaviors currently exposed by Domain, plus an `Authenticate` command (login orchestration — not a Domain behavior on its own, since it composes `IPasswordHasher.Verify` with `RecordSuccessfulLogin`/`RecordFailedLogin`).

## Abstractions

- **`IUserRepository`** (`Identity.Application/Abstractions`) — same shape as `ITenantRepository`/`IClientRepository`:
  - `CreateAsync(User user, Guid userId, CancellationToken)`
  - `GetByIdAsync(Guid tenantId, Guid userId, CancellationToken) -> Result<User>`
  - `GetByEmailAsync(Guid tenantId, Email email, CancellationToken) -> Result<User>`
  - `GetByUsernameAsync(Guid tenantId, Username username, CancellationToken) -> Result<User>`
  - `ExistsByEmailAsync(Guid tenantId, Email email, CancellationToken) -> Task<bool>`
  - `ExistsByUsernameAsync(Guid tenantId, Username username, CancellationToken) -> Task<bool>`
  - `UpdateAsync(User user, Guid userId, CancellationToken)`
  - `SoftDeleteAsync(User user, Guid userId, CancellationToken)` — mirrors `IClientRepository.DeleteAsync`; Infrastructure is where `MarkAsDeleted` actually gets called, not Application.
- **`IIdentityDbContext : IDbContext`** — `DbSet<User> Users`, used only by the two read-side queries (`Get`, `List`), same split as `IClientManagementDbContext`.
- **`IdentityPolicyOptions`** (new options class, bound via `IOptions<IdentityPolicyOptions>`) — no per-tenant policy store exists yet, so `PasswordPolicy`/`MfaPolicy`/lockout threshold values come from `appsettings` as global defaults. New `Identity.Application.ConfigurationKeys` class holds the section name, following the `<Layer>.ConfigurationKeys` convention. Shape:
  ```
  PasswordMinLength, PasswordRequireUppercase, PasswordRequireDigit, PasswordRequireSymbol,
  PasswordBreachCheckEnabled, PasswordHistoryCount, MaxFailedLoginAttempts, LockoutDuration (TimeSpan),
  MfaPolicyMode
  ```
  Per-tenant override is explicitly future work.

## Features

All under `Features/Users/`. Command/Query records + `Handler`s, matching `Tenancy`/`ClientManagement` file layout (`Command.cs`/`Query.cs` + `Handler.cs`, plus a DTO file where a query returns projected data).

### Registration & verification

- **`Register`** — `(TenantId, Email, Username, Password)`. Validates `Email`/`Username` VOs, checks `ExistsByEmailAsync`/`ExistsByUsernameAsync`, `User.Register(...)`, generates a random verification token (32 random bytes, URL-safe base64 — same generation approach as `ClientSecret`), SHA-256 hashes it, `EmailVerificationToken.Create(hash, expiresAtUtc)`, `user.AddEmailVerificationToken(token)`. Runs `PasswordPolicyEvaluator.EvaluateAsync` (no current credential/history yet), hashes via `IPasswordHasher.Hash`, `PasswordCredential.Create(...)`, `user.SetPassword(credential, policy)`. `CreateAsync(user, user.Id, ct)` — the new user is its own audit actor (no prior authenticated actor exists for self-registration). Returns `Result<UserRegistered>` DTO: `(UserId, RawVerificationToken)` — raw token is for a future email-dispatch caller, not meant for a public HTTP response body (a Presentation-layer decision, out of scope here).
- **`RequestEmailVerification`** — `(TenantId, UserId)`. Resend path: `GetByIdAsync`, generate+hash a new token the same way, `AddEmailVerificationToken`, `UpdateAsync`. Returns raw token the same way as `Register`.
- **`VerifyEmail`** — `(TenantId, UserId, RawToken)`. SHA-256 the raw token, `GetByIdAsync`, `user.VerifyEmail(hash)`, `UpdateAsync`.

### Authentication

- **`Authenticate`** — `(TenantId, EmailOrUsername, Password)`. Looks up by email if it parses as one (`Email.Create` succeeds), else by username. Not-found and wrong-password both return the same generic `UserErrors.Validation("Invalid credentials.")` — no distinguishing signal (standard login-enumeration hygiene). If `Status == Locked` and `Lockout.LockedUntilUtc > now`, returns `UserErrors.AccountLocked(...)` before even checking the password. Verifies via `IPasswordHasher.Verify`; on success calls `RecordSuccessfulLogin()`, and if `Credential.NeedsRehash(currentAlgorithmVersion)` also rehashes via `RehashCredential`; on failure calls `RecordFailedLogin(options.MaxFailedLoginAttempts, options.LockoutDuration)`. Always `UpdateAsync` before returning (both branches mutate state). Returns `Result<AuthenticatedUser>` DTO: `(UserId, Status, PendingActions)` — no token/session, that's a separate future pass.

### Password management

- **`ChangePassword`** — `(TenantId, UserId, CurrentPassword, NewPassword)`. `GetByIdAsync`, verify `CurrentPassword` against `user.Credential.Hash` via `IPasswordHasher.Verify` (fail with generic invalid-credentials error on mismatch), `PasswordPolicyEvaluator.EvaluateAsync` for the new password, `user.SetPassword(...)`, `UpdateAsync`.
- **`RequestPasswordReset`** — `(TenantId, Email)`. Looks up by email; **if not found, returns `Result.Ok()` anyway** (no token generated) to avoid user-enumeration via response-timing/shape. If found: generate+hash a random token same as verification tokens, `user.RequestPasswordReset(token)`, `UpdateAsync`. Returns `Result<PasswordResetRequested?>` — `null` payload when no matching user, populated DTO otherwise; caller must not let the two cases become distinguishable at the HTTP layer (a Presentation-layer concern, noted here so it isn't lost).
- **`ResetPassword`** — `(TenantId, Email, RawToken, NewPassword)`. Looks up by email (not-found → generic invalid-token error, same enumeration hygiene), SHA-256 the raw token, `PasswordPolicyEvaluator.EvaluateAsync`, `PasswordCredential.Create(...)`, `user.ResetPassword(hash, credential, policy)`, `UpdateAsync`.

### Lifecycle (admin actions)

- **`Unlock`** — `(TenantId, UserId, ActorUserId)`. `GetByIdAsync`, `user.Unlock()`, `UpdateAsync`.
- **`Disable`** — `(TenantId, UserId, Reason, ActorUserId)`. `GetByIdAsync`, `user.Disable(reason)`, `UpdateAsync`.
- **`SoftDelete`** — `(TenantId, UserId, ActorUserId)`. `GetByIdAsync`, `user.SoftDelete()`, `repository.SoftDeleteAsync(user, actorUserId, ct)`.

### MFA

- **`EnrollMfaFactor`** — `(TenantId, UserId, FactorType, CredentialData)`. `CredentialData` is accepted as an opaque pre-built string — actual TOTP secret generation and at-rest encryption need a port that doesn't exist in Domain yet (only `IPasswordHasher`/`IBreachedPasswordChecker` are defined); **explicitly deferred**, same treatment as the `MfaFactorType.WebAuthnPasskey` stub. `MfaFactor.Create(factorType, credentialData)`, generates a batch (10) of recovery codes — each a random value hashed via SHA-256 (same generation pattern as `ClientSecret`/verification tokens), `RecoveryCode.Create(hash)` per code — `user.EnrollMfaFactor(factor, codes)`, `UpdateAsync`. Returns `Result<MfaFactorEnrolled>` DTO: `(FactorId, RawRecoveryCodes[])` — raw codes shown once, same pattern as `ClientSecretIssued`.
- **`VerifyMfaFactor`** — `(TenantId, UserId, FactorId)`. Orchestration only — `user.VerifyMfaFactor(factorId)`, `UpdateAsync`. Actual TOTP-code validation has the same missing-port caveat as `EnrollMfaFactor`; this command trusts that validation happened upstream (an explicit gap to close when the TOTP port is added).
- **`RevokeMfaFactor`** — `(TenantId, UserId, FactorId, ActorIsAdmin)`. `GetByIdAsync`, builds `MfaPolicy` from `IdentityPolicyOptions`, `user.RevokeMfaFactor(factorId, policy, actorIsAdmin)`, `UpdateAsync`.

### Attributes & required actions

- **`SetAttribute`** — `(TenantId, UserId, Key, Value, ValueType)`. `GetByIdAsync`, `user.SetAttribute(...)`, `UpdateAsync`.
- **`RequiredActions/Add`** — `(TenantId, UserId, Action)`. `GetByIdAsync`, `user.AddRequiredAction(action)`, `UpdateAsync`.
- **`RequiredActions/Clear`** — `(TenantId, UserId, Action)`. `GetByIdAsync`, `user.ClearRequiredAction(action)`, `UpdateAsync`.

### Queries

- **`Get`** — `(TenantId, UserId) -> Result<UserDetail>`. `IIdentityDbContext.Users`, filtered by `TenantId`/`!IsDeleted`, projects `Id, Email, Username, Status, MfaFactors (summary), Attributes, PendingActions, CreatedAt`.
- **`List`** — `(TenantId, PageNumber, PageSize, Status?) -> Result<PaginationListResponse<UserListItem>>`. Same shape as `ClientManagement`'s `List`: filter, `LongCountAsync`, `OrderByDescending(CreatedAt)`, `Skip/Take`, projects `Id, Email, Username, Status, CreatedAt`.

## Error handling

No new error codes needed — `UserErrorCodes`/`UserErrors` in Domain already cover every failure case these handlers surface (`NotFound`, `Validation(...)` variants, `AccountLocked`). Handlers return `Result.Fail(x.Errors)` propagated from Domain/repository calls; no Application-specific error factory needed for this pass.

## Testing

New `tests/WardenId.Identity.Application.Tests` (xUnit), mirroring `Identity.Domain.Tests` style: one fact/theory per handler covering the success path and each documented failure branch, using simple fakes for `IUserRepository`/`IIdentityDbContext`/`IPasswordHasher`/`IBreachedPasswordChecker`/`IdentityPolicyOptions` (no real database, no EF, no mocking framework beyond what the repo already uses).

## Explicitly out of scope (this pass)

- Token/session issuance on successful `Authenticate` — ties to Tenancy's per-tenant signing keys, deserves its own design pass once needed.
- TOTP secret generation, at-rest encryption, and code verification — no port exists yet; `EnrollMfaFactor`/`VerifyMfaFactor` are orchestration-only stubs around this gap.
- Actual email dispatch for verification/reset tokens — `IEmailService` exists in `SharedKernel.Application` but is unused repo-wide; wiring it (templates, base URLs for links) is a future Presentation/Infrastructure concern. This pass returns raw tokens in result DTOs only.
- Per-tenant policy overrides — `IdentityPolicyOptions` is global/appsettings-bound only.
- `Identity.Infrastructure`, `Identity.Presentation`, EF configuration/migrations, Gateway DI wiring, `IResultExtensions` mapping table (no new error codes needed, so no change required there either).
