using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Audit.Infrastructure.Migrations
{
    /// <summary>
    /// Search columns on audit.events (EventType, Actor, ActorId, Targets, Outcome) so the event list
    /// can filter on them. New rows get them at write time from the AuditEvent the payload is
    /// serialized from (AuditEventRecordFactory); existing rows are backfilled here from their stored
    /// payload. Rows whose payload retention already purged (I6), and payloads that can't be read,
    /// keep NULLs: there is nothing left to read the values from, and they are never guessed.
    ///
    /// Payload, LeafHash and Sequence are untouched, so segment roots, header chains and checkpoints
    /// verify exactly as before; the search columns are not hashed and play no part in verification.
    /// </summary>
    public partial class AddEventSearchColumns : Migration
    {
        /// <summary>
        /// Fills the search columns of rows that still have a payload and don't have them yet. Reads the
        /// canonical JSON the same way CanonicalAuditEventSerializer writes it (PascalCase names, enums
        /// as ints) and builds the same strings as AuditEventRecordFactory: "platform:{type}:{id}" /
        /// "tenant:{tid}:{type}:{id}" with the lowercased ActorType name, targets as "{type key}:{id}"
        /// in payload order, outcome as the AuditResult name. A payload that isn't readable JSON of that
        /// shape leaves every column NULL instead of failing the migration. Idempotent. Public so a test
        /// can rerun it over an already-sealed chain.
        /// </summary>
        public const string BackfillSql = """
            CREATE FUNCTION audit.events_payload_search_columns(
                payload bytea,
                OUT event_type text, OUT actor text, OUT actor_id text, OUT targets text[], OUT outcome text)
            LANGUAGE plpgsql IMMUTABLE AS $$
            DECLARE
                doc jsonb;
            BEGIN
                doc := convert_from(payload, 'UTF8')::jsonb;

                event_type := doc->>'EventType';
                actor_id := doc->'Actor'->>'Id';
                actor := CASE WHEN (doc->'Actor'->>'IsPlatform')::boolean
                            THEN 'platform'
                            ELSE 'tenant:' || (doc->'Actor'->>'TenantId') END
                    || ':' || CASE (doc->'Actor'->>'Type')::int
                        WHEN 1 THEN 'enduser'
                        WHEN 2 THEN 'adminuser'
                        WHEN 3 THEN 'serviceaccount'
                        WHEN 4 THEN 'oauthclient'
                        WHEN 5 THEN 'system'
                        WHEN 6 THEN 'anonymous'
                        WHEN 7 THEN 'authenticatedunknown'
                    END
                    || ':' || actor_id;
                targets := ARRAY(
                    SELECT (t->>'Type') || ':' || (t->>'Id')
                    FROM jsonb_array_elements(doc->'Targets') WITH ORDINALITY AS x(t, n)
                    ORDER BY n);
                outcome := CASE (doc->'Outcome'->>'Status')::int WHEN 1 THEN 'Success' WHEN 2 THEN 'Failure' END;

                IF event_type IS NULL OR actor IS NULL OR outcome IS NULL THEN
                    event_type := NULL; actor := NULL; actor_id := NULL; targets := NULL; outcome := NULL;
                END IF;
            EXCEPTION WHEN others THEN
                event_type := NULL; actor := NULL; actor_id := NULL; targets := NULL; outcome := NULL;
            END;
            $$;

            UPDATE audit.events AS e
            SET ("EventType", "Actor", "ActorId", "Targets", "Outcome") = (
                SELECT s.event_type, s.actor, s.actor_id, s.targets, s.outcome
                FROM audit.events_payload_search_columns(e."Payload") AS s)
            WHERE e."Payload" IS NOT NULL AND e."EventType" IS NULL;

            DROP FUNCTION audit.events_payload_search_columns(bytea);
            """;

        private const string GuardWithSearchColumns = """
            CREATE OR REPLACE FUNCTION audit.events_append_only_guard() RETURNS trigger AS $$
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

                    IF (OLD."EventType" IS NOT NULL AND NEW."EventType" IS DISTINCT FROM OLD."EventType")
                        OR (OLD."Actor" IS NOT NULL AND NEW."Actor" IS DISTINCT FROM OLD."Actor")
                        OR (OLD."ActorId" IS NOT NULL AND NEW."ActorId" IS DISTINCT FROM OLD."ActorId")
                        OR (OLD."Targets" IS NOT NULL AND NEW."Targets" IS DISTINCT FROM OLD."Targets")
                        OR (OLD."Outcome" IS NOT NULL AND NEW."Outcome" IS DISTINCT FROM OLD."Outcome") THEN
                        RAISE EXCEPTION 'audit.events: search columns (EventType/Actor/ActorId/Targets/Outcome) may only be filled once, never changed';
                    END IF;
                END IF;

                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            """;

        private const string GuardWithoutSearchColumns = """
            CREATE OR REPLACE FUNCTION audit.events_append_only_guard() RETURNS trigger AS $$
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
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Actor",
                schema: "audit",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorId",
                schema: "audit",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                schema: "audit",
                table: "events",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                schema: "audit",
                table: "events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "Targets",
                schema: "audit",
                table: "events",
                type: "text[]",
                nullable: true);

            // Same trigger, plus: a search column may be filled once (NULL -> value, which is what the
            // backfill below and every insert do) and never changed or cleared after that.
            migrationBuilder.Sql(GuardWithSearchColumns);

            migrationBuilder.Sql(BackfillSql);

            migrationBuilder.CreateIndex(
                name: "IX_events_Actor_OccurredAt",
                schema: "audit",
                table: "events",
                columns: new[] { "Actor", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_events_ActorId_OccurredAt",
                schema: "audit",
                table: "events",
                columns: new[] { "ActorId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_events_EventType_OccurredAt",
                schema: "audit",
                table: "events",
                columns: new[] { "EventType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_events_Outcome_OccurredAt",
                schema: "audit",
                table: "events",
                columns: new[] { "Outcome", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_events_Targets",
                schema: "audit",
                table: "events",
                column: "Targets")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(GuardWithoutSearchColumns);

            migrationBuilder.DropIndex(
                name: "IX_events_Actor_OccurredAt",
                schema: "audit",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_ActorId_OccurredAt",
                schema: "audit",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_EventType_OccurredAt",
                schema: "audit",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_Outcome_OccurredAt",
                schema: "audit",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_Targets",
                schema: "audit",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Actor",
                schema: "audit",
                table: "events");

            migrationBuilder.DropColumn(
                name: "ActorId",
                schema: "audit",
                table: "events");

            migrationBuilder.DropColumn(
                name: "EventType",
                schema: "audit",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Outcome",
                schema: "audit",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Targets",
                schema: "audit",
                table: "events");
        }
    }
}
