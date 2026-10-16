using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Tenancy.Infrastructure.Persistence
{
    /// <inheritdoc />
    public partial class AddSigningKeyLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantSigningKeys_TenantId_ActiveUnique",
                schema: "Tenancy",
                table: "SigningKeys");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActivatedAt",
                schema: "Tenancy",
                table: "SigningKeys",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetiredAt",
                schema: "Tenancy",
                table: "SigningKeys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetiringSince",
                schema: "Tenancy",
                table: "SigningKeys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "Tenancy",
                table: "SigningKeys",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Existing keys: IsActive = true -> Active (activated when created). Any other key never
            // signed after the IsActive backfill (BackfillActiveSigningKeys), so it is Retired, not
            // published. Done before IsActive is dropped.
            migrationBuilder.Sql(
                "UPDATE \"Tenancy\".\"SigningKeys\" SET " +
                "\"ActivatedAt\" = \"CreatedAt\", " +
                "\"Status\" = CASE WHEN \"IsActive\" THEN 'Active' ELSE 'Retired' END, " +
                "\"RetiredAt\" = CASE WHEN \"IsActive\" THEN NULL ELSE now() END;");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "Tenancy",
                table: "SigningKeys");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSigningKeys_TenantId_ActiveUnique",
                schema: "Tenancy",
                table: "SigningKeys",
                column: "TenantId",
                unique: true,
                filter: "\"Status\" = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantSigningKeys_TenantId_ActiveUnique",
                schema: "Tenancy",
                table: "SigningKeys");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "Tenancy",
                table: "SigningKeys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE \"Tenancy\".\"SigningKeys\" SET \"IsActive\" = (\"Status\" = 'Active');");

            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                schema: "Tenancy",
                table: "SigningKeys");

            migrationBuilder.DropColumn(
                name: "RetiredAt",
                schema: "Tenancy",
                table: "SigningKeys");

            migrationBuilder.DropColumn(
                name: "RetiringSince",
                schema: "Tenancy",
                table: "SigningKeys");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "Tenancy",
                table: "SigningKeys");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSigningKeys_TenantId_ActiveUnique",
                schema: "Tenancy",
                table: "SigningKeys",
                column: "TenantId",
                unique: true,
                filter: "\"IsActive\" = true");
        }
    }
}
