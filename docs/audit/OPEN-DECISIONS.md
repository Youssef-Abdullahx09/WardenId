# Audit Module — Open Decisions

Per spec §12 — surfaced, not guessed. Need answers before phase 2 (persistence/write
path needs retention + `token.ops` scope; phase 3 needs segment/interval defaults; phase 4
needs the verifier shipping decision) can be implemented with confidence.

1. ~~**Retention defaults per channel**~~ — **Resolved (user decision, phase 5):**
   - `authn.interactive` — 400 days
   - `authn.silent` — 30 days
   - `authn.machine` — 14 days
   - `admin.directory` — 2555 days (7y)
   - `admin.config` — 2555 days (7y)
   - `token.ops` — 1 day
   - `platform` — 2555 days (7y)

   Bound via `AuditRetentionOptions`/`ConfigurationKeys.Retention` (`Audit:Retention` section), same
   config-not-code-change posture as the sealer's `AuditSealerOptions`.

2. **Tenant checkpoint anchoring** — do tenant checkpoints anchor into the platform chain
   at seal time (every tenant checkpoint immediately references the platform chain's
   current head) or on a schedule (periodic batch anchor, e.g. hourly/daily)? This affects
   whether the sealer needs cross-channel coordination per-segment or can run channels
   fully independently. **Phase 3 built assuming "fully independently"** — each channel
   seals/checkpoints on its own, no cross-channel anchoring. Revisit if platform-anchoring
   is actually wanted; it's additive on top of what's built, not a rework.

3. **`token.ops` scope** — platform-only default-off channel, or can individual tenants
   opt in/configure it themselves? Affects whether the channel's on/off state lives in
   platform config or per-tenant settings.

4. **Segment size and seal interval defaults** — the "size OR time, whichever fires
   first" rule (spec §6) needs actual numbers per channel (e.g. rows-per-segment,
   max-seconds-before-forced-seal), likely different for `authn.machine` (very high
   volume) vs `admin.config` (very low volume). **Defaulted in phase 3, not user-confirmed:**
   one global default for now (segment = 10,000 events OR 5 minutes, whichever first;
   checkpoint = every 10 segments OR 1 hour, whichever looser), exposed via
   `ConfigurationKeys.Audit` so it's a config change, not a code change, to differentiate
   per channel later or adjust these numbers.

5. ~~**Verifier shipping surface**~~ — **Resolved (user decision, phase 4): HTTP endpoints
   only.** No standalone CLI/`WardenId.Audit.Verifier` executable was built. Revisit if an
   auditor-trust-nothing story (verify against a DB backup with zero running Warden instance)
   is actually needed later — the proof/verification math itself
   (`MerkleInclusionProof`/`MerkleConsistencyProof`, the `VerificationReport` checks in
   `Audit.Application/Features/Verification/Verify`) has no dependency on ASP.NET Core and
   could be lifted into a CLI without a rewrite.

6. **Purge-vs-export ordering** — **Resolved (user decision, phase 5): gate on export.**
   A row is only purge-eligible once the sealed segment covering it has `ExportedAt != null`
   (i.e. successfully published via CAP). If no CAP sink ever consumes the export topic, rows
   past their retention window simply accumulate un-purged rather than silently losing payload
   data that was never archived anywhere. Revisit if this backlog risk proves undesirable in
   practice — `RetentionPolicy.IsPurgeable` takes `isExported` as a plain bool parameter, so
   flipping to age-only is a one-line change at the call site, not a rework.

7. **CAP export payload shape** — **Resolved (user decision, phase 5): full events.**
   `AuditSegmentExportedMessage` carries the segment header plus every covered event's canonical
   `Payload` bytes and `LeafHash` — lets an external sink act as a genuine archive/witness tier
   (spec §1) rather than a pointer back into Warden's own DB.

8. **Spec §8 (append-only enforcement) scope** — **Resolved (user decision, phase 5): included.**
   Built as: (a) a DB trigger blocking `DELETE` and any `UPDATE` touching a column other than
   `Payload` on `audit.events` (portable, ships in a migration); (b) `IAuditCheckpointWitness` —
   optional append-only-file and webhook implementations, invoked per signed checkpoint; (c) a
   startup warning when neither witness is configured. **Not built:** actual Postgres role
   separation (a distinct DB role with only `SELECT`/`INSERT` on `audit.events`, and a
   further-restricted role with only column-level `UPDATE (Payload)`). This repo has no
   role-provisioning infra at all today (no docker-compose/IaC, one `DefaultConnectionString`),
   and auto-running `CREATE ROLE`/`GRANT` inside an EF migration that executes on every startup
   would crash the app on any host where the configured DB user lacks `CREATEROLE` — too large a
   blast radius for a defense-in-depth nicety. Shipped instead as a manual, documented SQL script
   (`docs/audit/db-roles.sql`) ops can run once; a startup warning fires if no separate retention
   connection string is configured. Revisit if a hosted/managed deployment story needs this
   automated.
