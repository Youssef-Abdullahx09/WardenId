using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Protocol.Infrastructure.Persistence.ProtocolMigrations
{
    /// <inheritdoc />
    public partial class DropEntityAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantTokenPolicies_IsDeleted",
                schema: "Protocol",
                table: "TenantTokenPolicies");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_IsDeleted",
                schema: "Protocol",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Protocol",
                table: "TenantTokenPolicies");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                schema: "Protocol",
                table: "TenantTokenPolicies");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Protocol",
                table: "TenantTokenPolicies");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "Protocol",
                table: "TenantTokenPolicies");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Protocol",
                table: "TenantTokenPolicies");

            migrationBuilder.DropColumn(
                name: "LastModificationAt",
                schema: "Protocol",
                table: "TenantTokenPolicies");

            migrationBuilder.DropColumn(
                name: "LastModificationById",
                schema: "Protocol",
                table: "TenantTokenPolicies");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Protocol",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                schema: "Protocol",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Protocol",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "Protocol",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Protocol",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "LastModificationAt",
                schema: "Protocol",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "LastModificationById",
                schema: "Protocol",
                table: "Sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "Protocol",
                table: "TenantTokenPolicies",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "Protocol",
                table: "TenantTokenPolicies",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Protocol",
                table: "TenantTokenPolicies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "Protocol",
                table: "TenantTokenPolicies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Protocol",
                table: "TenantTokenPolicies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModificationAt",
                schema: "Protocol",
                table: "TenantTokenPolicies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModificationById",
                schema: "Protocol",
                table: "TenantTokenPolicies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "Protocol",
                table: "Sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "Protocol",
                table: "Sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Protocol",
                table: "Sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "Protocol",
                table: "Sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Protocol",
                table: "Sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModificationAt",
                schema: "Protocol",
                table: "Sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModificationById",
                schema: "Protocol",
                table: "Sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantTokenPolicies_IsDeleted",
                schema: "Protocol",
                table: "TenantTokenPolicies",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_IsDeleted",
                schema: "Protocol",
                table: "Sessions",
                column: "IsDeleted");
        }
    }
}
