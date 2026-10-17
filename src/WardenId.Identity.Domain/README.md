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

Signed-in users also manage their own MFA on Protocol's account page. Self-enrollment (`BeginTotpEnrollment`) is
allowed without a code only while the user has no enabled factor and the policy isn't Off (or they must enroll).
Every change to an existing factor needs a current code first, checked by `Identity.Application`'s
`SecondFactorCheck`, the same check `VerifySecondFactor` uses (lockout counting, replay, retries, `SecondFactorFailure`
tags). The change is saved together with the code's use: `BeginTotpReplacement` (TOTP or recovery code) starts a new
enrollment, `RegenerateRecoveryCodes` (TOTP only) replaces every recovery code, and `RemoveMfa` (TOTP or recovery
code; refused before any code check while the policy requires MFA) removes every factor and code without asking for
a new enrollment. Completing any enrollment removes the user's other TOTP factors (`User.RemoveOtherMfaFactors`), so a
replacement leaves only the new authenticator. A wrong current password on a password change (admin API or account
page) is a failed login too, and a locked account can't change its password until the lockout ends.

Users, MfaFactors and RecoveryCodes carry an xmin concurrency token, so racing sign-in steps can't both accept one
code or lose a failed attempt: `IUserRepository` saves fail with `UserErrors.ConcurrencyConflict` (409 over HTTP),
and the sign-in handlers reload and try again (Authenticate 3 times, VerifySecondFactor 5).

## Terms of service

`RequiredAction.AcceptTos` is a sign-in step, not a block: Protocol shows the terms link (`terms.url` in the identity
policy, the tenant's override over the global `IdentityPolicy:TermsUrl`, null by default) and calls `AcceptTerms`,
which stores `User.TermsAcceptedAtUtc`, clears the action and raises `UserTermsAcceptedDomainEvent`. A tenant's
`terms.url` must be an absolute http or https URL of at most 2048 characters.

## Explicitly out of scope in this pass

- `IUserRepository` — deferred to the future Identity Application/Infrastructure/Presentation
  pass, matching how `IClientRepository` was deferred out of `ClientManagement`'s Domain-only
  pass and added later in `ClientManagement.Application/Abstractions/`.
- FR-ID-14 (impersonation) — Won't-v1, skipped entirely.
- FR-ID-08 (WebAuthn/passkeys) — only the `MfaFactorType.WebAuthnPasskey` enum stub exists.
- `IPasswordHasher`/`IBreachedPasswordChecker` implementations — Infrastructure concern, not
  built here; this project only defines the ports.
