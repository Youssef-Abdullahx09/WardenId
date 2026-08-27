# Warden ID — Audit Module Implementation Spec

You are implementing the **Audit module** for Warden ID, a self-hosted multi-tenant
OIDC/OAuth2 identity provider built as a .NET modular monolith.

---

## 0. Before you write any code

Inspect the existing solution and **match its conventions rather than inventing new ones**:

1. Read an existing module end to end (Tenancy is the most complete). Note the folder
   layout, project naming, namespace roots, DI container registration, `DbContext`
   placement, `IDesignTimeDbContextFactory` pattern, and migration setup.
2. Read the SharedKernel. Note what already exists: result types, base entity/aggregate
   types, clock abstraction, CAP filters, `ReadFileService`.
3. Read the CAP wiring — the `ISubscribeFilter` pipeline and the messaging schema.

Then write a short plan to `docs/audit/PLAN.md` covering the files you intend to add
and any place where this spec conflicts with existing conventions. **Stop and wait for
review before implementing.**

If anything in this spec contradicts an established pattern in the codebase, the
codebase wins — flag the conflict in the plan instead of silently diverging.

---

## 1. Design summary

Three layers, each borrowed from a different production system:

| Layer | Source | What it gives us |
|---|---|---|
| Record | Okta System Log | One envelope, dot-notation taxonomy, target array, first-class outcome |
| Routing | Entra ID | N channels over that envelope, independent volume/retention/default-on |
| Integrity | CloudTrail, via RFC 6962 Merkle | Append-only proof, O(log n) inclusion proofs |

The **database is the system of record.** Files and external sinks are archive and
witness tiers. Warden must be fully functional and verifiable with nothing but Postgres.

---

## 2. Scope

**In scope:** event envelope and taxonomy, actor model, capture pipeline, transactional
write path, channel routing and partitioning, sealer with Merkle segments and signed
checkpoints, inclusion/consistency proof generation and verification, query API,
retention with hash-preserving purge, export via CAP.

**Out of scope for now:** admin UI, OCSF field mapping, CAEP/SSF push, witness
co-signing, SIEM-specific sink adapters. Leave extension points, build no adapters.

---

## 3. Domain model

### 3.1 Actor

Everything joins on this type. Get it right first.

```csharp
public enum ActorType { EndUser, AdminUser, ServiceAccount, OAuthClient, System, Anonymous }

public sealed record ActorScope
{
    public bool IsPlatform { get; }
    public TenantId? TenantId { get; }   // null iff IsPlatform
}

public sealed record Actor(
    ActorType Type,
    string Id,              // opaque, tombstoned, never reassigned
    ActorScope Scope,
    Actor? OnBehalfOf);     // impersonation — maps to the `act` claim
```

Rules:

- `Id` is **opaque and permanent**. Never store email, username, or display name on the
  actor. Rendering a human-readable name is a join against a separately-tombstoned
  subject directory, performed at read time.
- Serialized form must be scope-qualified and unambiguous:
  `platform:adminuser:{id}` / `tenant:{tid}:oauthclient:{clientId}`.
  A bare GUID that could be either a platform admin or a tenant admin is a footgun.
- `System` covers hosted services, process managers, and bootstrap. It is never null-actor.
- `Anonymous` covers pre-authentication events (failed login with unknown username).
- Dynamic Client Registration produces an actor with no human behind it. Represent it
  honestly as `ServiceAccount` or `Anonymous` with `CreatedVia = dcr` on the target —
  do not fabricate a synthetic admin.

### 3.2 Envelope

```csharp
public sealed record AuditEvent
{
    public Guid Id { get; init; }                     // UUIDv7, time-ordered
    public AuditChannel Channel { get; init; }
    public string EventType { get; init; }            // "user.session.start"
    public int SchemaVersion { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public AuditSeverity Severity { get; init; }

    public Actor Actor { get; init; }
    public IReadOnlyList<AuditTarget> Targets { get; init; }   // always an array
    public AuditOutcome Outcome { get; init; }                 // Result + Reason

    public ClientContext Client { get; init; }                 // ip, ua, asn, geo
    public AuthContext AuthenticationContext { get; init; }    // sid, jti, acr, amr, auth_time, protocol
    public Correlation Correlation { get; init; }              // requestId, traceId, transactionId
    public IReadOnlyList<PropertyChange> Changes { get; init; }// old/new per field
}
```

`AuditTarget` carries `Type`, `Id`, and optional `CreatedVia`. Targets are an array
because one action can affect several objects.

**There is no untyped `Dictionary<string, object>` bag anywhere in this envelope.**
Untyped bags are how secrets leak into audit logs. If a new field is needed, add it to
the typed envelope and bump `SchemaVersion`.

### 3.3 Channels

| Channel | Default | Volume | Retention |
|---|---|---|---|
| `authn.interactive` | on | medium | long |
| `authn.silent` | on | high | short |
| `authn.machine` | on | very high | short |
| `admin.directory` | on | low | longest |
| `admin.config` | on | very low | longest |
| `token.ops` | **off** | extreme | very short |
| `platform` | on, separate store | low | longest |

Channel is a **physical partition key**, not a filter. Use declarative partitioning so a
flood on `authn.machine` cannot degrade queries against `admin.config`.

`platform` must not live in tenant-scoped storage — it has to retain the tenant-deletion
event after the tenant's schema is dropped.

---

## 4. Non-negotiable invariants

These are correctness-critical and each has a failure mode that looks like something else.
Implement them exactly. Every one gets a test.

**I1 — Domain separation on hashes.**
`leaf = SHA256(0x00 || canonical_bytes)`, `internal = SHA256(0x01 || left || right)`.
Omitting the prefix byte enables a second-preimage attack. RFC 6962.

**I2 — Persist the bytes you hashed.**
Serialize the envelope once, store those exact bytes in a `bytea` column, and compute the
leaf hash from them at insert time. Never rehydrate an EF entity and re-serialize at
verification time — JSON settings, value converters, and migrations all drift.

**I3 — Writers never assign the sequence.**
`Sequence` is nullable on insert. A DB sequence leaks gaps on rollback, and a gap is
indistinguishable from a deletion. The sealer assigns a **dense** sequence over
already-committed rows.

**I4 — Seal below the snapshot watermark.**
Only seal rows whose `xmin` is below `pg_snapshot_xmin(pg_current_snapshot())`. Anything
above it may still be preceded by an in-flight transaction.

**I5 — Hash over ciphertext, not plaintext.**
PII fields are encrypted before canonicalization. Destroying a subject key for a GDPR
erasure must leave every root and proof intact.

**I6 — Retention purges the payload, keeps the leaf hash.**
`UPDATE audit.events SET payload = NULL WHERE ...`. The `(sequence, leaf_hash)` row
survives so roots stay recomputable and sequence density still detects deletion.
**Never `DELETE` from `audit.events` in the retention path.**

**I7 — Ambient context is `AsyncLocal`, not `IHttpContextAccessor`.**
CAP consumers, hosted services, and the provisioning process manager have no
`HttpContext`. An accessor built on it silently yields anonymous actors in exactly the
paths that matter most.

**I8 — Capture is not interceptor-only.**
Most IdP events have no database write: failed logins, rejected `redirect_uri`, PKCE
mismatch, introspection. `SaveChangesInterceptor` gives per-property diffs for admin
CRUD; everything else must be emitted explicitly.

**I9 — Redaction is enforced by types.**
Sensitive values enter the envelope only as `ProtectedValue`, which exposes a hash or a
short prefix and has no accessor returning the raw value. Never log: authorization codes,
access/refresh/ID tokens, client secrets, passwords, PKCE verifiers, session cookies.
Log instead: `jti`, `sid`, `kid`, token-family id, `auth_time`.

**I10 — Fail-open on `authn.*`, fail-closed on `admin.config`.**
An audit write failure must never break a login. Refusing to rotate a signing key when
the action cannot be recorded is correct.

---

## 5. Write path

```
Capture   → AuditEvent built with ambient actor/correlation, redaction enforced
Write     → INSERT into audit.events, in the caller's transaction where one exists
Sealer    → dense sequence, Merkle segment, chained header, periodic signed checkpoint
Exporter  → CAP publish sealed segments to external sinks
```

`audit.events` **is** the outbox. Do not route audit writes through CAP's `published`
table — there is exactly one consumer, in-process, writing to the same database. Adding a
broker hop makes audit durability depend on RabbitMQ and creates a window where the event
is committed but invisible. CAP appears at the exporter, where a process boundary is
actually crossed.

Two write modes:

- **Transactional** (`admin.*`) — audit row and business change commit or fail together.
- **Direct append** (`authn.*`, `token.ops`) — no ambient transaction exists; standalone
  insert, fail-open.

Module `DbContext`s are isolated but the audit row must land in the caller's transaction.
Put `AuditEventConfiguration : IEntityTypeConfiguration<AuditEventRecord>` in SharedKernel
and have each module's `DbContext` apply it, all mapped to `audit.events`. One
`SaveChanges`, one local transaction, no distributed coordination. Modules append only —
never read another module's audit rows.

---

## 6. Sealer

A `BackgroundService`, one active sealer per channel across all replicas.

- Distributed lock via **Postgres advisory locks** (`pg_try_advisory_lock`) — they release
  on connection drop, so a killed pod cannot wedge a channel.
- Seal on **size OR time**, whichever fires first. Size alone means a quiet tenant never
  seals; time alone means a busy channel builds unbounded segments.
- Chain every segment. Sign a checkpoint every N segments or every T seconds, whichever
  is looser — keeps signing-key access rare.
- The sealer may lag arbitrarily without affecting availability. Events are queryable the
  instant they commit. Alert on backlog depth; never block a write on sealing.

### Structure

Segment trees plus a chained header list, plus a second-level Merkle tree over segment
headers so consistency proofs stay O(log n) rather than degrading to a linear walk.

```sql
audit.events      (id, sequence NULL, channel, tenant_id, occurred_at,
                   payload bytea NULL, leaf_hash bytea NOT NULL, xmin-derived seal state)
audit.segments    (segment_id, channel, tenant_id, seq_start, seq_end,
                   merkle_root bytea, prev_header_hash bytea, header_hash bytea, sealed_at)
audit.checkpoints (checkpoint_id, segment_id, tree_size, root_hash bytea,
                   kid, signature bytea, signed_at)
```

Do **not** persist internal Merkle nodes. Read `leaf_hash` ordered by `sequence` and
rebuild in memory — 100k leaves is ~3.2 MB and a few ms of SHA-256. Add a node cache only
if proof serving measurably becomes hot.

### Signing

ES256, using a **dedicated audit key distinct from the token signing key**, sign-only.
Reuse the existing `Es256SigningKeyGenerator` / Data Protection pattern from Tenancy.
Publish the public key at a verification endpoint.

Checkpoint body, signed-note style for interop:

```
warden-audit/{tenantId}/{channel}
{tree_size}
{root_hash_base64}

— {kid} {signature_base64}
```

Key rotation must itself be an audited event chained into the log, and the first
checkpoint under a new key must be verifiable from the old one (dual-sign, or a rotation
attestation signed by the outgoing key). Otherwise a verifier cannot distinguish
legitimate rotation from key substitution.

---

## 7. Proofs and verification

Expose, as application-layer queries:

- **Inclusion proof** — given an event id, return canonical bytes plus O(log n) sibling
  hashes and the covering checkpoint.
- **Consistency proof** — given two tree sizes, prove the earlier is a strict prefix of
  the later.
- **Full verification** — (1) checkpoint signature and key-rotation continuity,
  (2) header chain `H(header_i) == header_{i+1}.prev_header_hash`, (3) per-segment root
  recomputation, (4) sequence density `(seq_end - seq_start + 1) == leaf_count` and
  segments contiguous.

Step 4 is why the dense sequence exists: a Merkle root proves the leaves you were *given*
are intact; it cannot tell you a leaf was removed before you looked.

Ship a standalone verifier that runs against the database with **no dependency on module
internals** — an auditor must be able to verify without trusting Warden's own code paths.

---

## 8. Append-only enforcement

Defense in depth. The chain detects tampering; these make it fail loudly instead.

- App role: `INSERT` and `SELECT` only on `audit.*`. Retention runs under a separate,
  narrowly-scoped role.
- `BEFORE UPDATE OR DELETE` trigger that raises, with an explicit exemption for the
  retention role's payload nulling.
- Config for external checkpoint sinks: append-only file on a separate volume, and a
  webhook per checkpoint. Both optional; when neither is configured, log a startup warning
  stating that the audit log is only as durable as the database. The chain cannot detect
  tail truncation or a dropped schema without an external witness — say so plainly.

---

## 9. Phases

Implement in order. **Stop after each phase and wait for review.**

1. **Domain + contracts** — Actor, envelope, channels, `ProtectedValue`, canonical
   serializer, leaf hashing. Pure, no I/O. Heavy unit tests.
2. **Persistence + write path** — schema, partitioning, SharedKernel entity config,
   `IAuditContextAccessor`, middleware and CAP filter for ambient context, interceptor
   for admin diffs, explicit emission API.
3. **Sealer** — watermark, dense sequencing, Merkle segments, header chain, checkpoints,
   advisory locking.
4. **Proofs + verifier** — inclusion, consistency, standalone verification tool.
5. **Retention + export** — hash-preserving purge, per-channel policy, CAP segment export.

---

## 10. Acceptance tests

Each must exist and pass:

- Round-trip an envelope through canonical serialization; byte-identical output across
  process restarts and after an unrelated migration.
- Tamper a payload byte in the DB → verification fails at the segment root.
- Delete a row → verification fails on sequence density.
- Insert a backdated row → verification fails.
- Null a payload via the retention path → verification still **passes**.
- Destroy a subject encryption key → verification still **passes**, PII unreadable.
- Concurrent writers under load → dense sequence, no gaps, no duplicates.
- Two sealer instances race → exactly one seals; the other no-ops.
- Sealer killed mid-segment → restarts cleanly, no gap, no double-seal.
- Inclusion proof for one event verifies without reading any other event's payload.
- Failed login with unknown username produces an event with `Anonymous` actor.
- Event emitted from a CAP consumer carries the correct system actor and correlation.
- Audit write failure on `authn.interactive` does not fail the login.
- Audit write failure on `admin.config` rolls back the config change.
- No test can construct an envelope containing a raw token, code, or secret.

---

## 11. Do not

- Do not add a `Dictionary<string, object>` or `debugContext` bag to the envelope.
- Do not put `CreatedBy`/`UpdatedBy` on audit rows — the actor is already in the envelope.
- Do not use `IHttpContextAccessor` in the audit context accessor.
- Do not `DELETE` from `audit.events` anywhere.
- Do not reuse the token signing key for checkpoints.
- Do not route audit writes through CAP.
- Do not assign sequence numbers at insert time.
- Do not snapshot display names or emails onto audit rows.
- Do not add SIEM-vendor adapters or OCSF mapping in this pass.
- Do not invent new architectural patterns where the codebase already has one.

---

## 12. Surface these rather than guessing

Add to `docs/audit/OPEN-DECISIONS.md` and stop for input:

- Retention defaults per channel (numbers, not "long"/"short").
- Whether tenant checkpoints anchor into the platform chain at seal time or on a schedule.
- Whether `token.ops` is per-tenant-configurable or platform-only.
- Segment size and seal interval defaults.
- Whether the verifier ships as a CLI, an endpoint, or both.
