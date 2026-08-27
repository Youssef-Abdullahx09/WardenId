using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChannelAsString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // segments/checkpoints: Postgres has no implicit int -> text cast and forbids a
            // subquery in ALTER COLUMN TYPE's USING clause entirely, so each goes through a
            // shadow column: add text column, backfill via UPDATE (subqueries ARE allowed
            // there), drop the int column, rename the new one into place (same approach as
            // Identity's PendingActionsAsStrings migration).
            migrationBuilder.Sql(
                """
                ALTER TABLE audit.segments ADD COLUMN "ChannelNew" character varying(30);

                UPDATE audit.segments SET "ChannelNew" = CASE "Channel"
                    WHEN 1 THEN 'AuthnInteractive'
                    WHEN 2 THEN 'AuthnSilent'
                    WHEN 3 THEN 'AuthnMachine'
                    WHEN 4 THEN 'AdminDirectory'
                    WHEN 5 THEN 'AdminConfig'
                    WHEN 6 THEN 'TokenOps'
                    WHEN 7 THEN 'Platform'
                END;

                ALTER TABLE audit.segments ALTER COLUMN "ChannelNew" SET NOT NULL;
                DROP INDEX audit."IX_segments_Channel_SequenceEnd";
                ALTER TABLE audit.segments DROP COLUMN "Channel";
                ALTER TABLE audit.segments RENAME COLUMN "ChannelNew" TO "Channel";
                CREATE INDEX "IX_segments_Channel_SequenceEnd" ON audit.segments ("Channel", "SequenceEnd");
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE audit.checkpoints ADD COLUMN "ChannelNew" character varying(30);

                UPDATE audit.checkpoints SET "ChannelNew" = CASE "Channel"
                    WHEN 1 THEN 'AuthnInteractive'
                    WHEN 2 THEN 'AuthnSilent'
                    WHEN 3 THEN 'AuthnMachine'
                    WHEN 4 THEN 'AdminDirectory'
                    WHEN 5 THEN 'AdminConfig'
                    WHEN 6 THEN 'TokenOps'
                    WHEN 7 THEN 'Platform'
                END;

                ALTER TABLE audit.checkpoints ALTER COLUMN "ChannelNew" SET NOT NULL;
                DROP INDEX audit."IX_checkpoints_Channel_SignedAt";
                ALTER TABLE audit.checkpoints DROP COLUMN "Channel";
                ALTER TABLE audit.checkpoints RENAME COLUMN "ChannelNew" TO "Channel";
                CREATE INDEX "IX_checkpoints_Channel_SignedAt" ON audit.checkpoints ("Channel", "SignedAt");
                """);

            // events: Channel is the partition key (see InitialCreate), and Postgres flatly
            // refuses to retype a column that's part of a partition key ("cannot alter type of
            // column ... because it is part of the partition key"). No shadow-column trick can
            // work around that either — the column identity itself is what's pinned. The only
            // path is a full rebuild: rename the old partitioned table out of the way, create a
            // new one on text partition bounds, copy rows across (mapping int -> name), then
            // drop the old one. DROP TABLE never fires the append-only trigger below — that only
            // guards actual DELETE/UPDATE statements, not DDL — so the existing rows' history
            // isn't "deleted" in the sense I6 cares about.
            migrationBuilder.Sql(
                """
                ALTER TABLE audit.events RENAME TO events_old;
                ALTER TABLE audit.events_old RENAME CONSTRAINT "PK_events" TO "PK_events_old";

                -- Renaming the parent doesn't rename its partitions — they'd otherwise collide
                -- by name with the new partitions created below.
                ALTER TABLE audit.events_authn_interactive RENAME TO events_authn_interactive_old;
                ALTER TABLE audit.events_authn_silent RENAME TO events_authn_silent_old;
                ALTER TABLE audit.events_authn_machine RENAME TO events_authn_machine_old;
                ALTER TABLE audit.events_admin_directory RENAME TO events_admin_directory_old;
                ALTER TABLE audit.events_admin_config RENAME TO events_admin_config_old;
                ALTER TABLE audit.events_token_ops RENAME TO events_token_ops_old;
                ALTER TABLE audit.events_platform RENAME TO events_platform_old;
                ALTER TABLE audit.events_default RENAME TO events_default_old;

                CREATE TABLE audit.events (
                    "Id" uuid NOT NULL,
                    "Channel" character varying(30) NOT NULL,
                    "Sequence" bigint NULL,
                    "TenantId" uuid NULL,
                    "OccurredAt" timestamp with time zone NOT NULL,
                    "Payload" bytea NULL,
                    "LeafHash" bytea NOT NULL,
                    CONSTRAINT "PK_events" PRIMARY KEY ("Id", "Channel")
                ) PARTITION BY LIST ("Channel");

                CREATE TABLE audit.events_authn_interactive PARTITION OF audit.events FOR VALUES IN ('AuthnInteractive');
                CREATE TABLE audit.events_authn_silent PARTITION OF audit.events FOR VALUES IN ('AuthnSilent');
                CREATE TABLE audit.events_authn_machine PARTITION OF audit.events FOR VALUES IN ('AuthnMachine');
                CREATE TABLE audit.events_admin_directory PARTITION OF audit.events FOR VALUES IN ('AdminDirectory');
                CREATE TABLE audit.events_admin_config PARTITION OF audit.events FOR VALUES IN ('AdminConfig');
                CREATE TABLE audit.events_token_ops PARTITION OF audit.events FOR VALUES IN ('TokenOps');
                CREATE TABLE audit.events_platform PARTITION OF audit.events FOR VALUES IN ('Platform');
                CREATE TABLE audit.events_default PARTITION OF audit.events DEFAULT;

                INSERT INTO audit.events ("Id", "Channel", "Sequence", "TenantId", "OccurredAt", "Payload", "LeafHash")
                SELECT "Id",
                    CASE "Channel"
                        WHEN 1 THEN 'AuthnInteractive'
                        WHEN 2 THEN 'AuthnSilent'
                        WHEN 3 THEN 'AuthnMachine'
                        WHEN 4 THEN 'AdminDirectory'
                        WHEN 5 THEN 'AdminConfig'
                        WHEN 6 THEN 'TokenOps'
                        WHEN 7 THEN 'Platform'
                    END,
                    "Sequence", "TenantId", "OccurredAt", "Payload", "LeafHash"
                FROM audit.events_old;

                -- The old partitioned table still holds indexes/trigger under the same names
                -- ("IX_events_...", "events_append_only_guard") since renaming a table doesn't
                -- rename those — drop it first so the new table can claim the names back.
                DROP TABLE audit.events_old;

                CREATE INDEX "IX_events_Channel_Sequence" ON audit.events ("Channel", "Sequence");
                CREATE INDEX "IX_events_TenantId_OccurredAt" ON audit.events ("TenantId", "OccurredAt");

                CREATE TRIGGER events_append_only_guard
                    BEFORE UPDATE OR DELETE ON audit.events
                    FOR EACH ROW EXECUTE FUNCTION audit.events_append_only_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE audit.events RENAME TO events_old;
                ALTER TABLE audit.events_old RENAME CONSTRAINT "PK_events" TO "PK_events_old";

                ALTER TABLE audit.events_authn_interactive RENAME TO events_authn_interactive_old;
                ALTER TABLE audit.events_authn_silent RENAME TO events_authn_silent_old;
                ALTER TABLE audit.events_authn_machine RENAME TO events_authn_machine_old;
                ALTER TABLE audit.events_admin_directory RENAME TO events_admin_directory_old;
                ALTER TABLE audit.events_admin_config RENAME TO events_admin_config_old;
                ALTER TABLE audit.events_token_ops RENAME TO events_token_ops_old;
                ALTER TABLE audit.events_platform RENAME TO events_platform_old;
                ALTER TABLE audit.events_default RENAME TO events_default_old;

                CREATE TABLE audit.events (
                    "Id" uuid NOT NULL,
                    "Channel" integer NOT NULL,
                    "Sequence" bigint NULL,
                    "TenantId" uuid NULL,
                    "OccurredAt" timestamp with time zone NOT NULL,
                    "Payload" bytea NULL,
                    "LeafHash" bytea NOT NULL,
                    CONSTRAINT "PK_events" PRIMARY KEY ("Id", "Channel")
                ) PARTITION BY LIST ("Channel");

                CREATE TABLE audit.events_authn_interactive PARTITION OF audit.events FOR VALUES IN (1);
                CREATE TABLE audit.events_authn_silent PARTITION OF audit.events FOR VALUES IN (2);
                CREATE TABLE audit.events_authn_machine PARTITION OF audit.events FOR VALUES IN (3);
                CREATE TABLE audit.events_admin_directory PARTITION OF audit.events FOR VALUES IN (4);
                CREATE TABLE audit.events_admin_config PARTITION OF audit.events FOR VALUES IN (5);
                CREATE TABLE audit.events_token_ops PARTITION OF audit.events FOR VALUES IN (6);
                CREATE TABLE audit.events_platform PARTITION OF audit.events FOR VALUES IN (7);
                CREATE TABLE audit.events_default PARTITION OF audit.events DEFAULT;

                INSERT INTO audit.events ("Id", "Channel", "Sequence", "TenantId", "OccurredAt", "Payload", "LeafHash")
                SELECT "Id",
                    CASE "Channel"
                        WHEN 'AuthnInteractive' THEN 1
                        WHEN 'AuthnSilent' THEN 2
                        WHEN 'AuthnMachine' THEN 3
                        WHEN 'AdminDirectory' THEN 4
                        WHEN 'AdminConfig' THEN 5
                        WHEN 'TokenOps' THEN 6
                        WHEN 'Platform' THEN 7
                    END,
                    "Sequence", "TenantId", "OccurredAt", "Payload", "LeafHash"
                FROM audit.events_old;

                DROP TABLE audit.events_old;

                CREATE INDEX "IX_events_Channel_Sequence" ON audit.events ("Channel", "Sequence");
                CREATE INDEX "IX_events_TenantId_OccurredAt" ON audit.events ("TenantId", "OccurredAt");

                CREATE TRIGGER events_append_only_guard
                    BEFORE UPDATE OR DELETE ON audit.events
                    FOR EACH ROW EXECUTE FUNCTION audit.events_append_only_guard();
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE audit.checkpoints ADD COLUMN "ChannelOld" integer;

                UPDATE audit.checkpoints SET "ChannelOld" = CASE "Channel"
                    WHEN 'AuthnInteractive' THEN 1
                    WHEN 'AuthnSilent' THEN 2
                    WHEN 'AuthnMachine' THEN 3
                    WHEN 'AdminDirectory' THEN 4
                    WHEN 'AdminConfig' THEN 5
                    WHEN 'TokenOps' THEN 6
                    WHEN 'Platform' THEN 7
                END;

                ALTER TABLE audit.checkpoints ALTER COLUMN "ChannelOld" SET NOT NULL;
                DROP INDEX audit."IX_checkpoints_Channel_SignedAt";
                ALTER TABLE audit.checkpoints DROP COLUMN "Channel";
                ALTER TABLE audit.checkpoints RENAME COLUMN "ChannelOld" TO "Channel";
                CREATE INDEX "IX_checkpoints_Channel_SignedAt" ON audit.checkpoints ("Channel", "SignedAt");
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE audit.segments ADD COLUMN "ChannelOld" integer;

                UPDATE audit.segments SET "ChannelOld" = CASE "Channel"
                    WHEN 'AuthnInteractive' THEN 1
                    WHEN 'AuthnSilent' THEN 2
                    WHEN 'AuthnMachine' THEN 3
                    WHEN 'AdminDirectory' THEN 4
                    WHEN 'AdminConfig' THEN 5
                    WHEN 'TokenOps' THEN 6
                    WHEN 'Platform' THEN 7
                END;

                ALTER TABLE audit.segments ALTER COLUMN "ChannelOld" SET NOT NULL;
                DROP INDEX audit."IX_segments_Channel_SequenceEnd";
                ALTER TABLE audit.segments DROP COLUMN "Channel";
                ALTER TABLE audit.segments RENAME COLUMN "ChannelOld" TO "Channel";
                CREATE INDEX "IX_segments_Channel_SequenceEnd" ON audit.segments ("Channel", "SequenceEnd");
                """);
        }
    }
}
