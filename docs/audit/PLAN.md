# Audit Module — Implementation Plan

Prepared per `docs/audit/audit-module-spec.md` §0. Codebase inspected: `WardenId.Tenancy.*`
(all 4 layers, DI wiring, migrations, `Es256SigningKeyGenerator`, xmin/rowversion
handling), `SharedKernel.*` (all 8 projects), and the CAP wiring in
`SharedKernel.Infrastructure`. Findings and conflicts below; **stop for review before
implementing anything.**

---

## 1. Module shape — matches existing convention, one addition

New bounded-context module, 4 layers, same as Tenancy:

```
src/WardenId.Audit.Domain/
src/WardenId.Audit.Application/
src/WardenId.Audit.Infrastructure/
src/WardenId.Audit.Presentation/
```

Plus **shared, cross-module pieces** living in `SharedKernel.*` (per spec §5's explicit
instruction), which is the one addition beyond "just another module":

```
src/WardenId.SharedKernel.Persistence/Audit/AuditEventRecord.cs
src/WardenId.SharedKernel.Persistence/Audit/AuditEventConfiguration.cs
src/WardenId.SharedKernel.Persistence/Audit/AuditCaptureInterceptor.cs
src/WardenId.SharedKernel.Application/Audit/IAuditContextAccessor.cs
src/WardenId.SharedKernel.Application/Audit/AuditAmbientContext.cs
src/WardenId.SharedKernel.Application/Audit/IAuditRecorder.cs   // explicit-emission API (I8)
```

`WardenId.Gateway/DependencyInjection/AuditDependencyInjection.cs` +
`Program.cs: builder.Services.AddAuditModule(...)`, one line, same as every other module.

---

## 2. Conventions confirmed reusable as-is

- Vertical slices under `Features/<Aggregate>/<UseCase>/`, `IEndpoint` + `Request.ToCommand(...)`
  on Presentation, `Command`/`Handler` MediatR pair on Application — no deviation for the
  query/proof endpoints in phase 4.
- `AggregateRoot`/`Entity`/`ValueObject` base types, `Result<T>` everywhere, `Create`/
  `FromTrustedSource` split on value objects (`ProtectedValue`, `Actor` serialization).
- `IDesignTimeDbContextFactory` + `AppConfigurationConstants.PathToGateway` pattern for
  `AuditDbContext` (segments/checkpoints — see §4).
- `IEndpoint` auto-discovery needs nothing extra — `WardenId.Audit.Presentation` just
  needs to exist and end in `Presentation`.
- Config: nested `ConfigurationKeys.Audit { Section = nameof(Audit); ... }` static class,
  matching `ConfigurationKeys.CapPublisher`/`RabbitMq`.
- `Es256SigningKeyGenerator`'s **shape** (ECDSA P-256 via BCL, `IDataProtector`-encrypted
  private key, `CryptographicOperations.ZeroMemory`) is the template for the checkpoint
  signer — see conflict note in §5, it is not directly reusable as a type.

---

## 3. Conflicts / gaps vs. established convention

These are real gaps, not just unknowns — flagging per spec's "codebase wins" rule.

### 3.1 `AuditEventConfiguration` cross-assembly application (spec §5)

Each module's `OnModelCreating` calls `ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly())`
— scoped to that module's **own** assembly. A config class living in
`SharedKernel.Persistence` will **not** be auto-discovered by `TenancyDbContext`,
`IdentityDbContext`, etc. Each module's `DbContext.OnModelCreating` needs one explicit line:

```csharp
modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
```

added after `HasDefaultSchema(Schema)` / before `base.OnModelCreating(...)`, mirroring where
the assembly-scan call already sits.

### 3.2 Shared table, multiple owning `DbContext`s → migration collision

If `audit.events` is mapped in every module's model (`TenancyDbContext`,
`IdentityDbContext`, `ClientManagementDbContext`, `AuditDbContext`), each module's next
`dotnet ef migrations add` will try to create the same table. Resolution: designate
`WardenId.Audit.Infrastructure`'s `AuditDbContext` as the **owning** context (its
migration creates `audit.events`, `audit.segments`, `audit.checkpoints`); every other
module maps the entity with:

```csharp
modelBuilder.Entity<AuditEventRecord>().ToTable("events", "audit", t => t.ExcludeFromMigrations());
```

Flagging this explicitly since it's a non-obvious EF Core API and the spec doesn't call
it out.

### 3.3 `IResultExtensions.HandleApiFailure` doesn't recognize module-local error codes

The switch only matches `SharedKernel.Presentation.ErrorCodes` constants
(`InvalidCredentials`, `AccountLocked`, `NotFound`, `DuplicateIdentity`, `Forbidden`,
`InvalidToken`) — a module's own `<Module>ErrorCodes.NotFound` (different string constant,
same *name*) falls through to default 400, exactly like `TenantErrorCodes.NotFound`
already does today. Audit query/proof endpoints (inclusion proof for missing event,
checkpoint verification conflict) should reuse the shared `ErrorCodes.NotFound` value
where a 404 is wanted; anything needing a status not in that switch (e.g. a distinct code
for "verification failed" vs plain 400) requires **extending the shared switch** —
surfacing that as a decision, not silently adding a parallel mapping mechanism.

### 3.4 No existing `AsyncLocal` ambient-context pattern (I7)

`ICurrentUser`/`ICurrentTenant` are 100% `IHttpContextAccessor`-based. There is nothing in
the codebase to mirror for `IAuditContextAccessor` — it is new infrastructure, not a
reuse. Plan: `AsyncLocal<AuditAmbientContext?>` backing field, set by (a) HTTP middleware
early in the pipeline from the JWT claims already used by `CurrentUser`/`CurrentTenant`,
and (b) explicitly by CAP consumers and hosted services/process managers before doing any
work that emits audit events. No production code currently sets ambient context outside
HTTP — every non-HTTP call site that should populate it needs an explicit new call.

### 3.5 No existing `ISubscribeFilter`, no CAP consumers at all

Zero `[CapSubscribe]` consumers exist yet anywhere in the codebase, and zero
`ISubscribeFilter` implementations. The audit CAP filter (setting ambient context for
consumers) will be the **first** of its kind — there's no pipeline to match, full
latitude, but also nothing to validate the shape against until a real consumer exists.
Given the CAP exporter (phase 5) is itself the first real consumer, the filter and the
exporter should probably land in the same phase-5 pass so the filter has something to
prove itself against.

### 3.6 CAP's own outbox is untouched — confirms spec's premise

`AddCap(...)` sets no custom schema/table names, so CAP uses its own default `cap` schema
(`cap.published`/`cap.received`/`cap.lock`), globally, regardless of which `DbContext`
triggers a publish. `audit.events` must not be confused with this — it's a fully separate,
hand-rolled table that CAP never touches until the phase-5 exporter reads sealed segments
and publishes them out. No conflict here, just confirming the separation spec §5 demands
is achievable with zero changes to existing CAP config.

### 3.7 `DomainEventsInterceptor` is not a threat to the write path — but needs one explicit guarantee

`DomainEventsInterceptor.GetDomainEvents()` only iterates `ChangeTracker.Entries<AggregateRoot>()`
with pending events. As long as `AuditEventRecord` is a plain `Entity` (or not an
`AggregateRoot` at all) and never calls `RaiseDomainEvent`, it's structurally impossible
for an audit insert to hit `capPublisher.PublishAsync` — confirmed by reading the
interceptor, not just assumed. **Decision needed:** does `AuditEventRecord` derive from
`SharedKernel.Domain.Entity` (gets `Id`/audit-field plumbing it doesn't need — `CreatedById`
etc. are explicitly forbidden by spec §11) or a new minimal base with just `Id`? Leaning
toward a standalone record type implementing only what `IEntityTypeConfiguration` needs,
**not** deriving from `Entity`, to avoid `CreatedById`/`LastModificationById` columns
existing on the table at all. Flagging as the cleanest way to satisfy §11's
"do not put `CreatedBy`/`UpdatedBy` on audit rows" — the base class itself would have
introduced them.

### 3.8 No `pg_snapshot_xmin` / raw-SQL precedent (I4)

The only existing `xmin` touchpoint (`Tenant.Version` + `IsRowVersion()`) is EF's
optimistic-concurrency mapping — never read out via raw SQL, no helper exists. The sealer
needs new infrastructure: a `FromSqlRaw`/raw `NpgsqlCommand` call for
`SELECT pg_snapshot_xmin(pg_current_snapshot())`, compared against each row's actual
`xmin` (also raw-SQL-projected, since EF doesn't expose system columns to LINQ without an
explicit shadow property). This is new, not a gap in convention — noting it since it's the
one place raw SQL is unavoidable in an otherwise EF-only codebase.

### 3.9 No clock abstraction — matches convention, not a gap

No `IClock` exists anywhere; every timestamp is `DateTimeOffset.UtcNow` called directly.
Audit's `OccurredAt`, segment `sealed_at`, checkpoint `signed_at` will follow the same
direct-call convention rather than introducing `IClock` as a one-off for this module.

### 3.10 No `BackgroundService` / advisory-lock precedent

The sealer (phase 3) is the first `BackgroundService` and first `pg_try_advisory_lock`
usage in this codebase. No existing pattern to match; nothing to reconcile either.

### 3.11 Checkpoint signing key is a parallel type, not a reuse of `Es256SigningKeyGenerator`

That generator is Tenancy-specific: it returns a Tenancy.Application-owned
`SigningKeyMaterial` record and always produces a public JWK (kty/crv/x/y/alg/use/kid) for
token verification. A checkpoint signer only needs the ECDSA keypair plus
Data-Protection-at-rest for the private half — no JWKS shape. Plan: a new
`Es256CheckpointSigner` in `WardenId.Audit.Infrastructure`, same crypto shape
(ECDSA P-256, `IDataProtector`, zero-memory cleanup), own Data Protection purpose string
(`"WardenId.Audit.CheckpointKeys.v1"`), persisted via its own
`AddDataProtection().PersistKeysToDbContext<AuditDbContext>()` rather than sharing
Tenancy's key ring — keeps the audit signing key's blast radius independent of Tenancy's
token-signing key, per spec §6's explicit requirement that they be distinct.

---

## 4. Persistence layout

```
audit.events       — owned by AuditDbContext migrations; mapped (ExcludeFromMigrations)
                      by every other module's DbContext for the write path
audit.segments     — AuditDbContext only
audit.checkpoints  — AuditDbContext only
```

`AuditDbContext : DbContextBase, IAuditDbContext` in `WardenId.Audit.Infrastructure`,
schema `"audit"`, own `__EFMigrationsHistory` table name, own
`AuditDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AuditDbContext>` following
the Tenancy pattern exactly (`AppConfigurationConstants.PathToGateway`).

`platform`-channel events (spec §3.3: must survive tenant schema drop) go to the same
`audit.events` table but with `TenantId = null` and are never touched by a tenant's schema
lifecycle — schema-per-tenant isolation doesn't apply here since `audit` is its own schema
outside any tenant's.

---

## 5. Write path

- **Direct append** (`authn.*`, `token.ops`): `IAuditRecorder.RecordAsync(AuditEvent, Guid?
  tenantId, ...)` — implemented using a standalone `IDbContextFactory<AuditDbContext>`
  insert (no ambient transaction assumed) — canonical-serialize, compute leaf hash (I1/I2),
  insert with `sequence = NULL`. Failure is caught and logged, never rethrown (I10
  fail-open). See §5.1 below for where this interface actually lives — it's not
  `SharedKernel.Application`.
- **Transactional** (`admin.*`): a new `AuditCaptureInterceptor : SaveChangesInterceptor`,
  registered per module `DbContext` the same way `DomainEventsInterceptor` is
  (`options.AddInterceptors(...)` in each module's `AddDbContext` callback). On
  `SavingChangesAsync`, diffs `ChangeTracker` entries for tracked aggregates into
  `PropertyChange` list, builds the envelope from ambient context
  (`IAuditContextAccessor`), adds an `AuditEventRecord` to the same `ChangeTracker` before
  `base.SavingChangesAsync` runs — same transaction, same `SaveChanges` call, no CAP
  involvement, per §3.7 above. Failure here **must** propagate (I10 fail-closed) — no
  try/catch swallowing in this interceptor, unlike the direct-append path. Built — see §5.2.

### 5.1 Correction to this plan: `IAuditRecorder`/`IAuditContextAccessor` cannot live in SharedKernel

Discovered while implementing phase 2, after `Audit.Domain` had grown its real type system
(`Actor`, `AuditChannel`, `AuditEventType`, ...). Anything whose *signature* mentions those
types — `IAuditRecorder.RecordAsync(AuditEvent, ...)`, `IAuditContextAccessor`'s
`AuditAmbientContext` — cannot live in `SharedKernel.Application`/`SharedKernel.Persistence`
without SharedKernel taking a dependency on a feature module, inverting the one rule that
makes SharedKernel a shared kernel (dependencies flow *into* it, never out).

Resolution, now built this way:

- **`SharedKernel.Persistence`**: only `AuditEventRecord` (bare POCO, primitive-typed —
  `int Channel`, not `AuditChannel`) + `AuditEventConfiguration`. Zero dependency on
  `Audit.Domain`. This is the one piece that genuinely is shared-kernel-shaped: a dumb row,
  not a rich object.
- **`WardenId.Audit.Application`** (new project, not previously planned as such): owns
  `IAuditContextAccessor`, `AuditAmbientContext`, `IAuditRecorder` — anything whose surface
  touches `Audit.Domain` types. Modules that want to emit an audit event take a project
  reference to this, the same way every module already takes one on `SharedKernel`. This
  isn't a bounded-context violation — Audit is a cross-cutting concern other modules
  legitimately depend on, same shape as logging/telemetry, just with its own rich domain
  model instead of an anemic shared one.
- **`WardenId.Audit.Infrastructure`** (new project): `AuditDbContext` (owns the
  `audit.events` migration — hand-authored raw-SQL `PARTITION BY LIST ("Channel")`, 7 named
  partitions + a `DEFAULT` catch-all, `PRIMARY KEY (Id, Channel)` since Postgres requires
  the partition key in any unique constraint on a partitioned table),
  `AuditDbContextDesignTimeFactory`, `AsyncLocalAuditContextAccessor` (I7), `AuditRecorder`
  (I10 fail-open, uses `IDbContextFactory<AuditDbContext>` rather than plain `AddDbContext`
  — deliberate deviation from Tenancy's pattern, since Audit's consumers, including the
  phase-3 sealer, aren't request-scoped). Deliberately **does not** register
  `DomainEventsInterceptor` — `AuditEventRecord` never raises domain events and audit
  writes must never depend on CAP (§5 above); registering it would open a CAP-participating
  transaction on every insert for nothing.
- `WardenId.Gateway` takes a project reference to `Audit.Infrastructure` and calls
  `AddAuditModule` (currently just `AddAuditInfrastructure` — no `Audit.Presentation`
  project exists yet, deferred until phase 4's query/proof endpoints need one).

### 5.2 `AuditCaptureInterceptor`'s opt-in contract — resolved

The contract lives in `SharedKernel.Domain` (not `Audit.Domain`), for the same reason as
§5.1: an aggregate in `Tenancy.Domain`/`Identity.Domain`/etc. implementing it must not need
a reference to `Audit.Domain`, and `SharedKernel.Domain` is the one place every module's
Domain project already references.

```csharp
public interface IAuditableAggregate
{
    string AuditTargetTypeKey { get; }        // resolved via AuditTargetType.FromKey — throws if unregistered
    AuditableChannel AuditChannel { get; }    // new small enum in SharedKernel.Domain: AdminDirectory | AdminConfig
    IReadOnlySet<string> AuditSensitivePropertyNames { get; }
}
```

`AuditCaptureInterceptor` (`WardenId.Audit.Infrastructure`) scans
`ChangeTracker.Entries().Where(e => e.Entity is IAuditableAggregate && e.State is Added or
Modified)`, diffs each entry's *scalar* properties only (navigation/collection changes are
out of scope — a handler needing those audited emits explicitly via `IAuditRecorder`), and
adds the resulting `AuditEventRecord` to the same `ChangeTracker`/transaction. Key design
points:

- **Only 3 generic `AuditEventType` catalog entries** (`admin.entity.create/update/delete`),
  reused across every `IAuditableAggregate` — not one triplet per aggregate type. Which
  entity changed is on `AuditTarget`, not encoded into the event type. Avoids an
  N-aggregates × 3 catalog-entry scaling problem.
- **`EntityState.Deleted` never reaches the interceptor** — `DbContextBase.
  CheckAndUpdateEntities` converts a hard delete into `IsDeleted=true` +
  `EntityState.Modified` *before* the interceptor's hook runs (it's plain code executing
  ahead of `base.SaveChangesAsync`, where the EF interceptor pipeline actually lives). A
  soft-delete is detected instead by checking whether `IsDeleted` flipped `false→true`
  among the diffed properties, promoting it to `admin.entity.delete`.
- **Fails closed twice over**, matching I10 for `admin.*`: throws if
  `IAuditContextAccessor.Current` is null (no ambient actor — the write is blocked, not
  saved unaudited with a fabricated actor), and throws if `AuditTargetTypeKey` isn't a
  registered `AuditTargetType.Key` (catalog drift blocks the save rather than silently
  skipping the audit row).
- Sensitive properties render as `AuditValue.Redacted(ProtectedValue.Redact(...))`; an
  empty-string sensitive value falls back to `Plain("")` rather than throwing (nothing to
  protect in zero characters) — `ProtectedValue.Redact` itself still rejects empty input.

Registered as a singleton in `AddAuditInfrastructure` (stateless, safe to share) but **not
yet wired into any module's `DbContext`** — no `Tenancy`/`Identity`/`ClientManagement`
aggregate implements `IAuditableAggregate` yet, and wiring `options.AddInterceptors(...)` +
`AuditEventConfiguration` + `.ExcludeFromMigrations()` into those `DbContext`s is real work
in modules that haven't asked for it. That adoption is the next real integration step,
independent of this module's own build.

Tested against two synthetic aggregates in `WardenId.Audit.Infrastructure.Tests` (no real
consumer exists yet, same posture as `AuditEventType`'s seed catalog) — 8 tests covering
create/update/soft-delete diffs, redaction, both fail-closed paths, and that the audit row
and business row genuinely commit in one `SaveChanges` call.

---

## 6. Phased delivery — stop after each

Matches spec §9. Phase 1 (Domain + contracts) is done in full. Phase 2 (persistence +
write path) was previously marked done in full here — that was stale: spec §9.2 also
lists HTTP middleware for ambient context, which didn't exist. Now built:

- **`WardenId.Audit.Presentation`** (new project, presentation-layer only — no
  `IEndpoint`s yet): `AuditContextMiddleware`, reading the same `sub`/`TenantId` claims
  `CurrentUser`/`CurrentTenant` already read, calling `IAuditContextAccessor.BeginScope`
  for the request duration. Wired via `app.UseMiddleware<AuditContextMiddleware>()` in
  `Program.cs` (before `MapEndpoints()`) rather than a module DI extension — no module
  currently modularizes pipeline (`app.Use...`) registration, only service registration.
- **`ActorType.AuthenticatedUnknown`** — new enum member. No role/actor-type claim exists
  anywhere yet (login/token issuance isn't built — confirmed by grep, zero
  `ClaimsIdentity`/`new Claim(` call sites outside `CurrentUser`/`CurrentTenant`'s
  read side). Guessing `EndUser` vs `AdminUser` for an authenticated principal would
  mislabel audit rows, so an authenticated-but-untyped principal gets this value instead;
  revisit once real role claims land.
- The CAP filter (spec §9.2's other ambient-context producer) is still deferred to phase 5
  per §3.5 above — unchanged, that one already had an explicit rationale.

Tested in `WardenId.Audit.Presentation.Tests` (new — no other module has a
Presentation-layer test project, but the middleware's claim-parsing/actor-resolution logic
warranted it): 5 tests covering anonymous/authenticated/tenant-scoped actor resolution,
client-context capture, and scope restoration.

Phase 2 is now done in full.

**Phase 3 (sealer) is now done in full.** Built in `WardenId.Audit.Infrastructure/Sealing/`:

- **`AuditSealerService`** — the codebase's first `BackgroundService` (§3.10). Polls on
  `AuditSealerOptions.PollIntervalSeconds`, discovers every `(channel, tenantId)` pair with
  unsealed rows, and processes each independently (docs/audit/OPEN-DECISIONS.md #2 — no
  cross-channel/cross-tenant anchoring).
- **`ISealableEventReader` / `PgSealableEventReader`** — I4's watermark-below filter
  (`xmin::text::xid8 < pg_snapshot_xmin(pg_current_snapshot())::xid8`) plus the dense-sequence
  candidate fetch, both raw SQL since neither EF nor the InMemory test provider can express
  them (§3.8). Sits behind an interface specifically so `AuditSealerService`'s
  sequencing/chaining/policy logic stays unit-testable against InMemory + a fake, while this
  class itself has no test of its own — it needs a real Postgres to verify.
- **`IChannelAdvisoryLock` / `PgChannelAdvisoryLock`** — session-scoped
  `pg_try_advisory_lock`/`pg_advisory_unlock` on the same connection for the pass's duration;
  same untestable-without-Postgres caveat.
- **`SegmentSealPolicy` / `CheckpointDuePolicy`** (`WardenId.Audit.Domain/Sealing/`) — pure
  size-or-time / segments-and-time decision logic, heavily unit tested, keeping the
  Infrastructure orchestration thin per the "Domain: zero I/O, heavy unit tests" convention.
- **`Es256CheckpointSigner`** — ES256, IEEE P1363 signature format (standard JOSE ES256 shape).
  **Correction to §5.1's plan**: it does *not* call its own
  `AddDataProtection().PersistKeysToDbContext<AuditDbContext>()`. Discovered before
  implementing: Tenancy's `AddTenancyInfrastructure` already calls
  `AddDataProtection().PersistKeysToDbContext<TenancyDbContext>()`, and
  `PersistKeysToDbContext` under the hood is just `services.Configure<KeyManagementOptions>`
  — calling it from a second module doesn't compose, it silently overwrites the app-wide
  `XmlRepository` with whichever module registered last (`AddTenancyModule` runs after
  `AddAuditModule` in `Program.cs`), so both modules' keys would physically end up in
  whichever DbContext won, regardless of which module asked for its own. Resolved (user
  decision): Audit shares the app's single DataProtection key ring, calling only
  `dataProtectionProvider.CreateProtector("WardenId.Audit.CheckpointKeys.v1")` against the
  provider Tenancy already registers. The derived key material is still cryptographically
  distinct per-purpose (satisfies spec §6's "dedicated audit key distinct from the token
  signing key"); only physical storage is shared, not a dedicated repository as originally
  planned. `AuditCheckpointSigningKeyRecord`'s unique-active-key index handles concurrent
  first-key generation across sealer replicas.

Tested in `WardenId.Audit.Infrastructure.Tests` — `SegmentSealPolicyTests`/
`CheckpointDuePolicyTests` (Domain), `AuditSealerServiceTests` (8 tests: dense sequencing,
chaining across passes, both seal thresholds, advisory-lock skip, per-tenant independence,
checkpoint signing and its idempotency), `Es256CheckpointSignerTests` (key generation/reuse,
signature verifies against the published JWK). `PgSealableEventReader`/`PgChannelAdvisoryLock`
have no unit test — they're thin Postgres-only wrappers, verified against a real database.

**Phase 4 (proofs + verifier) is now done in full**, shipped as HTTP endpoints only (user
decision on docs/audit/OPEN-DECISIONS.md #5 — no standalone CLI/`WardenId.Audit.Verifier`
executable was built; revisit if an auditor-trust-nothing story is needed later).

- **`MerkleInclusionProof` / `MerkleConsistencyProof`** (`WardenId.Audit.Domain`) — RFC 6962
  §2.1.1/§2.1.2 audit path and consistency proof, generation and reconstruction each mirroring
  the exact same recursive split so the two sides can only disagree if the underlying data is
  actually wrong. Cross-validated against `MerkleTree.ComputeRoot` directly (not just against
  each other) across every leaf index for 12 leaf-count values (inclusion) and every
  `(earlierSize, laterSize)` pair up to 100 leaves (consistency) — 700+ generated cases, plus
  an exhaustive tamper-detection pass over every size pair. `MerkleConsistencyProof.Reconstruct`
  does NOT always fold its caller-supplied "trusted earlier root" into the output — for some
  size pairs the earlier subtree is fully re-derived from proof-embedded hashes without ever
  consulting it. This is correct (matches RFC 6962: the verifier is expected to independently
  compare BOTH reconstructed roots against its own two known checkpoint roots, not rely on a
  "seed and hope it propagates" check) — documented on the method, and cost one rewritten unit
  test that had assumed otherwise before the exhaustive cross-check caught the wrong premise.
- **`IAuditProofRepository`** (`Audit.Application/Abstractions`) — read-only DTOs/queries
  (events, segments, checkpoints, checkpoint keys), implemented by `AuditProofRepository`
  (`Audit.Infrastructure`) using `IDbContextFactory<AuditDbContext>` (same non-request-scoped
  convention as the recorder/sealer). Everything here reads only already-sealed data — plain
  LINQ throughout, no raw SQL/xmin needed, so unlike the sealer's Postgres-only pieces this is
  fully unit-testable against InMemory (`AuditProofRepositoryTests`, 4 tests). Deliberately a
  focused repository, not a scoped `IAuditDbContext` passthrough — avoids introducing a second
  DbContext lifetime and keeps `AuditSegmentRecord`/`AuditCheckpointRecord`/
  `AuditCheckpointSigningKeyRecord` in Infrastructure rather than moving them to Application.
- **Four query/handler pairs** (`Audit.Application/Features/`): `Proofs/Inclusion` (event id →
  canonical bytes, leaf hash, audit path, earliest covering checkpoint by ordinal segment
  position), `Proofs/Consistency` (channel/tenant + two checkpoint tree sizes → proof),
  `Verification/Verify` (full chain check — segment density/contiguity/root/header-chain,
  checkpoint root/signature — see below), `CheckpointKeys/List` (publishes public JWKs, spec
  §6). Resolves docs/audit/PLAN.md §3.3's deferred decision: no new HTTP status mapping was
  needed — `AuditErrorCodes.NotFound`'s string value already coincides with the shared
  `ErrorCodes.NotFound` used by `HandleApiFailure`'s switch (404 falls out for free), and full
  verification always returns `Result.Ok` — a broken chain is `Verified = false` in a 200
  response body, not an HTTP error, since "the chain doesn't verify" is a normal, successfully
  computed outcome, not a failure to compute it.
- **`VerificationReport`** checks, independently per segment (`SequenceDense`, `Contiguous`,
  `MerkleRootMatches`, `HeaderChainMatches`) and per checkpoint (`RootHashMatches`,
  `SignatureValid`, `SigningKeyKnown`) — a single tampered field only flips its own specific
  check, never the others, verified explicitly in tests (deleted row, tampered
  `PreviousHeaderHash`, non-contiguous `SequenceStart`, tampered checkpoint root, tampered
  signature byte, unknown signing key — 9 tests total). **Key-rotation continuity (spec §6)
  is NOT checked** — `Es256CheckpointSigner` never rotates yet (phase 3), so there is nothing
  to trace continuity across; each checkpoint is only checked against its own `kid`'s known
  key. Revisit once rotation exists.
- Presentation: 4 `IEndpoint`s under `Audit.Presentation/Features/`, tag `Audit - Proofs` /
  `Audit - Verification`, all `RequireAuthorization()` per convention (unenforced today, same
  as every other endpoint — no auth pipeline exists yet).

Tested: 19 tests in the new `WardenId.Audit.Application.Tests` project (handler-level, against
a `FakeAuditProofRepository` + a `ChainFixture` helper that builds genuinely-signed
events→segment→checkpoint chains via the real domain factories, not hand-rolled fixtures);
4 new `AuditProofRepositoryTests` in `Audit.Infrastructure.Tests`; 703 Domain tests (up from
105) covering the two proof algorithms.

---

## 6b. Phase 5 (retention + export) — now done in full

Per docs/audit/OPEN-DECISIONS.md #1/#6/#7/#8 (numbers, ordering, payload shape, §8 scope all
user-resolved before implementing). Built as designed below, with one correction caught while
writing the append-only trigger: an early draft blocked any `UPDATE` on `audit.events` other
than `Payload`, which would have broken the sealer's own legitimate one-time `Sequence`
assignment (I3) the moment the trigger shipped to a real Postgres — the sealer's existing
InMemory tests couldn't catch this since InMemory has no triggers at all. The shipped trigger
instead allows exactly two one-time transitions (`Sequence` NULL → a value, `Payload` bytes →
NULL) and blocks everything else, including a second change to either.

- **`WardenId.Audit.Domain/Retention/RetentionPolicy.cs`** — pure: `IsPurgeable(occurredAt, now,
  retentionWindow, isExported) => isExported && now - occurredAt >= retentionWindow`. Same
  shape/testing posture as `SegmentSealPolicy`/`CheckpointDuePolicy`.
- **`AuditRetentionOptions`** (`Audit.Infrastructure/Retention`) — one day-count property per
  channel (§OPEN-DECISIONS #1's resolved numbers), `RetentionWindowFor(AuditChannel)`, plus
  `PollIntervalSeconds`. `ConfigurationKeys.Retention` (`Audit:Retention` section) mirrors
  `ConfigurationKeys.Sealer`'s shape.
- **`AuditRetentionService`** (`BackgroundService`) — per poll: for every `AuditChannel`, load its
  *exported* segments (`ExportedAt != null`), and for each, load covered events with
  `Payload != null && OccurredAt < now - retentionWindow`, null `Payload`, `SaveChangesAsync`.
  No advisory lock (unlike the sealer) — a payload-nulling `UPDATE` is naturally idempotent, so
  two replicas racing the same segment just both no-op past the first winner. Bounded per-segment
  load (≤ `MaxSegmentSize` rows) keeps this safe without `ExecuteUpdateAsync`, which the
  `EntityFrameworkCore.InMemory` provider used by every existing Audit test doesn't support —
  staying with the sealer's load/mutate/`SaveChanges` shape keeps this testable the same way.
- **`AuditSegmentRecord.ExportedAt`** (`DateTimeOffset?`, new column + migration) — set once a
  segment's export has committed. Retention's `isExported` check and the exporter's own
  "what's left to export" query both key off this.
- **`AuditExporterService`** (`BackgroundService`, `Audit.Infrastructure/Exporting`) — per poll:
  load segments with `ExportedAt == null`, for each load its covered events (count must equal
  `SequenceEnd - SequenceStart + 1` — logs and skips the segment this pass otherwise, e.g. sealer
  lag), build `AuditSegmentExportedMessage` (segment header + every event's `Id`/`Sequence`/
  `OccurredAt`/`Payload`/`LeafHash` — §OPEN-DECISIONS #7), open a plain EF transaction, publish via
  `IAuditSegmentPublisher` enlisted onto that transaction (mirrors `DomainEventsInterceptor`'s
  already-open-transaction branch — `capPublisher.Transaction = ActivatorUtilities
  .CreateInstance<PostgreSqlCapTransaction>(...)`), set `ExportedAt`, `SaveChangesAsync`, commit.
  Publish and the `ExportedAt` flag land in one transaction so "marked exported" implies
  "genuinely landed in CAP's own durable outbox" — a crash between the two rolls both back.
  `IAuditSegmentPublisher`/`CapAuditSegmentPublisher` split purely for testability (no hand-rolled
  `ICapPublisher` fake exists or is practical — same reasoning as `ISealableEventReader`/
  `IChannelAdvisoryLock`'s split), with a `FakeAuditSegmentPublisher` recording published
  messages in `AuditExporterServiceTests`. Per-segment try/catch in the poll loop — one bad
  segment must not block every other tenant's export, same posture as the sealer's per-segment
  error handling.
- **Migration**: `audit.segments.ExportedAt` column; a `BEFORE UPDATE OR DELETE` trigger on
  `audit.events` (raw SQL, on the partitioned parent — Postgres propagates row-level triggers to
  every partition the same way it already propagates the phase-1 indexes) that raises on any
  `DELETE`; on any change to `Id`/`Channel`/`TenantId`/`OccurredAt`/`LeafHash`; on `Sequence`
  changing once it's already non-null (a second reassignment); and on `Payload` being restored
  once purged or replaced with different non-null bytes. This is the properly scoped enforcement
  of I3/I6/spec §8 — independent of *which* DB role issues the statement, unlike a role-grant-only
  approach — while still letting the sealer's one-time `Sequence` assignment and retention's
  one-time `Payload` nulling through (see this section's opening correction).
- **`IAuditCheckpointWitness`** (`Audit.Application/Abstractions` — signature only mentions
  primitives/`AuditChannel`, same `Audit.Application`-not-SharedKernel placement as
  `IAuditRecorder`) with two optional built-ins in `Audit.Infrastructure/Witnessing`:
  `FileAppendCheckpointWitness` (appends the signed-note-style body from spec §6 to a configured
  append-only file path) and `WebhookCheckpointWitness` (POSTs the same body to a configured URL).
  Both no-op/absent unless configured; `AuditSealerService.CheckpointIfDueAsync` invokes whichever
  are registered right after a checkpoint is created, best-effort (log and continue — a witness
  failure must not stop the checkpoint from being recorded in `audit.checkpoints`, which is
  already the authoritative signed record). `AddAuditInfrastructure` logs a single startup warning
  when neither witness is configured, matching spec §8's required posture, and a second warning
  when no dedicated retention connection string is configured (see below).
- **`docs/audit/db-roles.sql`** (new, not a migration) — a documented, manually-run script:
  `CREATE ROLE warden_audit_app`/`warden_audit_retention`, `GRANT SELECT, INSERT ON audit.events TO
  warden_audit_app`, `GRANT UPDATE (Payload) ON audit.events TO warden_audit_retention` (column-
  level grant), explicit `REVOKE UPDATE, DELETE ON audit.events FROM warden_audit_app`. Not run
  automatically — see docs/audit/OPEN-DECISIONS.md #8 for why. `AuditRetentionService` reads its
  own optional `ConnectionStrings:AuditRetention`; falls back to the default connection string
  with a startup warning if absent, same shape as the checkpoint-witness warning above.

Tested in `WardenId.Audit.Domain.Tests` (`RetentionPolicyTests`, 4 tests — not-exported/
within-window/past-window/exact-boundary) and `WardenId.Audit.Infrastructure.Tests`:
`AuditRetentionServiceTests` (5 — purges when exported+past-window, leaves payload when
not-exported even if very old, leaves payload within the retention window, never touches
unsealed rows, idempotent against a second pass); `AuditExporterServiceTests` (4, against
`FakeAuditSegmentPublisher` since no hand-rolled `ICapPublisher` fake is practical — publishes
and marks `ExportedAt`, skips an already-exported segment, skips a segment the sealer hasn't
fully caught up on yet rather than exporting a partial one, and one segment's publish failure
doesn't block another tenant's); two new `AuditSealerServiceTests` for witness invocation
(invokes every configured witness with the signed-note text, and a witness throwing doesn't
prevent the checkpoint itself from being recorded); `FileAppendCheckpointWitnessTests` (2) and
`WebhookCheckpointWitnessTests` (3, against a stub `HttpMessageHandler`). `CapAuditSegmentPublisher`
and the append-only trigger have no test of their own — both need a real CAP-wired Postgres to
verify, same posture as `PgSealableEventReader`/`PgChannelAdvisoryLock`.

---

## 7. Open questions

See `docs/audit/OPEN-DECISIONS.md` — surfaced per spec §12, not guessed at here.
