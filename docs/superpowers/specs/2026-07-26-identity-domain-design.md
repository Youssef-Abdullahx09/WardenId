# User Aggregate — Identity Domain Layer Design

Date: 2026-07-26
Module: `WardenId.Identity.Domain` (new project)
Scope: **Domain layer only.** No Application/Infrastructure/Presentation projects in this pass.

## Context

Requirements source: a BRD module "M2: Identity & Credentials" (FR-ID-01 through FR-ID-13) supplied in the request. That BRD file, and the "Kensington Oval"/"MOARP"/`PlatformSettings` conventions referenced alongside it, do not exist in this repository — this spec instead follows the conventions already established in `WardenId.Tenancy.Domain` (`Tenant.cs`, `Slug.cs`, `TenantErrors.cs`) and `WardenId.ClientManagement.Domain` (`Client.cs`, `ClientSecret.cs`, `ClientId.cs`): flat `WardenId.<Module>.<Layer>` projects, `SharedKernel.Domain.AggregateRoot`/`Entity`/`ValueObject` base types, `Result<T>`-returning static factories, `Events/` folder per module.

FR-ID-14 (impersonation) is excluded entirely (Won't-v1). FR-ID-08 (WebAuthn/passkeys) is stubbed as an unused `MfaFactorType` enum member only.

## Open questions resolved

- **PasswordCredential boundary**: embedded child entity of `User`, same transaction boundary — not its own aggregate. Rehash-on-login and password-change both mutate `User` atomically; no eventual-consistency need identified.
- **MfaFactor boundary**: child entity under `User`, not an independent aggregate — same reasoning, no requirement for factor mutation independent of `User`.
- **Notification port (FR-ID-13)**: lives in `WardenId.SharedKernel.Application`, not Identity — Tenancy will likely need email eventually too (invites, lifecycle notices), so the port shouldn't be Identity-owned.

## Enums

- `UserStatus` — `PendingVerification = 1, Active = 2, Disabled = 3, Locked = 4, SoftDeleted = 5`
- `MfaFactorType` — `Totp = 1` (only implemented type); `WebAuthnPasskey = 2` reserved, unused — the FR-ID-08 extension point.
- `RequiredAction` — `ResetPassword = 1, AcceptTos = 2, EnrollMfa = 3, VerifyEmail = 4`. Plain enum (not a VO class) — no invariants beyond membership in this finite set.
- `UserAttributeValueType` — `String = 1, Number = 2, Bool = 3, Date = 4`. Type tag only; Domain doesn't parse/coerce.
- `MfaPolicyMode` — `Off = 1, Optional = 2, Required = 3, RequiredForAdmins = 4`

## Value Objects

All follow the `Slug`-style split: `Create(raw)` validates external input, `FromTrustedSource(value)` skips validation for EF materialization.

- **`Email`** (FR-ID-01) — format + max-length validation on `Create`; normalized to lowercase for comparison. Uniqueness itself is a repository/query-level invariant (`IUserRepository.ExistsByEmailAsync`), not enforced inside the VO.
- **`Username`** (FR-ID-01) — length/charset validation, same split, same uniqueness caveat.
- **`PasswordPolicy`** (FR-ID-03) — tenant-level config VO: `MinLength`, `RequireUppercase`, `RequireDigit`, `RequireSymbol` (bools), `BreachCheckEnabled` (bool), `HistoryCount` (int, how many prior hashes to retain/reject-reuse-against).
- **`MfaPolicy`** (FR-ID-09) — tenant-level config VO wrapping `MfaPolicyMode`.
- **`LoginLockoutState`** (FR-ID-06) — `FailedAttemptCount` (int), `LockedUntilUtc` (`DateTimeOffset?`). Immutable; `User` replaces it wholesale on `RecordFailedLogin`/`RecordSuccessfulLogin`/`Unlock`, same pattern `Tenant.Status` uses.

## Child Entities

All derive from `SharedKernel.Domain.Entity`. Secrets/tokens/codes store hashes only — Domain never generates or sees raw secret material, that crosses the `IPasswordHasher`/Infrastructure boundary.

- **`PasswordCredential`** (FR-ID-02) — `Hash`, `Algorithm` (string/enum, `Argon2id` today), `AlgorithmVersion` (int), `SetAtUtc`. `NeedsRehash(currentVersion)` compares stored vs. current. Entity rather than a plain VO: it mutates in place over time (rehash replaces `Hash`/`AlgorithmVersion` on the same logical credential) and `PasswordHistory` needs to reference prior instances by identity, not just by value.
- **`PasswordHistoryEntry`** (FR-ID-03) — one retired credential snapshot: `HashSnapshot`, `Algorithm`, `SetAtUtc`. `User` holds a capped collection of these (capped at `PasswordPolicy.HistoryCount`); `User.SetPassword` is responsible for trimming.
- **`EmailVerificationToken`** (FR-ID-04) — `TokenHash`, `ExpiresAtUtc`, `ConsumedAtUtc`. `Consume()` guards already-consumed/expired.
- **`PasswordResetToken`** (FR-ID-05) — `TokenHash`, `ExpiresAtUtc`, `ConsumedAtUtc`, own `Consume()`. Kept as a distinct class from `EmailVerificationToken` (not a shared base) — same shape today, but consumption side effects differ (reset also clears lockout), and a shared base would force them to change in lockstep later.
- **`MfaFactor`** (FR-ID-07, stub FR-ID-08) — `FactorType` (`MfaFactorType`), `CredentialData` (opaque ciphertext blob, Infrastructure encrypts/decrypts), `Enabled`, `EnrolledAtUtc`, `VerifiedAtUtc`.
- **`RecoveryCode`** (FR-ID-07) — `CodeHash`, `UsedAtUtc`. Generated as a batch when an `MfaFactor` is enrolled; `Consume()` guards reuse.
- **`UserAttribute`** (FR-ID-11) — `Key` (string), `Value` (string), `ValueType` (`UserAttributeValueType`).

## User Aggregate

### State

`TenantId` (Guid, required — constructor throws `ArgumentException` if `Guid.Empty`), `Email`, `Username`, `Status` (`UserStatus`), `Credential` (`PasswordCredential?`), `Lockout` (`LoginLockoutState`), owned collections: `MfaFactors`, `RecoveryCodes`, `UserAttributes`, `PendingActions` (`RequiredAction`), `PasswordHistoryEntries`, `EmailVerificationTokens`, `PasswordResetTokens`. All collections `IReadOnlyCollection<T>` over private backing `List<T>`, matching `Tenant.SigningKeys`/`Client.Secrets`.

### Create

```
User.Register(tenantId, email, username) -> Result<User>
```

- Guards `tenantId != Guid.Empty` (throws — construction contract violation, not a validation failure)
- `Status = PendingVerification`
- Adds `RequiredAction.VerifyEmail` to `PendingActions`
- Raises `UserRegisteredDomainEvent`

Password is **not** set during `Register` — `SetPassword` is called separately once a hash is computed (Infrastructure), same reasoning as `Client.Create` not taking secrets/signing keys up front.

### Behaviors

- `VerifyEmail(token)` (FR-ID-04) — consumes matching `EmailVerificationToken`, clears `RequiredAction.VerifyEmail`, `PendingVerification → Active`. Fails if token invalid/expired/consumed or status isn't `PendingVerification`. Raises `UserEmailVerifiedDomainEvent`.
- `SetPassword(newCredential, policy)` (FR-ID-02/03) — moves current `Credential` (if any) into `PasswordHistoryEntries`, trims to `policy.HistoryCount`, sets `Credential = newCredential`. Raises `PasswordChangedDomainEvent`. (Reuse-against-history and complexity checks happen in `PasswordPolicyEvaluator` *before* this is called — see Domain Services — this method just performs the swap once a candidate is approved.)
- `RecordFailedLogin(policy)` (FR-ID-06) — increments `Lockout.FailedAttemptCount`; once it crosses a threshold, sets `LockedUntilUtc` and `Status = Locked`. Raises `LoginFailedDomainEvent`, and `AccountLockedOutDomainEvent` when the lock actually triggers. (Named `AccountLockedOut` rather than the BRD's literal "AccountLocked" to avoid a name clash with `UserStatus.Locked` — flag if exact BRD wording is required instead.)
- `RecordSuccessfulLogin()` (FR-ID-06) — resets `Lockout` to zero/null. No event — not a state transition worth raising, just bookkeeping.
- `Unlock()` (FR-ID-10) — guards `Status == Locked`, resets `Lockout`, `Status = Active`. Raises `UserUnlockedDomainEvent`.
- `Disable(reason)` (FR-ID-10) — guards not-already-`Disabled`. Raises `UserDisabledDomainEvent`.
- `SoftDelete()` (FR-ID-10) — guards not-already-deleted, `Status = SoftDeleted` (base `IsDeleted`/`DeletedAt` are set by the EF interceptor via `MarkAsDeleted`, called by Application — this method only guards/updates the domain `Status`). Raises `UserSoftDeletedDomainEvent`.
- `EnrollMfaFactor(factor, recoveryCodes, mfaPolicy)` (FR-ID-07/09) — adds factor + its recovery-code batch. Raises `MfaFactorEnrolledDomainEvent`.
- `RevokeMfaFactor(factorId, mfaPolicy)` (FR-ID-07/09) — guards via `MfaPolicyEvaluator.CanRevokeFactor` (won't allow revoking the last factor when policy requires MFA). Raises `MfaFactorRevokedDomainEvent`.
- `RequestPasswordReset(token)` (FR-ID-05) — adds a `PasswordResetToken`. Raises `PasswordResetRequestedDomainEvent`.
- `ResetPassword(token, newCredential, policy)` (FR-ID-05) — consumes token, calls same swap logic as `SetPassword`, also resets `Lockout`. Raises `PasswordResetDomainEvent`.
- `SetAttribute(key, value, valueType)` (FR-ID-11) — upserts by key. No event.
- `AddRequiredAction(action)` / `ClearRequiredAction(action)` (FR-ID-12) — dedupe-on-add, no-op-on-missing-remove. No event — Application-layer pipeline reads `PendingActions` directly rather than reacting to an event.

Hard-delete (FR-ID-10) is **not** an aggregate method — it's a physical row delete at the repository/Infrastructure level; nothing left for Domain to guard once the row doesn't exist.

## Domain Services

Stateless, no repository access, take VOs/the aggregate as plain parameters:

- **`PasswordPolicyEvaluator`** (FR-ID-03) — `Evaluate(PasswordPolicy policy, string candidatePassword, IReadOnlyCollection<PasswordHistoryEntry> history) -> Result`. Checks length/complexity directly; delegates breach-list lookup to `IBreachedPasswordChecker` (a port, since breach-list lookup is inherently I/O — HIBP API or local corpus — and can't be pure Domain logic); checks reuse against `history`.
- **`MfaPolicyEvaluator`** (FR-ID-09) — `IsMfaRequired(MfaPolicy policy, bool userIsAdmin) -> bool`; `CanRevokeFactor(MfaPolicy policy, User user, MfaFactor factor) -> Result` (read-only; the actual mutation stays on `User.RevokeMfaFactor`, which calls this internally).

## Ports (Domain interfaces, Infrastructure implements)

- `IPasswordHasher` (FR-ID-02) — `Hash(string plaintext) -> string`, `Verify(string plaintext, string hash) -> bool`, `NeedsRehash(string hash) -> bool`.
- `IBreachedPasswordChecker` (FR-ID-03) — `IsBreachedAsync(string plaintext) -> Task<bool>`.
- Notification port for FR-ID-13 is **not** defined here — lives in `WardenId.SharedKernel.Application` per the resolved open question above.

## Repository

`IUserRepository` (Identity.Domain): `GetByIdAsync(Guid tenantId, Guid id)`, `GetByEmailAsync(Guid tenantId, Email email)`, `GetByUsernameAsync(Guid tenantId, Username username)`, `ExistsByEmailAsync(Guid tenantId, Email email)`, `ExistsByUsernameAsync(Guid tenantId, Username username)` (the FR-ID-01 uniqueness contract, explicit rather than implied), `AddAsync(User user)`. Tenant-scoped params always first, no `IQueryable`/LINQ leakage in the signature. Exact update-tracking shape (explicit `Update` vs. relying on EF change-tracking) will follow whatever `ITenantRepository`/`IClientRepository` actually do — checked at implementation time, not guessed here.

## Errors & Events

- `UserErrorCodes` / `UserErrors` static classes, same shape as `TenantErrorCodes`/`ClientErrorCodes`: `MetadataKey`, `NotFound`, `Validation`, plus a new `AccountLocked` code so `HandleApiFailure()` can map it to 423 (extending the mapping table in `IResultExtensions`, per this repo's existing convention for that status code). Specific factories per invariant above (`UserEmailAlreadyVerified`, `UserTokenExpired`, `UserTokenAlreadyConsumed`, `UserInvalidStatusTransition`, `UserMfaFactorNotFound`, `UserCannotRevokeLastRequiredFactor`, etc.).
- `Events/` folder: `UserRegisteredDomainEvent`, `UserEmailVerifiedDomainEvent`, `PasswordChangedDomainEvent`, `LoginFailedDomainEvent`, `AccountLockedOutDomainEvent`, `UserUnlockedDomainEvent`, `UserDisabledDomainEvent`, `UserSoftDeletedDomainEvent`, `MfaFactorEnrolledDomainEvent`, `MfaFactorRevokedDomainEvent`, `PasswordResetRequestedDomainEvent`, `PasswordResetDomainEvent`.

## Testing

New `tests/WardenId.Identity.Domain.Tests` (xUnit). One fact/theory per invariant/transition listed above — VO construction (`Create` success/failure cases), aggregate state transitions (valid + invalid-transition-guard cases), `DomainEvents` raised correctly, domain-service evaluators. No database, no mocking beyond simple fakes for `IPasswordHasher`/`IBreachedPasswordChecker` where a test needs one.

## README

`src/WardenId.Identity.Domain/README.md` — documents the three resolved boundary decisions (PasswordCredential and MfaFactor as child entities not aggregates, notification port living in SharedKernel), same style as the existing `architecture.md` reference in the original request (no such file exists in this repo yet — this README is the first of its kind here, scoped to this module only).

## Explicitly Out of Scope (this pass)

- FR-ID-14 (impersonation) — skipped entirely per Won't-v1.
- FR-ID-08 (WebAuthn/passkeys) — only the `MfaFactorType.WebAuthnPasskey` enum stub, no implementation.
- Application/Infrastructure/Presentation projects, CQRS commands/queries, EF configuration, Gateway wiring, migrations.
- `IResultExtensions` mapping table change for the new `AccountLocked` error code — noted here as a required follow-up but not made in this pass since it touches `SharedKernel.Presentation`, outside Identity.Domain.
- Enforcing FR-ID-01 uniqueness as a live DB constraint (unique index) — Infrastructure/EF concern, not Domain.
