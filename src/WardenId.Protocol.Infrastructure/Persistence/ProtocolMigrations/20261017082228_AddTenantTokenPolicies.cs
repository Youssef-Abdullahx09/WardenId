using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardenId.Protocol.Infrastructure.Persistence.ProtocolMigrations
{
    /// <inheritdoc />
    public partial class AddTenantTokenPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantTokenPolicies",
                schema: "Protocol",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessTokenLifetime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    IdTokenLifetime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    RefreshTokenLifetime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    RefreshTokenAbsoluteLifetime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    AuthorizationCodeLifetime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    SessionCookieLifetime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    SessionAbsoluteLifetime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModificationById = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantTokenPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantTokenPolicies_IsDeleted",
                schema: "Protocol",
                table: "TenantTokenPolicies",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_TenantTokenPolicies_TenantId",
                schema: "Protocol",
                table: "TenantTokenPolicies",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantTokenPolicies",
                schema: "Protocol");
        }
    }
}
