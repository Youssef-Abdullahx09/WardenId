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
| `MfaFactor` | Has its own lifecycle (`Create` → unverified/disabled → `Verify()` → enabled) independent of any single request. |
| `RecoveryCode` | One-time-use, needs individual consumption tracking (`UsedAtUtc`) — a plain string list on `User` couldn't express that. |
| `UserAttribute` | Typed key/value pairs need their own identity for update-in-place semantics (`UpdateValue`). |

## Explicitly out of scope in this pass

- `IUserRepository` — deferred to the future Identity Application/Infrastructure/Presentation
  pass, matching how `IClientRepository` was deferred out of `ClientManagement`'s Domain-only
  pass and added later in `ClientManagement.Application/Abstractions/`.
- FR-ID-14 (impersonation) — Won't-v1, skipped entirely.
- FR-ID-08 (WebAuthn/passkeys) — only the `MfaFactorType.WebAuthnPasskey` enum stub exists.
- `IPasswordHasher`/`IBreachedPasswordChecker` implementations — Infrastructure concern, not
  built here; this project only defines the ports.
