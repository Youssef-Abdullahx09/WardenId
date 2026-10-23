using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Tenancy.Infrastructure.Persistence
{
    /// <inheritdoc />
    public partial class DropEntityAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_IsDeleted",
                schema: "Tenancy",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_SigningKeys_IsDeleted",
                schema: "Tenancy",
                table: "SigningKeys");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                schema: "Tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "Tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LastModificationAt",
                schema: "Tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LastModificationById",
                schema: "Tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                schema: "Tenancy",
                table: "SigningKeys");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Tenancy",
                table: "SigningKeys");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "Tenancy",
                table: "SigningKeys");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Tenancy",
                table: "SigningKeys");

            migrationBuilder.DropColumn(
                name: "LastModificationAt",
                schema: "Tenancy",
                table: "SigningKeys");

            migrationBuilder.DropColumn(
                name: "LastModificationById",
                schema: "Tenancy",
                table: "SigningKeys");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "Tenancy",
                table: "Tenants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Tenancy",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "Tenancy",
                table: "Tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Tenancy",
                table: "Tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModificationAt",
                schema: "Tenancy",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModificationById",
                schema: "Tenancy",
                table: "Tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "Tenancy",
                table: "SigningKeys",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Tenancy",
                table: "SigningKeys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "Tenancy",
                table: "SigningKeys",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Tenancy",
                table: "SigningKeys",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModificationAt",
                schema: "Tenancy",
                table: "SigningKeys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModificationById",
                schema: "Tenancy",
                table: "SigningKeys",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_IsDeleted",
                schema: "Tenancy",
                table: "Tenants",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_SigningKeys_IsDeleted",
                schema: "Tenancy",
                table: "SigningKeys",
                column: "IsDeleted");
        }
    }
}
