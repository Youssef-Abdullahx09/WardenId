using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSegmentExportedAtAndAppendOnlyTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExportedAt",
                schema: "audit",
                table: "segments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_segments_ExportedAt",
                schema: "audit",
                table: "segments",
                column: "ExportedAt");

            // Spec §8 append-only enforcement / I6 "never DELETE from audit.events, anywhere" —
            // enforced in the DB itself, independent of which app-level role/connection issues
            // the statement (see docs/audit/OPEN-DECISIONS.md #8 for why real Postgres role
            // separation is a separate, manually-provisioned piece — docs/audit/db-roles.sql —
            // rather than something this auto-run migration creates). A row-level trigger on a
            // partitioned table's parent cascades to every partition automatically, the same
            // way the phase-1 per-partition indexes already do.
            // Two legitimate UPDATE paths exist on this table and both must stay narrowly
            // scoped: the sealer assigns Sequence exactly once, NULL -> a value (I3), and
            // retention nulls Payload exactly once, bytes -> NULL (I6). Everything else —
            // Id/Channel/TenantId/OccurredAt/LeafHash ever changing, Sequence changing once
            // already assigned, Payload being restored or swapped for different bytes — is a
            // tamper attempt and must raise.
            migrationBuilder.Sql("""
                CREATE FUNCTION audit.events_append_only_guard() RETURNS trigger AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'audit.events is append-only: DELETE is not permitted (spec I6)';
                    END IF;

                    IF TG_OP = 'UPDATE' THEN
                        IF NEW."Id" IS DISTINCT FROM OLD."Id"
                            OR NEW."Channel" IS DISTINCT FROM OLD."Channel"
                            OR NEW."TenantId" IS DISTINCT FROM OLD."TenantId"
                            OR NEW."OccurredAt" IS DISTINCT FROM OLD."OccurredAt"
                            OR NEW."LeafHash" IS DISTINCT FROM OLD."LeafHash" THEN
                            RAISE EXCEPTION 'audit.events is append-only: Id/Channel/TenantId/OccurredAt/LeafHash may never change';
                        END IF;

                        IF OLD."Sequence" IS NOT NULL AND NEW."Sequence" IS DISTINCT FROM OLD."Sequence" THEN
                            RAISE EXCEPTION 'audit.events: Sequence may only be assigned once (I3), never reassigned';
                        END IF;

                        IF OLD."Payload" IS NULL AND NEW."Payload" IS DISTINCT FROM OLD."Payload" THEN
                            RAISE EXCEPTION 'audit.events: Payload already purged, it cannot be restored';
                        END IF;

                        IF NEW."Payload" IS DISTINCT FROM OLD."Payload" AND NEW."Payload" IS NOT NULL THEN
                            RAISE EXCEPTION 'audit.events: Payload may only ever be nulled (I6), never replaced';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER events_append_only_guard
                    BEFORE UPDATE OR DELETE ON audit.events
                    FOR EACH ROW EXECUTE FUNCTION audit.events_append_only_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TRIGGER events_append_only_guard ON audit.events;""");
            migrationBuilder.Sql("""DROP FUNCTION audit.events_append_only_guard();""");

            migrationBuilder.DropIndex(
                name: "IX_segments_ExportedAt",
                schema: "audit",
                table: "segments");

            migrationBuilder.DropColumn(
                name: "ExportedAt",
                schema: "audit",
                table: "segments");
        }
    }
}
