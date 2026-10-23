using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.ClientManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropEntityAuditColumns : Migration
    {
        private static readonly string[] ClientChildTables =
        [
            SchemaConsts.ClientSecretsTable,
            SchemaConsts.ClientSigningKeysTable
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Before ClientId turns NOT NULL and IsDeleted goes: a deleted client's secrets and signing keys
            // have no further use (Client.Delete now drops them), and neither have soft-deleted or orphaned ones.
            foreach (var child in ClientChildTables)
                migrationBuilder.Sql($$"""
                    DELETE FROM "{{ClientManagementDbContext.Schema}}"."{{child}}" c
                    WHERE c."IsDeleted"
                       OR c."{{SchemaConsts.ClientIdForeignKey}}" IS NULL
                       OR EXISTS (
                          SELECT 1 FROM "{{ClientManagementDbContext.Schema}}"."{{SchemaConsts.ClientsTable}}" cl
                          WHERE cl."Id" = c."{{SchemaConsts.ClientIdForeignKey}}" AND cl."IsDeleted");
                    """);

            migrationBuilder.DropIndex(
                name: "IX_ClientSigningKeys_IsDeleted",
                schema: "ClientManagement",
                table: "ClientSigningKeys");

            migrationBuilder.DropIndex(
                name: "IX_ClientSecrets_IsDeleted",
                schema: "ClientManagement",
                table: "ClientSecrets");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "ClientManagement",
                table: "ClientSigningKeys");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                schema: "ClientManagement",
                table: "ClientSigningKeys");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "ClientManagement",
                table: "ClientSigningKeys");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "ClientManagement",
                table: "ClientSigningKeys");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "ClientManagement",
                table: "ClientSigningKeys");

            migrationBuilder.DropColumn(
                name: "LastModificationAt",
                schema: "ClientManagement",
                table: "ClientSigningKeys");

            migrationBuilder.DropColumn(
                name: "LastModificationById",
                schema: "ClientManagement",
                table: "ClientSigningKeys");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "ClientManagement",
                table: "ClientSecrets");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                schema: "ClientManagement",
                table: "ClientSecrets");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "ClientManagement",
                table: "ClientSecrets");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "ClientManagement",
                table: "ClientSecrets");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "ClientManagement",
                table: "ClientSecrets");

            migrationBuilder.DropColumn(
                name: "LastModificationAt",
                schema: "ClientManagement",
                table: "ClientSecrets");

            migrationBuilder.DropColumn(
                name: "LastModificationById",
                schema: "ClientManagement",
                table: "ClientSecrets");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                schema: "ClientManagement",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "ClientManagement",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LastModificationAt",
                schema: "ClientManagement",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LastModificationById",
                schema: "ClientManagement",
                table: "Clients");

            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                schema: "ClientManagement",
                table: "ClientSigningKeys",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                schema: "ClientManagement",
                table: "ClientSecrets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-adds the columns only: the secret and signing key rows Up deleted are not restored.
            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                schema: "ClientManagement",
                table: "ClientSigningKeys",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "ClientManagement",
                table: "ClientSigningKeys",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "ClientManagement",
                table: "ClientSigningKeys",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "ClientManagement",
                table: "ClientSigningKeys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "ClientManagement",
                table: "ClientSigningKeys",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "ClientManagement",
                table: "ClientSigningKeys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModificationAt",
                schema: "ClientManagement",
                table: "ClientSigningKeys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModificationById",
                schema: "ClientManagement",
                table: "ClientSigningKeys",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                schema: "ClientManagement",
                table: "ClientSecrets",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "ClientManagement",
                table: "ClientSecrets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "ClientManagement",
                table: "ClientSecrets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "ClientManagement",
                table: "ClientSecrets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "ClientManagement",
                table: "ClientSecrets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "ClientManagement",
                table: "ClientSecrets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModificationAt",
                schema: "ClientManagement",
                table: "ClientSecrets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModificationById",
                schema: "ClientManagement",
                table: "ClientSecrets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "ClientManagement",
                table: "Clients",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "ClientManagement",
                table: "Clients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModificationAt",
                schema: "ClientManagement",
                table: "Clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModificationById",
                schema: "ClientManagement",
                table: "Clients",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientSigningKeys_IsDeleted",
                schema: "ClientManagement",
                table: "ClientSigningKeys",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ClientSecrets_IsDeleted",
                schema: "ClientManagement",
                table: "ClientSecrets",
                column: "IsDeleted");
        }
    }
}
