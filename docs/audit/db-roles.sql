-- WardenId Audit module — optional DB role separation (spec §8, docs/audit/OPEN-DECISIONS.md #8).
--
-- NOT run automatically. EF migrations execute on every startup (see CLAUDE.md); auto-running
-- CREATE ROLE/GRANT here would crash the app on any host where the configured DB user lacks
-- CREATEROLE, and self-hosted deployments manage their own DB users. Run this once, manually,
-- as a superuser (or a role with CREATEROLE + ownership of the audit schema), then point
-- ConnectionStrings:AuditRetention (WardenId.Gateway/appsettings.json) at the second role's
-- credentials. Until you do, the retention purge path runs under the same role as the rest of
-- the app and a startup warning says so.
--
-- The append-only trigger (migration AddSegmentExportedAtAndAppendOnlyTrigger) already blocks
-- DELETE and any UPDATE outside Sequence-once/Payload-once regardless of which role issues the
-- statement — this script narrows *who* can even attempt the Payload-nulling UPDATE in the
-- first place, in addition to that.

-- Replace these with real, secret values before running.
\set app_password '''change-me-app'''
\set retention_password '''change-me-retention'''

-- Normal app traffic: every module inserts into audit.events; only the sealer additionally
-- assigns Sequence, and nothing app-side ever touches Payload once written.
CREATE ROLE warden_audit_app LOGIN PASSWORD :app_password;
GRANT USAGE ON SCHEMA audit TO warden_audit_app;
GRANT SELECT, INSERT ON audit.events TO warden_audit_app;
GRANT SELECT, INSERT, UPDATE ON audit.segments, audit.checkpoints, audit.checkpoint_signing_keys TO warden_audit_app;
GRANT UPDATE ("Sequence") ON audit.events TO warden_audit_app; -- the sealer's one-time assignment
REVOKE UPDATE, DELETE ON audit.events FROM warden_audit_app; -- re-assert: no other column, ever

-- Retention: only ever needs to null Payload on already-sealed, already-exported rows.
CREATE ROLE warden_audit_retention LOGIN PASSWORD :retention_password;
GRANT USAGE ON SCHEMA audit TO warden_audit_retention;
GRANT SELECT ON audit.events, audit.segments TO warden_audit_retention;
GRANT UPDATE ("Payload") ON audit.events TO warden_audit_retention;
