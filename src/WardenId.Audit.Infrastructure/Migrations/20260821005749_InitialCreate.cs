using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            // Postgres declarative partitioning has no EF Core migration-builder API, so this
            // is hand-written raw SQL rather than migrationBuilder.CreateTable(...). Partitioned
            // by Channel (spec §3.3): a flood on AuthnMachine must not degrade queries against
            // AdminConfig, and Platform's own partition keeps it physically separate from
            // tenant-scoped storage even though every channel shares one logical table/schema.
            // Any unique constraint — hence the primary key — on a partitioned table must
            // include the partition key column, which is why the PK is (Id, Channel) rather
            // than Id alone; this doesn't weaken uniqueness since Id is already a UUID.
            migrationBuilder.Sql("""
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
                """);

            // Values match WardenId.Audit.Domain.AuditChannel's explicit int assignments —
            // AuthnInteractive=1 .. Platform=7. A DEFAULT partition catches anything else so an
            // unmapped or future channel value fails loudly at query time rather than silently
            // dropping the insert.
            migrationBuilder.Sql(
                """CREATE TABLE audit.events_authn_interactive PARTITION OF audit.events FOR VALUES IN (1);""");
            migrationBuilder.Sql(
                """CREATE TABLE audit.events_authn_silent PARTITION OF audit.events FOR VALUES IN (2);""");
            migrationBuilder.Sql(
                """CREATE TABLE audit.events_authn_machine PARTITION OF audit.events FOR VALUES IN (3);""");
            migrationBuilder.Sql(
                """CREATE TABLE audit.events_admin_directory PARTITION OF audit.events FOR VALUES IN (4);""");
            migrationBuilder.Sql(
                """CREATE TABLE audit.events_admin_config PARTITION OF audit.events FOR VALUES IN (5);""");
            migrationBuilder.Sql(
                """CREATE TABLE audit.events_token_ops PARTITION OF audit.events FOR VALUES IN (6);""");
            migrationBuilder.Sql(
                """CREATE TABLE audit.events_platform PARTITION OF audit.events FOR VALUES IN (7);""");
            migrationBuilder.Sql(
                """CREATE TABLE audit.events_default PARTITION OF audit.events DEFAULT;""");

            // Created on the parent — Postgres propagates a matching index to every existing
            // and future partition automatically.
            migrationBuilder.Sql(
                """CREATE INDEX "IX_events_Channel_Sequence" ON audit.events ("Channel", "Sequence");""");
            migrationBuilder.Sql(
                """CREATE INDEX "IX_events_TenantId_OccurredAt" ON audit.events ("TenantId", "OccurredAt");""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping a partitioned parent table drops all of its partitions with it — no
            // need to enumerate or CASCADE.
            migrationBuilder.Sql("""DROP TABLE audit.events;""");
        }
    }
}
