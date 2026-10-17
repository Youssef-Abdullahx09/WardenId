# WardenId.Identity.Domain

Domain layer for the Identity & Credentials module (BRD M2, FR-ID-01 through FR-ID-13,
excluding FR-ID-14). See `docs/superpowers/specs/2026-07-26-identity-domain-design.md`
for the full design and `docs/superpowers/plans/2026-07-26-identity-domain.md` for how
it was built.

## Aggregate boundary

`User` is the sole aggregate root. Three boundary questions were resolved during design:

- **`PasswordCredential` is a child entity of `User`, not its own aggregate.** Rehash-on-login
  (FR-ID-02) and password changes both mutate `User` atomically in one transaction — there's
  no requirement for eventual consistency between a credential and its owning user.
- **`MfaFactor` (and its `RecoveryCode` batch) is a child entity of `User`, not an independent
  aggregate root.** Nothing in FR-ID-07/FR-ID-09 requires revoking a factor without loading
  the full `User`.
- **The notification port for FR-ID-13 (email delivery) does not live in this project.** It
  lives in `WardenId.SharedKernel.Application`, since Tenancy will likely need email delivery
  too (invites, lifecycle notices) — Identity isn't the only future consumer.

## Why each entity landed where it did

| Entity | Why a child entity, not a bare value object |
|---|---|
| `PasswordCredential` | Mutates in place over time (rehash replaces `Hash`/`AlgorithmVersion` on the same logical credential); `PasswordHistory` entries need to reference prior instances by identity. |
| `PasswordHistoryEntry` | A retired credential snapshot — needs its own identity/audit trail distinct from the live `Credential`. |
| `EmailVerificationToken` / `PasswordResetToken` | Kept as two separate classes (not a shared base) despite an identical shape today — their consumption side effects differ (reset also clears lockout), and a shared base would force them to change in lockstep later. |
| `MfaFactor` | Has its own lifecycle (`User.BeginMfaEnrollment` → unverified/disabled → `User.CompleteMfaEnrollment` → enabled) spanning requests, and keeps `LastAcceptedTimeStep` so a TOTP code is never accepted twice. |
| `RecoveryCode` | One-time-use, needs individual consumption tracking (`UsedAtUtc`) — a plain string list on `User` couldn't express that. |
| `UserAttribute` | Typed key/value pairs need their own identity for update-in-place semantics (`UpdateValue`). |

## MFA (TOTP)

Users enroll their own factor at sign-in (Protocol's pages, through `Identity.Integration.Command`'s
`BeginTotpEnrollment`/`CompleteTotpEnrollment`); admins can only revoke one factor or reset them all
(`User.ResetMfa`, which adds the EnrollMfa required action whatever the policy). The domain never sees a raw
secret or code: `MfaFactor.CredentialData` holds the Data Protection-protected secret (purpose
`WardenId.Identity.TotpSecret.v1`), `RecoveryCode.CodeHash` a SHA-256 hash, and the TOTP math (RFC 6238,
SHA-1, 6 digits, 30 s, ±1 step) lives in `Identity.Application/Mfa`. A wrong code at sign-in is a failed
login (`RecordFailedLogin`); for a user with a second factor, only the code step resets the counter.

The `AddTotpFactorUsage` migration deletes every factor and recovery code stored before it: they came from the
removed admin enroll endpoint, which stored no usable secret. Their users aren't given EnrollMfa; Authenticate asks
for enrollment whenever the tenant's policy requires MFA and the user has no enabled factor.

Users, MfaFactors and RecoveryCodes carry an xmin concurrency token, so racing sign-in steps can't both accept one
code or lose a failed attempt: `IUserRepository` saves fail with `UserErrors.ConcurrencyConflict` (409 over HTTP),
and the sign-in handlers reload and try again (Authenticate 3 times, VerifySecondFactor 5).

## Explicitly out of scope in this pass

- `IUserRepository` — deferred to the future Identity Application/Infrastructure/Presentation
  pass, matching how `IClientRepository` was deferred out of `ClientManagement`'s Domain-only
  pass and added later in `ClientManagement.Application/Abstractions/`.
- FR-ID-14 (impersonation) — Won't-v1, skipped entirely.
- FR-ID-08 (WebAuthn/passkeys) — only the `MfaFactorType.WebAuthnPasskey` enum stub exists.
- `IPasswordHasher`/`IBreachedPasswordChecker` implementations — Infrastructure concern, not
  built here; this project only defines the ports.
